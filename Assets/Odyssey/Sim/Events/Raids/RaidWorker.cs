#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Pawns.Wildlife;

namespace Odyssey.Sim.Events
{
    /// <summary>
    /// A raid's own parameters (design 55 §8), the nested block design 23 §8 recommended the day
    /// a second worker wanted fields the first did not: <c>&lt;raid&gt;…&lt;/raid&gt;</c> on an
    /// <see cref="IncidentDef"/>. Every number but the owner's is INVENTED and a playtest number.
    /// </summary>
    public sealed class RaidParams
    {
        // ---- the size, when the caller leaves it to the incident (design 55 §9) ---------------

        /// <summary>Raiders per standing colonist.</summary>
        public int perColonist = 1;

        /// <summary>One more raider for every this many days survived. 0 for none.</summary>
        public int daysPerExtra = 5;

        public int minSize = 1;

        /// <summary>The largest band the incident picks for itself. A caller may ask for more.</summary>
        public int maxAutoSize = 30;

        // ---- the arrival (§4) ------------------------------------------------------------------

        /// <summary>Over how many ticks the band walks on: the whole of it, whatever its size.</summary>
        public int arrivalTicks = 300;

        /// <summary>How far in from the edge the gather point is, in cells.</summary>
        public int gatherInset = 12;

        /// <summary>How far from its point a member mills, in cells.</summary>
        public int gatherRadius = 5;

        // ---- the build-up (§3; the owner's numbers) --------------------------------------------

        public int loiterHoursMin = 2;

        public int loiterHoursMax = 4;

        /// <summary>How far from the gather point to the target the probe point is, per mille.</summary>
        public int probePerMille = 500;

        public int probeHours = 1;

        /// <summary>A standing colonist this near a staging member, in cells, and the band attacks at once.</summary>
        public int earlyTriggerCells = 15;

        // ---- the end (§6) ----------------------------------------------------------------------

        /// <summary>The share of the band down or dead, per mille, at which the rest withdraw.</summary>
        public int retreatPerMille = 500;

        /// <summary>The mix a caller that names none gets, by <c>RaidMixDef</c> defName.</summary>
        public string mix = "RaidMix_Mixed";
    }

    /// <summary>How big a raid is when the caller leaves it to the incident (design 55 §9).</summary>
    public static class RaidBudget
    {
        /// <summary>
        /// <c>clamp(perColonist × standing colonists + day ÷ daysPerExtra, minSize, maxAutoSize)</c>.
        /// The owner's headcount and days, because nothing in the game has a value to total yet; a
        /// wealth measure replaces this function and nothing else.
        /// </summary>
        public static int AutoSize(int standingColonists, int tick, RaidParams p)
        {
            int day = tick / Calendar.TicksPerDay;
            int size = p.perColonist * standingColonists + (p.daysPerExtra > 0 ? day / p.daysPerExtra : 0);
            return Math.Max(p.minSize, Math.Min(p.maxAutoSize, size));
        }

        /// <summary>Colonists on their feet, which is who a raid is sized against.</summary>
        public static int StandingColonists(PawnContext ctx)
        {
            int n = 0;
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
                if (pawns[i].IsColonist && Melee.IsStanding(pawns[i])) n++;
            return n;
        }
    }

    /// <summary>
    /// A raid (design 55): a band of hostiles, made from a mix, that walks on along one edge,
    /// gathers, probes, assaults the hearth and withdraws at half. This worker decides everything
    /// at the moment it fires and hands it to <see cref="RaidSystem"/>, which only runs the clock.
    ///
    /// <para><b>Four draws, four streams</b> (<see cref="RaidPurpose"/>), each mixed with the tick
    /// by <see cref="IncidentContext.Random"/>: the edge, the order the kinds walk on in, the
    /// loiter, and — per member, per think — the mill. A raid fired on another tick comes from
    /// another edge; the same raid fired on the same tick of the same world is the same raid.</para>
    ///
    /// <para><b>Refused, not trimmed</b> (design 55 §9): a band that would take the board past
    /// <see cref="PawnRegistry.PawnCeiling"/> does not fire, and the debug row says so.</para>
    /// </summary>
    public sealed class RaidWorker : IncidentWorker
    {
        /// <summary>How many rings out from a wanted point to look for a cell of the census.</summary>
        public const int NearestRings = 8;

        public override string Name => "Raid";

