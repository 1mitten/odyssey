#nullable enable
using System;

namespace Odyssey.Hud
{
    /// <summary>
    /// <b>Where the clouds are, and whether a camera can see them</b> (design 63): the placement
    /// arithmetic of the two Meadow cloud rings, engine-free so every rule in it is a fast-tier test.
    ///
    /// <para><b>A ring round the camera, not a thing on the board.</b> Both rings are centred on the
    /// camera's own position every frame, so nobody walks or scrolls to the edge of the sky. Their
    /// height is the world's, so an eye on the ground looks up at them and a camera high over the
    /// board looks across.</para>
    ///
    /// <para><b>Cover is height, not opacity.</b> The pack's shader is opaque and its "Cloud
    /// Strength" is a vertical wobble, not a density. So a clear sky squashes the rings to a low
    /// band on the horizon and full cover stands them up to the demo's banks: continuous, no pop,
    /// and nothing new in the shader.</para>
    ///
    /// <para><b>Nothing is drawn that cannot be seen.</b> The colony camera at its usual pitch
    /// sees no sky at all, so <see cref="CanBeSeen"/> compares the highest ray in the view with the
    /// lowest point of the ring and the director submits nothing when the one is below the other.</para>
    /// </summary>
    public static class CloudDeck
    {
        /// <summary>The low ring's horizontal scale: the Meadow demo's, about 1,540 m out.</summary>
        public const float LowSpread = 10f;

        /// <summary>The low ring's vertical scale under a clear sky: a band of flat cloud.</summary>
        public const float LowHeightClear = 3.0f;

        /// <summary>The low ring's vertical scale at full cover: the demo's banks.</summary>
        public const float LowHeightFull = 7.2f;

        /// <summary>The high ring's horizontal scale: the demo's, about 1,220 m out.</summary>
        public const float HighSpread = 8.8f;

        /// <summary>The high ring's vertical scale under a clear sky.</summary>
        public const float HighHeightClear = 4.4f;

        /// <summary>The high ring's vertical scale at full cover: the demo's.</summary>
        public const float HighHeightFull = 8.8f;

        /// <summary>The low ring's base above the top of the board, in metres.</summary>
        public const float LowBase = 20f;

        /// <summary>The high ring's base above the top of the board: the demo's gap between the two.</summary>
        public const float HighBase = 68f;

        /// <summary>
        /// How fast the low ring turns, in degrees per game second at ordinary wind. A point on the
        /// horizon crosses a degree in twenty seconds at speed 1: a drift that is there if you watch
        /// for it and never draws the eye.
        /// </summary>
        public const float DriftDegreesPerSecond = 0.05f;

        /// <summary>The high ring turns at this fraction of the low one, which is what gives the sky depth.</summary>
        public const float HighDriftRatio = 0.6f;

        /// <summary>
        /// The ring's inside, as a fraction of its outside: measured at 0.535 (low) and 0.54 (high)
        /// by <c>MeadowLookBuilder</c>, and taken a little smaller, because a smaller inner radius
        /// makes the ring's lowest point look lower from above, so the check draws more often,
        /// never less.
        /// </summary>
        public const float InnerFraction = 0.5f;

        /// <summary>How far below the ring's lowest point the view may reach before the rings are skipped, in degrees.</summary>
        public const float MarginDegrees = 2f;

        /// <summary>The hours the clouds go, after the golden hour, and come back, before dawn's (owner, 2026-09-26).</summary>
        public const float FadeOutStart = 19.5f, FadeOutEnd = 21f, FadeInStart = 5f, FadeInEnd = 6.5f;

        /// <summary>
        /// The quickest the clouds may come or go, in real seconds for a whole fade: a jump in the
        /// hour (a skip, a load, a debug change) fades rather than snaps (owner, 2026-09-26). Far
        /// slower than the dusk fade ever runs at speed 3, so the clock alone paces an ordinary day.
        /// </summary>
        public const float JumpFadeSeconds = 4f;

        /// <summary>How much a downpour stands the rings up beyond their cover: rain is heavier than cloud alone.</summary>
        public const float RainLift = 0.35f;

        /// <summary>
        /// <b>How much of the clouds there is at an hour</b>, 1 by day and 0 by night (design 63
        /// §4c): the owner wants the night dark, so the clouds dissolve into the sky after the golden
        /// hour and come back before dawn's, eased at both ends. At nought nothing is drawn.
        /// </summary>
        public static float Presence(float hour)
        {
            float h = hour % 24f;
            if (h < 0f) h += 24f;
            if (h >= FadeInEnd && h <= FadeOutStart) return 1f;
            if (h > FadeOutStart && h < FadeOutEnd) return 1f - Smooth((h - FadeOutStart) / (FadeOutEnd - FadeOutStart));
            if (h > FadeInStart && h < FadeInEnd) return Smooth((h - FadeInStart) / (FadeInEnd - FadeInStart));
            return 0f;
        }

