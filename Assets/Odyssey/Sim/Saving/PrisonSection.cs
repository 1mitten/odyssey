#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Saving
{
    /// <summary>
    /// Every pawn the colony holds, held, or means to take (design 58 §4a), as a save section of
    /// its own: keyed by pawn id, absent from an older save — which then loads with nobody in
    /// custody — and <b>no world format bump</b>, because a reader skips a section it does not know.
    ///
    /// <para><b>Only pawns with something to say are written</b>: a custody other than free, or a
    /// record that is not empty. A colony that never takes a prisoner writes a count of nought.
    /// Hashing is <see cref="Pawn.ContributeTo"/>'s, as it is for the area.</para>
    /// </summary>
    public sealed class PrisonSection : ISaveable
    {
        /// <summary>The record layout this build writes. 1: custody and the record's fields.</summary>
        public const int Layout = 1;

        const int CustodyCount = 4;
        const int ModeCount = 5;

        readonly PawnRegistry _pawns;
        readonly List<Pawn> _scratch = new List<Pawn>();

        public PrisonSection(PawnRegistry pawns) =>
            _pawns = pawns ?? throw new ArgumentNullException(nameof(pawns));

        public string SaveKey => "odyssey.prison";

        static bool Says(Pawn pawn) =>
            pawn.Custody != PawnCustody.Free || (pawn.Prison != null && !pawn.Prison.IsEmpty);

        public void Save(SaveWriter writer)
        {
            _scratch.Clear();
            IReadOnlyList<Pawn> all = _pawns.All;
            for (int i = 0; i < all.Count; i++) if (Says(all[i])) _scratch.Add(all[i]);

            writer.Write(Layout);
            writer.Write(_scratch.Count);
            for (int i = 0; i < _scratch.Count; i++)
            {
                Pawn pawn = _scratch[i];
                PrisonRecord record = pawn.Prison ?? new PrisonRecord();
                writer.Write(pawn.Id.Value);
                writer.Write((int)pawn.Custody);
                writer.Write((record.Joined ? 1 : 0) | (record.Dressed ? 2 : 0)
                             | (record.Arrested ? 4 : 0) | (record.CaptureMark ? 8 : 0));
                writer.Write((int)record.Mode);
                writer.Write(record.Willingness);
                writer.Write(record.LastChatTick);
            }
            _scratch.Clear();
        }

        public void Load(SaveReader reader)
        {
            int layout = reader.ReadInt();
            if (layout < 1 || layout > Layout)
                throw new SaveLoadException(
                    $"The prison section has layout {layout} and this build reads up to {Layout}.");

            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int id = reader.ReadInt();
                int custody = reader.ReadInt();
                int flags = reader.ReadInt();
                int mode = reader.ReadInt();
                int willingness = reader.ReadInt();
                int lastChat = reader.ReadInt();

                Pawn? pawn = _pawns.Get(new PawnId(id));
                if (pawn == null) continue;
                pawn.Custody = custody >= 0 && custody < CustodyCount ? (PawnCustody)custody : PawnCustody.Free;
                var record = new PrisonRecord
                {
                    Joined = (flags & 1) != 0,
                    Dressed = (flags & 2) != 0,
                    Arrested = (flags & 4) != 0,
                    CaptureMark = (flags & 8) != 0,
                    Mode = mode >= 0 && mode < ModeCount ? (PrisonMode)mode : PrisonMode.Hold,
                    Willingness = willingness,
                    LastChatTick = lastChat,
                };
                pawn.Prison = record.IsEmpty ? null : record;
            }
        }
    }
}
