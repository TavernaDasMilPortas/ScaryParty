using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.UI.Piaria.Core;

namespace ScaryParty.UI.Piaria.Presenters
{
    /// <summary>
    /// Shell único de UI Toolkit para a Preparação entre Noites (2.1):
    /// Resumo -> Imprevistos -> Oportunidades -> Loja -> Preparar -> Sorteio/Contagem.
    /// </summary>
    [DisallowMultipleComponent]
    public class NightIntermissionPresenter : MonoBehaviour
    {
        public enum TabPage
        {
            Summary = 0,
            Hindrance = 1,
            Opportunity = 2,
            Shop = 3,
            Prepare = 4,
            Reveal = 5
        }

        public static NightIntermissionPresenter Instance { get; private set; }

        [SerializeField] private UIDocument _uiDocument;
        private VisualElement _root;
        private VisualElement _shell;
        private VisualElement _pageHost;
        private Label _titleLabel;
        private Label _subtitleLabel;
        private Label _budgetLabel;
        private Label _statusLabel;
        private Button _nextBtn;
        private Button _tabSummary;
        private Button _tabHindrance;
        private Button _tabOpportunity;
        private Button _tabShop;
        private Button _tabPrepare;

        private TabPage _activeTab = TabPage.Summary;
        private bool _isOpen;
        private bool _lockPushed;
        private bool _buildFailed;
        private bool _listDirty;
        private uint _lastObservedRevision;
        private uint _lastNightRevision;
        private NightIntermissionTheme _theme;
        private NightSummaryPagePresenter _summaryPresenter;
        private ModifierVotePagePresenter _votePresenter;
        private RunShopPagePresenter _shopPresenter;
        private LotteryRevealPresenter _revealPresenter;
        private PizzeriaNetworkState _subscribedNetState;

        public bool IsOpen => _isOpen;
        public TabPage ActiveTab => _activeTab;
        public bool CanRenderIntermission => _root != null || (!_buildFailed && EnsureBuilt());

        public static NightIntermissionPresenter EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<NightIntermissionPresenter>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            var go = new GameObject("NightIntermissionPresenter (UI Toolkit)");
            return go.AddComponent<NightIntermissionPresenter>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDisable()
        {
            UnsubscribeCommandEvents();
            UnsubscribeNetworkLists();
            ReleaseInputLock();
            if (_root != null) UIToolkitTweenService.KillTweensRecursive(_root);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnsubscribeCommandEvents();
            UnsubscribeNetworkLists();
            ReleaseInputLock();
            if (_root != null) UIToolkitTweenService.KillTweensRecursive(_root);
        }

        private PizzeriaCommandHandler _subscribedHandler;

        private void EnsureNetworkListsSubscribed(PizzeriaNetworkState net)
        {
            if (_subscribedNetState == net) return;
            UnsubscribeNetworkLists();
            if (net == null) return;

            if (net.RunVoteOptions != null) net.RunVoteOptions.OnListChanged += OnVoteOptionsListChanged;
            if (net.RunShopOffers != null) net.RunShopOffers.OnListChanged += OnShopOffersListChanged;
            if (net.RunPlayerStates != null) net.RunPlayerStates.OnListChanged += OnPlayerStatesListChanged;
            _subscribedNetState = net;
        }

        private void UnsubscribeNetworkLists()
        {
            if (_subscribedNetState == null) return;
            if (_subscribedNetState.RunVoteOptions != null) _subscribedNetState.RunVoteOptions.OnListChanged -= OnVoteOptionsListChanged;
            if (_subscribedNetState.RunShopOffers != null) _subscribedNetState.RunShopOffers.OnListChanged -= OnShopOffersListChanged;
            if (_subscribedNetState.RunPlayerStates != null) _subscribedNetState.RunPlayerStates.OnListChanged -= OnPlayerStatesListChanged;
            _subscribedNetState = null;
        }

        private void OnVoteOptionsListChanged(NetworkListEvent<NetRunVoteOptionDto> _) => _listDirty = true;
        private void OnShopOffersListChanged(NetworkListEvent<NetRunShopOfferDto> _) => _listDirty = true;
        private void OnPlayerStatesListChanged(NetworkListEvent<NetPlayerIntermissionDto> _) => _listDirty = true;

