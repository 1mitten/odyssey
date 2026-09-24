#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// **Every part of the landscape is drawn, on the board that is actually played.**
    ///
    /// <para><b>The owner's report, 2026-09-16:</b> on the low ground under the trees "there
    /// appears to be no ground texture or grass". The screenshot showed a flat, untextured,
    /// un-tufted plane with woodland standing on it and a hard diagonal edge where the green
    /// meadow stopped. Nothing was wrong with the ground: it was not being drawn at all, and what
    /// showed through was the sky.</para>
    ///
    /// <para><b>The mechanism.</b> The wooded board is terraced — <c>surfaceRelief</c> of two, so
    /// the outdoor surface spans five layers — while <c>SliceSettings.LowestDrawnLayer</c> stopped
    /// <c>belowDepth</c> (three) layers under the slice. Standing on a high terrace therefore cut
    /// the low ones away, and the trees survived because a tree lives in the air cell one layer
    /// *above* the ground it grows from, which could still be inside the band.</para>
    ///
    /// <para><b>Why it is measured here rather than asserted in a fixture.</b> A fixture proves
    /// the arithmetic and cannot prove that the shape the generator makes is the shape the fixture
    /// assumed — <c>SlicePickerBoardTests</c> exists for the same reason and says so at greater
    /// length. Every case below carries a <b>control</b> run against the band as it was, because a
    /// passing measurement means nothing until the same measurement is seen to fail.</para>
    /// </summary>
    public class LandscapeBandTests
    {
        const int Size = 120;
        const int Layers = 16;

        sealed class Board : IDisposable
        {
            public GridSize Size = default!;
            public WorldRenderModel Model = default!;
            public ModuleLibrary Library = default!;
            public int StartLayer;
            public SliceSettings Slice = default!;

            public void Dispose() => Library?.Dispose();
        }

        static Board Generate(uint seed)
        {
            var size = new GridSize(LandscapeBandTests.Size, LandscapeBandTests.Size, Layers);
            var grid = new CellGrid(size);
            var result = NaturalMapGenerator.Generate(grid, seed, NaturalMapGenDef.For(size).MakeWooded());

            var library = new ModuleLibrary(null);
            var model = new WorldRenderModel(size, new ChunkGrid(size), library);
            model.RefreshAll(grid, result.Context.Edifices);

            // Exactly what OdysseyBootstrap does: the slice opens on the colony's own layer, and
            // that same layer is what followDepth measures "underground" against.
            int start = result.StartCell.Y;
            return new Board
            {
                Size = size,
                Model = model,
                Library = library,
                StartLayer = start,
                Slice = new SliceSettings { surfaceLayer = start, followDepth = true },
            };
        }

        /// <summary>The topmost solid cell of a column, or -1 where the column is empty.</summary>
        static int TopSolid(Board board, int x, int z)
        {
            for (int y = Layers - 1; y >= 0; y--)
                if (board.Model.IsSolid(board.Size.Index(x, z, y))) return y;
            return -1;
        }

        /// <summary>Columns whose surface cell falls below a band starting at <paramref name="lowest"/>.</summary>
        static int SurfacesBelow(Board board, int lowest)
        {
            int cut = 0;
            for (int z = 0; z < Size; z++)
            for (int x = 0; x < Size; x++)
            {
                int top = TopSolid(board, x, z);
                if (top >= 0 && top < lowest) cut++;
            }
            return cut;
        }

        /// <summary>
        /// Standing where the game opens, and one layer up — which is where the owner's screenshot
        /// was taken from, and much the worse case, because the depth budget is measured from the
        /// slice and every step up takes another terrace out of it.
        /// </summary>
        [TestCase(1u)]
        [TestCase(2u)]
        [TestCase(3u)]
        public void NoTerraceIsCutAwayFromTheSurface(uint seed)
        {
            using Board board = Generate(seed);

            for (int step = 0; step <= 1; step++)
            {
                int active = board.StartLayer + step;
                if (active >= Layers) continue;

                int withFloor = Math.Max(0,
                    board.Slice.LowestDrawnLayer(active, board.Model.LowestOutdoorLayer));
                int control = Math.Max(0, board.Slice.LowestDrawnLayer(active));

                int cut = SurfacesBelow(board, withFloor);
                int controlCut = SurfacesBelow(board, control);

                Console.WriteLine(
                    $"[Landscape] seed {seed}, slice L{active}: landscape floor L{board.Model.LowestOutdoorLayer}, " +
                    $"band from L{withFloor} cuts {cut:N0} columns; the depth budget alone " +
                    $"(L{control}) cuts {controlCut:N0} of {Size * Size:N0}.");

                Assert.That(cut, Is.Zero,
                    $"seed {seed}, slice L{active}: part of the landscape is not drawn, and what " +
                    "shows through the gap is the sky");

                // The control has to fail, or a green result above means nothing. One layer up
                // from the start it takes at least a hundredth of the board off every seed, and
                // 6,140 columns of 14,400 off seed 1 — which is the picture the owner sent.
                if (step == 1)
                    Assert.That(controlCut, Is.GreaterThan(Size * Size / 100),
                        $"seed {seed}: the depth budget alone cut almost nothing away, so this " +
                        "measurement proves nothing — the board changed under the test");
            }
        }

        /// <summary>
        /// The floor is measured off the board rather than assumed, so it has to land where the
        /// generator's own terracing lands — one layer below the shallowest column, never below the
        /// deepest rock.
        /// </summary>
        [Test]
        public void TheLandscapeFloorIsTheLowestSurfaceOnTheBoard()
        {
            using Board board = Generate(1u);

            int lowestSurface = Layers;
            for (int z = 0; z < Size; z++)
            for (int x = 0; x < Size; x++)
            {
                int top = TopSolid(board, x, z);
                if (top >= 0 && top < lowestSurface) lowestSurface = top;
            }

            Assert.That(board.Model.LowestOutdoorLayer, Is.EqualTo(lowestSurface));
            Assert.That(board.Model.LowestOutdoorLayer, Is.GreaterThan(0),
                "the floor is the bottom of the landscape, not the bottom of the map");
        }

        /// <summary>
        /// Walls down on the played board (design 42 §3a): the colony opens on the top terrace, and
        /// every lower terrace must count as ground rather than as a tunnel, or the layer above it
        /// is drawn as an x-ray — the see-through building the owner reported on 2026-09-24.
        /// Measured on the generated board rather than asserted from a remembered number.
        /// </summary>
        [Test]
        public void WithTheWallsDownEveryTerraceIsAboveGround()
        {
            using Board board = Generate(1u);
            SliceSettings slice = board.Slice;
            slice.landscapeFloor = board.Model.LowestOutdoorLayer;

            TestContext.WriteLine($"[WallsDown] seed 1: opens on L{board.StartLayer}, " +
                                  $"landscape floor (topmost rock of the lowest column) L{board.Model.LowestOutdoorLayer}");
            Assert.That(board.Model.LowestOutdoorLayer, Is.LessThan(board.StartLayer - 1),
                "the fixture has lower terraces to stand on");

            for (int layer = board.Model.LowestOutdoorLayer + 1; layer < board.StartLayer; layer++)
            {
                slice.wallsLowered = false;
                Assert.That(slice.BelowSurface(layer), Is.True, $"walls up, L{layer}: the rule as it was");
                slice.wallsLowered = true;
                Assert.That(slice.BelowSurface(layer), Is.False, $"walls down, L{layer}: a terrace is ground");
                Assert.That(slice.GhostsAbove(layer), Is.False, $"walls down, L{layer}: nothing above is see-through");
            }

            slice.wallsLowered = true;
            Assert.That(slice.BelowSurface(board.Model.LowestOutdoorLayer), Is.True,
                "beneath the lowest ground is still underground");
        }

        /// <summary>
        /// Underground is untouched. The cap is already off downwards there, and a landscape floor
        /// that lifted it would hide the working the player went down to look at.
        /// </summary>
        [Test]
        public void GoingUndergroundStillDrawsEveryLayerBelow()
        {
            using Board board = Generate(1u);

            int active = board.StartLayer - 2;
            Assert.That(board.Slice.BelowSurface(active), Is.True, "the fixture is not underground");
            Assert.That(board.Slice.LowestDrawnLayer(active, board.Model.LowestOutdoorLayer),
                Is.Zero);
        }
    }
}
