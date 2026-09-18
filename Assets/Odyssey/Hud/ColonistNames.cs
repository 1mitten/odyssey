#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// Colonist given names, keyed by <see cref="PawnId"/>.
    ///
    /// The pool is <c>docs/design/colonist-names.csv</c>, generated into
    /// <see cref="ColonistNamePool"/>: given names only, no surnames, because a holding is small
    /// enough to be on first-name terms and a single short name fits the roster bar, the densest
    /// region in the interface. The simulation has no names and no opinion about names; this is
    /// an interface-side identity, stable for a pawn's whole life because <see cref="PawnId"/>
    /// is stable for a pawn's whole life.
    ///
    /// Deterministic on the id, never random: a colonist keeps one name across saves, sessions
    /// and screenshots, which is what makes a screenshot caption trustworthy.
    /// </summary>
    public static class ColonistNames
    {
        /// <summary>
        /// The name the simulation publishes a colonist's roll seed under.
        ///
        /// <para>A string literal and not a shared constant, on purpose: this assembly cannot
        /// reference <c>Odyssey.Sim</c> at all, and a constant both sides imported would be the
        /// shared file the <c>PawnAspect</c> seam exists to avoid. <see cref="SkillCatalogue"/>
        /// makes the same bargain for the same reason, and a Sim-side test holds the two spellings
        /// together.</para>
        /// </summary>
        public const string RollSeedAspect = "odyssey.pawn.rollseed";

        static readonly AspectKey RollSeedKey = AspectKey.Of(RollSeedAspect);

        /// <summary>
        /// The name a colonist in the published frame goes by — the ordinary way to ask.
        ///
        /// <para>A pawn with no seed published falls back to zero, which is a name rather than a
        /// blank: a colonist whose aspect went missing should look like somebody, not like a
        /// bug.</para>
        /// </summary>
        public static string Of(WorldSnapshot snapshot, PawnId id) => Of(RollSeedOf(snapshot, id), id);

        /// <summary>
        /// The seed a colonist in the published frame was rolled from — what every derived identity
        /// keys on, so it is read in one place rather than in each of them.
        ///
        /// <para>Zero when nothing published one, which is a person rather than a blank: a colonist
        /// whose aspect went missing should look like somebody, not like a bug.</para>
        /// </summary>
        public static uint RollSeedOf(WorldSnapshot snapshot, PawnId id) =>
            snapshot != null && snapshot.TryGetPawnAspect(id, RollSeedKey, out int value)
                ? unchecked((uint)value)
                : 0u;

        /// <summary>
        /// The pool, generated from <c>docs/design/colonist-names.csv</c> — the one place a
        /// colonist's name is decided, which is also what the wiki lists so the owner can correct
        /// any of them (owner, 2026-09-18: *"we need a big pool of random names"*, and *"make it
        /// performant then and centralise it if need be"*).
        ///
        /// <para><b>The promise this keeps.</b> This was eight names lifted from the mockups, with
        /// a comment saying it "extends to about forty at M2, when pawn generation needs a pool
        /// that does not repeat in a colony of fifty". It is 244, which is six times what that
        /// promise asked for and enough that the <see cref="Of"/> cycle suffix below — the
        /// "Wrenn 2" that a ninth colonist used to get — is unreachable by any colony this game
        /// will build.</para>
        ///
        /// <para><b>Nothing is parsed at run time.</b> The generator writes the array as literals,
        /// so the strings live in the assembly's constant pool and naming a colonist is an index
        /// and a modulo. That matters because the roster strip and the inspect header ask per
        /// figure per frame.</para>
        /// </summary>
        static readonly string[] Pool = ColonistNamePool.Names;

        /// <summary>
        /// The name the colonist rolled from this seed goes by (U40).
        ///
        /// <para><b>Keyed on the roll rather than on the pawn id since colonist select</b> (owner,
        /// 2026-09-17). The id is a slot — first colonist, second colonist — so a name taken from
        /// it survives a reroll, and pressing Reroll would have given you the same Wrenn with
        /// different numbers rather than a different person. The seed is what a reroll changes, so
        /// it is what the name has to follow.</para>
        ///
        /// <para><b>The id is still mixed in</b>, and has to be: every colonist a world places
        /// itself shares that world's seed, so a name from the seed alone would call all five of
        /// them the same thing. What the pair gives is a name that changes when either the person
        /// or the slot does, which is exactly when a player would expect a different face.</para>
        ///
        /// <para>Still deterministic and still nowhere near the simulation: a colonist keeps one
        /// name across saves, sessions and screenshots, because the seed is saved and the id is
        /// stable for a pawn's whole life.</para>
        /// </summary>
        public static string Of(uint rollSeed, PawnId id)
        {
            // A colonist the player named answers with that name and nothing else. The count is
            // checked rather than the dictionary because the book is empty in most colonies and
            // this is asked per figure per frame: an empty book costs one integer compare, and
            // the paragraph below about allocating nothing stays true.
            if (Book.Count > 0)
            {
                string? given = Book.Given(id);
                if (given != null) return given;
            }

            return Rolled(rollSeed, id);
        }

        /// <summary>
        /// The colonists the player has named themselves, which <see cref="Of(uint, PawnId)"/>
        /// answers from before it reads the pool.
        ///
        /// <para><b>One book, held by the one place a name is decided.</b> It could have been a
        /// field on <c>HudDirectors</c> and passed to every caller, and that was rejected: naming
        /// is asked for in seven places across two assemblies, and a parameter seven callers may
        /// forget is a rule with seven owners — the fault pattern this project keeps meeting. The
        /// composition root clears it when a colony starts or loads, which is the whole of its
        /// lifetime.</para>
        /// </summary>
        public static ColonistNameBook Book { get; } = new ColonistNameBook();

        /// <summary>
        /// The name the pool gives this roll — what a colonist is called before anybody renames
        /// them, and what the select screen deals.
        ///
        /// <para>Separate from <see cref="Of(uint, PawnId)"/> because the select screen is naming
        /// <i>candidates</i>, and a candidate in slot 0 shares <see cref="PawnId"/> 1 with the
        /// colonist the last game named: asking <see cref="Of(uint, PawnId)"/> there would deal a
        /// fresh stranger already wearing somebody else's name.</para>
        /// </summary>
        public static string Rolled(uint rollSeed, PawnId id)
        {
            if (!id.IsValid) return "nobody";

            // The seed chooses where in the pool the colony starts reading; the id says how far
            // along from there. That shape is deliberate and it is the whole reason this is not
            // simply a hash of the pair.
            //
            // **Every colonist a world places itself shares that world's seed**, so they share an
            // offset, and their ids then walk them to as many different names as there are
            // colonists — the guarantee the old id-only scheme gave for free and the one a plain
            // hash would have thrown away. Out of a pool of eight, five colonists collided better
            // than half the time under a hash; two people in a colony of five called Wrenn is not
            // a naming scheme. The pool is 244 now and the walk still cannot repeat, which is a
            // stronger claim than "unlikely" and costs the same arithmetic.
            //
            // On the select screen the three candidates carry three different seeds and so three
            // different offsets, which can collide — `ColonistSelect` is what promises they do
            // not, because distinctness there is a fact about a screen of three rather than about
            // a name.
            int index = (id.Value - 1 + (int)(Offset(rollSeed) % (uint)Pool.Length)) % Pool.Length;

            // The cycle suffix is for a colony bigger than the pool, which at 244 names is not a
            // colony this game builds. Kept rather than deleted because it is the one branch that
            // makes the method total, and it is the only line here that allocates — so on every
            // path anybody actually walks, naming a colonist allocates nothing at all.
            int cycle = (id.Value - 1) / Pool.Length;
            return cycle == 0 ? Pool[index] : Pool[index] + " " + (cycle + 1);
        }

        /// <summary>
        /// Where in the pool a seed starts reading.
        ///
        /// <para>Avalanched rather than taken raw, so that two seeds one apart do not give two
        /// names one apart — a reroll would otherwise walk down the pool in order, which reads as
        /// a list being stepped through rather than as a new person arriving.</para>
        /// </summary>
        static uint Offset(uint seed)
        {
            unchecked
            {
                uint h = seed;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
