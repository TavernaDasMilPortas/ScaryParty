#ifndef SCARYPARTY_ILLUSTRATE_STYLIZED_CORE_INCLUDED
#define SCARYPARTY_ILLUSTRATE_STYLIZED_CORE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Globais controladas pelo TimeOfDayLightingDirector e RealtimeStylizedProvider
float _SP_DebugLightingView;
float _SP_DayNightBlend;
float _SP_GlobalShadowFloor;
float _SP_GlobalShadowLift;
float _SP_GlobalIndirectWeight;
float4 _SP_SkyTintColor;

CBUFFER_START(UnityPerMaterial)
    float4 _MainColor;
    float4 _Texture_ST;
    float4 _NormalMap_ST;

    float _CullMode;
    float _AlphaClipThreshold;
    float _AlphaCutoff;

    float _UseColorAdjustments;
    float _ClampAdjustments;
    float _SaturationShift;
    float _ValueShift;
    float _HueShift;
    float _Contrast;
    float _PosterizeColors;
    float _ColorNumbers;

    float _LightingMode;
    float _PosterizeLight;
    float _LightSteps;
    float _LightRamp;
    float _LightRampOffset;
    float4 _LightColor;
    float4 _ShadowColor;
    float _MultiplyByLightColor;
    float _UseShadows;
    float _ReceiveShadows;

    float _MinShadowBrightness;
    float _ShadowLift;
    float _IndirectDiffuseWeight;
    float _AdditionalLightsWeight;
    float _MaxAdditionalLightContribution;

    float4 _InteriorZoneFillColor;
    float _InteriorZoneWeight;

    float _AOStrength;
    float _UseVertexAO;
    float _EmissionMaskStrength;
    float4 _EmissionMaskColor;

    float _UseSpecular;
    float4 _SpecularColor;
    float _SpecularRamp;
    float _SpecularRampOffset;

    float _UseRimLighting;
    float4 _RimLightColor;
    float _RimRamp;
    float _RimLightRampOffset;
    float _RimLightLitIntensity;
    float _RimLightShadowIntensity;

    float _UseEmission;
    float4 _EmissionColor;
    float _EmissionEffectScale;

    float _UseOutlines;
    float _OutlineWidth;
    float _NormalSurfaceOutline;
    float4 _OutlineColor;
CBUFFER_END

TEXTURE2D(_Texture);            SAMPLER(sampler_Texture);
TEXTURE2D(_NormalMap);          SAMPLER(sampler_NormalMap);
TEXTURE2D(_EmissionTexture);    SAMPLER(sampler_EmissionTexture);
TEXTURE2D(_AOTexture);          SAMPLER(sampler_AOTexture);
TEXTURE2D(_EmissionMask);       SAMPLER(sampler_EmissionMask);

