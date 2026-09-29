using Unity.Netcode;
using UnityEngine;

namespace ScaryParty.Pizzeria.Network
{
    public class PizzeriaNetworkState : NetworkBehaviour
    {
        public static PizzeriaNetworkState Instance { get; private set; }

        public NetworkList<NetItemDto> Items;
        public NetworkList<NetToolDto> Tools;
        public NetworkList<NetStorageDto> StorageSlots;
        public NetworkList<NetStationSlotDto> StationSlots;
        public NetworkList<NetOrderDto> Orders;
        public NetworkList<NetSupplyDto> SupplyOrders;
        public NetworkList<NetUpgradeDto> Upgrades;

        public NetworkVariable<int> RestaurantBudget = new NetworkVariable<int>(
            500, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<uint> PublishedRevision = new NetworkVariable<uint>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private void Awake()
        {
            Instance = this;
            Items = new NetworkList<NetItemDto>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            Tools = new NetworkList<NetToolDto>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            StorageSlots = new NetworkList<NetStorageDto>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            StationSlots = new NetworkList<NetStationSlotDto>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            Orders = new NetworkList<NetOrderDto>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            SupplyOrders = new NetworkList<NetSupplyDto>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            Upgrades = new NetworkList<NetUpgradeDto>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        }

        public bool IsHandOccupied(ulong playerId, int slotId)
        {
            if (Items != null)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    var it = Items[i];
                    if (it.LocationType == (byte)ScaryParty.Pizzeria.Domain.Types.LocationType.Hand &&
                        it.HolderId == playerId && it.SlotId == slotId)
                        return true;
                }
            }

            if (Tools != null)
            {
                for (int i = 0; i < Tools.Count; i++)
                {
                    var t = Tools[i];
                    if (t.LocationType == (byte)ScaryParty.Pizzeria.Domain.Types.LocationType.Hand &&
                        t.HolderId == playerId && t.SlotId == slotId)
                        return true;
                }
            }

            return false;
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }
    }
}
