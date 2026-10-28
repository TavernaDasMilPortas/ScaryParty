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
                var netState = PizzeriaNetworkState.Instance;
                if (netState != null)
                {
                    int targetSlot = ResolveTargetSlotIndex();
                    // Priorizar caixa no slot mirado
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
                    // Fallback para qualquer caixa na bancada
                    for (int i = 0; i < netState.Items.Count; i++)
                    {
                        var item = netState.Items[i];
                        if (item.LocationType == (byte)LocationType.StationSlot && item.HolderId == (ulong)stationId && item.PackagingState == (byte)PackagingState.Boxed)
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

            // 1. Verificar primeiro o slot mirado
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

            // 2. Fallback para qualquer caixa na bancada se não estiver colocando outra coisa
            if (LastAimExplicitSlot < 0)
            {
                for (int i = 0; i < netState.Items.Count; i++)
                {
                    var item = netState.Items[i];
                    if (item.LocationType == (byte)LocationType.StationSlot && item.HolderId == (ulong)stationId && item.PackagingState == (byte)PackagingState.Boxed)
                    {
                        if (item.LabelDestinationId < 0)
                        {
                            if (ScaryParty.Pizzeria.Presentation.PizzeriaHudBuilder.Instance != null)
                            {
                                ScaryParty.Pizzeria.Presentation.PizzeriaHudBuilder.Instance.OpenStagingAddressPicker(item.ItemId, item.SlotId);
                                return;
                            }
                        }
                    }
                }
            }

            TryPickOrPlace(interactor);
        }
    }
}
