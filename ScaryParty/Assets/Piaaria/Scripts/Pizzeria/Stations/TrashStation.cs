using UnityEngine;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Stations
{    public class TrashStation : StationView
    {
        public override string InteractPrompt => "[F] Segurar para Descartar em " + stationName;
    }}
