using UnityEngine;

namespace ScaryParty.VisualStyles
{
    /// <summary>
    /// Shared helper for validating Distant Lands Illustrate shaders, writing confirmed
    /// properties, synchronizing real shader keywords, and updating MaterialPropertyBlocks safely.
    /// </summary>
    public static class VisualStyleShaderUtility
    {
        public const string ShaderOpaque = "Distant Lands/Illustrate/Opaque";
        public const string ShaderSimple = "Distant Lands/Illustrate/Simple";
        public const string ShaderTransparent = "Distant Lands/Illustrate/Transparent";
        public const string ShaderAlphaClipped = "Distant Lands/Illustrate/Alpha Clipped";
        public const string ShaderOutline = "Distant Lands/Illustrate/Outline";
        public const string ScreenSpaceOutlineShader = "Shader Graphs/Outline Effect";

        public static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        public static readonly int ColorId = Shader.PropertyToID("_Color");
        public static readonly int MainColorId = Shader.PropertyToID("_MainColor");
        public static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private static readonly string[] PerturbationToggleProperties =
        {
            "_UseWave",
            "_UseSway",
            "_UseFlutter",
            "_UseSwirl",
            "_UseHSVVariation",
            "_UseTriplanar",
            "_UseGlint",
            "_UseHalftone",
            "_UseScreenNoise",
            "_UseGradientShading",
            "_UseCOZYPrecipitation",
            "_UseDissolve"
        };

        /// <summary>
        /// Returns the Unity shader name corresponding to an IllustrateShaderFamily.
        /// </summary>
        public static string GetShaderName(IllustrateShaderFamily family)
        {
            switch (family)
            {
                case IllustrateShaderFamily.Simple:
                    return ShaderSimple;
                case IllustrateShaderFamily.Transparent:
                    return ShaderTransparent;
                case IllustrateShaderFamily.AlphaClipped:
                    return ShaderAlphaClipped;
                case IllustrateShaderFamily.Outline:
                    return ShaderOutline;
                case IllustrateShaderFamily.Opaque:
                default:
                    return ShaderOpaque;
            }
        }

        /// <summary>
        /// Checks whether a shader belongs to the Distant Lands Illustrate family.
        /// </summary>
        public static bool IsIllustrateShader(Shader shader)
        {
            return shader != null && shader.name.StartsWith("Distant Lands/Illustrate/");
        }

        /// <summary>
        /// Infers the IllustrateShaderFamily from a shader name.
        /// </summary>
        public static IllustrateShaderFamily InferFamilyFromShader(Shader shader)
        {
            if (shader == null) return IllustrateShaderFamily.Opaque;
            string name = shader.name;
            if (name == ShaderSimple) return IllustrateShaderFamily.Simple;
            if (name == ShaderTransparent) return IllustrateShaderFamily.Transparent;
            if (name == ShaderAlphaClipped) return IllustrateShaderFamily.AlphaClipped;
            if (name == ShaderOutline) return IllustrateShaderFamily.Outline;
            return IllustrateShaderFamily.Opaque;
        }

