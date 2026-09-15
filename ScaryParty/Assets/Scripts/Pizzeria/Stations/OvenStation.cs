using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Stations
{
    public class OvenStation : StationView
    {
        public override string InteractPrompt => "[E] Inserir/Retirar do " + stationName;

        public override void OnInteract(GameObject interactor)
        {
            var netObj = interactor.GetComponent<NetworkObject>();
            if (netObj == null) return;
            ulong playerId = netObj.NetworkObjectId;

            var state = PizzeriaNetworkState.Instance;
            var cmd = PizzeriaCommandHandler.Instance;
            if (state == null || cmd == null) return;

            NetItemDto itemOnStation = default;
            bool hasItemOnStation = false;

            NetItemDto itemInHand = default;
            bool hasItemInHand = false;

            for (int i = 0; i < state.Items.Count; i++)
            {
                var item = state.Items[i];
                if (item.LocationType == (byte)LocationType.StationSlot && item.SlotId == stationId)
                {
                    itemOnStation = item;
                    hasItemOnStation = true;
                }
                if (item.LocationType == (byte)LocationType.Hand && item.HolderId == playerId)
                {
                    itemInHand = item;
                    hasItemInHand = true;
                }
            }

            if (hasItemOnStation && !hasItemInHand)
            {
                cmd.RemoveOvenServerRpc(stationId, 0, 0); // 0 = right hand
            }
            else if (!hasItemOnStation && hasItemInHand)
            {
                cmd.InsertOvenServerRpc(itemInHand.ItemId, stationId, 0);
            }
        }

        protected override void Update()
        {
            if (_progressBar == null) return;
            var state = PizzeriaNetworkState.Instance;
            if (state == null) return;

            bool foundActive = false;
            for (int i = 0; i < state.Items.Count; i++)
            {
                var item = state.Items[i];
                if (item.LocationType == (byte)LocationType.StationSlot && item.SlotId == stationId && item.CookingStage != (byte)CookingStage.Uncooked)
                {
                    foundActive = true;
                    if (item.BurnProgress > 0)
                    {
                        // Burning
                        _progressBar.SetProgress(item.BurnProgress, Color.red);
                    }
                    else if (item.CookProgress < 1f)
                    {
                        // Cooking
                        _progressBar.SetProgress(item.CookProgress, item.CookProgress > 0.8f ? Color.yellow : Color.green);
                    }
                    else
                    {
                        // Baked, waiting to burn or be removed
                        _progressBar.SetProgress(1f, Color.yellow);
                    }
                    break;
                }
            }

            if (!foundActive)
            {
                _progressBar.Hide();
            }
        }
    }
}
