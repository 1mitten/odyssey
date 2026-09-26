// The dream's blur (docs/design/56-wake-up.md §4): a dual-filter blur, halved down a short chain
// and doubled back up, then laid over the frame at the strength the wake asks for.
//
//   Pass 0, Down      five taps: the centre and four diagonal corners, read from the level above.
//   Pass 1, Up        eight taps in a tent, read from the level below.
//   Pass 2, Lay       the blurred picture blended over the camera colour, alpha = _WakeStrength.
//
// **Our own rather than URP's depth of field** (§4a): URP's Bokeh radius is capped at twenty
// pixels of the screen's height (GetMaxBokehRadiusInPixels) and its Gaussian at 1.5, so at 4K both
// read as "soft" rather than "asleep". A dual-filter chain's radius grows with its depth, and the
// depth is chosen from the screen's height, so the dream is the same dream at 1080 and at 2160.
//
// _WakeOffset spreads the taps: 1 is the textbook filter, and the wake scales it down with the
// strength so the blur shrinks as it fades rather than merely thinning.
Shader "Odyssey/WakeBlur"
{
    Properties
    {
        _WakeOffset ("Tap spread", Float) = 1
        _WakeStrength ("How much of the blur is laid over the frame", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float _WakeOffset;
        float _WakeStrength;
        // _BlitTexture_TexelSize comes from Blit.hlsl, as it does for the outline.

        half3 Tap(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
        }
        ENDHLSL

        Pass
        {
            Name "Down"
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 o = _BlitTexture_TexelSize.xy * 0.5 * _WakeOffset;
                half3 c = Tap(uv) * 4.0;
                c += Tap(uv + float2(-o.x, -o.y));
                c += Tap(uv + float2( o.x, -o.y));
                c += Tap(uv + float2(-o.x,  o.y));
                c += Tap(uv + float2( o.x,  o.y));
                return half4(c * 0.125, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Up"
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 o = _BlitTexture_TexelSize.xy * 0.5 * _WakeOffset;
                half3 c = Tap(uv + float2(-o.x * 2.0, 0.0));
                c += Tap(uv + float2( o.x * 2.0, 0.0));
                c += Tap(uv + float2(0.0, -o.y * 2.0));
                c += Tap(uv + float2(0.0,  o.y * 2.0));
                c += Tap(uv + float2(-o.x,  o.y)) * 2.0;
                c += Tap(uv + float2( o.x,  o.y)) * 2.0;
                c += Tap(uv + float2(-o.x, -o.y)) * 2.0;
                c += Tap(uv + float2( o.x, -o.y)) * 2.0;
                return half4(c / 12.0, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Lay"
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(Tap(input.texcoord), saturate(_WakeStrength));
            }
            ENDHLSL
        }
    }
}
