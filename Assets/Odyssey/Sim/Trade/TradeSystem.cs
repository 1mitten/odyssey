#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Events;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Pawns.Wildlife;
using Odyssey.Sim.Saving;

namespace Odyssey.Sim.Trade
{
    /// <summary>
    /// One trader on the board (design 57 §5): who, what kind, how long it has left, its purse and
    /// its stock. The stock is counts, not things: nothing of it is on the board until the colony
    /// buys it, when it is set down beside the trader.
    /// </summary>
    public sealed class Visit
    {
        public int Id;

        /// <summary>The trader's pawn id.</summary>
        public int Pawn;

        /// <summary>The trader kind, an index into <see cref="IncidentContent.Traders"/>.</summary>
        public int Kind;

        /// <summary>The tick it arrived.</summary>
        public int ArrivedTick;

        /// <summary>Ticks of its stay still to run. Counts down only while no negotiation is open (design 57 §5).</summary>
        public int StayLeft;

        /// <summary>The gold it carries.</summary>
        public int Purse;

        /// <summary>How many of each item it carries, by <c>ItemIndex</c>. Gold is never here: it is the purse.</summary>
        public int[] Stock = Array.Empty<int>();

        /// <summary>The negotiating colonist's pawn id, or 0 while nobody is (design 57 §6).</summary>
        public int Negotiator;

        /// <summary>The negotiator is beside the trader and the ledger may open.</summary>
        public bool Ready;

        /// <summary>
        /// How many sessions this visit has opened, so the interface opens its window once per
        /// session and never again for the one it has already shown.
        /// </summary>
        public int Session;

        public bool InSession => Negotiator != 0;
    }

    /// <summary>
    /// Every trader on the board, and the clock that sends each home (design 57 §5). A visit starts
    /// when the trader incident spawns one — or when any visitor appears without one, which is how
    /// the debug menu's Spawn trader row gets a purse and a stock — and ends when the trader walks
    /// off an edge, dies, or is taken off the board.
    ///
    /// <para><b>In the pawn phase at order 16</b>, after the raids (15), so a raid that arrived this
    /// tick sends a trader away the same tick, and before the jobs (20), so a trader told to leave
    /// thinks about the edge on the tick it was told.</para>
    ///
    /// <para><b>Scales with the visits</b>, which is one: a lookup and a decrement each tick. A scan
    /// of every pawn every <see cref="AdoptTicks"/> finds a visitor with no visit. Nothing else while
    /// no trader is on the board.</para>
    ///
    /// <para><b>Hashed and saved only while a visit exists</b>, the pattern the raids set, so a colony
    /// no trader ever visited hashes as it did before trading and no golden moved.</para>
    /// </summary>
    public sealed class TradeSystem : IWorldSystem, IStateHashable, ISaveable, ISnapshotContributor
    {
        /// <summary>How often visitors without a visit are looked for, in ticks.</summary>
        public const int AdoptTicks = 30;

        /// <summary>The record layout this build writes. 1: the first.</summary>
        public const int Layout = 1;

        readonly PawnContext _ctx;
        readonly List<Visit> _visits = new List<Visit>();
        int _nextId = 1;

        // The deal being described (design 57 §6): the TradeLines since the last commit, for one
        // trader. Never saved or hashed — it lives from one intent drain to the commit in the same
        // drain, and is thrown away by the commit, by a line for another trader and by the tick.
        readonly List<(int Item, int Count)> _lines = new List<(int Item, int Count)>();
        int _linesFor;

        int[] _colony = Array.Empty<int>();
        byte[] _worst = Array.Empty<byte>();
        readonly List<TradeDrops.Drop> _plan = new List<TradeDrops.Drop>();

