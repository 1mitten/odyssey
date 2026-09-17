#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// Walking along a real shoreline, on a real generated board.
    ///
    /// <para><b>Why not a hand-built fixture.</b> Every other continuity test here builds the world
    /// it measures, which makes the measurement exact and the geometry whatever the test author
    /// thought of. The owner's report is about somewhere nobody authored: "when the colonist gets
    /// out of the water — this is when it gets jittery and snappy as the colonist walks along the
    /// shoreline it just came out of" (2026-09-17). A generated bank is irregular — its cells take
    /// different bank shapes and rotations as the water's edge turns — and a hand-built straight
    /// run of one shape cannot show what happens where two different ones meet. So this generates
    /// the board the game loads and walks the shore it finds.</para>
    ///
    /// <para>Smaller than the played board, because a render mirror of 120 x 120 x 16 in a unit
    /// test is a quarter of a million cells of refresh for a measurement that only needs a stream
    /// with a bank on it.</para>
    /// </summary>
    public class ShorelineWalkTests
    {
        [SetUp]
        public void Reset()
        {
            WaterLine.Reset();
            BankLayout.Reset();
            GroundRelief.Reset();
            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;
        }

        [TearDown]
        public void Restore()
        {
            WaterLine.Reset();
            BankLayout.Reset();
            GroundRelief.Reset();
        }

        static PawnView Walking(CellRef from, CellRef to, int percent) =>
            new PawnView(new PawnId(1), from, 100, 100, 50, -1, to, percent);

        sealed class Board
        {
            public GridSize Size;
            public CellGrid Grid = null!;
            public WorldRenderModel Model = null!;
        }

        static Board Generate(int n = 64, uint seed = 1u)
        {
            var size = new GridSize(n, n, 16);
            var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);
            gen.MakeWooded();

            var grid = new CellGrid(size);
            MapGenOutcome result = MapGenerator.Generate(grid, seed, gen);

            var model = new WorldRenderModel(size, new ChunkGrid(size), new ModuleLibrary(null));
            model.RefreshAll(grid, result.Natural!.Context.Edifices);

            return new Board { Size = size, Grid = grid, Model = model };
        }

        static bool Solid(Board board, int x, int z, int y) =>
            board.Size.Contains(x, z, y) &&
            (board.Grid.Flags[board.Size.Index(x, z, y)] & CellFlags.SolidTerrain) != 0;

        static bool Standable(Board board, CellRef cell) =>
            cell.Y > 0 && board.Size.Contains(cell.X, cell.Z, cell.Y) &&
            !Solid(board, cell.X, cell.Z, cell.Y) &&
            Solid(board, cell.X, cell.Z, cell.Y - 1);

        static bool Water(Board board, CellRef cell) =>
            board.Size.Contains(cell.X, cell.Z, cell.Y) &&
            NaturalContent.IsWater(board.Grid.Terrain[board.Size.Index(cell.X, cell.Z, cell.Y)]);

        /// <summary>
        /// Every standable cell that has water beside it: the shore, as generated.
        /// </summary>
        static List<CellRef> Shore(Board board)
        {
            var shore = new List<CellRef>();

            for (int y = 1; y < board.Size.SizeY; y++)
            for (int z = 1; z < board.Size.SizeZ - 1; z++)
            for (int x = 1; x < board.Size.SizeX - 1; x++)
            {
                var cell = new CellRef(x, z, y);
                if (Water(board, cell) || !Standable(board, cell)) continue;

                bool beside =
                    Water(board, new CellRef(x + 1, z, y)) || Water(board, new CellRef(x - 1, z, y)) ||
                    Water(board, new CellRef(x, z + 1, y)) || Water(board, new CellRef(x, z - 1, y)) ||
                    Water(board, new CellRef(x + 1, z, y - 1)) || Water(board, new CellRef(x - 1, z, y - 1)) ||
                    Water(board, new CellRef(x, z + 1, y - 1)) || Water(board, new CellRef(x, z - 1, y - 1));

                if (beside) shore.Add(cell);
            }

            return shore;
        }

        /// <summary>The worst drawn-height jump between two consecutive samples of one step.</summary>
        static (float worst, float at) WorstJump(WorldRenderModel model, CellRef from, CellRef to)
        {
            const int samples = 200;
            float worst = 0f, at = 0f;
            float previous = PawnPose.Of(Walking(from, from, 0), 0f, 0, out _, model).y;

            for (int i = 0; i <= samples; i++)
            {
                int percent = Mathf.RoundToInt(i * 100f / samples);
                float y = PawnPose.Of(Walking(from, to, percent), 0f, 0, out _, model).y;
                float jump = Mathf.Abs(y - previous);
                if (jump > worst) { worst = jump; at = i / (float)samples; }
                previous = y;
            }

            return (worst, at);
        }

        /// <summary>
        /// The measurement: every step between two neighbouring shore cells on the board, and the
        /// worst drawn-height jump any of them contains.
        /// </summary>
        [Test]
        public void HowFarADrawnFigureJumpsWalkingAlongARealShoreline()
        {
            Board board = Generate();
            List<CellRef> shore = Shore(board);

            Assert.That(shore, Is.Not.Empty, "the generated board has no shoreline to walk along");

            float worstOfAll = 0f, worstAt = 0f;
            CellRef worstFrom = default, worstTo = default;
            int steps = 0;

            foreach (CellRef cell in shore)
            {
                foreach (CellRef next in new[]
                {
                    new CellRef(cell.X + 1, cell.Z, cell.Y),
                    new CellRef(cell.X, cell.Z + 1, cell.Y),
                })
                {
                    if (Water(board, next) || !Standable(board, next)) continue;

                    steps++;
                    (float worst, float at) = WorstJump(board.Model, cell, next);
                    if (worst > worstOfAll)
                    {
                        worstOfAll = worst;
                        worstAt = at;
                        worstFrom = cell;
                        worstTo = next;
                    }
                }
            }

            TestContext.WriteLine(
                $"MEASURED shoreline: {shore.Count} shore cells, {steps} steps along it. " +
                $"Worst drawn-height jump {worstOfAll * 1000f:F1} mm at phase {worstAt:F3}, on " +
                $"({worstFrom.X},{worstFrom.Z},L{worstFrom.Y}) -> ({worstTo.X},{worstTo.Z},L{worstTo.Y}). " +
                $"One frame of walking is 25.0 mm.");

            Assert.That(steps, Is.GreaterThan(20), "too few shore steps for the measurement to mean much");

            // And the shape of the worst one, because "31 mm somewhere in the step" does not say
            // whether it is a step function, a corner, or a fast but honest ramp — and those want
            // three different fixes. Printing the profile is cheaper than three guesses.
            if (worstOfAll > 0f) Profile(board.Model, worstFrom, worstTo, "worst shoreline step");
        }

        /// <summary>
        /// The drawn height across one step, printed, with the per-sample change beside it.
        ///
        /// <para>Only the samples around the largest change, because a hundred lines of height is
        /// not a diagnosis and the ten either side of the jump are.</para>
        /// </summary>
        static void Profile(WorldRenderModel model, CellRef from, CellRef to, string what)
        {
            var heights = new float[101];
            for (int percent = 0; percent <= 100; percent++)
                heights[percent] = PawnPose.Of(Walking(from, to, percent), 0f, 0, out _, model).y;

            int at = 1;
            for (int percent = 2; percent <= 100; percent++)
                if (Mathf.Abs(heights[percent] - heights[percent - 1]) >
                    Mathf.Abs(heights[at] - heights[at - 1])) at = percent;

            var line = new System.Text.StringBuilder();
            line.Append($"PROFILE {what} ({from.X},{from.Z},L{from.Y}) -> ({to.X},{to.Z},L{to.Y}), " +
                        $"biggest change at {at}%:");

            for (int percent = Mathf.Max(0, at - 5); percent <= Mathf.Min(100, at + 5); percent++)
                line.Append($" [{percent}%] {heights[percent]:F4}");

            // The first sample of the NEXT step from the same cell. A jump at the very end of a
            // step is really a disagreement between where this step finishes and where the next
            // one starts, and that is invisible to anything that samples one step at a time.
            float handover = PawnPose.Of(Walking(to, to, 0), 0f, 0, out _, model).y;
            line.Append($" | standing at the destination afterwards: {handover:F4}" +
                        $" (hand-over gap {(handover - heights[100]) * 1000f:F1} mm)");

            TestContext.WriteLine(line.ToString());
        }

        /// <summary>
        /// And the step out of the water itself, everywhere on the board it can be taken.
        /// </summary>
        [Test]
        public void HowFarADrawnFigureJumpsClimbingOutOfRealWater()
        {
            Board board = Generate();

            float worstOfAll = 0f, worstAt = 0f;
            CellRef worstFrom = default, worstTo = default;
            int steps = 0;
            float deepest = 0f;
            var rises = new List<float>();

            for (int y = 1; y < board.Size.SizeY; y++)
            for (int z = 1; z < board.Size.SizeZ - 1; z++)
            for (int x = 1; x < board.Size.SizeX - 1; x++)
            {
                var wet = new CellRef(x, z, y);
                if (!Water(board, wet) || !Standable(board, wet)) continue;

                foreach (CellRef dry in new[]
                {
                    new CellRef(x + 1, z, y), new CellRef(x - 1, z, y),
                    new CellRef(x, z + 1, y), new CellRef(x, z - 1, y),
                    new CellRef(x + 1, z, y + 1), new CellRef(x - 1, z, y + 1),
                    new CellRef(x, z + 1, y + 1), new CellRef(x, z - 1, y + 1),
                })
                {
                    if (Water(board, dry) || !Standable(board, dry)) continue;

                    steps++;
                    (float worst, float at) = WorstJump(board.Model, wet, dry);
                    if (worst > worstOfAll)
                    {
                        worstOfAll = worst;
                        worstAt = at;
                        worstFrom = wet;
                        worstTo = dry;
                    }

                    // How far into the destination's own ground the figure ever is — **and only
                    // while it is horizontally over that cell**, which is the half of the step
                    // where being below its floor means being inside it.
                    //
                    // The first version of this asked from t = 0 and reported 353 cm, which was
                    // the instrument's fault and not the game's: a colonist at the start of the
                    // step is still in the water, and being below the bank it has not reached yet
                    // is what swimming is. A measurement that counts the correct case as the fault
                    // it is looking for cannot tell you whether the fault is there.
                    float top = WaterLine.RestingHeight(board.Model, dry);
                    for (int percent = 50; percent <= 100; percent += 2)
                    {
                        float below = top - PawnPose.Of(Walking(wet, dry, percent), 0f, 0, out _, board.Model).y;
                        if (below > deepest) deepest = below;
                    }

                    rises.Add(Mathf.Abs(top - WaterLine.RestingHeight(board.Model, wet)));
                }
            }

            rises.Sort();
            float median = rises.Count == 0 ? 0f : rises[rises.Count / 2];
            float biggest = rises.Count == 0 ? 0f : rises[rises.Count - 1];

            TestContext.WriteLine(
                $"MEASURED climbing out: {steps} exits. Worst jump {worstOfAll * 1000f:F1} mm at " +
                $"phase {worstAt:F3}, on ({worstFrom.X},{worstFrom.Z},L{worstFrom.Y}) -> " +
                $"({worstTo.X},{worstTo.Z},L{worstTo.Y}). Rise to climb: median {median:F2} m, " +
                $"worst {biggest:F2} m. Deepest into the destination's ground while over it: " +
                $"{deepest * 100f:F0} cm.");

            Assert.That(steps, Is.GreaterThan(0), "no colonist can get out of the water on this board");
        }
    }
}
