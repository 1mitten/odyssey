#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// What lane A's combat tests share (design 33 §6A): a bare board, a way to stand a pawn on a
    /// named cell, a tape of the published fight, a weapon in a hand without lane D's equip job,
    /// and melee rules that record every swing they decide. Test code only.
    /// </summary>
    static class CombatFixture
    {
        public static readonly GridSize Size = new GridSize(60, 60, 16);

        /// <param name="beds">A bed each unless a test says otherwise: a marauder with nobody standing
        /// attacks the colony's buildings (design 33 §14b), so a test of it idling has none.</param>
        public static ColonyWorld Board(int colonists = 2, uint seed = 7u, int beds = -1)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = beds < 0 ? colonists : beds;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: false);
        }

        public static IntentRejection Send(ColonyWorld colony, Intent intent)
        {
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(intent);
            colony.World.Tick();
            var rejected = colony.World.Intents.Rejected;
            return rejected.Count == 0 ? IntentRejection.None : rejected[0].Reason;
        }

        public static IntentRejection Draft(ColonyWorld colony, Pawn pawn, bool on = true) =>
            Send(colony, new Intent(IntentKind.SetDrafted, default, pawn.Id.Value, on ? 1 : 0));

        public static IntentRejection Attack(ColonyWorld colony, Pawn attacker, Pawn target) =>
            Send(colony, new Intent(IntentKind.OrderAttack, Size.FromIndex(target.Cell), attacker.Id.Value, target.Id.Value));

        /// <summary>The standable cell <paramref name="dx"/>, <paramref name="dz"/> from the colony's start.</summary>
        public static int Near(ColonyWorld colony, int dx, int dz)
        {
            CellRef at = colony.Start;
            return colony.Pawns.Cells.NearestWalkableInColumn(at.X + dx, at.Z + dz, at.Y);
        }

        /// <summary>Put a pawn on a cell with nothing in hand, as if it had walked there and stopped.</summary>
        public static void Stand(ColonyWorld colony, Pawn pawn, int cell)
        {
            colony.Jobs.EndJob(pawn, JobStatus.Failed);
            pawn.ClearPath();
            pawn.Destination = -1;
            pawn.Cell = cell;
        }

        /// <summary>A pawn of a kind on a cell, after the first tick so the world is running.</summary>
        public static Pawn Spawn(ColonyWorld colony, int kind, int cell) => colony.Pawns.Pawns.Spawn(cell, kind);

        /// <summary>Set a person's melee level exactly.</summary>
        public static void SetMelee(Pawn pawn, int level) =>
            pawn.Skills[SkillIndex.Melee] = pawn.Content.Skills[SkillIndex.Melee].ExperienceForLevel(level);

        public static Armament Fists(PawnContext ctx) => new Armament(ctx.Content.Combat.fists);

        public static Armament Weapon(PawnContext ctx, int item) => new Armament(ctx.Content.Items[item].weapon!, item);

        /// <summary>A blow that lands for exactly this much, with no stun.</summary>
        public static SwingOutcome Blow(int damageMilli) => new SwingOutcome(CombatEventKind.Hit, damageMilli);

        /// <summary>Land an exact blow now, through the one method, on the world's current tick.</summary>
        public static void Strike(ColonyWorld colony, Pawn attacker, Pawn target, int damageMilli) =>
            colony.Pawns.Combat!.ApplySwing(attacker, target, Fists(colony.Pawns), Blow(damageMilli), colony.World.CurrentTick);

        /// <summary>
        /// The fight as published: every <see cref="CombatEventView"/> the snapshot carried, once,
        /// read after each tick by id so nothing the ring drops between reads is missed at the rates
        /// these tests fight at.
        /// </summary>
        public sealed class Tape
        {
            public readonly List<CombatEventView> Events = new List<CombatEventView>();
            int _seen;

            public void Read(ColonyWorld colony)
            {
                var events = colony.World.Views.Current.CombatEvents;
                for (int i = 0; i < events.Length; i++)
                {
                    if (events[i].Id <= _seen) continue;
                    Events.Add(events[i]);
                    _seen = events[i].Id;
                }
            }

            public void Tick(ColonyWorld colony, int ticks)
            {
                for (int t = 0; t < ticks; t++)
                {
                    colony.World.Tick();
                    Read(colony);
                }
            }

            public List<CombatEventView> By(Pawn attacker, CombatEventKind kind) =>
                Events.FindAll(e => e.Attacker == attacker.Id && e.Kind == kind);

            public List<CombatEventView> Of(CombatEventKind kind) => Events.FindAll(e => e.Kind == kind);

            /// <summary>
            /// The ticks <paramref name="attacker"/>'s blows reached their impact — a Hit, a Miss or a
            /// Dodge. Since design 33 §9g a swing is decided when its wind-up begins, so the rules
            /// below see its first tick; this is its last.
            /// </summary>
            public List<int> Landed(Pawn attacker) =>
                Events.FindAll(e => e.Attacker == attacker.Id
                        && (e.Kind == CombatEventKind.Hit || e.Kind == CombatEventKind.Miss || e.Kind == CombatEventKind.Dodge))
                    .ConvertAll(e => e.Tick);

            /// <summary>Every swing <paramref name="attacker"/> began, critical or not (design 33 §9g).</summary>
            public List<CombatEventView> Swings(Pawn attacker) =>
                Events.FindAll(e => e.Attacker == attacker.Id
                    && (e.Kind == CombatEventKind.Swing || e.Kind == CombatEventKind.SwingCritical));
        }

        /// <summary>
        /// The shipped rules, writing down every swing they decide: when, who, and what came of it.
        /// Since design 33 §9g a swing is decided on the tick its wind-up <b>begins</b>, so these are
        /// swing starts; <see cref="Tape.Landed"/> has the impacts.
        /// </summary>
        public sealed class RecordingRules : MeleeRules
        {
            public readonly List<(int Tick, int Attacker, int Target, CombatEventKind Result)> Swings =
                new List<(int, int, int, CombatEventKind)>();

            public override SwingOutcome Resolve(Pawn attacker, Pawn defender, in Armament armament, PawnContext ctx, int tick)
            {
                SwingOutcome outcome = base.Resolve(attacker, defender, armament, ctx, tick);
                Swings.Add((tick, attacker.Id.Value, defender.Id.Value, outcome.Result));
                return outcome;
            }

            public List<int> TicksOf(Pawn attacker) =>
                Swings.FindAll(s => s.Attacker == attacker.Id.Value).ConvertAll(s => s.Tick);
        }

        /// <summary>The shipped rules at a melee level the test names, per pawn, instead of the skill's.</summary>
        public sealed class FixedLevelRules : MeleeRules
        {
            public readonly Dictionary<int, int> Levels = new Dictionary<int, int>();

            public override int MeleeLevel(Pawn pawn) =>
                Levels.TryGetValue(pawn.Id.Value, out int level) ? level : base.MeleeLevel(pawn);
        }

        /// <summary>The shipped weapon lookup, with one weapon put in named hands — lane D's equip, without lane D.</summary>
        public sealed class HeldWeapon : WeaponRules
        {
            readonly Dictionary<int, int> _held = new Dictionary<int, int>();

            public HeldWeapon Give(Pawn pawn, int item)
            {
                _held[pawn.Id.Value] = item;
                return this;
            }

            public override Armament ArmamentOf(Pawn pawn, PawnContext ctx) =>
                _held.TryGetValue(pawn.Id.Value, out int item)
                    ? new Armament(ctx.Content.Items[item].weapon!, item)
                    : base.ArmamentOf(pawn, ctx);
        }

        /// <summary>Counts each hook, in the order heard.</summary>
        public sealed class HookCounter : ICombatListener
        {
            public readonly List<string> Heard = new List<string>();
            public int SwingCount, DamageCount, DownedCount, DiedCount, LastCorpse;

            public void SwingResolved(in SwingReport report)
            {
                SwingCount++;
                Heard.Add("swing:" + report.Target.Id.Value);
            }

            public void DamageApplied(in DamageReport report)
            {
                DamageCount++;
                Heard.Add("damage:" + report.Target.Id.Value);
            }

            public void Downed(Pawn pawn, Pawn? by, int tick)
            {
                DownedCount++;
                Heard.Add("downed:" + pawn.Id.Value);
            }

            public void Died(Pawn pawn, Pawn? by, int corpseId, int tick)
            {
                DiedCount++;
                LastCorpse = corpseId;
                Heard.Add("died:" + pawn.Id.Value);
            }
        }
    }
}
