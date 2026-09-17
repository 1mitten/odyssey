#nullable enable
using System;
using System.IO;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// U36: the header grew a <see cref="SaveRecipe"/> — map type, scenario, colony name, day —
    /// and files can be written straight to and read straight from a path, so a load screen can
    /// list a folder of saves from their headers alone. <see cref="SaveTests"/> still covers the
    /// section format and the failure modes that predate this; these are what changed.
    /// </summary>
    public class SaveFormatV2Tests
    {
        static (SimWorld world, SaveableCounter counter) Build(uint seed = 77) =>
            SaveFormatV2Tests.BuildWith(seed, new GridSize(8, 8, 4));

        static (SimWorld world, SaveableCounter counter) BuildWith(uint seed, GridSize size)
        {
            var counter = new SaveableCounter();
            var world = new SimWorldBuilder()
                .WithSeed(seed)
                .WithSize(size)
                .AddTickable(_ => counter)
                .Build();
            return (world, counter);
        }

        /// <summary>
        /// Three since U38, which added the two natural-board flags to the recipe. The number is
        /// asserted rather than merely read so that a bump is always a deliberate line in a diff —
        /// that is the whole reason this test exists, and it did its job on the way to 3.
        /// </summary>
        [Test]
        public void TheFormatVersionIsThree()
        {
            Assert.That(WorldSave.CurrentFormatVersion, Is.EqualTo(3));
        }

        [Test]
        public void ARecipeSavedRoundTripsThroughTheHeader()
        {
            var (world, counter) = Build();
            world.Tick(10);
            var recipe = new SaveRecipe(MapType.Natural, "Scenario_Bare", "Meridian", day: 3,
                barren: true, wooded: true);

            using var stream = new MemoryStream();
            WorldSave.Save(world, stream, new[] { counter }, recipe);
            stream.Position = 0;

            var (restored, restoredCounter) = Build();
            var header = WorldSave.Load(restored, stream, new[] { restoredCounter });

            Assert.That(header.FormatVersion, Is.EqualTo(3));
            Assert.That(header.Recipe.Map, Is.EqualTo(MapType.Natural));
            Assert.That(header.Recipe.Scenario, Is.EqualTo("Scenario_Bare"));
            Assert.That(header.Recipe.ColonyName, Is.EqualTo("Meridian"));
            Assert.That(header.Recipe.Day, Is.EqualTo(3));

            // The two flags U38 added. Both true is the wooded meadow the scene actually loads,
            // and it is the combination that used to be indistinguishable from the full natural
            // generator once the file was written.
            Assert.That(header.Recipe.Barren, Is.True);
            Assert.That(header.Recipe.Wooded, Is.True);
        }

        /// <summary>
        /// The bug U38's round-trip test found, written down as its own test: a header that cannot
        /// say which of the three natural boards it was made on.
        ///
        /// <para><b>The state hash could not have caught this</b>, which is why it survived U36.
        /// <c>GridSaveSection</c> writes every cell of every field, so a world rebuilt on the wrong
        /// board is entirely overwritten by the load and the hashes agree. What differs is
        /// everything worldgen returns beside the cells — the start cell the camera frames a loaded
        /// colony on, the outcome, the count of cells marked for work.</para>
        /// </summary>
        [Test]
        public void TheRecipeSaysWhichOfTheThreeNaturalBoardsItWas()
        {
            foreach ((bool barren, bool wooded) in new[] { (false, false), (true, false), (true, true) })
            {
                var (world, counter) = Build();
                var recipe = new SaveRecipe(MapType.Natural, "Scenario_Bare", "Meridian", 3, barren, wooded);

                using var stream = new MemoryStream();
                WorldSave.Save(world, stream, new[] { counter }, recipe);
                stream.Position = 0;

                var (restored, restoredCounter) = Build();
                SaveHeader header = WorldSave.Load(restored, stream, new[] { restoredCounter });

                Assert.That(header.Recipe.Barren, Is.EqualTo(barren),
                    $"a board written as barren={barren}, wooded={wooded} did not read back that way");
                Assert.That(header.Recipe.Wooded, Is.EqualTo(wooded),
                    $"a board written as barren={barren}, wooded={wooded} did not read back that way");
            }
        }

        [Test]
        public void SavingWithNoRecipeReadsBackUnknown()
        {
            // Every existing call site (WorldSave.Save(world, stream, components)) still compiles
            // and still writes a version-2 file; it just carries no recipe.
            var (world, counter) = Build();

            using var stream = new MemoryStream();
            WorldSave.Save(world, stream, new[] { counter });
            stream.Position = 0;

            var (restored, restoredCounter) = Build();
            var header = WorldSave.Load(restored, stream, new[] { restoredCounter });

            Assert.That(header.Recipe.Map, Is.EqualTo(MapType.Unknown));
            Assert.That(header.Recipe.Scenario, Is.Empty);
            Assert.That(header.Recipe.ColonyName, Is.Empty);
            Assert.That(header.Recipe.Day, Is.EqualTo(-1));
        }

        [Test]
        public void ReadHeaderOnlyNeedsNeitherAWorldNorTheComponentList()
        {
            var (world, counter) = Build(seed: 555);
            world.Tick(42);
            var recipe = new SaveRecipe(MapType.RuinedCity, "Scenario_Playtest", "Outpost Nine", day: 7);

            using var stream = new MemoryStream();
            WorldSave.Save(world, stream, new[] { counter }, recipe);
            stream.Position = 0;

            SaveHeader header = WorldSave.ReadHeaderOnly(stream);

            Assert.That(header.Seed, Is.EqualTo(555u));
            Assert.That(header.Tick, Is.EqualTo(42));
            Assert.That(header.Size, Is.EqualTo(new GridSize(8, 8, 4)));
            Assert.That(header.Recipe.Map, Is.EqualTo(MapType.RuinedCity));
            Assert.That(header.Recipe.Scenario, Is.EqualTo("Scenario_Playtest"));
            Assert.That(header.Recipe.ColonyName, Is.EqualTo("Outpost Nine"));
            Assert.That(header.Recipe.Day, Is.EqualTo(7));
        }

        [Test]
        public void AVersion1FileReadsBackWithSaneDefaults()
        {
            // Built by hand to the pre-U36 layout: magic, version 1, seed, size, tick, zero
            // sections — nothing this test constructs went through the new writer at all, which
            // is the point: an old file on disk, not a new one that happens to claim to be old.
            byte[] bytes = BuildVersion1Fixture(seed: 99, size: new GridSize(12, 10, 3), tick: 250);

            using var stream = new MemoryStream(bytes);
            SaveHeader header = WorldSave.ReadHeaderOnly(stream);

            Assert.That(header.FormatVersion, Is.EqualTo(1));
            Assert.That(header.Seed, Is.EqualTo(99u));
            Assert.That(header.Size, Is.EqualTo(new GridSize(12, 10, 3)));
            Assert.That(header.Tick, Is.EqualTo(250));
            Assert.That(header.Recipe.Map, Is.EqualTo(MapType.Unknown), "a v1 file never recorded a map type");
            Assert.That(header.Recipe.Scenario, Is.Empty);
            Assert.That(header.Recipe.ColonyName, Is.Empty);
            Assert.That(header.Recipe.Day, Is.EqualTo(-1));
        }

        [Test]
        public void AVersion1FileStillLoadsIntoAMatchingWorld()
        {
            byte[] bytes = BuildVersion1Fixture(seed: 314, size: new GridSize(8, 8, 4), tick: 60);

            var (restored, restoredCounter) = BuildWith(seed: 314, size: new GridSize(8, 8, 4));
            using var stream = new MemoryStream(bytes);
            var header = WorldSave.Load(restored, stream, new[] { restoredCounter });

            Assert.That(header.SkippedSections, Is.Empty);
            Assert.That(restored.CurrentTick, Is.EqualTo(60));
            Assert.That(header.Recipe.Map, Is.EqualTo(MapType.Unknown));
        }

        [Test]
        public void ReadHeaderOnlyStillRejectsATruncatedFile()
        {
            var (world, counter) = Build();
            world.Tick(5);
            using var full = new MemoryStream();
            WorldSave.Save(world, full, new[] { counter }, new SaveRecipe(MapType.Natural, "s", "n", 1));

            byte[] truncated = full.ToArray();
            Array.Resize(ref truncated, truncated.Length / 2);

            using var stream = new MemoryStream(truncated);
            var ex = Assert.Throws<SaveLoadException>(() => WorldSave.ReadHeaderOnly(stream));
            Assert.That(ex!.Message, Does.Contain("truncated"));
        }

        [Test]
        public void ReadHeaderOnlyStillRejectsANonSaveFile()
        {
            using var stream = new MemoryStream(new byte[64]);
            var ex = Assert.Throws<SaveLoadException>(() => WorldSave.ReadHeaderOnly(stream));
            Assert.That(ex!.Message, Does.Contain("Not an Odyssey save"));
        }

        [Test]
        public void FilesRoundTripThroughAPath()
        {
            var (world, counter) = Build(seed: 909);
            world.Tick(17);
            var recipe = new SaveRecipe(MapType.Natural, "Scenario_Bare", "Meridian", day: 1);

            string path = Path.Combine(Path.GetTempPath(), $"odyssey-save-{Guid.NewGuid():N}.oysave");
            try
            {
                WorldSave.SaveToFile(path, world, new[] { counter }, recipe);

                SaveHeader listed = WorldSave.ReadHeaderOnly(path);
                Assert.That(listed.Seed, Is.EqualTo(909u));
                Assert.That(listed.Recipe.ColonyName, Is.EqualTo("Meridian"));

                var (restored, restoredCounter) = Build(seed: 909);
                SaveHeader loaded = WorldSave.LoadFromFile(path, restored, new[] { restoredCounter });
                Assert.That(loaded.Tick, Is.EqualTo(17));
                Assert.That(restored.CurrentTick, Is.EqualTo(17));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void AColonyWorldStatesItsOwnRecipeFromWhatItWasBuiltWith()
        {
            var request = new ColonyRequest
            {
                Size = new GridSize(20, 20, 4),
                Seed = 246,
                Scenario = ScenarioDef.Bare(),
                Barren = true,
                Wooded = false,
                Map = MapType.Natural,
                Name = "Meridian",
            };
            var colony = ColonyWorld.Build(request);

            SaveRecipe recipe = colony.Recipe(day: 5);

            Assert.That(recipe.Map, Is.EqualTo(MapType.Natural));
            Assert.That(recipe.Scenario, Is.EqualTo("Scenario_Bare"));
            Assert.That(recipe.ColonyName, Is.EqualTo("Meridian"));
            Assert.That(recipe.Day, Is.EqualTo(5));
        }

        /// <summary>
        /// The exact bytes a build before U36 wrote: magic, version 1, seed, size, tick, then a
        /// zero section count. The magic value is copied from <c>WorldSave</c>'s own private
        /// constant, which is documented there as "any stable value" — this is the one place
        /// outside that file allowed to know it, because proving the old layout still reads means
        /// writing bytes the new writer never produced.
        /// </summary>
        static byte[] BuildVersion1Fixture(uint seed, GridSize size, int tick)
        {
            const ulong magic = 0x59455353594451;
            using var stream = new MemoryStream();
            using (var binary = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                binary.Write(magic);
                binary.Write(1); // format version 1
                binary.Write(seed);
                binary.Write(size.SizeX);
                binary.Write(size.SizeZ);
                binary.Write(size.SizeY);
                binary.Write(tick);
                binary.Write(0); // no sections
            }
            return stream.ToArray();
        }

        /// <summary>
        /// The exact bytes a build between U36 and U38 wrote: everything version 3 writes except
        /// the two board flags. Written by hand for the same reason the version 1 fixture is —
        /// proving an old layout still reads means producing bytes the current writer never
        /// will.
        /// </summary>
        static byte[] BuildVersion2Fixture(uint seed, GridSize size, int tick, string colony)
        {
            const ulong magic = 0x59455353594451;
            using var stream = new MemoryStream();
            using (var binary = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                binary.Write(magic);
                binary.Write(2); // format version 2
                binary.Write(seed);
                binary.Write(size.SizeX);
                binary.Write(size.SizeZ);
                binary.Write(size.SizeY);
                binary.Write(tick);
                binary.Write((int)MapType.Natural);

                void Text(string value)
                {
                    byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(value);
                    binary.Write(utf8.Length);
                    binary.Write(utf8);
                }

                Text("Scenario_Bare");
                Text(colony);
                binary.Write(7);  // day
                binary.Write(0);  // no sections
            }
            return stream.ToArray();
        }

        /// <summary>
        /// A version 2 file still loads, and its two absent board flags read back false rather
        /// than as a guess.
        ///
        /// <para>False and false is the full natural generator, which is what
        /// <c>MapType.Natural</c> meant on its own before version 3 could say otherwise — so an
        /// old file is read as the thing it would have been read as before, rather than as the
        /// board the current scene happens to load.</para>
        /// </summary>
        [Test]
        public void AVersion2FileStillLoadsAndItsBoardFlagsAreFalse()
        {
            var size = new GridSize(12, 10, 3);
            byte[] bytes = BuildVersion2Fixture(seed: 77, size: size, tick: 500, colony: "Ashford");

            using var stream = new MemoryStream(bytes);
            SaveHeader header = WorldSave.ReadHeaderOnly(stream);

            Assert.That(header.FormatVersion, Is.EqualTo(2));
            Assert.That(header.Recipe.ColonyName, Is.EqualTo("Ashford"));
            Assert.That(header.Recipe.Day, Is.EqualTo(7));
            Assert.That(header.Recipe.Barren, Is.False);
            Assert.That(header.Recipe.Wooded, Is.False);
        }
    }
}
