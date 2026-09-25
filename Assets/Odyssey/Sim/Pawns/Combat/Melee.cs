#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The questions every part of the fight asks about two pawns (design 33 §6A): is this one
    /// dead, is it on its feet, can that one reach it with a blow, is it a threat to me. <b>Lane
    /// A's file.</b> One owner for each answer, because the attack driver, the resolver, the draft's
    /// hold and three think nodes all ask them, and four copies of "in reach" would be the fault
    /// <c>docs/bug-patterns.md</c> lists first.
    ///
    /// <para>Every answer is read off saved state — cells, hit points, flags, the job in hand —
    /// never off a path, which is not saved: a save taken mid-fight must answer every one of these
    /// the same way after the load.</para>
    /// </summary>
    public static class Melee
    {
        /// <summary>
        /// Past saving: at or below its species' death line. A pawn is dead from the blow that put
        /// it there, although it stays in the registry until the deferred removal at the end of the
        /// tick (design 33 §3) — so everything that picks a target or lands a blow asks this first.
        /// </summary>
        public static bool IsDead(Pawn pawn) => pawn.HpMilli <= pawn.DeathAtMilli;

        /// <summary>On its feet and alive: a pawn a hunt may choose and a blow may be aimed at.</summary>
        public static bool IsStanding(Pawn pawn) => !pawn.Downed && !IsDead(pawn);

        /// <summary>
        /// Can <paramref name="attacker"/> strike <paramref name="target"/> from where each stands?
        /// The same cell, or the next one along on the same layer by a step the attacker could take
        /// — orthogonal, or diagonal with neither corner blocked. So no blow passes through a wall's
        /// corner, and nobody is struck from the block above: a hop-adjacent target is reached by
        /// the chase, not swung at over the edge.
        /// </summary>
        public static bool InReach(PawnContext ctx, Pawn attacker, Pawn target, TraverseMode mode)
        {
            int a = attacker.Cell, b = target.Cell;
            if (a == b) return true;
            int stride = ctx.Size.LayerStride;
            if (a / stride != b / stride) return false;
            return ctx.Nav.IsLegalStep(a, b, mode);
        }

        /// <summary>Is <paramref name="other"/> in a melee job aimed at <paramref name="me"/> right now?</summary>
        public static bool IsAttacking(Pawn other, Pawn me) =>
            other.CombatTarget == me.Id.Value && other.CurrentJob != null
            && other.CurrentJob.DefIndex == JobIndex.AttackMelee;

        /// <summary>
        /// Something a colonist hits without being told to (design 33 §1): a standing hostile, or
        /// anybody standing who is attacking her — a hog that turned, a colonist who struck her.
        /// </summary>
        public static bool IsThreatTo(Pawn other, Pawn me) =>
            other != me && IsStanding(other) && (other.IsHostile || IsAttacking(other, me));

        /// <summary>
        /// The first threat within reach of <paramref name="me"/>, in id order, or null. <b>Scales
        /// with the pawns on the board</b>: one integer comparison each and a step test for the
        /// few that are threats. Asked by a colonist's self-defence at each think; a drafted
        /// colonist's hold asks <see cref="HoldTarget"/>, which answers this first in the same pass.
        /// </summary>
        public static Pawn? AdjacentThreat(PawnContext ctx, Pawn me)
        {
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (!IsThreatTo(other, me)) continue;
                if (InReach(ctx, me, other, TraverseMode.Colonist)) return other;
            }
            return null;
        }

        /// <summary>
        /// The colonist <paramref name="attacker"/> is fighting, when it is a bandit or an animal
        /// in a melee attack on one and on its feet; else null (design 33 §15). The one answer to
        /// "is a colonist being attacked, and by whom": the attacker's own job and target, which are
        /// what swings at her. A colonist attacking a colonist — the player's Ctrl order, or the
        /// blows she takes back — is not, so it summons nobody. A building attack names no pawn.
        /// </summary>
        public static Pawn? ColonistUnderAttackBy(PawnContext ctx, Pawn attacker)
        {
            if (attacker.CombatTarget == 0 || !IsInAnAttack(attacker) || attacker.IsColonist) return null;
            Pawn? victim = ctx.Pawns.Get(new PawnId(attacker.CombatTarget));
            return victim != null && victim.IsColonist && !IsDead(victim) ? victim : null;
        }

        /// <summary>
        /// What a drafted colonist on her hold fights (design 33 §2b, §15), in one pass over the
        /// pawns:
        /// <list type="number">
        /// <item><b>A threat in reach</b> (<see cref="IsThreatTo"/>, <see cref="InReach"/>) — the first
        /// in list order, exactly as <see cref="AdjacentThreat"/> answers it — and
        /// <paramref name="joining"/> is false: she strikes from where she stands.</item>
        /// <item>Else <b>the attacker of another colonist</b>, a bandit or an animal
        /// (<see cref="ColonistUnderAttackBy"/>), with the victim and the attacker both within
        /// <see cref="CombatDef.helpRadiusCells"/> of her (<see cref="WithinHelp"/>) and the attacker
        /// reachable in her own mode — and <paramref name="joining"/> is true: she goes to it. The
        /// nearest victim's attacker (squared distance in cells, as <see cref="ChooseSide"/>
        /// measures), a tie to the lower victim id, then the lower attacker id.</item>
        /// <item>Else null: she holds.</item>
        /// </list>
        /// <para><b>Scales with the pawns on the board</b>, and is the same one pass the hold's threat
        /// scan already was: for a pawn at peace an extra integer comparison (its target is
        /// nought), a lookup by id for each pawn in an attack, and a reachability test (two array
        /// reads) only for a candidate nearer than the best so far. Asked every tick by each drafted
        /// colonist on her hold and at each of her thinks — never by anybody undrafted, walking an
        /// order, or in a fight already — so it costs nothing while nobody is drafted.</para>
        /// <para><b>And by an undrafted colonist whose response is Defend</b> (design 33 §18d): the
        /// same rule, so she joins exactly the fights a drafted colonist on her hold would. Her
        /// per-tick notice asks it only while something hostile is about
        /// (<see cref="AnythingHostile"/>).</para>
        /// </summary>
        public static Pawn? HoldTarget(PawnContext ctx, Pawn me, out bool joining)
        {
            joining = false;
            int radius = ctx.Content.Combat.helpRadiusCells;
            GridSize size = ctx.Size;
            CellRef m = size.FromIndex(me.Cell);

            Pawn? best = null;
            int bestDistance = int.MaxValue, bestVictim = int.MaxValue;
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (IsThreatTo(other, me) && InReach(ctx, me, other, TraverseMode.Colonist)) return other;

                // Another colonist's fight: not one aimed at me, which the blow above answers once
                // it is in reach.
                if (other.CombatTarget == 0 || other.CombatTarget == me.Id.Value) continue;
                Pawn? victim = ColonistUnderAttackBy(ctx, other);
                if (victim == null || victim == me) continue;
                if (!WithinHelp(size, m, victim.Cell, radius) || !WithinHelp(size, m, other.Cell, radius)) continue;

                CellRef v = size.FromIndex(victim.Cell);
                int dx = v.X - m.X, dz = v.Z - m.Z, dy = v.Y - m.Y;
                int distance = dx * dx + dz * dz + dy * dy;
                if (distance > bestDistance) continue;
                if (distance == bestDistance
                    && (victim.Id.Value > bestVictim || (victim.Id.Value == bestVictim && other.Id.Value > best!.Id.Value))) continue;
                if (!ctx.CanTravel(me, other.Cell, TraverseMode.Colonist)) continue;

                best = other;
                bestDistance = distance;
                bestVictim = victim.Id.Value;
            }

            joining = best != null;
            return best;
        }

        /// <summary>
        /// The nearest danger to <paramref name="me"/>, for a colonist whose response is Flee (design
        /// 33 §18d), or null: a standing pawn within <see cref="CombatDef.helpRadiusCells"/> of her
        /// (<see cref="WithinHelp"/>, the one number for <i>near</i> in a fight) that is a bandit,
        /// whatever it is doing; an animal attacking a colonist (<see cref="ColonistUnderAttackBy"/>);
        /// or anybody attacking her. A wild animal at peace is not danger. <b>Only danger that can
        /// reach her in its own mode counts</b>, so a bandit behind a shut door (§16b) does not
        /// keep her off work. Nearest by squared distance in cells, a tie to the lower id.
        /// <para><b>Scales with the pawns on the board</b>: an integer comparison or two each, and a
        /// reachability test (two array reads) only for a candidate nearer than the best so far.</para>
        /// </summary>
        public static Pawn? DangerTo(PawnContext ctx, Pawn me)
        {
            int radius = ctx.Content.Combat.helpRadiusCells;
            GridSize size = ctx.Size;
            CellRef m = size.FromIndex(me.Cell);

            Pawn? best = null;
            int bestDistance = int.MaxValue;
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (other == me || !IsStanding(other)) continue;
                bool danger = other.IsHostile || IsAttacking(other, me)
                    || (!other.IsPerson && ColonistUnderAttackBy(ctx, other) != null);
                if (!danger || !WithinHelp(size, m, other.Cell, radius)) continue;

                CellRef o = size.FromIndex(other.Cell);
                int dx = o.X - m.X, dz = o.Z - m.Z, dy = o.Y - m.Y;
                int distance = dx * dx + dz * dz + dy * dy;
                if (distance >= bestDistance) continue;
                if (!ctx.CanTravel(other, me.Cell, other.OwnMode)) continue;
                best = other;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>
        /// Is anything hostile about (design 33 §18f): a standing bandit, or anybody in an attack
        /// on a colonist? The gate in front of the per-tick notice of a Defend or Flee colonist
        /// (<see cref="HostilityResponses.Notices"/>): with nothing hostile neither can act, so
        /// neither scans. Everything either would act on is one of these — a threat to her is a
        /// hostile or somebody attacking her, a fight to join is somebody attacking a colonist, and
        /// danger is one of the three. <b>Scales with the pawns on the board</b>, and is asked at
        /// most once a tick, by the first responder who needs it.
        /// </summary>
        public static bool AnythingHostile(PawnContext ctx)
        {
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (other.IsHostile && IsStanding(other)) return true;
                if (other.CombatTarget == 0 || !IsInAnAttack(other)) continue;
                Pawn? target = ctx.Pawns.Get(new PawnId(other.CombatTarget));
                if (target != null && target.IsColonist) return true;
            }
            return false;
        }

        /// <summary>
        /// Is <paramref name="cell"/> within <paramref name="radius"/> cells of <paramref name="m"/> on
        /// the ground (design 33 §15): Chebyshev across the layer, and the same layer or one either
        /// side, so a fight on the terrace step above or below counts and one three storeys down
        /// does not.
        /// </summary>
        static bool WithinHelp(GridSize size, CellRef m, int cell, int radius)
        {
            CellRef c = size.FromIndex(cell);
            return System.Math.Abs(c.X - m.X) <= radius && System.Math.Abs(c.Z - m.Z) <= radius
                && System.Math.Abs(c.Y - m.Y) <= 1;
        }

        /// <summary>
        /// Is <paramref name="pawn"/> in a melee attack — on anybody, or on a building — and on its
        /// feet? Only these hold a side (design 33 §7c). <b>A building attack counts</b> (§13e): its
        /// target is 0, and asking for a pawn target here let two ordered on one wall stand on one
        /// tile. The job, not the target, is the question: the rescue names its patient in the same
        /// field and is not an attack.
        /// </summary>
        public static bool IsInAnAttack(Pawn pawn) =>
            pawn.CurrentJob != null && pawn.CurrentJob.DefIndex == JobIndex.AttackMelee && IsStanding(pawn);

        /// <summary>
        /// The side an attacker holds (design 33 §7c): the cell she is walking to, else the one she
        /// stands on. Read off <see cref="Pawn.Destination"/> and <see cref="Pawn.Cell"/>, both
        /// saved, so a claim needs no state of its own and a load holds every claim it saved.
        /// </summary>
        public static int SideOf(Pawn pawn) => pawn.Destination >= 0 ? pawn.Destination : pawn.Cell;

        /// <summary>
        /// Does another pawn in a fight hold <paramref name="cell"/> (design 33 §7c, §8c)? A pawn
        /// in an attack holds its side (<see cref="SideOf"/>), and so does a pawn somebody is
        /// attacking — the cell it stands on or is walking to — whatever it is doing, standing or
        /// down. That is the one rule every fighter obeys: nobody in a fight stops on a cell
        /// another in a fight holds. <paramref name="me"/>'s own claims are not counted against
        /// her.
        ///
        /// <para><b>Scales with the pawns on the board</b>: one integer comparison each for those
        /// not fighting, and a lookup by id for each attacker's target. Asked when an attacker
        /// would stop on a cell in reach, before each swing, and by the orders that place a
        /// drafted colonist — never per tick by one standing waiting for her swing clock.</para>
        /// </summary>
        public static bool Holds(PawnContext ctx, Pawn me, int cell)
        {
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (other == me || !IsInAnAttack(other)) continue;
                if (SideOf(other) == cell) return true;
                if (other.CombatTarget == me.Id.Value) continue;
                Pawn? target = ctx.Pawns.Get(new PawnId(other.CombatTarget));
                if (target != null && SideOf(target) == cell) return true;
            }
            return false;
        }

        /// <summary>How far round a target a side is looked for: its eight neighbours, then the ring beyond.</summary>
        public const int SideRings = 2;

        /// <summary>
        /// Where <paramref name="me"/> should stand to fight <paramref name="target"/> (design 33
        /// §7c): of the cells beside it on its layer that she could strike it from and can reach,
        /// the one no other fighter holds (<see cref="Holds"/>, and where the target itself is
        /// walking to) and nearest her; all eight held, the nearest free one she can stand on and
        /// reach in the ring beyond, to wait there until a side frees up; and −1 when that ring is
        /// full too. Her own cell counts as free for her, so an attacker
        /// already beside the target keeps where she is.
        ///
        /// <para><b>Deterministic, and integer.</b> "Nearest" is the squared distance in cells from
        /// where she stands; a tie goes to the first in a fixed scan (−Z to +Z, then −X to +X).
        /// Who else holds a side is read in one pass over the pawns into a 5 × 5 mask round the
        /// target, so the claims are resolved in the order the pawns tick — by id — and never in a
        /// dictionary's.</para>
        ///
        /// <para><b>Scales with the pawns on the board</b> for the one pass (an integer comparison
        /// each for those not fighting), plus the 24 cells within two of the target — never the
        /// board. Asked when an attack starts, when its target has left the cell the side was
        /// chosen against (at the chase's own cadence), and once a chase-cadence by an attacker
        /// waiting a ring back.</para>
        /// </summary>
        public static int ChooseSide(PawnContext ctx, Pawn me, Pawn target, TraverseMode mode)
        {
            GridSize size = ctx.Size;
            int centre = target.Cell;
            CellRef t = size.FromIndex(centre);
            CellRef m = size.FromIndex(me.Cell);

            int taken = 0;
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (other == me || !IsInAnAttack(other)) continue;
                taken |= Bit(size, t, SideOf(other));

                // What another fight is aimed at holds its cell too (design 33 §8c): a side chosen
                // on it would stand two fighters on one tile. Not the target's own cell, which is
                // never a side, and not me.
                if (other.CombatTarget == me.Id.Value || other.CombatTarget == target.Id.Value) continue;
                Pawn? aimed = ctx.Pawns.Get(new PawnId(other.CombatTarget));
                if (aimed != null) taken |= Bit(size, t, SideOf(aimed));
            }

            // Nor where my own target is walking to: it will stand there.
            if (target.Destination >= 0) taken |= Bit(size, t, target.Destination);

            for (int ring = 1; ring <= SideRings; ring++)
            {
                int best = -1, bestDistance = int.MaxValue;
                for (int dz = -ring; dz <= ring; dz++)
                for (int dx = -ring; dx <= ring; dx++)
                {
                    if (System.Math.Abs(dx) != ring && System.Math.Abs(dz) != ring) continue;
                    if ((taken & (1 << ((dz + SideRings) * 5 + dx + SideRings))) != 0) continue;
                    int x = t.X + dx, z = t.Z + dz;
                    if (!size.Contains(x, z, t.Y)) continue;
                    int cell = size.Index(x, z, t.Y);

                    // Beside it: a cell she could strike it from, which is Melee.InReach's own test.
                    // A ring back: anywhere she can stand.
                    if (ring == 1 ? !ctx.Nav.IsLegalStep(cell, centre, mode) : !ctx.Nav.Grid.CanEnter(cell, mode)) continue;
                    if (!ctx.CanTravel(me, cell, mode)) continue;

                    int ex = x - m.X, ez = z - m.Z, ey = t.Y - m.Y;
                    int distance = ex * ex + ez * ez + ey * ey;
                    if (distance >= bestDistance) continue;
                    best = cell;
                    bestDistance = distance;
                }
                if (best >= 0) return best;
            }
            return -1;
        }

        /// <summary>The bit for <paramref name="cell"/> in the 5 × 5 mask round <paramref name="t"/>, or nought outside it.</summary>
        static int Bit(GridSize size, CellRef t, int cell)
        {
            if (cell < 0) return 0;
            CellRef s = size.FromIndex(cell);
            int dx = s.X - t.X, dz = s.Z - t.Z;
            if (s.Y != t.Y || dx < -SideRings || dx > SideRings || dz < -SideRings || dz > SideRings) return 0;
            return 1 << ((dz + SideRings) * 5 + dx + SideRings);
        }

        /// <summary>
        /// Which way a body falls, away from the blow: one of eight headings, 0 is +Z and clockwise
        /// from above (<see cref="Corpse.Facing"/>). Nought when there is nobody to fall away from.
        /// </summary>
        public static byte FallFacing(GridSize size, int from, int at)
        {
            if (from < 0 || from == at) return 0;
            CellRef a = size.FromIndex(from), b = size.FromIndex(at);
            int dx = System.Math.Sign(b.X - a.X), dz = System.Math.Sign(b.Z - a.Z);
            if (dx == 0 && dz == 0) return 0;
            // 0 +Z, 1 +Z+X, 2 +X, 3 -Z+X, 4 -Z, 5 -Z-X, 6 -X, 7 +Z-X.
            if (dx == 0) return (byte)(dz > 0 ? 0 : 4);
            if (dz == 0) return (byte)(dx > 0 ? 2 : 6);
            if (dx > 0) return (byte)(dz > 0 ? 1 : 3);
            return (byte)(dz > 0 ? 7 : 5);
        }
    }
}
