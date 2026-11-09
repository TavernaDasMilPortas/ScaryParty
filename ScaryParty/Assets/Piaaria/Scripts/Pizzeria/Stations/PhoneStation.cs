using UnityEngine;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Stations
{
    public class PhoneStation : StationView
    {
        [Header("Telefone")]
        public PhoneRinger ringer;

        public bool isRinging = false;
        private ulong _currentCallId = 0;

        public override string InteractPrompt 
        {
            get
            {
                if (isRinging)
                {
                    return "[E] Atender Telefone (Chamada Recebida!)";
                }
                return "[E] Telefone (Sem chamadas ativas)";
            }
        }

        protected override void Start()
        {
            base.Start();
            if (ringer == null)
            {
                ringer = GetComponent<PhoneRinger>();
                if (ringer == null)
                {
                    ringer = gameObject.AddComponent<PhoneRinger>();
                }
            }
        }

        protected override void Update()
        {
            base.Update();

            var netState = PizzeriaNetworkState.Instance;
            if (netState == null || netState.ActiveCalls == null)
            {
                UpdateRingingState(false, 0);
                return;
            }

            if (netState.ActiveCalls.Count > 0)
            {
                var firstCall = netState.ActiveCalls[0];
                UpdateRingingState(true, firstCall.CallId);
            }
            else
            {
                UpdateRingingState(false, 0);
            }
        }

        private void UpdateRingingState(bool ringing, ulong callId)
        {
            isRinging = ringing;
            _currentCallId = callId;

            if (ringer != null)
            {
                ringer.SetRinging(ringing);
            }
        }

        public override void OnInteract(GameObject interactor)
        {
            if (!isRinging || _currentCallId == 0)
            {
                Presentation.NotificationManager.Show("Nenhuma chamada ativa para atender no momento.", Presentation.NotificationType.Warning);
                return;
            }

            if (OrderCommands.Instance != null)
            {
                OrderCommands.Instance.AnswerCallServerRpc(_currentCallId);
            }
            else if (PizzeriaCommandHandler.Instance != null)
            {
                PizzeriaCommandHandler.Instance.AnswerPhoneServerRpc(stationId);
            }

            // Parar som de imediato no cliente local para resposta tátil instantânea
            if (ringer != null)
            {
                ringer.SetRinging(false);
            }
        }
    }
}
