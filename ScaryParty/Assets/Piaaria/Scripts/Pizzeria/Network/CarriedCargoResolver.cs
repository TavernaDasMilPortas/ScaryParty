using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Network
{
    public static class CarriedCargoResolver
    {
        public static bool TryGetEquippedBackpack(PizzeriaNetworkState state, ulong playerId, out NetBackpackDto backpack)
        {
            backpack = default;
            if (state == null || state.Backpacks == null) return false;
            for (int i = 0; i < state.Backpacks.Count; i++)
            {
                var candidate = state.Backpacks[i];
                if (candidate.EquippedBy != BackpackState.NoOwner && candidate.EquippedBy == playerId)
                {
                    backpack = candidate;
                    return true;
                }
            }
            return false;
        }

        public static bool IsCarried(NetItemDto item, ulong playerId, ulong equippedBackpackId, bool hasBackpack)
        {
            return (item.LocationType == (byte)LocationType.Hand && item.HolderId == playerId) ||
                   (hasBackpack && item.LocationType == (byte)LocationType.Backpack && item.HolderId == equippedBackpackId);
        }

        public static List<NetItemDto> GetCarriedItems(PizzeriaNetworkState state, ulong playerId)
        {
            var items = new List<NetItemDto>();
            if (state == null || state.Items == null) return items;
            bool hasBackpack = TryGetEquippedBackpack(state, playerId, out var backpack);
            for (int i = 0; i < state.Items.Count; i++)
            {
                var item = state.Items[i];
                if (IsCarried(item, playerId, backpack.BackpackId, hasBackpack)) items.Add(item);
            }
            return items;
        }
    }
}
