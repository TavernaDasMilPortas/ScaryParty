using System.Collections;
using ScaryParty.Cutscenes;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.UI.Piaria.Presenters;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace ScaryParty.Pizzeria.Presentation
{
    public enum IntroPresentationStage
    {
        Loading = 0,
        IntroPreparing = 1,
        IntroPlaying = 2,
        Briefing = 3,
        PreparationVisible = 4,
        Countdown = 5,
        NightActive = 6,
        Error = 7
    }

    /// <summary>
    /// Orquestrador da sequência obrigatória de entrada da gameplay:
    /// LoadingOpaque -> CutsceneReady (preload) -> CutscenePlaying -> Briefing -> Preparation -> Countdown -> NightActive.
    /// Garante que o overlay de loading permaneça 100% opaco até o primeiro frame da cutscene (ou briefing fallback) estar pronto,
    /// e mantém HUD e relógio bloqueados até NightActive.
    /// </summary>
    public sealed class GameplayIntroCutsceneFlow : MonoBehaviour
    {
        public static GameplayIntroCutsceneFlow Instance { get; private set; }
        public static IntroPresentationStage CurrentStage { get; private set; } = IntroPresentationStage.Loading;
        public static bool IsBlockingIntermission => Instance != null && CurrentStage < IntroPresentationStage.PreparationVisible;
        public static event System.Action<IntroPresentationStage> OnStageChanged;

        private static void SetStage(IntroPresentationStage next)
        {
            if (CurrentStage == next) return;
            Debug.Log($"[IntroFlow] Stage: {CurrentStage} -> {next}");
            CurrentStage = next;
            OnStageChanged?.Invoke(next);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            CurrentStage = IntroPresentationStage.Loading;
            OnStageChanged = null;
        }

        public static bool ShouldAbortIntroAfterLoading(LoadingOperationState loadingState)
        {
            return loadingState == LoadingOperationState.ErrorVisible ||
                   loadingState == LoadingOperationState.Failed;
        }

        [SerializeField] private string introCutsceneId = "cutscene_01";
        [SerializeField] private float nightBannerDurationSeconds = 2.8f;
        [SerializeField] private float preloadTimeoutSeconds = 15f;

        private bool _hasStarted;
        private bool _cutsceneFinished;
        private bool _hasInputLock;
        private CutscenePlaybackService _boundService;

        public static GameplayIntroCutsceneFlow EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<GameplayIntroCutsceneFlow>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }
            var go = new GameObject("GameplayIntroCutsceneFlow");
            return go.AddComponent<GameplayIntroCutsceneFlow>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            SetStage(IntroPresentationStage.Loading);
        }

        private void Start()
        {
            if (_hasStarted) return;
            _hasStarted = true;
            StartCoroutine(RunIntroSequence());
        }

        private void AcquireIntroLock()
        {
            if (_hasInputLock) return;
            GameplayInputGate.PushLock();
            _hasInputLock = true;
        }

        private void ReleaseIntroLock()
        {
            if (!_hasInputLock) return;
            GameplayInputGate.PopLock();
            _hasInputLock = false;
        }

        private IEnumerator RunIntroSequence()
        {
            SetStage(IntroPresentationStage.Loading);
            CutscenePlaybackService.IsNightTimerPaused = true;
            SetGameplayHudVisibility(false);

            var coord = PizzeriaSceneLoadingCoordinator.Instance;

            // Aguarda RoutineTransition concluir carregamento/NavMesh/pizzaria/player e entrar efetivamente em handoff
            while (coord != null && coord.IsLoading && !coord.IsWaitingForPresentationHandoff)
            {
                yield return null;
            }
            int holdToken = coord != null ? coord.CurrentOperationToken : 0;

            if (coord != null && ShouldAbortIntroAfterLoading(coord.OperationState))
            {
                SetStage(IntroPresentationStage.Error);
                coord.ReleaseToCutscene(holdToken);
                yield break;
            }

            // 1. CutsceneReady: Preload completo atrás do overlay 100% opaco
            SetStage(IntroPresentationStage.IntroPreparing);
            AcquireIntroLock();

            var service = CutscenePlaybackService.EnsureInstance();
            bool hasCutscene = service != null && service.HasDefinition(introCutsceneId);
            if (hasCutscene)
            {
                _boundService = service;
                _cutsceneFinished = false;
                service.Completed += OnCutsceneEnded;
                service.Skipped += OnCutsceneEnded;
                service.Failed += OnCutsceneFailed;

                if (service.State == CutscenePlaybackState.Idle || service.State == CutscenePlaybackState.Failed)
                {
                    service.Prepare(introCutsceneId);
                }

                float prepStart = Time.unscaledTime;
                while (service != null &&
                       service.State == CutscenePlaybackState.Preparing &&
                       (Time.unscaledTime - prepStart) < preloadTimeoutSeconds)
                {
                    yield return null;
                }

                bool readyToPlay = service != null && service.State == CutscenePlaybackState.Ready;
                if (readyToPlay && service.Play(introCutsceneId))
                {
                    // Primeiro frame da cutscene já preparado na RenderTexture: aguarda 1 frame com overlay visível abaixo do Loading e então revela!
                    SetStage(IntroPresentationStage.IntroPlaying);
                    yield return null;
                    coord?.ReleaseToCutscene(holdToken);

                    while (service != null &&
                           (!_cutsceneFinished || service.State != CutscenePlaybackState.Idle))
                    {
                        yield return null;
                    }
                }
                else
                {
                    // Fallback seguro se preload falhou/expirou: aborta cutscene e aguarda unload antes do briefing
                    service?.Cancel();
                    while (service != null && service.State != CutscenePlaybackState.Idle)
                    {
                        yield return null;
                    }
                    coord?.ReleaseToCutscene(holdToken);
                }

                UnbindCutsceneEvents();
            }
            else
            {
                // Sem cutscene definida: libera overlay diretamente para o briefing
                coord?.ReleaseToCutscene(holdToken);
            }

            // Aguarda eventual fade-out do loading concluir antes de exibir Briefing
            while (coord != null && coord.IsLoading)
            {
                yield return null;
            }

            // 2. Briefing: sobe popup da meta da noite e sai antes do menu de preparação
            ReadNightContext(out int nightNumber, out int revenueGoal);
            SetStage(IntroPresentationStage.Briefing);
            yield return ShowNightObjectiveBannerRoutine(nightNumber, revenueGoal);

            // 3. Preparation: menu de melhorias/imprevistos/oportunidades se houver (com cursor liberado)
            ReleaseIntroLock();
            if (IsPreparationPhaseActive())
            {
                SetStage(IntroPresentationStage.PreparationVisible);
                while (IsPreparationPhaseActive())
                {
                    yield return null;
                }
            }
            else
            {
                SetStage(IntroPresentationStage.PreparationVisible);
            }

            // 4. NightActive: cada região da HUD entra do canto/borda mais próximo com efeito elástico + Feel
            SetStage(IntroPresentationStage.NightActive);
            if (HudPresenter.Instance != null)
            {
                HudPresenter.Instance.RevealGameplayHudElastic();
            }
            CutscenePlaybackService.IsNightTimerPaused = false;
        }

        private static void SetGameplayHudVisibility(bool visible)
        {
            if (HudPresenter.Instance != null)
            {
                HudPresenter.Instance.SetGameplayHudVisible(visible);
            }
        }

        private static void ReadNightContext(out int nightNumber, out int revenueGoal)
        {
            nightNumber = 1;
            revenueGoal = 60;
            var netState = PizzeriaNetworkState.Instance;
            if (netState != null && netState.IsSpawned)
            {
                var dto = netState.NightState.Value;
                nightNumber = Mathf.Max(0, dto.NightNumber);
                revenueGoal = Mathf.Max(0, dto.RevenueGoal);
            }
            else if (PizzeriaRoot.Instance != null && PizzeriaRoot.Instance.DomainState != null)
            {
                nightNumber = Mathf.Max(0, PizzeriaRoot.Instance.DomainState.CurrentNightNumber);
                revenueGoal = Mathf.Max(0, PizzeriaRoot.Instance.DomainState.NightRevenueGoal);
            }
        }

        private static bool IsPreparationPhaseActive()
        {
            var netState = PizzeriaNetworkState.Instance;
            if (netState != null && netState.IsSpawned)
            {
                return netState.NightState.Value.Phase == (byte)NightPhase.PreparingNextNight;
            }
            return PizzeriaRoot.Instance != null &&
                   PizzeriaRoot.Instance.DomainState != null &&
                   PizzeriaRoot.Instance.DomainState.CurrentNightPhase == NightPhase.PreparingNextNight;
        }

        private void UnbindCutsceneEvents()
        {
            if (_boundService == null) return;
            _boundService.Completed -= OnCutsceneEnded;
            _boundService.Skipped -= OnCutsceneEnded;
            _boundService.Failed -= OnCutsceneFailed;
            _boundService = null;
        }

        private void OnCutsceneEnded(string id)
        {
            if (id == introCutsceneId)
                _cutsceneFinished = true;
        }

        private void OnCutsceneFailed(string id, string reason)
        {
            if (id == introCutsceneId)
                _cutsceneFinished = true;
        }

        private IEnumerator ShowNightObjectiveBannerRoutine(int nightNumber, int revenueGoal)
        {
            var canvasGo = new GameObject("NightObjectiveBannerCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var cg = canvasGo.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelRect = panelGo.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.24f, 0.36f);
            panelRect.anchorMax = new Vector2(0.76f, 0.64f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.25f, 0.14f, 0.09f, 0.94f);

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.05f, 0.50f);
            titleRect.anchorMax = new Vector2(0.95f, 0.90f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            var titleText = titleGo.AddComponent<Text>();
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 52;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.99f, 0.83f, 0.30f, 1f);
            titleText.text = nightNumber == 0
                ? "NOITE 0 — TUTORIAL"
                : (nightNumber == 1 ? "PRIMEIRA NOITE" : $"NOITE {nightNumber}");

            var subGo = new GameObject("Objective");
            subGo.transform.SetParent(panelGo.transform, false);
            var subRect = subGo.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.05f, 0.12f);
            subRect.anchorMax = new Vector2(0.95f, 0.48f);
            subRect.offsetMin = Vector2.zero;
            subRect.offsetMax = Vector2.zero;
            var subText = subGo.AddComponent<Text>();
            subText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            subText.fontSize = 32;
            subText.alignment = TextAnchor.MiddleCenter;
            subText.color = new Color(1f, 0.96f, 0.90f, 1f);
            subText.text = revenueGoal > 0
                ? $"Objetivo da Noite: R$ {revenueGoal}"
                : "Atenda os clientes e prepare a equipe!";

            float fade = ScaryParty.UI.Piaria.Core.UIToolkitTweenService.ReduceMotion ? 0.12f : 0.58f;
            float t = 0f;
            while (t < fade)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / fade);
                cg.alpha = u;
                if (!ScaryParty.UI.Piaria.Core.UIToolkitTweenService.ReduceMotion)
                {
                    float s = u - 1f;
                    float overshoot = 1.70158f;
                    float back = 1f + (overshoot + 1f) * s * s * s + overshoot * s * s;
                    float yOffset = Mathf.LerpUnclamped(-320f, 0f, back);
                    panelRect.anchoredPosition = new Vector2(0f, yOffset);
                }
                yield return null;
            }
            cg.alpha = 1f;
            panelRect.anchoredPosition = Vector2.zero;
            ScaryParty.UI.Piaria.Core.UIFeedbackService.Instance?.PlaySuccess();

            yield return new WaitForSecondsRealtime(Mathf.Max(0.5f, nightBannerDurationSeconds));

            t = 0f;
            float outDur = ScaryParty.UI.Piaria.Core.UIToolkitTweenService.ReduceMotion ? 0.10f : 0.48f;
            while (t < outDur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / outDur);
                cg.alpha = 1f - u;
                if (!ScaryParty.UI.Piaria.Core.UIToolkitTweenService.ReduceMotion)
                {
                    panelRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, 120f, u * u));
                }
                yield return null;
            }

            Destroy(canvasGo);
        }

        private void OnDestroy()
        {
            UnbindCutsceneEvents();
            if (CutscenePlaybackService.Instance != null &&
                CutscenePlaybackService.Instance.State != CutscenePlaybackState.Idle)
            {
                CutscenePlaybackService.Instance.Cancel();
            }
            ReleaseIntroLock();
            SetGameplayHudVisibility(true);
            CutscenePlaybackService.IsNightTimerPaused = false;
            if (Instance == this)
            {
                SetStage(IntroPresentationStage.NightActive);
                Instance = null;
            }
        }
    }
}
