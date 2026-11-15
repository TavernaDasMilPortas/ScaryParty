using System;
using System.Text;
using UnityEngine;
using TMPro;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Presentation.Feedback;
using ScaryParty.VisualStyles;

namespace ScaryParty.Pizzeria.Stations
{
    public class TicketSheetView : MonoBehaviour, IInteractable
    {
        private const float EntryFlutterDuration = 0.28f;
        private const float UrgentEnterSeconds = 45f;
        private const float UrgentExitSeconds = 48f;

        [Header("Components")]
        public TextMeshPro textMesh;
        public Renderer paperRenderer;
        [SerializeField] private Transform sheetVisualPivot;

        public NetOrderDto Order { get; private set; }
        public bool HasOrder { get; private set; }

        private MaterialPropertyBlock _paperMpb;
        private Color _basePaperColor = new Color(0.96f, 0.94f, 0.88f);
        private Color _focusedColor = new Color(1.0f, 0.98f, 0.75f);
        private Color _urgentPaperColor = new Color(1.0f, 0.82f, 0.74f);
        private bool _isFocused = false;
        private ulong _boundOrderId;
        private bool _isUrgentPulseActive;
        private bool _hasFiredDeadlineWarning;
        private float _entryFlutterTimer;
        private bool _pivotCaptured;
        private Vector3 _pivotBaseLocalPos;
        private Quaternion _pivotBaseLocalRot = Quaternion.identity;
        private Vector3 _pivotBaseLocalScale = Vector3.one;

        public string InteractPrompt => HasOrder ? $"[E] Abrir Comanda #{Order.OrderId}" : string.Empty;

        private void Awake()
        {
            if (paperRenderer == null)
            {
                paperRenderer = GetComponent<Renderer>();
            }

            _paperMpb = new MaterialPropertyBlock();
            ApplyPaperTint(_basePaperColor);
            CaptureVisualPivotIfSafe();

            var col = GetComponent<Collider>();
            if (col == null || col is MeshCollider)
            {
                if (col != null) Destroy(col);
                var box = gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(1f, 1f, 0.08f);
                box.center = new Vector3(0f, 0f, -0.02f);
            }
        }

        public static string FormatOrderDestinationHeader(NetOrderDto order)
        {
            if (order.IsDineIn)
            {
                int tableNum = order.TableId > 0 ? order.TableId : Math.Max(1, order.DestinationId - 1000);
                return $"Mesa {tableNum} (Salão)";
            }
            return AddressFormat.Format(new AddressId(order.DestinationId));
        }

        public static string FormatOrderLineCompact(NetPizzaSpecDto specDto, int recipeIdFallback, int qty, DefinitionCatalog catalog)
        {
            var spec = specDto.BaseDoughId > 0 || specDto.ToppingCount > 0
                ? specDto.ToDomain()
                : (catalog != null ? catalog.ResolveSpecificationFromRecipeOrDefault(recipeIdFallback) : null);

            if (spec != null)
            {
                return PizzaOrderFormatter.FormatTicketLineCompact(qty, spec, catalog);
            }

            string rName = catalog != null && catalog.Recipes.TryGetValue(recipeIdFallback, out var r) ? r.Name : $"Pizza #{recipeIdFallback}";
            return $"{qty}x {rName}";
        }

