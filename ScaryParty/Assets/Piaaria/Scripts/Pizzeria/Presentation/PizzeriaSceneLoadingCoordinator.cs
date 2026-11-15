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

        [Header("Configuração de Transição")]
        [SerializeField] private float _fadeDuration = 0.35f;
        [SerializeField] private float _loadingTimeoutSeconds = 40f;

        private UIDocument _loadingUiDocument;
        private VisualElement _overlayRoot;
        private ProgressBar _progressBar;
        private Label _nightBannerLabel;
        private Label _statusLabel;
        private Label _stageTagLabel;
        private Label _hintLabel;
        private VisualElement _spinnerIcon;

        private bool _isLoading;
        private bool _hasInputLock;
        private bool _hookedNetworkEvents;
        private float _currentProgress;
        private float _hintTimer;
        private int _hintIndex;

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

                var card = new VisualElement();
                card.style.width = 600;
                card.style.paddingLeft = 32;
                card.style.paddingRight = 32;
                card.style.paddingTop = 28;
                card.style.paddingBottom = 28;
                card.style.backgroundColor = new Color(0.11f, 0.09f, 0.14f, 0.96f);
                card.style.borderTopWidth = 2;
                card.style.borderBottomWidth = 2;
                card.style.borderLeftWidth = 2;
                card.style.borderRightWidth = 2;
                var borderCol = new Color(0.95f, 0.48f, 0.16f, 0.85f);
                card.style.borderTopColor = borderCol;
                card.style.borderBottomColor = borderCol;
                card.style.borderLeftColor = borderCol;
                card.style.borderRightColor = borderCol;
                card.style.borderTopLeftRadius = 14;
                card.style.borderTopRightRadius = 14;
                card.style.borderBottomLeftRadius = 14;
                card.style.borderBottomRightRadius = 14;
                card.style.alignItems = Align.Center;

                var title = new Label("P.I.A.R.I.A. — TURNO NOTURNO");
                title.style.fontSize = 30;
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.color = new Color(0.98f, 0.78f, 0.28f);
                title.style.marginBottom = 6;
                card.Add(title);

                _nightBannerLabel = new Label();
                _nightBannerLabel.style.fontSize = 18;
                _nightBannerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                _nightBannerLabel.style.color = new Color(1f, 0.56f, 0.24f);
                _nightBannerLabel.style.marginBottom = 20;
                RefreshNightBannerText();
                card.Add(_nightBannerLabel);

                _spinnerIcon = new VisualElement();
                _spinnerIcon.style.width = 46;
                _spinnerIcon.style.height = 46;
                _spinnerIcon.style.borderTopWidth = 4;
                _spinnerIcon.style.borderBottomWidth = 4;
                _spinnerIcon.style.borderLeftWidth = 4;
                _spinnerIcon.style.borderRightWidth = 4;
                _spinnerIcon.style.borderTopColor = new Color(0.96f, 0.45f, 0.18f);
                _spinnerIcon.style.borderBottomColor = new Color(0.24f, 0.22f, 0.28f);
                _spinnerIcon.style.borderLeftColor = new Color(0.24f, 0.22f, 0.28f);
                _spinnerIcon.style.borderRightColor = new Color(0.24f, 0.22f, 0.28f);
                _spinnerIcon.style.borderTopLeftRadius = 23;
                _spinnerIcon.style.borderTopRightRadius = 23;
                _spinnerIcon.style.borderBottomLeftRadius = 23;
                _spinnerIcon.style.borderBottomRightRadius = 23;
                _spinnerIcon.style.marginBottom = 18;
                card.Add(_spinnerIcon);

                _stageTagLabel = new Label("ETAPA: CONNECTINGSESSION");
                _stageTagLabel.style.fontSize = 11;
                _stageTagLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                _stageTagLabel.style.color = new Color(0.76f, 0.72f, 0.82f);
                _stageTagLabel.style.marginBottom = 4;
                card.Add(_stageTagLabel);

                _statusLabel = new Label("Preparando turno...");
                _statusLabel.style.fontSize = 16;
                _statusLabel.style.color = Color.white;
                _statusLabel.style.marginBottom = 12;
                card.Add(_statusLabel);

                _progressBar = new ProgressBar();
                _progressBar.style.width = 500;
                _progressBar.style.height = 16;
                _progressBar.style.marginBottom = 20;
                card.Add(_progressBar);

                _hintLabel = new Label(GameplayHints[0]);
                _hintLabel.style.fontSize = 12;
                _hintLabel.style.color = new Color(0.72f, 0.72f, 0.78f);
                _hintLabel.style.whiteSpace = WhiteSpace.Normal;
                _hintLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                card.Add(_hintLabel);

                _overlayRoot.Add(card);

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

            if (_spinnerIcon != null)
            {
                float rot = (Time.unscaledTime * 280f) % 360f;
                _spinnerIcon.style.rotate = new Rotate(rot);
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

        /// <summary>
        /// Transição offline local controlada pelo coordenador.
        /// </summary>
        public void TransitionToScene(string targetSceneName, Action onReadyCallback = null)
        {
            if (_isLoading) return;
            _expectedTargetScene = targetSceneName;
            _trackedNetworkAsyncOp = null;
            _networkSceneLoadCompleted = false;
            StartCoroutine(RoutineTransition(targetSceneName, false, onReadyCallback));
        }

        /// <summary>
        /// Transição autoritativa em rede (Host/Server). Só inicia o overlay se o SceneManager aceitar a troca.
        /// </summary>
        public SceneEventProgressStatus BeginNetworkedTransition(
            string targetSceneName,
            int nightNumber = 1,
            int revenueGoal = 150,
            Action onReadyCallback = null)
        {
            SetNightContext(nightNumber, revenueGoal);

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || nm.SceneManager == null)
            {
                Debug.LogError("[PizzeriaSceneLoadingCoordinator] BeginNetworkedTransition exige servidor ativo com NetworkSceneManager.");
                return SceneEventProgressStatus.InternalNetcodeError;
            }

            TryHookNetworkSceneEvents();
            _expectedTargetScene = targetSceneName;
            _trackedNetworkAsyncOp = null;
            _networkSceneLoadCompleted = false;

            if (!_isLoading)
            {
                StartCoroutine(RoutineTransition(targetSceneName, true, onReadyCallback));
            }

            var status = nm.SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                AbortLoading($"Falha ao iniciar carregamento em rede ({status}).");
            }
            return status;
        }

        public void AbortLoading(string reason = null)
        {
            StopAllCoroutines();
            if (!string.IsNullOrEmpty(reason))
            {
                Debug.LogWarning($"[PizzeriaSceneLoadingCoordinator] Carregamento interrompido: {reason}");
            }

            if (_overlayRoot != null)
            {
                _overlayRoot.style.opacity = 0f;
                _overlayRoot.style.display = DisplayStyle.None;
            }

            ReleaseInputLockIfHeld();
            _isLoading = false;
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
                    if (!IsGameplaySceneName(sceneEvent.SceneName)) return;
                    _expectedTargetScene = sceneEvent.SceneName;
                    _trackedNetworkAsyncOp = sceneEvent.AsyncOperation;
                    _networkSceneLoadCompleted = false;
                    if (!_isLoading)
                    {
                        StartCoroutine(RoutineTransition(sceneEvent.SceneName, true, null));
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

        private IEnumerator RoutineTransition(string targetSceneName, bool networked, Action onReadyCallback)
        {
            _isLoading = true;
            AcquireInputLock();
            EnsureUIOverlay();
            RefreshNightBannerText();

            float startRealtime = Time.realtimeSinceStartup;
            float maxTimeout = Mathf.Max(10f, _loadingTimeoutSeconds);

            // 1. Fade-in do overlay
            _overlayRoot.style.display = DisplayStyle.Flex;
            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _overlayRoot.style.opacity = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _fadeDuration));
                yield return null;
            }
            _overlayRoot.style.opacity = 1f;

            // Etapa 1: ConnectingSession
            SetStage(LoadingStage.ConnectingSession, 0.08f, networked
                ? "Conectando sessão e validando jogadores..."
                : "Inicializando turno local (offline)...");
            if (networked)
            {
                float connWait = 0f;
                while (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening && connWait < 3f)
                {
                    connWait += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            yield return null;

            // Etapa 2: LoadingScene
            SetStage(LoadingStage.LoadingScene, 0.14f, "Carregando cena e recursos visuais...");
            if (!networked)
            {
                AsyncOperation op = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
                if (op != null)
                {
                    while (!op.isDone && !HasTimedOut(startRealtime, maxTimeout))
                    {
                        float p = Mathf.Lerp(0.14f, 0.42f, op.progress);
                        SetStage(LoadingStage.LoadingScene, p, "Carregando cena e recursos visuais...");
                        yield return null;
                    }
                }
            }
            else
            {
                while (!HasTimedOut(startRealtime, maxTimeout))
                {
                    float asyncP = _trackedNetworkAsyncOp != null ? _trackedNetworkAsyncOp.progress : 0.5f;
                    SetStage(LoadingStage.LoadingScene, Mathf.Lerp(0.14f, 0.42f, asyncP), "Sincronizando e carregando cena...");

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

            // Etapa 3: GeneratingCity
            SetStage(LoadingStage.GeneratingCity, 0.48f, "Gerando cidade procedural e malha viária...");
            float cityWait = 0f;
            CityGenerator cityGen = FindFirstObjectByType<CityGenerator>();
            while (cityGen == null && cityWait < 2.5f && !HasTimedOut(startRealtime, maxTimeout))
            {
                cityWait += Time.unscaledDeltaTime;
                cityGen = FindFirstObjectByType<CityGenerator>();
                yield return null;
            }

            while (cityGen != null && (cityGen.CityData == null || !cityGen.ClosuresSynchronized) && !HasTimedOut(startRealtime, maxTimeout))
            {
                SetStage(LoadingStage.GeneratingCity, 0.56f, "Gerando quarteirões, prédios e vias urbanas...");
                yield return null;
            }

            // Etapa 4: BakingNavigation
            SetStage(LoadingStage.BakingNavigation, 0.68f, "Validando malha de navegação (NavMesh)...");
            while (NavigationWorldManager.Instance != null && !NavigationWorldManager.Instance.IsReady && !HasTimedOut(startRealtime, maxTimeout))
            {
                yield return null;
            }

            // Etapa 5: SpawningPizzeria
            SetStage(LoadingStage.SpawningPizzeria, 0.84f, "Preparando pizzaria, bancadas e quadro de pedidos...");
            while (!IsPizzeriaReady(networked) && !HasTimedOut(startRealtime, maxTimeout))
            {
                yield return null;
            }

            // Atualiza banner se PizzeriaRoot já expôs estado de rede/configuração
            RefreshNightBannerText();

            // Etapa 6: SpawningPlayers
            SetStage(LoadingStage.SpawningPlayers, 0.94f, "Posicionando equipe no ponto de entrada...");
            float playerWait = 0f;
            while (!IsLocalPlayerSpawned() && playerWait < 6f && !HasTimedOut(startRealtime, maxTimeout))
            {
                playerWait += Time.unscaledDeltaTime;
                yield return null;
            }

            // Etapa 7: Ready
            if (HasTimedOut(startRealtime, maxTimeout))
            {
                Debug.LogWarning($"[PizzeriaSceneLoadingCoordinator] Timeout ({maxTimeout:F0}s) ao aguardar prontidão da cena {targetSceneName}. Liberando controle com fallback seguro.");
                SetStage(LoadingStage.Ready, 1.0f, "Tempo limite atingido — liberando partida...");
            }
            else
            {
                SetStage(LoadingStage.Ready, 1.0f, "Pronto! Iniciando turno na pizzaria...");
            }

            yield return new WaitForSecondsRealtime(0.25f);
            onReadyCallback?.Invoke();

            // Fade-out e liberação segura do input
            elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _overlayRoot.style.opacity = 1f - Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _fadeDuration));
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
            return true;
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
            _currentProgress = progress01;
            if (_progressBar != null)
            {
                _progressBar.value = Mathf.Clamp01(progress01) * 100f;
            }
            if (_stageTagLabel != null)
            {
                _stageTagLabel.text = $"ETAPA: {stage.ToString().ToUpperInvariant()}";
            }
            if (_statusLabel != null)
            {
                _statusLabel.text = statusText;
            }
        }
    }
}
