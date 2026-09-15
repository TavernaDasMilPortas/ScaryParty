using UnityEngine;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Stations
{    public class PackagingStation : StationView
    {
        public override string InteractPrompt => "[F] Segurar para Embalar em " + stationName;
    }}
