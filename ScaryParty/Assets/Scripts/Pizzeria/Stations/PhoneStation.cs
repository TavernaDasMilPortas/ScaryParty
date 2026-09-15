using UnityEngine;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Stations
{    public class PhoneStation : StationView
    {
        public bool isRinging = false;
        public override string InteractPrompt => isRinging ? "[E] Atender Telefone!" : "[E] Telefone (Sem chamadas)";

        public override void OnInteract(GameObject interactor)
        {
            var cmd = PizzeriaCommandHandler.Instance;
            if (cmd != null)
            {
                cmd.AnswerPhoneServerRpc(stationId);
            }
        }
    }}
