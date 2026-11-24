using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Stations;
using ScaryParty.UI.Piaria.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ScaryParty.UI.Piaria.Presenters
{
    /// <summary>
    /// Central de Operações da Pizzaria (P4): acessível via [J] ou Terminal de Suprimentos.
    /// Abas reais integradas ao domínio e Netcode: Pedidos, Reposição, Loja e Funcionários.
    /// </summary>
    public class OperationsHubPresenter : MonoBehaviour, IModalView
    {
        public static OperationsHubPresenter Instance { get; private set; }

        public bool CanCloseWithEscape => _isOpen;
        public VisualElement RootElement => _overlay;
        public bool IsOpen => _isOpen;

        public static OperationsHubPresenter EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<OperationsHubPresenter>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var hud = FindFirstObjectByType<HudPresenter>();
            if (hud != null)
                return hud.gameObject.AddComponent<OperationsHubPresenter>();

            var go = new GameObject("OperationsHubPresenter");
            return go.AddComponent<OperationsHubPresenter>();
        }

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _overlay;
        private Label _budgetHeaderLabel;
        private Label _phaseHeaderLabel;
        private Button _tabOrdersBtn;
        private Button _tabSuppliesBtn;
        private Button _tabShopBtn;
        private Button _tabEmployeesBtn;
        private ScrollView _contentScroll;

        private string _activeTab = "orders";
        private bool _isOpen;
        private bool _pushedToStack;
        private bool _pushedGateFallback;
        private float _refreshTimer;
        private string _pendingOfferId = string.Empty;
        private string _pendingRequestId = string.Empty;
        private float _pendingStartedAtRealtime;
        private int _hubRequestSeq;
        private float _pendingEmployeeActionUntil;
        private bool _subscribedShopEvents;
        private PizzeriaCommandHandler _subscribedHandler;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName.IndexOf("Lobby", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sceneName.IndexOf("Ready", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            UnsubscribeShopPurchaseEvents();
            if (_isOpen)
            {
                _isOpen = false;
                ReleaseModalLock();
            }
            if (Instance == this) Instance = null;
        }

        private void EnsureSubscribedShopPurchaseEvents()
        {
            var handler = PizzeriaCommandHandler.Instance;
            if (handler == null) return;
            if (_subscribedShopEvents && ReferenceEquals(_subscribedHandler, handler)) return;

            UnsubscribeShopPurchaseEvents();
            _subscribedHandler = handler;
            _subscribedHandler.OnShopPurchaseResultReceived += HandleShopPurchaseResult;
            _subscribedShopEvents = true;
        }

        private void UnsubscribeShopPurchaseEvents()
        {
            if (_subscribedHandler != null)
            {
                _subscribedHandler.OnShopPurchaseResultReceived -= HandleShopPurchaseResult;
                _subscribedHandler = null;
            }
            _subscribedShopEvents = false;
        }

        private void HandleShopPurchaseResult(bool success, string offerId, string requestId, string message)
        {
            if (string.IsNullOrEmpty(_pendingOfferId)) return;
            if (!string.IsNullOrEmpty(requestId) && !string.Equals(requestId, _pendingRequestId, StringComparison.Ordinal))
                return;

            _pendingOfferId = string.Empty;
            _pendingRequestId = string.Empty;
            _pendingStartedAtRealtime = 0f;
            if (_isOpen) RefreshCurrentTab();
        }

        public void Open(string tab = "orders")
        {
            EnsureUIInitialized();
            if (_overlay == null) return;

            EnsureSubscribedShopPurchaseEvents();
            if (!string.IsNullOrEmpty(tab))
                _activeTab = tab;

            if (!_isOpen)
            {
                _isOpen = true;
                _overlay.style.display = DisplayStyle.Flex;
                AcquireModalLock();
            }

            UpdateTabButtons();
            RefreshCurrentTab();
        }

        public void Toggle(string tab = null)
        {
            if (_isOpen) Close();
            else Open(tab ?? _activeTab);
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            _pendingOfferId = string.Empty;
            _pendingRequestId = string.Empty;
            _pendingEmployeeActionUntil = 0f;
            UnsubscribeShopPurchaseEvents();
            if (_overlay != null)
                _overlay.style.display = DisplayStyle.None;
            ReleaseModalLock();
        }

        private void AcquireModalLock()
        {
            if (UIModalStack.Instance != null)
            {
                if (!_pushedToStack)
                {
                    UIModalStack.Instance.PushModal(this);
                    _pushedToStack = true;
                }
            }
            else if (!_pushedGateFallback)
            {
                GameplayInputGate.PushModal();
                _pushedGateFallback = true;
            }
        }

        private void ReleaseModalLock()
        {
            if (_pushedToStack)
            {
                _pushedToStack = false;
                UIModalStack.Instance?.PopModal(this);
            }
            if (_pushedGateFallback)
            {
                _pushedGateFallback = false;
                GameplayInputGate.PopModal();
            }
        }

        private void Update()
        {
            if (_isOpen)
            {
                GameplayInputGate.MaintainMenuCursorUnlocked();
            }
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame)
            {
                if (_isOpen)
                {
                    Close();
                    return;
                }
                if (!GameplayInputGate.IsLocked &&
                    (PauseEscapeMenuPresenter.Instance == null || !PauseEscapeMenuPresenter.Instance.IsOpen))
                {
                    Open(_activeTab);
                    return;
                }
            }
#endif
            if (!_isOpen) return;

            EnsureSubscribedShopPurchaseEvents();
            if (!string.IsNullOrEmpty(_pendingOfferId) && Time.realtimeSinceStartup - _pendingStartedAtRealtime > 8f)
            {
                _pendingOfferId = string.Empty;
                _pendingRequestId = string.Empty;
                RefreshCurrentTab();
            }

            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= 0.45f)
            {
                _refreshTimer = 0f;
                RefreshCurrentTab();
            }
        }

        private void EnsureUIInitialized()
        {
            if (_overlay != null) return;

            var hud = FindFirstObjectByType<HudPresenter>();
            _doc = hud != null ? hud.uiDocument : GetComponent<UIDocument>();
            if (_doc == null) _doc = gameObject.AddComponent<UIDocument>();
            if (_doc.panelSettings == null)
            {
                var ps = Resources.FindObjectsOfTypeAll<PanelSettings>();
                if (ps != null && ps.Length > 0) _doc.panelSettings = ps[0];
            }

            _root = _doc.rootVisualElement;
            if (_root == null) return;

            _overlay = new VisualElement { name = "OperationsHubOverlay" };
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0;
            _overlay.style.top = 0;
            _overlay.style.right = 0;
            _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new Color(0.03f, 0.04f, 0.06f, 0.80f);
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.display = DisplayStyle.None;

            var card = new VisualElement { name = "OperationsHubCard" };
            card.style.width = Length.Percent(84);
            card.style.maxWidth = 940;
            card.style.height = Length.Percent(82);
            card.style.maxHeight = 680;
            card.style.backgroundColor = new Color(0.11f, 0.12f, 0.16f, 0.98f);
            card.style.borderTopWidth = 2;
            card.style.borderBottomWidth = 2;
            card.style.borderLeftWidth = 2;
            card.style.borderRightWidth = 2;
            var border = new Color(0.95f, 0.72f, 0.25f);
            card.style.borderTopColor = border;
            card.style.borderBottomColor = border;
            card.style.borderLeftColor = border;
            card.style.borderRightColor = border;
            card.style.borderTopLeftRadius = 12;
            card.style.borderTopRightRadius = 12;
            card.style.borderBottomLeftRadius = 12;
            card.style.borderBottomRightRadius = 12;
            card.style.paddingLeft = 20;
            card.style.paddingRight = 20;
            card.style.paddingTop = 16;
            card.style.paddingBottom = 16;

            var topBar = new VisualElement();
            topBar.style.flexDirection = FlexDirection.Row;
            topBar.style.justifyContent = Justify.SpaceBetween;
            topBar.style.alignItems = Align.Center;
            topBar.style.marginBottom = 8;

            var title = new Label("CENTRAL DE OPERAÇÕES [J]");
            title.style.fontSize = 22;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.98f, 0.82f, 0.36f);
            topBar.Add(title);

            var closeBtn = new Button(Close) { text = "Fechar [ESC]" };
            closeBtn.style.height = 34;
            closeBtn.style.paddingLeft = 14;
            closeBtn.style.paddingRight = 14;
            closeBtn.style.backgroundColor = new Color(0.55f, 0.22f, 0.22f);
            closeBtn.style.color = Color.white;
            closeBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            topBar.Add(closeBtn);
            card.Add(topBar);

            var infoRow = new VisualElement();
            infoRow.style.flexDirection = FlexDirection.Row;
            infoRow.style.justifyContent = Justify.SpaceBetween;
            infoRow.style.marginBottom = 10;

            _phaseHeaderLabel = new Label("Noite 1 — Em Andamento");
            _phaseHeaderLabel.style.fontSize = 14;
            _phaseHeaderLabel.style.color = new Color(0.84f, 0.88f, 0.94f);
            infoRow.Add(_phaseHeaderLabel);

            _budgetHeaderLabel = new Label("Caixa Coletivo: R$ 0");
            _budgetHeaderLabel.style.fontSize = 15;
            _budgetHeaderLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _budgetHeaderLabel.style.color = new Color(0.45f, 0.94f, 0.56f);
            infoRow.Add(_budgetHeaderLabel);
            card.Add(infoRow);

            var tabRow = new VisualElement();
            tabRow.style.flexDirection = FlexDirection.Row;
            tabRow.style.marginBottom = 10;

            _tabOrdersBtn = CreateTabButton("📋 Pedidos", "orders", tabRow);
            _tabSuppliesBtn = CreateTabButton("📦 Reposição", "supplies", tabRow);
            _tabShopBtn = CreateTabButton("🛠 Loja", "shop", tabRow);
            _tabEmployeesBtn = CreateTabButton("👥 Funcionários", "employees", tabRow);
            card.Add(tabRow);

            _contentScroll = new ScrollView { name = "OperationsHubScroll" };
            _contentScroll.style.flexGrow = 1;
            _contentScroll.style.backgroundColor = new Color(0.07f, 0.08f, 0.11f, 0.92f);
            _contentScroll.style.borderTopLeftRadius = 8;
            _contentScroll.style.borderTopRightRadius = 8;
            _contentScroll.style.borderBottomLeftRadius = 8;
            _contentScroll.style.borderBottomRightRadius = 8;
            _contentScroll.style.paddingLeft = 12;
            _contentScroll.style.paddingRight = 12;
            _contentScroll.style.paddingTop = 10;
            _contentScroll.style.paddingBottom = 10;
            card.Add(_contentScroll);

            _overlay.Add(card);
            _root.Add(_overlay);
        }

        private Button CreateTabButton(string label, string tabKey, VisualElement parent)
        {
            var btn = new Button(() =>
            {
                _activeTab = tabKey;
                UpdateTabButtons();
                RefreshCurrentTab();
            })
            {
                text = label
            };
            btn.style.flexGrow = 1;
            btn.style.height = 38;
            btn.style.marginRight = 6;
            btn.style.fontSize = 14;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.color = Color.white;
            btn.style.borderTopLeftRadius = 6;
            btn.style.borderTopRightRadius = 6;
            btn.style.borderBottomLeftRadius = 6;
            btn.style.borderBottomRightRadius = 6;
            parent.Add(btn);
            return btn;
        }

        private void UpdateTabButtons()
        {
            ApplyTabColor(_tabOrdersBtn, _activeTab == "orders");
            ApplyTabColor(_tabSuppliesBtn, _activeTab == "supplies");
            ApplyTabColor(_tabShopBtn, _activeTab == "shop");
            ApplyTabColor(_tabEmployeesBtn, _activeTab == "employees");
        }

        private static void ApplyTabColor(Button btn, bool active)
        {
            if (btn == null) return;
            btn.style.backgroundColor = active
                ? new Color(0.82f, 0.50f, 0.15f)
                : new Color(0.20f, 0.22f, 0.28f);
        }

        private void RefreshCurrentTab()
        {
            if (_contentScroll == null) return;
            _contentScroll.Clear();

            var net = PizzeriaNetworkState.Instance;
            if (net == null)
            {
                _contentScroll.Add(CreateEmptyLabel("Estado da pizzaria indisponível fora da sessão ativa."));
                return;
            }

            var night = net.NightState.Value;
            if (_budgetHeaderLabel != null)
                _budgetHeaderLabel.text = $"Caixa Coletivo: R$ {net.RestaurantBudget.Value} | Faturamento: R$ {night.EarnedRevenue}/{night.RevenueGoal}";

            if (_phaseHeaderLabel != null)
            {
                string nTag = night.NightNumber == 0 ? "Noite 0 (Tutorial)" : $"Noite {night.NightNumber}";
                _phaseHeaderLabel.text = $"{nTag} — Fase: {(NightPhase)night.Phase}";
            }

            switch (_activeTab)
            {
                case "supplies":
                    PopulateSuppliesTab(net);
                    break;
                case "shop":
                    PopulateShopTab(net);
                    break;
                case "employees":
                    PopulateEmployeesTab(net);
                    break;
                default:
                    PopulateOrdersTab(net);
                    break;
            }
        }

        private void PopulateOrdersTab(PizzeriaNetworkState net)
        {
            var catalog = PizzeriaRoot.Instance?.Catalog;
            double now = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening
                ? NetworkManager.Singleton.ServerTime.Time
                : Time.timeAsDouble;
            ulong localId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0UL;

            int activeShown = 0;
            for (int i = 0; i < net.Orders.Count; i++)
            {
                var ord = net.Orders[i];
                var lc = (OrderLifecycle)ord.Lifecycle;
                if (lc != OrderLifecycle.Accepted)
                    continue;

                activeShown++;
                var row = CreateCardRow();

                string channel = ord.IsDineIn ? "Salão" : "Telefone/Delivery";
                string dest = TicketSheetView.FormatOrderDestinationHeader(ord);
                int secLeft = Mathf.Max(0, Mathf.CeilToInt((float)(ord.DeadlineTime - now)));
                string ownerText = TicketSheetView.FormatClaimedOwner(ord.ClaimedByClientId);
                string stageText = TicketSheetView.FormatTaskStageBadge(ord.TaskStage);

                var header = new Label($"Pedido #{ord.OrderId} [{channel}] → {dest} | Prazo: {secLeft}s | Etapa: {stageText} | Resp.: {ownerText}");
                header.style.fontSize = 14;
                header.style.unityFontStyleAndWeight = FontStyle.Bold;
                header.style.color = new Color(0.98f, 0.88f, 0.45f);
                header.style.marginBottom = 4;
                row.Add(header);

                var linesSb = new StringBuilder();
                if (net.OrderLines != null)
                {
                    for (int l = 0; l < net.OrderLines.Count; l++)
                    {
                        var line = net.OrderLines[l];
                        if (line.OrderId != ord.OrderId) continue;
                        string compact = TicketSheetView.FormatOrderLineCompact(line.Spec, line.RecipeId, line.RequestedQuantity, catalog);
                        linesSb.AppendLine($"• {compact} — Entregues/Unidades: {line.DeliveredQuantity}/{line.RequestedQuantity}");
                    }
                }
                if (linesSb.Length == 0 && ord.LineCount > 0 && ord.Qty_0 > 0)
                {
                    linesSb.AppendLine($"• {TicketSheetView.FormatOrderLineCompact(ord.Spec_0, ord.RecipeId_0, ord.Qty_0, catalog)}");
                }

                var linesLbl = new Label(linesSb.ToString().TrimEnd());
                linesLbl.style.fontSize = 13;
                linesLbl.style.color = new Color(0.90f, 0.92f, 0.95f);
                linesLbl.style.whiteSpace = WhiteSpace.Normal;
                linesLbl.style.marginBottom = 6;
                row.Add(linesLbl);

                var btnRow = new VisualElement();
                btnRow.style.flexDirection = FlexDirection.Row;

                ulong capturedId = ord.OrderId;
                bool claimedByMe = ord.ClaimedByClientId == localId;
                var claimBtn = new Button(() =>
                {
                    PizzeriaCommandHandler.Instance?.ToggleClaimOrderServerRpc(capturedId);
                })
                {
                    text = claimedByMe ? "Liberar Pedido" : "Assumir Pedido"
                };
                claimBtn.style.height = 30;
                claimBtn.style.marginRight = 8;
                claimBtn.style.backgroundColor = claimedByMe ? new Color(0.65f, 0.42f, 0.16f) : new Color(0.18f, 0.46f, 0.72f);
                claimBtn.style.color = Color.white;
                btnRow.Add(claimBtn);

                var cancelBtn = new Button(() =>
                {
                    OrderCommands.Instance?.CancelOrderServerRpc(capturedId);
                })
                {
                    text = "Cancelar Pedido"
                };
                cancelBtn.style.height = 30;
                cancelBtn.style.backgroundColor = new Color(0.62f, 0.22f, 0.22f);
                cancelBtn.style.color = Color.white;
                btnRow.Add(cancelBtn);

                row.Add(btnRow);
                _contentScroll.Add(row);
            }

            if (activeShown == 0)
            {
                _contentScroll.Add(CreateEmptyLabel("Nenhum pedido ativo no momento. Atenda clientes no balcão ou o telefone!"));
            }
        }

        private void PopulateSuppliesTab(PizzeriaNetworkState net)
        {
            var catalog = PizzeriaRoot.Instance?.Catalog;
            int budget = net.RestaurantBudget.Value;
            float etaMult = net.RunState.Value.SupplyEtaMultiplier > 0.01f
                ? net.RunState.Value.SupplyEtaMultiplier
                : 1f;
            if (etaMult <= 1.001f &&
                PizzeriaRoot.Instance != null &&
                PizzeriaRoot.Instance.IsServer &&
                PizzeriaRoot.Instance.RunProgressionService != null)
            {
                float srvEta = PizzeriaRoot.Instance.RunProgressionService.RecalculateEffectiveStats().SupplyEtaMultiplier;
                if (srvEta > 0.01f) etaMult = srvEta;
            }

            var stockSection = CreateSectionHeader("Estoque Atual (Geladeira & Despensa)");
            _contentScroll.Add(stockSection);

            if (net.StorageSlots == null || net.StorageSlots.Count == 0)
            {
                _contentScroll.Add(CreateEmptyLabel("Nenhum compartimento de estoque registrado."));
            }
            else
            {
                for (int i = 0; i < net.StorageSlots.Count; i++)
                {
                    var slot = net.StorageSlots[i];
                    string ingName = catalog != null && catalog.Ingredients.TryGetValue(slot.IngredientDefId, out var ing)
                        ? ing.Name
                        : $"Ingrediente #{slot.IngredientDefId}";
                    string tag = slot.Quantity <= 0 ? "[ESGOTADO]" : (slot.Quantity <= 3 ? "[CRÍTICO]" : "[OK]");
                    var lbl = new Label($"• {ingName} (Armazém #{slot.StorageId}): {slot.Quantity}/{slot.MaxCapacity} {tag}");
                    lbl.style.fontSize = 13;
                    lbl.style.color = slot.Quantity <= 3 ? new Color(0.95f, 0.45f, 0.40f) : new Color(0.84f, 0.92f, 0.86f);
                    lbl.style.marginBottom = 3;
                    _contentScroll.Add(lbl);
                }
            }

            _contentScroll.Add(CreateSectionHeader("Catálogo de Reposição (Entrega na Doca / ReceivingStation)"));
            if (catalog == null || catalog.SupplyItems == null || catalog.SupplyItems.Count == 0)
            {
                _contentScroll.Add(CreateEmptyLabel("Nenhum item de reposição configurado no PizzeriaConfig."));
            }
            else
            {
                foreach (var kvp in catalog.SupplyItems)
                {
                    var sup = kvp.Value;
                    string supName = !string.IsNullOrEmpty(sup.Name)
                        ? sup.Name
                        : (catalog.Ingredients.TryGetValue(sup.IngredientId, out var ing) ? $"Pacote {ing.Name}" : $"Pacote #{sup.Id}");
                    int etaSec = Mathf.Max(2, Mathf.CeilToInt(sup.BaseETA * etaMult));
                    bool affordable = budget >= sup.PackPrice;

                    var row = CreateCardRow();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.justifyContent = Justify.SpaceBetween;
                    row.style.alignItems = Align.Center;

                    var info = new Label($"{supName} | +{sup.UnitsPerPack} un. | ETA ~{etaSec}s | R$ {sup.PackPrice}");
                    info.style.fontSize = 14;
                    info.style.color = affordable ? Color.white : new Color(0.92f, 0.58f, 0.58f);
                    row.Add(info);

                    int capturedSupId = sup.Id;
                    var orderBtn = new Button(() =>
                    {
                        PizzeriaCommandHandler.Instance?.PlaceSupplyOrderServerRpc(capturedSupId, 1);
                    })
                    {
                        text = affordable ? $"Encomendar (R$ {sup.PackPrice})" : "Saldo insuficiente"
                    };
                    orderBtn.SetEnabled(affordable);
                    orderBtn.style.height = 32;
                    orderBtn.style.backgroundColor = affordable ? new Color(0.18f, 0.56f, 0.34f) : new Color(0.40f, 0.24f, 0.24f);
                    orderBtn.style.color = Color.white;
                    row.Add(orderBtn);
                    _contentScroll.Add(row);
                }
            }

            _contentScroll.Add(CreateSectionHeader("Encomendas em Trânsito / Recebimento"));
            if (net.SupplyOrders == null || net.SupplyOrders.Count == 0)
            {
                _contentScroll.Add(CreateEmptyLabel("Nenhuma encomenda de suprimentos em andamento."));
            }
            else
            {
                double now = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening
                    ? NetworkManager.Singleton.ServerTime.Time
                    : Time.timeAsDouble;
                for (int i = 0; i < net.SupplyOrders.Count; i++)
                {
                    var so = net.SupplyOrders[i];
                    var st = (SupplyOrderLifecycle)so.State;
                    int secsLeft = Mathf.Max(0, Mathf.CeilToInt((float)(so.ExpectedArrivalTime - now)));
                    string desc = st switch
                    {
                        SupplyOrderLifecycle.InTransit => $"Em trânsito ({secsLeft}s restantes)",
                        SupplyOrderLifecycle.WaitingForReceivingSlot => "Aguardando baía de recebimento",
                        SupplyOrderLifecycle.Arrived => $"Na Baía #{so.ReceivingSlotIndex + 1} (Receiving)",
                        _ => "Descarregado no estoque"
                    };
                    string sum = so.Summary.Length > 0 ? so.Summary.ToString() : $"Ingrediente #{so.PrimaryIngredientDefId} (+{so.TotalOrderedUnits}un)";
                    var lbl = new Label($"• Encomenda #{so.SupplyOrderId} — {sum} | R$ {so.TotalCost} | {desc}");
                    lbl.style.fontSize = 13;
                    lbl.style.color = st == SupplyOrderLifecycle.Unloaded ? new Color(0.55f, 0.85f, 0.60f) : new Color(0.95f, 0.85f, 0.45f);
                    lbl.style.marginBottom = 3;
                    _contentScroll.Add(lbl);
                }
            }
        }

        private void PopulateShopTab(PizzeriaNetworkState net)
        {
            int budget = net.RestaurantBudget.Value;
            _contentScroll.Add(CreateSectionHeader("Loja da Pizzaria & Upgrades da RUN (Caixa Coletivo)"));

            if (net.RunShopOffers != null && net.RunShopOffers.Count > 0)
            {
                for (int i = 0; i < net.RunShopOffers.Count; i++)
                {
                    var offer = net.RunShopOffers[i];
                    if ((ShopBenefitKind)offer.BenefitKind == ShopBenefitKind.HireEmployee) continue;
                    AddDynamicShopRow(offer, budget);
                }
            }
            else
            {
                var infoLbl = new Label("Catalogo de ofertas da loja aguardando replicacao do servidor.");
                infoLbl.style.color = new Color(0.85f, 0.72f, 0.45f);
                _contentScroll.Add(infoLbl);
            }
        }

        private void AddDynamicShopRow(NetRunShopOfferDto offer, int budget)
        {
            bool hasCap = offer.MaxLevel > 0;
            bool maxed = hasCap && offer.CurrentLevel >= offer.MaxLevel;
            bool affordable = budget >= offer.EffectiveCost;
            bool canBuy = offer.CanPurchase && !maxed && affordable;

            var row = CreateCardRow();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.flexWrap = Wrap.Wrap;

            var textCol = new VisualElement();
            textCol.style.flexGrow = 1;
            textCol.style.flexShrink = 1;
            string stackLabel = hasCap ? $"[{offer.CurrentLevel}/{offer.MaxLevel}]" : "[Consumível]";
            var tLbl = new Label($"{offer.DisplayName} {stackLabel} — R$ {offer.EffectiveCost}");
            tLbl.style.fontSize = 14;
            tLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            tLbl.style.color = maxed ? new Color(0.55f, 0.90f, 0.58f) : Color.white;
            textCol.Add(tLbl);

            var dLbl = new Label(offer.Description.ToString());
            dLbl.style.fontSize = 12;
            dLbl.style.color = new Color(0.76f, 0.80f, 0.86f);
            dLbl.style.whiteSpace = WhiteSpace.Normal;
            textCol.Add(dLbl);

            if (!canBuy && offer.LockReason.Length > 0)
            {
                var lockLbl = new Label(offer.LockReason.ToString());
                lockLbl.style.fontSize = 11;
                lockLbl.style.color = new Color(0.92f, 0.56f, 0.48f);
                textCol.Add(lockLbl);
            }
            row.Add(textCol);

            string offerIdStr = offer.OfferId.ToString();
            bool hasPending = !string.IsNullOrEmpty(_pendingOfferId);
            bool isThisPending = string.Equals(_pendingOfferId, offerIdStr, StringComparison.Ordinal);
            bool effectiveCanBuy = canBuy && !hasPending;
            string btnText = isThisPending
                ? "..."
                : (maxed
                    ? "MAXIMIZADO"
                    : (hasPending
                        ? "AGUARDE..."
                        : (effectiveCanBuy ? $"Comprar (R$ {offer.EffectiveCost})" : $"Indisp. (R$ {offer.EffectiveCost})")));
            var buyBtn = new Button(() =>
            {
                if (!string.IsNullOrEmpty(_pendingOfferId)) return;
                _pendingOfferId = offerIdStr;
                _pendingRequestId = $"hub_{++_hubRequestSeq}_{Guid.NewGuid().ToString("N").Substring(0, 6)}";
                _pendingStartedAtRealtime = Time.realtimeSinceStartup;
                PizzeriaCommandHandler.Instance?.PurchaseRunShopOfferCorrelatedServerRpc(
                    new Unity.Collections.FixedString64Bytes(offerIdStr),
                    new Unity.Collections.FixedString64Bytes(_pendingRequestId));
                RefreshCurrentTab();
            })
            {
                text = btnText
            };
            buyBtn.SetEnabled(effectiveCanBuy);
            buyBtn.style.height = 34;
            buyBtn.style.minWidth = 150;
            buyBtn.style.backgroundColor = maxed
                ? new Color(0.24f, 0.42f, 0.28f)
                : (canBuy ? new Color(0.20f, 0.52f, 0.72f) : new Color(0.45f, 0.22f, 0.22f));
            buyBtn.style.color = Color.white;
            row.Add(buyBtn);

            _contentScroll.Add(row);
        }

        private void PopulateEmployeesTab(PizzeriaNetworkState net)
        {
            int budget = net.RestaurantBudget.Value;
            _contentScroll.Add(CreateSectionHeader("Contratação de Equipe"));

            int cashierCost = 0;
            bool hasCashierOffer = false;
            string cashierName = "Balconista";
            string cashierOfferId = string.Empty;

            int pizzaioloCost = 0;
            bool hasPizzaioloOffer = false;
            string pizzaioloName = "Pizzaiolo";
            string pizzaioloOfferId = string.Empty;

            if (net.RunShopOffers != null)
            {
                for (int i = 0; i < net.RunShopOffers.Count; i++)
                {
                    var o = net.RunShopOffers[i];
                    if ((ShopBenefitKind)o.BenefitKind != ShopBenefitKind.HireEmployee) continue;
                    string id = o.OfferId.ToString();
                    if (o.LegacyByteId == 5 || id.IndexOf("cashier", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hasCashierOffer = true;
                        cashierCost = o.EffectiveCost;
                        cashierName = o.DisplayName.ToString();
                        cashierOfferId = id;
                    }
                    else if (o.LegacyByteId == 6 || id.IndexOf("pizzaiolo", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hasPizzaioloOffer = true;
                        pizzaioloCost = o.EffectiveCost;
                        pizzaioloName = o.DisplayName.ToString();
                        pizzaioloOfferId = id;
                    }
                }
            }

            if (!hasCashierOffer && !hasPizzaioloOffer)
            {
                var snap = PizzeriaRoot.Instance?.RunProgressionService?.BalancingSnapshot;
                var cashierProf = snap?.FindEmployeeProfile(EmployeeRole.Cashier);
                var pizzaioloProf = snap?.FindEmployeeProfile(EmployeeRole.Pizzaiolo);
                if (cashierProf != null)
                {
                    hasCashierOffer = true;
                    cashierCost = cashierProf.HireCost;
                    cashierName = cashierProf.DisplayName;
                }
                if (pizzaioloProf != null)
                {
                    hasPizzaioloOffer = true;
                    pizzaioloCost = pizzaioloProf.HireCost;
                    pizzaioloName = pizzaioloProf.DisplayName;
                }
            }

            var hireRow = new VisualElement();
            hireRow.style.flexDirection = FlexDirection.Row;
            hireRow.style.flexWrap = Wrap.Wrap;
            hireRow.style.marginBottom = 10;

            bool hasPendingPurchase = !string.IsNullOrEmpty(_pendingOfferId);
            bool hasPendingEmpAction = Time.realtimeSinceStartup < _pendingEmployeeActionUntil;
            bool cashierPending = string.Equals(_pendingOfferId, cashierOfferId, StringComparison.Ordinal);
            bool pizzaioloPending = string.Equals(_pendingOfferId, pizzaioloOfferId, StringComparison.Ordinal);
            bool canHireCashier = hasCashierOffer && budget >= cashierCost && !hasPendingPurchase && !hasPendingEmpAction;
            bool canHirePizzaiolo = hasPizzaioloOffer && budget >= pizzaioloCost && !hasPendingPurchase && !hasPendingEmpAction;

            var hireCashierBtn = new Button(() =>
            {
                if (!string.IsNullOrEmpty(_pendingOfferId) || Time.realtimeSinceStartup < _pendingEmployeeActionUntil) return;
                if (!string.IsNullOrEmpty(cashierOfferId))
                {
                    _pendingOfferId = cashierOfferId;
                    _pendingRequestId = $"hub_{++_hubRequestSeq}_{Guid.NewGuid().ToString("N").Substring(0, 6)}";
                    _pendingStartedAtRealtime = Time.realtimeSinceStartup;
                    PizzeriaCommandHandler.Instance?.PurchaseRunShopOfferCorrelatedServerRpc(
                        new Unity.Collections.FixedString64Bytes(cashierOfferId),
                        new Unity.Collections.FixedString64Bytes(_pendingRequestId));
                }
                else
                {
                    _pendingEmployeeActionUntil = Time.realtimeSinceStartup + 2.5f;
                    PizzeriaCommandHandler.Instance?.HireEmployeeFromHubServerRpc((byte)EmployeeRole.Cashier);
                }
                RefreshCurrentTab();
            })
            {
                text = cashierPending
                    ? "..."
                    : (hasCashierOffer
                        ? (budget >= cashierCost ? $"{cashierName} (R$ {cashierCost})" : $"{cashierName} R$ {cashierCost} (Saldo Insuf.)")
                        : "Balconista indisponivel")
            };
            hireCashierBtn.SetEnabled(canHireCashier);
            hireCashierBtn.style.flexGrow = 1;
            hireCashierBtn.style.height = 38;
            hireCashierBtn.style.marginRight = 8;
            hireCashierBtn.style.backgroundColor = canHireCashier ? new Color(0.20f, 0.52f, 0.34f) : new Color(0.42f, 0.22f, 0.22f);
            hireCashierBtn.style.color = Color.white;
            hireRow.Add(hireCashierBtn);

            var hirePizzaioloBtn = new Button(() =>
            {
                if (!string.IsNullOrEmpty(_pendingOfferId) || Time.realtimeSinceStartup < _pendingEmployeeActionUntil) return;
                if (!string.IsNullOrEmpty(pizzaioloOfferId))
                {
                    _pendingOfferId = pizzaioloOfferId;
                    _pendingRequestId = $"hub_{++_hubRequestSeq}_{Guid.NewGuid().ToString("N").Substring(0, 6)}";
                    _pendingStartedAtRealtime = Time.realtimeSinceStartup;
                    PizzeriaCommandHandler.Instance?.PurchaseRunShopOfferCorrelatedServerRpc(
                        new Unity.Collections.FixedString64Bytes(pizzaioloOfferId),
                        new Unity.Collections.FixedString64Bytes(_pendingRequestId));
                }
                else
                {
                    _pendingEmployeeActionUntil = Time.realtimeSinceStartup + 2.5f;
                    PizzeriaCommandHandler.Instance?.HireEmployeeFromHubServerRpc((byte)EmployeeRole.Pizzaiolo);
                }
                RefreshCurrentTab();
            })
            {
                text = pizzaioloPending
                    ? "..."
                    : (hasPizzaioloOffer
                        ? (budget >= pizzaioloCost ? $"{pizzaioloName} (R$ {pizzaioloCost})" : $"{pizzaioloName} R$ {pizzaioloCost} (Saldo Insuf.)")
                        : "Pizzaiolo indisponivel")
            };
            hirePizzaioloBtn.SetEnabled(canHirePizzaiolo);
            hirePizzaioloBtn.style.flexGrow = 1;
            hirePizzaioloBtn.style.height = 38;
            hirePizzaioloBtn.style.backgroundColor = canHirePizzaiolo ? new Color(0.20f, 0.52f, 0.34f) : new Color(0.42f, 0.22f, 0.22f);
            hirePizzaioloBtn.style.color = Color.white;
            hireRow.Add(hirePizzaioloBtn);
            _contentScroll.Add(hireRow);

            _contentScroll.Add(CreateSectionHeader("Equipe Contratada nesta RUN"));
            if (net.Employees == null || net.Employees.Count == 0)
            {
                _contentScroll.Add(CreateEmptyLabel("Nenhum funcionário contratado nesta RUN."));
                return;
            }

            for (int i = 0; i < net.Employees.Count; i++)
            {
                var emp = net.Employees[i];
                string roleName = emp.Role == 0 ? "Balconista" : "Pizzaiolo";
                string lifeState = emp.IsAlive ? $"VIVO ({emp.CurrentHp}/{emp.MaxHp} HP)" : "CAÍDO (retorna na próxima noite)";
                string taskDesc = emp.TargetOrderId > 0
                    ? $"Pedido #{emp.TargetOrderId} ({emp.UnitsCompleted}/{Mathf.Max(1, emp.UnitsRequired)})"
                    : "Aguardando demanda";
                string blockDesc = emp.TaskState == (byte)EmployeeTaskState.Blocked
                    ? FormatBlockReason(emp.BlockReason)
                    : ((EmployeeTaskState)emp.TaskState).ToString();

                var empRole = (EmployeeRole)emp.Role;
                var empService = PizzeriaRoot.Instance?.EmployeeTaskService;
                var profile = empService?.GetRoleProfile(empRole);
                int maxLevel = emp.MaxLevel > 0 ? emp.MaxLevel : (profile != null ? profile.MaxLevel : 5);
                int nextCost = emp.MaxLevel > 0
                    ? emp.NextUpgradeCost
                    : (profile != null
                        ? profile.ComputeCostForLevel(emp.Level + 1)
                        : ((emp.Role == 0 ? cashierCost : pizzaioloCost) * (emp.Level + 1)));
                bool atMaxLevel = emp.Level >= maxLevel;
                bool canUpgrade = !atMaxLevel && budget >= nextCost && !hasPendingEmpAction;

                var row = CreateCardRow();
                row.style.flexDirection = FlexDirection.Row;
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.alignItems = Align.Center;

                var infoLbl = new Label(
                    $"{roleName} #{emp.EmployeeId} (Nv.{emp.Level}) — {lifeState}\n" +
                    $"Tarefa: {taskDesc} | Estado: {blockDesc} | Erro: {emp.SkillErrorPercent}%");
                infoLbl.style.fontSize = 13;
                infoLbl.style.color = emp.IsAlive ? Color.white : new Color(0.95f, 0.52f, 0.52f);
                infoLbl.style.whiteSpace = WhiteSpace.Normal;
                row.Add(infoLbl);

                int capturedEmpId = emp.EmployeeId;
                var upgBtn = new Button(() =>
                {
                    if (Time.realtimeSinceStartup < _pendingEmployeeActionUntil) return;
                    _pendingEmployeeActionUntil = Time.realtimeSinceStartup + 1.5f;
                    PizzeriaCommandHandler.Instance?.UpgradeEmployeeFromHubServerRpc(capturedEmpId);
                    RefreshCurrentTab();
                })
                {
                    text = atMaxLevel
                        ? "NÍVEL MÁX."
                        : (canUpgrade ? $"Promover (R$ {nextCost})" : $"R$ {nextCost} (Insuf.)")
                };
                upgBtn.SetEnabled(canUpgrade);
                upgBtn.style.height = 32;
                upgBtn.style.minWidth = 140;
                upgBtn.style.backgroundColor = canUpgrade ? new Color(0.24f, 0.44f, 0.68f) : new Color(0.40f, 0.24f, 0.24f);
                upgBtn.style.color = Color.white;
                row.Add(upgBtn);

                _contentScroll.Add(row);
            }
        }

        private static string FormatBlockReason(byte reason)
        {
            return (EmployeeBlockReason)reason switch
            {
                EmployeeBlockReason.NoDough => "Bloqueado: Sem Massa",
                EmployeeBlockReason.MissingIngredient => "Bloqueado: Falta Ingrediente",
                EmployeeBlockReason.OvenBusy => "Bloqueado: Forno Cheio",
                EmployeeBlockReason.ToolInUse => "Bloqueado: Ferramenta em Uso",
                EmployeeBlockReason.PathBlocked => "Bloqueado: Caminho Obstruído",
                _ => "Bloqueado"
            };
        }

        private static Label CreateSectionHeader(string text)
        {
            var lbl = new Label(text);
            lbl.style.fontSize = 15;
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            lbl.style.color = new Color(0.96f, 0.80f, 0.36f);
            lbl.style.marginTop = 8;
            lbl.style.marginBottom = 6;
            return lbl;
        }

        private static VisualElement CreateCardRow()
        {
            var row = new VisualElement();
            row.style.backgroundColor = new Color(0.15f, 0.17f, 0.22f, 0.96f);
            row.style.borderTopLeftRadius = 6;
            row.style.borderTopRightRadius = 6;
            row.style.borderBottomLeftRadius = 6;
            row.style.borderBottomRightRadius = 6;
            row.style.paddingLeft = 12;
            row.style.paddingRight = 12;
            row.style.paddingTop = 8;
            row.style.paddingBottom = 8;
            row.style.marginBottom = 8;
            return row;
        }

        private static Label CreateEmptyLabel(string text)
        {
            var lbl = new Label(text);
            lbl.style.fontSize = 14;
            lbl.style.color = new Color(0.75f, 0.78f, 0.84f);
            lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
            lbl.style.marginTop = 10;
            lbl.style.marginBottom = 10;
            lbl.style.whiteSpace = WhiteSpace.Normal;
            return lbl;
        }
    }
}
