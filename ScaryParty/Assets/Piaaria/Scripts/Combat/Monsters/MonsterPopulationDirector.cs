using System.Collections.Generic;
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
        private PizzeriaNetworkState _subscribedNetState;
        private int _spawnedForNight = -1;
        private ulong _nextMonsterId = 1;
        private float _syncTimer;

        public IReadOnlyList<MonsterNpcController> ActiveMonsters => _activeMonsters;
        public IReadOnlyDictionary<ulong, MonsterNpcController> ClientProxies => _clientProxies;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            UnsubscribeClientMonsters();
            ClearAllClientProxies();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;

            if (!root.IsServer)
            {
                EnsureClientSubscribed();
                return;
            }

            var phase = root.DomainState.CurrentNightPhase;
            int night = root.DomainState.CurrentNightNumber;

            if (phase == NightPhase.Active && night >= 1 && _spawnedForNight != night)
            {
                SpawnPopulationForNightServer(root, night);
            }
            else if (phase == NightPhase.PreparingNextNight || phase == NightPhase.Defeat)
            {
                if (_activeMonsters.Count > 0)
                {
                    ClearNightMonstersServer();
                }
            }

            _syncTimer += Time.deltaTime;
            if (_syncTimer >= 0.25f)
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
                    Destroy(_activeMonsters[i].gameObject);
            }
            _activeMonsters.Clear();
            _spawnedForNight = -1;

            var ns = PizzeriaNetworkState.Instance;
            if (ns != null && ns.Monsters != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                ns.Monsters.Clear();
            }
        }

        public void ClearAllClientProxies()
        {
            foreach (var kvp in _clientProxies)
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            }
            _clientProxies.Clear();
        }

        private void EnsureClientSubscribed()
        {
            var ns = PizzeriaNetworkState.Instance;
            if (ns == null || ns.Monsters == null) return;
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
            SyncClientProxiesFromNetworkList();
        }

        public void SyncClientProxiesFromNetworkList()
        {
            var ns = PizzeriaNetworkState.Instance;
            if (ns == null || ns.Monsters == null) return;

            var aliveIds = new HashSet<ulong>();
            for (int i = 0; i < ns.Monsters.Count; i++)
            {
                var dto = ns.Monsters[i];
                aliveIds.Add(dto.MonsterId);
                if (_clientProxies.TryGetValue(dto.MonsterId, out var proxy) && proxy != null)
                {
                    proxy.ApplyReplicatedDto(dto);
                }
                else
                {
                    _clientProxies[dto.MonsterId] = SpawnMonsterClientProxy(dto);
                }
            }

            var toRemove = new List<ulong>();
            foreach (var kvp in _clientProxies)
            {
                if (kvp.Value == null || !aliveIds.Contains(kvp.Key))
                    toRemove.Add(kvp.Key);
            }
            for (int i = 0; i < toRemove.Count; i++)
            {
                if (_clientProxies.TryGetValue(toRemove[i], out var p) && p != null)
                    Destroy(p.gameObject);
                _clientProxies.Remove(toRemove[i]);
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

        private void SpawnPopulationForNightServer(PizzeriaRoot root, int night)
        {
            ClearNightMonstersServer();
            _spawnedForNight = night;

            var stats = root.RunProgressionService != null
                ? root.RunProgressionService.RecalculateEffectiveStats()
                : new RunEffectiveStats();

            int vampireCount = Mathf.Max(1, Mathf.RoundToInt((1 + night) * stats.VampireSpawnMultiplier));
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

            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = 0.38f;

            BuildMonsterVisual(go, species, out var eyeRenderers);

            var agent = go.AddComponent<NavMeshAgent>();
            agent.speed = species == NpcSpecies.Werewolf ? 4.8f : (species == NpcSpecies.Zombie ? 2.4f : 3.6f);
            agent.radius = 0.38f;
            agent.height = 1.8f;

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

            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = 0.38f;

            BuildMonsterVisual(go, species, out var eyeRenderers);

            var ctrl = go.AddComponent<MonsterNpcController>();
            ctrl.ConfigureClientProxy(dto.MonsterId, species, eyeRenderers, dto);
            return ctrl;
        }

        private static void BuildMonsterVisual(GameObject go, NpcSpecies species, out Renderer[] eyeRenderers)
        {
            Color tint = species switch
            {
                NpcSpecies.Vampire => new Color(0.25f, 0.08f, 0.14f),
                NpcSpecies.Werewolf => new Color(0.32f, 0.24f, 0.18f),
                NpcSpecies.Ghost => new Color(0.72f, 0.88f, 0.95f),
                NpcSpecies.Zombie => new Color(0.26f, 0.48f, 0.24f),
                _ => Color.gray
            };

            GameObject visualPrefab = ScaryParty.VisualStyles.VisualStyleManager.Active?.activeLibrary?.styledCharacterVariantPrefab;
            if (visualPrefab == null) visualPrefab = Resources.Load<GameObject>("CharacterVisual");
            if (visualPrefab != null)
            {
                var inst = Instantiate(visualPrefab, go.transform, false);
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
                Destroy(body.GetComponent<Collider>());
                body.transform.SetParent(go.transform, false);
                body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                body.transform.localScale = new Vector3(0.75f, 0.9f, 0.75f);
                var bodyRend = body.GetComponent<Renderer>();
                if (bodyRend != null && bodyRend.material != null)
                {
                    bodyRend.material.color = tint;
                }
            }

            var eyeObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            eyeObj.name = "Eyes";
            Destroy(eyeObj.GetComponent<Collider>());
            eyeObj.transform.SetParent(go.transform, false);
            eyeObj.transform.localPosition = new Vector3(0f, 1.52f, 0.34f);
            eyeObj.transform.localScale = new Vector3(0.42f, 0.10f, 0.12f);
            eyeRenderers = new[] { eyeObj.GetComponent<Renderer>() };
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
                        float roofY = center.y + Mathf.Max(4.5f, b.floorCount * 3.5f);
                        return new Vector3(center.x, roofY, center.z);
                    }
                    if (NavMesh.SamplePosition(b.entrancePosition, out var navHit, 5f, NavMesh.AllAreas))
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

            var activeIds = new HashSet<ulong>();
            for (int i = 0; i < _activeMonsters.Count; i++)
            {
                var m = _activeMonsters[i];
                if (m == null) continue;
                activeIds.Add(m.MonsterId);

                var dto = new NetMonsterDto
                {
                    MonsterId = m.MonsterId,
                    Species = (byte)m.Species,
                    BehaviorState = (byte)m.CurrentBehaviorState,
                    CurrentHp = m.CurrentHealth,
                    MaxHp = m.MaxHealth,
                    PosX = m.transform.position.x,
                    PosY = m.transform.position.y,
                    PosZ = m.transform.position.z,
                    Yaw = m.transform.eulerAngles.y,
                    IsAlerted = m.CurrentBehaviorState == NpcBehaviorState.Aggressive || m.CurrentBehaviorState == NpcBehaviorState.Attacking,
                    Revision = m.Revision
                };

                int idx = -1;
                for (int j = 0; j < ns.Monsters.Count; j++)
                {
                    if (ns.Monsters[j].MonsterId == dto.MonsterId) { idx = j; break; }
                }
                if (idx >= 0)
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
                if (!activeIds.Contains(ns.Monsters[j].MonsterId))
                    ns.Monsters.RemoveAt(j);
            }
        }
    }
}
