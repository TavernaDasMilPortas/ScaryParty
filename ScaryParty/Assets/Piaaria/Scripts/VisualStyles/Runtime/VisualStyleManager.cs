using System;
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
            RestoreScreenSpaceOutlineFeature();
            RestoreRuntimeRenderPipelineIfNeeded();
            if (Active == this)
            {
                Active = null;
                _interactionHighlightActive = false;
            }
        }

        private void LateUpdate()
        {
            SyncScreenSpaceOutlineFeature(force: false);
        }

        public void RegisterAsActive()
        {
            Active = this;
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
                _resolver = new VisualMaterialResolver(activeProfile, activeLibrary);
            }
            else
            {
                _resolver = null;
            }
        }

        public void SetStyle(VisualStyleProfile profile, VisualMaterialLibrary library, bool applySceneLighting = true)
        {
            if (_resolver != null)
            {
                _resolver.RestoreRecordedOriginals();
            }

            activeProfile = profile;
            activeLibrary = library;
            RebuildResolver();

            if (applySceneLighting && activeProfile != null)
            {
                ApplySceneLighting();
            }

            OnStyleChanged?.Invoke(this);
        }

        public void ApplySceneLighting()
        {
            if (activeProfile == null || !activeProfile.sceneLighting.overrideSceneLightingInVariant) return;

            var s = activeProfile.sceneLighting;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = s.ambientSkyColor;
            RenderSettings.ambientEquatorColor = s.ambientEquatorColor;
            RenderSettings.ambientGroundColor = s.ambientGroundColor;
            RenderSettings.ambientIntensity = s.ambientIntensity;

            Light sun = RenderSettings.sun;
            if (sun == null)
            {
                var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] != null && lights[i].type == LightType.Directional)
                    {
                        sun = lights[i];
                        break;
                    }
                }
            }

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
            var mgr = Active;
            if (mgr != null && mgr.Resolver != null && root != null)
            {
                mgr.Resolver.ApplyToHierarchy(root, contextKey, defaultRole, categoryId, includeInactive: true, recordOriginal: true);
            }
        }
    }
}
