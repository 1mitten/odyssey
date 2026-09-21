#nullable enable
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Saving;
using Odyssey.Sim.Temperature;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Review probes for PR #164 (design 28 §12). Each test states a property the design claims
    /// and checks whether the code has it. <b>Every probe here fails on the branch as reviewed
    /// on 2026-09-21</b>, which is why the fixture is explicit: a failing probe is an open
    /// finding, and the fix for one turns its probe into an ordinary test by moving it out of
    /// this file. Run them with
    /// <c>scripts/test-fast.sh --filter FullyQualifiedName~TemperatureReviewProbes</c>.
    /// </summary>
    [TestFixture, Explicit, Category("Review")]
    public class TemperatureReviewProbes
    {
        sealed class Fixture
        {
            public readonly GridSize Size;
            public readonly CellGrid Cells;
            public readonly List<PlacedEdifice> Edifices;
            public readonly EnclosureGrid Enclosure;
            public readonly PawnContext Ctx;
            public readonly TemperatureSystem Temperature;
            public readonly SimWorld World;
            public readonly ClimateDef Climate;

            public Fixture(int sx = 20, int sz = 20, int sy = 4, ClimateDef? climate = null)
            {
                Size = new GridSize(sx, sz, sy);
                Cells = new CellGrid(Size);
                Edifices = new List<PlacedEdifice>();
                for (int x = 0; x < sx; x++)
                for (int z = 0; z < sz; z++)
                    Cells.Floor[Size.Index(x, z, 0)] = CoreContent.SlabBuilt;

                var nav = new NavGraph(Cells);
                Ctx = new PawnContext(Cells, nav, new PathService(new PathFinder(nav)), ContentPack.Pawns())
                {
                    Enclosure = Enclosure = new EnclosureGrid(Cells, Edifices),
                };
                Climate = climate ?? Still(-2_000);
                Ctx.Temperature = Temperature = new TemperatureSystem(Ctx, Edifices, Climate);
                World = new SimWorldBuilder().WithSeed(1).WithSize(Size)
                    .AddSystem(_ => Enclosure)
                    .AddSystem(_ => Temperature)
                    .Build();
            }

            public static ClimateDef Still(int meanC)
            {
                var climate = new ClimateDef { annualMeanC = meanC, dailyAmplitudeC = 0 };
                for (int m = 0; m < climate.monthlyOffsetC.Count; m++) climate.monthlyOffsetC[m] = 0;
                return climate;
            }

            public int Cell(int x, int z, int y = 0) => Size.Index(x, z, y);

            public void Pass()
            {
                int target = ((World.CurrentTick / TemperatureSystem.IntervalTicks) + 1)
                             * TemperatureSystem.IntervalTicks;
                while (World.CurrentTick < target) World.Tick();
            }

            public void Passes(int count) { for (int i = 0; i < count; i++) Pass(); }

            public int Temp(int x, int z, int y = 0) => Temperature.CellTemp(Cell(x, z, y), World.CurrentTick);

            public void BuildWall(int x, int z, int y = 0)
            {
                int c = Cell(x, z, y);
                Edifices.Add(new PlacedEdifice { CellIndex = c, Def = CoreContent.EdificeWall, Stuff = CoreContent.StuffConcrete });
                Cells.Edifice[c] = Edifices.Count - 1;
                Cells.Flags[c] |= CellFlags.BlockingEdifice;
                Enclosure.MarkDirty(c);
            }

            public void RemoveWall(int x, int z, int y = 0)
            {
                int c = Cell(x, z, y);
                int handle = Cells.Edifice[c];
                var placed = Edifices[handle];
                placed.Removed = true;
                Edifices[handle] = placed;
                Cells.Edifice[c] = -1;
                Cells.Flags[c] &= ~CellFlags.BlockingEdifice;
                Enclosure.MarkDirty(c);
            }

            public void BuildSlab(int x, int z, int y)
            {
                int c = Cell(x, z, y);
                Cells.Floor[c] = CoreContent.SlabBuilt;
                Enclosure.MarkDirty(c);
            }

            public void RemoveSlab(int x, int z, int y)
            {
                int c = Cell(x, z, y);
                Cells.Floor[c] = CoreContent.SlabNone;
                Enclosure.MarkDirty(c);
            }

            public void BuildCampfire(int x, int z, int y = 0)
            {
                int c = Cell(x, z, y);
                Edifices.Add(new PlacedEdifice { CellIndex = c, Def = CoreContent.EdificeCampfire, Stuff = NaturalContent.StuffWood });
                Cells.Edifice[c] = Edifices.Count - 1;
            }

            /// <summary>Walls on the rectangle's edge, slabs above the interior (the roof), and
            /// — unless told otherwise — slabs under the interior on layers above 0.</summary>
            public int BuildRoom(int x0, int z0, int x1, int z1, int y, bool floor = true, bool roof = true)
            {
                for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    bool edge = x == x0 || x == x1 || z == z0 || z == z1;
                    if (edge) BuildWall(x, z, y);
                    else if (floor && y > 0) BuildSlab(x, z, y);
                }
                if (roof)
                    for (int x = x0 + 1; x < x1; x++)
                    for (int z = z0 + 1; z < z1; z++)
                        BuildSlab(x, z, y + 1);
                return Enclosure.RoomAt(Cell(x0 + 1, z0 + 1, y));
            }

            public ThermalRoom Room(int key, int y)
            {
                foreach (var r in Enclosure.RoomsOn(y)) if (r.Key == key) return r;
                throw new System.InvalidOperationException("no room " + key + " on layer " + y);
            }
        }

        // ---- (a) the shared slab is charged to the sky as well ------------------------------------

        [Test]
        public void ProbeA_ACellarUnderALoftIsNotChargedForTheSkyThroughTheLoftsFloor()
        {
            var f = new Fixture(sy: 4);
            int cellar = f.BuildRoom(2, 2, 7, 7, y: 0, floor: false);       // roof = the loft's floor
            int loft = f.BuildRoom(2, 2, 7, 7, y: 1, floor: false);          // its own roof at y=2
            Assert.That(cellar, Is.GreaterThan(0));
            Assert.That(loft, Is.GreaterThan(0));

            ThermalRoom lower = f.Room(cellar, 0);
            ThermalRoom upper = f.Room(loft, 1);
            int slabLink = 0;
            foreach (var l in upper.SlabLinks) if (l.Other == cellar) slabLink = l.PerMille;

            TestContext.WriteLine($"cellar: CeilingSkyCells={lower.CeilingSkyCells} CeilingRockCells={lower.CeilingRockCells} " +
                                  $"| loft SlabLink to cellar = {slabLink} per mille");
            Assert.That(slabLink, Is.EqualTo(16 * TemperatureConductance.SlabPerMille),
                "the loft records the shared slab as a slab link");
            Assert.That(lower.CeilingSkyCells, Is.EqualTo(0),
                "a ceiling that is another room's floor is not open sky");
        }

        [Test]
        public void ProbeA2_ABuildingAboveNeverMakesTheRoomBelowColder()
        {
            int Equilibrium(bool loftAbove)
            {
                var f = new Fixture(sy: 4);
                f.BuildRoom(2, 2, 7, 7, y: 0, floor: false);
                if (loftAbove) f.BuildRoom(2, 2, 7, 7, y: 1, floor: false);
                f.BuildCampfire(4, 4, y: 0);
                f.Passes(600);
                return f.Temp(4, 5, 0);
            }
            int alone = Equilibrium(false);
            int withLoft = Equilibrium(true);
            TestContext.WriteLine($"fired cellar alone = {alone / 100.0:F1} C, under a sealed loft = {withLoft / 100.0:F1} C, outdoors -20 C");
            Assert.That(withLoft, Is.GreaterThanOrEqualTo(alone),
                "a sealed room above should insulate the room below, never chill it");
        }

        // ---- (b) merging rooms ----------------------------------------------------------------

        [Test]
        public void ProbeB_KnockingThroughFromAWarmCupboardIntoAColdHallMixesByArea()
        {
            var f = new Fixture(sx: 30, sz: 20, sy: 3);
            int cupboard = f.BuildRoom(2, 2, 7, 7, y: 0);      // 4x4 interior, the lower key
            int hall = f.BuildRoom(7, 2, 20, 12, y: 0);        // shares the x=7 wall; 12x9 interior
            Assert.That(cupboard, Is.LessThan(hall));
            f.BuildCampfire(4, 4, y: 0);
            f.Passes(600);
            int warm = f.Temp(4, 5, 0);
            int cold = f.Temp(15, 6, 0);
            Assert.That(warm - cold, Is.GreaterThan(1_500), "the cupboard is warm and the hall is cold to begin with");

            for (int z = 3; z <= 6; z++) f.BuildSlab(7, z, 1);   // roof over the gap, or the join is sky
            for (int z = 3; z <= 6; z++) f.RemoveWall(7, z, 0);
            f.Pass();
            Assert.That(f.Enclosure.RoomAt(f.Cell(15, 6, 0)), Is.EqualTo(cupboard), "one room now, under the cupboard's key");
            int merged = f.Temp(15, 6, 0);
            int expectedMix = (warm * 16 + cold * 108) / 124;
            TestContext.WriteLine($"cupboard {warm / 100.0:F1} C, hall {cold / 100.0:F1} C; after knocking through the hall reads {merged / 100.0:F1} C; area mix would be about {expectedMix / 100.0:F1} C");
            Assert.That(merged, Is.LessThan(warm - 500),
                "a hall of 108 cold cells does not become as warm as the 16-cell cupboard it was joined to");
        }

        // ---- (c) a room that dissolves and comes back ----------------------------------------------

        [Test]
        public void ProbeC_ARoomOpenedToTheSkyForADayComesBackCold()
        {
            var f = new Fixture();
            int key = f.BuildRoom(2, 2, 7, 7, y: 0);
            f.BuildCampfire(4, 4, y: 0);
            f.Passes(600);
            int warm = f.Temp(4, 5, 0);
            Assert.That(warm, Is.GreaterThan(0));

            f.RemoveSlab(3, 3, 1);         // a hole in the roof: the room is gone, its cells outdoors
            f.Passes(500);                 // a game day open to a -20 C sky
            Assert.That(f.Enclosure.RoomAt(f.Cell(4, 5, 0)), Is.EqualTo(0), "the room dissolved");
            Assert.That(f.Temp(4, 5, 0), Is.EqualTo(-2_000), "its cells read the outdoors meanwhile");

            f.BuildSlab(3, 3, 1);
            f.Pass();
            Assert.That(f.Enclosure.RoomAt(f.Cell(4, 5, 0)), Is.EqualTo(key), "same cells, same key");
            int back = f.Temp(4, 5, 0);
            TestContext.WriteLine($"was {warm / 100.0:F1} C, a day open to -20 C, re-sealed reads {back / 100.0:F1} C");
            Assert.That(back, Is.LessThan(warm - 1_000),
                "a room that stood open to a -20 C sky for a day does not come back at its old temperature");
        }

        [Test]
        public void ProbeC2_ThatResurrectionDivergesAcrossASave()
        {
            Fixture Build()
            {
                var f = new Fixture();
                f.BuildRoom(2, 2, 7, 7, y: 0);
                f.BuildCampfire(4, 4, y: 0);
                return f;
            }
            var played = Build();
            played.Passes(600);
            played.RemoveWall(4, 2, 0);
            played.Passes(10);

            using var buffer = new MemoryStream();
            WorldSave.Save(played.World, buffer, new ISaveable[] { played.Temperature });
            buffer.Position = 0;
            var loaded = Build();
            loaded.RemoveWall(4, 2, 0);
            WorldSave.Load(loaded.World, buffer, new ISaveable[] { loaded.Temperature });

            played.BuildWall(4, 2, 0);
            loaded.BuildWall(4, 2, 0);
            played.Pass();
            loaded.Pass();
            int a = played.Temp(4, 5, 0), b = loaded.Temp(4, 5, 0);
            TestContext.WriteLine($"re-sealed after the same history: played {a / 100.0:F1} C, loaded {b / 100.0:F1} C");
            Assert.That(b, Is.EqualTo(a), "a played world and a loaded one must agree on a re-sealed room");
        }

        // ---- (d) the fixed-point sweep runs only over the layers the edit marked -----------------------

        [Test]
        public void ProbeD_RoofingTheGroundFloorLastEnclosesTheCellarUnderIt()
        {
            var f = new Fixture(sy: 5);
            // The cellar: walls at y=0, no roof of its own; the ground floor's slabs are its roof
            // but one is left out for the stair.
            f.BuildRoom(2, 2, 7, 7, y: 0, floor: false, roof: false);
            f.BuildRoom(2, 2, 7, 7, y: 1, floor: true, roof: false);   // floor slabs at y=1
            f.RemoveSlab(5, 5, 1);                                     // the stairwell
            Assert.That(f.Enclosure.RoomAt(f.Cell(4, 4, 1)), Is.EqualTo(0), "no roof yet: nothing is enclosed");
            Assert.That(f.Enclosure.RoomAt(f.Cell(4, 4, 0)), Is.EqualTo(0));

            // Now the roof, last — the natural order of building a house.
            for (int x = 3; x <= 6; x++)
            for (int z = 3; z <= 6; z++)
                f.BuildSlab(x, z, 2);
            f.World.Tick();

            int groundFloor = f.Enclosure.RoomAt(f.Cell(4, 4, 1));
            int cellar = f.Enclosure.RoomAt(f.Cell(4, 4, 0));

            // The same board, solved from scratch — which is what a load does.
            var fresh = new EnclosureGrid(f.Cells, f.Edifices);
            int cellarFresh = fresh.RoomAt(f.Cell(4, 4, 0));
            TestContext.WriteLine($"played: ground floor room {groundFloor}, cellar room {cellar}; a fresh solve of the same board says the cellar is room {cellarFresh}");
            Assert.That(groundFloor, Is.GreaterThan(0), "the ground floor is enclosed once roofed");
            Assert.That(cellar, Is.EqualTo(cellarFresh),
                "the played world and a fresh solve of the same board must agree about whether the cellar is a room");
        }

        // ---- (e) body heat truncates to nothing in an ordinary room -------------------------------------

        [Test]
        public void ProbeE_OneColonistInAFourByFourRoomWarmsItAtAll()
        {
            var f = new Fixture(climate: Fixture.Still(-1_000));
            f.BuildRoom(2, 2, 7, 7, y: 0);                 // 16 cells of air
            f.Ctx.Pawns.Spawn(f.Cell(4, 4, 0));
            int before = f.Temp(3, 3, 0);
            f.Passes(200);
            int after = f.Temp(3, 3, 0);
            int perPawn = f.Ctx.Content.Temperature.bodyHeatPerPass;
            TestContext.WriteLine($"body heat {perPawn} per pass over 16 cells = {perPawn / 16} per pass after integer division; room went {before} -> {after}");
            Assert.That(after, Is.GreaterThan(before), "one person in a small sealed room warms it a little");
        }

        // ---- (f) how fast the severity bar fills ------------------------------------------------------

        [Test]
        public void ProbeF_ACandleNightFillsTheSeverityBarInAboutFourHours()
        {
            var tuning = ContentPack.Pawns().Temperature;
            int perInterval = -tuning.SeverityDelta(-1_300);           // a Candle night, -13 C
            int intervals = (1_000 + perInterval - 1) / perInterval;
            long ticks = (long)intervals * ContentPack.Pawns().NeedsIntervalTicks;
            TestContext.WriteLine($"at -13 C the bar gains {perInterval} an interval: full in {intervals} intervals = {ticks} ticks = {ticks / (double)Calendar.TicksPerHour:F2} game hours (Temperature.xml says about four)");
            Assert.That(ticks, Is.GreaterThan(2L * Calendar.TicksPerHour),
                "the XML promises hours of exposure before the bar is full, not minutes");
        }

        // ---- (g) the cached ambient does not ride the save ---------------------------------------------

        [Test]
        public void ProbeG_AColonistsAmbientSurvivesASave()
        {
            var f = new Fixture();
            var pawn = f.Ctx.Pawns.Spawn(f.Cell(10, 10, 0));
            pawn.AmbientTempC = -2_000;
            Assert.That(pawn.WorkRatePerMille(WorkTypeIndex.Haul), Is.EqualTo(700), "cold work is slow before the save");

            using var buffer = new MemoryStream();
            WorldSave.Save(f.World, buffer, new ISaveable[] { f.Ctx.Pawns });
            buffer.Position = 0;
            var g = new Fixture();
            WorldSave.Load(g.World, buffer, new ISaveable[] { g.Ctx.Pawns });
            var back = g.Ctx.Pawns.All[0];
            TestContext.WriteLine($"work rate before save {pawn.WorkRatePerMille(WorkTypeIndex.Haul)}, after load {back.WorkRatePerMille(WorkTypeIndex.Haul)} until the next needs interval");
            Assert.That(back.WorkRatePerMille(WorkTypeIndex.Haul), Is.EqualTo(700),
                "the work rate a loaded colonist runs at must be the one she was saved at");
        }
    }
}
