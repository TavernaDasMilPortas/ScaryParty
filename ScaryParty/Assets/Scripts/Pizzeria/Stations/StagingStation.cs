using UnityEngine;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Stations
{
    public class StagingStation : StationView
    {
        public override string InteractPrompt
        {
            get
            {
                int targetSlot = ResolveTargetSlotIndex();
                if (targetSlot < 0) return string.Empty;

                var netState = PizzeriaNetworkState.Instance;
                if (netState != null)
                {
                    for (int i = 0; i < netState.Items.Count; i++)
                    {
                        var item = netState.Items[i];
                        if (item.LocationType == (byte)LocationType.StationSlot && item.HolderId == (ulong)stationId && item.SlotId == targetSlot && item.PackagingState == (byte)PackagingState.Boxed)
                        {
                            if (item.LabelDestinationId >= 0)
                                return $"[E] Pegar Caixa ({AddressFormat.Display(item.LabelDestinationId)})";
                            else
                                return $"[E] Etiquetar Caixa em {stationName}";
                        }
                    }
                }
                return base.InteractPrompt;
            }
        }

        public override void OnInteract(GameObject interactor)
        {
            var netState = PizzeriaNetworkState.Instance;
            if (netState == null) return;

            int targetSlot = ResolveTargetSlotIndex();
            if (targetSlot < 0) return;

            for (int i = 0; i < netState.Items.Count; i++)
            {
                var item = netState.Items[i];
                if (item.LocationType == (byte)LocationType.StationSlot && item.HolderId == (ulong)stationId && item.SlotId == targetSlot && item.PackagingState == (byte)PackagingState.Boxed)
                {
                    if (item.LabelDestinationId < 0)
                    {
                        if (ScaryParty.Pizzeria.Presentation.PizzeriaHudBuilder.Instance != null)
                        {
                            ScaryParty.Pizzeria.Presentation.PizzeriaHudBuilder.Instance.OpenStagingAddressPicker(item.ItemId, item.SlotId);
                            return;
                        }
                    }
                    else
                    {
                        TryPickOrPlace(interactor);
                        return;
                    }
                }
            }

            TryPickOrPlace(interactor);
        }
    }
}
