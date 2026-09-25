// The selection highlight laid over the frame, from the mask (docs/design/44-selection-highlight.md).
//
// Overlays, not a copy of the frame: neither pass reads the scene, so neither needs an intermediate
// texture, and both are scissored to the selection's own rectangle on screen.
//
//   Pass 0, Line   alpha-blended in the one colour:
//                    outside the silhouette   the line - max(visible ring, hidden ring * _HiddenAlpha)
//                    inside a tile's top face the wash - _FillAlpha
//   Pass 1, Lift   multiplied: the visible part of the thing made _Lift brighter, hue kept.
//
// **The lift multiplies rather than blends towards white**, and that was measured, not chosen: a
// white blend in linear light raises a near-zero channel furthest, so an orange jumpsuit lifted by
// "3.5 per cent" came out visibly washed towards cream (photographed against a control, 2026-09-25).
// Multiplying brightens every channel in proportion and leaves the colour the colour it was.
//
// The mask is sampled bilinearly, so its edges are soft and the line comes out anti-aliased.
Shader "Odyssey/SelectionComposite"
{
    Properties
    {
        _SelectionColour ("Colour", Color) = (1, 1, 1, 1)
        _Radius ("Line width in pixels", Float) = 2.5
        _HiddenAlpha ("Strength of the line through a wall", Float) = 0.4
        _Lift ("Brightening of the visible part", Float) = 0.12
        _FillAlpha ("Wash over a tile", Float) = 0.16
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float4 _SelectionColour;
        float _Radius;
        float _HiddenAlpha;
        float _Lift;
        float _FillAlpha;

        half3 Mask(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
        }
        ENDHLSL

        Pass
        {
            Name "Line"
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // Two rings of twelve: the outer one sets the width, the inner one catches a thing
            // thinner than the gap between two outer taps.
            static const int Taps = 12;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half3 centre = Mask(uv);

                float2 texel = _BlitTexture_TexelSize.xy * _Radius;
                half visible = 0, hidden = 0;
                [unroll]
                for (int i = 0; i < Taps; i++)
                {
                    float angle = (i + 0.5) * (6.2831853 / Taps);
                    float2 dir = float2(cos(angle), sin(angle));
                    half3 outer = Mask(uv + dir * texel);
                    half3 inner = Mask(uv + dir * texel * 0.5);
                    visible = max(visible, max(outer.r, inner.r));
                    hidden = max(hidden, max(outer.g, inner.g));
                }

                // The line lies outside the silhouette only: over the thing's own pixels, and over
                // whatever stands in front of the part of it that is hidden, there is none.
                half outside = 1.0 - saturate(centre.g);
                half edge = max(visible, hidden * _HiddenAlpha) * outside;

                half alpha = saturate(max(edge, centre.b * _FillAlpha)) * _SelectionColour.a;
                return half4(_SelectionColour.rgb, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Lift"
            // destination + destination * source: the frame made brighter where the thing is seen.
            Blend DstColor One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half lift = Mask(input.texcoord).r * _Lift;
                return half4(lift, lift, lift, 0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
