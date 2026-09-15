using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Network
{
    public class PizzeriaCommandHandler : NetworkBehaviour
    {
        public static PizzeriaCommandHandler Instance { get; private set; }

        private ReplicationProjector _projector;

        private void Awake()
        {
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                var netState = GetComponent<PizzeriaNetworkState>();
                _projector = new ReplicationProjector(netState);
            }
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        private void CommitAndReplicate()
        {
            var root = PizzeriaRoot.Instance;
            if (root != null && root.DomainState != null && _projector != null)
            {
                _projector.ProjectFullState(root.DomainState);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void DispenseIngredientServerRpc(int storageId, int ingredientDefId, byte hand, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;

            var res = root.TransferService.DispenseFromStorage(storageId, ingredientDefId, senderClientId, (HandSlotIndex)hand);
            if (res.Success)
            {
                CommitAndReplicate();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void TransferItemServerRpc(ulong itemIdVal, byte targetLocType, ulong targetHolderId, int targetSlotId, ServerRpcParams rpcParams = default)
        {
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;

            var loc = new LocationRef((LocationType)targetLocType, targetHolderId, targetSlotId);
            var res = root.TransferService.TransferItem(new ItemId(itemIdVal), loc);
            if (res.Success)
            {
                CommitAndReplicate();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void TransferToolServerRpc(ulong toolIdVal, byte targetLocType, ulong targetHolderId, int targetSlotId, ServerRpcParams rpcParams = default)
        {
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;

            var loc = new LocationRef((LocationType)targetLocType, targetHolderId, targetSlotId);
            var res = root.TransferService.TransferTool(new ToolItemId(toolIdVal), loc, root.Clock.Now);
            if (res.Success)
            {
                CommitAndReplicate();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void StartWorkServerRpc(int stationId, int slotIndex, int processId, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;

            var slot = new StationSlotId(stationId, slotIndex);
            var res = root.ProcessingService.StartWork(slot, processId, senderClientId, root.DomainState, root.Catalog, root.Clock);
            if (res.Success)
            {
                CommitAndReplicate();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void HeartbeatWorkServerRpc(int stationId, int slotIndex, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;

            var slot = new StationSlotId(stationId, slotIndex);
            root.ProcessingService.HeartbeatWork(slot, senderClientId, root.DomainState, root.Clock);
        }

        [ServerRpc(RequireOwnership = false)]
        public void CancelWorkServerRpc(int stationId, int slotIndex, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;

            var slot = new StationSlotId(stationId, slotIndex);
            var res = root.ProcessingService.CancelWork(slot, senderClientId, root.DomainState, root.Clock);
            if (res.Success)
            {
                CommitAndReplicate();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void CompleteWorkServerRpc(int stationId, int slotIndex, ServerRpcParams rpcParams = default)
        {
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;

            var slot = new StationSlotId(stationId, slotIndex);
            var res = root.ProcessingService.CompleteWork(slot, root.DomainState, root.Catalog);
            if (res.Success)
            {
                CommitAndReplicate();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void AddIngredientToPizzaServerRpc(ulong pizzaIdVal, ulong ingredientIdVal, ServerRpcParams rpcParams = default)
        {
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;

            var state = root.DomainState;
            if (!state.Items.TryGetValue(new ItemId(pizzaIdVal), out var pizza)) return;
            if (!state.Items.TryGetValue(new ItemId(ingredientIdVal), out var ingItem)) return;

            // Permissive add: Add ingredient composition to pizza
            if (pizza.AddIngredient(ingItem.DefinitionId, ingItem.PrepStage, 1))
            {
                // Consume ingredient item
                root.TransferService.DestroyItem(ingItem.Id);
                CommitAndReplicate();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void InsertOvenServerRpc(ulong pizzaIdVal, int stationId, int slotIndex, ServerRpcParams rpcParams = default)
        {
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;

            var slot = new StationSlotId(stationId, slotIndex);
            var res = root.ProcessingService.InsertOven(new ItemId(pizzaIdVal), slot, root.DomainState, 15f, root.Clock);
            if (res.Success)
            {
                CommitAndReplicate();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void RemoveOvenServerRpc(int stationId, int slotIndex, byte targetHand, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;

            var slot = new StationSlotId(stationId, slotIndex);
            var targetLoc = LocationRef.InHand(senderClientId, (HandSlotIndex)targetHand);
            var res = root.ProcessingService.RemoveOven(slot, targetLoc, root.DomainState, root.Clock);
            if (res.Success)
            {
                CommitAndReplicate();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void StartPackagingServerRpc(ulong pizzaIdVal, ServerRpcParams rpcParams = default)
        {
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;

            var pizzaId = new ItemId(pizzaIdVal);
            if (root.DomainState.Items.TryGetValue(pizzaId, out var pizza))
            {
                // Permissive packaging: can package raw, baked or burned!
                pizza.PackagingState = PackagingState.Boxed;
                pizza.Category = ItemCategory.Box;
                pizza.Revision++;
                root.DomainState.IncrementRevision();
                CommitAndReplicate();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void ConfirmStageBoxServerRpc(ulong boxIdVal, int stagingSlotIndex, int chosenDestinationId, ServerRpcParams rpcParams = default)
        {
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;

            var boxId = new ItemId(boxIdVal);
            if (root.DomainState.Items.TryGetValue(boxId, out var box))
            {
                box.LabelDestinationId = chosenDestinationId;
                box.Location = LocationRef.InStaging(stagingSlotIndex);
                box.Revision++;
                root.DomainState.IncrementRevision();
                CommitAndReplicate();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void AnswerPhoneServerRpc(int phoneStationId, ServerRpcParams rpcParams = default)
        {
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;

            // Answering the phone immediately publishes a multi-pizza order!
            // Pick a destination (e.g. 4 for testing, or from catalog)
            int destId = 4;
            var recipes = new List<(int recipeId, int qty)> { (1 /* Mussarela */, 1), (2 /* Calabresa */, 1) };
            var res = root.OrderService.CreatePhoneOrder(destId, recipes, root.DomainState, root.Catalog, root.Clock);
            if (res.Success)
            {
                CommitAndReplicate();
                Debug.Log($"[PizzeriaCommandHandler] Telefone atendido! Pedido #{res.Value.Value} publicado para o destino #{destId}.");
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void PurchaseSupplyServerRpc(int supplyItemId, int packQuantity, ServerRpcParams rpcParams = default)
        {
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;

            if (!root.Catalog.SupplyItems.TryGetValue(supplyItemId, out var supData)) return;

            int totalCost = supData.PackPrice * packQuantity;
            if (root.DomainState.RestaurantBudget < totalCost)
            {
                Debug.LogWarning("[PizzeriaCommandHandler] Saldo insuficiente para compra de suprimentos.");
                return;
            }

            root.DomainState.RestaurantBudget -= totalCost;
            var orderId = root.DomainState.GenerateSupplyOrderId();
            double now = root.Clock.Now;
            var supOrder = new SupplyOrderState(orderId, now, now + supData.BaseETA, totalCost);
            supOrder.AddLine(supplyItemId, supData.IngredientId, supData.UnitsPerPack, packQuantity);

            root.DomainState.SupplyOrders[orderId] = supOrder;
            root.DomainState.IncrementRevision();
            CommitAndReplicate();
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitDeliveryServerRpc(int destinationId, ulong[] selectedItemIds, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;

            var state = root.DomainState;
            var order = root.OrderService.GetActiveOrderForDestination(destinationId, state);

            var deliveredList = new List<ItemState>();
            if (selectedItemIds != null)
            {
                for (int i = 0; i < selectedItemIds.Length; i++)
                {
                    if (state.Items.TryGetValue(new ItemId(selectedItemIds[i]), out var item))
                    {
                        deliveredList.Add(item);
                    }
                }
            }

            var eval = root.DeliveryEvaluator.EvaluateDelivery(order, deliveredList, root.Catalog, root.Config.burnedPayoutFactor, root.Config.unbakedPayoutFactor, root.Config.restaurantBaseBonus);

            // Destroy delivered items (they have been consumed by customer)
            for (int i = 0; i < deliveredList.Count; i++)
            {
                root.TransferService.DestroyItem(deliveredList[i].Id);
            }

            // Credit Restaurant Budget
            state.RestaurantBudget += eval.RestaurantEarned;

            // Credit Player Personal Money (via PlayerState if found)
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.TryGetValue(senderClientId, out var client))
            {
                if (client.PlayerObject != null)
                {
                    var playerState = client.PlayerObject.GetComponent<PlayerState>();
                    if (playerState != null)
                    {
                        playerState.Money.Value += eval.PersonalEarned;
                    }
                }
            }

            CommitAndReplicate();
            Debug.Log($"[PizzeriaCommandHandler] Entrega concluída no destino #{destinationId}. Pessoal: +R${eval.PersonalEarned}, Restaurante: +R${eval.RestaurantEarned}.");
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetUpgradeLevelServerRpc(int upgradeId, int level, ServerRpcParams rpcParams = default)
        {
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;

            var res = root.UpgradeService.SetUpgradeLevel(upgradeId, level, root.DomainState, root.Catalog);
            if (res.Success)
            {
                CommitAndReplicate();
            }
        }
    }
}
