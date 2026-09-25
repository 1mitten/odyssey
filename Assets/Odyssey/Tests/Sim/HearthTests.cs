#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using static Odyssey.Tests.Sim.HomeFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Design 43 §3f: the hearth is the one campfire home is centred on. The first raised while
    /// there is none takes the title; the player moves it with <c>SetHearth</c>; taking it down
    /// leaves none, and nothing is promoted in its place.
    /// </summary>
    public class HearthTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ColonyWorld Board()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 0;
            scenario.stockpileCells = 0;
            return ColonyWorld.Build(Size, 7u, scenario, barren: true, wooded: false);
        }

        static Hearth Hearth(ColonyWorld colony) => colony.Pawns.Hearth!;

        static CellRef Near(ColonyWorld colony, int dx, int dz)
        {
            CellRef stand = Size.FromIndex(colony.Pawns.Pawns.All[0].Cell);
            return new CellRef(stand.X + dx, stand.Z + dz, stand.Y);
        }

        static IntentRejection SetHearth(ColonyWorld colony, int cell) =>
            CombatFixture.Send(colony, new Intent(IntentKind.SetHearth, Size.FromIndex(cell)));

        [Test]
        public void TheFirstCampfireRaisedBecomesTheHearthAndASecondDoesNot()
        {
            var colony = Board();
            Assert.That(Hearth(colony).Exists, Is.False);

            int first = Campfire(colony, Near(colony, 6, 0));
            Assert.That(Hearth(colony).Cell, Is.EqualTo(first));

            Campfire(colony, Near(colony, -6, 0));
            Assert.That(Hearth(colony).Cell, Is.EqualTo(first), "a second campfire took the title");
        }

        [Test]
        public void AWallIsNeverTheHearth()
        {
            var colony = Board();
            Raise(colony, Near(colony, 6, 0), BuildingHandle.Wall);
            Assert.That(Hearth(colony).Exists, Is.False);
        }

        [Test]
        public void SetHearthMovesItAndRefusesWhatIsNotACampfireOfOurs()
        {
            var colony = Board();
            int first = Campfire(colony, Near(colony, 6, 0));
            int second = Campfire(colony, Near(colony, -20, 0));
            int wall = Raise(colony, Near(colony, 0, 6), BuildingHandle.Wall);
            Assume.That(colony.Pawns.Home!.Contains(second), Is.False, "the far campfire is already home");

            Assert.That(SetHearth(colony, first), Is.EqualTo(IntentRejection.AlreadyInThatState));
            Assert.That(SetHearth(colony, wall), Is.EqualTo(IntentRejection.NotPermitted), "a wall became the hearth");
            Assert.That(SetHearth(colony, Size.Index(Near(colony, 0, -6))), Is.EqualTo(IntentRejection.NotPermitted),
                "open ground became the hearth");

            Assert.That(SetHearth(colony, second), Is.EqualTo(IntentRejection.None));
            Assert.That(Hearth(colony).Cell, Is.EqualTo(second));
            Assert.That(colony.Pawns.Home!.Contains(second), Is.True, "home did not move with the hearth");
            Assert.That(colony.Pawns.Home.Contains(first), Is.False, "the old hearth kept its home");
        }

        [Test]
        public void ARuinsCampfireCannotBeTheHearth()
        {
            var colony = Board();
            int cell = Size.Index(Near(colony, 6, 0));
            var records = colony.Construction.Edifices.Records;
            records.Add(new PlacedEdifice { CellIndex = cell, Def = CoreContent.EdificeCampfire, Built = false });
            colony.Pawns.Cells.Edifice[cell] = records.Count - 1;

            Assert.That(SetHearth(colony, cell), Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(Hearth(colony).Exists, Is.False);
        }

        [Test]
        public void TakingTheHearthDownLeavesNoneAndTheNextCampfireRaisedBecomesIt()
        {
            var colony = Board();
            int first = Campfire(colony, Near(colony, 6, 0));
            int standing = Campfire(colony, Near(colony, -6, 0));

            Assert.That(colony.Construction.Demolish(colony.Pawns, first, out _), Is.True);
            Assert.That(Hearth(colony).Exists, Is.False, "the hearth outlived its campfire");
            Assert.That(colony.Pawns.Home!.IsEmpty, Is.True);
            Assert.That(Hearth(colony).Cell, Is.Not.EqualTo(standing), "a standing campfire was promoted");

            int next = Campfire(colony, Near(colony, 0, 8));
            Assert.That(Hearth(colony).Cell, Is.EqualTo(next));
        }

        [Test]
        public void ItIsPublished()
        {
            var colony = Board();
            colony.World.Tick();
            Assert.That(colony.World.Views.Current.HearthCell, Is.EqualTo(-1));

            int fire = Campfire(colony, Near(colony, 6, 0));
            colony.World.Tick();
            Assert.That(colony.World.Views.Current.HearthCell, Is.EqualTo(fire));
        }

        [Test]
        public void ItIsSavedAndHashedAndTheWorldsStayTogether()
        {
            var colony = Board();
            int first = Campfire(colony, Near(colony, 6, 0));
            int second = Campfire(colony, Near(colony, -6, 0));
            ulong atFirst = colony.World.ComputeStateHash().Value;

            Assume.That(SetHearth(colony, second), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.World.ComputeStateHash().Value, Is.Not.EqualTo(atFirst), "the hash cannot see the hearth");

            var restored = Board();
            restored.Load(colony.Save());
            Assert.That(Hearth(restored).Cell, Is.EqualTo(second));
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value));

            colony.World.Tick(300);
            restored.World.Tick(300);
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value),
                "the worlds parted after the load");
            Assert.That(first, Is.Not.EqualTo(second));
        }
    }
}
