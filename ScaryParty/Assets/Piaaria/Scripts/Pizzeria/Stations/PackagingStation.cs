using UnityEngine;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Stations
{
    public class PackagingStation : StationView
    {
        public override string InteractPrompt
        {
            get
            {
                int slotIdx = ResolveTargetSlotIndex();
                if (slotIdx < 0) return string.Empty;

                var netState = PizzeriaNetworkState.Instance;
                if (netState != null)
                {
                    for (int i = 0; i < netState.Items.Count; i++)
                    {
                        var item = netState.Items[i];
                        if (item.LocationType == (byte)LocationType.StationSlot && item.HolderId == (ulong)stationId && item.SlotId == slotIdx)
                        {
                            bool canPackage = item.Category == (byte)ItemCategory.PizzaBase || item.Category == (byte)ItemCategory.Pizza;
                            if (canPackage && item.PackagingState != (byte)PackagingState.Boxed)
                                return "[F] Embalar Pizza / [E] Pegar";
                            if (item.PackagingState == (byte)PackagingState.Boxed)
                                return "[E] Pegar Caixa";
                        }
                    }
                }
                return base.InteractPrompt;
            }
        }

        protected override void Start()
        {
            base.Start();
            stationName = "Bancada de Embalagem";
        }
    }
}