struct AttributesStylized
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float2 staticLightmapUV : TEXCOORD1;
    float4 color        : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VaryingsStylized
{
    float4 positionCS   : SV_POSITION;
    float2 uv           : TEXCOORD0;
    float3 positionWS   : TEXCOORD1;
    half3 normalWS      : TEXCOORD2;
    half4 tangentWS     : TEXCOORD3;
    float4 shadowCoord  : TEXCOORD4;
    half3 vertexSH      : TEXCOORD5;
    float fogFactor     : TEXCOORD6;
    half4 vertexColor   : TEXCOORD7;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

half3 ApplyHsvAdjust(half3 col, float hueShift, float satShift, float valShift, float contrast, float clampOut)
{
    // Conversao leve para ajuste artistico sem saturacao destrutiva
    float lum = dot(col, half3(0.2126h, 0.7152h, 0.0722h));
    half3 satCol = lerp(half3(lum, lum, lum), col, saturate(1.0h + satShift));
    half3 valCol = satCol * max(0.05h, 1.0h + valShift);
    half3 conCol = (valCol - 0.5h) * max(0.2h, contrast) + 0.5h;
    return clampOut > 0.5h ? saturate(conCol) : max(0.0h, conCol);
}

float ComputeToonBandFactor(float ndotl, float rampWidth, float rampOffset, float steps, float posterize)
{
    float halfLambert = ndotl * 0.5 + 0.5;
    float halfWidth = max(0.005, rampWidth * 0.5);
    float edge0 = rampOffset - halfWidth;
    float edge1 = rampOffset + halfWidth;
    float smoothBand = smoothstep(edge0, edge1, halfLambert);

    if (posterize > 0.5)
    {
        float stepCount = max(2.0, floor(steps + 0.5));
        float bucket = min(stepCount - 1.0, floor(smoothBand * stepCount));
        float stepped = bucket / (stepCount - 1.0);
        // Mapeia todas as bandas de [0..1] para manter o contraste entre sombra e luz direta
        return lerp(smoothBand, stepped, 0.90);
    }
    return smoothBand;
}

half3 EvaluateAdditionalToonLight(Light light, half3 normalWS, float ramp, float rampOffset, float steps, float posterize)
{
    half ndotl = dot(normalWS, light.direction);
    // Point/Spot lights fill interiors smoothly without concentric posterized bands on flat walls.
    float band = ComputeToonBandFactor(ndotl, max(0.45, ramp), rampOffset * 0.85, steps, 0.0);
    // Luzes adicionais nao devem escurecer o alvo nem ultrapassar atenuacao fisica
    float atten = saturate(light.distanceAttenuation * light.shadowAttenuation);
    // Contribui apenas no hemisferio voltado para a luz local
    float facingMask = saturate(ndotl * 0.65 + 0.35);
    return light.color * (band * facingMask * atten);
}

VaryingsStylized StylizedVert(AttributesStylized input)
{
    VaryingsStylized output = (VaryingsStylized)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.positionCS = posInputs.positionCS;
    output.positionWS = posInputs.positionWS;
    output.uv = TRANSFORM_TEX(input.texcoord, _Texture);
    output.normalWS = normInputs.normalWS;
    output.tangentWS = half4(normInputs.tangentWS, input.tangentOS.w);
    output.shadowCoord = GetShadowCoord(posInputs);
    output.vertexSH = SampleSHVertex(output.normalWS);
    output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
    output.vertexColor = input.color;
    return output;
}

half4 StylizedFrag(VaryingsStylized input, bool alphaClip, bool isSimpleMode)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 texSample = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, input.uv);
    half4 albedoAlpha = texSample * _MainColor;

#if defined(_ALPHATEST_ON)
    clip(albedoAlpha.a - max(_AlphaClipThreshold, _AlphaCutoff));
#endif
    if (alphaClip)
    {
        clip(albedoAlpha.a - max(_AlphaClipThreshold, _AlphaCutoff));
    }

    half3 baseAlbedo = albedoAlpha.rgb;
    if (_UseColorAdjustments > 0.5)
    {
        baseAlbedo = ApplyHsvAdjust(baseAlbedo, _HueShift, _SaturationShift, _ValueShift, _Contrast > 0.01 ? _Contrast : 1.0, _ClampAdjustments);
    }

    if (_PosterizeColors > 0.5 && _ColorNumbers > 1.0)
    {
        baseAlbedo = floor(baseAlbedo * _ColorNumbers + 0.5) / _ColorNumbers;
    }

    // Normal WS (somente quando _NORMALMAP esta habilitado)
    half3 normalWS = SafeNormalize(input.normalWS);
#if defined(_NORMALMAP)
    if (!isSimpleMode)
    {
        half4 nSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_Texture, input.uv);
        half3 normalTS = UnpackNormal(nSample);
        half3 tangentWS = SafeNormalize(input.tangentWS.xyz);
        half3 bitangentWS = SafeNormalize(cross(normalWS, tangentWS) * input.tangentWS.w);
        normalWS = SafeNormalize(mul(normalTS, half3x3(tangentWS, bitangentWS, normalWS)));
    }
