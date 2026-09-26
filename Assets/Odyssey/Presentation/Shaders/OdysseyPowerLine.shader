// The power lines (design 32 §9): a flat colour drawn over everything.
//
// Why depth-test Always. A line runs through walls and under floors — decision 2 — and the
// owner's decision 6 is that a shown line is seen wherever it is. A depth-tested line inside a
// wall is a line the wall hides, which is the one place a player most needs to see it. So the
// pass ignores the depth buffer and draws last, in the Overlay queue, after everything it must
// show through.
//
// Why a shader of our own rather than URP's Unlit with a property flipped. URP's Unlit carries
// its depth test as a fixed state on some versions and a property on others, and a material that
// sets a property the shader does not read is a line that silently goes back behind the walls.
// A dozen lines of our own say what they do.
//
// Instanced, with the colour per material rather than per instance: the lines are bucketed by
// state (live, dark, idle, ordered, marked) and each bucket is one call, so there are five
// materials and never a per-instance property to keep alive in a build.
Shader "Odyssey/PowerLine"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Overlay"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "PowerLine"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite Off
            ZTest Always
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float shade : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // A touch of shape so a line reads as a rod rather than a flat strip: the top face
                // full, the sides a little darker. Not lighting — the colour is the state, and a
                // light that could darken a live line to look dark would be lying.
                float3 n = TransformObjectToWorldNormal(input.normalOS);
                output.shade = 0.78 + 0.22 * saturate(n.y);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return half4(_BaseColor.rgb * input.shade, _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
