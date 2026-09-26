// Cracks (design 57): a damaged wall or a face being mined, drawn by drawing the thing's own mesh a
// second time over itself with this shader.
//
// A multiply, not a colour. `Blend DstColor Zero` scales what is already on screen, so the wall
// keeps its material, its tint, its shadows and its dusk and only gains the damage: a crack is the
// surface made darker along a line, which is what a crack looks like, and it reads the same at noon
// and at night without this shader knowing anything about light.
//
// The pattern is made here, not painted. The crack lines are the borders of a Voronoi tiling in
// world space (the exact border distance, so a line has a width in metres), projected three ways
// and blended by the normal, so a wall's two faces and a rock's top and sides all crack, and two
// cracked walls side by side carry one network across the joint. The tiling is warped by a value
// noise so its borders run jagged, and a slower noise decides which stretches of them show —
// `_Coverage` of the surface — so a hairline stage is a few cracks and a crumbling one is all of
// them, and the same wall grows its cracks further rather than swapping one pattern for another.
// `_Fine` adds a second, finer network for the later stages; `_Grime` darkens the whole surface a
// little, which is what still reads from far off once the lines are thinner than a pixel.
//
// Drawn exactly on the surface it cracks: ZTest LEqual with a depth offset towards the camera, so a
// coincident copy of the same triangles always wins against itself and never against anything
// really in front. No depth write, no shadows, no light: nothing else in the frame reads it.
//
// Antialiased by the line's own screen footprint (fwidth), and faded as a line gets thinner than a
// pixel, so a zoomed-out colony does not shimmer.
Shader "Odyssey/Crack"
{
    Properties
    {
        _Coverage ("Share of the crack network shown", Range(0, 1)) = 0.5
        _Width ("Crack width (m)", Range(0.002, 0.1)) = 0.02
        _Darkness ("Multiply at a crack's centre", Range(0, 1)) = 0.3
        _Grime ("Darkening of the whole surface", Range(0, 0.5)) = 0.05
        _Fine ("Share of the finer network shown", Range(0, 1)) = 0
        _Scale ("Cracks per metre", Float) = 0.9
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float _Coverage;
        float _Width;
        float _Darkness;
        float _Grime;
        float _Fine;
        float _Scale;
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

    float2 Hash2(float2 p)
    {
        p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
        return frac(sin(p) * 43758.5453);
    }

    float Hash1(float2 p)
    {
        return frac(sin(dot(p, float2(41.3, 289.1))) * 43758.5453);
    }

    // Smooth value noise, 0 to 1: what warps the tiling and what decides where cracks show.
    float ValueNoise(float2 p)
    {
        float2 i = floor(p);
        float2 f = frac(p);
        float2 u = f * f * (3.0 - 2.0 * f);
        float a = Hash1(i);
        float b = Hash1(i + float2(1, 0));
        float c = Hash1(i + float2(0, 1));
        float d = Hash1(i + float2(1, 1));
        return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
    }

    // Distance to the nearest border of a jittered Voronoi tiling, in cell units: the classic
    // two-pass exact border distance, so a crack can be given a width.
    float Border(float2 p)
    {
        float2 n = floor(p);
        float2 f = frac(p);

        float2 mg = 0;
        float2 mr = 0;
        float md = 8.0;
        [unroll] for (int j = -1; j <= 1; j++)
        [unroll] for (int i = -1; i <= 1; i++)
        {
            float2 g = float2(i, j);
            float2 r = g + Hash2(n + g) - f;
            float d = dot(r, r);
            if (d < md) { md = d; mr = r; mg = g; }
        }

        md = 8.0;
        [unroll] for (int j2 = -2; j2 <= 2; j2++)
        [unroll] for (int i2 = -2; i2 <= 2; i2++)
        {
            float2 g = mg + float2(i2, j2);
            float2 r = g + Hash2(n + g) - f;
            float2 diff = r - mr;
            if (dot(diff, diff) > 0.00001)
                md = min(md, dot(0.5 * (mr + r), normalize(diff)));
        }
        return md;
    }

    // How much of a crack is at this point of one projection, 0 to 1.
    float CrackAt(float2 p, float scale, float width, float coverage)
    {
        // Warped, so a border runs jagged like a crack rather than straight like paving.
        float2 warp = float2(ValueNoise(p * 2.3), ValueNoise(p * 2.3 + 5.2)) - 0.5;
        float border = Border(p * scale + 0.45 * warp) / scale;      // metres to the nearest border

        // Which stretches of the network show: where a slow noise is over the threshold, so more
        // coverage lets the same cracks run further rather than drawing new ones.
        float threshold = 1.0 - coverage;
        float shown = threshold <= 0.0 ? 1.0 : smoothstep(threshold, threshold + 0.12, ValueNoise(p * 0.7 + 3.1));

        float fw = max(fwidth(border), 1e-5);
        float stroke = 1.0 - smoothstep(width - fw, width + fw, border);
        // Thinner than a pixel, it cannot be drawn honestly: fade it rather than let it shimmer.
        float fade = saturate(width / fw);
        return stroke * shown * fade;
    }

    half4 Frag(Varyings input) : SV_Target
    {
        UNITY_SETUP_INSTANCE_ID(input);
        float3 n = normalize(input.normalWS);
        float3 w = pow(abs(n), 4.0);
        w /= max(w.x + w.y + w.z, 1e-5);

        float3 p = input.positionWS;
        float coarse = w.x * CrackAt(p.zy, _Scale, _Width, _Coverage)
                     + w.y * CrackAt(p.xz, _Scale, _Width, _Coverage)
                     + w.z * CrackAt(p.xy, _Scale, _Width, _Coverage);

        float fine = 0.0;
        if (_Fine > 0.0)
        {
            float fineScale = _Scale * 3.1;
            float fineWidth = _Width * 0.5;
            fine = w.x * CrackAt(p.zy + 17.0, fineScale, fineWidth, _Fine)
                 + w.y * CrackAt(p.xz + 17.0, fineScale, fineWidth, _Fine)
                 + w.z * CrackAt(p.xy + 17.0, fineScale, fineWidth, _Fine);
        }

        float crack = saturate(max(coarse, fine * 0.8));
        float multiply = (1.0 - _Grime) * lerp(1.0, _Darkness, crack);
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
