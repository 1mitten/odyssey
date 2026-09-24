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
    /// The 2026-09-21 review of the thermal model (design 28 §12), one test per finding. Each
    /// began as an explicit probe that failed on the branch as built; the fix for the finding
    /// is what turned it into a test. The names say what the game must do, not what it did.
    /// </summary>
    [TestFixture]
    public class TemperatureRegressionTests
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

        // ---- F6: the shared slab ---------------------------------------------------------------

        [Test]
        public void ACellarUnderALoftIsNotChargedForTheSkyThroughTheLoftsFloor()
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

            Assert.That(slabLink, Is.EqualTo(16 * TemperatureConductance.SlabPerMille),
                "the loft records the shared slab as a slab link");
            Assert.That(lower.CeilingSkyCells, Is.EqualTo(0),
                "a ceiling that is another room's floor is not open sky");
        }

        [Test]
        public void ABuildingAboveNeverMakesTheRoomBelowColder()
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
            Assert.That(withLoft, Is.GreaterThanOrEqualTo(alone),
                $"a sealed room above insulates the room below, never chills it: alone {alone}, under a loft {withLoft}");
        }

        [Test]
        public void ARoomsSurfacesFollowTheLayerAboveWithoutARefill()
        {
            // The two-phase solve: the cellar's ceiling is classified against the loft's room
            // table, so when the loft is built after the cellar the cellar's surfaces must move
            // from sky to shared slab even though nothing on the cellar's own layer changed.
            var f = new Fixture(sy: 4);
            int cellar = f.BuildRoom(2, 2, 7, 7, y: 0, floor: false);
            Assert.That(f.Room(cellar, 0).CeilingSkyCells, Is.EqualTo(16), "alone, the roof is against the sky");

            f.BuildRoom(2, 2, 7, 7, y: 1, floor: false);
            Assert.That(f.Room(cellar, 0).CeilingSkyCells, Is.EqualTo(0),
                "once the loft stands, the cellar's roof is the loft's floor");
        }

        // ---- F5: merging --------------------------------------------------------------------------

        [Test]
        public void KnockingThroughFromAWarmCupboardIntoAColdHallMixesByArea()
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
            Assert.That(merged, Is.LessThan(warm - 500),
                $"a hall of 108 cold cells does not become as warm as the 16-cell cupboard: merged {merged}, mix about {expectedMix}");
            Assert.That(merged, Is.GreaterThan(cold + 500), "nor does the cupboard's heat vanish");
        }

        [Test]
        public void ARoomThatComesThroughASolveUnchangedKeepsItsTemperatureToTheUnit()
        {
            var f = new Fixture();
            f.BuildRoom(2, 2, 7, 7, y: 0);
            f.BuildCampfire(4, 4, y: 0);
            f.Passes(300);
            int before = f.Temp(4, 5, 0);
            f.BuildWall(15, 15, 0);                // an edit elsewhere on the layer: a re-fill
            Assert.That(f.Temp(4, 5, 0), Is.EqualTo(before));
        }

        // ---- F4: resurrection ---------------------------------------------------------------------

        [Test]
        public void ARoomOpenedToTheSkyForADayComesBackCold()
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
            Assert.That(f.Temp(4, 5, 0), Is.LessThan(warm - 1_000),
                "a room that stood open to a -20 C sky for a day does not come back at its old temperature");
        }

        [Test]
        public void AResurrectedRoomReadsTheSameOnBothSidesOfASave()
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
            Assert.That(loaded.Temp(4, 5, 0), Is.EqualTo(played.Temp(4, 5, 0)),
                "a played world and a loaded one must agree on a re-sealed room");
        }

        [Test]
        public void ALoadedRoomKeepsTheTemperatureTheFileGaveIt()
        {
            // The other side of the same rule: the first fill a layer ever has is a save
            // reattaching, not a room whose cells were outdoors.
            var f = new Fixture();
            f.BuildRoom(2, 2, 7, 7, y: 0);
            f.BuildCampfire(4, 4, y: 0);
            f.Passes(300);
            int warm = f.Temp(4, 5, 0);

            using var buffer = new MemoryStream();
            WorldSave.Save(f.World, buffer, new ISaveable[] { f.Temperature });
            buffer.Position = 0;

            var g = new Fixture();
            g.BuildRoom(2, 2, 7, 7, y: 0);    // solved before the load, as the game's is
            g.BuildCampfire(4, 4, y: 0);
            WorldSave.Load(g.World, buffer, new ISaveable[] { g.Temperature });
            Assert.That(g.Temp(4, 5, 0), Is.EqualTo(warm));

            var h = new Fixture();            // and loaded before anything asked
            h.BuildRoom(2, 2, 7, 7, y: 0);
            h.BuildCampfire(4, 4, y: 0);
            buffer.Position = 0;
            WorldSave.Load(h.World, buffer, new ISaveable[] { h.Temperature });
            h.Pass();
            g.Pass();
            Assert.That(h.Temp(4, 5, 0), Is.EqualTo(g.Temp(4, 5, 0)));
        }

        // ---- F2: the dirty window ------------------------------------------------------------------

        [Test]
        public void RoofingTheGroundFloorLastEnclosesTheCellarUnderIt()
        {
            var f = new Fixture(sy: 5);
            f.BuildRoom(2, 2, 7, 7, y: 0, floor: false, roof: false);
            f.BuildRoom(2, 2, 7, 7, y: 1, floor: true, roof: false);   // floor slabs at y=1
            f.RemoveSlab(5, 5, 1);                                     // the stairwell
            Assert.That(f.Enclosure.RoomAt(f.Cell(4, 4, 1)), Is.EqualTo(0), "no roof yet: nothing is enclosed");
            Assert.That(f.Enclosure.RoomAt(f.Cell(4, 4, 0)), Is.EqualTo(0));

            for (int x = 3; x <= 6; x++)
            for (int z = 3; z <= 6; z++)
                f.BuildSlab(x, z, 2);
            f.World.Tick();

            int groundFloor = f.Enclosure.RoomAt(f.Cell(4, 4, 1));
            int cellar = f.Enclosure.RoomAt(f.Cell(4, 4, 0));
            var fresh = new EnclosureGrid(f.Cells, f.Edifices);
            Assert.That(groundFloor, Is.GreaterThan(0), "the ground floor is enclosed once roofed");
            Assert.That(cellar, Is.GreaterThan(0), "and so is the cellar that opens into it");
            Assert.That(cellar, Is.EqualTo(fresh.RoomAt(f.Cell(4, 4, 0))),
                "the played world and a fresh solve of the same board agree");
        }

        [Test]
        public void AShaftThreeDeepComesToLifeFromOneRoofOnOneSolve()
        {
            var f = new Fixture(sy: 6);
            for (int y = 0; y <= 2; y++)
                f.BuildRoom(2, 2, 7, 7, y, floor: y > 0, roof: false);
            f.RemoveSlab(5, 5, 1);
            f.RemoveSlab(5, 5, 2);
            Assert.That(f.Enclosure.RoomAt(f.Cell(4, 4, 0)), Is.EqualTo(0));

            for (int x = 3; x <= 6; x++)
            for (int z = 3; z <= 6; z++)
                f.BuildSlab(x, z, 3);
            f.World.Tick();

            for (int y = 0; y <= 2; y++)
                Assert.That(f.Enclosure.RoomAt(f.Cell(4, 4, y)), Is.GreaterThan(0), $"layer {y} is a room");
            var fresh = new EnclosureGrid(f.Cells, f.Edifices);
            for (int y = 0; y <= 2; y++)
                Assert.That(f.Enclosure.RoomAt(f.Cell(4, 4, y)), Is.EqualTo(fresh.RoomAt(f.Cell(4, 4, y))));
        }

        // ---- F3: the floor that came out ---------------------------------------------------------

        [Test]
        public void TakingARoofSlabOutThroughTheConstructionGridUnroofsTheRoom()
        {
            ColonyWorld colony = ColonyWorld.Build(BoardSizes.Small, 7u, ScenarioDef.Bare(),
                mapType: MapType.Natural, wooded: false);
            CellGrid cells = colony.Pawns.Cells;
            GridSize size = cells.Size;
            EnclosureGrid enclosure = colony.Pawns.Enclosure!;

            // A flat 6x6 patch of the surface: the first column whose neighbourhood stands on
            // the same layer with air above.
            int x0 = -1, z0 = -1, y0 = -1;
            for (int x = 10; x < size.SizeX - 16 && x0 < 0; x++)
            for (int z = 10; z < size.SizeZ - 16 && x0 < 0; z++)
            {
                int y = SurfaceAt(cells, x, z);
                if (y <= 0 || y + 2 >= size.SizeY) continue;
                bool flat = true;
                for (int dx = 0; dx < 6 && flat; dx++)
                for (int dz = 0; dz < 6 && flat; dz++)
                    if (SurfaceAt(cells, x + dx, z + dz) != y
                        || cells.IsSolidTerrain(size.Index(x + dx, z + dz, y + 1))) flat = false;
                if (flat) { x0 = x; z0 = z; y0 = y; }
            }
            Assert.That(x0, Is.GreaterThanOrEqualTo(0), "the barren meadow has a flat patch");

            List<PlacedEdifice> edifices = colony.Pawns.Construction!.Edifices.Records;
            for (int dx = 0; dx < 6; dx++)
            for (int dz = 0; dz < 6; dz++)
            {
                bool edge = dx == 0 || dx == 5 || dz == 0 || dz == 5;
                int c = size.Index(x0 + dx, z0 + dz, y0);
                if (edge)
                {
                    edifices.Add(new PlacedEdifice { CellIndex = c, Def = CoreContent.EdificeWall, Stuff = NaturalContent.StuffWood });
                    cells.Edifice[c] = edifices.Count - 1;
                    cells.Flags[c] |= CellFlags.BlockingEdifice;
                }
                else
                {
                    cells.Floor[c + size.LayerStride] = CoreContent.SlabBuilt;
                }
                enclosure.MarkDirty(c);
            }
            int inside = size.Index(x0 + 2, z0 + 2, y0);
            Assert.That(enclosure.RoomAt(inside), Is.GreaterThan(0), "the hut is a room");

            // Then the job's own path out, not the fixture's.
            bool removed = colony.Pawns.Construction!.RemoveSlab(colony.Pawns, inside + size.LayerStride, out _);
            Assert.That(removed, "the slab was ours to take");
            colony.World.Tick();
            Assert.That(enclosure.RoomAt(inside), Is.EqualTo(0),
                "a roof with a slab out of it is no roof, on the very next solve");
        }

        static int SurfaceAt(CellGrid cells, int x, int z)
        {
            GridSize size = cells.Size;
            for (int y = size.SizeY - 1; y > 0; y--)
                if (cells.IsSolidTerrain(size.Index(x, z, y - 1)) && !cells.IsSolidTerrain(size.Index(x, z, y)))
                    return y;
            return -1;
        }

        // ---- F7: the remainder ---------------------------------------------------------------------

        [Test]
        public void OneColonistInAFourByFourRoomWarmsItAtAll()
        {
            var f = new Fixture(climate: Fixture.Still(-1_000));
            f.BuildRoom(2, 2, 7, 7, y: 0);                 // 16 cells of air, 15 a pass from one body
            f.Ctx.Pawns.Spawn(f.Cell(4, 4, 0));
            int before = f.Temp(3, 3, 0);
            f.Passes(200);
            Assert.That(f.Temp(3, 3, 0), Is.GreaterThan(before),
                "one person in a small sealed room warms it a little");
        }

        [Test]
        public void TheResidualRidesTheSaveAndTheHash()
        {
            var f = new Fixture(climate: Fixture.Still(-1_000));
            f.BuildRoom(2, 2, 7, 7, y: 0);
            f.Ctx.Pawns.Spawn(f.Cell(4, 4, 0));
            f.Passes(3);                                   // 45 owed, 2 spent, 13 carried

            using var buffer = new MemoryStream();
            WorldSave.Save(f.World, buffer, new ISaveable[] { f.Temperature });
            buffer.Position = 0;
            var g = new Fixture(climate: Fixture.Still(-1_000));
            g.BuildRoom(2, 2, 7, 7, y: 0);
            g.Ctx.Pawns.Spawn(g.Cell(4, 4, 0));
            WorldSave.Load(g.World, buffer, new ISaveable[] { g.Temperature });

            var a = new StateHash(); f.Temperature.ContributeTo(ref a);
            var b = new StateHash(); g.Temperature.ContributeTo(ref b);
            Assert.That(b.Value, Is.EqualTo(a.Value), "the section hashes alike after a round trip");

            f.Passes(20);
            g.Passes(20);
            Assert.That(g.Temp(3, 3, 0), Is.EqualTo(f.Temp(3, 3, 0)), "and the two worlds go on warming in step");
        }

        // ---- F1: how fast the severity bar fills -------------------------------------------------------

        [Test]
        public void ACandleNightFillsTheSeverityBarInAboutFourHours()
        {
            var tuning = ContentPack.Pawns().Temperature;
            int perInterval = -tuning.SeverityDelta(-1_300);           // a Candle night, -13 C
            int intervals = (1_000 + perInterval - 1) / perInterval;
            long ticks = (long)intervals * ContentPack.Pawns().NeedsIntervalTicks;
            Assert.That(ticks, Is.InRange(3L * Calendar.TicksPerHour, 5L * Calendar.TicksPerHour),
                $"the XML promises about four hours of exposure before the bar is full; this is {ticks} ticks");
        }

        // ---- F8: the cached ambient rides the save ------------------------------------------------------

        [Test]
        public void AColonistsAmbientSurvivesASave()
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
            Assert.That(back.AmbientTempC, Is.EqualTo(-2_000));
            Assert.That(back.WorkRatePerMille(WorkTypeIndex.Haul), Is.EqualTo(700),
                "the work rate a loaded colonist runs at is the one she was saved at");
        }
    }
}