        /// <summary>
        /// Applies all confirmed style properties and keywords to a material using the selected family.
        /// Only writes properties verified to exist on the active shader.
        /// </summary>
        public static void ApplyProfileToMaterial(
            Material mat,
            VisualStyleProfile profile,
            string categoryId,
            VisualSemanticRole role,
            Color baseColor,
            IllustrateShaderFamily family,
            Texture mainTexture = null,
            Vector2? textureScale = null,
            Vector2? textureOffset = null,
            Texture normalMap = null,
            float? alphaCutoff = null,
            int? cullMode = null,
            Texture emissionTexture = null,
            Color? emissionColor = null,
            bool forceUnlit = false)
        {
            if (mat == null || profile == null) return;

            EffectiveCategorySettings cat = profile.GetEffectiveCategorySettings(categoryId);
            bool hasColorAdjustShader = mat.HasProperty("_UseColorAdjustments");
            Color finalMainColor = ComputeAdjustedMainColor(baseColor, cat, profile, applyHsvInColor: !hasColorAdjustShader);

            SetColorSafe(mat, "_MainColor", finalMainColor);
            if (mainTexture != null)
            {
                SetTextureSafe(mat, "_Texture", mainTexture);
                if (textureScale.HasValue && mat.HasProperty("_Texture"))
                {
                    if ((mat.GetTextureScale("_Texture") - textureScale.Value).sqrMagnitude > 1e-6f)
                    {
                        mat.SetTextureScale("_Texture", textureScale.Value);
                    }
                }
                if (textureOffset.HasValue && mat.HasProperty("_Texture"))
                {
                    if ((mat.GetTextureOffset("_Texture") - textureOffset.Value).sqrMagnitude > 1e-6f)
                    {
                        mat.SetTextureOffset("_Texture", textureOffset.Value);
                    }
                }
            }

            if (normalMap != null && profile.materials.preserveNormalMaps)
            {
                SetTextureSafe(mat, "_NormalMap", normalMap);
            }

            if (cullMode.HasValue)
            {
                SetFloatSafe(mat, "_CullMode", cullMode.Value);
            }

            // Color adjustments (supported on Opaque/Transparent/AlphaClipped/Outline; omitted on Simple)
            if (hasColorAdjustShader)
            {
                bool needsAdjustments = Mathf.Abs(cat.saturationDelta) > 0.001f ||
                                        Mathf.Abs(profile.colorPalette.valueShift) > 0.001f;
                SetFloatSafe(mat, "_UseColorAdjustments", needsAdjustments ? 1f : 0f);
                SetFloatSafe(mat, "_ClampAdjustments", profile.colorPalette.clampAdjustments ? 1f : 0f);
                SetFloatSafe(mat, "_SaturationShift", Mathf.Clamp(cat.saturationDelta, -1f, 1f));
                SetFloatSafe(mat, "_ValueShift", Mathf.Clamp(profile.colorPalette.valueShift, -1f, 1f));
                SetFloatSafe(mat, "_HueShift", 0f);
            }

            bool allowColorPosterize = profile.colorPalette.posterizeColors && cat.allowColorPosterize &&
                                       (mainTexture == null || !IsDetailedFoodRole(role));
            SetFloatSafe(mat, "_PosterizeColors", allowColorPosterize ? 1f : 0f);
            SetFloatSafe(mat, "_ColorNumbers", Mathf.Clamp(profile.colorPalette.colorNumbers, 1, 20));

            // Lighting parameters
            int lightingMode = forceUnlit || (role == VisualSemanticRole.PlayerPupil || role == VisualSemanticRole.PlayerEye)
                ? (profile.characters.eyesUnlitCrisp ? 0 : profile.lighting.lightingMode)
                : profile.lighting.lightingMode;

            SetFloatSafe(mat, "_LightingMode", lightingMode);
            SetFloatSafe(mat, "_PosterizeLight", profile.lighting.posterizeLight ? 1f : 0f);
            SetFloatSafe(mat, "_LightSteps", Mathf.Clamp(profile.lighting.lightSteps + cat.lightStepsOffset, 1, 20));

            float ramp = cat.lightRampOverride >= 0f ? cat.lightRampOverride : profile.lighting.lightRamp;
            if (role == VisualSemanticRole.PlayerSkin && profile.characters.skinLightRampSoftness > 0f)
            {
                ramp = Mathf.Max(ramp, profile.characters.skinLightRampSoftness);
            }
            SetFloatSafe(mat, "_LightRamp", Mathf.Clamp(ramp, 0f, 2f));
            SetFloatSafe(mat, "_LightRampOffset", Mathf.Clamp01(profile.lighting.lightRampOffset));
            SetColorSafe(mat, "_LightColor", profile.lighting.lightColorTint);
            SetColorSafe(mat, "_ShadowColor", profile.lighting.shadowTint);
            SetFloatSafe(mat, "_MultiplyByLightColor", profile.lighting.multiplyByLightColor ? 1f : 0f);
            SetFloatSafe(mat, "_UseShadows", profile.lighting.useShadows ? 1f : 0f);
            SetFloatSafe(mat, "_ReceiveShadows", profile.lighting.receiveShadows ? 1f : 0f);
            SyncKeyword(mat, "_RECEIVE_SHADOWS_OFF", !profile.lighting.receiveShadows);

            // Specular & Rim
            SetFloatSafe(mat, "_UseSpecular", profile.specularRim.useSpecular ? 1f : 0f);
            SetColorSafe(mat, "_SpecularColor", profile.specularRim.specularColor);
            SetFloatSafe(mat, "_SpecularRamp", Mathf.Clamp(profile.specularRim.specularRamp, 0f, 2f));
            SetFloatSafe(mat, "_SpecularRampOffset", Mathf.Clamp01(profile.specularRim.specularRampOffset));

            bool isCharacter = categoryId == VisualCategoryIds.Characters || IsCharacterRole(role);
            bool rimActive = cat.overrideRim ? cat.rimEnabled :
                (isCharacter ? profile.characters.rimLightEnabled : profile.specularRim.useRimLighting);

            SetFloatSafe(mat, "_UseRimLighting", rimActive ? 1f : 0f);
            Color rimColor = isCharacter ? profile.characters.rimColor : profile.specularRim.rimLightColor;
            float rimLit = isCharacter ? profile.characters.rimIntensity : profile.specularRim.rimLightLitIntensity;
            SetColorSafe(mat, "_RimLightColor", rimColor);
            SetFloatSafe(mat, "_RimRamp", Mathf.Clamp(profile.specularRim.rimRamp, 0f, 2f));
            SetFloatSafe(mat, "_RimLightRampOffset", Mathf.Clamp01(profile.specularRim.rimLightRampOffset));
            SetFloatSafe(mat, "_RimLightLitIntensity", Mathf.Clamp01(rimLit));
            SetFloatSafe(mat, "_RimLightShadowIntensity", Mathf.Clamp01(profile.specularRim.rimLightShadowIntensity));

            // Alpha Clip
            if (family == IllustrateShaderFamily.AlphaClipped)
            {
                float cutoff = alphaCutoff.HasValue ? alphaCutoff.Value : 0.5f;
                SetFloatSafe(mat, "_AlphaClipThreshold", cutoff);
                SetFloatSafe(mat, "_AlphaCutoff", cutoff);
                SyncKeyword(mat, "_ALPHATEST_ON", true);
            }

            // Emission
            bool hasEmission = emissionColor.HasValue && emissionColor.Value.maxColorComponent > 0.01f;
            if (profile.materials.preserveEmission && hasEmission)
            {
                SetFloatSafe(mat, "_UseEmission", 1f);
                if (emissionTexture != null)
                {
                    SetTextureSafe(mat, "_EmissionTexture", emissionTexture);
                }
                SetColorSafe(mat, "_EmissionColor", emissionColor.Value);
                SetFloatSafe(mat, "_EmissionEffectScale", 1f);
            }
            else
            {
                SetFloatSafe(mat, "_UseEmission", 0f);
            }

            // Traditional Material Outline
            if (family == IllustrateShaderFamily.Outline || mat.HasProperty("_UseOutlines"))
            {
                bool useMatOutline = profile.outlines.outlineMode == VisualStyleOutlineMode.MaterialOutline;
                float mult = isCharacter ? profile.outlines.characterOutlineMultiplier : cat.outlineMultiplier;
                SetFloatSafe(mat, "_UseOutlines", useMatOutline && mult > 0.001f ? 1f : 0f);
                SetFloatSafe(mat, "_OutlineWidth", Mathf.Clamp(profile.outlines.outlineWidth * mult, 0f, 1f));
                SetFloatSafe(mat, "_NormalSurfaceOutline", profile.outlines.normalSurfaceOutline ? 1f : 0f);
                SetColorSafe(mat, "_OutlineColor", profile.outlines.outlineColor);
            }

            // Perturbation safeguards
            if (profile.materials.disableIllustratePerturbations)
            {
                for (int i = 0; i < PerturbationToggleProperties.Length; i++)
                {
                    SetFloatSafe(mat, PerturbationToggleProperties[i], 0f);
                }
            }
        }

