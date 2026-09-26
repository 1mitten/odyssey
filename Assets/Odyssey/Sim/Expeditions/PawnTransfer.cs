#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Expeditions
{
    /// <summary>A side of the board, numbered as <c>EdgeTarget</c>'s scan numbers its edges.</summary>
    public enum BoardSide
    {
        /// <summary>x = 0.</summary>
        West = 0,

        /// <summary>x = the last column.</summary>
        East = 1,

        /// <summary>z = 0.</summary>
        South = 2,

        /// <summary>z = the last row.</summary>
        North = 3,
    }

    /// <summary>Why a colonist may not set out (design 64 §6b).</summary>
    public enum DepartRefusal
    {
        None,
        NotAColonist,
        Downed,
        Carried,
        Bleeding,
        CarryingSomebody,
    }

    /// <summary>
    /// One stack a colonist took off a board: what it was, how many, how good (design 64 §6b).
    /// The def index is into the <see cref="PawnContent"/> every board of a campaign shares, so it
    /// means the same thing on the board she arrives at.
    /// </summary>
    public readonly struct CargoLine
    {
        public readonly int DefIndex;
        public readonly int Stack;
        public readonly int Quality;

        /// <summary>The weapon in her hand, which she takes up again on arrival, not a load to put down.</summary>
        public readonly bool IsWeapon;

        public CargoLine(int defIndex, int stack, int quality, bool isWeapon)
        {
            DefIndex = defIndex;
            Stack = stack;
            Quality = quality;
            IsWeapon = isWeapon;
        }
    }

    /// <summary>
    /// Taking a colonist off one board and putting her on another (design 64 §6b, §6e).
    ///
    /// <para><b>She travels as the same object.</b> Everything she <i>is</i> — skills, passions,
    /// health, memories, work priorities, schedule, mood, her roll seed and so her name and face —
    /// stays on the <see cref="Pawn"/> untouched. What is cleared is what she was <i>doing</i> on the
    /// board she left: a path, a job, a fight, a draft, a carrier. Those name cells, items and pawns
    /// on that board and mean nothing on the next one.</para>
    ///
    /// <para><b>Between ticks only.</b> <see cref="Detach"/> takes her out of the registry, and a
    /// registry list shifting under a pawn loop is the reason <c>Theft.Leave</c>, which this is
    /// modelled on, is deferred. The departure job defers it the same way.</para>
    /// </summary>
    public static class PawnTransfer
    {
        /// <summary>Whether this colonist may set out now, and if not, why.</summary>
        public static DepartRefusal CanDepart(Pawn pawn)
        {
            if (!pawn.IsColonist) return DepartRefusal.NotAColonist;
            if (pawn.Downed) return DepartRefusal.Downed;
            if (pawn.CarriedBy != 0) return DepartRefusal.Carried;
            if (pawn.Health != null && pawn.Health.BleedingSeverityMilli > 0) return DepartRefusal.Bleeding;
            if (IsCarryingSomebody(pawn)) return DepartRefusal.CarryingSomebody;
            return DepartRefusal.None;
        }

        /// <summary>
        /// Take her off <paramref name="from"/>: pack what she holds into cargo, end her job, clear
        /// what she was doing and take her out of the registry — keeping her bed, because she is
        /// coming back to it. Returns the cargo, which is what she has with her on the road.
        /// </summary>
        public static List<CargoLine> Detach(ColonyWorld from, Pawn pawn)
        {
            PawnContext ctx = from.Pawns;
            var cargo = new List<CargoLine>();

            // The load, out of the job before the job ends, or the job's cleanup drops it where she
            // stands — exactly as Theft.Leave does it.
            Job? job = pawn.CurrentJob;
            if (job != null && job.CarriedItem >= 0)
            {
                ColonyItem? load = ctx.Items.Get(new ThingId(job.CarriedItem));
                job.CarriedItem = -1;
                if (load != null && !load.Despawned)
                {
                    cargo.Add(new CargoLine(load.DefIndex, load.Stack, load.Quality, isWeapon: false));
                    ctx.Items.Despawn(load);
                }
            }

            ColonyItem? weapon = WeaponHand.Held(pawn, ctx);
            pawn.EquippedItem = 0;
            if (weapon != null)
            {
                cargo.Add(new CargoLine(weapon.DefIndex, weapon.Stack, weapon.Quality, isWeapon: true));
                ctx.Items.Despawn(weapon);
            }

            if (pawn.CurrentJob != null) from.Jobs.EndJob(pawn, JobStatus.Succeeded);
            ClearBoardState(pawn);
            ctx.Pawns.Despawn(pawn, DespawnReason.Departed);
            pawn.Context = null;
            return cargo;
        }

        /// <summary>
        /// Put her on <paramref name="to"/> at <paramref name="cell"/> and give her back what she
        /// carried: the weapon back into her hand, every load put down where she came in for the
        /// colony to haul (design 64 §6e: the light logistics the owner chose).
        /// </summary>
        public static void Arrive(ColonyWorld to, Pawn pawn, int cell, IReadOnlyList<CargoLine> cargo)
        {
            PawnContext ctx = to.Pawns;
            pawn.Cell = cell;
            ctx.Pawns.Adopt(pawn);
            for (int i = 0; i < cargo.Count; i++)
            {
                CargoLine line = cargo[i];
                if (line.IsWeapon && pawn.EquippedItem == 0)
                {
                    ColonyItem? item = PutDown(ctx, cell, line);
                    if (item != null) WeaponHand.TakeUp(pawn, item, ctx);
                }
                else
                {
                    PutDown(ctx, cell, line);
                }
            }
        }

        /// <summary>
        /// The cell a party walks on at, on one side of a board: the nearest cell on that edge that
        /// can be reached from the board's own start, so nobody arrives on a ledge they cannot leave.
        /// -1 when that whole side is cut off.
        /// </summary>
        public static int EdgeCell(ColonyWorld board, BoardSide side)
        {
            PawnContext ctx = board.Pawns;
            int origin = board.Grid.Size.Index(board.Start);
            return EdgeTarget.FindOnSide(ctx, origin, (int)side, TraverseMode.Colonist);
        }

        /// <summary>
        /// Everything that named the board she was on: the path and the step in hand, the fight, the
        /// draft, sleep, a carrier. Each is either a cell, an item or a pawn on that board, or a
        /// state that only means something while she is standing on it.
        /// </summary>
        static void ClearBoardState(Pawn pawn)
        {
            pawn.ClearPath();
            pawn.Destination = -1;
            pawn.CombatTarget = 0;
            pawn.RetaliateAgainst = 0;
            pawn.RetaliateUntilTick = 0;
            pawn.CarriedBy = 0;
            pawn.Drafted = false;
            pawn.DraftQuietSinceTick = 0;
            pawn.Asleep = false;
            pawn.Leaving = false;
            pawn.PendingSwing = 0;
            pawn.PendingDamageMilli = 0;
            pawn.PendingStunTicks = 0;
        }

        static bool IsCarryingSomebody(Pawn pawn)
        {
            PawnContext? ctx = pawn.Context;
            if (ctx == null) return false;
            IReadOnlyList<Pawn> all = ctx.Pawns.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].CarriedBy == pawn.Id.Value) return true;
            return false;
        }

        static ColonyItem? PutDown(PawnContext ctx, int near, CargoLine line)
        {
            int cell = ctx.Items.NearestCellWithSpace(ctx.Cells, near, line.DefIndex, line.Stack, JobDriver.DropSearchRadius);
            if (cell < 0) return null;
            ThingId id = ctx.Items.Spawn(line.DefIndex, cell, line.Stack);
            ColonyItem? item = ctx.Items.Get(id);
            if (item != null && line.IsWeapon) item.Quality = (byte)line.Quality;
            return item;
        }
    }
}