#endif

    float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

    // AO: B1 baked/vertex AO + URP SSAO suavizada para cartoon
    half bakedAO = SAMPLE_TEXTURE2D(_AOTexture, sampler_Texture, input.uv).r;
    half vertAO = _UseVertexAO > 0.5 ? input.vertexColor.a : 1.0h;
    half modelAO = lerp(1.0h, min(bakedAO, vertAO), saturate(_AOStrength));

    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(screenUV, 1.0h);
    half indirectAO = min(modelAO, aoFactor.indirectAmbientOcclusion);

    // InputData para Forward+ Clustering & Shadows URP
    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = viewDirWS;
    inputData.normalizedScreenSpaceUV = screenUV;
    inputData.shadowCoord = input.shadowCoord;
    half4 shadowMask = CalculateShadowMask(inputData);

    // Luz Direcional Principal (preserva intensidade e cor reais ao longo do dia/noite!)
    Light mainLight = GetMainLight(input.shadowCoord, input.positionWS, shadowMask);
    half mainShadow = _ReceiveShadows > 0.5 && _UseShadows > 0.5 ? mainLight.shadowAttenuation : 1.0h;
    half ndotlMain = dot(normalWS, mainLight.direction);
    float rawMainBand = ComputeToonBandFactor(ndotlMain, _LightRamp, _LightRampOffset, _LightSteps, _PosterizeLight);

    // Dentro da pizzaria suaviza vazamentos de sombra do teto mantendo o relevo 3D (ndotl) nitido.
    float interiorMask = saturate(_InteriorZoneWeight * 3.0);
    half effectiveShadow = lerp(mainShadow, 1.0h, interiorMask * 0.80h);
    float effectiveMainBand = saturate(rawMainBand * lerp(1.0, (float)effectiveShadow, _UseShadows > 0.5 ? 1.0 : 0.0));
    float shadowedMainBand = effectiveMainBand;

    float floorMin = saturate(max(_MinShadowBrightness, _SP_GlobalShadowFloor));
    float lift = saturate(max(_ShadowLift, _SP_GlobalShadowLift));
    float indirectW = max(0.05, _IndirectDiffuseWeight * (_SP_GlobalIndirectWeight > 0.01 ? _SP_GlobalIndirectWeight : 1.0));

    half3 mainLightCol = _MultiplyByLightColor > 0.5 ? (mainLight.color * _LightColor.rgb) : _LightColor.rgb;
    half3 shRaw = SampleSHPixel(input.vertexSH, normalWS);
    half3 shClamped = min(shRaw * indirectW, half3(0.85h, 0.85h, 0.85h)) * indirectAO;
    half3 shadowTintCol = max(_ShadowColor.rgb, half3(floorMin, floorMin, floorMin));
    half3 ambientShadowBase = max(shClamped * 0.65h, half3(floorMin, floorMin, floorMin) * indirectAO);
    half3 shadowState = max(shadowTintCol * 0.72h, ambientShadowBase * shadowTintCol * 1.35h) + (_ShadowColor.rgb * lift * 0.25h);
    half3 litState = max(mainLightCol, shClamped * 0.95h);
    half3 mainLightContrib = lerp(shadowState, litState, effectiveMainBand);

    // Luzes Adicionais (Forward e Forward+ Cluster Loop)
    half3 additionalContrib = half3(0.0h, 0.0h, 0.0h);
#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0u; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light addLight = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
        additionalContrib += EvaluateAdditionalToonLight(addLight, normalWS, _LightRamp, _LightRampOffset, _LightSteps, _PosterizeLight);
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light addLight = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
        additionalContrib += EvaluateAdditionalToonLight(addLight, normalWS, _LightRamp, _LightRampOffset, _LightSteps, _PosterizeLight);
    LIGHT_LOOP_END