        /// <summary>
        /// Updates only the semantic color channels inside an existing MaterialPropertyBlock for a specific slot,
        /// preserving all previously written properties by other gameplay systems.
        /// </summary>
        public static void ApplyColorBlockToSlot(
            Renderer renderer,
            int slotIndex,
            Color color,
            MaterialPropertyBlock reusableBlock = null)
        {
            if (renderer == null) return;
            var block = reusableBlock ?? new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, slotIndex);
            block.SetColor(MainColorId, color);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block, slotIndex);
        }

        /// <summary>
        /// Reads the primary color from a source material regardless of URP Lit, Standard, or Illustrate naming.
        /// </summary>
        public static Color ExtractBaseColor(Material source, Color fallback)
        {
            if (source == null) return fallback;
            if (source.HasProperty(MainColorId)) return source.GetColor(MainColorId);
            if (source.HasProperty(BaseColorId)) return source.GetColor(BaseColorId);
            if (source.HasProperty(ColorId)) return source.GetColor(ColorId);
            return fallback;
        }

        /// <summary>
        /// Reads the main texture and its tiling/offset from a source material.
        /// </summary>
        public static Texture ExtractMainTexture(Material source, out Vector2 scale, out Vector2 offset)
        {
            scale = Vector2.one;
            offset = Vector2.zero;
            if (source == null) return null;

            string[] props = { "_Texture", "_BaseMap", "_MainTex" };
            for (int i = 0; i < props.Length; i++)
            {
                if (source.HasProperty(props[i]))
                {
                    Texture tex = source.GetTexture(props[i]);
                    if (tex != null)
                    {
                        scale = source.GetTextureScale(props[i]);
                        offset = source.GetTextureOffset(props[i]);
                        return tex;
                    }
                }
            }
            return null;
        }

        private static Color ComputeAdjustedMainColor(Color baseColor, EffectiveCategorySettings cat, VisualStyleProfile profile, bool applyHsvInColor)
        {
            Color result = baseColor;
            Color.RGBToHSV(result, out float h, out float s, out float v);
            float contrast = Mathf.Clamp(profile.colorPalette.contrast, 0.5f, 1.5f);

            if (applyHsvInColor)
            {
                float satFactor = Mathf.Clamp(1f + cat.saturationDelta, 0f, 2f);
                float valDelta = profile.colorPalette.valueShift * 0.25f;
                s = Mathf.Clamp01(s * satFactor);
                v = Mathf.Clamp01(((v - 0.5f) * contrast + 0.5f) + valDelta);
            }
            else
            {
                v = Mathf.Clamp01((v - 0.5f) * contrast + 0.5f);
            }

            Color adjusted = Color.HSVToRGB(h, s, v);
            adjusted.a = result.a;
            return adjusted;
        }

        private static bool IsDetailedFoodRole(VisualSemanticRole role)
        {
            return role == VisualSemanticRole.PizzaDough ||
                   role == VisualSemanticRole.PizzaSauce ||
                   role == VisualSemanticRole.PizzaCheese ||
                   role == VisualSemanticRole.PizzaPepperoni ||
                   role == VisualSemanticRole.PizzaMushroom ||
                   role == VisualSemanticRole.PizzaBaked ||
                   role == VisualSemanticRole.FoodGeneric;
        }

        private static bool IsCharacterRole(VisualSemanticRole role)
        {
            return role == VisualSemanticRole.PlayerBody ||
                   role == VisualSemanticRole.PlayerApron ||
                   role == VisualSemanticRole.PlayerSkin ||
                   role == VisualSemanticRole.PlayerEye ||
                   role == VisualSemanticRole.PlayerPupil ||
                   role == VisualSemanticRole.NpcBody;
        }

        public static void SetFloatSafe(Material mat, string propertyName, float value)
        {
            if (mat != null && mat.HasProperty(propertyName))
            {
                if (!Mathf.Approximately(mat.GetFloat(propertyName), value))
                {
                    mat.SetFloat(propertyName, value);
                }
            }
        }

        public static void SetColorSafe(Material mat, string propertyName, Color value)
        {
            if (mat != null && mat.HasProperty(propertyName))
            {
                Color current = mat.GetColor(propertyName);
                if (Mathf.Abs(current.r - value.r) > 0.0005f ||
                    Mathf.Abs(current.g - value.g) > 0.0005f ||
                    Mathf.Abs(current.b - value.b) > 0.0005f ||
                    Mathf.Abs(current.a - value.a) > 0.0005f)
                {
                    mat.SetColor(propertyName, value);
                }
            }
        }

        public static void SetTextureSafe(Material mat, string propertyName, Texture value)
        {
            if (mat != null && mat.HasProperty(propertyName))
            {
                if (mat.GetTexture(propertyName) != value)
                {
                    mat.SetTexture(propertyName, value);
                }
            }
        }

        public static void SyncKeyword(Material mat, string keyword, bool enabled)
        {
            if (mat == null || string.IsNullOrEmpty(keyword)) return;
            bool current = mat.IsKeywordEnabled(keyword);
            if (current == enabled) return;
            if (enabled)
            {
                mat.EnableKeyword(keyword);
            }
            else
            {
                mat.DisableKeyword(keyword);
            }
        }
    }
}
