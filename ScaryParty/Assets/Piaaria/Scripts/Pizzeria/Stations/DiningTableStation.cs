using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.Pizzeria.Stations
{
    /// <summary>
    /// Estação de Mesa do Salão para atendimento presencial (1 mesa inteira por NPC nesta versão).
    /// - Possui 4 âncoras completas para o assento:
    ///   1) seatAnchor: Root no piso para alinhamento do transform do personagem.
    ///   2) seatSurfaceAnchor: Superfície real do assento da cadeira (~0.45m de altura) para validação da pelve.
    ///   3) approachAnchor: Ponto no NavMesh limpo para a caminhada do NPC sem concorrer com a física da mesa.
    ///   4) exitAnchor: Ponto de saída desobstruído para o NPC levantar e retornar à navegação.
    /// - Esculpe automaticamente o NavMesh via NavMeshObstacle com carving para impedir atravessamento.
    /// - Interagir com [E] ou [F] segurando uma pizza assada SEM CAIXA serve a pizza diretamente na mesa.
    /// </summary>
    public class DiningTableStation : MonoBehaviour, IInteractable
    {
        [Header("Identificação da Mesa")]
        public int tableId = 1;
        public string tableName = "Mesa 1";

        [Header("Âncoras de Assento da Cadeira")]
        public Transform seatAnchor;          // Root no piso para o Transform do personagem
        public Transform seatSurfaceAnchor;   // Superfície real do assento (cushion ~0.45m)
        public Transform approachAnchor;      // Ponto de aproximação no NavMesh (fora do carving)
        public Transform exitAnchor;          // Ponto de saída segura após levantar
        public Transform plateAnchor;         // Ponto de apoio da pizza na mesa

        [Header("Condimentos da Mesa")]
        [Tooltip("Objetos de molhos/condimentos que ficam sobre a mesa e devem ser recolhidos quando a pizza for servida.")]
        public GameObject[] sauceObjects;

        [Header("Cadeira Associada e Animação Discreta")]
        [Tooltip("Cadeira associada explicitamente a esta mesa. Resolvida automaticamente se vazia.")]
        public Transform associatedChair;
        [SerializeField] private float chairPullOutDistance = 0.18f;
        [SerializeField] private float chairAnimSpeed = 6.0f;

        private bool _chairPoseCaptured;
        private Vector3 _chairRestWorldPos;
        private Quaternion _chairRestWorldRot;
        private Vector3 _chairPullDirectionWorld;
        private bool _chairTargetOccupied;
        private float _chairOccupiedT;

        private static readonly System.Collections.Generic.Dictionary<int, DiningTableStation> _instancesByTableId = new System.Collections.Generic.Dictionary<int, DiningTableStation>();
        public static bool TryGetTable(int id, out DiningTableStation table) => _instancesByTableId.TryGetValue(id, out table);

        private void OnEnable()
        {
            if (tableId > 0)
            {
                _instancesByTableId[tableId] = this;
            }
        }

        private void OnDisable()
        {
            if (tableId > 0 && _instancesByTableId.TryGetValue(tableId, out var cur) && cur == this)
            {
                _instancesByTableId.Remove(tableId);
            }
            SetChairOccupiedVisual(0, false);
        }

        private void Awake()
        {
            EnsureAnchors();
            EnsureSauceObjects();
            EnsureNavMeshObstacle();
        }

        public void SetChairOccupiedVisual(bool occupied) => SetChairOccupiedVisual(0, occupied);

        public void SetChairOccupiedVisual(int seatIndex, bool occupied)
        {
            if (seatIndex != 0) return;
            _chairTargetOccupied = occupied;
        }

        public void EnsureSauceObjects()
        {
            if (sauceObjects != null && sauceObjects.Length > 0) return;

            var found = new System.Collections.Generic.List<GameObject>();
            for (int i = 0; i < transform.childCount; i++)
            {
                var ch = transform.GetChild(i);
                string n = ch.name.ToLowerInvariant();
                if (n.Contains("molho") || n.Contains("sauce") || n.Contains("condiment") || n.Contains("ketchup"))
                {
                    found.Add(ch.gameObject);
                }
            }
            if (found.Count > 0)
            {
                sauceObjects = found.ToArray();
            }
        }

        private void Update()
        {
            UpdateSauceVisibility();
            UpdateChairAnimation();
        }

        private void UpdateChairAnimation()
        {
            if (!_chairPoseCaptured || associatedChair == null) return;

            float target = _chairTargetOccupied ? 1f : 0f;
            if (Mathf.Approximately(_chairOccupiedT, target)) return;

            _chairOccupiedT = Mathf.MoveTowards(_chairOccupiedT, target, Time.deltaTime * chairAnimSpeed);
            associatedChair.position = _chairRestWorldPos + _chairPullDirectionWorld * (chairPullOutDistance * _chairOccupiedT);
            associatedChair.rotation = _chairRestWorldRot;
        }

        public void UpdateSauceVisibility()
        {
            EnsureSauceObjects();
            if (sauceObjects == null || sauceObjects.Length == 0) return;

            bool hasPizzaServed = false;
            if (TryGetTableState(out var tbl, out _))
            {
                hasPizzaServed = tbl.ServedPizzaItemId != 0;
            }

            bool shouldShowSauces = !hasPizzaServed;
            for (int i = 0; i < sauceObjects.Length; i++)
            {
                if (sauceObjects[i] != null && sauceObjects[i].activeSelf != shouldShowSauces)
                {
                    sauceObjects[i].SetActive(shouldShowSauces);
                }
            }
        }

        public void EnsureNavMeshObstacle()
        {
            var obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle == null)
            {
                obstacle = gameObject.AddComponent<NavMeshObstacle>();
            }

            obstacle.shape = NavMeshObstacleShape.Box;
            if (TryCalculateTableBodyWorldBounds(out Bounds worldBounds))
            {
                Vector3 c = worldBounds.center;
                Vector3 e = worldBounds.extents;
                Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

                for (int x = -1; x <= 1; x += 2)
                {
                    for (int y = -1; y <= 1; y += 2)
                    {
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 worldCorner = new Vector3(c.x + e.x * x, c.y + e.y * y, c.z + e.z * z);
                            Vector3 localCorner = transform.InverseTransformPoint(worldCorner);
                            min = Vector3.Min(min, localCorner);
                            max = Vector3.Max(max, localCorner);
                        }
                    }
                }

                Vector3 localCenter = (min + max) * 0.5f;
                Vector3 localSize = max - min;
                obstacle.center = localCenter;
                obstacle.size = new Vector3(
                    Mathf.Max(0.55f, localSize.x * 0.88f),
                    Mathf.Max(0.75f, localSize.y),
                    Mathf.Max(0.55f, localSize.z * 0.88f));
            }
            else
            {
                obstacle.center = new Vector3(0f, 0.45f, 0f);
                obstacle.size = new Vector3(1.0f, 0.9f, 1.0f);
            }

            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
        }

        private bool TryCalculateTableBodyWorldBounds(out Bounds worldBounds)
        {
            worldBounds = default;
            bool hasBounds = false;

            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (r.GetComponentInParent<Canvas>() != null) continue;
                if (associatedChair != null && (r.transform == associatedChair || r.transform.IsChildOf(associatedChair))) continue;

                string lower = r.gameObject.name.ToLowerInvariant();
                if (lower.Contains("cadeira") || lower.Contains("chair") || lower.Contains("seat") ||
                    lower.Contains("molho") || lower.Contains("sauce") || lower.Contains("condiment") ||
                    lower.Contains("ketchup") || lower.Contains("plate") || lower.Contains("pizza") ||
                    lower.Contains("box") || lower.Contains("progress") || lower.Contains("ui") ||
                    lower.Contains("highlight") || lower.Contains("indicator"))
                    continue;

                if (r.bounds.size.sqrMagnitude < 0.0001f) continue;

                if (!hasBounds)
                {
                    worldBounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    worldBounds.Encapsulate(r.bounds);
                }
            }

            return hasBounds;
        }

        private Transform ResolveAssociatedChair()
        {
            if (associatedChair != null) return associatedChair;

            Transform nearestChair = null;

            // 1. Procurar cadeira filha explícita
            for (int i = 0; i < transform.childCount; i++)
            {
                var ch = transform.GetChild(i);
                string n = ch.name.ToLowerInvariant();
                if ((n.Contains("cadeira") || n.Contains("chair")) && !n.Contains("anchor"))
                {
                    nearestChair = ch;
                    break;
                }
            }

            // 2. Procurar em irmãos próximos
            if (nearestChair == null && transform.parent != null)
            {
                float minDist = 2.2f;
                for (int i = 0; i < transform.parent.childCount; i++)
                {
                    var sibling = transform.parent.GetChild(i);
                    if (sibling == transform) continue;
                    string n = sibling.name.ToLowerInvariant();
                    if ((n.Contains("cadeira") || n.Contains("chair")) && !n.Contains("anchor"))
                    {
                        float dist = Vector2.Distance(
                            new Vector2(sibling.position.x, sibling.position.z),
                            new Vector2(transform.position.x, transform.position.z));
                        if (dist < minDist)
                        {
                            minDist = dist;
                            nearestChair = sibling;
                        }
                    }
                }
            }

            // 3. Procurar na raiz da pizzaria
            if (nearestChair == null)
            {
                var root = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance != null
                    ? ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance.transform
                    : transform.root;
                var all = root.GetComponentsInChildren<Transform>(true);
                float minDist = 2.2f;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] == transform) continue;
                    string n = all[i].name.ToLowerInvariant();
                    if ((n.Contains("cadeira") || n.Contains("chair")) && !n.Contains("anchor"))
                    {
                        float dist = Vector2.Distance(
                            new Vector2(all[i].position.x, all[i].position.z),
                            new Vector2(transform.position.x, transform.position.z));
                        if (dist < minDist)
                        {
                            minDist = dist;
                            nearestChair = all[i];
                        }
                    }
                }
            }

            associatedChair = nearestChair;
            return associatedChair;
        }

        private void SnapChairToFloorIfNeeded(Transform chair)
        {
            if (chair == null || _chairPoseCaptured) return;

            var renderers = chair.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds cb = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (!hasBounds)
                {
                    cb = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    cb.Encapsulate(r.bounds);
                }
            }

            if (hasBounds)
            {
                float floorY = Presentation.CustomerSpawner.SnapToFloor(chair.position).y;
                float sinkDelta = floorY - cb.min.y;
                // A08: Corrige cadeira afundada (ex: pivô centralizado no mesh) ou levemente solta sem tocar a raiz do NPC
                if (Mathf.Abs(sinkDelta) > 0.015f && Mathf.Abs(sinkDelta) < 0.75f)
                {
                    chair.position += Vector3.up * sinkDelta;
                }
            }

            _chairRestWorldPos = chair.position;
            _chairRestWorldRot = chair.rotation;
            Vector3 away = chair.position - transform.position;
            away.y = 0f;
            _chairPullDirectionWorld = away.sqrMagnitude > 0.001f ? away.normalized : -transform.forward;
            _chairPoseCaptured = true;
        }

        public void EnsureAnchors()
        {
            Transform chair = ResolveAssociatedChair();
            SnapChairToFloorIfNeeded(chair);

            float chairSeatY = 0.45f;
            if (chair != null)
            {
                var rend = chair.GetComponentInChildren<Renderer>();
                if (rend != null)
                {
                    chairSeatY = Mathf.Clamp(rend.bounds.size.y * 0.48f, 0.40f, 0.52f);
                }
            }

            if (seatAnchor == null)
            {
                seatAnchor = transform.Find("SeatAnchor");
            }

            // A06: Nunca reutilizar o Transform da cadeira como seatAnchor
            if (seatAnchor == null || (chair != null && seatAnchor == chair))
            {
                var go = new GameObject("SeatAnchor");
                go.transform.SetParent(transform, true);

                if (chair != null)
                {
                    Vector3 chairPos = _chairPoseCaptured ? _chairRestWorldPos : chair.position;
                    Vector3 toTable = transform.position - chairPos;
                    toTable.y = 0f;
                    Quaternion faceTable = toTable.sqrMagnitude > 0.001f
                        ? Quaternion.LookRotation(toTable.normalized, Vector3.up)
                        : chair.rotation;

                    go.transform.position = Presentation.CustomerSpawner.SnapToFloor(chairPos);
                    go.transform.rotation = faceTable;
                }
                else
                {
                    go.transform.localPosition = new Vector3(0f, 0f, -0.75f);
                    go.transform.localRotation = Quaternion.identity;
                    Vector3 groundFallback = Presentation.CustomerSpawner.SnapToFloor(go.transform.position);
                    go.transform.position = groundFallback;
                }

                seatAnchor = go.transform;
            }
            else
            {
                // Garante que a âncora permaneça no piso
                seatAnchor.position = Presentation.CustomerSpawner.SnapToFloor(seatAnchor.position);
            }

            // Âncora 2: seatSurfaceAnchor (superfície do assento ~0.45m acima do piso)
            if (seatSurfaceAnchor == null)
            {
                seatSurfaceAnchor = seatAnchor.Find("SeatSurfaceAnchor");
                if (seatSurfaceAnchor == null)
                {
                    var sGo = new GameObject("SeatSurfaceAnchor");
                    sGo.transform.SetParent(seatAnchor, false);
                    sGo.transform.localPosition = new Vector3(0f, chairSeatY, 0f);
                    sGo.transform.localRotation = Quaternion.identity;
                    seatSurfaceAnchor = sGo.transform;
                }
            }

            // Direção de saída / trás da cadeira (oposta à mesa)
            Vector3 toTableDir = transform.position - seatAnchor.position;
            toTableDir.y = 0f;
            Vector3 backDir = toTableDir.sqrMagnitude > 0.001f ? -toTableDir.normalized : -seatAnchor.forward;
            Vector3 sideDir = Vector3.Cross(Vector3.up, backDir).normalized;

            if (_chairPullDirectionWorld.sqrMagnitude < 0.001f)
            {
                _chairPullDirectionWorld = backDir;
            }

            // Âncora 3: approachAnchor (~0.70m atrás da cadeira, no NavMesh fora do carving)
            if (approachAnchor == null || approachAnchor == seatAnchor)
            {
                approachAnchor = transform.Find("ApproachAnchor");
                if (approachAnchor == null)
                {
                    var aGo = new GameObject("ApproachAnchor");
                    aGo.transform.SetParent(transform, true);
                    Vector3 approachPos = seatAnchor.position + backDir * 0.70f;
                    aGo.transform.position = Presentation.CustomerSpawner.SnapToFloor(approachPos);
                    aGo.transform.rotation = Quaternion.LookRotation(-backDir, Vector3.up);
                    approachAnchor = aGo.transform;
                }
            }

            // Âncora 4: exitAnchor (~0.65m atrás e 0.35m lateral)
            if (exitAnchor == null || exitAnchor == seatAnchor)
            {
                exitAnchor = transform.Find("ExitAnchor");
                if (exitAnchor == null)
                {
                    var eGo = new GameObject("ExitAnchor");
                    eGo.transform.SetParent(transform, true);
                    Vector3 exitPos = seatAnchor.position + backDir * 0.65f + sideDir * 0.35f;
                    eGo.transform.position = Presentation.CustomerSpawner.SnapToFloor(exitPos);
                    eGo.transform.rotation = Quaternion.LookRotation(backDir, Vector3.up);
                    exitAnchor = eGo.transform;
                }
            }

            // A07: Âncora de prato calculada via bounds do corpo da mesa e convertida por Transform
            if (plateAnchor == null)
            {
                plateAnchor = transform.Find("PlateAnchor");
                if (plateAnchor == null)
                {
                    var go = new GameObject("PlateAnchor");
                    go.transform.SetParent(transform, true);
                    Vector3 worldPlatePos = TryCalculateTableBodyWorldBounds(out Bounds tableBounds)
                        ? new Vector3(tableBounds.center.x, tableBounds.max.y + 0.02f, tableBounds.center.z)
                        : transform.position + Vector3.up * 0.78f;
                    go.transform.position = worldPlatePos;
                    go.transform.localRotation = Quaternion.identity;
                    plateAnchor = go.transform;
                }
            }
        }

        public Vector3 GetSeatPosition()
        {
            EnsureAnchors();
            Vector3 rawPos = seatAnchor != null
                ? seatAnchor.position
                : transform.position - transform.forward * 0.75f;

            if (_chairPoseCaptured && associatedChair != null && _chairOccupiedT > 0.001f)
            {
                rawPos += _chairPullDirectionWorld * (chairPullOutDistance * _chairOccupiedT);
            }

            // Mantém o root do NPC sempre nivelado ao piso
            return Presentation.CustomerSpawner.SnapToFloor(rawPos);
        }

        public Vector3 GetSeatSurfacePosition()
        {
            EnsureAnchors();
            if (seatSurfaceAnchor != null) return seatSurfaceAnchor.position;
            return GetSeatPosition() + Vector3.up * 0.45f;
        }

        public Vector3 GetApproachPosition()
        {
            EnsureAnchors();
            if (approachAnchor != null) return Presentation.CustomerSpawner.SnapToFloor(approachAnchor.position);
            return GetSeatPosition() - transform.forward * 0.65f;
        }

        public Vector3 GetExitPosition()
        {
            EnsureAnchors();
            if (exitAnchor != null) return Presentation.CustomerSpawner.SnapToFloor(exitAnchor.position);
            return GetApproachPosition();
        }

        public Quaternion GetSeatRotation()
        {
            EnsureAnchors();
            if (seatAnchor != null)
            {
                Vector3 toTable = transform.position - seatAnchor.position;
                toTable.y = 0f;
                if (toTable.sqrMagnitude > 0.01f)
                    return Quaternion.LookRotation(toTable.normalized, Vector3.up);
                return seatAnchor.rotation;
            }
            return transform.rotation;
        }

        public Quaternion GetApproachRotation()
        {
            EnsureAnchors();
            if (approachAnchor != null) return approachAnchor.rotation;
            return GetSeatRotation();
        }

        public Vector3 GetPlatePosition()
        {
            EnsureAnchors();
            return plateAnchor != null ? plateAnchor.position : transform.position + Vector3.up * 0.28f;
        }

        public bool TryGetTableState(out NetTableDto tableDto, out NetCustomerDto customerDto)
        {
            tableDto = default;
            customerDto = default;
            var netState = PizzeriaNetworkState.Instance;
            if (netState == null || netState.Tables == null) return false;

            for (int i = 0; i < netState.Tables.Count; i++)
            {
                var t = netState.Tables[i];
                if (t.TableId == tableId)
                {
                    tableDto = t;
                    if (t.ReservedByCustomerId != 0 && netState.Customers != null)
                    {
                        for (int c = 0; c < netState.Customers.Count; c++)
                        {
                            if (netState.Customers[c].CustomerId == t.ReservedByCustomerId)
                            {
                                customerDto = netState.Customers[c];
                                break;
                            }
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        public string InteractPrompt
        {
            get
            {
                if (TryGetTableState(out var tbl, out var cust) && !tbl.IsFree && cust.CustomerId != 0)
                {
                    if (cust.IsWaitingForFood)
                    {
                        var spec = cust.RequestedSpec.ToDomain();
                        var catalog = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance?.Catalog;
                        string summary = PizzaOrderFormatter.FormatShortSummary(spec, catalog);
                        return $"[E] Servir Mesa {tableId} (Pizza sem caixa) — {summary}";
                    }
                    return $"Mesa {tableId} — Cliente comendo";
                }
                return $"Mesa {tableId} (Livre)";
            }
        }

        public void OnInteract(GameObject interactor)
        {
            TryServeTableFromPlayer(interactor);
        }

        public void TryServeTableFromPlayer(GameObject interactor)
        {
            if (interactor == null) return;
            var netObj = interactor.GetComponent<NetworkObject>();
            if (netObj == null) return;

            var netState = PizzeriaNetworkState.Instance;
            var cmd = PizzeriaCommandHandler.Instance;
            if (netState == null || cmd == null) return;

            var invAdapter = interactor.GetComponent<ScaryParty.Pizzeria.Player.PlayerInventoryAdapter>();
            int activeHand = invAdapter != null ? (int)invAdapter.ActiveHand : 0;
            int otherHand = activeHand == 0 ? 1 : 0;
            ulong playerId = netObj.OwnerClientId;

            ulong foundPizzaId = 0;
            bool foundBoxed = false;

            for (int pass = 0; pass < 2; pass++)
            {
                int handToCheck = pass == 0 ? activeHand : otherHand;
                for (int i = 0; i < netState.Items.Count; i++)
                {
                    var it = netState.Items[i];
                    if (it.LocationType == (byte)LocationType.Hand && it.HolderId == playerId && it.SlotId == handToCheck)
                    {
                        if (it.PackagingState == (byte)PackagingState.Boxed || it.Category == (byte)ItemCategory.Box)
                        {
                            foundBoxed = true;
                        }
                        else if (it.Category == (byte)ItemCategory.Pizza || it.Category == (byte)ItemCategory.PizzaBase)
                        {
                            foundPizzaId = it.ItemId;
                            break;
                        }
                    }
                }
                if (foundPizzaId != 0) break;
            }

            if (foundPizzaId != 0)
            {
                cmd.ServeTablePizzaServerRpc(tableId, foundPizzaId);
            }
            else if (foundBoxed)
            {
                NotificationManager.Show("Clientes na mesa recebem a pizza SEM caixa! Caixas são apenas para Delivery.", NotificationType.Warning);
            }
            else
            {
                NotificationManager.Show($"Segure a pizza assada (sem caixa) na mão para servir a Mesa {tableId}!", NotificationType.Info);
            }
        }

        public void OnFocus() { }
        public void OnLoseFocus() { }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            EnsureAnchors();
            if (seatAnchor != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(seatAnchor.position, 0.22f);
                Gizmos.DrawRay(seatAnchor.position, seatAnchor.forward * 0.4f);
            }
            if (seatSurfaceAnchor != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(seatSurfaceAnchor.position, new Vector3(0.38f, 0.02f, 0.38f));
                if (seatAnchor != null)
                {
                    Gizmos.DrawLine(seatAnchor.position, seatSurfaceAnchor.position);
                }
            }
            if (approachAnchor != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(approachAnchor.position, 0.20f);
                Gizmos.DrawRay(approachAnchor.position, approachAnchor.forward * 0.4f);
            }
            if (exitAnchor != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(exitAnchor.position, 0.20f);
                Gizmos.DrawRay(exitAnchor.position, exitAnchor.forward * 0.4f);
            }
            if (plateAnchor != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(plateAnchor.position, 0.18f);
            }
        }
#endif
    }
}
