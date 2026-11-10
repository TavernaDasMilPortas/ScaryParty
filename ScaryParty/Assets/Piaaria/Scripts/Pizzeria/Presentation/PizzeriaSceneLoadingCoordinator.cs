using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Netcode;
using ScaryParty.Navigation.Core;
using ScaryParty.Pizzeria.Composition;

namespace ScaryParty.Pizzeria.Presentation
{
    /// <summary>
    /// Coordenador persistente de transições de cena e carregamento (Fase 4).
    /// Controla o overlay de carregamento em UI Toolkit, o bloqueio de input por token via GameplayInputGate,
    /// a prontidão do NavMesh e a liberação suave dos controles de câmera/jogador.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class PizzeriaSceneLoadingCoordinator : MonoBehaviour
    {
        public static PizzeriaSceneLoadingCoordinator Instance { get; private set; }

        [Header("Configuração de Transição")]
        [SerializeField] private float _fadeDuration = 0.4f;
        [SerializeField] private float _loadingTimeoutSeconds = 40f;

        private UIDocument _loadingUiDocument;
        private VisualElement _overlayRoot;
        private ProgressBar _progressBar;
        private Label _statusLabel;
        private Label _hintLabel;
        private VisualElement _spinnerIcon;

        private bool _isLoading = false;
        private float _currentProgress = 0f;
        private float _hintTimer = 0f;
        private int _hintIndex = 0;

        private static readonly string[] GameplayHints = new[]
        {
            "Dica: Segure [F] por 0,6 s para atender clientes no balcão e registrar pedidos no quadro.",
            "Dica: Clientes sentados no salão recebem a pizza sem caixa diretamente no prato!",
            "Dica: Use caixas de pizza apenas para pedidos recebidos pelo telefone (Delivery).",
            "Dica: Fique atento ao forno! Retire a pizza assim que dourar para evitar fumaça e queimar.",
            "Dica: A despensa guarda ingredientes secos e caixas; a geladeira guarda queijos e frios.",
            "Dica: Pressione [E] nas bancadas para pegar ou posicionar recipientes e utensílios.",
            "Dica: A mochila de entrega permite transportar várias caixas de pizza simultaneamente nas costas."
        };

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
        }

