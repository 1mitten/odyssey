#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The cell grid as a save section (OQ-08), palette-encoded and bit-packed per 25 × 25 × 5
    /// chunk per d-06-save-load.md.
    ///
    /// Two properties carry the weight. A round trip must reproduce the grid exactly, which the
    /// grid hash answers in one number. And saving the same state twice must produce identical
    /// bytes, which is the cheap detector for unordered iteration — a palette built from a
    /// dictionary walk would pass the first test and fail this one.
    /// </summary>
    public class GridSaveTests
    {
        static readonly GridSize Small = new GridSize(60, 60, 5);

        /// <summary>250 × 250 × 40: exactly 10 × 10 × 8 save chunks, which is the no-padding case.</summary>
        static readonly GridSize ScaleTarget = GridSize.ScaleTarget;

        static SimWorld WorldFor(GridSize size, uint seed = 1) =>
            new SimWorldBuilder().WithSeed(seed).WithSize(size).Build();

        static byte[] SaveOf(CellGrid grid, uint seed = 1)
        {
            var world = WorldFor(grid.Size, seed);
            using var stream = new MemoryStream();
            WorldSave.Save(world, stream, new ISaveable[] { new GridSaveSection(grid) });
            return stream.ToArray();
        }

        static CellGrid LoadInto(GridSize size, byte[] bytes, uint seed = 1)
        {
            var grid = new CellGrid(size);
            var world = WorldFor(size, seed);
            using var stream = new MemoryStream(bytes, writable: false);
            WorldSave.Load(world, stream, new ISaveable[] { new GridSaveSection(grid) });
            return grid;
        }

        static ulong HashOf(CellGrid grid)
        {
            var hash = StateHash.New();
            grid.ContributeTo(ref hash);
            return hash.Value;
        }

        static CellGrid City(GridSize size, uint seed = 4242)
        {
            var grid = new CellGrid(size);
            WorldGenerator.Generate(grid, seed, MapGenDef.For(size));
            return grid;
        }

        /// <summary>A natural board as the game builds it — see <see cref="PlayedMap"/>.</summary>
        static CellGrid Natural(GridSize size, uint seed = 4242) => PlayedMap.Generate(size, seed);

        // ---------------------------------------------------------------- the round trip

        [Test]
        public void AnEmptyGridRoundTrips()
        {
            // Every chunk uniform, which is the encoding's best case and its easiest bug: a
            // uniform chunk writes no data array at all, so a reader that expected one would
            // desynchronise here and nowhere else.
            var grid = new CellGrid(Small);
            var loaded = LoadInto(Small, SaveOf(grid));

            Assert.That(HashOf(loaded), Is.EqualTo(HashOf(grid)));
            for (int i = 0; i < grid.Edifice.Length; i++)
                Assert.That(loaded.Edifice[i], Is.EqualTo(-1), "an empty grid holds -1, not 0");
        }

        [Test]
        public void EveryAuthoritativeFieldRoundTrips()
        {
            var grid = new CellGrid(Small);
            var rng = new Random(7);

            // Deliberately noisy: a value per cell from a wide range defeats the palette and
            // exercises the raw fallback as well as wide bit-packing.
            for (int i = 0; i < grid.Terrain.Length; i++)
            {
                grid.Terrain[i] = (ushort)rng.Next(0, 900);
                grid.Floor[i] = (ushort)rng.Next(0, 5);
                grid.FloorStuff[i] = (ushort)rng.Next(0, 400);
                grid.Edifice[i] = rng.Next(0, 4) == 0 ? -1 : rng.Next(0, 70_000);
                grid.Flags[i] = (CellFlags)rng.Next(0, 32);
            }

            var loaded = LoadInto(Small, SaveOf(grid));

            for (int i = 0; i < grid.Terrain.Length; i++)
            {
                Assert.That(loaded.Terrain[i], Is.EqualTo(grid.Terrain[i]), $"terrain at {i}");
                Assert.That(loaded.Floor[i], Is.EqualTo(grid.Floor[i]), $"floor at {i}");
                Assert.That(loaded.FloorStuff[i], Is.EqualTo(grid.FloorStuff[i]), $"floor stuff at {i}");
                Assert.That(loaded.Edifice[i], Is.EqualTo(grid.Edifice[i]), $"edifice at {i}");
                Assert.That(loaded.Flags[i], Is.EqualTo(grid.Flags[i]), $"flags at {i}");
            }

            Assert.That(HashOf(loaded), Is.EqualTo(HashOf(grid)));
        }

        [Test]
        public void ASizeThatIsNotAWholeNumberOfChunksRoundTrips()
        {
            // 60 x 60 x 5 gives short chunks at the far edge — 10 wide and 10 deep rather than 25.
            // The encoding stores no value for a cell that does not exist, so an off-by-one in the
            // extent maths shows up as a corrupt grid rather than as wasted bytes.
            foreach (var size in new[]
                     {
                         new GridSize(26, 26, 6), new GridSize(51, 49, 11), new GridSize(1, 1, 1),
                     })
            {
                var grid = new CellGrid(size);
                for (int i = 0; i < grid.Terrain.Length; i++)
                {
                    grid.Terrain[i] = (ushort)(i % 13);
                    grid.Flags[i] = (CellFlags)(i % 4);
                }

                var loaded = LoadInto(size, SaveOf(grid));
                Assert.That(HashOf(loaded), Is.EqualTo(HashOf(grid)), $"{size} did not round trip");
            }
        }

        [Test]
        public void SavingTheSameStateTwiceProducesIdenticalBytes()
        {
            // The cheap detector for unordered iteration. A palette built by walking a dictionary
            // would round-trip perfectly and still fail here, intermittently, months later.
            var grid = City(Small);

            byte[] first = SaveOf(grid);
            byte[] second = SaveOf(grid);
            Assert.That(second, Is.EqualTo(first));

            // And again from a separately generated but identical grid, which is the case that
            // catches a palette seeded from anything other than the cells themselves.
            byte[] third = SaveOf(City(Small));
            Assert.That(third, Is.EqualTo(first));
        }

        // ---------------------------------------------------------------- real maps, at scale

        [Test, Category("Long")]
        public void BothGeneratedScaleTargetMapsRoundTripAndFitTheBudget()
        {
            Check("ruined city", City(ScaleTarget));
            Check("natural", Natural(ScaleTarget));
        }

        /// <summary>
        /// Every board the menu offers round-trips, and says what it costs on disk.
        ///
        /// <para>The budget is the scale target's 4 MiB, unchanged, because it is a ceiling on the
        /// largest board rather than a per-size figure. What this arm is actually for is the
        /// bytes-per-cell column: the encoding is a per-chunk palette with bit-packed indices, so
        /// a board that is mostly uniform rock and air compresses to almost nothing and a board
        /// that is mostly surface does not. A wider board is more surface per cell than a deeper
        /// one, and that shows up here and nowhere else.</para>
        /// </summary>
        [Test, Category("Long")]
        public void EveryOfferedBoardRoundTripsAndFitsTheBudget(
            [ValueSource(nameof(OfferedBoards))] GridSize size)
            => Check("natural", Natural(size));

        static IEnumerable<GridSize> OfferedBoards()
        {
            yield return BoardSizes.Standard;
            yield return BoardSizes.Large;
            yield return BoardSizes.Huge;
        }

        static void Check(string label, CellGrid grid)
        {
            var watch = Stopwatch.StartNew();
            byte[] bytes = SaveOf(grid);
            long saveMs = watch.ElapsedMilliseconds;

            watch.Restart();
            var loaded = LoadInto(grid.Size, bytes);
            long loadMs = watch.ElapsedMilliseconds;

            double mib = bytes.Length / (1024.0 * 1024.0);
            TestContext.WriteLine(
                $"{label} {grid.Size}: {bytes.Length:N0} bytes ({mib:F2} MiB), " +
                $"save {saveMs} ms, load {loadMs} ms — " +
                $"{(double)bytes.Length / grid.Terrain.Length:F2} bytes/cell over 5 fields");

            Assert.That(HashOf(loaded), Is.EqualTo(HashOf(grid)), $"{label}: grid hash differs after a round trip");
            Assert.That(bytes.Length, Is.LessThan(4 * 1024 * 1024), $"{label}: over the 4 MiB budget");
        }

        // ---------------------------------------------------------------- what is not saved

        [Test]
        public void DerivedFieldsAreNotSavedAndDoNotComeBack()
        {
            // Support is rebuilt on load by the caller, because a recomputed value is correct by
            // construction whereas a saved one can be stale. This pins that it is genuinely
            // absent — and that loading clears it, so a loaded grid cannot keep a stale
            // derivation from whatever the world held before. Region used to be asserted on
            // beside it, and the assertion was vacuous: nothing ever wrote the field (OQ-38).
            var grid = City(Small);
            for (int i = 0; i < grid.Support.Length; i++) grid.Support[i] = 4;

            byte[] withDerived = SaveOf(grid);

            Array.Clear(grid.Support, 0, grid.Support.Length);
            byte[] withoutDerived = SaveOf(grid);

            Assert.That(withDerived, Is.EqualTo(withoutDerived),
                "support changed the file, so it is being saved");

            var target = new CellGrid(Small);
            for (int i = 0; i < target.Support.Length; i++) target.Support[i] = 7;

            var world = WorldFor(Small);
            using var stream = new MemoryStream(withDerived, writable: false);
            WorldSave.Load(world, stream, new ISaveable[] { new GridSaveSection(target) });

            Assert.That(target.Support, Is.All.Zero, "load left a stale support value");
        }

        [Test]
        public void ASaveWithoutAGridSectionStillLoads()
        {
            // Older saves predate this section, and the container skips what it does not know.
            var world = WorldFor(Small);
            using var stream = new MemoryStream();
            WorldSave.Save(world, stream, Array.Empty<ISaveable>());
            stream.Position = 0;

            var grid = new CellGrid(Small);
            var restored = WorldFor(Small);
            var header = WorldSave.Load(restored, stream, new ISaveable[] { new GridSaveSection(grid) });

            Assert.That(header.SkippedSections, Is.Empty);
            Assert.That(HashOf(grid), Is.EqualTo(HashOf(new CellGrid(Small))), "the grid was left untouched");
        }

        [Test]
        public void AGridSectionFromAnUnknownBuildIsSkippedRatherThanFatal()
        {
            var grid = City(Small);
            var world = WorldFor(Small);

            using var stream = new MemoryStream();
            WorldSave.Save(world, stream, new ISaveable[] { new FutureGridSection(grid) });
            stream.Position = 0;

            var target = new CellGrid(Small);
            var restored = WorldFor(Small);
            var header = WorldSave.Load(restored, stream, new ISaveable[] { new GridSaveSection(target) });

            Assert.That(header.SkippedSections, Is.EqualTo(new[] { "odyssey.grid.v99" }));
        }

        /// <summary>A section key this build does not know, standing in for a later format.</summary>
        sealed class FutureGridSection : ISaveable
        {
            readonly GridSaveSection _inner;
            public FutureGridSection(CellGrid grid) => _inner = new GridSaveSection(grid);
            public string SaveKey => "odyssey.grid.v99";
            public void Save(SaveWriter writer) => _inner.Save(writer);
            public void Load(SaveReader reader) => _inner.Load(reader);
        }

        // ---------------------------------------------------------------- the packing itself

        [Test]
        public void IndexWidthIsTheSmallestThatHoldsThePalette()
        {
            Assert.That(GridSaveSection.BitsFor(1), Is.EqualTo(1), "one bit is the floor, never zero");
            Assert.That(GridSaveSection.BitsFor(2), Is.EqualTo(1));
            Assert.That(GridSaveSection.BitsFor(3), Is.EqualTo(2));
            Assert.That(GridSaveSection.BitsFor(4), Is.EqualTo(2));
            Assert.That(GridSaveSection.BitsFor(5), Is.EqualTo(3));
            Assert.That(GridSaveSection.BitsFor(256), Is.EqualTo(8));
        }

        [Test]
        public void AUniformMapCostsAlmostNothing()
        {
            // The case the whole scheme exists for: a chunk with one palette entry stores its
            // value and no data array. 250 x 250 x 40 is 800 chunks over five fields, so the
            // floor is 4,000 chunk records of a kind byte and a uint.
            var grid = new CellGrid(ScaleTarget);
            byte[] bytes = SaveOf(grid);

            TestContext.WriteLine($"uniform {ScaleTarget}: {bytes.Length:N0} bytes for 2.5M cells over 5 fields");
            Assert.That(bytes.Length, Is.LessThan(64 * 1024),
                "a uniform map should cost a few kilobytes, not megabytes");
        }

        [Test]
        public void ACorruptChunkIsRejectedRatherThanMisread()
        {
            var grid = City(Small);
            byte[] bytes = SaveOf(grid);

            // Flip a byte deep inside the payload. The encoding cannot detect every corruption —
            // that is what a checksum would be for — but an index outside its palette, or a
            // packed-length that disagrees with the cell count, must not be read as data.
            var corrupt = (byte[])bytes.Clone();
            for (int i = bytes.Length / 2; i < bytes.Length; i++) corrupt[i] ^= 0xFF;

            var target = new CellGrid(Small);
            var world = WorldFor(Small);
            using var stream = new MemoryStream(corrupt, writable: false);

            Assert.Throws<SaveLoadException>(
                () => WorldSave.Load(world, stream, new ISaveable[] { new GridSaveSection(target) }));
        }
    }
}
