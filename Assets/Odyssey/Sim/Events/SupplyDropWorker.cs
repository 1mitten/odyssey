#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Events
{
    /// <summary>
    /// The first event (design 23 §6): a stack of something falls out of the sky and lands on
    /// whatever is under it, and then it is a thing like any other — the starting stockpile
    /// accepts it and a hauler fetches it. The event is the reward; nothing is asked of anyone.
    ///
    /// <para><b>Anywhere on the board</b> (owner, 2026-09-20), which is the reference's own
    /// behaviour and not aimed at the colony: a column is drawn uniformly, and it qualifies if
    /// <see cref="World.CellGrid.SkyLanding"/> finds it a cell open to the sky with room for the
    /// load. Up to <see cref="MaxDraws"/> columns are tried, which on a board that is mostly
    /// meadow finds one on the first or second draw and on a board that is mostly wall, tree and
    /// water still nearly always finds one. A drop that lands out of the colony's reach lies
    /// where it fell: the Events row's jump is how a player finds it, and a reachability re-draw
    /// is the one-line change if that proves maddening.</para>
    ///
    /// <para><b>A fact about the moment.</b> Both draws mix the tick in, for the reason
    /// <c>PawnPurpose.DeconstructRefund</c> gives: keyed on the seed alone, every drop in a world
    /// would land on the same cell with the same stack, stable and then farmable. Keyed on the
    /// tick as well it still replays identically from a seed, which is all determinism asks.</para>
    /// </summary>
    public sealed class SupplyDropWorker : IncidentWorker
    {
        /// <summary>How many columns to try before giving up on a board.</summary>
        public const int MaxDraws = 64;

        public override string Name => "SupplyDrop";

        public override bool CanFireNow(IncidentContext ctx, in IncidentParms parms)
        {
            IncidentDef def = ctx.Content.Defs[parms.Def];
            int item = ctx.Content.ItemIndex[parms.Def];
            if (item < 0) return false;

            // Checked against the largest stack the Def allows, so the answer does not depend on
            // a draw this predicate is not entitled to make. A cell with room for the most has
            // room for whatever TryExecute rolls.
            return TryFindLanding(ctx, parms, item, def.stackMax, out _);
        }

        public override bool TryExecute(IncidentContext ctx, in IncidentParms parms)
        {
            IncidentDef def = ctx.Content.Defs[parms.Def];
            int item = ctx.Content.ItemIndex[parms.Def];
            if (item < 0) return false;

            var payload = ctx.Random(IncidentPurpose.Payload);
            int stack = def.stackMin + payload.NextInt(def.stackMax - def.stackMin + 1);

            if (!TryFindLanding(ctx, parms, item, stack, out int landing)) return false;

            ctx.Skyfallers.Launch(parms.Def, item, stack, landing, ctx.Tick, ctx.Tick + def.fallTicks);
            ctx.Ledger.Record(parms.Def, landing, ctx.Tick);
            return true;
        }

        /// <summary>
        /// A cell the load can land in: the caller's, if it named one that qualifies, else a
        /// column drawn from the board until one does.
        /// </summary>
        static bool TryFindLanding(IncidentContext ctx, in IncidentParms parms, int item, int stack, out int landing)
        {
            if (parms.Cell is { } forced)
            {
                landing = ctx.Cells.SkyLanding(forced.X, forced.Z);
                return landing >= 0 && ctx.Items.CellHasSpace(landing, item, stack);
            }

            var draw = ctx.Random(IncidentPurpose.Landing);
            GridSize size = ctx.Size;
            for (int attempt = 0; attempt < MaxDraws; attempt++)
            {
                int x = draw.NextInt(size.SizeX);
                int z = draw.NextInt(size.SizeZ);
                landing = ctx.Cells.SkyLanding(x, z);
                if (landing >= 0 && ctx.Items.CellHasSpace(landing, item, stack)) return true;
            }

            landing = -1;
            return false;
        }
    }
}