        public void BindOrder(NetOrderDto order, double serverTime, DefinitionCatalog catalog)
        {
            bool isNewOrder = !HasOrder || _boundOrderId != order.OrderId;
            Order = order;
            _boundOrderId = order.OrderId;
            HasOrder = true;
            gameObject.SetActive(true);

            bool isTerminal = order.Lifecycle == (byte)OrderLifecycle.Delivered ||
                              order.Lifecycle == (byte)OrderLifecycle.Cancelled;
            double remaining = order.DeadlineTime - serverTime;

            if (isNewOrder)
            {
                _hasFiredDeadlineWarning = false;
                _isUrgentPulseActive = false;
                _entryFlutterTimer = EntryFlutterDuration;
                GameFeelPresenter.EnsureInstance().TriggerSimple(
                    GameFeelEventId.OrderPosted,
                    ResolveVisualPivot() ?? transform,
                    order.OrderId,
                    0,
                    (uint)order.OrderId,
                    GameFeelMaterialFamily.CardboardPaper,
                    1f,
                    false);
            }

            if (isTerminal)
            {
                _isUrgentPulseActive = false;
            }
            else
            {
                if (remaining > 0 && remaining < UrgentEnterSeconds)
                {
                    _isUrgentPulseActive = true;
                    if (!_hasFiredDeadlineWarning)
                    {
                        _hasFiredDeadlineWarning = true;
                        GameFeelPresenter.EnsureInstance().TriggerSimple(
                            GameFeelEventId.OrderDeadlineWarning,
                            ResolveVisualPivot() ?? transform,
                            order.OrderId,
                            0,
                            (uint)order.OrderId,
                            GameFeelMaterialFamily.CardboardPaper,
                            1f,
                            false);
                    }
                }
                else if (remaining >= UrgentExitSeconds || remaining <= 0)
                {
                    _isUrgentPulseActive = false;
                }
            }

            RefreshPaperColor();

            if (textMesh == null) return;

            var sb = new StringBuilder();
            sb.AppendLine($"<size=120%><b>#{order.OrderId}</b></size>");
            string destColor = order.IsDineIn ? "#005599" : "#990000";
            sb.AppendLine($"<color={destColor}><b>{FormatOrderDestinationHeader(order)}</b></color>");
            sb.AppendLine("<size=70%>------------------</size>");

            if (order.LineCount > 0 && order.Qty_0 > 0)
            {
                sb.AppendLine($"• {FormatOrderLineCompact(order.Spec_0, order.RecipeId_0, order.Qty_0, catalog)}");
            }
            if (order.LineCount > 1 && order.Qty_1 > 0)
            {
                sb.AppendLine($"• {FormatOrderLineCompact(order.Spec_1, order.RecipeId_1, order.Qty_1, catalog)}");
            }
            if (order.LineCount > 2 && order.Qty_2 > 0)
            {
                sb.AppendLine($"• {FormatOrderLineCompact(order.Spec_2, order.RecipeId_2, order.Qty_2, catalog)}");
            }

            sb.AppendLine("<size=70%>------------------</size>");

            if (order.Lifecycle == (byte)OrderLifecycle.Delivered)
            {
                sb.AppendLine("<color=green><b>CONCLUÍDO</b></color>");
            }
            else if (order.Lifecycle == (byte)OrderLifecycle.Cancelled)
            {
                sb.AppendLine("<color=red><b>CANCELADO</b></color>");
            }
            else
            {
                if (remaining > 0)
                {
                    int mins = (int)(remaining / 60);
                    int secs = (int)(remaining % 60);
                    string colorTag = remaining > 120 ? "<color=#008800>" : (remaining > 45 ? "<color=#CC8800>" : "<color=#CC0000>");
                    sb.AppendLine($"{colorTag}<b>⏱ {mins:00}:{secs:00}</b></color>");
                }
                else
                {
                    sb.AppendLine("<color=red><b>EXPIRADO</b></color>");
                }
            }

            textMesh.text = sb.ToString();
        }

        private void Update()
        {
            if (_isUrgentPulseActive)
            {
                RefreshPaperColor();
            }

            if (_entryFlutterTimer > 0f)
            {
                _entryFlutterTimer = Mathf.Max(0f, _entryFlutterTimer - Time.deltaTime);
                UpdateEntryFlutter();
            }
        }

        public void Clear()
        {
            HasOrder = false;
            _boundOrderId = 0;
            _isUrgentPulseActive = false;
            _hasFiredDeadlineWarning = false;
            _entryFlutterTimer = 0f;
            ResetVisualPivotToRest();
            ApplyPaperTint(_basePaperColor);
            gameObject.SetActive(false);
            if (textMesh != null) textMesh.text = string.Empty;
        }