        private void EnsureUIOverlay()
        {
            if (_loadingUiDocument == null)
            {
                _loadingUiDocument = gameObject.GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
            }

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
                _overlayRoot.style.backgroundColor = new Color(0.07f, 0.07f, 0.09f, 1f);
                _overlayRoot.style.justifyContent = Justify.Center;
                _overlayRoot.style.alignItems = Align.Center;
                _overlayRoot.style.display = DisplayStyle.None;
                _overlayRoot.style.opacity = 0f;

                // Painel central de conteúdo
                var container = new VisualElement();
                container.style.width = 540;
                container.style.alignItems = Align.Center;

                // Título
                var title = new Label("P.I.A.R.I.A.");
                title.style.fontSize = 38;
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.color = new Color(0.96f, 0.76f, 0.28f);
                title.style.marginBottom = 10;
                container.Add(title);

                var subtitle = new Label("SISTEMA INTEGRADO DE PIZZARIA E CIDADE");
                subtitle.style.fontSize = 13;
                subtitle.style.color = new Color(0.70f, 0.70f, 0.75f);
                subtitle.style.marginBottom = 35;
                container.Add(subtitle);

                // Ícone Spinner
                _spinnerIcon = new VisualElement();
                _spinnerIcon.style.width = 48;
                _spinnerIcon.style.height = 48;
                _spinnerIcon.style.borderTopWidth = 4;
                _spinnerIcon.style.borderBottomWidth = 4;
                _spinnerIcon.style.borderLeftWidth = 4;
                _spinnerIcon.style.borderRightWidth = 4;
                _spinnerIcon.style.borderTopColor = new Color(0.95f, 0.45f, 0.20f);
                _spinnerIcon.style.borderBottomColor = new Color(0.2f, 0.2f, 0.25f);
                _spinnerIcon.style.borderLeftColor = new Color(0.2f, 0.2f, 0.25f);
                _spinnerIcon.style.borderRightColor = new Color(0.2f, 0.2f, 0.25f);
                _spinnerIcon.style.borderTopLeftRadius = 24;
                _spinnerIcon.style.borderTopRightRadius = 24;
                _spinnerIcon.style.borderBottomLeftRadius = 24;
                _spinnerIcon.style.borderBottomRightRadius = 24;
                _spinnerIcon.style.marginBottom = 25;
                container.Add(_spinnerIcon);

                // Etapa textual de progresso
                _statusLabel = new Label("Carregando...");
                _statusLabel.style.fontSize = 15;
                _statusLabel.style.color = Color.white;
                _statusLabel.style.marginBottom = 12;
                container.Add(_statusLabel);

                // Barra de progresso
                _progressBar = new ProgressBar();
                _progressBar.style.width = 500;
                _progressBar.style.height = 14;
                _progressBar.style.marginBottom = 25;
                container.Add(_progressBar);

                // Dica curta
                _hintLabel = new Label(GameplayHints[0]);
                _hintLabel.style.fontSize = 12;
                _hintLabel.style.color = new Color(0.65f, 0.65f, 0.70f);
                _hintLabel.style.whiteSpace = WhiteSpace.Normal;
                _hintLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                container.Add(_hintLabel);

                _overlayRoot.Add(container);

                var root = _loadingUiDocument.rootVisualElement;
                if (root != null)
                {
                    root.Add(_overlayRoot);
                }
            }
        }

        private void Update()
        {
            if (!_isLoading) return;

            // Animação contínua de rotação do spinner
            if (_spinnerIcon != null)
            {
                float rot = (Time.unscaledTime * 280f) % 360f;
                _spinnerIcon.style.rotate = new Rotate(rot);
            }

            // Rotação periódica de dicas
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
        /// Inicia o fluxo coordenado de carregamento entre cenas.
        /// </summary>
        public void TransitionToScene(string targetSceneName, Action onReadyCallback = null)
        {
            if (_isLoading) return;
            StartCoroutine(RoutineTransition(targetSceneName, onReadyCallback));
        }

        private IEnumerator RoutineTransition(string targetSceneName, Action onReadyCallback)
        {
            _isLoading = true;
            GameplayInputGate.PushLock();
            EnsureUIOverlay();

            // 1. Fade-in do overlay
            _overlayRoot.style.display = DisplayStyle.Flex;
            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _overlayRoot.style.opacity = Mathf.Clamp01(elapsed / _fadeDuration);
                yield return null;
            }
            _overlayRoot.style.opacity = 1f;

            // 2. Carregamento de cena assíncrono
            SetStage(0.15f, "Carregando cena e recursos visuais...");
            AsyncOperation op = SceneManager.LoadSceneAsync(targetSceneName);
            if (op != null)
            {
                while (!op.isDone)
                {
                    float p = Mathf.Lerp(0.15f, 0.45f, op.progress);
                    SetStage(p, "Carregando cena e recursos visuais...");
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSecondsRealtime(0.5f);
            }

            // 3. Inicialização e geração do mundo
            SetStage(0.55f, "Inicializando ambiente urbano e gerando módulos...");
            yield return new WaitForSecondsRealtime(0.2f);

            // 4. Aguardar prontidão da navegação NavMesh
            SetStage(0.75f, "Validando malha de navegação (NavMesh)...");
            float navWaitTimer = 0f;
            while (NavigationWorldManager.Instance != null && !NavigationWorldManager.Instance.IsReady && navWaitTimer < 10f)
            {
                navWaitTimer += Time.unscaledDeltaTime;
                yield return null;
            }

            // 5. Posicionamento e conexão da pizzaria
            SetStage(0.90f, "Sincronizando estado da sessão e preparando bancadas...");
            yield return new WaitForSecondsRealtime(0.2f);

            // 6. Conclusão da prontidão
            SetStage(1.0f, "Pronto! Iniciando partida...");
            yield return new WaitForSecondsRealtime(0.3f);

            onReadyCallback?.Invoke();

            // 7. Fade-out do overlay e liberação de controle
            elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _overlayRoot.style.opacity = 1f - Mathf.Clamp01(elapsed / _fadeDuration);
                yield return null;
            }
            _overlayRoot.style.opacity = 0f;
            _overlayRoot.style.display = DisplayStyle.None;

            GameplayInputGate.PopLock();
            _isLoading = false;
        }

        private void SetStage(float progress01, string statusText)
        {
            _currentProgress = progress01;
            if (_progressBar != null)
            {
                _progressBar.value = Mathf.Clamp01(progress01) * 100f;
            }
            if (_statusLabel != null)
            {
                _statusLabel.text = statusText;
            }
        }
    }
}
