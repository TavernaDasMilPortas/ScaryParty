using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Network;

namespace ScaryParty.Pizzeria.Presentation
{
    /// <summary>
    /// Apresentador em UI Toolkit (com pool de elementos) para exibir as barras de paciência
    /// dos clientes presenciais (NPCs) acima da cabeça em espaço de tela, com:
    /// - Cor dinâmica verde -> amarelo -> vermelho conforme a paciência cai;
    /// - Filtro de distância, frustum da câmera e oclusão por Raycast;
    /// - Exibição concisa dos traços e estado do cliente.
    /// </summary>
    public class CustomerPatienceHudPresenter : MonoBehaviour
    {
        public static CustomerPatienceHudPresenter Instance { get; private set; }

        [Header("Configurações de Visibilidade")]
        public float maxVisibleDistance = 16f;
        public Vector3 worldHeadOffset = new Vector3(0f, 2.08f, 0f);
        public LayerMask occlusionMask = ~0;

        private UIDocument _document;
        private VisualElement _overlayContainer;
        private readonly List<PatienceWidget> _widgetPool = new List<PatienceWidget>();

        private sealed class PatienceWidget
        {
            public VisualElement Root;
            public Label TitleLabel;
            public Label TraitsLabel;
            public VisualElement BarTrack;
            public VisualElement BarFill;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            EnsureOverlayContainer();
        }

        private void OnDestroy()
        {
            if (_overlayContainer != null && _overlayContainer.parent != null)
            {
                _overlayContainer.RemoveFromHierarchy();
            }
            if (Instance == this) Instance = null;
        }

        private void EnsureOverlayContainer()
        {
            if (_overlayContainer != null) return;

            _document = GetComponent<UIDocument>();
            if (_document == null)
            {
                var docs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
                for (int i = 0; i < docs.Length; i++)
                {
                    if (docs[i] != null && docs[i].rootVisualElement != null)
                    {
                        _document = docs[i];
                        break;
                    }
                }
            }

            if (_document == null || _document.rootVisualElement == null) return;

            _overlayContainer = new VisualElement
            {
                name = "customer-patience-hud-layer",
                pickingMode = PickingMode.Ignore
            };
            _overlayContainer.style.position = Position.Absolute;
            _overlayContainer.style.left = 0;
            _overlayContainer.style.top = 0;
            _overlayContainer.style.right = 0;
            _overlayContainer.style.bottom = 0;
            _document.rootVisualElement.Add(_overlayContainer);
        }

        private PatienceWidget GetOrCreateWidget(int index)
        {
            while (_widgetPool.Count <= index)
            {
                var root = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };
                root.style.position = Position.Absolute;
                root.style.width = 148;
                root.style.paddingLeft = 6;
                root.style.paddingRight = 6;
                root.style.paddingTop = 4;
                root.style.paddingBottom = 4;
                root.style.backgroundColor = new Color(0.08f, 0.08f, 0.11f, 0.84f);
                root.style.borderTopLeftRadius = 5;
                root.style.borderTopRightRadius = 5;
                root.style.borderBottomLeftRadius = 5;
                root.style.borderBottomRightRadius = 5;
                root.style.display = DisplayStyle.None;

                var title = new Label
                {
                    pickingMode = PickingMode.Ignore
                };
                title.style.fontSize = 11;
                title.style.color = new Color(0.96f, 0.94f, 0.88f);
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.unityTextAlign = TextAnchor.MiddleCenter;

                var traits = new Label
                {
                    pickingMode = PickingMode.Ignore
                };
                traits.style.fontSize = 9;
                traits.style.color = new Color(0.75f, 0.82f, 0.90f);
                traits.style.unityTextAlign = TextAnchor.MiddleCenter;
                traits.style.marginBottom = 3;

                var track = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };
                track.style.height = 6;
                track.style.backgroundColor = new Color(0.18f, 0.18f, 0.22f, 0.95f);
                track.style.borderTopLeftRadius = 3;
                track.style.borderTopRightRadius = 3;
                track.style.borderBottomLeftRadius = 3;
                track.style.borderBottomRightRadius = 3;
                track.style.overflow = Overflow.Hidden;

                var fill = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };
                fill.style.height = Length.Percent(100);
                fill.style.width = Length.Percent(100);
                fill.style.backgroundColor = new Color(0.25f, 0.85f, 0.38f);
                track.Add(fill);

                root.Add(title);
                root.Add(traits);
                root.Add(track);

                _overlayContainer?.Add(root);

