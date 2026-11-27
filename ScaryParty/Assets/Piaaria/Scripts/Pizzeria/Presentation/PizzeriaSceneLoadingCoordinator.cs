using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Netcode;
using ScaryParty.Navigation.Core;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Lighting;
using ScaryParty.VisualStyles;

namespace ScaryParty.Pizzeria.Presentation
{
    public enum LoadingStage
    {
        ConnectingSession,
        LoadingScene,
        GeneratingCity,
        BakingNavigation,
        SpawningPizzeria,
        SpawningPlayers,
        Ready
    }

    /// <summary>
    /// Coordenador persistente de transições de cena e carregamento (A16 + O1/N2).
    /// Controla o overlay temático em UI Toolkit, acompanha eventos reais de cena (local e NGO),
    /// bloqueia input via GameplayInputGate e aguarda cidade, NavMesh, pizzaria e jogador.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class PizzeriaSceneLoadingCoordinator : MonoBehaviour
    {
        public static PizzeriaSceneLoadingCoordinator Instance { get; private set; }

        public enum SceneReadinessProfile
        {
            MenuOrLobby = 0,
            ReadyRoom = 1,
            Gameplay = 2
        }

        [Header("Configuração de Transição")]
        [SerializeField] private float _fadeDuration = 0.55f;
        [SerializeField] private float _loadingTimeoutSeconds = 40f;

        private UIDocument _loadingUiDocument;
        private PanelSettings _runtimePanelSettings;
        private VisualElement _overlayRoot;
        private VisualElement _card;
        private ProgressBar _progressBar;
        private VisualElement _progressFillBar;
        private VisualElement _progressFillImage;
        private Label _percentLabel;
        private Label _nightBannerLabel;
        private Label _statusLabel;
        private Label _stageTagLabel;
        private Label _hintLabel;
        private VisualElement _spinnerIcon;
        private VisualElement _pizzaCorner;
        private readonly System.Collections.Generic.List<VisualElement> _waveLetters = new System.Collections.Generic.List<VisualElement>();
        private readonly VisualElement[] _loadingDots = new VisualElement[3];
        private bool _animationsRunning;
        private float _dotTimer;
        private int _dotStep;
        private bool _holdingForPresentation;
        private bool _waitingForPresentationHandoff;
        private bool _presentationReleased;
        private int _presentationHoldToken;
        private Button _cancelBtn;
        private VisualElement _errorBox;
        private Label _errorReasonLabel;
        private Button _errorRetryBtn;
        private Button _errorMenuBtn;

        private LoadingTransitionStateMachine _stateMachine;
        private readonly StageTimingRecorder _stageTimings = new StageTimingRecorder();
        private Action _retryCallback;
        private bool _hasInputLock;
        private NetworkSceneManager _hookedSceneManager;
        private NetworkManager _hookedDisconnectManager;
        private bool _currentTransitionIsNetworked;
        private float _hintTimer;
        private int _hintIndex;
        private float _visualProgress01;
        private float _visualProgressVelocity;

        private bool _isLoading => _stateMachine != null && _stateMachine.IsActiveState;
        private bool _awaitingConnection => _stateMachine != null && _stateMachine.State == LoadingOperationState.Connecting;
        private int _transitionId => _stateMachine != null ? _stateMachine.OperationToken : 0;
        private float _currentProgress => _stateMachine != null ? _stateMachine.ConfirmedProgress : 0f;
        private float _displayedProgress => _stateMachine != null ? _stateMachine.DisplayedProgress : 0f;

        private LoadingStage _currentStage = LoadingStage.ConnectingSession;
        private int _displayNightNumber = 1;
        private int _displayNightGoal = 150;
        private AsyncOperation _trackedNetworkAsyncOp;
        private bool _networkSceneLoadCompleted;
        private string _expectedTargetScene;

        private static readonly string[] GameplayHints = new[]
        {
            "Dica: Segure [F] no balcão para atender clientes e registrar pedidos no quadro.",
            "Dica: Clientes sentados no salão recebem a pizza sem caixa diretamente no prato.",
            "Dica: Use caixas apenas para pedidos recebidos pelo telefone (Delivery).",
            "Dica: Retire a pizza do forno assim que dourar para evitar fumaça e prejuízo.",
            "Dica: A despensa guarda massa e caixas; a geladeira guarda queijos e frios.",
            "Dica: Na rua, pressione [E] perto da moto de entregas com as mãos livres.",
            "Dica: Cumpra a meta de faturamento da noite para avançar para a próxima!"
        };

        public static PizzeriaSceneLoadingCoordinator EnsureInstance()
        {
            if (Instance != null) return Instance;

            var existing = FindFirstObjectByType<PizzeriaSceneLoadingCoordinator>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var go = new GameObject("PizzeriaSceneLoadingCoordinator");
            return go.AddComponent<PizzeriaSceneLoadingCoordinator>();
        }

        public LoadingStage CurrentStage => _currentStage;
        public float CurrentProgress => _currentProgress;
        public bool IsLoading => _isLoading;
        public bool IsHoldingForPresentation => _holdingForPresentation;
        public bool IsWaitingForPresentationHandoff => _waitingForPresentationHandoff;
        public int CurrentOperationToken => _transitionId;
        public LoadingOperationState OperationState => _stateMachine != null ? _stateMachine.State : LoadingOperationState.Idle;

        public bool HoldForPresentation(int token)
        {
            if (token == 0 || token != _transitionId) return false;
            _holdingForPresentation = true;
            _presentationHoldToken = token;
            _presentationReleased = false;
            return true;
        }

        public void ReleaseToCutscene(int token)
        {
            if (!_holdingForPresentation) return;
            if (token != 0 && token != _presentationHoldToken) return;
            Debug.Log($"[LoadingCoordinator] ReleaseToCutscene recebido (token={token}) — iniciando fade-out do loading.");
            _presentationReleased = true;
            _holdingForPresentation = false;
        }

        private LoadingTransitionStateMachine EnsureStateMachine()
        {
            if (_stateMachine == null)
            {
                _stateMachine = new LoadingTransitionStateMachine(AcquireInputLock, ReleaseInputLockIfHeld);
            }
            return _stateMachine;
        }

        public bool IsOperationActive(int token)
        {
            return _stateMachine != null && _stateMachine.IsTokenActive(token);
        }

        public void SetNightContext(int nightNumber, int revenueGoal)
        {
            _displayNightNumber = Mathf.Max(1, nightNumber);
            _displayNightGoal = Mathf.Max(0, revenueGoal);
            RefreshNightBannerText();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureUIOverlay();
            TryHookNetworkSceneEvents();
        }

        private void OnEnable()
        {
            TryHookNetworkSceneEvents();
        }

        private void OnDisable()
        {
            UnhookNetworkSceneEvents();
            _stateMachine?.ForceReleaseOnDestroy();
            ReleaseInputLockIfHeld();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnhookNetworkSceneEvents();
            _stateMachine?.ForceReleaseOnDestroy();
            ReleaseInputLockIfHeld();
        }

        private void EnsureUIOverlay()
        {
            if (_loadingUiDocument == null)
            {
                _loadingUiDocument = gameObject.GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
            }

            if (_runtimePanelSettings == null)
            {
                var catalog = ScaryParty.UI.Piaria.Core.PiariaUIAssetCatalog.Instance;
                if (catalog != null && catalog.panelSettings != null)
                {
                    _runtimePanelSettings = Instantiate(catalog.panelSettings);
                }
                else
                {
                    _runtimePanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                    _runtimePanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                    _runtimePanelSettings.referenceResolution = new Vector2Int(1920, 1080);
                    _runtimePanelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                    _runtimePanelSettings.match = 0.5f;
                }
                _runtimePanelSettings.sortingOrder = 5000;
                _runtimePanelSettings.name = "LoadingPanelSettings_Runtime";
            }

            if (_loadingUiDocument.panelSettings == null)
            {
                _loadingUiDocument.panelSettings = _runtimePanelSettings;
            }
            else
            {
                _loadingUiDocument.panelSettings.sortingOrder = Mathf.Max(_loadingUiDocument.panelSettings.sortingOrder, 5000);
            }
            _loadingUiDocument.sortingOrder = 5000;

            if (_overlayRoot == null)
            {
                var docRoot = _loadingUiDocument.rootVisualElement;
                var template = ScaryParty.UI.Piaria.Core.PiariaPresenterUtils.ResolveTemplate(
                    c => c.templateLoadingScreen,
                    "Assets/Piaaria/UI/Piaria/UXML/LoadingScreen.uxml");

                if (template != null && docRoot != null)
                {
                    template.CloneTree(docRoot);
                    _overlayRoot = docRoot.Q<VisualElement>("PizzeriaLoadingOverlay");
                    if (_overlayRoot != null)
                    {
                        BindTemplateOverlayElements();
                    }
                }
            }

            if (_overlayRoot == null)
            {
                BuildFallbackOverlay();
            }
        }

        private void BindTemplateOverlayElements()
        {
            if (_overlayRoot == null) return;
            _overlayRoot.style.display = DisplayStyle.None;
            _overlayRoot.style.opacity = 0f;
            ScaryParty.UI.Piaria.Core.PiariaPresenterUtils.RegisterResponsiveLayout(_overlayRoot);

            _card = _overlayRoot.Q<VisualElement>("LoadingCenterContainer");
            _nightBannerLabel = _overlayRoot.Q<Label>("LoadingNightBanner");
            _stageTagLabel = _overlayRoot.Q<Label>("LoadingStageTag");
            _percentLabel = _overlayRoot.Q<Label>("LoadingPercentLabel");
            _progressFillBar = _overlayRoot.Q<VisualElement>("LoadingBarFill");
            _progressFillImage = _overlayRoot.Q<VisualElement>("LoadingBarFillImage");
            _statusLabel = _overlayRoot.Q<Label>("LoadingStatusLabel");
            _hintLabel = _overlayRoot.Q<Label>("LoadingHintLabel");
            _cancelBtn = _overlayRoot.Q<Button>("LoadingCancelBtn");
            _errorBox = _overlayRoot.Q<VisualElement>("LoadingErrorBox");
            _errorReasonLabel = _overlayRoot.Q<Label>("LoadingErrorReason");
            _errorRetryBtn = _overlayRoot.Q<Button>("LoadingErrorRetryBtn");
            _errorMenuBtn = _overlayRoot.Q<Button>("LoadingErrorMenuBtn");
            _pizzaCorner = _overlayRoot.Q<VisualElement>("LoadingPizzaCorner");

            _waveLetters.Clear();
            _overlayRoot.Query<VisualElement>(className: "loading-wave-letter").ForEach(el => _waveLetters.Add(el));
            _loadingDots[0] = _overlayRoot.Q<VisualElement>("LoadingDot0");
            _loadingDots[1] = _overlayRoot.Q<VisualElement>("LoadingDot1");
            _loadingDots[2] = _overlayRoot.Q<VisualElement>("LoadingDot2");

            var catalog = ScaryParty.UI.Piaria.Core.PiariaUIAssetCatalog.Instance;
            if (catalog != null)
            {
                var bgLayer = _overlayRoot.Q<VisualElement>("LoadingBgLayer");
                if (bgLayer != null && catalog.loadingBackground != null)
                    bgLayer.style.backgroundImage = new StyleBackground(catalog.loadingBackground);

                var barFrame = _overlayRoot.Q<VisualElement>("LoadingBarFrame");
                if (barFrame != null && catalog.progressBarFrame != null)
                    barFrame.style.backgroundImage = new StyleBackground(catalog.progressBarFrame);

                if (barFrame != null && _progressFillImage != null)
                {
                    barFrame.RegisterCallback<GeometryChangedEvent>(evt =>
                    {
                        float w = barFrame.contentRect.width;
                        if (w > 1f) _progressFillImage.style.width = w;
                    });
                }

                var fillTarget = _progressFillImage ?? _progressFillBar;
                if (fillTarget != null && catalog.progressBarFill != null)
                {
                    fillTarget.style.backgroundImage = new StyleBackground(catalog.progressBarFill);
                    fillTarget.style.backgroundColor = Color.clear;
                }

                if (_pizzaCorner != null && catalog.loadingPizza != null)
                    _pizzaCorner.style.backgroundImage = new StyleBackground(catalog.loadingPizza);

                if (catalog.loadingDot != null)
                {
                    for (int i = 0; i < _loadingDots.Length; i++)
                    {
                        if (_loadingDots[i] != null)
                        {
                            _loadingDots[i].style.backgroundImage = new StyleBackground(catalog.loadingDot);
                            _loadingDots[i].style.backgroundColor = Color.clear;
                            _loadingDots[i].style.borderTopWidth = 0;
                            _loadingDots[i].style.borderBottomWidth = 0;
                            _loadingDots[i].style.borderLeftWidth = 0;
                            _loadingDots[i].style.borderRightWidth = 0;
                        }
                    }
                }
            }

            if (_cancelBtn != null)
            {
                _cancelBtn.clicked += CancelCurrentOperation;
                ScaryParty.UI.Piaria.Core.UIToolkitTweenService.BindInteractiveFeedback(_cancelBtn, 1.03f, 0.97f);
            }
            if (_errorRetryBtn != null)
            {
                _errorRetryBtn.clicked += RetryFailedOperation;
                ScaryParty.UI.Piaria.Core.UIToolkitTweenService.BindInteractiveFeedback(_errorRetryBtn, 1.03f, 0.97f);
            }
            if (_errorMenuBtn != null)
            {
                _errorMenuBtn.clicked += ReturnToLobbyFromError;
                ScaryParty.UI.Piaria.Core.UIToolkitTweenService.BindInteractiveFeedback(_errorMenuBtn, 1.03f, 0.97f);
            }

            RefreshNightBannerText();
        }

        private void BuildFallbackOverlay()
        {
            if (_overlayRoot != null) return;
            _overlayRoot = new VisualElement
            {
                name = "PizzeriaLoadingOverlay"
            };

                _overlayRoot.style.position = Position.Absolute;
                _overlayRoot.style.left = 0;
                _overlayRoot.style.right = 0;
                _overlayRoot.style.top = 0;
                _overlayRoot.style.bottom = 0;
                _overlayRoot.style.backgroundColor = new Color(0.06f, 0.05f, 0.08f, 0.98f);
                _overlayRoot.style.justifyContent = Justify.Center;
                _overlayRoot.style.alignItems = Align.Center;
                _overlayRoot.style.display = DisplayStyle.None;
                _overlayRoot.style.opacity = 0f;

                _card = new VisualElement();
                _card.style.width = 620;
                _card.style.paddingLeft = 32;
                _card.style.paddingRight = 32;
                _card.style.paddingTop = 28;
                _card.style.paddingBottom = 28;
                _card.style.backgroundColor = new Color(0.11f, 0.09f, 0.14f, 0.96f);
                _card.style.borderTopWidth = 2;
                _card.style.borderBottomWidth = 2;
                _card.style.borderLeftWidth = 2;
                _card.style.borderRightWidth = 2;
                var borderCol = new Color(0.95f, 0.48f, 0.16f, 0.85f);
                _card.style.borderTopColor = borderCol;
                _card.style.borderBottomColor = borderCol;
                _card.style.borderLeftColor = borderCol;
                _card.style.borderRightColor = borderCol;
                _card.style.borderTopLeftRadius = 14;
                _card.style.borderTopRightRadius = 14;
                _card.style.borderBottomLeftRadius = 14;
                _card.style.borderBottomRightRadius = 14;
                _card.style.alignItems = Align.Center;

                var title = new Label("P.I.A.R.I.A. — TURNO NOTURNO");
                title.style.fontSize = 30;
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.color = new Color(0.98f, 0.78f, 0.28f);
                title.style.marginBottom = 6;
                _card.Add(title);

                _nightBannerLabel = new Label();
                _nightBannerLabel.style.fontSize = 18;
                _nightBannerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                _nightBannerLabel.style.color = new Color(1f, 0.56f, 0.24f);
                _nightBannerLabel.style.marginBottom = 20;
                RefreshNightBannerText();
                _card.Add(_nightBannerLabel);

                _spinnerIcon = new VisualElement();
                _spinnerIcon.style.width = 48;
                _spinnerIcon.style.height = 48;
                _spinnerIcon.style.borderTopWidth = 5;
                _spinnerIcon.style.borderBottomWidth = 5;
                _spinnerIcon.style.borderLeftWidth = 5;
                _spinnerIcon.style.borderRightWidth = 5;
                _spinnerIcon.style.borderTopColor = new Color(0.96f, 0.45f, 0.18f);
                _spinnerIcon.style.borderBottomColor = new Color(0.98f, 0.78f, 0.28f);
                _spinnerIcon.style.borderLeftColor = new Color(0.24f, 0.22f, 0.28f);
                _spinnerIcon.style.borderRightColor = new Color(0.24f, 0.22f, 0.28f);
                _spinnerIcon.style.borderTopLeftRadius = 24;
                _spinnerIcon.style.borderTopRightRadius = 24;
                _spinnerIcon.style.borderBottomLeftRadius = 24;
                _spinnerIcon.style.borderBottomRightRadius = 24;
                _spinnerIcon.style.marginBottom = 16;
                _card.Add(_spinnerIcon);

                _stageTagLabel = new Label("Preparando conexão");
                _stageTagLabel.style.fontSize = 12;
                _stageTagLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                _stageTagLabel.style.color = new Color(0.84f, 0.80f, 0.90f);
                _stageTagLabel.style.marginBottom = 4;
                _card.Add(_stageTagLabel);

                _statusLabel = new Label("Preparando turno...");
                _statusLabel.style.fontSize = 16;
                _statusLabel.style.color = Color.white;
                _statusLabel.style.marginBottom = 10;
                _card.Add(_statusLabel);

                var progressRow = new VisualElement();
                progressRow.style.flexDirection = FlexDirection.Row;
                progressRow.style.alignItems = Align.Center;
                progressRow.style.width = 520;
                progressRow.style.marginBottom = 18;

                _progressBar = new ProgressBar();
                _progressBar.style.flexGrow = 1;
                _progressBar.style.height = 18;
                _progressBar.style.marginRight = 10;
                progressRow.Add(_progressBar);

                _percentLabel = new Label("0%");
                _percentLabel.style.width = 52;
                _percentLabel.style.fontSize = 15;
                _percentLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                _percentLabel.style.color = new Color(0.98f, 0.78f, 0.28f);
                _percentLabel.style.unityTextAlign = TextAnchor.MiddleRight;
                progressRow.Add(_percentLabel);
                _card.Add(progressRow);

                _hintLabel = new Label(GameplayHints[0]);
                _hintLabel.style.fontSize = 12;
                _hintLabel.style.color = new Color(0.72f, 0.72f, 0.78f);
                _hintLabel.style.whiteSpace = WhiteSpace.Normal;
                _hintLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                _card.Add(_hintLabel);

                _cancelBtn = new Button(CancelCurrentOperation) { text = "Cancelar Conexão" };
                _cancelBtn.style.marginTop = 12;
                _cancelBtn.style.height = 32;
                _cancelBtn.style.paddingLeft = 14;
                _cancelBtn.style.paddingRight = 14;
                _cancelBtn.style.backgroundColor = new Color(0.32f, 0.28f, 0.36f);
                _cancelBtn.style.color = Color.white;
                _cancelBtn.style.display = DisplayStyle.None;
                _card.Add(_cancelBtn);

                _errorBox = new VisualElement { name = "LoadingErrorBox" };
                _errorBox.style.width = Length.Percent(100);
                _errorBox.style.marginTop = 14;
                _errorBox.style.paddingTop = 10;
                _errorBox.style.paddingBottom = 10;
                _errorBox.style.paddingLeft = 12;
                _errorBox.style.paddingRight = 12;
                _errorBox.style.backgroundColor = new Color(0.28f, 0.08f, 0.09f, 0.96f);
                _errorBox.style.borderTopLeftRadius = 8;
                _errorBox.style.borderTopRightRadius = 8;
                _errorBox.style.borderBottomLeftRadius = 8;
                _errorBox.style.borderBottomRightRadius = 8;
                _errorBox.style.alignItems = Align.Center;
                _errorBox.style.display = DisplayStyle.None;

                _errorReasonLabel = new Label();
                _errorReasonLabel.style.fontSize = 14;
                _errorReasonLabel.style.color = new Color(1f, 0.72f, 0.72f);
                _errorReasonLabel.style.whiteSpace = WhiteSpace.Normal;
                _errorReasonLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                _errorReasonLabel.style.marginBottom = 10;
                _errorBox.Add(_errorReasonLabel);

                var errBtnRow = new VisualElement();
                errBtnRow.style.flexDirection = FlexDirection.Row;
                errBtnRow.style.justifyContent = Justify.Center;

                _errorRetryBtn = new Button(RetryFailedOperation) { text = "Tentar Novamente" };
                _errorRetryBtn.style.height = 34;
                _errorRetryBtn.style.paddingLeft = 14;
                _errorRetryBtn.style.paddingRight = 14;
                _errorRetryBtn.style.marginRight = 8;
                _errorRetryBtn.style.backgroundColor = new Color(0.85f, 0.45f, 0.16f);
                _errorRetryBtn.style.color = Color.white;
                _errorRetryBtn.style.display = DisplayStyle.None;
                errBtnRow.Add(_errorRetryBtn);

                _errorMenuBtn = new Button(ReturnToLobbyFromError) { text = "Voltar ao Menu" };
                _errorMenuBtn.style.height = 34;
                _errorMenuBtn.style.paddingLeft = 14;
                _errorMenuBtn.style.paddingRight = 14;
                _errorMenuBtn.style.backgroundColor = new Color(0.72f, 0.22f, 0.22f);
                _errorMenuBtn.style.color = Color.white;
                errBtnRow.Add(_errorMenuBtn);

                _errorBox.Add(errBtnRow);
                _card.Add(_errorBox);

                _overlayRoot.Add(_card);

                var root = _loadingUiDocument.rootVisualElement;
                if (root != null)
                {
                    root.Add(_overlayRoot);
                }
        }

        private void SetProgressVisual(float progress01)
        {
            float pct = Mathf.Clamp01(progress01) * 100f;
            if (_progressBar != null) _progressBar.value = pct;
            if (_progressFillBar != null) _progressFillBar.style.width = Length.Percent(pct);
            if (_percentLabel != null) _percentLabel.text = $"{Mathf.RoundToInt(pct)}%";
        }

        private void StartLoadingAnimations()
        {
            if (_animationsRunning) return;
            _animationsRunning = true;
            _dotTimer = 0f;
            _dotStep = 1;
            if (_waveLetters.Count > 0)
            {
                ScaryParty.UI.Piaria.Core.UIToolkitTweenService.WaveLetters(_waveLetters, 9f, 0.06f, 0.45f);
            }
            if (_pizzaCorner != null)
            {
                ScaryParty.UI.Piaria.Core.UIToolkitTweenService.SpinAndPulseLoop(_pizzaCorner, 1.05f, 0.9f, 9f);
            }
        }

        private void StopLoadingAnimations()
        {
            if (!_animationsRunning) return;
            _animationsRunning = false;
            for (int i = 0; i < _waveLetters.Count; i++)
            {
                if (_waveLetters[i] != null)
                {
                    ScaryParty.UI.Piaria.Core.UIToolkitTweenService.KillTweens(_waveLetters[i]);
                    _waveLetters[i].style.translate = new StyleTranslate(new Translate(0f, 0f, 0f));
                }
            }
            if (_pizzaCorner != null)
            {
                ScaryParty.UI.Piaria.Core.UIToolkitTweenService.KillTweens(_pizzaCorner);
                _pizzaCorner.style.scale = new StyleScale(new Scale(Vector2.one));
                _pizzaCorner.style.rotate = new StyleRotate(new Rotate(Angle.Degrees(0f)));
            }
        }

        private void Update()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null &&
                (ShouldRehookInstance(_hookedSceneManager, nm.SceneManager) ||
                 ShouldRehookInstance(_hookedDisconnectManager, nm)))
            {
                TryHookNetworkSceneEvents();
            }

