#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Odyssey.Sim.Saving
{
    /// <summary>
    /// Everything about a folder of save files except <b>where the folder is</b>.
    ///
    /// <para>The folder is <c>Application.persistentDataPath/Saves</c> and that sentence is the
    /// only part of the subject Sim cannot have: <c>Application</c> lives in UnityEngine, which
    /// this assembly deliberately cannot reference (<c>Odyssey.Sim.csproj</c> references only
    /// <c>Odyssey.Sim.Contracts</c>, so a reach for UnityEngine breaks the build rather than a
    /// review). So the caller hands in the path and this owns the rest — what a file is called,
    /// what to call it when that name is taken, and what order the player sees them in — which is
    /// how the interesting half of 17-start-flow.md §5 ends up in the fast tier with no Unity and
    /// no filesystem.</para>
    ///
    /// <para><b>A save is identified by its header, never by its name.</b> Nothing here is ever
    /// parsed back: <see cref="WorldSave.ReadHeaderOnly(string)"/> is where a save's seed, size,
    /// tick and <see cref="SaveRecipe"/> come from. The name exists so the folder is legible to a
    /// human in a file browser, and that is the only requirement it has to meet. That is why the
    /// slug may throw away as much of a colony name as it likes, and why the disambiguator can be
    /// a bare number rather than something reversible.</para>
    /// </summary>
    public static class SaveCatalogue
    {
        /// <summary>
        /// The extension. Nothing in the format implied one — <see cref="WorldSave"/> writes a
        /// magic number and takes a <see cref="Stream"/>, and no code anywhere named a path — so
        /// this is a choice, made here, once.
        ///
        /// <para><c>.odyssey</c> over <c>.sav</c> or <c>.dat</c> because the folder is meant to be
        /// read by a person: a distinctive extension says which game a stray file belongs to when
        /// it has been copied somewhere else, and it will not collide with another application's
        /// association on the player's machine. It costs seven bytes of a name whose only budget
        /// is a path limit we stay well inside of.</para>
        /// </summary>
        public const string Extension = ".odyssey";

        /// <summary>
        /// What a colony name is cut down to before it becomes part of a file name.
        ///
        /// <para>48 is not a filesystem limit; it is a share of one. Windows' classic path limit
        /// is 260 characters, <c>persistentDataPath</c> under a user profile is comfortably under
        /// 120 of them, and <c>/Saves/</c> plus <see cref="Extension"/> plus a day and a
        /// disambiguator is under 30 more. 48 leaves the whole thing near 200 in the worst case
        /// while still being longer than any colony name a player will type on purpose. A name
        /// that overruns is cut, not rejected: refusing to save because a colony is called
        /// something long would be an absurd failure, and the name is not identity anyway.</para>
        /// </summary>
        public const int MaxSlugLength = 48;

        /// <summary>What an unnameable colony is called. See <see cref="Slug"/>.</summary>
        public const string FallbackSlug = "colony";

        // ------------------------------------------------------------------ naming

        /// <summary>
        /// A colony name reduced to something every filesystem will accept: ASCII lowercase
        /// letters and digits, with every run of anything else becoming a single hyphen, trimmed
        /// at both ends and cut to <see cref="MaxSlugLength"/>.
        ///
        /// <para><b>ASCII only, deliberately.</b> Keeping accented letters would produce names
        /// that are correct on every modern filesystem and then turn into mojibake the first time
        /// a folder is zipped, copied to a stick, or read by a tool that assumes a code page — and
        /// the failure would land on the player, in the one place we promised legibility. A name
        /// that survives the round trip in a plainer form is worth more than one that is faithful
        /// until it is moved.</para>
        ///
        /// <para>A name that keeps nothing — empty, whitespace, entirely punctuation, or written
        /// wholly in a script this throws away — becomes <see cref="FallbackSlug"/> rather than an
        /// empty string, because an empty stem would make the file <c>-day-4.odyssey</c> and give
        /// the player nothing to read.</para>
        /// </summary>
        public static string Slug(string? colonyName)
        {
            var builder = new StringBuilder(colonyName?.Length ?? 0);
            bool separatorPending = false;

            foreach (char raw in colonyName ?? string.Empty)
            {
                char c = AsciiLower(raw);
                if (IsKept(c))
                {
                    // A leading run of rubbish produces no hyphen: the trim would only take it
                    // off again, and doing it here keeps the cut below off a phantom character.
                    if (separatorPending && builder.Length > 0) builder.Append('-');
                    separatorPending = false;
                    builder.Append(c);
                }
                else
                {
                    separatorPending = true;
                }
            }

            if (builder.Length > MaxSlugLength) builder.Length = MaxSlugLength;
            while (builder.Length > 0 && builder[builder.Length - 1] == '-') builder.Length--;

            return builder.Length == 0 ? FallbackSlug : builder.ToString();
        }

        /// <summary>
        /// The name a recipe wants: the slugged colony name, the day it was saved on, and
        /// <see cref="Extension"/> — <c>riverbend-day-12.odyssey</c>.
        ///
        /// <para><b>The day is in the name and the day is never omitted</b>, even when the recipe
        /// does not know it (a version 1 file, or <see cref="SaveRecipe.Unknown"/>, whose day is
        /// -1 — that case writes <c>-day-unknown</c>). Two reasons, and the second is the one that
        /// would have bitten. The obvious one is that a folder sorted by name then reads as a
        /// colony's history. The other is that <c>-day-</c> in the middle of every stem is what
        /// stops a colony called "Con", "Aux", "Prn" or "Nul" producing a file name Windows
        /// reserves for a device and refuses to create — <c>con-day-4.odyssey</c> is an ordinary
        /// file, <c>con.odyssey</c> is not. Dropping the segment for an unknown day would have
        /// re-opened exactly that hole.</para>
        ///
        /// <para>The day is formatted invariantly, so a machine with a different culture writes
        /// the same name.</para>
        /// </summary>
        public static string FileNameFor(SaveRecipe recipe)
        {
            string day = recipe.Day >= 0 ? recipe.Day.ToString(CultureInfo.InvariantCulture) : "unknown";
            return Slug(recipe.ColonyName) + "-day-" + day + Extension;
        }

        /// <summary>
        /// The name <paramref name="wanted"/>, or the first free variation of it: a hyphen and a
        /// number from 2 upwards inserted before the extension, so
        /// <c>riverbend-day-12.odyssey</c> becomes <c>riverbend-day-12-2.odyssey</c> and then
        /// <c>-3</c>. Deterministic, and it terminates: each attempt is a name not yet tried, so
        /// one more than the number of taken names is always free.
        ///
        /// <para><b>Comparison is case-insensitive</b> even though everything this class produces
        /// is lowercase, because the set of taken names comes off a real folder — which may be on
        /// a case-insensitive filesystem, and may hold files a player renamed by hand. Treating
        /// <c>Riverbend-Day-12.odyssey</c> as free would mean overwriting it on Windows, and
        /// silently destroying a colony is the one outcome this whole area exists to avoid.</para>
        /// </summary>
        public static string FreeName(string wanted, IEnumerable<string>? taken)
        {
            if (wanted == null) throw new ArgumentNullException(nameof(wanted));

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (taken != null)
                foreach (string name in taken)
                    if (name != null) used.Add(name);

            if (!used.Contains(wanted)) return wanted;

            string extension = Path.GetExtension(wanted);
            string stem = wanted.Substring(0, wanted.Length - extension.Length);

            for (int attempt = 2; ; attempt++)
            {
                string candidate = stem + "-" + attempt.ToString(CultureInfo.InvariantCulture) + extension;
                if (!used.Contains(candidate)) return candidate;
            }
        }

        /// <summary>The name to write a save under, in a folder that already holds
        /// <paramref name="taken"/>. The two halves above, in the order the one real caller wants
        /// them.</summary>
        public static string FileNameFor(SaveRecipe recipe, IEnumerable<string>? taken) =>
            FreeName(FileNameFor(recipe), taken);

        // ------------------------------------------------------ naming by the player's own name

        /// <summary>
        /// What an unnameable colony is called when a <i>person</i> is going to read it, as opposed
        /// to <see cref="FallbackSlug"/>, which is what the filesystem gets. The two are the same
        /// word and a test holds them to it: <c>Slug(FallbackName) == FallbackSlug</c>, so a later
        /// change to one that forgets the other is red rather than merely odd.
        /// </summary>
        public const string FallbackName = "Colony";

        /// <summary>
        /// What is put on the end of a stem that would otherwise be a Windows device name. See
        /// <see cref="FileNameForName"/> for why it exists at all, and
        /// <see cref="IsReservedName"/> for what it is defending against.
        ///
        /// <para>A suffix rather than a prefix because the folder is sorted by name and a player
        /// looking for the save they called "Aux" should find it under A.</para>
        /// </summary>
        public const string DeviceSuffix = "-save";

        /// <summary>
        /// The names MS-DOS gave to devices and Windows has reserved ever since: creating a file
        /// called any of these fails, with or without an extension, in any mix of case. Lowercase
        /// here because <see cref="IsReservedName"/> folds before it compares.
        /// </summary>
        static readonly string[] ReservedNames =
        {
            "con", "prn", "aux", "nul",
            "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
            "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9",
        };

        /// <summary>
        /// Whether Windows would refuse to create a file with this name.
        ///
        /// <para><b>The extension does not save you</b>, which is the part that catches people:
        /// <c>con.odyssey</c> is as unopenable as <c>con</c>, because the rule is applied to the
        /// part before the first dot. So this answers about a whole file name, strips at the first
        /// dot, and folds case — a player who types "Nul" is asking for the same file a player who
        /// types "NUL" is.</para>
        ///
        /// <para>The classic four and the numbered twenty-two, and no more. Windows also reserves
        /// these names followed by trailing spaces or dots, and <c>CONIN$</c> / <c>CONOUT$</c> on
        /// recent builds — none of which <see cref="Slug"/> can produce, since it keeps only ASCII
        /// letters, digits and interior hyphens. This is a public check all the same, because
        /// something that is true by construction elsewhere is worth being able to assert.</para>
        /// </summary>
        public static bool IsReservedName(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;

            int dot = fileName!.IndexOf('.');
            string stem = dot < 0 ? fileName : fileName.Substring(0, dot);

            foreach (string reserved in ReservedNames)
                if (string.Equals(stem, reserved, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        /// <summary>
        /// Whether a name the player typed can become a file at all: whether
        /// <see cref="Slug"/> would keep anything of it.
        ///
        /// <para>This exists because a player-chosen name and a colony name want opposite
        /// treatment when they slug to nothing. A colony called "!!!" still has to be saved, so
        /// <see cref="Slug"/> quietly substitutes <see cref="FallbackSlug"/>. A <i>save</i> called
        /// "!!!" is a player asking for something, and answering with a file called
        /// <c>colony.odyssey</c> would be the interface lying about what it did — worse, the next
        /// "!!!" and the next "..." would both land on that same file and overwrite it. So the
        /// prompt refuses the name instead, and this is the question it refuses on.</para>
        ///
        /// <para>Length is deliberately not part of it: a name too long for the path budget is
        /// <i>cut</i>, never rejected, which is the bargain <see cref="MaxSlugLength"/> already
        /// struck.</para>
        /// </summary>
        public static bool IsUsableName(string? saveName)
        {
            foreach (char raw in saveName ?? string.Empty)
                if (IsKept(AsciiLower(raw)))
                    return true;

            return false;
        }

        /// <summary>
        /// The file a name the player typed maps to: the slug, plus <see cref="Extension"/>.
        ///
        /// <para><b>No disambiguator, and that is the whole point of this method existing beside
        /// <see cref="FileNameFor(SaveRecipe, IEnumerable{string})"/>.</b> The owner's report on
        /// 2026-09-17 was that saving twice produced two files; it did, because the recipe-named
        /// path asks <see cref="FreeName"/> to step around anything already there. A name the
        /// player chose has to map to exactly one file, for ever, or saving again cannot mean
        /// overwriting. Collision is therefore not this method's problem — it is a question the
        /// prompt asks with <see cref="NameIsTaken(string, IEnumerable{string})"/> and the player
        /// answers.</para>
        ///
        /// <para><b>The device-name hole has to be closed here deliberately.</b> Every name the
        /// recipe path produces contains <c>-day-N</c>, and the author of that method recorded that
        /// the segment is what keeps a colony called "Con" or "Nul" from producing a file Windows
        /// will not create. A player-chosen name has no such segment, so the protection is gone and
        /// is put back by <see cref="DeviceSuffix"/>: "Aux" is saved as
        /// <c>aux-save.odyssey</c>.</para>
        ///
        /// <para><b>Two names can map to one file, and that is safe rather than merely tolerated.</b>
        /// "Ashford" and "ashford" both give <c>ashford.odyssey</c>; so do "Aux" and "aux save".
        /// Nothing here hides that, because collision is decided <i>on the file name</i> — so every
        /// many-to-one case in the slug surfaces as the same overwrite question the player would get
        /// for typing the name twice, rather than as a silent clobber. Deciding it on the typed
        /// name is what would be unsafe, and it is the thing this shape rules out.</para>
        ///
        /// <para>It cannot refuse. An unusable name falls back to <see cref="FallbackSlug"/> rather
        /// than throwing, because a method on the path to writing a save must not turn a bad name
        /// into a crash; <see cref="IsUsableName"/> is how a caller arranges never to ask.</para>
        /// </summary>
        public static string FileNameForName(string? saveName)
        {
            string stem = Slug(saveName);
            if (IsReservedName(stem)) stem += DeviceSuffix;
            return stem + Extension;
        }

        /// <summary>
        /// The suffix marking the copy an autosave keeps of what it is about to write over.
        /// </summary>
        public const string PreviousSuffix = "-previous";

        /// <summary>
        /// The file the *last* autosave of this save should be kept as —
        /// <c>ashford.odyssey</c> becomes <c>ashford-previous.odyssey</c>.
        ///
        /// <para><b>Why one copy is kept at all.</b> The autosave writes over the colony's own
        /// file, which is what the owner asked for (2026-09-21) and what stops a folder filling
        /// up. But an autosave taken thirty seconds after a disaster would then be the only copy
        /// there is, and a save interrupted halfway would be the only copy there is <i>and</i>
        /// unreadable. One previous generation costs one file per colony and answers both.</para>
        ///
        /// <para><b>It is an ordinary save file and it is listed like one.</b> A backup nobody can
        /// see is a backup nobody can use — the load screen shows it, named for what it is, and it
        /// opens like any other.</para>
        ///
        /// <para>A colony the player has literally named "Ashford previous" would slug to the same
        /// file. That is the same many-to-one the naming rules already surface as an overwrite
        /// question (<see cref="FileNameForName"/>), and it is recorded rather than defended
        /// against.</para>
        /// </summary>
        public static string PreviousFileName(string? fileName)
        {
            string name = fileName ?? string.Empty;
            string stem = name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - Extension.Length)
                : name;

            if (stem.Length == 0) stem = FallbackSlug;
            if (stem.EndsWith(PreviousSuffix, StringComparison.Ordinal)) return stem + Extension;

            return stem + PreviousSuffix + Extension;
        }

        /// <summary>Whether the file <paramref name="saveName"/> maps to is already in
        /// <paramref name="taken"/>. Case-insensitive, for the reason <see cref="FreeName"/>
        /// is: the folder may be case-insensitive and may hold files a player renamed by
        /// hand.</summary>
        public static bool NameIsTaken(string? saveName, IEnumerable<string>? taken) =>
            NameIsTaken(saveName, taken, null);

        /// <summary>
        /// The same question with one file excused: <paramref name="ignoring"/> is a file name
        /// that does not count as a collision.
        ///
        /// <para>That is how a session saves over its own file without being asked. Save writes to
        /// the file the session is bound to — the one it was loaded from or last saved to — and
        /// "you are about to overwrite the save you are playing" is not a question worth putting to
        /// somebody who just pressed Save. Every <i>other</i> file is still a collision, including
        /// under Save as…, where nothing is excused and typing the bound name asks like any
        /// other.</para>
        /// </summary>
        public static bool NameIsTaken(string? saveName, IEnumerable<string>? taken, string? ignoring)
        {
            if (taken == null) return false;

            string wanted = FileNameForName(saveName);
            foreach (string name in taken)
            {
                if (name == null) continue;
                if (!string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase)) continue;
                if (ignoring != null && string.Equals(name, ignoring, StringComparison.OrdinalIgnoreCase)) continue;
                return true;
            }

            return false;
        }

        /// <summary>
        /// The name to offer a player who has not chosen one: the colony, the word day, and the
        /// day — "Riverbend day 12".
        ///
        /// <para><b>It lives here rather than in the presenter so that the two naming paths agree
        /// on the first save.</b> Slug this and you get exactly the stem
        /// <see cref="FileNameFor(SaveRecipe)"/> produces, so a player who accepts the offered name
        /// writes the file an unnamed save would have written, and a folder does not end up holding
        /// <c>riverbend-day-12.odyssey</c> and <c>riverbend-day-12-2.odyssey</c> with the same
        /// colony in both. A test states that equality rather than leaving it to the two methods
        /// happening to spell the day the same way.</para>
        ///
        /// <para>The one case where they part is a colony name over
        /// <see cref="MaxSlugLength"/>: this suggestion carries its day after the name, so the cut
        /// falls on the day rather than on the colony. The player is looking at the text when that
        /// happens and can type something shorter, which is the difference between a suggestion and
        /// a derivation.</para>
        /// </summary>
        public static string SuggestedName(SaveRecipe recipe)
        {
            string colony = IsUsableName(recipe.ColonyName) ? recipe.ColonyName.Trim() : FallbackName;
            string day = recipe.Day >= 0 ? recipe.Day.ToString(CultureInfo.InvariantCulture) : "unknown";
            return colony + " day " + day;
        }

        /// <summary>Whether a path looks like one of ours, by extension alone. Case-insensitive,
        /// for the same reason <see cref="FreeName"/> is.</summary>
        public static bool IsSaveFile(string? path) =>
            !string.IsNullOrEmpty(path) && path!.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

        // ------------------------------------------------------------------ the listing

        /// <summary>
        /// The listing, as a pure function: entries ordered <b>newest first</b> by the file's own
        /// modification time, ties broken by name so the order is total and therefore testable.
        ///
        /// <para>Modification time rather than the header's tick, because the player's question is
        /// "which did I play last", and a colony reloaded from an earlier save legitimately has a
        /// lower tick than one abandoned days ago. The clock is also the only ordering a file the
        /// build <i>cannot read</i> still has, and those stay in the list.</para>
        ///
        /// <para><b>Nothing is dropped.</b> An unreadable or newer-format file keeps its place
        /// with its <see cref="SaveEntry.Problem"/> attached, because a player whose save vanishes
        /// from the list concludes their colony is gone — which is worse than being told the build
        /// cannot open it, and much worse than being told why.</para>
        ///
        /// <para>The sort is not stable (<see cref="List{T}.Sort(Comparison{T})"/> is a
        /// quicksort), which is exactly why the tie-break has to decide every pair rather than
        /// leaving equal times to whatever order the directory enumeration happened to give.</para>
        /// </summary>
        public static IReadOnlyList<SaveEntry> Order(IEnumerable<SaveEntry>? candidates)
        {
            var entries = new List<SaveEntry>();
            if (candidates != null)
                foreach (SaveEntry entry in candidates)
                    if (entry != null)
                        entries.Add(entry);

            entries.Sort(Newest);
            return entries;
        }

        /// <summary>Newest first; then by name, case-insensitively and then ordinally, so no two
        /// distinct entries ever compare equal.</summary>
        static int Newest(SaveEntry a, SaveEntry b)
        {
            int byTime = b.ModifiedUtc.CompareTo(a.ModifiedUtc);
            if (byTime != 0) return byTime;

            int byName = string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase);
            if (byName != 0) return byName;

            return string.CompareOrdinal(a.Path, b.Path);
        }

        // ------------------------------------------------------------------ the one IO method

        /// <summary>
        /// The only method here that touches a disk: enumerate <paramref name="folder"/>, read
        /// each save's header, and hand the lot to <see cref="Order"/>. Everything that decides
        /// anything is above this line and testable without a filesystem; this is the thin part.
        ///
        /// <para><b>One bad file cannot fail the listing.</b> Every per-file step — the timestamp
        /// and the header read alike — is caught and turned into an entry carrying the reason, so
        /// a truncated, locked or newer-format save costs the player that one row rather than the
        /// whole screen.</para>
        ///
        /// <para>A folder that does not exist, or cannot be enumerated, lists as empty rather than
        /// throwing: to a player those are the same statement as an empty folder — no saves yet.
        /// Creating it is the caller's business, since the caller is the only one who knows where
        /// it is.</para>
        /// </summary>
        public static IReadOnlyList<SaveEntry> Read(string folder)
        {
            string[] files;
            try
            {
                files = Directory.Exists(folder)
                    ? Directory.GetFiles(folder, "*" + Extension)
                    : Array.Empty<string>();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return Array.Empty<SaveEntry>();
            }

            var entries = new List<SaveEntry>(files.Length);
            foreach (string path in files)
            {
                // GetFiles' pattern matches 8.3 aliases and, on some platforms, longer
                // extensions; check it ourselves so the rule is the one IsSaveFile states.
                if (!IsSaveFile(path)) continue;

                DateTime modified;
                try
                {
                    modified = File.GetLastWriteTimeUtc(path);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    // Undated rather than absent: it sorts to the bottom and still tells the
                    // player the file is there.
                    entries.Add(SaveEntry.Unreadable(path, DateTime.MinValue, Describe(e)));
                    continue;
                }

                try
                {
                    entries.Add(SaveEntry.Readable(path, modified, WorldSave.ReadHeaderOnly(path)));
                }
                catch (Exception e) when (e is SaveLoadException || e is IOException ||
                                          e is UnauthorizedAccessException)
                {
                    entries.Add(SaveEntry.Unreadable(path, modified, Describe(e)));
                }
            }

            return Order(entries);
        }

        /// <summary>The exception's own message, which for a save is already a sentence written
        /// for a person — "Save format version 3 is newer than this build understands (2)." — and
        /// for an IO failure is the platform's. Never the type name and never a stack.</summary>
        static string Describe(Exception e) =>
            string.IsNullOrWhiteSpace(e.Message) ? e.GetType().Name : e.Message.Trim();

        static char AsciiLower(char c) => c >= 'A' && c <= 'Z' ? (char)(c + ('a' - 'A')) : c;

        static bool IsKept(char c) => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
    }

    /// <summary>
    /// One row of the load screen: a file, when it was last written, and either the header that
    /// was read from it or the reason it could not be.
    ///
    /// <para><b>One type, not a candidate and an entry.</b> <see cref="SaveCatalogue.Order"/> is a
    /// sort and nothing more — it neither reads files nor derives anything — so a second type
    /// either side of it would carry the same four fields and exist only to be converted. The
    /// consequence worth having is that a test can build the rows directly and ask the ordering
    /// question without a filesystem, which is the whole reason the IO is one method.</para>
    /// </summary>
    public sealed class SaveEntry
    {
        SaveEntry(string path, DateTime modifiedUtc, SaveHeader? header, string? problem)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            ModifiedUtc = modifiedUtc;
            Header = header;
            Problem = problem;
        }

        /// <summary>A save this build read the header of.</summary>
        public static SaveEntry Readable(string path, DateTime modifiedUtc, SaveHeader header) =>
            new SaveEntry(path, modifiedUtc, header ?? throw new ArgumentNullException(nameof(header)), null);

        /// <summary>A file in the saves folder this build could not read, and why. It is listed,
        /// not hidden; see <see cref="SaveCatalogue.Order"/>.</summary>
        public static SaveEntry Unreadable(string path, DateTime modifiedUtc, string reason) =>
            new SaveEntry(path, modifiedUtc, null,
                string.IsNullOrWhiteSpace(reason) ? "The file could not be read." : reason);

        /// <summary>Where the file is. The folder half came from the caller and is passed
        /// straight through; nothing here interprets it.</summary>
        public string Path { get; }

        /// <summary>The file's own last-write time, in UTC so two saves written either side of a
        /// daylight-saving change still order correctly.</summary>
        public DateTime ModifiedUtc { get; }

        /// <summary>The header, or null when <see cref="Problem"/> says why not.</summary>
        public SaveHeader? Header { get; }

        /// <summary>Why this file could not be read, or null when it could. A sentence for the
        /// player, not a diagnostic.</summary>
        public string? Problem { get; }

        /// <summary>Whether this row describes a save that can be loaded.</summary>
        public bool IsReadable => Header != null;

        /// <summary>The file name alone, which is what a list draws when the header has no colony
        /// name to give it — and what an unreadable row has instead of one.</summary>
        public string FileName => System.IO.Path.GetFileName(Path);

        public override string ToString() =>
            IsReadable ? FileName : FileName + " (" + Problem + ")";
    }
}
