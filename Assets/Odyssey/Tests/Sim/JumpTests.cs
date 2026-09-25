#nullable enable
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Jumping a one-cell stream (design 44): the rule, its price, and where it is not allowed.
    ///
    /// <para>The fixture is a stream laid the way the generator lays one, because a hand-built
    /// stream at the walking layer — which is what <c>WaterTests.WadingCostsThreeTimesWalking</c>
    /// uses — has no gap to jump. Solid ground to layer 1, the channel's bed at layer 0 and its water
    /// at layer 1, level with the bank's solid top, so a colonist on the bank stands at layer 2 with
    /// open air over the water beside her. That is <c>NaturalWaterPasses.LowerChannels</c>' shape.</para>
    /// </summary>
    public class JumpTests
    {
        /// <summary>Where a colonist stands on the banks of every fixture here.</summary>
        internal const int Bank = 2;

        /// <summary>The first channel column.</summary>
        internal const int Channel = 5;

        /// <summary>
        /// A stream <paramref name="width"/> cells wide running the whole length of the board, so
        /// there is no way round it.
        /// </summary>
        internal static CellGrid Stream(int width, int sx = 12, int sz = 7, int sy = 4, bool deep = false)
        {
            var size = new GridSize(sx, sz, sy);
            var cells = new CellGrid(size);
            for (int z = 0; z < sz; z++)
            for (int x = 0; x < sx; x++)
            {
                bool wet = x >= Channel && x < Channel + width;
                Set(cells, size.Index(x, z, 0), wet ? NaturalContent.TerrainSand : NaturalContent.TerrainSubsoil);
                Set(cells, size.Index(x, z, 1), wet
                    ? (deep ? NaturalContent.TerrainDeepWater : NaturalContent.TerrainShallowWater)
                    : NaturalContent.TerrainGrass);
            }
            return cells;
        }

        internal static void Set(CellGrid grid, int index, ushort terrain)
        {
            grid.Terrain[index] = terrain;
            if (NaturalContent.IsSolid(terrain)) grid.Flags[index] |= CellFlags.SolidTerrain;
            else grid.Flags[index] &= ~CellFlags.SolidTerrain;
            if (NaturalContent.IsImpassable(terrain)) grid.Flags[index] |= CellFlags.ImpassableTerrain;
            else grid.Flags[index] &= ~CellFlags.ImpassableTerrain;
        }

        static NavGraph Built(CellGrid cells)
        {
            var nav = new NavGraph(cells);
            nav.MarkAllDirty();
            nav.Rebuild();
            return nav;
        }

        static int At(CellGrid cells, int x, int z, int y) => cells.Size.Index(x, z, y);

        // ---------------------------------------------------------------- the rule

        [Test]
        public void AOneCellStreamIsJumpedBankToBank()
        {
            var cells = Stream(1);
            var nav = Built(cells);
            int near = At(cells, Channel - 1, 3, Bank), far = At(cells, Channel + 1, 3, Bank);

            Assert.That(nav.IsJumpAcross(near, far, TraverseMode.Colonist), Is.True);
            Assert.That(nav.IsJumpAcross(far, near, TraverseMode.Colonist), Is.True, "a jump goes both ways");
            Assert.That(nav.IsLegalStep(near, far, TraverseMode.Colonist), Is.True,
                "the planner may plan it but the mover would drop it");

            var finder = new PathFinder(nav);
            PathResult result = finder.FindPath(At(cells, 1, 3, Bank), At(cells, 9, 3, Bank), TraverseMode.Colonist);
            Assert.That(result.Ok, Is.True);
            Assert.That(result.Cost, Is.EqualTo(8 * MoveCost.Orthogonal),
                "eight cells of ground, the jump priced as two of them");

            int[] path = finder.PathToArray();
            for (int i = 0; i < path.Length; i++)
                Assert.That(cells.Size.FromIndex(path[i]).Y, Is.EqualTo(Bank), "the path went into the water");
            Assert.That(path, Does.Contain(near).And.Contain(far));
            Assert.That(finder.ValidatePath(path, TraverseMode.Colonist), Is.True);
        }

        [TestCase(2)]
        [TestCase(3)]
        public void AWiderStreamIsWadedNotJumped(int width)
        {
            var cells = Stream(width);
            var nav = Built(cells);

            Assert.That(nav.IsJumpAcross(At(cells, Channel - 1, 3, Bank), At(cells, Channel + 1, 3, Bank), TraverseMode.Colonist),
                Is.False, "a two-cell step landed over the water");

            var finder = new PathFinder(nav);
            PathResult result = finder.FindPath(At(cells, 1, 3, Bank), At(cells, 9, 3, Bank), TraverseMode.Colonist);
            Assert.That(result.Ok, Is.True, "wading must stay possible");
            bool wet = false;
            foreach (int c in finder.PathToArray()) wet |= cells.Size.FromIndex(c).Y == 1;
            Assert.That(wet, Is.True, "a stream too wide to jump is waded");
        }

        [Test]
        public void BanksOnDifferentLayersAreNotJumped()
        {
            var cells = Stream(1);
            // The far bank a layer higher: a terrace beside the stream.
            for (int z = 0; z < cells.Size.SizeZ; z++)
            for (int x = Channel + 1; x < cells.Size.SizeX; x++)
                Set(cells, At(cells, x, z, Bank), NaturalContent.TerrainGrass);
            var nav = Built(cells);

            int near = At(cells, Channel - 1, 3, Bank);
            Assert.That(nav.IsJumpAcross(near, At(cells, Channel + 1, 3, Bank), TraverseMode.Colonist), Is.False,
                "jumped into a bank");
            Assert.That(nav.IsLegalStep(near, At(cells, Channel + 1, 3, Bank + 1), TraverseMode.Colonist), Is.False,
                "jumped up a layer");
        }

        [Test]
        public void AFloorPouredOverTheWaterRetractsTheJump()
        {
            var cells = Stream(1);
            var nav = Built(cells);
            int near = At(cells, Channel - 1, 3, Bank), far = At(cells, Channel + 1, 3, Bank);
            int gap = At(cells, Channel, 3, Bank);

            cells.Floor[gap] = 1;
            nav.MarkDirty(gap);
            nav.Rebuild();

            Assert.That(nav.IsJumpAcross(near, far, TraverseMode.Colonist), Is.False,
                "there is a floor to walk on now");
            Assert.That(nav.IsJumpAcross(At(cells, Channel - 1, 2, Bank), At(cells, Channel + 1, 2, Bank), TraverseMode.Colonist),
                Is.True, "the row beside the floor still has its jump");
        }

        [Test]
        public void AWallOnTheBankRetractsTheJump()
        {
            var cells = Stream(1);
            var nav = Built(cells);
            int near = At(cells, Channel - 1, 3, Bank), far = At(cells, Channel + 1, 3, Bank);

            cells.Flags[far] |= CellFlags.BlockingEdifice;
            nav.MarkDirty(far);
            nav.Rebuild();

            Assert.That(nav.IsJumpAcross(near, far, TraverseMode.Colonist), Is.False);
            Assert.That(nav.IsLegalStep(near, far, TraverseMode.Colonist), Is.False);
        }

        [Test]
        public void NothingJumpsATrenchWithNoWaterInIt()
        {
            var cells = Stream(1);
            for (int z = 0; z < cells.Size.SizeZ; z++)
                Set(cells, At(cells, Channel, z, 1), NaturalContent.TerrainAir);
            var nav = Built(cells);

            Assert.That(nav.IsJumpAcross(At(cells, Channel - 1, 3, Bank), At(cells, Channel + 1, 3, Bank), TraverseMode.Colonist),
                Is.False, "a hole is not a stream");
        }

        [Test]
        public void ADeepChannelIsNotJumped()
        {
            var cells = Stream(1, deep: true);
            var nav = Built(cells);

            Assert.That(nav.IsJumpAcross(At(cells, Channel - 1, 3, Bank), At(cells, Channel + 1, 3, Bank), TraverseMode.Colonist),
                Is.False, "a jump that falls short has to land somewhere a person can stand");
        }

        [Test]
        public void PeopleJumpAndAnimalsDoNot()
        {
            var cells = Stream(1);
            var nav = Built(cells);
            int near = At(cells, Channel - 1, 3, Bank), far = At(cells, Channel + 1, 3, Bank);

            foreach (TraverseMode person in new[] { TraverseMode.Colonist, TraverseMode.Hauler, TraverseMode.Bandit })
            {
                Assert.That(nav.IsJumpAcross(near, far, person), Is.True, $"{person} did not jump");
                Assert.That(nav.Reachable(At(cells, 1, 3, Bank), At(cells, 9, 3, Bank), person), Is.True,
                    $"{person}'s districts do not cross the stream");
            }

            foreach (TraverseMode animal in new[] { TraverseMode.Animal, TraverseMode.Climber })
            {
                Assert.That(nav.IsJumpAcross(near, far, animal), Is.False, $"{animal} jumped");
                Assert.That(nav.Reachable(At(cells, 1, 3, Bank), At(cells, 9, 3, Bank), animal), Is.False,
                    $"a jump joined {animal}'s districts across water it cannot enter");
            }
        }

        // ---------------------------------------------------------------- the price

        [Test]
        public void TheJumpIsCheaperThanWadingAndNoCheaperThanWalking()
        {
            int wade = NavGraph.HopCost(up: false) + NavGraph.HopCost(up: true);
            Assert.That(NavGraph.JumpCost(), Is.LessThan(wade), "a colonist offered both would wade");

            // Not a taste: the cell search's heuristic estimates one Orthogonal a cell, so a
            // two-cell step under two of them makes it inadmissible wherever one is used.
            Assert.That(NavGraph.JumpCost(), Is.GreaterThanOrEqualTo(2 * MoveCost.Orthogonal),
                "the jump is cheaper than the heuristic's estimate of two cells");
        }

        /// <summary>
        /// The region graph's edge carries the owner's number, measured off a built graph — the
        /// jump's copy of <c>HopPriceHasOneOwnerTests.TheRegionGraphPricesAHopAtTheOwnersPrice</c>.
        /// </summary>
        [Test]
        public void TheRegionGraphPricesAJumpAtTheOwnersPrice()
        {
            var cells = Stream(1);
            var nav = Built(cells);
            int from = nav.RegionOfCell(At(cells, Channel - 1, 3, Bank));
            int to = nav.RegionOfCell(At(cells, Channel + 1, 3, Bank));
            Assert.That(to, Is.Not.EqualTo(from), "the fixture is wrong: the two banks share a region");

            int found = -1;
            int start = nav.AdjacencyStart(from);
            for (int i = 0; i < nav.AdjacencyCount(from); i++)
            {
                int link = nav.AdjacencyLink(start + i);
                if (nav.KindOfLink(link) != LinkKind.Jump || nav.LinkOther(link, from) != to) continue;
                found = link;
                break;
            }

            Assert.That(found, Is.Not.EqualTo(-1), "no jump edge was built between the two banks");
            Assert.That(nav.LinkCostFrom(found, from), Is.EqualTo(NavGraph.JumpCost()));
            Assert.That(nav.LinkCostFrom(found, to), Is.EqualTo(NavGraph.JumpCost()));
            Assert.That(nav.LinkModeMask(found), Is.EqualTo(NavGraph.JumpMask));
        }

        // ---------------------------------------------------------------- the rebuild

        /// <summary>
        /// The ownership argument of design 44 §5, proved rather than argued: a jump link is owned by
        /// the block holding its near end and the dirty radius was not widened, so random edits to
        /// ground, floors and water must leave the incremental graph identical to one built from
        /// scratch. The sibling of <c>PathingTests.IncrementalRebuildMatchesAFullRebuildOverRandomisedEdits</c>,
        /// which knows no water.
        /// </summary>
        [Test]
        public void IncrementalRebuildMatchesAFullRebuildWithStreamsInIt()
        {
            var size = new GridSize(23, 19, 4); // not multiples of 10: clipped blocks too
            var cells = new CellGrid(size);
            var rng = new DeterministicRandom(4343u);

            for (int z = 0; z < size.SizeZ; z++)
            for (int x = 0; x < size.SizeX; x++)
            {
                Set(cells, size.Index(x, z, 0), NaturalContent.TerrainSubsoil);
                Set(cells, size.Index(x, z, 1), Surface(rng.NextInt(100)));
            }

            var nav = Built(cells);
            int roundsWithJumps = 0;

            for (int round = 0; round < 40; round++)
            {
                for (int edit = 0; edit < 6; edit++)
                {
                    int x = rng.NextInt(size.SizeX), z = rng.NextInt(size.SizeZ), y = 1 + rng.NextInt(2);
                    int c = size.Index(x, z, y);
                    int choice = rng.NextInt(10);
                    if (choice < 6) Set(cells, c, Surface(rng.NextInt(100)));
                    else cells.Floor[c] = cells.Floor[c] != 0 ? (ushort)0 : (ushort)1;
                    nav.MarkDirty(c);
                }

                nav.Rebuild();
                NavGraph fresh = Built(cells);

                Assert.That(nav.StructureFingerprint(), Is.EqualTo(fresh.StructureFingerprint()),
                    $"round {round}: incremental rebuild diverged from a rebuild from scratch");
                Assert.That(nav.LiveLinkCount(), Is.EqualTo(fresh.LiveLinkCount()),
                    $"round {round}: link count diverged — a stale jump outlived its edit");

                if (CountJumps(nav, size) > 0) roundsWithJumps++;
            }

            Assert.That(roundsWithJumps, Is.GreaterThan(10), "the fixture stopped exercising jumps");

            static ushort Surface(int roll) =>
                roll < 60 ? NaturalContent.TerrainGrass
                : roll < 85 ? NaturalContent.TerrainShallowWater
                : NaturalContent.TerrainAir;
        }

        /// <summary>
        /// How many one-cell crossings the board the game loads actually has (design 44 §8). A
        /// measurement as much as a test: if the played board had none, no golden could move, no
        /// colonist would ever jump in play, and the owner would find nothing to try.
        /// </summary>
        [TestCase(1u)]
        [TestCase(2u)]
        [TestCase(3u)]
        public void ThePlayedBoardHasStreamsNarrowEnoughToJump(uint seed)
        {
            var size = new GridSize(120, 120, 16);
            CellGrid cells = PlayedMap.Generate(size, seed);
            var nav = new NavGraph(cells);
            nav.MarkAllDirty();
            nav.Rebuild();

            int jumps = 0, wet = 0;
            for (int y = 0; y < size.SizeY; y++)
            for (int z = 0; z < size.SizeZ; z++)
            for (int x = 0; x < size.SizeX; x++)
            {
                int c = size.Index(x, z, y);
                if (nav.Grid.CostClass[c] == NaturalContent.CostClassShallowWater && (nav.Grid.Flags[c] & NavFlags.Walkable) != 0) wet++;
                if (x + 2 < size.SizeX && nav.IsJumpAcross(c, size.Index(x + 2, z, y), TraverseMode.Colonist)) jumps++;
                if (z + 2 < size.SizeZ && nav.IsJumpAcross(c, size.Index(x, z + 2, y), TraverseMode.Colonist)) jumps++;
            }

            TestContext.Out.WriteLine($"MEASURED seed {seed}: {jumps} one-cell crossings over {wet} wadeable cells");
            Assert.That(wet, Is.GreaterThan(0), "the played board grew no water");
            Assert.That(jumps, Is.GreaterThan(0), "the played board has water and nowhere narrow enough to jump it");
        }

        static int CountJumps(NavGraph nav, GridSize size)
        {
            int count = 0;
            for (int y = 0; y < size.SizeY; y++)
            for (int z = 0; z < size.SizeZ; z++)
            for (int x = 0; x + 2 < size.SizeX; x++)
                if (nav.IsJumpAcross(size.Index(x, z, y), size.Index(x + 2, z, y), TraverseMode.Colonist)) count++;
            return count;
        }
    }

    /// <summary>
    /// A colonist and her jump, through the game's own composition root (design 44 §6): the
    /// barren board with one channel carved straight across it, a drafted colonist ordered to the
    /// far side, and the roll forced each way — by the debug switch for a fall, and by a
    /// replaced movement Def for a clean jump, because a test that waited on a one-in-thirty
    /// roll would be a test of the seed.
    /// </summary>
    public class JumpColonyTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 16);

        sealed class Crossing
        {
            public readonly ColonyWorld Colony;
            public readonly Pawn Pawn;
            public readonly int Layer;
            public readonly int Near, Far, Water, Goal;

            public Crossing(ColonyWorld colony, Pawn pawn, int layer, int near, int far, int water, int goal)
            {
                Colony = colony; Pawn = pawn; Layer = layer; Near = near; Far = far; Water = water; Goal = goal;
            }
        }

        static ColonyWorld Board(uint seed = 7u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 1;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: false);
        }

        static IntentRejection Send(ColonyWorld colony, Intent intent)
        {
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(intent);
            colony.World.Tick();
            var rejected = colony.World.Intents.Rejected;
            return rejected.Count == 0 ? IntentRejection.None : rejected[0].Reason;
        }

        /// <summary>
        /// The colonist drafted and standing still, a one-cell channel cut straight across the
        /// board three cells east of her — water in the cell level with the ground's top, sand under
        /// it, the generator's shape — and the far bank four cells past it as the goal.
        /// </summary>
        static Crossing Build(uint seed = 7u, ColonyWorld? into = null)
        {
            ColonyWorld colony = into ?? Board(seed);
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Assert.That(Send(colony, new Intent(IntentKind.SetDrafted, default, pawn.Id.Value, 1)), Is.EqualTo(IntentRejection.None));
            for (int t = 0; t < 400 && pawn.HasPath; t++) colony.World.Tick();

            CellRef at = Size.FromIndex(pawn.Cell);
            int x = at.X + 3;
            Assume.That(x + 5, Is.LessThan(Size.SizeX), "the colonist spawned too near the east edge");
            CellGrid cells = colony.Pawns.Cells;
            for (int z = 0; z < Size.SizeZ; z++)
            {
                for (int y = 0; y < at.Y - 1; y++) JumpTests.Set(cells, Size.Index(x, z, y), NaturalContent.TerrainSand);
                JumpTests.Set(cells, Size.Index(x, z, at.Y - 1), NaturalContent.TerrainShallowWater);
                colony.Pawns.Nav.MarkDirty(Size.Index(x, z, at.Y - 1));
            }
            colony.Pawns.Nav.Rebuild();

            int near = Size.Index(x - 1, at.Z, at.Y), far = Size.Index(x + 1, at.Z, at.Y);
            Assume.That(colony.Pawns.Nav.IsJumpAcross(near, far, TraverseMode.Colonist), Is.True,
                "the board under the colonist is not flat enough for the fixture");
            return new Crossing(colony, pawn, at.Y, near, far, Size.Index(x, at.Z, at.Y - 1), Size.Index(x + 5, at.Z, at.Y));
        }

        /// <summary>A copy of the movement Def with another failure rate, swapped in rather than
        /// written through: the Def is shared by every record in the process.</summary>
        static void FailChance(ColonyWorld colony, int perMille)
        {
            MovementDef was = colony.Pawns.Content.Movement;
            var copy = new MovementDef();
            foreach (FieldInfo f in typeof(MovementDef).GetFields(BindingFlags.Public | BindingFlags.Instance))
                f.SetValue(copy, f.GetValue(was));
            copy.jumpFailPerMille = perMille;
            colony.Pawns.Content.Movement = copy;
        }

        static IntentRejection Order(Crossing c) =>
            Send(c.Colony, new Intent(IntentKind.OrderMove, Size.FromIndex(c.Goal), c.Pawn.Id.Value));

        static ulong Hash(ColonyWorld colony) => colony.World.ComputeStateHash().Value;

        /// <summary>Every cell she stood in, in order, until she stands at the goal.</summary>
        static List<int> Walk(Crossing c, int ticks = 3_000)
        {
            var stood = new List<int> { c.Pawn.Cell };
            for (int t = 0; t < ticks && c.Pawn.Cell != c.Goal; t++)
            {
                c.Colony.World.Tick();
                if (c.Pawn.Cell != stood[stood.Count - 1]) stood.Add(c.Pawn.Cell);
            }
            return stood;
        }

        [Test]
        public void AColonistJumpsTheStreamAndStaysDry()
        {
            Crossing c = Build();
            FailChance(c.Colony, 0);
            Assert.That(Order(c), Is.EqualTo(IntentRejection.None));

            List<int> stood = Walk(c);
            Assert.That(c.Pawn.Cell, Is.EqualTo(c.Goal), "she never arrived");
            Assert.That(stood, Does.Not.Contain(c.Water), "she went into the water");
            int i = stood.IndexOf(c.Near);
            Assert.That(i, Is.GreaterThanOrEqualTo(0));
            Assert.That(stood[i + 1], Is.EqualTo(c.Far), "the step off the near bank did not land on the far one");
        }

        [Test]
        public void AFailedJumpLandsInTheWaterAtTheJumpsPriceAndClimbsOutOnTheFarSide()
        {
            Crossing c = Build();
            Assert.That(Send(c.Colony, new Intent(IntentKind.DebugJumpsFail, default, 1)), Is.EqualTo(IntentRejection.None));
            Assert.That(Order(c), Is.EqualTo(IntentRejection.None));

            // Measured in ticks from take-off to the splash: a jump's price, not a drop's.
            int inTheAir = 0;
            var stood = new List<int> { c.Pawn.Cell };
            for (int t = 0; t < 3_000 && c.Pawn.Cell != c.Goal; t++)
            {
                if (c.Pawn.JumpLanding == c.Water) inTheAir++;
                c.Colony.World.Tick();
                if (c.Pawn.Cell != stood[stood.Count - 1]) stood.Add(c.Pawn.Cell);
            }

            Assert.That(c.Pawn.Cell, Is.EqualTo(c.Goal), "she never climbed out and arrived");
            int i = stood.IndexOf(c.Near);
            Assert.That(stood[i + 1], Is.EqualTo(c.Water), "the short jump did not land in the water");
            Assert.That(stood[i + 2], Is.EqualTo(c.Far), "she did not climb out on the far side");

            int rate = c.Pawn.MoveRatePerMille();
            int jumpTicks = NavGraph.JumpCost() * Rates.Scale / rate;
            int dropTicks = NavGraph.HopCost(up: false) * Rates.Scale / rate;
            Assert.That(inTheAir, Is.GreaterThanOrEqualTo(jumpTicks - 2),
                $"a short jump took {inTheAir} ticks: a drop's {dropTicks}, not a jump's {jumpTicks}");
        }

        [Test]
        public void TheDebugSwitchSaysWhenItChangedNothing()
        {
            Crossing c = Build();
            Assert.That(Send(c.Colony, new Intent(IntentKind.DebugJumpsFail, default, 0)), Is.EqualTo(IntentRejection.AlreadyInThatState));
            Assert.That(Send(c.Colony, new Intent(IntentKind.DebugJumpsFail, default, 1)), Is.EqualTo(IntentRejection.None));
            Assert.That(c.Colony.Pawns.DebugJumpsAlwaysFail, Is.True);
            Assert.That(PausedIntents.AppliesWhilePaused(IntentKind.DebugJumpsFail), Is.True, "a menu switch has to work while paused");
        }

        [Test]
        public void AJumpsOutcomeIsTheSameInARunAndItsLockstepTwin()
        {
            Crossing a = Build(), b = Build();
            FailChance(a.Colony, 500);
            FailChance(b.Colony, 500);
            Order(a);
            Order(b);
            for (int t = 0; t < 1_500; t++)
            {
                a.Colony.World.Tick();
                b.Colony.World.Tick();
                Assert.That(b.Pawn.JumpLanding, Is.EqualTo(a.Pawn.JumpLanding), $"tick {t}: the twins rolled differently");
            }
            Assert.That(Hash(b.Colony), Is.EqualTo(Hash(a.Colony)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ASaveTakenMidJumpResumesTheSameJumpAndNeverRollsAgain(bool fallsShort)
        {
            Crossing c = Build();
            if (fallsShort) Send(c.Colony, new Intent(IntentKind.DebugJumpsFail, default, 1));
            else FailChance(c.Colony, 0);
            Order(c);

            for (int t = 0; t < 2_000 && !(c.Pawn.JumpLanding >= 0 && c.Pawn.MoveProgress > 0); t++)
                c.Colony.World.Tick();
            int landing = c.Pawn.JumpLanding;
            Assume.That(landing, Is.EqualTo(fallsShort ? c.Water : c.Far), "never saved mid-jump");

            ColonyWorld restored = Board();
            restored.Load(c.Colony.Save());
            Pawn back = restored.Pawns.Pawns.Get(c.Pawn.Id)!;
            Assert.That(back.JumpLanding, Is.EqualTo(landing), "the landing did not survive the load");
            Assert.That(Hash(restored), Is.EqualTo(Hash(c.Colony)), "differs immediately after loading");

            // The loaded world is told to fail every jump. If the jump in the air were rolled
            // again, a clean one would now fall short.
            // Both worlds are ticked together from here, so the fall's case can compare them.
            restored.Pawns.DebugJumpsAlwaysFail = true;
            int before = back.Cell;
            for (int t = 0; t < 400 && back.Cell == before; t++) { c.Colony.World.Tick(); restored.World.Tick(); }
            Assert.That(back.Cell, Is.EqualTo(landing), "the resumed jump landed somewhere else");

            if (!fallsShort) return;
            // A fall resumes identically all the way out: the switch has nothing left to roll.
            for (int t = 0; t < 1_200; t++) { c.Colony.World.Tick(); restored.World.Tick(); }
            Assert.That(Hash(restored), Is.EqualTo(Hash(c.Colony)), "the worlds parted after the load");
        }

        [Test]
        public void AnOrderGivenMidJumpLandsTheSameJump()
        {
            Crossing c = Build();
            Send(c.Colony, new Intent(IntentKind.DebugJumpsFail, default, 1));
            Order(c);
            for (int t = 0; t < 2_000 && !(c.Pawn.JumpLanding >= 0 && c.Pawn.MoveProgress > 0); t++)
                c.Colony.World.Tick();
            Assume.That(c.Pawn.JumpLanding, Is.EqualTo(c.Water));

            // Sent back where she came from, in the air: the jump she is in lands where it was
            // rolled to, and is not taken off the bank a second time.
            Send(c.Colony, new Intent(IntentKind.OrderMove, Size.FromIndex(c.Near), c.Pawn.Id.Value));
            Assert.That(c.Pawn.JumpLanding, Is.EqualTo(c.Water), "the order dropped the landing");
            int before = c.Pawn.Cell;
            for (int t = 0; t < 400 && c.Pawn.Cell == before; t++) c.Colony.World.Tick();
            Assert.That(c.Pawn.Cell, Is.EqualTo(c.Water));
        }

        [Test]
        public void TheShortJumpIsPublishedForTheDrawing()
        {
            Crossing c = Build();
            Send(c.Colony, new Intent(IntentKind.DebugJumpsFail, default, 1));
            Order(c);
            bool seen = false;
            for (int t = 0; t < 2_000 && c.Pawn.Cell != c.Water; t++)
            {
                c.Colony.World.Tick();
                if (c.Colony.World.Views.Current.TryGetPawn(c.Pawn.Id, out PawnView view) && view.JumpingShort)
                {
                    seen = true;
                    Assert.That(view.NextCell, Is.EqualTo(Size.FromIndex(c.Water)));
                }
            }
            Assert.That(seen, Is.True, "a short jump was never published");
        }

        // ---------------------------------------------------------------- the odds

        [Test]
        public void CarryingAndHungerMakeAJumpLikelierToFail()
        {
            var def = new MovementDef();
            int well = MovementSystem.JumpFailPerMille(def, carrying: false, 1_000);
            Assert.That(well, Is.EqualTo(def.jumpFailPerMille), "a well, unladen colonist fails at the base rate");
            Assert.That(MovementSystem.JumpFailPerMille(def, carrying: true, 1_000), Is.EqualTo(2 * well));
            Assert.That(MovementSystem.JumpFailPerMille(def, carrying: false, Pawn.ConditionFloorPerMille),
                Is.EqualTo(well * 1_000 / Pawn.ConditionFloorPerMille), "condition divides the chance");
            Assert.That(MovementSystem.JumpFailPerMille(def, carrying: true, Pawn.ConditionFloorPerMille),
                Is.GreaterThan(MovementSystem.JumpFailPerMille(def, carrying: true, 1_000)));

            var reckless = new MovementDef { jumpFailPerMille = 900 };
            Assert.That(MovementSystem.JumpFailPerMille(reckless, carrying: true, 700), Is.EqualTo(1_000), "clamped");
        }

        [Test]
        public void TheRollFailsAtTheRateItIsGiven()
        {
            int fails = 0, n = 20_000;
            for (int i = 0; i < n; i++)
                if (MovementSystem.FallsShort(12_345u, 1_000 + i, 1 + (i % 7), 30)) fails++;
            Assert.That(fails, Is.InRange(n * 30 / 1_000 - 150, n * 30 / 1_000 + 150),
                "thirty in a thousand did not come out near thirty in a thousand");
            Assert.That(MovementSystem.FallsShort(1u, 1, 1, 0), Is.False);
            Assert.That(MovementSystem.FallsShort(1u, 1, 1, 1_000), Is.True);
        }
    }
}
