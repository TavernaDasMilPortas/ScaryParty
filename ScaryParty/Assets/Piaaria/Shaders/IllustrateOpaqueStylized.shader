Shader "ScaryParty/Illustrate Stylized/Opaque"
{
    Properties
    {
        _Texture ("Texture", 2D) = "white" {}
        _MainColor ("Main Color", Color) = (1,1,1,1)
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _CullMode ("Cull Mode", Float) = 2

        [Toggle] _UseColorAdjustments ("Use Color Adjustments", Float) = 0
        [Toggle] _ClampAdjustments ("Clamp Adjustments", Float) = 1
        _SaturationShift ("Saturation Shift", Range(-1, 1)) = 0
        _ValueShift ("Value Shift", Range(-1, 1)) = 0
        _HueShift ("Hue Shift", Range(-1, 1)) = 0
        _Contrast ("Contrast", Range(0.2, 2)) = 1
        [Toggle] _PosterizeColors ("Posterize Colors", Float) = 0
        _ColorNumbers ("Color Numbers", Float) = 6

        _LightingMode ("Lighting Mode", Float) = 1
        [Toggle] _PosterizeLight ("Posterize Light", Float) = 1
        _LightSteps ("Light Steps", Float) = 3
        _LightRamp ("Light Ramp", Range(0, 2)) = 0.85
        _LightRampOffset ("Light Ramp Offset", Range(0, 1)) = 0.5
        _LightColor ("Light Color Tint", Color) = (1,1,1,1)
        _ShadowColor ("Shadow Color", Color) = (0.45,0.44,0.56,1)
        [Toggle] _MultiplyByLightColor ("Multiply By Light Color", Float) = 1
        [Toggle] _UseShadows ("Use Shadows", Float) = 1
        [Toggle] _ReceiveShadows ("Receive Shadows", Float) = 1

        _MinShadowBrightness ("Min Shadow Brightness", Range(0, 1)) = 0.28
        _ShadowLift ("Shadow Lift", Range(0, 1)) = 0.08
        _IndirectDiffuseWeight ("Indirect Diffuse Weight", Range(0, 2)) = 1.0
        _AdditionalLightsWeight ("Additional Lights Weight", Range(0, 3)) = 1.0
        _MaxAdditionalLightContribution ("Max Additional Light Contribution", Range(0.1, 4)) = 1.65

        _InteriorZoneFillColor ("Interior Zone Fill Color", Color) = (0,0,0,0)
        _InteriorZoneWeight ("Interior Zone Weight", Range(0, 1)) = 0

        _AOTexture ("AO Texture (B1)", 2D) = "white" {}
        _AOStrength ("AO Strength", Range(0, 1)) = 0.25
        [Toggle] _UseVertexAO ("Use Vertex AO", Float) = 0
        _EmissionMask ("Emission Mask (B1)", 2D) = "white" {}
        _EmissionMaskStrength ("Emission Mask Strength", Range(0, 8)) = 0
        _EmissionMaskColor ("Emission Mask Color", Color) = (1,0.86,0.58,1)

        [Toggle] _UseSpecular ("Use Specular", Float) = 0
        _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        _SpecularRamp ("Specular Ramp", Range(0, 2)) = 0.1
        _SpecularRampOffset ("Specular Ramp Offset", Range(0, 1)) = 0.9

        [Toggle] _UseRimLighting ("Use Rim Lighting", Float) = 1
        _RimLightColor ("Rim Light Color", Color) = (0.85,0.9,1,1)
        _RimRamp ("Rim Ramp", Range(0, 2)) = 1.0
        _RimLightRampOffset ("Rim Light Ramp Offset", Range(0, 1)) = 0.58
        _RimLightLitIntensity ("Rim Lit Intensity", Range(0, 1)) = 0.35
        _RimLightShadowIntensity ("Rim Shadow Intensity", Range(0, 1)) = 0.12

        [Toggle] _UseEmission ("Use Emission", Float) = 0
        _EmissionTexture ("Emission Texture", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _EmissionEffectScale ("Emission Scale", Float) = 1

        [Toggle] _UseOutlines ("Use Outlines", Float) = 0
        _OutlineWidth ("Outline Width", Float) = 0.05
        [Toggle] _NormalSurfaceOutline ("Normal Surface Outline", Float) = 0
        _OutlineColor ("Outline Color", Color) = (0.14,0.12,0.16,1)

        [HideInInspector] _AlphaClipThreshold ("Alpha Clip Threshold", Float) = 0.5
        [HideInInspector] _AlphaCutoff ("Alpha Cutoff", Float) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_CullMode]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _NORMALMAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "IllustrateStylizedCore.hlsl"

            VaryingsStylized vert(AttributesStylized input) { return StylizedVert(input); }
            half4 frag(VaryingsStylized input) : SV_Target { return StylizedFrag(input, false, false); }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_CullMode]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_CullMode]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_CullMode]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}
