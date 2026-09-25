#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Saving
{
    /// <summary>
    /// Each colonist's area setting (design 43 §4a), as a save section of its own: keyed by pawn
    /// id, absent from an older save — which then loads with everybody at
    /// <see cref="PawnArea.Anywhere"/> — and <b>no world format bump</b>, because a reader skips a
    /// section it does not know.
    ///
    /// <para><b>Only colonists with something to say are written</b>, so a colony nobody
    /// restricts writes a count of nought. Not in <c>odyssey.combat</c>, because it is not combat;
    /// this is where the Assign tab's later settings go, each appended under a new layout.</para>
    ///
    /// <para>Hashing is <see cref="Pawn.ContributeTo"/>'s, as it is for the response.</para>
    /// </summary>
    public sealed class AssignSection : ISaveable
    {
        /// <summary>The record layout this build writes. 1: the area.</summary>
        public const int Layout = 1;

        readonly PawnRegistry _pawns;
        readonly List<Pawn> _scratch = new List<Pawn>();

        public AssignSection(PawnRegistry pawns) =>
            _pawns = pawns ?? throw new ArgumentNullException(nameof(pawns));

        public string SaveKey => "odyssey.assign";

        public void Save(SaveWriter writer)
        {
            _scratch.Clear();
            IReadOnlyList<Pawn> all = _pawns.All;
            for (int i = 0; i < all.Count; i++) if (all[i].Area != PawnArea.Anywhere) _scratch.Add(all[i]);

            writer.Write(Layout);
            writer.Write(_scratch.Count);
            for (int i = 0; i < _scratch.Count; i++)
            {
                writer.Write(_scratch[i].Id.Value);
                writer.Write((int)_scratch[i].Area);
            }
            _scratch.Clear();
        }

        public void Load(SaveReader reader)
        {
            int layout = reader.ReadInt();
            if (layout < 1 || layout > Layout)
                throw new SaveLoadException(
                    $"The assign section has layout {layout} and this build reads up to {Layout}.");

            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int id = reader.ReadInt();
                int area = reader.ReadInt();
                Pawn? pawn = _pawns.Get(new Contracts.PawnId(id));
                if (pawn == null) continue;
                pawn.Area = area >= 0 && area < PawnAreas.Count ? (PawnArea)area : PawnArea.Anywhere;
            }
        }
    }
}