        public TradeSystem(PawnContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public string Name => "Trade";
        public TickPhase Phase => TickPhase.Pawns;
        public int Order => 16;

        /// <summary>The job pipeline, to turn a trader for the edge. Set by the composition root.</summary>
        internal JobSystem? Jobs { get; set; }

        public IReadOnlyList<Visit> Visits => _visits;

        public int Count => _visits.Count;

        /// <summary>The visit a pawn is the trader of, or null.</summary>
        public Visit? VisitOf(int pawn)
        {
            for (int i = 0; i < _visits.Count; i++) if (_visits[i].Pawn == pawn) return _visits[i];
            return null;
        }

        /// <summary>
        /// A new visit for a trader already on the board: its purse and stock rolled from its kind
        /// on <paramref name="rng"/>, and its stay the kind's hours.
        /// </summary>
        public Visit Begin(Pawn trader, int kindIndex, int tick, ref DeterministicRandom rng)
        {
            TraderKind kind = Kinds()[kindIndex];
            TraderKindDef def = kind.Def;
            var visit = new Visit
            {
                Id = _nextId++,
                Pawn = trader.Id.Value,
                Kind = kindIndex,
                ArrivedTick = tick,
                StayLeft = def.stayHours * Calendar.TicksPerHour,
                Purse = def.purseMin + rng.NextInt(def.purseMax - def.purseMin + 1),
                Stock = new int[_ctx.Content.Items.Length],
            };

            // Lines by weight, without repeating one.
            int lines = def.linesMin + rng.NextInt(def.linesMax - def.linesMin + 1);
            var taken = new bool[def.stock.Count];
            for (int n = 0; n < lines; n++)
            {
                int total = 0;
                for (int i = 0; i < def.stock.Count; i++) if (!taken[i]) total += def.stock[i].weight;
                if (total <= 0) break;
                int roll = rng.NextInt(total);
                for (int i = 0; i < def.stock.Count; i++)
                {
                    if (taken[i]) continue;
                    roll -= def.stock[i].weight;
                    if (roll >= 0) continue;
                    taken[i] = true;
                    TraderStockEntry line = def.stock[i];
                    visit.Stock[kind.Items[i]] += line.min + rng.NextInt(line.max - line.min + 1);
                    break;
                }
            }

            _visits.Add(visit);
            return visit;
        }

        public void Tick(SimWorld world)
        {
            _lines.Clear();
            _linesFor = 0;
            int tick = world.CurrentTick;
            if (tick % AdoptTicks == 0) Adopt(tick);
            if (_visits.Count == 0) return;
            _ctx.Sync(world);

            bool raid = (_ctx.Raids?.Count ?? 0) > 0;
            for (int v = _visits.Count - 1; v >= 0; v--)
            {
                Visit visit = _visits[v];
                Pawn? trader = _ctx.Pawns.Get(new PawnId(visit.Pawn));
                if (trader == null)
                {
                    // Dead, or taken off the board some other way. Its stock goes with it (design 57 §5).
                    _visits.RemoveAt(v);
                    continue;
                }

                if (!trader.Leaving)
                {
                    if (!visit.InSession && visit.StayLeft > 0) visit.StayLeft--;
                    if (visit.StayLeft <= 0 || raid) SendAway(visit, trader);
                    continue;
                }

                if (WildlifeSystem.IsEdge(_ctx.Size, trader.Cell) && !trader.HasPath
                    && (trader.CurrentJob == null || trader.CurrentJob.DefIndex == JobIndex.Wait))
                {
                    Depart(trader);
                    _visits.RemoveAt(v);
                }
            }
        }

        /// <summary>
        /// Send the trader home now: the session ends, and it turns for the nearest edge. What an
        /// expired stay, a raid, and accidental harm all do (design 57 §5, §7).
        /// </summary>
        public void SendAway(Visit visit, Pawn trader)
        {
            EndSession(visit);
            if (trader.Leaving) return;
            trader.Leaving = true;
            visit.StayLeft = 0;
            if (Jobs != null && !trader.Downed && trader.CurrentJob != null) Jobs.Interrupt(trader, JobStatus.Failed);
        }

        /// <summary>The negotiation is over, however it ended. The window closes on the next frame.</summary>
        public void EndSession(Visit visit)
        {
            visit.Negotiator = 0;
            visit.Ready = false;
        }

        /// <summary>Off the board, with its pistol: a guest's weapon leaves with it (design 57 §5).</summary>
        void Depart(Pawn trader)
        {
            ColonyItem? weapon = WeaponHand.Held(trader, _ctx);
            trader.EquippedItem = 0;
            if (weapon != null) _ctx.Items.Despawn(weapon);
            Jobs?.EndJob(trader, JobStatus.Succeeded);
            _ctx.Pawns.Despawn(trader);
        }

        /// <summary>Give every visitor on the board that has no visit one of the first kind: the debug row's trader.</summary>
        void Adopt(int tick)
        {
            IReadOnlyList<Pawn> all = _ctx.Pawns.All;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn pawn = all[i];
                if (!pawn.IsVisitor || VisitOf(pawn.Id.Value) != null) continue;
                if (Kinds().Length == 0) return;
                var rng = DeterministicRandom.ForTick(_ctx.Seed, tick, TradePurpose.Stock ^ (uint)pawn.Id.Value);
                Begin(pawn, 0, tick, ref rng);
            }
        }

