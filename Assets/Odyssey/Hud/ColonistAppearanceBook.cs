#nullable enable
using System.Collections.Generic;

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
        readonly Dictionary<int, ColonistAppearance> _cache = new Dictionary<int, ColonistAppearance>();
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

        /// <summary>The appearance of a pawn: an override if one was set, otherwise derived.</summary>
        public ColonistAppearance For(int pawnId)
        {
            if (_overrides.TryGetValue(pawnId, out ColonistAppearance chosen)) return chosen;
            if (_cache.TryGetValue(pawnId, out ColonistAppearance cached)) return cached;

            ColonistAppearance made = ColonistAppearance.Of(Seed, pawnId, LookCount);
            _cache[pawnId] = made;
            return made;
        }

        /// <summary>Which body a pawn wears. Shorthand for <c>For(pawnId).Look</c>.</summary>
        public int LookFor(int pawnId) => For(pawnId).Look;

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
