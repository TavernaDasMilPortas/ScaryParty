using System;
using UnityEngine;
using UnityEngine.UIElements;
using ScaryParty.UI.Piaria.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ScaryParty.UI.Piaria.Presenters
{
    public class ScoreboardPresenter : MonoBehaviour
    {
        public static ScoreboardPresenter Instance { get; private set; }

        [Header("Configuração de UI Document")]
        public UIDocument uiDocument;

        private VisualElement _root;
        private VisualElement _scoreboardCard;
        private ScrollView _scoreboardList;
        private VisualElement _opsRoot;
        private OperationsStatusPresenter _opsPresenter;

        private bool _isOpen = false;
        private float _lastRefreshTime = -999f;

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

            ResolveUIDocument();
        }

        public void ResolveUIDocument()
        {
            PiariaPresenterUtils.ResolveUIDocument(this, ref uiDocument, "Assets/Piaaria/UI/Piaria/UXML/MainGameHUD.uxml");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveUIDocument();
        }

        private void Reset()
        {
            ResolveUIDocument();
        }
#endif

        private void Start()
        {
            var activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (activeSceneName.IndexOf("Lobby", StringComparison.OrdinalIgnoreCase) >= 0 ||
                activeSceneName.IndexOf("Ready", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                enabled = false;
                return;
            }

            EnsureInitialized();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void EnsureInitialized()
        {
            if (_opsRoot != null || _root != null) return;
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>() ?? FindFirstObjectByType<UIDocument>();
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            var catalog = PiariaUIAssetCatalog.Instance;

            // Tenta conectar o novo painel OperationsStatusOverlay (4 blocos TAB)
            _opsRoot = uiDocument.rootVisualElement.Q<VisualElement>("ops-overlay-root");
            if (_opsRoot == null)
            {
                VisualTreeAsset opsTpl = PiariaPresenterUtils.ResolveTemplate(
                    c => c.templateOperationsStatusOverlay,
                    "Assets/Piaaria/UI/Piaria/UXML/OperationsStatusOverlay.uxml");
                if (opsTpl != null)
                {
                    var opsTree = opsTpl.Instantiate();
                    opsTree.style.position = Position.Absolute;
                    opsTree.style.left = 0;
                    opsTree.style.right = 0;
                    opsTree.style.top = 0;
                    opsTree.style.bottom = 0;
                    opsTree.pickingMode = PickingMode.Ignore;
                    uiDocument.rootVisualElement.Add(opsTree);
                    _opsRoot = opsTree.Q<VisualElement>("ops-overlay-root") ?? opsTree;
                }
            }

            if (_opsRoot != null)
            {
                if (catalog != null)
                {
                    if (catalog.styleNightIntermission != null && !_opsRoot.styleSheets.Contains(catalog.styleNightIntermission))
                        _opsRoot.styleSheets.Add(catalog.styleNightIntermission);
                    if (catalog.styleOperationsStatus != null && !_opsRoot.styleSheets.Contains(catalog.styleOperationsStatus))
                        _opsRoot.styleSheets.Add(catalog.styleOperationsStatus);
                }
                _opsPresenter = new OperationsStatusPresenter(_opsRoot);
                _opsRoot.style.display = DisplayStyle.None;
                return;
            }

            _root = uiDocument.rootVisualElement.Q<VisualElement>("ScoreboardRoot");
            if (_root == null)
            {
                var template = PiariaPresenterUtils.ResolveTemplate(
                    c => c.templateScoreboardOverlay,
                    "Assets/Piaaria/UI/Piaria/UXML/ScoreboardOverlay.uxml");
                if (template != null)
                {
                    template.CloneTree(uiDocument.rootVisualElement);
                    _root = uiDocument.rootVisualElement.Q<VisualElement>("ScoreboardRoot");
                }
            }
            if (_root == null) return;

            _scoreboardCard = _root.Q<VisualElement>("ScoreboardCard");
            _scoreboardList = _root.Q<ScrollView>("ScoreboardList");

            if (catalog != null && _scoreboardCard != null && catalog.paperPanel3 != null)
            {
                _scoreboardCard.style.backgroundImage = new StyleBackground(catalog.paperPanel3);
            }

            _root.style.display = DisplayStyle.None;
        }

        private void Update()
        {
            EnsureInitialized();
            if (_opsRoot == null && _root == null) return;

            // Se houver modal aberto na pilha ou campo de texto com foco, não exibe o placar
            if (UIModalStack.Instance != null && UIModalStack.Instance.HasActiveModal)
            {
                if (_isOpen) Hide();
                return;
            }

            bool tabHeld = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                tabHeld = Keyboard.current.tabKey.isPressed;
#else
            tabHeld = Input.GetKey(KeyCode.Tab);
#endif

            bool blockedByOverlay = (NightIntermissionPresenter.Instance != null && NightIntermissionPresenter.Instance.IsOpen) ||
                                   (Core.UIModalStack.Instance != null && Core.UIModalStack.Instance.HasActiveModal);
            if (blockedByOverlay) tabHeld = false;

            if (tabHeld && !_isOpen)
            {
                Show();
            }
            else if (!tabHeld && _isOpen)
            {
                Hide();
            }

            if (_isOpen && Time.time - _lastRefreshTime >= 0.35f)
            {
                _lastRefreshTime = Time.time;
                RefreshData();
            }
        }

        public void Show()
        {
            if (_isOpen) return;
            if (_opsRoot != null)
            {
                _isOpen = true;
                RefreshData();
                _opsRoot.style.display = DisplayStyle.Flex;
                UIToolkitTweenService.PopIn(_opsRoot.Q<VisualElement>(className: "ops-panel") ?? _opsRoot, 0.16f, 0.96f);
                NightIntermissionFeelBridge.Play(NightUIFeedbackId.TabOverlayOpen, FeedbackPriority.Normal);
                return;
            }
            if (_root == null) return;
            _isOpen = true;
            RefreshData();

            var curr = _root;
            while (curr != null && curr != uiDocument?.rootVisualElement)
            {
                curr.style.display = DisplayStyle.Flex;
                curr = curr.parent;
            }
            UIToolkitTweenService.PopIn(_scoreboardCard != null ? _scoreboardCard : _root, 0.16f, 0.96f);
            NightIntermissionFeelBridge.Play(NightUIFeedbackId.TabOverlayOpen, FeedbackPriority.Normal);
        }

        public void Hide()
        {
            if (!_isOpen) return;
            if (_opsRoot != null)
            {
                _isOpen = false;
                NightIntermissionFeelBridge.Play(NightUIFeedbackId.TabOverlayClose, FeedbackPriority.Low);
                var panel = _opsRoot.Q<VisualElement>(className: "ops-panel") ?? _opsRoot;
                UIToolkitTweenService.PopOut(panel, 0.12f, 0.96f, () =>
                {
                    if (_opsRoot != null) _opsRoot.style.display = DisplayStyle.None;
                });
                return;
            }
            if (_root == null) return;
            _isOpen = false;
            NightIntermissionFeelBridge.Play(NightUIFeedbackId.TabOverlayClose, FeedbackPriority.Low);

            UIToolkitTweenService.PopOut(_scoreboardCard != null ? _scoreboardCard : _root, 0.12f, 0.96f, () =>
            {
                _root.style.display = DisplayStyle.None;
                if (_root.parent != null && _root.parent != uiDocument?.rootVisualElement)
                {
                    _root.parent.style.display = DisplayStyle.None;
                }
            });
        }

        private void RefreshData()
        {
            if (_opsPresenter != null)
            {
                _opsPresenter.Bind(ScaryParty.Pizzeria.Network.PizzeriaNetworkState.Instance);
                return;
            }
            if (_scoreboardList == null) return;
            _scoreboardList.Clear();

            var allPlayers = FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
            foreach (var p in allPlayers)
            {
                if (p == null) continue;

                var row = new VisualElement();
                row.AddToClassList("scoreboard-item-row");

                string pName = p.PlayerName.Value.ToString();
                if (string.IsNullOrEmpty(pName)) pName = $"Jogador #{p.OwnerClientId}";

                var nameLbl = new Label(pName);
                nameLbl.AddToClassList("scoreboard-item-cell");
                nameLbl.AddToClassList("scoreboard-player-name");
                nameLbl.AddToClassList("col-name");
                nameLbl.style.color = p.PlayerColor.Value;

                int held = p.HeldPizzas.Value;
                string slotsText = (held > 0) ? $"● {held} / 2" : "□ 0 / 2";
                var slotsLbl = new Label(slotsText);
                slotsLbl.AddToClassList("scoreboard-item-cell");
                slotsLbl.AddToClassList("scoreboard-pizzas-cell");
                slotsLbl.AddToClassList("col-pizzas");

                var moneyLbl = new Label($"R${p.Money.Value}");
                moneyLbl.AddToClassList("scoreboard-item-cell");
                moneyLbl.AddToClassList("scoreboard-money-cell");
                moneyLbl.AddToClassList("col-money");

                row.Add(nameLbl);
                row.Add(slotsLbl);
                row.Add(moneyLbl);

                _scoreboardList.Add(row);
            }
        }
    }
}
