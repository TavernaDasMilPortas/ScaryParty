using System;
using ScaryParty.Lighting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScaryParty.VisualStyles
{
    /// <summary>
    /// Bootstrap runtime para a GameScene original (Modo A: preview reversivel, Modo B: default persistido,
    /// e Modo C: GameScene_CelShaded), sem depender de UnityEditor nem alterar GameScene.unity em RuntimePreview.
    /// </summary>
    public static class VisualStyleGameplayBootstrap
    {
        public static event Action<Scene, VisualStyleManager, VisualStyleRuntimeConfig> OnSceneLightingBootstrapped;

        private const string BootstrapObjectName = "[VisualStyleRuntimeBootstrap]";
        private static bool _sceneHookRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnSceneLightingBootstrapped = null;
            VisualStyleRuntimeConfig.ClearSessionOverride();
            VisualStyleRuntimeConfig.ClearCache();
            LightingFeatureRegistry.ResetToDefaultRealtimeOnly();
            LightingShaderGlobals.ResetGlobals();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            _sceneHookRegistered = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeSceneLoad()
        {
            if (!_sceneHookRegistered)
            {
                SceneManager.sceneLoaded += HandleSceneLoaded;
                _sceneHookRegistered = true;
            }
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Application.isPlaying) return;
            if (mode == LoadSceneMode.Additive) return;
            EvaluateScene(scene);
        }

        public static void EvaluateActiveScene()
        {
            EvaluateScene(SceneManager.GetActiveScene());
        }

        public static void EvaluateScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            VisualStyleRuntimeConfig cfg = VisualStyleRuntimeConfig.LoadCached();
            if (cfg == null) return;

            bool shouldApply = cfg.ResolveEffectiveStyleForScene(
                scene.name,
                out VisualApplicationMode effectiveMode,
                out VisualStyleProfile profile,
                out VisualMaterialLibrary library);

            if (!shouldApply && VisualStyleManager.Active == null)
            {
                return;
            }

            if (shouldApply)
            {
                VisualStyleManager manager = EnsureManagerInScene(scene, effectiveMode == VisualApplicationMode.RuntimePreview);
                if (library != null)
                {
                    if (library.renderPipelineAsset != null)
                    {
                        manager.variantRenderPipelineAsset = library.renderPipelineAsset;
                    }
                    if (library.renderPipelineRendererData is UnityEngine.Rendering.Universal.ScriptableRendererData srd)
                    {
                        manager.variantRendererData = srd;
                    }
                }

                manager.SetStyle(profile, library, applySceneLighting: true, applyToLoadedSceneRenderers: true);
                EnsureTimeOfDayDirector(manager.gameObject, cfg);
                OnSceneLightingBootstrapped?.Invoke(scene, manager, cfg);
            }
            else if (VisualStyleManager.Active != null)
            {
                VisualStyleManager.Active.SetStyle(null, null, applySceneLighting: true, applyToLoadedSceneRenderers: false);
                OnSceneLightingBootstrapped?.Invoke(scene, VisualStyleManager.Active, cfg);
            }
        }

        public static void ApplyStyleLiveInCurrentScene(VisualStyleProfile profile, VisualMaterialLibrary library, bool asSessionPreview = true)
        {
            if (!Application.isPlaying) return;

            if (profile == null || library == null)
            {
                RestoreOriginalInCurrentScene(clearSessionOverride: asSessionPreview);
                return;
            }

            if (asSessionPreview)
            {
                VisualStyleRuntimeConfig.SetRuntimePreviewOverride(profile, library, VisualApplicationMode.RuntimePreview);
            }

            Scene activeScene = SceneManager.GetActiveScene();
            VisualStyleManager manager = EnsureManagerInScene(activeScene, transient: asSessionPreview);
            if (library.renderPipelineAsset != null)
            {
                manager.variantRenderPipelineAsset = library.renderPipelineAsset;
            }
            if (library.renderPipelineRendererData is UnityEngine.Rendering.Universal.ScriptableRendererData srd)
            {
                manager.variantRendererData = srd;
            }

            manager.SetStyle(profile, library, applySceneLighting: true, applyToLoadedSceneRenderers: true);

            VisualStyleRuntimeConfig cfg = VisualStyleRuntimeConfig.LoadCached();
            EnsureTimeOfDayDirector(manager.gameObject, cfg);
            OnSceneLightingBootstrapped?.Invoke(activeScene, manager, cfg);
        }

        public static void RestoreOriginalInCurrentScene(bool clearSessionOverride = true)
        {
            if (clearSessionOverride)
            {
                VisualStyleRuntimeConfig.SetRuntimePreviewOverride(null, null, VisualApplicationMode.Disabled);
            }

            LightingShaderGlobals.ResetGlobals();

            if (VisualStyleManager.Active != null)
            {
                VisualStyleManager.Active.SetStyle(null, null, applySceneLighting: true, applyToLoadedSceneRenderers: false);
            }

            TimeOfDayLightingDirector tod = TimeOfDayLightingDirector.Active;
            if (tod != null)
            {
                tod.RestoreBaselineIfCaptured();
            }

            OnSceneLightingBootstrapped?.Invoke(SceneManager.GetActiveScene(), VisualStyleManager.Active, VisualStyleRuntimeConfig.LoadCached());
        }

        private static VisualStyleManager EnsureManagerInScene(Scene scene, bool transient)
        {
            if (VisualStyleManager.Active != null)
            {
                return VisualStyleManager.Active;
            }

            VisualStyleManager existing = UnityEngine.Object.FindAnyObjectByType<VisualStyleManager>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.RegisterAsActive();
                return existing;
            }

            var go = new GameObject(BootstrapObjectName);
            if (transient)
            {
                go.hideFlags = HideFlags.DontSave;
            }
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(go, scene);
            }
            return go.AddComponent<VisualStyleManager>();
        }

        private static void EnsureTimeOfDayDirector(GameObject hostGo, VisualStyleRuntimeConfig cfg)
        {
            if (hostGo == null) return;
            TimeOfDayLightingDirector tod = UnityEngine.Object.FindAnyObjectByType<TimeOfDayLightingDirector>(FindObjectsInactive.Include);
            if (tod == null)
            {
                tod = hostGo.AddComponent<TimeOfDayLightingDirector>();
            }
            if (cfg != null)
            {
                float startHour = VisualStyleRuntimeConfig.GetSessionTimeOverride() ?? cfg.initialTimeOfDayHours;
                tod.ConfigureFromRuntimeConfig(cfg.enableDayNightCycle, startHour, cfg.lockTimeOfDayForTests);
            }
        }
    }
}
