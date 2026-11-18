using System;
using ScaryParty.VisualStyles;
using UnityEngine;
using UnityEngine.Rendering;

namespace ScaryParty.Lighting
{
    /// <summary>
    /// Controla o ciclo global Tarde / Crepusculo / Noite / Madrugada sem alocar DynamicGI por frame
    /// nem criar segunda Luz Direcional com sombra.
    /// </summary>
    [DefaultExecutionOrder(260)]
    public sealed class TimeOfDayLightingDirector : MonoBehaviour
    {
        public static TimeOfDayLightingDirector Active { get; private set; }

        [Header("Configuracao do Ciclo")]
        public TimeOfDayProfile profile;
        [Range(0f, 24f)] public float currentClockHours = 17f;
        [Tooltip("Velocidade em horas de jogo por minuto real (0 = congelado).")]
        public float gameHoursPerRealMinute = 0.75f;
        public bool autoAdvance = true;
        public bool lockTimeForTests = false;
        public bool enableControl = true;

        public event Action<LightingEnvironmentState> OnTimeOfDayEvaluated;

        public bool IsActivelyControllingScene =>
            enableControl &&
            (VisualStyleRuntimeConfig.IsStyledGameplayActive() ||
             (VisualStyleManager.Active != null && VisualStyleManager.Active.activeProfile != null));
        public LightingEnvironmentState CurrentState => _currentState;

        private LightingEnvironmentState _currentState;
        private TimeOfDayProfile _runtimeFallbackProfile;
        private Light _sunLight;
        private Material _originalSkyboxAsset;
        private Material _runtimeSkyboxCopy;
        private bool _hasCapturedBaseline;
        private Quaternion _baselineSunRotation;
        private Color _baselineSunColor;
        private float _baselineSunIntensity;
        private Color _baselineFogColor;
        private float _baselineFogDensity;
        private bool _baselineFog;

        private void Awake()
        {
            Active = this;
            EnsureProfileLoaded();
        }

        private void EnsureTestOverlay()
        {
            // Removido na B5.1: controles de horario vivem no PlaytestDebugController (Inspector Odin), sem overlay no GameView.
        }

        private void OnEnable()
        {
            Active = this;
            EnsureProfileLoaded();
            if (IsActivelyControllingScene)
            {
                EvaluateAndApply(currentClockHours);
            }
        }

        private void OnDestroy()
        {
            RestoreBaselineIfCaptured();
            if (_runtimeSkyboxCopy != null)
            {
                Destroy(_runtimeSkyboxCopy);
                _runtimeSkyboxCopy = null;
            }
            if (_runtimeFallbackProfile != null)
            {
                Destroy(_runtimeFallbackProfile);
                _runtimeFallbackProfile = null;
            }
            if (Active == this) Active = null;
        }

        private void Update()
        {
            if (!Application.isPlaying || !IsActivelyControllingScene) return;

            if (autoAdvance && !lockTimeForTests && gameHoursPerRealMinute > 0.0001f)
            {
                currentClockHours = (currentClockHours + (gameHoursPerRealMinute / 60f) * Time.deltaTime) % 24f;
            }

            EvaluateAndApply(currentClockHours);
        }

        public void ConfigureFromRuntimeConfig(bool enableCycle, float startHour, bool lockedForTests)
        {
            autoAdvance = enableCycle;
            lockTimeForTests = lockedForTests;
            currentClockHours = ((startHour % 24f) + 24f) % 24f;
            EnsureProfileLoaded();
            if (IsActivelyControllingScene)
            {
                EvaluateAndApply(currentClockHours);
            }
        }

        public void SetClockHours(float hours, bool evaluateNow = true)
        {
            currentClockHours = ((hours % 24f) + 24f) % 24f;
            VisualStyleRuntimeConfig.SetSessionTimeOverride(currentClockHours);
            if (evaluateNow)
            {
                EvaluateAndApply(currentClockHours);
            }
        }

        public void JumpToPresetAnchor(float anchorHours)
        {
            SetClockHours(anchorHours, evaluateNow: true);
        }

        public void ApplyStyleModulation(VisualStyleProfile activeStyleProfile)
        {
            EvaluateAndApply(currentClockHours, activeStyleProfile);
        }

