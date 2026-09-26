#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Events;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Pawns.Wildlife;

namespace Odyssey.Sim.Trade
{
    /// <summary>
    /// A trader walks in (design 57 §5): one person of the incident's trader kind, on an edge drawn
    /// the way a raid's is (<see cref="EdgeArrival"/>), with a purse and a stock rolled at the
    /// arrival. Its own mind takes it to the hearth; <see cref="TradeSystem"/> sends it home.
    ///
    /// <para><b>One trader at a time.</b> The trade window serves one visit, and a second trader
    /// arriving while the first waits would be a queue nobody asked for.</para>
    /// </summary>
    public sealed class TraderWorker : IncidentWorker
    {
        public override string Name => "Trader";

        public override void Validate(IncidentDef def, PawnContent pawns)
        {
            if (def.trader == null)
                throw new DefLoadException($"{def.Origin}: incident '{def.defName}' is a trader's arrival with no <trader> block.");
        }

        public override bool CanFireNow(IncidentContext ctx, in IncidentParms parms)
        {
            TraderParams? p = ctx.Content.Defs[parms.Def].trader;
            TradeSystem? trade = ctx.Pawns.Trade;
            if (p == null || trade == null) return false;
            if (trade.Count > 0) return false;
            if (ctx.Content.TraderIndex(p.kind) < 0) return false;
            if (!RaidWorker.Fits(ctx.Pawns, 1)) return false;
            return RaidWorker.Origin(ctx.Pawns) >= 0;
        }

        public override bool TryExecute(IncidentContext ctx, in IncidentParms parms)
        {
            PawnContext pawns = ctx.Pawns;
            TradeSystem? trade = pawns.Trade;
            TraderParams? p = ctx.Content.Defs[parms.Def].trader;
            if (p == null || trade == null) return false;
            int kindIndex = ctx.Content.TraderIndex(p.kind);
            if (kindIndex < 0) return false;

            int origin = RaidWorker.Origin(pawns);
            if (origin < 0) return false;
            SurfaceCensus census = SurfaceCensus.Take(ctx.Cells, pawns.Nav, null, ctx.Size.FromIndex(origin), 0, TraverseMode.Bandit);
            if (!EdgeArrival.TryPick(ctx, census, TradePurpose.Edge, out _, out int centre, out List<int> _))
                return false;

            int cell = pawns.Pawns.FreeSpawnCell(centre, TraverseMode.Bandit);
            Pawn trader = pawns.Pawns.Spawn(cell, ctx.Content.Traders[kindIndex].PawnKind);
            var rng = ctx.Random(TradePurpose.Stock);
            trade.Begin(trader, kindIndex, ctx.Tick, ref rng);
            ctx.Ledger.Record(parms.Def, cell, ctx.Tick);
            return true;
        }
    }
}
