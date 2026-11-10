using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using Unity.AI.Navigation;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Stations;
using ScaryParty.Player;

namespace ScaryParty.Pizzeria.Presentation
{
    /// <summary>
    /// Gerencia a instanciação visual e movimentação dos clientes presenciais (NPCs) a partir de PizzeriaNetworkState.Customers,
    /// valida/reconstrói o NavMeshSurface após o posicionamento procedural da pizzaria e garante as âncoras de filas e mesas.
    /// </summary>
    public class CustomerSpawner : MonoBehaviour
    {
        public static CustomerSpawner Instance { get; private set; }

        [Header("Prefab Visual Compartilhado")]
        public GameObject characterVisualPrefab;

        private readonly Dictionary<ulong, CustomerNpcView> _activeViews = new Dictionary<ulong, CustomerNpcView>();
        private readonly Dictionary<int, CustomerCounterStation> _counterByQueue = new Dictionary<int, CustomerCounterStation>();
        private readonly Dictionary<int, DiningTableStation> _tableById = new Dictionary<int, DiningTableStation>();
        private bool _subscribed;
        private float _cacheTimer;

        public IReadOnlyDictionary<ulong, CustomerNpcView> ActiveViews => _activeViews;

        private void Awake()
        {
            Instance = this;
            EnsureVisualPrefabLoaded();
        }

        private void Start()
        {
            EnsureDiningStationsExist();
            ValidateOrRebuildNavMesh();
            TrySubscribeNetworkState();
        }

        private void OnDestroy()
        {
            UnsubscribeNetworkState();
            foreach (var kvp in _activeViews)
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            }
            _activeViews.Clear();
            if (Instance == this) Instance = null;
        }

        private void EnsureVisualPrefabLoaded()
        {
            if (characterVisualPrefab != null) return;
            characterVisualPrefab = Resources.Load<GameObject>("CharacterVisual");
#if UNITY_EDITOR
            if (characterVisualPrefab == null)
            {
                characterVisualPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piaaria/Prefabs/Characters/CharacterVisual.prefab");
            }
#endif
            if (characterVisualPrefab == null)
            {
                var anyPlayerVisual = FindFirstObjectByType<CharacterVisualController>();
                if (anyPlayerVisual != null)
                {
                    characterVisualPrefab = anyPlayerVisual.gameObject;
                }
            }
        }

        public static Vector3 SnapToFloor(Vector3 worldPos)
        {
            float fallbackFloorY = worldPos.y;
            if (PizzeriaRoot.Instance != null)
            {
                fallbackFloorY = PizzeriaRoot.Instance.transform.TransformPoint(new Vector3(0f, -1.60f, 0f)).y;
            }

            // Raycast seletivo filtrando especificamente colisores de piso (Floor_Salao, etc.) para não colidir com mesas, cadeiras ou NPCs
            var hits = Physics.RaycastAll(worldPos + Vector3.up * 2.0f, Vector3.down, 6.0f, ~0, QueryTriggerInteraction.Ignore);
            float bestFloorY = float.MinValue;
            bool foundFloor = false;

            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                string colName = hit.collider.name.ToLowerInvariant();
                bool isFloorCollider = colName.Contains("floor") || colName.Contains("piso") || colName.Contains("ground") || colName.Contains("chao");
                if (isFloorCollider)
                {
                    bestFloorY = Mathf.Max(bestFloorY, hit.point.y);
                    foundFloor = true;
                }
            }

            if (foundFloor)
            {
                worldPos.y = bestFloorY;
                return worldPos;
            }

