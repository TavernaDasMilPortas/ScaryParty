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

        protected override void Start()
        {
            base.Start();
            stationName = "Bancada de Preparo";

            if (!slotsBoundToObjects && (slotAnchors == null || slotAnchors.Length < 2))
            {
                var slotsParent = transform.Find("Slots");
                if (slotsParent == null)
                {
                    var go = new GameObject("Slots");
                    go.transform.SetParent(transform, false);
                    slotsParent = go.transform;
                }

                slotAnchors = new Transform[2];
                var s0 = new GameObject("Slot_0");
                s0.transform.SetParent(slotsParent, false);
                s0.transform.localPosition = new Vector3(-0.35f, 0.38f, 0f);
                slotAnchors[0] = s0.transform;

                var s1 = new GameObject("Slot_1");
                s1.transform.SetParent(slotsParent, false);
                s1.transform.localPosition = new Vector3(0.35f, 0.38f, 0f);
                slotAnchors[1] = s1.transform;
            }
        }
    }
}