        TraderKind[] Kinds() => _ctx.Incidents?.Content.Traders ?? Array.Empty<TraderKind>();

        // ---- the deal (design 57 §6) -------------------------------------------------------------

        /// <summary>The kind of trader a visit is.</summary>
        public TraderKind KindOf(Visit visit) => Kinds()[visit.Kind];

        /// <summary>What the trader pays for one of <paramref name="item"/>.</summary>
        public int BuyPrice(Visit visit, int item) => TradePricing.Buy(_ctx.Content.Items[item], KindOf(visit).Def);

        /// <summary>What one of <paramref name="item"/> costs the colony.</summary>
        public int SellPrice(Visit visit, int item) => TradePricing.Sell(_ctx.Content.Items[item], KindOf(visit).Def);

        /// <summary><c>TradeLine(A = trader, B = item, C = signed count)</c>: buffered for the commit.</summary>
        public IntentRejection HandleLine(Intent intent)
        {
            Visit? visit = VisitOf(intent.A);
            if (visit == null) return IntentRejection.OutOfBounds;
            if (intent.B < 0 || intent.B >= _ctx.Content.Items.Length || intent.B == ItemIndex.Gold) return IntentRejection.OutOfBounds;
            if (intent.C == 0) return IntentRejection.AlreadyInThatState;
            if (_linesFor != intent.A)
            {
                _lines.Clear();
                _linesFor = intent.A;
            }
            _lines.Add((intent.B, intent.C));
            return IntentRejection.None;
        }

