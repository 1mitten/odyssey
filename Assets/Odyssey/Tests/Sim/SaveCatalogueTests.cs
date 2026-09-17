#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The saves folder, minus the folder (17-start-flow.md §5). Three questions, and all three
    /// are answerable without a filesystem, which is the point of the split: what a save is
    /// called, what it is called when that is taken, and what order the player sees them in.
    ///
    /// <para>Only <see cref="SaveCatalogue.Read"/> touches a disk, so only the last two tests
    /// here do either.</para>
    /// </summary>
    public class SaveCatalogueTests
    {
        // A header cannot be constructed from a test assembly — SaveHeader's constructor is
        // internal, deliberately, so that a header only ever comes from a file. So the readable
        // rows here get a real one, written and read back through the format in memory.
        static SaveHeader HeaderOf(SaveRecipe recipe)
        {
            var world = new SimWorldBuilder().WithSeed(11).WithSize(new GridSize(4, 4, 2)).Build();
            using var stream = new MemoryStream();
            WorldSave.Save(world, stream, Array.Empty<ISaveable>(), recipe);
            stream.Position = 0;
            return WorldSave.ReadHeaderOnly(stream);
        }

        static SaveRecipe Recipe(string colonyName, int day) =>
            new SaveRecipe(MapType.Natural, "scenario.bare", colonyName, day);

        static SaveEntry Row(string name, int minutesAgo) =>
            SaveEntry.Readable(Path.Combine("Saves", name),
                new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc).AddMinutes(-minutesAgo),
                HeaderOf(Recipe("Riverbend", 3)));

        static string[] NamesOf(IEnumerable<SaveEntry> entries) => entries.Select(e => e.FileName).ToArray();

        // ---------------------------------------------------------------- the name

        [Test]
        public void AFileNameIsDerivedFromTheRecipe()
        {
            Assert.That(SaveCatalogue.FileNameFor(Recipe("Riverbend", 12)), Is.EqualTo("riverbend-day-12.odyssey"));

            // Case folded, spaces and punctuation collapsed to single hyphens, ends trimmed.
            Assert.That(SaveCatalogue.FileNameFor(Recipe("  The Long   Watch!! ", 4)),
                Is.EqualTo("the-long-watch-day-4.odyssey"));

            // Digits survive; a run of separators is one hyphen, not three.
            Assert.That(SaveCatalogue.FileNameFor(Recipe("Outpost 7 — North/South", 101)),
                Is.EqualTo("outpost-7-north-south-day-101.odyssey"));
        }

        [Test]
        public void TheDayIsAlwaysInTheStem()
        {
            // An unknown day is named rather than dropped, and the reason is not cosmetic: the
            // "-day-" segment is what keeps a colony called Con, Aux, Prn or Nul off a Windows
            // device name. con-day-4.odyssey is an ordinary file; con.odyssey is not creatable.
            Assert.That(SaveCatalogue.FileNameFor(SaveRecipe.Unknown), Is.EqualTo("colony-day-unknown.odyssey"));
            Assert.That(SaveCatalogue.FileNameFor(Recipe("Con", 4)), Is.EqualTo("con-day-4.odyssey"));

            foreach (string reserved in new[] { "CON", "aux", "Prn", "nul", "com1", "LPT9" })
            {
                string name = SaveCatalogue.FileNameFor(Recipe(reserved, 1));
                string stem = Path.GetFileNameWithoutExtension(name);
                Assert.That(stem, Does.Contain("-day-"), $"'{reserved}' produced a bare device name: {name}");
            }
        }

        [Test]
        public void AColonyNameThatSlugsToNothingStillGetsAName()
        {
            foreach (string awkward in new[] { "", "   ", "!!!", "---", "...", "  ??  ", "日本語" })
            {
                string name = SaveCatalogue.FileNameFor(Recipe(awkward, 9));
                Assert.That(name, Is.EqualTo("colony-day-9.odyssey"), $"'{awkward}' produced {name}");
                Assert.That(name, Does.Not.StartWith("-"), "an empty stem would read as a hidden or odd file");
            }

            // A name that is partly throwaway keeps what it can rather than falling back.
            Assert.That(SaveCatalogue.FileNameFor(Recipe("!!Fort 9!!", 2)), Is.EqualTo("fort-9-day-2.odyssey"));
        }

        [Test]
        public void AVeryLongColonyNameIsCutRatherThanRejected()
        {
            string huge = new string('a', 400) + " " + new string('b', 400);
            string name = SaveCatalogue.FileNameFor(Recipe(huge, 12345));

            Assert.That(Path.GetFileNameWithoutExtension(name), Has.Length.LessThanOrEqualTo(
                SaveCatalogue.MaxSlugLength + "-day-12345".Length));
            Assert.That(name, Is.EqualTo(new string('a', SaveCatalogue.MaxSlugLength) + "-day-12345.odyssey"));

            // And the cut never leaves a trailing hyphen, which would read as a typo in the
            // folder: a name whose 48th character is a separator loses it.
            string cutOnASeparator = new string('a', SaveCatalogue.MaxSlugLength - 1) + " tail";
            Assert.That(SaveCatalogue.FileNameFor(Recipe(cutOnASeparator, 1)),
                Is.EqualTo(new string('a', SaveCatalogue.MaxSlugLength - 1) + "-day-1.odyssey"));

            // Whatever the name, the file part stays far inside a path budget.
            Assert.That(name, Has.Length.LessThan(80));
        }

        [Test]
        public void EverySlugIsFilesystemSafe()
        {
            char[] hostile = Path.GetInvalidFileNameChars();
            foreach (string colony in new[]
                     {
                         "A:B", "up/down", "back\\slash", "star*", "quote\"", "pipe|", "<angle>", "tab\tstop",
                         "new\nline", "Ravnsbjørg", "Ω Colony", "dot.dot.dot", "trailing.",
                     })
            {
                string name = SaveCatalogue.FileNameFor(Recipe(colony, 1));
                Assert.That(name.IndexOfAny(hostile), Is.EqualTo(-1), $"'{colony}' produced {name}");
                Assert.That(name, Does.EndWith(SaveCatalogue.Extension));
                Assert.That(Path.GetFileNameWithoutExtension(name), Does.Not.Contain("."),
                    "a dot in the stem makes the extension ambiguous to a file browser");
            }
        }

        // ---------------------------------------------------------------- collisions

        [Test]
        public void AFreeNameIsTheOneAskedForWhenNothingHoldsIt()
        {
            Assert.That(SaveCatalogue.FreeName("riverbend-day-12.odyssey", Array.Empty<string>()),
                Is.EqualTo("riverbend-day-12.odyssey"));
            Assert.That(SaveCatalogue.FreeName("riverbend-day-12.odyssey", new[] { "elsewhere-day-1.odyssey" }),
                Is.EqualTo("riverbend-day-12.odyssey"));
            Assert.That(SaveCatalogue.FreeName("riverbend-day-12.odyssey", null),
                Is.EqualTo("riverbend-day-12.odyssey"));
        }

        [Test]
        public void ASecondCollisionDoesNotReuseTheFirstsAnswer()
        {
            var taken = new List<string> { "riverbend-day-12.odyssey" };

            string first = SaveCatalogue.FreeName("riverbend-day-12.odyssey", taken);
            Assert.That(first, Is.EqualTo("riverbend-day-12-2.odyssey"));

            taken.Add(first);
            string second = SaveCatalogue.FreeName("riverbend-day-12.odyssey", taken);
            Assert.That(second, Is.EqualTo("riverbend-day-12-3.odyssey"));
            Assert.That(second, Is.Not.EqualTo(first), "a second save would have overwritten the first");

            taken.Add(second);
            Assert.That(SaveCatalogue.FreeName("riverbend-day-12.odyssey", taken),
                Is.EqualTo("riverbend-day-12-4.odyssey"));

            // Gaps are filled rather than skipped past: the answer is the first free name, not
            // one past the highest, so deleting a save reuses its number.
            taken.Remove("riverbend-day-12-3.odyssey");
            Assert.That(SaveCatalogue.FreeName("riverbend-day-12.odyssey", taken),
                Is.EqualTo("riverbend-day-12-3.odyssey"));
        }

        [Test]
        public void TakenNamesAreComparedTheWayAFilesystemWould()
        {
            // The folder is real and may be case-insensitive, and a player may have renamed a
            // file by hand. Treating a name that differs only in case as free means overwriting
            // a colony on Windows.
            Assert.That(SaveCatalogue.FreeName("riverbend-day-12.odyssey", new[] { "RiverBend-Day-12.ODYSSEY" }),
                Is.EqualTo("riverbend-day-12-2.odyssey"));
        }

        [Test]
        public void TheWholeNameIsChosenInOneCall()
        {
            var taken = new[] { "riverbend-day-12.odyssey", "riverbend-day-12-2.odyssey" };
            Assert.That(SaveCatalogue.FileNameFor(Recipe("Riverbend", 12), taken),
                Is.EqualTo("riverbend-day-12-3.odyssey"));
        }

        // ---------------------------------------------------------------- the listing

        [Test]
        public void TheListingIsNewestFirst()
        {
            var ordered = SaveCatalogue.Order(new[]
            {
                Row("middle.odyssey", 60),
                Row("oldest.odyssey", 600),
                Row("newest.odyssey", 1),
            });

            Assert.That(NamesOf(ordered), Is.EqualTo(new[] { "newest.odyssey", "middle.odyssey", "oldest.odyssey" }));
        }

        [Test]
        public void EqualTimesBreakByNameSoTheOrderIsTestable()
        {
            // Not cosmetic: List.Sort is a quicksort and is not stable, so equal times without a
            // total tie-break would come out in whatever order the directory enumeration gave —
            // a listing that differs between two runs of the same folder.
            var ordered = SaveCatalogue.Order(new[]
            {
                Row("charlie.odyssey", 30), Row("alpha.odyssey", 30), Row("bravo.odyssey", 30),
                Row("Delta.odyssey", 30),
            });

            Assert.That(NamesOf(ordered),
                Is.EqualTo(new[] { "alpha.odyssey", "bravo.odyssey", "charlie.odyssey", "Delta.odyssey" }),
                "ties are broken by name, case-insensitively");
        }

        /// <summary>
        /// <b>The negative control.</b> The two tests above would both pass against a
        /// <see cref="SaveCatalogue.Order"/> that returned its input untouched, if the input
        /// happened to arrive in the right order — and a directory enumeration often does hand
        /// files over alphabetically, which on a set like <c>newest / middle / oldest</c> is
        /// nearly the answer. This one cannot: it asserts that the input order and the output
        /// order genuinely differ, so a listing that sorts nothing fails here rather than passing
        /// by luck.
        ///
        /// <para>Run, not assumed: with <c>Order</c>'s <c>entries.Sort(Newest)</c> line commented
        /// out, <c>scripts/test-fast.sh --filter Name~SaveCatalogue</c> reported
        /// <c>Failed: 5, Passed: 11</c> — this test among the five — and with the line restored,
        /// 16 of 16 passed. (The same edit breaks the two tests above as well, which is the point:
        /// it is only <i>this</i> test that could not have been fooled by a lucky input
        /// order.)</para>
        /// </summary>
        [Test]
        public void TheOrderingWouldFailIfTheListingDidNotSort()
        {
            var input = new[]
            {
                Row("a-oldest.odyssey", 900),
                Row("b-newest.odyssey", 1),
                Row("c-middle.odyssey", 90),
            };

            IReadOnlyList<SaveEntry> ordered = SaveCatalogue.Order(input);

            Assert.That(NamesOf(ordered),
                Is.EqualTo(new[] { "b-newest.odyssey", "c-middle.odyssey", "a-oldest.odyssey" }));
            Assert.That(NamesOf(ordered), Is.Not.EqualTo(NamesOf(input)),
                "the expected order is the input order, so this test could not detect a listing that never sorts");
        }

        [Test]
        public void AnUnreadableFileIsKeptInTheListWithItsReason()
        {
            var ordered = SaveCatalogue.Order(new[]
            {
                Row("good.odyssey", 100),
                SaveEntry.Unreadable(Path.Combine("Saves", "broken.odyssey"),
                    new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc), "Save file is truncated."),
            });

            // Kept, and kept in its place: a player whose save vanishes from the list concludes
            // their colony is gone, so an unreadable file sorts by its clock like any other.
            Assert.That(NamesOf(ordered), Is.EqualTo(new[] { "broken.odyssey", "good.odyssey" }));

            SaveEntry broken = ordered[0];
            Assert.That(broken.IsReadable, Is.False);
            Assert.That(broken.Header, Is.Null);
            Assert.That(broken.Problem, Is.EqualTo("Save file is truncated."));
            Assert.That(ordered[1].IsReadable, Is.True);
            Assert.That(ordered[1].Problem, Is.Null);
            Assert.That(ordered[1].Header!.Recipe.ColonyName, Is.EqualTo("Riverbend"));
        }

        [Test]
        public void AnEmptyInputIsAnEmptyListing()
        {
            Assert.That(SaveCatalogue.Order(Array.Empty<SaveEntry>()), Is.Empty);
            Assert.That(SaveCatalogue.Order(null), Is.Empty);
        }

        // ---------------------------------------------------------------- the one IO method

        [Test]
        public void AFolderIsReadWithTheGoodFilesAndTheBadOnesAlike()
        {
            string folder = Path.Combine(Path.GetTempPath(), "odyssey-save-catalogue-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var world = new SimWorldBuilder().WithSeed(77).WithSize(new GridSize(4, 4, 2)).Build();
                string good = Path.Combine(folder, "riverbend-day-12.odyssey");
                WorldSave.SaveToFile(good, world, Array.Empty<ISaveable>(), Recipe("Riverbend", 12));

                byte[] bytes = File.ReadAllBytes(good);

                // Truncated inside the header: the format's own EndOfStream path.
                string truncated = Path.Combine(folder, "cut-day-3.odyssey");
                File.WriteAllBytes(truncated, bytes.Take(10).ToArray());

                // Written by a build from the future: the case §5 names explicitly, which must be
                // listed with its reason rather than hidden.
                byte[] future = (byte[])bytes.Clone();
                BitConverter.GetBytes(99).CopyTo(future, 8); // the format version, after the magic
                string newer = Path.Combine(folder, "future-day-9.odyssey");
                File.WriteAllBytes(newer, future);

                // Not ours at all, and not listed.
                File.WriteAllText(Path.Combine(folder, "notes.txt"), "not a save");

                // Deliberately not alphabetical order of age: the newest is the middle name.
                File.SetLastWriteTimeUtc(truncated, new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(good, new DateTime(2026, 9, 17, 8, 0, 0, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(newer, new DateTime(2026, 9, 14, 8, 0, 0, DateTimeKind.Utc));

                IReadOnlyList<SaveEntry> entries = SaveCatalogue.Read(folder);

                Assert.That(NamesOf(entries),
                    Is.EqualTo(new[] { "riverbend-day-12.odyssey", "future-day-9.odyssey", "cut-day-3.odyssey" }),
                    "three saves, newest first, and the text file is not one of them");

                Assert.That(entries[0].IsReadable, Is.True);
                Assert.That(entries[0].Header!.Recipe.ColonyName, Is.EqualTo("Riverbend"));
                Assert.That(entries[0].Header!.Recipe.Day, Is.EqualTo(12));
                Assert.That(entries[0].Header!.Seed, Is.EqualTo(77u));

                Assert.That(entries[1].IsReadable, Is.False, "a newer format cannot be loaded by this build");
                Assert.That(entries[1].Problem, Does.Contain("99"), entries[1].Problem);

                Assert.That(entries[2].IsReadable, Is.False);
                Assert.That(entries[2].Problem, Is.Not.Null.And.Not.Empty);

                // And the name the next save would take, given what is already there.
                string next = SaveCatalogue.FileNameFor(Recipe("Riverbend", 12), NamesOf(entries));
                Assert.That(next, Is.EqualTo("riverbend-day-12-2.odyssey"));
            }
            finally
            {
                try { Directory.Delete(folder, recursive: true); }
                catch (IOException) { /* a leftover temp folder is not a test failure */ }
            }
        }

        [Test]
        public void AFolderThatDoesNotExistListsAsEmptyRatherThanThrowing()
        {
            // To a player, "no folder yet" and "no saves yet" are the same statement. Creating it
            // is the caller's business, because the caller is the only one who knows where it is.
            string missing = Path.Combine(Path.GetTempPath(), "odyssey-no-such-" + Guid.NewGuid().ToString("N"));
            Assert.That(SaveCatalogue.Read(missing), Is.Empty);
        }
    }
}
