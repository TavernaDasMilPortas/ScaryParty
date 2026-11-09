using UnityEngine;
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
    /// - Interagir com [E] ou [F] segurando uma pizza assada SEM CAIXA serve a pizza diretamente na mesa.
    /// - Rejeita pizzas embaladas em caixa com mensagem clara.
    /// </summary>
    public class DiningTableStation : MonoBehaviour, IInteractable
    {
        [Header("Identificação da Mesa")]
        public int tableId = 1;
        public string tableName = "Mesa 1";

        [Header("Âncoras de Assento e Prato")]
        public Transform seatAnchor;
        public Transform plateAnchor;

        private void Awake()
        {
            EnsureAnchors();
        }

        public void EnsureAnchors()
        {
            if (seatAnchor == null)
            {
                seatAnchor = transform.Find("SeatAnchor");
                if (seatAnchor == null)
                {
                    // Tentar localizar cadeira próxima ou filha
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
                }
                if (seatAnchor == null)
                {
                    var go = new GameObject("SeatAnchor");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(0f, 0f, -0.75f);
                    go.transform.localRotation = Quaternion.identity;
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
                    go.transform.localPosition = new Vector3(0f, 0.78f, 0f);
                    go.transform.localRotation = Quaternion.identity;
                    plateAnchor = go.transform;
                }
            }
        }

        public Vector3 GetSeatPosition()
        {
            EnsureAnchors();
            if (seatAnchor != null)
            {
                return seatAnchor.position;
            }
            Vector3 rawPos = transform.position - transform.forward * 0.75f;
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
