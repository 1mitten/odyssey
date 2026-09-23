#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Saving
{
    /// <summary>
    /// The combat state of the pawns that have any (design 33 §2a), as one save section on the
    /// kind and wildlife sections' terms: keyed by pawn id, absent from an older save — which then
    /// loads with nobody drafted — and no world format bump.
    ///
    /// <para><b>Only pawns with something to say are written</b>, so a colony that has never
    /// fought writes a count of nought. The record opens with its own layout number so the units
    /// after C1 can append hit points and the rest without moving the world's format number
    /// either.</para>
    ///
    /// <para>Hashing is <see cref="Pawn.ContributeTo"/>'s, not this section's. What it writes is
    /// exactly what that hashes: the draft flag and its quiet tick, and a finishing step.</para>
    /// </summary>
    public sealed class CombatSection : ISaveable
    {
        /// <summary>The record layout this build writes. 1 is C1's: flags, quiet tick, finishing step.</summary>
        public const int Layout = 1;

        const int FlagDrafted = 1;

        readonly PawnRegistry _pawns;
        readonly List<Pawn> _scratch = new List<Pawn>();

        public CombatSection(PawnRegistry pawns) =>
            _pawns = pawns ?? throw new ArgumentNullException(nameof(pawns));

        public string SaveKey => "odyssey.combat";

        static bool HasState(Pawn pawn) => pawn.Drafted || pawn.FinishingStepTo >= 0;

        public void Save(SaveWriter writer)
        {
            _scratch.Clear();
            IReadOnlyList<Pawn> all = _pawns.All;
            for (int i = 0; i < all.Count; i++) if (HasState(all[i])) _scratch.Add(all[i]);

            writer.Write(Layout);
            writer.Write(_scratch.Count);
            for (int i = 0; i < _scratch.Count; i++)
            {
                Pawn pawn = _scratch[i];
                writer.Write(pawn.Id.Value);
                writer.Write(pawn.Drafted ? FlagDrafted : 0);
                writer.Write(pawn.DraftQuietSinceTick);
                writer.Write(pawn.FinishingStepTo);
            }
            _scratch.Clear();
        }

        public void Load(SaveReader reader)
        {
            int layout = reader.ReadInt();
            if (layout < 1 || layout > Layout)
                throw new SaveLoadException(
                    $"The combat section has layout {layout} and this build reads up to {Layout}.");

            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int id = reader.ReadInt();
                int flags = reader.ReadInt();
                int quiet = reader.ReadInt();
                int finishing = reader.ReadInt();

                Pawn? pawn = _pawns.Get(new Contracts.PawnId(id));
                if (pawn == null) continue;

                pawn.Drafted = (flags & FlagDrafted) != 0;
                pawn.DraftQuietSinceTick = pawn.Drafted ? quiet : 0;

                // A step an order interrupted, rebuilt as the one-step path it was (design 33
                // §2d). The pawn section has already restored the progress into it, and
                // AdoptPath leaves progress alone for exactly this case — a resume.
                if (finishing >= 0)
                {
                    pawn.AdoptPath(new[] { pawn.Cell, finishing }, 2);
                    pawn.FinishingStepTo = finishing;
                }
            }
        }
    }
}
