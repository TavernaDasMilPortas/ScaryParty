using UnityEngine;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Stations
{    public class PrepStation : StationView
    {
        [Header("Preparo")]
        public int defaultProcessId = 1;

        public override string InteractPrompt => "[F] Segurar para Trabalhar em " + stationName;
    }}
