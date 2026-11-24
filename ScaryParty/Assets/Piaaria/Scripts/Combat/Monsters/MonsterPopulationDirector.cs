using System.Collections.Generic;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace ScaryParty.Combat.Monsters
{
    /// <summary>
    /// Diretor servidor-autoritativo de população noturna de monstros (M5):
    /// cria vampiros (em telhados quando disponíveis), lobisomens, fantasmas e zumbis,
    /// replica o estado em PizzeriaNetworkState.Monsters e limpa ao encerrar/resetar a noite.
    /// </summary>
    public class MonsterPopulationDirector : MonoBehaviour
    {
        public static MonsterPopulationDirector Instance { get; private set; }

        public static MonsterPopulationDirector EnsureOn(PizzeriaRoot root)
        {
            if (root == null) return null;
            var existing = root.GetComponent<MonsterPopulationDirector>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }
            Instance = root.gameObject.AddComponent<MonsterPopulationDirector>();
            return Instance;
        }

        private readonly List<MonsterNpcController> _activeMonsters = new List<MonsterNpcController>();
        private readonly Dictionary<ulong, MonsterNpcController> _clientProxies = new Dictionary<ulong, MonsterNpcController>();
        private readonly List<NetMonsterDto> _clientSyncBuffer = new List<NetMonsterDto>();
        private readonly HashSet<ulong> _reusableAliveIds = new HashSet<ulong>();
        private readonly HashSet<ulong> _reusableActiveIds = new HashSet<ulong>();
        private readonly List<ulong> _reusableToRemove = new List<ulong>();
        private readonly Dictionary<ulong, int> _reusableNetIndexById = new Dictionary<ulong, int>();
        private PizzeriaNetworkState _subscribedNetState;
        private int _spawnedForNight = -1;
        private ulong _nextMonsterId = 1;
        private float _syncTimer;
        private bool _isShuttingDown;

        public IReadOnlyList<MonsterNpcController> ActiveMonsters => _activeMonsters;
        public IReadOnlyDictionary<ulong, MonsterNpcController> ClientProxies => _clientProxies;
        public int ClientProxyCount => _clientProxies.Count;
        public static bool ShowTechnicalSpeciesOverlay { get; set; }

        public MonsterNpcController SpawnDebugMonsterServer(PizzeriaRoot root, NpcSpecies species)
        {
            if (root == null || !root.IsServer) return null;
            var buildings = FindObjectsByType<CityBuilding>(FindObjectsSortMode.None);
            bool perched = species == NpcSpecies.Vampire && buildings != null && buildings.Length > 0;
            Vector3 pos = ResolveRooftopOrStreetSpawn(buildings, root.transform.position, _activeMonsters.Count + UnityEngine.Random.Range(1, 25), perched);
            var ctrl = SpawnMonsterServer(root, species, pos, perched);
            ReplicateMonstersServer();
            return ctrl;
        }

        private static void DestroyObjectSafe(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }

        private void Awake()
        {
            Instance = this;
            _isShuttingDown = false;
        }

        private void OnEnable()
        {
            _isShuttingDown = false;
            PizzeriaNetworkState.OnNetworkStateSpawned += HandleNetworkStateSpawned;
            PizzeriaNetworkState.OnNetworkStateDespawning += HandleNetworkStateDespawning;
        }

        private void OnDisable()
        {
            PizzeriaNetworkState.OnNetworkStateSpawned -= HandleNetworkStateSpawned;
            PizzeriaNetworkState.OnNetworkStateDespawning -= HandleNetworkStateDespawning;
            UnsubscribeClientMonsters();
        }

        private void OnDestroy()
        {
            ClearOnSessionEnd();
            if (Instance == this) Instance = null;
        }

        public void ClearOnSessionEnd()
        {
            _isShuttingDown = true;
            UnsubscribeClientMonsters();
            for (int i = _activeMonsters.Count - 1; i >= 0; i--)
            {
                if (_activeMonsters[i] != null)
                    DestroyObjectSafe(_activeMonsters[i].gameObject);
            }
            _activeMonsters.Clear();
            _spawnedForNight = -1;
            ClearAllClientProxies();
        }

        private void HandleNetworkStateSpawned(PizzeriaNetworkState ns)
        {
            if (_isShuttingDown || ns == null || !ns.IsSpawned) return;
            var root = PizzeriaRoot.Instance;
            if (root != null && root.IsSpawned && !root.IsServer)
            {
                EnsureClientSubscribed();
            }
        }

        private void HandleNetworkStateDespawning(PizzeriaNetworkState ns)
        {
            if (ns == _subscribedNetState)
            {
                UnsubscribeClientMonsters();
                ClearAllClientProxies();
            }
        }

        private void Update()
        {
            if (_isShuttingDown) return;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsSpawned) return;

            if (!root.IsServer)
            {
                EnsureClientSubscribed();
                return;
            }

            if (root.DomainState == null) return;

            var phase = root.DomainState.CurrentNightPhase;
            int night = root.DomainState.CurrentNightNumber;

            if (phase == NightPhase.Active && night >= 1 && _spawnedForNight != night)
            {
                SpawnPopulationForNightServer(root, night);
            }
            else if (phase == NightPhase.Results || phase == NightPhase.PreparingNextNight || phase == NightPhase.Defeat)
            {
                if (_activeMonsters.Count > 0 || (PizzeriaNetworkState.Instance != null && PizzeriaNetworkState.Instance.Monsters != null && PizzeriaNetworkState.Instance.Monsters.Count > 0))
                {
                    ClearNightMonstersServer();
                }
            }

            _syncTimer += Time.deltaTime;
            if (_syncTimer >= 0.08f)
            {
                _syncTimer = 0f;
                ReplicateMonstersServer();
            }
        }

        public void ClearNightMonstersServer()
        {
            for (int i = _activeMonsters.Count - 1; i >= 0; i--)
            {
                if (_activeMonsters[i] != null)
                    DestroyObjectSafe(_activeMonsters[i].gameObject);
            }
            _activeMonsters.Clear();
            _spawnedForNight = -1;

            var ns = PizzeriaNetworkState.Instance;
            if (ns != null && ns.IsSpawned && ns.Monsters != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                ns.Monsters.Clear();
            }
        }

        public void ClearAllClientProxies()
        {
            foreach (var kvp in _clientProxies)
            {
                if (kvp.Value != null) DestroyObjectSafe(kvp.Value.gameObject);
            }
            _clientProxies.Clear();
        }

        private void EnsureClientSubscribed()
        {
            if (_isShuttingDown) return;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsSpawned || root.IsServer) return;

            var ns = PizzeriaNetworkState.Instance;
            if (ns == null || !ns.IsSpawned || ns.Monsters == null) return;
            if (_subscribedNetState == ns) return;

            UnsubscribeClientMonsters();
            _subscribedNetState = ns;
            _subscribedNetState.Monsters.OnListChanged += OnClientMonstersListChanged;
            SyncClientProxiesFromNetworkList();
        }

        private void UnsubscribeClientMonsters()
        {
            if (_subscribedNetState != null && _subscribedNetState.Monsters != null)
            {
                _subscribedNetState.Monsters.OnListChanged -= OnClientMonstersListChanged;
            }
            _subscribedNetState = null;
        }

        private void OnClientMonstersListChanged(NetworkListEvent<NetMonsterDto> changeEvent)
        {
            if (_isShuttingDown) return;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsSpawned || root.IsServer) return;
            SyncClientProxiesFromNetworkList();
        }

        public void SyncClientProxiesFromNetworkList()
        {
            if (_isShuttingDown) return;
            var root = PizzeriaRoot.Instance;
            if (root != null && root.IsServer)
            {
                // Nunca duplicar corpos no Host: o servidor já possui _activeMonsters.
                ClearAllClientProxies();
                return;
            }

            var ns = PizzeriaNetworkState.Instance;
            if (ns == null || !ns.IsSpawned || ns.Monsters == null)
            {
                ClearAllClientProxies();
                return;
            }

            _clientSyncBuffer.Clear();
            for (int i = 0; i < ns.Monsters.Count; i++)
            {
                _clientSyncBuffer.Add(ns.Monsters[i]);
            }
            SyncClientProxiesFromSnapshot(_clientSyncBuffer);
        }

        /// <summary>
        /// Sincroniza proxies client-side a partir de um snapshot replicado (NetworkList ou teste unitário).
        /// </summary>
        public void SyncClientProxiesFromSnapshot(IReadOnlyList<NetMonsterDto> snapshot)
        {
            if (_isShuttingDown) return;
            var root = PizzeriaRoot.Instance;
            if (root != null && root.IsServer)
            {
                ClearAllClientProxies();
                return;
            }

            if (snapshot == null || snapshot.Count == 0)
            {
                ClearAllClientProxies();
                return;
            }

            _reusableAliveIds.Clear();
            for (int i = 0; i < snapshot.Count; i++)
            {
                var dto = snapshot[i];
                _reusableAliveIds.Add(dto.MonsterId);
                if (_clientProxies.TryGetValue(dto.MonsterId, out var proxy) && proxy != null)
                {
                    proxy.ApplyReplicatedDto(dto);
                }
                else
                {
                    _clientProxies[dto.MonsterId] = SpawnMonsterClientProxy(dto);
                }
            }

            _reusableToRemove.Clear();
            foreach (var kvp in _clientProxies)
            {
                if (kvp.Value == null || !_reusableAliveIds.Contains(kvp.Key))
                    _reusableToRemove.Add(kvp.Key);
            }
            for (int i = 0; i < _reusableToRemove.Count; i++)
            {
                if (_clientProxies.TryGetValue(_reusableToRemove[i], out var p) && p != null)
                    DestroyObjectSafe(p.gameObject);
                _clientProxies.Remove(_reusableToRemove[i]);
            }
        }

        public int CountActiveZombies()
        {
            int count = 0;
            for (int i = 0; i < _activeMonsters.Count; i++)
            {
                var m = _activeMonsters[i];
                if (m != null && m.IsAlive && m.Species == NpcSpecies.Zombie)
                    count++;
            }
            return count;
        }

        public MonsterNpcController SpawnConvertedZombieServer(Vector3 worldPos)
        {
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer) return null;
            return SpawnMonsterServer(root, NpcSpecies.Zombie, worldPos, perchedOnRooftop: false);
        }

        public static int ComputeVampireSpawnCount(int nightNumber, float spawnMultiplier)
        {
            return Mathf.Max(1, Mathf.RoundToInt((1 + Mathf.Max(0, nightNumber)) * Mathf.Max(0.1f, spawnMultiplier)));
        }

        private void SpawnPopulationForNightServer(PizzeriaRoot root, int night)
        {
            ClearNightMonstersServer();
            _spawnedForNight = night;

            var stats = root.RunProgressionService != null
                ? root.RunProgressionService.RecalculateEffectiveStats()
                : new RunEffectiveStats();

            int vampireCount = ComputeVampireSpawnCount(night, stats.VampireSpawnMultiplier);
            int werewolfCount = night >= 1 ? 1 : 0;
            int ghostCount = night >= 2 ? 1 : 0;
            int zombieCount = 1 + (night >= 2 ? 1 : 0);

            var buildings = FindObjectsByType<CityBuilding>(FindObjectsSortMode.None);
            Vector3 pizzeriaPos = root.transform.position;

            for (int i = 0; i < vampireCount; i++)
            {
                Vector3 pos = ResolveRooftopOrStreetSpawn(buildings, pizzeriaPos, i, rooftop: true);
                SpawnMonsterServer(root, NpcSpecies.Vampire, pos, perchedOnRooftop: buildings != null && buildings.Length > 0);
            }
            for (int i = 0; i < werewolfCount; i++)
            {
                Vector3 pos = ResolveRooftopOrStreetSpawn(buildings, pizzeriaPos, 10 + i, rooftop: false);
                SpawnMonsterServer(root, NpcSpecies.Werewolf, pos, perchedOnRooftop: false);
            }
            for (int i = 0; i < ghostCount; i++)
            {
                Vector3 pos = ResolveRooftopOrStreetSpawn(buildings, pizzeriaPos, 20 + i, rooftop: false);
                SpawnMonsterServer(root, NpcSpecies.Ghost, pos, perchedOnRooftop: false);
            }
            for (int i = 0; i < zombieCount; i++)
            {
                Vector3 pos = ResolveRooftopOrStreetSpawn(buildings, pizzeriaPos, 30 + i, rooftop: false);
                SpawnMonsterServer(root, NpcSpecies.Zombie, pos, perchedOnRooftop: false);
            }

            ReplicateMonstersServer();
        }

        private MonsterNpcController SpawnMonsterServer(PizzeriaRoot root, NpcSpecies species, Vector3 pos, bool perchedOnRooftop)
        {
            ulong monsterId = _nextMonsterId++;
            var go = new GameObject($"Monster_{species}_{monsterId}");
            go.transform.position = pos;

            var visualCatalog = PlaytestReferenceManifest.LoadDefault()?.monsterVisualCatalog as MonsterVisualCatalog;
            MonsterSpeciesVisualEntry visualEntry = null;
            visualCatalog?.TryGetEntry(species, out visualEntry);

            var capsule = go.AddComponent<CapsuleCollider>();
            float colH = visualEntry != null && visualEntry.colliderHeight > 0.2f ? visualEntry.colliderHeight : 1.8f;
            float colR = visualEntry != null && visualEntry.colliderRadius > 0.1f ? visualEntry.colliderRadius : 0.38f;
            capsule.center = new Vector3(0f, colH * 0.5f, 0f);
            capsule.height = colH;
            capsule.radius = colR;

            BuildMonsterVisual(go, species, visualEntry, out var eyeRenderers);

            var agent = go.AddComponent<NavMeshAgent>();
            agent.speed = species == NpcSpecies.Werewolf ? 4.8f : (species == NpcSpecies.Zombie ? 2.4f : 3.6f);
            agent.radius = colR;
            agent.height = colH;

            var ctrl = go.AddComponent<MonsterNpcController>();
            var profile = root.SpeciesHostilityService != null
                ? root.SpeciesHostilityService.GetProfile(species)
                : new SpeciesCapabilityProfile { Species = species };
            ctrl.ConfigureServer(monsterId, species, profile, perchedOnRooftop, eyeRenderers);
            _activeMonsters.Add(ctrl);
            return ctrl;
        }

        private MonsterNpcController SpawnMonsterClientProxy(NetMonsterDto dto)
        {
            var species = (NpcSpecies)dto.Species;
            var go = new GameObject($"MonsterProxy_{species}_{dto.MonsterId}");
            go.transform.position = new Vector3(dto.PosX, dto.PosY, dto.PosZ);

            var visualCatalog = PlaytestReferenceManifest.LoadDefault()?.monsterVisualCatalog as MonsterVisualCatalog;
            MonsterSpeciesVisualEntry visualEntry = null;
            visualCatalog?.TryGetEntry(species, out visualEntry);

            var capsule = go.AddComponent<CapsuleCollider>();
            float colH = visualEntry != null && visualEntry.colliderHeight > 0.2f ? visualEntry.colliderHeight : 1.8f;
            float colR = visualEntry != null && visualEntry.colliderRadius > 0.1f ? visualEntry.colliderRadius : 0.38f;
            capsule.center = new Vector3(0f, colH * 0.5f, 0f);
            capsule.height = colH;
            capsule.radius = colR;

            BuildMonsterVisual(go, species, visualEntry, out var eyeRenderers);

            var ctrl = go.AddComponent<MonsterNpcController>();
            ctrl.ConfigureClientProxy(dto.MonsterId, species, eyeRenderers, dto);
            return ctrl;
        }

        private static void BuildMonsterVisual(
            GameObject go,
            NpcSpecies species,
            MonsterSpeciesVisualEntry visualEntry,
            out Renderer[] eyeRenderers)
        {
            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(go.transform, false);
            visualRoot.transform.localPosition = Vector3.zero;
            visualRoot.transform.localRotation = Quaternion.identity;

            var rig = go.GetComponent<EntityVisualAnchorRig>() ?? go.AddComponent<EntityVisualAnchorRig>();
            rig.rigRole = EntityVisualAnchorRig.RigRole.Monster;
            rig.placeholderId = $"monster_{species}";

            Color tint = visualEntry != null
                ? visualEntry.baseTint
                : species switch
                {
                    NpcSpecies.Vampire => new Color(0.25f, 0.08f, 0.14f),
                    NpcSpecies.Werewolf => new Color(0.32f, 0.24f, 0.18f),
                    NpcSpecies.Ghost => new Color(0.72f, 0.88f, 0.95f),
                    NpcSpecies.Zombie => new Color(0.26f, 0.48f, 0.24f),
                    _ => Color.gray
                };

            GameObject eyeObj = null;
            if (visualEntry != null && visualEntry.visualPrefab != null)
            {
                var inst = Instantiate(visualEntry.visualPrefab, visualRoot.transform, false);
                inst.name = "MonsterModel";
                inst.transform.localPosition = new Vector3(0f, visualEntry.visualYOffset, 0f);
                inst.transform.localRotation = Quaternion.identity;
                if (visualEntry.visualLocalScale.sqrMagnitude > 0.001f)
                    inst.transform.localScale = visualEntry.visualLocalScale;
                var cols = inst.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < cols.Length; i++) DestroyObjectSafe(cols[i]);
                Transform foundEyes = inst.transform.Find("Eyes");
                if (foundEyes != null) eyeObj = foundEyes.gameObject;
            }
            else
            {
                GameObject visualPrefab = ScaryParty.VisualStyles.VisualStyleManager.Active?.activeLibrary?.styledCharacterVariantPrefab;
                if (visualPrefab == null) visualPrefab = Resources.Load<GameObject>("CharacterVisual");
                if (visualPrefab != null)
                {
                    var inst = Instantiate(visualPrefab, visualRoot.transform, false);
                    inst.name = "CharacterVisual";
                    inst.transform.localPosition = Vector3.zero;
                    inst.transform.localRotation = Quaternion.identity;
                    var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    if (smr != null)
                    {
                        var mpb = new MaterialPropertyBlock();
                        smr.GetPropertyBlock(mpb);
                        mpb.SetColor("_Color", tint);
                        mpb.SetColor("_BaseColor", tint);
                        smr.SetPropertyBlock(mpb);
                    }
                }
                else
                {
                    var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    body.name = "BodyVisual";
                    DestroyObjectSafe(body.GetComponent<Collider>());
                    body.transform.SetParent(visualRoot.transform, false);
                    body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                    body.transform.localScale = new Vector3(0.75f, 0.9f, 0.75f);
                    var bodyRend = body.GetComponent<Renderer>();
                    if (bodyRend != null)
                    {
                        ApplyUrpMaterialTint(bodyRend, tint);
                    }
                }
            }

            if (eyeObj == null)
            {
                eyeObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                eyeObj.name = "Eyes";
                DestroyObjectSafe(eyeObj.GetComponent<Collider>());
                eyeObj.transform.SetParent(visualRoot.transform, false);
                eyeObj.transform.localPosition = new Vector3(0f, 1.52f, 0.34f);
                eyeObj.transform.localScale = new Vector3(0.42f, 0.10f, 0.12f);
                var eRend = eyeObj.GetComponent<Renderer>();
                if (eRend != null) ApplyUrpMaterialTint(eRend, Color.white);
            }

            eyeRenderers = eyeObj != null ? eyeObj.GetComponentsInChildren<Renderer>(true) : System.Array.Empty<Renderer>();
        }

        private static void ApplyUrpMaterialTint(Renderer rend, Color color)
        {
            if (rend == null) return;
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (urpLit != null && (rend.sharedMaterial == null || rend.sharedMaterial.shader.name == "Standard"))
            {
                var m = new Material(urpLit);
                m.color = color;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
                rend.sharedMaterial = m;
            }
            var mpb = new MaterialPropertyBlock();
            rend.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", color);
            mpb.SetColor("_BaseColor", color);
            rend.SetPropertyBlock(mpb);
        }

        private static Vector3 ResolveRooftopOrStreetSpawn(CityBuilding[] buildings, Vector3 pizzeriaPos, int salt, bool rooftop)
        {
            if (buildings != null && buildings.Length > 0)
            {
                for (int tries = 0; tries < buildings.Length; tries++)
                {
                    var b = buildings[(salt + tries * 3) % buildings.Length];
                    if (b == null) continue;
                    if (!string.IsNullOrEmpty(b.buildingId) &&
                        b.buildingId.IndexOf("pizzaria", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    Vector3 center = b.transform.position;
                    if ((center - pizzeriaPos).sqrMagnitude < 20f * 20f) continue;

                    if (rooftop)
                    {
                        if (b.rooftopPerchAnchor != null)
                            return b.rooftopPerchAnchor.position;
                        float roofY = center.y + Mathf.Max(4.5f, b.floorCount * 3.5f);
                        return new Vector3(center.x, roofY, center.z);
                    }
                    Vector3 streetTarget = b.rooftopDropAnchor != null
                        ? b.rooftopDropAnchor.position
                        : (b.sidewalkApproachAnchor != null ? b.sidewalkApproachAnchor.position : b.entrancePosition);
                    if (NavMesh.SamplePosition(streetTarget, out var navHit, 5f, NavMesh.AllAreas))
                        return navHit.position;
                }
            }

            float angle = (salt * 47f) * Mathf.Deg2Rad;
            Vector3 candidate = pizzeriaPos + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 24f;
            if (NavMesh.SamplePosition(candidate, out var fallbackHit, 10f, NavMesh.AllAreas))
                return fallbackHit.position;
            return candidate;
        }

        private void ReplicateMonstersServer()
        {
            var ns = PizzeriaNetworkState.Instance;
            if (ns == null || ns.Monsters == null) return;

            _reusableActiveIds.Clear();
            _reusableNetIndexById.Clear();
            for (int j = 0; j < ns.Monsters.Count; j++)
            {
                _reusableNetIndexById[ns.Monsters[j].MonsterId] = j;
            }

            for (int i = 0; i < _activeMonsters.Count; i++)
            {
                var m = _activeMonsters[i];
                if (m == null) continue;
                _reusableActiveIds.Add(m.MonsterId);

                var dto = new NetMonsterDto
                {
                    MonsterId = m.MonsterId,
                    Species = (byte)m.Species,
                    BehaviorState = (byte)m.CurrentBehaviorState,
                    TelegraphKind = m.CurrentTelegraphKind,
                    CurrentHp = m.CurrentHealth,
                    MaxHp = m.MaxHealth,
                    PosX = m.transform.position.x,
                    PosY = m.transform.position.y,
                    PosZ = m.transform.position.z,
                    Yaw = m.transform.eulerAngles.y,
                    IsAlerted = m.CurrentBehaviorState == NpcBehaviorState.Aggressive || m.CurrentBehaviorState == NpcBehaviorState.Attacking,
                    Revision = m.Revision
                };

                if (_reusableNetIndexById.TryGetValue(dto.MonsterId, out int idx))
                {
                    if (!ns.Monsters[idx].Equals(dto)) ns.Monsters[idx] = dto;
                }
                else
                {
                    ns.Monsters.Add(dto);
                }
            }

            for (int j = ns.Monsters.Count - 1; j >= 0; j--)
            {
                if (!_reusableActiveIds.Contains(ns.Monsters[j].MonsterId))
                    ns.Monsters.RemoveAt(j);
            }
        }
    }
}