            worldPos.y = fallbackFloorY;
            return worldPos;
        }

        /// <summary>
        /// Valida se existe NavMesh ativo na área da pizzaria após o posicionamento procedural e reconstrói se necessário.
        /// </summary>
        public void ValidateOrRebuildNavMesh()
        {
            var root = PizzeriaRoot.Instance != null ? PizzeriaRoot.Instance.transform : transform;
            Vector3 checkPos = root.position + Vector3.up * 0.2f;
            if (UnityEngine.AI.NavMesh.SamplePosition(checkPos, out _, 3.5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                return;
            }

            var surface = root.GetComponentInChildren<NavMeshSurface>(true);
            if (surface != null)
            {
                surface.BuildNavMesh();
            }
        }

        public void EnsureDiningStationsExist()
        {
            _counterByQueue.Clear();
            _tableById.Clear();

            var root = PizzeriaRoot.Instance != null ? PizzeriaRoot.Instance.transform : transform;

            var counters = root.GetComponentsInChildren<CustomerCounterStation>(true);
            for (int i = 0; i < counters.Length; i++)
            {
                if (counters[i] != null)
                {
                    counters[i].EnsureQueueSlotAnchors(3);
                    counters[i].EnsureNavMeshObstacle();
                    _counterByQueue[counters[i].queueIndex] = counters[i];
                }
            }

            // Se o prefab ainda não tiver os 2 pontos de fila configurados, cria pontos de fila padrão alinhados ao fluxo do salão
            if (!_counterByQueue.ContainsKey(0) || !_counterByQueue.ContainsKey(1))
            {
                Transform entrance = PizzeriaRoot.Instance != null && PizzeriaRoot.Instance.entranceAnchor != null
                    ? PizzeriaRoot.Instance.entranceAnchor
                    : root;

                // Direção para o interior da pizzaria (oposto à fachada/saída da rua)
                Vector3 intoPizzeria = Vector3.right;
                if (PizzeriaRoot.Instance != null && PizzeriaRoot.Instance.GetComponent<PizzeriaFootprint>() is { } fp)
                {
                    intoPizzeria = new Vector3(-fp.entranceDirectionLocal.x, 0f, -fp.entranceDirectionLocal.y).normalized;
                }
                Quaternion counterRot = Quaternion.LookRotation(intoPizzeria, Vector3.up);
                Vector3 lateralDir = Vector3.Cross(Vector3.up, intoPizzeria).normalized;

                for (int q = 0; q < 2; q++)
                {
                    if (_counterByQueue.ContainsKey(q)) continue;
                    var go = new GameObject($"CustomerQueueCounter_{q}");
                    go.transform.SetParent(root, false);
                    float lateralOffset = q == 0 ? -1.0f : 1.0f;
                    Vector3 basePos = entrance != root
                        ? root.InverseTransformPoint(entrance.position) + intoPizzeria * 4.2f + lateralDir * lateralOffset
                        : intoPizzeria * 3.5f + lateralDir * lateralOffset;
                    go.transform.localPosition = basePos;
                    go.transform.localRotation = counterRot;

                    var col = go.AddComponent<BoxCollider>();
                    col.center = new Vector3(0f, 1.0f, 0f);
                    col.size = new Vector3(0.9f, 1.8f, 0.9f);

                    var station = go.AddComponent<CustomerCounterStation>();
                    station.queueIndex = q;
                    station.counterName = $"Balcão de Atendimento (Fila {q + 1})";
                    station.EnsureQueueSlotAnchors(3);
                    station.EnsureNavMeshObstacle();
                    _counterByQueue[q] = station;
                }
            }

            var tables = root.GetComponentsInChildren<DiningTableStation>(true);
            for (int i = 0; i < tables.Length; i++)
            {
                if (tables[i] != null && tables[i].tableId > 0)
                {
                    tables[i].EnsureAnchors();
                    tables[i].EnsureNavMeshObstacle();
                    _tableById[tables[i].tableId] = tables[i];
                }
            }

            // Auto-vincular mesas existentes na hierarquia (Salao/Mesa_*) caso ainda não tenham DiningTableStation
            if (_tableById.Count == 0)
            {
                var allTransforms = root.GetComponentsInChildren<Transform>(true);
                int nextId = 1;
                for (int i = 0; i < allTransforms.Length && nextId <= 6; i++)
                {
                    var t = allTransforms[i];
                    if (t == null || t == root) continue;
                    string lower = t.name.ToLowerInvariant();
                    if ((lower.StartsWith("mesa") || lower.Contains("diningtable") || lower.Contains("table_")) &&
                        t.GetComponent<StationView>() == null &&
                        t.GetComponent<DiningTableStation>() == null)
                    {
                        var dts = t.gameObject.AddComponent<DiningTableStation>();
                        dts.tableId = nextId;
                        dts.tableName = $"Mesa {nextId}";
                        if (t.GetComponentInChildren<Collider>() == null)
                        {
                            var bc = t.gameObject.AddComponent<BoxCollider>();
                            bc.center = new Vector3(0f, 0.45f, 0f);
                            bc.size = new Vector3(1.1f, 0.9f, 1.1f);
                        }
                        dts.EnsureAnchors();
                        dts.EnsureNavMeshObstacle();
                        _tableById[nextId] = dts;
                        nextId++;
                    }
                }
            }

            // Fallback se não houver nenhuma malha batizada de mesa
            if (_tableById.Count == 0)
            {
                for (int id = 1; id <= 4; id++)
                {
                    var go = new GameObject($"DiningTable_{id}");
                    go.transform.SetParent(root, false);
                    float x = (id % 2 == 1) ? -2.4f : 2.4f;
                    float z = (id <= 2) ? -1.5f : -3.5f;
                    go.transform.localPosition = new Vector3(x, 0f, z);
                    var bc = go.AddComponent<BoxCollider>();
                    bc.center = new Vector3(0f, 0.45f, 0f);
                    bc.size = new Vector3(1.1f, 0.9f, 1.1f);

                    var dts = go.AddComponent<DiningTableStation>();
                    dts.tableId = id;
                    dts.tableName = $"Mesa {id}";
                    dts.EnsureAnchors();
                    dts.EnsureNavMeshObstacle();
                    _tableById[id] = dts;
                }
            }

            // Garantir que todos os balcões e bancadas da pizzaria possuam NavMeshObstacle com carving ativo
            var allSceneTransforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allSceneTransforms.Length; i++)
            {
                var t = allSceneTransforms[i];
                if (t == null || t == root) continue;
                string lower = t.name.ToLowerInvariant();
                if (lower.Contains("balcao") || lower.Contains("counter"))
                {
                    var obs = t.GetComponent<NavMeshObstacle>();
                    if (obs == null)
                    {
                        obs = t.gameObject.AddComponent<NavMeshObstacle>();
                    }
                    var rend = t.GetComponentInChildren<Renderer>();
                    if (rend != null)
                    {
                        obs.center = t.InverseTransformPoint(rend.bounds.center);
                        obs.size = rend.bounds.size;
                    }
                    else
                    {
                        obs.center = new Vector3(0f, 0.5f, 0f);
                        obs.size = new Vector3(1f, 1f, 1f);
                    }
                    obs.shape = NavMeshObstacleShape.Box;
                    obs.carving = true;
                    obs.carveOnlyStationary = true;
                }
            }
        }

        private void TrySubscribeNetworkState()
        {
            if (_subscribed) return;
            var netState = PizzeriaNetworkState.Instance;
            if (netState == null || netState.Customers == null) return;

            netState.Customers.OnListChanged += OnCustomersChanged;
            _subscribed = true;

            for (int i = 0; i < netState.Customers.Count; i++)
            {
                SyncCustomerView(netState.Customers[i]);
            }
        }

        private void UnsubscribeNetworkState()
        {
            if (!_subscribed) return;
            var netState = PizzeriaNetworkState.Instance;
            if (netState != null && netState.Customers != null)
            {
                netState.Customers.OnListChanged -= OnCustomersChanged;
            }
            _subscribed = false;
        }

        private void OnCustomersChanged(NetworkListEvent<NetCustomerDto> changeEvent)
        {
            RefreshAllCustomers();
        }

        private void Update()
        {
            if (!_subscribed)
            {
                TrySubscribeNetworkState();
            }

            _cacheTimer -= Time.deltaTime;
            if (_cacheTimer <= 0f)
            {
                _cacheTimer = 0.25f;
                RefreshAllCustomers();
            }
        }

        public void RefreshAllCustomers()
        {
            var netState = PizzeriaNetworkState.Instance;
            if (netState == null || netState.Customers == null) return;

            var presentIds = new HashSet<ulong>();
            for (int i = 0; i < netState.Customers.Count; i++)
            {
                var dto = netState.Customers[i];
                if (dto.Phase == (byte)CustomerPhase.Despawned) continue;
                presentIds.Add(dto.CustomerId);
                SyncCustomerView(dto);
            }

            var toRemove = new List<ulong>();
            foreach (var kvp in _activeViews)
            {
                if (!presentIds.Contains(kvp.Key))
                {
                    if (kvp.Value != null)
                    {
                        if (CityPopulationDirector.Instance != null)
                        {
                            CityPopulationDirector.Instance.ReturnCustomerToCityPopulation(kvp.Value);
                        }
                        else
                        {
                            Destroy(kvp.Value.gameObject);
                        }
                    }
                    toRemove.Add(kvp.Key);
                }
            }
            for (int i = 0; i < toRemove.Count; i++)
            {
                _activeViews.Remove(toRemove[i]);
            }
        }

        private void SyncCustomerView(NetCustomerDto dto)
        {
            if (_counterByQueue.Count == 0 || _tableById.Count == 0)
            {
                EnsureDiningStationsExist();
            }

            var catalog = PizzeriaRoot.Instance?.Catalog;
            float speedMult = 1f;
            if (catalog != null)
            {
                if (catalog.CustomerTraits.TryGetValue(dto.PositiveTraitId, out var pt))
                    speedMult *= pt.WalkSpeedMultiplier;
                if (catalog.CustomerTraits.TryGetValue(dto.NegativeTraitId, out var nt))
                    speedMult *= nt.WalkSpeedMultiplier;
            }

            ResolveTargetPose(dto, out Vector3 targetPos, out Quaternion targetRot);

            if (!_activeViews.TryGetValue(dto.CustomerId, out var view) || view == null)
            {
                Vector3 spawnPos = SnapToFloor(GetEntrancePosition());
                var npcGo = new GameObject($"CustomerNPC_{dto.CustomerId}");
                npcGo.transform.SetParent(transform, true);
                npcGo.transform.position = spawnPos;
                npcGo.transform.rotation = targetRot;

                var agent = npcGo.AddComponent<NavMeshAgent>();
                agent.speed = 2.4f * Mathf.Clamp(speedMult, 0.7f, 1.5f);
                agent.angularSpeed = 360f;
                agent.acceleration = 12f;
                agent.stoppingDistance = 0.15f;
                agent.baseOffset = 0f;
                agent.height = 1.65f;
                agent.radius = 0.35f;

                // NavMesh alignment
                if (UnityEngine.AI.NavMesh.SamplePosition(spawnPos, out var navHit, 3.5f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    spawnPos = navHit.position;
                    npcGo.transform.position = spawnPos;
                }
                else if (ScaryParty.Navigation.Core.NavigationWorldManager.Instance != null &&
                         ScaryParty.Navigation.Core.NavigationWorldManager.Instance.SamplePosition(spawnPos, out var managerPos, 4.0f))
                {
                    spawnPos = managerPos;
                    npcGo.transform.position = spawnPos;
                }

                GameObject visualInst = InstantiateCustomerVisual();
                view = npcGo.AddComponent<CustomerNpcView>();
                view.Initialize(dto, visualInst, speedMult);
                _activeViews[dto.CustomerId] = view;
            }

            view.ApplyState(dto, targetPos, targetRot, speedMult);
        }

        private GameObject InstantiateCustomerVisual()
        {
            EnsureVisualPrefabLoaded();
            if (characterVisualPrefab != null)
            {
                var inst = Instantiate(characterVisualPrefab);
                inst.name = "CharacterVisual";
                inst.SetActive(true);
                return inst;
            }

            // Fallback procedural caso prefab não esteja carregado em teste isolado
            var fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fallback.name = "CharacterVisual_Fallback";
            var col = fallback.GetComponent<Collider>();
            if (col != null) Destroy(col);
            fallback.AddComponent<CharacterAppearance>();
            fallback.AddComponent<CharacterVisualController>();
            return fallback;
        }

        private Vector3 GetEntrancePosition()
        {
            Vector3 pos;
            if (PizzeriaRoot.Instance != null && PizzeriaRoot.Instance.entranceAnchor != null)
                pos = PizzeriaRoot.Instance.entranceAnchor.position;
            else
                pos = transform.position - transform.forward * 5f;
            return SnapToFloor(pos);
        }

        private void ResolveTargetPose(NetCustomerDto dto, out Vector3 pos, out Quaternion rot)
        {
            var phase = (CustomerPhase)dto.Phase;
            if (dto.IsInQueue && _counterByQueue.TryGetValue(dto.QueueIndex, out var counter) && counter != null)
            {
                pos = SnapToFloor(counter.GetQueueSlotPosition(dto.QueueSlotIndex));
                rot = counter.GetQueueSlotRotation(dto.QueueSlotIndex);
                return;
            }

            if ((phase == CustomerPhase.WalkingToTable ||
                 phase == CustomerPhase.SeatedWaitingFood ||
                 phase == CustomerPhase.Eating) &&
                dto.AssignedTableId > 0 &&
                _tableById.TryGetValue(dto.AssignedTableId, out var table) &&
                table != null)
            {
                pos = table.GetSeatPosition();
                rot = table.GetSeatRotation();
                return;
            }

            pos = SnapToFloor(GetEntrancePosition());
            rot = Quaternion.LookRotation(-transform.forward, Vector3.up);
        }
    }
}
