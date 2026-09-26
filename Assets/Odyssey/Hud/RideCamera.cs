#nullable enable
using System;

namespace Odyssey.Hud
{
    /// <summary>
    /// Where the camera stands while it rides with a colonist (design 56 §3): what the camera
    /// must know about her this frame.
    /// </summary>
    public readonly struct RideFrame
    {
        public RideFrame(float feetX, float feetY, float feetZ, float eyeLift, float yaw, float pitch, float arm)
        {
            FeetX = feetX;
            FeetY = feetY;
            FeetZ = feetZ;
            EyeLift = eyeLift;
            Yaw = yaw;
            Pitch = pitch;
            Arm = arm;
        }

        /// <summary>Where she is drawn standing, in world metres.</summary>
        public readonly float FeetX, FeetY, FeetZ;

        /// <summary>How far her eyes are above her feet, in metres.</summary>
        public readonly float EyeLift;

        /// <summary>Which way the camera looks, in degrees about the vertical: Unity's convention, 0 along +z, 90 along +x.</summary>
        public readonly float Yaw;

        /// <summary>How far the camera looks down, in degrees; negative looks up. Unity's convention.</summary>
        public readonly float Pitch;

        /// <summary>How far behind her the player has asked the camera to stand, in metres. Nought is her eyes.</summary>
        public readonly float Arm;
    }

    /// <summary>The camera's answer: where it stands, which way it looks, and how long the arm came out.</summary>
    public readonly struct RidePose
    {
        public RidePose(float x, float y, float z, float yaw, float pitch, float arm, float shoulder, float fromEyes)
        {
            X = x;
            Y = y;
            Z = z;
            Yaw = yaw;
            Pitch = pitch;
            Arm = arm;
            Shoulder = shoulder;
            FromEyes = fromEyes;
        }

        /// <summary>The camera's position, in world metres.</summary>
        public readonly float X, Y, Z;

        /// <summary>Degrees about the vertical, as asked.</summary>
        public readonly float Yaw;

        /// <summary>Degrees down, as asked.</summary>
        public readonly float Pitch;

        /// <summary>
        /// The arm as it came out after the walls had their say: at most the arm asked for, and
        /// shorter where something solid stood behind her.
        /// </summary>
        public readonly float Arm;

        /// <summary>
        /// How far over to her shoulder the camera sits, 0 at the eyes to 1 fully behind her.
        /// </summary>
        public readonly float Shoulder;

        /// <summary>
        /// How far the camera came out from her eyes, in metres. What the presentation reads to
        /// decide whether her head is in the way (design 56 §4): at the eye stop, and wherever a
        /// wall has pushed the camera up against her.
        /// </summary>
        public readonly float FromEyes;
    }

    /// <summary>
    /// The chase camera's geometry, engine-free so every rule in it is a fast-tier test
    /// (design 56 §3).
    ///
    /// <para><b>Behind the shoulder, not behind the head.</b> A camera straight behind a colonist
    /// puts her body over the middle of the screen, which is exactly where whatever she is
    /// swinging at is. Over the right shoulder, and a little above the eyes, the fight is beside
    /// her rather than through her. Every chase camera worth copying does this.</para>
    ///
    /// <para><b>The shoulder blends into the eyes.</b> The wheel moves the arm; as it shortens to
    /// nought the pivot slides from over the shoulder to just in front of the face, so there is
    /// one continuous control from "watch her" to "see what she sees" rather than two modes.</para>
    ///
    /// <para><b>A spring arm, not a boom.</b> The arm is marched out from the pivot and stops short
    /// of the first solid thing, so a wall behind her pulls the camera in rather than filling the
    /// screen. The pivot's own sideways step is marched too, from the eyes, so walking with a
    /// wall at her right shoulder does not put the camera in it. What counts as solid is the
    /// caller's (<paramref name="blocked"/> in <see cref="Solve"/>): the presentation asks the
    /// render mirror, and a test asks whatever it likes.</para>
    /// </summary>
    public static class RideCamera
    {
        /// <summary>The field of view while riding, in degrees. The colony camera's 40 is a telephoto at a metre.</summary>
        public const float FieldOfView = 60f;

        /// <summary>The near clip while riding, in metres: the eye stop sits a hand's width from things.</summary>
        public const float NearClip = 0.05f;

        /// <summary>How far above her eyes the pivot sits when the camera is fully behind her.</summary>
        public const float ShoulderAboveEyes = 0.35f;

