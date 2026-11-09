using UnityEngine;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Stations
{    public class SupplierTerminal : StationView
    {
        public override string InteractPrompt => "[E] Acessar " + stationName;

        public override void OnInteract(GameObject interactor)
        {
            Debug.Log("[SupplierTerminal] Abrindo catálogo de fornecedores.");
        }
    }}
