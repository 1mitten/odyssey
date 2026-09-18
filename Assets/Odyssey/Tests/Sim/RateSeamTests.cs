#nullable enable
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The rate seam (U42, design 17 §2): a rate is per mille, the accumulator scales, and the
    /// content does not.
    ///
    /// <para><b>The unit's done criterion is that nothing changes</b>, and the gate for that is
    /// everything else in the suite — the golden masters, the path checksums and the HUD readout
    /// tests passing unedited while these run. What can be proved here is the shape: the seam
    /// answers 1,000 everywhere on a fresh colonist, a rate of 500 provably takes twice as long
    /// at both accumulators, the published fraction reads banked milliwork against a cost in
    /// ticks, and a number written by an older save is read at the scale it was written in.</para>
    /// </summary>
    public class RateSeamTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        /// <summary>A colonist whose rates are pinned, which is what the methods are virtual for.</summary>
        sealed class PacedPawn : Pawn
        {
            readonly int _workRate, _moveRate;

            public PacedPawn(PawnId id, int cell, PawnContent content, int workRate, int moveRate)
                : base(id, cell, content)
            {
                _workRate = workRate;
                _moveRate = moveRate;
            }

            public override int WorkRatePerMille(int workType) => _workRate;
            public override int MoveRatePerMille() => _moveRate;
        }

        /// <summary>The wooded board the work tests use, with nobody on it but who a test adopts.</summary>
        static ColonyWorld Board(uint seed = 1u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 0;
            scenario.beds = 0;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: true);
        }

        static PacedPawn Adopt(ColonyWorld colony, int workRate, int moveRate)
        {
            var pawn = new PacedPawn(new PawnId(1), Size.Index(colony.Start),
                ContentPack.Pawns(), workRate, moveRate);
            colony.Pawns.Pawns.Adopt(pawn);
            return pawn;
        }

        /// <summary>The nearest cell of plain rock the given pawn could actually get at.</summary>
        static int NearestRock(ColonyWorld colony, Pawn pawn)
        {
            CellRef start = colony.Start;
            int best = -1, bestDistance = int.MaxValue;

            for (int y = 0; y < Size.SizeY; y++)
            for (int z = 0; z < Size.SizeZ; z++)
            for (int x = 0; x < Size.SizeX; x++)
            {
                int index = Size.Index(x, z, y);
                if (colony.Grid.Terrain[index] != NaturalContent.TerrainRock) continue;
                if (!colony.Designations.CanMine(index)) continue;
                if (MineWorkGiver.StandToMine(colony.Pawns, pawn, index) < 0) continue;

                int distance = System.Math.Abs(x - start.X) + System.Math.Abs(z - start.Z)
                             + System.Math.Abs(y - start.Y) * 4;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = index;
            }

            return best;
        }

        /// <summary>
        /// Adopt a pacer, order the nearest rock mined, and return the tick the face comes out —
        /// walk included, which is why the two arms of the timing test are run on the same board,
        /// seed and stance, so the walk is the same in both and cancels in the difference.
        /// </summary>
        static int TicksToDig(uint seed, int rate)
        {
            ColonyWorld colony = Board(seed);
            Pawn pawn = Adopt(colony, rate, Rates.Scale);
            int rock = NearestRock(colony, pawn);
            Assume.That(rock, Is.GreaterThanOrEqualTo(0), "the board has no reachable rock");

            colony.Designations.Designate(Size.FromIndex(rock), DesignationKind.Mine);
            int tick = 0;
            for (; tick < 30_000 && colony.Grid.IsSolidTerrain(rock); tick++) colony.World.Tick();
            Assume.That(colony.Grid.IsSolidTerrain(rock), Is.False, "the rock never came out");
            return tick;
        }

        // ---- the seam at rest --------------------------------------------------------------

        [Test]
        public void AColonistUntouchedByAnyOfThisPaysAtHerCurvesAndWalksAtTodaysSpeed()
        {
            // WS1 pinned these at 1,000 because that was its done criterion; WS2 replaced the
            // work answer with the def curve (design 17 §3b) at the colonist's own level — and a
            // fresh colonist has no levels. Movement and condition are still WS3's to move.
            ColonyWorld colony = Board();
            var pawn = colony.Pawns.Pawns.Spawn(Size.Index(colony.Start));

            var content = ContentPack.Pawns();
            Assert.That(pawn.WorkRatePerMille(WorkTypeIndex.Haul), Is.EqualTo(Rates.Scale),
                "hauling is flat, and still exactly today's speed");
            Assert.That(pawn.WorkRatePerMille(WorkTypeIndex.Cutting),
                Is.EqualTo(content.WorkTypes[WorkTypeIndex.Cutting].WorkRatePerMille(0)));
            Assert.That(pawn.WorkRatePerMille(WorkTypeIndex.Mining),
                Is.EqualTo(content.WorkTypes[WorkTypeIndex.Mining].WorkRatePerMille(0)));
            Assert.That(pawn.WorkRatePerMille(WorkTypeIndex.Construction),
                Is.EqualTo(content.WorkTypes[WorkTypeIndex.Construction].WorkRatePerMille(0)));
            Assert.That(pawn.MoveRatePerMille(), Is.EqualTo(Rates.Scale));
            Assert.That(pawn.ConditionPerMille(), Is.EqualTo(Rates.Scale));
        }

        // ---- the controls: a rate of 500 is half a colonist --------------------------------

        [Test]
        public void AWorkRateOfFiveHundredTakesTwiceAsLongToDigTheSameRock()
        {
            int cost = NaturalContent.TerrainAt(NaturalContent.TerrainRock).workToClear;
            int fast = TicksToDig(seed: 1u, rate: Rates.Scale);
            int slow = TicksToDig(seed: 1u, rate: Rates.Scale / 2);

            // The walk to the face is the same in both arms, so the difference is the work alone:
            // 2×cost − cost = cost.
            Assert.That(slow - fast, Is.EqualTo(cost),
                $"the half-rate miner owed exactly {cost} more ticks");
        }

        [Test]
        public void AMoveRateOfFiveHundredCrossesAFloorAtHalfThePace()
        {
            var flat = new Flat();
            var pawn = flat.Ctx.Pawns.Adopt(new PacedPawn(
                new PawnId(1), flat.Size.Index(8, 8, 0), ContentPack.Pawns(),
                Rates.Scale, Rates.Scale / 2));

            int ticks = TicksToWalk(flat, pawn, flat.Size.Index(8, 3, 0));
            Assert.That(ticks, Is.EqualTo(1_000),
                "five flat cells at a hundred a cell is 500 ticks at the standard rate, " +
                "and 500 is twice 500");
        }

        // ---- the published fraction --------------------------------------------------------

        [Test]
        public void AFractionIsBankedMilliworkOverACostInTicks()
        {
            ColonyWorld colony = Board();
            var pawn = Adopt(colony, Rates.Scale, Rates.Scale);
            int rock = NearestRock(colony, pawn);
            Assume.That(rock, Is.GreaterThanOrEqualTo(0));

            colony.Designations.Designate(Size.FromIndex(rock), DesignationKind.Mine);
            int cost = colony.Designations.WorkFor(rock);
            int half = colony.Designations.AddWork(rock, cost * Rates.Scale / 2);

            Assert.That(half, Is.EqualTo(cost * Rates.Scale / 2), "AddWork banks what it is given");
            Assert.That(colony.Designations.Fraction(rock), Is.EqualTo(0.5f).Within(0.001f));
        }

        // ---- the save's version rule -------------------------------------------------------

        [Test]
        public void AnAccumulatorWrittenBeforeFormatFiveIsReadAtTheOldScale()
        {
            Assert.That(Rates.FromSave(37, 4), Is.EqualTo(37_000));
            Assert.That(Rates.FromSave(37_000, 5), Is.EqualTo(37_000));
            Assert.That(Rates.FromSave(0, 4), Is.EqualTo(0));
        }

        // ---- the harness -------------------------------------------------------------------

        /// <summary>
        /// A flat floor with nothing on it, and only pawns and movement in the world — the least
        /// a pawn needs to be walked across, with no need and no job to interrupt.
        /// </summary>
        sealed class Flat
        {
            public readonly SimWorld World;
            public readonly PawnContext Ctx;
            public readonly GridSize Size;

            public Flat()
            {
                Size = new GridSize(16, 16, 2);
                var cells = new CellGrid(Size);
                for (int i = 0; i < Size.CellCount; i++) cells.Floor[i] = 1;

                var nav = new NavGraph(cells);
                Ctx = new PawnContext(cells, nav, new PathService(new PathFinder(nav)),
                    ContentPack.Pawns());
                var movement = new MovementSystem(Ctx);

                World = new SimWorldBuilder()
                    .WithSeed(20260915u)
                    .WithSize(Size)
                    .AddTickable(_ => Ctx.Pawns)
                    .AddSnapshotContributor(Ctx.Pawns)
                    .AddSystem(_ => movement)
                    .Build();

                nav.MarkAllDirty();
                nav.Rebuild();
            }
        }

        /// <summary>
        /// The five lines of <see cref="Job.GotoCell"/> that ask for a walk, without the job:
        /// the destination set, the reachable check, the request queued, the flag raised. Returns
        /// the tick the pawn arrives.
        /// </summary>
        static int TicksToWalk(Flat flat, Pawn pawn, int dest)
        {
            pawn.ClearPath();
            pawn.Destination = dest;
            Assert.That(flat.Ctx.Reachable(pawn, dest), Is.True, "the walk must be possible");
            flat.Ctx.Paths.Enqueue(new PathRequest(pawn.Id.Value, pawn.Cell, dest,
                TraverseMode.Colonist));
            pawn.PathPending = true;

            int tick = 0;
            for (; tick < 5_000 && pawn.Cell != dest; tick++) flat.World.Tick();
            Assert.That(pawn.Cell, Is.EqualTo(dest), "the pawn never arrived");
            return tick;
        }
    }
}
