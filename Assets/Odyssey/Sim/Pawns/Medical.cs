#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The rules of treatment (design 37 §4), in one place so the doctor's giver, the patient's
    /// own mind and the job that does the work cannot come to disagree about who may be treated,
    /// by whom, or by how much.
    ///
    /// <para><b>Who is a patient.</b> A colonist below the treatment cap (80 % of her pool) who has
    /// not been treated inside the cooldown. A doctor comes to her only once she is <i>lying
    /// still</i> — downed where she fell, or in bed as a patient or asleep — because a doctor chasing
    /// a colonist who is walking about with a scratch is not a treatment, it is a pursuit.</para>
    ///
    /// <para><b>The heal.</b> The supplies' <see cref="ItemDef.healPerUnit"/>, or
    /// <see cref="CombatDef.bareHeal"/> with none; halved for treating yourself; clamped to the cap
    /// (80 %, or 60 % for yourself). Never lowers the pool: a treatment given above the cap does
    /// nothing and uses nothing.</para>
    ///
    /// <para><b>The body (design 43, merged 2026-09-25).</b> A treatment is also the tend: when the
    /// work is done every injury on the patient is tended at the treater's quality and every bleed
    /// stops. And <b>a bleeding colonist is a patient whatever her pool and whatever the
    /// cooldown</b> — a cut on a colonist at 90 % kills her in a day and a half if nobody comes —
    /// but a treatment inside the cooldown tends without healing, so a stack of supplies is still
    /// not a substitute for rest. Design 43 §15.</para>
    /// </summary>
    public static class Medical
    {
        /// <summary>Is this cell the head of a bed the colony has? The list is sorted (<c>ColonyItems.AddBed</c>).</summary>
        public static bool IsBedCell(PawnContext ctx, int cell)
        {
            var beds = ctx.Items.Beds;
            int low = 0, high = beds.Count - 1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                if (beds[mid] == cell) return true;
                if (beds[mid] < cell) low = mid + 1;
                else high = mid - 1;
            }
            return false;
        }

        /// <summary>
        /// A treatment would do something now: she is bleeding (design 43 §4: any tend stops it), or
        /// she is below the doctor's cap and outside the cooldown.
        /// </summary>
        public static bool NeedsTreatment(Pawn pawn, PawnContext ctx) =>
            pawn.IsColonist && pawn.CarriedBy == 0
            && (IsBleeding(pawn) || (Below(pawn, ctx.Content.Combat.treatCapPerMille) && Heals(pawn, ctx)));

        /// <summary>
        /// Would a treatment given now heal the pool — outside the cooldown — or only tend? Constant
        /// for the length of one treatment, because the cooldown is set only when one finishes.
        /// </summary>
        public static bool Heals(Pawn pawn, PawnContext ctx) => ctx.CurrentTick >= pawn.TreatedUntilTick;

        /// <summary>An untended wound on her body (design 43 §4). Nobody without a body bleeds.</summary>
        public static bool IsBleeding(Pawn pawn) =>
            pawn.HasHealthState && pawn.Health!.BleedingSeverityMilli > 0;

        /// <summary>
        /// Anything a player's order to treat her would do: what <see cref="NeedsTreatment"/> asks,
        /// or an injury nobody has tended yet — a tended injury heals anywhere (design 43 §6), which
        /// is worth a player's click even on a colonist the doctor's own round would leave alone.
        /// </summary>
        public static bool WorthAnOrder(Pawn pawn, PawnContext ctx) =>
            NeedsTreatment(pawn, ctx)
            || (pawn.IsColonist && pawn.CarriedBy == 0 && pawn.HasHealthState && pawn.Health!.UntendedCount > 0);

        /// <summary>Lying still: down where she fell, or asleep — the only patients a doctor's round walks to.</summary>
        public static bool LyingStill(Pawn pawn) => pawn.Downed || pawn.Asleep;

        /// <summary>A patient a doctor may walk to: in need, and lying still — downed, or in bed.</summary>
        public static bool AwaitsDoctor(Pawn pawn, PawnContext ctx)
        {
            if (!NeedsTreatment(pawn, ctx)) return false;
            if (pawn.Downed) return true;
            if (!pawn.Asleep) return false;
            return IsPatient(pawn) || IsBedCell(ctx, pawn.Cell);
        }

        /// <summary>Lying down as a patient right now.</summary>
        public static bool IsPatient(Pawn pawn) =>
            pawn.CurrentJob != null && pawn.CurrentJob.DefIndex == JobIndex.Patient;

        /// <summary>Hit points below this fraction of the pool, per mille.</summary>
        public static bool Below(Pawn pawn, int perMille) =>
            (long)pawn.HpMilli * 1_000 < (long)pawn.HpMaxMilli * perMille;

        /// <summary>
        /// Could any colonist but <paramref name="patient"/> come and treat her? Standing, not
        /// broken, not drafted, Doctor switched on, and able to walk to where she is. The
        /// self-treatment fallback is the answer "no" to this and nothing else (owner, 2026-09-24).
        /// Scales with the colonists, and is asked only by a hurt colonist between jobs.
        /// </summary>
        public static bool DoctorCanReach(Pawn patient, PawnContext ctx)
        {
            var all = ctx.Pawns.All;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn doctor = all[i];
                if (doctor == patient || !doctor.IsColonist) continue;
                if (doctor.Downed || doctor.IsBroken || doctor.Drafted) continue;
                if (doctor.WorkPriority(WorkTypeIndex.Doctor) <= 0) continue;
                if (ctx.Reachable(doctor, patient.Cell)) return true;
            }

            return false;
        }

        /// <summary>
        /// The nearest supplies <paramref name="pawn"/> can reach and claim — anything whose Def
        /// heals, so a weaker item later is found by the same scan — or null.
        /// </summary>
        public static ColonyItem? NearestSupplies(Pawn pawn, PawnContext ctx)
        {
            ColonyItem? best = null;
            int bestDistance = int.MaxValue;
            var items = ctx.Items.Items;
            for (int i = 0; i < items.Count; i++)
            {
                ColonyItem item = items[i];
                if (item.Despawned || item.Forbidden) continue;
                if (ctx.Content.Items[item.DefIndex].healPerUnit <= 0) continue;

                int at = ctx.WhereIs(item);
                if (at < 0) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Item, item.Id.Value);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                int distance = ctx.Distance(pawn.Cell, at);
                if (distance >= bestDistance) continue;
                if (!ctx.Reachable(pawn, at)) continue;

                bestDistance = distance;
                best = item;
            }

            return best;
        }

        /// <summary>
        /// What one treatment would leave <paramref name="patient"/> at, in thousandths: her pool
        /// plus the heal, clamped to the cap, and never below where she is.
        /// </summary>
        /// <param name="suppliesDef">The item used, or -1 for a bare dressing.</param>
        public static int HealedMilli(Pawn patient, PawnContext ctx, int suppliesDef, bool self)
        {
            long after = patient.HpMilli + HealDelta(ctx, suppliesDef, self);
            long cap = CapMilli(patient, ctx, self);
            if (after > cap) after = cap;
            return after < patient.HpMilli ? patient.HpMilli : (int)after;
        }

        /// <summary>The raw heal a treatment is worth before the cap, in thousandths: constant for
        /// the whole toil, since neither the supplies used nor whether it is self-treatment change
        /// mid-treatment.</summary>
        public static long HealDelta(PawnContext ctx, int suppliesDef, bool self)
        {
            CombatDef combat = ctx.Content.Combat;
            long heal = (suppliesDef >= 0 ? ctx.Content.Items[suppliesDef].healPerUnit : combat.bareHeal)
                        * (long)Rates.Scale;
            return self ? heal * combat.selfHealPerMille / 1_000 : heal;
        }

        /// <summary>The pool <paramref name="patient"/>'s treatment may not heal past, in thousandths.</summary>
        public static long CapMilli(Pawn patient, PawnContext ctx, bool self)
        {
            int capPerMille = self ? ctx.Content.Combat.selfCapPerMille : ctx.Content.Combat.treatCapPerMille;
            return (long)patient.HpMaxMilli * capPerMille / 1_000;
        }

        /// <summary>
        /// Add a share of a treatment to the pool, clamped to <paramref name="capMilli"/>, and the
        /// same points off the body's ledger. Getting up is <see cref="GetUpIfAble"/>'s, at the end.
        ///
        /// <para><b>Called every tick the treatment toil progresses</b>, not once at the end
        /// (owner, 2026-09-25: a bar that fills as she works rather than jumping at the finish, and
        /// "pulled away for a partial heal" landing for real rather than being lost). The share is
        /// the toil's own to work out — see <c>TreatJobDriver</c> — because it is the one place that
        /// knows how much of the work is done; this only ever adds and only ever up to the cap, so
        /// a caller cannot overshoot it by calling twice.</para>
        /// </summary>
        public static void ApplyShare(Pawn patient, PawnContext ctx, long shareMilli, long capMilli)
        {
            if (shareMilli <= 0) return;
            long after = patient.HpMilli + shareMilli;
            if (after > capMilli) after = capMilli;
            if (after <= patient.HpMilli) return;
            int healed = (int)after - patient.HpMilli;
            patient.HpMilli = (int)after;
            // The same points off the body's ledger, the worst injury first, in the same call: for
            // a pawn with a body the pool and the ledger never disagree (design 43 §2's invariant,
            // which the combat gate walks). The pool written alone here would be a second owner of
            // healing, and the ledger would keep pain shock on a colonist the pool calls well.
            patient.Health?.Heal(healed);
        }

        /// <summary>
        /// A downed patient a treatment has lifted past the line gets up — at the end of the tick,
        /// through the combat system, which owns that rule — and only once the body lets her too
        /// (design 43 §3): pain past the shock line, blood past its worst stage or a head at nought
        /// keep her down at any pool.
        ///
        /// <para><b>Asked when the treatment ends, not on every share</b> (merge with design 43,
        /// 2026-09-25). Asked per share, she stood up at 15 % a third of the way through her own
        /// treatment, stopped lying still, and the doctor's job failed: the unit went back on the
        /// floor unused, no cooldown was set, and the next doctor started again — a free heal each
        /// time. The driver asks in <c>Cleanup</c>, so a doctor called away still leaves her on her
        /// feet if what landed was enough.</para>
        /// </summary>
        public static void GetUpIfAble(Pawn patient, PawnContext ctx)
        {
            if (!patient.Downed) return;
            if ((long)patient.HpMilli * 1_000 < (long)patient.HpMaxMilli * ctx.Content.Combat.downedRecoverAtPerMille)
                return;

            int tick = ctx.CurrentTick;
            CombatSystem? combat = ctx.Combat;
            if (combat != null)
                ctx.Defer(_ => { if (patient.Downed && !patient.CurrentVitals().Incapacitated) combat.Recover(patient, tick); });
        }

        /// <summary>
        /// Is there any point in her lying down as a patient? In a bed, always: the bed heals. On
        /// the ground, only while a doctor could still come — lying in the mud heals nothing — which
        /// is also what stops her lying down and getting straight up again every tick.
        /// </summary>
        public static bool WorthLyingDown(Pawn pawn, PawnContext ctx, int bed) =>
            bed >= 0 || (NeedsTreatment(pawn, ctx) && DoctorCanReach(pawn, ctx));

        /// <summary>Would she treat herself right now, if she were up? The think node's first branch, as a question.</summary>
        public static bool WouldSelfTreat(Pawn pawn, PawnContext ctx) =>
            WantsSelfTreatment(pawn, ctx)
            && !DoctorCanReach(pawn, ctx)
            && NearestSupplies(pawn, ctx) != null;

        /// <summary>
        /// Would treating herself do anything: bleeding, or under the self-treatment cap and outside
        /// the cooldown. Whether anybody else could come is <see cref="DoctorCanReach"/>'s question.
        /// </summary>
        public static bool WantsSelfTreatment(Pawn pawn, PawnContext ctx) =>
            IsBleeding(pawn) || (Below(pawn, ctx.Content.Combat.selfCapPerMille) && Heals(pawn, ctx));

        /// <summary>
        /// The tend a treatment ends in (design 43 §5): every injury on <paramref name="patient"/>
        /// tended at the treater's Medicine quality times the potency of what she used — supplies or
        /// a bare dressing — and every bleed stopped, whatever the quality. A pawn with no body has
        /// nothing to tend. Returns how many injuries were tended.
        /// </summary>
        public static int Tend(Pawn patient, Pawn by, PawnContext ctx, bool supplies)
        {
            HealthDef? body = patient.Body;
            if (body == null || ctx.Combat == null) return 0;
            int quality = body.TendQualityPerMille(by.SkillLevel(SkillIndex.Medicine), supplies);
            return ctx.Combat.Tend(patient, quality);
        }

        /// <summary>
        /// Where a hurt colonist lies down: her own bed, else the nearest free one she can reach,
        /// else -1 for the ground. The sleep's choice exactly, without the collapse rule — a
        /// patient is not exhausted, she is hurt.
        /// </summary>
        public static int BedFor(Pawn pawn, PawnContext ctx)
        {
            var beds = ctx.Items.Beds;
            int bestBed = -1, bestDistance = int.MaxValue;
            int me = pawn.Id.Value;
            for (int i = 0; i < beds.Count; i++)
            {
                int cell = beds[i];
                if (!BedRules.CanUse(pawn, cell, ctx)) continue;
                int owner = BedRules.OwnerAt(ctx, cell);

                long key = ReservationManager.Key(ReservationTargetKind.Cell, cell);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;
                if (!ctx.Reachable(pawn, cell)) continue;
                if (owner == me) return cell;

                int distance = ctx.Distance(pawn.Cell, cell);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestBed = cell;
            }

            return bestBed;
        }
    }

    /// <summary>
    /// The Doctor work type's one giver (design 37 §4): the nearest patient lying still whom this
    /// colonist can reach and claim, treated with the nearest supplies — or with a bare dressing
    /// when the colony has none she can reach. An <b>emergency</b> giver, like rescue's. Never
    /// herself: that is the patient's own fallback (<see cref="PatientThinkNode"/>).
    ///
    /// <para>Scales with the colonists, and only on a think by a colonist with Doctor switched on.</para>
    /// </summary>
    public sealed class DoctorWorkGiver : WorkGiver
    {
        public override string Name => "Doctor";

        public override int WorkType => WorkTypeIndex.Doctor;

        public override bool Emergency => true;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            // Somebody bleeding before somebody who is not (design 43 §5: the bleed is the clock),
            // then the nearest.
            Pawn? best = null;
            bool bestBleeding = false;
            int bestDistance = int.MaxValue;
            var all = ctx.Pawns.All;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn patient = all[i];
                if (patient == pawn || !Medical.AwaitsDoctor(patient, ctx)) continue;

                bool bleeding = Medical.IsBleeding(patient);
                if (bestBleeding && !bleeding) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Pawn, patient.Id.Value);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                int distance = ctx.Distance(pawn.Cell, patient.Cell);
                if (bleeding == bestBleeding && distance >= bestDistance) continue;
                if (!ctx.Reachable(pawn, patient.Cell)) continue;

                bestDistance = distance;
                bestBleeding = bleeding;
                best = patient;
            }

            if (best == null) return false;

            ColonyItem? supplies = Medical.NearestSupplies(pawn, ctx);
            job.Reset(JobIndex.Treat);
            job.WorkTicks = best.Id.Value;
            job.DestCell = best.Cell;
            if (supplies != null)
            {
                job.TargetItem = supplies.Id;
                job.TargetCell = ctx.WhereIs(supplies);
            }
            return true;
        }
    }

    /// <summary>
    /// A hurt colonist's own mind (design 37 §4), between the needs and the work: under the
    /// self-treatment cap, with supplies and no doctor who could come, she treats herself; under
    /// half her pool, she goes to bed as a patient. Declines for anybody else, which costs a
    /// comparison a think.
    /// </summary>
    public sealed class PatientThinkNode : ThinkNode
    {
        public override string Name => "Patient";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            if (!pawn.IsColonist || pawn.Downed || pawn.Asleep || pawn.IsBroken || pawn.Drafted) return false;
            CombatDef combat = ctx.Content.Combat;
            // A bleeding colonist is a patient whatever her pool (design 43 §15): she lies down for
            // the doctor, or treats herself when nobody can come.
            bool bleeding = Medical.IsBleeding(pawn);
            if (!bleeding && !Medical.Below(pawn, combat.treatCapPerMille)) return false;

            if (Medical.WantsSelfTreatment(pawn, ctx) && !Medical.DoctorCanReach(pawn, ctx))
            {
                // NearestSupplies is asked here rather than through WouldSelfTreat so the scan runs once.
                ColonyItem? supplies = Medical.NearestSupplies(pawn, ctx);
                if (supplies != null)
                {
                    job.Reset(JobIndex.Treat);
                    job.WorkTicks = pawn.Id.Value;
                    job.DestCell = -1;
                    job.TargetItem = supplies.Id;
                    job.TargetCell = ctx.WhereIs(supplies);
                    return true;
                }
            }

            if (!bleeding && !Medical.Below(pawn, combat.patientBelowPerMille)) return false;

            int bed = Medical.BedFor(pawn, ctx);
            if (!Medical.WorthLyingDown(pawn, ctx, bed)) return false;

            job.Reset(JobIndex.Patient);
            job.TargetCell = bed;
            return true;
        }
    }

    /// <summary>
    /// <c>Job_Treat</c> (design 37 §4): fetch one unit of supplies if the job names any, walk to
    /// the patient, and work until the treatment is done; then heal her. Treating herself, a
    /// colonist does it where she took the supplies up, at three times the work.
    ///
    /// <para>The patient is <see cref="Job.WorkTicks"/> — a <see cref="PawnId"/> value, saved and
    /// hashed with the job, the field every driver uses for its own purpose. The supplies are taken
    /// at the grasp and used on the heal; a treatment that fails in between puts the unit down
    /// again rather than using it (<see cref="Cleanup"/>).</para>
    ///
    /// <para><b>It ends in the tend</b> (design 43 §5, folded in at the merge 2026-09-25): every
    /// injury on her tended at the treater's quality, every bleed stopped. There is one doctor's
    /// job, not a treatment and a tend beside it with two owners of who a doctor walks to.</para>
    /// </summary>
    public class TreatJobDriver : JobDriver
    {
        const int ToilFetch = 0, ToilLift = 1, ToilGo = 2, ToilTreat = 3;

        public override int WorkType => WorkTypeIndex.Doctor;

        public override int WorkFocus =>
            ToilIndex == ToilTreat && Job.DestCell >= 0 && Pawn.Destination < 0 ? Job.DestCell : -1;

        bool Self => Job.WorkTicks == Pawn.Id.Value;

        public override void Begin(Pawn pawn, Job job)
        {
            base.Begin(pawn, job);
            // The pool reuses one driver per pawn, and a loaded job re-begins its kneel.
            _kneeling = false;
            // No supplies named: a bare dressing, so nothing to fetch.
            if (job.TargetItem == ThingId.None) ToilIndex = ToilGo;
        }

        public override bool TryMakeReservations(PawnContext ctx)
        {
            if (Job.TargetItem != ThingId.None)
            {
                var item = ctx.Items.Get(Job.TargetItem);
                if (item == null || ctx.WhereIs(item) < 0) return false;
                long itemKey = ReservationManager.Key(ReservationTargetKind.Item, Job.TargetItem.Value);
                if (!ctx.Reservations.Reserve(Pawn.Id, itemKey)) return false;
                Pawn.HeldReservations.Add(itemKey);
            }

            long patientKey = ReservationManager.Key(ReservationTargetKind.Pawn, Job.WorkTicks);
            if (!ctx.Reservations.Reserve(Pawn.Id, patientKey)) return false;
            Pawn.HeldReservations.Add(patientKey);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            Pawn? patient = ctx.Pawns.Get(new PawnId(Job.WorkTicks));
            if (patient == null || patient.CarriedBy != 0) return JobStatus.Failed;

            if (ToilIndex < ToilTreat)
            {
                // Before the work starts: still worth doing — on a player's order, anything a
                // treatment would do; on the doctor's own round, a patient lying still.
                if (Job.PlayerForced ? !Medical.WorthAnOrder(patient, ctx) : !Medical.NeedsTreatment(patient, ctx))
                    return JobStatus.Failed;
                if (!Self && !Job.PlayerForced && !Medical.AwaitsDoctor(patient, ctx)) return JobStatus.Failed;
            }
            // Once the work has started it runs to the end (merge with design 43, 2026-09-25): a
            // treatment that lifts her to the cap part-way no longer stops being needed, fails,
            // and puts the unit back on the floor unused with no cooldown set. On the doctor's
            // round she must still be lying still; getting up is asked only when it ends.
            else if (!Self && !Job.PlayerForced && !Medical.LyingStill(patient)) return JobStatus.Failed;

            switch (ToilIndex)
            {
                case ToilFetch:
                {
                    var item = ctx.Items.Get(Job.TargetItem);
                    if (item == null || !StillAt(ctx, item, Job.TargetCell)) return JobStatus.Failed;
                    JobStatus walk = GotoCell(ctx, Job.TargetCell);
                    if (walk == JobStatus.Succeeded) NextToil();
                    return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                case ToilLift:
                    return Lift(ctx);

                case ToilGo:
                {
                    if (Self)
                    {
                        NextToil();
                        return JobStatus.Ongoing;
                    }

                    Job.DestCell = patient.Cell;
                    if (InReach(ctx, patient))
                    {
                        Stop();
                        NextToil();
                        return JobStatus.Ongoing;
                    }
                    return Approach(ctx, patient);
                }

                default:
                {
                    if (!Self)
                    {
                        Job.DestCell = patient.Cell;
                        if (!InReach(ctx, patient))
                        {
                            // Walking back to her side, in this toil so the work done so far is
                            // kept (design 43 §14c's rule). Going back to the walk toil threw it
                            // away, and the shares already paid then paid out again from nought:
                            // one unit could heal more than its forty. The kneel is over the moment
                            // the stance is (GrowingJob's own rule).
                            Pawn.BeginGesture(PawnGesture.None);
                            _kneeling = false;
                            return Approach(ctx, patient);
                        }
                        Stop();
                    }

                    // Kneeling at her side to dress the wound — the sower's own kneel, re-used for
                    // the same reason it was re-used for the lift: down where she is lying. In a
                    // bed she is tended standing, bent over the bed rather than down at the floor
                    // (owner, 2026-09-25: "stood up if customer in bed"), and a patient on her feet
                    // (an ordered treatment) is tended standing too. Begun once as the work starts,
                    // and again after a walk back, and held while it runs, as the sow's own kneel is.
                    if (!_kneeling && (Self || Medical.LyingStill(patient)) && !Medical.IsBedCell(ctx, patient.Cell))
                        Pawn.BeginGesture(PawnGesture.Sow);
                    _kneeling = true;

                    ColonyItem? carried = Job.CarriedItem >= 0 ? ctx.Items.Get(new ThingId(Job.CarriedItem)) : null;
                    if (Job.TargetItem != ThingId.None && carried == null) return JobStatus.Failed;
                    int suppliesDef = carried != null ? carried.DefIndex : -1;

                    CombatDef combat = ctx.Content.Combat;
                    long totalTicks = (long)ctx.Content.Jobs[Job.DefIndex].workTicks
                        * (Self ? combat.selfWorkFactor : 1) * Rates.Scale;

                    // The health bar over her head fills as the work does, not at the end (owner,
                    // 2026-09-25): each tick adds this toil's own share of the whole heal, worked
                    // out from the work done this tick against the work the whole treatment takes —
                    // never from the pool itself, which a caller reading it mid-treatment must not
                    // perturb the arithmetic of. Summed over every tick this lands exactly the full
                    // heal, because a share is a difference of two cumulative fractions and every
                    // tick but the rounding on the last is accounted for by the one before it.
                    // Inside the cooldown — a bleeding patient treated again — it tends and heals
                    // nothing (design 43 §15): the supplies are not a substitute for rest.
                    long before = System.Math.Min(ToilProgress, totalTicks);
                    ToilProgress += Pawn.WorkRatePerMille(WorkTypeIndex.Doctor);
                    Work(ctx);
                    long after = System.Math.Min(ToilProgress, totalTicks);

                    bool heals = Medical.Heals(patient, ctx);
                    long heal = heals ? Medical.HealDelta(ctx, suppliesDef, Self) : 0;
                    long share = totalTicks > 0 ? heal * after / totalTicks - heal * before / totalTicks : heal;
                    Medical.ApplyShare(patient, ctx, share, Medical.CapMilli(patient, ctx, Self));

                    if (ToilProgress < totalTicks) return JobStatus.Ongoing;

                    Pawn.BeginGesture(PawnGesture.None);
                    _kneeling = false;
                    // The tend (design 43 §5): every injury tended at her quality, every bleed stopped.
                    Medical.Tend(patient, Pawn, ctx, supplies: carried != null);
                    if (heals) patient.TreatedUntilTick = ctx.CurrentTick + combat.treatedCooldownTicks;
                    if (carried != null)
                    {
                        ctx.Items.Despawn(carried);
                        Job.CarriedItem = -1;
                    }
                    return JobStatus.Succeeded;
                }
            }
        }

        // Whether the kneel has been begun in this stretch of work. Presentation's cue only,
        // re-derived after a load by beginning it again, so neither saved nor hashed.
        bool _kneeling;

        /// <summary>Standing beside her, not on her: she may be lying in the cell.</summary>
        bool InReach(PawnContext ctx, Pawn patient) =>
            Pawn.Cell != patient.Cell && StillInReach(ctx, Pawn, patient.Cell, 0, 0);

        /// <summary>
        /// Walk to a free cell beside her, chosen once and kept while it is still beside her (design
        /// 43 §14c): a patient on her feet moves, and choosing again on every step she took cleared
        /// the walk each time, so a doctor following her never arrived. No free cell beside her (a
        /// packed bed row, design 37 §12a) fails rather than overlapping her — the giver picks
        /// somebody else, or the same patient once a neighbour clears. <see cref="Job.TargetCell"/>
        /// holds it: the supplies' cell is spent once the unit is in hand.
        /// </summary>
        JobStatus Approach(PawnContext ctx, Pawn patient)
        {
            int stand = Job.TargetCell;
            if (stand < 0 || stand == patient.Cell || !Beside(ctx.Size, stand, patient.Cell))
                Job.TargetCell = stand = FellJobDriver.StandBeside(ctx, Pawn, patient.Cell);
            if (stand < 0) return JobStatus.Failed;

            JobStatus walk = GotoCell(ctx, stand);
            return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
        }

        /// <summary>One of the eight cells round <paramref name="of"/> on its layer.</summary>
        static bool Beside(GridSize size, int cell, int of)
        {
            if (cell == of) return false;
            CellRef a = size.FromIndex(cell), b = size.FromIndex(of);
            return a.Y == b.Y && System.Math.Abs(a.X - b.X) <= 1 && System.Math.Abs(a.Z - b.Z) <= 1;
        }

        /// <summary>Standing to work: whatever walk was in hand is over.</summary>
        void Stop()
        {
            if (Pawn.Destination < 0 && !Pawn.HasPath) return;
            Pawn.ClearPath();
            Pawn.Destination = -1;
        }

        /// <summary>
        /// <see cref="LiftToil"/>'s motion and timing, taking one unit rather than the stack: a
        /// doctor carries a box to the patient and leaves the rest of the pile where it was.
        /// </summary>
        JobStatus Lift(PawnContext ctx)
        {
            var item = ctx.Items.Get(Job.TargetItem);
            if (item == null) return JobStatus.Failed;

            int total = ctx.Content.LiftTicks * Rates.Scale;
            int grasp = ctx.Content.LiftGraspTicks * Rates.Scale;
            if (grasp > total) grasp = total;
            if (grasp < 1) grasp = 1;

            if (ToilProgress == 0) Pawn.BeginGesture(PawnGesture.Lift);
            int elapsed = ToilProgress += Rates.Scale;

            if (elapsed < grasp) return AtHand(ctx, item) ? JobStatus.Ongoing : JobStatus.Failed;

            if (elapsed == grasp)
            {
                if (!AtHand(ctx, item)) return JobStatus.Failed;
                ColonyItem taken = ctx.Items.SplitOff(item, 1, Pawn.Id);
                Job.CarriedItem = taken.Id.Value;
            }

            if (elapsed < total) return JobStatus.Ongoing;
            NextToil();
            return JobStatus.Ongoing;
        }

        public override void Cleanup(PawnContext ctx, JobStatus status)
        {
            _kneeling = false;
            DropCarried(ctx);
            // Up if what landed was enough, whether the treatment finished or was cut short.
            Pawn? patient = ctx.Pawns.Get(new PawnId(Job.WorkTicks));
            if (patient != null) Medical.GetUpIfAble(patient, ctx);
        }
    }

    /// <summary>
    /// <c>Job_Patient</c> (design 37 §4): walk to the bed <see cref="PatientThinkNode"/> chose — or
    /// lie down where she is when there is none — and sleep there until she is back to the
    /// release line. Asleep, so rest runs and so the bed's heal (<c>CombatSystem.Heal</c>, which
    /// asks for a pawn down or asleep on a bed) runs too; and asleep in a bed is what a doctor
    /// walks to.
    ///
    /// <para><b>On the ground she stays only while treatment can still come</b>: once she has been
    /// treated and nothing more can happen until the cooldown runs out, lying in the mud heals
    /// nothing, so she gets up and the next think decides again.</para>
    /// </summary>
    public class PatientJobDriver : JobDriver
    {
        /// <summary>How often a lying patient asks whether lying there still helps, in ticks.</summary>
        public const int RecheckTicks = 250;

        public override bool TryMakeReservations(PawnContext ctx)
        {
            if (Job.TargetCell < 0) return true;
            long key = ReservationManager.Key(ReservationTargetKind.Cell, Job.TargetCell);
            if (!ctx.Reservations.Reserve(Pawn.Id, key)) return false;
            Pawn.HeldReservations.Add(key);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            CombatDef combat = ctx.Content.Combat;
            // Up at the release line — unless she is still bleeding, which is what she is lying there for.
            if (!Medical.Below(Pawn, combat.patientReleasePerMille) && !Medical.IsBleeding(Pawn)) return JobStatus.Succeeded;

            if (ToilIndex == 0)
            {
                if (Job.TargetCell < 0 || Pawn.Cell == Job.TargetCell)
                {
                    if (Job.TargetCell >= 0) ctx.Construction?.TryClaimForSleeper(Job.TargetCell, Pawn.Id);
                    NextToil();
                    return JobStatus.Ongoing;
                }

                JobStatus walk = GotoCell(ctx, Job.TargetCell);
                if (walk == JobStatus.Succeeded)
                {
                    ctx.Construction?.TryClaimForSleeper(Job.TargetCell, Pawn.Id);
                    NextToil();
                }
                return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
            }

            // Hunger gets her up: nothing else interrupts a job, and a patient is in bed for days.
            if (Pawn.Needs[NeedIndex.Food] < ctx.Content.Needs[NeedIndex.Food].seekThreshold)
                return JobStatus.Succeeded;

            // Now and then, not every tick: both questions scan the colony.
            if (ToilProgress % (RecheckTicks * Rates.Scale) == 0)
            {
                if (!Medical.WorthLyingDown(Pawn, ctx, Job.TargetCell)) return JobStatus.Succeeded;
                if (Medical.WouldSelfTreat(Pawn, ctx)) return JobStatus.Succeeded;
            }

            Pawn.Asleep = true;
            ToilProgress += Rates.Scale;
            return JobStatus.Ongoing;
        }

        public override void Cleanup(PawnContext ctx, JobStatus status) => Pawn.Asleep = false;
    }
}
