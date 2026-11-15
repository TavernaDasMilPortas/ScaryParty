using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.Lighting
{
    /// <summary>
    /// Backend ativo para contribuicao de iluminacao (A = RealtimeStylized, futuro B = HybridPrepared / BakedPrecomputedFuture).
    /// </summary>
    public enum LightingBackendMode
    {
        RealtimeStylized = 0,
        HybridPrepared = 1,
        BakedPrecomputedFuture = 2
    }

    /// <summary>
    /// Modos tecnicos de isolamento de iluminacao para diagnostico visual.
    /// </summary>
    public enum LightingDebugViewMode
    {
        Standard = 0,
        UnlitAlbedo = 1,
        MainLightOnly = 2,
        AdditionalLightsOnly = 3,
        IndirectAndAmbientOnly = 4,
        FinalToonBands = 5
    }

    public enum LightingSpaceType
    {
        ExteriorCity = 0,
        PizzeriaKitchen = 1,
        PizzeriaCounter = 2,
        PizzeriaDining = 3,
        InteriorGeneric = 4
    }

    public enum PizzeriaZoneId
    {
        None = 0,
        Kitchen = 1,
        Counter = 2,
        Dining = 3
    }

    [Serializable]
    public struct LightingEnvironmentState
    {
        [Range(0f, 24f)] public float clockHours;
        [Range(0f, 1f)] public float normalizedDayProgress;
        public Vector3 sunDirection;
        public Color sunColor;
        public float sunIntensity;
        public Color ambientSkyColor;
        public Color ambientEquatorColor;
        public Color ambientGroundColor;
        public float ambientIntensity;
        public Color fogColor;
        public float fogDensity;
        public float minShadowBrightness;
        public float shadowLift;
        public float indirectDiffuseWeight;
        public Color pizzeriaDefaultFillColor;
        public float pizzeriaDefaultFillWeight;
        public bool stylizedProfileActive;
    }

    [Serializable]
    public class LightingContext
    {
        public string sceneName;
        public string activeStyleId;
        public LightingBackendMode requestedBackend = LightingBackendMode.RealtimeStylized;
        public LightingBackendMode resolvedBackend = LightingBackendMode.RealtimeStylized;
        public LightingDebugViewMode debugView = LightingDebugViewMode.Standard;
        public Vector3 cameraPosition;
        public int qualityTier = 1; // 0 = Low, 1 = Medium, 2 = High
        public bool isMultiplayerActive;
        public bool isServer;
        public bool hasPrecomputedCityData;
        public bool hasPrecomputedInteriorData;
        public LightingEnvironmentState environment;
    }

    public interface ILightingContributionProvider
    {
        string ProviderId { get; }
        int Priority { get; }
        LightingBackendMode SupportedBackend { get; }
        bool IsAvailable(LightingContext context);
        void Prepare(LightingContext context);
        void Evaluate(LightingContext context);
        void ApplyToTarget(Renderer renderer, MaterialPropertyBlock block, LightingContext context);
        void Dispose();
    }

    /// <summary>
    /// Identificadores de propriedades globais e por-instancia de shader compartilhadas entre A e futuro B.
    /// </summary>
    public static class LightingShaderGlobals
    {
        public static readonly int DebugLightingViewId = Shader.PropertyToID("_SP_DebugLightingView");
        public static readonly int DayNightBlendId = Shader.PropertyToID("_SP_DayNightBlend");
        public static readonly int GlobalShadowFloorId = Shader.PropertyToID("_SP_GlobalShadowFloor");
        public static readonly int GlobalShadowLiftId = Shader.PropertyToID("_SP_GlobalShadowLift");
        public static readonly int GlobalIndirectWeightId = Shader.PropertyToID("_SP_GlobalIndirectWeight");
        public static readonly int SkyTintColorId = Shader.PropertyToID("_SP_SkyTintColor");

        public static readonly int MinShadowBrightnessId = Shader.PropertyToID("_MinShadowBrightness");
        public static readonly int ShadowLiftId = Shader.PropertyToID("_ShadowLift");
        public static readonly int IndirectDiffuseWeightId = Shader.PropertyToID("_IndirectDiffuseWeight");
        public static readonly int AdditionalLightsWeightId = Shader.PropertyToID("_AdditionalLightsWeight");
        public static readonly int MaxAdditionalLightContributionId = Shader.PropertyToID("_MaxAdditionalLightContribution");

        public static readonly int InteriorZoneFillColorId = Shader.PropertyToID("_InteriorZoneFillColor");
        public static readonly int InteriorZoneWeightId = Shader.PropertyToID("_InteriorZoneWeight");

        public static readonly int AoTextureId = Shader.PropertyToID("_AOTexture");
        public static readonly int AoStrengthId = Shader.PropertyToID("_AOStrength");
        public static readonly int UseVertexAoId = Shader.PropertyToID("_UseVertexAO");
        public static readonly int EmissionMaskId = Shader.PropertyToID("_EmissionMask");
        public static readonly int EmissionMaskStrengthId = Shader.PropertyToID("_EmissionMaskStrength");
        public static readonly int EmissionMaskColorId = Shader.PropertyToID("_EmissionMaskColor");

        public static void SetDebugView(LightingDebugViewMode mode)
        {
            Shader.SetGlobalFloat(DebugLightingViewId, (float)(int)mode);
        }

        public static void ResetGlobals()
        {
            Shader.SetGlobalFloat(DebugLightingViewId, 0f);
            Shader.SetGlobalFloat(DayNightBlendId, 0f);
            Shader.SetGlobalFloat(GlobalShadowFloorId, 0f);
            Shader.SetGlobalFloat(GlobalShadowLiftId, 0f);
            Shader.SetGlobalFloat(GlobalIndirectWeightId, 1f);
            Shader.SetGlobalColor(SkyTintColorId, Color.white);
        }
    }

    /// <summary>
    /// Provedor padrao da Solucao A: iluminacao cartoon em tempo real com piso indireto, zonas e culling.
    /// </summary>
    public sealed class RealtimeStylizedProvider : ILightingContributionProvider
    {
        public string ProviderId => "realtime_stylized_v1";
        public int Priority => 10;
        public LightingBackendMode SupportedBackend => LightingBackendMode.RealtimeStylized;

        public bool IsAvailable(LightingContext context) => true;

        public void Prepare(LightingContext context)
        {
        }

        public void Evaluate(LightingContext context)
        {
            if (context == null) return;
            Shader.SetGlobalFloat(LightingShaderGlobals.DebugLightingViewId, (float)(int)context.debugView);
            Shader.SetGlobalFloat(LightingShaderGlobals.DayNightBlendId, context.environment.normalizedDayProgress);
            Shader.SetGlobalFloat(LightingShaderGlobals.GlobalShadowFloorId, context.environment.minShadowBrightness);
            Shader.SetGlobalFloat(LightingShaderGlobals.GlobalShadowLiftId, context.environment.shadowLift);
            Shader.SetGlobalFloat(LightingShaderGlobals.GlobalIndirectWeightId, context.environment.indirectDiffuseWeight);
            Shader.SetGlobalColor(LightingShaderGlobals.SkyTintColorId, context.environment.ambientSkyColor);
        }

        public void ApplyToTarget(Renderer renderer, MaterialPropertyBlock block, LightingContext context)
        {
            if (block == null || context == null) return;
            if (context.environment.pizzeriaDefaultFillWeight > 0.001f)
            {
                block.SetColor(LightingShaderGlobals.InteriorZoneFillColorId, context.environment.pizzeriaDefaultFillColor);
                block.SetFloat(LightingShaderGlobals.InteriorZoneWeightId, context.environment.pizzeriaDefaultFillWeight);
            }
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Contrato vazio de B3 para testar validacao e fallback seguro quando dados pre-calculados nao existem.
    /// </summary>
    public sealed class FuturePrecomputedProviderStub : ILightingContributionProvider
    {
        public string ProviderId => "future_precomputed_b3_stub";
        public int Priority => 50;
        public LightingBackendMode SupportedBackend { get; }

        public FuturePrecomputedProviderStub(LightingBackendMode backend = LightingBackendMode.HybridPrepared)
        {
            SupportedBackend = backend;
        }

        public bool IsAvailable(LightingContext context)
        {
            return context != null && (context.hasPrecomputedCityData || context.hasPrecomputedInteriorData);
        }

        public void Prepare(LightingContext context) { }
        public void Evaluate(LightingContext context) { }
        public void ApplyToTarget(Renderer renderer, MaterialPropertyBlock block, LightingContext context) { }
        public void Dispose() { }
    }

    /// <summary>
    /// Registro central de provedores de iluminacao que permite trocar A para B com fallback garantido para A.
    /// </summary>
    public static class LightingFeatureRegistry
    {
        private static readonly List<ILightingContributionProvider> _providers = new List<ILightingContributionProvider>();
        private static readonly RealtimeStylizedProvider _defaultRealtime = new RealtimeStylizedProvider();
        private static LightingDebugViewMode _activeDebugView = LightingDebugViewMode.Standard;

        public static LightingDebugViewMode ActiveDebugView
        {
            get => _activeDebugView;
            set
            {
                _activeDebugView = value;
                LightingShaderGlobals.SetDebugView(value);
            }
        }

        static LightingFeatureRegistry()
        {
            RegisterProvider(_defaultRealtime);
        }

        public static IReadOnlyList<ILightingContributionProvider> Providers => _providers;

        public static void RegisterProvider(ILightingContributionProvider provider)
        {
            if (provider == null) return;
            for (int i = 0; i < _providers.Count; i++)
            {
                if (_providers[i].ProviderId == provider.ProviderId)
                {
                    _providers[i] = provider;
                    SortProviders();
                    return;
                }
            }
            _providers.Add(provider);
            SortProviders();
        }

        public static void UnregisterProvider(string providerId)
        {
            if (string.IsNullOrEmpty(providerId) || providerId == _defaultRealtime.ProviderId) return;
            for (int i = _providers.Count - 1; i >= 0; i--)
            {
                if (_providers[i].ProviderId == providerId)
                {
                    _providers[i].Dispose();
                    _providers.RemoveAt(i);
                }
            }
        }

        public static void ResetToDefaultRealtimeOnly()
        {
            for (int i = _providers.Count - 1; i >= 0; i--)
            {
                if (_providers[i].ProviderId != _defaultRealtime.ProviderId)
                {
                    _providers[i].Dispose();
                    _providers.RemoveAt(i);
                }
            }
            if (!_providers.Contains(_defaultRealtime))
            {
                _providers.Add(_defaultRealtime);
            }
            ActiveDebugView = LightingDebugViewMode.Standard;
        }

        public static ILightingContributionProvider ResolveProvider(
            LightingBackendMode requestedBackend,
            LightingContext context,
            out LightingBackendMode resolvedBackend,
            out string warningMessage)
        {
            warningMessage = null;

            if (requestedBackend == LightingBackendMode.RealtimeStylized)
            {
                resolvedBackend = LightingBackendMode.RealtimeStylized;
                return _defaultRealtime;
            }

            // Procura provedor compativel e disponivel para modo hibrido ou pre-calculado.
            for (int i = _providers.Count - 1; i >= 0; i--)
            {
                ILightingContributionProvider p = _providers[i];
                if (p != null && p.SupportedBackend == requestedBackend && p.IsAvailable(context))
                {
                    resolvedBackend = p.SupportedBackend;
                    return p;
                }
            }

            // B3 Fallback obrigatorio para RealtimeStylized sem dados pre-calculados validos.
            resolvedBackend = LightingBackendMode.RealtimeStylized;
            warningMessage = $"Backend '{requestedBackend}' sem provedor/dados validos; mantendo fallback seguro em RealtimeStylized.";
            if (context != null)
            {
                context.resolvedBackend = resolvedBackend;
            }
            return _defaultRealtime;
        }

        public static void Evaluate(LightingContext context)
        {
            if (context == null) return;
            context.debugView = _activeDebugView;
            ILightingContributionProvider active = ResolveProvider(context.requestedBackend, context, out LightingBackendMode resolved, out _);
            context.resolvedBackend = resolved;
            active?.Prepare(context);
            active?.Evaluate(context);
        }

        private static void SortProviders()
        {
            _providers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }
    }
}
