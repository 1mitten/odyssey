#nullable enable
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Trade
{
    /// <summary>
    /// <b>The one owner of what a trader pays and charges</b> (design 65 §3). The ledger's rows, the
    /// commit that re-checks them and anything later that prices a thing all ask here, so a price
    /// shown and a price charged cannot disagree. Per-mille integers, so a price is the same on every
    /// machine.
    ///
    /// <code>
    /// buy  (the trader pays)   = max(1,       floor(value × buyPerMille  × negotiator / 10^6))
    /// sell (the colony pays)   = max(buy + 1, ceil (value × sellPerMille / negotiator))
    /// </code>
    ///
    /// <para><paramref name="negotiatorPerMille"/> is 1,000 until a Social skill exists; it is the one
    /// multiplier that skill will move. Selling always at least a unit above buying is what makes a
    /// buy-and-sell-back loop lose.</para>
    /// </summary>
    public static class TradePricing
    {
        public const int NeutralNegotiator = 1000;

        /// <summary>What the trader pays for one, or nought for a thing with no value.</summary>
        public static int Buy(ItemDef item, TraderKindDef kind, int negotiatorPerMille = NeutralNegotiator)
        {
            if (item.marketValue <= 0) return 0;
            long raw = (long)item.marketValue * kind.buyPerMille * negotiatorPerMille / 1_000_000L;
            return raw < 1 ? 1 : (int)raw;
        }

        /// <summary>What one costs the colony, or nought for a thing with no value.</summary>
        public static int Sell(ItemDef item, TraderKindDef kind, int negotiatorPerMille = NeutralNegotiator)
        {
            if (item.marketValue <= 0) return 0;
            int negotiator = negotiatorPerMille <= 0 ? 1 : negotiatorPerMille;
            long num = (long)item.marketValue * kind.sellPerMille;
            long raw = (num + negotiator - 1) / negotiator;
            int floor = Buy(item, kind, negotiatorPerMille) + 1;
            return raw < floor ? floor : (int)raw;
        }
    }
}
