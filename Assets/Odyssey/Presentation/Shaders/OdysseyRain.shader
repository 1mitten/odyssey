// Rain, drawn entirely on the GPU: streaks falling through a box that follows the camera's focus,
// and splashes where they land. Prototype for the rain look (claude/rain-look); the weather
// design's §7 and docs/research/d-20-rain-rendering.md hold the reasoning.
//
// **No mesh, no particle system, no CPU per drop.** RainDirector issues one
// Graphics.RenderPrimitives call per material — six vertices per instance, N instances — and
// every drop's place, fall and fade is computed here from its instance id and the rain clock. A
// downpour of twenty thousand streaks costs the CPU exactly what a drizzle of two hundred does.
//
// **World-anchored, not camera-anchored.** Each drop's column is a lattice position wrapped into
// the box around the focus, so panning moves the box over the rain rather than dragging the rain
// with it.
//
// **Cover is exact and costs one texture fetch.** OdysseySkyAt (OdysseyWeather.hlsl) says how high
// the rain stops over this column. A streak whose head is below it is not drawn; a splash is
// placed on it. So rain stops on a roof, splashes on the roof, and never falls through a canopy.
//
// _Mode 0 draws streaks, 1 draws splashes. Two materials, one shader: the branch is uniform.
Shader "Odyssey/Rain"
{
    Properties
    {
        _Mode("Mode (0 streaks, 1 splashes)", Float) = 0
        _Tint("Tint", Color) = (0.78, 0.84, 0.92, 1)
        // xyz the focus the box is centred on, w its half-width in metres. Written every frame.
        _Box("Box", Vector) = (0, 0, 0, 40)
        // x streak length (m), y streak width (m), z fall speed (m/s), w opacity
        _Look("Streak look", Vector) = (0.8, 0.012, 11, 0.42)
        // x box height (m), y splash period (s), z ground splash radius (m), w water ring radius (m)
        _Splash("Splash look", Vector) = (40, 0.55, 0.16, 0.42)
        // metres sideways per metre fallen, per unit of _OdysseyWind
        _WindSlant("Wind slant", Float) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "RainForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_fog
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "OdysseyWeather.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Mode;
                float4 _Tint;
                float4 _Box;
                float4 _Look;
                float4 _Splash;
                float _WindSlant;
            CBUFFER_END

            // Set by WindDirector: xyz the push, w the gust phase. Zero is still air.
            float4 _OdysseyWind;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 shape      : TEXCOORD1;   // x alpha, y ring radius (0..1), z ring width, w disc
                half   fogFactor  : TEXCOORD2;
            };

            uint Pcg(uint v)
            {
                uint state = v * 747796405u + 2891336453u;
                uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
                return (word >> 22u) ^ word;
            }

            float Rand(uint v) { return Pcg(v) * (1.0 / 4294967296.0); }

            // A lattice column wrapped into the box: the same world position whatever the focus,
            // until the focus moves far enough that the column wraps to the other side.
            float2 Column(float rx, float rz)
            {
                float box = 2.0 * _Box.w;
                return _Box.xz + (frac(float2(rx, rz) - _Box.xz / box + 0.5) - 0.5) * box;
            }

            // How wide one screen pixel is at this distance, in metres. The absolute value because
            // URP flips the projection when it draws into a texture, which makes _m11 negative —
            // and a negative pixel culled every splash in the first sheet.
            float PixelMetres(float distance)
            {
                return distance * 2.0 / (_ScreenParams.y * abs(UNITY_MATRIX_P._m11));
            }

            static const uint Corners[6] = { 0u, 1u, 2u, 0u, 2u, 3u };

            Varyings Culled()
            {
                Varyings o = (Varyings)0;
                o.positionCS = float4(2, 2, 2, 1);   // outside the clip volume on every axis
                return o;
            }

            Varyings Streak(uint vid, uint iid)
            {
                float rx = Rand(iid * 3u), rz = Rand(iid * 3u + 1u), rp = Rand(iid * 3u + 2u);
                float height = _Splash.x;
                float speed = _Look.z * lerp(0.85, 1.15, rx);
                float cycle = frac(rp + _OdysseyRain.z * speed / height);
                float fallen = cycle * height;
                float top = _Box.y + height * 0.7;

                float2 drift = _OdysseyWind.xz * _WindSlant;
                float2 column = Column(rx, rz);
                float2 at = column + drift * fallen;
                float3 head = float3(at.x, top - fallen, at.y);

                float2 sky = OdysseySkyAt(head.xz);
                if (head.y < sky.x) return Culled();

                float3 fall = normalize(float3(drift.x, -1.0, drift.y));
                float streakLength = _Look.x * lerp(0.6, 1.0, _OdysseyRain.x);
                float3 tail = head - fall * streakLength;

                float3 camera = _WorldSpaceCameraPos;
                float distance = max(0.01, length(head - camera));
                float pixel = PixelMetres(distance);
                // At least a pixel and a half: a quad thinner than a pixel misses most pixel
                // centres, and the streak breaks into dashes that read as noise.
                float width = max(_Look.y, pixel * 1.5);

                float3 axis = normalize(tail - head);
                float3 side = normalize(cross(axis, normalize(camera - head)));

                uint corner = Corners[vid];
                float across = (corner == 1u || corner == 2u) ? 0.5 : -0.5;
                float along = (corner >= 2u) ? 1.0 : 0.0;
                float3 world = lerp(head, tail, along) + side * (across * width);

                // Fades: the box's edge, the lens, the moment a drop enters at the top, and the
                // brightness a streak widened to one pixel must give back to stay the same weight.
                float edge = 1.0 - smoothstep(0.55, 1.0, length(head.xz - _Box.xz) / _Box.w);
                float lens = saturate((distance - 2.0) / 6.0);
                float enter = saturate(cycle * 8.0);
                float thin = _Look.y / width;

                Varyings o;
                o.positionCS = TransformWorldToHClip(world);
                o.uv = float2(across + 0.5, along);
                o.shape = float4(_Look.w * edge * lens * enter * thin, 0, 0, 0);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            Varyings Splash(uint vid, uint iid)
            {
                float period = _Splash.y;
                float phase = _OdysseyRain.z / period + Rand(iid * 5u + 4u);
                uint round_ = (uint)floor(phase);
                float age = frac(phase);

                uint seed = iid * 5u + round_ * 7919u;
                float2 column = Column(Rand(seed), Rand(seed + 1u));
                float2 sky = OdysseySkyAt(column);
                if (sky.y == ODYSSEY_SKY_CANOPY) return Culled();

                bool water = sky.y == ODYSSEY_SKY_WATER;
                float life = water ? 0.95 : 0.32;
                if (age > life) return Culled();
                float t = age / life;

                float reach = water ? _Splash.w : _Splash.z;
                float radius = lerp(0.04, reach, water ? sqrt(t) : t);
                float3 centre = float3(column.x, sky.x + (water ? 0.02 : 0.04), column.y);

                float3 camera = _WorldSpaceCameraPos;
                float distance = max(0.01, length(centre - camera));
                float pixel = PixelMetres(distance);
                // A ring smaller than about two pixels is noise, not a splash: fade it out.
                float visible = saturate(reach * 2.0 / pixel - 1.0);
                float edge = 1.0 - smoothstep(0.55, 1.0, length(column - _Box.xz) / _Box.w);
                if (visible * edge <= 0.001) return Culled();

                float halfSize = radius + max(0.02, pixel);
                uint corner = Corners[vid];
                float2 local = float2(corner == 1u || corner == 2u ? 1.0 : -1.0, corner >= 2u ? 1.0 : -1.0);
                float3 world = centre + float3(local.x, 0, local.y) * halfSize;

                Varyings o;
                o.positionCS = TransformWorldToHClip(world);
                o.uv = local;
                // Strongest where a splash is seen in life: rings on water, crowns on a roof or a
                // path. On grass the blades swallow most of it, so it is a glint and not a disc —
                // the first sheet drew it full strength there and the meadow read as hail.
                bool built = sky.y == ODYSSEY_SKY_BUILT;
                float strength = water ? 1.5 : built ? 1.8 : 0.35;
                float alpha = (1.0 - t) * visible * edge * _Look.w * strength;
                float ringWidth = max(0.02, pixel * 1.2) / halfSize;
                o.shape = float4(alpha, radius / halfSize, ringWidth, built ? (1.0 - t) : 0.0);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            Varyings Vertex(uint vid : SV_VertexID, uint iid : SV_InstanceID)
            {
                if (_OdysseyRain.x <= 0.001) return Culled();
                if (_Mode < 0.5) return Streak(vid, iid);
                return Splash(vid, iid);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                Light sun = GetMainLight();
                // Rain has no colour of its own: it is the sky and the sun caught in water, so it
                // darkens at dusk and all but vanishes at night, as it should.
                half3 lit = _Tint.rgb * (unity_AmbientSky.rgb * 1.1 + sun.color * 0.22);

                float alpha;
                if (_Mode < 0.5)
                {
                    float across = saturate(1.3 - abs(input.uv.x * 2.0 - 1.0));
                    float along = lerp(1.0, 0.25, input.uv.y);
                    alpha = input.shape.x * across * along;
                }
                else
                {
                    float d = length(input.uv);
                    float ring = 1.0 - smoothstep(0.0, input.shape.z, abs(d - input.shape.y));
                    float disc = (1.0 - smoothstep(input.shape.y * 0.4, input.shape.y, d)) * input.shape.w * 0.5;
                    alpha = input.shape.x * saturate(ring + disc);
                    lit *= 1.25;
                }

                half3 colour = MixFog(lit, input.fogFactor);
                return half4(colour, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
