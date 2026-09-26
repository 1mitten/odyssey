#nullable enable

using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    // The prisoner line's drivers (design 60 §7), JobHandle 28 to 35, claimed together by the
    // contracts step (P3) so that every table a save depends on is extended once, and each written
    // by its own unit (P5 to P12).

    /// <summary>
    /// <c>Job_Capture</c> (design 60 §7): the rescue's walk, lift, carry and lay — the same stoop,
    /// the same cradle, the same set-down on every other end — to a prison bed rather than a
    /// colony one, and on the lay she is taken: into custody, given the bed, dressed for the cell.
    /// A subclass rather than a copy, so the carry has one owner.
    /// </summary>
    public class CaptureJobDriver : RescueJobDriver
    {
        /// <summary>Judged as a prisoner: she is not one until she is laid down, and the bed is for one.</summary>
        protected override BedUser UserFor(Pawn patient) => BedUser.Prisoner;

        /// <summary>Laid in the bed, then taken: custody, the bed hers, the jumpsuit on.</summary>
        protected override void Lay(PawnContext ctx, Pawn patient)
        {
            base.Lay(ctx, patient);
            CustodyRules.Take(patient, ctx);
            ctx.Construction?.AssignOwnerAt(Job.DestCell, patient.Id.Value);
            PrisonRecord record = patient.Prison ??= new PrisonRecord();
            record.Dressed = true;
        }
    }

    /// <summary>
    /// <c>Job_FeedPrisoner</c> (design 60 §7): fetch one portion of food, carry it to a prisoner and
    /// feed her there — through her door, to her bed if she is shackled or down. The doctor's shape
    /// (<see cref="TreatJobDriver"/>): one unit off the pile, not the pile, and a carried thing is
    /// put down by <see cref="JobDriver.DropCarried"/> on every other end. <see cref="Job.WorkTicks"/>
    /// names the prisoner, as it names the patient there, so no job field is added.
    /// </summary>
    public class FeedPrisonerJobDriver : JobDriver
    {
        const int ToilFetch = 0, ToilLift = 1, ToilGo = 2, ToilFeed = 3;

        /// <summary>How long a feeding takes at her side, in ticks: a meal held to her, not a banquet.</summary>
        public const int FeedTicks = 120;

        public override bool TryMakeReservations(PawnContext ctx)
        {
            var item = ctx.Items.Get(Job.TargetItem);
            if (item == null || ctx.WhereIs(item) < 0) return false;
            long itemKey = ReservationManager.Key(ReservationTargetKind.Item, Job.TargetItem.Value);
            long prisonerKey = ReservationManager.Key(ReservationTargetKind.Pawn, Job.WorkTicks);
            if (!ctx.Reservations.CanReserve(Pawn.Id, itemKey) || !ctx.Reservations.CanReserve(Pawn.Id, prisonerKey))
                return false;
            ctx.Reservations.Reserve(Pawn.Id, itemKey);
            Pawn.HeldReservations.Add(itemKey);
            ctx.Reservations.Reserve(Pawn.Id, prisonerKey);
            Pawn.HeldReservations.Add(prisonerKey);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            Pawn? prisoner = ctx.Pawns.Get(new PawnId(Job.WorkTicks));
            if (prisoner == null || prisoner.Custody != PawnCustody.Prisoner || prisoner.CarriedBy != 0)
                return JobStatus.Failed;

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
                {
                    var item = ctx.Items.Get(Job.TargetItem);
                    if (item == null) return JobStatus.Failed;
                    int total = ctx.Content.LiftTicks * Rates.Scale;
                    int grasp = System.Math.Max(1, System.Math.Min(total, ctx.Content.LiftGraspTicks * Rates.Scale));
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

                case ToilGo:
                {
                    // Beside her, never on her: she may be lying in the cell.
                    if (Pawn.Cell != prisoner.Cell && StillInReach(ctx, Pawn, prisoner.Cell, 0, 0))
                    {
                        if (Pawn.Destination >= 0 || Pawn.HasPath) { Pawn.ClearPath(); Pawn.Destination = -1; }
                        NextToil();
                        return JobStatus.Ongoing;
                    }
                    // A free cell beside her, chosen once and kept while it is still beside her —
                    // the doctor's rule (design 43 §14c), or a warden following her never arrives.
                    int stand = Job.DestCell = PrisonerFollow.StandFor(ctx, Pawn, Job.DestCell, prisoner.Cell);
                    if (stand < 0) return JobStatus.Failed;
                    return GotoCell(ctx, stand) == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                default:
                {
                    ColonyItem? carried = Job.CarriedItem >= 0 ? ctx.Items.Get(new ThingId(Job.CarriedItem)) : null;
                    if (carried == null) return JobStatus.Failed;
                    // She may walk off mid-meal (design 60 §16 #8): he follows with the plate, as a
                    // warden's visit does, and feeds her only at her side.
                    if (!StillInReach(ctx, Pawn, prisoner.Cell, 0, 0))
                    {
                        Job.DestCell = PrisonerFollow.StandFor(ctx, Pawn, Job.DestCell, prisoner.Cell);
                        if (Job.DestCell < 0) return JobStatus.Failed;
                        return GotoCell(ctx, Job.DestCell) == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                    }
                    ToilProgress += Rates.Scale;
                    if (ToilProgress < FeedTicks * Rates.Scale) return JobStatus.Ongoing;

                    ItemDef food = ctx.Content.Items[carried.DefIndex];
                    int max = ctx.Content.Needs[NeedIndex.Food].max;
                    prisoner.Needs[NeedIndex.Food] = System.Math.Min(max, prisoner.Needs[NeedIndex.Food] + food.nutrition);
                    int thought = food.ateThought;
                    if ((uint)thought < (uint)ctx.Content.Thoughts.Length) prisoner.AddMemory(thought, ctx.CurrentTick);
                    ctx.Items.Despawn(carried);
                    Job.CarriedItem = -1;
                    ctx.Kitchen?.Invalidate();
                    return JobStatus.Succeeded;
                }
            }
        }

        public override void Cleanup(PawnContext ctx, JobStatus status) => DropCarried(ctx);
    }

    /// <summary>
    /// <b>Where to stand beside somebody who may walk on</b> (design 60 §16 #8, #10): the one rule
    /// every job that goes to a prisoner's side follows — feeding, talking, walking her out and
    /// arresting. Keep the place while it is still beside her; choose again only once it is not,
    /// and then only at a step boundary or standing still, as the melee chase does. Chosen afresh
    /// every tick she moved, each new destination snapped the step in hand back, and a warden
    /// followed anybody walking on at half pace.
    /// </summary>
    public static class PrisonerFollow
    {
        /// <summary>The place to walk to beside <paramref name="target"/>, keeping <paramref name="stand"/> where it still serves; -1 for none.</summary>
        public static int StandFor(PawnContext ctx, Pawn walker, int stand, int target)
        {
            if (stand >= 0 && stand != target && Beside(ctx.Size, stand, target)) return stand;
            bool boundary = walker.MoveProgress < walker.MoveRatePerMille();
            if (stand >= 0 && stand != target && !boundary && walker.Destination >= 0) return stand;
            return FellJobDriver.StandBeside(ctx, walker, target);
        }

        static bool Beside(GridSize size, int cell, int of)
        {
            if (cell == of) return false;
            CellRef a = size.FromIndex(cell), b = size.FromIndex(of);
            return a.Y == b.Y && System.Math.Abs(a.X - b.X) <= 1 && System.Math.Abs(a.Z - b.Z) <= 1;
        }
    }

    /// <summary>
    /// A warden's visit to a prisoner (design 60 §8, §10): walk to her, stand beside her — following
    /// her if she walks off mid-sentence rather than starting over — and spend <see cref="Ticks"/>
    /// there, then <see cref="Finish"/>. <see cref="Job.WorkTicks"/> names the prisoner, as the
    /// feeding's does. The chat and the escort differ only in how long and what happens at the end.
    /// </summary>
    public abstract class BesidePrisonerJobDriver : JobDriver
    {
        public override int WorkFocus => ToilIndex == 1 ? Job.DestCell : -1;

        /// <summary>Whether the visit is still wanted: she is still in the state that asked for it.</summary>
        protected abstract bool StillWanted(Pawn prisoner, PawnContext ctx);

        /// <summary>How long the visit lasts, in ticks at full pace.</summary>
        protected abstract int Ticks(PawnContext ctx);

        /// <summary>Whether the visit trains the job's skill as work does.</summary>
        protected virtual bool Trains => false;

        /// <summary>The visit is over.</summary>
        protected abstract void Finish(Pawn prisoner, PawnContext ctx);

        public override bool TryMakeReservations(PawnContext ctx)
        {
            long key = ReservationManager.Key(ReservationTargetKind.Pawn, Job.WorkTicks);
            if (!ctx.Reservations.Reserve(Pawn.Id, key)) return false;
            Pawn.HeldReservations.Add(key);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            Pawn? prisoner = ctx.Pawns.Get(new PawnId(Job.WorkTicks));
            if (prisoner == null || !StillWanted(prisoner, ctx) || prisoner.CarriedBy != 0)
                return JobStatus.Failed;

            if (ToilIndex == 0)
            {
                if (Pawn.Cell != prisoner.Cell && StillInReach(ctx, Pawn, prisoner.Cell, 0, 0))
                {
                    if (Pawn.Destination >= 0 || Pawn.HasPath) { Pawn.ClearPath(); Pawn.Destination = -1; }
                    Job.DestCell = prisoner.Cell;
                    NextToil();
                    return JobStatus.Ongoing;
                }
                int stand = Job.TargetCell = PrisonerFollow.StandFor(ctx, Pawn, Job.TargetCell, prisoner.Cell);
                if (stand < 0) return JobStatus.Failed;
                return GotoCell(ctx, stand) == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
            }

            // She may walk off mid-visit; the warden follows rather than starting over.
            if (!StillInReach(ctx, Pawn, prisoner.Cell, 0, 0))
            {
                Job.TargetCell = PrisonerFollow.StandFor(ctx, Pawn, Job.TargetCell, prisoner.Cell);
                if (Job.TargetCell < 0) return JobStatus.Failed;
                return GotoCell(ctx, Job.TargetCell) == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
            }
            Job.DestCell = prisoner.Cell;
            ToilProgress += Rates.Scale;
            if (Trains) Work(ctx);
            if (ToilProgress < Ticks(ctx) * Rates.Scale) return JobStatus.Ongoing;

            Finish(prisoner, ctx);
            return JobStatus.Succeeded;
        }
    }

    /// <summary>
    /// <c>Job_Chat</c> (design 60 §8): talk to a prisoner in Recruit mode for the job's work ticks,
    /// training Social as any working job trains its skill; at the end the bar fills by
    /// <see cref="Recruitment.Factors"/>'s gain, and she joins if it is full.
    /// </summary>
    public class ChatJobDriver : BesidePrisonerJobDriver
    {
        protected override bool StillWanted(Pawn prisoner, PawnContext ctx) => Recruitment.Due(prisoner, ctx.CurrentTick);
        protected override int Ticks(PawnContext ctx) => ctx.Content.Jobs[Job.DefIndex].workTicks;
        protected override bool Trains => true;
        protected override void Finish(Pawn prisoner, PawnContext ctx) => Recruitment.Chat(prisoner, Pawn, ctx);
    }

    /// <summary>
    /// <c>Job_Escort</c> (design 60 §10): a warden goes to a prisoner the player has chosen to
    /// release or exile, opens her way out, and lets her go — <see cref="PrisonRelease.Let"/>.
    /// A short visit: the door is opened, a word is said, and she is on her way.
    /// </summary>
    public class EscortJobDriver : BesidePrisonerJobDriver
    {
        /// <summary>How long letting her go takes at the cell.</summary>
        public const int EscortTicks = 120;

        protected override bool StillWanted(Pawn prisoner, PawnContext ctx) => PrisonRelease.Wanted(prisoner);
        protected override int Ticks(PawnContext ctx) => EscortTicks;
        protected override void Finish(Pawn prisoner, PawnContext ctx) => PrisonRelease.Let(prisoner, ctx);
    }

    /// <summary>
    /// <c>Job_GoToCell</c> (design 60 §10): a prisoner on her feet walks herself to her prison bed
    /// — a raider who surrendered, a colonist who came quietly. She walks in a colonist's mode for
    /// the length of this job, so the doors on the way in open for her; once she is there she is
    /// dressed for the cell, and her own mode, which opens nothing, holds her.
    /// </summary>
    public class GoToCellJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => Job.DestCell >= 0;

        public override JobStatus Tick(PawnContext ctx)
        {
            if (Pawn.Custody != PawnCustody.Prisoner || PrisonerTrees.OwnBed(Pawn, ctx) != Job.DestCell)
                return JobStatus.Failed;
            JobStatus walk = GotoCell(ctx, Job.DestCell);
            if (walk != JobStatus.Succeeded) return walk;
            (Pawn.Prison ??= new PrisonRecord()).Dressed = true;
            return JobStatus.Succeeded;
        }
    }

    /// <summary>
    /// <c>Job_Escape</c> (design 60 §9c): an escapee runs for the edge of the board, and at it she
    /// is gone. The door is not this job's: bashing one is the fight's own building attack, chosen
    /// by the escape tree when no path out is open.
    /// </summary>
    public class EscapeJobDriver : WalkOffJobDriver
    {
        protected override PawnCustody Leaving => PawnCustody.Escaping;
        protected override int Incident => IncidentHandle.PrisonerEscaped;
    }

    /// <summary>
    /// <c>Job_LeaveFree</c> (design 60 §6, §10): a pawn let go — released or exiled — walks to the
    /// edge of the board and is gone. Nobody is told; the player chose it.
    /// </summary>
    public class LeaveFreeJobDriver : WalkOffJobDriver
    {
        protected override PawnCustody Leaving => PawnCustody.Released;
        protected override int Incident => -1;
    }

    /// <summary>
    /// The walk off the board both ways out share (design 60 §9c, §10): to <c>Job.DestCell</c> on
    /// the edge, then gone at the end of the tick — the thief's shape, deferred for death's reason
    /// (a despawn shifts the list every pawn loop walks). The custody is asked every tick, so a
    /// runner downed and taken back mid-walk stops at once.
    /// </summary>
    public abstract class WalkOffJobDriver : JobDriver
    {
        /// <summary>The custody this walk belongs to.</summary>
        protected abstract PawnCustody Leaving { get; }

        /// <summary>What the ledger is told when she is gone, or -1 for nothing.</summary>
        protected abstract int Incident { get; }

        public override bool TryMakeReservations(PawnContext ctx) => Job.DestCell >= 0;

        public override JobStatus Tick(PawnContext ctx)
        {
            if (Pawn.Custody != Leaving || Pawn.Downed) return JobStatus.Failed;
            if (ToilIndex == 0)
            {
                JobStatus walk = GotoCell(ctx, Job.DestCell);
                if (walk != JobStatus.Succeeded) return walk;
                NextToil();
            }
            Pawn pawn = Pawn;
            int jobDef = Job.DefIndex, incident = Incident;
            ctx.Defer(world => PrisonExit.Leave(ctx, pawn, jobDef, incident, world.CurrentTick));
            return JobStatus.Ongoing;
        }
    }

    /// <summary>
    /// <c>Job_Arrest</c> (design 60 §10): walk to a colonist, following her if she walks on, and at
    /// her side take her — <see cref="Arrest.Contact"/>, which rolls whether she resists.
    /// </summary>
    public class ArrestJobDriver : BesidePrisonerJobDriver
    {
        /// <summary>How long the arrest itself takes at her side: a hand on the shoulder.</summary>
        public const int ArrestTicks = 30;

        protected override bool StillWanted(Pawn target, PawnContext ctx) => Arrest.CanBeArrested(target);
        protected override int Ticks(PawnContext ctx) => ArrestTicks;
        protected override void Finish(Pawn target, PawnContext ctx) => Arrest.Contact(Pawn, target, ctx);
    }
}