#endif

    half3 clampedAdditional = min(additionalContrib * _AdditionalLightsWeight, half3(_MaxAdditionalLightContribution, _MaxAdditionalLightContribution, _MaxAdditionalLightContribution));

    // Indirect / SH + Ambient Floor + Zona Interior da Pizzaria
    half3 floorIndirect = ambientShadowBase + (_ShadowColor.rgb * lift * 0.25h);
    half3 interiorFill = _InteriorZoneFillColor.rgb * saturate(_InteriorZoneWeight);
    half3 indirectAndZone = floorIndirect + interiorFill;

    // Rim Light & Specular estilizados
    half3 rimSpecContrib = half3(0.0h, 0.0h, 0.0h);
    if (!isSimpleMode && _UseRimLighting > 0.5)
    {
        float fresnel = 1.0 - saturate(dot(normalWS, viewDirWS));
        float rimBand = smoothstep(saturate(_RimLightRampOffset - _RimRamp * 0.35), saturate(_RimLightRampOffset + _RimRamp * 0.35 + 0.001), fresnel);
        float rimStrength = lerp(_RimLightShadowIntensity, _RimLightLitIntensity, shadowedMainBand);
        rimSpecContrib += _RimLightColor.rgb * (rimBand * rimStrength);
    }
    if (!isSimpleMode && _UseSpecular > 0.5)
    {
        half3 halfVec = SafeNormalize(mainLight.direction + viewDirWS);
        float specDot = saturate(dot(normalWS, halfVec));
        float specBand = smoothstep(_SpecularRampOffset, min(0.999, _SpecularRampOffset + max(0.01, _SpecularRamp)), specDot);
        rimSpecContrib += _SpecularColor.rgb * (specBand * shadowedMainBand * mainLight.color * 0.5);
    }

    // Emissao (base + B1 EmissionMask para fachadas/janelas/letreiros procedurais via MPB)
    half3 emissionContrib = half3(0.0h, 0.0h, 0.0h);
    if (_UseEmission > 0.5)
    {
        half3 emTex = SAMPLE_TEXTURE2D(_EmissionTexture, sampler_Texture, input.uv).rgb;
        emissionContrib += emTex * _EmissionColor.rgb * max(1.0h, _EmissionEffectScale);
    }
    if (_EmissionMaskStrength > 0.001)
    {
        half emMask = SAMPLE_TEXTURE2D(_EmissionMask, sampler_Texture, input.uv).r;
        emissionContrib += emMask * _EmissionMaskColor.rgb * _EmissionMaskStrength;
    }

    // Modos de debug tecnico (_SP_DebugLightingView)
    int debugMode = (int)round(_SP_DebugLightingView);
    if (debugMode == 1)
    {
        return half4(baseAlbedo, albedoAlpha.a);
    }
    if (debugMode == 2)
    {
        return half4(baseAlbedo * mainLightContrib, albedoAlpha.a);
    }
    if (debugMode == 3)
    {
        return half4(baseAlbedo * clampedAdditional, albedoAlpha.a);
    }
    if (debugMode == 4)
    {
        return half4(baseAlbedo * indirectAndZone, albedoAlpha.a);
    }
    if (debugMode == 5)
    {
        half b = saturate(shadowedMainBand);
        return half4(b, b, b, albedoAlpha.a);
    }

    half shadowFillMask = lerp(0.75h, 0.25h, (half)effectiveMainBand);
    half3 localFill = (clampedAdditional * 0.55h + interiorFill * 0.45h) * shadowFillMask;
    half3 rawLightSum = mainLightContrib + localFill;
    half3 overThreshold = max(rawLightSum - 1.05h, half3(0.0h, 0.0h, 0.0h));
    half3 softKneeSum = min(rawLightSum, half3(1.05h, 1.05h, 1.05h)) + (overThreshold / (1.0h + overThreshold * 2.5h));

    half3 combinedLighting = (_LightingMode < 0.5)
        ? half3(1.0h, 1.0h, 1.0h) // Unlit mode option
        : softKneeSum;

    half3 finalRgb = baseAlbedo * combinedLighting + rimSpecContrib + emissionContrib;
    finalRgb = MixFog(finalRgb, input.fogFactor);
    return half4(finalRgb, albedoAlpha.a);
}

#endif
