using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Stations
{
    public class OvenStation : StationView
    {
        public override string InteractPrompt
        {
            get
            {
                var netState = PizzeriaNetworkState.Instance;
                var networkManager = NetworkManager.Singleton;
                if (netState == null || networkManager == null) return "Forno";

                ulong localClientId = networkManager.LocalClientId;
                bool stationHasPizza = false;
                bool playerHasPizza = false;
                float cookProgress = 0f;
                float burnProgress = 0f;

                for (int i = 0; i < netState.Items.Count; i++)
                {
                    var item = netState.Items[i];
                    bool isPizza = item.Category == (byte)ItemCategory.PizzaBase || item.Category == (byte)ItemCategory.Pizza;
                    
                    if (item.LocationType == (byte)LocationType.StationSlot && item.HolderId == (ulong)stationId)
                    {
                        stationHasPizza = true;
                        cookProgress = item.CookProgress;
                        burnProgress = item.BurnProgress;
                    }
                    if (item.LocationType == (byte)LocationType.Hand && item.HolderId == localClientId)
                    {
                        if (isPizza) playerHasPizza = true;
                    }
                }

                if (stationHasPizza)
                {
                    string status = "Assando";
                    if (burnProgress > 0) status = "Queimando!";
                    else if (cookProgress >= 1f) status = "Pronta!";
                    
                    int pct = Mathf.FloorToInt((burnProgress > 0 ? burnProgress : cookProgress) * 100);
                    return $"[E] Retirar Pizza ({status} {pct}%)";
                }
                if (!stationHasPizza && playerHasPizza) return "[E] Colocar no Forno";
                
                return "Forno vazio";
            }
        }

        public override void OnInteract(GameObject interactor)
        {
            var netObj = interactor.GetComponent<NetworkObject>();
            if (netObj == null) return;
            ulong playerId = netObj.OwnerClientId;

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
                if (item.LocationType == (byte)LocationType.StationSlot && item.HolderId == (ulong)stationId)
                {
                    itemOnStation = item;
                    hasItemOnStation = true;
                    bool isPizza = item.Category == (byte)ItemCategory.PizzaBase || item.Category == (byte)ItemCategory.Pizza;
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
                if (item.LocationType == (byte)LocationType.StationSlot && item.HolderId == (ulong)stationId && item.CookingStage != (byte)CookingStage.Uncooked)
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
