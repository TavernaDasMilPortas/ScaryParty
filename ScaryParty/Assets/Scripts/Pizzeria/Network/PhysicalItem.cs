using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Player;

namespace ScaryParty.Pizzeria.Network
{
    public class PhysicalItem : NetworkBehaviour, IInteractable
    {
        [Header("Identidade de Rede")]
        public NetworkVariable<ulong> ItemInstanceId = new NetworkVariable<ulong>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [Header("Metadados Visuais")]
        public string itemName = "Item";
        public ItemCategory category = ItemCategory.Other;
        public int definitionId = 0;

        public virtual string InteractPrompt => $"[E] Pegar {itemName}";

        public virtual void OnInteract(GameObject interactor)
        {
            if (ItemInstanceId.Value == 0) return;
            var cmd = PizzeriaCommandHandler.Instance;
            if (cmd != null && NetworkManager.Singleton != null)
            {
                ulong myClientId = NetworkManager.Singleton.LocalClientId;
                byte handSlot = 0;
                var invAdapter = interactor.GetComponent<PlayerInventoryAdapter>();
                if (invAdapter != null)
                {
                    handSlot = (byte)invAdapter.ActiveHand;
                }
                cmd.TransferItemServerRpc(ItemInstanceId.Value, (byte)LocationType.Hand, myClientId, handSlot);
            }
        }

        public virtual void OnFocus() { }
        public virtual void OnLoseFocus() { }
    }
}
