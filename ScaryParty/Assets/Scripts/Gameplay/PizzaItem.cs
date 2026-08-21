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
            // Attempt to pick up
            bool pickedUp = interaction.TryPickUpItem(pizzaType, (int)NetworkObjectId);
            
            if (pickedUp)
            {
                // Tell server to despawn this pizza box
                PickUpServerRpc();
            }
            else
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowInteractionPrompt("Hands are full!");
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
