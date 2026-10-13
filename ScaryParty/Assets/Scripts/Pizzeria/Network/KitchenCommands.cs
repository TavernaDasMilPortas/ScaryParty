using Unity.Netcode;
using UnityEngine;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Network
{
    public class KitchenCommands : NetworkBehaviour
    {
        public static KitchenCommands Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            PizzeriaSession.Kitchen = this;
        }

        public override void OnNetworkDespawn()
        {
            if (PizzeriaSession.Kitchen == this)
                PizzeriaSession.Kitchen = null;
        }

        [ServerRpc(RequireOwnership = false)]
        public void DispenseIngredientServerRpc(int storageId, int ingredientDefId, HandSlotIndex hand, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            // 1. Validar jogador e posição
            Vector3 playerPos = PlayerContextResolver.GetPlayerPosition(sender);
            if (StationRegistry.TryGetStation(storageId, out var storageTrans))
            {
                if (!InteractionValidator.ValidateDistanceAndSight(sender, storageTrans.position, root.Config.interactDistance, root.Config.serverDistanceTolerance))
                {
                    PizzeriaSession.Notifier?.NotifyClient(sender, "Você está muito longe do armazenamento.", true);
                    return;
                }
            }

            // 2. Validar mão explícita (sem fallback silencioso)
            var targetHand = LocationRef.InHand(sender, hand);
            if (TransferService.IsLocationOccupied(root.DomainState, targetHand))
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, "A mão selecionada já está ocupada.", true);
                return;
            }

            // 3. Executar no domínio
            var res = TransferService.DispenseFromStorage(root.DomainState, root.Catalog, storageId, ingredientDefId, sender, hand);
            if (!res.Success)
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, res.Message, true);
                return;
            }

            // 4. Commit e Notificação
            CommitAndReplicate();
        }

        [ServerRpc(RequireOwnership = false)]
        public void TransferItemServerRpc(ulong itemIdVal, LocationRef destination, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            var itemId = new ItemId(itemIdVal);
            if (!root.DomainState.Items.TryGetValue(itemId, out var item))
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, "Item não encontrado.", true);
                return;
            }

            // Validação de proximidade se o destino for estação
            if (destination.Type == LocationType.StationSlot)
            {
                if (StationRegistry.TryGetStation((int)destination.HolderId, out var stationTrans))
                {
                    if (!InteractionValidator.ValidateDistanceAndSight(sender, stationTrans.position, root.Config.interactDistance, root.Config.serverDistanceTolerance))
                    {
                        PizzeriaSession.Notifier?.NotifyClient(sender, "Muito longe da bancada de destino.", true);
                        return;
                    }
                }
            }

            var res = TransferService.TransferItem(root.DomainState, itemId, destination, sender);
            if (!res.Success)
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, res.Message, true);
                return;
            }

            CommitAndReplicate();
        }

        [ServerRpc(RequireOwnership = false)]
        public void TransferToolServerRpc(ulong toolIdVal, LocationRef destination, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            var toolId = new ToolItemId(toolIdVal);
            var res = TransferService.TransferTool(root.DomainState, toolId, destination, sender, root.Clock.Now);
            if (!res.Success)
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, res.Message, true);
                return;
            }

            CommitAndReplicate();
        }

        [ServerRpc(RequireOwnership = false)]
        public void AddIngredientToPizzaServerRpc(ulong pizzaIdVal, ulong ingredientIdVal, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            var pizzaId = new ItemId(pizzaIdVal);
            var ingId = new ItemId(ingredientIdVal);

            if (!root.DomainState.Items.TryGetValue(pizzaId, out var pizza) || !root.DomainState.Items.TryGetValue(ingId, out var ingredient))
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, "Item não encontrado.", true);
                return;
            }

            if (!pizza.CanAddIngredient(ingredient.DefinitionId))
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, "Não é possível adicionar ingredientes a esta pizza.", true);
                return;
            }

            bool added = pizza.AddIngredient(ingredient.DefinitionId, ingredient.PrepStage, 1);
            if (added)
            {
                TransferService.DestroyItem(root.DomainState, ingId);
                CommitAndReplicate();
            }
            else
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, "Falha ao adicionar ingrediente.", true);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void BeginProcessServerRpc(int stationId, int slotIndex, int processId, ulong toolIdVal, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            if (StationRegistry.TryGetStation(stationId, out var stTrans))
            {
                if (!InteractionValidator.ValidateDistanceAndSight(sender, stTrans.position, root.Config.interactDistance, root.Config.serverDistanceTolerance))
                {
                    PizzeriaSession.Notifier?.NotifyClient(sender, "Muito longe da bancada.", true);
                    return;
                }
            }

            var slotId = new StationSlotId(stationId, slotIndex);
            var itemLoc = LocationRef.InStation(stationId, slotIndex);
            ItemId inputItemId = ItemId.None;
            foreach (var item in root.DomainState.Items.Values)
            {
                if (item.Location.Equals(itemLoc)) { inputItemId = item.Id; break; }
            }

            if (!inputItemId.IsValid)
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, "Bancada vazia.", true);
                return;
            }

            var toolId = new ToolItemId(toolIdVal);
            var res = root.ProcessingService.BeginOperation(
                root.DomainState, slotId, OperationKind.Process, ResumePolicy.Resumable,
                inputItemId, toolId, processId, sender, root.Catalog, (float)root.Clock.Now, root.Config.workLeaseSeconds);

            if (!res.Success)
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, res.Message, true);
                return;
            }

            CommitAndReplicate();
        }

        [ServerRpc(RequireOwnership = false)]
        public void HeartbeatProcessServerRpc(int stationId, int slotIndex, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            var slotId = new StationSlotId(stationId, slotIndex);
            root.ProcessingService.Heartbeat(root.DomainState, slotId, sender, (float)root.Clock.Now, root.Config.workLeaseSeconds);
        }

        [ServerRpc(RequireOwnership = false)]
        public void CancelProcessServerRpc(int stationId, int slotIndex, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            var slotId = new StationSlotId(stationId, slotIndex);
            root.ProcessingService.CancelOperation(root.DomainState, slotId, sender, (float)root.Clock.Now);
            CommitAndReplicate();
        }

        [ServerRpc(RequireOwnership = false)]
        public void InsertOvenServerRpc(ulong pizzaIdVal, int slotIndex, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            var slotId = new StationSlotId(1 /* Oven Station ID */, slotIndex);
            var res = root.OvenService.InsertPizza(root.DomainState, slotId, new ItemId(pizzaIdVal), sender, root.Catalog, root.Config.defaultBakeDuration);
            if (!res.Success)
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, res.Message, true);
                return;
            }

            CommitAndReplicate();
        }

        [ServerRpc(RequireOwnership = false)]
        public void RemoveOvenServerRpc(int slotIndex, HandSlotIndex targetHand, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            var slotId = new StationSlotId(1, slotIndex);
            var handLoc = LocationRef.InHand(sender, targetHand);
            if (TransferService.IsLocationOccupied(root.DomainState, handLoc))
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, "Sua mão está ocupada.", true);
                return;
            }

            var res = root.OvenService.RemovePizza(root.DomainState, slotId, handLoc, sender);
            if (!res.Success)
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, res.Message, true);
                return;
            }

            CommitAndReplicate();
        }

        [ServerRpc(RequireOwnership = false)]
        public void BeginPackageServerRpc(ulong pizzaIdVal, int stationId, int slotIndex, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            var slotId = new StationSlotId(stationId, slotIndex);
            var pizzaId = new ItemId(pizzaIdVal);

            var res = root.ProcessingService.BeginOperation(
                root.DomainState, slotId, OperationKind.Package, ResumePolicy.ResetOnCancel,
                pizzaId, ToolItemId.None, 0, sender, root.Catalog, (float)root.Clock.Now, root.Config.workLeaseSeconds, root.Config.packagingDuration);

            if (!res.Success)
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, res.Message, true);
                return;
            }

            CommitAndReplicate();
        }

        [ServerRpc(RequireOwnership = false)]
        public void BeginDiscardHoldServerRpc(ulong itemIdVal, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            var res = root.PlayerHoldService.BeginDiscard(
                root.DomainState, sender, new ItemId(itemIdVal), root.Config.discardDuration, (float)root.Clock.Now, root.Config.workLeaseSeconds);

            if (!res.Success)
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, res.Message, true);
                return;
            }

            CommitAndReplicate();
        }

        [ServerRpc(RequireOwnership = false)]
        public void HeartbeatDiscardHoldServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            root.PlayerHoldService.Heartbeat(root.DomainState, sender, (float)root.Clock.Now, root.Config.workLeaseSeconds);
        }

        [ServerRpc(RequireOwnership = false)]
        public void CancelDiscardHoldServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            root.PlayerHoldService.Cancel(root.DomainState, sender);
            CommitAndReplicate();
        }

        private void CommitAndReplicate()
        {
            PizzeriaCommandHandler.Instance?.CommitAndReplicate();
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }
    }
}
