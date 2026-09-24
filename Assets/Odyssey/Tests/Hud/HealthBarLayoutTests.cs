#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The health bar over a pawn's head (design 33 §8a): its pieces and its inks.
    ///
    /// <para><b>The flicker was two translucent boxes in one place.</b> The fill was drawn inside
    /// the track, both see-through and neither writing depth, so which covered the other was the
    /// draw order — and the draw order of two translucent things is the sort by distance from the
    /// camera to each one's centre, which for a full bar was the <i>same</i> centre on every
    /// frame. The bar is now pieces that never overlap, so no order can change what it looks like:
    /// that is the property held here, over every fraction.</para>
    /// </summary>
    public class HealthBarLayoutTests
    {
        static readonly HealthBarPiece[] Pieces = new HealthBarPiece[HealthBarLayout.MaxPieces];

        static int Lay(float fraction) => HealthBarLayout.Pieces(fraction, Pieces);

        static float Overlap(in HealthBarPiece a, in HealthBarPiece b)
        {
            float w = Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left);
            float h = Math.Min(a.Top, b.Top) - Math.Max(a.Bottom, b.Bottom);
            return w > 0f && h > 0f ? w * h : 0f;
        }

        static float Area(in HealthBarPiece p) => (p.Right - p.Left) * (p.Top - p.Bottom);

        static readonly float[] Fractions = BuildFractions();

        static float[] BuildFractions()
        {
            var list = new float[103];
            for (int i = 0; i <= 100; i++) list[i] = i / 100f;
            list[101] = 0.0001f;
            list[102] = 0.9999f;
            return list;
        }

        /// <summary>
        /// The fix. No two pieces of one bar cover the same point, at any fraction, so the
        /// translucent sort — which cannot order two pieces that share a centre, and orders two
        /// that do not by where the bar is on the screen — has nothing to decide.
        /// </summary>
        [Test]
        public void NoTwoPiecesOfABarOverlapAtAnyFraction()
        {
            foreach (float f in Fractions)
            {
                int n = Lay(f);
                for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    Assert.That(Overlap(Pieces[i], Pieces[j]), Is.LessThan(1e-7f),
                        $"at {f}: {Pieces[i].Ink} [{Pieces[i].Left},{Pieces[i].Right}]x[{Pieces[i].Bottom},{Pieces[i].Top}] covers "
                        + $"{Pieces[j].Ink} [{Pieces[j].Left},{Pieces[j].Right}]x[{Pieces[j].Bottom},{Pieces[j].Top}]");
            }
        }

        /// <summary>
        /// And they leave no gap either: the pieces tile the bar's outer rectangle exactly, so the
        /// ground never shows through as a hairline between the fill and its plate.
        /// </summary>
        [Test]
        public void ThePiecesTileTheWholeBar()
        {
            float whole = HealthBarLayout.OuterWidth * HealthBarLayout.OuterHeight;
            foreach (float f in Fractions)
            {
                int n = Lay(f);
                float sum = 0f;
                for (int i = 0; i < n; i++)
                {
                    Assert.That(Pieces[i].Right, Is.GreaterThan(Pieces[i].Left), $"at {f}: piece {i} has no width");
                    Assert.That(Pieces[i].Top, Is.GreaterThan(Pieces[i].Bottom), $"at {f}: piece {i} has no height");
                    Assert.That(Pieces[i].Left, Is.GreaterThanOrEqualTo(-HealthBarLayout.OuterWidth / 2f - 1e-6f));
                    Assert.That(Pieces[i].Right, Is.LessThanOrEqualTo(HealthBarLayout.OuterWidth / 2f + 1e-6f));
                    Assert.That(Pieces[i].Bottom, Is.GreaterThanOrEqualTo(-HealthBarLayout.OuterHeight / 2f - 1e-6f));
                    Assert.That(Pieces[i].Top, Is.LessThanOrEqualTo(HealthBarLayout.OuterHeight / 2f + 1e-6f));
                    sum += Area(Pieces[i]);
                }
                Assert.That(sum, Is.EqualTo(whole).Within(1e-5f), $"at {f}");
                Assert.That(n, Is.LessThanOrEqualTo(HealthBarLayout.MaxPieces));
            }
        }

        /// <summary>
        /// The fill is the fraction of the channel, from the left; an empty bar has no fill at all
        /// and a full one fills the channel, with the plate's margin still round it.
        /// </summary>
        [Test]
        public void TheFillIsTheFractionOfTheChannelFromTheLeft()
        {
            foreach (float f in new[] { 0.25f, 0.5f, 1f })
            {
                int n = Lay(f);
                int fills = 0;
                for (int i = 0; i < n; i++)
                {
                    if (Pieces[i].Ink != HealthBarInk.Fill) continue;
                    fills++;
                    Assert.That(Pieces[i].Right - Pieces[i].Left, Is.EqualTo(HealthBarLayout.ChannelWidth * f).Within(1e-5f));
                    Assert.That(Pieces[i].Top - Pieces[i].Bottom, Is.EqualTo(HealthBarLayout.ChannelHeight).Within(1e-5f));
                    Assert.That(Pieces[i].Left, Is.EqualTo(-HealthBarLayout.ChannelWidth / 2f).Within(1e-5f), "from the left");
                }
                Assert.That(fills, Is.EqualTo(1), $"at {f}");
            }

            int empty = Lay(0f);
            for (int i = 0; i < empty; i++)
                Assert.That(Pieces[i].Ink, Is.Not.EqualTo(HealthBarInk.Fill), "an empty bar has a fill");

            Assert.That(HealthBarLayout.Pieces(float.NaN, Pieces), Is.EqualTo(empty), "NaN is an empty bar");
            Assert.That(HealthBarLayout.Pieces(2f, Pieces), Is.EqualTo(Lay(1f)), "more than full is full");
        }

        /// <summary>
        /// The owner's "more prominent" (2026-09-23): a thin dark outline on the very edge, a dark
        /// plate inside it that frames the fill on every side, and a channel thicker than the old
        /// 0.11 m fill.
        /// </summary>
        [Test]
        public void AThickerBarInADarkPlateInsideAThinOutline()
        {
            Assert.That(HealthBarLayout.ChannelHeight, Is.GreaterThan(0.11f), "no thicker than the bar that flickered");
            Assert.That(HealthBarLayout.OutlineWidth, Is.LessThan(HealthBarLayout.PlateMargin), "the outline is the thin one");

            float halfW = HealthBarLayout.OuterWidth / 2f, halfH = HealthBarLayout.OuterHeight / 2f;
            int n = Lay(1f);
            for (int i = 0; i < n; i++)
            {
                HealthBarPiece p = Pieces[i];
                bool onTheEdge = p.Left <= -halfW + 1e-6f || p.Right >= halfW - 1e-6f
                    || p.Bottom <= -halfH + 1e-6f || p.Top >= halfH - 1e-6f;
                switch (p.Ink)
                {
                    case HealthBarInk.Outline:
                        Assert.That(onTheEdge, Is.True, "an outline piece off the edge");
                        Assert.That(Math.Min(p.Right - p.Left, p.Top - p.Bottom),
                            Is.EqualTo(HealthBarLayout.OutlineWidth).Within(1e-6f));
                        break;
                    case HealthBarInk.Plate:
                    case HealthBarInk.Fill:
                        Assert.That(onTheEdge, Is.False, $"a {p.Ink} piece on the outer edge: the outline must go all round");
                        break;
                }
            }

            // Inset: the fill stands exactly an outline and a margin in from every side.
            float inset = HealthBarLayout.OutlineWidth + HealthBarLayout.PlateMargin;
            for (int i = 0; i < n; i++)
            {
                if (Pieces[i].Ink != HealthBarInk.Fill) continue;
                Assert.That(Pieces[i].Left, Is.EqualTo(-halfW + inset).Within(1e-6f));
                Assert.That(Pieces[i].Right, Is.EqualTo(halfW - inset).Within(1e-6f));
                Assert.That(Pieces[i].Bottom, Is.EqualTo(-halfH + inset).Within(1e-6f));
                Assert.That(Pieces[i].Top, Is.EqualTo(halfH - inset).Within(1e-6f));
            }
        }

        // ---- The inks (owner, 2026-09-23: "use a green like the one used in the colony stats -
        // more greener - deeper colours please") ----------------------------------------------

        static void Hsv(HudColour c, out float hue, out float saturation, out float value)
        {
            float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
            float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
            value = max;
            saturation = max > 0f ? d / max : 0f;
            if (d <= 0f) { hue = 0f; return; }
            if (max == r) hue = 60f * (((g - b) / d) % 6f);
            else if (max == g) hue = 60f * ((b - r) / d + 2f);
            else hue = 60f * ((r - g) / d + 4f);
            if (hue < 0f) hue += 360f;
        }

        static void AssertDeeper(HudColour bar, HudColour stat, string name)
        {
            Hsv(bar, out float h, out float s, out float v);
            Hsv(stat, out float h0, out float s0, out float v0);
            float turn = Math.Abs(h - h0);
            if (turn > 180f) turn = 360f - turn;
            Assert.That(turn, Is.LessThanOrEqualTo(3f), $"{name} {bar.Hex} is not the hue of {stat.Hex}");
            Assert.That(s, Is.GreaterThanOrEqualTo(s0 + 0.15f), $"{name} {bar.Hex} is not more saturated than {stat.Hex}");
            Assert.That(v, Is.LessThan(v0), $"{name} {bar.Hex} is not deeper than {stat.Hex}");
            Assert.That(bar.A, Is.EqualTo(1f), $"{name}: the ink is opaque; the world bar's opacity is the layout's");
        }

        /// <summary>
        /// The green is the colony stats' green — the need bars' <see cref="HudTheme.Good"/>, the
        /// same hue — more saturated and deeper; the amber and the red likewise from
        /// <see cref="HudTheme.Warn"/> and <see cref="HudTheme.Bad"/>.
        /// </summary>
        [Test]
        public void TheBarsInksAreTheStatInksDeeper()
        {
            AssertDeeper(CombatFeedbackModel.HealthGood, HudTheme.Good, "green");
            AssertDeeper(CombatFeedbackModel.HealthWarn, HudTheme.Warn, "amber");
            AssertDeeper(CombatFeedbackModel.HealthBad, HudTheme.Bad, "red");
        }

        /// <summary>
        /// The plate is dark and the outline darker, both see-through enough that a figure behind
        /// the bar is not blotted out, and the fill nearly opaque so its colour is its colour.
        /// </summary>
        [Test]
        public void ThePlateIsDarkTheOutlineDarkerAndNeitherIsSolid()
        {
            Hsv(CombatFeedbackModel.HealthBarPlate, out _, out _, out float plate);
            Hsv(CombatFeedbackModel.HealthBarOutline, out _, out _, out float outline);
            Assert.That(plate, Is.LessThan(0.15f), "the plate is not dark");
            Assert.That(outline, Is.LessThanOrEqualTo(plate), "the outline is not darker than the plate");
            Assert.That(CombatFeedbackModel.HealthBarPlate.A, Is.InRange(0.55f, 0.9f));
            Assert.That(CombatFeedbackModel.HealthBarOutline.A, Is.InRange(0.6f, 0.95f));
            Assert.That(HealthBarLayout.FillOpacity, Is.InRange(0.85f, 0.97f));

            HudColour fill = CombatFeedbackModel.HealthGood;
            Assert.That(HealthBarLayout.InkOf(HealthBarInk.Fill, fill),
                Is.EqualTo(fill.WithAlpha(HealthBarLayout.FillOpacity)));
            Assert.That(HealthBarLayout.InkOf(HealthBarInk.Plate, fill), Is.EqualTo(CombatFeedbackModel.HealthBarPlate));
            Assert.That(HealthBarLayout.InkOf(HealthBarInk.Outline, fill), Is.EqualTo(CombatFeedbackModel.HealthBarOutline));
        }

        [Test]
        public void TheFractionIsTheShareLeftClampedAndNeverADivisionByNought()
        {
            Assert.That(HealthBarLayout.Fraction(50_000, 100_000), Is.EqualTo(0.5f));
            Assert.That(HealthBarLayout.Fraction(-10_000, 100_000), Is.Zero, "a downed pawn's bar is empty, not negative");
            Assert.That(HealthBarLayout.Fraction(120_000, 100_000), Is.EqualTo(1f));
            Assert.That(HealthBarLayout.Fraction(5, 0), Is.Zero);
        }
    }
}