        private void EnsureCommandEventsSubscribed()
        {
            var handler = PizzeriaCommandHandler.Instance;
            if (_subscribedHandler == handler) return;
            UnsubscribeCommandEvents();
            if (handler != null)
            {
                handler.OnShopPurchaseResultReceived += OnShopPurchaseResultReceived;
                _subscribedHandler = handler;
            }
        }

        private void UnsubscribeCommandEvents()
        {
            if (_subscribedHandler != null)
            {
                _subscribedHandler.OnShopPurchaseResultReceived -= OnShopPurchaseResultReceived;
                _subscribedHandler = null;
            }
        }

        private void OnShopPurchaseResultReceived(bool success, string offerId, string requestId, string message)
        {
            if (!_isOpen || _shopPresenter == null) return;
            if (!success && _statusLabel != null && !string.IsNullOrEmpty(message))
            {
                _statusLabel.text = message;
            }
            _shopPresenter.HandleShopPurchaseResult(success, offerId, requestId, message, PizzeriaNetworkState.Instance);
        }

        public void ShowIntermission()
        {
            if (ScaryParty.Pizzeria.Presentation.GameplayIntroCutsceneFlow.IsBlockingIntermission) return;
            if (!EnsureBuilt()) return;
            EnsureCommandEventsSubscribed();
            var net = PizzeriaNetworkState.Instance;
            EnsureNetworkListsSubscribed(net);

            if (!_isOpen)
            {
                _isOpen = true;
                _listDirty = false;
                _lastObservedRevision = net != null ? net.RunState.Value.Revision : 0;
                _lastNightRevision = net != null ? net.NightState.Value.Revision : 0;
                _root.style.display = DisplayStyle.Flex;
                if (!_lockPushed)
                {
                    ScaryParty.Pizzeria.Presentation.GameplayInputGate.PushModal();
                    _lockPushed = true;
                }
                UIToolkitTweenService.PopIn(_shell, 0.28f);
                NightIntermissionFeelBridge.Play(NightUIFeedbackId.IntermissionOpen, FeedbackPriority.Normal);
                SelectTab(TabPage.Summary, animate: true);
            }

            RefreshFromNetwork();
        }

        public void HideIntermission()
        {
            if (!_isOpen) return;
            _isOpen = false;
            _listDirty = false;
            _lastObservedRevision = 0;
            _lastNightRevision = 0;
            UnsubscribeCommandEvents();
            UnsubscribeNetworkLists();
            _shopPresenter?.ClearPendingPurchase();
            if (_root != null)
            {
                UIToolkitTweenService.KillTweensRecursive(_root);
                _root.style.display = DisplayStyle.None;
            }
            ReleaseInputLock();
        }

        public void SelectTab(TabPage tab, bool animate = true)
        {
            var net = PizzeriaNetworkState.Instance;
            bool isLocked = net != null && net.RunState.Value.IntermissionStage >= (byte)IntermissionStage.Locked;
            if (isLocked && tab != TabPage.Reveal)
            {
                return;
            }
            if (_activeTab != tab && _isOpen)
            {
                NightIntermissionFeelBridge.Play(NightUIFeedbackId.TabSwitch, FeedbackPriority.Low);
            }
            _activeTab = tab;
            UpdateTabVisuals();
            RebuildActivePage(animate);
        }

        private void Update()
        {
            var net = PizzeriaNetworkState.Instance;
            if (net == null) return;

            var night = net.NightState.Value;
            var run = net.RunState.Value;
            bool blockedByIntro = ScaryParty.Pizzeria.Presentation.GameplayIntroCutsceneFlow.IsBlockingIntermission;
            bool shouldShow = !blockedByIntro &&
                              night.Phase == (byte)NightPhase.PreparingNextNight &&
                              !run.IsRunCompleted;

            if (shouldShow && !_isOpen)
            {
                if (!_buildFailed) ShowIntermission();
            }
            else if (!shouldShow && _isOpen)
            {
                HideIntermission();
            }
            else if (_isOpen)
            {
                EnsureNetworkListsSubscribed(net);
                ScaryParty.Pizzeria.Presentation.GameplayInputGate.MaintainMenuCursorUnlocked();
                if (_activeTab == TabPage.Reveal && _revealPresenter != null)
                {
                    double serverNow = NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : Time.unscaledTimeAsDouble;
                    _revealPresenter.Tick(net, serverNow);
                }
                if (_listDirty || run.Revision != _lastObservedRevision || night.Revision != _lastNightRevision)
                {
                    _listDirty = false;
                    _lastObservedRevision = run.Revision;
                    _lastNightRevision = night.Revision;
                    RefreshFromNetwork();
                }
            }
        }

