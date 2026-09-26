#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Saving
{
    /// <summary>
    /// The body's ledger of every pawn that has anything on it (design 43 §9): its blood lost and
    /// its injury records, keyed by pawn id, on the combat section's terms — absent from an older
    /// save, which then loads with nobody injured, and <b>no world format bump</b>, because a keyed
    /// section is skipped by a reader that does not know it.
    ///
    /// <para><b>Read after the combat section</b> by its place in <c>ColonyWorld.SaveComponents</c>,
    /// which restores the pool this ledger sits over. Only pawns with something to say are written,
    /// so a colony nobody has hurt writes a count of nought.</para>
    ///
    /// <para>Hashing is <see cref="PawnHealth.ContributeTo"/>'s, reached from
    /// <see cref="Pawn.ContributeTo"/>. What this writes is exactly what that hashes, in the same
    /// order, which is what <c>HealthSaveTests</c> holds.</para>
    /// </summary>
    public sealed class HealthSection : ISaveable
    {
        /// <summary>The record layout this build writes. 1 is design 43's.</summary>
        public const int Layout = 1;

        readonly PawnRegistry _pawns;
        readonly List<Pawn> _scratch = new List<Pawn>();

        public HealthSection(PawnRegistry pawns) =>
            _pawns = pawns ?? throw new ArgumentNullException(nameof(pawns));

        public string SaveKey => "odyssey.health";

        public void Save(SaveWriter writer)
        {
            _scratch.Clear();
            IReadOnlyList<Pawn> all = _pawns.All;
            for (int i = 0; i < all.Count; i++) if (all[i].HasHealthState) _scratch.Add(all[i]);

            writer.Write(Layout);
            writer.Write(_scratch.Count);
            for (int i = 0; i < _scratch.Count; i++)
            {
                Pawn pawn = _scratch[i];
                PawnHealth health = pawn.Health!;
                writer.Write(pawn.Id.Value);
                writer.Write(health.BloodLossMicro);
                writer.Write(health.Count);
                for (int r = 0; r < health.Count; r++)
                {
                    ref Affliction record = ref health[r];
                    writer.Write(record.Region);
                    writer.Write((int)record.Kind);
                    writer.Write(record.SeverityMilli);
                    writer.Write(record.Tended);
                    writer.Write(record.TendQualityPerMille);
                    writer.Write(record.Tick);
                }
            }
            _scratch.Clear();
        }

        public void Load(SaveReader reader)
        {
            int layout = reader.ReadInt();
            if (layout < 1 || layout > Layout)
                throw new SaveLoadException(
                    $"The health section has layout {layout} and this build reads up to {Layout}.");

            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int id = reader.ReadInt();
                int blood = reader.ReadInt();
                int records = reader.ReadInt();
                var health = new PawnHealth { BloodLossMicro = blood };
                for (int r = 0; r < records; r++)
                {
                    var record = new Affliction
                    {
                        Region = reader.ReadInt(),
                        Kind = (AfflictionKind)reader.ReadInt(),
                        SeverityMilli = reader.ReadInt(),
                        Tended = reader.ReadBool(),
                        TendQualityPerMille = reader.ReadInt(),
                        Tick = reader.ReadInt(),
                    };
                    health.Restore(record);
                }

                Pawn? pawn = _pawns.Get(new Contracts.PawnId(id));
                if (pawn == null || pawn.Body == null) continue;
                pawn.Health = health;
            }
        }
    }
}
