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
        private VisualElement _backpackPanel;
        private VisualElement _backpackBox;
        private VisualElement _backpackPizzaLayer;
        private VisualElement _backpackGradientLayer;
        private Label _backpackCountLabel;

        // Mãos
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
        private bool _isMinimapFullscreen = false;
        private bool _hudGameplayVisible = true;

        // Noite / Combate / Mochila / Dica contextual
        private VisualElement _hudTopLeft;
        private VisualElement _hudTopRight;
        private VisualElement _hudBottomLeft;
        private VisualElement _hudBottomRight;
        private VisualElement _nightBannerBox;
        private Label _nightBannerLabel;
        private VisualElement _combatStatusBox;
        private Label _healthStatusLabel;
        private VisualElement _slingPill;
        private Label _slingStatusLabel;
        private Label _bagIntegrityLabel;
        private VisualElement _weaponAmmoPill;
        private Label _weaponAmmoLabel;
        private VisualElement _contextualHintContainer;
        private Label _contextualHintLabel;
        private float _contextualHintHideAt = -1f;
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
            GameplayIntroCutsceneFlow.OnStageChanged -= HandlePresentationStageChanged;
            GameplayIntroCutsceneFlow.OnStageChanged += HandlePresentationStageChanged;
        }

        private void HandlePresentationStageChanged(IntroPresentationStage stage)
        {
            if (stage != IntroPresentationStage.NightActive)
            {
                SetGameplayHudVisible(false);
            }
        }

        private void OnDestroy()
        {
            GameplayIntroCutsceneFlow.OnStageChanged -= HandlePresentationStageChanged;
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
                var template = PiariaPresenterUtils.ResolveTemplate(
                    c => c.templateMainGameHud,
                    "Assets/Piaaria/UI/Piaria/UXML/MainGameHUD.uxml");
                if (template != null)
                {
                    template.CloneTree(uiDocument.rootVisualElement);
                    _root = uiDocument.rootVisualElement.Q<VisualElement>("MainGameHUDRoot");
                }
            }
            if (_root == null) return;

            _root.pickingMode = PickingMode.Ignore;
            PiariaPresenterUtils.ConfigureHierarchyPicking(_root);
            PiariaPresenterUtils.RegisterResponsiveLayout(_root);

            _moneyLabel = _root.Q<Label>("MoneyLabel");
            _scoreLabel = _root.Q<Label>("ScoreLabel");
            _activeOrdersLabel = _root.Q<Label>("ActiveOrdersLabel");
            _interactionPrompt = _root.Q<Label>("InteractionPrompt");

            _progressBarBox = _root.Q<VisualElement>("ProgressBarContainer");
            _progressBarFill = _root.Q<VisualElement>("ProgressBarFill");
            _progressBarLabel = _root.Q<Label>("ProgressBarLabel");

            _backpackPanel = _root.Q<VisualElement>("BackpackPanel");
            _backpackBox = _root.Q<VisualElement>("BackpackContainer");
            _backpackPizzaLayer = _root.Q<VisualElement>("BackpackPizzaLayer");
            _backpackGradientLayer = _root.Q<VisualElement>("BackpackGradientLayer");
            _backpackCountLabel = _root.Q<Label>("BackpackCountLabel");
            _bagIntegrityLabel = _root.Q<Label>("BagIntegrityLabel");

            _leftHandCard = _root.Q<VisualElement>("LeftHandSlot");
            _rightHandCard = _root.Q<VisualElement>("RightHandSlot");
            _leftHandContent = _root.Q<Label>("LeftHandContent");
            _rightHandContent = _root.Q<Label>("RightHandContent");
            _digit1Icon = _root.Q<VisualElement>("Digit1Icon");
            _digit2Icon = _root.Q<VisualElement>("Digit2Icon");

            _minimapContainer = _root.Q<VisualElement>("MinimapContainer");
            _minimapPaperFrame = _root.Q<VisualElement>("MinimapPaperFrame");
            _minimapImage = _root.Q<VisualElement>("MinimapImage");

            _hudTopLeft = _root.Q<VisualElement>(className: "hud-top-left");
            _hudTopRight = _root.Q<VisualElement>(className: "hud-top-right");
            _hudBottomLeft = _root.Q<VisualElement>(className: "hud-bottom-left");
            _hudBottomRight = _root.Q<VisualElement>(className: "hud-bottom-right");

            _nightBannerBox = _root.Q<VisualElement>("NightStatusBanner");
            _nightBannerLabel = _root.Q<Label>("NightStatusLabel");
            _combatStatusBox = _root.Q<VisualElement>("CombatBagStatusBox");
            _healthStatusLabel = _root.Q<Label>("HealthStatusLabel");
            _weaponAmmoPill = _root.Q<VisualElement>("WeaponAmmoPill");
            _weaponAmmoLabel = _root.Q<Label>("WeaponAmmoLabel");
            _slingPill = _root.Q<VisualElement>("SlingPill");
            _slingStatusLabel = _root.Q<Label>("SlingStatusLabel");
            _contextualHintContainer = _root.Q<VisualElement>("ContextualHintContainer");
            _contextualHintLabel = _root.Q<Label>("ContextualHintLabel");
            _crosshairContainer = _root.Q<VisualElement>("CombatCrosshair");

            ApplyCatalogAssets();

            SetMoney(0, immediate: true);
            SetDeliveries(0);
            HidePrompt();
            HideProgress();
            SetBackpackCount(0);
            SetHandState(false, true, "Vazio");
            SetHandState(true, false, "Vazio");

            EnsureExperienceHudWidgets();

            bool introBlocking = GameplayIntroCutsceneFlow.Instance != null &&
                                 GameplayIntroCutsceneFlow.CurrentStage != IntroPresentationStage.NightActive;
            if (introBlocking)
            {
                SetGameplayHudVisible(false);
            }
            else
            {
                TryShowInitialDiscoveryHint();
            }

            NightResultsPresenter.EnsureInstance();
            ContextualTutorialPresenter.EnsureInstance();
            PauseEscapeMenuPresenter.EnsureInstance();
            OperationsHubPresenter.EnsureInstance();
            LifeAndSpectatorOverlayPresenter.EnsureInstance();
            RunPreparationPresenter.EnsureInstance();
        }

        private void TryShowInitialDiscoveryHint()
        {
            const string key = "Piaria_EscHintShownCount";
            int shownCount = PlayerPrefs.GetInt(key, 0);
            if (shownCount < 3)
            {
                PlayerPrefs.SetInt(key, shownCount + 1);
                PlayerPrefs.Save();
                ShowContextualHint("ESC: Controles e Pausa  |  F1: Tutorial", 5.5f);
            }
        }

        public void ShowContextualHint(string message, float durationSeconds = 4.0f)
        {
            EnsureInitialized();
            if (_contextualHintContainer == null || _contextualHintLabel == null) return;
            if (string.IsNullOrWhiteSpace(message))
            {
                HideContextualHint();
                return;
            }

            _contextualHintLabel.text = message;
            _contextualHintHideAt = durationSeconds > 0f ? Time.unscaledTime + durationSeconds : -1f;
            UIToolkitTweenService.SlideIn(_contextualHintContainer, new Vector2(0f, 16f), 0.22f);
        }

        public void HideContextualHint()
        {
            _contextualHintHideAt = -1f;
            if (_contextualHintContainer != null && _contextualHintContainer.style.display != DisplayStyle.None)
            {
                UIToolkitTweenService.FadeOut(_contextualHintContainer, 0.16f);
            }
        }

        public void SetGameplayHudVisible(bool visible)
        {
            EnsureInitialized();
            _hudGameplayVisible = visible;
            if (_root == null) return;

            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void RevealGameplayHudElastic()
        {
            EnsureInitialized();
            _hudGameplayVisible = true;
            if (_root == null) return;

            _root.style.display = DisplayStyle.Flex;

            UIToolkitTweenService.ElasticSlideIn(_hudTopLeft, new Vector2(-110f, -60f), 0.52f, 0.00f);
            UIToolkitTweenService.ElasticSlideIn(_hudTopRight, new Vector2(110f, -60f), 0.52f, 0.06f);
            UIToolkitTweenService.ElasticSlideIn(_hudBottomLeft, new Vector2(-110f, 90f), 0.54f, 0.12f);
            UIToolkitTweenService.ElasticSlideIn(_hudBottomRight, new Vector2(110f, 90f), 0.54f, 0.18f);

            NightIntermissionFeelBridge.Play(NightUIFeedbackId.CountdownGo, FeedbackPriority.High);
            UIFeedbackService.Instance?.PlaySuccess();
            TryShowInitialDiscoveryHint();
        }

        private void EnsureExperienceHudWidgets()
        {
            // Estrutura e estilização agora residem em MainGameHUD.uxml e HUD.uss.
            // Este método permanece apenas como guarda defensiva caso um template legado seja carregado.
            if (_root == null) return;
            if (_nightBannerBox == null) _nightBannerBox = _root.Q<VisualElement>("NightStatusBanner");
            if (_nightBannerLabel == null) _nightBannerLabel = _root.Q<Label>("NightStatusLabel");
            if (_combatStatusBox == null) _combatStatusBox = _root.Q<VisualElement>("CombatBagStatusBox");
            if (_healthStatusLabel == null) _healthStatusLabel = _root.Q<Label>("HealthStatusLabel");
            if (_crosshairContainer == null) _crosshairContainer = _root.Q<VisualElement>("CombatCrosshair");
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

            _healthStatusLabel.RemoveFromClassList("status-healthy");
            _healthStatusLabel.RemoveFromClassList("status-warning");
            _healthStatusLabel.RemoveFromClassList("status-critical");

            if (isDead)
            {
                _healthStatusLabel.text = $"MORTO (#{spectatingTargetClientId})";
                _healthStatusLabel.AddToClassList("status-critical");
            }
            else if (incapacitated)
            {
                string pct = rescueProgress > 0.01f ? $" ({Mathf.RoundToInt(rescueProgress * 100f)}%)" : string.Empty;
                _healthStatusLabel.text = $"NOCAUTE ({Mathf.CeilToInt(rescueSeconds)}s{pct})";
                _healthStatusLabel.AddToClassList("status-critical");
            }
            else
            {
                string healPct = rescueMode == 1 && rescueProgress > 0.01f
                    ? $" | +{Mathf.RoundToInt(rescueProgress * 100f)}%"
                    : string.Empty;
                _healthStatusLabel.text = $"HP {currentHp}/{maxHp}{healPct}";
                float ratio = maxHp > 0 ? (float)currentHp / maxHp : 1f;
                _healthStatusLabel.AddToClassList(ratio <= 0.3f ? "status-critical" : (ratio <= 0.6f ? "status-warning" : "status-healthy"));
            }
        }

        public void SetWeaponAmmoHud(bool equipped, string weaponLabel, int currentAmmo, int reserveAmmo, bool isReloading, bool isAiming)
        {
            EnsureInitialized();
            if (_weaponAmmoPill != null)
            {
                _weaponAmmoPill.style.display = equipped ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (_weaponAmmoLabel != null && equipped)
            {
                _weaponAmmoLabel.text = isReloading
                    ? $"{weaponLabel}: Recarregando..."
                    : $"{weaponLabel}: {currentAmmo}/{reserveAmmo}";
            }

            if (_crosshairContainer != null)
            {
                _crosshairContainer.style.display = equipped ? DisplayStyle.Flex : DisplayStyle.None;
                _crosshairContainer.EnableInClassList("crosshair-aiming", isAiming);
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
            UIToolkitTweenService.KillTweens(_minimapContainer);
            _minimapContainer.style.translate = new StyleTranslate(new Translate(0f, 0f, 0f));
            _minimapContainer.EnableInClassList("minimap-fullscreen", isFullscreen);

            if (_minimapPaperFrame != null)
            {
                if (isFullscreen)
                {
                    _minimapPaperFrame.style.backgroundImage = StyleKeyword.None;
                    UIToolkitTweenService.PopIn(_minimapPaperFrame, 0.22f, 0.88f);
                }
                else
                {
                    var catalog = PiariaUIAssetCatalog.Instance;
                    if (catalog != null && catalog.minimapPaperBase != null)
                        _minimapPaperFrame.style.backgroundImage = new StyleBackground(catalog.minimapPaperBase);
                }
            }
        }

        private void Update()
        {
            if (GameplayIntroCutsceneFlow.Instance != null &&
                GameplayIntroCutsceneFlow.CurrentStage != IntroPresentationStage.NightActive)
            {
                if (_root != null && _root.style.display != DisplayStyle.None)
                    _root.style.display = DisplayStyle.None;
                return;
            }

            // Vincula dinamicamente a RenderTexture do WorldImage do minimapa se ainda não estiver vinculada
            if (_minimapImage != null && _minimapImage.resolvedStyle.backgroundImage.texture == null)
            {
                var worldImage = FindFirstObjectByType<Kamgam.UGUIWorldImage.WorldImage>();
                if (worldImage != null && worldImage.RenderTexture != null)
                {
                    _minimapImage.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(worldImage.RenderTexture));
                }
            }

            if (_contextualHintHideAt > 0f && Time.unscaledTime >= _contextualHintHideAt)
            {
                HideContextualHint();
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

                string nightTag = n.NightNumber == 0 ? "NOITE 0" : $"NOITE {n.NightNumber}";
                string metaPart = n.RevenueGoal > 0 ? $" • Meta R$ {n.EarnedRevenue}/{n.RevenueGoal}" : string.Empty;
                _nightBannerLabel.text = $"{nightTag} [{phaseLabel}]{metaPart}";
            }

            ulong localClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0UL;
            if (_slingPill != null && _slingStatusLabel != null)
            {
                if (netState.TryGetSlingedWeapon(localClientId, out var slinged))
                {
                    _slingPill.style.display = DisplayStyle.Flex;
                    _slingStatusLabel.text = $"Bandoleira [G]: {slinged.CurrentMagAmmo}/{slinged.MaxMagAmmo}";
                }
                else if (netState.TryGetEquippedWeapon(localClientId, out var eqWep) && eqWep.WeaponType == 2)
                {
                    _slingPill.style.display = DisplayStyle.Flex;
                    _slingStatusLabel.text = "[G] Guardar Espingarda";
                }
                else
                {
                    _slingPill.style.display = DisplayStyle.None;
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
                        _bagIntegrityLabel.style.display = DisplayStyle.Flex;
                        _bagIntegrityLabel.RemoveFromClassList("bag-warning");
                        _bagIntegrityLabel.RemoveFromClassList("bag-critical");

                        if (bp.IsTorn)
                        {
                            _bagIntegrityLabel.text = "Rasgada [F] 3s";
                            _bagIntegrityLabel.AddToClassList("bag-critical");
                        }
                        else if (pct <= 40)
                        {
                            _bagIntegrityLabel.text = $"{pct}% [F]";
                            _bagIntegrityLabel.AddToClassList("bag-warning");
                        }
                        else
                        {
                            _bagIntegrityLabel.text = $"{pct}% ({bp.ItemCount}/{bp.Capacity})";
                        }
                        break;
                    }
                }
                if (!found)
                {
                    _bagIntegrityLabel.style.display = DisplayStyle.None;
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