            if (!_isLoading) return;

            StartLoadingAnimations();

            // Suavização contínua (SmoothDamp) da barra e do percentual com dt limitado para não saltar após frames pesados
            if (_stateMachine != null)
            {
                float target = _stateMachine.ConfirmedProgress;
                _stateMachine.StepDisplayedProgress(Time.unscaledDeltaTime * 0.9f);
                if (_visualProgress01 < target)
                {
                    float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                    if (ScaryParty.UI.Piaria.Core.UIToolkitTweenService.ReduceMotion)
                    {
                        _visualProgress01 = target;
                    }
                    else
                    {
                        _visualProgress01 = Mathf.SmoothDamp(_visualProgress01, target, ref _visualProgressVelocity, 0.16f, Mathf.Infinity, dt);
                        if (target - _visualProgress01 < 0.002f)
                            _visualProgress01 = target;
                    }
                    SetProgressVisual(_visualProgress01);
                }
            }

            if (_spinnerIcon != null)
            {
                if (ScaryParty.UI.Piaria.Core.UIToolkitTweenService.ReduceMotion)
                {
                    _spinnerIcon.style.rotate = new Rotate(0f);
                    _spinnerIcon.style.opacity = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 3.5f);
                }
                else
                {
                    _spinnerIcon.style.opacity = 1f;
                    float rot = (Time.unscaledTime * 280f) % 360f;
                    _spinnerIcon.style.rotate = new Rotate(rot);
                }
            }

            // Ciclo de 1 a 3 pontinhos mantendo espaço reservado sem variar largura
            _dotTimer += Time.unscaledDeltaTime;
            if (_dotTimer >= 0.4f)
            {
                _dotTimer = 0f;
                _dotStep = (_dotStep % 3) + 1;
                for (int i = 0; i < _loadingDots.Length; i++)
                {
                    if (_loadingDots[i] != null)
                    {
                        bool visible = ScaryParty.UI.Piaria.Core.UIToolkitTweenService.ReduceMotion || i < _dotStep;
                        _loadingDots[i].style.opacity = visible ? 1f : 0.15f;
                    }
                }
            }

            _hintTimer += Time.unscaledDeltaTime;
            if (_hintTimer >= 3.5f)
            {
                _hintTimer = 0f;
                _hintIndex = (_hintIndex + 1) % GameplayHints.Length;
                if (_hintLabel != null)
                {
                    _hintLabel.text = GameplayHints[_hintIndex];
                }
            }
        }

        public static SceneReadinessProfile ResolveReadinessProfile(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return SceneReadinessProfile.MenuOrLobby;
            if (sceneName.IndexOf("Ready", StringComparison.OrdinalIgnoreCase) >= 0)
                return SceneReadinessProfile.ReadyRoom;
            if (IsGameplaySceneName(sceneName))
                return SceneReadinessProfile.Gameplay;
            return SceneReadinessProfile.MenuOrLobby;
        }

        public void ShowImmediateIntentOverlay(string statusText = "Preparando transição...")
        {
            BeginConnectionOperation(statusText, 15f);
        }

        /// <summary>
        /// Inicia uma operação de conexão/entrada com token, animação ativa e timeout garantido.
        /// </summary>
        public int BeginConnectionOperation(string statusText = "Conectando sessão...", float timeoutSeconds = 15f, Action retryAction = null)
        {
            var sm = EnsureStateMachine();
            if (sm.IsActiveState)
            {
                return sm.OperationToken;
            }

            StopAllCoroutines();
            EnsureUIOverlay();
            TryHookNetworkSceneEvents();

            _currentTransitionIsNetworked = true;
            _retryCallback = retryAction;
            if (!sm.TryBeginConnecting(null, SceneReadinessProfile.MenuOrLobby.ToString(), Time.realtimeSinceStartup, timeoutSeconds, out int opToken))
            {
                return opToken;
            }

            _expectedTargetScene = null;
            _trackedNetworkAsyncOp = null;
            _networkSceneLoadCompleted = false;

            if (_errorBox != null) _errorBox.style.display = DisplayStyle.None;
            if (_nightBannerLabel != null) _nightBannerLabel.style.display = DisplayStyle.None;
            if (_cancelBtn != null) _cancelBtn.style.display = DisplayStyle.Flex;
            if (_overlayRoot != null)
            {
                _overlayRoot.style.display = DisplayStyle.Flex;
                _overlayRoot.style.opacity = 1f;
            }

            _visualProgress01 = 0.02f;
            _visualProgressVelocity = 0f;
            SetProgressVisual(_visualProgress01);
            SetStage(LoadingStage.ConnectingSession, 0.02f, statusText);

            StartCoroutine(RoutineWaitForConnection(opToken, Mathf.Max(4f, timeoutSeconds)));
            return opToken;
        }

        public void CompleteConnection(int token)
        {
            var sm = EnsureStateMachine();
            if (!sm.IsTokenActive(token)) return;
            sm.AdvanceConfirmedProgress(token, LoadingOperationState.Connecting, 0.06f);
        }

        public void FailConnectionOperation(int token, string reason)
        {
            var sm = EnsureStateMachine();
            if (!sm.IsTokenActive(token)) return;
            AbortLoadingWithToken(token, string.IsNullOrWhiteSpace(reason) ? "Falha ao conectar à sessão." : reason);
        }

        public void CancelConnectionOperation(int token)
        {
            var sm = EnsureStateMachine();
            bool wasConnecting = sm.State == LoadingOperationState.Connecting;
            if (!sm.Cancel(token)) return;

            StopAllCoroutines();
            _waitingForPresentationHandoff = false;
            _holdingForPresentation = false;
            _presentationReleased = false;
            if (_cancelBtn != null) _cancelBtn.style.display = DisplayStyle.None;
            if (wasConnecting)
            {
                if (RoomDiscoveryService.Instance != null)
                {
                    RoomDiscoveryService.Instance.StopHosting();
                }
                UgsLobbySessionService.StopHostingLobby();
            }
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening &&
                (!NetworkManager.Singleton.IsServer || wasConnecting))
            {
                NetworkManager.Singleton.Shutdown();
            }
            _currentTransitionIsNetworked = false;
            HideOverlayNow();
        }

        public void CancelCurrentOperation()
        {
            if (_stateMachine == null) return;
            CancelConnectionOperation(_stateMachine.OperationToken);
        }

        private void RetryFailedOperation()
        {
            var retry = _retryCallback;
            if (retry == null)
            {
                ReturnToLobbyFromError();
                return;
            }

            var sm = EnsureStateMachine();
            sm.DismissErrorToIdle();
            HideOverlayNow();
            retry.Invoke();
        }

        private IEnumerator RoutineWaitForConnection(int opToken, float timeoutSeconds)
        {
            var sm = EnsureStateMachine();
            float startRealtime = Time.realtimeSinceStartup;
            while (sm.IsTokenActive(opToken) && sm.State == LoadingOperationState.Connecting)
            {
                float now = Time.realtimeSinceStartup;
                if (sm.IsTimedOut(now))
                {
                    AbortLoadingWithToken(opToken, "Tempo limite ao conectar à sala. Verifique o código, IP ou disponibilidade do host.");
                    yield break;
                }

                // Progresso visual suave entre 2% e 6% durante negociação de conexão
                float t = Mathf.Clamp01((now - startRealtime) / timeoutSeconds);
                sm.AdvanceConfirmedProgress(opToken, LoadingOperationState.Connecting, Mathf.Lerp(0.02f, 0.06f, t));
                yield return null;
            }
        }

        /// <summary>
        /// Transição offline local controlada pelo coordenador.
        /// </summary>
        public void TransitionToScene(string targetSceneName, Action onReadyCallback = null)
        {
            var sm = EnsureStateMachine();
            if (sm.IsActiveState && sm.State != LoadingOperationState.Connecting) return;

            StopAllCoroutines();
            _waitingForPresentationHandoff = false;
            _holdingForPresentation = false;
            _presentationReleased = false;
            _currentTransitionIsNetworked = false;
            _retryCallback = null;
            int token = sm.BeginSceneTransition(
                targetSceneName,
                ResolveReadinessProfile(targetSceneName).ToString(),
                Time.realtimeSinceStartup,
                Mathf.Max(10f, _loadingTimeoutSeconds));

            _expectedTargetScene = targetSceneName;
            _trackedNetworkAsyncOp = null;
            _networkSceneLoadCompleted = false;
            _visualProgress01 = 0f;
            _visualProgressVelocity = 0f;
            SetProgressVisual(0f);
            if (_cancelBtn != null) _cancelBtn.style.display = DisplayStyle.None;
            StartCoroutine(RoutineTransition(token, targetSceneName, false, startNetworkLoadAfterFirstFrame: false, onReadyCallback));
        }

        /// <summary>
        /// Transição autoritativa em rede (Host/Server). Apresenta o overlay no clique e aguarda 1 frame antes de chamar LoadScene.
        /// </summary>
        public SceneEventProgressStatus BeginNetworkedTransition(
            string targetSceneName,
            int nightNumber = 1,
            int revenueGoal = 150,
            Action onReadyCallback = null)
        {
            var sm = EnsureStateMachine();
            if (sm.IsActiveState && sm.State != LoadingOperationState.Connecting) return SceneEventProgressStatus.SceneEventInProgress;

            SetNightContext(nightNumber, revenueGoal);

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || nm.SceneManager == null)
            {
                Debug.LogError("[PizzeriaSceneLoadingCoordinator] BeginNetworkedTransition exige servidor ativo com NetworkSceneManager.");
                AbortLoading("Sessão de rede indisponível para iniciar a transição.");
                return SceneEventProgressStatus.InternalNetcodeError;
            }

            StopAllCoroutines();
            TryHookNetworkSceneEvents();
            _waitingForPresentationHandoff = false;
            _holdingForPresentation = false;
            _presentationReleased = false;
            _currentTransitionIsNetworked = true;
            _retryCallback = null;
            int token = sm.BeginSceneTransition(
                targetSceneName,
                ResolveReadinessProfile(targetSceneName).ToString(),
                Time.realtimeSinceStartup,
                Mathf.Max(10f, _loadingTimeoutSeconds));

            _expectedTargetScene = targetSceneName;
            _trackedNetworkAsyncOp = null;
            _networkSceneLoadCompleted = false;
            _visualProgress01 = 0f;
            _visualProgressVelocity = 0f;
            SetProgressVisual(0f);

            EnsureUIOverlay();
            if (_cancelBtn != null) _cancelBtn.style.display = DisplayStyle.None;
            if (_errorBox != null) _errorBox.style.display = DisplayStyle.None;
            if (_overlayRoot != null)
            {
                _overlayRoot.style.display = DisplayStyle.Flex;
                _overlayRoot.style.opacity = 1f;
            }
            SetStage(LoadingStage.ConnectingSession, 0.02f, "Conectando sessão...");
            StartCoroutine(RoutineTransition(token, targetSceneName, true, startNetworkLoadAfterFirstFrame: true, onReadyCallback));
            return SceneEventProgressStatus.Started;
        }

        public void AbortLoading(string reason = null)
        {
            var sm = EnsureStateMachine();
            AbortLoadingWithToken(sm.OperationToken, reason);
        }

        private void AbortLoadingWithToken(int token, string reason)
        {
            var sm = EnsureStateMachine();
            if (sm.IsActiveState && token != sm.OperationToken) return;

            StopAllCoroutines();
            _waitingForPresentationHandoff = false;
            _holdingForPresentation = false;
            _presentationReleased = false;
            if (_cancelBtn != null) _cancelBtn.style.display = DisplayStyle.None;

            if (!string.IsNullOrEmpty(reason))
            {
                sm.Fail(token, reason);
                Debug.LogWarning($"[PizzeriaSceneLoadingCoordinator] Carregamento interrompido: {reason}");
                EnsureUIOverlay();
                if (_overlayRoot != null && _errorBox != null && _errorReasonLabel != null)
                {
                    _errorReasonLabel.text = reason;
                    if (_errorRetryBtn != null)
                    {
                        _errorRetryBtn.style.display = _retryCallback != null ? DisplayStyle.Flex : DisplayStyle.None;
                    }
                    _errorBox.style.display = DisplayStyle.Flex;
                    _overlayRoot.style.opacity = 1f;
                    _overlayRoot.style.display = DisplayStyle.Flex;
                    AcquireInputLock();
                    ScaryParty.UI.Piaria.Core.UIFeedbackService.Instance?.PlayError();
                    return;
                }
            }

            sm.DismissErrorToIdle();
            HideOverlayNow();
        }

        private void HideOverlayNow()
        {
            _waitingForPresentationHandoff = false;
            _holdingForPresentation = false;
            _presentationReleased = false;
            _visualProgress01 = 0f;
            _visualProgressVelocity = 0f;
            StopLoadingAnimations();
            if (_cancelBtn != null) _cancelBtn.style.display = DisplayStyle.None;
            if (_errorBox != null) _errorBox.style.display = DisplayStyle.None;
            if (_overlayRoot != null)
            {
                _overlayRoot.style.opacity = 0f;
                _overlayRoot.style.display = DisplayStyle.None;
            }
            ReleaseInputLockIfHeld();
        }

        private void ReturnToLobbyFromError()
        {
            _retryCallback = null;
            _currentTransitionIsNetworked = false;
            _stateMachine?.DismissErrorToIdle();
            GameplayInputGate.Reset();
            if (RoomDiscoveryService.Instance != null)
                RoomDiscoveryService.Instance.StopHosting();
            UgsLobbySessionService.StopHostingLobby();
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            var activeScene = SceneManager.GetActiveScene().name;
            if (string.Equals(activeScene, "LobbyScene", StringComparison.OrdinalIgnoreCase))
            {
                HideOverlayNow();
            }
            else
            {
                TransitionToScene("LobbyScene");
            }
        }

        public static bool ShouldRehookInstance(object hookedInstance, object currentInstance)
        {
            return currentInstance != null && !ReferenceEquals(hookedInstance, currentInstance);
        }

        public static bool ShouldAbortOnClientDisconnect(
            LoadingOperationState state,
            bool isNetworkedTransition,
            bool isServer,
            ulong disconnectedClientId,
            ulong localClientId)
        {
            if (isServer) return false;
            if (disconnectedClientId != localClientId && disconnectedClientId != NetworkManager.ServerClientId)
                return false;

            if (state == LoadingOperationState.Connecting)
                return true;

            return isNetworkedTransition &&
                   (state == LoadingOperationState.LoadingScene || state == LoadingOperationState.WaitingReadiness);
        }

        private void TryHookNetworkSceneEvents()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            var sceneMgr = nm.SceneManager;
            if (ShouldRehookInstance(_hookedSceneManager, sceneMgr))
            {
                if (_hookedSceneManager != null)
                {
                    try { _hookedSceneManager.OnSceneEvent -= OnNetworkSceneEvent; } catch { }
                }
                _hookedSceneManager = sceneMgr;
                if (_hookedSceneManager != null)
                {
                    _hookedSceneManager.OnSceneEvent += OnNetworkSceneEvent;
                }
            }

            if (ShouldRehookInstance(_hookedDisconnectManager, nm))
            {
                if (_hookedDisconnectManager != null)
                {
                    try { _hookedDisconnectManager.OnClientDisconnectCallback -= OnLocalClientDisconnected; } catch { }
                }
                _hookedDisconnectManager = nm;
                _hookedDisconnectManager.OnClientDisconnectCallback += OnLocalClientDisconnected;
            }
        }

        private void UnhookNetworkSceneEvents()
        {
            if (_hookedSceneManager != null)
            {
                try { _hookedSceneManager.OnSceneEvent -= OnNetworkSceneEvent; } catch { }
                _hookedSceneManager = null;
            }

            if (_hookedDisconnectManager != null)
            {
                try { _hookedDisconnectManager.OnClientDisconnectCallback -= OnLocalClientDisconnected; } catch { }
                _hookedDisconnectManager = null;
            }
        }

        private void OnLocalClientDisconnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            bool isServer = nm != null && nm.IsServer;
            ulong localId = nm != null ? nm.LocalClientId : 0UL;
            if (!ShouldAbortOnClientDisconnect(OperationState, _currentTransitionIsNetworked, isServer, clientId, localId))
                return;

            AbortLoading("Conexão recusada ou encerrada pelo host.");
        }

        private void OnNetworkSceneEvent(SceneEvent sceneEvent)
        {
            switch (sceneEvent.SceneEventType)
            {
                case SceneEventType.Load:
                case SceneEventType.Synchronize:
                    if (string.IsNullOrWhiteSpace(sceneEvent.SceneName)) return;
                    _currentTransitionIsNetworked = true;
                    _expectedTargetScene = sceneEvent.SceneName;
                    _trackedNetworkAsyncOp = sceneEvent.AsyncOperation;
                    _networkSceneLoadCompleted = sceneEvent.SceneEventType == SceneEventType.Synchronize;
                    if (!_isLoading || _awaitingConnection)
                    {
                        StopAllCoroutines();
                        _waitingForPresentationHandoff = false;
                        _holdingForPresentation = false;
                        _presentationReleased = false;
                        var sm = EnsureStateMachine();
                        _retryCallback = null;
                        int token = _awaitingConnection && sm.PromoteConnectingToSceneLoad(
                            sm.OperationToken,
                            sceneEvent.SceneName,
                            ResolveReadinessProfile(sceneEvent.SceneName).ToString(),
                            Time.realtimeSinceStartup,
                            Mathf.Max(10f, _loadingTimeoutSeconds))
                            ? sm.OperationToken
                            : sm.BeginSceneTransition(
                                sceneEvent.SceneName,
                                ResolveReadinessProfile(sceneEvent.SceneName).ToString(),
                                Time.realtimeSinceStartup,
                                Mathf.Max(10f, _loadingTimeoutSeconds));
                        if (_cancelBtn != null) _cancelBtn.style.display = DisplayStyle.None;
                        StartCoroutine(RoutineTransition(token, sceneEvent.SceneName, true, startNetworkLoadAfterFirstFrame: false, null));
                    }
                    break;

                case SceneEventType.LoadComplete:
                case SceneEventType.LoadEventCompleted:
                case SceneEventType.SynchronizeComplete:
                    if (string.IsNullOrEmpty(_expectedTargetScene) ||
                        string.Equals(sceneEvent.SceneName, _expectedTargetScene, StringComparison.OrdinalIgnoreCase))
                    {
                        _networkSceneLoadCompleted = true;
                    }
                    break;
            }
        }

        private static bool IsGameplaySceneName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return false;
            string configured = VisualStyleRuntimeConfig.GetGameplaySceneName();
            if (string.Equals(sceneName, configured, StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(sceneName, VisualStyleRuntimeConfig.CanonicalGameplaySceneName, StringComparison.OrdinalIgnoreCase)) return true;
            return sceneName.IndexOf("GameScene", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private IEnumerator RoutineTransition(int myTransitionId, string targetSceneName, bool networked, bool startNetworkLoadAfterFirstFrame, Action onReadyCallback)
        {
            var sm = EnsureStateMachine();
            AcquireInputLock();
            EnsureUIOverlay();
            RefreshNightBannerText();

            var readiness = ResolveReadinessProfile(targetSceneName);
            float startRealtime = Time.realtimeSinceStartup;
            float maxTimeout = Mathf.Max(10f, _loadingTimeoutSeconds);

            // 1. Fade-in imediato do overlay sobre a cena atual antes de qualquer carga pesada
            if (_errorBox != null) _errorBox.style.display = DisplayStyle.None;
            _overlayRoot.style.display = DisplayStyle.Flex;
            if (_nightBannerLabel != null)
                _nightBannerLabel.style.display = readiness == SceneReadinessProfile.Gameplay ? DisplayStyle.Flex : DisplayStyle.None;

            float elapsed = 0f;
            float fadeDur = ScaryParty.UI.Piaria.Core.UIToolkitTweenService.ReduceMotion ? 0.08f : _fadeDuration;
            while (elapsed < fadeDur)
            {
                if (myTransitionId != _transitionId) yield break;
                elapsed += Time.unscaledDeltaTime;
                _overlayRoot.style.opacity = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeDur));
                yield return null;
            }
            _overlayRoot.style.opacity = 1f;
            yield return null; // Garante ao menos 1 frame completo apresentado antes de iniciar trabalho pesado
            if (myTransitionId != _transitionId) yield break;

            // Etapa 1: Conexão (5%)
            SetStage(LoadingStage.ConnectingSession, 0.05f, networked
                ? "Conectando sessão e validando jogadores..."
                : "Preparando transição de cena...");

            if (startNetworkLoadAfterFirstFrame)
            {
                var nm = NetworkManager.Singleton;
                if (nm == null || !nm.IsServer || nm.SceneManager == null)
                {
                    AbortLoading("Servidor de rede não está disponível para trocar de cena.");
                    yield break;
                }
                var netStatus = nm.SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
                if (netStatus != SceneEventProgressStatus.Started)
                {
                    AbortLoading($"Falha ao iniciar carregamento em rede ({netStatus}).");
                    yield break;
                }
            }
            else if (networked)
            {
                float connWait = 0f;
                while (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening && connWait < 3f)
                {
                    if (myTransitionId != _transitionId) yield break;
                    connWait += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            // Etapa 2: Cena/Recursos (5% -> 40%)
            SetStage(LoadingStage.LoadingScene, 0.08f, "Carregando cenário e recursos...");
            if (!networked)
            {
                AsyncOperation op = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
                if (op != null)
                {
                    while (!op.isDone && !HasTimedOut(startRealtime, maxTimeout))
                    {
                        if (myTransitionId != _transitionId) yield break;
                        float norm = op.progress < 0.9f ? Mathf.Clamp01(op.progress / 0.9f) : 1f;
                        SetStage(LoadingStage.LoadingScene, Mathf.Lerp(0.08f, 0.40f, norm), "Carregando cenário e recursos...");
                        yield return null;
                    }
                }
            }
            else
            {
                while (!HasTimedOut(startRealtime, maxTimeout))
                {
                    if (myTransitionId != _transitionId) yield break;
                    float asyncP = _trackedNetworkAsyncOp != null ? Mathf.Clamp01(_trackedNetworkAsyncOp.progress / 0.9f) : 0.4f;
                    SetStage(LoadingStage.LoadingScene, Mathf.Lerp(0.08f, 0.40f, asyncP), "Sincronizando cena com a equipe...");

                    var activeScene = SceneManager.GetActiveScene();
                    bool activeMatches = IsTargetActiveScene(activeScene.name, targetSceneName);
                    bool asyncDone = _trackedNetworkAsyncOp == null || _trackedNetworkAsyncOp.isDone;
                    if (activeMatches && (_networkSceneLoadCompleted || asyncDone))
                    {
                        break;
                    }
                    yield return null;
                }
            }

            if (myTransitionId != _transitionId) yield break;
            if (HasTimedOut(startRealtime, maxTimeout))
            {
                AbortLoading($"Tempo limite ao carregar a cena '{targetSceneName}'.");
                yield break;
            }

            if (readiness == SceneReadinessProfile.Gameplay)
            {
                // Etapa 3: Cidade/Pátio (40% -> 70%)
                SetStage(LoadingStage.GeneratingCity, 0.45f, "Construindo cidade e pátio...");
                CityGenerator cityGen = FindFirstObjectByType<CityGenerator>();
                while (cityGen == null && !HasTimedOut(startRealtime, maxTimeout))
                {
                    if (myTransitionId != _transitionId) yield break;
                    cityGen = FindFirstObjectByType<CityGenerator>();
                    yield return null;
                }

                if (cityGen == null)
                {
                    AbortLoading($"Dependência obrigatória ausente em '{targetSceneName}': CityGenerator.");
                    yield break;
                }

                while ((cityGen.CityData == null || !cityGen.ClosuresSynchronized) && !HasTimedOut(startRealtime, maxTimeout))
                {
                    if (myTransitionId != _transitionId) yield break;
                    SetStage(LoadingStage.GeneratingCity, 0.60f, "Finalizando quarteirões, muralhas e vias...");
                    yield return null;
                }

                if (HasTimedOut(startRealtime, maxTimeout))
                {
                    AbortLoading($"Tempo limite ao gerar cidade em '{targetSceneName}'.");
                    yield break;
                }

                // Etapa 4: Navegação (70% -> 85%)
                SetStage(LoadingStage.BakingNavigation, 0.75f, "Preparando rotas e navegação...");
                while ((NavigationWorldManager.Instance == null || !NavigationWorldManager.Instance.IsReady) && !HasTimedOut(startRealtime, maxTimeout))
                {
                    if (myTransitionId != _transitionId) yield break;
                    yield return null;
                }

                if (NavigationWorldManager.Instance == null || !NavigationWorldManager.Instance.IsReady)
                {
                    AbortLoading($"Sistema de navegação (NavMesh) não ficou pronto em '{targetSceneName}'.");
                    yield break;
                }

                // Etapa 5: Pizzaria e Replicação (85% -> 95%)
                SetStage(LoadingStage.SpawningPizzeria, 0.88f, "Abrindo pizzaria e sincronizando dados...");
                while (!IsPizzeriaReady(networked) && !HasTimedOut(startRealtime, maxTimeout))
                {
                    if (myTransitionId != _transitionId) yield break;
                    yield return null;
                }
                RefreshNightBannerText();

                // Etapa 6: Jogador, Câmera, HUD e Iluminação (95% -> 100%)
                SetStage(LoadingStage.SpawningPlayers, 0.95f, "Posicionando equipe, HUD e iluminação...");
                var contract = BuildReadinessContract(targetSceneName, readiness, networked, cityGen);
                while (!contract.Evaluate(null, null) && !HasTimedOut(startRealtime, maxTimeout))
                {
                    if (myTransitionId != _transitionId) yield break;
                    yield return null;
                }

                if (HasTimedOut(startRealtime, maxTimeout) || !contract.Evaluate(null, null))
                {
                    AbortLoadingWithToken(myTransitionId, contract.BuildDiagnosticSummary());
                    yield break;
                }
            }
            else if (readiness == SceneReadinessProfile.ReadyRoom)
            {
                SetStage(LoadingStage.SpawningPlayers, 0.92f, "Sincronizando sala de espera...");
                var contract = BuildReadinessContract(targetSceneName, readiness, networked, null);
                while (!contract.Evaluate(null, null) && !HasTimedOut(startRealtime, maxTimeout))
                {
                    if (myTransitionId != _transitionId) yield break;
                    yield return null;
                }

                if (HasTimedOut(startRealtime, maxTimeout) || !contract.Evaluate(null, null))
                {
                    AbortLoadingWithToken(myTransitionId, contract.BuildDiagnosticSummary());
                    yield break;
                }
            }
            else
            {
                // Menu / Lobby: exige câmera e UIDocument da cena prontos
                SetStage(LoadingStage.SpawningPlayers, 0.92f, "Finalizando interface...");
                var contract = BuildReadinessContract(targetSceneName, readiness, networked, null);
                while (!contract.Evaluate(null, null) && !HasTimedOut(startRealtime, maxTimeout))
                {
                    if (myTransitionId != _transitionId) yield break;
                    yield return null;
                }

                if (HasTimedOut(startRealtime, maxTimeout) || !contract.Evaluate(null, null))
                {
                    AbortLoadingWithToken(myTransitionId, contract.BuildDiagnosticSummary());
                    yield break;
                }
            }

            // Aguarda 1 frame para garantir iluminação/câmera/HUD renderizados
            yield return null;
            if (myTransitionId != _transitionId) yield break;

            SetStage(LoadingStage.Ready, readiness == SceneReadinessProfile.Gameplay ? 0.99f : 1.0f,
                readiness == SceneReadinessProfile.Gameplay ? "Preparando apresentação..." : "Tudo pronto!");
            sm.MarkReady(myTransitionId);

            if (readiness == SceneReadinessProfile.Gameplay)
            {
                // Mantém overlay 100% opaco até a apresentação (cutscene/briefing) concluir preload e liberar explicitamente
                _waitingForPresentationHandoff = true;
                HoldForPresentation(myTransitionId);
                Debug.Log($"[LoadingCoordinator] Gameplay pronto — aguardando ReleaseToCutscene (token={myTransitionId}).");
                onReadyCallback?.Invoke();

                float holdStart = Time.realtimeSinceStartup;
                while (_holdingForPresentation && !_presentationReleased)
                {
                    if (myTransitionId != _transitionId)
                    {
                        _waitingForPresentationHandoff = false;
                        yield break;
                    }
                    if (Time.realtimeSinceStartup - holdStart >= 15f)
                    {
                        Debug.LogWarning("[LoadingCoordinator] Timeout aguardando ReleaseToCutscene — liberando overlay.");
                        _holdingForPresentation = false;
                        break;
                    }
                    yield return null;
                }
                _waitingForPresentationHandoff = false;
            }
            else
            {
                SetProgressVisual(1.0f);
                ScaryParty.UI.Piaria.Core.UIFeedbackService.Instance?.PlaySuccess();
                yield return new WaitForSecondsRealtime(0.18f);
                if (myTransitionId != _transitionId) yield break;
                onReadyCallback?.Invoke();
            }

            if (!ScaryParty.UI.Piaria.Core.UIToolkitTweenService.ReduceMotion && _visualProgress01 < 0.995f)
            {
                float finishWait = 0f;
                while (_visualProgress01 < 0.995f && finishWait < 0.35f)
                {
                    if (myTransitionId != _transitionId) yield break;
                    finishWait += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            _visualProgress01 = 1f;
            _visualProgressVelocity = 0f;
            SetProgressVisual(1.0f);
            ScaryParty.UI.Piaria.Core.UIFeedbackService.Instance?.PlaySuccess();

            // Fade-out e liberação segura do input
            sm.BeginFadeOut(myTransitionId);
            elapsed = 0f;
            while (elapsed < fadeDur)
            {
                if (myTransitionId != _transitionId) yield break;
                elapsed += Time.unscaledDeltaTime;
                _overlayRoot.style.opacity = 1f - Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeDur));
                yield return null;
            }

            StopLoadingAnimations();
            _overlayRoot.style.opacity = 0f;
            _overlayRoot.style.display = DisplayStyle.None;
            _stageTimings.EndActiveStage(Time.realtimeSinceStartup);
            Debug.Log($"[LoadingCoordinator] Transição para '{targetSceneName}' concluída ({_stageTimings.BuildSummary()}).");
            sm.Complete(myTransitionId);
        }

        private SceneReadinessContract BuildReadinessContract(string targetSceneName, SceneReadinessProfile profile, bool networked, CityGenerator cityGen)
        {
            var contract = new SceneReadinessContract(targetSceneName)
                .Add(new DelegateReadinessRequirement(
                    "ActiveScene",
                    true,
                    () => IsTargetActiveScene(SceneManager.GetActiveScene().name, targetSceneName),
                    $"Cena ativa difere de '{targetSceneName}'"))
                .Add(new DelegateReadinessRequirement(
                    "LocalCamera",
                    true,
                    () => Camera.main != null,
                    "Camera.main ausente"))
                .Add(new DelegateReadinessRequirement(
                    "MainUI",
                    true,
                    HasActiveSceneUIDocument,
                    "UIDocument principal inativo/vazio"));

            if (profile == SceneReadinessProfile.ReadyRoom && networked)
            {
                contract.Add(new DelegateReadinessRequirement(
                    "NetworkListening",
                    true,
                    () => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening,
                    "NetworkManager não está escutando"));
            }
            else if (profile == SceneReadinessProfile.Gameplay)
            {
                contract
                    .Add(new DelegateReadinessRequirement("CityGenerator", true, () => cityGen != null && cityGen.CityData != null, "CityData não gerado"))
                    .Add(new DelegateReadinessRequirement("ClosuresSynchronized", true, () => cityGen != null && cityGen.ClosuresSynchronized, "Fechamentos urbanos pendentes"))
                    .Add(new DelegateReadinessRequirement("NavigationWorldManager", true, () => NavigationWorldManager.Instance != null && NavigationWorldManager.Instance.IsReady, "NavMesh não pronto"))
                    .Add(new DelegateReadinessRequirement("PizzeriaDomain", true, () => IsPizzeriaReady(networked), "PizzeriaRoot/Snapshot não pronto"))
                    .Add(new DelegateReadinessRequirement("LocalPlayer", true, IsLocalPlayerSpawned, "Player local não spawnado"))
                    .Add(new DelegateReadinessRequirement(
                        "LightingReadyGate",
                        LightingReadyGate.Active != null,
                        () => LightingReadyGate.Active == null || LightingReadyGate.Active.IsReady,
                        "Iluminação não pronta"));
            }

            return contract;
        }

        private bool HasTimedOut(float startRealtime, float maxTimeout)
        {
            return (Time.realtimeSinceStartup - startRealtime) >= maxTimeout;
        }

        private static bool IsTargetActiveScene(string activeSceneName, string targetSceneName)
        {
            if (string.IsNullOrEmpty(targetSceneName)) return true;
            if (string.Equals(activeSceneName, targetSceneName, StringComparison.OrdinalIgnoreCase)) return true;
            return IsGameplaySceneName(activeSceneName) && IsGameplaySceneName(targetSceneName);
        }

        private bool HasActiveSceneUIDocument()
        {
            var docs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            for (int i = 0; i < docs.Length; i++)
            {
                var doc = docs[i];
                if (doc == null || doc == _loadingUiDocument) continue;
                if (doc.isActiveAndEnabled && doc.rootVisualElement != null && doc.rootVisualElement.childCount > 0)
                    return true;
            }
            return false;
        }

        private static bool IsPizzeriaReady(bool networked)
        {
            var root = PizzeriaRoot.Instance;
            if (root == null) return false;
            if (root.Catalog == null) return false;
            if (!networked || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer))
            {
                return root.DomainState != null;
            }

            var net = ScaryParty.Pizzeria.Network.PizzeriaNetworkState.Instance;
            return net != null && net.IsSpawned && net.RunState.Value.Revision > 0;
        }

        private static bool IsLocalPlayerSpawned()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
            {
                return nm.LocalClient != null && nm.LocalClient.PlayerObject != null;
            }

            return FindFirstObjectByType<global::PlayerInteraction>() != null;
        }

        private void AcquireInputLock()
        {
            if (_hasInputLock) return;
            GameplayInputGate.PushLock();
            _hasInputLock = true;
        }

        private void ReleaseInputLockIfHeld()
        {
            if (!_hasInputLock) return;
            GameplayInputGate.PopLock();
            _hasInputLock = false;
        }

        private void RefreshNightBannerText()
        {
            if (_nightBannerLabel != null)
            {
                _nightBannerLabel.text = $"NOITE {_displayNightNumber} — META: R$ {_displayNightGoal}";
            }
        }

        private void SetStage(LoadingStage stage, float progress01, string statusText)
        {
            _currentStage = stage;
            if (stage == LoadingStage.ConnectingSession) _stageTimings.Reset();
            _stageTimings.BeginStage(stage.ToString(), Time.realtimeSinceStartup);
            var sm = EnsureStateMachine();
            var opState = stage switch
            {
                LoadingStage.ConnectingSession => LoadingOperationState.Connecting,
                LoadingStage.LoadingScene => LoadingOperationState.LoadingScene,
                LoadingStage.GeneratingCity => LoadingOperationState.WaitingReadiness,
                LoadingStage.BakingNavigation => LoadingOperationState.WaitingReadiness,
                LoadingStage.SpawningPizzeria => LoadingOperationState.WaitingReadiness,
                LoadingStage.SpawningPlayers => LoadingOperationState.WaitingReadiness,
                LoadingStage.Ready => LoadingOperationState.Ready,
                _ => LoadingOperationState.LoadingScene
            };
            sm.AdvanceConfirmedProgress(sm.OperationToken, opState, progress01);
            if (_stageTagLabel != null)
            {
                _stageTagLabel.text = stage switch
                {
                    LoadingStage.ConnectingSession => "Conexão",
                    LoadingStage.LoadingScene => "Cenário",
                    LoadingStage.GeneratingCity => "Cidade",
                    LoadingStage.BakingNavigation => "Navegação",
                    LoadingStage.SpawningPizzeria => "Pizzaria",
                    LoadingStage.SpawningPlayers => "Equipe",
                    LoadingStage.Ready => "Concluído",
                    _ => "Carregando"
                };
            }
            if (_statusLabel != null)
            {
                _statusLabel.text = statusText;
            }
        }
    }
}
