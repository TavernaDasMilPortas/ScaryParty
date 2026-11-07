using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ScaryParty.UI.Piaria.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ScaryParty.UI.Piaria.Presenters
{
    public class HudPresenter : MonoBehaviour
    {
        public static HudPresenter Instance { get; private set; }

        [Header("ConfiguraÃƒÆ’Ã‚Â§ÃƒÆ’Ã‚Â£o de UI Document")]
        public UIDocument uiDocument;

        // Elementos do HUD
        private VisualElement _root;
        private Label _moneyLabel;
        private Label _scoreLabel;
        private Label _activeOrdersLabel;
        private Label _interactionPrompt;

        // Barra de Progresso
        private VisualElement _progressBarBox;
        private VisualElement _progressBarFill;
        private Label _progressBarLabel;

        // Mochila
        private VisualElement _backpackBox;
        private VisualElement _backpackPizzaLayer;
        private VisualElement _backpackGradientLayer;
        private Label _backpackCountLabel;

        // MÃƒÆ’Ã‚Â£os
        private VisualElement _leftHandCard;
        private VisualElement _rightHandCard;
        private Label _leftHandContent;
        private Label _rightHandContent;
        private VisualElement _digit1Icon;
        private VisualElement _digit2Icon;

        // Minimapa
        private VisualElement _minimapContainer;
        private VisualElement _minimapPaperFrame;
        private VisualElement _minimapImage;

        private int _currentMoney = 0;
        private int _currentDeliveries = 0;
        private bool _isDraggingMap = false;
        private Vector2 _lastMousePos;
        private bool _isMinimapFullscreen = false;

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
            PiariaPresenterUtils.ResolveUIDocument(this, ref uiDocument, "Assets/UI/Piaria/UXML/MainGameHUD.uxml");
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
                // Este presenter ÃƒÆ’Ã‚Â© exclusivo da gameplay (GameScene). Desativa-se fora dela.
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

            _root = uiDocument.rootVisualElement.Q<VisualElement>("MainGameHUDRoot");
            if (_root == null)
            {
                var template = Resources.Load<VisualTreeAsset>("MainGameHUD")
#if UNITY_EDITOR
                    ?? UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Piaria/UXML/MainGameHUD.uxml")
#endif
                    ;
                if (template != null)
                {
                    template.CloneTree(uiDocument.rootVisualElement);
                    _root = uiDocument.rootVisualElement.Q<VisualElement>("MainGameHUDRoot");
                }
            }
            if (_root == null) return;

            _root.pickingMode = PickingMode.Ignore;
            PiariaPresenterUtils.ConfigureHierarchyPicking(_root);

            _moneyLabel = _root.Q<Label>("MoneyLabel");
            _scoreLabel = _root.Q<Label>("ScoreLabel");
            _activeOrdersLabel = _root.Q<Label>("ActiveOrdersLabel");
            _interactionPrompt = _root.Q<Label>("InteractionPrompt");

            _progressBarBox = _root.Q<VisualElement>("ProgressBarContainer");
            _progressBarFill = _root.Q<VisualElement>("ProgressBarFill");
            _progressBarLabel = _root.Q<Label>("ProgressBarLabel");

            _backpackBox = _root.Q<VisualElement>("BackpackContainer");
            _backpackPizzaLayer = _root.Q<VisualElement>("BackpackPizzaLayer");
            _backpackGradientLayer = _root.Q<VisualElement>("BackpackGradientLayer");
            _backpackCountLabel = _root.Q<Label>("BackpackCountLabel");

            _leftHandCard = _root.Q<VisualElement>("LeftHandSlot");
            _rightHandCard = _root.Q<VisualElement>("RightHandSlot");
            _leftHandContent = _root.Q<Label>("LeftHandContent");
            _rightHandContent = _root.Q<Label>("RightHandContent");
            _digit1Icon = _root.Q<VisualElement>("Digit1Icon");
            _digit2Icon = _root.Q<VisualElement>("Digit2Icon");

            _minimapContainer = _root.Q<VisualElement>("MinimapContainer");
            _minimapPaperFrame = _root.Q<VisualElement>("MinimapPaperFrame");
            _minimapImage = _root.Q<VisualElement>("MinimapImage");

            ApplyCatalogAssets();
            // The map presenter owns pointer and wheel input.

            SetMoney(0, immediate: true);
            SetDeliveries(0);
            HidePrompt();
            HideProgress();
            SetBackpackCount(0);
            SetHandState(false, true, "Vazio");
            SetHandState(true, false, "Vazio");
        }

        private void ApplyCatalogAssets()
        {
            var catalog = PiariaUIAssetCatalog.Instance;
            if (catalog == null) return;

            var iconMoney = _root.Q<VisualElement>("IconMoney");
            if (iconMoney != null && catalog.iconMoneyBill != null)
                iconMoney.style.backgroundImage = new StyleBackground(catalog.iconMoneyBill);

            var iconDeliveries = _root.Q<VisualElement>("IconDeliveries");
            if (iconDeliveries != null && catalog.iconDeliveries != null)
                iconDeliveries.style.backgroundImage = new StyleBackground(catalog.iconDeliveries);

            if (_digit1Icon != null && catalog.digit1 != null)
                _digit1Icon.style.backgroundImage = new StyleBackground(catalog.digit1);

            if (_digit2Icon != null && catalog.digit2 != null)
                _digit2Icon.style.backgroundImage = new StyleBackground(catalog.digit2);

            if (_leftHandCard != null && catalog.handFrameLeft != null)
                _leftHandCard.style.backgroundImage = new StyleBackground(catalog.handFrameLeft);

            if (_rightHandCard != null && catalog.handFrameRight != null)
                _rightHandCard.style.backgroundImage = new StyleBackground(catalog.handFrameRight);

            var progressFrame = _root.Q<VisualElement>("ProgressBarFrame");
            if (progressFrame != null && catalog.progressBarFrame != null)
                progressFrame.style.backgroundImage = new StyleBackground(catalog.progressBarFrame);

            if (_progressBarFill != null && catalog.progressBarFill != null)
                _progressBarFill.style.backgroundImage = new StyleBackground(catalog.progressBarFill);

            if (_minimapPaperFrame != null && catalog.minimapPaperBase != null)
                _minimapPaperFrame.style.backgroundImage = new StyleBackground(catalog.minimapPaperBase);
        }

        public void SetMoney(int total, bool immediate = false)
        {
            EnsureInitialized();
            if (_moneyLabel == null) return;

            int prev = _currentMoney;
            _currentMoney = total;

            if (immediate)
            {
                _moneyLabel.text = $"R${total}";
            }
            else
            {
                UIToolkitTweenService.TweenCounter(_moneyLabel, prev, total, 0.35f, "R$");
                UIFeedbackService.Instance?.PlaySuccess();
            }
        }

        public void SetDeliveries(int total)
        {
            EnsureInitialized();
            _currentDeliveries = total;
            if (_scoreLabel != null)
            {
                _scoreLabel.text = $"Entregas: {total}";
            }
        }

        public void SetHandState(bool isRightHand, bool isSelected, string itemName)
        {
            EnsureInitialized();
            var card = isRightHand ? _rightHandCard : _leftHandCard;
            var lbl = isRightHand ? _rightHandContent : _leftHandContent;

            if (lbl != null)
            {
                lbl.text = string.IsNullOrEmpty(itemName) ? "Vazio" : itemName;
            }

            if (card != null)
            {
                if (isSelected) card.AddToClassList("hand-card-active");
                else card.RemoveFromClassList("hand-card-active");
            }
        }

        public void ShowPrompt(string message)
        {
            EnsureInitialized();
            if (_interactionPrompt != null)
            {
                _interactionPrompt.text = message;
                _interactionPrompt.style.display = DisplayStyle.Flex;
                _interactionPrompt.style.opacity = 1f;
            }
        }

        public void HidePrompt()
        {
            if (_interactionPrompt != null)
            {
                _interactionPrompt.style.display = DisplayStyle.None;
            }
        }

        public void ShowProgress(float normalized, string actionLabel = "Trabalhando...")
        {
            EnsureInitialized();
            if (_progressBarBox != null)
            {
                _progressBarBox.style.display = DisplayStyle.Flex;
                if (_progressBarFill != null)
                    _progressBarFill.style.width = new Length(Mathf.Clamp01(normalized) * 100f, LengthUnit.Percent);
                if (_progressBarLabel != null)
                    _progressBarLabel.text = actionLabel;
            }
        }

        public void HideProgress()
        {
            if (_progressBarBox != null)
            {
                _progressBarBox.style.display = DisplayStyle.None;
            }
        }

        public void SetBackpackCount(int count)
        {
            EnsureInitialized();
            if (_backpackBox == null) return;

            if (count <= 0)
            {
                _backpackBox.style.display = DisplayStyle.None;
                return;
            }

            _backpackBox.style.display = DisplayStyle.Flex;
            if (_backpackCountLabel != null) _backpackCountLabel.text = count.ToString();

            var catalog = PiariaUIAssetCatalog.Instance;
            if (catalog != null)
            {
                var pSprite = catalog.GetBackpackPizzaSprite(count);
                if (_backpackPizzaLayer != null && pSprite != null)
                    _backpackPizzaLayer.style.backgroundImage = new StyleBackground(pSprite);

                var gSprite = catalog.GetBackpackGradientSprite(count);
                if (_backpackGradientLayer != null && gSprite != null)
                    _backpackGradientLayer.style.backgroundImage = new StyleBackground(gSprite);
            }
        }

        public void UpdateActiveOrdersDisplay(string text)
        {
            EnsureInitialized();
            if (_activeOrdersLabel != null)
            {
                _activeOrdersLabel.text = text;
            }
        }

        // ========================================
        // MINIMAPA
        // ========================================

        public void ToggleMinimapFullscreen(bool isFullscreen)
        {
            EnsureInitialized();
            if (_minimapContainer == null) return;
            _isMinimapFullscreen = isFullscreen;

            if (isFullscreen)
            {
                float rootW = _root != null && _root.resolvedStyle.width > 0 ? _root.resolvedStyle.width : Screen.width;
                float rootH = _root != null && _root.resolvedStyle.height > 0 ? _root.resolvedStyle.height : Screen.height;
                float side = Mathf.Round(Mathf.Min(rootW, rootH) * 0.78f);

                _minimapContainer.style.width = side;
                _minimapContainer.style.height = side;
                _minimapContainer.style.left = Mathf.Round((rootW - side) * 0.5f);
                _minimapContainer.style.top = Mathf.Round((rootH - side) * 0.5f);
                _minimapContainer.style.right = StyleKeyword.Null;
                _minimapContainer.style.bottom = StyleKeyword.Null;

                if (_minimapPaperFrame != null)
                {
                    _minimapPaperFrame.style.width = Length.Percent(100);
                    _minimapPaperFrame.style.height = Length.Percent(100);
                    _minimapPaperFrame.style.backgroundImage = StyleKeyword.None;
                    SetBorderRadius(_minimapPaperFrame, 12);
                }
            }
            else
            {
                _minimapContainer.style.width = StyleKeyword.Null;
                _minimapContainer.style.height = StyleKeyword.Null;
                _minimapContainer.style.left = StyleKeyword.Null;
                _minimapContainer.style.top = StyleKeyword.Null;
                _minimapContainer.style.right = 28;
                _minimapContainer.style.bottom = 24;

                if (_minimapPaperFrame != null)
                {
                    _minimapPaperFrame.style.width = 180;
                    _minimapPaperFrame.style.height = 180;
                    var catalog = PiariaUIAssetCatalog.Instance;
                    if (catalog != null && catalog.minimapPaperBase != null)
                        _minimapPaperFrame.style.backgroundImage = new StyleBackground(catalog.minimapPaperBase);
                    SetBorderRadius(_minimapPaperFrame, 90);
                }
            }
        }

        private void SetBorderRadius(VisualElement el, float radius)
        {
            if (el == null) return;
            el.style.borderTopLeftRadius = radius;
            el.style.borderTopRightRadius = radius;
            el.style.borderBottomLeftRadius = radius;
            el.style.borderBottomRightRadius = radius;
        }

        private void Update()
        {
            // Vincula dinamicamente a RenderTexture do WorldImage do minimapa se ainda nÃƒÆ’Ã‚Â£o estiver vinculada
            if (_minimapImage != null && _minimapImage.resolvedStyle.backgroundImage.texture == null)
            {
                var worldImage = FindFirstObjectByType<Kamgam.UGUIWorldImage.WorldImage>();
                if (worldImage != null && worldImage.RenderTexture != null)
                {
                    _minimapImage.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(worldImage.RenderTexture));
                }
            }
        }
    }
}
