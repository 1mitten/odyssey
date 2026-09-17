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

        // ---------------------------------------------------------------- a name the player chose

        [Test]
        public void ANameThePlayerTypedBecomesTheFileItsSlugAsksFor()
        {
            Assert.That(SaveCatalogue.FileNameForName("Ashford"), Is.EqualTo("ashford.odyssey"));
            Assert.That(SaveCatalogue.FileNameForName("  Before the winter!! "),
                Is.EqualTo("before-the-winter.odyssey"));
            Assert.That(SaveCatalogue.FileNameForName("Riverbend day 12"),
                Is.EqualTo("riverbend-day-12.odyssey"));

            // Nothing hostile survives, and the stem carries no dot, so a file browser still knows
            // where the extension starts.
            char[] hostile = Path.GetInvalidFileNameChars();
            foreach (string typed in new[] { "A:B", "up/down", "back\\slash", "pipe|", "dot.dot", "Ravnsbjørg" })
            {
                string file = SaveCatalogue.FileNameForName(typed);
                Assert.That(file.IndexOfAny(hostile), Is.EqualTo(-1), $"'{typed}' produced {file}");
                Assert.That(Path.GetFileNameWithoutExtension(file), Does.Not.Contain("."));
                Assert.That(file, Does.EndWith(SaveCatalogue.Extension));
            }
        }

        [Test]
        public void ANameThePlayerChoseIsNeverDisambiguated()
        {
            // This is the behaviour the owner asked to have removed (2026-09-17: "I notice you keep
            // saving a new game everytime"). The recipe path steps around a collision on purpose;
            // the named path must not, or saving twice under one name cannot mean overwriting once.
            var folder = new[] { "ashford.odyssey", "ashford-2.odyssey", "ashford-3.odyssey" };

            Assert.That(SaveCatalogue.FileNameForName("Ashford"), Is.EqualTo("ashford.odyssey"));
            Assert.That(SaveCatalogue.FileNameForName("Ashford"), Is.EqualTo("ashford.odyssey"),
                "a name maps to one file for ever, or Save cannot overwrite");

            // And the collision is reported rather than routed around: the answer is a question for
            // the player, not a second file.
            Assert.That(SaveCatalogue.NameIsTaken("Ashford", folder), Is.True);
            Assert.That(SaveCatalogue.NameIsTaken("Ashford", Array.Empty<string>()), Is.False);
            Assert.That(SaveCatalogue.NameIsTaken("Ashford", null), Is.False);

            // The recipe path is untouched by any of this and still disambiguates.
            Assert.That(SaveCatalogue.FileNameFor(Recipe("Riverbend", 12), new[] { "riverbend-day-12.odyssey" }),
                Is.EqualTo("riverbend-day-12-2.odyssey"));
        }

        [Test]
        public void AReservedDeviceNameIsEscapedNowThatTheDaySegmentIsGone()
        {
            // The recipe path was safe by accident: every stem it produced contained "-day-N", and
            // the previous author recorded that as the thing keeping a colony called Con off a
            // Windows device name. A name the player typed has no such segment, so the protection
            // had to be put back on purpose.
            var reserved = new List<string> { "CON", "prn", "Aux", "nul" };
            for (int i = 1; i <= 9; i++) { reserved.Add("com" + i); reserved.Add("LPT" + i); }

            foreach (string device in reserved)
            {
                string file = SaveCatalogue.FileNameForName(device);
                Assert.That(SaveCatalogue.IsReservedName(file), Is.False, $"'{device}' produced {file}");
                Assert.That(file, Is.EqualTo(device.ToLowerInvariant() + SaveCatalogue.DeviceSuffix +
                                             SaveCatalogue.Extension));
            }

            // The extension does not save you, which is the half that catches people: the rule
            // applies to the part before the first dot.
            Assert.That(SaveCatalogue.IsReservedName("con"), Is.True);
            Assert.That(SaveCatalogue.IsReservedName("CON.odyssey"), Is.True);
            Assert.That(SaveCatalogue.IsReservedName("Nul.txt"), Is.True);
            Assert.That(SaveCatalogue.IsReservedName("com9.odyssey"), Is.True);

            // And near misses are ordinary files. com10 is not a device, and neither is a device
            // name with anything attached to it.
            foreach (string ordinary in new[] { "com0", "com10", "lpt0", "console", "connor", "aux2", "" })
                Assert.That(SaveCatalogue.IsReservedName(ordinary), Is.False, ordinary);

            // The escape itself is not reserved, and a player who types it back gets the same file
            // — which is a visible overwrite question, not a silent clobber. See FileNameForName.
            Assert.That(SaveCatalogue.FileNameForName("con save"), Is.EqualTo("con-save.odyssey"));
            Assert.That(SaveCatalogue.FileNameForName("Con"), Is.EqualTo("con-save.odyssey"));
            Assert.That(SaveCatalogue.NameIsTaken("con save", new[] { "con-save.odyssey" }), Is.True);
        }

        [Test]
        public void ANameThatKeepsNothingIsRefusedRatherThanQuietlyRenamed()
        {
            // A colony called "!!!" still has to be saved, so Slug substitutes a fallback. A save
            // called "!!!" is a player asking for something, and answering with colony.odyssey
            // would be the interface lying — and would put every unusable name on one file.
            foreach (string nothing in new[] { "", "   ", "!!!", "---", "...", "  ??  ", "日本語", "Ω" })
                Assert.That(SaveCatalogue.IsUsableName(nothing), Is.False, $"'{nothing}'");

            Assert.That(SaveCatalogue.IsUsableName(null), Is.False);

            foreach (string usable in new[] { "a", "7", "Ashford", "!!Fort 9!!", "Ω9" })
                Assert.That(SaveCatalogue.IsUsableName(usable), Is.True, $"'{usable}'");

            // Length is not part of it: an over-long name is cut, never rejected.
            Assert.That(SaveCatalogue.IsUsableName(new string('a', 400)), Is.True);

            // It still cannot throw. A method on the path to writing a save must not turn a bad
            // name into a crash, so it falls back; the prompt is what arranges never to ask.
            Assert.That(SaveCatalogue.FileNameForName("!!!"),
                Is.EqualTo(SaveCatalogue.FallbackSlug + SaveCatalogue.Extension));
            Assert.That(SaveCatalogue.FileNameForName(null),
                Is.EqualTo(SaveCatalogue.FallbackSlug + SaveCatalogue.Extension));
        }

        [Test]
        public void AVeryLongChosenNameIsCutRatherThanRejected()
        {
            string huge = new string('a', 400) + " " + new string('b', 400);
            string file = SaveCatalogue.FileNameForName(huge);

            Assert.That(file, Is.EqualTo(new string('a', SaveCatalogue.MaxSlugLength) + SaveCatalogue.Extension));
            Assert.That(file, Has.Length.LessThan(80), "the file part stays far inside any path budget");

            // The cut never leaves a trailing hyphen, which would read as a typo in the folder.
            string cutOnASeparator = new string('a', SaveCatalogue.MaxSlugLength - 1) + " tail";
            Assert.That(SaveCatalogue.FileNameForName(cutOnASeparator),
                Is.EqualTo(new string('a', SaveCatalogue.MaxSlugLength - 1) + SaveCatalogue.Extension));

            // Two long names that differ only past the cut are one file, and that is reported as a
            // collision rather than hidden — which is the whole reason collision is decided on the
            // file name and not on what was typed.
            string other = new string('a', 400) + " something else";
            Assert.That(SaveCatalogue.FileNameForName(other), Is.EqualTo(file));
            Assert.That(SaveCatalogue.NameIsTaken(other, new[] { file }), Is.True);
        }

        [Test]
        public void TwoNamesThatDifferOnlyInCaseAreOneSave()
        {
            // The folder may be case-insensitive, and a player may have renamed a file by hand.
            // Treating "Ashford" and "ashford" as two saves would overwrite one of them on Windows
            // and not on Linux — the same fault this file's FreeName tests already guard.
            Assert.That(SaveCatalogue.FileNameForName("Ashford"), Is.EqualTo(SaveCatalogue.FileNameForName("ashford")));
            Assert.That(SaveCatalogue.NameIsTaken("ASHFORD", new[] { "Ashford.ODYSSEY" }), Is.True);
            Assert.That(SaveCatalogue.NameIsTaken("Ashford", new[] { "ashford.odyssey" }), Is.True);
            Assert.That(SaveCatalogue.NameIsTaken("Ashford", new[] { "ashfordd.odyssey" }), Is.False);
        }

        [Test]
        public void TheFileASessionIsBoundToIsNotACollisionWithItself()
        {
            var folder = new[] { "ashford.odyssey", "bellwether.odyssey" };

            // Save writes over the file this session is bound to. "You are about to overwrite the
            // save you are playing" is not a question worth putting to somebody who pressed Save.
            Assert.That(SaveCatalogue.NameIsTaken("Ashford", folder, "ashford.odyssey"), Is.False);
            Assert.That(SaveCatalogue.NameIsTaken("Ashford", folder, "ASHFORD.ODYSSEY"), Is.False,
                "the excuse is matched the way a filesystem would match it");

            // Every other file is still a collision, bound session or not.
            Assert.That(SaveCatalogue.NameIsTaken("Bellwether", folder, "ashford.odyssey"), Is.True);

            // And nothing is excused when nothing is bound, which is Save as… and the first save of
            // a session alike.
            Assert.That(SaveCatalogue.NameIsTaken("Ashford", folder, null), Is.True);
        }

        [Test]
        public void TheOfferedNameWritesTheFileAnUnnamedSaveWouldHave()
        {
            // The point of the suggestion living in the catalogue rather than in the presenter: a
            // player who accepts it lands on the file the recipe path would have chosen, so a
            // folder never ends up holding riverbend-day-12 and riverbend-day-12-2 for one colony.
            foreach (SaveRecipe recipe in new[]
                     {
                         Recipe("Riverbend", 12),
                         Recipe("  The Long   Watch!! ", 4),
                         Recipe("Outpost 7 — North/South", 101),
                         Recipe("Con", 4),
                         Recipe("!!!", 9),
                         SaveRecipe.Unknown,
                     })
            {
                string suggested = SaveCatalogue.SuggestedName(recipe);
                Assert.That(SaveCatalogue.IsUsableName(suggested), Is.True, suggested);
                Assert.That(SaveCatalogue.FileNameForName(suggested),
                    Is.EqualTo(SaveCatalogue.FileNameFor(recipe)), suggested);
            }

            Assert.That(SaveCatalogue.SuggestedName(Recipe("Riverbend", 12)), Is.EqualTo("Riverbend day 12"));
            Assert.That(SaveCatalogue.SuggestedName(SaveRecipe.Unknown), Is.EqualTo("Colony day unknown"));

            // The two fallbacks are the same word said to two audiences, and they have to stay so:
            // the equality above rests on it for a colony with no usable name.
            Assert.That(SaveCatalogue.Slug(SaveCatalogue.FallbackName), Is.EqualTo(SaveCatalogue.FallbackSlug));
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