        public override void Validate(IncidentDef def, PawnContent pawns)
        {
            RaidParams? p = def.raid;
            if (p == null)
                throw new DefLoadException($"{def.Origin}: incident '{def.defName}' is a raid with no <raid> block.");
            void Require(bool ok, string what)
            {
                if (!ok) throw new DefLoadException($"{def.Origin}: incident '{def.defName}' has {what}.");
            }
            Require(p.perColonist >= 0 && p.daysPerExtra >= 0, "a negative size rule");
            Require(p.minSize >= 1 && p.maxAutoSize >= p.minSize, $"auto size {p.minSize}–{p.maxAutoSize}");
            Require(p.arrivalTicks >= 0, $"arrivalTicks {p.arrivalTicks}");
            Require(p.gatherInset >= 0 && p.gatherRadius >= 1, "a gather point that is not one");
            Require(p.loiterHoursMin >= 0 && p.loiterHoursMax >= p.loiterHoursMin, $"loiter {p.loiterHoursMin}–{p.loiterHoursMax} hours");
            Require(p.probePerMille >= 0 && p.probePerMille <= 1000 && p.probeHours >= 0, "a probe out of range");
            Require(p.earlyTriggerCells >= 0, $"earlyTriggerCells {p.earlyTriggerCells}");
            Require(p.retreatPerMille >= 1 && p.retreatPerMille <= 1000, $"retreatPerMille {p.retreatPerMille}");
            Require(p.mix.Length > 0, "no mix");
        }

        public override bool CanFireNow(IncidentContext ctx, in IncidentParms parms)
        {
            RaidParams? p = ctx.Content.Defs[parms.Def].raid;
            if (p == null || ctx.Pawns.Raids == null) return false;
            if (Origin(ctx.Pawns) < 0) return false;
            return Fits(ctx.Pawns, SizeFor(ctx, parms, p));
        }

        public override bool TryExecute(IncidentContext ctx, in IncidentParms parms)
        {
            PawnContext pawns = ctx.Pawns;
            RaidSystem? raids = pawns.Raids;
            RaidParams? p = ctx.Content.Defs[parms.Def].raid;
            if (p == null || raids == null) return false;

            int mixIndex = parms.Mix >= 0 ? parms.Mix : ctx.Content.MixIndex(p.mix);
            if (mixIndex < 0 || mixIndex >= ctx.Content.Mixes.Length) return false;
            int size = SizeFor(ctx, parms, p);
            if (size <= 0 || !Fits(pawns, size)) return false;

            int origin = Origin(pawns);
            if (origin < 0) return false;
            GridSize grid = ctx.Size;
            SurfaceCensus census = SurfaceCensus.Take(ctx.Cells, pawns.Nav, null, grid.FromIndex(origin), 0, TraverseMode.Bandit);
            if (census.Edge.Count == 0) return false;

            // The edge: a side with any reachable cell, then a centre on it, then the side's cells
            // nearest that centre as the band's slots (EdgeArrival, shared with the trader).
            if (!EdgeArrival.TryPick(ctx, census, RaidPurpose.Edge, out int side, out int centre, out List<int> slots))
                return false;

            int target = RaidTargets.Resolve(pawns, origin);
            int gather = GatherPoint(ctx, census, side, centre, p.gatherInset);
            int probe = Between(ctx, census, gather, target, p.probePerMille);

            // Who: the mix's exact split, walked on in an order drawn once.
            RaidMix mix = ctx.Content.Mixes[mixIndex];
            var counts = new int[mix.Kinds.Length];
            mix.Compose(size, counts);
            var kinds = new int[size];
            int k = 0;
            for (int row = 0; row < counts.Length; row++)
                for (int n = 0; n < counts[row]; n++) kinds[k++] = mix.Kinds[row];
            var slotDraw = ctx.Random(RaidPurpose.Slots);
            for (int i = size - 1; i > 0; i--)
            {
                int j = slotDraw.NextInt(i + 1);
                (kinds[i], kinds[j]) = (kinds[j], kinds[i]);
            }

            var arrivals = new List<RaidArrival>(size);
            for (int i = 0; i < size; i++)
            {
                int at = ctx.Tick + 1 + (int)((long)i * p.arrivalTicks / size);
                arrivals.Add(new RaidArrival(at, slots[i % slots.Count], kinds[i]));
            }

            var loiterDraw = ctx.Random(RaidPurpose.Loiter);
            int loiterMin = p.loiterHoursMin * Calendar.TicksPerHour;
            int loiterMax = p.loiterHoursMax * Calendar.TicksPerHour;
            int loiter = loiterMin + loiterDraw.NextInt(loiterMax - loiterMin + 1);

            raids.Begin(parms.Def, mixIndex, ctx.Tick, gather, probe, target, loiter, p.probeHours * Calendar.TicksPerHour,
                p.gatherRadius, p.earlyTriggerCells, p.retreatPerMille, arrivals);

            // The Events row: the mix and the size ride in the entry's detail (design 55 §7).
            ctx.Ledger.Record(parms.Def, gather, ctx.Tick, mixIndex, size);
            return true;
        }

