using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace ScaryParty.Cutscenes
{
    public enum CutscenePlaybackState
    {
        Idle = 0,
        Preparing = 1,
        Ready = 2,
        Playing = 3,
        Finishing = 4,
        Failed = 5
    }

    public sealed class CutscenePlaybackService : MonoBehaviour
    {
        public static CutscenePlaybackService Instance { get; private set; }
        public static bool IsNightTimerPaused { get; set; }

        public static CutscenePlaybackService EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<CutscenePlaybackService>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }
            var go = new GameObject("CutscenePlaybackService");
            return go.AddComponent<CutscenePlaybackService>();
        }

        [SerializeField] private List<CutsceneDefinition> definitions = new List<CutsceneDefinition>();
        [SerializeField] private CutsceneOverlayPresenter overlayPresenter;

        private readonly Dictionary<string, CutsceneDefinition> _byId = new Dictionary<string, CutsceneDefinition>();
        private CutscenePlaybackState _state = CutscenePlaybackState.Idle;
        private CutsceneDefinition _activeDef;
        private CutsceneStage _activeStage;
        private Scene _loadedScene;
        private RenderTexture _renderTexture;
        private Coroutine _activeRoutine;
        private bool _completionFired;

        // Saved gameplay state
        private Camera _disabledGameplayCam;
        private bool _savedCursorVisible;
        private CursorLockMode _savedCursorLock;
        private AudioListener _savedGameplayListener;

        public CutscenePlaybackState State => _state;

        public event Action<string> Prepared;
        public event Action<string> Started;
        public event Action<string> Completed;
        public event Action<string> Skipped;
        public event Action<string, string> Failed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (overlayPresenter == null)
            {
                overlayPresenter = gameObject.AddComponent<CutsceneOverlayPresenter>();
            }
            overlayPresenter.Initialize();
            overlayPresenter.SkipRequested += OnSkipRequested;

            RebuildLookup();
        }

        public bool HasDefinition(string id) => !string.IsNullOrEmpty(id) && _byId.ContainsKey(id);

        public void RegisterDefinition(CutsceneDefinition def)
        {
            if (def != null && !string.IsNullOrEmpty(def.cutsceneId))
            {
                _byId[def.cutsceneId] = def;
            }
        }

        private void RebuildLookup()
        {
            _byId.Clear();
            foreach (var def in Resources.LoadAll<CutsceneDefinition>(""))
            {
                RegisterDefinition(def);
            }
            foreach (var def in definitions)
            {
                RegisterDefinition(def);
            }
        }

        public void Prepare(string cutsceneId)
        {
            if (_state != CutscenePlaybackState.Idle && _state != CutscenePlaybackState.Failed)
            {
                Debug.LogWarning($"[CutscenePlaybackService] Cannot prepare '{cutsceneId}' while in state {_state}.");
                return;
            }

            if (!_byId.TryGetValue(cutsceneId, out var def))
            {
                Fail(cutsceneId, $"CutsceneDefinition '{cutsceneId}' not found.");
                return;
            }

            _activeDef = def;
            _completionFired = false;
            _activeRoutine = StartCoroutine(PrepareRoutine(def));
        }

        public void Play(string cutsceneId)
        {
            if (_state == CutscenePlaybackState.Ready && _activeDef != null && _activeDef.cutsceneId == cutsceneId)
            {
                _activeRoutine = StartCoroutine(PlayRoutine(_activeDef));
                return;
            }

            if (_state != CutscenePlaybackState.Idle && _state != CutscenePlaybackState.Failed)
            {
                Debug.LogWarning($"[CutscenePlaybackService] Cannot play '{cutsceneId}' while in state {_state}.");
                return;
            }

            if (!_byId.TryGetValue(cutsceneId, out var def))
            {
                Fail(cutsceneId, $"CutsceneDefinition '{cutsceneId}' not found.");
                return;
            }

            _activeDef = def;
            _completionFired = false;
            _activeRoutine = StartCoroutine(PrepareAndPlayRoutine(def));
        }

        public void Skip()
        {
            if (_state != CutscenePlaybackState.Playing || _activeDef == null || !_activeDef.allowSkip)
                return;

            FinishSession(skipped: true);
        }

        public void Cancel()
        {
            if (_state == CutscenePlaybackState.Idle) return;
            FinishSession(skipped: true);
        }

        private void OnSkipRequested() => Skip();

        private IEnumerator PrepareAndPlayRoutine(CutsceneDefinition def)
        {
            yield return PrepareRoutine(def);
            if (_state == CutscenePlaybackState.Ready)
            {
                yield return PlayRoutine(def);
            }
        }

        private IEnumerator PrepareRoutine(CutsceneDefinition def)
        {
            _state = CutscenePlaybackState.Preparing;

            if (string.IsNullOrEmpty(def.targetSceneName))
            {
                Fail(def.cutsceneId, "Target scene name is empty.");
                yield break;
            }

            var loadOp = SceneManager.LoadSceneAsync(def.targetSceneName, LoadSceneMode.Additive);
            if (loadOp == null)
            {
                Fail(def.cutsceneId, $"Failed to start additive load for '{def.targetSceneName}'.");
                yield break;
            }

            while (!loadOp.isDone)
                yield return null;

            _loadedScene = SceneManager.GetSceneByName(def.targetSceneName);
            if (!_loadedScene.IsValid() || !_loadedScene.isLoaded)
            {
                Fail(def.cutsceneId, $"Scene '{def.targetSceneName}' failed to load.");
                yield break;
            }

            _activeStage = FindStageInScene(_loadedScene);
            if (_activeStage == null || _activeStage.outputCamera == null || _activeStage.director == null)
            {
                Fail(def.cutsceneId, "CutsceneStage, outputCamera, or director missing in loaded scene.");
                yield break;
            }

            AllocateRenderTexture(def);
            _activeStage.outputCamera.targetTexture = _renderTexture;
            _activeStage.director.timeUpdateMode = def.useUnscaledTime
                ? DirectorUpdateMode.UnscaledGameTime
                : DirectorUpdateMode.GameTime;
            _activeStage.director.time = 0.0;
            _activeStage.director.Evaluate();
            _activeStage.outputCamera.Render();

            _state = CutscenePlaybackState.Ready;
            Prepared?.Invoke(def.cutsceneId);
        }

        private IEnumerator PlayRoutine(CutsceneDefinition def)
        {
            if (_activeStage == null || _activeStage.director == null)
            {
                FinishSession(skipped: true);
                yield break;
            }

            _state = CutscenePlaybackState.Playing;

            _savedCursorVisible = Cursor.visible;
            _savedCursorLock = Cursor.lockState;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            // Ensure single AudioListener
            if (_activeStage != null && _activeStage.cutsceneAudioListener != null && _activeStage.cutsceneAudioListener.enabled)
            {
                foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                {
                    if (listener != _activeStage.cutsceneAudioListener && listener.enabled)
                    {
                        _savedGameplayListener = listener;
                        listener.enabled = false;
                        break;
                    }
                }
            }

            float aspect = (float)def.referenceWidth / Mathf.Max(1, def.referenceHeight);
            overlayPresenter.Show(_renderTexture, aspect, def.allowSkip);

            // Fade in
            float elapsed = 0f;
            float fadeIn = Mathf.Max(0.01f, def.fadeInDuration);
            while (elapsed < fadeIn && _state == CutscenePlaybackState.Playing && _activeStage != null && _activeStage.director != null)
            {
                elapsed += Time.unscaledDeltaTime;
                overlayPresenter.SetAlpha(elapsed / fadeIn);
                yield return null;
            }

            if (_activeStage == null || _activeStage.director == null)
            {
                FinishSession(skipped: true);
                yield break;
            }

            overlayPresenter.SetAlpha(1f);

            if (def.disableGameplay3DRenderingWhenOpaque)
            {
                var mainCam = Camera.main;
                if (mainCam != null && _activeStage != null && mainCam != _activeStage.outputCamera && mainCam.enabled)
                {
                    _disabledGameplayCam = mainCam;
                    _disabledGameplayCam.enabled = false;
                }
            }

            _activeStage.director.Play();
            Started?.Invoke(def.cutsceneId);

            double duration = def.durationSeconds > 0f ? def.durationSeconds : _activeStage.director.duration;
            while (_state == CutscenePlaybackState.Playing && _activeStage != null && _activeStage.director != null && _activeStage.director.time < duration)
            {
                yield return null;
            }

            if (_state == CutscenePlaybackState.Playing)
            {
                bool wasDestroyedMidPlay = _activeStage == null || _activeStage.director == null;
                FinishSession(skipped: wasDestroyedMidPlay);
            }
        }

        private void FinishSession(bool skipped)
        {
            if (_state == CutscenePlaybackState.Finishing || _state == CutscenePlaybackState.Idle)
                return;

            _state = CutscenePlaybackState.Finishing;
            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
                _activeRoutine = null;
            }

            string id = _activeDef != null ? _activeDef.cutsceneId : string.Empty;
            StartCoroutine(CleanupRoutine(id, skipped));
        }

        private IEnumerator CleanupRoutine(string id, bool skipped)
        {
            if (_disabledGameplayCam != null)
            {
                _disabledGameplayCam.enabled = true;
                _disabledGameplayCam = null;
            }

            if (_savedGameplayListener != null)
            {
                _savedGameplayListener.enabled = true;
                _savedGameplayListener = null;
            }

            Cursor.visible = _savedCursorVisible;
            Cursor.lockState = _savedCursorLock;

            if (_activeStage != null && _activeStage.director != null)
                _activeStage.director.Stop();

            overlayPresenter.Hide();

            if (_activeStage != null && _activeStage.outputCamera != null)
                _activeStage.outputCamera.targetTexture = null;

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
                _renderTexture = null;
            }

            if (_loadedScene.IsValid() && _loadedScene.isLoaded)
            {
                var unloadOp = SceneManager.UnloadSceneAsync(_loadedScene);
                if (unloadOp != null)
                {
                    while (!unloadOp.isDone)
                        yield return null;
                }
            }

            _activeStage = null;
            _activeDef = null;
            _state = CutscenePlaybackState.Idle;

            if (!_completionFired)
            {
                _completionFired = true;
                if (skipped) Skipped?.Invoke(id);
                else Completed?.Invoke(id);
            }
        }

        private void Fail(string id, string reason)
        {
            Debug.LogError($"[CutscenePlaybackService] Failed '{id}': {reason}");
            _state = CutscenePlaybackState.Failed;
            Failed?.Invoke(id, reason);
            StartCoroutine(CleanupRoutine(id, skipped: true));
        }

        private void AllocateRenderTexture(CutsceneDefinition def)
        {
            int w = def.referenceWidth;
            int h = def.referenceHeight;
            if (def.qualityPreset == CutsceneQualityPreset.Medium720p && h > 720)
            {
                float scale = 720f / h;
                w = Mathf.RoundToInt(w * scale);
                h = 720;
            }
            else if (def.qualityPreset == CutsceneQualityPreset.High1080p && h > 1080)
            {
                float scale = 1080f / h;
                w = Mathf.RoundToInt(w * scale);
                h = 1080;
            }

            _renderTexture = new RenderTexture(Mathf.Max(64, w), Mathf.Max(64, h), 24, RenderTextureFormat.DefaultHDR);
            _renderTexture.name = $"CutsceneRT_{def.cutsceneId}";
            _renderTexture.Create();
        }

        private static CutsceneStage FindStageInScene(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var stage = root.GetComponentInChildren<CutsceneStage>(true);
                if (stage != null) return stage;
            }
            return null;
        }
    }
}
