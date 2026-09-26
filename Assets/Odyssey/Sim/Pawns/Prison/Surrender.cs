#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <b>A badly hurt raider giving up</b> (design 58 §10; Going Medieval's way in, a-20 §4). The
    /// blow that leaves a standing hostile under three-tenths of her hit points rolls once — only
    /// that blow, so a raider is not asked again on every hit — and she yields a quarter of the
    /// time, half when her band is withdrawing or nobody of hers stands near. <b>Only when there
    /// is a free prison bed</b>: no cell, no surrender, so a colony is never handed a prisoner it
    /// has nowhere to put. She drops her weapon, is taken, is given the bed, and walks there
    /// herself — the capture's haul, saved.
    /// </summary>
    public static class Surrender
    {
        /// <summary>Under this, per mille of her pool, she may yield.</summary>
        public const int BelowPerMille = 300;

        /// <summary>A quarter of the time.</summary>
        public const int ChancePerMille = 250;

        /// <summary>How near, in cells across, one of her own must stand for her not to be alone.</summary>
        public const int AloneRadius = 10;

        /// <summary>Whether this blow took her from at or above the line to below it.</summary>
        public static bool Crossed(Pawn target, int before) =>
            (long)before * 1_000 >= (long)target.HpMaxMilli * BelowPerMille
            && (long)target.HpMilli * 1_000 < (long)target.HpMaxMilli * BelowPerMille;

        /// <summary>Her chance of yielding now, per mille, or nought where she may not.</summary>
        public static int ChanceOf(Pawn target, PawnContext ctx)
        {
            if (!target.IsHostile || target.Custody != PawnCustody.Free || !target.IsPerson || target.Downed)
                return 0;
            if (CaptureRules.BedFor(target, target, ctx) < 0) return 0;
            bool withdrawing = ctx.Raids?.GroupOf(target.Id.Value)?.Phase == RaidPhase.Withdrawing;
            return withdrawing || Alone(target, ctx) ? ChancePerMille * 2 : ChancePerMille;
        }

        /// <summary>
        /// The blow is struck: roll, and yield if she does. Called by the one owner of damage,
        /// after it has decided she is still standing. Returns whether she surrendered.
        /// </summary>
        public static bool Consider(Pawn target, int before, PawnContext ctx, int tick)
        {
            if (!Crossed(target, before)) return false;
            int chance = ChanceOf(target, ctx);
            if (chance <= 0) return false;
            var rng = DeterministicRandom.ForTick(ctx.Seed, tick, PrisonPurpose.Surrender ^ (uint)target.Id.Value);
            if (rng.NextInt(1_000) >= chance) return false;
            Yield(target, ctx, tick);
            return true;
        }

        /// <summary>She gives up: taken (which puts her weapon down), a bed given, and the colony told.</summary>
        public static void Yield(Pawn target, PawnContext ctx, int tick)
        {
            if (ctx.Combat == null || !ctx.Combat.Jobs.TakeIntoCustody(target)) return;
            int bed = CaptureRules.BedFor(target, target, ctx);
            if (bed >= 0) ctx.Construction?.AssignOwnerAt(bed, target.Id.Value);
            ctx.Incidents?.Ledger.Record(IncidentHandle.Surrendered, target.Cell, tick);
        }

        static bool Alone(Pawn target, PawnContext ctx)
        {
            GridSize size = ctx.Size;
            CellRef me = size.FromIndex(target.Cell);
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (other == target || !other.IsHostile || !other.IsPerson || !Melee.IsStanding(other)) continue;
                CellRef at = size.FromIndex(other.Cell);
                if (System.Math.Abs(at.Y - me.Y) > 1) continue;
                if (System.Math.Abs(at.X - me.X) > AloneRadius || System.Math.Abs(at.Z - me.Z) > AloneRadius) continue;
                return false;
            }
            return true;
        }
    }
}
