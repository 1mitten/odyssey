#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The Build palette's selection, layout and readouts.
    ///
    /// <para><b>Why these are in the fast tier at all.</b> The specification for the three layouts
    /// is mostly a statement about pixels, and pixels are judged by eye. But four of its
    /// acceptance criteria are not: a fresh profile opens in Rows, a chosen layout survives a
    /// reload, switching layout changes no selection, and every label clears 4.5:1 against its own
    /// button. Each of those is a claim that can be wrong silently, and each would otherwise be
    /// checked by opening the game and remembering to look. <see cref="BuildPaletteModel"/> exists
    /// in the Unity-free assembly so that they can be checked in eleven seconds instead.</para>
    /// </summary>
    public class BuildPaletteTests
    {
        static (BuildPaletteModel palette, DesignateDirector designate, SettingsDirector settings)
            Open(FakeSettingsStore? store = null)
        {
            var designate = new DesignateDirector();
            var settings = new SettingsDirector();
            if (store != null) settings.UseStore(store);
            return (new BuildPaletteModel(designate, settings), designate, settings);
        }

        /// <summary>Where the Zones category sits today. Looked up by key rather than written
        /// as a number, so reordering the palette cannot silently re-point these tests.</summary>
        static int ZonesCategory
        {
            get
            {
                for (int i = 0; i < PaletteTools.Categories.Length; i++)
                    if (PaletteTools.Categories[i].key == "ui.arch.category.zones") return i;
                Assert.Fail("the Zones category is gone from the palette");
                return -1;
            }
        }

        // ---------------------------------------------------------------- the categories

        /// <summary>
        /// Eight categories, and the two that were dropped stay dropped rather than hidden.
        ///
        /// <para><b>Zones came back (U49) and that is the exception recorded, not the rule
        /// relaxed.</b> The 2026-09-17 sweep took Orders, Zones and Salvage off because they did
        /// not answer "what would you like to put down". A growing zone does: it is painted on the
        /// board like a floor and worked like one, so it returns as one placeable thing — while
        /// Orders stays gone (its live tools are pinned in the gutter strip) and Salvage stays
        /// gone outright. This test holds both halves: the row that returned is here, and the two
        /// that left for good have not crept back.</para>
        /// </summary>
        [Test]
        public void EightCategoriesAndTheDroppedOnesStayDropped()
        {
            Assert.That(PaletteTools.Categories.Length, Is.EqualTo(8));

            var keys = new List<string>();
            foreach (var (key, _) in PaletteTools.Categories) keys.Add(key);

            Assert.That(keys, Does.Contain("ui.arch.category.zones"),
                "the growing zone is a thing to put down, and the palette is where things to " +
                "put down live");
            Assert.That(keys, Does.Not.Contain("ui.arch.category.orders"));
            Assert.That(keys, Does.Not.Contain("ui.arch.category.salvage"));
        }

        /// <summary>
        /// Every category has a hue, and the hues are the category's identity as well as its
        /// selected state — so no two may be the same colour.
        ///
        /// <para>This is the machine-checkable half of the acceptance criterion "each is visually
        /// distinct with the labels masked". It cannot say whether two hues are far enough apart
        /// to tell at a glance, which is an eye's question; it can say that nobody has pasted a
        /// row twice, which is how two categories usually come to share a colour.</para>
        /// </summary>
        [Test]
        public void EveryCategoryHasItsOwnHue()
        {
            Assert.That(HudTheme.BuildCategoryTiers.Length, Is.EqualTo(PaletteTools.Categories.Length),
                "a category without a hue would draw in whatever the previous one left behind");

            var seen = new List<string>();
            foreach (HudTheme.BuildTier tier in HudTheme.BuildCategoryTiers)
            {
                Assert.That(seen, Does.Not.Contain(tier.Hue.Hex),
                    $"two categories share the hue {tier.Hue.Hex}");
                seen.Add(tier.Hue.Hex);
            }
        }

        /// <summary>
        /// A selected tile is a stronger wash of its own hue, never the interface's cyan.
        ///
        /// <para>The rule matters because cyan is what "this is on" means everywhere else in this
        /// HUD, and on this one tier it would destroy the only thing the tier is for: with a
        /// global highlight, the selected category stops being the one category you can identify
        /// by colour.</para>
        /// </summary>
        [Test]
        public void SelectionOnTheCategoryTierIsTheCategorysOwnColour()
        {
            foreach (HudTheme.BuildTier tier in HudTheme.BuildCategoryTiers)
            {
                Assert.That(tier.Selected.Hex, Is.EqualTo(tier.Hue.Hex));
                Assert.That(tier.SelectedFill.R, Is.EqualTo(tier.Hue.R));
                Assert.That(tier.SelectedFill.G, Is.EqualTo(tier.Hue.G));
                Assert.That(tier.SelectedFill.B, Is.EqualTo(tier.Hue.B));
                Assert.That(tier.SelectedFill.A, Is.GreaterThan(tier.Fill.A),
                    "the selected wash is not stronger than the resting one, so selection is invisible");
            }
        }

        /// <summary>
        /// Rail's sub-type grid is tall enough for the largest category.
        ///
        /// <para><b>This is the fast-tier half of a PlayMode failure.</b> Rail's only claim is
        /// that its height does not change when the category does, and it did: the panel measured
        /// 376 px on Structure and 343 on Production, because the grid sized itself to whatever
        /// was in it. The fix is a fixed grid, and a fixed grid is a number that can go stale —
        /// so this is the test that fails the moment a category outgrows it, which costs eleven
        /// seconds rather than a PlayMode run, and which says what to change.</para>
        /// </summary>
        [Test]
        public void TheRailGridIsTallEnoughForTheLargestCategory()
        {
            int largest = 0;
            string widest = string.Empty;
            foreach (var (key, tools) in PaletteTools.Categories)
                if (tools.Length > largest)
                {
                    largest = tools.Length;
                    widest = Registry.Label(key);
                }

            int needed = HudLayout.BuildRailRowsNeeded(largest);
            Assert.That(HudLayout.BuildRailSubRows, Is.EqualTo(needed),
                $"{widest} has {largest} sub-types, which is {needed} rows of " +
                $"{HudLayout.BuildRailColumns}, and the Rail grid is fixed at " +
                $"{HudLayout.BuildRailSubRows}. Either the grid is too short and the category's " +
                "last tiles are cut off, or it is too tall and the panel carries dead space.");
        }

        // ---------------------------------------------------------------- layout

        /// <summary>A profile that has never been told opens in Rows.</summary>
        [Test]
        public void AFreshProfileOpensInRows()
        {
            var (palette, _, _) = Open(new FakeSettingsStore());
            Assert.That(palette.Layout, Is.EqualTo(BuildPaletteLayout.Rows));
            Assert.That(BuildPaletteModel.Default, Is.EqualTo(BuildPaletteLayout.Rows));
        }

        /// <summary>
        /// A chosen layout survives the reload. The store is the same fake in both halves, which
        /// is what "the same machine, opened again" means here.
        /// </summary>
        [Test]
        public void TheChosenLayoutComesBack()
        {
            var store = new FakeSettingsStore();

            var (first, _, _) = Open(store);
            Assert.That(first.SetLayout(BuildPaletteLayout.Bar), Is.True);

            var (second, _, _) = Open(store);
            Assert.That(second.Layout, Is.EqualTo(BuildPaletteLayout.Bar));
        }

        /// <summary>A stored number that is not a layout is ignored rather than cast.</summary>
        [Test]
        public void ANonsenseStoredLayoutFallsBackToTheDefault()
        {
            var store = new FakeSettingsStore();
            store.Preset(SettingsDirector.BuildLayoutKey, 97);

            var (palette, _, _) = Open(store);
            Assert.That(palette.Layout, Is.EqualTo(BuildPaletteModel.Default));
        }

        /// <summary>
        /// The criterion the whole shared-state design exists for: switching layout preserves
        /// category, sub-type and material exactly.
        /// </summary>
        [Test]
        public void SwitchingLayoutChangesNothingThatIsSelected()
        {
            var (palette, _, _) = Open(new FakeSettingsStore());

            palette.SelectCategory(0);
            palette.SelectSubType(PaletteTools.Wall);
            palette.SelectMaterial(StuffHandle.Stone);

            int category = palette.Category;
            string subType = palette.SubType;
            int material = palette.Material;

            foreach (BuildPaletteLayout layout in BuildPaletteModel.Layouts)
            {
                Assert.That(palette.SetLayout(layout), Is.True);
                Assert.That(palette.Category, Is.EqualTo(category), "the category moved");
                Assert.That(palette.SubType, Is.EqualTo(subType), "the sub-type moved");
                Assert.That(palette.Material, Is.EqualTo(material), "the material moved");
            }
        }

        /// <summary>
        /// A layout switch mid-drag is refused, because the cells already swept have no meaning
        /// in the panel that would replace this one.
        /// </summary>
        [Test]
        public void TheLayoutCannotChangeUnderADragInProgress()
        {
            var (palette, designate, _) = Open(new FakeSettingsStore());
            palette.SelectSubType(PaletteTools.Wall);

            designate.Begin(new CellRef(4, 2, 4));
            Assert.That(designate.Dragging, Is.True, "the fixture did not actually start a drag");

            Assert.That(palette.SetLayout(BuildPaletteLayout.Rail), Is.False);
            Assert.That(palette.Layout, Is.EqualTo(BuildPaletteLayout.Rows));

            designate.Abandon();
            Assert.That(palette.SetLayout(BuildPaletteLayout.Rail), Is.True);
        }

        /// <summary>
        /// Both controls over one preference. The settings row and the header switcher have to
        /// read and write the same value, or the second one a player touches appears to undo the
        /// first.
        /// </summary>
        [Test]
        public void TheSettingsRowAndTheHeaderSwitcherAreOnePreference()
        {
            var (palette, _, settings) = Open(new FakeSettingsStore());

            settings.SetBuildPaletteLayout(BuildPaletteLayout.Bar);
            Assert.That(palette.Layout, Is.EqualTo(BuildPaletteLayout.Bar));

            palette.SetLayout(BuildPaletteLayout.Rail);
            Assert.That(settings.BuildPaletteLayout, Is.EqualTo(BuildPaletteLayout.Rail));
        }

        /// <summary>Changing layout tells the shell once, so it can tear one down and raise the
        /// other. A repeat of the layout already showing says nothing.</summary>
        [Test]
        public void ALayoutChangeIsAnnouncedOnceAndOnlyWhenItChanges()
        {
            var (palette, _, _) = Open(new FakeSettingsStore());

            int raised = 0;
            palette.LayoutChanged += _ => raised++;

            palette.SetLayout(BuildPaletteLayout.Rail);
            palette.SetLayout(BuildPaletteLayout.Rail);
            Assert.That(raised, Is.EqualTo(1));
        }

        // ---------------------------------------------------------------- selection

        /// <summary>
        /// Opening a category lands on its first buildable entry rather than on index zero.
        /// </summary>
        [Test]
        public void ACategoryOpensOnSomethingThatWorks()
        {
            var (palette, _, _) = Open();

            for (int i = 0; i < PaletteTools.Categories.Length; i++)
            {
                palette.SelectCategory(i);

                string[] tools = PaletteTools.Categories[i].tools;
                string expected = tools[0];
                foreach (string tool in tools)
                {
                    if (!BuildPaletteModel.IsBuildable(tool)) continue;
                    expected = tool;
                    break;
                }

                Assert.That(palette.SubType, Is.EqualTo(expected),
                    $"{Registry.Label(PaletteTools.Categories[i].key)} opened on the wrong entry");
            }
        }

        /// <summary>
        /// A palette that has just been built has armed nothing.
        ///
        /// <para><b>This is the test for a real defect, found through its symptom.</b> The model is
        /// constructed when the HUD attaches to its directors — long before the player opens
        /// anything — and it used to arm its landing sub-type there. So the game began in build
        /// mode: a wall on the cursor from the first frame, which nobody had asked for and which a
        /// click on the world would have placed. It surfaced as the owner noticing that the command
        /// bar's Build cap was lit when it should not have been, and the lit cap was telling the
        /// truth.</para>
        /// </summary>
        [Test]
        public void ANewPaletteHasArmedNothing()
        {
            var (palette, designate, _) = Open();

            Assert.That(designate.Tool, Is.EqualTo(DesignateTool.None),
                "building the palette put a tool in the player's hand before the game started");
            Assert.That(palette.SubTypeIsArmed, Is.False);
            Assert.That(palette.SubType, Is.EqualTo(PaletteTools.Wall),
                "the palette should still be pointing at something, so that opening it is not blank");
        }

        /// <summary>
        /// The first click on the tile the palette was already pointing at still arms it.
        ///
        /// <para>The other half of the fix above, and the way it could have gone wrong: with the
        /// landing sub-type seeded but unarmed, a guard of "already selected, do nothing" makes the
        /// first tile a player reaches for a dead button.</para>
        /// </summary>
        [Test]
        public void ClickingTheTileAlreadyPointedAtStillArmsIt()
        {
            var (palette, designate, _) = Open();
            Assert.That(palette.SubType, Is.EqualTo(PaletteTools.Wall));

            palette.SelectSubType(PaletteTools.Wall);

            Assert.That(palette.SubTypeIsArmed, Is.True,
                "the first click of a session landed on the tile the palette was pointing at and " +
                "did nothing");
            Assert.That(designate.Tool, Is.EqualTo(DesignateTool.Build));
        }

        /// <summary>
        /// Each sub-type remembers what it was last made of, separately from the others.
        /// </summary>
        [Test]
        public void ASubTypeRemembersWhatItWasLastMadeOf()
        {
            var (palette, _, _) = Open();

            palette.SelectCategory(0);
            palette.SelectSubType(PaletteTools.Wall);
            palette.SelectMaterial(StuffHandle.Stone);
            Assert.That(palette.Material, Is.EqualTo(StuffHandle.Stone));

            // Away and back: a different category, then this one again.
            palette.SelectCategory(3);
            palette.SelectCategory(0);
            palette.SelectSubType(PaletteTools.Wall);

            Assert.That(palette.Material, Is.EqualTo(StuffHandle.Stone),
                "the wall forgot that it was last built out of stone");
        }

        /// <summary>
        /// Only a thing made of something offers a material. An order is a verb applied to what is
        /// already there.
        /// </summary>
        [Test]
        public void AnOrderIsNotMadeOfAnything()
        {
            var (palette, _, _) = Open();

            palette.SelectCategory(0);
            palette.SelectSubType(PaletteTools.Wall);
            Assert.That(palette.WantsMaterial, Is.True);

            palette.TogglePinned(PaletteTools.Cancel);
            Assert.That(palette.WantsMaterial, Is.False);
            Assert.That(palette.CostLine, Is.Empty);
        }

        // ---------------------------------------------------------------- the plant tier

        /// <summary>
        /// The growing zone is the one order that takes a payload, and the payload is a crop:
        /// arming it opens the plant tier and closes the material one.
        /// </summary>
        [Test]
        public void TheZoneToolOffersACropAndNotAMaterial()
        {
            var (palette, designate, _) = Open();
            palette.SelectCategory(ZonesCategory);
            palette.SelectSubType(PaletteTools.GrowZone);

            Assert.That(designate.Tool, Is.EqualTo(DesignateTool.GrowZone));
            Assert.That(palette.WantsPlant, Is.True);
            Assert.That(palette.WantsMaterial, Is.False,
                "soil is not built out of anything, and two payload rows open at once would be " +
                "two questions asked of one tool");
            Assert.That(palette.Breadcrumb, Is.EqualTo("Zones › Growing zone › Carrot"));
        }

        /// <summary>
        /// Choosing a crop configures the next box; it does not arm the tool. The same bargain
        /// <see cref="AnOrderIsNotMadeOfAnything"/>'s material tier makes one tier over.
        /// </summary>
        [Test]
        public void ChoosingACropDoesNotArmAnything()
        {
            var (palette, designate, _) = Open();
            palette.SelectCategory(ZonesCategory);
            palette.SelectSubType(PaletteTools.GrowZone);
            designate.Tool = DesignateTool.None;

            palette.SelectPlant(PlantHandle.Carrot);
            Assert.That(designate.Tool, Is.EqualTo(DesignateTool.None),
                "a pick in the plant tier left a tool in the player's hand");
            Assert.That(designate.Plant, Is.EqualTo(PlantHandle.Carrot));
        }

        /// <summary>
        /// A crop nothing offers cannot be chosen, even directly — the picker walks
        /// <see cref="PaletteTools.Plants"/>, and so does the guard.
        /// </summary>
        [Test]
        public void APlantOutsideTheTableCannotBeChosen()
        {
            var (palette, designate, _) = Open();
            palette.SelectCategory(ZonesCategory);
            palette.SelectSubType(PaletteTools.GrowZone);

            int unknown = PlantHandle.Count; // one past the last real crop
            Assert.That(Array.IndexOf(PaletteTools.Plants, unknown), Is.LessThan(0));

            palette.SelectPlant(unknown);
            Assert.That(designate.Plant, Is.Not.EqualTo(unknown));
        }

        /// <summary>
        /// Another pinned action held hides the picker, exactly as it hides the material band:
        /// while the next drag cancels or mines, a panel offering crops would be offering a
        /// choice the drag cannot use.
        /// </summary>
        [Test]
        public void AnotherOrderHeldHidesThePlantTier()
        {
            var (palette, _, _) = Open();
            palette.SelectCategory(ZonesCategory);
            palette.SelectSubType(PaletteTools.GrowZone);
            Assert.That(palette.WantsPlant, Is.True);

            palette.TogglePinned(PaletteTools.Cancel);
            Assert.That(palette.WantsPlant, Is.False);

            palette.TogglePinned(PaletteTools.Cancel);
            Assert.That(palette.WantsPlant, Is.True, "putting the order down lost the picker");
        }

        /// <summary>
        /// Holding the zone tool keeps the picker up — the exemption in
        /// <c>BuildPaletteModel.WantsPlant</c>, and the reason it is not a copy of
        /// <c>WantsMaterial</c>'s pinned rule: the zone tool <i>is</i> a pinned action, and the
        /// crop is what its next drag carries.
        /// </summary>
        [Test]
        public void HoldingTheZoneToolKeepsItsOwnPicker()
        {
            var (palette, _, _) = Open();
            palette.SelectCategory(ZonesCategory);
            palette.SelectSubType(PaletteTools.GrowZone);
            Assert.That(palette.ArmedPinned, Is.EqualTo(PaletteTools.GrowZone),
                "the fixture did not actually pick the tool up");
            Assert.That(palette.WantsPlant, Is.True);

            // Down and up again, the way the strip's toggle moves it: the picker stays open in
            // both states, because the crop is wanted exactly when the tool is held.
            palette.TogglePinned(PaletteTools.GrowZone);
            Assert.That(palette.ArmedPinned, Is.Empty);
            Assert.That(palette.WantsPlant, Is.True,
                "pointing at the zone tool without holding it is when the picker is wanted most");

            palette.TogglePinned(PaletteTools.GrowZone);
            Assert.That(palette.WantsPlant, Is.True, "holding the tool shut its own picker");
        }

        // ---------------------------------------------------------------- stock

        /// <summary>
        /// A material the colony has none of cannot be chosen, and an armed build falls off it on
        /// to something it does have.
        /// </summary>
        [Test]
        public void AnEmptyStoreTakesItsMaterialOffTheMenu()
        {
            var (palette, _, _) = Open();
            palette.SelectSubType(PaletteTools.Wall);
            palette.SelectMaterial(StuffHandle.Wood);

            palette.ReadStockFrom(stuff => stuff == StuffHandle.Stone ? 40 : 0);

            Assert.That(palette.IsStocked(StuffHandle.Wood), Is.False);
            Assert.That(palette.Material, Is.EqualTo(StuffHandle.Stone),
                "the palette stayed armed with a material the colony does not have");

            palette.SelectMaterial(StuffHandle.Wood);
            Assert.That(palette.Material, Is.EqualTo(StuffHandle.Stone),
                "an out-of-stock material was chosen anyway");
        }

        /// <summary>
        /// Before a snapshot exists, everything is in stock. An interface that greys every
        /// material out because it has not been told yet is worse than one that offers a build a
        /// hauler then waits on.
        /// </summary>
        [Test]
        public void WithNothingKnownEverythingIsOffered()
        {
            var (palette, _, _) = Open();
            foreach (int stuff in PaletteTools.Materials)
                Assert.That(palette.IsStocked(stuff), Is.True);
        }

        // ---------------------------------------------------------------- readouts

        /// <summary>The breadcrumb names all three tiers of a build, in order.</summary>
        [Test]
        public void TheBreadcrumbNamesTheWholeChoice()
        {
            var (palette, _, _) = Open();
            palette.SelectCategory(0);
            palette.SelectSubType(PaletteTools.Wall);
            palette.SelectMaterial(StuffHandle.Wood);

            Assert.That(palette.Breadcrumb, Is.EqualTo("Structure › Wall › Wood"));
        }

        /// <summary>
        /// The cost appears once, in the panel, in the form the specification prints it.
        /// </summary>
        [Test]
        public void TheCostIsOneLineInTheMaterialThatIsArmed()
        {
            var (palette, _, _) = Open();
            palette.SelectSubType(PaletteTools.Wall);
            palette.SelectMaterial(StuffHandle.Wood);
            palette.ReadCostFrom((building, stuff) => 6);

            Assert.That(palette.CostLine, Is.EqualTo("6 wood / tile"));
        }

        /// <summary>Without a cost table there is no cost line, rather than a zero.</summary>
        [Test]
        public void AnUnwiredPaletteQuotesNoPrice()
        {
            var (palette, _, _) = Open();
            palette.SelectSubType(PaletteTools.Wall);
            Assert.That(palette.CostLine, Is.Empty);
        }

        // ---------------------------------------------------------------- mode

        /// <summary>
        /// While a pinned action is held, the panel's header line says so in that action's own
        /// words instead of describing a build that is not about to happen (owner, 2026-09-17).
        /// </summary>
        [Test]
        public void HoldingAnActionMakesThePanelSayWhichModeItIsIn()
        {
            var (palette, _, _) = Open();
            palette.SelectSubType(PaletteTools.Wall);
            Assert.That(palette.HeaderLine, Is.EqualTo(palette.Breadcrumb));

            palette.TogglePinned(PaletteTools.Deconstruct);
            Assert.That(palette.ArmedPinned, Is.EqualTo(PaletteTools.Deconstruct));
            Assert.That(palette.HeaderLine, Is.EqualTo(Registry.Label(PaletteTools.Deconstruct)));

            palette.TogglePinned(PaletteTools.Deconstruct);
            Assert.That(palette.ArmedPinned, Is.Empty, "the action did not put down");
        }

        /// <summary>
        /// Every pinned action has a colour of its own, and no two share one — because the colour
        /// is what tells the player which mode they are in.
        /// </summary>
        [Test]
        public void EveryPinnedActionHasItsOwnColour()
        {
            var seen = new List<string>();
            foreach (string key in PaletteTools.Pinned)
            {
                HudColour? hue = HudTheme.PinnedActionHue(key);
                Assert.That(hue, Is.Not.Null, $"{key} is pinned and has no colour to announce itself in");
                Assert.That(seen, Does.Not.Contain(hue!.Value.Hex),
                    $"two pinned actions share the colour {hue.Value.Hex}, so the mode is ambiguous");
                seen.Add(hue.Value.Hex);
            }
        }

        /// <summary>Nothing else claims a mode colour, so the four that do are the four that
        /// mean it.</summary>
        [Test]
        public void OnlyAPinnedActionHasAModeColour()
        {
            foreach (var (key, tools) in PaletteTools.Categories)
            {
                Assert.That(HudTheme.PinnedActionHue(key), Is.Null, key);
                foreach (string tool in tools)
                    if (Array.IndexOf(PaletteTools.Pinned, tool) < 0)
                        Assert.That(HudTheme.PinnedActionHue(tool), Is.Null, tool);
            }
        }

        // ---------------------------------------------------------------- contrast

        /// <summary>
        /// Every label in the palette clears 4.5:1 against the button it is drawn on.
        ///
        /// <para><b>The two the specification names are the two that nearly fail</b>: Power's
        /// #e8d15c and Wood's #f2e3cb are the lightest colours in the panel, and a light ink on a
        /// light tint is how a palette like this usually loses a label. The category tiers are
        /// measured against their own washed fill over the panel over the brightest terrain the
        /// game can draw, which is the same worst case <c>HudContrast</c> uses everywhere else and
        /// is strictly harsher than any real board.</para>
        /// </summary>
        [Test]
        public void EveryLabelIsLegibleOnItsOwnButton()
        {
            for (int i = 0; i < HudTheme.BuildCategoryTiers.Length; i++)
            {
                HudTheme.BuildTier tier = HudTheme.BuildCategoryTiers[i];
                string name = Registry.Label(PaletteTools.Categories[i].key);

                HudColour restingBackground = HudContrast.Over(tier.Fill,
                    HudContrast.Over(HudTheme.PopoverFill, new HudColour(255, 255, 255)));
                Assert.That(HudContrast.Ratio(HudContrast.Over(tier.Ink, restingBackground), restingBackground),
                    Is.GreaterThanOrEqualTo(HudContrast.BodyMinimum), $"{name}, at rest");

                HudColour selectedBackground = HudContrast.Over(tier.SelectedFill,
                    HudContrast.Over(HudTheme.PopoverFill, new HudColour(255, 255, 255)));
                Assert.That(HudContrast.Ratio(HudContrast.Over(tier.Selected, selectedBackground), selectedBackground),
                    Is.GreaterThanOrEqualTo(HudContrast.BodyMinimum), $"{name}, selected");
            }
        }

        /// <summary>
        /// And every material label on its own tint. These are opaque light fills with dark ink,
        /// which is the one place in this HUD the usual direction is reversed.
        /// </summary>
        [Test]
        public void EveryMaterialLabelIsLegibleOnItsTint()
        {
            foreach (int stuff in new[]
                     { StuffHandle.Wood, StuffHandle.Stone, StuffHandle.Concrete, StuffHandle.Steel })
            {
                HudTheme.MaterialTint? tint = HudTheme.MaterialTintOf(stuff);
                Assert.That(tint, Is.Not.Null);

                Assert.That(HudContrast.Ratio(tint!.Value.Ink, tint.Value.Fill),
                    Is.GreaterThanOrEqualTo(HudContrast.BodyMinimum),
                    $"stuff {stuff}: the material's name is not legible on the material's own tint");
            }
        }

        /// <summary>
        /// A disabled sub-type is quiet, and still readable.
        ///
        /// <para><b>This test is the reason one token departs from the specification</b>, and it
        /// found the problem rather than being written around it: the specified 0.30 ink measures
        /// 2.71:1 against its own chip over the brightest terrain the game can draw. WCAG exempts
        /// inactive controls, so nothing external said that was wrong. What says it is wrong is
        /// this particular palette at this particular moment — one of its twenty-seven sub-types
        /// is live, so the disabled state is very nearly the whole panel and is the only thing
        /// telling a player what the game will eventually let them build. 0.35 measures 3.21:1.
        /// See <see cref="HudTheme.SubTypeDisabledInk"/> for when to put it back.</para>
        ///
        /// <para>The other half of the assertion is the half that stops the fix going too far: a
        /// disabled chip must stay clearly quieter than a live one, or the tier loses the only
        /// signal it has for "you cannot press this yet".</para>
        /// </summary>
        [Test]
        public void ADisabledSubTypeIsQuietAndStillReadable()
        {
            HudColour panel = HudContrast.Over(HudTheme.PopoverFill, new HudColour(255, 255, 255));

            HudColour offBackground = HudContrast.Over(HudTheme.SubTypeDisabledFill, panel);
            double off = HudContrast.Ratio(
                HudContrast.Over(HudTheme.SubTypeDisabledInk, offBackground), offBackground);

            HudColour onBackground = HudContrast.Over(HudTheme.SubTypeFill, panel);
            double on = HudContrast.Ratio(
                HudContrast.Over(HudTheme.SubTypeInk, onBackground), onBackground);

            Assert.That(off, Is.GreaterThanOrEqualTo(3.0),
                "the name of a thing the player cannot build yet has gone below 3:1");
            Assert.That(off, Is.LessThan(on * 0.5),
                "a disabled chip is no longer obviously quieter than a live one");
        }
    }
}
