#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Temperature;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Radiance: a heat source warms the cell it stands in and the ones beside it, on top of the
    /// room's air (docs/design/31-campfire-art-and-fire.md §15).
    ///
    /// <para><b>The thing design 28 refuses to do, done without doing it.</b> That model is one
    /// scalar per enclosed room and stores nothing per cell, which is the only reason it is
    /// affordable beside a 2.5 M cell board. Radiance is a pure function of distance to a source,
    /// so it needs no storage either — which is what makes a hot tile and a warm ring possible
    /// without abandoning the decision underneath the whole thermal model.</para>
    /// </summary>
    public class RadiantHeatTests
    {
        /// <summary>A fire's own cell is hotter than the room it stands in.</summary>
        [Test]
        public void TheFiresOwnCellIsHotterThanTheRoom()
        {
            var f = new TemperatureTests.Fixture(20, 20, 3);
            f.BuildRoom(2, 2, 8, 8, 0);
            f.BuildCampfire(5, 5);
            f.Passes(6);

            int fire = f.Temperature.CellTemp(f.Cell(5, 5, 0), f.World.CurrentTick);
            int air = f.Temperature.RoomTempC(f.Cell(5, 5, 0), f.World.CurrentTick);

            Assert.That(fire, Is.GreaterThan(air),
                "standing in the fire is no warmer than standing across the room from it");
            Assert.That(fire - air, Is.EqualTo(2_600).Within(1),
                "the fire's own cell should carry the whole of its radiantC");
        }

        /// <summary>
        /// It falls off with distance and stops.
        ///
        /// <para>The ring the owner asked for: hot in the middle, warm beside it, nothing a
        /// little further out. Asserted as an ordering rather than as four numbers, so retuning
        /// <c>radiantC</c> or the falloff does not falsify a test that is about the shape.</para>
        /// </summary>
        [Test]
        public void ItFallsOffWithDistanceAndStops()
        {
            var f = new TemperatureTests.Fixture(20, 20, 3);
            f.BuildRoom(2, 2, 12, 12, 0);
            f.BuildCampfire(5, 5);
            f.Passes(6);

            long tick = f.World.CurrentTick;
            int at0 = f.Temperature.CellTemp(f.Cell(5, 5, 0), tick);
            int at1 = f.Temperature.CellTemp(f.Cell(6, 5, 0), tick);
            int at2 = f.Temperature.CellTemp(f.Cell(7, 5, 0), tick);
            int at3 = f.Temperature.CellTemp(f.Cell(8, 5, 0), tick);
            int air = f.Temperature.RoomTempC(f.Cell(8, 5, 0), tick);

            Assert.That(at0, Is.GreaterThan(at1), "the fire's cell is not the hottest");
            Assert.That(at1, Is.GreaterThan(at2), "radiance does not fall with distance");
            Assert.That(at2, Is.GreaterThan(at3), "radiance does not fall with distance");
            Assert.That(at3, Is.EqualTo(air),
                $"radiance still reaches {TemperatureConductance.RadiantRangeCells + 1} cells out, " +
                "so it has no range at all");
        }

        /// <summary>
        /// The ring is <b>square</b>, so a diagonal neighbour is as warm as an orthogonal one.
        ///
        /// <para>Chebyshev rather than Euclidean, because the player is looking at a grid: a round
        /// falloff puts the diagonal neighbours in a different colour band from the orthogonal
        /// ones at the same apparent distance, which reads as a bug rather than as physics.</para>
        /// </summary>
        [Test]
        public void TheRingIsSquare()
        {
            var f = new TemperatureTests.Fixture(20, 20, 3);
            f.BuildRoom(2, 2, 10, 10, 0);
            f.BuildCampfire(6, 6);
            f.Passes(6);

            long tick = f.World.CurrentTick;
            int orthogonal = f.Temperature.CellTemp(f.Cell(7, 6, 0), tick);
            int diagonal = f.Temperature.CellTemp(f.Cell(7, 7, 0), tick);

            Assert.That(diagonal, Is.EqualTo(orthogonal),
                "the diagonal neighbour is a different temperature from the orthogonal one, so " +
                "the ring is round and the grid will show a cross");
        }

        /// <summary>
        /// A wall stops it.
        ///
        /// <para>One comparison rather than a ray: a source only reaches cells in its own room.
        /// A fire on the other side of a wall must not warm you through it, and a colonist who
        /// can feel next door's fire is the fault this guards.</para>
        /// </summary>
        [Test]
        public void AWallStopsIt()
        {
            var f = new TemperatureTests.Fixture(24, 20, 3);
            f.BuildRoom(2, 2, 6, 8, 0);
            f.BuildRoom(7, 2, 12, 8, 0);
            f.BuildCampfire(5, 5);
            f.Passes(6);

            long tick = f.World.CurrentTick;
            int beyond = f.Cell(8, 5, 0);

            Assert.That(f.Temperature.CellTemp(beyond, tick),
                Is.EqualTo(f.Temperature.RoomTempC(beyond, tick)),
                "a fire warms a cell in the next room by shining through the wall");
        }

        /// <summary>
        /// It does not reach through a floor.
        ///
        /// <para>A campfire is not underfloor heating. The rooms above and below already exchange
        /// through the slab, which is design 28's business; radiance staying on its own layer is
        /// what keeps the two from being counted twice.</para>
        /// </summary>
        [Test]
        public void ItDoesNotReachThroughAFloor()
        {
            var f = new TemperatureTests.Fixture(20, 20, 3);
            f.BuildRoom(2, 2, 8, 8, 0);
            f.BuildRoom(2, 2, 8, 8, 1);
            f.BuildCampfire(5, 5);
            f.Passes(6);

            long tick = f.World.CurrentTick;
            int above = f.Cell(5, 5, 1);

            Assert.That(f.Temperature.CellTemp(above, tick),
                Is.EqualTo(f.Temperature.RoomTempC(above, tick)),
                "the fire is warming the cell directly above it through the floor");
        }

        /// <summary>
        /// A board with no heat source on it pays nothing.
        ///
        /// <para>Every board in the game until somebody builds a campfire, which is what makes
        /// this the case worth pinning: <c>CellTemp</c> is asked per growing cell by the growth
        /// pass, over fields of two thousand.</para>
        /// </summary>
        [Test]
        public void ABoardWithNoFireReadsExactlyTheAir()
        {
            var f = new TemperatureTests.Fixture(20, 20, 3);
            f.BuildRoom(2, 2, 8, 8, 0);
            f.Passes(6);

            long tick = f.World.CurrentTick;
            for (int x = 3; x <= 7; x++)
            {
                int cell = f.Cell(x, 5, 0);
                Assert.That(f.Temperature.CellTemp(cell, tick),
                    Is.EqualTo(f.Temperature.RoomTempC(cell, tick)),
                    $"cell ({x}, 5) reads warmer than its room with nothing burning on the board");
            }
        }
    }
}
