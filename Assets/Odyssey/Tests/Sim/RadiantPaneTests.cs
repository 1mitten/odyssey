#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// What the inspect pane says about a tile near a fire — through the pane's own path, not
    /// through the thermal fixture (design 32 §4a).
    ///
    /// <para><b>Written because `RadiantHeatTests` passed while the game showed nothing.</b> Those
    /// tests ask <c>CellTemp</c> directly and were right about it. The pane does not: it answers
    /// the cell the player <i>clicked</i>, and a click on open ground lands on the <b>solid</b>
    /// cell it is drawn on while the air, the rooms and the heat sources all live in the cell
    /// above. Radiance does not cross layers, so every tile around a campfire was being asked
    /// about a different storey and quite correctly said nothing.</para>
    ///
    /// <para>Owner, 2026-09-23: *"surrounding tiles of campfire didn't seem to happen"*. The
    /// first guess was that the fire's own tile answered because it is clicked <i>on the fire</i>
    /// and only the neighbours failed. <b>Measured, that was wrong</b>: with the lift removed all
    /// three tiles — the fire's, the one beside it and one six cells away — read <b>1067</b>, the
    /// bare outdoor curve, identically. Every click resolves to the ground, so nothing was
    /// reading the air at all, indoors or out. The reported symptom was the visible half of a
    /// larger silence.</para>
    /// </summary>
    public class RadiantPaneTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 8);

        static ColonyWorld Board()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 0;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, 1u, scenario, barren: true, wooded: false);
        }

        static CellDetail Ask(ColonyWorld colony, CellRef cell)
        {
            colony.World.Intents.Submit(new Intent(IntentKind.QueryCell, cell));
            colony.World.Tick();
            Assert.That(colony.World.Views.Current.TryGetCellDetail(Size.Index(cell), out CellDetail detail),
                Is.True, "the world published no detail for the cell that was asked about");
            return detail;
        }

        /// <summary>
        /// Raise a campfire on the first standable cell of a column, and hand back the ground
        /// cell beneath it — which is what a click there resolves to.
        /// </summary>
        static (CellRef ground, CellRef air) Fire(ColonyWorld colony, int x, int z)
        {
            // Scanned rather than assumed: a campfire wants a CLEAR cell, and the scenario puts
            // a colonist and a starting kit somewhere on this board. Taking the first column that
            // will actually have one beats picking a coordinate and hoping.
            for (int step = 0; step < 64; step++)
            {
                int cx = x + step % 8;
                int cz = z + step / 8;
                if (cx >= Size.SizeX - 2 || cz >= Size.SizeZ - 2) continue;

                int air = colony.Grid.NearestWalkableInColumn(cx, cz, Size.SizeY - 2);
                if (air < 0) continue;

                CellRef at = Size.FromIndex(air);
                if (at.Y < 1) continue;
                if (colony.Construction.Place(at, BuildingHandle.Campfire, StuffHandle.Wood)
                    != IntentRejection.None) continue;

                colony.Construction.RaiseWhenClear(colony.Pawns, air, BuildingHandle.Campfire);
                colony.World.Tick();

                Assert.That(colony.Grid.Edifice[air], Is.GreaterThanOrEqualTo(0),
                    "the campfire was placed but never raised");

                return (new CellRef(at.X, at.Z, at.Y - 1), at);
            }

            Assert.Fail("no column on this board would take a campfire");
            return default;
        }

        /// <summary>
        /// The tiles around a fire read warmer than the ones further off — asked the way a player
        /// asks, by clicking the ground.
        /// </summary>
        [Test]
        public void TheGroundBesideAFireReadsWarmerThanGroundFurtherOff()
        {
            ColonyWorld colony = Board();
            (CellRef ground, _) = Fire(colony, 20, 20);

            // Run far enough for a thermal pass, which is what gathers the heat sources.
            colony.World.Tick(TemperatureSystemInterval);

            int beside = Ask(colony, new CellRef(ground.X + 1, ground.Z, ground.Y)).AmbientTempC;
            int away = Ask(colony, new CellRef(ground.X + 6, ground.Z, ground.Y)).AmbientTempC;

            Assert.That(beside, Is.Not.EqualTo(int.MinValue), "the pane said nothing at all");
            Assert.That(beside, Is.GreaterThan(away),
                "the ground one cell from a campfire is no warmer than ground six cells away, so " +
                "the pane is asking about a cell the fire cannot reach");
        }

        /// <summary>And the fire's own tile is the warmest of the three.</summary>
        [Test]
        public void TheFiresOwnTileIsTheWarmest()
        {
            ColonyWorld colony = Board();
            (CellRef ground, _) = Fire(colony, 20, 20);
            colony.World.Tick(TemperatureSystemInterval);

            int onIt = Ask(colony, ground).AmbientTempC;
            int beside = Ask(colony, new CellRef(ground.X + 1, ground.Z, ground.Y)).AmbientTempC;
            int away = Ask(colony, new CellRef(ground.X + 6, ground.Z, ground.Y)).AmbientTempC;

            Assert.That(onIt, Is.GreaterThan(beside), "the fire's own tile is not the hottest");
            Assert.That(beside, Is.GreaterThan(away), "the ring is not warmer than the field");
        }

        /// <summary>
        /// The fire's own tile clears the band the pane draws red at, which is the whole of the
        /// owner's ask about colour — <c>HudTheme.Temperature</c> reddens above 3,500.
        /// </summary>
        [Test]
        public void TheFiresOwnTileClearsTheRedBand()
        {
            ColonyWorld colony = Board();
            (CellRef ground, _) = Fire(colony, 20, 20);
            colony.World.Tick(TemperatureSystemInterval);

            Assert.That(Ask(colony, ground).AmbientTempC, Is.GreaterThan(3_500),
                "the fire's own tile does not reach the band the pane draws red at, so it will " +
                "never look dangerous however the colour table is read");
        }

        const int TemperatureSystemInterval =
            Odyssey.Sim.Temperature.TemperatureSystem.IntervalTicks * 2;
    }
}
