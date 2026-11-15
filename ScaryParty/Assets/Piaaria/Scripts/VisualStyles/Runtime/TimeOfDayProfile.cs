using System;
using UnityEngine;

namespace ScaryParty.Lighting
{
    /// <summary>
    /// ScriptableObject de curvas artisticas para o ciclo Tarde (17h) -> Crepusculo (19h) -> Noite (22h) -> Madrugada (02h).
    /// </summary>
    [CreateAssetMenu(fileName = "TimeOfDayProfile_Default", menuName = "Scary Party/Lighting/Time Of Day Profile")]
    public class TimeOfDayProfile : ScriptableObject
    {
        [Header("Direcional (Sol / Lua unica com sombra)")]
        public Gradient sunColor = new Gradient();
        public AnimationCurve sunIntensityCurve = new AnimationCurve();
        public AnimationCurve sunPitchCurve = new AnimationCurve();
        public float sunBaseYaw = 38f;
        public float sunYawSweep = 110f;

        [Header("Ambiente Trilight e Neblina Cartoon")]
        public Gradient ambientSkyColor = new Gradient();
        public Gradient ambientEquatorColor = new Gradient();
        public Gradient ambientGroundColor = new Gradient();
        public AnimationCurve ambientIntensityCurve = new AnimationCurve();

        public Gradient fogColor = new Gradient();
        public AnimationCurve fogDensityCurve = new AnimationCurve();
        public Gradient skyTintGradient = new Gradient();

        [Header("Piso de Sombra e Balanco Cidade/Pizzaria")]
        public AnimationCurve minShadowBrightnessCurve = new AnimationCurve();
        public AnimationCurve shadowLiftCurve = new AnimationCurve();
        public AnimationCurve indirectDiffuseWeightCurve = new AnimationCurve();
        public AnimationCurve cityNightEmissionCurve = new AnimationCurve();
        public AnimationCurve interiorFillMultiplierCurve = new AnimationCurve();

        private void Reset()
        {
            ApplyCartoonDefaults();
        }

        private void OnEnable()
        {
            if (sunIntensityCurve == null || sunIntensityCurve.length == 0)
            {
                ApplyCartoonDefaults();
            }
        }

        public static float NormalizeHourToUnit(float hours)
        {
            float wrapped = hours % 24f;
            if (wrapped < 0f) wrapped += 24f;
            return wrapped / 24f;
        }

        public LightingEnvironmentState Evaluate(float clockHours, LightingEnvironmentState baseState = default)
        {
            float wrappedHours = clockHours % 24f;
            if (wrappedHours < 0f) wrappedHours += 24f;
            float t = wrappedHours / 24f;

            float pitch = Mathf.Clamp(sunPitchCurve.Evaluate(t), 12f, 75f);
            float yaw = sunBaseYaw + (t - 0.5f) * sunYawSweep;
            Vector3 sunDir = (Quaternion.Euler(pitch, yaw, 0f) * Vector3.back).normalized;

            baseState.clockHours = wrappedHours;
            baseState.normalizedDayProgress = t;
            baseState.sunDirection = sunDir;
            baseState.sunColor = sunColor.Evaluate(t);
            baseState.sunIntensity = Mathf.Max(0.25f, sunIntensityCurve.Evaluate(t));
            baseState.ambientSkyColor = ambientSkyColor.Evaluate(t);
            baseState.ambientEquatorColor = ambientEquatorColor.Evaluate(t);
            baseState.ambientGroundColor = ambientGroundColor.Evaluate(t);
            baseState.ambientIntensity = Mathf.Clamp(ambientIntensityCurve.Evaluate(t), 0.65f, 1.8f);
            baseState.fogColor = fogColor.Evaluate(t);
            baseState.fogDensity = Mathf.Clamp(fogDensityCurve.Evaluate(t), 0f, 0.02f);
            baseState.minShadowBrightness = Mathf.Clamp(minShadowBrightnessCurve.Evaluate(t), 0.18f, 0.65f);
            baseState.shadowLift = Mathf.Clamp(shadowLiftCurve.Evaluate(t), 0f, 0.45f);
            baseState.indirectDiffuseWeight = Mathf.Clamp(indirectDiffuseWeightCurve.Evaluate(t), 0.5f, 1.8f);
            return baseState;
        }

        public float EvaluateCityNightEmission(float clockHours)
        {
            return Mathf.Clamp01(cityNightEmissionCurve.Evaluate(NormalizeHourToUnit(clockHours)));
        }

        public float EvaluateInteriorFillMultiplier(float clockHours)
        {
            return Mathf.Clamp(interiorFillMultiplierCurve.Evaluate(NormalizeHourToUnit(clockHours)), 0.5f, 2.0f);
        }

        public Color EvaluateSkyTint(float clockHours)
        {
            return skyTintGradient.Evaluate(NormalizeHourToUnit(clockHours));
        }

