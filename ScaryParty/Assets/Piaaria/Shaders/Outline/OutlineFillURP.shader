//
//  OutlineFillURP.shader
//  Adapted for Universal Render Pipeline (URP 17+)
//  Inspired by QuickOutline (Chris Nolet)
//
Shader "ScaryParty/OutlineFillURP" 
{
    Properties 
    {
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4 // 4 = LEqual
        _OutlineColor("Outline Color", Color) = (1, 0.85, 0.2, 1)
        _OutlineWidth("Outline Width", Range(0, 10)) = 2.5
    }

    SubShader 
    {
        Tags 
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+110"
            "RenderType" = "Transparent"
            "DisableBatching" = "True"
        }

        Pass 
        {
            Name "OutlineFill"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off
            ZTest [_ZTest]
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB

            Stencil 
            {
                Ref 1
                Comp NotEqual
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes 
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 smoothNormalOS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings 
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes input) 
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 normalOS = any(input.smoothNormalOS) ? input.smoothNormalOS : input.normalOS;
                
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                
                float3 posVS = TransformWorldToView(posWS);
                float3 normalVS = TransformWorldToViewDir(normalWS, true);

                float depthFactor = max(-posVS.z, 0.05);
                posVS += normalVS * (depthFactor * _OutlineWidth * 0.001);

                output.positionCS = TransformWViewToHClip(posVS);
                output.color = _OutlineColor;

                return output;
            }

            half4 frag(Varyings input) : SV_Target 
            {
                return input.color;
            }
            ENDHLSL
        }
    }
}