        /// <summary>
        /// What is left of a ring's height at a presence: all of it by day, nothing at the last of
        /// the fade. A quarter was left at first, and the band that remained vanished in one frame
        /// at 21:00 and came back in one at 05:00 — the snap the owner saw (design 63 §4e).
        /// </summary>
        public static float Sink(float presence) => Math.Max(0f, Math.Min(1f, presence));

        /// <summary>
        /// Where a ring's base stands at a presence: its own height by day, the eye's at the last of
        /// the fade. Flattened at eye level a ring is edge-on, a line of no thickness on the horizon,
        /// so it goes without a last frame to see; fading in, it rises out of the horizon.
        /// </summary>
        public static float BaseAt(float dayBase, float eyeY, float presence)
        {
            float p = Math.Max(0f, Math.Min(1f, presence));
            return eyeY + (dayBase - eyeY) * p;
        }

        /// <summary>
        /// The presence shown, one frame on: towards the clock's <paramref name="target"/> by at most
        /// a whole fade per <see cref="JumpFadeSeconds"/> of <paramref name="realSeconds"/>.
        /// </summary>
        public static float EasePresence(float shown, float target, float realSeconds)
        {
            float step = Math.Max(0f, realSeconds) / JumpFadeSeconds;
            if (shown < target) return Math.Min(target, shown + step);
            return Math.Max(target, shown - step);
        }

        /// <summary>The cover the rings stand to: the sky's cloud, and more under rain.</summary>
        public static float StandingCover(float cloud, float rain) =>
            Math.Max(0f, Math.Min(1f, cloud + RainLift * Math.Max(0f, rain)));

        static float Smooth(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return t * t * (3f - 2f * t);
        }

        /// <summary>The vertical scale for a cover of <paramref name="cloud"/>, 0 clear to 1 overcast.</summary>
        public static float HeightScale(float cloud, float clear, float full)
        {
            float t = Math.Max(0f, Math.Min(1f, cloud));
            t = t * t * (3f - 2f * t);
            return clear + (full - clear) * t;
        }

        /// <summary>
        /// A ring's turn after <paramref name="gameSeconds"/> more game time at a wind of
        /// <paramref name="wind"/> (1 is ordinary; the storm blows harder). Kept in [0, 360) so the
        /// float never loses the precision a long session would otherwise take from it.
        /// </summary>
        public static float Advance(float yawDegrees, float gameSeconds, float wind, float degreesPerSecond)
        {
            if (gameSeconds <= 0f) return yawDegrees;
            float yaw = yawDegrees + gameSeconds * Math.Max(0f, wind) * degreesPerSecond;
            yaw %= 360f;
            return yaw < 0f ? yaw + 360f : yaw;
        }

        /// <summary>
        /// The highest elevation any ray of a camera reaches, in degrees above the horizontal.
        /// <paramref name="pitchDown"/> is Unity's convention (positive looks down); the camera has
        /// no roll, which neither the colony camera nor the ride camera ever has.
        ///
        /// <para>The top edge of the view is the highest line in it. Its centre is highest while the
        /// edge looks above the horizon; below it, the corners are, because they lean out sideways
        /// and a longer ray at the same drop points less far down.</para>
        /// </summary>
        public static float HighestElevation(float pitchDown, float verticalFovDegrees, float aspect)
        {
            double p = pitchDown * Math.PI / 180.0;
            double t = Math.Tan(verticalFovDegrees * 0.5 * Math.PI / 180.0);
            double s = t * Math.Max(0.01f, aspect);
            double y = -Math.Sin(p) + t * Math.Cos(p);
            double z = Math.Cos(p) + t * Math.Sin(p);
            double centre = Math.Atan2(y, z);
            double corner = Math.Atan2(y, Math.Sqrt(z * z + s * s));
            return (float)(Math.Max(centre, corner) * 180.0 / Math.PI);
        }

        /// <summary>
        /// The lowest elevation any point of a ring stands at from an eye at <paramref name="eyeY"/>,
        /// in degrees. <paramref name="outerRadius"/> is the ring's reach from its centre, which is
        /// the eye; <paramref name="bottomY"/> is its lowest point in the world.
        ///
        /// <para>A base above the eye is lowest where it is furthest; a base below the eye is lowest
        /// where it is nearest, which is the inside of the ring.</para>
        /// </summary>
        public static float LowestElevation(float outerRadius, float bottomY, float eyeY)
        {
            float drop = bottomY - eyeY;
            float reach = drop >= 0f ? outerRadius : outerRadius * InnerFraction;
            return (float)(Math.Atan2(drop, Math.Max(1f, reach)) * 180.0 / Math.PI);
        }

        /// <summary>Whether a view whose highest ray is at <paramref name="highestView"/> can see anything at or above <paramref name="lowestRing"/>.</summary>
        public static bool CanBeSeen(float highestView, float lowestRing) => highestView >= lowestRing - MarginDegrees;
    }
}
