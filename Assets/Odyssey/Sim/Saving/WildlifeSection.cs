#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Saving
{
    /// <summary>
    /// Which animals have decided to leave the board (design 30 §3). Its own section rather than
    /// a field in the pawn record, for the reason the kind section is: a save without it loads
    /// with nobody leaving, so no format bump and no old save refused. Keyed by pawn id, like
    /// the kind section, so the order of the pawn list is never a contract here.
    /// </summary>
    public sealed class WildlifeSection : ISaveable
    {
        readonly PawnRegistry _pawns;

        public WildlifeSection(PawnRegistry pawns) =>
            _pawns = pawns ?? throw new ArgumentNullException(nameof(pawns));

        public string SaveKey => "odyssey.wildlife";

        public void Save(SaveWriter writer)
        {
            var leaving = new List<int>();
            IReadOnlyList<Pawn> all = _pawns.All;
            for (int i = 0; i < all.Count; i++) if (all[i].Leaving) leaving.Add(all[i].Id.Value);
            writer.Write(leaving.Count);
            for (int i = 0; i < leaving.Count; i++) writer.Write(leaving[i]);
        }

        public void Load(SaveReader reader)
        {
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int id = reader.ReadInt();
                Pawn? pawn = _pawns.Get(new Contracts.PawnId(id));
                if (pawn != null) pawn.Leaving = true;
            }
        }
    }
}
