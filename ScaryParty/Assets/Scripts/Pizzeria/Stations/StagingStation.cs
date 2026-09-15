using UnityEngine;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Stations
{    public class StagingStation : StationView
    {
        public override string InteractPrompt => "[E] Etiquetar Caixa em " + stationName;

        public override void OnInteract(GameObject interactor)
        {
            var netState = PizzeriaNetworkState.Instance;
            if (netState == null) return;

            for (int i = 0; i < netState.Items.Count; i++)
            {
                var item = netState.Items[i];
                if (item.LocationType == (byte)LocationType.StagingSlot && item.PackagingState == (byte)PackagingState.Boxed)
                {
                    if (ScaryParty.Pizzeria.Presentation.PizzeriaHudBuilder.Instance != null)
                    {
                        ScaryParty.Pizzeria.Presentation.PizzeriaHudBuilder.Instance.OpenStagingAddressPicker(item.ItemId, item.SlotId);
                        return;
                    }
                }
            }
            Debug.Log("[StagingStation] Nenhuma caixa na bancada de retirada para etiquetar.");
        }
    }}
