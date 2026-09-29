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

        // Novo Módulo Pizzaria: Entrega autoritativa em lote via DeliveryPointAdapter
        if (ScaryParty.Pizzeria.Network.PizzeriaCommandHandler.Instance != null)
        {
            bool submitted = ScaryParty.Pizzeria.Integration.DeliveryPointAdapter.TrySubmitDelivery(pointIndex);
            if (!submitted && UIManager.Instance != null)
            {
                UIManager.Instance.ShowInteractionPrompt("Nenhuma pizza para entregar aqui!");
            }
            return;
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
