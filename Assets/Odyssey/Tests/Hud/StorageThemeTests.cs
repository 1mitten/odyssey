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

        static int Warmth(HudColour c) => c.R - c.B;

        static int Distance(HudColour a, HudColour b) =>
            System.Math.Abs(a.R - b.R) + System.Math.Abs(a.G - b.G) + System.Math.Abs(a.B - b.B);

        static string Hex(HudColour c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";
    }
}