        /// <summary>
        /// <c>TradeCommit(A = trader, B = balance)</c>: the lines since the last commit, applied whole
        /// or refused whole. Refused unless the session is ready, something moves, every count is one
        /// the side holds, the balance is the one the ledger showed, the side that pays can pay, and
        /// every bought stack has somewhere to land.
        /// </summary>
        public IntentRejection HandleCommit(Intent intent)
        {
            bool mine = _linesFor == intent.A;
            var lines = new List<(int Item, int Count)>(mine ? _lines : new List<(int, int)>());
            _lines.Clear();
            _linesFor = 0;

            Visit? visit = VisitOf(intent.A);
            if (visit == null) return IntentRejection.OutOfBounds;
            Pawn? trader = _ctx.Pawns.Get(new PawnId(visit.Pawn));
            if (trader == null || !visit.InSession || !visit.Ready) return IntentRejection.NotPermitted;

            int items = _ctx.Content.Items.Length;
            var net = new int[items];
            for (int i = 0; i < lines.Count; i++) net[lines[i].Item] += lines[i].Count;

            Ensure(items);
            ColonyTradeStock.Count(_ctx, trader.Cell, _colony);
            long balance = 0;
            bool moving = false;
            for (int item = 0; item < items; item++)
            {
                int n = net[item];
                if (n == 0) continue;
                moving = true;
                if (_ctx.Content.Items[item].marketValue <= 0) return IntentRejection.NotPermitted;
                if (n > 0)
                {
                    if (n > visit.Stock[item]) return IntentRejection.NotPermitted;
                    balance += (long)n * SellPrice(visit, item);
                }
                else
                {
                    if (-n > _colony[item]) return IntentRejection.NotPermitted;
                    balance -= (long)-n * BuyPrice(visit, item);
                }
            }
            if (!moving) return IntentRejection.AlreadyInThatState;
            if (balance != intent.B) return IntentRejection.NotPermitted;
            if (balance > 0 && balance > _colony[ItemIndex.Gold]) return IntentRejection.NotPermitted;
            if (balance < 0 && -balance > visit.Purse) return IntentRejection.NotPermitted;

            var goods = new List<(int Item, int Count)>();
            for (int item = 0; item < items; item++) if (net[item] > 0) goods.Add((item, net[item]));
            if (balance < 0) goods.Add((ItemIndex.Gold, (int)-balance));
            if (!TradeDrops.TryPlan(_ctx, trader.Cell, goods, _plan)) return IntentRejection.NotPermitted;

            // Everything checked: now it happens, all of it.
            for (int item = 0; item < items; item++)
            {
                int n = net[item];
                if (n < 0)
                {
                    ColonyTradeStock.Take(_ctx, trader.Cell, item, -n);
                    visit.Stock[item] += -n;
                }
                else if (n > 0) visit.Stock[item] -= n;
            }
            if (balance > 0) ColonyTradeStock.Take(_ctx, trader.Cell, ItemIndex.Gold, (int)balance);
            visit.Purse += (int)balance;
            TradeDrops.Apply(_ctx, _plan);
            return IntentRejection.None;
        }

        /// <summary><c>TradeCancel(A = trader)</c>: the negotiation ends; the negotiator's job sees it and stops.</summary>
        public IntentRejection HandleCancel(Intent intent)
        {
            Visit? visit = VisitOf(intent.A);
            if (visit == null) return IntentRejection.OutOfBounds;
            if (!visit.InSession) return IntentRejection.AlreadyInThatState;
            EndSession(visit);
            return IntentRejection.None;
        }

        void Ensure(int items)
        {
            if (_colony.Length != items) _colony = new int[items];
            if (_worst.Length != items) _worst = new byte[items];
        }

        // ---- publish -------------------------------------------------------------------------------

        /// <summary>
        /// A <see cref="TradeView"/> for every visit and, for a ready session only, a row for every
        /// item either side holds. The colony's stock is counted only then: a trader standing by the
        /// fire with nobody negotiating costs one view a publish.
        /// </summary>
        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            for (int v = 0; v < _visits.Count; v++)
            {
                Visit visit = _visits[v];
                Pawn? trader = _ctx.Pawns.Get(new PawnId(visit.Pawn));
                if (trader == null) continue;

                int items = _ctx.Content.Items.Length;
                Ensure(items);
                bool rows = visit.InSession && visit.Ready;
                int gold = 0;
                if (rows)
                {
                    ColonyTradeStock.Count(_ctx, trader.Cell, _colony, _worst);
                    gold = _colony[ItemIndex.Gold];
                }

                writer.AddTrade(new TradeView(visit.Id, new PawnId(visit.Pawn), new PawnId(visit.Negotiator), visit.Ready,
                    visit.Session, visit.Purse, gold, visit.StayLeft, trader.Leaving));
                if (!rows) continue;

                for (int item = 0; item < items; item++)
                {
                    if (item == ItemIndex.Gold) continue;
                    ItemDef def = _ctx.Content.Items[item];
                    if (def.marketValue <= 0) continue;
                    int colony = _colony[item], stock = item < visit.Stock.Length ? visit.Stock[item] : 0;
                    if (colony == 0 && stock == 0) continue;
                    byte traderQuality = def.weapon != null && stock > 0 ? (byte)QualityHandle.Normal : (byte)0;
                    writer.AddTradeRow(new TradeRowView(visit.Id, item, colony, stock, BuyPrice(visit, item),
                        SellPrice(visit, item), _worst[item], traderQuality));
                }
            }
        }

