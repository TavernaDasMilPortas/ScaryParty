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
        public NetworkList<NetOrderLineDto> OrderLines;
        public NetworkList<NetCallDto> Calls;
        public NetworkList<NetCallDto> ActiveCalls => Calls;
        public NetworkList<NetStackDto> Stacks;
        public NetworkList<NetBackpackDto> Backpacks;
        public NetworkList<NetSupplyDto> SupplyOrders;
        public NetworkList<NetUpgradeDto> Upgrades;
        public NetworkList<NetCustomerDto> Customers;
        public NetworkList<NetTableDto> Tables;
        public NetworkList<NetWeaponStateDto> Weapons;
        public NetworkList<NetPlayerCombatDto> PlayerCombatStates;

        public NetworkVariable<NetNightStateDto> NightState = new NetworkVariable<NetNightStateDto>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<NetMotorcycleDto> MotorcycleState = new NetworkVariable<NetMotorcycleDto>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<int> RestaurantBudget = new NetworkVariable<int>(
            500, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<uint> PublishedRevision = new NetworkVariable<uint>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<int> CatalogHash = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<int> AddressRegistryHash = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private void Awake()
        {
            Instance = this;
            Items = new NetworkList<NetItemDto>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            Tools = new NetworkList<NetToolDto>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            StorageSlots = new NetworkList<NetStorageDto>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            StationSlots = new NetworkList<NetStationSlotDto>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            Orders = new NetworkList<NetOrderDto>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            OrderLines = new NetworkList<NetOrderLineDto>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            Calls = new NetworkList<NetCallDto>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            Stacks = new NetworkList<NetStackDto>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            Backpacks = new NetworkList<NetBackpackDto>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            SupplyOrders = new NetworkList<NetSupplyDto>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            Upgrades = new NetworkList<NetUpgradeDto>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            Customers = new NetworkList<NetCustomerDto>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            Tables = new NetworkList<NetTableDto>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            Weapons = new NetworkList<NetWeaponStateDto>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            PlayerCombatStates = new NetworkList<NetPlayerCombatDto>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        }

        public bool IsHandOccupiedByWeapon(ulong playerId, int slotId)
        {
            if (Weapons == null) return false;
            for (int i = 0; i < Weapons.Count; i++)
            {
                var w = Weapons[i];
                if (w.IsAvailableOnRack || w.OwnerClientId != playerId) continue;
                if (w.WeaponType == 2) return true; // Shotgun (2 maos)
                if (w.WeaponType == 1 && slotId == (int)ScaryParty.Pizzeria.Domain.Types.HandSlotIndex.Right) return true; // Pistola na mao direita
            }
            return false;
        }

        public bool TryGetEquippedWeapon(ulong playerId, out NetWeaponStateDto weapon)
        {
            if (Weapons != null)
            {
                for (int i = 0; i < Weapons.Count; i++)
                {
                    if (!Weapons[i].IsAvailableOnRack && Weapons[i].OwnerClientId == playerId)
                    {
                        weapon = Weapons[i];
                        return true;
                    }
                }
            }
            weapon = default;
            return false;
        }

        public bool TryGetPlayerCombatState(ulong playerId, out NetPlayerCombatDto combatState)
        {
            if (PlayerCombatStates != null)
            {
                for (int i = 0; i < PlayerCombatStates.Count; i++)
                {
                    if (PlayerCombatStates[i].ClientId == playerId)
                    {
                        combatState = PlayerCombatStates[i];
                        return true;
                    }
                }
            }
            combatState = default;
            return false;
        }

        public bool IsHandOccupied(ulong playerId, int slotId)
        {
            if (IsHandOccupiedByWeapon(playerId, slotId))
                return true;

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
