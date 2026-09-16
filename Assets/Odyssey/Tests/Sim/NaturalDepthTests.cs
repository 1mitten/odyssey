#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// How deep the board is, and why.
    ///
    /// <para>The ground sits as high as it can while leaving
    /// <see cref="NaturalMapGenDef.headroomLayers"/> of sky above the tallest terrace, and
    /// everything under it is the mine. Before that rule the ground sat two fifths of the way up,
    /// which on the played 120 x 120 x 16 board left exactly two layers of rock between the
    /// subsoil and the bedrock — too thin for the coal band, which starts seven cells down, so
    /// **coal never generated at all**. Nothing caught it, because a map with no coal in it looks
    /// exactly like a map nobody has dug deep enough into yet.</para>
    ///
    /// <para>That is what <see cref="CoalGeneratesOnThePlayedBoard"/> is here to stop happening
    /// again, and it is why the others measure the column rather than trusting the parameter.</para>
    /// </summary>
    public class NaturalDepthTests
    {
        const int Layers = 16;

        static (CellGrid grid, MapGenOutcome outcome, NaturalMapGenDef gen) PlayedBoard(uint seed = 1u)
        {
            var size = new GridSize(120, 120, Layers);
            var grid = new CellGrid(size);
            var gen = NaturalMapGenDef.For(size).MakeWooded();
            return (grid, MapGenerator.Generate(grid, seed, gen), gen);
        }

        [Test]
        public void TheGroundIsAsHighAsTheHeadroomAllows()
        {
            var size = new GridSize(120, 120, Layers);
            var gen = NaturalMapGenDef.For(size);

            // 16 layers, one for the surface itself, three of sky, two of relief above nominal.
            Assert.That(gen.groundLayer, Is.EqualTo(Layers - 1 - gen.headroomLayers - gen.surfaceRelief));
            Assert.That(gen.groundLayer, Is.EqualTo(10), "the played board's ground layer moved");
        }

        [Test]
        public void EveryColumnKeepsItsHeadroomAndItsRock()
        {
            var (grid, outcome, gen) = PlayedBoard();
            var size = grid.Size;
            var report = outcome.Natural!.Report;

            Assert.That(size.SizeY - 1 - report.SurfaceMaxY, Is.GreaterThanOrEqualTo(gen.headroomLayers),
                "the tallest terrace ate into the headroom");

            for (int gz = 0; gz < size.SizeZ; gz++)
            for (int gx = 0; gx < size.SizeX; gx++)
            {
                int rock = 0;
                for (int gy = 0; gy < size.SizeY; gy++)
                {
                    ushort terrain = grid.Terrain[size.Index(gx, gz, gy)];
                    if (terrain == NaturalContent.TerrainRock || NaturalContent.IsOre(terrain)) rock++;
                }

                // Rock is what mining is for. A column with none of it is a column the whole
                // feature is missing from, whatever the surface looks like.
                Assert.That(rock, Is.GreaterThan(0), $"column {gx},{gz} has no rock in it at all");
            }
        }

        [Test]
        public void TheDeepBandIsReachableFromTheSurface()
        {
            var (grid, outcome, _) = PlayedBoard();
            var size = grid.Size;
            var ctx = outcome.Natural!.Context;

            // Coal's band starts seven cells below the local surface. At least somewhere on the
            // board there has to be rock that deep, or the band is decoration.
            int deepest = 0;
            for (int gz = 0; gz < size.SizeZ; gz++)
            for (int gx = 0; gx < size.SizeX; gx++)
            {
                int column = ctx.Column(gx, gz);
                int depth = ctx.SurfaceY[column] - ctx.BedrockTopY[column];
                if (depth > deepest) deepest = depth;
            }

            var coal = NaturalContent.OreAt(1);
            Assert.That(coal.Terrain, Is.EqualTo(NaturalContent.TerrainCoalSeam), "the ore table was reordered");
            Assert.That(deepest, Is.GreaterThanOrEqualTo(coal.MinDepth),
                $"the deepest rock on the board is {deepest} cells down; coal starts at {coal.MinDepth}");
        }

        [Test]
        public void CoalGeneratesOnThePlayedBoard()
        {
            // Across a handful of seeds, not one: a single seed that happens to roll no coal
            // would make this test a coin toss rather than a guarantee.
            int seedsWithCoal = 0;
            for (uint seed = 1; seed <= 5; seed++)
            {
                var (_, outcome, _) = PlayedBoard(seed);
                if (outcome.Natural!.Report.OreCellsByKind[1] > 0) seedsWithCoal++;
            }

            Assert.That(seedsWithCoal, Is.EqualTo(5), "coal failed to generate on a played board");
        }

        [Test]
        public void IronOutweighsCoal()
        {
            // Iron is the shallow find and coal is the reason to keep digging, so the board must
            // carry more of the first than the second. This is the depth banding working, not a
            // tuning preference: coal's band only overlaps the bottom of the rock.
            var (_, outcome, _) = PlayedBoard();
            var report = outcome.Natural!.Report;

            Assert.That(report.OreCellsByKind[0], Is.GreaterThan(0), "no iron on the board");
            Assert.That(report.OreCellsByKind[0], Is.GreaterThan(report.OreCellsByKind[1]),
                "coal is no rarer than iron, so digging deeper buys nothing");
        }

        [Test]
        public void AShallowBoardCompressesRatherThanThrowing()
        {
            // The slice is five layers. The headroom rule asks for a ground layer it cannot have,
            // and the heightfield clamp is what keeps that from being an exception.
            var size = new GridSize(60, 60, 5);
            var grid = new CellGrid(size);
            var gen = NaturalMapGenDef.For(size).MakeWooded();

            Assert.DoesNotThrow(() => MapGenerator.Generate(grid, 1u, gen));
        }
    }
}
