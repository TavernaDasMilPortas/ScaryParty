using System.Collections;
using ScaryParty.Cutscenes;
using ScaryParty.Pizzeria.Composition;
using UnityEngine;
using UnityEngine.UI;

namespace ScaryParty.Pizzeria.Presentation
{
    /// <summary>
    /// Fluxo de entrada da cena de gameplay:
    /// 1. Pausa o relógio da noite ao entrar na cena.
    /// 2. Reproduz a cutscene de introdução em RawImage (assim que o loading termina).
    /// 3. Ao término/skip da cutscene, exibe o banner da Noite 1 e o objetivo em dinheiro.
    /// 4. Libera o controle do jogador e inicia a contagem do tempo da noite.
    /// </summary>
    public sealed class GameplayIntroCutsceneFlow : MonoBehaviour
    {
        public static GameplayIntroCutsceneFlow Instance { get; private set; }

        [SerializeField] private string introCutsceneId = "cutscene_01";
        [SerializeField] private float nightBannerDurationSeconds = 3.5f;

        private bool _hasStarted;
        private bool _cutsceneFinished;
        private bool _hasInputLock;

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
        }

        private void Start()
        {
            if (_hasStarted) return;
            _hasStarted = true;
            StartCoroutine(RunIntroSequence());
        }

        private IEnumerator RunIntroSequence()
        {
            CutscenePlaybackService.IsNightTimerPaused = true;

            // Aguardar o loading screen terminar antes de iniciar a cutscene
            while (PizzeriaSceneLoadingCoordinator.Instance != null && PizzeriaSceneLoadingCoordinator.Instance.IsLoading)
            {
                yield return null;
            }

            GameplayInputGate.PushLock();
            _hasInputLock = true;

            var service = CutscenePlaybackService.EnsureInstance();
            if (service.HasDefinition(introCutsceneId))
            {
                _cutsceneFinished = false;
                service.Completed += OnCutsceneEnded;
                service.Skipped += OnCutsceneEnded;
                service.Failed += OnCutsceneFailed;

                service.Play(introCutsceneId);

                while (!_cutsceneFinished)
                {
                    yield return null;
                }

                service.Completed -= OnCutsceneEnded;
                service.Skipped -= OnCutsceneEnded;
                service.Failed -= OnCutsceneFailed;
            }

            // Exibir banner da Primeira Noite + Objetivo em dinheiro
            int nightNumber = 1;
            int revenueGoal = 60;
            if (PizzeriaRoot.Instance != null && PizzeriaRoot.Instance.DomainState != null)
            {
                nightNumber = Mathf.Max(1, PizzeriaRoot.Instance.DomainState.CurrentNightNumber);
                revenueGoal = Mathf.Max(10, PizzeriaRoot.Instance.DomainState.NightRevenueGoal);
            }

            yield return ShowNightObjectiveBannerRoutine(nightNumber, revenueGoal);

            // Liberar input e iniciar a contagem do tempo da noite
            if (_hasInputLock)
            {
                GameplayInputGate.PopLock();
                _hasInputLock = false;
            }
            CutscenePlaybackService.IsNightTimerPaused = false;
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
            canvas.sortingOrder = 450;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var cg = canvasGo.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelRect = panelGo.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.25f, 0.38f);
            panelRect.anchorMax = new Vector2(0.75f, 0.62f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.06f, 0.08f, 0.88f);

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.48f);
            titleRect.anchorMax = new Vector2(1f, 0.92f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            var titleText = titleGo.AddComponent<Text>();
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 54;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1f, 0.85f, 0.35f, 1f);
            titleText.text = nightNumber == 1 ? "PRIMEIRA NOITE" : $"NOITE {nightNumber}";

            var subGo = new GameObject("Objective");
            subGo.transform.SetParent(panelGo.transform, false);
            var subRect = subGo.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0f, 0.1f);
            subRect.anchorMax = new Vector2(1f, 0.5f);
            subRect.offsetMin = Vector2.zero;
            subRect.offsetMax = Vector2.zero;
            var subText = subGo.AddComponent<Text>();
            subText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            subText.fontSize = 34;
            subText.alignment = TextAnchor.MiddleCenter;
            subText.color = Color.white;
            subText.text = $"Objetivo da Noite: R$ {revenueGoal}";

            float fade = 0.4f;
            float t = 0f;
            while (t < fade)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Clamp01(t / fade);
                yield return null;
            }
            cg.alpha = 1f;

            yield return new WaitForSecondsRealtime(Mathf.Max(0.5f, nightBannerDurationSeconds));

            t = 0f;
            while (t < fade)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = 1f - Mathf.Clamp01(t / fade);
                yield return null;
            }

            Destroy(canvasGo);
        }

        private void OnDestroy()
        {
            if (_hasInputLock)
            {
                GameplayInputGate.PopLock();
                _hasInputLock = false;
            }
            CutscenePlaybackService.IsNightTimerPaused = false;
            if (Instance == this) Instance = null;
        }
    }
}
