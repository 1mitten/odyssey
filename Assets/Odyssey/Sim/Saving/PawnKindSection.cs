#nullable enable
using System;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Saving
{
    /// <summary>
    /// Every pawn's kind (design 29 §6), as one save section, on exactly the terms of
    /// <see cref="PawnSeedSection"/>: keyed by pawn id, skippable in both directions, and no
    /// format bump. A save written before animals existed has no section here, so
    /// <see cref="Load"/> never runs and every restored pawn keeps the kind
    /// <see cref="PawnRegistry.Load"/> gave it — the colonist's, which is what that colony was.
    ///
    /// <para>Hashing is <see cref="Pawn.ContributeTo"/>'s, not this section's.</para>
    /// </summary>
    public sealed class PawnKindSection : ISaveable
    {
        readonly PawnRegistry _pawns;

        public PawnKindSection(PawnRegistry pawns) =>
            _pawns = pawns ?? throw new ArgumentNullException(nameof(pawns));

        public string SaveKey => "odyssey.pawn.kinds";

        public void Save(SaveWriter writer)
        {
            var all = _pawns.All;
            writer.Write(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                writer.Write(all[i].Id.Value);
                writer.Write(all[i].Kind);
            }
        }

        public void Load(SaveReader reader)
        {
            int count = reader.ReadInt();
            int kinds = _pawns.KindCount;
            for (int i = 0; i < count; i++)
            {
                int id = reader.ReadInt();
                int kind = reader.ReadInt();

                // A kind this build does not have is refused rather than clamped: a hog read as
                // a colonist would be a pawn silently given needs and a place in the roster.
                if (kind < 0 || kind >= kinds)
                    throw new SaveLoadException(
                        $"The save gives pawn {id} kind {kind} and this build has {kinds} kinds.");

                Pawn? pawn = _pawns.Get(new Contracts.PawnId(id));
                if (pawn != null) pawn.Kind = kind;
            }
        }
    }
}