        public void EvaluateAndApply(float clockHours, VisualStyleProfile styleOverride = null)
        {
            EnsureProfileLoaded();
            if (profile == null) return;

            CaptureBaselineOnce();

            VisualStyleProfile styleProfile = styleOverride != null
                ? styleOverride
                : (VisualStyleManager.Active != null ? VisualStyleManager.Active.activeProfile : null);

            LightingEnvironmentState state = profile.Evaluate(clockHours, _currentState);
            if (styleProfile != null)
            {
                state.minShadowBrightness = Mathf.Max(state.minShadowBrightness, styleProfile.lighting.minShadowBrightness);
                state.shadowLift = Mathf.Max(state.shadowLift, styleProfile.lighting.shadowLift);
                state.indirectDiffuseWeight = styleProfile.lighting.indirectDiffuseWeight;
                state.stylizedProfileActive = true;
            }

            _currentState = state;

            // Aplica apenas na unica Directional Light principal com sombras suaves e cascades controladas
            Light sun = ResolvePrimaryDirectionalLight();
            if (sun != null)
            {
                sun.color = state.sunColor;
                sun.intensity = state.sunIntensity;
                sun.transform.rotation = Quaternion.LookRotation(-state.sunDirection, Vector3.up);
            }

            // Trilight sem disparar DynamicGI.UpdateEnvironment() em runtime
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = state.ambientSkyColor;
            RenderSettings.ambientEquatorColor = state.ambientEquatorColor;
            RenderSettings.ambientGroundColor = state.ambientGroundColor;
            RenderSettings.ambientIntensity = state.ambientIntensity;
            RenderSettings.fogColor = state.fogColor;
            RenderSettings.fogDensity = state.fogDensity;

            UpdateRuntimeSkyboxTint(profile.EvaluateSkyTint(clockHours));

            // Atualiza globais de shader via contrato B0 (RealtimeStylizedProvider)
            var ctx = new LightingContext
            {
                sceneName = gameObject.scene.name,
                activeStyleId = styleProfile != null ? styleProfile.styleId : string.Empty,
                requestedBackend = VisualStyleRuntimeConfig.LoadCached()?.lightingBackendMode ?? LightingBackendMode.RealtimeStylized,
                environment = state
            };
            LightingFeatureRegistry.Evaluate(ctx);

            OnTimeOfDayEvaluated?.Invoke(state);
        }

        public float GetCityNightEmissionWeight()
        {
            EnsureProfileLoaded();
            return profile != null ? profile.EvaluateCityNightEmission(currentClockHours) : 0f;
        }

        public float GetInteriorFillMultiplier()
        {
            EnsureProfileLoaded();
            return profile != null ? profile.EvaluateInteriorFillMultiplier(currentClockHours) : 1f;
        }

        public void RestoreBaselineIfCaptured()
        {
            if (!_hasCapturedBaseline) return;

            if (_sunLight != null)
            {
                _sunLight.color = _baselineSunColor;
                _sunLight.intensity = _baselineSunIntensity;
                _sunLight.transform.rotation = _baselineSunRotation;
            }

            RenderSettings.fog = _baselineFog;
            RenderSettings.fogColor = _baselineFogColor;
            RenderSettings.fogDensity = _baselineFogDensity;

            if (_originalSkyboxAsset != null)
            {
                RenderSettings.skybox = _originalSkyboxAsset;
            }
        }

        private void EnsureProfileLoaded()
        {
            if (profile != null) return;
            profile = Resources.Load<TimeOfDayProfile>("TimeOfDayProfile_Default");
            if (profile == null)
            {
                if (_runtimeFallbackProfile == null)
                {
                    _runtimeFallbackProfile = ScriptableObject.CreateInstance<TimeOfDayProfile>();
                    _runtimeFallbackProfile.hideFlags = HideFlags.DontSave;
                    _runtimeFallbackProfile.ApplyCartoonDefaults();
                }
                profile = _runtimeFallbackProfile;
            }
        }

        private void CaptureBaselineOnce()
        {
            if (_hasCapturedBaseline) return;
            _sunLight = ResolvePrimaryDirectionalLight();
            if (_sunLight != null)
            {
                _baselineSunRotation = _sunLight.transform.rotation;
                _baselineSunColor = _sunLight.color;
                _baselineSunIntensity = _sunLight.intensity;
            }
            _baselineFog = RenderSettings.fog;
            _baselineFogColor = RenderSettings.fogColor;
            _baselineFogDensity = RenderSettings.fogDensity;
            _originalSkyboxAsset = RenderSettings.skybox;
            _hasCapturedBaseline = true;
        }

        private Light ResolvePrimaryDirectionalLight()
        {
            if (_sunLight != null) return _sunLight;
            if (RenderSettings.sun != null)
            {
                _sunLight = RenderSettings.sun;
                return _sunLight;
            }

            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                {
                    _sunLight = lights[i];
                    return _sunLight;
                }
            }
            return null;
        }

        private void UpdateRuntimeSkyboxTint(Color skyTint)
        {
            if (!Application.isPlaying) return;
            if (_originalSkyboxAsset == null) return;

            if (_runtimeSkyboxCopy == null)
            {
                _runtimeSkyboxCopy = new Material(_originalSkyboxAsset);
                _runtimeSkyboxCopy.hideFlags = HideFlags.DontSave;
                RenderSettings.skybox = _runtimeSkyboxCopy;
            }

            if (_runtimeSkyboxCopy.HasProperty("_Tint")) _runtimeSkyboxCopy.SetColor("_Tint", skyTint);
            if (_runtimeSkyboxCopy.HasProperty("_SkyTint")) _runtimeSkyboxCopy.SetColor("_SkyTint", skyTint);
        }
    }
}
