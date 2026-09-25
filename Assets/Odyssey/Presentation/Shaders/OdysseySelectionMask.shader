// The selected thing, drawn again into the selection mask (docs/design/44-selection-highlight.md).
//
// Two passes per draw, both against the camera's own depth attachment, which is bound read-only:
//   Visible     ZTest LEqual  -> R = strength, B = strength * fill (a tile's top face)
//   Silhouette  ZTest Always  -> G = strength
// Max blending, so the parts of one colonist, and several colonists, combine rather than add.
//
// The strength and the fill arrive as globals set before each draw, because a renderer drawn with
// DrawRenderer takes no property block; the albedo and cut-off of alpha-clipped art are on a copy of
// the material made per texture. A cut-off of zero is an opaque part and samples nothing.
Shader "Odyssey/SelectionMask"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        float _SelectionStrength;
        float _SelectionFill;
        float _SelectionCutoff;
        TEXTURE2D(_SelectionAlbedo);
        SAMPLER(sampler_SelectionAlbedo);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.uv;
            return output;
        }

        void ClipToArt(float2 uv)
        {
            if (_SelectionCutoff > 0.0)
                clip(SAMPLE_TEXTURE2D(_SelectionAlbedo, sampler_SelectionAlbedo, uv).a - _SelectionCutoff);
        }
        ENDHLSL

        Pass
        {
            Name "Visible"
            ZTest LEqual
            ZWrite Off
            Cull Off
            // Pulled a hair towards the camera, so the thing does not lose a depth tie with itself.
            Offset -1, -1
            BlendOp Max
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                ClipToArt(input.uv);
                return half4(_SelectionStrength, 0, _SelectionStrength * _SelectionFill, 0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Silhouette"
            ZTest Always
            ZWrite Off
            Cull Off
            BlendOp Max
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                ClipToArt(input.uv);
                return half4(0, _SelectionStrength, 0, 0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
