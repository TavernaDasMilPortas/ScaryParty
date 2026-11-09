using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;

namespace ScaryParty.Pizzeria.Presentation
{
    /// <summary>
    /// Visualizador e sinalizador de ponto de entrega no mundo 3D.
    /// Exibe beacon de calçada, luz de varanda pulsante para pedidos ativos,
    /// texto 3D em billboard com a numeração formatada e feedback visual de entrega.
    /// </summary>
    public class DeliveryPointView : MonoBehaviour
    {
        private static readonly Dictionary<int, DeliveryPointView> _registry = new Dictionary<int, DeliveryPointView>();

        [Header("Identificação do Ponto")]
        [SerializeField] private int _addressId;
        public int AddressId { get => _addressId; set => _addressId = value; }

        [Header("Elementos Visuais")]
        [SerializeField] private Light _porchLight;
        [SerializeField] private Renderer _beaconRenderer;
        [SerializeField] private TextMesh _worldText;

        [Header("Cores e Pulso")]
        [SerializeField] private Color _activeOrderColor = new Color(1f, 0.75f, 0.2f);
        [SerializeField] private Color _idleColor = new Color(0.2f, 0.4f, 0.8f, 0.5f);
        [SerializeField] private float _pulseSpeed = 3f;

        private MaterialPropertyBlock _propBlock;
        private float _feedbackTimer = 0f;
        private bool _isDeliveredFlash = false;

        public static DeliveryPointView Get(int addressId)
        {
            _registry.TryGetValue(addressId, out var view);
            return view;
        }

        private void Awake()
        {
            var dp = GetComponent<DeliveryPoint>();
            if (dp != null)
            {
                _addressId = dp.AddressId;
            }
            _registry[_addressId] = this;
            _propBlock = new MaterialPropertyBlock();
            EnsureVisualElements();
        }

        private void OnDestroy()
        {
            if (_registry.TryGetValue(_addressId, out var view) && view == this)
            {
                _registry.Remove(_addressId);
            }
        }

        public void EnsureVisualElements()
        {
            // Texto 3D exibindo o número da residência
            if (_worldText == null)
            {
                var textObj = transform.Find("AddressWorldText");
                if (textObj == null)
                {
                    var go = new GameObject("AddressWorldText");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(0, 1.8f, 0);
                    _worldText = go.AddComponent<TextMesh>();
                    _worldText.fontSize = 28;
                    _worldText.characterSize = 0.08f;
                    _worldText.alignment = TextAlignment.Center;
                    _worldText.anchor = TextAnchor.MiddleCenter;
                    _worldText.color = Color.white;
                }
                else
                {
                    _worldText = textObj.GetComponent<TextMesh>();
                }
            }

            if (_worldText != null)
            {
                _worldText.text = $"Casa {AddressFormat.Display(_addressId)}";
            }

            // Renderer do cilindro do beacon
            if (_beaconRenderer == null)
            {
                _beaconRenderer = GetComponent<Renderer>();
            }

            // Luz de varanda (point light)
            if (_porchLight == null)
            {
                var lightObj = transform.Find("PorchLight");
                if (lightObj == null)
                {
                    var go = new GameObject("PorchLight");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(0, 2.2f, 0);
                    _porchLight = go.AddComponent<Light>();
                    _porchLight.type = LightType.Point;
                    _porchLight.range = 7f;
                    _porchLight.intensity = 1.2f;
                    _porchLight.color = _idleColor;
                }
                else
                {
                    _porchLight = lightObj.GetComponent<Light>();
                }
            }
        }

        private void Update()
        {
            // Billboard do texto 3D para encarar a câmera principal
            if (_worldText != null && Camera.main != null)
            {
                _worldText.transform.rotation = Quaternion.LookRotation(_worldText.transform.position - Camera.main.transform.position);
            }

            if (_feedbackTimer > 0f)
            {
                _feedbackTimer -= Time.deltaTime;
                if (_feedbackTimer <= 0f)
                {
                    _isDeliveredFlash = false;
                }
            }

            UpdateBeaconState();
        }

        private void UpdateBeaconState()
        {
            if (_isDeliveredFlash)
            {
                // Flash verde celebrando o recebimento da entrega
                if (_porchLight != null)
                {
                    _porchLight.color = Color.green;
                    _porchLight.intensity = 3.5f;
                }
                if (_beaconRenderer != null)
                {
                    _propBlock.SetColor("_BaseColor", Color.green);
                    _propBlock.SetColor("_EmissionColor", Color.green * 4f);
                    _beaconRenderer.SetPropertyBlock(_propBlock);
                }
                return;
            }

            bool hasActiveOrder = false;
            var netState = PizzeriaNetworkState.Instance;
            if (netState != null && netState.Orders != null)
            {
                for (int i = 0; i < netState.Orders.Count; i++)
                {
                    var o = netState.Orders[i];
                    if (o.DestinationId == _addressId && o.Lifecycle == (byte)OrderLifecycle.Active)
                    {
                        hasActiveOrder = true;
                        break;
                    }
                }
            }

            if (hasActiveOrder)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * _pulseSpeed);
                Color c = Color.Lerp(_activeOrderColor, Color.yellow, pulse);

                if (_porchLight != null)
                {
                    _porchLight.color = c;
                    _porchLight.intensity = 1.8f + pulse * 1.2f;
                }
                if (_beaconRenderer != null)
                {
                    _propBlock.SetColor("_BaseColor", c);
                    _propBlock.SetColor("_EmissionColor", c * (2f + pulse * 2f));
                    _beaconRenderer.SetPropertyBlock(_propBlock);
                }
            }
            else
            {
                if (_porchLight != null)
                {
                    _porchLight.color = _idleColor;
                    _porchLight.intensity = 0.6f;
                }
                if (_beaconRenderer != null)
                {
                    _propBlock.SetColor("_BaseColor", _idleColor);
                    _propBlock.SetColor("_EmissionColor", _idleColor * 0.5f);
                    _beaconRenderer.SetPropertyBlock(_propBlock);
                }
            }
        }

        public void TriggerDeliveryFeedback()
        {
            _feedbackTimer = 2.0f;
            _isDeliveredFlash = true;

            // Feedback sonoro harmônico
            PizzeriaSoundManager.Instance?.PlayDing();
        }

        public static void PlayDeliveryFeedbackForAddress(int addressId)
        {
            if (_registry.TryGetValue(addressId, out var view) && view != null)
            {
                view.TriggerDeliveryFeedback();
            }
        }
    }
}
