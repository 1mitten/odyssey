#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The interface acceptance criteria, as arithmetic.
    ///
    /// <para>Each of these was a sentence in a specification that nothing could check: "no two HUD
    /// panels overlap at 1280x720, 1920x1080 or 2560x1440", "total HUD coverage at or under 18% of
    /// the viewport with nothing selected", "every command-bar item has a visible hotkey; nothing
    /// is cut off at the right edge", "body text at 4.5:1 or better against the scrim over the
    /// brightest terrain in the game". They are testable here because the geometry, the palette
    /// and the bar's overflow rule all moved into the Unity-free assembly. The PlayMode gate then
    /// measures the realised boxes against the same model, so this cannot pass by describing a
    /// screen the shell is not building.</para>
    ///
    /// <para>The three resolutions the criteria name are all 16:9, and the panel scales with the
    /// screen against a 1920x1080 reference, so on a real monitor they resolve to one logical
    /// canvas. They are still run separately: the layout is anchored rather than scaled, so it has
    /// to be right at a literal 1280x720 as well — a host with a fixed-size panel, or a future
    /// decision to stop scaling, should not silently break it.</para>
    /// </summary>
    public class HudLayoutTests
    {
        static readonly (int W, int H)[] Resolutions = { (1280, 720), (1920, 1080), (2560, 1440) };

        /// <summary>A colony as the vertical slice ships it: three colonists, sixteen layers.</summary>
        const int Colonists = 3;
        const int Layers = 16;

        /// <summary>Every commodity the ledger knows, which is the expanded stores panel — the
        /// tallest that region ever gets.</summary>
        const int AllStoreRows = 6;

        [Test]
        public void NoTwoPanelsOverlapAtAnyOfTheThreeResolutions()
        {
            foreach ((int width, int height) in Resolutions)
            foreach (HudContent content in Cases())
            {
                var boxes = HudLayout.Solve(width, height, content);
                (HudRegion A, HudRegion B)? clash = HudLayout.FirstOverlap(boxes);

                Assert.That(clash, Is.Null,
                    clash == null ? string.Empty
                        : $"at {width}x{height} the {clash.Value.A} panel {boxes[clash.Value.A]} " +
                          $"overlaps the {clash.Value.B} panel {boxes[clash.Value.B]}");
            }
        }

        [Test]
        public void EveryPanelStaysInsideTheViewport()
        {
            foreach ((int width, int height) in Resolutions)
            foreach (HudContent content in Cases())
            foreach (KeyValuePair<HudRegion, HudRect> entry in HudLayout.Solve(width, height, content))
            {
                HudRect box = entry.Value;
                if (box.Empty) continue;

                Assert.That(box.X, Is.GreaterThanOrEqualTo(0f), $"{entry.Key} starts off the left edge at {width}x{height}");
                Assert.That(box.Y, Is.GreaterThanOrEqualTo(0f), $"{entry.Key} starts above the top edge at {width}x{height}");
                Assert.That(box.Right, Is.LessThanOrEqualTo(width),
                    $"{entry.Key} runs {box.Right - width:0.#} px past the right edge at {width}x{height}");
                Assert.That(box.Bottom, Is.LessThanOrEqualTo(height),
                    $"{entry.Key} runs {box.Bottom - height:0.#} px past the bottom edge at {width}x{height}");
            }
        }

        /// <summary>
        /// The headline criterion: the HUD covers at most 18% of the screen with nothing selected,
        /// down from about 31%. Asserted in the state the criterion names — a colony running, no
        /// selection, no alerts — and the stores panel collapsed, which is its own default.
        ///
        /// <para><b>Measured on the logical canvas, and that is the honest place to measure it.</b>
        /// The panel scales with the screen against a 1920x1080 reference, so all three
        /// resolutions the criteria name are 16:9 and resolve to exactly this one canvas: a panel
        /// covers the same <i>fraction</i> of a 1280x720 monitor as of a 4K one, which is the whole
        /// point of scaling with the screen. Measuring a literal 1280x720 canvas would be measuring
        /// a configuration the game does not ship — the HUD would be drawn at 1080p sizes on a
        /// 720p canvas, which is not what a player ever sees.</para>
        ///
        /// <para>The second case is a 4:3 screen. Unity's MatchWidthOrHeight at a match of 0.5
        /// takes the geometric mean of the two scale factors, so the logical canvas keeps very
        /// nearly the same <i>area</i> whatever the aspect — 1663x1247 against 1920x1080 is 2.073
        /// million square pixels against 2.074 — and the coverage figure is therefore a property of
        /// the layout rather than of the monitor. That is worth pinning, because it is the reason
        /// one number can answer the criterion for every screen.</para>
        /// </summary>
        [Test]
        public void CoverageWithNothingSelectedIsUnderTheCeiling()
        {
            (int W, int H, string What)[] canvases =
            {
                (1920, 1080, "the 16:9 canvas every resolution the criteria name resolves to"),
                (1663, 1247, "the canvas a 4:3 screen resolves to at the same panel scale"),
            };

            foreach ((int width, int height, string what) in canvases)
            {
                HudContent content = HudContent.NothingSelected(Colonists, storeRows: 3, Layers);
                var boxes = HudLayout.Solve(width, height, content);
                float coverage = HudLayout.Coverage(boxes, width, height);

                TestContext.WriteLine($"{width}x{height} ({what}): {coverage:P1} covered — " +
                    string.Join(", ", boxes.Select(b => $"{b.Key} {b.Value.Area / (width * height):P1}")));

                Assert.That(coverage, Is.LessThanOrEqualTo(HudLayout.CoverageCeiling),
                    $"at {width}x{height} the HUD covers {coverage:P1} of the viewport, over the " +
                    $"{HudLayout.CoverageCeiling:P0} ceiling. Per region: " +
                    string.Join(", ", boxes.Select(b => $"{b.Key} {b.Value.Area / (width * height):P1}")));
            }
        }

        /// <summary>
        /// And the worst case is reported rather than asserted, because the criterion is stated
        /// about the resting screen. A colonist selected and the stores panel thrown open is the
        /// most the HUD ever shows, and it is worth knowing what that costs.
        /// </summary>
        [Test]
        public void TheBusiestScreenIsMeasuredAndReported()
        {
            var content = new HudContent(Colonists, AllStoreRows, alerts: 2, Layers, needRows: 2);
            var boxes = HudLayout.Solve(1920, 1080, content);
            float coverage = HudLayout.Coverage(boxes, 1920, 1080);

            TestContext.WriteLine($"Busiest screen at 1920x1080: {coverage:P1} covered — " +
                string.Join(", ", boxes.Select(b => $"{b.Key} {b.Value}")));

            // Not the resting ceiling, but it must still be less than the HUD it replaces, which
            // measured about 31% doing nothing at all.
            Assert.That(coverage, Is.LessThan(0.31f),
                "the busiest screen covers more than the old resting HUD did, which is not a rebuild");
        }

        [Test]
        public void TheColonistStripNeverRunsIntoThePanelsEitherSideOfIt()
        {
            // A colony far larger than the slice will ever have: the strip must clamp rather than
            // grow into the stores panel, which is what makes "no two panels overlap" true by
            // construction rather than true for the colony sizes somebody happened to try.
            foreach ((int width, int height) in Resolutions)
            {
                var content = new HudContent(colonists: 40, storeRows: AllStoreRows, alerts: 3,
                    layers: Layers, needRows: 2);
                var boxes = HudLayout.Solve(width, height, content);

                Assert.That(HudLayout.FirstOverlap(boxes), Is.Null,
                    $"a colony of forty overflows the strip at {width}x{height}");
                Assert.That(HudLayout.VisibleCards(width, 40), Is.GreaterThan(0),
                    $"no colonist card fits at all at {width}x{height}");
            }
        }

        [Test]
        public void TheInspectPaneNeverReachesTheCommandBar()
        {
            foreach ((int width, int height) in Resolutions)
            foreach (HudContent content in Cases())
            {
                var boxes = HudLayout.Solve(width, height, content);
                HudRect pane = boxes[HudRegion.Inspect];
                HudRect bar = boxes[HudRegion.CommandBar];

                Assert.That(pane.Bottom, Is.LessThanOrEqualTo(bar.Y),
                    $"the inspect pane ends at {pane.Bottom:0.#} and the command bar starts at " +
                    $"{bar.Y:0.#} at {width}x{height}. The pane's bottom offset assumes the bar is " +
                    "one row tall, which is only safe because the bar may not wrap.");
            }
        }

        static IEnumerable<HudContent> Cases()
        {
            yield return HudContent.NothingSelected(Colonists, 3, Layers);          // resting
            yield return new HudContent(Colonists, AllStoreRows, 0, Layers, 2);     // colonist selected
            yield return new HudContent(Colonists, AllStoreRows, 3, Layers, 2);     // and in trouble
            yield return new HudContent(0, 0, 0, Layers, 0);                        // nobody left
            yield return new HudContent(8, AllStoreRows, 1, 32, 4);                 // a deeper, fuller game
        }
    }

    /// <summary>
    /// The command bar's own criteria: every item shows a hotkey, nothing runs off the edge, and
    /// what does not fit is reachable in Menu rather than lost.
    /// </summary>
    public class HudCommandTests
    {
        [Test]
        public void EveryItemShowsAHotkeyAndCarriesARegisteredName()
        {
            foreach (HudCommand command in HudCommands.All)
            {
                Assert.That(command.Hotkey, Is.Not.Empty, $"{command.Key} has no hotkey cap");
                Assert.That(Registry.Labels, Does.ContainKey(command.Key),
                    $"{command.Key} is not in the naming registry");
                Assert.That(command.Label, Is.EqualTo(Registry.Label(command.Key)),
                    "a command-bar label is the registry's word, never one invented in C#");
            }
        }

        [Test]
        public void HotkeysAreDistinctAndAvoidTheKeysTheGameAlreadyUses()
        {
            // M, C and X arm the designate tools; R and F move the slice; V cycles layer
            // visibility; Space and 1 to 3 are the clock; Home recentres. A legend on a control
            // that claims a key doing something else is worse than no legend.
            var taken = new[] { "M", "C", "X", "R", "F", "V", "1", "2", "3" };
            var seen = new List<string>();

            foreach (HudCommand command in HudCommands.All)
            {
                Assert.That(seen, Has.No.Member(command.Hotkey),
                    $"two command-bar items both claim {command.Hotkey}");
                Assert.That(taken, Has.No.Member(command.Hotkey),
                    $"{command.Key} claims {command.Hotkey}, which the game already uses");
                seen.Add(command.Hotkey);
            }
        }

        [Test]
        public void TheBarNeverRunsPastTheEdgeAtAnyWidth()
        {
            List<float> widths = HudCommands.ModelWidths();

            for (int screen = 480; screen <= 3840; screen += 17)
            {
                float available = screen - 2 * HudLayout.Edge;
                int shown = HudCommands.Fit(widths, available);
                float bar = HudCommands.BarWidth(widths, shown);

                Assert.That(bar, Is.LessThanOrEqualTo(available + 0.01f),
                    $"at {screen} px wide the bar comes out {bar:0.#} px against {available:0.#} of room");
                Assert.That(shown, Is.InRange(0, widths.Count - 1));
            }
        }

        [Test]
        public void MenuIsNeverTheItemThatGetsDropped()
        {
            // Menu is where everything that did not fit goes, so a bar narrow enough to drop it
            // would strand every other item with it.
            List<float> widths = HudCommands.ModelWidths();
            Assert.That(HudCommands.Fit(widths, 1f), Is.Zero,
                "at no width at all the bar shows Menu and nothing else, rather than nothing");
            Assert.That(HudCommands.BarWidth(widths, 0), Is.GreaterThan(0f),
                "the bar with everything in Menu still has Menu in it");
        }

        [Test]
        public void TheWholeBarFitsOnACommonDesktop()
        {
            List<float> widths = HudCommands.ModelWidths();
            int shown = HudCommands.Fit(widths, 1920 - 2 * HudLayout.Edge);

            Assert.That(shown, Is.EqualTo(widths.Count - 1),
                $"only {shown} of {widths.Count - 1} command items fit at 1920 px wide, so the " +
                "bar is overflowing into Menu on the resolution it is designed for. The modelled " +
                "widths are deliberately generous, so this is a real squeeze rather than an " +
                "estimate being pessimistic.");
        }

        [Test]
        public void ExactlyOneItemIsPrimary()
        {
            int primaries = HudCommands.All.Count(c => c.Primary);
            Assert.That(primaries, Is.EqualTo(1), "the bar has one filled item: Build");
            Assert.That(HudCommands.All.Single(c => c.Primary).Key, Is.EqualTo(HudCommands.BuildKey));
        }
    }

    /// <summary>
    /// The type scale and the palette: six steps and no others, numbers always mono, and body text
    /// legible over the brightest thing the world can draw.
    /// </summary>
    public class HudTypeTests
    {
        [Test]
        public void TheScaleIsTheSixStepsAndTheHotkeyLegend()
        {
            var sizes = HudType.Sizes.OrderBy(s => s).ToArray();

            // Eleven appears twice — the panel label and the hotkey cap — so six roles and the
            // legend come to six distinct sizes.
            Assert.That(sizes, Is.EqualTo(new[] { 11, 12, 13, 14, 19, 24 }),
                "the interface may use these sizes and no others. A seventh is a design decision " +
                "and belongs in the specification before it belongs in the code.");
        }

        [Test]
        public void TheClockIsMonoAndSoIsAnythingAskedForAsANumber()
        {
            Assert.That(HudType.Of(HudTextRole.Clock).Mono, Is.True, "the clock is always mono");
            Assert.That(HudType.Of(HudTextRole.Hotkey).Mono, Is.True, "a hotkey cap is always mono");

            foreach (HudTextRole role in HudType.Roles)
            {
                HudTextStyle numeric = HudType.Of(role, numeric: true);
                Assert.That(numeric.Mono, Is.True, $"{role} as a number is not set in the mono face");
                Assert.That(numeric.Weight, Is.EqualTo(HudType.MonoWeight),
                    $"{role} as a number is not at the weight every figure is set in");
                Assert.That(numeric.Size, Is.EqualTo(HudType.Of(role).Size),
                    "asking for a number changes the family, never the step");
            }
        }

        [Test]
        public void OnlyThePanelLabelIsTrackedAndUpperCased()
        {
            foreach (HudTextRole role in HudType.Roles)
            {
                HudTextStyle style = HudType.Of(role);
                bool label = role == HudTextRole.PanelLabel;
                Assert.That(style.Uppercase, Is.EqualTo(label), $"{role} upper-casing");
                Assert.That(style.LetterSpacing > 0f, Is.EqualTo(label), $"{role} tracking");
            }
        }

        [Test]
        public void BodyTextIsLegibleOverTheBrightestTerrainInTheGame()
        {
            // White is used as the terrain rather than a sampled colour: every terrain in the game
            // is darker than white, so a ratio that holds here holds against all of them and the
            // test cannot go stale when a new biome lands.
            (string name, HudColour ink)[] body =
            {
                ("primary", HudTheme.TextPrimary),
                ("meta", HudTheme.TextMeta),
                ("dim", HudTheme.TextDim),
            };

            foreach ((string name, HudColour ink) in body)
            {
                double ratio = HudContrast.OverBrightestTerrain(ink, HudTheme.PanelFill);
                TestContext.WriteLine($"{name} over a panel over white: {ratio:0.00}:1");
                Assert.That(ratio, Is.GreaterThanOrEqualTo(HudContrast.BodyMinimum),
                    $"{name} ink reads at {ratio:0.00}:1 against a panel laid over white terrain");
            }
        }

        [Test]
        public void TheAccentCarriesItsOwnInk()
        {
            // The one place the palette puts text on a filled block: the primary command and the
            // active speed button. The dark ink has to survive it.
            double ratio = HudContrast.Ratio(HudTheme.OnAccent, HudTheme.Accent);
            TestContext.WriteLine($"on-accent ink over accent: {ratio:0.00}:1");
            Assert.That(ratio, Is.GreaterThanOrEqualTo(HudContrast.BodyMinimum));
        }

        [Test]
        public void EveryCategoryColourIsAPaletteToken()
        {
            var palette = new[]
            {
                HudTheme.Accent.Hex, HudTheme.Warn.Hex, HudTheme.Bad.Hex, HudTheme.Good.Hex,
                HudTheme.Info.Hex, HudTheme.TextMeta.Hex,
            };

            foreach (HudCategory category in System.Enum.GetValues(typeof(HudCategory)))
                Assert.That(palette, Has.Member(HudTheme.ColourOf(category).Hex),
                    $"{category} resolves to a colour that is not in the palette. A screen with a " +
                    "hue per commodity is a screen with no colour code at all.");
        }

        [Test]
        public void EveryCategorisedKeyIsARegisteredName()
        {
            foreach (string key in HudTheme.CategorisedKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key),
                    $"{key} carries a category colour but is not a name the registry knows");
        }

        [Test]
        public void EveryAlertKeyIsARegisteredName()
        {
            foreach (string key in AlertModel.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
        }
    }
}
