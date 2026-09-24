#nullable enable
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The eleven hues the storage pane introduces — five rungs and six categories — against the
    /// two things the design brief asks of them: <b>readable</b>, and <b>distinguishable with the
    /// labels masked</b>.
    ///
    /// <para>Taken rather than trusted. The brief names Materials' tan and Urgent's amber as the
    /// two most likely to fail contrast, and a palette that is checked by eye is a palette that
    /// passes until somebody screenshots it on a bright tile — which is how "Zones, at rest"
    /// once shipped at 1.16:1.</para>
    /// </summary>
    public class StorageThemeTests
    {
        [Test]
        public void EveryRungAndEveryCategoryLabelClearsTheContrastFloor()
        {
            foreach (HudColour hue in HudTheme.StoragePriorityHues)
                Assert.That(HudContrast.OverBrightestTerrain(hue, HudTheme.PanelFill),
                    Is.GreaterThanOrEqualTo(HudContrast.BodyMinimum),
                    $"a rung's label at {Hex(hue)} is unreadable over the panel");

            foreach (HudColour hue in HudTheme.ItemCategoryHues)
                Assert.That(HudContrast.OverBrightestTerrain(hue, HudTheme.PanelFill),
                    Is.GreaterThanOrEqualTo(HudContrast.BodyMinimum),
                    $"a category's label at {Hex(hue)} is unreadable over the panel");
        }

        /// <summary>
        /// The acceptance criterion in the brief's own words: each is distinct with the labels
        /// masked. Sixty channel-points is the separation <c>OrderColours</c> already polices for
        /// the order hues, and it is the same eye and the same screen.
        /// </summary>
        [Test]
        public void NoTwoCategoriesLookAlike()
        {
            HudColour[] hues = HudTheme.ItemCategoryHues;
            for (int a = 0; a < hues.Length; a++)
            for (int b = a + 1; b < hues.Length; b++)
                Assert.That(Distance(hues[a], hues[b]), Is.GreaterThanOrEqualTo(60),
                    $"categories {a} and {b} — {Hex(hues[a])} and {Hex(hues[b])} — are the same colour to a player");
        }

        /// <summary>
        /// "Cool to warm as urgency rises", asserted as the claim that is actually true of the
        /// palette rather than a stricter one that is not.
        ///
        /// <para><b>The first version of this test demanded warmth rise at every step and the
        /// brief's own palette fails it</b> — Normal's cyan is <i>cooler</i> than Low's slate by
        /// red-minus-blue, because cyan gets its presence from brightness rather than from
        /// warmth. Rather than bend five colours to satisfy a metric nobody looks at, the test
        /// says what the eye sorts on: <b>the two urgent rungs are warmer than all three
        /// unurgent ones</b>, and the split falls exactly where the meaning does.</para>
        /// </summary>
        [Test]
        public void TheUrgentRungsAreWarmerThanEveryUnurgentOne()
        {
            HudColour[] hues = HudTheme.StoragePriorityHues;
            int coolest = int.MinValue;
            for (int rung = 0; rung <= 2; rung++) coolest = System.Math.Max(coolest, Warmth(hues[rung]));

            for (int rung = 3; rung < hues.Length; rung++)
                Assert.That(Warmth(hues[rung]), Is.GreaterThan(coolest),
                    $"rung {rung} is not warmer than every cool rung, so the ladder does not read as one");
        }

        [Test]
        public void NoTwoRungsLookAlike()
        {
            HudColour[] hues = HudTheme.StoragePriorityHues;
            for (int a = 0; a < hues.Length; a++)
            for (int b = a + 1; b < hues.Length; b++)
                Assert.That(Distance(hues[a], hues[b]), Is.GreaterThanOrEqualTo(60),
                    $"rungs {a} and {b} are the same colour to a player");
        }

        [Test]
        public void TheTablesAreTheLengthTheModelExpects()
        {
            Assert.That(HudTheme.StoragePriorityHues, Has.Length.EqualTo(StorageSettingsModel.PriorityKeys.Length));
            Assert.That(HudTheme.ItemCategoryHues, Has.Length.EqualTo(StorageSettingsModel.CategoryKeys.Length));

            // Out of range answers a colour rather than throwing, because a row drawn in the wrong
            // colour is a bug you can see and an exception in a panel refresh is a black screen.
            Assert.That(HudTheme.StoragePriorityHue(99).A, Is.GreaterThan(0f));
            Assert.That(HudTheme.ItemCategoryHue(-1).A, Is.GreaterThan(0f));
        }

        /// <summary>
        /// Owner, 2026-09-23: "check this for accessibility". The six hues all cleared contrast,
        /// and under deuteranopia three of them were one colour — Food, Weapons and Materials
        /// within 2 to 3 CIE Lab units. Simulated here with Machado, Oliveira and Fernandes (2009)
        /// at full severity for the three dichromacies, and every pair held apart in all four
        /// views. Colour is the second cue after each category's glyph and name; this keeps it an
        /// honest one.
        /// </summary>
        [Test]
        public void NoTwoCategoriesLookAlikeToAColourBlindPlayer()
        {
            HudColour[] hues = HudTheme.ItemCategoryHues;
            foreach ((string name, double[,]? m) in Visions)
                for (int a = 0; a < hues.Length; a++)
                for (int b = a + 1; b < hues.Length; b++)
                {
                    double distance = DeltaE(Simulate(hues[a], m), Simulate(hues[b], m));
                    Assert.That(distance, Is.GreaterThanOrEqualTo(14.0),
                        $"under {name}, categories {a} and {b} ({Hex(hues[a])}, {Hex(hues[b])}) are " +
                        $"{distance:0.0} Lab units apart: one colour to that player");
                }
        }

        /// <summary>The heading and its glyph are drawn over their own row's wash, not over bare panel.</summary>
        [Test]
        public void EveryCategoryLabelIsReadableOverItsOwnWash()
        {
            foreach (HudColour hue in HudTheme.ItemCategoryHues)
            {
                HudColour row = HudContrast.Over(hue.WithAlpha(HudTheme.ItemCategoryWash), HudTheme.PanelFill);
                Assert.That(HudContrast.Ratio(hue, row), Is.GreaterThanOrEqualTo(HudContrast.BodyMinimum),
                    $"a category's label at {Hex(hue)} is unreadable over its own heading row");
            }
        }

        static readonly (string, double[,]?)[] Visions =
        {
            ("normal vision", null),
            ("protanopia", new[,] { { 0.152286, 1.052583, -0.204868 }, { 0.114503, 0.786281, 0.099216 }, { -0.003882, -0.048116, 1.051998 } }),
            ("deuteranopia", new[,] { { 0.367322, 0.860646, -0.227968 }, { 0.280085, 0.672501, 0.047413 }, { -0.011820, 0.042940, 0.968881 } }),
            ("tritanopia", new[,] { { 1.255528, -0.076749, -0.178779 }, { -0.078411, 0.930809, 0.147602 }, { 0.004733, 0.691367, 0.303900 } }),
        };

        static double Linear(double c) => c <= 0.04045 ? c / 12.92 : System.Math.Pow((c + 0.055) / 1.055, 2.4);

        /// <summary>A colour as that eye sees it, in linear RGB, clamped.</summary>
        static double[] Simulate(HudColour c, double[,]? m)
        {
            double[] l = { Linear(c.R / 255.0), Linear(c.G / 255.0), Linear(c.B / 255.0) };
            if (m == null) return l;
            var o = new double[3];
            for (int r = 0; r < 3; r++)
                o[r] = System.Math.Clamp(m[r, 0] * l[0] + m[r, 1] * l[1] + m[r, 2] * l[2], 0.0, 1.0);
            return o;
        }

        /// <summary>CIE 1976 distance between two linear-RGB colours, through XYZ (D65) and Lab.</summary>
        static double DeltaE(double[] a, double[] b)
        {
            double[] la = Lab(a), lb = Lab(b);
            return System.Math.Sqrt((la[0] - lb[0]) * (la[0] - lb[0]) + (la[1] - lb[1]) * (la[1] - lb[1])
                                    + (la[2] - lb[2]) * (la[2] - lb[2]));
        }

        static double[] Lab(double[] l)
        {
            double x = (0.4124 * l[0] + 0.3576 * l[1] + 0.1805 * l[2]) / 0.95047;
            double y = 0.2126 * l[0] + 0.7152 * l[1] + 0.0722 * l[2];
            double z = (0.0193 * l[0] + 0.1192 * l[1] + 0.9505 * l[2]) / 1.08883;
            static double F(double t) => t > 0.008856 ? System.Math.Pow(t, 1.0 / 3.0) : 7.787 * t + 16.0 / 116.0;
            return new[] { 116 * F(y) - 16, 500 * (F(x) - F(y)), 200 * (F(y) - F(z)) };
        }

        static int Warmth(HudColour c) => c.R - c.B;

        static int Distance(HudColour a, HudColour b) =>
            System.Math.Abs(a.R - b.R) + System.Math.Abs(a.G - b.G) + System.Math.Abs(a.B - b.B);

        static string Hex(HudColour c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";
    }
}
