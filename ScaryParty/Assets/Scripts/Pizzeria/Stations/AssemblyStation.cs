using UnityEngine;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Stations
{    public class AssemblyStation : StationView
    {
        public override string InteractPrompt => "[E] Montar Pizza em " + stationName;
    }}
