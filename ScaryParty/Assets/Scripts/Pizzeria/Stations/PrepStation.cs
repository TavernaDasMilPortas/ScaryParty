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
                int slotIdx = ResolveTargetSlotIndex();
                if (slotIdx < 0) return string.Empty;

                var netState = PizzeriaNetworkState.Instance;
                var networkManager = NetworkManager.Singleton;
                if (netState == null || networkManager == null) return "Bancada de Preparo";

                ulong localClientId = networkManager.LocalClientId;
                bool slotHasItem = false;
                NetItemDto slotItem = default;
                bool slotHasTool = false;
                NetToolDto slotTool = default;

                bool playerHasItem = false;
                NetItemDto handItem = default;
                bool playerHasTool = false;

                for (int i = 0; i < netState.Items.Count; i++)
                {
                    var item = netState.Items[i];
                    if (item.LocationType == (byte)LocationType.StationSlot && item.HolderId == (ulong)stationId && item.SlotId == slotIdx)
                    {
                        slotHasItem = true;
                        slotItem = item;
                    }
                    if (item.LocationType == (byte)LocationType.Hand && item.HolderId == localClientId)
                    {
                        playerHasItem = true;
                        handItem = item;
                    }
                }

                for (int i = 0; i < netState.Tools.Count; i++)
                {
                    var tool = netState.Tools[i];
                    if (tool.LocationType == (byte)LocationType.StationSlot && tool.HolderId == (ulong)stationId && tool.SlotId == slotIdx)
                    {
                        slotHasTool = true;
                        slotTool = tool;
                    }
                    if (tool.LocationType == (byte)LocationType.Hand && tool.HolderId == localClientId)
                    {
                        playerHasTool = true;
                    }
                }

                if (slotHasItem)
                {
                    if (slotItem.PrepStage == (byte)ProcessingStage.Raw)
                    {
                        if (slotItem.DefinitionId == 1) return "[F] Segurar para Abrir Massa / [E] Pegar";
                        if (slotItem.DefinitionId == 4) return "[F] Segurar para Ralar Queijo / [E] Pegar";
                        if (slotItem.DefinitionId == 6 || slotItem.DefinitionId == 8) return "[F] Segurar para Fatiar / [E] Pegar";
                        return "[F] Segurar para Trabalhar / [E] Pegar";
                    }
                    return "[E] Pegar Ingrediente Preparado";
                }

                if (slotHasTool)
                {
                    string tName = slotTool.DefinitionId == 1 ? "Ralador" : "Faca";
                    return $"[E] Pegar {tName}";
                }

                if (playerHasItem)
                {
                    if (handItem.PrepStage == (byte)ProcessingStage.Raw)
                        return "[E] Colocar / [F] Colocar e Trabalhar";
                    return "[E] Colocar na bancada";
                }

                if (playerHasTool)
                    return "[E] Colocar Utensílio";

                return "Bancada de Preparo";
            }
        }

        protected override void Start()
        {
            base.Start();
            stationName = "Bancada de Preparo";

            if (!slotsBoundToObjects && (slotAnchors == null || slotAnchors.Length == 0))
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
