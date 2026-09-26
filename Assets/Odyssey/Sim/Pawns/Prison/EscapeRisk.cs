#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Why a prisoner's escape risk is what it is, as bits (design 58 §9a). Published as one number
    /// so the prisoner's pane can say it in words. Some raise it and some lower it.
    /// </summary>
    [System.Flags]
    public enum EscapeReasons
    {
        None = 0,
        /// <summary>Mood below 300: ×3.</summary>
        Miserable = 1 << 0,
        /// <summary>Mood 300–499: ×1.5.</summary>
        Unhappy = 1 << 1,
        /// <summary>Mood above 700: ×0.5.</summary>
        Content = 1 << 2,
        /// <summary>A door of her cell stands open: ×2.</summary>
        DoorOpen = 1 << 3,
        /// <summary>Shackled in the open, with no walls round her: ×2.</summary>
        Shackled = 1 << 4,
        /// <summary>No free, standing colonist within ten cells: ×1.5.</summary>
        Unwatched = 1 << 5,
        /// <summary>Whole: ×1.5.</summary>
        Unhurt = 1 << 6,
        /// <summary>Hurt: ×0.5.</summary>
        Hurt = 1 << 7,
        /// <summary>Fed, and no injury left untended: ×0.7.</summary>
        WellKept = 1 << 8,
    }

    /// <summary>A prisoner's escape risk and its reasons, and how many are held — the divisor.</summary>
    public readonly struct EscapeOdds
    {
        /// <summary>The chance she breaks out in a day, in parts per million.</summary>
        public readonly int PerDayPpm;
        public readonly EscapeReasons Reasons;
        /// <summary>How many prisoners the colony holds, whose square root divides every one's risk.</summary>
        public readonly int Held;

        public EscapeOdds(int perDayPpm, EscapeReasons reasons, int held)
        {
            PerDayPpm = perDayPpm;
            Reasons = reasons;
            Held = held;
        }

        /// <summary>The hourly roll's threshold: the day's chance spread over twenty-four rolls.</summary>
        public int PerHourPpm => PerDayPpm / 24;
    }

    /// <summary>
    /// <b>The one owner of how likely a prisoner is to break out</b> (design 58 §9). The hourly roll
    /// and the pane's readout both call <see cref="Odds"/>, so the risk a player reads is the risk
    /// rolled — the reference's hidden break chance, made visible and given its reasons.
    ///
    /// <code>
    /// per day = 2 % × mood × door or shackles × watch × hurt × kept × 1 / √(prisoners held)
    /// </code>
    /// The square root is the headcount fix: four prisoners each carry half the risk one would, so
    /// a prison's total doubles rather than quadruples. Every number is a proposal the owner
    /// confirms at the first play.
    /// </summary>
    public static class EscapeRisk
    {
        /// <summary>Two per cent a day: a mean of fifty days for one prisoner nobody minds.</summary>
        public const int BasePpm = 20_000;

        /// <summary>How near, in cells across, a colonist must be to count as watching.</summary>
        public const int WatchRadius = 10;

        /// <summary>
        /// Her odds now. Nought for anybody who is not a held prisoner standing on her own feet:
        /// downed, carried, or already out.
        /// </summary>
        public static EscapeOdds Odds(Pawn pawn, PawnContext ctx)
        {
            int held = Held(ctx);
            if (pawn.Custody != PawnCustody.Prisoner || pawn.Downed || pawn.CarriedBy != 0 || Melee.IsDead(pawn))
                return new EscapeOdds(0, EscapeReasons.None, held);

            long ppm = BasePpm;
            EscapeReasons why = EscapeReasons.None;

            if (pawn.Mood < 300) { ppm *= 3; why |= EscapeReasons.Miserable; }
            else if (pawn.Mood < 500) { ppm = ppm * 3 / 2; why |= EscapeReasons.Unhappy; }
            else if (pawn.Mood > 700) { ppm /= 2; why |= EscapeReasons.Content; }

            if (PrisonerTrees.IsShackled(pawn, ctx)) { ppm *= 2; why |= EscapeReasons.Shackled; }
            else if (ADoorStandsOpen(pawn, ctx)) { ppm *= 2; why |= EscapeReasons.DoorOpen; }

            if (!Watched(pawn, ctx)) { ppm = ppm * 3 / 2; why |= EscapeReasons.Unwatched; }

            if (pawn.HpMilli >= pawn.HpMaxMilli) { ppm = ppm * 3 / 2; why |= EscapeReasons.Unhurt; }
            else { ppm /= 2; why |= EscapeReasons.Hurt; }

            bool fed = pawn.Needs[NeedIndex.Food] >= ctx.Content.Needs[NeedIndex.Food].seekThreshold;
            bool tended = !pawn.HasHealthState || pawn.Health!.UntendedCount == 0;
            if (fed && tended) { ppm = ppm * 7 / 10; why |= EscapeReasons.WellKept; }

            // × 1000 / √n, with √n in thousandths so a colony of two is not rounded to one.
            ppm = ppm * 1_000 / System.Math.Max(1_000, ISqrt((long)System.Math.Max(1, held) * 1_000_000));
            return new EscapeOdds((int)System.Math.Min(1_000_000, ppm), why, held);
        }

        /// <summary>
        /// The hourly roll (design 58 §9b): true when she breaks out this hour. One draw on the
        /// escape stream keyed by her id, against exactly the threshold <see cref="Odds"/> publishes.
        /// </summary>
        public static bool Rolls(Pawn pawn, PawnContext ctx, int tick)
        {
            int threshold = Odds(pawn, ctx).PerHourPpm;
            if (threshold <= 0) return false;
            var rng = DeterministicRandom.ForTick(ctx.Seed, tick, PrisonPurpose.Escape ^ (uint)pawn.Id.Value);
            return rng.NextInt(1_000_000) < threshold;
        }

        /// <summary>Whether this is her hour: rolls are staggered by id, one a game hour each.</summary>
        public static bool Due(Pawn pawn, int tick) => (tick + pawn.Id.Value) % Calendar.TicksPerHour == 0;

        /// <summary>How many prisoners the colony holds right now.</summary>
        public static int Held(PawnContext ctx)
        {
            int n = 0;
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++) if (pawns[i].Custody == PawnCustody.Prisoner) n++;
            return n;
        }

        /// <summary>A door of the room she stands in is open — held by somebody passing, or broken off its hinges.</summary>
        static bool ADoorStandsOpen(Pawn pawn, PawnContext ctx)
        {
            World.ThermalRoom? room = PrisonerTrees.Room(ctx, PrisonerTrees.CellRoom(pawn, ctx));
            if (room == null || ctx.Doors == null) return false;
            var doors = room.Doors;
            for (int i = 0; i < doors.Count; i++) if (ctx.Doors.IsOpen(doors[i].Cell)) return true;
            return false;
        }

        /// <summary>Somebody free and on her feet within <see cref="WatchRadius"/> cells, on her layer or one either side.</summary>
        static bool Watched(Pawn pawn, PawnContext ctx)
        {
            GridSize size = ctx.Size;
            CellRef me = size.FromIndex(pawn.Cell);
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (!other.IsColonist || !Melee.IsStanding(other)) continue;
                CellRef at = size.FromIndex(other.Cell);
                if (System.Math.Abs(at.Y - me.Y) > 1) continue;
                if (System.Math.Abs(at.X - me.X) > WatchRadius || System.Math.Abs(at.Z - me.Z) > WatchRadius) continue;
                return true;
            }
            return false;
        }

        static long ISqrt(long n)
        {
            if (n <= 0) return 0;
            long x = (long)System.Math.Sqrt(n);
            while (x * x > n) x--;
            while ((x + 1) * (x + 1) <= n) x++;
            return x;
        }
    }
}