        // ---- hash --------------------------------------------------------------------------------

        public void ContributeTo(ref StateHash hash)
        {
            if (_visits.Count == 0) return;
            hash.Add(_visits.Count);
            hash.Add(_nextId);
            for (int v = 0; v < _visits.Count; v++)
            {
                Visit visit = _visits[v];
                hash.Add(visit.Id);
                hash.Add(visit.Pawn);
                hash.Add(visit.Kind);
                hash.Add(visit.ArrivedTick);
                hash.Add(visit.StayLeft);
                hash.Add(visit.Purse);
                hash.Add(visit.Negotiator);
                hash.Add(visit.Ready ? 1 : 0);
                hash.Add(visit.Session);
                for (int i = 0; i < visit.Stock.Length; i++)
                {
                    if (visit.Stock[i] == 0) continue;
                    hash.Add(i);
                    hash.Add(visit.Stock[i]);
                }
            }
        }

        // ---- save --------------------------------------------------------------------------------

        public string SaveKey => "odyssey.trade";

        public void Save(SaveWriter writer)
        {
            writer.Write(Layout);
            writer.Write(_nextId);
            writer.Write(_visits.Count);
            for (int v = 0; v < _visits.Count; v++)
            {
                Visit visit = _visits[v];
                writer.Write(visit.Id);
                writer.Write(visit.Pawn);
                writer.Write(visit.Kind);
                writer.Write(visit.ArrivedTick);
                writer.Write(visit.StayLeft);
                writer.Write(visit.Purse);
                writer.Write(visit.Negotiator);
                writer.Write(visit.Ready);
                writer.Write(visit.Session);
                int lines = 0;
                for (int i = 0; i < visit.Stock.Length; i++) if (visit.Stock[i] != 0) lines++;
                writer.Write(lines);
                for (int i = 0; i < visit.Stock.Length; i++)
                {
                    if (visit.Stock[i] == 0) continue;
                    writer.Write(i);
                    writer.Write(visit.Stock[i]);
                }
            }
        }

        public void Load(SaveReader reader)
        {
            _visits.Clear();
            int layout = reader.ReadInt();
            if (layout != Layout)
                throw new SaveLoadException($"{SaveKey} layout {layout} is not one this build reads ({Layout}).");
            _nextId = reader.ReadInt();
            int visits = reader.ReadInt();
            for (int v = 0; v < visits; v++)
            {
                var visit = new Visit
                {
                    Id = reader.ReadInt(),
                    Pawn = reader.ReadInt(),
                    Kind = reader.ReadInt(),
                    ArrivedTick = reader.ReadInt(),
                    StayLeft = reader.ReadInt(),
                    Purse = reader.ReadInt(),
                    Negotiator = reader.ReadInt(),
                    Ready = reader.ReadBool(),
                    Session = reader.ReadInt(),
                    Stock = new int[_ctx.Content.Items.Length],
                };
                int lines = reader.ReadInt();
                for (int l = 0; l < lines; l++)
                {
                    int item = reader.ReadInt();
                    int count = reader.ReadInt();
                    if ((uint)item < (uint)visit.Stock.Length) visit.Stock[item] = count;
                }
                _visits.Add(visit);
            }
        }
    }

    /// <summary>
    /// Named random purposes for trading, beside <see cref="RaidPurpose"/> and for its reason: each
    /// draw its own stream. SHA-256's round constants K28 and K29, which nothing else used when
    /// they were taken (2026-09-26). Grep the constant before taking the next one.
    /// </summary>
    public static class TradePurpose
    {
        /// <summary>Which side of the board, and where on it, the trader walks on.</summary>
        public const uint Edge = 0xC6E0_0BF3;

        /// <summary>The trader's purse and stock. Mixed with the pawn's id for an adopted trader.</summary>
        public const uint Stock = 0xD5A7_9147;
    }
}
