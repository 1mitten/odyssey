#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Trade
{
    /// <summary>
    /// What kind of trader comes (design 57 §5): who walks in, what purse they carry, what they have
    /// to sell and at what gap, and how long they stay. Loaded from <c>Traders.xml</c> and bound by
    /// <see cref="TraderKind.Bind"/>. One kind today, general goods; a weapons dealer or a food
    /// merchant is another row in that file, not another class.
    /// </summary>
    public class TraderKindDef : Def
    {
        /// <summary>The pawn kind that walks in, by defName: a person of the Visitor faction.</summary>
        public string pawnKind = "PawnKind_Trader";

        /// <summary>The purse, in gold, rolled at the arrival between these two, both inclusive.</summary>
        public int purseMin = 400;

        public int purseMax = 900;

        /// <summary>How many stock lines are rolled from <see cref="stock"/>, both inclusive, never repeating one.</summary>
        public int linesMin = 4;

        public int linesMax = 7;

        /// <summary>
        /// What the trader charges, per mille of an item's value (design 57 §3): 1,400 is ×1.4. Always
        /// above <see cref="buyPerMille"/>, and <c>TradePricing</c> holds a unit's gap besides, so a
        /// buy-and-sell-back loop always loses.
        /// </summary>
        public int sellPerMille = 1400;

        /// <summary>What the trader pays, per mille of an item's value: 600 is ×0.6.</summary>
        public int buyPerMille = 600;

        /// <summary>How long the trader stays by the hearth before leaving, in game hours. The clock stops while a negotiation is open.</summary>
        public int stayHours = 24;

        public List<TraderStockEntry> stock = new List<TraderStockEntry>();
    }

    /// <summary>One line a trader may carry: an item, how likely it is against the others, and how many.</summary>
    public class TraderStockEntry
    {
        public string item = string.Empty;
        public int weight = 100;
        public int min = 1;
        public int max = 1;
    }

    /// <summary>A <see cref="TraderKindDef"/> bound to the pawn content: its pawn kind and every line's item as indices.</summary>
    public sealed class TraderKind
    {
        TraderKind(TraderKindDef def, int pawnKind, int[] items)
        {
            Def = def;
            PawnKind = pawnKind;
            Items = items;
        }

        public TraderKindDef Def { get; }

        /// <summary>The <c>PawnKindIndex</c> that walks in.</summary>
        public int PawnKind { get; }

        /// <summary>Each stock line's <c>ItemIndex</c>, parallel to <see cref="TraderKindDef.stock"/>.</summary>
        public int[] Items { get; }

        /// <summary>
        /// Bind a Def, or throw a <see cref="DefLoadException"/> naming it: the pawn kind must be a
        /// visitor, every line an item with a value, and every range the right way round.
        /// </summary>
        public static TraderKind Bind(TraderKindDef def, PawnContent pawns)
        {
            int kind = -1;
            for (int k = 0; k < pawns.Kinds.Length; k++)
                if (pawns.Kinds[k].defName == def.pawnKind) { kind = k; break; }
            if (kind < 0)
                throw new DefLoadException($"{def.Origin}: trader kind '{def.defName}' names pawn kind '{def.pawnKind}', which the pawn content does not have.");
            if (pawns.Kinds[kind].faction != Faction.Visitor)
                throw new DefLoadException($"{def.Origin}: trader kind '{def.defName}' names pawn kind '{def.pawnKind}', which is not a visitor.");
            if (def.purseMin < 0 || def.purseMax < def.purseMin)
                throw new DefLoadException($"{def.Origin}: trader kind '{def.defName}' has a purse of {def.purseMin} to {def.purseMax}.");
            if (def.linesMin < 0 || def.linesMax < def.linesMin || def.linesMax > def.stock.Count)
                throw new DefLoadException($"{def.Origin}: trader kind '{def.defName}' rolls {def.linesMin} to {def.linesMax} lines from {def.stock.Count}.");
            if (def.buyPerMille <= 0 || def.sellPerMille <= def.buyPerMille)
                throw new DefLoadException($"{def.Origin}: trader kind '{def.defName}' sells at {def.sellPerMille} and buys at {def.buyPerMille} per mille; it must sell dearer than it buys.");
            if (def.stayHours <= 0)
                throw new DefLoadException($"{def.Origin}: trader kind '{def.defName}' stays {def.stayHours} hours.");

            var items = new int[def.stock.Count];
            for (int i = 0; i < def.stock.Count; i++)
            {
                TraderStockEntry line = def.stock[i];
                int item = -1;
                for (int t = 0; t < pawns.Items.Length; t++)
                    if (pawns.Items[t].defName == line.item) { item = t; break; }
                if (item < 0)
                    throw new DefLoadException($"{def.Origin}: trader kind '{def.defName}' stocks '{line.item}', which the pawn content does not carry.");
                if (item == ItemIndex.Gold)
                    throw new DefLoadException($"{def.Origin}: trader kind '{def.defName}' stocks gold, which is the purse, never a line.");
                if (pawns.Items[item].marketValue <= 0)
                    throw new DefLoadException($"{def.Origin}: trader kind '{def.defName}' stocks '{line.item}', which has no value to price it by.");
                if (line.weight <= 0 || line.min <= 0 || line.max < line.min)
                    throw new DefLoadException($"{def.Origin}: trader kind '{def.defName}' has line '{line.item}' with weight {line.weight} and {line.min} to {line.max}.");
                items[i] = item;
            }
            return new TraderKind(def, kind, items);
        }
    }

    /// <summary>
    /// A trader incident's own parameters (design 57 §5): which kind of trader comes. The second
    /// per-worker block on <see cref="Events.IncidentDef"/>, after the raid's.
    /// </summary>
    public sealed class TraderParams
    {
        /// <summary>The <see cref="TraderKindDef"/> that comes, by defName.</summary>
        public string kind = "TraderKind_General";
    }
}
