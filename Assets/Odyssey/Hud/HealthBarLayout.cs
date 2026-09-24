#nullable enable

namespace Odyssey.Hud
{
    /// <summary>Which ink a piece of a health bar is drawn in.</summary>
    public enum HealthBarInk : byte
    {
        /// <summary>The thin dark rim round the whole bar: <see cref="CombatFeedbackModel.HealthBarOutline"/>.</summary>
        Outline,

        /// <summary>The dark backing inside the rim, round the fill and behind the lost share: <see cref="CombatFeedbackModel.HealthBarPlate"/>.</summary>
        Plate,

        /// <summary>The share of the pool left, in <see cref="CombatFeedbackModel.HealthBarColour"/>.</summary>
        Fill,
    }

    /// <summary>
    /// One rectangle of a health bar, in metres in the bar's own plane: <c>u</c> along the
    /// screen's right, <c>v</c> along its up, the origin at the bar's centre.
    /// </summary>
    public readonly struct HealthBarPiece
    {
        public readonly float Left, Right, Bottom, Top;
        public readonly HealthBarInk Ink;

        public HealthBarPiece(float left, float right, float bottom, float top, HealthBarInk ink)
        {
            Left = left;
            Right = right;
            Bottom = bottom;
            Top = top;
            Ink = ink;
        }

        public float Width => Right - Left;
        public float Height => Top - Bottom;
        public float CentreU => (Left + Right) * 0.5f;
        public float CentreV => (Bottom + Top) * 0.5f;
    }

    /// <summary>
    /// The health bar over a pawn's head (design 33 §8a): a thin dark outline, a dark plate
    /// inside it, and the fill in a channel inside that — laid out as rectangles in a plane that
    /// faces the camera. Unity-free, so the fast tier holds the shape; presentation places each
    /// piece and draws it.
    ///
    /// <para><b>No two pieces overlap, and that is the fix for the flicker, not a style.</b> The
    /// bar was a translucent fill box drawn inside a translucent track box. Neither writes depth,
    /// so which one showed on top was the order they were drawn in, and the order of two
    /// translucent draws is the sort by distance from the camera to each one's centre. For a
    /// full bar — every drafted colonist nobody has hurt — the two centres were the same point, an
    /// exact tie on 480 frames out of 480 of a measured walk, left to however the sort broke it
    /// that frame; for a part-full bar the order turned over as the pawn crossed the middle of the
    /// screen. The fill showed at 0.62 of its colour one way round and at 0.29 the other. Pieces
    /// that never cover each other look the same in any order, so there is nothing left to sort.
    /// <b>Do not lay the plate behind the fill as one rectangle</b>, however much tidier it
    /// looks: that is the overlap back, and <c>HealthBarLayoutTests</c> fails on it.</para>
    ///
    /// <para>Every size is INVENTED (2026-09-23), to be judged at the play camera.</para>
    /// </summary>
    public static class HealthBarLayout
    {
        /// <summary>How wide the fill is when full, in metres: the old bar's width.</summary>
        public const float ChannelWidth = 1.0f;

        /// <summary>How tall the fill is, in metres. The old fill was a 0.11 m box.</summary>
        public const float ChannelHeight = 0.13f;

        /// <summary>How much of the dark plate shows round the fill on every side.</summary>
        public const float PlateMargin = 0.03f;

        /// <summary>The outline's width: thin, the rim that separates the plate from grass or rock.</summary>
        public const float OutlineWidth = 0.018f;

        /// <summary>The fill's opacity over the world: nearly solid, so the colour reads as the colour.</summary>
        public const float FillOpacity = 0.92f;

        /// <summary>The most pieces a bar is ever laid out in: four of outline, three of plate
        /// round the channel, the plate behind the lost share, and the fill.</summary>
        public const int MaxPieces = 9;

        public static float OuterWidth => ChannelWidth + 2f * (PlateMargin + OutlineWidth);

        public static float OuterHeight => ChannelHeight + 2f * (PlateMargin + OutlineWidth);

        /// <summary>The share of the pool left, 0 to 1. A pool of nought is an empty bar rather than a division.</summary>
        public static float Fraction(int hpMilli, int hpMaxMilli)
        {
            if (hpMaxMilli <= 0 || hpMilli <= 0) return 0f;
            if (hpMilli >= hpMaxMilli) return 1f;
            return (float)hpMilli / hpMaxMilli;
        }

        /// <summary>The ink a piece is drawn in, given the fill's colour for this bar.</summary>
        public static HudColour InkOf(HealthBarInk ink, HudColour fill) => ink switch
        {
            HealthBarInk.Outline => CombatFeedbackModel.HealthBarOutline,
            HealthBarInk.Plate => CombatFeedbackModel.HealthBarPlate,
            _ => fill.WithAlpha(FillOpacity),
        };

        /// <summary>
        /// Lay a bar <paramref name="fraction"/> full into <paramref name="into"/>, which holds at
        /// least <see cref="MaxPieces"/>; returns how many were written. Allocates nothing.
        /// </summary>
        public static int Pieces(float fraction, HealthBarPiece[] into)
        {
            if (!(fraction > 0f)) fraction = 0f; // NaN too
            else if (fraction > 1f) fraction = 1f;

            float right = OuterWidth * 0.5f, left = -right;
            float top = OuterHeight * 0.5f, bottom = -top;
            const float o = OutlineWidth, m = PlateMargin;
            int n = 0;

            // The outline: top and bottom the full width, the sides between them.
            into[n++] = new HealthBarPiece(left, right, top - o, top, HealthBarInk.Outline);
            into[n++] = new HealthBarPiece(left, right, bottom, bottom + o, HealthBarInk.Outline);
            into[n++] = new HealthBarPiece(left, left + o, bottom + o, top - o, HealthBarInk.Outline);
            into[n++] = new HealthBarPiece(right - o, right, bottom + o, top - o, HealthBarInk.Outline);

            // The plate inside it: above and below the channel the full inner width, then its left margin.
            float l1 = left + o, r1 = right - o, b1 = bottom + o, t1 = top - o;
            into[n++] = new HealthBarPiece(l1, r1, t1 - m, t1, HealthBarInk.Plate);
            into[n++] = new HealthBarPiece(l1, r1, b1, b1 + m, HealthBarInk.Plate);
            into[n++] = new HealthBarPiece(l1, l1 + m, b1 + m, t1 - m, HealthBarInk.Plate);

            // The channel: the fill from the left, and the plate behind whatever is lost, run on
            // into the right margin so the two meet on one edge and never cover each other.
            float cl = l1 + m, cb = b1 + m, ct = t1 - m;
            float fillEnd = cl + ChannelWidth * fraction;
            if (fillEnd - cl > 1e-5f)
                into[n++] = new HealthBarPiece(cl, fillEnd, cb, ct, HealthBarInk.Fill);
            else
                fillEnd = cl;
            into[n++] = new HealthBarPiece(fillEnd, r1, cb, ct, HealthBarInk.Plate);
            return n;
        }
    }
}
