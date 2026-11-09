using Unity.Netcode;
using UnityEngine;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Network
{
    public class OrderCommands : NetworkBehaviour
    {
        public static OrderCommands Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            PizzeriaSession.Orders = this;
        }

        public override void OnNetworkDespawn()
        {
            if (PizzeriaSession.Orders == this)
                PizzeriaSession.Orders = null;
        }

        [ServerRpc(RequireOwnership = false)]
        public void AnswerCallServerRpc(ulong callIdVal, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            var callId = new CallId(callIdVal);
            var res = root.CallService.AnswerCall(
                root.DomainState, callId, root.OrderService, root.Catalog, root.Clock, root.Config.maxActiveOrders, root.Config.deliveryTimeLimit);

            if (!res.Success)
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, res.Message, true);
                return;
            }

            PizzeriaCommandHandler.Instance?.CommitAndReplicate();
            PizzeriaSession.Notifier?.NotifyClient(sender, "Chamada atendida! Novo pedido registrado na comanda.");
        }

        [ServerRpc(RequireOwnership = false)]
        public void ArchiveTicketDirectServerRpc(ulong orderIdVal, ServerRpcParams rpcParams = default)
        {
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            var orderId = new OrderId(orderIdVal);
            root.OrderService.ArchiveTicket(orderId, root.DomainState);
            PizzeriaCommandHandler.Instance?.CommitAndReplicate();
        }

        [ServerRpc(RequireOwnership = false)]
        public void BeginArchiveTicketHoldServerRpc(ulong orderIdVal, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            var orderId = new OrderId(orderIdVal);
            var res = root.PlayerHoldService.BeginArchiveTicket(
                root.DomainState, sender, orderId, root.Config.archiveHoldDuration, (float)root.Clock.Now, root.Config.workLeaseSeconds);

            if (!res.Success)
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, res.Message, true);
                return;
            }

            PizzeriaCommandHandler.Instance?.CommitAndReplicate();
        }

        [ServerRpc(RequireOwnership = false)]
        public void HeartbeatArchiveTicketHoldServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            root.PlayerHoldService.Heartbeat(root.DomainState, sender, (float)root.Clock.Now, root.Config.workLeaseSeconds);
        }

        [ServerRpc(RequireOwnership = false)]
        public void CancelArchiveTicketHoldServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            root.PlayerHoldService.Cancel(root.DomainState, sender);
            PizzeriaCommandHandler.Instance?.CommitAndReplicate();
        }

        [ServerRpc(RequireOwnership = false)]
        public void RestoreTicketServerRpc(ulong orderIdVal, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            var orderId = new OrderId(orderIdVal);
            var res = root.OrderService.RestoreTicket(orderId, root.DomainState);
            if (!res.Success)
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, res.Message, true);
                return;
            }

            PizzeriaCommandHandler.Instance?.CommitAndReplicate();
            PizzeriaSession.Notifier?.NotifyClient(sender, "Comanda restaurada para o quadro!");
        }

        [ServerRpc(RequireOwnership = false)]
        public void CancelOrderServerRpc(ulong orderIdVal, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var root = PizzeriaRoot.Instance;
            if (root == null || !root.IsServer || root.DomainState == null) return;

            var orderId = new OrderId(orderIdVal);
            var res = root.OrderService.CancelOrder(orderId, root.DomainState);
            if (!res.Success)
            {
                PizzeriaSession.Notifier?.NotifyClient(sender, res.Message, true);
                return;
            }

            PizzeriaCommandHandler.Instance?.CommitAndReplicate();
            PizzeriaSession.Notifier?.BroadcastFeedback("Um pedido foi cancelado.", true);
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }
    }
}
