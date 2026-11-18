using UnityEngine;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Stations
{
    public class SupplierTerminal : StationView
    {
        public override string InteractPrompt => "[E] Acessar Terminal de Suprimentos / Operações";

        public override void OnInteract(GameObject interactor)
        {
            ScaryParty.UI.Piaria.Presenters.OperationsHubPresenter.EnsureInstance().Open("supplies");
        }
    }
}
