using UnityEngine;
using UnityEngine.Serialization;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Integration;
using ScaryParty.Pizzeria.Composition;

/// <summary>
/// Componente de ponto de entrega no mundo.
/// Representa o destino de entrega de residências na cidade.
/// </summary>
public class DeliveryPoint : MonoBehaviour, IInteractable
{
    [Header("Informações do Ponto de Entrega")]
    public CityBuilding associatedBuilding;

    [FormerlySerializedAs("pointIndex")]
    [SerializeField] private int _addressId;
    public int AddressId { get => _addressId; set => _addressId = value; }
    public int pointIndex { get => _addressId; set => _addressId = value; }

    [Tooltip("Status de destaque visual. A interação de entrega permanece sempre disponível.")]
    public bool isActive = true;

    [Header("Animação")]
    [SerializeField] private float _floatSpeed = 2f;
    [SerializeField] private float _floatAmplitude = 0.2f;

    private float _startY;
    private Renderer[] _renderers;
    private DeliveryPointView _view;

    public string InteractPrompt => $"Pressione [E] para Entregar no {AddressFormat.Display(_addressId)}";

    private void Start()
    {
        _startY = transform.position.y;
        _renderers = GetComponentsInChildren<Renderer>();

        // Assegurar visualizador do ponto de entrega
        _view = GetComponent<DeliveryPointView>();
        if (_view == null)
        {
            _view = gameObject.AddComponent<DeliveryPointView>();
        }

        // Registrar posição no AddressRegistry se o PizzeriaRoot já estiver ativo
        if (PizzeriaRoot.Instance != null && PizzeriaRoot.Instance.AddressRegistry != null)
        {
            PizzeriaRoot.Instance.AddressRegistry.Register(
                new AddressId(_addressId),
                new DomainVector3(transform.position.x, transform.position.y, transform.position.z));
        }
    }

    private void Update()
    {
        // Animação de flutuação suave do marcador
        if (isActive)
        {
            float newY = _startY + Mathf.Sin(Time.time * _floatSpeed) * _floatAmplitude;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
    }

    public void OnInteract(GameObject player)
    {
        // Abrir modal de seleção de caixas se disponível
        if (DeliverySelectionPresenter.Instance != null)
        {
            DeliverySelectionPresenter.Instance.Open(_addressId);
            return;
        }

        // Fallback para entrega em lote direta via adapter
        bool submitted = DeliveryPointAdapter.TrySubmitDelivery(_addressId);
        if (!submitted && UIManager.Instance != null)
        {
            UIManager.Instance.ShowInteractionPrompt("Nenhuma pizza para entregar aqui!");
        }
    }

    public void OnFocus()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowInteractionPrompt(InteractPrompt);
        }
    }

    public void OnLoseFocus()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideInteractionPrompt();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isActive ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 1.2f);

        if (associatedBuilding != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, associatedBuilding.transform.position);
        }
    }
}
