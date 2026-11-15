Shader "ScaryParty/Illustrate Stylized/ScreenSpaceOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.133, 0.157, 0.192, 1)
        _Width ("Width", Range(0, 5)) = 1.2
        [ToggleUI] _USEDEPTHTEST ("Use Depth Test", Float) = 1
        _Depth_Threshold ("Depth Threshold", Range(0.5, 12)) = 5.4
        [ToggleUI] _USENORMALSTEST ("Use Normals Test", Float) = 1
        _NormalThreshold ("Normal Threshold", Range(0.05, 2)) = 0.45
        [ToggleUI] _USECOLORTEST ("Use Color Test", Float) = 1
        _ColorThreshold ("Color Threshold", Range(0.05, 2)) = 0.32
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "IllustrateScreenSpaceOutline"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragOutline
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            // Guard against materials carrying legacy shader keywords with matching float property names.
            #undef _USEDEPTHTEST
            #undef _USENORMALSTEST
            #undef _USECOLORTEST

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _Width;
                float _USEDEPTHTEST;
                float _Depth_Threshold;
                float _USENORMALSTEST;
                float _NormalThreshold;
                float _USECOLORTEST;
                float _ColorThreshold;
            CBUFFER_END

            float GetOutlineEyeDepth(float rawDepth)
            {
                if (unity_OrthoParams.w > 0.5)
                {
                    return LinearDepthToEyeDepth(rawDepth);
                }
                return LinearEyeDepth(rawDepth, _ZBufferParams);
            }

            float ComputeLinearDepthPlaneCoord(float eyeDepth)
            {
                // Perspective 1/Z is linear across any 3D plane in screen space; Ortho Z is already linear.
                return (unity_OrthoParams.w > 0.5) ? eyeDepth : rcp(max(eyeDepth, 0.05));
            }

            bool IsSkyRawDepth(float rawDepth)
            {
            #if UNITY_REVERSED_Z
                return rawDepth <= 0.00001;
            #else
                return rawDepth >= 0.99999;
            #endif
            }

            half3 SampleBlitColorSafe(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, UnityStereoTransformScreenSpaceTex(clamp(uv, 0.0001, 0.9999)), 0).rgb;
            }

            half4 FragOutline(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;
                half4 baseCol = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, UnityStereoTransformScreenSpaceTex(uv), 0);

                if (_Width <= 0.001)
                {
                    return baseCol;
                }

                float stepPx = clamp(_Width, 0.5, 5.0);
                float2 texel = _ScreenSize.zw * stepPx;

                float2 uvL = clamp(uv + float2(-texel.x,  0.0), 0.0001, 0.9999);
                float2 uvR = clamp(uv + float2( texel.x,  0.0), 0.0001, 0.9999);
                float2 uvU = clamp(uv + float2( 0.0,  texel.y), 0.0001, 0.9999);
                float2 uvD = clamp(uv + float2( 0.0, -texel.y), 0.0001, 0.9999);

                // Diagonal samples when width > 1.25 to prevent diagonal pinching and keep corners solid.
                float2 diagTexel = texel * 0.7071;
                float2 uvTL = clamp(uv + float2(-diagTexel.x,  diagTexel.y), 0.0001, 0.9999);
                float2 uvTR = clamp(uv + float2( diagTexel.x,  diagTexel.y), 0.0001, 0.9999);
                float2 uvBL = clamp(uv + float2(-diagTexel.x, -diagTexel.y), 0.0001, 0.9999);
                float2 uvBR = clamp(uv + float2( diagTexel.x, -diagTexel.y), 0.0001, 0.9999);

                float rawC = SampleSceneDepth(uv);
                float rawL = SampleSceneDepth(uvL);
                float rawR = SampleSceneDepth(uvR);
                float rawU = SampleSceneDepth(uvU);
                float rawD = SampleSceneDepth(uvD);

                bool skyC = IsSkyRawDepth(rawC);
                bool skyL = IsSkyRawDepth(rawL);
                bool skyR = IsSkyRawDepth(rawR);
                bool skyU = IsSkyRawDepth(rawU);
                bool skyD = IsSkyRawDepth(rawD);

                if (skyC && skyL && skyR && skyU && skyD)
                {
                    return baseCol;
                }

                float dC = GetOutlineEyeDepth(rawC);
                float dL = GetOutlineEyeDepth(rawL);
                float dR = GetOutlineEyeDepth(rawR);
                float dU = GetOutlineEyeDepth(rawU);
                float dD = GetOutlineEyeDepth(rawD);

                float edgeFactor = 0.0;

                if (_USEDEPTHTEST > 0.5)
                {
                    float wC = ComputeLinearDepthPlaneCoord(dC);
                    float wL = ComputeLinearDepthPlaneCoord(dL);
                    float wR = ComputeLinearDepthPlaneCoord(dR);
                    float wU = ComputeLinearDepthPlaneCoord(dU);
                    float wD = ComputeLinearDepthPlaneCoord(dD);

                    // Plane-invariant 2nd difference on 1/Z: zero on flat floors/ceilings/streets at any grazing angle.
                    float lapX = abs(wR + wL - 2.0 * wC);
                    float lapY = abs(wU + wD - 2.0 * wC);
                    float minNeighbor = min(min(dL, dR), min(dU, dD));

                    float depthLap = lapX + lapY;

                    if (_Width > 1.25)
                    {
                        float rawTL = SampleSceneDepth(uvTL);
                        float rawTR = SampleSceneDepth(uvTR);
                        float rawBL = SampleSceneDepth(uvBL);
                        float rawBR = SampleSceneDepth(uvBR);
                        float dTL = GetOutlineEyeDepth(rawTL);
                        float dTR = GetOutlineEyeDepth(rawTR);
                        float dBL = GetOutlineEyeDepth(rawBL);
                        float dBR = GetOutlineEyeDepth(rawBR);
                        float wTL = ComputeLinearDepthPlaneCoord(dTL);
                        float wTR = ComputeLinearDepthPlaneCoord(dTR);
                        float wBL = ComputeLinearDepthPlaneCoord(dBL);
                        float wBR = ComputeLinearDepthPlaneCoord(dBR);
                        float lapDiag1 = abs(wTR + wBL - 2.0 * wC);
                        float lapDiag2 = abs(wTL + wBR - 2.0 * wC);
                        depthLap = max(depthLap, lapDiag1 + lapDiag2);
                        minNeighbor = min(minNeighbor, min(min(dTL, dTR), min(dBL, dBR)));
                    }

                    float refDist = min(dC, minNeighbor);
                    float scaleRef = (unity_OrthoParams.w > 0.5) ? 1.0 : refDist;
                    depthLap *= scaleRef;

                    float fgMask = saturate((minNeighbor - dC) * 4.0 + 1.0);
                    float fgWeight = lerp(0.55, 1.0, fgMask);

                    float depthThresh = max(0.008, _Depth_Threshold * 0.0045);
                    float distAtten = smoothstep(85.0, 25.0, refDist);
                    float depthEdge = smoothstep(depthThresh * 0.80, depthThresh * 1.15, depthLap * fgWeight) * distAtten;
                    edgeFactor = max(edgeFactor, depthEdge);
                }

                if (_USENORMALSTEST > 0.5 && !skyC)
                {
                    float3 nC = SampleSceneNormals(uv);
                    float3 nL = skyL ? nC : SampleSceneNormals(uvL);
                    float3 nR = skyR ? nC : SampleSceneNormals(uvR);
                    float3 nU = skyU ? nC : SampleSceneNormals(uvU);
                    float3 nD = skyD ? nC : SampleSceneNormals(uvD);

                    float normalGrad = length(nR - nL) + length(nU - nD);

                    if (_Width > 1.25)
                    {
                        float3 nTL = SampleSceneNormals(uvTL);
                        float3 nTR = SampleSceneNormals(uvTR);
                        float3 nBL = SampleSceneNormals(uvBL);
                        float3 nBR = SampleSceneNormals(uvBR);
                        float diagNorm = (length(nTR - nBL) + length(nTL - nBR)) * 0.85;
                        normalGrad = max(normalGrad, diagNorm);
                    }

                    float normThresh = max(0.15, _NormalThreshold);
                    float normDistAtten = smoothstep(70.0, 20.0, dC);
                    float normalEdge = smoothstep(normThresh * 0.80, normThresh * 1.12, normalGrad) * normDistAtten;
                    edgeFactor = max(edgeFactor, normalEdge);
                }

                if (_USECOLORTEST > 0.5)
                {
                    half3 cL = saturate(SampleBlitColorSafe(uvL));
                    half3 cR = saturate(SampleBlitColorSafe(uvR));
                    half3 cU = saturate(SampleBlitColorSafe(uvU));
                    half3 cD = saturate(SampleBlitColorSafe(uvD));

                    float colorGrad = length((float3)(cR - cL)) + length((float3)(cU - cD));
                    float colorThresh = max(0.12, _ColorThreshold);
                    float colorDistAtten = smoothstep(60.0, 18.0, dC);
                    float colorEdge = smoothstep(colorThresh * 0.85, colorThresh * 1.15, colorGrad) * colorDistAtten;
                    edgeFactor = max(edgeFactor, colorEdge);
                }

                float finalAlpha = saturate(edgeFactor * 1.25 * _OutlineColor.a);
                half3 outRgb = lerp(baseCol.rgb, _OutlineColor.rgb, (half)finalAlpha);
                return half4(outRgb, baseCol.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
