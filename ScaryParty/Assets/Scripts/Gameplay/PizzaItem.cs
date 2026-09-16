using UnityEngine;
using Unity.Netcode;

/// <summary>
/// A pizza box that can be picked up by the player.
/// </summary>
public class PizzaItem : NetworkBehaviour, IInteractable
{
    [Header("Pizza Data")]
    public string pizzaType = "Pepperoni Pizza";
    public int deliveryPointId = -1;

    public string InteractPrompt => $"Press [E] to Pick Up {pizzaType}";

    public void OnInteract(GameObject player)
    {
        PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
        if (interaction != null)
        {
            var cmd = ScaryParty.Pizzeria.Network.PizzeriaCommandHandler.Instance;
            if (cmd != null && NetworkManager.Singleton != null)
            {
                ulong myClientId = NetworkManager.Singleton.LocalClientId;
                byte handSlot = 0;
                var invAdapter = player.GetComponent<ScaryParty.Pizzeria.Player.PlayerInventoryAdapter>();
                if (invAdapter != null)
                {
                    handSlot = (byte)invAdapter.ActiveHand;
                }
                
                cmd.TransferItemServerRpc(NetworkObjectId, (byte)ScaryParty.Pizzeria.Domain.Types.LocationType.Hand, myClientId, handSlot);
                
                // Keep the old despawn behavior but skip the legacy hand tracking
                PickUpServerRpc();
            }
            else
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowInteractionPrompt("Cannot pick up (Network error)");
            }
        }
    }

    private InteractableHighlight _highlight;

    private void Awake()
    {
        _highlight = GetComponent<InteractableHighlight>();
        if (_highlight == null)
        {
            _highlight = gameObject.AddComponent<InteractableHighlight>();
        }
    }

    public void OnFocus() 
    { 
        if (_highlight != null) _highlight.EnableHighlight();

        if (UIManager.Instance != null)
            UIManager.Instance.ShowInteractionPrompt(InteractPrompt);
    }
    
    public void OnLoseFocus() 
    {
        if (_highlight != null) _highlight.DisableHighlight();

        if (UIManager.Instance != null)
            UIManager.Instance.HideInteractionPrompt();
    }

    [ServerRpc(RequireOwnership = false)]
    private void PickUpServerRpc()
    {
        GetComponent<NetworkObject>().Despawn(true);
    }
}
