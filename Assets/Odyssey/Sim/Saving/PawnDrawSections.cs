#nullable enable
using System;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Saving
{
    /// <summary>
    /// Every pawn's <see cref="Pawn.Profile"/> (design 41 §4.4), as a section of its own for the
    /// reason <see cref="PawnSeedSection"/> is one: the pawn record has no version, and a section
    /// is skippable in both directions.
    ///
    /// <para><b>A save written before the draw has no such section</b>, so every restored pawn
    /// keeps the constructor's <see cref="RollProfile.Legacy"/> — which is exactly what that colony
    /// was, and is what keeps its colonists walking at the pace they were rolled at, since pace is
    /// re-derived from the seed and the band is the profile's.</para>
    ///
    /// <para>Keyed by pawn id. Hashing is <see cref="Pawn.ContributeTo"/>'s.</para>
    /// </summary>
    public sealed class PawnProfileSection : ISaveable
    {
        readonly PawnRegistry _pawns;

        public PawnProfileSection(PawnRegistry pawns) =>
            _pawns = pawns ?? throw new ArgumentNullException(nameof(pawns));

        public string SaveKey => "odyssey.pawn.profile";

        public void Save(SaveWriter writer)
        {
            var all = _pawns.All;
            writer.Write(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                writer.Write(all[i].Id.Value);
                writer.Write((int)all[i].Profile);
            }
        }

        public void Load(SaveReader reader)
        {
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int id = reader.ReadInt();
                int profile = reader.ReadInt();
                Pawn? pawn = _pawns.Get(new PawnId(id));
                // An id this save no longer holds, or a profile this build does not know, is
                // dropped rather than fatal: the pawn keeps Legacy, which rolls as it always did.
                if (pawn != null && Enum.IsDefined(typeof(RollProfile), (byte)profile))
                    pawn.Profile = (RollProfile)profile;
            }
        }
    }

    /// <summary>
    /// Every pawn's traits (design 41 §3.5): per pawn, its id, a count, and that many
    /// <see cref="TraitHandle"/> indices. A save without the section loads with nobody holding a
    /// trait, which is what it meant. No format bump, for <see cref="PawnSeedSection"/>'s reason.
    /// </summary>
    public sealed class PawnTraitSection : ISaveable
    {
        readonly PawnRegistry _pawns;

        public PawnTraitSection(PawnRegistry pawns) =>
            _pawns = pawns ?? throw new ArgumentNullException(nameof(pawns));

        public string SaveKey => "odyssey.pawn.traits";

        public void Save(SaveWriter writer)
        {
            var all = _pawns.All;
            writer.Write(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                int[] traits = all[i].Traits;
                writer.Write(all[i].Id.Value);
                writer.Write(traits.Length);
                for (int t = 0; t < traits.Length; t++) writer.Write(traits[t]);
            }
        }

        public void Load(SaveReader reader)
        {
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int id = reader.ReadInt();
                int n = reader.ReadInt();
                if (n < 0 || n > 64) throw new InvalidOperationException($"a pawn with {n} traits is not a save this build wrote.");
                var traits = new int[n];
                int kept = 0;
                for (int t = 0; t < n; t++)
                {
                    int handle = reader.ReadInt();
                    // A trait this build does not have is dropped, not fatal: the colonist loses
                    // one line of character rather than the whole colony being refused.
                    if (handle >= 0 && handle < TraitHandle.Count) traits[kept++] = handle;
                }

                Pawn? pawn = _pawns.Get(new PawnId(id));
                if (pawn == null) continue;
                if (kept != n) Array.Resize(ref traits, kept);
                pawn.Traits = traits;
            }
        }
    }
}
