using UnityEngine;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Stations
{
    public class ReceivingStation : StationView
    {
        public override string InteractPrompt => "[E] Descarregar Suprimentos em " + stationName;

        public override void OnInteract(GameObject interactor)
        {
            PizzeriaCommandHandler.Instance?.UnloadArrivedSuppliesServerRpc(stationId);
        }
    }
}
