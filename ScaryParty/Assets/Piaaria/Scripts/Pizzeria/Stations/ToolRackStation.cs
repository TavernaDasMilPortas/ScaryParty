using UnityEngine;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Stations
{
    public class ToolRackStation : StationView
    {
        public override string InteractPrompt
        {
            get
            {
                var netState = PizzeriaNetworkState.Instance;
                var networkManager = Unity.Netcode.NetworkManager.Singleton;
                if (netState == null || networkManager == null) return "[E] " + stationName;

                ulong localClientId = networkManager.LocalClientId;
                var invAdapter = networkManager.LocalClient?.PlayerObject?.GetComponent<ScaryParty.Pizzeria.Player.PlayerInventoryAdapter>();
                int activeHand = invAdapter != null ? (int)invAdapter.ActiveHand : 0;

                NetToolDto toolInHand = default;
                bool hasToolInHand = false;
                bool hasItemInHand = false;

                for (int i = 0; i < netState.Tools.Count; i++)
                {
                    var t = netState.Tools[i];
                    if (t.LocationType == (byte)LocationType.Hand && t.HolderId == localClientId && t.SlotId == activeHand)
                    {
                        toolInHand = t;
                        hasToolInHand = true;
                        break;
                    }
                }

                for (int i = 0; i < netState.Items.Count; i++)
                {
                    var it = netState.Items[i];
                    if (it.LocationType == (byte)LocationType.Hand && it.HolderId == localClientId && it.SlotId == activeHand)
                    {
                        hasItemInHand = true;
                        break;
                    }
                }

                if (hasToolInHand)
                {
                    string tName = toolInHand.DefinitionId == 1 ? "Ralador" : "Faca";
                    return $"[E] Guardar {tName}";
                }

                if (hasItemInHand)
                {
                    return "[Mão Ocupada com Item]";
                }

                for (int i = 0; i < netState.Tools.Count; i++)
                {
                    var t = netState.Tools[i];
                    if (t.LocationType == (byte)LocationType.StationSlot && t.HolderId == (ulong)stationId)
                    {
                        string tName = t.DefinitionId == 1 ? "Ralador" : "Faca";
                        return $"[E] Pegar {tName}";
                    }
                }

                return "Suporte de Utensílios Vazio";
            }
        }

        public override void OnInteract(GameObject interactor)
        {
            var netObj = interactor.GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj == null) return;
            ulong playerId = netObj.OwnerClientId;

            var invAdapter = interactor.GetComponent<ScaryParty.Pizzeria.Player.PlayerInventoryAdapter>();
            int activeHand = invAdapter != null ? (int)invAdapter.ActiveHand : 0;

            var state = PizzeriaNetworkState.Instance;
            var cmd = PizzeriaCommandHandler.Instance;
            if (state == null || cmd == null) return;

            NetToolDto toolOnRack = default;
            bool hasToolOnRack = false;

            NetToolDto toolInHand = default;
            bool hasToolInHand = false;

            bool hasItemInHand = false;
            for (int i = 0; i < state.Items.Count; i++)
            {
                var it = state.Items[i];
                if (it.LocationType == (byte)LocationType.Hand && it.HolderId == playerId && it.SlotId == activeHand)
                {
                    hasItemInHand = true;
                    break;
                }
            }

            for (int i = 0; i < state.Tools.Count; i++)
            {
                var tool = state.Tools[i];
                if (tool.LocationType == (byte)LocationType.StationSlot && tool.HolderId == (ulong)stationId)
                {
                    if (!hasToolOnRack)
                    {
                        toolOnRack = tool;
                        hasToolOnRack = true;
                    }
                }
                if (tool.LocationType == (byte)LocationType.Hand && tool.HolderId == playerId && tool.SlotId == activeHand)
                {
                    toolInHand = tool;
                    hasToolInHand = true;
                }
            }

            int otherHand = activeHand == 0 ? 1 : 0;
            bool otherHandFree = true;
            for (int i = 0; i < state.Items.Count; i++)
            {
                var it = state.Items[i];
                if (it.LocationType == (byte)LocationType.Hand && it.HolderId == playerId && it.SlotId == otherHand)
                {
                    otherHandFree = false;
                    break;
                }
            }
            if (otherHandFree)
            {
                for (int i = 0; i < state.Tools.Count; i++)
                {
                    var t = state.Tools[i];
                    if (t.LocationType == (byte)LocationType.Hand && t.HolderId == playerId && t.SlotId == otherHand)
                    {
                        otherHandFree = false;
                        break;
                    }
                }
            }

            if (hasToolInHand)
            {
                int freeSlot = 0;
                for (int slot = 0; slot < 4; slot++)
                {
                    bool slotFree = true;
                    for (int i = 0; i < state.Tools.Count; i++)
                    {
                        var t = state.Tools[i];
                        if (t.LocationType == (byte)LocationType.StationSlot && t.HolderId == (ulong)stationId && t.SlotId == slot)
                        {
                            slotFree = false;
                            break;
                        }
                    }
                    if (slotFree)
                    {
                        freeSlot = slot;
                        break;
                    }
                }
                cmd.TransferToolServerRpc(toolInHand.ToolId, (byte)LocationType.StationSlot, (ulong)stationId, freeSlot);
                string tName = toolInHand.DefinitionId == 1 ? "Ralador" : "Faca";
                ScaryParty.Pizzeria.Presentation.NotificationManager.Show($"{tName} guardado", ScaryParty.Pizzeria.Presentation.NotificationType.Info);
            }
            else if (hasToolOnRack)
            {
                int targetPickHand = -1;
                if (!hasItemInHand)
                {
                    targetPickHand = activeHand;
                }
                else if (otherHandFree)
                {
                    targetPickHand = otherHand;
                }

                if (targetPickHand != -1)
                {
                    cmd.TransferToolServerRpc(toolOnRack.ToolId, (byte)LocationType.Hand, playerId, targetPickHand);
                    string tName = toolOnRack.DefinitionId == 1 ? "Ralador" : "Faca";
                    ScaryParty.Pizzeria.Presentation.NotificationManager.Show($"{tName} coletado", ScaryParty.Pizzeria.Presentation.NotificationType.Info);
                }
                else
                {
                    ScaryParty.Pizzeria.Presentation.NotificationManager.Show("Ambas as mãos ocupadas! Libere uma mão para pegar o utensílio.", ScaryParty.Pizzeria.Presentation.NotificationType.Warning);
                }
            }
        }
    }
}