        public void ApplyCartoonDefaults()
        {
            // Chaves normalizadas em 24h:
            // 02:00 = 0.083 | 07:00 = 0.292 | 12:00 = 0.500 | 17:00 = 0.708 | 19:00 = 0.792 | 22:00 = 0.917
            sunColor = CreateGradient(
                (0.083f, new Color(0.49f, 0.60f, 0.85f)), // 02h madrugada
                (0.292f, new Color(1.00f, 0.86f, 0.70f)), // 07h manha
                (0.500f, new Color(1.00f, 0.98f, 0.94f)), // 12h meio dia
                (0.708f, new Color(1.00f, 0.90f, 0.74f)), // 17h tarde dourada
                (0.792f, new Color(1.00f, 0.58f, 0.37f)), // 19h crepusculo
                (0.917f, new Color(0.55f, 0.69f, 0.92f))  // 22h noite viva
            );

            sunIntensityCurve = new AnimationCurve(
                new Keyframe(0.083f, 0.48f),
                new Keyframe(0.292f, 1.15f),
                new Keyframe(0.500f, 1.45f),
                new Keyframe(0.708f, 1.32f),
                new Keyframe(0.792f, 0.92f),
                new Keyframe(0.917f, 0.58f)
            );

            sunPitchCurve = new AnimationCurve(
                new Keyframe(0.083f, 52f),
                new Keyframe(0.292f, 28f),
                new Keyframe(0.500f, 62f),
                new Keyframe(0.708f, 36f),
                new Keyframe(0.792f, 15f),
                new Keyframe(0.917f, 44f)
            );

            ambientSkyColor = CreateGradient(
                (0.083f, new Color(0.22f, 0.28f, 0.46f)),
                (0.292f, new Color(0.68f, 0.80f, 0.94f)),
                (0.500f, new Color(0.74f, 0.85f, 0.96f)),
                (0.708f, new Color(0.67f, 0.78f, 0.88f)),
                (0.792f, new Color(0.47f, 0.51f, 0.76f)),
                (0.917f, new Color(0.25f, 0.33f, 0.54f))
            );

            ambientEquatorColor = CreateGradient(
                (0.083f, new Color(0.24f, 0.27f, 0.40f)),
                (0.292f, new Color(0.72f, 0.66f, 0.60f)),
                (0.500f, new Color(0.74f, 0.72f, 0.68f)),
                (0.708f, new Color(0.73f, 0.64f, 0.60f)),
                (0.792f, new Color(0.70f, 0.47f, 0.51f)),
                (0.917f, new Color(0.29f, 0.32f, 0.46f))
            );

            ambientGroundColor = CreateGradient(
                (0.083f, new Color(0.16f, 0.18f, 0.27f)),
                (0.500f, new Color(0.38f, 0.34f, 0.31f)),
                (0.708f, new Color(0.41f, 0.35f, 0.34f)),
                (0.792f, new Color(0.29f, 0.24f, 0.33f)),
                (0.917f, new Color(0.19f, 0.21f, 0.31f))
            );

            ambientIntensityCurve = new AnimationCurve(
                new Keyframe(0.083f, 0.92f),
                new Keyframe(0.500f, 1.18f),
                new Keyframe(0.708f, 1.15f),
                new Keyframe(0.792f, 1.08f),
                new Keyframe(0.917f, 0.98f)
            );

            fogColor = CreateGradient(
                (0.083f, new Color(0.13f, 0.16f, 0.25f)),
                (0.500f, new Color(0.76f, 0.85f, 0.92f)),
                (0.708f, new Color(0.83f, 0.86f, 0.90f)),
                (0.792f, new Color(0.49f, 0.45f, 0.60f)),
                (0.917f, new Color(0.17f, 0.20f, 0.31f))
            );

            fogDensityCurve = new AnimationCurve(
                new Keyframe(0.083f, 0.0045f),
                new Keyframe(0.500f, 0.0018f),
                new Keyframe(0.708f, 0.0022f),
                new Keyframe(0.792f, 0.0032f),
                new Keyframe(0.917f, 0.0038f)
            );

            skyTintGradient = CreateGradient(
                (0.083f, new Color(0.30f, 0.38f, 0.62f)),
                (0.500f, new Color(0.95f, 0.97f, 1.00f)),
                (0.708f, new Color(1.00f, 0.93f, 0.85f)),
                (0.792f, new Color(0.82f, 0.56f, 0.70f)),
                (0.917f, new Color(0.38f, 0.46f, 0.72f))
            );

            minShadowBrightnessCurve = new AnimationCurve(
                new Keyframe(0.083f, 0.40f),
                new Keyframe(0.500f, 0.38f),
                new Keyframe(0.708f, 0.38f),
                new Keyframe(0.792f, 0.40f),
                new Keyframe(0.917f, 0.42f)
            );

            shadowLiftCurve = new AnimationCurve(
                new Keyframe(0.083f, 0.18f),
                new Keyframe(0.500f, 0.12f),
                new Keyframe(0.708f, 0.14f),
                new Keyframe(0.792f, 0.18f),
                new Keyframe(0.917f, 0.20f)
            );

            indirectDiffuseWeightCurve = new AnimationCurve(
                new Keyframe(0.083f, 1.15f),
                new Keyframe(0.500f, 1.00f),
                new Keyframe(0.708f, 1.05f),
                new Keyframe(0.792f, 1.15f),
                new Keyframe(0.917f, 1.20f)
            );

            cityNightEmissionCurve = new AnimationCurve(
                new Keyframe(0.083f, 0.72f),
                new Keyframe(0.292f, 0.10f),
                new Keyframe(0.500f, 0.00f),
                new Keyframe(0.708f, 0.12f),
                new Keyframe(0.792f, 0.85f),
                new Keyframe(0.917f, 1.00f)
            );

            interiorFillMultiplierCurve = new AnimationCurve(
                new Keyframe(0.083f, 1.18f),
                new Keyframe(0.500f, 0.88f),
                new Keyframe(0.708f, 0.94f),
                new Keyframe(0.792f, 1.08f),
                new Keyframe(0.917f, 1.18f)
            );
        }

        private static Gradient CreateGradient(params (float time, Color col)[] keys)
        {
            var g = new Gradient();
            var colorKeys = new GradientColorKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                colorKeys[i] = new GradientColorKey(keys[i].col, keys[i].time);
            }
            var alphaKeys = new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            };
            g.SetKeys(colorKeys, alphaKeys);
            return g;
        }
    }
}
