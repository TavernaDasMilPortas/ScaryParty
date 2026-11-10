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
    /// - Possui âncora de assento (seatAnchor) para o NPC sentar e âncora de prato (plateAnchor) para exibir a pizza servida.
    /// - Esculpe automaticamente o NavMesh via NavMeshObstacle com carving para impedir atravessamento.
    /// - Localiza a cadeira física real no salão para garantir orientação de frente para a mesa.
    /// - Interagir com [E] ou [F] segurando uma pizza assada SEM CAIXA serve a pizza diretamente na mesa.
    /// </summary>
    public class DiningTableStation : MonoBehaviour, IInteractable
    {
        [Header("Identificação da Mesa")]
        public int tableId = 1;
        public string tableName = "Mesa 1";

        [Header("Âncoras de Assento e Prato")]
        public Transform seatAnchor;
        public Transform plateAnchor;

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
        }

        private void Awake()
        {
            EnsureAnchors();
            EnsureNavMeshObstacle();
        }

        public void EnsureNavMeshObstacle()
        {
            var obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle == null)
            {
                obstacle = gameObject.AddComponent<NavMeshObstacle>();
            }

            obstacle.shape = NavMeshObstacleShape.Box;
            var mf = GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds b = mf.sharedMesh.bounds;
                Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                for (int x = -1; x <= 1; x += 2)
                {
                    for (int y = -1; y <= 1; y += 2)
                    {
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 cornerLocal = b.center + new Vector3(b.extents.x * x, b.extents.y * y, b.extents.z * z);
                            Vector3 cornerThis = transform.InverseTransformPoint(mf.transform.TransformPoint(cornerLocal));
                            min = Vector3.Min(min, cornerThis);
                            max = Vector3.Max(max, cornerThis);
                        }
                    }
                }
                Vector3 localCenter = (min + max) * 0.5f;
                Vector3 localSize = max - min;
                obstacle.center = localCenter;
                obstacle.size = new Vector3(Mathf.Max(0.6f, localSize.x * 0.9f),
                                            Mathf.Max(0.8f, localSize.y),
                                            Mathf.Max(0.6f, localSize.z * 0.9f));
            }
            else
            {
                obstacle.center = new Vector3(0f, 0.45f, 0f);
                obstacle.size = new Vector3(1.1f, 0.9f, 1.1f);
            }

            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
        }

        public void EnsureAnchors()
        {
            if (seatAnchor == null)
            {
                seatAnchor = transform.Find("SeatAnchor");
            }

            if (seatAnchor == null)
            {
                // 1. Tentar localizar cadeira próxima ou filha
                for (int i = 0; i < transform.childCount; i++)
                {
                    var ch = transform.GetChild(i);
                    string n = ch.name.ToLowerInvariant();
                    if (n.Contains("cadeira") || n.Contains("chair") || n.Contains("seat"))
                    {
                        seatAnchor = ch;
                        break;
                    }
                }

                // 2. Se não encontrou em filhos, procurar a cadeira mais próxima entre irmãos ou na hierarquia do salão
                if (seatAnchor == null)
                {
                    Transform nearestChair = null;
                    float minDist = 2.5f;

                    // Procurar em irmãos (ex: sob Furniture)
                    if (transform.parent != null)
                    {
                        for (int i = 0; i < transform.parent.childCount; i++)
                        {
                            var sibling = transform.parent.GetChild(i);
                            if (sibling == transform) continue;
                            string n = sibling.name.ToLowerInvariant();
                            if (n.Contains("cadeira") || n.Contains("chair") || n.Contains("seat"))
                            {
                                float dist = Vector3.Distance(new Vector3(sibling.position.x, 0f, sibling.position.z),
                                                              new Vector3(transform.position.x, 0f, transform.position.z));
                                if (dist < minDist)
                                {
                                    minDist = dist;
                                    nearestChair = sibling;
                                }
                            }
                        }
                    }

                    // Se ainda não encontrou, buscar na raiz da pizzaria
                    if (nearestChair == null)
                    {
                        var root = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance != null
                            ? ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance.transform
                            : transform.root;
                        var all = root.GetComponentsInChildren<Transform>(true);
                        for (int i = 0; i < all.Length; i++)
                        {
                            if (all[i] == transform) continue;
                            string n = all[i].name.ToLowerInvariant();
                            if (n.Contains("cadeira") || n.Contains("chair") || n.Contains("seat"))
                            {
                                float dist = Vector3.Distance(new Vector3(all[i].position.x, 0f, all[i].position.z),
                                                              new Vector3(transform.position.x, 0f, transform.position.z));
                                if (dist < minDist)
                                {
                                    minDist = dist;
                                    nearestChair = all[i];
                                }
                            }
                        }
                    }

                    var go = new GameObject("SeatAnchor");
                    go.transform.SetParent(transform, true);

                    if (nearestChair != null)
                    {
                        // Posiciona a âncora exatamente na cadeira correspondente
                        Vector3 chairPos = nearestChair.position;
                        Vector3 toTable = transform.position - chairPos;
                        toTable.y = 0f;
                        Quaternion faceTable = toTable.sqrMagnitude > 0.001f
                            ? Quaternion.LookRotation(toTable.normalized, Vector3.up)
                            : nearestChair.rotation;

                        go.transform.position = chairPos;
                        go.transform.rotation = faceTable;
                    }
                    else
                    {
                        // Fallback relativo à frente da mesa
                        go.transform.localPosition = new Vector3(0f, 0f, -0.75f);
                        go.transform.localRotation = Quaternion.identity;
                    }

                    seatAnchor = go.transform;
                }
            }

            if (plateAnchor == null)
            {
                plateAnchor = transform.Find("PlateAnchor");
                if (plateAnchor == null)
                {
                    var go = new GameObject("PlateAnchor");
                    go.transform.SetParent(transform, false);
                    var rend = GetComponentInChildren<Renderer>();
                    float topY = rend != null ? rend.bounds.max.y - transform.position.y + 0.02f : 0.20f;
                    go.transform.localPosition = new Vector3(0f, topY, 0f);
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
            return Presentation.CustomerSpawner.SnapToFloor(rawPos);
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

        public Vector3 GetPlatePosition()
        {
            EnsureAnchors();
            return plateAnchor != null ? plateAnchor.position : transform.position + Vector3.up * 0.78f;
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

            // Procurar pizza na mão ativa (ou na outra mão como fallback)
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
    }
}
