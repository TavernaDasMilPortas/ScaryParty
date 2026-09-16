using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Stations
{
    public class PrepStation : StationView
    {
        [Header("Preparo")]
        public int defaultProcessId = 1;

        public override string InteractPrompt
        {
            get
            {
                var netState = PizzeriaNetworkState.Instance;
                var networkManager = NetworkManager.Singleton;
                if (netState == null || networkManager == null) return "Bancada de Preparo";

                ulong localClientId = networkManager.LocalClientId;
                bool stationHasItem = false;
                bool playerHasItem = false;

                for (int i = 0; i < netState.Items.Count; i++)
                {
                    var item = netState.Items[i];
                    if (item.LocationType == (byte)LocationType.StationSlot && item.HolderId == (ulong)stationId)
                    {
                        stationHasItem = true;
                    }
                    if (item.LocationType == (byte)LocationType.Hand && item.HolderId == localClientId)
                    {
                        playerHasItem = true;
                    }
                }

                if (stationHasItem) return "[F] Segurar para Trabalhar";
                if (!stationHasItem && playerHasItem) return "[E] Colocar na bancada / [F] Colocar e Trabalhar";
                
                return "Bancada de Preparo vazia";
            }
        }
    }
}
