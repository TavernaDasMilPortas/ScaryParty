using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Presentation;

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
            if (!CanAccessBackpack(root.DomainState, bpId, sender, false)) return;

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

            if (!PlayerContextResolver.TryGetPlayer(sender, out var player, out _)) return;
            BackpackState equipped = null;
            foreach (var bag in root.DomainState.Backpacks.Values)
                if (bag.EquippedBy == sender) { equipped = bag; break; }
            if (equipped == null)
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, "Nenhuma mochila equipada.", true);
                return;
            }
            if (!TryFindSafeBagPlacement(player.transform, equipped.Id, out var playerPos))
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, "Não há superfície segura para largar a mochila.", true);
                return;
            }
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

        public static bool TryFindSafeBagPlacement(Transform player, BackpackId bagId, out Vector3 position)
        {
            position = default;
            if (player == null) return false;
            var authoredBags = FindObjectsByType<ScaryParty.Pizzeria.Authoring.DeliveryBagAuthoring>(FindObjectsSortMode.None);
            Collider bagCollider = null;
            foreach (var authoredBag in authoredBags)
                if (authoredBag.BackpackId == bagId.Value) { bagCollider = authoredBag.InteractionCollider; break; }
            if (bagCollider == null) return false;
            if (!(bagCollider is BoxCollider box)) return false;
            Vector3 scale = bagCollider.transform.lossyScale;
            Vector3 extents = Vector3.Scale(box.size * 0.5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            extents = new Vector3(Mathf.Max(0.2f, extents.x), Mathf.Max(0.25f, extents.y), Mathf.Max(0.2f, extents.z));
            Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            float distance = Mathf.Max(0.9f, extents.magnitude + 0.35f);
            Vector3[] offsets = { forward * distance, side * distance, -side * distance, -forward * distance };
            int mask = ~LayerMask.GetMask("Ignore Raycast", "Player");
            foreach (var offset in offsets)
            {
                Vector3 candidate = player.position + offset;
                if (!Physics.Raycast(candidate + Vector3.up * 1.5f, Vector3.down, out var ground, 4f, mask, QueryTriggerInteraction.Ignore) ||
                    ground.transform.IsChildOf(player) || Vector3.Dot(ground.normal, Vector3.up) < 0.7f)
                    continue;
                Vector3 center = ground.point + Vector3.up * extents.y;
                bool blocked = false;
                foreach (var collider in Physics.OverlapBox(center, extents * 0.95f, Quaternion.identity, mask, QueryTriggerInteraction.Ignore))
                {
                    if (collider == bagCollider || collider.transform.IsChildOf(player)) continue;
                    blocked = true;
                    break;
                }
                if (blocked) continue;
                position = ground.point;
                return true;
            }
            return false;
        }

        [ServerRpc(RequireOwnership = false)]
        public void StoreBackpackItemServerRpc(ulong backpackIdVal, ulong boxItemIdVal, HandSlotIndex hand, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            var bpId = new BackpackId(backpackIdVal);
            var boxId = new ItemId(boxItemIdVal);
            if (hand != HandSlotIndex.Left && hand != HandSlotIndex.Right) return;
            var handLoc = LocationRef.InHand(sender, hand);

            if (!CanAccessBackpack(root.DomainState, bpId, sender, true)) return;

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
            if (hand != HandSlotIndex.Left && hand != HandSlotIndex.Right) return;
            var handLoc = LocationRef.InHand(sender, hand);

            if (TransferService.IsLocationOccupied(root.DomainState, handLoc))
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, "A sua mão está ocupada.", true);
                return;
            }

            if (!CanAccessBackpack(root.DomainState, bpId, sender, true)) return;

            var res = BackpackService.RetrieveItem(root.DomainState, bpId, boxId, handLoc);
            if (!res.Success)
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, res.Message, true);
                return;
            }

            PizzeriaCommandHandler.Instance?.CommitAndReplicate();
        }

        private static bool CanAccessBackpack(PizzeriaState state, BackpackId id, ulong sender, bool allowEquipped)
        {
            if (!state.Backpacks.TryGetValue(id, out var bag))
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, "Mochila desconhecida.", true);
                return false;
            }
            if (allowEquipped && bag.IsEquipped && bag.EquippedBy == sender) return true;
            if (bag.IsEquipped)
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, "Mochila já equipada por outro jogador.", true);
                return false;
            }
            var pos = bag.WorldPos;
            if (InteractionValidator.ValidateDistanceAndSight(sender, new Vector3(pos.X, pos.Y, pos.Z))) return true;
            PizzeriaSession.Notifier?.NotifyClient(sender, "Mochila fora de alcance.", true);
            return false;
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

            // Credita dinheiro pessoal no PlayerState do jogador
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.TryGetValue(sender, out var client))
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

            PizzeriaCommandHandler.Instance?.CommitAndReplicate();

            string feedbackMsg = $"Entrega realizada no {AddressFormat.Display(addressIdVal)}! Ganho pessoal: R${eval.PersonalEarned}. Bônus da pizzaria: R${eval.RestaurantEarned}.";
            PizzeriaSession.Notifier?.NotifyClient(sender, feedbackMsg);
            PizzeriaSession.Notifier?.BroadcastFeedback($"Entrega concluída no {AddressFormat.Display(addressIdVal)}.");

            // Exibe painel detalhado de resultado para o cliente solicitante
            var clientParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { sender } } };
            DeliveryResultClientRpc(
                addressIdVal,
                eval.DeliveredCount,
                eval.CorrectBakedCount,
                eval.BurnedCount,
                eval.WrongOrIncompleteCount,
                eval.PersonalEarned,
                eval.RestaurantEarned,
                eval.OrderId != Domain.Types.OrderId.None,
                clientParams);

            // Dispara feedback visual e sonoro no endereço
            DeliveryFeedbackClientRpc(addressIdVal);
        }

        [ClientRpc]
        private void DeliveryResultClientRpc(
            int addressIdVal,
            int deliveredCount,
            int correctCount,
            int burnedCount,
            int wrongCount,
            int personalEarned,
            int restaurantEarned,
            bool orderCompleted,
            ClientRpcParams rpcParams = default)
        {
            if (DeliveryResultPresenter.Instance != null)
            {
                DeliveryResultPresenter.Instance.Show(
                    addressIdVal,
                    deliveredCount,
                    correctCount,
                    burnedCount,
                    wrongCount,
                    personalEarned,
                    restaurantEarned,
                    orderCompleted);
            }
        }

        [ClientRpc]
        private void DeliveryFeedbackClientRpc(int addressIdVal)
        {
            DeliveryPointView.PlayDeliveryFeedbackForAddress(addressIdVal);
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }
    }
}