        public void OnInteract(GameObject interactor)
        {
            if (!HasOrder) return;

            GameFeelPresenter.EnsureInstance().TriggerSimple(
                GameFeelEventId.OrderInspected,
                ResolveVisualPivot() ?? transform,
                Order.OrderId,
                0,
                (uint)Order.OrderId,
                GameFeelMaterialFamily.CardboardPaper,
                1f,
                true);

            if (TicketModalPresenter.Instance != null)
            {
                TicketModalPresenter.Instance.Open(Order);
            }
        }

        public void OnFocus()
        {
            _isFocused = true;
            RefreshPaperColor();
        }

        public void OnLoseFocus()
        {
            _isFocused = false;
            RefreshPaperColor();
        }

        private Transform ResolveVisualPivot()
        {
            if (sheetVisualPivot != null && sheetVisualPivot != transform)
                return sheetVisualPivot;
            if (paperRenderer != null && paperRenderer.transform != transform)
                return paperRenderer.transform;
            return null;
        }

        private void CaptureVisualPivotIfSafe()
        {
            if (_pivotCaptured) return;
            var pivot = ResolveVisualPivot();
            if (pivot == null) return;
            _pivotBaseLocalPos = pivot.localPosition;
            _pivotBaseLocalRot = pivot.localRotation;
            _pivotBaseLocalScale = pivot.localScale;
            _pivotCaptured = true;
        }

        private void UpdateEntryFlutter()
        {
            var pivot = ResolveVisualPivot();
            if (pivot == null) return;
            CaptureVisualPivotIfSafe();

            if (_entryFlutterTimer <= 0f || GameFeelSettings.ReduceMotion)
            {
                ResetVisualPivotToRest();
                return;
            }

            float t = 1f - (_entryFlutterTimer / EntryFlutterDuration);
            float intensity = GameFeelSettings.GetEffectiveChannelIntensity(GameFeelChannel.Movement);
            float flutter = Mathf.Sin(t * Mathf.PI * 2.5f) * Mathf.Exp(-3.5f * t) * 8f * intensity;
            float stretchY = 1f + Mathf.Sin(t * Mathf.PI) * 0.08f * intensity;
            float stretchX = 1f - Mathf.Sin(t * Mathf.PI) * 0.04f * intensity;

            pivot.localPosition = _pivotBaseLocalPos + new Vector3(0f, Mathf.Sin(t * Mathf.PI) * 0.015f * intensity, 0f);
            pivot.localRotation = _pivotBaseLocalRot * Quaternion.Euler(flutter * 0.5f, 0f, flutter);
            pivot.localScale = Vector3.Scale(_pivotBaseLocalScale, new Vector3(stretchX, stretchY, 1f));
        }

        private void ResetVisualPivotToRest()
        {
            if (!_pivotCaptured) return;
            var pivot = ResolveVisualPivot();
            if (pivot == null) return;
            pivot.localPosition = _pivotBaseLocalPos;
            pivot.localRotation = _pivotBaseLocalRot;
            pivot.localScale = _pivotBaseLocalScale;
        }

        private void RefreshPaperColor()
        {
            if (_isFocused)
            {
                ApplyPaperTint(_focusedColor);
                return;
            }

            if (_isUrgentPulseActive)
            {
                float weight = (GameFeelSettings.AllowFlashes && !GameFeelSettings.ReduceMotion)
                    ? (0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.time * 5.5f)))
                    : 0.75f;
                ApplyPaperTint(Color.Lerp(_basePaperColor, _urgentPaperColor, weight));
                return;
            }

            ApplyPaperTint(_basePaperColor);
        }

        private void ApplyPaperTint(Color color)
        {
            if (paperRenderer == null) return;
            _paperMpb ??= new MaterialPropertyBlock();
            int slots = paperRenderer.sharedMaterials != null ? Mathf.Max(1, paperRenderer.sharedMaterials.Length) : 1;
            for (int s = 0; s < slots; s++)
            {
                VisualStyleShaderUtility.ApplyColorBlockToSlot(paperRenderer, s, color, _paperMpb);
            }
        }

        private void OnDisable()
        {
            _entryFlutterTimer = 0f;
            ResetVisualPivotToRest();
        }

        private void OnDestroy()
        {
            ResetVisualPivotToRest();
        }
    }
}
