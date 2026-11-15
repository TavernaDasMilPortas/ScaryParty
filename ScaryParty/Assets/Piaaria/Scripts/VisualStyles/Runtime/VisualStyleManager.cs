using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ScaryParty.VisualStyles
{
    /// <summary>
    /// Scene-scoped cosmetic coordinator providing serialized active style profile,
    /// VisualMaterialLibrary, and O(1) VisualMaterialResolver to gameplay factories without AssetDatabase.
    /// </summary>
    [DefaultExecutionOrder(250)]
    [DisallowMultipleComponent]
    public class VisualStyleManager : MonoBehaviour
    {
        public static VisualStyleManager Active { get; private set; }
        public static event Action<VisualStyleManager> OnActiveManagerChanged;
        private static bool _interactionHighlightActive;

        [Header("Style Assets (Serialized References)")]
        public VisualStyleProfile activeProfile;
        public VisualMaterialLibrary activeLibrary;
        public ScriptableRendererData variantRendererData;
        public RenderPipelineAsset variantRenderPipelineAsset;

        [Header("Behavior")]
        public bool applySceneLightingOnAwake = true;
        public bool applyToPreplacedRenderersOnAwake = false;

        public event Action<VisualStyleManager> OnStyleChanged;

        private readonly Dictionary<Renderer, Material[]> _baselineOriginals = new Dictionary<Renderer, Material[]>();
        private bool _hasBaselineSceneLighting;
        private UnityEngine.Rendering.AmbientMode _baselineAmbientMode;
        private Color _baselineSkyColor;
        private Color _baselineEquatorColor;
        private Color _baselineGroundColor;
        private float _baselineAmbientIntensity;
        private Light _baselineSun;
        private Color _baselineSunColor;
        private float _baselineSunIntensity;

        private VisualMaterialResolver _resolver;
        private RenderPipelineAsset _previousQualityRenderPipeline;
        private bool _appliedQualityRenderPipeline;
        private FullScreenPassRendererFeature _cachedScreenSpaceOutlineFeature;
        private bool _initialFeatureActive = true;
        private bool _hasCachedFeatureState;
        private bool _lastAppliedFeatureActive;

        public static void SetInteractionHighlightActive(bool active)
        {
            _interactionHighlightActive = active;
        }

        public static bool IsInteractionHighlightActive => _interactionHighlightActive;

        public VisualMaterialResolver Resolver
        {
            get
            {
                if (_resolver == null && activeLibrary != null)
                {
                    _resolver = new VisualMaterialResolver(activeProfile, activeLibrary);
                }
                return _resolver;
            }
        }

        public static bool HasActiveStyle => Active != null && Active.activeLibrary != null;

        private void Awake()
        {
            RegisterAsActive();
            RebuildResolver();

            if (applySceneLightingOnAwake && activeProfile != null)
            {
                ApplySceneLighting();
            }

            if (applyToPreplacedRenderersOnAwake && Resolver != null)
            {
                Resolver.ApplyToHierarchy(gameObject, null, VisualSemanticRole.Unclassified, VisualCategoryIds.Props, true, true);
            }
        }

        private void OnEnable()
        {
            if (Active == null)
            {
                RegisterAsActive();
            }

            ApplyRuntimeRenderPipelineIfNeeded();
            CacheScreenSpaceOutlineFeature();
            SyncScreenSpaceOutlineFeature(force: true);
        }

        private void OnDisable()
        {
            RestoreScreenSpaceOutlineFeature();
            RestoreRuntimeRenderPipelineIfNeeded();
        }

        private void OnDestroy()
        {
            RestoreAllSessionVisualState();
            if (Active == this)
            {
                Active = null;
                _interactionHighlightActive = false;
                OnActiveManagerChanged?.Invoke(null);
            }
        }

        private void LateUpdate()
        {
            SyncScreenSpaceOutlineFeature(force: false);
        }

        public void RegisterAsActive()
        {
            Active = this;
            OnActiveManagerChanged?.Invoke(this);
        }

        public void RestoreAllSessionVisualState()
        {
            if (_resolver != null)
            {
                _resolver.RestoreRecordedOriginals(clearEntries: true);
            }
            else if (_baselineOriginals.Count > 0)
            {
                foreach (var kvp in _baselineOriginals)
                {
                    if (kvp.Key != null && kvp.Value != null)
                    {
                        kvp.Key.sharedMaterials = kvp.Value;
                    }
                }
                _baselineOriginals.Clear();
            }

            RestoreSceneLightingBaseline();
            RestoreScreenSpaceOutlineFeature();
            RestoreRuntimeRenderPipelineIfNeeded();
            ScaryParty.Lighting.LightingShaderGlobals.ResetGlobals();
        }

        private void ApplyRuntimeRenderPipelineIfNeeded()
        {
            if (!Application.isPlaying || variantRenderPipelineAsset == null) return;
            if (!_appliedQualityRenderPipeline)
            {
                _previousQualityRenderPipeline = QualitySettings.renderPipeline;
                _appliedQualityRenderPipeline = true;
            }

            if (QualitySettings.renderPipeline != variantRenderPipelineAsset)
            {
                QualitySettings.renderPipeline = variantRenderPipelineAsset;
            }
        }

        private void RestoreRuntimeRenderPipelineIfNeeded()
        {
            if (!_appliedQualityRenderPipeline) return;
            QualitySettings.renderPipeline = _previousQualityRenderPipeline;
            _previousQualityRenderPipeline = null;
            _appliedQualityRenderPipeline = false;
        }

        private void CacheScreenSpaceOutlineFeature()
        {
            _cachedScreenSpaceOutlineFeature = null;
            _hasCachedFeatureState = false;
            if (variantRendererData == null || variantRendererData.rendererFeatures == null) return;

            for (int i = 0; i < variantRendererData.rendererFeatures.Count; i++)
            {
                if (variantRendererData.rendererFeatures[i] is FullScreenPassRendererFeature fsp)
                {
                    _cachedScreenSpaceOutlineFeature = fsp;
                    _initialFeatureActive = fsp.isActive;
                    _hasCachedFeatureState = true;
                    break;
                }
            }
        }

        private void SyncScreenSpaceOutlineFeature(bool force)
        {
            if (_cachedScreenSpaceOutlineFeature == null) return;

            if (activeProfile != null && _cachedScreenSpaceOutlineFeature.passMaterial != null)
            {
                VisualStyleShaderUtility.ApplyOutlineSettingsToPassMaterial(
                    _cachedScreenSpaceOutlineFeature.passMaterial,
                    activeProfile.outlines);
            }

            bool desired = activeProfile != null &&
                           activeProfile.outlines.outlineMode == VisualStyleOutlineMode.ScreenSpace &&
                           !(activeProfile.outlines.suppressOutlineOnInteractionFocus && _interactionHighlightActive);

            if (force || _lastAppliedFeatureActive != desired || _cachedScreenSpaceOutlineFeature.isActive != desired)
            {
                _cachedScreenSpaceOutlineFeature.SetActive(desired);
                _lastAppliedFeatureActive = desired;
            }
        }

        private void RestoreScreenSpaceOutlineFeature()
        {
            if (_cachedScreenSpaceOutlineFeature != null && _hasCachedFeatureState)
            {
                _cachedScreenSpaceOutlineFeature.SetActive(_initialFeatureActive);
            }
        }

        public void RebuildResolver()
        {
            if (activeLibrary != null)
            {
                activeLibrary.EnsureLookupInitialized();
                _resolver = new VisualMaterialResolver(activeProfile, activeLibrary, _baselineOriginals);
            }
            else
            {
                _resolver = null;
            }
        }

        public void SetStyle(VisualStyleProfile profile, VisualMaterialLibrary library, bool applySceneLighting = true)
        {
            SetStyle(profile, library, applySceneLighting, applyToLoadedSceneRenderers: false);
        }

        public void SetStyle(VisualStyleProfile profile, VisualMaterialLibrary library, bool applySceneLighting, bool applyToLoadedSceneRenderers)
        {
            if (profile == null || library == null)
            {
                if (_resolver != null)
                {
                    _resolver.RestoreRecordedOriginals(clearEntries: false);
                }
                else
                {
                    foreach (var kvp in _baselineOriginals)
                    {
                        if (kvp.Key != null && kvp.Value != null)
                        {
                            kvp.Key.sharedMaterials = kvp.Value;
                        }
                    }
                }

                activeProfile = null;
                activeLibrary = null;
                _resolver = null;
                RestoreSceneLightingBaseline();
                RestoreScreenSpaceOutlineFeature();
                RestoreRuntimeRenderPipelineIfNeeded();
                ScaryParty.Lighting.LightingShaderGlobals.ResetGlobals();
                OnStyleChanged?.Invoke(this);
                return;
            }

            activeProfile = profile;
            activeLibrary = library;

            if (library.renderPipelineAsset != null)
            {
                variantRenderPipelineAsset = library.renderPipelineAsset;
            }
            if (library.renderPipelineRendererData is ScriptableRendererData libSrd)
            {
                variantRendererData = libSrd;
            }

            ApplyRuntimeRenderPipelineIfNeeded();
            CacheScreenSpaceOutlineFeature();
            SyncScreenSpaceOutlineFeature(force: true);

            RebuildResolver();
            EvaluateLightingContract();

            if (applySceneLighting && activeProfile != null)
            {
                ApplySceneLighting();
            }

            _resolver?.ReapplyAllRecorded();

            if (applyToLoadedSceneRenderers)
            {
                ScanAndApplyToActiveScene();
            }

            OnStyleChanged?.Invoke(this);
        }

        public void ScanAndApplyToActiveScene()
        {
            if (_resolver == null) return;
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded) return;

            GameObject[] roots = activeScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null || root == gameObject) continue;
                _resolver.ApplyToHierarchy(root, null, VisualSemanticRole.Unclassified, VisualCategoryIds.Props, includeInactive: true, recordOriginal: true);
            }
        }

        public void CaptureSceneLightingBaselineIfNeeded()
        {
            if (_hasBaselineSceneLighting) return;
            _baselineAmbientMode = RenderSettings.ambientMode;
            _baselineSkyColor = RenderSettings.ambientSkyColor;
            _baselineEquatorColor = RenderSettings.ambientEquatorColor;
            _baselineGroundColor = RenderSettings.ambientGroundColor;
            _baselineAmbientIntensity = RenderSettings.ambientIntensity;
            _baselineSun = FindDirectionalSunLight();
            if (_baselineSun != null)
            {
                _baselineSunColor = _baselineSun.color;
                _baselineSunIntensity = _baselineSun.intensity;
            }
            _hasBaselineSceneLighting = true;
        }

        public void RestoreSceneLightingBaseline()
        {
            if (!_hasBaselineSceneLighting) return;
            RenderSettings.ambientMode = _baselineAmbientMode;
            RenderSettings.ambientSkyColor = _baselineSkyColor;
            RenderSettings.ambientEquatorColor = _baselineEquatorColor;
            RenderSettings.ambientGroundColor = _baselineGroundColor;
            RenderSettings.ambientIntensity = _baselineAmbientIntensity;
            if (_baselineSun != null)
            {
                _baselineSun.color = _baselineSunColor;
                _baselineSun.intensity = _baselineSunIntensity;
            }
        }

        private void EvaluateLightingContract()
        {
            if (activeProfile == null) return;
            var cfg = VisualStyleRuntimeConfig.LoadCached();
            var ctx = new ScaryParty.Lighting.LightingContext
            {
                sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                activeStyleId = activeProfile.styleId,
                requestedBackend = cfg != null ? cfg.lightingBackendMode : ScaryParty.Lighting.LightingBackendMode.RealtimeStylized,
                debugView = ScaryParty.Lighting.LightingFeatureRegistry.ActiveDebugView,
                environment = new ScaryParty.Lighting.LightingEnvironmentState
                {
                    minShadowBrightness = activeProfile.lighting.minShadowBrightness,
                    shadowLift = activeProfile.lighting.shadowLift,
                    indirectDiffuseWeight = activeProfile.lighting.indirectDiffuseWeight,
                    ambientSkyColor = activeProfile.sceneLighting.ambientSkyColor,
                    ambientEquatorColor = activeProfile.sceneLighting.ambientEquatorColor,
                    ambientGroundColor = activeProfile.sceneLighting.ambientGroundColor,
                    ambientIntensity = activeProfile.sceneLighting.ambientIntensity,
                    sunColor = activeProfile.sceneLighting.directionalLightColor,
                    sunIntensity = activeProfile.sceneLighting.directionalLightIntensity,
                    stylizedProfileActive = true
                }
            };
            ScaryParty.Lighting.LightingFeatureRegistry.Evaluate(ctx);
        }

        private static Light FindDirectionalSunLight()
        {
            Light sun = RenderSettings.sun;
            if (sun != null) return sun;
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                {
                    return lights[i];
                }
            }
            return null;
        }

        public void ApplySceneLighting()
        {
            if (activeProfile == null || !activeProfile.sceneLighting.overrideSceneLightingInVariant) return;

            CaptureSceneLightingBaselineIfNeeded();
            EvaluateLightingContract();

            // Se o TimeOfDayLightingDirector estiver gerenciando o ciclo dia/noite ativo, ele compoe a luz direcional e o ambiente.
            var tod = ScaryParty.Lighting.TimeOfDayLightingDirector.Active;
            if (tod != null && tod.IsActivelyControllingScene)
            {
                tod.ApplyStyleModulation(activeProfile);
                return;
            }

            var s = activeProfile.sceneLighting;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = s.ambientSkyColor;
            RenderSettings.ambientEquatorColor = s.ambientEquatorColor;
            RenderSettings.ambientGroundColor = s.ambientGroundColor;
            RenderSettings.ambientIntensity = s.ambientIntensity;

            Light sun = FindDirectionalSunLight();
            if (sun != null)
            {
                sun.color = s.directionalLightColor;
                sun.intensity = s.directionalLightIntensity;
            }
        }

        public Material ResolveRoleMaterial(VisualSemanticRole role, Material fallback = null)
        {
            if (Resolver == null) return fallback;
            Material mat = Resolver.ResolveRoleMaterial(role);
            return mat != null ? mat : fallback;
        }

        public void ApplyToVisual(GameObject visualRoot, string contextKey = null, VisualSemanticRole defaultRole = VisualSemanticRole.Unclassified, string categoryHint = VisualCategoryIds.Props)
        {
            if (visualRoot == null || Resolver == null) return;
            Resolver.ApplyToHierarchy(visualRoot, contextKey, defaultRole, categoryHint, includeInactive: true, recordOriginal: true);
        }

        public static Material ResolveMaterialOrFallback(
            Material current,
            VisualSemanticRole role,
            string categoryId,
            Func<Material> fallbackCreator = null)
        {
            var mgr = Active;
            if (mgr != null && mgr.Resolver != null)
            {
                Material resolved = mgr.Resolver.ResolveForSlot(
                    current, role, categoryId, null, 0,
                    out _, out _, out _);
                if (resolved != null) return resolved;
            }

            return current != null ? current : fallbackCreator?.Invoke();
        }

        public static void ApplyHierarchyOrFallback(
            GameObject root,
            VisualSemanticRole defaultRole,
            string categoryId,
            string contextKey = null)
        {
            if (root == null) return;
            if (Application.isPlaying)
            {
                EnsureRuntimeBinder(root, defaultRole, categoryId, contextKey);
            }

            var mgr = Active;
            if (mgr != null && mgr.Resolver != null)
            {
                mgr.Resolver.ApplyToHierarchy(root, contextKey, defaultRole, categoryId, includeInactive: true, recordOriginal: true);
            }
        }

        public static VisualStyleBinder EnsureRuntimeBinder(
            GameObject root,
            VisualSemanticRole defaultRole,
            string categoryId,
            string contextKey = null)
        {
            if (root == null) return null;
            VisualStyleBinder binder = root.GetComponent<VisualStyleBinder>();
            if (binder == null)
            {
                binder = root.AddComponent<VisualStyleBinder>();
                binder.defaultRole = defaultRole;
                binder.defaultCategoryId = categoryId;
                binder.contextKey = contextKey;
                binder.applyOnAwake = true;
            }
            return binder;
        }
    }
}
