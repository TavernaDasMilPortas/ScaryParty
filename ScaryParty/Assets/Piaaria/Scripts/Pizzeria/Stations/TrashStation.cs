using UnityEngine;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Stations
{    public class TrashStation : StationView
    {
        public override string InteractPrompt => "[F] Segurar para Descartar em " + stationName;

        public override void OnInteract(GameObject interactor)
        {
            ScaryParty.Pizzeria.Presentation.NotificationManager.Show(
                "Segure [F] mirando na lixeira para descartar o item da mão.",
                ScaryParty.Pizzeria.Presentation.NotificationType.Info);
        }
    }}
