// An ink line around everything the camera can see, drawn from the depth buffer.
//
// Why depth rather than geometry. Outlining by rendering back faces expanded along their normals
// costs a second draw of every object in the world, which in a renderer that submits tens of
// thousands of instanced modules a frame is the one thing it is built not to do. A screen-space
// pass costs one fullscreen blit whatever the scene contains, and the scene here contains a lot.
//
// Why depth rather than depth and normals. A normals texture comes from a prepass that re-renders
// the world through each shader's DepthNormals pass, and this game's geometry is submitted with
// Graphics.RenderMeshInstanced from a camera callback — whether that reaches the prepass depends
// on the pack shaders having the pass at all. Depth is copied from the real depth buffer after
// opaques, so it is guaranteed to contain exactly what was drawn. The cost is that two flat
// surfaces meeting at the same depth get no crease line; silhouettes, which are most of the look,
// all come through.
Shader "Odyssey/Outline"
{
    Properties
    {
        _OutlineColour ("Outline colour", Color) = (0.06, 0.09, 0.08, 0.85)
        _Thickness ("Thickness in pixels", Float) = 1.2
        _DepthThreshold ("Depth threshold", Float) = 0.012
        _FadeStart ("Fade start in metres", Float) = 40
        _FadeEnd ("Fade end in metres", Float) = 90
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "OdysseyOutline"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 _OutlineColour;
            float _Thickness;
            float _DepthThreshold;
            float _FadeStart;
            float _FadeEnd;

            // Metres from the camera, so the comparison below is in world units rather than in the
            // depth buffer's own wildly non-linear ones.
            float EyeDepth(float2 uv)
            {
                return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float2 texel = _BlitTexture_TexelSize.xy * _Thickness;

                // Roberts cross: two diagonal differences. Cheaper than a Sobel, and on a depth
                // buffer it gives a thinner, more even line, which is what an ink edge wants.
                float d0 = EyeDepth(uv + float2(-texel.x, -texel.y));
                float d1 = EyeDepth(uv + float2( texel.x,  texel.y));
                float d2 = EyeDepth(uv + float2(-texel.x,  texel.y));
                float d3 = EyeDepth(uv + float2( texel.x, -texel.y));

                float gradient = sqrt((d1 - d0) * (d1 - d0) + (d3 - d2) * (d3 - d2));

                // The threshold has to grow with distance or the whole far half of the board
                // becomes a solid outline: at a fixed tolerance, one pixel spans more metres the
                // further away it is, so a flat field eventually trips it everywhere. Scaling by
                // the depth at this pixel keeps the line on real edges at every zoom level.
                float centre = EyeDepth(uv);
                float threshold = max(_DepthThreshold * centre, 1e-4);

                float edge = saturate((gradient - threshold) / threshold);

                // Let the line go as things recede, or thin geometry turns into a solid blot.
                //
                // A clump of grass half a metre across is a few pixels wide at board distance, and
                // every one of those pixels sits on a depth discontinuity — so the whole clump
                // becomes outline and the meadow reads as a field of dark smudges. This is not the
                // threshold being too low; it is the geometry being smaller than the detector. The
                // honest fix is to stop drawing a line nobody could read anyway, which is also
                // what aerial perspective does in the reference art.
                float fade = 1.0 - smoothstep(_FadeStart, _FadeEnd, centre);
                edge *= fade;

                scene.rgb = lerp(scene.rgb, _OutlineColour.rgb, edge * _OutlineColour.a);
                return scene;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
