#nullable enable
using System;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Saving
{
    /// <summary>
    /// Each colonist's traits (design 44 §3), as one save section on exactly the terms of
    /// <see cref="PawnSeedSection"/> and <see cref="PawnKindSection"/>: keyed by pawn id, skippable in
    /// both directions, and <b>no format bump</b>. A save written before traits has no section here,
    /// so <see cref="Load"/> never runs and every restored colonist has none — which is what she was,
    /// and the owner's "new colonies only".
    ///
    /// <para><b>Only pawns with something to say are written</b>, so a colony of traitless pawns
    /// writes a count of nought. The record carries a trailing field for the kind of break a pawn is
    /// in (design 44 §5c), written as nought until the break taxonomy fills it; it is here from the
    /// first write so the section never has to change shape.</para>
    ///
    /// <para>Hashing is <see cref="Pawn.ContributeTo"/>'s, not this section's.</para>
    /// </summary>
    public sealed class PawnMindSection : ISaveable
    {
        readonly PawnRegistry _pawns;

        public PawnMindSection(PawnRegistry pawns) =>
            _pawns = pawns ?? throw new ArgumentNullException(nameof(pawns));

        public string SaveKey => "odyssey.pawn.mind";

        public void Save(SaveWriter writer)
        {
            var all = _pawns.All;
            int count = 0;
            for (int i = 0; i < all.Count; i++) if (HasAnything(all[i])) count++;
            writer.Write(count);
            for (int i = 0; i < all.Count; i++)
            {
                Pawn pawn = all[i];
                if (!HasAnything(pawn)) continue;
                writer.Write(pawn.Id.Value);
                writer.Write(pawn.Traits.Count);
                for (int t = 0; t < pawn.Traits.Count; t++) writer.Write(pawn.Traits[t]);
                writer.Write(pawn.BreakKind);
            }
        }

        public void Load(SaveReader reader)
        {
            int count = reader.ReadInt();
            int known = _pawns.TraitCount;
            int breaks = _pawns.BreakCount;
            for (int i = 0; i < count; i++)
            {
                int id = reader.ReadInt();
                int traits = reader.ReadInt();
                if (traits < 0 || traits > Contracts.TraitHandle.MaxPerPawn)
                    throw new SaveLoadException($"The save gives pawn {id} {traits} traits.");

                Pawn? pawn = _pawns.Get(new Contracts.PawnId(id));
                pawn?.Traits.Clear();
                for (int t = 0; t < traits; t++)
                {
                    int trait = reader.ReadInt();
                    // A trait this build does not have is refused rather than dropped: a colonist
                    // silently losing who she is on a load is the kind of change nothing reports.
                    if (trait < 0 || trait >= known)
                        throw new SaveLoadException($"The save gives pawn {id} trait {trait} and this build has {known}.");
                    pawn?.Traits.Add(trait);
                }

                int breakKind = reader.ReadInt();
                if (breakKind < 0 || breakKind >= breaks)
                    throw new SaveLoadException($"The save gives pawn {id} break {breakKind} and this build has {breaks}.");
                if (pawn != null) pawn.BreakKind = breakKind;
            }
        }

        static bool HasAnything(Pawn pawn) => pawn.Traits.Count > 0 || pawn.BreakKind != 0;
    }
}
