#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The one place that answers "what does this colonist look like".
    ///
    /// <para><b>Why an object rather than a pair of matching settings.</b> Two separate things draw
    /// colonists — the live animated figures for the pawns on screen, and the baked instanced form
    /// for everyone past the figure cap or on a machine with no art — and they must agree, or a
    /// colonist changes identity the moment the colony grows past the cap or the camera moves.
    /// That used to be kept by handing the same salt to two properties and trusting two doc
    /// comments to stay honest. It is now kept by both drawers holding <i>this object</i>, so
    /// agreement is a fact about the object graph and a refactor that clones it fails a test.</para>
    ///
    /// <para><b>The look index space is the catalogue family index, always.</b> Not "rows that
    /// happen to have usable art". The two drawers previously disagreed about this in a way
    /// nothing could catch: the instanced path sized its lottery from every row of the family,
    /// while the figure director dropped rows with no prefab or no usable gait and <i>compacted
    /// the survivors</i>, so look <c>i</c> was not row <c>i</c> the moment anything was missing.
    /// Holes are handled where they belong — a drawer that cannot realise a look declines to draw
    /// that pawn and lets the other path have it — so a missing row degrades one colonist instead
    /// of silently re-dealing the whole colony.</para>
    ///
    /// <para><b>The seam for a later appearance panel.</b> <see cref="Override"/> is where a
    /// player-chosen appearance would go. It is empty, it is not serialised, and nothing writes to
    /// it yet; when the panel lands it is the overrides that enter the save, never the derivation.
    /// </para>
    /// </summary>
    public sealed class ColonistAppearanceBook
    {
        readonly Dictionary<int, Entry> _cache = new Dictionary<int, Entry>();
        readonly Dictionary<int, ColonistAppearance> _overrides = new Dictionary<int, ColonistAppearance>();

        /// <summary>
        /// The world seed the cast is dealt from, so the same world deals the same people on every
        /// load. Zero is legal and simply deals a particular cast; it is not treated as "unset".
        /// </summary>
        public uint Seed { get; }

        /// <summary>
        /// How many colonist rows the catalogue has — every row, holes included. One when there is
        /// no catalogue at all, so a clone without the licensed packs still answers every question
        /// rather than dividing by zero.
        /// </summary>
        public int LookCount { get; }

        /// <summary>
        /// <para><b>The catalogue is not named here, and that is what lets this file live in
        /// <c>Odyssey.Hud</c>.</b> Counting the colonist family was the book's only tie to Unity;
        /// it is now <c>AppearanceBooks.For</c> on the Presentation side, which is where the
        /// catalogue already lives. What is bought by the move is that the whole derivation — and
        /// the 300 lines of test that pin it — runs in the fast tier, and that the HUD can ask
        /// what a colonist looks like at all, which is what a flat avatar needs
        /// (<c>docs/design/20-avatars.md</c>).</para>
        /// </summary>
        public ColonistAppearanceBook(uint seed, int lookCount)
        {
            Seed = seed;
            LookCount = lookCount < 1 ? 1 : lookCount;
        }

        /// <summary>
        /// The appearance of a pawn: an override if one was set, otherwise derived.
        ///
        /// <para><b><paramref name="rollSeed"/> is the pawn's own, and zero means it has none.</b>
        /// A colonist is dealt from the seed they were rolled from, the same one their name, age,
        /// trade and skills come from — so rerolling a candidate on the setup screen changes the
        /// face along with everything else, and the person chosen there is the person who walks
        /// around (<c>docs/design/20-avatars.md</c> §5). Before this the whole cast came off one
        /// world-level seed, which no card could know.</para>
        ///
        /// <para><b>Zero falls back to <see cref="Seed"/>, and that is the compatibility path</b>
        /// rather than a guard: <c>ColonistNames.RollSeedOf</c> answers zero for a save written
        /// before U40, and those colonies should keep the faces they had.</para>
        /// </summary>
        /// <summary>
        /// Deal the whole cast from this seed, whatever roll seed a colonist carries. Zero, the
        /// default, means every colonist is dealt from their own.
        ///
        /// <para><b>It is how the two development switches survived the change.</b>
        /// <c>colonistLookSeed</c> pins a cast the owner liked while the palette is being judged,
        /// and <c>randomCastEachSession</c> deals fresh faces on every Play. Both are about
        /// looking at lots of colonists quickly, and both would simply have stopped working once a
        /// face followed the pawn instead of the world — so rather than leave two inspector fields
        /// that quietly do nothing, they set this.</para>
        /// </summary>
        public uint Pinned { get; set; }

        public ColonistAppearance For(int pawnId, uint rollSeed)
        {
            if (_overrides.TryGetValue(pawnId, out ColonistAppearance chosen)) return chosen;

            uint seed = Pinned != 0u ? Pinned : rollSeed != 0u ? rollSeed : Seed;

            // Keyed on the pawn and checked against the seed. A pawn's roll seed does not change
            // once it has one, so in the ordinary case this is a plain hit — but a figure asked
            // for before the aspect arrived would otherwise be cached on the fallback for ever.
            if (_cache.TryGetValue(pawnId, out Entry cached) && cached.Seed == seed)
                return cached.Appearance;

            ColonistAppearance made = ColonistAppearance.Of(seed, pawnId, LookCount);
            _cache[pawnId] = new Entry(seed, made);
            return made;
        }

        /// <summary>Which body a pawn wears. Shorthand for <c>For(pawnId, rollSeed).Look</c>.</summary>
        public int LookFor(int pawnId, uint rollSeed) => For(pawnId, rollSeed).Look;

        /// <summary>The appearance of a pawn in the published frame — the ordinary way to ask.</summary>
        public ColonistAppearance For(WorldSnapshot snapshot, PawnId pawn) =>
            For(pawn.Value, ColonistNames.RollSeedOf(snapshot, pawn));

        /// <summary>Which body a pawn in the published frame wears.</summary>
        public int LookFor(WorldSnapshot snapshot, PawnId pawn) =>
            For(snapshot, pawn).Look;

        readonly struct Entry
        {
            public readonly uint Seed;
            public readonly ColonistAppearance Appearance;

            public Entry(uint seed, ColonistAppearance appearance)
            {
                Seed = seed;
                Appearance = appearance;
            }
        }

        /// <summary>
        /// Give one pawn an appearance of its own, for the appearance panel that does not exist
        /// yet. Present so the shape is right; nothing in the game calls it.
        /// </summary>
        public void Override(int pawnId, in ColonistAppearance appearance)
        {
            _overrides[pawnId] = appearance;
            _cache.Remove(pawnId);
        }

        /// <summary>Forget a pawn's override and go back to what the seed says.</summary>
        public void ClearOverride(int pawnId)
        {
            _overrides.Remove(pawnId);
            _cache.Remove(pawnId);
        }

        /// <summary>How many pawns carry an override. Zero in the game today.</summary>
        public int OverrideCount => _overrides.Count;
    }
}
