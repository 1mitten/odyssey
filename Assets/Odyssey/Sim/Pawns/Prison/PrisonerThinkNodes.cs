#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The minds of a pawn the colony holds (design 60 §6), chosen by custody ahead of the hostile
    /// tree: an escapee is hostile, but she is not a raider and must not ask a band what to do.
    /// Shared arrays; the nodes hold no state.
    ///
    /// <para><b>What holds her is the door, not the mind.</b> A prisoner walks in
    /// <see cref="Pathing.TraverseMode.Bandit"/>, which cannot open one, so every reachability
    /// question below is already bounded by her cell. The nodes add what the door cannot: food
    /// and beds are chosen from her own room even when a door happens to be held open, and the
    /// wander never picks a door cell, so she only ever goes through one when she means to leave.</para>
    /// </summary>
    public static class PrisonerTrees
    {
        /// <summary>A held prisoner: down, else to her cell, else a patient, else her needs, else her shackles, else the cell, else wait.</summary>
        static readonly ThinkNode[] Held =
        {
            new DownedThinkNode(), new ToMyCellThinkNode(), new PrisonerPatientThinkNode(), new PrisonerNeedsThinkNode(),
            new ShackledThinkNode(), new CellWanderThinkNode(), new PrisonerWaitThinkNode(),
        };

        /// <summary>
        /// A prisoner breaking out (design 60 §6, §9c): down, else strike back at whoever laid hands on
        /// her while he is beside her, else out by any way open — a door held open included — else
        /// break the door, else wait for one to open.
        /// </summary>
        static readonly ThinkNode[] Escaping =
        {
            new DownedThinkNode(), new FightBackThinkNode(), new EscapeRunThinkNode(), new EscapeBashThinkNode(),
            new PrisonerWaitThinkNode(),
        };

        /// <summary>A pawn let go (design 60 §10): down, else walk off the board, else wait.</summary>
        static readonly ThinkNode[] Released = { new DownedThinkNode(), new LeaveFreeThinkNode(), new PrisonerWaitThinkNode() };

        /// <summary>The tree for this custody. Never asked for <see cref="PawnCustody.Free"/>.</summary>
        public static ThinkNode[] For(PawnCustody custody) => custody switch
        {
            PawnCustody.Escaping => Escaping,
            PawnCustody.Released => Released,
            _ => Held,
        };

        /// <summary>The held prisoner's tree, in traversal order, so a test can assert it.</summary>
        public static IReadOnlyList<ThinkNode> HeldTree => Held;

        // ---- where she is kept ------------------------------------------------------------------

        /// <summary>The prison bed she owns, or -1.</summary>
        public static int OwnBed(Pawn pawn, PawnContext ctx)
        {
            var beds = ctx.Items.Beds;
            int me = pawn.Id.Value;
            for (int i = 0; i < beds.Count; i++)
                if (BedRules.OwnerAt(ctx, beds[i]) == me && BedRules.PurposeAt(ctx, beds[i]) == BedPurpose.Prison)
                    return beds[i];
            return -1;
        }

        /// <summary>Whether her own bed is a shackle bed: a prison bed with no room around it.</summary>
        public static bool IsShackled(Pawn pawn, PawnContext ctx)
        {
            int bed = OwnBed(pawn, ctx);
            return bed >= 0 && ctx.BedPurposes != null && ctx.BedPurposes.IsShackled(bed);
        }

        /// <summary>The cell she stands in, if it is a cell — a room with a prison bed in it — or 0.</summary>
        public static int CellRoom(Pawn pawn, PawnContext ctx)
        {
            if (ctx.Enclosure == null || ctx.BedPurposes == null) return 0;
            int room = ctx.Enclosure.RoomAt(pawn.Cell);
            return ctx.BedPurposes.IsCell(room) ? room : 0;
        }

        /// <summary>The enclosure's room record for a key, found on its own layer.</summary>
        public static ThermalRoom? Room(PawnContext ctx, int key)
        {
            if (key == 0 || ctx.Enclosure == null) return null;
            var rooms = ctx.Enclosure.RoomsOn(ctx.Size.FromIndex(key).Y);
            for (int i = 0; i < rooms.Count; i++) if (rooms[i].Key == key) return rooms[i];
            return null;
        }
    }

    /// <summary>
    /// A held prisoner's own needs (design 60 §6): eat what is in her cell, sleep in her prison bed.
    /// Never food or a bed outside the room she is kept in; a shackled prisoner, or one in no cell,
    /// eats only what a warden brings her and sleeps where she is kept.
    /// </summary>
    public sealed class PrisonerNeedsThinkNode : ThinkNode
    {
        public override string Name => "PrisonerNeeds";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            if (pawn.Asleep) return false;
            int room = PrisonerTrees.CellRoom(pawn, ctx);

            if (pawn.Needs[NeedIndex.Food] < ctx.Content.Needs[NeedIndex.Food].seekThreshold
                && room != 0 && TryEatInCell(pawn, ctx, job, room))
                return true;

            return pawn.Needs[NeedIndex.Rest] < ctx.Content.Needs[NeedIndex.Rest].seekThreshold
                   && TrySleepInCell(pawn, ctx, job, room);
        }

        /// <summary>The best food lying in her own room, then the nearest of it.</summary>
        static bool TryEatInCell(Pawn pawn, PawnContext ctx, Job job, int room)
        {
            var items = ctx.Items.Items;
            int best = -1, bestCell = -1, bestTier = int.MaxValue, bestDistance = int.MaxValue;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Despawned || item.Forbidden) continue;
                ItemDef food = ctx.Content.Items[item.DefIndex];
                if (food.nutrition <= 0 || food.foodTier > bestTier) continue;
                int at = ctx.WhereIs(item);
                if (at < 0 || ctx.Enclosure!.RoomAt(at) != room) continue;
                int distance = ctx.Distance(pawn.Cell, at);
                if (food.foodTier == bestTier && distance >= bestDistance) continue;
                long key = ReservationManager.Key(ReservationTargetKind.Item, item.Id.Value);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;
                if (!ctx.CanTravel(pawn, at, pawn.OwnMode)) continue;
                bestTier = food.foodTier;
                bestDistance = distance;
                best = i;
                bestCell = at;
            }
            if (best < 0) return false;
            job.Reset(JobIndex.Eat);
            job.TargetItem = items[best].Id;
            job.TargetCell = bestCell;
            // In her own mode, never a colonist's (design 60 §16 #2): Reset leaves Colonist, which
            // opens doors, and a route out of one door and in at another raised her own escape risk.
            job.Mode = pawn.OwnMode;
            return true;
        }

        /// <summary>
        /// Her own prison bed, else the nearest prison bed nobody owns in her room, else the
        /// ground where she stands. Never a fireside: the nearest fire is outside the cell.
        /// </summary>
        static bool TrySleepInCell(Pawn pawn, PawnContext ctx, Job job, int room)
        {
            int own = PrisonerTrees.OwnBed(pawn, ctx);
            int bed = own >= 0 && ctx.CanTravel(pawn, own, pawn.OwnMode) && Free(pawn, ctx, own) ? own : -1;
            if (bed < 0 && room != 0)
            {
                var beds = ctx.Items.Beds;
                int bestDistance = int.MaxValue;
                for (int i = 0; i < beds.Count; i++)
                {
                    int cell = beds[i];
                    if (!BedRules.CanUse(pawn, cell, ctx) || ctx.Enclosure!.RoomAt(cell) != room) continue;
                    if (!Free(pawn, ctx, cell) || !ctx.CanTravel(pawn, cell, pawn.OwnMode)) continue;
                    int distance = ctx.Distance(pawn.Cell, cell);
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    bed = cell;
                }
            }
            job.Reset(JobIndex.Sleep);
            job.TargetCell = bed;
            job.Mode = pawn.OwnMode;
            return true;
        }

        static bool Free(Pawn pawn, PawnContext ctx, int cell) =>
            ctx.Reservations.CanReserve(pawn.Id, ReservationManager.Key(ReservationTargetKind.Cell, cell));
    }

    /// <summary>
    /// A hurt prisoner lies down for the doctor (design 60 §16 #3), as <see cref="PatientThinkNode"/>
    /// sends a colonist: bleeding, whatever her pool, or under the patient line. A doctor walks only
    /// to somebody lying still, and without this a raider who surrendered bleeding stood in her
    /// cell untended until the blood loss put her down. Her own prison bed through the one bed rule,
    /// walked in her own mode so the cell still holds her; never treating herself.
    /// </summary>
    public sealed class PrisonerPatientThinkNode : ThinkNode
    {
        public override string Name => "PrisonerPatient";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            if (pawn.Downed || pawn.Asleep || !Medical.NeedsTreatment(pawn, ctx)) return false;
            if (!Medical.IsBleeding(pawn) && !Medical.Below(pawn, ctx.Content.Combat.patientBelowPerMille)) return false;
            int bed = Medical.BedFor(pawn, ctx);
            if (bed >= 0 && !ctx.CanTravel(pawn, bed, pawn.OwnMode)) bed = -1;
            if (!Medical.WorthLyingDown(pawn, ctx, bed)) return false;
            job.Reset(JobIndex.Patient);
            job.TargetCell = bed;
            job.Mode = pawn.OwnMode;
            return true;
        }
    }

    /// <summary>
    /// A shackled prisoner (design 60 §5b, §6) stays on her bed: walks to it if she is not on it,
    /// and waits there. Declines for anybody whose own bed is not a shackle bed.
    /// </summary>
    public sealed class ShackledThinkNode : ThinkNode
    {
        public override string Name => "Shackled";

        /// <summary>How long one wait on the bed lasts before she thinks again.</summary>
        public const int WaitTicks = 500;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            if (!PrisonerTrees.IsShackled(pawn, ctx)) return false;
            int bed = PrisonerTrees.OwnBed(pawn, ctx);
            // Through a door as ToMyCell walks her into a cell (design 60 §16 #6): a shackle bed in
            // a yard with a gate was out of reach in her own mode, and she waited where she
            // surrendered for good, at the shackled risk.
            if (pawn.Cell != bed && ctx.CanTravel(pawn, bed, TraverseMode.Colonist))
            {
                job.Reset(JobIndex.Goto);
                job.TargetCell = bed;
                job.Mode = TraverseMode.Colonist;
                return true;
            }
            job.Reset(JobIndex.Wait);
            job.WorkTicks = WaitTicks;
            return true;
        }
    }

    /// <summary>
    /// A prisoner walks her cell by day (design 60 §6): a cell of her room chosen by a draw of her
    /// own, never a door — doors are the room's boundary, so they are not in its cells at all.
    /// Declines for a prisoner who is not standing in a cell.
    /// </summary>
    public sealed class CellWanderThinkNode : ThinkNode
    {
        public override string Name => "CellWander";

        /// <summary>How often, per mille, a think in a cell is a walk rather than a wait.</summary>
        public const int WalkPerMille = 400;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            ThermalRoom? room = PrisonerTrees.Room(ctx, PrisonerTrees.CellRoom(pawn, ctx));
            if (room == null || room.Cells.Count == 0) return false;

            var rng = DeterministicRandom.ForTick(ctx.Seed, ctx.CurrentTick, PrisonPurpose.CellWander ^ (uint)pawn.Id.Value);
            if (rng.NextInt(1000) >= WalkPerMille) return false;

            for (int attempt = 0; attempt < 4; attempt++)
            {
                int cell = room.Cells[rng.NextInt(room.Cells.Count)];
                if (cell == pawn.Cell || !ctx.Cells.IsWalkable(cell) || !ctx.CanTravel(pawn, cell)) continue;
                job.Reset(JobIndex.Wander);
                job.TargetCell = cell;
                job.Mode = pawn.OwnMode;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// A prisoner on her feet who owns a prison bed in a cell but stands outside that cell — a
    /// raider who surrendered, a colonist arrested, or anybody who strayed — walks herself to the
    /// bed (design 60 §10), in a colonist's mode for the walk so the cell door lets her in. A
    /// shackle bed is the shackles' node's; a prisoner with no bed has nowhere to go.
    /// </summary>
    public sealed class ToMyCellThinkNode : ThinkNode
    {
        public override string Name => "ToMyCell";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            if (ctx.Enclosure == null || ctx.BedPurposes == null) return false;
            int bed = PrisonerTrees.OwnBed(pawn, ctx);
            if (bed < 0 || ctx.BedPurposes.IsShackled(bed)) return false;
            int room = ctx.Enclosure.RoomAt(bed);
            if (room == 0 || ctx.Enclosure.RoomAt(pawn.Cell) == room) return false;
            if (!ctx.CanTravel(pawn, bed, TraverseMode.Colonist)) return false;
            job.Reset(JobIndex.GoToCell);
            job.TargetCell = bed;
            job.DestCell = bed;
            job.Mode = TraverseMode.Colonist;
            return true;
        }
    }

    /// <summary>
    /// An escapee strikes back at the one she holds a grudge against — the colonist who tried to
    /// arrest her (design 60 §10) — while he stands beside her and the grudge lasts. Only beside
    /// her: she is running, and does not chase him.
    /// </summary>
    public sealed class FightBackThinkNode : ThinkNode
    {
        public override string Name => "FightBack";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            if (pawn.RetaliateAgainst == 0 || ctx.CurrentTick >= pawn.RetaliateUntilTick) return false;
            Pawn? foe = ctx.Pawns.Get(new PawnId(pawn.RetaliateAgainst));
            TraverseMode mode = pawn.OwnMode;
            if (foe == null || !Melee.IsStanding(foe) || !Melee.InReach(ctx, pawn, foe, mode)) return false;
            return AttackJob.Fill(ctx, pawn, foe, job, mode);
        }
    }

    /// <summary>
    /// An escapee runs for the nearest edge she can reach in her own mode (design 60 §9c) — which
    /// cannot open a door, so while her cell is shut this declines and the door is broken instead.
    /// </summary>
    public sealed class EscapeRunThinkNode : ThinkNode
    {
        public override string Name => "EscapeRun";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            int edge = Theft.EdgeFrom(ctx, pawn.Cell, pawn.OwnMode);
            if (edge < 0) return false;
            job.Reset(JobIndex.Escape);
            job.DestCell = edge;
            job.TargetCell = edge;
            job.Mode = pawn.OwnMode;
            return true;
        }
    }

    /// <summary>
    /// An escapee with no way out breaks one (design 60 §9c): the nearest door of the room she is
    /// in that she can get at, with the fight's own building attack — so how long a door holds is
    /// its hit points against a bare fist, and <b>what a cell door is built of now matters</b>.
    /// With no door to break (a room walled all round) she breaks the nearest colony building
    /// between her and the way out, which is the bandit's own choice.
    /// </summary>
    public sealed class EscapeBashThinkNode : ThinkNode
    {
        public override string Name => "EscapeBash";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            TraverseMode mode = pawn.OwnMode;
            World.ThermalRoom? room = ctx.Enclosure == null ? null
                : PrisonerTrees.Room(ctx, ctx.Enclosure.RoomAt(pawn.Cell));
            if (room != null)
            {
                BuildingTarget best = default;
                int bestDistance = int.MaxValue;
                bool found = false;
                var doors = room.Doors;
                for (int i = 0; i < doors.Count; i++)
                {
                    if (!BuildingTargets.TryFind(ctx, doors[i].Cell, out BuildingTarget door)) continue;
                    if (!BuildingTargets.CanReach(ctx, pawn, door, mode)) continue;
                    int distance = ctx.Distance(pawn.Cell, door.Anchor);
                    if (distance >= bestDistance) continue;
                    best = door;
                    bestDistance = distance;
                    found = true;
                }
                if (found) return AttackJob.FillBuilding(ctx, pawn, best, job, mode);
            }
            return BuildingTargets.TryNearestColonyTarget(ctx, pawn, mode, out BuildingTarget any)
                   && AttackJob.FillBuilding(ctx, pawn, any, job, mode);
        }
    }

    /// <summary>A pawn released or exiled walks to the nearest edge she can reach, and leaves (design 60 §10).</summary>
    public sealed class LeaveFreeThinkNode : ThinkNode
    {
        public override string Name => "LeaveFree";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            int edge = Theft.EdgeFrom(ctx, pawn.Cell, pawn.OwnMode);
            if (edge < 0) return false;
            job.Reset(JobIndex.LeaveFree);
            job.DestCell = edge;
            job.TargetCell = edge;
            job.Mode = pawn.OwnMode;
            return true;
        }
    }

    /// <summary>A prisoner with nothing else to do stands where she is for a while, then thinks again.</summary>
    public sealed class PrisonerWaitThinkNode : ThinkNode
    {
        /// <summary>How long one wait lasts before the prisoner thinks again.</summary>
        public const int WaitTicks = 250;

        public override string Name => "PrisonerWait";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            job.Reset(JobIndex.Wait);
            job.WorkTicks = WaitTicks;
            return true;
        }
    }
}
