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

        private void ResolveUIDocument()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>() ?? FindFirstObjectByType<UIDocument>();
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
            if (_root != null) return;
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>() ?? FindFirstObjectByType<UIDocument>();
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            _root = uiDocument.rootVisualElement.Q<VisualElement>("ScoreboardRoot");
            if (_root == null)
            {
                var template = Resources.Load<VisualTreeAsset>("ScoreboardOverlay")
#if UNITY_EDITOR
                    ?? UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Piaria/UXML/ScoreboardOverlay.uxml")
#endif
                    ;
                if (template != null)
                {
                    template.CloneTree(uiDocument.rootVisualElement);
                    _root = uiDocument.rootVisualElement.Q<VisualElement>("ScoreboardRoot");
                }
            }
            if (_root == null) return;

            _scoreboardCard = _root.Q<VisualElement>("ScoreboardCard");
            _scoreboardList = _root.Q<ScrollView>("ScoreboardList");

            var catalog = PiariaUIAssetCatalog.Instance;
            if (catalog != null && _scoreboardCard != null && catalog.paperPanel3 != null)
            {
                _scoreboardCard.style.backgroundImage = new StyleBackground(catalog.paperPanel3);
            }

            _root.style.display = DisplayStyle.None;
        }

        private void Update()
        {
            EnsureInitialized();
            if (_root == null) return;

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
            if (_root == null || _isOpen) return;
            _isOpen = true;
            RefreshData();

            var curr = _root;
            while (curr != null && curr != uiDocument?.rootVisualElement)
            {
                curr.style.display = DisplayStyle.Flex;
                curr = curr.parent;
            }
            UIToolkitTweenService.PopIn(_scoreboardCard != null ? _scoreboardCard : _root, 0.16f, 0.96f);
        }

        public void Hide()
        {
            if (_root == null || !_isOpen) return;
            _isOpen = false;

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
