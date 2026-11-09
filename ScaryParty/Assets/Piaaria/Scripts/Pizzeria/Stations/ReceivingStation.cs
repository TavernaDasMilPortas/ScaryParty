using UnityEngine;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Stations
{    public class ReceivingStation : StationView
    {
        public override string InteractPrompt => "[E] Descarregar Crate em " + stationName;
    }}
