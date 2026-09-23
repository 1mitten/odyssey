#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Power;
using Odyssey.Sim.Temperature;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Power (design 32): the line layer, the nets, the balance, the burn and the heat. The
    /// sections follow design 32's own.
    ///
    /// <para>Two fixtures, for two kinds of question. <see cref="Rig"/> is the smallest world a
    /// net can be judged in — a grid, rooms, the thermal pass and the power grid, no colonists —
    /// because a balance or a burn is arithmetic and a colony around it is scenery. The colony
    /// (<see cref="Board"/>) is for everything a player's order goes through: the intent, the
    /// lift, the cancel, the save.</para>
    /// </summary>
    public class PowerTests
    {
        // ---- the rig --------------------------------------------------------------------------

        sealed class Rig
        {
            public readonly GridSize Size;
            public readonly CellGrid Cells;
            public readonly List<PlacedEdifice> Edifices;
            public readonly EnclosureGrid Enclosure;
            public readonly PawnContext Ctx;
            public readonly TemperatureSystem Temperature;
            public readonly PowerGrid Power;
            public readonly SimWorld World;

            public Rig(int sx = 24, int sz = 24, int sy = 4, ClimateDef? climate = null)
            {
                Size = new GridSize(sx, sz, sy);
                Cells = new CellGrid(Size);
                Edifices = new List<PlacedEdifice>();

                // Ground under layer 0, as the thermal tests lay it.
                for (int x = 0; x < sx; x++)
                for (int z = 0; z < sz; z++)
                    Cells.Floor[Size.Index(x, z, 0)] = CoreContent.SlabBuilt;

                var nav = new NavGraph(Cells);
                Ctx = new PawnContext(Cells, nav, new PathService(new PathFinder(nav)), ContentPack.Pawns())
                {
                    Enclosure = Enclosure = new EnclosureGrid(Cells, Edifices),
                };
                Ctx.Temperature = Temperature = new TemperatureSystem(Ctx, Edifices, climate ?? Still());
                Ctx.Power = Power = new PowerGrid(Cells, Edifices);
                World = new SimWorldBuilder().WithSeed(1).WithSize(Size)
                    .AddSystem(_ => Enclosure)
                    .AddSystem(_ => Power)
                    .AddSystem(_ => Temperature)
                    .Build();
            }

            public int Cell(int x, int z, int y = 0) => Size.Index(x, z, y);

            public void Line(int x, int z, int y = 0) => Power.AddLine(Cell(x, z, y));

            /// <summary>A run of line along X on one layer, both ends included.</summary>
            public void LineX(int x0, int x1, int z, int y = 0)
            {
                for (int x = x0; x <= x1; x++) Line(x, z, y);
            }

            int Stand(ushort def, int x, int z, int y, byte facing)
            {
                int c = Cell(x, z, y);
                Edifices.Add(new PlacedEdifice
                {
                    CellIndex = c, Def = def, Stuff = NaturalContent.StuffWood, Built = true, Facing = facing,
                });
                int handle = Edifices.Count - 1;
                Cells.Edifice[c] = handle;
                int second = EdificeFootprint.SecondCell(c, def, facing, Size);
                if (second >= 0) Cells.Edifice[second] = handle;
                Power.AddDevice(handle);
                return handle;
            }

            /// <summary>A generator with its head here, facing east so its second cell is x + 1,
            /// its hopper filled with this much wood.</summary>
            public int Generator(int x, int z, int y = 0, int wood = 75) =>
                Fuelled(Stand(CoreContent.EdificeGenerator, x, z, y, facing: 1), wood);

            int Fuelled(int handle, int wood)
            {
                Power.SetFuelMilli(handle, wood * PowerGrid.Milli);
                return handle;
            }

            public int Heater(int x, int z, int y = 0) => Stand(CoreContent.EdificeHeater, x, z, y, facing: 0);

            public void BurnPasses(int count)
            {
                for (int i = 0; i < count; i++) Power.Tick(World);
            }

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

            public void BuildWall(int x, int z, int y = 0)
            {
                int c = Cell(x, z, y);
                Edifices.Add(new PlacedEdifice { CellIndex = c, Def = CoreContent.EdificeWall, Stuff = CoreContent.StuffConcrete });
                Cells.Edifice[c] = Edifices.Count - 1;
                Cells.Flags[c] |= CellFlags.BlockingEdifice;
                Enclosure.MarkDirty(c);
            }

            public int BuildRoom(int x0, int z0, int x1, int z1, int y = 0)
            {
                for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                    if (x == x0 || x == x1 || z == z0 || z == z1) BuildWall(x, z, y);
                for (int x = x0 + 1; x < x1; x++)
                for (int z = z0 + 1; z < z1; z++)
                {
                    int c = Cell(x, z, y + 1);
                    Cells.Floor[c] = CoreContent.SlabBuilt;
                    Enclosure.MarkDirty(c);
                }
                return Enclosure.RoomAt(Cell(x0 + 1, z0 + 1, y));
            }
        }

        /// <summary>A climate with no seasons and no day: whatever moves a room is what the test put in it.</summary>
        static ClimateDef Still(int meanC = -1_000)
        {
            var climate = new ClimateDef { annualMeanC = meanC, dailyAmplitudeC = 0 };
            for (int m = 0; m < climate.monthlyOffsetC.Count; m++) climate.monthlyOffsetC[m] = 0;
            return climate;
        }

        // ---- the colony -----------------------------------------------------------------------

        static readonly GridSize BoardSize = new GridSize(60, 60, 16);

        static ColonyWorld Board(uint seed = 1u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 3;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(BoardSize, seed, scenario, barren: true, wooded: false);
        }

        static PowerGrid PowerOf(ColonyWorld colony) => colony.Pawns.Power!;

        /// <summary>An open air cell on the start's layer, this far from the start, with ground under it.</summary>
        static int Open(ColonyWorld colony, int dx, int dz)
        {
            CellRef s = colony.Start;
            int cell = BoardSize.Index(s.X + dx, s.Z + dz, s.Y);
            Assume.That(colony.Construction.Allows(cell), Is.True, "an ordinary buildable cell");
            return cell;
        }

        static IntentRejection Send(ColonyWorld colony, IntentKind kind, int cell, int a = 0, int b = 0, int c = 0)
        {
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(new Intent(kind, BoardSize.FromIndex(cell), a, b, c));
            colony.World.Tick();
            var rejected = colony.World.Intents.Rejected;
            return rejected.Count == 0 ? IntentRejection.None : rejected[0].Reason;
        }

        static IntentRejection OrderLine(ColonyWorld colony, int cell) =>
            Send(colony, IntentKind.PlaceBuilding, cell, BuildingHandle.Conduit, StuffHandle.Wood);

        /// <summary>Order a building, bring its material and raise it, now, as the build job would.</summary>
        static int RaiseNow(ColonyWorld colony, int cell, int building, int facing = 0)
        {
            Assert.That(colony.Construction.Place(BoardSize.FromIndex(cell), building, StuffHandle.Wood, facing),
                Is.EqualTo(IntentRejection.None));
            colony.Construction.Deliver(cell, ConstructionContent.BuildingAt(building).costCount);
            Assert.That(colony.Construction.Raise(colony.Pawns, cell), Is.True);
            return colony.Grid.Edifice[cell];
        }

        static ulong HashOf(PowerGrid power)
        {
            var hash = new StateHash();
            power.ContributeTo(ref hash);
            return hash.Value;
        }

        // ---- the content (§6, §7) ---------------------------------------------------------------

        /// <summary>
        /// The three rows say what design 32 says, and the one number every later test leans on —
        /// five heaters to a generator — holds.
        /// </summary>
        [Test]
        public void TheContentIsDesign32s()
        {
            BuildingDef line = ConstructionContent.BuildingAt(BuildingHandle.Conduit);
            BuildingDef generator = ConstructionContent.BuildingAt(BuildingHandle.Generator);
            BuildingDef heater = ConstructionContent.BuildingAt(BuildingHandle.Heater);

            Assert.That(line.conduit, Is.True);
            Assert.That(line.edifice, Is.EqualTo(CoreContent.EdificeNone), "a line is not an edifice");
            // All part and no material: one scrap metal a cell (design 32 §14).
            Assert.That(line.costCount, Is.Zero);
            Assert.That(line.partItem, Is.EqualTo(ItemHandle.Salvage));
            Assert.That(line.partCount, Is.EqualTo(1));
            Assert.That(generator.partItem, Is.EqualTo(ItemHandle.Salvage));
            Assert.That(generator.partCount, Is.EqualTo(20));
            Assert.That(heater.partCount, Is.EqualTo(5));

            Assert.That(generator.edifice, Is.EqualTo(CoreContent.EdificeGenerator));
            Assert.That(generator.footprint, Is.EqualTo(2));
            Assert.That(generator.powerOutputW, Is.EqualTo(1_000));
            Assert.That(generator.fuelItem, Is.EqualTo(ItemHandle.Wood));
            Assert.That(generator.fuelCapacity, Is.EqualTo(75));
            Assert.That(generator.fuelPerDay, Is.EqualTo(22));

            Assert.That(heater.edifice, Is.EqualTo(CoreContent.EdificeHeater));
            Assert.That(heater.powerDrawW, Is.EqualTo(175));
            Assert.That(generator.powerOutputW / heater.powerDrawW, Is.EqualTo(5),
                "five heaters to a generator, and a sixth darkens the net");

            Assert.That(ConstructionContent.BuildingAt(BuildingHandle.Campfire).IsPowered, Is.False);
            Assert.That(generator.IsPowered && heater.IsPowered, Is.True);
        }

        // ---- the line layer (§3) ----------------------------------------------------------------

        /// <summary>
        /// Decision 2 in one test: a line goes where a wall stands, which is the whole reason a
        /// line is not an edifice — and it is still refused in rock and water.
        /// </summary>
        [Test]
        public void ALineGoesThroughAWallButNotThroughRockOrWater()
        {
            ColonyWorld colony = Board();
            PowerGrid power = PowerOf(colony);
            int wall = Open(colony, 3, 3);
            RaiseNow(colony, wall, BuildingHandle.Wall);
            Assume.That(colony.Grid.Edifice[wall], Is.GreaterThanOrEqualTo(0), "a wall stands there");

            Assert.That(OrderLine(colony, wall), Is.EqualTo(IntentRejection.None));
            Assert.That(power.HasSite(wall), Is.True, "the line is ordered inside the wall");

            // Two layers down is rock with rock above it: no lift, and no line.
            int rock = wall - 2 * BoardSize.LayerStride;
            Assume.That(colony.Grid.IsSolidTerrain(rock) && colony.Grid.IsSolidTerrain(rock + BoardSize.LayerStride), Is.True);
            Assert.That(OrderLine(colony, rock), Is.EqualTo(IntentRejection.NotPermitted));

            int wet = Open(colony, 5, 3);
            colony.Grid.Terrain[wet] = NaturalContent.TerrainShallowWater;
            Assert.That(power.AllowsLine(wet), Is.False, "a cell you can wade through takes no line");
        }

        /// <summary>A line needs nothing under it: a run can climb a shaft (decisions 2 and 3).</summary>
        [Test]
        public void ALineMayHangInOpenAir()
        {
            ColonyWorld colony = Board();
            int high = Open(colony, 4, 4) + 3 * BoardSize.LayerStride;
            Assume.That(colony.Grid.HasFloor(high), Is.False, "three storeys up with nothing below");

            Assert.That(OrderLine(colony, high), Is.EqualTo(IntentRejection.None));
            Assert.That(PowerOf(colony).HasSite(high), Is.True);
        }

        /// <summary>
        /// A click on the ground names the air standing on it, exactly as a wall's does — the
        /// path every dragged run takes. And the lift is the line's own: over a wall it lands in
        /// the wall's cell, where a wall's lift would refuse.
        /// </summary>
        [Test]
        public void AnOrderNamedAtTheGroundLandsOnTheCellAbove()
        {
            ColonyWorld colony = Board();
            int air = Open(colony, 3, 5);
            int ground = air - BoardSize.LayerStride;

            Assert.That(OrderLine(colony, ground), Is.EqualTo(IntentRejection.None));
            Assert.That(PowerOf(colony).HasSite(air), Is.True);
            Assert.That(PowerOf(colony).HasSite(ground), Is.False);

            int wall = Open(colony, 4, 5);
            RaiseNow(colony, wall, BuildingHandle.Wall);
            Assert.That(colony.Construction.WhereItWouldLand(wall - BoardSize.LayerStride, BuildingHandle.Conduit),
                Is.EqualTo(wall), "the line's lift goes into the wall's cell");
            Assert.That(colony.Construction.WhereItWouldLand(wall - BoardSize.LayerStride, BuildingHandle.Wall),
                Is.EqualTo(wall - BoardSize.LayerStride), "the control: a wall's lift does not");
        }

        /// <summary>A dragged run is judged on one layer, exactly as a wall's is.</summary>
        [Test]
        public void ADraggedRunLandsOnOneLayer()
        {
            ColonyWorld colony = Board();
            int air = Open(colony, 2, 7);
            var run = new List<CellRef>();
            for (int i = 0; i < 5; i++) run.Add(BoardSize.FromIndex(air - BoardSize.LayerStride + i));

            Assert.That(colony.Construction.RunLayerFor(run, BuildingHandle.Conduit), Is.EqualTo(colony.Start.Y));
        }

        /// <summary>
        /// A cell holds a wall order and a line order at once, and a cancel takes both — the line
        /// has its own lane, not the construction grid's one site per cell.
        /// </summary>
        [Test]
        public void AWallOrderAndALineOrderShareACellAndOneCancelTakesBoth()
        {
            ColonyWorld colony = Board();
            PowerGrid power = PowerOf(colony);
            int cell = Open(colony, 6, 2);

            Assert.That(Send(colony, IntentKind.PlaceBuilding, cell, BuildingHandle.Wall, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.None));
            Assert.That(OrderLine(colony, cell), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Construction.At(cell), Is.EqualTo(BuildingHandle.Wall));
            Assert.That(power.HasSite(cell), Is.True);

            Assert.That(Send(colony, IntentKind.CancelBuilding, cell), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Construction.At(cell), Is.EqualTo(BuildingHandle.None));
            Assert.That(power.HasSite(cell), Is.False);

            // A cell with nothing but a line order in it is still a cancel that did something.
            Assert.That(OrderLine(colony, cell), Is.EqualTo(IntentRejection.None));
            Assert.That(Send(colony, IntentKind.CancelBuilding, cell), Is.EqualTo(IntentRejection.None));
            Assert.That(Send(colony, IntentKind.CancelBuilding, cell), Is.EqualTo(IntentRejection.AlreadyInThatState),
                "the control: an empty cell has nothing to cancel");
        }

        /// <summary>
        /// The pane's Cancel for a line takes the line's order and nothing else (design 32 §14) —
        /// and the control, the cancel drag's intent, takes the wall order beside it too.
        /// </summary>
        [Test]
        public void CancellingALineFromItsPaneLeavesTheWallOrderInTheCell()
        {
            ColonyWorld colony = Board();
            PowerGrid power = PowerOf(colony);
            int cell = Open(colony, 6, 4);
            Send(colony, IntentKind.PlaceBuilding, cell, BuildingHandle.Wall, StuffHandle.Wood);
            OrderLine(colony, cell);

            Assert.That(Send(colony, IntentKind.CancelConduit, cell), Is.EqualTo(IntentRejection.None));
            Assert.That(power.HasSite(cell), Is.False, "the line order is gone");
            Assert.That(colony.Construction.At(cell), Is.EqualTo(BuildingHandle.Wall), "and the wall order stands");
            Assert.That(Send(colony, IntentKind.CancelConduit, cell), Is.EqualTo(IntentRejection.AlreadyInThatState));

            OrderLine(colony, cell);
            Send(colony, IntentKind.CancelBuilding, cell);
            Assert.That(colony.Construction.At(cell), Is.EqualTo(BuildingHandle.None), "the control: the drag's cancel takes both");
        }

        /// <summary>Only a built line can be marked to come up, and a cancel takes the mark back.</summary>
        [Test]
        public void ARemovalMarkNeedsABuiltLine()
        {
            ColonyWorld colony = Board();
            PowerGrid power = PowerOf(colony);
            int cell = Open(colony, 7, 3);

            Assert.That(Send(colony, IntentKind.RemoveConduit, cell), Is.EqualTo(IntentRejection.NotPermitted),
                "nothing to take up");
            power.AddLine(cell);
            Assert.That(Send(colony, IntentKind.RemoveConduit, cell), Is.EqualTo(IntentRejection.None));
            Assert.That(power.IsMarked(cell), Is.True);
            Assert.That(Send(colony, IntentKind.RemoveConduit, cell), Is.EqualTo(IntentRejection.AlreadyInThatState));

            Assert.That(Send(colony, IntentKind.CancelBuilding, cell), Is.EqualTo(IntentRejection.None));
            Assert.That(power.IsMarked(cell), Is.False);
            Assert.That(power.IsLine(cell), Is.True, "a cancelled mark leaves the line where it was");
        }

        /// <summary>
        /// A colony that has built nothing electric hashes exactly as it did before power existed
        /// (§8) — the reason no golden moves in this unit — and the control: one line does move it.
        /// </summary>
        [Test]
        public void AnEmptyGridAddsNothingToTheHashAndOneLineDoes()
        {
            ColonyWorld colony = Board();
            PowerGrid power = PowerOf(colony);
            Assert.That(HashOf(power), Is.EqualTo(new StateHash().Value), "empty adds nothing");

            ulong before = colony.World.ComputeStateHash().Value;
            power.AddLine(Open(colony, 3, 3));
            Assert.That(colony.World.ComputeStateHash().Value, Is.Not.EqualTo(before));
        }

        /// <summary>Lines, orders, marks and a building's switch and hopper all survive a save.</summary>
        [Test]
        public void TheGridRoundTripsThroughASave()
        {
            ColonyWorld colony = Board();
            PowerGrid power = PowerOf(colony);
            int a = Open(colony, 3, 3), b = Open(colony, 4, 3), site = Open(colony, 8, 3);
            power.AddLine(a);
            power.AddLine(b);
            Assert.That(power.MarkRemoval(b), Is.EqualTo(IntentRejection.None));
            Assert.That(power.PlaceLine(site), Is.EqualTo(IntentRejection.None));
            power.AddSiteWork(site, 7_000);
            int gen = RaiseNow(colony, Open(colony, 3, 5), BuildingHandle.Generator, facing: 1);
            power.SetFuelMilli(gen, 12_345);
            Assert.That(power.SetSwitch(BoardSize.Index(BoardSize.FromIndex(a).X, BoardSize.FromIndex(a).Z + 2, colony.Start.Y), false),
                Is.EqualTo(IntentRejection.None));
            colony.World.Tick();

            byte[] bytes = colony.Save();
            ulong hash = colony.World.ComputeStateHash().Value;

            ColonyWorld again = Board();
            again.Load(bytes);
            PowerGrid loaded = PowerOf(again);

            Assert.That(again.World.ComputeStateHash().Value, Is.EqualTo(hash));
            Assert.That(loaded.IsLine(a) && loaded.IsLine(b), Is.True);
            Assert.That(loaded.IsMarked(b), Is.True);
            Assert.That(loaded.SiteWork(site), Is.EqualTo(7_000));
            Assert.That(loaded.FuelMilli(gen), Is.EqualTo(12_345));
            Assert.That(loaded.Devices[loaded.DeviceIndex(gen)].On, Is.False);
        }

        /// <summary>
        /// A colony saved between two burn passes and loaded again runs on exactly as the one that
        /// never stopped: the burn's carried remainder is in the save, and the balance — derived,
        /// never saved — comes back the same.
        /// </summary>
        [Test]
        public void APlayedAndALoadedColonyBurnTheSame()
        {
            ColonyWorld Build(out int gen)
            {
                ColonyWorld c = Board();
                PowerGrid p = PowerOf(c);
                for (int x = 2; x <= 9; x++) p.AddLine(Open(c, x, 2));
                gen = RaiseNow(c, Open(c, 2, 3), BuildingHandle.Generator, facing: 1);
                p.SetFuelMilli(gen, 40_000);
                RaiseNow(c, Open(c, 6, 3), BuildingHandle.Heater);
                RaiseNow(c, Open(c, 8, 3), BuildingHandle.Heater);
                return c;
            }

            ColonyWorld played = Build(out int playedGen);
            played.World.Tick(250);                     // two passes and part of a third
            byte[] bytes = played.Save();

            ColonyWorld loaded = Build(out _);
            loaded.Load(bytes);
            Assert.That(loaded.World.ComputeStateHash().Value, Is.EqualTo(played.World.ComputeStateHash().Value));

            played.World.Tick(1_000);
            loaded.World.Tick(1_000);
            Assert.That(loaded.World.ComputeStateHash().Value, Is.EqualTo(played.World.ComputeStateHash().Value));
            Assert.That(PowerOf(played).FuelMilli(playedGen), Is.LessThan(40_000), "it did burn");
        }

        // ---- what is published (§9, §10) ----------------------------------------------------------

        static int Count(ReadOnlySpan<ConduitView> rows, ConduitKind kind)
        {
            int n = 0;
            foreach (var row in rows) if (row.Kind == kind) n++;
            return n;
        }

        /// <summary>
        /// Built lines are published only while the interface watches (process §3), and an order
        /// is published always — the control that the channel is alive while built lines are not.
        /// </summary>
        [Test]
        public void BuiltLinesArePublishedOnlyWhileWatchedAndOrdersAlways()
        {
            ColonyWorld colony = Board();
            PowerGrid power = PowerOf(colony);
            for (int x = 3; x <= 5; x++) power.AddLine(Open(colony, x, 3));
            Assert.That(power.PlaceLine(Open(colony, 6, 3)), Is.EqualTo(IntentRejection.None));
            colony.World.Tick();

            var frame = colony.World.Views.Current;
            Assert.That(Count(frame.Conduits, ConduitKind.Ordered), Is.EqualTo(1), "an order is always drawn");
            Assert.That(Count(frame.Conduits, ConduitKind.Built), Is.Zero, "nobody is watching");

            Assert.That(Send(colony, IntentKind.WatchPower, Open(colony, 3, 3), a: 1), Is.EqualTo(IntentRejection.None));
            frame = colony.World.Views.Current;
            Assert.That(Count(frame.Conduits, ConduitKind.Built), Is.EqualTo(3));

            Send(colony, IntentKind.WatchPower, Open(colony, 3, 3), a: 0);
            Assert.That(Count(colony.World.Views.Current.Conduits, ConduitKind.Built), Is.Zero);
        }

        /// <summary>
        /// A paused world answers the watch without spending a tick, so the lines appear the moment
        /// a power tool is armed, paused or not.
        /// </summary>
        [Test]
        public void APausedWorldStartsShowingTheLinesAtOnce()
        {
            ColonyWorld colony = Board();
            PowerOf(colony).AddLine(Open(colony, 3, 3));
            colony.World.Tick();
            int tick = colony.World.CurrentTick;

            colony.World.Intents.Submit(new Intent(IntentKind.WatchPower, colony.Start, 1));
            colony.World.RepublishViews();

            Assert.That(colony.World.CurrentTick, Is.EqualTo(tick), "no tick was spent");
            Assert.That(Count(colony.World.Views.Current.Conduits, ConduitKind.Built), Is.EqualTo(1));
        }

        /// <summary>The links are the simulation's: the middle of a run joins east and west, and a riser joins up.</summary>
        [Test]
        public void EachLineCarriesItsOwnLinks()
        {
            ColonyWorld colony = Board();
            PowerGrid power = PowerOf(colony);
            int west = Open(colony, 3, 3), middle = Open(colony, 4, 3), east = Open(colony, 5, 3);
            power.AddLine(west);
            power.AddLine(middle);
            power.AddLine(east);
            power.AddLine(middle + BoardSize.LayerStride);
            Send(colony, IntentKind.WatchPower, middle, a: 1);

            byte links = 0;
            foreach (var row in colony.World.Views.Current.Conduits)
                if (row.CellIndex == middle) links = row.Links;
            Assert.That(links, Is.EqualTo((byte)((1 << 1) | (1 << 3) | (1 << 4))), "east, west and up");
        }

        /// <summary>
        /// Every power building is published with what the pane and the alerts need — whether it
        /// is running, its net, its fuel — and every net with its balance.
        /// </summary>
        [Test]
        public void TheBuildingsAndNetsArePublished()
        {
            ColonyWorld colony = Board();
            PowerGrid power = PowerOf(colony);
            for (int x = 2; x <= 7; x++) power.AddLine(Open(colony, x, 2));
            int genHead = Open(colony, 2, 3);
            int gen = RaiseNow(colony, genHead, BuildingHandle.Generator, facing: 1);
            power.SetFuelMilli(gen, 20_000);
            int heaterCell = Open(colony, 6, 3);
            RaiseNow(colony, heaterCell, BuildingHandle.Heater);
            colony.World.Tick();

            var frame = colony.World.Views.Current;
            Assert.That(frame.TryGetPowerDevice(genHead + 1, out PowerDeviceView g), Is.True, "the far cell names it too");
            Assert.That(g.Role, Is.EqualTo(PowerRole.Generator));
            Assert.That(g.Powered && g.On, Is.True);
            Assert.That(g.LoadW, Is.EqualTo(175));
            Assert.That(g.FuelCapacityMilli, Is.EqualTo(75_000));
            Assert.That(g.FuelMilli, Is.LessThanOrEqualTo(20_000));

            Assert.That(frame.TryGetPowerDevice(heaterCell, out PowerDeviceView h), Is.True);
            Assert.That(h.Role, Is.EqualTo(PowerRole.Consumer));
            Assert.That(h.Powered, Is.True);
            Assert.That(h.NetKey, Is.EqualTo(g.NetKey));

            Assert.That(frame.TryGetPowerNet(g.NetKey, out PowerNetView net), Is.True);
            Assert.That(net.SupplyW, Is.EqualTo(1_000));
            Assert.That(net.DemandW, Is.EqualTo(175));
            Assert.That(net.State, Is.EqualTo(PowerNetState.Live));
        }

        // ---- nets (§4) --------------------------------------------------------------------------

        /// <summary>
        /// Decision 3: a line joins the line beside it and the line directly above or below it —
        /// and not one touching only at a corner.
        /// </summary>
        [Test]
        public void LinesJoinFaceToFaceAndUpAndDownButNotCornerToCorner()
        {
            var rig = new Rig();
            rig.Line(2, 2, 0);
            rig.Line(3, 2, 0);
            rig.Line(3, 2, 1);   // straight up
            rig.Line(4, 3, 1);   // corner to corner with (3,2,1): not joined

            Assert.That(rig.Power.Nets.Count, Is.EqualTo(2));
            Assert.That(rig.Power.NetAtLine(rig.Cell(3, 2, 1))!.Value.Key, Is.EqualTo(rig.Cell(2, 2, 0)),
                "the riser is on the first net, keyed by its lowest cell");
            Assert.That(rig.Power.NetAtLine(rig.Cell(4, 3, 1))!.Value.Key, Is.EqualTo(rig.Cell(4, 3, 1)));
        }

        /// <summary>
        /// Buildings do not carry power (§2a): a heater beside a generator with no line between
        /// them is on no net, and one line under them joins both.
        /// </summary>
        [Test]
        public void BuildingsDoNotCarryPowerOnlyLinesDo()
        {
            var rig = new Rig();
            int gen = rig.Generator(2, 2);          // cells (2,2) and (3,2)
            int heater = rig.Heater(4, 2);

            Assert.That(rig.Power.NetKeyOf(gen), Is.EqualTo(-1));
            Assert.That(rig.Power.IsPowered(heater), Is.False, "touching a generator is not a connection");

            rig.Line(3, 2);                          // under the generator's far cell, beside the heater
            Assert.That(rig.Power.NetKeyOf(gen), Is.EqualTo(rig.Cell(3, 2)));
            Assert.That(rig.Power.NetKeyOf(heater), Is.EqualTo(rig.Cell(3, 2)));
            Assert.That(rig.Power.IsPowered(heater), Is.True);
        }

        /// <summary>A building touching two nets joins the lowest-keyed and bridges nothing.</summary>
        [Test]
        public void ABuildingTouchingTwoNetsJoinsOneAndBridgesNeither()
        {
            var rig = new Rig();
            rig.Line(5, 4);
            rig.Line(7, 4);
            int heater = rig.Heater(6, 4);

            Assert.That(rig.Power.Nets.Count, Is.EqualTo(2), "the heater has not joined them");
            Assert.That(rig.Power.NetKeyOf(heater), Is.EqualTo(rig.Cell(5, 4)));
        }

        // ---- the balance (§5) ---------------------------------------------------------------------

        /// <summary>
        /// Decision 7, with its control: five heaters run on one generator, and a sixth darkens
        /// all six rather than itself — the whole net goes, there is no shedding.
        /// </summary>
        [Test]
        public void ASixthHeaterDarkensTheWholeNet()
        {
            var rig = new Rig();
            rig.LineX(2, 14, 5);
            int gen = rig.Generator(2, 6);
            var heaters = new List<int>();
            for (int i = 0; i < 5; i++) heaters.Add(rig.Heater(4 + 2 * i, 6));

            foreach (int h in heaters) Assert.That(rig.Power.IsPowered(h), Is.True, "five fit");
            Assert.That(rig.Power.NetOf(gen)!.Value.State, Is.EqualTo(PowerNetState.Live));

            heaters.Add(rig.Heater(14, 6));
            foreach (int h in heaters) Assert.That(rig.Power.IsPowered(h), Is.False, "a sixth darkens all six");
            Assert.That(rig.Power.NetOf(gen)!.Value.State, Is.EqualTo(PowerNetState.Dark));
            Assert.That(rig.Power.NetOf(gen)!.Value.DemandW, Is.EqualTo(6 * 175),
                "demand counts what is switched on, not what is powered — or a dark net would relight and flicker");
        }

        /// <summary>A switched-off heater wants nothing, so switching one off relights a net one short.</summary>
        [Test]
        public void SwitchingOneOffRelightsTheNet()
        {
            var rig = new Rig();
            rig.LineX(2, 14, 5);
            rig.Generator(2, 6);
            var heaters = new List<int>();
            for (int i = 0; i < 6; i++) heaters.Add(rig.Heater(4 + 2 * i, 6));
            Assume.That(rig.Power.IsPowered(heaters[0]), Is.False);

            Assert.That(rig.Power.SetSwitch(rig.Cell(14, 6), on: false), Is.EqualTo(IntentRejection.None));
            for (int i = 0; i < 5; i++) Assert.That(rig.Power.IsPowered(heaters[i]), Is.True);
            Assert.That(rig.Power.IsPowered(heaters[5]), Is.False, "the one switched off stays off");
            Assert.That(rig.Power.SetSwitch(rig.Cell(14, 6), on: false), Is.EqualTo(IntentRejection.AlreadyInThatState));
        }

        /// <summary>A switched-off generator makes nothing, and either of its cells names it.</summary>
        [Test]
        public void AGeneratorSwitchedOffByItsFarCellMakesNothing()
        {
            var rig = new Rig();
            rig.LineX(2, 6, 5);
            rig.Generator(2, 6);
            int heater = rig.Heater(5, 6);
            Assume.That(rig.Power.IsPowered(heater), Is.True);

            Assert.That(rig.Power.SetSwitch(rig.Cell(3, 6), on: false), Is.EqualTo(IntentRejection.None));
            Assert.That(rig.Power.IsPowered(heater), Is.False);
        }

        // ---- the burn (§6) ------------------------------------------------------------------------

        /// <summary>Decision 8's floor: a generator carrying nothing burns nothing.</summary>
        [Test]
        public void AGeneratorCarryingNothingBurnsNothing()
        {
            var rig = new Rig();
            rig.LineX(2, 6, 5);
            int gen = rig.Generator(2, 6, wood: 10);

            rig.BurnPasses(PowerGrid.PassesPerDay);
            Assert.That(rig.Power.FuelMilli(gen), Is.EqualTo(10_000));
        }

        /// <summary>
        /// Decision 8, exactly: one heater (175 W of 1,000) for one day burns 22 × 0.175 = 3.85
        /// wood to the milli-unit, and five burn 19.25 — the carried remainder loses nothing.
        /// </summary>
        [TestCase(1, 3_850)]
        [TestCase(5, 19_250)]
        public void TheBurnIsInProportionToTheLoadToTheMilliUnit(int heaters, int burnedMilli)
        {
            var rig = new Rig();
            rig.LineX(2, 14, 5);
            int gen = rig.Generator(2, 6);
            for (int i = 0; i < heaters; i++) rig.Heater(4 + 2 * i, 6);

            rig.BurnPasses(PowerGrid.PassesPerDay);
            Assert.That(75_000 - rig.Power.FuelMilli(gen), Is.EqualTo(burnedMilli));
        }

        /// <summary>
        /// Two generators on one net share its load by their output, so between them they burn
        /// what one would — over-building costs no wood (decision 8's point).
        /// </summary>
        [Test]
        public void TwoGeneratorsShareTheLoadAndBurnWhatOneWould()
        {
            var rig = new Rig();
            rig.LineX(2, 14, 5);
            int a = rig.Generator(2, 6);
            int b = rig.Generator(6, 6);
            rig.Heater(10, 6);
            rig.Heater(12, 6);

            Assert.That(rig.Power.LoadOf(a) + rig.Power.LoadOf(b), Is.EqualTo(350));
            rig.BurnPasses(PowerGrid.PassesPerDay);
            int burned = 150_000 - rig.Power.FuelMilli(a) - rig.Power.FuelMilli(b);
            Assert.That(burned, Is.EqualTo(7_700), "22 × 0.35 of a day, whoever carries it");
        }

        /// <summary>A generator that runs dry stops, and the net it was carrying goes dark.</summary>
        [Test]
        public void AGeneratorThatRunsDryDarkensItsNet()
        {
            var rig = new Rig();
            rig.LineX(2, 6, 5);
            int gen = rig.Generator(2, 6, wood: 0);
            rig.Power.SetFuelMilli(gen, 5);
            int heater = rig.Heater(5, 6);
            Assume.That(rig.Power.IsPowered(heater), Is.True);

            rig.BurnPasses(1);
            Assert.That(rig.Power.FuelMilli(gen), Is.EqualTo(0));
            Assert.That(rig.Power.IsPowered(heater), Is.False);
            Assert.That(rig.Power.NetOf(heater)!.Value.State, Is.EqualTo(PowerNetState.Dark));
        }

        /// <summary>Refuelling is in whole units, never past the top, and relights an empty generator.</summary>
        [Test]
        public void FuelGoesInWholeUnitsAndNeverPastTheTop()
        {
            var rig = new Rig();
            rig.LineX(2, 6, 5);
            int gen = rig.Generator(2, 6, wood: 0);
            int heater = rig.Heater(5, 6);
            Assume.That(rig.Power.IsPowered(heater), Is.False);
            Assert.That(rig.Power.NeedsRefuel(gen), Is.True);
            Assert.That(rig.Power.RoomForFuel(gen), Is.EqualTo(75));

            Assert.That(rig.Power.AddFuel(gen, 100), Is.EqualTo(75), "never past the top");
            Assert.That(rig.Power.IsPowered(heater), Is.True, "and it runs again");
            Assert.That(rig.Power.NeedsRefuel(gen), Is.False);

            rig.Power.SetFuelMilli(gen, 37_500);
            Assert.That(rig.Power.NeedsRefuel(gen), Is.False, "exactly half is not below half");
            rig.Power.SetFuelMilli(gen, 37_499);
            Assert.That(rig.Power.NeedsRefuel(gen), Is.True);
        }

        // ---- the heat (§6, §7) --------------------------------------------------------------------

        /// <summary>
        /// A heater warms only while powered, a generator in proportion to its load — and neither
        /// through the thermal pass's own table, which the control proves holds nothing for them.
        /// </summary>
        [Test]
        public void HeatIsTheGridsAnswer()
        {
            var rig = new Rig();
            rig.LineX(2, 6, 5);
            int gen = rig.Generator(2, 6);
            int heater = rig.Heater(5, 6);

            Assert.That(rig.Power.HeatOf(heater), Is.EqualTo(1_000));
            Assert.That(rig.Power.HeatOf(gen), Is.EqualTo(400 * 175 / 1_000), "a fifth-ish load, a fifth-ish heat");

            rig.Power.SetSwitch(rig.Cell(5, 6), on: false);
            Assert.That(rig.Power.HeatOf(heater), Is.EqualTo(0));
            Assert.That(rig.Power.HeatOf(gen), Is.EqualTo(0), "carrying nothing, burning nothing, warming nothing");
        }

        /// <summary>
        /// The end-to-end: a sealed room with a heater in it and a generator outside, the line
        /// running in through the wall. Powered, it warms; the same room with the generator
        /// switched off stays at the outdoors.
        /// </summary>
        [Test]
        public void ARoomWarmsOnlyWhileItsHeaterIsPowered()
        {
            int Run(bool powered)
            {
                var rig = new Rig();
                rig.BuildRoom(6, 6, 11, 11);
                int heater = rig.Heater(8, 8);
                rig.Generator(2, 8);                 // (2,8) and (3,8), outside
                rig.LineX(3, 8, 9);                  // through the wall at (6,9) to beside the heater
                rig.Line(8, 9);
                if (!powered) rig.Power.SetSwitch(rig.Cell(2, 8), on: false);
                Assume.That(rig.Power.IsPowered(heater), Is.EqualTo(powered));

                rig.Passes(200);
                return rig.Temperature.CellTemp(rig.Cell(9, 9), rig.World.CurrentTick);
            }

            int warm = Run(powered: true);
            int cold = Run(powered: false);
            Assert.That(cold, Is.EqualTo(-1_000), "the control: an unpowered heater warms nothing");
            Assert.That(warm, Is.GreaterThan(cold + 1_000), "a powered heater holds the room well above the outdoors");
        }
    }
}
