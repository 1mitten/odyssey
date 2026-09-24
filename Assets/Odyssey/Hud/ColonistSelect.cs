#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

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
        /// The traits they were dealt, as <see cref="TraitHandle"/> indices in the order dealt
        /// (design 41 §3). The interface names each through <see cref="TraitHandle.Keys"/>.
        /// </summary>
        public readonly IReadOnlyList<int> Traits;

        /// <summary>Their walking pace, per mille of an ordinary colonist's — the SPD the machine spins.</summary>
        public readonly int PacePerMille;

        /// <summary>Which tables they were drawn from: what the colony is told to roll them with.</summary>
        public readonly RollProfile Profile;

        public Candidate(uint seed, string name, int age, string occupation,
            IReadOnlyList<SkillRow>? skills, IReadOnlyList<int>? traits = null, int pacePerMille = 1_000,
            RollProfile profile = RollProfile.Standard)
        {
            Seed = seed;
            Name = name ?? string.Empty;
            Age = age;
            Occupation = occupation ?? string.Empty;
            Skills = skills ?? Array.Empty<SkillRow>();
            Traits = traits ?? Array.Empty<int>();
            PacePerMille = pacePerMille;
            Profile = profile;
        }

        /// <summary>"Wrenn, 34" — the line at the top of a card and of the detail beside it.</summary>
        public string NameAndAge => Age > 0 ? Name + ", " + Age : Name;
    }

    /// <summary>
    /// How the three are chosen (design 41 §5.1): dealt and rerolled until you like them, or
    /// pulled once each on the machine. One or the other, for the whole colony.
    /// </summary>
    public enum CreationMode
    {
        Standard,
        Gamble,
    }

    /// <summary>Where one gamble slot stands (design 41 §5.3). Standard's slots are always <see cref="Dealt"/>.</summary>
    public enum SlotState
    {
        /// <summary>A Standard card: dealt, and rerollable unless kept.</summary>
        Dealt,

        /// <summary>A gamble slot nobody has pulled.</summary>
        Unpulled,

        /// <summary>Pulled: the colonist is drawn and the machine is revealing them.</summary>
        Spinning,

        /// <summary>Revealed and kept for good. Only the name can change.</summary>
        Landed,
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
        /// <summary>The registry keys naming the two creation modes (design 41 §5.1).</summary>
        public const string StandardKey = "ui.newgame.mode.standard";

        public const string GambleKey = "ui.newgame.mode.gamble";

        public static readonly string[] IconKeys = { TitleKey, RerollKey, LockKey, StandardKey, GambleKey };

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

        readonly Func<uint, int, RollProfile, Candidate> _roll;

        readonly SlotState[] _state = new SlotState[Slots];

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
        public ColonistSelect(Func<uint, int, RollProfile, Candidate> roll) =>
            _roll = roll ?? throw new ArgumentNullException(nameof(roll));

        /// <summary>A select screen whose roll does not care which tables it is asked for — a
        /// test's stub, which names people and nothing else.</summary>
        public ColonistSelect(Func<uint, int, Candidate> roll)
        {
            if (roll == null) throw new ArgumentNullException(nameof(roll));
            _roll = (seed, slot, _) => roll(seed, slot);
        }

        // ---- the mode (design 41 §5) ---------------------------------------------------------------

        /// <summary>Standard or Gamble. Standard until the player says otherwise.</summary>
        public CreationMode Mode { get; private set; } = CreationMode.Standard;

        /// <summary>Where a slot stands. <see cref="SlotState.Dealt"/> for every Standard slot.</summary>
        public SlotState StateOf(int slot) => slot >= 0 && slot < Slots ? _state[slot] : SlotState.Dealt;

        /// <summary>Whether any slot has been pulled — the moment the choice of mode is made for good.</summary>
        public bool AnyPulled
        {
            get
            {
                for (int i = 0; i < Slots; i++)
                    if (_state[i] == SlotState.Spinning || _state[i] == SlotState.Landed) return true;
                return false;
            }
        }

        /// <summary>
        /// Whether the mode may still be changed: only before the first pull (design 41 §5.1, the
        /// owner's "you have to choose one or the other"). Enforced here, not by the view.
        /// </summary>
        public bool CanChangeMode => !AnyPulled;

        /// <summary>
        /// Switch between Standard and Gamble. Refused after the first pull. Standard deals a fresh
        /// three; Gamble lays three face-down cards. Locks and typed names go with the cards.
        /// </summary>
        public bool SetMode(CreationMode mode, Func<uint> seeds)
        {
            if (seeds == null) throw new ArgumentNullException(nameof(seeds));
            if (mode == Mode) return true;
            if (!CanChangeMode) return false;

            Mode = mode;
            if (mode == CreationMode.Standard)
            {
                Deal(seeds);
                return true;
            }

            for (int i = 0; i < Slots; i++)
            {
                _locked[i] = false;
                _given[i] = null;
                _cards[i] = default;
                _state[i] = SlotState.Unpulled;
            }

            Selected = 0;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Pull this slot: draw its colonist from the Gamble tables, now, before a reel has moved
        /// (design 41 §2 decision 2, and the spec's "the spin only reveals it"). Refused for a slot
        /// already pulled, in Standard, or while another slot is still being revealed.
        /// </summary>
        public bool Pull(int slot, Func<uint> seeds)
        {
            if (seeds == null) throw new ArgumentNullException(nameof(seeds));
            if (Mode != CreationMode.Gamble || slot < 0 || slot >= Slots) return false;
            if (_state[slot] != SlotState.Unpulled) return false;
            if (Spinning >= 0) return false;

            _cards[slot] = DrawUnused(seeds, slot);
            _state[slot] = SlotState.Spinning;
            Selected = slot;
            Changed?.Invoke();
            return true;
        }

        /// <summary>The machine has finished revealing this slot: it is kept for good.</summary>
        public bool Land(int slot)
        {
            if (slot < 0 || slot >= Slots || _state[slot] != SlotState.Spinning) return false;
            _state[slot] = SlotState.Landed;
            Changed?.Invoke();
            return true;
        }

        /// <summary>The slot being revealed, or -1.</summary>
        public int Spinning
        {
            get
            {
                for (int i = 0; i < Slots; i++) if (_state[i] == SlotState.Spinning) return i;
                return -1;
            }
        }

        /// <summary>The first slot nobody has pulled, or -1 when all three have been.</summary>
        public int NextUnpulled
        {
            get
            {
                for (int i = 0; i < Slots; i++) if (_state[i] == SlotState.Unpulled) return i;
                return -1;
            }
        }

        /// <summary>
        /// Whether Start may build the colony: always in Standard, and in Gamble only once all three
        /// have landed (design 41 §5.3). <c>MenuDirector.Start</c> refuses on it as well as the view
        /// drawing it.
        /// </summary>
        public bool CanStart
        {
            get
            {
                if (Mode == CreationMode.Standard) return true;
                for (int i = 0; i < Slots; i++) if (_state[i] != SlotState.Landed) return false;
                return true;
            }
        }

        /// <summary>Which tables each slot was drawn from, in slot order — what <c>ColonyRequest.Profiles</c> takes.</summary>
        public RollProfile[] ChosenProfiles()
        {
            var profiles = new RollProfile[Slots];
            for (int i = 0; i < Slots; i++) profiles[i] = _cards[i].Profile;
            return profiles;
        }

        /// <summary>
        /// Let go of a gamble: what Start does once the colony is asked for, so the next New game is
        /// a new decision rather than the same machine (pulls otherwise stick, design 41 §5.3). Only
        /// the mode and the slot states go — the cards stay readable until the next
        /// <see cref="Deal"/> replaces them, so anything reading the page as the colony is built
        /// still reads the colony.
        /// </summary>
        public void Forget()
        {
            Mode = CreationMode.Standard;
            for (int i = 0; i < Slots; i++)
            {
                _locked[i] = false;
                _state[i] = SlotState.Dealt;
            }

            Changed?.Invoke();
        }

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
            // Nobody to name until they have been revealed: a name is not a stat (design 19 §10),
            // but a face-down card and one still spinning have nobody on them yet.
            if (_state[slot] == SlotState.Unpulled || _state[slot] == SlotState.Spinning) return false;

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
        public bool CanReroll => Mode == CreationMode.Standard && LockedCount < Slots;

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

            // Pulls stick (design 41 §5.3, owner's decision 6): Back and New game must not wash a
            // pull out. A reveal Back interrupted is finished rather than lost — the colonist was
            // drawn at the press, and only the show was cut short.
            if (Mode == CreationMode.Gamble && AnyPulled)
            {
                for (int i = 0; i < Slots; i++)
                    if (_state[i] == SlotState.Spinning) _state[i] = SlotState.Landed;
                Changed?.Invoke();
                return;
            }

            Mode = CreationMode.Standard;
            for (int i = 0; i < Slots; i++) _state[i] = SlotState.Dealt;
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
            // Gamble has no Keep: every landed card is kept already, and an unpulled one has nobody.
            if (Mode != CreationMode.Standard) return false;

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
            RollProfile profile = Mode == CreationMode.Gamble ? RollProfile.Gamble : RollProfile.Standard;
            Candidate drawn = _roll(seeds(), slot, profile);
            for (int attempt = 0; attempt < 8 && NameIsTaken(drawn.Name, slot); attempt++)
                drawn = _roll(seeds(), slot, profile);
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
