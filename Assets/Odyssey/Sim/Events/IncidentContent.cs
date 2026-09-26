#nullable enable
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Events
{
    /// <summary>
    /// Whether an incident is a gift, a nuisance or neither: the reference's "letter kind", and
    /// the axis a storyteller will one day weight when it picks (design 23 §3). Presentation
    /// reads it for colour; nothing in the simulation reads it yet.
    /// </summary>
    public enum IncidentFavourability
    {
        Neutral = 0,
        Good = 1,
        Bad = 2,
    }

    /// <summary>
    /// The bag an incident is drawn from. The reference picks a category first and an incident
    /// inside it by weight, which is what lets a storyteller pace threats and gifts on separate
    /// clocks (design 23 §3). One category is populated today.
    /// </summary>
    public enum IncidentCategory
    {
        Misc = 0,
        ThreatSmall = 1,
        ThreatBig = 2,
        Arrival = 3,
        Condition = 4,
    }

    /// <summary>
    /// One kind of event the world can have happen to it (design 23 §4).
    ///
    /// <para><b>Two halves.</b> The gates — <see cref="earliestDay"/> to <see cref="maxFires"/>
    /// — say when a storyteller may pick this; the worker parameters — <see cref="item"/> to
    /// <see cref="fallTicks"/> — say what happens when it is picked. The gates are read by nothing
    /// yet: there is no storyteller, and the one thing that fires an incident today is the debug
    /// menu, which by design ignores them. They are declared now so the content's shape is settled
    /// before a scheduler exists to read it, and so the fingerprint pins them.</para>
    ///
    /// <para><b>The worker is named, not typed.</b> A Def is data and cannot hold a class; it
    /// holds the name a worker answers to, and <see cref="IncidentContent.FromDefs"/> resolves it
    /// at load so a misspelt worker is a content error with a file and a line rather than an
    /// event that silently never fires.</para>
    /// </summary>
    public class IncidentDef : Def
    {
        /// <summary>The registry key the Events panel and the wiki name this by, <c>ui.bulletin.*</c>.</summary>
        public string bulletinKey = string.Empty;

        public IncidentFavourability favourability = IncidentFavourability.Neutral;

        public IncidentCategory category = IncidentCategory.Misc;

        /// <summary>The <see cref="IncidentWorker.Name"/> of the class that fires this.</summary>
        public string worker = string.Empty;

        // ---- gates, for the storyteller that does not exist yet ----------------------------

        /// <summary>The first colony day this may fire on, counted from 0.</summary>
        public int earliestDay;

        /// <summary>Days that must pass after one firing before the next.</summary>
        public int minRefireDays;

        /// <summary>Its weight within its category, in the same units <c>OreKindDef.weight</c> uses.</summary>
        public int weight = 100;

        /// <summary>Colonists the colony must have for this to be worth firing.</summary>
        public int minColonists = 1;

        /// <summary>How many times it may ever fire, or 0 for no limit.</summary>
        public int maxFires;

        // ---- the worker's parameters ----------------------------------------------------------

        /// <summary>What a drop pays out. Empty for an incident that delivers nothing.</summary>
        [DefReference(typeof(ItemDef), Optional = true)] public string item = string.Empty;

        public int stackMin = 1;

        public int stackMax = 1;

        /// <summary>How long a thing is in the air before it lands. 0 lands it on the same tick.</summary>
        public int fallTicks;

        /// <summary>
        /// A raid's own parameters (design 55 §8), or null for any incident that is not one: the first
        /// per-worker block, the shape design 23 §8 asked for rather than widening the flat set.
        /// </summary>
        public RaidParams? raid;

        /// <summary>
        /// A trader's own parameters (design 57 §5): which kind of trader comes. Null for any
        /// incident that is not a trader's arrival.
        /// </summary>
        public Trade.TraderParams? trader;
    }

    /// <summary>
    /// The events content, read from a loaded <see cref="DefDatabase"/> and bound: every Def
    /// beside the item index it pays out and the worker that fires it (design 23 §4).
    ///
    /// <para><b><see cref="Order"/> is the contract</b>, exactly as <c>WorldContent.TerrainOrder</c>
    /// is: position in it <i>is</i> the incident index, which the ledger saves and hashes and
    /// <c>IncidentHandle</c> in the contracts assembly repeats. <c>IncidentContentTests</c> holds
    /// the two lists to the same length and the interface's key table to the same order.</para>
    /// </summary>
    public sealed class IncidentContent
    {
        /// <summary>Every incident in index order. Parallel to <c>IncidentHandle</c>.</summary>
        public static readonly string[] Order =
        {
            "Incident_SupplyDrop",
            "Incident_ScrapDrop",
            // Written down by the world, never fired (design 33 §17): a bandit leaving the board.
            "Incident_Theft",
            "Incident_BanditLeft",
            "Incident_MedicalDrop",
            // A band of hostiles from one edge (design 55), appended.
            "Incident_Raid",
            // A trader walks in from an edge and waits by the hearth (design 57), appended.
            "Incident_Trader",
        };

        /// <summary>
        /// Every raid mix in index order (design 55 §8), the same contract as <see cref="Order"/>:
        /// position <i>is</i> the mix index the intent carries and the ledger records, so append,
        /// never insert. The debug dropdown lists them in this order.
        /// </summary>
        public static readonly string[] MixOrder =
        {
            "RaidMix_Bandits",
            "RaidMix_Gunmen",
            "RaidMix_Mixed",
        };

        /// <summary>
        /// Every trader kind in index order (design 57 §5), the same contract as <see cref="Order"/>:
        /// position <i>is</i> the kind index a visit saves, so append, never insert.
        /// </summary>
        public static readonly string[] TraderOrder =
        {
            "TraderKind_General",
        };

        public IncidentDef[] Defs = System.Array.Empty<IncidentDef>();

        /// <summary>The raid mixes, bound, in <see cref="MixOrder"/>.</summary>
        public RaidMix[] Mixes = System.Array.Empty<RaidMix>();

        /// <summary>The trader kinds, bound, in <see cref="TraderOrder"/>.</summary>
        public Trade.TraderKind[] Traders = System.Array.Empty<Trade.TraderKind>();

        /// <summary>The <c>ItemIndex</c> each incident pays out, or -1 for none. Parallel to <see cref="Defs"/>.</summary>
        public int[] ItemIndex = System.Array.Empty<int>();

        /// <summary>The worker each incident fires through. Parallel to <see cref="Defs"/>.</summary>
        public IncidentWorker[] Workers = System.Array.Empty<IncidentWorker>();

        public int Count => Defs.Length;

        /// <summary>The Def types this content is made of, registered in one place.</summary>
        public static DefLoader Register(DefLoader loader) =>
            loader.Register<IncidentDef>().Register<RaidMixDef>().Register<Trade.TraderKindDef>();

        /// <summary>
        /// Read and bind. A missing Def, an unknown worker or an item the pawn content does not
        /// carry throw here, naming the culprit, rather than surfacing as an event that does
        /// something odd on the day it first fires; what a worker's own fields must satisfy is
        /// checked by <see cref="IncidentWorker.Validate"/>, so a stack range written backwards
        /// throws too, from the one worker that reads stack ranges.
        /// </summary>
        public static IncidentContent FromDefs(DefDatabase defs, PawnContent pawns)
        {
            var content = new IncidentContent
            {
                Defs = new IncidentDef[Order.Length],
                ItemIndex = new int[Order.Length],
                Workers = new IncidentWorker[Order.Length],
            };

            for (int i = 0; i < Order.Length; i++)
            {
                IncidentDef def = One(defs, Order[i]);
                content.Defs[i] = def;

                if (def.bulletinKey.Length == 0)
                    throw new DefLoadException($"{def.Origin}: incident '{def.defName}' has no bulletinKey, so nothing could name it on screen.");
                if (def.worker.Length == 0)
                    throw new DefLoadException($"{def.Origin}: incident '{def.defName}' names no worker.");

                IncidentWorker? worker = IncidentWorkerRegistry.Resolve(def.worker);
                content.Workers[i] = worker ?? throw new DefLoadException(
                    $"{def.Origin}: incident '{def.defName}' names worker '{def.worker}', which nothing in the " +
                    $"simulation assembly answers to. Known: {string.Join(", ", IncidentWorkerRegistry.Names)}.");

                content.ItemIndex[i] = def.item.Length == 0 ? -1 : ItemIndexOf(pawns, def);
            }

            // The mixes after the workers resolve and before any is asked to validate: a raid's
            // Def names a mix, and its worker checks the name (design 55 §8).
            content.Mixes = new RaidMix[MixOrder.Length];
            for (int m = 0; m < MixOrder.Length; m++)
                content.Mixes[m] = RaidMix.Bind(OneMix(defs, MixOrder[m]), pawns);

            // The trader kinds (design 57 §5), bound before the workers validate: a trader's Def
            // names one.
            content.Traders = new Trade.TraderKind[TraderOrder.Length];
            for (int t = 0; t < TraderOrder.Length; t++)
                content.Traders[t] = Trade.TraderKind.Bind(OneTrader(defs, TraderOrder[t]), pawns);

            // What each worker's own fields must satisfy is the worker's to say.
            for (int i = 0; i < Order.Length; i++)
                content.Workers[i].Validate(content.Defs[i], pawns);

            // A raid's mix is a name the worker cannot resolve alone (it sees the pawns, not the mixes).
            for (int i = 0; i < Order.Length; i++)
            {
                IncidentDef def = content.Defs[i];
                if (def.trader != null && content.TraderIndex(def.trader.kind) < 0)
                    throw new DefLoadException(
                        $"{def.Origin}: incident '{def.defName}' names trader kind '{def.trader.kind}', which the content does not have.");
                if (def.raid != null && content.MixIndex(def.raid.mix) < 0)
                    throw new DefLoadException(
                        $"{def.Origin}: incident '{def.defName}' names raid mix '{def.raid.mix}', which the content does not have.");
            }

            return content;
        }

        /// <summary>
        /// The pawn content's index for the item a Def names. By name over the bound table rather
        /// than through the database's own handle, because the item index the ledger, the save and
        /// every <c>ThingView</c> carry is <c>PawnContent.FromDefs</c>'s list order and not the
        /// loader's sorted one — the same two orders <c>WorldContent</c> keeps apart.
        /// </summary>
        static int ItemIndexOf(PawnContent pawns, IncidentDef def)
        {
            for (int i = 0; i < pawns.Items.Length; i++)
                if (pawns.Items[i].defName == def.item) return i;
            throw new DefLoadException(
                $"{def.Origin}: incident '{def.defName}' pays out '{def.item}', which the pawn content does not carry.");
        }

        /// <summary>The index of the mix named <paramref name="defName"/> in <see cref="MixOrder"/>, or -1.</summary>
        public int MixIndex(string defName)
        {
            for (int m = 0; m < Mixes.Length; m++)
                if (Mixes[m].Def.defName == defName) return m;
            return -1;
        }

        /// <summary>The index of the trader kind named <paramref name="defName"/> in <see cref="TraderOrder"/>, or -1.</summary>
        public int TraderIndex(string defName)
        {
            for (int t = 0; t < Traders.Length; t++)
                if (Traders[t].Def.defName == defName) return t;
            return -1;
        }

        static Trade.TraderKindDef OneTrader(DefDatabase defs, string defName)
        {
            if (!defs.HasTable<Trade.TraderKindDef>())
                throw new DefLoadException($"the content has no TraderKindDef at all, and '{defName}' is required.");
            if (!defs.Table<Trade.TraderKindDef>().TryGetHandle(defName, out var handle))
                throw new DefLoadException($"the content has no TraderKindDef named '{defName}'.");
            return defs.Table<Trade.TraderKindDef>()[handle];
        }

        static RaidMixDef OneMix(DefDatabase defs, string defName)
        {
            if (!defs.HasTable<RaidMixDef>())
                throw new DefLoadException($"the content has no RaidMixDef at all, and '{defName}' is required.");
            if (!defs.Table<RaidMixDef>().TryGetHandle(defName, out var handle))
                throw new DefLoadException($"the content has no RaidMixDef named '{defName}'.");
            return defs.Table<RaidMixDef>()[handle];
        }

        static IncidentDef One(DefDatabase defs, string defName)
        {
            if (!defs.HasTable<IncidentDef>())
                throw new DefLoadException($"the content has no IncidentDef at all, and '{defName}' is required.");
            if (!defs.Table<IncidentDef>().TryGetHandle(defName, out var handle))
                throw new DefLoadException($"the content has no IncidentDef named '{defName}'.");
            return defs.Table<IncidentDef>()[handle];
        }
    }
}
