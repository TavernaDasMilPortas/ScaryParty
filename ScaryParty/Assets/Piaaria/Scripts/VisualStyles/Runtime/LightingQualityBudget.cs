using System;
using UnityEngine;

namespace ScaryParty.Lighting
{
    public enum LightingQualityTier
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    [Serializable]
    public class LightingTierSpec
    {
        public string label = "Medium";
        public int mainShadowResolution = 2048;
        public int additionalShadowAtlasSize = 2048;
        public int shadowCascadeCount = 2;
        public float shadowDistance = 35f;
        public int tierLowResolution = 128;
        public int tierMediumResolution = 256;
        public int tierHighResolution = 512;
        public int maxShadowedLocalLights = 1;
        public int maxPooledStreetLights = 8;
        public float cityLightCullDistance = 38f;
        public bool enableScreenSpaceOutline = true;
        [Range(0f, 1f)] public float ssaoIntensity = 0.20f;
        [Range(0f, 1f)] public float ssaoDirectLightingStrength = 0.15f;
    }

    /// <summary>
    /// Orcamento escalavel Low/Medium/High para manter sombras URP dentro do atlas 2048 sem warnings.
    /// </summary>
    [CreateAssetMenu(fileName = "LightingQualityBudget_Default", menuName = "Scary Party/Lighting/Quality Budget")]
    public class LightingQualityBudget : ScriptableObject
    {
        public LightingQualityTier activeTier = LightingQualityTier.Medium;

        public LightingTierSpec low = new LightingTierSpec
        {
            label = "Low",
            mainShadowResolution = 1024,
            additionalShadowAtlasSize = 1024,
            shadowCascadeCount = 1,
            shadowDistance = 28f,
            tierLowResolution = 128,
            tierMediumResolution = 128,
            tierHighResolution = 256,
            maxShadowedLocalLights = 0,
            maxPooledStreetLights = 4,
            cityLightCullDistance = 26f,
            enableScreenSpaceOutline = false,
            ssaoIntensity = 0.15f,
            ssaoDirectLightingStrength = 0.12f
        };

        public LightingTierSpec medium = new LightingTierSpec
        {
            label = "Medium",
            mainShadowResolution = 2048,
            additionalShadowAtlasSize = 2048,
            shadowCascadeCount = 2,
            shadowDistance = 35f,
            tierLowResolution = 128,
            tierMediumResolution = 256,
            tierHighResolution = 512,
            maxShadowedLocalLights = 1,
            maxPooledStreetLights = 8,
            cityLightCullDistance = 38f,
            enableScreenSpaceOutline = true,
            ssaoIntensity = 0.20f,
            ssaoDirectLightingStrength = 0.15f
        };

        public LightingTierSpec high = new LightingTierSpec
        {
            label = "High",
            mainShadowResolution = 2048,
            additionalShadowAtlasSize = 2048,
            shadowCascadeCount = 2,
            shadowDistance = 42f,
            tierLowResolution = 128,
            tierMediumResolution = 256,
            tierHighResolution = 512,
            maxShadowedLocalLights = 1,
            maxPooledStreetLights = 12,
            cityLightCullDistance = 48f,
            enableScreenSpaceOutline = true,
            ssaoIntensity = 0.20f,
            ssaoDirectLightingStrength = 0.15f
        };

        public LightingTierSpec GetSpec(LightingQualityTier tier)
        {
            switch (tier)
            {
                case LightingQualityTier.Low: return low;
                case LightingQualityTier.High: return high;
                default: return medium;
            }
        }

        public LightingTierSpec GetActiveSpec() => GetSpec(activeTier);
    }
}
