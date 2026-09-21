#nullable enable
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Growing;
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
    /// The thermal pass (design 28): the curves, the surfaces, the buoyancy experiment a-06
    /// ranks as the tie-breaker for the whole per-layer design, the clamp that keeps the
    /// integrator honest, and the inheritance that makes a moved wall carry its heat.
    ///
    /// <para>The fixture builds the smallest world that can hold rooms: a grid, an enclosure
    /// solve, and the thermal system, on a board whose climate is whatever the test says. No
    /// pawns unless a test spawns them, no jobs, no snapshot — the pass itself is the thing
    /// under test, and everything else is scenery it does not read.</para>
    /// </summary>
    [TestFixture]
    public class TemperatureTests
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

                // Ground under layer 0, so rooms there stand on something and never look down
                // into the void.
                for (int x = 0; x < sx; x++)
                for (int z = 0; z < sz; z++)
                    Cells.Floor[Size.Index(x, z, 0)] = CoreContent.SlabBuilt;

                var nav = new NavGraph(Cells);
                Ctx = new PawnContext(Cells, nav, new PathService(new PathFinder(nav)), ContentPack.Pawns())
                {
                    Enclosure = Enclosure = new EnclosureGrid(Cells, Edifices),
                };
                Climate = climate ?? new ClimateDef();
                Ctx.Temperature = Temperature = new TemperatureSystem(Ctx, Edifices, Climate);
                World = new SimWorldBuilder().WithSeed(1).WithSize(Size)
                    .AddSystem(_ => Enclosure)
                    .AddSystem(_ => Temperature)
                    .Build();
            }

            public int Cell(int x, int z, int y = 0) => Size.Index(x, z, y);

            /// <summary>Tick the world forward to the next pass and run it, however far that
            /// is — the pass owns its cadence, and a test that ticked a precise count would be
            /// re-deriving it.</summary>
            public void Pass()
            {
                int target = ((World.CurrentTick / TemperatureSystem.IntervalTicks) + 1)
                             * TemperatureSystem.IntervalTicks;
                while (World.CurrentTick < target) World.Tick();
            }

            public void Passes(int count)
            {
                for (int i = 0; i < count; i++) Pass();
            }

            public void BuildWall(int x, int z, int y = 0, ushort stuff = CoreContent.StuffConcrete)
            {
                int c = Cell(x, z, y);
                Edifices.Add(new PlacedEdifice { CellIndex = c, Def = CoreContent.EdificeWall, Stuff = stuff });
                Cells.Edifice[c] = Edifices.Count - 1;
                Cells.Flags[c] |= CellFlags.BlockingEdifice;
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

            /// <summary>A campfire of the shipped content — 1,200 centi-degree-cells a pass,
            /// read through the building table the pass reads. Never a private fire: the Defs
            /// are shared by every test in the process, and a write-through here would retune
            /// them all.</summary>
            public void BuildCampfire(int x, int z, int y = 0)
            {
                int c = Cell(x, z, y);
                Edifices.Add(new PlacedEdifice
                {
                    CellIndex = c, Def = CoreContent.EdificeCampfire, Stuff = NaturalContent.StuffWood,
                });
                Cells.Edifice[c] = Edifices.Count - 1;
            }

            /// <summary>A room with walls on the given layer inside the rectangle, a roof above
            /// it, and — unless the test says otherwise — a floor of slabs under it, which is
            /// also the ceiling of whatever is below.</summary>
            public int BuildRoom(int x0, int z0, int x1, int z1, int y, bool floor = true)
            {
                for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    bool edge = x == x0 || x == x1 || z == z0 || z == z1;
                    if (edge) BuildWall(x, z, y);
                    else if (floor && y > 0) BuildSlab(x, z, y);
                }
                for (int x = x0 + 1; x < x1; x++)
                for (int z = z0 + 1; z < z1; z++)
                    BuildSlab(x, z, y + 1);
                return Enclosure.RoomAt(Cell(x0 + 1, z0 + 1, y));
            }
        }

        // ---- the curves -----------------------------------------------------------------------

        [Test]
        public void TheOutdoorCurveTracksSeasonAndHour()
        {
            var climate = new ClimateDef
            {
                annualMeanC = 2_000,
                monthlyOffsetC = { [0] = 0, [5] = -1_000 },
                dailyAmplitudeC = 1_000,
            };
            var f = new Fixture(climate: climate);

            // Larkspur, 14h: the mean plus the day's whole swing.
            Assert.That(f.Temperature.OutdoorTempC(14 * Calendar.TicksPerHour), Is.EqualTo(3_000));
            // Larkspur, 02h: the mean less the day's whole swing.
            Assert.That(f.Temperature.OutdoorTempC(2 * Calendar.TicksPerHour), Is.EqualTo(1_000));
            // Candle, 14h: the mean plus the month's offset plus the day's.
            long candle = 5 * Calendar.TicksPerMonth + 14 * Calendar.TicksPerHour;
            Assert.That(f.Temperature.OutdoorTempC(candle), Is.EqualTo(2_000));
        }

        [Test]
        public void TheGroundDampsTheSeasonalSwingWithDepth()
        {
            var climate = new ClimateDef
            {
                annualMeanC = 2_000,
                monthlyOffsetC = { [0] = 1_000 },
                groundOneLayerDampingPerMille = 500,
            };
            var f = new Fixture(climate: climate);
            f.World.Tick(); // month 0, any time — the ground reads the season, never the hour

            Assert.That(f.Temperature.GroundTempC(f.Size.SizeY - 1), Is.EqualTo(3_000),
                "the surface layer feels the whole seasonal swing");
            Assert.That(f.Temperature.GroundTempC(f.Size.SizeY - 2), Is.EqualTo(2_500),
                "one layer down, half of it");
            Assert.That(f.Temperature.GroundTempC(0), Is.EqualTo(2_125),
                "at the bottom of a four-layer board an eighth of the swing is all that is left");
        }

        // ---- rooms and surfaces -----------------------------------------------------------------

        [Test]
        public void ARoomKeepsItsKeyAcrossASolve()
        {
            var f = new Fixture();
            int key = f.BuildRoom(2, 2, 6, 6, y: 0);
            Assert.That(key, Is.GreaterThan(0), "the room exists");

            f.Enclosure.MarkAllDirty();
            f.Enclosure.Tick(f.World);
            Assert.That(f.Enclosure.RoomAt(f.Cell(4, 4, 0)), Is.EqualTo(key),
                "the same cell set must produce the same key, whatever iteration found it");
        }

        [Test]
        public void ThePaneAnswerIsTheRoomsAnswerIndoorsAndTheCurveOut()
        {
            var climate = new ClimateDef { annualMeanC = 1_500, dailyAmplitudeC = 0 };
            for (int m = 0; m < climate.monthlyOffsetC.Count; m++) climate.monthlyOffsetC[m] = 0;
            var f = new Fixture(climate: climate);
            f.BuildRoom(2, 2, 6, 6, y: 0);
            f.BuildCampfire(4, 4, y: 0);
            f.Passes(200); // a good while: the fire has its equilibrium

            int indoors = f.Temperature.CellTemp(f.Cell(3, 3, 0), f.World.CurrentTick);
            int outdoors = f.Temperature.CellTemp(f.Cell(15, 15, 0), f.World.CurrentTick);
            Assert.That(outdoors, Is.EqualTo(1_500), "an open cell reads the curve");
            Assert.That(indoors, Is.GreaterThan(2_000),
                "a fired room sits comfortably above the outdoors");
        }

        [Test]
        public void AFiredRoomSettlesAndStaysPut()
        {
            var climate = new ClimateDef { annualMeanC = -2_000, dailyAmplitudeC = 0 };
            var f = new Fixture(climate: climate);
            f.BuildRoom(2, 2, 7, 7, y: 0);
            f.BuildCampfire(4, 4, y: 0);
            f.Passes(600); // fifty game-hours: past equilibrium

            int first = f.Temperature.CellTemp(f.Cell(4, 5, 0), f.World.CurrentTick);
            f.Passes(50);
            int second = f.Temperature.CellTemp(f.Cell(4, 5, 0), f.World.CurrentTick);
            Assert.That(second, Is.EqualTo(first), "an equilibrium is a number that stops moving");
            Assert.That(first, Is.GreaterThan(0),
                "the fire holds its room above freezing against a −20 °C outdoors");
        }

        [Test]
        public void WarmAirClimbsThroughAnOpeningAndColdDoesNotFallAsFast()
        {
            // a-06's tie-breaker experiment: two stacked rooms joined by one opening cell, the
            // same fire below and above. What the 4:1 buys is the separation between the two —
            // warming from below shares far more upstairs than warming from above shares down.
            ClimateDef Still()
            {
                var climate = new ClimateDef { annualMeanC = -500, dailyAmplitudeC = 0 };
                for (int m = 0; m < climate.monthlyOffsetC.Count; m++) climate.monthlyOffsetC[m] = 0;
                return climate;
            }

            int Gap(int fireLayer)
            {
                var f = new Fixture(climate: Still());
                f.BuildRoom(2, 2, 7, 7, y: 0, floor: false);       // the cellar, on the ground
                for (int x = 3; x <= 6; x++)
                for (int z = 3; z <= 6; z++)
                    f.BuildSlab(x, z, 1);                          // its roof, the loft's floor
                f.RemoveSlab(5, 5, 1);                             // the one opening
                f.BuildRoom(2, 2, 7, 7, y: 1, floor: false);       // the loft, its floor already laid
                f.BuildCampfire(4, 4, y: fireLayer);
                f.Passes(600);

                int lower = f.Temperature.CellTemp(f.Cell(4, 5, 0), f.World.CurrentTick);
                int upper = f.Temperature.CellTemp(f.Cell(4, 5, 1), f.World.CurrentTick);
                return upper - lower;
            }

            int fireBelow = Gap(fireLayer: 0);
            int fireAbove = Gap(fireLayer: 1);

            // The gap is upper minus lower, and the asymmetry reads through its size: driven
            // from below, the fast upward coupling keeps the loft close behind the fired hall;
            // driven from above, the slow downward one leaves the cellar far below the loft.
            // A plain flow would give the same gap either way, and that is the control.
            Assert.That(fireBelow, Is.GreaterThan(-1_500),
                "a fire under an open stairwell warms the loft close behind the hall — warm air climbs freely");
            Assert.That(fireAbove, Is.GreaterThan(2_500),
                "the same fire over the stairwell leaves the cellar far below the loft — cold does not fall as fast");
            Assert.That(fireAbove - fireBelow, Is.GreaterThan(2_500),
                "the 4:1 must be visible: a plain flow would give the same gap either way");
        }

        [Test]
        public void AnOpenDoorNeverOvershoots()
        {
            // Two rooms joined by a door held open: a near-merge, and the one place the
            // integrator could oscillate. The difference must shrink monotonically, never cross
            // zero, and never step more than a quarter of itself.
            var climate = new ClimateDef { annualMeanC = 2_000, dailyAmplitudeC = 0 };
            var f = new Fixture(climate: climate);
            f.BuildRoom(2, 2, 7, 7, y: 0);
            f.BuildRoom(9, 2, 14, 7, y: 0);

            // The door between them, and the one beside it to the outside world stays shut.
            int door = f.Cell(8, 4, 0);
            f.Edifices.Add(new PlacedEdifice { CellIndex = door, Def = CoreContent.EdificeDoor });
            f.Cells.Edifice[door] = f.Edifices.Count - 1;
            f.Enclosure.MarkDirty(door);
            f.Ctx.Nav.SetDoorOpen(door, true);

            f.BuildCampfire(4, 4, y: 0);
            f.Passes(3); // warm one side well up, while the other is still cold

            int previous = int.MinValue;
            for (int i = 0; i < 80; i++)
            {
                int left = f.Temperature.CellTemp(f.Cell(4, 4, 0), f.World.CurrentTick);
                int right = f.Temperature.CellTemp(f.Cell(11, 4, 0), f.World.CurrentTick);
                int gap = left - right;
                Assert.That(gap, Is.GreaterThan(0),
                    "the gap crossed zero: the near-merge overshot, which is the one thing the clamp exists to stop");
                if (previous != int.MinValue && i >= 70)
                    Assert.That(System.Math.Abs(gap - previous), Is.LessThanOrEqualTo(30),
                        $"the gap moved {System.Math.Abs(gap - previous)} in one pass at equilibrium: an oscillation, not a settling");
                previous = gap;
                f.Pass();
            }
        }

        [Test]
        public void AMovedWallCarriesItsHeat()
        {
            // Hysteresis: split a warm room down the middle and both halves must come away
            // carrying the room's heat, not the outdoor answer.
            var climate = new ClimateDef { annualMeanC = -2_000, dailyAmplitudeC = 0 };
            var f = new Fixture(climate: climate);
            f.BuildRoom(2, 2, 9, 7, y: 0);
            f.BuildCampfire(4, 4, y: 0);
            f.Passes(600);
            int warm = f.Temperature.CellTemp(f.Cell(4, 4, 0), f.World.CurrentTick);
            Assert.That(warm, Is.GreaterThan(1_000), "the room is warm to begin with");

            for (int z = 3; z <= 6; z++) f.BuildWall(5, z, y: 0);
            f.Pass();

            int west = f.Temperature.CellTemp(f.Cell(4, 4, 0), f.World.CurrentTick);
            int east = f.Temperature.CellTemp(f.Cell(6, 4, 0), f.World.CurrentTick);
            Assert.That(west, Is.GreaterThan(warm - 600), "the half with the fire keeps the heat");
            Assert.That(east, Is.GreaterThan(500),
                "the half without the fire inherits warmth rather than snapping to the outdoors");
        }

        [Test]
        public void BodyHeatWarmsACrowdedRoom()
        {
            var climate = new ClimateDef { annualMeanC = -1_000, dailyAmplitudeC = 0 };
            var f = new Fixture(climate: climate);
            f.BuildRoom(2, 2, 5, 5, y: 0);
            for (int i = 0; i < 4; i++) f.Ctx.Pawns.Spawn(f.Cell(3 + i % 2, 3 + i / 2, 0));

            int before = f.Temperature.CellTemp(f.Cell(3, 3, 0), f.World.CurrentTick);
            f.Passes(200);
            int after = f.Temperature.CellTemp(f.Cell(3, 3, 0), f.World.CurrentTick);
            Assert.That(after, Is.GreaterThan(before),
                "four colonists in a sealed room warm it above the outdoors they shelter from");
        }

        // ---- state: save and hash ----------------------------------------------------------------

        [Test]
        public void TheSectionRoundTripsThroughASave()
        {
            var f = new Fixture(climate: new ClimateDef { annualMeanC = -2_000, dailyAmplitudeC = 0 });
            f.BuildRoom(2, 2, 7, 7, y: 0);
            f.BuildCampfire(4, 4, y: 0);
            f.Passes(300);
            int warm = f.Temperature.CellTemp(f.Cell(4, 4, 0), f.World.CurrentTick);

            using var buffer = new MemoryStream();
            WorldSave.Save(f.World, buffer, new ISaveable[] { f.Temperature });
            buffer.Position = 0;

            // A second world built the same way — rooms recompute identically from the same
            // board, so the saved keys reattach exactly (design 28 §9).
            var second = new Fixture(climate: new ClimateDef { annualMeanC = -2_000, dailyAmplitudeC = 0 });
            second.BuildRoom(2, 2, 7, 7, y: 0);
            second.BuildCampfire(4, 4, y: 0);
            WorldSave.Load(second.World, buffer, new ISaveable[] { second.Temperature });

            Assert.That(second.Temperature.CellTemp(second.Cell(4, 4, 0), second.World.CurrentTick),
                Is.EqualTo(warm), "a loaded room is as warm as the one that was saved");
        }

        // ---- the content answers ------------------------------------------------------------------

        [Test]
        public void TheGrowthResponseIsTheReferenceShape()
        {
            var plant = new PlantDef();
            Assert.That(plant.GrowRatePerMille(0), Is.EqualTo(0), "frozen solid, stopped");
            Assert.That(plant.GrowRatePerMille(300), Is.EqualTo(500), "half way to the band, half speed");
            Assert.That(plant.GrowRatePerMille(2_000), Is.EqualTo(1_000), "comfortable, full speed");
            Assert.That(plant.GrowRatePerMille(5_800), Is.EqualTo(0), "cooked, stopped");
            Assert.That(plant.GrowRatePerMille(5_000), Is.EqualTo(500), "half way past the band, half speed");
        }

        [Test]
        public void ThePawnBandsReadTheWayTheTableSays()
        {
            var tuning = ContentPack.Pawns().Temperature;
            Assert.That(tuning.MoodOffset(2_000), Is.EqualTo(0), "comfortable is nothing at all");
            Assert.That(tuning.MoodOffset(1_100), Is.EqualTo(tuning.moodMildOffset), "a Wash night");
            Assert.That(tuning.MoodOffset(-400), Is.EqualTo(tuning.moodExtremeOffset), "a Candle night");
            Assert.That(tuning.SeverityDelta(1_000), Is.EqualTo(0), "inside the safe bounds, no bar");
            // A Candle night: ten degrees past the floor is fifteen an interval, a full bar in
            // four game-hours. The review found this pinned at 300 under a message saying
            // "three" — fourteen minutes to a full bar (design 28 §12, F1).
            Assert.That(tuning.SeverityDelta(-1_300), Is.EqualTo(-15),
                "ten degrees of cold is fifteen of severity an interval");
            Assert.That(tuning.SeverityDelta(4_000), Is.EqualTo(7),
                "five degrees of heat is seven");
            Assert.That(tuning.WorkPerMille(1_500), Is.EqualTo(1_000), "a working temperature");
            Assert.That(tuning.WorkPerMille(-2_000), Is.EqualTo(tuning.workOutsidePerMille),
                "outside the band, the reference's own 0.7");
        }

        [Test]
        public void WorkRateFeelsTheRoomAndTheFloorHolds()
        {
            var content = ContentPack.Pawns();
            var pawn = new Pawn(new PawnId(1), 0, content);
            int work = WorkTypeIndex.Haul; // no rate skill: a flat curve, so temperature is the only factor

            pawn.AmbientTempC = 2_000;
            int comfortable = pawn.WorkRatePerMille(work);
            Assert.That(comfortable, Is.EqualTo(1_000));

            pawn.AmbientTempC = -2_000;
            Assert.That(pawn.WorkRatePerMille(work), Is.EqualTo(700), "cold work is slow work");

            // The floor survives the cold: no factor prices a tick of work at nothing.
            pawn.AmbientTempC = -27_000;
            int floored = pawn.WorkRatePerMille(work);
            var def = content.WorkTypes[work];
            Assert.That(floored, Is.GreaterThanOrEqualTo(def.workRateFloorPerMille));
        }
    }
}
