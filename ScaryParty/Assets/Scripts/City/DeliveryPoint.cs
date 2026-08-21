using UnityEngine;

/// <summary>
/// Component for delivery point markers.
/// Allows players to deliver pizzas by interacting with them.
/// </summary>
public class DeliveryPoint : MonoBehaviour, IInteractable
{
    [Header("Delivery Point Info")]
    public CityBuilding associatedBuilding;
    public int pointIndex;
    public bool isActive;

    [Header("Animation Settings")]
    [SerializeField] private float _floatSpeed = 2f;
    [SerializeField] private float _floatAmplitude = 0.5f;

    private float _startY;
    private Renderer[] _renderers;

    public string InteractPrompt => $"Press [E] to Deliver to Point #{pointIndex}";

    private void Start()
    {
        _startY = transform.position.y;
        _renderers = GetComponentsInChildren<Renderer>();

        _highlight = GetComponent<InteractableHighlight>();
        if (_highlight == null)
        {
            _highlight = gameObject.AddComponent<InteractableHighlight>();
        }
    }

    private void Update()
    {
        // Bobbing animation when active
        if (isActive)
        {
            float newY = _startY + Mathf.Sin(Time.time * _floatSpeed) * _floatAmplitude;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }

        // Toggle visibility
        if (_renderers != null)
        {
            foreach (var r in _renderers)
            {
                r.enabled = isActive;
            }
        }
    }

    public void OnInteract(GameObject player)
    {
        if (!isActive) return;

        PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
        if (interaction != null)
        {
            // Simple check: Is the player holding any pizza?
            // In a real scenario, we check if they hold the *correct* pizza type.
            string heldPizza = "";
            if (interaction.rightHand.isFull && interaction.rightHand.itemName.Contains("Pizza"))
                heldPizza = interaction.rightHand.itemName;
            else if (interaction.leftHand.isFull && interaction.leftHand.itemName.Contains("Pizza"))
                heldPizza = interaction.leftHand.itemName;

            if (!string.IsNullOrEmpty(heldPizza))
            {
                if (PizzariaManager.Instance != null && PizzariaManager.Instance.HasOrder(heldPizza, pointIndex))
                {
                    // Deliver!
                    interaction.RemoveItem(heldPizza);
                    
                    // Notify the Pizzaria Manager
                    if (player.GetComponent<Unity.Netcode.NetworkObject>() != null)
                    {
                        ulong clientId = player.GetComponent<Unity.Netcode.NetworkObject>().OwnerClientId;
                        PizzariaManager.Instance.CompleteOrder(heldPizza, pointIndex, clientId);
                    }

                    // Deactivate this point until it's requested again
                    isActive = false;
                }
                else
                {
                    if (UIManager.Instance != null)
                        UIManager.Instance.ShowInteractionPrompt("Wrong pizza for this address!");
                }
            }
            else
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowInteractionPrompt("You don't have a pizza to deliver here!");
            }
        }
    }

    private InteractableHighlight _highlight;

    public void OnFocus() 
    { 
        if (!isActive) return;

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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isActive ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position, 1f);

        if (associatedBuilding != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, associatedBuilding.transform.position);
        }
    }
}
