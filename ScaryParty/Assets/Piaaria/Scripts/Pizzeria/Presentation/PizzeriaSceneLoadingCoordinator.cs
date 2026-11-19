using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Netcode;
using ScaryParty.Navigation.Core;
using ScaryParty.Pizzeria.Composition;
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
        [SerializeField] private float _fadeDuration = 0.22f;
        [SerializeField] private float _loadingTimeoutSeconds = 40f;

        private UIDocument _loadingUiDocument;
        private PanelSettings _runtimePanelSettings;
        private VisualElement _overlayRoot;
        private VisualElement _card;
        private ProgressBar _progressBar;
        private Label _percentLabel;
        private Label _nightBannerLabel;
        private Label _statusLabel;
        private Label _stageTagLabel;
        private Label _hintLabel;
        private VisualElement _spinnerIcon;
        private VisualElement _errorBox;
        private Label _errorReasonLabel;
        private Button _errorMenuBtn;

        private bool _isLoading;
        private bool _hasInputLock;
        private bool _hookedNetworkEvents;
        private float _currentProgress;
        private float _displayedProgress;
        private float _hintTimer;
        private int _hintIndex;
        private int _transitionId;

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
            ReleaseInputLockIfHeld();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnhookNetworkSceneEvents();
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
        }

        private void Update()
        {
            if (!_hookedNetworkEvents)
            {
                TryHookNetworkSceneEvents();
            }

            if (!_isLoading) return;

            // Suavização monotônica do progresso exibido (sempre <= progresso confirmado)
            if (_displayedProgress < _currentProgress)
            {
                _displayedProgress = Mathf.MoveTowards(_displayedProgress, _currentProgress, Time.unscaledDeltaTime * 0.9f);
                if (_progressBar != null) _progressBar.value = Mathf.Clamp01(_displayedProgress) * 100f;
                if (_percentLabel != null) _percentLabel.text = $"{Mathf.RoundToInt(Mathf.Clamp01(_displayedProgress) * 100f)}%";
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
            EnsureUIOverlay();
            AcquireInputLock();
            if (_errorBox != null) _errorBox.style.display = DisplayStyle.None;
            if (_overlayRoot != null)
            {
                _overlayRoot.style.display = DisplayStyle.Flex;
                _overlayRoot.style.opacity = 1f;
            }
            _currentProgress = 0.02f;
            _displayedProgress = 0.02f;
            if (_progressBar != null) _progressBar.value = 2f;
            if (_percentLabel != null) _percentLabel.text = "2%";
            if (_statusLabel != null) _statusLabel.text = statusText;
            if (_stageTagLabel != null) _stageTagLabel.text = "Iniciando";
        }

        /// <summary>
        /// Transição offline local controlada pelo coordenador.
        /// </summary>
        public void TransitionToScene(string targetSceneName, Action onReadyCallback = null)
        {
            if (_isLoading) return;
            _transitionId++;
            _expectedTargetScene = targetSceneName;
            _trackedNetworkAsyncOp = null;
            _networkSceneLoadCompleted = false;
            _currentProgress = 0f;
            _displayedProgress = 0f;
            StartCoroutine(RoutineTransition(_transitionId, targetSceneName, false, startNetworkLoadAfterFirstFrame: false, onReadyCallback));
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
            if (_isLoading) return SceneEventProgressStatus.SceneEventInProgress;

            SetNightContext(nightNumber, revenueGoal);

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || nm.SceneManager == null)
            {
                Debug.LogError("[PizzeriaSceneLoadingCoordinator] BeginNetworkedTransition exige servidor ativo com NetworkSceneManager.");
                AbortLoading("Sessão de rede indisponível para iniciar a transição.");
                return SceneEventProgressStatus.InternalNetcodeError;
            }

            TryHookNetworkSceneEvents();
            _transitionId++;
            _expectedTargetScene = targetSceneName;
            _trackedNetworkAsyncOp = null;
            _networkSceneLoadCompleted = false;
            _currentProgress = 0f;
            _displayedProgress = 0f;

            ShowImmediateIntentOverlay("Conectando sessão...");
            StartCoroutine(RoutineTransition(_transitionId, targetSceneName, true, startNetworkLoadAfterFirstFrame: true, onReadyCallback));
            return SceneEventProgressStatus.Started;
        }

        public void AbortLoading(string reason = null)
        {
            _transitionId++;
            StopAllCoroutines();
            _isLoading = false;
            if (!string.IsNullOrEmpty(reason))
            {
                Debug.LogWarning($"[PizzeriaSceneLoadingCoordinator] Carregamento interrompido: {reason}");
                EnsureUIOverlay();
                if (_overlayRoot != null && _errorBox != null && _errorReasonLabel != null)
                {
                    _errorReasonLabel.text = reason;
                    _errorBox.style.display = DisplayStyle.Flex;
                    _overlayRoot.style.opacity = 1f;
                    _overlayRoot.style.display = DisplayStyle.Flex;
                    AcquireInputLock();
                    ScaryParty.UI.Piaria.Core.UIFeedbackService.Instance?.PlayError();
                    return;
                }
            }

            HideOverlayNow();
        }

        private void HideOverlayNow()
        {
            if (_errorBox != null) _errorBox.style.display = DisplayStyle.None;
            if (_overlayRoot != null)
            {
                _overlayRoot.style.opacity = 0f;
                _overlayRoot.style.display = DisplayStyle.None;
            }
            ReleaseInputLockIfHeld();
            _isLoading = false;
        }

        private void ReturnToLobbyFromError()
        {
            _isLoading = false;
            GameplayInputGate.Reset();
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
            TransitionToScene("LobbyScene");
        }

        private void TryHookNetworkSceneEvents()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SceneManager == null || _hookedNetworkEvents) return;

            nm.SceneManager.OnSceneEvent += OnNetworkSceneEvent;
            _hookedNetworkEvents = true;
        }

        private void UnhookNetworkSceneEvents()
        {
            var nm = NetworkManager.Singleton;
            if (_hookedNetworkEvents && nm != null && nm.SceneManager != null)
            {
                nm.SceneManager.OnSceneEvent -= OnNetworkSceneEvent;
            }
            _hookedNetworkEvents = false;
        }

        private void OnNetworkSceneEvent(SceneEvent sceneEvent)
        {
            switch (sceneEvent.SceneEventType)
            {
                case SceneEventType.Load:
                    if (string.IsNullOrWhiteSpace(sceneEvent.SceneName)) return;
                    _expectedTargetScene = sceneEvent.SceneName;
                    _trackedNetworkAsyncOp = sceneEvent.AsyncOperation;
                    _networkSceneLoadCompleted = false;
                    if (!_isLoading)
                    {
                        _transitionId++;
                        _currentProgress = 0f;
                        _displayedProgress = 0f;
                        StartCoroutine(RoutineTransition(_transitionId, sceneEvent.SceneName, true, startNetworkLoadAfterFirstFrame: false, null));
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
            _isLoading = true;
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
                float cityWait = 0f;
                CityGenerator cityGen = FindFirstObjectByType<CityGenerator>();
                while (cityGen == null && cityWait < 2.5f && !HasTimedOut(startRealtime, maxTimeout))
                {
                    if (myTransitionId != _transitionId) yield break;
                    cityWait += Time.unscaledDeltaTime;
                    cityGen = FindFirstObjectByType<CityGenerator>();
                    yield return null;
                }

                while (cityGen != null && (cityGen.CityData == null || !cityGen.ClosuresSynchronized) && !HasTimedOut(startRealtime, maxTimeout))
                {
                    if (myTransitionId != _transitionId) yield break;
                    SetStage(LoadingStage.GeneratingCity, 0.60f, "Finalizando quarteirões, muralhas e vias...");
                    yield return null;
                }

                // Etapa 4: Navegação (70% -> 85%)
                SetStage(LoadingStage.BakingNavigation, 0.75f, "Preparando rotas e navegação...");
                while (NavigationWorldManager.Instance != null && !NavigationWorldManager.Instance.IsReady && !HasTimedOut(startRealtime, maxTimeout))
                {
                    if (myTransitionId != _transitionId) yield break;
                    yield return null;
                }

                // Etapa 5: Pizzaria e Replicação (85% -> 95%)
                SetStage(LoadingStage.SpawningPizzeria, 0.88f, "Abrindo pizzaria e sincronizando dados...");
                while (!IsPizzeriaReady(networked) && !HasTimedOut(startRealtime, maxTimeout))
                {
                    if (myTransitionId != _transitionId) yield break;
                    yield return null;
                }
                RefreshNightBannerText();

                // Etapa 6: Jogador, Câmera e Iluminação (95% -> 100%)
                SetStage(LoadingStage.SpawningPlayers, 0.95f, "Posicionando equipe e iluminação...");
                while ((!IsLocalPlayerSpawned() || Camera.main == null) && !HasTimedOut(startRealtime, maxTimeout))
                {
                    if (myTransitionId != _transitionId) yield break;
                    yield return null;
                }

                if (HasTimedOut(startRealtime, maxTimeout) || !IsPizzeriaReady(networked) || !IsLocalPlayerSpawned())
                {
                    AbortLoading($"Tempo limite ({maxTimeout:F0}s) ao aguardar prontidão de '{targetSceneName}'. Retorne ao menu.");
                    yield break;
                }
            }
            else
            {
                // Menu / Lobby / ReadyRoom: valida câmera e UI pronta
                SetStage(LoadingStage.SpawningPlayers, 0.92f, "Finalizando interface...");
                while (Camera.main == null && !HasTimedOut(startRealtime, maxTimeout))
                {
                    if (myTransitionId != _transitionId) yield break;
                    yield return null;
                }
            }

            // Aguarda 1 frame para garantir iluminação/câmera/HUD renderizados
            yield return null;
            if (myTransitionId != _transitionId) yield break;

            SetStage(LoadingStage.Ready, 1.0f, "Tudo pronto!");
            _displayedProgress = 1.0f;
            if (_progressBar != null) _progressBar.value = 100f;
            if (_percentLabel != null) _percentLabel.text = "100%";
            ScaryParty.UI.Piaria.Core.UIFeedbackService.Instance?.PlaySuccess();

            yield return new WaitForSecondsRealtime(0.18f);
            if (myTransitionId != _transitionId) yield break;
            onReadyCallback?.Invoke();

            // Fade-out e liberação segura do input
            elapsed = 0f;
            while (elapsed < fadeDur)
            {
                if (myTransitionId != _transitionId) yield break;
                elapsed += Time.unscaledDeltaTime;
                _overlayRoot.style.opacity = 1f - Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeDur));
                yield return null;
            }

            _overlayRoot.style.opacity = 0f;
            _overlayRoot.style.display = DisplayStyle.None;
            ReleaseInputLockIfHeld();
            _isLoading = false;
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
            _currentProgress = Mathf.Max(_currentProgress, Mathf.Clamp01(progress01));
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
