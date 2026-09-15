using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;

namespace ScaryParty.Pizzeria.Integration
{
    public static class DeliveryPointAdapter
    {
        public static bool TrySubmitDelivery(int pointIndex)
        {
            var netState = PizzeriaNetworkState.Instance;
            var cmd = PizzeriaCommandHandler.Instance;
            if (netState == null || cmd == null || NetworkManager.Singleton == null) return false;

            ulong myId = NetworkManager.Singleton.LocalClientId;
            var carriedBoxes = new List<ulong>();

            // Collect all boxes carried by the local player (hands or backpack)
            for (int i = 0; i < netState.Items.Count; i++)
            {
                var item = netState.Items[i];
                bool isCarriedByMe = item.HolderId == myId &&
                    (item.LocationType == (byte)LocationType.Hand || item.LocationType == (byte)LocationType.Backpack);

                if (isCarriedByMe && item.PackagingState == (byte)PackagingState.Boxed)
                {
                    // By default submit boxes labeled for this point, or any carried box if none specifically labeled
                    carriedBoxes.Add(item.ItemId);
                }
            }

            if (carriedBoxes.Count == 0)
            {
                Debug.LogWarning("[DeliveryPointAdapter] Nenhuma caixa na mão ou mochila para entregar.");
                return false;
            }

            // Submit batch delivery to server
            cmd.SubmitDeliveryServerRpc(pointIndex, carriedBoxes.ToArray());
            return true;
        }
    }
}
