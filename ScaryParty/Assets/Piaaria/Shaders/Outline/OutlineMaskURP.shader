//
//  OutlineMaskURP.shader
//  Adapted for Universal Render Pipeline (URP 17+)
//  Inspired by QuickOutline (Chris Nolet)
//
Shader "ScaryParty/OutlineMaskURP" 
{
    Properties 
    {
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4 // 4 = LEqual
    }

    SubShader 
    {
        Tags 
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
            "DisableBatching" = "True"
        }

        Pass 
        {
            Name "OutlineMask"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off
            ZTest [_ZTest]
            ZWrite Off
            ColorMask 0

            Stencil 
            {
                Ref 1
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes 
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings 
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input) 
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target 
            {
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