        /// <summary>The caller's size, or the incident's own when the caller named none.</summary>
        static int SizeFor(IncidentContext ctx, in IncidentParms parms, RaidParams p) =>
            SizeFor(ctx.Pawns, p, parms.Points, ctx.Tick);

        /// <summary>Would a band of <paramref name="size"/> fit under the pawn ceiling, with every raid still arriving?</summary>
        public static bool Fits(PawnContext pawns, int size) => size <= Room(pawns);

        /// <summary>
        /// How many more pawns the board can take under <see cref="PawnRegistry.PawnCeiling"/>, the
        /// members of every raid still walking on counted as already here. The one owner of that
        /// sum: the debug row reads it to say why a raid was refused (design 55 §9).
        /// </summary>
        public static int Room(PawnContext pawns) =>
            PawnRegistry.PawnCeiling - pawns.Pawns.Count - (pawns.Raids?.PendingArrivals ?? 0);

        /// <summary>How many a raid of this incident would bring: the caller's size, or its own at 0.</summary>
        public static int SizeFor(PawnContext pawns, RaidParams p, int size, int tick) =>
            size > 0 ? size : RaidBudget.AutoSize(RaidBudget.StandingColonists(pawns), tick, p);

        /// <summary>
        /// Where the census is taken from and the fallback target: the colony's start, else the
        /// first standing colonist, else -1 — a board with no colony has nobody to raid.
        /// </summary>
        internal static int Origin(PawnContext pawns)
        {
            GridSize size = pawns.Size;
            if (pawns.ColonyStart is CellRef start && size.Contains(start))
            {
                int cell = pawns.Cells.NearestWalkableInColumn(start.X, start.Z, start.Y);
                if (cell >= 0) return cell;
            }
            var all = pawns.Pawns.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].IsColonist && Melee.IsStanding(all[i])) return all[i].Cell;
            return -1;
        }

        /// <summary>The census cell nearest the point <paramref name="inset"/> cells straight in from the edge centre.</summary>
        static int GatherPoint(IncidentContext ctx, SurfaceCensus census, int side, int centre, int inset)
        {
            GridSize size = ctx.Size;
            CellRef c = size.FromIndex(centre);
            int x = c.X, z = c.Z;
            switch (side)
            {
                case 0: x = Math.Min(size.SizeX - 1, inset); break;
                case 1: x = Math.Max(0, size.SizeX - 1 - inset); break;
                case 2: z = Math.Min(size.SizeZ - 1, inset); break;
                default: z = Math.Max(0, size.SizeZ - 1 - inset); break;
            }
            int found = NearestInCensus(ctx, census, x, z);
            return found >= 0 ? found : centre;
        }

        /// <summary>The census cell nearest the point <paramref name="perMille"/> of the way from one cell to another.</summary>
        static int Between(IncidentContext ctx, SurfaceCensus census, int from, int to, int perMille)
        {
            GridSize size = ctx.Size;
            CellRef a = size.FromIndex(from), b = size.FromIndex(to);
            int x = a.X + (b.X - a.X) * perMille / 1000;
            int z = a.Z + (b.Z - a.Z) * perMille / 1000;
            int found = NearestInCensus(ctx, census, x, z);
            return found >= 0 ? found : from;
        }

        static int NearestInCensus(IncidentContext ctx, SurfaceCensus census, int cx, int cz)
        {
            GridSize size = ctx.Size;
            for (int ring = 0; ring <= NearestRings; ring++)
            for (int dz = -ring; dz <= ring; dz++)
            for (int dx = -ring; dx <= ring; dx++)
            {
                if (Math.Abs(dx) != ring && Math.Abs(dz) != ring) continue;
                int x = cx + dx, z = cz + dz;
                if (x < 0 || z < 0 || x >= size.SizeX || z >= size.SizeZ) continue;
                int cell = SurfaceCensus.Topmost(ctx.Cells, x, z);
                if (cell >= 0 && census.Contains(cell)) return cell;
            }
            return -1;
        }
    }
}
