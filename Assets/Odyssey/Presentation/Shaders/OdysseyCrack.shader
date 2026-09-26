// Cracks (design 57): a damaged wall or a face being mined, drawn by drawing the thing's own mesh a
// second time over itself with this shader.
//
// A multiply, not a colour. `Blend DstColor Zero` scales what is already on screen, so the wall
// keeps its material, its tint, its shadows and its dusk and only gains the damage: a crack is the
// surface made darker along a line, which is what a crack looks like, and it reads the same at noon
// and at night without this shader knowing anything about light.
//
// The pattern is OdysseyCrack.hlsl's — rays from an impact point per cell with branches forking
// off them — shared with Odyssey/Shard so the pieces a wall falls into carry its cracks. One
// number drives it, `_Severity`, the level over the ladder's length; the renderer keeps a material
// per level.
//
// Drawn exactly on the surface it cracks: ZTest LEqual with a depth offset towards the camera, so a
// coincident copy of the same triangles always wins against itself and never against anything
// really in front. No depth write, no shadows, no light: nothing else in the frame reads it.
Shader "Odyssey/Crack"
{
    Properties
    {
        _Severity ("How broken, 0 to 1", Range(0, 1)) = 0.5
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "OdysseyCrack.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float _Severity;
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
        float3 positionWS : TEXCOORD0;
        float3 normalWS : TEXCOORD1;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);
        VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
        output.positionCS = position.positionCS;
        output.positionWS = position.positionWS;
        output.normalWS = TransformObjectToWorldNormal(input.normalOS);
        return output;
    }

    half4 Frag(Varyings input) : SV_Target
    {
        UNITY_SETUP_INSTANCE_ID(input);
        float multiply = OdysseyCrackMultiply(input.positionWS, normalize(input.normalWS), _Severity);
        return half4(multiply.xxx, 1.0);
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Crack"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Back
            Blend DstColor Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
