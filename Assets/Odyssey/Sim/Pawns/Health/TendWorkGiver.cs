#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Who needs tending, and with what (design 43 §5). Shared by the giver and the driver so the
    /// question has one owner.
    /// </summary>
    public static class TendRules
    {
        /// <summary>
        /// A colonist with an injury nobody has tended, lying or standing, and in nobody's arms: a
        /// patient being carried is tended once she is set down. An animal has no ledger and a
        /// bandit is not ours to tend. Asks the ledger's flag first, so a colony nobody has hurt
        /// pays one comparison a pawn.
        /// </summary>
        public static bool NeedsTending(Pawn pawn) =>
            pawn.HasHealthState && pawn.IsColonist && pawn.CarriedBy == 0 && !Melee.IsDead(pawn)
            && pawn.Health!.UntendedCount > 0;

        /// <summary>
        /// The nearest medkit a doctor can reach and claim, on the floor or in a store, or null.
        /// <b>Scales with the things on the board</b>, once per tend given, and only when there is
        /// somebody to tend.
        /// </summary>
        public static ColonyItem? NearestMedkit(Pawn doctor, PawnContext ctx)
        {
            int kit = ctx.Content.MedkitItem;
            if (kit < 0) return null;

            ColonyItem? best = null;
            int bestDistance = int.MaxValue;
            var items = ctx.Items.Items;
            for (int i = 0; i < items.Count; i++)
            {
                ColonyItem item = items[i];
                if (item.Despawned || item.DefIndex != kit) continue;
                int at = ctx.WhereIs(item);
                if (at < 0) continue;
                int distance = ctx.Distance(doctor.Cell, at);
                if (distance >= bestDistance) continue;
                if (!ctx.Reservations.CanReserve(doctor.Id, KitKey(item))) continue;
                if (!ctx.Reachable(doctor, at, doctor.OwnMode)) continue;
                best = item;
                bestDistance = distance;
            }
            return best;
        }

        public static long KitKey(ColonyItem item) => ReservationManager.Key(ReservationTargetKind.Item, item.Id.Value);

        /// <summary>
        /// Is anybody on its feet attacking <paramref name="pawn"/>? A doctor under attack does not
        /// tend: her own response to danger comes first. <b>Scales with the pawns</b>, one comparison
        /// each.
        /// </summary>
        public static bool UnderAttack(PawnContext ctx, Pawn pawn)
        {
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
                if (pawns[i] != pawn && Melee.IsInAnAttack(pawns[i]) && pawns[i].CombatTarget == pawn.Id.Value) return true;
            return false;
        }
    }

    /// <summary>
    /// The doctor (design 43 §5): a colonist whose Doctor priority is set tends the colonist most
    /// in need she can reach — somebody bleeding before somebody who is not, then the nearest —
    /// with a medkit if one can be reached and bare hands if not. Never herself (design 43 §5: a
    /// lone colonist bleeding out is a scenario decision, not a default). An <b>emergency</b>
    /// giver, as rescue's is: a cut bleeding out outranks a wall at the same priority.
    ///
    /// <para><b>Nobody hurt, nothing touched.</b> The scan asks <see cref="Pawn.HasHealthState"/>
    /// first and answers no without a claim or a field written, so a colony nobody has hurt pays one
    /// flag a pawn a think and no golden moves for the giver.</para>
    /// </summary>
    public sealed class TendWorkGiver : WorkGiver
    {
        public override string Name => "Tend";

        public override int WorkType => WorkTypeIndex.Doctor;

        public override bool Emergency => true;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            if (!pawn.IsColonist || pawn.Downed) return false;
            if (TendRules.UnderAttack(ctx, pawn)) return false;

            Pawn? best = null;
            bool bestBleeding = false;
            int bestDistance = int.MaxValue;
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn patient = pawns[i];
                if (patient == pawn || !TendRules.NeedsTending(patient)) continue;

                bool bleeding = patient.Health!.BleedingSeverityMilli > 0;
                int distance = ctx.Distance(pawn.Cell, patient.Cell);
                if (bestBleeding && !bleeding) continue;
                if (bleeding == bestBleeding && distance >= bestDistance) continue;
                if (!ctx.Reservations.CanReserve(pawn.Id, RescueRules.PatientKey(patient))) continue;
                if (!ctx.Reachable(pawn, patient.Cell, pawn.OwnMode)) continue;
                if (Melee.ChooseSide(ctx, pawn, patient, pawn.OwnMode) < 0) continue;

                best = patient;
                bestBleeding = bleeding;
                bestDistance = distance;
            }

            if (best == null) return false;

            ColonyItem? kit = TendRules.NearestMedkit(pawn, ctx);

            // The patient rides on the doctor, as a rescue's does (Pawn.CombatTarget): saved and
            // hashed where it lives, so the job record needs no field of its own.
            pawn.CombatTarget = best.Id.Value;
            job.Reset(JobIndex.Tend);
            job.TargetItem = kit != null ? kit.Id : ThingId.None;
            job.TargetCell = kit != null ? ctx.WhereIs(kit) : -1;
            job.DestCell = best.Cell;
            job.Mode = pawn.OwnMode;
            return true;
        }
    }
}
