using UnityEngine;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Stations
{
    public class StagingStation : StationView
    {
        public override string InteractPrompt => "[E] Etiquetar Caixa em " + stationName;

        public override void OnInteract(GameObject interactor)
        {
            var netState = PizzeriaNetworkState.Instance;
            if (netState == null) return;

            bool foundBox = false;
            for (int i = 0; i < netState.Items.Count; i++)
            {
                var item = netState.Items[i];
                if (item.LocationType == (byte)LocationType.StationSlot && item.HolderId == (ulong)stationId && item.PackagingState == (byte)PackagingState.Boxed)
                {
                    foundBox = true;
                    if (ScaryParty.Pizzeria.Presentation.PizzeriaHudBuilder.Instance != null)
                    {
                        ScaryParty.Pizzeria.Presentation.PizzeriaHudBuilder.Instance.OpenStagingAddressPicker(item.ItemId, item.SlotId);
                        return;
                    }
                }
            }

            if (!foundBox)
            {
                TryPickOrPlace(interactor);
            }
        }
    }
}
