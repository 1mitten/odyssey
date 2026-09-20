#nullable enable
using System.Linq;
using System.Text;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// What the inspect pane says about a selected pile: its own name, not a placeholder, and the
    /// count in it.
    ///
    /// <para>The report behind it (owner, 2026-09-17): a wood pile read as a generic placeholder
    /// with no amount. The names come from <see cref="ItemLabels"/> and the count from
    /// <see cref="ThingView.Stack"/>, which the snapshot has carried all along — the pane simply
    /// never read either.</para>
    /// </summary>
    public class InspectItemTests
    {
        static readonly GridSize Size = new GridSize(10, 10, 4);

        static InspectModel LookingAt(WorldSnapshot frame, ThingId thing)
        {
            var model = new InspectModel();
            model.SetItem(thing);
            model.Refresh(frame);
            return model;
        }

        static WorldSnapshot FrameWithPile(int def, int stack, ThingId id = default)
        {
            var frame = new WorldSnapshot();
            frame.BeginWrite(tick: 0, Size, sliceLayer: 1);
            frame.AddThing(new ThingView(id.IsValid ? id : new ThingId(7),
                new CellRef(3, 4, 1), def, 0, stack));
            return frame;
        }

        [Test]
        public void AWoodPileIsWoodAndSaysHowManyAreInIt()
        {
            InspectModel model = LookingAt(FrameWithPile(ItemHandle.Wood, 27), new ThingId(7));

            // The count is in the headline, not under it. It lived on the state line until
            // 2026-09-19, where the owner read the pane and did not see it at all — so a pile
            // says its size where the eye lands, the way the growing pane says "Carrot × 5".
            Assert.That(model.Title, Is.EqualTo("Wood × 27"));
            Assert.That(model.Subtitle, Is.EqualTo("item"));
            Assert.That(model.Stack, Is.EqualTo(27));
            Assert.That(model.ItemIconKey, Is.EqualTo("ui.res.wood"),
                "a pile of wood drew the meal's icon before the pile had a name of its own");
        }

        [Test]
        public void SalvageIsScrapTheLedgersWordNotThePanesOldOne()
        {
            InspectModel model = LookingAt(FrameWithPile(ItemHandle.Salvage, 1), new ThingId(7));

            Assert.That(model.Title, Is.EqualTo("Scrap"));
        }

        [Test]
        public void AThingLyingOnItsOwnIsNotCountedAtAll()
        {
            // "Wood × 1" is a pile of one, which is a stilted way of saying "a log". The count
            // is only worth the width when there is a number to learn from it.
            InspectModel model = LookingAt(FrameWithPile(ItemHandle.Wood, 1), new ThingId(7));

            Assert.That(model.Title, Is.EqualTo("Wood"));
            Assert.That(model.Stack, Is.EqualTo(1));
        }

        [Test]
        public void AGoneItemKeepsThePaneOpenOnItsLastKnownValues()
        {
            var empty = new WorldSnapshot();
            empty.BeginWrite(tick: 0, Size, sliceLayer: 1);

            InspectModel model = LookingAt(empty, new ThingId(9));

            Assert.That(model.Subtitle, Is.EqualTo("item · no longer present"));
        }
    }

    /// <summary>
    /// What the pane says about a clicked cell once the world has answered the question: the
    /// tile's own name, and its facts as rows — one fact per line, label and value in fixed
    /// columns, in a fixed order.
    ///
    /// <para>Until this seam existed the pane's own words for a bare cell were "cell readout
    /// arrives with cell inspection" — a promise, shipped. These tests are it arriving (owner,
    /// 2026-09-17: rocks indistinguishable from grass, water silent about being water). The rows
    /// came the same day, from the same keyboard: a joined line made every number hunt for its
    /// label, so the tile window halved its width and stood its facts up in a column instead.</para>
    /// </summary>
    public class InspectCellTests
    {
        static readonly GridSize Size = new GridSize(10, 10, 4);

        static readonly CellRef At = new CellRef(3, 4, 1);

        static WorldSnapshot FrameWith(CellDetail detail, OrderView order = default)
        {
            var frame = new WorldSnapshot();
            frame.BeginWrite(tick: 0, Size, sliceLayer: 1);
            frame.AddCellDetail(detail);
            if (order.CellIndex != 0) frame.AddOrder(order);
            return frame;
        }

        static InspectModel Looking(WorldSnapshot frame)
        {
            var model = new InspectModel();
            model.SetCell(At);
            model.Refresh(frame);
            return model;
        }

        [Test]
        public void AZonedCellSaysWhatGrowsThereAndHowFarAlongItIs()
        {
            // The owner's ask (2026-09-18): clicking a growing zone should say what is growing
            // in it. The row names the crop and its ripeness; a painted-but-unseeded cell says
            // it is waiting rather than reading as 0% of nothing.
            InspectModel model = Looking(FrameWith(Detail(zonePlant: 0, cropGrowth: 430, zoneYield: 5)));
            Assert.That(Rows(model), Does.Contain("growing=" + Registry.Label(BuildLabels.PlantKey(0)) + " × 5 — 43% grown"),
                "a crop under way names itself, its plot's count and its ripeness");

            model = Looking(FrameWith(Detail(zonePlant: 0, cropGrowth: ushort.MaxValue)));
            Assert.That(Rows(model), Does.Contain("awaiting its seed"),
                "a fallow zone cell says it is waiting, not that nothing grows here");

            model = Looking(FrameWith(Detail()));
            Assert.That(Rows(model), Does.Not.Contain("growing="),
                "a cell in no zone says nothing about growing");
        }

        static CellDetail Detail(byte terrain = TerrainHandle.Grass, byte edifice = EdificeHandle.None,
            byte floorStuff = StuffHandle.None, byte support = 0,
            ushort moveCost = 1000, ushort workToClear = 0,
            byte quality = 0, int owner = 0, byte zonePlant = 255, ushort cropGrowth = ushort.MaxValue, byte zoneYield = 0) =>
            new CellDetail(Size.Index(At), terrain, edifice, floorStuff, support, moveCost, workToClear,
                quality, owner, zonePlant, cropGrowth, zoneYield);

        /// <summary>The rows as one readable line, in order: "walk speed=33%" and friends.</summary>
        static string Rows(InspectModel model)
        {
            var text = new StringBuilder();
            foreach (InspectRow row in model.CellRows)
            {
                if (text.Length > 0) text.Append(" | ");
                text.Append(row.Name).Append('=').Append(row.Value);
            }
            return text.ToString();
        }

        [Test]
        public void ShallowWaterIsNamedAndPricedAtAThird()
        {
            InspectModel model = Looking(FrameWith(
                Detail(terrain: TerrainHandle.ShallowWater, moveCost: 3000)));

            Assert.That(model.Title, Is.EqualTo("Shallow Water"));
            Assert.That(model.Subtitle, Is.EqualTo("cell"));
            Assert.That(Rows(model), Is.EqualTo("walk speed=33%"));
            Assert.That(model.CellIconKey, Is.EqualTo("ui.terrain.water.shallow"));
        }

        [Test]
        public void DeepWaterSaysItCannotBeCrossed()
        {
            InspectModel model = Looking(FrameWith(
                Detail(terrain: TerrainHandle.DeepWater, moveCost: 0)));

            Assert.That(model.Title, Is.EqualTo("Deep Water"));
            Assert.That(Rows(model), Is.EqualTo("walk speed=cannot walk"),
                "impassable is a different answer from slow, not an extreme of it");
        }

        [Test]
        public void MarshIsSlowGround()
        {
            InspectModel model = Looking(FrameWith(
                Detail(terrain: TerrainHandle.Marsh, moveCost: 1400)));

            Assert.That(model.Title, Is.EqualTo("Marsh"));
            Assert.That(Rows(model), Is.EqualTo("walk speed=71%"));
        }

        [Test]
        public void GrassAnswersWithFullSpeedAndItsOwnName()
        {
            InspectModel model = Looking(FrameWith(Detail()));

            Assert.That(model.Title, Is.EqualTo("Grass"));
            Assert.That(Rows(model), Is.EqualTo("walk speed=100%"));
        }

        /// <summary>
        /// The tree is what the click meant, so the tree is the title and the tile's facts follow
        /// as rows — the picker's own rule, read back.
        /// </summary>
        [Test]
        public void ATreeTitlesThePaneAndTheGroundAnswersBeneathIt()
        {
            InspectModel model = Looking(FrameWith(
                Detail(terrain: TerrainHandle.Air, edifice: EdificeHandle.TreeConifer)));

            Assert.That(model.Title, Is.EqualTo("Conifer"));
            Assert.That(Rows(model), Is.EqualTo("walk speed=100%"));
            Assert.That(model.CellIconKey, Is.EqualTo("ui.terrain.tree.conifer"));
        }

        /// <summary>
        /// A bed's own two facts, beside what it is (design 20 §8): how well it was made, and
        /// whose it is. The owner row is said even where nobody owns the bed yet, because it is
        /// the pane's one pickable fact — the actionable clause leads, the same rule a rock's
        /// minable clause follows.
        /// </summary>
        [Test]
        public void ABedSaysItsTierAndWhoseItIs()
        {
            WorldSnapshot frame = FrameWith(
                Detail(terrain: TerrainHandle.Air, edifice: EdificeHandle.Bed,
                    quality: QualityHandle.Decent, owner: 2));
            InspectModel model = Looking(frame);

            Assert.That(model.Title, Is.EqualTo("Bed"), "the bed's own icon key names it");
            Assert.That(Rows(model), Is.EqualTo(
                "quality=decent | owner=" + ColonistNames.Of(frame, new PawnId(2)) + " | walk speed=100%"));
            Assert.That(model.BedUnderPane, Is.True, "the shell arms the owner row on this word");
        }

        /// <summary>
        /// An unowned bed's owner row <b>names the action</b> rather than saying nothing with an
        /// em dash.
        ///
        /// <para>It was a dash, and the dash is why the feature could not be found: the row has
        /// been pickable since it was written and looked exactly like the facts above and below
        /// it, so the owner's report was that there was no way to assign a bed at all
        /// (2026-09-17). A row that is a control has to say so with the pointer still.</para>
        /// </summary>
        [Test]
        public void AnUnownedBedOffersItsOwnerRowAsAnAction()
        {
            InspectModel model = Looking(FrameWith(
                Detail(terrain: TerrainHandle.Air, edifice: EdificeHandle.Bed, quality: QualityHandle.Normal)));

            Assert.That(Rows(model), Is.EqualTo("quality=normal | owner=Assign… | walk speed=100%"));
            Assert.That(model.BedUnderPane, Is.True);
        }

        /// <summary>
        /// <b>The owner row stays pickable while the bed is held, not for one frame.</b>
        ///
        /// <para>This is the whole of the bug the owner reported three times across two sessions.
        /// <c>Refresh</c> clears <see cref="InspectModel.BedUnderPane"/> every time, and it used to
        /// be set inside the row rebuild — which <c>SetCellRows</c> skips whenever nothing about
        /// the cell has changed. So the flag was true on the refresh that built the rows and false
        /// on every refresh after it, while the row went on reading "Assign…" over a control the
        /// shell had already disarmed. Clicking it did nothing, for ever.</para>
        ///
        /// <para>A pane is refreshed many times a second and a player clicks a good deal later
        /// than that, so <b>the second refresh is the one that matters</b> and the first is the
        /// only one the old code got right. Refreshing twice here is not belt-and-braces; it is
        /// the test.</para>
        /// </summary>
        [Test]
        public void ABedStaysAssignableAfterTheRowsHaveSettled()
        {
            WorldSnapshot frame = FrameWith(
                Detail(terrain: TerrainHandle.Air, edifice: EdificeHandle.Bed,
                    quality: QualityHandle.Normal));
            InspectModel model = Looking(frame);

            Assert.That(model.BedUnderPane, Is.True, "the first refresh never armed the row");

            // Nothing has changed, so the rebuild is skipped — which is exactly when the flag used
            // to be lost.
            for (int i = 0; i < 5; i++) model.Refresh(frame);

            Assert.That(model.BedUnderPane, Is.True,
                "the owner row stopped being pickable while the same bed was still selected");
            Assert.That(Rows(model), Does.Contain("owner=Assign…"),
                "and it still says it is assignable, which is what made the fault invisible");
        }

        /// <summary>
        /// And the flag goes away when the bed does, so the affordance cannot outlive its subject.
        /// </summary>
        [Test]
        public void TheOwnerRowStopsBeingPickableWhenTheBedIsNoLongerHeld()
        {
            InspectModel model = Looking(FrameWith(
                Detail(terrain: TerrainHandle.Air, edifice: EdificeHandle.Bed,
                    quality: QualityHandle.Normal)));
            Assume.That(model.BedUnderPane, Is.True);

            model.Refresh(FrameWith(Detail(terrain: TerrainHandle.Grass)));

            Assert.That(model.BedUnderPane, Is.False, "a patch of grass is not a bed");
        }

        /// <summary>
        /// Every tier a player can be shown carries the colour <see cref="HudTheme.Quality"/>
        /// gives it, and Normal carries none.
        ///
        /// <para>The owner's specification is one colour per tier "anywhere quality is mentioned",
        /// so the tier's colour travels on the row rather than being chosen by whatever draws it —
        /// which is what makes a second surface naming a tier agree with this one for free.
        /// Normal is explicitly <b>no change</b>, which is not the same as the body colour: it
        /// means the surface keeps whatever it already had.</para>
        /// </summary>
        [Test]
        public void AQualityTierCarriesItsOwnColourAndNormalCarriesNone()
        {
            for (byte tier = QualityHandle.Poor; tier <= QualityHandle.Epic; tier++)
            {
                InspectModel model = Looking(FrameWith(
                    Detail(terrain: TerrainHandle.Air, edifice: EdificeHandle.Bed, quality: tier)));

                InspectRow row = model.CellRows[0];
                Assert.That(row.Name, Is.EqualTo("quality"));
                Assert.That(row.Tint?.Hex, Is.EqualTo(HudTheme.Quality(tier)?.Hex),
                    $"tier {tier} must be drawn in the one colour the theme gives it");
            }

            Assert.That(HudTheme.Quality(QualityHandle.Normal), Is.Null,
                "Normal is 'no change', so it names no colour for a surface to override with");
            Assert.That(HudTheme.Quality(QualityHandle.Poor), Is.Not.Null);
            Assert.That(HudTheme.Quality(QualityHandle.Epic), Is.Not.Null);
        }

        /// <summary>
        /// Every tier that names a colour is readable in it, on the darkest panel the game draws.
        /// A tier the player cannot read is worse than the uncoloured word it replaced.
        /// </summary>
        [Test]
        public void EveryQualityColourIsReadableOnAPanel()
        {
            HudColour panel = HudContrast.Over(
                HudTheme.PanelFill, HudContrast.Over(HudTheme.ScrimInk, new HudColour(255, 255, 255)));

            for (byte tier = QualityHandle.Poor; tier <= QualityHandle.Epic; tier++)
            {
                if (HudTheme.Quality(tier) is not HudColour ink) continue;
                Assert.That(HudContrast.Ratio(HudContrast.Over(ink, panel), panel),
                    Is.GreaterThanOrEqualTo(HudContrast.BodyMinimum),
                    $"{QualityLabels.Label(tier)} is not readable in its own colour");
            }
        }

        /// <summary>The control for the two above: no quality, no bed facts, no pickable row.</summary>
        [Test]
        public void AWallSaysNothingAboutQualityOrOwners()
        {
            InspectModel model = Looking(FrameWith(
                Detail(terrain: TerrainHandle.Air, edifice: EdificeHandle.Wall)));

            Assert.That(Rows(model), Is.EqualTo("walk speed=100%"));
            Assert.That(model.BedUnderPane, Is.False,
                "the pickable affordance cannot outlive the bed it described");
        }

        /// <summary>
        /// A rock's first row is the actionable one — the work of it — at the same honest estimate
        /// a site gives, because "how long is this rock" is why anyone clicks one.
        /// </summary>
        [Test]
        public void ARockLeadsWithTheWorkOfIt()
        {
            InspectModel model = Looking(FrameWith(
                Detail(terrain: TerrainHandle.Rock, support: 4, moveCost: 1000, workToClear: 700)));

            Assert.That(model.Title, Is.EqualTo("Rock"));
            Assert.That(Rows(model), Is.EqualTo(
                "minable=about 12s of work | walk speed=100% | support=4"));
        }

        [Test]
        public void AHalfCutFaceLeadsWithTheOrderStandingOnIt()
        {
            // 115 of 255 is 45 per cent, cut and banked on the designation.
            InspectModel model = Looking(FrameWith(
                Detail(terrain: TerrainHandle.Rock, support: 4, workToClear: 700),
                new OrderView(Size.Index(At), 1, 115)));

            Assert.That(Rows(model), Is.EqualTo(
                "mining=45% done | walk speed=100% | support=4"),
                "the order is the actionable row, so it leads and the estimate stands down");
        }

        [Test]
        public void ABuiltFloorIsTitledByItsMaterial()
        {
            InspectModel model = Looking(FrameWith(
                Detail(terrain: TerrainHandle.Air, floorStuff: StuffHandle.Wood)));

            Assert.That(model.Title, Is.EqualTo("Wood floor"));
            Assert.That(Rows(model), Is.EqualTo("walk speed=100%"),
                "the floor is the title; the rows do not say it twice");
        }

        [Test]
        public void NoAnswerYetSaysGroundRatherThanInventingOne()
        {
            var frame = new WorldSnapshot();
            frame.BeginWrite(tick: 0, Size, sliceLayer: 1);

            InspectModel model = Looking(frame);

            Assert.That(model.Title, Is.EqualTo("Ground"));
            Assert.That(model.Subtitle, Is.EqualTo("cell"));
            Assert.That(model.CellRows, Is.Empty,
                "the answer is one publish behind the click; nothing is better than a guess");
        }

        [Test]
        public void AWithdrawnAnswerReturnsThePaneToGround()
        {
            var model = new InspectModel();
            model.SetCell(At);
            model.Refresh(FrameWith(Detail(terrain: TerrainHandle.Rock, workToClear: 700)));
            Assert.That(model.Title, Is.EqualTo("Rock"));

            var answered = new WorldSnapshot();
            answered.BeginWrite(tick: 0, Size, sliceLayer: 1);
            model.Refresh(answered);

            Assert.That(model.Title, Is.EqualTo("Ground"));
            Assert.That(model.CellRows, Is.Empty);
        }

        /// <summary>
        /// The rows are the same instances until they would read differently, which is what lets
        /// the view compare by content cheaply and what keeps the pane from allocating fifteen
        /// times a second (ADR 0003, flip condition F1) — the same promise the site line makes.
        /// </summary>
        [Test]
        public void TheRowsAreNotRebuiltWhileTheyWouldReadTheSame()
        {
            var model = new InspectModel();
            model.SetCell(At);

            model.Refresh(FrameWith(Detail(terrain: TerrainHandle.Rock, support: 4, workToClear: 700)));
            string first = Rows(model);
            var values = model.CellRows.Select(row => row.Value).ToArray();

            model.Refresh(FrameWith(Detail(terrain: TerrainHandle.Rock, support: 4, workToClear: 700)));
            Assert.That(Rows(model), Is.EqualTo(first),
                "the pane rebuilt rows that read identically");
            for (int i = 0; i < values.Length; i++)
                Assert.That(ReferenceEquals(model.CellRows[i].Value, values[i]), Is.True,
                    $"row {i} was rebuilt although it reads the same");
        }

        /// <summary>
        /// A site leads over the tile it stands on, exactly as before the readout existed: the
        /// thing about to be built is more than the ground under it.
        /// </summary>
        [Test]
        public void ASiteLeadsOverTheTileUnderIt()
        {
            var frame = new WorldSnapshot();
            frame.BeginWrite(tick: 0, Size, sliceLayer: 1);
            frame.AddCellDetail(Detail(terrain: TerrainHandle.Grass));
            frame.AddSite(new SiteView(
                Size.Index(At), BuildingHandle.Wall, StuffHandle.Wood, 2, 5, 0, 135));

            InspectModel model = Looking(frame);

            Assert.That(model.Title, Is.EqualTo("Wall"));
            Assert.That(model.Site, Is.EqualTo("2 of 5 wood delivered"));
            Assert.That(model.CellRows, Is.Empty, "the site is the whole answer while it stands");
        }
    }
}
