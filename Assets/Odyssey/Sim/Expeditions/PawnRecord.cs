#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;

namespace Odyssey.Sim.Expeditions
{
    /// <summary>
    /// One colonist, whole, in a save (design 64 §12): the portable pawn codec, for a colonist on the
    /// road, who is on no board and so in none of a board's sections.
    ///
    /// <para><b>It writes nothing of its own.</b> A board spreads a pawn over several sections — the
    /// registry, the roll seed, the kind, combat, health, the home area — and every block here is the
    /// block that section writes, through the same helper. So a field added to a colonist is added
    /// once and both files carry it, and the codec cannot drift from the board format; the two were
    /// the same code before the codec existed and were split out for it.</para>
    ///
    /// <para><b>A detached colonist only.</b> What <see cref="PawnTransfer.Detach"/> clears — the path,
    /// the job, the fight, the carrier — is written as the empty value it then holds, and the job and
    /// reservations are not written at all: she has none on the road.</para>
    /// </summary>
    public static class PawnRecord
    {
        /// <summary>The codec's own layout, before any of the shared blocks. A save contract.</summary>
        public const int Layout = 1;

        public static void Write(SaveWriter writer, Pawn pawn)
        {
            writer.Write(Layout);
            writer.Write(pawn.Id.Value);
            writer.Write(pawn.Kind);
            writer.Write(pawn.Cell);
            writer.Write(unchecked((int)pawn.RollSeed));
            writer.Write((int)pawn.Area);
            PawnRegistry.WriteCharacter(writer, pawn);
            PawnRegistry.WriteCondition(writer, pawn);
            CombatSection.WriteRecord(writer, pawn);
            bool health = pawn.HasHealthState;
            writer.Write(health);
            if (health) HealthSection.WriteRecord(writer, pawn.Health!);
        }

        /// <summary>
        /// A colonist read back, on no board: no context, no drivers, no job. The campaign's
        /// <paramref name="content"/> is what she is made of, as on the board she left.
        /// </summary>
        public static Pawn Read(SaveReader reader, PawnContent content)
        {
            int layout = reader.ReadInt();
            if (layout < 1 || layout > Layout)
                throw new SaveLoadException(
                    $"A travelling colonist has layout {layout} and this build reads up to {Layout}.");

            int id = reader.ReadInt();
            int kind = reader.ReadInt();
            int cell = reader.ReadInt();
            var pawn = new Pawn(new PawnId(id), cell, content, kind);
            pawn.RollSeed = unchecked((uint)reader.ReadInt());
            pawn.Area = (PawnArea)reader.ReadInt();
            PawnRegistry.ReadCharacter(reader, pawn);
            PawnRegistry.ReadCondition(reader, pawn);
            CombatSection.ReadRecord(reader, CombatSection.Layout, pawn);
            if (reader.ReadBool())
            {
                var health = HealthSection.ReadRecord(reader);
                if (pawn.Body != null) pawn.Health = health;
            }
            return pawn;
        }
    }
}
