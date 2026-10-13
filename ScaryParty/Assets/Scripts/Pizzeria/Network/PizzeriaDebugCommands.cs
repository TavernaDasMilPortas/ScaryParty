using Unity.Netcode;
using UnityEngine;
using ScaryParty.Pizzeria.Composition;

namespace ScaryParty.Pizzeria.Network
{
    public class PizzeriaDebugCommands : NetworkBehaviour
    {
        public static PizzeriaDebugCommands Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        [ServerRpc(RequireOwnership = false)]
        public void AddBudgetServerRpc(int amount, ServerRpcParams rpcParams = default)
        {
            if (!Debug.isDebugBuild && !Application.isEditor) return;
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;

            root.DomainState.RestaurantBudget += amount;
            PizzeriaCommandHandler.Instance?.CommitAndReplicate();
        }

        [ServerRpc(RequireOwnership = false)]
        public void TriggerPhoneCallServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!Debug.isDebugBuild && !Application.isEditor) return;
            var root = PizzeriaRoot.Instance;
            if (root == null || root.DomainState == null) return;

            var lines = new System.Collections.Generic.List<Domain.Models.OrderLineState>
            {
                new Domain.Models.OrderLineState(root.DomainState.GenerateOrderLineId(), 1, 1)
            };
            root.CallService.EnqueueCall(root.DomainState, new Domain.Types.AddressId(0), lines, (float)root.Clock.Now, root.Config.callExpireSeconds);
            PizzeriaCommandHandler.Instance?.CommitAndReplicate();
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }
    }
}
