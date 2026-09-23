#nullable enable
using Odyssey.Hud;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// Where the fight's marks stand over a pawn and what ink they are in (design 33 §1): the
    /// health bar over the hurt and the drafted, and the hostile marker. <b>Whether</b> a bar is
    /// owed and how full it is are <see cref="CombatFeedbackModel"/>'s answers; this is the
    /// geometry that draws them, kept apart so a test can hold it without a renderer.
    ///
    /// <para>Every ink is a <see cref="HudTheme"/> token, never a colour written here: the bar is
    /// the HUD's good, warning and bad, and the hostile marker is its bad — brighter than the
    /// draft's deep red, so an enemy and one of ours cannot be confused at a glance.</para>
    /// </summary>
    public static class CombatMarks
    {
        /// <summary>How wide a full bar is, in metres.</summary>
        public const float BarWidth = 1.0f;

        /// <summary>How thick the bar's track is, in metres; the fill is a little thicker so it covers the track it lies in.</summary>
        public const float BarThickness = 0.09f;

        /// <summary>The fill's thickness over the track's.</summary>
        public const float FillThickness = 0.11f;

        /// <summary>How far above the top of a pawn's cursor box the bar sits, in metres: under the draft diamond.</summary>
        public const float BarLift = 0.05f;

        /// <summary>How high a downed pawn's marks stand above its feet: it is lying down, not standing.</summary>
        public const float DownedTop = 0.8f;

        /// <summary>The share of the pool left, 0 to 1. A pool of nought is an empty bar rather than a division.</summary>
        public static float Fraction(int hpMilli, int hpMaxMilli) =>
            hpMaxMilli > 0 ? Mathf.Clamp01((float)hpMilli / hpMaxMilli) : 0f;

        /// <summary>The fill's ink: good above three fifths, a warning above three tenths, bad below.</summary>
        public static HudColour BarInk(float fraction) =>
            fraction > 0.6f ? HudTheme.Good : fraction > 0.3f ? HudTheme.Warn : HudTheme.Bad;

        /// <summary>The track's ink: the panel's own fill, so a bar reads as a small HUD element.</summary>
        public static HudColour TrackInk => HudTheme.PanelFill.WithAlpha(0.85f);

        /// <summary>The hostile marker's ink (design 33 §1: "a red marker").</summary>
        public static HudColour HostileInk => HudTheme.Bad;

        /// <summary>
        /// A bar centred on <paramref name="centre"/>, lying along <paramref name="across"/> (the
        /// camera's right, flattened, so it is level on screen): where the track starts and ends,
        /// and where the fill ends. The fill always starts at the track's left end.
        /// </summary>
        public static void Bar(Vector3 centre, Vector3 across, float fraction,
            out Vector3 start, out Vector3 end, out Vector3 fillEnd)
        {
            across.y = 0f;
            if (across.sqrMagnitude < 1e-8f) across = Vector3.right;
            across.Normalize();
            start = centre - across * (BarWidth * 0.5f);
            end = centre + across * (BarWidth * 0.5f);
            fillEnd = start + across * (BarWidth * Mathf.Clamp01(fraction));
        }
    }
}
