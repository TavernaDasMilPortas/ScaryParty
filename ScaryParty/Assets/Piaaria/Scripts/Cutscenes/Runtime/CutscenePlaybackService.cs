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
        private string _loadingSceneName;
        private AsyncOperation _pendingLoadOp;
        private RenderTexture _renderTexture;
        private Coroutine _activeRoutine;
        private bool _completionFired;
        private bool _cleaningUp;

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
            foreach (var def in Resources.LoadAll<CutsceneDefinition>("Cutscenes"))
            {
                RegisterDefinition(def);
            }
            if (_byId.Count == 0)
            {
                foreach (var def in Resources.LoadAll<CutsceneDefinition>(""))
                {
                    RegisterDefinition(def);
                }
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

        public bool Play(string cutsceneId)
        {
            if (_state == CutscenePlaybackState.Ready && _activeDef != null && _activeDef.cutsceneId == cutsceneId)
            {
                _activeRoutine = StartCoroutine(PlayRoutine(_activeDef));
                return true;
            }

            if (_state != CutscenePlaybackState.Idle && _state != CutscenePlaybackState.Failed)
            {
                Debug.LogWarning($"[CutscenePlaybackService] Cannot play '{cutsceneId}' while in state {_state}.");
                return false;
            }

            if (!_byId.TryGetValue(cutsceneId, out var def))
            {
                Fail(cutsceneId, $"CutsceneDefinition '{cutsceneId}' not found.");
                return false;
            }

            _activeDef = def;
            _completionFired = false;
            _activeRoutine = StartCoroutine(PrepareAndPlayRoutine(def));
            return true;
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

            var existingScene = SceneManager.GetSceneByName(def.targetSceneName);
            if (existingScene.IsValid() && existingScene.isLoaded)
            {
                DisableAllScenePlayback(existingScene);
                var oldUnload = SceneManager.UnloadSceneAsync(existingScene);
                if (oldUnload != null)
                {
                    while (!oldUnload.isDone)
                        yield return null;
                }
            }

            _loadingSceneName = def.targetSceneName;
            var loadOp = SceneManager.LoadSceneAsync(def.targetSceneName, LoadSceneMode.Additive);
            if (loadOp == null)
            {
                _loadingSceneName = null;
                Fail(def.cutsceneId, $"Failed to start additive load for '{def.targetSceneName}'.");
                yield break;
            }

            _pendingLoadOp = loadOp;
            while (!loadOp.isDone)
                yield return null;
            _pendingLoadOp = null;

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

            foreach (var root in _loadedScene.GetRootGameObjects())
            {
                foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                {
                    if (cam != _activeStage.outputCamera)
                        cam.enabled = false;
                }
            }

            AllocateRenderTexture(def);
            // Impede auto-play (PlayOnAwake) e vazamento para tela principal durante preload
            _activeStage.director.Stop();
            _activeStage.director.timeUpdateMode = def.useUnscaledTime
                ? DirectorUpdateMode.UnscaledGameTime
                : DirectorUpdateMode.GameTime;
            _activeStage.director.time = 0.0;
            _activeStage.director.Evaluate();
            _activeStage.outputCamera.targetTexture = _renderTexture;
            _activeStage.outputCamera.enabled = false;
            _activeStage.outputCamera.Render();

            _state = CutscenePlaybackState.Ready;
            Debug.Log($"[CutscenePlaybackService] Prepared '{def.cutsceneId}' (scene='{def.targetSceneName}').");
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

            if (_activeStage.outputCamera != null)
            {
                _activeStage.outputCamera.targetTexture = _renderTexture;
                _activeStage.outputCamera.enabled = true;
            }

            _savedCursorVisible = Cursor.visible;
            _savedCursorLock = Cursor.lockState;
            Cursor.visible = def.allowSkip;
            Cursor.lockState = def.allowSkip ? CursorLockMode.None : CursorLockMode.Locked;

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
            overlayPresenter.SetAlpha(1f);

            if (_activeStage == null || _activeStage.director == null)
            {
                FinishSession(skipped: true);
                yield break;
            }

            if (def.disableGameplay3DRenderingWhenOpaque)
            {
                var mainCam = Camera.main;
                if (mainCam != null && _activeStage != null && mainCam != _activeStage.outputCamera && mainCam.enabled)
                {
                    _disabledGameplayCam = mainCam;
                    _disabledGameplayCam.enabled = false;
                }
            }

            double duration = def.durationSeconds > 0f ? def.durationSeconds : _activeStage.director.duration;
            if (duration <= 0.05) duration = 3.0;
            Debug.Log($"[CutscenePlaybackService] Playing '{def.cutsceneId}' (duration={duration:F2}s, allowSkip={def.allowSkip}).");

            _activeStage.director.Play();
            Started?.Invoke(def.cutsceneId);

            // Aguarda 1 frame para o PlayableDirector entrar em PlayState.Playing antes de monitorar o término
            yield return null;

            double elapsedPlayTime = 0.0;
            while (_state == CutscenePlaybackState.Playing &&
                   _activeStage != null &&
                   _activeStage.director != null &&
                   _activeStage.director.time < duration - 0.02 &&
                   elapsedPlayTime < duration + 0.15)
            {
                if (elapsedPlayTime > 0.25 && _activeStage.director.state != PlayState.Playing)
                    break;

                elapsedPlayTime += Time.unscaledDeltaTime;
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
            if (_cleaningUp || _state == CutscenePlaybackState.Finishing || _state == CutscenePlaybackState.Idle)
                return;

            _cleaningUp = true;
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
            _cleaningUp = true;

            // Desativa imediatamente a câmera/stage da cutscene antes de desconectar a RenderTexture
            // para impedir que a CutsceneOutputCamera renderize direto na tela principal.
            if (_activeStage != null)
            {
                if (_activeStage.outputCamera != null)
                {
                    _activeStage.outputCamera.enabled = false;
                    _activeStage.outputCamera.targetTexture = null;
                }
                if (_activeStage.director != null)
                {
                    _activeStage.director.Stop();
                }
                _activeStage.gameObject.SetActive(false);
            }

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

            if (overlayPresenter != null && _renderTexture != null)
            {
                float fadeOut = Mathf.Max(0.45f, _activeDef != null ? _activeDef.fadeOutDuration : 0.45f);
                float elapsed = 0f;
                while (elapsed < fadeOut)
                {
                    elapsed += Time.unscaledDeltaTime;
                    overlayPresenter.SetAlpha(1f - Mathf.Clamp01(elapsed / fadeOut));
                    yield return null;
                }
            }

            overlayPresenter.Hide();

            Cursor.visible = _savedCursorVisible;
            Cursor.lockState = _savedCursorLock;

            if (_pendingLoadOp != null && !_pendingLoadOp.isDone)
            {
                while (!_pendingLoadOp.isDone)
                    yield return null;
            }
            _pendingLoadOp = null;

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
                _renderTexture = null;
            }

            Scene sceneToUnload = _loadedScene;
            if (!sceneToUnload.IsValid() && !string.IsNullOrEmpty(_loadingSceneName))
            {
                sceneToUnload = SceneManager.GetSceneByName(_loadingSceneName);
            }

            bool sceneUnloaded = false;
            if (sceneToUnload.IsValid() && sceneToUnload.isLoaded)
            {
                DisableAllScenePlayback(sceneToUnload);
                var unloadOp = SceneManager.UnloadSceneAsync(sceneToUnload);
                if (unloadOp != null)
                {
                    while (!unloadOp.isDone)
                        yield return null;
                    sceneUnloaded = true;
                }
            }

            _pendingLoadOp = null;
            _loadingSceneName = null;
            _loadedScene = default;
            _activeStage = null;
            _activeDef = null;
            _cleaningUp = false;
            _state = CutscenePlaybackState.Idle;

            Debug.Log($"[CutscenePlaybackService] Cleanup concluído para '{id}' (skipped={skipped}, sceneUnloaded={sceneUnloaded}).");

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
            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
                _activeRoutine = null;
            }
            _state = CutscenePlaybackState.Failed;
            _completionFired = true;
            Failed?.Invoke(id, reason);
            if (!_cleaningUp)
            {
                StartCoroutine(CleanupRoutine(id, skipped: true));
            }
        }

        private static void DisableAllScenePlayback(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null) continue;
                foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                {
                    cam.enabled = false;
                    cam.targetTexture = null;
                }
                foreach (var dir in root.GetComponentsInChildren<PlayableDirector>(true))
                {
                    dir.Stop();
                    dir.enabled = false;
                }
                root.SetActive(false);
            }
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
