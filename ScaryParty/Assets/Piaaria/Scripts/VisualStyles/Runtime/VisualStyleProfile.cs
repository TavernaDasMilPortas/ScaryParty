using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.VisualStyles
{
    [Serializable]
    public class ToonLightingSettings
    {
        [Range(0, 1)] public int lightingMode = 1; // 1 = Lit, 0 = Unlit
        public bool posterizeLight = true;
        [Range(1, 6)] public int lightSteps = 3;
        [Range(0f, 2f)] public float lightRamp = 1f;
        [Range(0f, 1f)] public float lightRampOffset = 0.5f;
        public Color lightColorTint = Color.white;
        public Color shadowTint = new Color(0.62f, 0.60f, 0.70f, 1f);
        public bool multiplyByLightColor = true;
        public bool useShadows = true;
        public bool receiveShadows = true;
        [Range(0f, 2f)] public float mainLightInfluence = 1f;
    }

    [Serializable]
    public class ToonSpecularRimSettings
    {
        public bool useSpecular = false;
        public Color specularColor = Color.white;
        [Range(0f, 2f)] public float specularRamp = 0.1f;
        [Range(0f, 1f)] public float specularRampOffset = 0.9f;

        public bool useRimLighting = true;
        public Color rimLightColor = new Color(0.85f, 0.90f, 1f, 1f);
        [Range(0f, 2f)] public float rimRamp = 1.0f;
        [Range(0f, 1f)] public float rimLightRampOffset = 0.58f;
        [Range(0f, 1f)] public float rimLightLitIntensity = 0.35f;
        [Range(0f, 1f)] public float rimLightShadowIntensity = 0.12f;
    }

    [Serializable]
    public class ToonColorPaletteSettings
    {
        // Soft Toon Vibrant palette from plan Section 4 (#EF594B, #FFD65D, #49B7AA, #527CE9)
        public Color accentRed = new Color(0.937f, 0.349f, 0.294f, 1f);
        public Color accentYellow = new Color(1.000f, 0.839f, 0.365f, 1f);
        public Color accentTurquoise = new Color(0.286f, 0.718f, 0.667f, 1f);
        public Color accentBlue = new Color(0.322f, 0.486f, 0.914f, 1f);
        public Color neutralLight = new Color(0.95f, 0.94f, 0.90f, 1f);
        public Color neutralDark = new Color(0.24f, 0.25f, 0.28f, 1f);

        [Range(-1f, 1f)] public float saturationShift = 0.08f;
        [Range(-1f, 1f)] public float valueShift = 0.03f;
        [Range(0.5f, 1.5f)] public float contrast = 1.0f;
        public bool clampAdjustments = true;
        public bool posterizeColors = false;
        [Range(1, 20)] public int colorNumbers = 6;
    }

    [Serializable]
    public class ToonOutlineSettings
    {
        public VisualStyleOutlineMode outlineMode = VisualStyleOutlineMode.ScreenSpace;
        public Color outlineColor = new Color(0.14f, 0.12f, 0.16f, 1f);
        [Range(0f, 1f)] public float outlineWidth = 0.08f;
        public bool normalSurfaceOutline = false;

        [Header("Screen Space Outline Feature Settings")]
        [Range(0f, 5f)] public float screenSpaceWidth = 1.1f;
        public bool useDepthTest = true;
        [Range(0f, 2f)] public float depthThreshold = 0.25f;
        public bool useNormalsTest = true;
        [Range(0f, 2f)] public float normalThreshold = 0.45f;
        public bool useColorTest = false;
        [Range(0f, 2f)] public float colorThreshold = 0.2f;

        [Header("Category Multipliers & Distance")]
        [Range(0f, 2f)] public float characterOutlineMultiplier = 1.25f;
        [Range(0f, 2f)] public float buildingOutlineMultiplier = 0.20f;
        [Range(0f, 2f)] public float foodOutlineMultiplier = 0.15f;
        public float maxOutlineDistanceArchitecture = 45f;
        public float maxOutlineDistanceCharacters = 60f;
        public float maxOutlineDistanceProps = 35f;

        public VisualStyleOutlineQuality outlineQuality = VisualStyleOutlineQuality.Medium;
        public LayerMask outlineLayerMask = ~0;
        public bool suppressOutlineOnInteractionFocus = true;
    }

    [Serializable]
    public class ToonCharacterSettings
    {
        [Range(0f, 2f)] public float characterOutlineMultiplier = 1.25f;
        public bool rimLightEnabled = true;
        public Color rimColor = new Color(1f, 0.96f, 0.88f, 1f);
        [Range(0f, 1f)] public float rimIntensity = 0.45f;
        [Range(0.8f, 1.4f)] public float characterContrastBoost = 1.06f;
        public bool preserveDynamicBodyColor = true;
        [Range(0f, 2f)] public float skinLightRampSoftness = 1.15f;
        public bool eyesUnlitCrisp = true;
    }

    [Serializable]
    public class ToonMaterialFeatureSettings
    {
        public bool preserveTextures = true;
        public bool preserveNormalMaps = true;
        public bool preserveUVTilingOffset = true;
        public bool preserveAlphaAndTransparency = true;
        public bool preserveEmission = true;
        public bool preferSimpleForStreets = true;
        public bool disableIllustratePerturbations = true;
    }

    [Serializable]
    public class ToonSceneLightingSettings
    {
        public bool overrideSceneLightingInVariant = true;
        [Range(0.2f, 3f)] public float directionalLightIntensity = 1.35f;
        public Color directionalLightColor = new Color(1f, 0.97f, 0.92f, 1f);
        public Color ambientSkyColor = new Color(0.68f, 0.76f, 0.86f, 1f);
        public Color ambientEquatorColor = new Color(0.58f, 0.56f, 0.54f, 1f);
        public Color ambientGroundColor = new Color(0.32f, 0.28f, 0.26f, 1f);
        [Range(0f, 2f)] public float ambientIntensity = 1.15f;
        public bool addInteriorFillLights = true;
        [Range(0f, 5f)] public float interiorFillIntensity = 1.4f;
        [Range(2f, 25f)] public float interiorFillRange = 10f;
    }

    [Serializable]
    public class VisualCategoryOverride
    {
        public string categoryId = VisualCategoryIds.Props;
        public bool enabled = true;
        public IllustrateShaderFamily preferredShaderFamily = IllustrateShaderFamily.Opaque;
        public bool preserveTextures = true;
        public bool allowColorPosterize = true;
        [Range(-3, 3)] public int lightStepsOffset = 0;
        [Range(-1f, 2f)] public float lightRampOverride = -1f; // < 0 inherits profile
        [Range(-1f, 1f)] public float saturationDelta = 0f;
        [Range(0f, 2f)] public float outlineMultiplier = 1f;
        public bool overrideRim = false;
        public bool rimEnabled = false;
        public float maxOutlineDistance = 40f;
    }

    [Serializable]
    public class ToonDiscoverySettings
    {
        public VisualDiscoveryPolicy newUnknownMaterialPolicy = VisualDiscoveryPolicy.Review;
        [Range(0.5f, 1f)] public float autoApplyConfidenceThreshold = 0.85f;
        public bool autoSyncWatchedChangesToVariants = false;
        public VisualDiscoveryScope defaultScope = VisualDiscoveryScope.GameplayReachable;
        public List<string> selectedFolders = new List<string> { "Assets/Piaaria" };
        public List<string> excludedPathPrefixes = new List<string>
        {
            "Assets/ThirdParty/Distant Lands/",
            "Assets/TextMesh Pro/",
            "Assets/Piaaria/Art/VisualStyles/"
        };
        public VisualConversionRuleSet conversionRuleSet;
    }

    public struct EffectiveCategorySettings
    {
        public string categoryId;
        public IllustrateShaderFamily preferredShaderFamily;
        public bool preserveTextures;
        public bool allowColorPosterize;
        public int lightStepsOffset;
        public float lightRampOverride;
        public float saturationDelta;
        public float outlineMultiplier;
        public bool overrideRim;
        public bool rimEnabled;
    }

    /// <summary>
    /// Single source of truth ScriptableObject defining a complete global cel-shading visual style.
    /// </summary>
    [CreateAssetMenu(fileName = "VS_NewProfile", menuName = "Scary Party/Visual Styles/Visual Style Profile")]
    public class VisualStyleProfile : ScriptableObject
    {
        [Header("Identity")]
        public string styleId = "soft_toon_vibrant";
        public string displayName = "Soft Toon Vibrant";
        [TextArea(2, 4)] public string description = "Cartoon suave, contornos finos, paleta vibrante, texturas preservadas e personagens destacados.";
        public int profileVersion = 1;
        public VisualStylePresetType presetType = VisualStylePresetType.SoftToonVibrant;
        public Texture2D thumbnail;

        [Header("Toon Illumination & Shading")]
        public ToonLightingSettings lighting = new ToonLightingSettings();
        public ToonSpecularRimSettings specularRim = new ToonSpecularRimSettings();
        public ToonColorPaletteSettings colorPalette = new ToonColorPaletteSettings();
        public ToonOutlineSettings outlines = new ToonOutlineSettings();
        public ToonCharacterSettings characters = new ToonCharacterSettings();
        public ToonMaterialFeatureSettings materials = new ToonMaterialFeatureSettings();
        public ToonSceneLightingSettings sceneLighting = new ToonSceneLightingSettings();

        [Header("Per-Category Overrides (keyed by string categoryId)")]
        public List<VisualCategoryOverride> categoryOverrides = new List<VisualCategoryOverride>();

        [Header("Universal Asset Discovery & Conversion Policy")]
        public ToonDiscoverySettings discovery = new ToonDiscoverySettings();

        public VisualStyleProfile CloneTransient()
        {
            var clone = CreateInstance<VisualStyleProfile>();
            clone.CopyValuesFrom(this);
            clone.name = $"{name}_TransientClone";
            return clone;
        }

        public void CopyValuesFrom(VisualStyleProfile source)
        {
            if (source == null || ReferenceEquals(source, this)) return;
            string json = JsonUtility.ToJson(source);
            JsonUtility.FromJsonOverwrite(json, this);
        }

        public EffectiveCategorySettings GetEffectiveCategorySettings(string categoryId)
        {
            EffectiveCategorySettings result = new EffectiveCategorySettings
            {
                categoryId = categoryId,
                preferredShaderFamily = IllustrateShaderFamily.Opaque,
                preserveTextures = materials.preserveTextures,
                allowColorPosterize = true,
                lightStepsOffset = 0,
                lightRampOverride = -1f,
                saturationDelta = colorPalette.saturationShift,
                outlineMultiplier = 1f,
                overrideRim = false,
                rimEnabled = specularRim.useRimLighting
            };

            if (categoryId == VisualCategoryIds.Characters)
            {
                result.outlineMultiplier = outlines.characterOutlineMultiplier;
                result.rimEnabled = characters.rimLightEnabled;
            }
            else if (categoryId == VisualCategoryIds.City || categoryId == VisualCategoryIds.Architecture)
            {
                result.outlineMultiplier = outlines.buildingOutlineMultiplier;
            }
            else if (categoryId == VisualCategoryIds.Food)
            {
                result.outlineMultiplier = outlines.foodOutlineMultiplier;
                result.allowColorPosterize = false;
            }
            else if (categoryId == VisualCategoryIds.Streets && materials.preferSimpleForStreets)
            {
                result.preferredShaderFamily = IllustrateShaderFamily.Simple;
                result.outlineMultiplier = 0f;
            }

            if (categoryOverrides != null)
            {
                for (int i = 0; i < categoryOverrides.Count; i++)
                {
                    var ov = categoryOverrides[i];
                    if (ov != null && ov.enabled && string.Equals(ov.categoryId, categoryId, StringComparison.OrdinalIgnoreCase))
                    {
                        result.preferredShaderFamily = ov.preferredShaderFamily;
                        result.preserveTextures = ov.preserveTextures;
                        result.allowColorPosterize = ov.allowColorPosterize;
                        result.lightStepsOffset = ov.lightStepsOffset;
                        result.lightRampOverride = ov.lightRampOverride;
                        result.saturationDelta = colorPalette.saturationShift + ov.saturationDelta;
                        result.outlineMultiplier = ov.outlineMultiplier;
                        result.overrideRim = ov.overrideRim;
                        result.rimEnabled = ov.rimEnabled;
                        break;
                    }
                }
            }

            return result;
        }

        public VisualCategoryOverride GetOrAddCategoryOverride(string categoryId)
        {
            if (categoryOverrides == null) categoryOverrides = new List<VisualCategoryOverride>();
            for (int i = 0; i < categoryOverrides.Count; i++)
            {
                if (categoryOverrides[i] != null && string.Equals(categoryOverrides[i].categoryId, categoryId, StringComparison.OrdinalIgnoreCase))
                    return categoryOverrides[i];
            }

            var eff = GetEffectiveCategorySettings(categoryId);
            var created = new VisualCategoryOverride
            {
                categoryId = categoryId,
                enabled = true,
                preferredShaderFamily = eff.preferredShaderFamily,
                preserveTextures = eff.preserveTextures,
                allowColorPosterize = eff.allowColorPosterize,
                lightStepsOffset = eff.lightStepsOffset,
                lightRampOverride = eff.lightRampOverride,
                saturationDelta = 0f,
                outlineMultiplier = eff.outlineMultiplier,
                overrideRim = false,
                rimEnabled = eff.rimEnabled
            };
            categoryOverrides.Add(created);
            return created;
        }

        public string ComputeContentHash()
        {
            unchecked
            {
                // Serialize all value fields except discovery rule asset path noise when hashed separately
                string json = JsonUtility.ToJson(this);
                uint hash = 2166136261u;
                for (int i = 0; i < json.Length; i++)
                {
                    hash ^= json[i];
                    hash *= 16777619u;
                }
                return hash.ToString("x8");
            }
        }

        public void ApplyPresetDefaults(VisualStylePresetType preset)
        {
            presetType = preset;
            profileVersion = Math.Max(1, profileVersion);
            lighting = new ToonLightingSettings();
            specularRim = new ToonSpecularRimSettings();
            colorPalette = new ToonColorPaletteSettings();
            outlines = new ToonOutlineSettings();
            characters = new ToonCharacterSettings();
            materials = new ToonMaterialFeatureSettings();
            sceneLighting = new ToonSceneLightingSettings();

            switch (preset)
            {
                case VisualStylePresetType.AnimeSoft:
                    styleId = "anime_soft";
                    displayName = "Anime Soft";
                    description = "2-3 faixas suaves, contornos discretos, cores levemente mais suaves e rim light delicado nos personagens.";
                    lighting.lightSteps = 2;
                    lighting.lightRamp = 1.35f;
                    lighting.lightRampOffset = 0.48f;
                    lighting.shadowTint = new Color(0.68f, 0.65f, 0.76f, 1f);
                    colorPalette.saturationShift = -0.06f;
                    colorPalette.valueShift = 0.05f;
                    colorPalette.contrast = 0.96f;
                    colorPalette.posterizeColors = false;
                    outlines.outlineMode = VisualStyleOutlineMode.ScreenSpace;
                    outlines.outlineWidth = 0.045f;
                    outlines.screenSpaceWidth = 0.75f;
                    outlines.characterOutlineMultiplier = 1.2f;
                    outlines.buildingOutlineMultiplier = 0.1f;
                    characters.rimLightEnabled = true;
                    characters.rimIntensity = 0.55f;
                    characters.rimColor = new Color(1f, 0.97f, 0.90f, 1f);
                    materials.preserveTextures = true;
                    break;

                case VisualStylePresetType.ComicBold:
                    styleId = "comic_bold";
                    displayName = "Comic Bold";
                    description = "2 faixas bem definidas, contornos marcados, alto contraste e posterizacao seletiva.";
                    lighting.lightSteps = 2;
                    lighting.lightRamp = 0.18f;
                    lighting.lightRampOffset = 0.52f;
                    lighting.shadowTint = new Color(0.42f, 0.40f, 0.54f, 1f);
                    colorPalette.saturationShift = 0.16f;
                    colorPalette.contrast = 1.18f;
                    colorPalette.posterizeColors = true;
                    colorPalette.colorNumbers = 6;
                    outlines.outlineMode = VisualStyleOutlineMode.ScreenSpace;
                    outlines.outlineWidth = 0.14f;
                    outlines.screenSpaceWidth = 1.85f;
                    outlines.characterOutlineMultiplier = 1.4f;
                    outlines.buildingOutlineMultiplier = 0.45f;
                    outlines.foodOutlineMultiplier = 0.35f;
                    specularRim.useSpecular = true;
                    specularRim.specularRamp = 0.08f;
                    characters.rimLightEnabled = true;
                    characters.rimIntensity = 0.6f;
                    materials.preserveTextures = true;
                    break;

                case VisualStylePresetType.FlatGraphic:
                    styleId = "flat_graphic";
                    displayName = "Flat Graphic";
                    description = "1-2 faixas graficas chapadas, texturas essenciais e silhuetas limpas.";
                    lighting.lightSteps = 2;
                    lighting.lightRamp = 0.05f;
                    lighting.lightRampOffset = 0.50f;
                    lighting.shadowTint = new Color(0.70f, 0.72f, 0.78f, 1f);
                    specularRim.useSpecular = false;
                    specularRim.useRimLighting = false;
                    colorPalette.saturationShift = 0.05f;
                    colorPalette.contrast = 1.04f;
                    colorPalette.posterizeColors = false;
                    outlines.outlineMode = VisualStyleOutlineMode.Disabled;
                    outlines.outlineWidth = 0f;
                    outlines.screenSpaceWidth = 0f;
                    characters.rimLightEnabled = false;
                    materials.preserveTextures = false; // Essential markings/food retain via category overrides
                    break;

                case VisualStylePresetType.SoftToonVibrant:
                default:
                    styleId = "soft_toon_vibrant";
                    displayName = "Soft Toon Vibrant";
                    description = "Direcao padrao: 3 faixas suaves, contornos finos, paleta vibrante, texturas preservadas e personagens destacados.";
                    lighting.lightSteps = 3;
                    lighting.lightRamp = 0.85f;
                    lighting.lightRampOffset = 0.50f;
                    lighting.shadowTint = new Color(0.60f, 0.58f, 0.70f, 1f);
                    colorPalette.saturationShift = 0.10f;
                    colorPalette.valueShift = 0.03f;
                    colorPalette.contrast = 1.03f;
                    colorPalette.posterizeColors = false;
                    outlines.outlineMode = VisualStyleOutlineMode.ScreenSpace;
                    outlines.outlineWidth = 0.08f;
                    outlines.screenSpaceWidth = 1.1f;
                    outlines.characterOutlineMultiplier = 1.25f;
                    outlines.buildingOutlineMultiplier = 0.18f;
                    outlines.foodOutlineMultiplier = 0.15f;
                    characters.rimLightEnabled = true;
                    characters.rimIntensity = 0.42f;
                    materials.preserveTextures = true;
                    break;
            }

            SeedDefaultCategoryOverrides();
        }

        private void SeedDefaultCategoryOverrides()
        {
            categoryOverrides.Clear();
            categoryOverrides.Add(new VisualCategoryOverride
            {
                categoryId = VisualCategoryIds.Characters,
                preferredShaderFamily = IllustrateShaderFamily.Opaque,
                preserveTextures = true,
                allowColorPosterize = false,
                outlineMultiplier = outlines.characterOutlineMultiplier,
                overrideRim = true,
                rimEnabled = characters.rimLightEnabled,
                maxOutlineDistance = outlines.maxOutlineDistanceCharacters
            });
            categoryOverrides.Add(new VisualCategoryOverride
            {
                categoryId = VisualCategoryIds.Food,
                preferredShaderFamily = IllustrateShaderFamily.Opaque,
                preserveTextures = true, // Essential food detail preserved even in Flat Graphic
                allowColorPosterize = false,
                outlineMultiplier = outlines.foodOutlineMultiplier,
                maxOutlineDistance = outlines.maxOutlineDistanceProps
            });
            categoryOverrides.Add(new VisualCategoryOverride
            {
                categoryId = VisualCategoryIds.Streets,
                preferredShaderFamily = IllustrateShaderFamily.Simple,
                preserveTextures = true,
                allowColorPosterize = false,
                outlineMultiplier = 0f,
                overrideRim = true,
                rimEnabled = false,
                maxOutlineDistance = 0f
            });
            categoryOverrides.Add(new VisualCategoryOverride
            {
                categoryId = VisualCategoryIds.City,
                preferredShaderFamily = IllustrateShaderFamily.Opaque,
                preserveTextures = materials.preserveTextures,
                allowColorPosterize = colorPalette.posterizeColors,
                outlineMultiplier = outlines.buildingOutlineMultiplier,
                maxOutlineDistance = outlines.maxOutlineDistanceArchitecture
            });
            categoryOverrides.Add(new VisualCategoryOverride
            {
                categoryId = VisualCategoryIds.Architecture,
                preferredShaderFamily = IllustrateShaderFamily.Opaque,
                preserveTextures = materials.preserveTextures,
                allowColorPosterize = colorPalette.posterizeColors,
                outlineMultiplier = outlines.buildingOutlineMultiplier,
                maxOutlineDistance = outlines.maxOutlineDistanceArchitecture
            });
        }
    }
}
