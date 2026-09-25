// The marks a player steers by (design 33 §23): the drafted diamond, the order line, the landing
// and lock-on rings, the hostile marker, and the brackets round a selected colonist or animal.
//
// Why two passes. The owner, 2026-09-25: "always make sure these lines/selections when you move a
// colonist under draft that you can see them and they aren't obscured by terrain, bushes, trees".
// The bracket's material was depth-tested like any solid, so the meadow's grass, a bank or a tree
// between the camera and the mark hid it. Drawing over everything (the power lines' answer) would
// also draw a ring on the ground across the colonist standing in it and lose which is in front.
// So the mark is drawn twice, with depth tests that never both pass for one pixel:
//   - where nothing is in front of it, as it always was (ZTest LEqual);
//   - where something is, through it at _HiddenStrength of its opacity (ZTest Greater).
// Both after everything else in the frame (Transparent+100, after the foliage and the water), so
// every occluder is already in the depth buffer when the mark is tested against it.
//
// Two LightModes, because URP draws a renderer's SRPDefaultUnlit pass and its UniversalForward
// pass both, in that order; two passes under one LightMode would draw only the first.
//
// Unlit, with the power line's top-lit shade: a steering mark is a colour that means something,
// and a shadow or a dusk that dimmed it would hide it by another route.
Shader "Odyssey/SeeThroughMark"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _HiddenStrength ("Opacity where hidden", Range(0, 1)) = 0.5
        _Brightness ("Brightness", Range(0.5, 2)) = 1.2
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        float _HiddenStrength;
        float _Brightness;
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
        float3 n = TransformObjectToWorldNormal(input.normalOS);
        output.shade = 0.8 + 0.2 * saturate(n.y);
        return output;
    }

    half4 Shade(Varyings input, float strength)
    {
        UNITY_SETUP_INSTANCE_ID(input);
        return half4(saturate(_BaseColor.rgb * input.shade * _Brightness), _BaseColor.a * strength);
    }

    half4 FragHidden(Varyings input) : SV_Target { return Shade(input, _HiddenStrength); }
    half4 FragSeen(Varyings input) : SV_Target { return Shade(input, 1.0); }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Hidden"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            ZWrite Off
            ZTest Greater
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragHidden
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "Seen"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite Off
            ZTest LEqual
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragSeen
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