                _widgetPool.Add(new PatienceWidget
                {
                    Root = root,
                    TitleLabel = title,
                    TraitsLabel = traits,
                    BarTrack = track,
                    BarFill = fill
                });
            }

            return _widgetPool[index];
        }

        private void LateUpdate()
        {
            EnsureOverlayContainer();
            if (_overlayContainer == null) return;

            var spawner = CustomerSpawner.Instance;
            var cam = Camera.main;
            if (spawner == null || cam == null || spawner.ActiveViews == null || spawner.ActiveViews.Count == 0)
            {
                HideAllFrom(0);
                return;
            }

            var panel = _overlayContainer.panel;
            if (panel == null)
            {
                HideAllFrom(0);
                return;
            }

            var catalog = PizzeriaRoot.Instance?.Catalog;
            int activeWidgetIndex = 0;
            Vector3 camPos = cam.transform.position;

            foreach (var kvp in spawner.ActiveViews)
            {
                var view = kvp.Value;
                if (view == null) continue;

                var dto = view.CurrentState;
                var phase = (CustomerPhase)dto.Phase;

                // Mostrar barra apenas durante fila ou espera do pedido na mesa/caminhada
                bool showPatience = dto.IsInQueue || dto.IsWaitingForFood || phase == CustomerPhase.Eating;
                if (!showPatience) continue;

                Vector3 worldPos = view.transform.position + worldHeadOffset;
                float dist = Vector3.Distance(camPos, worldPos);
                if (dist > maxVisibleDistance) continue;

                Vector3 viewportPos = cam.WorldToViewportPoint(worldPos);
                if (viewportPos.z <= 0.05f || viewportPos.x < 0.02f || viewportPos.x > 0.98f || viewportPos.y < 0.02f || viewportPos.y > 0.98f)
                    continue;

                // Checagem de oclusão por obstáculos sólidos (paredes)
                Vector3 dir = worldPos - camPos;
                if (Physics.Raycast(camPos, dir.normalized, out RaycastHit hit, dist - 0.35f, occlusionMask, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider != null && !hit.collider.transform.IsChildOf(view.transform))
                    {
                        var playerInter = hit.collider.GetComponentInParent<PlayerInteraction>();
                        if (playerInter == null)
                        {
                            continue;
                        }
                    }
                }

                Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(panel, worldPos, cam);
                var widget = GetOrCreateWidget(activeWidgetIndex++);
                widget.Root.style.display = DisplayStyle.Flex;
                widget.Root.style.left = panelPos.x - 74f;
                widget.Root.style.top = panelPos.y - 38f;

                if (dto.IsInQueue)
                {
                    widget.TitleLabel.text = dto.QueueSlotIndex == 0
                        ? $"Fila {dto.QueueIndex + 1} (Balcão)"
                        : $"Fila {dto.QueueIndex + 1} (#{dto.QueueSlotIndex + 1})";
                }
                else if (dto.IsWaitingForFood)
                {
                    widget.TitleLabel.text = $"Mesa {dto.AssignedTableId} • Aguardando";
                }
                else
                {
                    widget.TitleLabel.text = $"Mesa {dto.AssignedTableId} • Comendo";
                }

                string posTrait = catalog != null && catalog.CustomerTraits.TryGetValue(dto.PositiveTraitId, out var pt) ? pt.Name : "Paciente";
                string negTrait = catalog != null && catalog.CustomerTraits.TryGetValue(dto.NegativeTraitId, out var nt) ? nt.Name : "Apressado";
                widget.TraitsLabel.text = $"{posTrait} • {negTrait}";

                float pct = phase == CustomerPhase.Eating ? 1f : Mathf.Clamp01(dto.ActivePatienceNormalized);
                widget.BarFill.style.width = Length.Percent(pct * 100f);
                widget.BarFill.style.backgroundColor = EvaluatePatienceColor(pct, phase == CustomerPhase.Eating);
            }

            HideAllFrom(activeWidgetIndex);
        }

        private static Color EvaluatePatienceColor(float normalized, bool isEating)
        {
            if (isEating)
                return new Color(0.28f, 0.78f, 0.92f);

            if (normalized >= 0.5f)
            {
                float t = (normalized - 0.5f) / 0.5f;
                return Color.Lerp(new Color(0.95f, 0.80f, 0.18f), new Color(0.24f, 0.84f, 0.38f), t);
            }
            else
            {
                float t = normalized / 0.5f;
                return Color.Lerp(new Color(0.92f, 0.22f, 0.20f), new Color(0.95f, 0.80f, 0.18f), t);
            }
        }

        private void HideAllFrom(int startIndex)
        {
            for (int i = startIndex; i < _widgetPool.Count; i++)
            {
                if (_widgetPool[i].Root != null)
                {
                    _widgetPool[i].Root.style.display = DisplayStyle.None;
                }
            }
        }
    }
}
