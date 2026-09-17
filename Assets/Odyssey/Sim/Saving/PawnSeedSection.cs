#nullable enable
using System;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Saving
{
    /// <summary>
    /// Every pawn's own roll seed (U40), as one save section.
    ///
    /// <para><b>Why a section of its own rather than four more bytes in the pawn record.</b> The
    /// pawn record has no version of its own and <see cref="ISaveable.Load"/> is handed no format
    /// version, so a field appended to it would make every save written before today unreadable —
    /// or would need the container version bumped and a branch that nothing in this codebase has a
    /// way to express. A section is skippable in both directions: a save that has one is read by a
    /// build that does not, and a save that lacks one is simply never dispatched to this component
    /// at all.</para>
    ///
    /// <para><b>The fallback is the whole reason that matters.</b> A save written before U40 has no
    /// section here, so <see cref="Load"/> never runs and every restored pawn keeps the seed
    /// <see cref="PawnRegistry.Load"/> gave it — the world's. That is not a degraded reading of an
    /// old save: it is exactly what that colony was, because before this unit every colonist rolled
    /// from the world seed and nothing else. The old save is read correctly rather than merely
    /// read.</para>
    ///
    /// <para><b>Keyed by pawn id, not written in list order.</b> The registry's own order is an
    /// implementation detail of a list, and a colonist that dies in a later milestone will make it
    /// one that changes; a seed applied to the wrong pawn would rename two colonists and swap what
    /// they are good at, silently, on a load. An id is what a pawn is.</para>
    ///
    /// <para>Hashing is <see cref="Pawn.ContributeTo"/>'s, not this section's — the seed travels
    /// with the pawn it belongs to, so a section that hashed it again would count it twice and say
    /// nothing new. This one only writes it down.</para>
    /// </summary>
    public sealed class PawnSeedSection : ISaveable
    {
        readonly PawnRegistry _pawns;

        public PawnSeedSection(PawnRegistry pawns) =>
            _pawns = pawns ?? throw new ArgumentNullException(nameof(pawns));

        public string SaveKey => "odyssey.pawn.seeds";

        public void Save(SaveWriter writer)
        {
            var all = _pawns.All;
            writer.Write(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                writer.Write(all[i].Id.Value);
                writer.Write(unchecked((int)all[i].RollSeed));
            }
        }

        public void Load(SaveReader reader)
        {
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int id = reader.ReadInt();
                uint seed = unchecked((uint)reader.ReadInt());

                // A seed for a pawn this save no longer holds is dropped rather than fatal, for the
                // reason an unknown section is skipped: the file is describing something this build
                // has no place to put, and refusing the whole colony over it helps nobody.
                Pawn? pawn = _pawns.Get(new Contracts.PawnId(id));
                if (pawn != null) pawn.RollSeed = seed;
            }
        }
    }
}
