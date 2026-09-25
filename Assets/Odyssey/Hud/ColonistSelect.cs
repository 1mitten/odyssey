#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Hud
{
    /// <summary>
    /// One candidate as the screen draws it: who they are, what they are good at, and whether the
    /// player has kept them.
    ///
    /// <para><b>Already rolled by the time it gets here.</b> The roll needs
    /// <c>Odyssey.Sim</c> — a pawn, the content pack, the two draws — and this assembly is compiled
    /// without it (ADR 0003). So the presenter rolls and hands the finished row over, which is the
    /// bargain <see cref="MenuDirector.ShowSaves"/> already makes with a save listing and
    /// <see cref="SavePrompt.Type"/> with a name's verdict: what is on screen and what it means
    /// arrive together, and the Unity-free half stays a fast-tier test.</para>
    /// </summary>
    public readonly struct Candidate
    {
        /// <summary>The seed this person was rolled from. What a reroll changes, and what the
        /// colony is built from.</summary>
        public readonly uint Seed;

        /// <summary>What they are called, from <see cref="ColonistNames"/>.</summary>
        public readonly string Name;

        /// <summary>How old they are (<see cref="ColonistIdentity"/>).</summary>
        public readonly int Age;

        /// <summary>What they did before the city fell.</summary>
        public readonly string Occupation;

        /// <summary>
        /// Every skill, in reading order — <b>the same <see cref="SkillRow"/> the inspect pane's
        /// Skills tab is drawn from</b>, so one grid draws both and the two cannot disagree about
        /// ordering, greying or where a passion pip goes.
        ///
        /// <para>All thirteen, including the nine nothing simulates yet: the owner asked for the
        /// whole grid with the inactive ones greyed, and showing four would hide that the rest
        /// exist.</para>
        /// </summary>
        public readonly IReadOnlyList<SkillRow> Skills;

        /// <summary>
        /// Who she is (design 51 §5f): the same rows the inspect pane draws, from
        /// <see cref="TraitSummary.Row"/>, so the card and the pane cannot word a trait two ways.
        /// Empty for a candidate built without them.
        /// </summary>
        public readonly IReadOnlyList<InspectRow> Traits;

        public Candidate(uint seed, string name, int age, string occupation,
            IReadOnlyList<SkillRow>? skills, IReadOnlyList<InspectRow>? traits = null)
        {
            Seed = seed;
            Name = name ?? string.Empty;
            Age = age;
            Occupation = occupation ?? string.Empty;
            Skills = skills ?? Array.Empty<SkillRow>();
            Traits = traits ?? Array.Empty<InspectRow>();
        }

        /// <summary>
        /// The traits as the detail pane's one block of text: a line each, the name and what it
        /// does. Empty when she has none.
        /// </summary>
        public string TraitLines
        {
            get
            {
                if (Traits.Count == 0) return string.Empty;
                var lines = new System.Text.StringBuilder();
                for (int i = 0; i < Traits.Count; i++)
                {
                    if (i > 0) lines.Append('\n');
                    lines.Append(Traits[i].Name);
                    if (!string.IsNullOrEmpty(Traits[i].Value)) lines.Append(" - ").Append(Traits[i].Value);
                }
                return lines.ToString();
            }
        }

        /// <summary>"Wrenn, 34" — the line at the top of a card and of the detail beside it.</summary>
        public string NameAndAge => Age > 0 ? Name + ", " + Age : Name;
    }

    /// <summary>
    /// The three people you take into the game (U40): who they are, which of them you have kept,
    /// and what Start hands to the world.
    ///
    /// <para><b>A lock is the whole of the interaction.</b> Reroll is one row rather than three,
    /// because a per-card reroll and a lock are the same control said twice — locking the two you
    /// like and pressing Reroll is the same act as rerolling the third, with fewer things on
    /// screen and one rule to learn instead of two.</para>
    ///
    /// <para><b>It raises and never performs</b>, like every director here: it holds seeds, and
    /// building a colony out of them is the bootstrap's.</para>
    ///
    /// <para><b>No two candidates share a name.</b> That is a promise about a screen of three, not
    /// about a name: the three carry three different seeds, and <see cref="ColonistNames"/> keys on
    /// the seed, so two of them landing on one word is perfectly possible and would read as a bug
    /// in the roll. The reroll draws again when it happens, bounded, and gives up rather than
    /// spinning — a repeated name is a blemish and a hang is not.</para>
    ///
    /// <para>Unity-free by construction (ADR 0003): everything here runs in the fast tier.</para>
    /// </summary>
    public sealed class ColonistSelect
    {
        /// <summary>
        /// How many people you take. A constant with one owner rather than a knob: the screen is
        /// laid out for three and the milestone describes three, so a second place to say so could
        /// only ever disagree with this one.
        /// </summary>
        public const int Slots = 3;

        /// <summary>The registry key naming the row that deals a new set.</summary>
        public const string RerollKey = "ui.colonists.reroll";

        /// <summary>The registry key naming the lock a card carries.</summary>
        public const string LockKey = "ui.colonists.lock";

        /// <summary>The registry key naming the screen itself — the caption over the three.</summary>
        public const string TitleKey = "ui.colonists.title";

        /// <summary>Every key this screen can put on screen, held to the naming CSV by
        /// <c>RegistryTests</c>.</summary>
        public static readonly string[] IconKeys = { TitleKey, RerollKey, LockKey };

        readonly Candidate[] _cards = new Candidate[Slots];
        readonly bool[] _locked = new bool[Slots];

        /// <summary>
        /// The name the player typed over a slot, or null where they have not.
        ///
        /// <para>Beside the card rather than in it, because <see cref="Candidate"/> is what a roll
        /// produced and a typed name is not: a card that carried its own new name would have to be
        /// rebuilt to rename somebody, and the roll and the rename would become one field with two
        /// writers.</para>
        /// </summary>
        readonly string?[] _given = new string?[Slots];

        readonly Func<uint, int, Candidate> _roll;

        /// <summary>
        /// The seam the presenter fills and a test drives: given a seed <b>and the slot it will
        /// occupy</b>, the candidate it produces. Rolling needs <c>Odyssey.Sim</c>, which this
        /// assembly cannot see.
        /// </summary>
        /// <remarks>
        /// <b>The slot is not decoration.</b> Both draws behind a colonist mix the pawn's id in,
        /// and the id comes from the slot — so the same seed in slot 0 and slot 2 is two different
        /// people. A card rolled without it would show the right name and the wrong skills for two
        /// of the three, and the player would only find out after pressing Start.
        /// </remarks>
        public ColonistSelect(Func<uint, int, Candidate> roll) =>
            _roll = roll ?? throw new ArgumentNullException(nameof(roll));

        /// <summary>The three, in the order they are drawn.</summary>
        public IReadOnlyList<Candidate> Cards => _cards;

        /// <summary>Whether this slot is kept through a reroll.</summary>
        public bool IsLocked(int slot) => slot >= 0 && slot < Slots && _locked[slot];

        /// <summary>
        /// Which candidate's detail is showing (world setup). The three are always on screen — you
        /// are choosing between them — and this is the one whose skills are drawn beside them.
        ///
        /// <para><b>Selecting and keeping are two gestures, not one.</b> On this page a click
        /// already means "show me this one", so it cannot also mean "hold on to this one": the
        /// Keep control is separate. That is the opposite of the little panel U40 built, where a
        /// card had nothing else to mean.</para>
        /// </summary>
        public int Selected { get; private set; }

        /// <summary>Show this one's detail. False for a slot that is not on screen, and a no-op
        /// when it is already showing.</summary>
        public bool Select(int slot)
        {
            if (slot < 0 || slot >= Slots) return false;
            if (Selected == slot) return true;

            Selected = slot;
            Changed?.Invoke();
            return true;
        }

        /// <summary>The candidate whose detail is showing.</summary>
        public Candidate Current => _cards[Selected];

        /// <summary>
        /// The name the player typed over this slot, or null where they have taken the one they
        /// were dealt. What <see cref="ChosenNames"/> carries into the colony.
        /// </summary>
        public string? GivenName(int slot) =>
            slot >= 0 && slot < Slots ? _given[slot] : null;

        /// <summary>What this slot is called on screen: the typed name where there is one, the
        /// dealt one otherwise.</summary>
        public string DisplayName(int slot) =>
            slot >= 0 && slot < Slots ? _given[slot] ?? _cards[slot].Name : string.Empty;

        /// <summary>"Wrenn, 34" for a slot, with the typed name where there is one — the line at
        /// the top of a card and of the detail beside it.</summary>
        public string DisplayNameAndAge(int slot)
        {
            if (slot < 0 || slot >= Slots) return string.Empty;

            int age = _cards[slot].Age;
            string name = DisplayName(slot);
            return age > 0 ? name + ", " + age : name;
        }

        /// <summary>
        /// Name this one yourself, or — with a name that cleans to nothing — stop, which puts the
        /// dealt name back.
        ///
        /// <para><b>Cleaned here rather than at the keyboard</b>, by the same
        /// <see cref="ColonistNameBook"/> that will hold it once the colony exists: the rule about
        /// what a name may be has one owner, so the card, the roster and the save cannot come to
        /// disagree about whether trailing spaces count.</para>
        /// </summary>
        /// <returns>Whether anything changed, so a presenter echoing every keystroke does not
        /// redraw three cards on the ones that altered nothing.</returns>
        public bool Rename(int slot, string? typed)
        {
            if (slot < 0 || slot >= Slots) return false;

            string clean = ColonistNameBook.Clean(typed);
            string? held = _given[slot];
            if (clean.Length == 0)
            {
                if (held == null) return false;
                _given[slot] = null;
            }
            else
            {
                if (held == clean) return false;
                _given[slot] = clean;
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// The three typed names, in slot order, null where the dealt name stands — what the
        /// composition root writes into <see cref="ColonistNames.Book"/> once the colony's pawns
        /// exist.
        /// </summary>
        public string?[] ChosenNames()
        {
            var names = new string?[Slots];
            for (int i = 0; i < Slots; i++) names[i] = _given[i];
            return names;
        }

        /// <summary>How many are locked, which is what tells a presenter that Reroll would do
        /// nothing.</summary>
        public int LockedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Slots; i++) if (_locked[i]) n++;
                return n;
            }
        }

        /// <summary>
        /// Whether Reroll would change anything. False with every slot locked, and the presenter
        /// draws the row inert from it — a row that silently does nothing reads as broken, and the
        /// player's next move is to press it harder rather than to unlock somebody.
        /// </summary>
        public bool CanReroll => LockedCount < Slots;

        /// <summary>Raised whenever anything the screen draws has changed.</summary>
        public event Action? Changed;

        /// <summary>
        /// Deal a fresh set and forget every lock.
        ///
        /// <para>Called when the screen is entered, for the reason <see cref="SeedField.Draw"/> is:
        /// arriving at a set you have already walked away from reads as a screen that has not
        /// noticed you left. A lock is an opinion about a particular three, so it goes with
        /// them.</para>
        /// </summary>
        public void Deal(Func<uint> seeds)
        {
            if (seeds == null) throw new ArgumentNullException(nameof(seeds));

            for (int i = 0; i < Slots; i++) _locked[i] = false;
            for (int i = 0; i < Slots; i++) _given[i] = null;
            for (int i = 0; i < Slots; i++) _cards[i] = DrawUnused(seeds, i);
            Selected = 0;
            Changed?.Invoke();
        }

        /// <summary>Deal again, keeping whatever is locked.</summary>
        public void Reroll(Func<uint> seeds)
        {
            if (seeds == null) throw new ArgumentNullException(nameof(seeds));
            if (!CanReroll) return;

            // A typed name goes with the person it was given to (owner, 2026-09-18). A reroll
            // deals a different face, different skills and a different name into the slot, so a
            // name left standing over it would be the player's word attached to somebody they
            // have never seen — the same reason §2 decision 2 moved the dealt name off the slot
            // and on to the roll. A locked slot keeps both, which is what locking means.
            for (int i = 0; i < Slots; i++)
                if (!_locked[i])
                {
                    _given[i] = null;
                    _cards[i] = DrawUnused(seeds, i);
                }

            Changed?.Invoke();
        }

        /// <summary>
        /// Keep this one, or stop keeping it. False for a slot that is not on screen.
        /// </summary>
        public bool ToggleLock(int slot)
        {
            if (slot < 0 || slot >= Slots) return false;

            _locked[slot] = !_locked[slot];
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// The three seeds, in slot order — what <c>ColonyRequest.Colonists</c> takes.
        /// </summary>
        public uint[] ChosenSeeds()
        {
            var seeds = new uint[Slots];
            for (int i = 0; i < Slots; i++) seeds[i] = _cards[i].Seed;
            return seeds;
        }

        /// <summary>
        /// Draw a candidate whose name no other slot is already using.
        ///
        /// <para>Bounded, and the bound is the point: the pool is small, so under an unlucky draw
        /// an unbounded loop would be a hang on the one screen a player cannot leave. After the
        /// attempts are spent the last draw is taken as it stands — two candidates sharing a name
        /// is a blemish somebody can reroll, and a frozen screen is not.</para>
        /// </summary>
        Candidate DrawUnused(Func<uint> seeds, int slot)
        {
            Candidate drawn = _roll(seeds(), slot);
            for (int attempt = 0; attempt < 8 && NameIsTaken(drawn.Name, slot); attempt++)
                drawn = _roll(seeds(), slot);
            return drawn;
        }

        bool NameIsTaken(string name, int exceptSlot)
        {
            for (int i = 0; i < Slots; i++)
            {
                if (i == exceptSlot) continue;

                // A slot that has not been dealt yet holds `default(Candidate)`, and that name is
                // **null, not empty**: a struct's default bypasses its own constructor, so the
                // null-coalescing there never runs. It collides with nothing either way, which is
                // what lets Deal fill the three in order without special-casing the first — but the
                // check has to be null-safe rather than length-safe, which the first version was
                // not and every test in this file said so at once.
                if (!string.IsNullOrEmpty(_cards[i].Name) && _cards[i].Name == name) return true;
            }
            return false;
        }
    }
}
