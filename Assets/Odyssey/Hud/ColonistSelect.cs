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

        /// <summary>The skills worth reading, best first, already cut to what fits.</summary>
        public readonly IReadOnlyList<CandidateSkill> Skills;

        public Candidate(uint seed, string name, IReadOnlyList<CandidateSkill>? skills)
        {
            Seed = seed;
            Name = name ?? string.Empty;
            Skills = skills ?? Array.Empty<CandidateSkill>();
        }
    }

    /// <summary>One line of a candidate's card: a skill's registry key, its name and its level.</summary>
    public readonly struct CandidateSkill
    {
        public readonly string Key;
        public readonly string Label;
        public readonly int Level;

        public CandidateSkill(string key, string label, int level)
        {
            Key = key;
            Label = label;
            Level = level;
        }
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
        readonly Func<uint, Candidate> _roll;

        /// <summary>
        /// The seam the presenter fills and a test drives: given a seed, the candidate it produces.
        /// Rolling needs <c>Odyssey.Sim</c>, which this assembly cannot see.
        /// </summary>
        public ColonistSelect(Func<uint, Candidate> roll) =>
            _roll = roll ?? throw new ArgumentNullException(nameof(roll));

        /// <summary>The three, in the order they are drawn.</summary>
        public IReadOnlyList<Candidate> Cards => _cards;

        /// <summary>Whether this slot is kept through a reroll.</summary>
        public bool IsLocked(int slot) => slot >= 0 && slot < Slots && _locked[slot];

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
            for (int i = 0; i < Slots; i++) _cards[i] = DrawUnused(seeds, i);
            Changed?.Invoke();
        }

        /// <summary>Deal again, keeping whatever is locked.</summary>
        public void Reroll(Func<uint> seeds)
        {
            if (seeds == null) throw new ArgumentNullException(nameof(seeds));
            if (!CanReroll) return;

            for (int i = 0; i < Slots; i++)
                if (!_locked[i])
                    _cards[i] = DrawUnused(seeds, i);

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
            Candidate drawn = _roll(seeds());
            for (int attempt = 0; attempt < 8 && NameIsTaken(drawn.Name, slot); attempt++)
                drawn = _roll(seeds());
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