        private void RefreshFromNetwork()
        {
            var net = PizzeriaNetworkState.Instance;
            if (net == null || _root == null) return;

            var night = net.NightState.Value;
            var run = net.RunState.Value;
            int budget = net.RestaurantBudget.Value;

            if (_titleLabel != null)
                _titleLabel.text = night.NightNumber <= 0 ? "PREPARAR PRIMEIRA NOITE" : $"NOITE {night.NightNumber} CONCLUÍDA";
            if (_subtitleLabel != null)
                _subtitleLabel.text = $"Preparando Noite {run.TargetNightNumber} de {run.MaxRunNights}";
            if (_budgetLabel != null)
                _budgetLabel.text = $"Caixa: R$ {budget}";

            bool isLocked = run.IntermissionStage >= (byte)IntermissionStage.Locked;
            _tabSummary?.SetEnabled(!isLocked);
            _tabHindrance?.SetEnabled(!isLocked);
            _tabOpportunity?.SetEnabled(!isLocked);
            _tabShop?.SetEnabled(!isLocked);
            _tabPrepare?.SetEnabled(!isLocked);

            if (run.IntermissionStage >= (byte)IntermissionStage.Revealing && _activeTab != TabPage.Reveal)
            {
                SelectTab(TabPage.Reveal, animate: true);
                return;
            }

            if (_activeTab == TabPage.Summary && _summaryPresenter != null)
            {
                _summaryPresenter.Bind(night, run, budget, animate: false);
            }
            else if ((_activeTab == TabPage.Hindrance || _activeTab == TabPage.Opportunity) && _votePresenter != null)
            {
                var kind = _activeTab == TabPage.Hindrance ? RunModifierKind.Hindrance : RunModifierKind.Opportunity;
                _votePresenter.Bind(kind, net, animateDrop: false);
            }
            else if ((_activeTab == TabPage.Shop || _activeTab == TabPage.Prepare) && _shopPresenter != null)
            {
                _shopPresenter.Bind(_activeTab == TabPage.Prepare, net);
            }
        }

        private void UpdateTabVisuals()
        {
            SetTabActive(_tabSummary, _activeTab == TabPage.Summary);
            SetTabActive(_tabHindrance, _activeTab == TabPage.Hindrance);
            SetTabActive(_tabOpportunity, _activeTab == TabPage.Opportunity);
            SetTabActive(_tabShop, _activeTab == TabPage.Shop);
            SetTabActive(_tabPrepare, _activeTab == TabPage.Prepare);

            if (_nextBtn != null)
            {
                _nextBtn.style.display = _activeTab == TabPage.Prepare || _activeTab == TabPage.Reveal
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }
        }

