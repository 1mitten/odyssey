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

            Assert.That(model.Title, Is.EqualTo("Wood"));
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

        static CellDetail Detail(byte terrain = TerrainHandle.Grass, byte edifice = EdificeHandle.None,
            byte floorStuff = StuffHandle.None, byte support = 0,
            ushort moveCost = 1000, ushort workToClear = 0) =>
            new CellDetail(Size.Index(At), terrain, edifice, floorStuff, support, moveCost, workToClear);

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
