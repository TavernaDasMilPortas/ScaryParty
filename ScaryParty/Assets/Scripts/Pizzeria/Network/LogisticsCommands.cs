using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Network
{
    public class LogisticsCommands : NetworkBehaviour
    {
        public static LogisticsCommands Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            PizzeriaSession.Logistics = this;
        }

        public override void OnNetworkDespawn()
        {
            if (PizzeriaSession.Logistics == this)
                PizzeriaSession.Logistics = null;
        }

        [ServerRpc(RequireOwnership = false)]
        public void DepositStackServerRpc(ulong stackIdVal, ulong boxItemIdVal, HandSlotIndex hand, int addressIdVal, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            var stackId = new StackId(stackIdVal);
            var boxId = new ItemId(boxItemIdVal);
            var handLoc = LocationRef.InHand(sender, hand);
            var addressId = new AddressId(addressIdVal);

            var res = StackService.DepositStack(root.DomainState, stackId, boxId, handLoc, addressId);
            if (!res.Success)
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, res.Message, true);
                return;
            }

            PizzeriaCommandHandler.Instance?.CommitAndReplicate();
            PizzeriaSession.Notifier?.NotifyClient(sender, $"Caixa guardada na pilha ({AddressFormat.Display(addressIdVal)}).");
        }

        [ServerRpc(RequireOwnership = false)]
        public void TakeStackTopServerRpc(ulong stackIdVal, ulong expectedTopItemIdVal, HandSlotIndex hand, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            var stackId = new StackId(stackIdVal);
            var expectedTop = new ItemId(expectedTopItemIdVal);
            var handLoc = LocationRef.InHand(sender, hand);

            if (TransferService.IsLocationOccupied(root.DomainState, handLoc))
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, "A sua mão está ocupada.", true);
                return;
            }

            var res = StackService.TakeTop(root.DomainState, stackId, expectedTop, handLoc);
            if (!res.Success)
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, res.Message, true);
                return;
            }

            PizzeriaCommandHandler.Instance?.CommitAndReplicate();
        }

        [ServerRpc(RequireOwnership = false)]
        public void EquipBackpackServerRpc(ulong backpackIdVal, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            var bpId = new BackpackId(backpackIdVal);
            var res = BackpackService.Equip(root.DomainState, bpId, sender);
            if (!res.Success)
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, res.Message, true);
                return;
            }

            PizzeriaCommandHandler.Instance?.CommitAndReplicate();
            PizzeriaSession.Notifier?.NotifyClient(sender, "Mochila equipada nas costas!");
        }

        [ServerRpc(RequireOwnership = false)]
        public void DropBackpackServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            Vector3 playerPos = PlayerContextResolver.GetPlayerPosition(sender);
            var domainPos = new DomainVector3(playerPos.x, playerPos.y, playerPos.z);

            var res = BackpackService.Drop(root.DomainState, sender, domainPos, DomainQuaternion.Identity);
            if (!res.Success)
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, res.Message, true);
                return;
            }

            PizzeriaCommandHandler.Instance?.CommitAndReplicate();
            PizzeriaSession.Notifier?.NotifyClient(sender, "Mochila colocada no chão.");
        }

        [ServerRpc(RequireOwnership = false)]
        public void StoreBackpackItemServerRpc(ulong backpackIdVal, ulong boxItemIdVal, HandSlotIndex hand, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            var bpId = new BackpackId(backpackIdVal);
            var boxId = new ItemId(boxItemIdVal);
            var handLoc = LocationRef.InHand(sender, hand);

            var res = BackpackService.StoreItem(root.DomainState, bpId, boxId, handLoc);
            if (!res.Success)
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, res.Message, true);
                return;
            }

            PizzeriaCommandHandler.Instance?.CommitAndReplicate();
            PizzeriaSession.Notifier?.NotifyClient(sender, "Pizza guardada na mochila.");
        }

        [ServerRpc(RequireOwnership = false)]
        public void RetrieveBackpackItemServerRpc(ulong backpackIdVal, ulong boxItemIdVal, HandSlotIndex hand, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            var bpId = new BackpackId(backpackIdVal);
            var boxId = new ItemId(boxItemIdVal);
            var handLoc = LocationRef.InHand(sender, hand);

            if (TransferService.IsLocationOccupied(root.DomainState, handLoc))
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, "A sua mão está ocupada.", true);
                return;
            }

            var res = BackpackService.RetrieveItem(root.DomainState, bpId, boxId, handLoc);
            if (!res.Success)
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, res.Message, true);
                return;
            }

            PizzeriaCommandHandler.Instance?.CommitAndReplicate();
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitDeliveryServerRpc(int addressIdVal, ulong[] itemIds, ulong attemptId, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            var destination = new AddressId(addressIdVal);
            var deliveryService = new DeliveryService();

            var list = new List<ItemId>();
            if (itemIds != null)
            {
                for (int i = 0; i < itemIds.Length; i++) list.Add(new ItemId(itemIds[i]));
            }

            var res = deliveryService.SubmitDelivery(
                root.DomainState, destination, list, attemptId, sender, root.Catalog, root.OrderService);

            if (!res.Success)
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, res.Message, true);
                return;
            }

            var eval = res.Value;
            PizzeriaCommandHandler.Instance?.CommitAndReplicate();

            string feedbackMsg = $"Entrega realizada no {AddressFormat.Display(addressIdVal)}! Ganho pessoal: R${eval.PersonalEarned}. Bônus da pizzaria: R${eval.RestaurantEarned}.";
            PizzeriaSession.Notifier?.NotifyClient(sender, feedbackMsg);
            PizzeriaSession.Notifier?.BroadcastFeedback($"Entrega concluída no {AddressFormat.Display(addressIdVal)}.");
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }
    }
}