        private void RebuildActivePage(bool animate)
        {
            if (_pageHost == null) return;
            UIToolkitTweenService.KillTweensRecursive(_pageHost);
            _revealPresenter?.Reset();
            _pageHost.Clear();

            var net = PizzeriaNetworkState.Instance;
            var catalog = PiariaUIAssetCatalog.Instance;

            if (_activeTab == TabPage.Summary)
            {
                VisualTreeAsset tpl = catalog != null ? catalog.templateNightSummaryPage : null;
#if UNITY_EDITOR
                if (tpl == null)
                    tpl = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Piaaria/UI/Piaria/UXML/NightSummaryPage.uxml");
#endif
                if (tpl != null)
                {
                    var page = tpl.Instantiate();
                    page.style.flexGrow = 1;
                    _pageHost.Add(page);
                    _summaryPresenter = new NightSummaryPagePresenter(page);
                    if (net != null)
                        _summaryPresenter.Bind(net.NightState.Value, net.RunState.Value, net.RestaurantBudget.Value, animate);
                }
            }
            else if (_activeTab == TabPage.Hindrance || _activeTab == TabPage.Opportunity)
            {
                VisualTreeAsset tpl = catalog != null ? catalog.templateModifierVotePage : null;
#if UNITY_EDITOR
                if (tpl == null)
                    tpl = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Piaaria/UI/Piaria/UXML/ModifierVotePage.uxml");
#endif
                if (tpl != null)
                {
                    var page = tpl.Instantiate();
                    page.style.flexGrow = 1;
                    _pageHost.Add(page);
                    _votePresenter = new ModifierVotePagePresenter(page);
                    var kind = _activeTab == TabPage.Hindrance ? RunModifierKind.Hindrance : RunModifierKind.Opportunity;
                    if (net != null)
                        _votePresenter.Bind(kind, net, animate);
                }
            }
            else if (_activeTab == TabPage.Shop || _activeTab == TabPage.Prepare)
            {
                VisualTreeAsset tpl = catalog != null ? catalog.templateShopPreparationPage : null;
#if UNITY_EDITOR
                if (tpl == null)
                    tpl = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Piaaria/UI/Piaria/UXML/ShopPreparationPage.uxml");
#endif
                if (tpl != null)
                {
                    var page = tpl.Instantiate();
                    page.style.flexGrow = 1;
                    _pageHost.Add(page);
                    _shopPresenter = new RunShopPagePresenter(page, tab => SelectTab(tab));
                    if (net != null)
                        _shopPresenter.Bind(_activeTab == TabPage.Prepare, net);
                }
            }
            else if (_activeTab == TabPage.Reveal)
            {
                VisualTreeAsset tpl = catalog != null ? catalog.templateLotteryRevealPage : null;
#if UNITY_EDITOR
                if (tpl == null)
                    tpl = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Piaaria/UI/Piaria/UXML/LotteryRevealPage.uxml");
#endif
                if (tpl != null)
                {
                    var page = tpl.Instantiate();
                    page.style.flexGrow = 1;
                    _pageHost.Add(page);
                    _revealPresenter = new LotteryRevealPresenter(page);
                }
            }
            else
            {
                var placeholder = new Label($"Página {_activeTab} em preparação...");
                placeholder.AddToClassList("intermission-subtitle");
                _pageHost.Add(placeholder);
            }
        }

        private void OnNextClicked()
        {
            var next = _activeTab switch
            {
                TabPage.Summary => TabPage.Hindrance,
                TabPage.Hindrance => TabPage.Opportunity,
                TabPage.Opportunity => TabPage.Shop,
                TabPage.Shop => TabPage.Prepare,
                _ => TabPage.Prepare
            };
            SelectTab(next, animate: true);
        }