        /// <summary>How far to her right the pivot sits when the camera is fully behind her.</summary>
        public const float ShoulderRight = 0.6f;

        /// <summary>
        /// How far in front of her eyes the eye stop is. Her head is taken away there (design 56
        /// §4), and this keeps the neck, which is not, out of the near plane.
        /// </summary>
        public const float EyeForward = 0.18f;

        /// <summary>The arm at and beyond which the camera is fully over her shoulder; below it, it blends to the eyes.</summary>
        public const float ShoulderBlendMetres = 1.5f;

        /// <summary>How far short of a solid thing the camera stops, so the near plane does not cut into it.</summary>
        public const float Clearance = 0.3f;

        /// <summary>The march's step, in metres. A wall is a 2.5 m cell, so this cannot step over one.</summary>
        public const float Step = 0.1f;

        /// <summary>What a colonist's eyes default to above her feet when her head cannot be measured.</summary>
        public const float DefaultEyeLift = 2.25f;

        /// <summary>
        /// Stand the camera for this frame. <paramref name="blocked"/> answers whether a point in
        /// the world is inside something solid; null is open air everywhere.
        /// </summary>
        public static RidePose Solve(in RideFrame frame, Func<float, float, float, bool>? blocked)
        {
            float arm = Math.Max(0f, frame.Arm);
            float shoulder = Clamp01(arm / ShoulderBlendMetres);

            float yaw = frame.Yaw * Deg;
            float pitch = frame.Pitch * Deg;
            float sinYaw = (float)Math.Sin(yaw), cosYaw = (float)Math.Cos(yaw);
            float sinPitch = (float)Math.Sin(pitch), cosPitch = (float)Math.Cos(pitch);

            // Unity's axes: forward at yaw 0 is +z, and a positive pitch looks down.
            float fx = sinYaw * cosPitch, fy = -sinPitch, fz = cosYaw * cosPitch;
            float rx = cosYaw, rz = -sinYaw;

            // The eyes, stepped out in front of the face as the camera comes in to them.
            float lead = EyeForward * (1f - shoulder);
            float ex = frame.FeetX + sinYaw * lead;
            float ey = frame.FeetY + frame.EyeLift;
            float ez = frame.FeetZ + cosYaw * lead;

            // The shoulder: up and to the right of the eyes, marched there so a wall beside her
            // shortens the step rather than swallowing the pivot.
            float ox = rx * ShoulderRight * shoulder;
            float oy = ShoulderAboveEyes * shoulder;
            float oz = rz * ShoulderRight * shoulder;
            float offset = (float)Math.Sqrt(ox * ox + oy * oy + oz * oz);
            float px = ex, py = ey, pz = ez;
            if (offset > 1e-4f)
            {
                float reach = March(ex, ey, ez, ox / offset, oy / offset, oz / offset, offset, blocked);
                float k = reach / offset;
                px = ex + ox * k;
                py = ey + oy * k;
                pz = ez + oz * k;
            }

            // The arm, back along the view from the pivot.
            float allowed = arm > 0f ? March(px, py, pz, -fx, -fy, -fz, arm, blocked) : 0f;

            float cx = px - fx * allowed, cy = py - fy * allowed, cz = pz - fz * allowed;
            float dx = cx - ex, dy = cy - ey, dz = cz - ez;
            return new RidePose(cx, cy, cz, frame.Yaw, frame.Pitch, allowed, shoulder,
                (float)Math.Sqrt(dx * dx + dy * dy + dz * dz));
        }

        /// <summary>
        /// How far along a unit direction the camera may go before it is within
        /// <see cref="Clearance"/> of something solid, up to <paramref name="length"/>.
        /// </summary>
        public static float March(float x, float y, float z, float dx, float dy, float dz, float length,
            Func<float, float, float, bool>? blocked)
        {
            if (blocked == null || length <= 0f) return Math.Max(0f, length);

            // Out as far as the arm plus the clearance, so a wall standing just beyond the arm's
            // end still keeps the lens its clearance away.
            float reach = length + Clearance;
            for (float s = Step; ; s += Step)
            {
                if (s > reach) s = reach;
                if (blocked(x + dx * s, y + dy * s, z + dz * s))
                    return Math.Max(0f, Math.Min(length, s - Clearance));
                if (s >= reach) return length;
            }
        }

        const float Deg = (float)(Math.PI / 180.0);

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
