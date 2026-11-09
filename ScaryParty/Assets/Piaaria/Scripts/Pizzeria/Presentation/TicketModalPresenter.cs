using System;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Presentation
{
    public class TicketModalPresenter : MonoBehaviour
    {
        public static TicketModalPresenter Instance { get; private set; }

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _modalOverlay;
        private VisualElement _sheetBox;

        private Label _titleLabel;
        private Label _addressLabel;
        private Label _timerLabel;
        private VisualElement _linesContainer;
        private VisualElement _archiveBar;
        private Label _archiveHintLabel;

        private NetOrderDto _currentOrder;
        private bool _isOpen = false;
        private int _openedFrame = -1;
        private float _archiveHoldTimer = 0f;
        private const float ArchiveHoldRequired = 1.0f;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }
        }

        private void Start()
        {
            EnsureUIInitialized();
        }

        private void EnsureUIInitialized()
        {
            if (_modalOverlay != null) return;

            if (UIManager.Instance != null && UIManager.Instance.uiDocument != null && UIManager.Instance.uiDocument.rootVisualElement != null)
            {
                _root = UIManager.Instance.uiDocument.rootVisualElement;
            }
            else
            {
                _doc = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
                if (_doc.panelSettings == null)
                {
                    var panelSettingsList = Resources.FindObjectsOfTypeAll<PanelSettings>();
                    if (panelSettingsList != null && panelSettingsList.Length > 0)
                        _doc.panelSettings = panelSettingsList[0];
                }
                _root = _doc.rootVisualElement;
            }

            if (_root == null) return;

            BuildModalHierarchy();
        }

        private void BuildModalHierarchy()
        {
            _modalOverlay = new VisualElement();
            _modalOverlay.name = "TicketModalOverlay";
            _modalOverlay.style.position = Position.Absolute;
            _modalOverlay.style.left = 0;
            _modalOverlay.style.top = 0;
            _modalOverlay.style.right = 0;
            _modalOverlay.style.bottom = 0;
            _modalOverlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            _modalOverlay.style.justifyContent = Justify.Center;
            _modalOverlay.style.alignItems = Align.Center;
            _modalOverlay.style.display = DisplayStyle.None;

            _sheetBox = new VisualElement();
            _sheetBox.name = "TicketSheetCard";
            _sheetBox.style.width = 600;
            _sheetBox.style.backgroundColor = new Color(0.96f, 0.94f, 0.88f, 1f);
            if (ScaryParty.UI.Piaria.Core.PiariaUIAssetCatalog.Instance?.paperPanel1 != null)
            {
                _sheetBox.style.backgroundImage = new StyleBackground(ScaryParty.UI.Piaria.Core.PiariaUIAssetCatalog.Instance.paperPanel1);
            }
            _sheetBox.style.borderTopWidth = 3;
            _sheetBox.style.borderBottomWidth = 3;
            _sheetBox.style.borderLeftWidth = 3;
            _sheetBox.style.borderRightWidth = 3;
            _sheetBox.style.borderTopColor = new Color(0.35f, 0.25f, 0.15f);
            _sheetBox.style.borderBottomColor = new Color(0.35f, 0.25f, 0.15f);
            _sheetBox.style.borderLeftColor = new Color(0.35f, 0.25f, 0.15f);
            _sheetBox.style.borderRightColor = new Color(0.35f, 0.25f, 0.15f);
            _sheetBox.style.borderTopLeftRadius = 10;
            _sheetBox.style.borderTopRightRadius = 10;
            _sheetBox.style.borderBottomLeftRadius = 10;
            _sheetBox.style.borderBottomRightRadius = 10;
            _sheetBox.style.paddingLeft = 28;
            _sheetBox.style.paddingRight = 28;
            _sheetBox.style.paddingTop = 24;
            _sheetBox.style.paddingBottom = 24;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 14;

            _titleLabel = new Label("COMANDA #0");
            _titleLabel.style.fontSize = 28;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = new Color(0.2f, 0.15f, 0.1f);
            header.Add(_titleLabel);

            var closeBtn = new Button(Close);
            closeBtn.text = "✕";
            closeBtn.style.fontSize = 22;
            closeBtn.style.width = 38;
            closeBtn.style.height = 38;
            closeBtn.style.backgroundColor = new Color(0.85f, 0.3f, 0.25f);
            closeBtn.style.color = Color.white;
            header.Add(closeBtn);

            _sheetBox.Add(header);

            _addressLabel = new Label("DESTINO: Endereço 1");
            _addressLabel.style.fontSize = 24;
            _addressLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _addressLabel.style.color = new Color(0.7f, 0.1f, 0.1f);
            _addressLabel.style.marginBottom = 10;
            _sheetBox.Add(_addressLabel);

            var divider1 = new VisualElement();
            divider1.style.height = 2;
            divider1.style.backgroundColor = new Color(0.7f, 0.65f, 0.55f);
            divider1.style.marginBottom = 14;
            _sheetBox.Add(divider1);

            var itemsHeader = new Label("COMPOSIÇÃO DO PEDIDO (INGREDIENTES):");
            itemsHeader.style.fontSize = 18;
            itemsHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            itemsHeader.style.color = new Color(0.3f, 0.25f, 0.2f);
            itemsHeader.style.marginBottom = 8;
            _sheetBox.Add(itemsHeader);

            _linesContainer = new VisualElement();
            _linesContainer.style.marginBottom = 18;
            _sheetBox.Add(_linesContainer);

            var divider2 = new VisualElement();
            divider2.style.height = 2;
            divider2.style.backgroundColor = new Color(0.7f, 0.65f, 0.55f);
            divider2.style.marginBottom = 14;
            _sheetBox.Add(divider2);

            _timerLabel = new Label("TEMPO RESTANTE: 05:00");
            _timerLabel.style.fontSize = 22;
            _timerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _timerLabel.style.color = new Color(0.1f, 0.5f, 0.1f);
            _timerLabel.style.marginBottom = 18;
            _sheetBox.Add(_timerLabel);

            _archiveHintLabel = new Label("[Segurar F] para arquivar comanda");
            _archiveHintLabel.style.fontSize = 17;
            _archiveHintLabel.style.color = new Color(0.4f, 0.35f, 0.3f);
            _archiveHintLabel.style.marginBottom = 6;
            _sheetBox.Add(_archiveHintLabel);

            var barBg = new VisualElement();
            barBg.style.height = 12;
            barBg.style.backgroundColor = new Color(0.8f, 0.75f, 0.7f);
            barBg.style.borderTopLeftRadius = 6;
            barBg.style.borderTopRightRadius = 6;
            barBg.style.borderBottomLeftRadius = 6;
            barBg.style.borderBottomRightRadius = 6;
            barBg.style.marginBottom = 18;

            _archiveBar = new VisualElement();
            _archiveBar.style.height = 12;
            _archiveBar.style.width = 0;
            _archiveBar.style.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
            _archiveBar.style.borderTopLeftRadius = 6;
            _archiveBar.style.borderTopRightRadius = 6;
            _archiveBar.style.borderBottomLeftRadius = 6;
            _archiveBar.style.borderBottomRightRadius = 6;
            barBg.Add(_archiveBar);
            _sheetBox.Add(barBg);

            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.FlexEnd;

            var closeButtonFooter = new Button(Close);
            closeButtonFooter.text = "Fechar [E / Esc]";
            closeButtonFooter.style.fontSize = 18;
            closeButtonFooter.style.paddingLeft = 20;
            closeButtonFooter.style.paddingRight = 20;
            closeButtonFooter.style.paddingTop = 10;
            closeButtonFooter.style.paddingBottom = 10;
            footer.Add(closeButtonFooter);

            _sheetBox.Add(footer);

            _modalOverlay.Add(_sheetBox);
            _root.Add(_modalOverlay);
        }

        public void Open(NetOrderDto order)
        {
            EnsureUIInitialized();
            if (_modalOverlay == null) return;

            _currentOrder = order;
            _isOpen = true;
            _openedFrame = Time.frameCount;
            _archiveHoldTimer = 0f;
            _modalOverlay.style.display = DisplayStyle.Flex;

            GameplayInputGate.PushLock();
            PopulateOrderData();
        }

        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _archiveHoldTimer = 0f;
            if (_modalOverlay != null)
            {
                _modalOverlay.style.display = DisplayStyle.None;
            }

            GameplayInputGate.PopLock();
        }

        private void PopulateOrderData()
        {
            if (_titleLabel != null)
            {
                string channelTag = _currentOrder.IsDineIn ? "SALÃO" : "DELIVERY";
                _titleLabel.text = $"COMANDA #{_currentOrder.OrderId} ({channelTag})";
            }

            if (_addressLabel != null)
            {
                _addressLabel.text = $"DESTINO: {TicketSheetView.FormatOrderDestinationHeader(_currentOrder)}";
                _addressLabel.style.color = _currentOrder.IsDineIn
                    ? new Color(0.1f, 0.35f, 0.65f)
                    : new Color(0.7f, 0.1f, 0.1f);
            }

            if (_linesContainer != null)
            {
                _linesContainer.Clear();
                var catalog = PizzeriaRoot.Instance?.Catalog;

                if (_currentOrder.LineCount > 0 && _currentOrder.Qty_0 > 0)
                    AddLineItem(_currentOrder.Spec_0, _currentOrder.RecipeId_0, _currentOrder.Qty_0, catalog);
                if (_currentOrder.LineCount > 1 && _currentOrder.Qty_1 > 0)
                    AddLineItem(_currentOrder.Spec_1, _currentOrder.RecipeId_1, _currentOrder.Qty_1, catalog);
                if (_currentOrder.LineCount > 2 && _currentOrder.Qty_2 > 0)
                    AddLineItem(_currentOrder.Spec_2, _currentOrder.RecipeId_2, _currentOrder.Qty_2, catalog);
            }

            UpdateTimerDisplay();
        }

        private void AddLineItem(NetPizzaSpecDto specDto, int recipeIdFallback, int qty, DefinitionCatalog catalog)
        {
            var spec = specDto.BaseDoughId > 0 || specDto.ToppingCount > 0
                ? specDto.ToDomain()
                : (catalog != null ? catalog.ResolveSpecificationFromRecipeOrDefault(recipeIdFallback) : null);

            string shortTitle = spec != null
                ? PizzaOrderFormatter.FormatShortTitle(spec, catalog, includePrefix: true)
                : (catalog != null && catalog.Recipes.TryGetValue(recipeIdFallback, out var r) ? r.Name : $"Pizza #{recipeIdFallback}");

            string detailLine = spec != null
                ? PizzaOrderFormatter.FormatDetailedIngredients(spec, catalog)
                : "Massa Aberta + Molho de Tomate + Queijo Ralado";

            var itemBox = new VisualElement();
            itemBox.style.marginBottom = 8;
            itemBox.style.paddingTop = 6;
            itemBox.style.paddingBottom = 6;
            itemBox.style.paddingLeft = 10;
            itemBox.style.paddingRight = 10;
            itemBox.style.backgroundColor = new Color(0.90f, 0.87f, 0.78f, 0.75f);
            itemBox.style.borderTopLeftRadius = 6;
            itemBox.style.borderTopRightRadius = 6;
            itemBox.style.borderBottomLeftRadius = 6;
            itemBox.style.borderBottomRightRadius = 6;

            var nameLbl = new Label($"• {qty}x {shortTitle}");
            nameLbl.style.fontSize = 20;
            nameLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLbl.style.color = new Color(0.2f, 0.15f, 0.1f);
            itemBox.Add(nameLbl);

            var detailLbl = new Label($"   Ingredientes: {detailLine}");
            detailLbl.style.fontSize = 15;
            detailLbl.style.color = new Color(0.35f, 0.28f, 0.2f);
            itemBox.Add(detailLbl);

            string packNote = _currentOrder.IsDineIn
                ? "   Serviço: Entregar na mesa SEM caixa (prato/mão)"
                : "   Serviço: Embalar na CAIXA e etiquetar endereço";
            var packLbl = new Label(packNote);
            packLbl.style.fontSize = 14;
            packLbl.style.color = _currentOrder.IsDineIn ? new Color(0.1f, 0.4f, 0.65f) : new Color(0.55f, 0.25f, 0.1f);
            itemBox.Add(packLbl);

            _linesContainer.Add(itemBox);
        }

        private void UpdateTimerDisplay()
        {
            if (_timerLabel == null) return;

            if (_currentOrder.Lifecycle == (byte)OrderLifecycle.Delivered)
            {
                _timerLabel.text = "STATUS: PEDIDO CONCLUÍDO ✅";
                _timerLabel.style.color = new Color(0.1f, 0.6f, 0.1f);
                return;
            }

            if (_currentOrder.Lifecycle == (byte)OrderLifecycle.Cancelled)
            {
                _timerLabel.text = "STATUS: PEDIDO CANCELADO ❌";
                _timerLabel.style.color = new Color(0.7f, 0.1f, 0.1f);
                return;
            }

            double now = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening
                ? NetworkManager.Singleton.ServerTime.Time
                : Time.time;

            double remaining = _currentOrder.DeadlineTime - now;
            if (remaining > 0)
            {
                int mins = (int)(remaining / 60);
                int secs = (int)(remaining % 60);
                _timerLabel.text = $"TEMPO RESTANTE: {mins:00}:{secs:00}";

                if (remaining > 120)
                    _timerLabel.style.color = new Color(0.1f, 0.6f, 0.1f);
                else if (remaining > 45)
                    _timerLabel.style.color = new Color(0.8f, 0.5f, 0.0f);
                else
                    _timerLabel.style.color = new Color(0.8f, 0.1f, 0.1f);
            }
            else
            {
                _timerLabel.text = "TEMPO ESGOTADO! (EXPIRADO)";
                _timerLabel.style.color = new Color(0.8f, 0.1f, 0.1f);
            }
        }

        private void Update()
        {
            if (!_isOpen) return;
            if (Time.frameCount == _openedFrame) return;

            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.escapeKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)
                {
                    Close();
                    return;
                }

                bool isDeliveredOrDone = _currentOrder.Lifecycle == (byte)OrderLifecycle.Delivered ||
                                         _currentOrder.Lifecycle == (byte)OrderLifecycle.Cancelled ||
                                         _currentOrder.Lifecycle == (byte)OrderLifecycle.Expired;

                if (isDeliveredOrDone && kb.fKey.isPressed)
                {
                    _archiveHoldTimer += Time.deltaTime;
                    float progress = Mathf.Clamp01(_archiveHoldTimer / ArchiveHoldRequired);
                    if (_archiveBar != null)
                    {
                        _archiveBar.style.width = new Length(progress * 100f, LengthUnit.Percent);
                    }

                    if (_archiveHoldTimer >= ArchiveHoldRequired)
                    {
                        TriggerArchive();
                    }
                }
                else
                {
                    _archiveHoldTimer = 0f;
                    if (_archiveBar != null)
                    {
                        _archiveBar.style.width = 0;
                    }
                }
            }

            UpdateTimerDisplay();
        }

        private void TriggerArchive()
        {
            if (OrderCommands.Instance != null)
            {
                OrderCommands.Instance.ArchiveTicketDirectServerRpc(_currentOrder.OrderId);
            }

            NotificationManager.Show($"Comanda #{_currentOrder.OrderId} arquivada!", NotificationType.Success);
            Close();
        }

        private void OnDestroy()
        {
            if (_isOpen)
            {
                GameplayInputGate.Reset();
            }
            if (Instance == this) Instance = null;
        }
    }
}