        private bool EnsureBuilt()
        {
            if (_root != null) return true;

            _theme = NightIntermissionTheme.ResolveActive();
            var catalog = PiariaUIAssetCatalog.Instance;

            if (_uiDocument == null)
            {
                _uiDocument = GetComponent<UIDocument>();
                if (_uiDocument == null)
                    _uiDocument = gameObject.AddComponent<UIDocument>();
            }

            if (_uiDocument.panelSettings == null && catalog != null && catalog.panelSettings != null)
                _uiDocument.panelSettings = catalog.panelSettings;
            if (_uiDocument.panelSettings == null)
            {
                var hud = FindFirstObjectByType<HudPresenter>();
                if (hud != null && hud.uiDocument != null && hud.uiDocument.panelSettings != null)
                    _uiDocument.panelSettings = hud.uiDocument.panelSettings;
            }
            if (_uiDocument.panelSettings == null)
            {
                var ps = Resources.FindObjectsOfTypeAll<PanelSettings>();
                if (ps != null && ps.Length > 0)
                    _uiDocument.panelSettings = ps[0];
            }

            _uiDocument.sortingOrder = 120;

            VisualTreeAsset screenTpl = catalog != null ? catalog.templateNightIntermissionScreen : null;
#if UNITY_EDITOR
            if (screenTpl == null)
                screenTpl = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Piaaria/UI/Piaria/UXML/NightIntermissionScreen.uxml");
#endif
            if (screenTpl == null || _uiDocument.panelSettings == null)
            {
                _buildFailed = true;
                Debug.LogError($"[NightIntermissionPresenter] Falha ao inicializar shell de preparação (screenTpl={(screenTpl != null)}, panelSettings={(_uiDocument.panelSettings != null)}). Fallback RunPreparationPresenter será utilizado.");
                return false;
            }

            _uiDocument.visualTreeAsset = screenTpl;
            _root = _uiDocument.rootVisualElement;
            if (_root == null)
            {
                _buildFailed = true;
                Debug.LogError("[NightIntermissionPresenter] rootVisualElement nulo após atribuir VisualTreeAsset; habilitando fallback RunPreparationPresenter.");
                return false;
            }
            _buildFailed = false;

            if (catalog != null)
            {
                if (catalog.styleNightIntermission != null && !_root.styleSheets.Contains(catalog.styleNightIntermission))
                    _root.styleSheets.Add(catalog.styleNightIntermission);
                if (catalog.styleNightModifierCards != null && !_root.styleSheets.Contains(catalog.styleNightModifierCards))
                    _root.styleSheets.Add(catalog.styleNightModifierCards);
            }
#if UNITY_EDITOR
            if (_root.styleSheets.count == 0)
            {
                var uss = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Piaaria/UI/Piaria/USS/NightIntermission.uss");
                if (uss != null && !_root.styleSheets.Contains(uss))
                    _root.styleSheets.Add(uss);
                var cardsUss = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Piaaria/UI/Piaria/USS/NightModifierCards.uss");
                if (cardsUss != null && !_root.styleSheets.Contains(cardsUss))
                    _root.styleSheets.Add(cardsUss);
            }
#endif

            _shell = _root.Q<VisualElement>("intermission-shell");
            _pageHost = _root.Q<VisualElement>("intermission-page-host");
            _titleLabel = _root.Q<Label>("intermission-title");
            _subtitleLabel = _root.Q<Label>("intermission-subtitle");
            _budgetLabel = _root.Q<Label>("intermission-budget-label");
            _statusLabel = _root.Q<Label>("intermission-status-label");
            _nextBtn = _root.Q<Button>("intermission-next-btn");

            _tabSummary = _root.Q<Button>("tab-summary");
            _tabHindrance = _root.Q<Button>("tab-hindrance");
            _tabOpportunity = _root.Q<Button>("tab-opportunity");
            _tabShop = _root.Q<Button>("tab-shop");
            _tabPrepare = _root.Q<Button>("tab-prepare");

            if (_tabHindrance != null && _theme != null)
                _tabHindrance.text = $"2. {_theme.GetCategoryLabel(RunModifierKind.Hindrance)}";

            _tabSummary?.RegisterCallback<ClickEvent>(_ => SelectTab(TabPage.Summary));
            _tabHindrance?.RegisterCallback<ClickEvent>(_ => SelectTab(TabPage.Hindrance));
            _tabOpportunity?.RegisterCallback<ClickEvent>(_ => SelectTab(TabPage.Opportunity));
            _tabShop?.RegisterCallback<ClickEvent>(_ => SelectTab(TabPage.Shop));
            _tabPrepare?.RegisterCallback<ClickEvent>(_ => SelectTab(TabPage.Prepare));
            _nextBtn?.RegisterCallback<ClickEvent>(_ => OnNextClicked());

            _root.style.display = DisplayStyle.None;
            return true;
        }

        private static void SetTabActive(Button btn, bool active)
        {
            if (btn == null) return;
            if (active) btn.AddToClassList("intermission-tab-active");
            else btn.RemoveFromClassList("intermission-tab-active");
        }

        private void ReleaseInputLock()
        {
            if (_lockPushed)
            {
                ScaryParty.Pizzeria.Presentation.GameplayInputGate.PopModal();
                _lockPushed = false;
            }
        }
    }
}
