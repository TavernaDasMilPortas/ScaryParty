using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using ScaryParty.UI.Piaria.Core;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;
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

        // Noite / Combate / Mochila (O5, O8, C1-C4)
        private VisualElement _nightBannerBox;
        private Label _nightBannerLabel;
        private VisualElement _combatStatusBox;
        private Label _healthStatusLabel;
        private Label _participantsStatusLabel;
        private Label _slingStatusLabel;
        private Label _bagIntegrityLabel;
        private Label _weaponAmmoLabel;
        private Label _claimedOrdersLabel;
        private Label _employeesStatusLabel;
        private Label _hintsTextLabel;
        private VisualElement _crosshairContainer;
        private float _hudMonitorTimer = 0f;
        private readonly Dictionary<int, float> _lowStockAlertCooldowns = new Dictionary<int, float>();

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
                    ?? UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Piaaria/UI/Piaria/UXML/MainGameHUD.uxml")
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

            EnsureExperienceHudWidgets();
            _hintsTextLabel = _root.Q<Label>(className: "hints-text");
            if (_hintsTextLabel != null)
            {
                _hintsTextLabel.text = "[F1] Tutorial • [J] Operações • [M] Mapa • [B] Mochila • [G] Bandoleira • [H] Receitas • [TAB] Placar • [ESC] Pausa";
            }

            NightResultsPresenter.EnsureInstance();
            ContextualTutorialPresenter.EnsureInstance();
            PauseEscapeMenuPresenter.EnsureInstance();
            OperationsHubPresenter.EnsureInstance();
            LifeAndSpectatorOverlayPresenter.EnsureInstance();
            RunPreparationPresenter.EnsureInstance();
        }

        private void EnsureExperienceHudWidgets()
        {
            if (_root == null || _nightBannerBox != null) return;

            _nightBannerBox = new VisualElement();
            _nightBannerBox.name = "NightStatusBanner";
            _nightBannerBox.pickingMode = PickingMode.Ignore;
            _nightBannerBox.style.position = Position.Absolute;
            _nightBannerBox.style.top = 14;
            _nightBannerBox.style.left = Length.Percent(28);
            _nightBannerBox.style.width = Length.Percent(44);
            _nightBannerBox.style.backgroundColor = new Color(0.08f, 0.08f, 0.11f, 0.82f);
            _nightBannerBox.style.borderTopLeftRadius = 8;
            _nightBannerBox.style.borderTopRightRadius = 8;
            _nightBannerBox.style.borderBottomLeftRadius = 8;
            _nightBannerBox.style.borderBottomRightRadius = 8;
            _nightBannerBox.style.borderBottomWidth = 2;
            _nightBannerBox.style.borderBottomColor = new Color(0.95f, 0.72f, 0.25f);
            _nightBannerBox.style.paddingTop = 6;
            _nightBannerBox.style.paddingBottom = 6;
            _nightBannerBox.style.paddingLeft = 14;
            _nightBannerBox.style.paddingRight = 14;

            _nightBannerLabel = new Label("NOITE 1 — PREPARANDO...");
            _nightBannerLabel.pickingMode = PickingMode.Ignore;
            _nightBannerLabel.style.fontSize = 15;
            _nightBannerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _nightBannerLabel.style.color = new Color(0.98f, 0.94f, 0.84f);
            _nightBannerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _nightBannerBox.Add(_nightBannerLabel);
            _root.Add(_nightBannerBox);

            _combatStatusBox = new VisualElement();
            _combatStatusBox.name = "CombatBagStatusBox";
            _combatStatusBox.pickingMode = PickingMode.Ignore;
            _combatStatusBox.style.position = Position.Absolute;
            _combatStatusBox.style.left = 20;
            _combatStatusBox.style.bottom = 175;
            _combatStatusBox.style.backgroundColor = new Color(0.08f, 0.09f, 0.12f, 0.80f);
            _combatStatusBox.style.borderTopLeftRadius = 8;
            _combatStatusBox.style.borderTopRightRadius = 8;
            _combatStatusBox.style.borderBottomLeftRadius = 8;
            _combatStatusBox.style.borderBottomRightRadius = 8;
            _combatStatusBox.style.paddingLeft = 12;
            _combatStatusBox.style.paddingRight = 12;
            _combatStatusBox.style.paddingTop = 8;
            _combatStatusBox.style.paddingBottom = 8;

            _healthStatusLabel = new Label("HP: 100 / 100");
            _healthStatusLabel.pickingMode = PickingMode.Ignore;
            _healthStatusLabel.style.fontSize = 14;
            _healthStatusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _healthStatusLabel.style.color = new Color(0.45f, 0.92f, 0.55f);
            _combatStatusBox.Add(_healthStatusLabel);

            _participantsStatusLabel = new Label("Equipe Online: Local");
            _participantsStatusLabel.pickingMode = PickingMode.Ignore;
            _participantsStatusLabel.style.fontSize = 12;
            _participantsStatusLabel.style.color = new Color(0.84f, 0.90f, 0.96f);
            _participantsStatusLabel.style.whiteSpace = WhiteSpace.Normal;
            _combatStatusBox.Add(_participantsStatusLabel);

            _slingStatusLabel = new Label("Bandoleira [G]: Sem espingarda");
            _slingStatusLabel.pickingMode = PickingMode.Ignore;
            _slingStatusLabel.style.fontSize = 12;
            _slingStatusLabel.style.color = new Color(0.95f, 0.85f, 0.52f);
            _combatStatusBox.Add(_slingStatusLabel);

            _bagIntegrityLabel = new Label("Mochila [B]: Sem mochila");
            _bagIntegrityLabel.pickingMode = PickingMode.Ignore;
            _bagIntegrityLabel.style.fontSize = 13;
            _bagIntegrityLabel.style.color = new Color(0.88f, 0.88f, 0.92f);
            _combatStatusBox.Add(_bagIntegrityLabel);

            _weaponAmmoLabel = new Label("");
            _weaponAmmoLabel.pickingMode = PickingMode.Ignore;
            _weaponAmmoLabel.style.fontSize = 14;
            _weaponAmmoLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _weaponAmmoLabel.style.color = new Color(0.98f, 0.82f, 0.42f);
            _weaponAmmoLabel.style.display = DisplayStyle.None;
            _combatStatusBox.Add(_weaponAmmoLabel);

            _claimedOrdersLabel = new Label("");
            _claimedOrdersLabel.pickingMode = PickingMode.Ignore;
            _claimedOrdersLabel.style.fontSize = 12;
            _claimedOrdersLabel.style.color = new Color(0.75f, 0.90f, 0.98f);
            _claimedOrdersLabel.style.display = DisplayStyle.None;
            _combatStatusBox.Add(_claimedOrdersLabel);

            _employeesStatusLabel = new Label("");
            _employeesStatusLabel.pickingMode = PickingMode.Ignore;
            _employeesStatusLabel.style.fontSize = 12;
            _employeesStatusLabel.style.color = new Color(0.86f, 0.82f, 0.96f);
            _employeesStatusLabel.style.display = DisplayStyle.None;
            _combatStatusBox.Add(_employeesStatusLabel);

            _root.Add(_combatStatusBox);

            _crosshairContainer = new VisualElement();
            _crosshairContainer.name = "CombatCrosshair";
            _crosshairContainer.pickingMode = PickingMode.Ignore;
            _crosshairContainer.style.position = Position.Absolute;
            _crosshairContainer.style.left = Length.Percent(50);
            _crosshairContainer.style.top = Length.Percent(50);
            _crosshairContainer.style.width = 16;
            _crosshairContainer.style.height = 16;
            _crosshairContainer.style.marginLeft = -8;
            _crosshairContainer.style.marginTop = -8;
            _crosshairContainer.style.borderTopWidth = 2;
            _crosshairContainer.style.borderBottomWidth = 2;
            _crosshairContainer.style.borderLeftWidth = 2;
            _crosshairContainer.style.borderRightWidth = 2;
            _crosshairContainer.style.borderTopColor = Color.white;
            _crosshairContainer.style.borderBottomColor = Color.white;
            _crosshairContainer.style.borderLeftColor = Color.white;
            _crosshairContainer.style.borderRightColor = Color.white;
            _crosshairContainer.style.borderTopLeftRadius = 8;
            _crosshairContainer.style.borderTopRightRadius = 8;
            _crosshairContainer.style.borderBottomLeftRadius = 8;
            _crosshairContainer.style.borderBottomRightRadius = 8;
            _crosshairContainer.style.display = DisplayStyle.None;
            _root.Add(_crosshairContainer);
        }

        public void SetCombatHealthHud(
            int currentHp,
            int maxHp,
            bool incapacitated,
            float rescueSeconds = 0f,
            bool isDead = false,
            float rescueProgress = 0f,
            byte rescueMode = 0,
            ulong spectatingTargetClientId = 0)
        {
            EnsureInitialized();
            if (_healthStatusLabel == null) return;

            if (isDead)
            {
                _healthStatusLabel.text = $"MORTO — Espectando Aliado #{spectatingTargetClientId} ([Q]/[E] trocar)";
                _healthStatusLabel.style.color = new Color(0.85f, 0.35f, 0.45f);
            }
            else if (incapacitated)
            {
                string pct = rescueProgress > 0.01f ? $" | Resgatando {Mathf.RoundToInt(rescueProgress * 100f)}%" : string.Empty;
                _healthStatusLabel.text = $"INCAPACITADO ({Mathf.CeilToInt(rescueSeconds)}s restantes{pct})";
                _healthStatusLabel.style.color = new Color(0.95f, 0.28f, 0.28f);
            }
            else
            {
                string healPct = rescueMode == 1 && rescueProgress > 0.01f
                    ? $" | Curando {Mathf.RoundToInt(rescueProgress * 100f)}%"
                    : string.Empty;
                _healthStatusLabel.text = $"HP: {currentHp} / {maxHp}{healPct}";
                float ratio = maxHp > 0 ? (float)currentHp / maxHp : 1f;
                _healthStatusLabel.style.color = ratio <= 0.3f
                    ? new Color(0.95f, 0.35f, 0.30f)
                    : (ratio <= 0.6f ? new Color(0.95f, 0.78f, 0.25f) : new Color(0.45f, 0.92f, 0.55f));
            }
        }

        public void SetWeaponAmmoHud(bool equipped, string weaponLabel, int currentAmmo, int reserveAmmo, bool isReloading, bool isAiming)
        {
            EnsureInitialized();
            if (_weaponAmmoLabel != null)
            {
                if (!equipped)
                {
                    _weaponAmmoLabel.style.display = DisplayStyle.None;
                }
                else
                {
                    _weaponAmmoLabel.style.display = DisplayStyle.Flex;
                    _weaponAmmoLabel.text = isReloading
                        ? $"{weaponLabel}: Recarregando..."
                        : $"{weaponLabel}: {currentAmmo} / {reserveAmmo}";
                }
            }

            if (_crosshairContainer != null)
            {
                _crosshairContainer.style.display = equipped ? DisplayStyle.Flex : DisplayStyle.None;
                float size = isAiming ? 12f : 20f;
                _crosshairContainer.style.width = size;
                _crosshairContainer.style.height = size;
                _crosshairContainer.style.marginLeft = -size * 0.5f;
                _crosshairContainer.style.marginTop = -size * 0.5f;
            }
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

            _hudMonitorTimer += Time.deltaTime;
            if (_hudMonitorTimer >= 0.4f)
            {
                _hudMonitorTimer = 0f;
                RefreshExperienceHud();
            }
        }

        private void RefreshExperienceHud()
        {
            var netState = PizzeriaNetworkState.Instance;
            if (netState == null) return;

            var n = netState.NightState.Value;
            if (_nightBannerLabel != null && n.NightNumber >= 0)
            {
                var phase = (NightPhase)n.Phase;
                string phaseLabel = phase switch
                {
                    NightPhase.Preparing => $"PREPARAÇÃO ({Mathf.CeilToInt(n.PrepRemainingSeconds)}s)",
                    NightPhase.Active => n.UnlimitedShiftTime
                        ? $"SEM LIMITE ({FormatMmSs(n.ElapsedSeconds)})"
                        : FormatMmSs(n.RemainingSeconds),
                    NightPhase.Closing => $"FECHANDO ({Mathf.CeilToInt(n.GraceRemainingSeconds)}s)",
                    NightPhase.Results => "VITÓRIA!",
                    NightPhase.Defeat => "DERROTA",
                    _ => ""
                };

                string repDelta = n.LastReputationDelta != 0
                    ? $" ({(n.LastReputationDelta > 0 ? "+" : "")}{n.LastReputationDelta})"
                    : string.Empty;
                int budget = netState.RestaurantBudget.Value;
                string nightTag = n.NightNumber == 0 ? "NOITE 0 (TUTORIAL)" : $"NOITE {n.NightNumber}";
                _nightBannerLabel.text = $"{nightTag} [{phaseLabel}]  |  META: R$ {n.EarnedRevenue}/{n.RevenueGoal}  |  CAIXA: R$ {budget}  |  REP: {n.Reputation}{repDelta}";
            }

            ulong localClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0UL;
            if (_slingStatusLabel != null)
            {
                if (netState.TryGetSlingedWeapon(localClientId, out var slinged))
                {
                    _slingStatusLabel.text = $"Bandoleira [G]: Espingarda guardada ({slinged.CurrentMagAmmo}/{slinged.MaxMagAmmo})";
                    _slingStatusLabel.style.color = new Color(0.55f, 0.95f, 0.62f);
                }
                else if (netState.TryGetEquippedWeapon(localClientId, out var eqWep) && eqWep.WeaponType == 2)
                {
                    _slingStatusLabel.text = "Bandoleira [G]: Espingarda em punho ([G] p/ guardar)";
                    _slingStatusLabel.style.color = new Color(0.98f, 0.85f, 0.42f);
                }
                else
                {
                    _slingStatusLabel.text = "Bandoleira [G]: Livre (sem espingarda)";
                    _slingStatusLabel.style.color = new Color(0.75f, 0.78f, 0.84f);
                }
            }

            if (_participantsStatusLabel != null && netState.PlayerCombatStates != null)
            {
                var pSb = new System.Text.StringBuilder("Equipe: ");
                var allPlayers = FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
                int listed = 0;
                for (int i = 0; i < netState.PlayerCombatStates.Count; i++)
                {
                    var pcs = netState.PlayerCombatStates[i];
                    string pName = null;
                    for (int p = 0; p < allPlayers.Length; p++)
                    {
                        if (allPlayers[p] != null && allPlayers[p].OwnerClientId == pcs.ClientId)
                        {
                            string candidateName = allPlayers[p].PlayerName.Value.ToString();
                            if (!string.IsNullOrWhiteSpace(candidateName)) pName = candidateName;
                            break;
                        }
                    }
                    if (string.IsNullOrEmpty(pName))
                        pName = pcs.ClientId == localClientId ? "Você" : $"Jog.#{pcs.ClientId}";

                    string lifeTag = pcs.IsDead
                        ? "Morto"
                        : (pcs.IsIncapacitated ? "Nocauteado" : $"{pcs.CurrentHp}HP");
                    if (listed > 0) pSb.Append(" | ");
                    pSb.Append($"{pName}: {lifeTag}");
                    listed++;
                }
                _participantsStatusLabel.text = listed > 0 ? pSb.ToString() : "Equipe: Local (1 jogador)";
            }

            if (_claimedOrdersLabel != null && netState.Orders != null)
            {
                var claimedSb = new System.Text.StringBuilder();
                double now = PizzeriaRoot.Instance != null && PizzeriaRoot.Instance.Clock != null
                    ? PizzeriaRoot.Instance.Clock.Now
                    : Time.timeAsDouble;
                for (int i = 0; i < netState.Orders.Count; i++)
                {
                    var ord = netState.Orders[i];
                    if (ord.Lifecycle == (byte)OrderLifecycle.Accepted && ord.ClaimedByClientId == localClientId)
                    {
                        int remSec = Mathf.Max(0, Mathf.CeilToInt((float)(ord.DeadlineTime - now)));
                        if (claimedSb.Length > 0) claimedSb.Append(" | ");
                        claimedSb.Append($"#{ord.OrderId} ({remSec}s)");
                    }
                }
                if (claimedSb.Length > 0)
                {
                    _claimedOrdersLabel.text = $"Comandas Assumidas: {claimedSb}";
                    _claimedOrdersLabel.style.display = DisplayStyle.Flex;
                }
                else
                {
                    _claimedOrdersLabel.style.display = DisplayStyle.None;
                }
            }

            if (_employeesStatusLabel != null && netState.Employees != null)
            {
                if (netState.Employees.Count == 0)
                {
                    _employeesStatusLabel.style.display = DisplayStyle.None;
                }
                else
                {
                    var empSb = new System.Text.StringBuilder("Equipe: ");
                    for (int i = 0; i < netState.Employees.Count; i++)
                    {
                        if (i > 0) empSb.Append(" | ");
                        var emp = netState.Employees[i];
                        string roleName = emp.Role == 0 ? "Caixa" : "Pizzaiolo";
                        if (!emp.IsAlive)
                        {
                            empSb.Append($"{roleName}#{emp.EmployeeId}: Caído");
                        }
                        else if (emp.TaskState == (byte)ScaryParty.Pizzeria.Domain.Services.EmployeeTaskState.Blocked)
                        {
                            string reason = emp.BlockReason switch
                            {
                                1 => "Sem Massa",
                                2 => "Falta Ingred.",
                                3 => "Forno Cheio",
                                4 => "Ferram. em Uso",
                                5 => "Caminho Bloq.",
                                _ => "Bloqueado"
                            };
                            empSb.Append($"{roleName}#{emp.EmployeeId}: {reason}");
                        }
                        else if (emp.TargetOrderId > 0)
                        {
                            empSb.Append($"{roleName}#{emp.EmployeeId}: Ped.#{emp.TargetOrderId}");
                        }
                        else
                        {
                            empSb.Append($"{roleName}#{emp.EmployeeId}: Livre");
                        }
                    }
                    _employeesStatusLabel.text = empSb.ToString();
                    _employeesStatusLabel.style.display = DisplayStyle.Flex;
                }
            }

            if (_bagIntegrityLabel != null && netState.Backpacks != null)
            {
                ulong localId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0UL;
                bool found = false;
                for (int i = 0; i < netState.Backpacks.Count; i++)
                {
                    var bp = netState.Backpacks[i];
                    if (bp.EquippedBy == localId)
                    {
                        found = true;
                        int pct = bp.MaxIntegrity > 0 ? Mathf.RoundToInt(100f * bp.Integrity / bp.MaxIntegrity) : 100;
                        if (bp.IsTorn)
                        {
                            _bagIntegrityLabel.text = "Mochila: RASGADA (0%) — Segure [F] 3s";
                            _bagIntegrityLabel.style.color = new Color(0.95f, 0.25f, 0.25f);
                        }
                        else if (pct <= 40)
                        {
                            _bagIntegrityLabel.text = $"Mochila: {pct}% (Danificada - [F] 3s)";
                            _bagIntegrityLabel.style.color = new Color(0.95f, 0.72f, 0.20f);
                        }
                        else
                        {
                            _bagIntegrityLabel.text = $"Mochila: {pct}% ({bp.ItemCount}/{bp.Capacity})";
                            _bagIntegrityLabel.style.color = new Color(0.85f, 0.90f, 0.95f);
                        }
                        break;
                    }
                }
                if (!found)
                {
                    _bagIntegrityLabel.text = "Mochila: Nenhuma equipada";
                    _bagIntegrityLabel.style.color = new Color(0.75f, 0.75f, 0.80f);
                }
            }

            // Alerta de estoque baixo (O8 / N6) com cooldown de 45s por ingrediente
            if (n.Phase == (byte)NightPhase.Active && netState.StorageSlots != null)
            {
                float now = Time.unscaledTime;
                var catalog = PizzeriaRoot.Instance?.Catalog;
                for (int i = 0; i < netState.StorageSlots.Count; i++)
                {
                    var slot = netState.StorageSlots[i];
                    if (slot.IngredientDefId != 1 && slot.IngredientDefId != 3 && slot.IngredientDefId != 4)
                        continue;

                    if (slot.Quantity <= 3)
                    {
                        if (!_lowStockAlertCooldowns.TryGetValue(slot.IngredientDefId, out float nextAllowed) || now >= nextAllowed)
                        {
                            _lowStockAlertCooldowns[slot.IngredientDefId] = now + 45f;
                            string ingName = catalog != null && catalog.Ingredients.TryGetValue(slot.IngredientDefId, out var def)
                                ? def.Name
                                : $"Ingrediente #{slot.IngredientDefId}";
                            NotificationManager.Show(
                                slot.Quantity <= 0
                                    ? $"Estoque ESGOTADO de {ingName}! Encomende suprimentos."
                                    : $"Estoque baixo de {ingName} ({slot.Quantity}/{slot.MaxCapacity})!",
                                NotificationType.Warning);
                        }
                    }
                }
            }
        }

        private static string FormatMmSs(float seconds)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
