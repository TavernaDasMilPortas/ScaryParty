using System;
using Unity.Netcode;
using UnityEngine;

namespace ScaryParty.Pizzeria.Network
{
    public class PizzeriaNotifier : NetworkBehaviour
    {
        public static PizzeriaNotifier Instance { get; private set; }

        public event Action<string, bool> OnFeedbackReceived;

        private void Awake()
        {
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            PizzeriaSession.Notifier = this;
        }

        public override void OnNetworkDespawn()
        {
            if (PizzeriaSession.Notifier == this)
                PizzeriaSession.Notifier = null;
        }

        public void NotifyClient(ulong clientId, string message, bool isError = false)
        {
            if (!IsServer) return;

            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            };

            NotifyFeedbackClientRpc(message, isError, rpcParams);
        }

        public void BroadcastFeedback(string message, bool isError = false)
        {
            if (!IsServer) return;
            NotifyFeedbackClientRpc(message, isError);
        }

        [ClientRpc]
        public void NotifyFeedbackClientRpc(string message, bool isError, ClientRpcParams clientRpcParams = default)
        {
            OnFeedbackReceived?.Invoke(message, isError);
            if (isError)
            {
                Debug.LogWarning($"[Pizzeria Feedback] {message}");
            }
            else
            {
                Debug.Log($"[Pizzeria Feedback] {message}");
            }
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }
    }
}
