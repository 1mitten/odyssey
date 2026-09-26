#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Events;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim.Events
{
    /// <summary>
    /// The raid (design 55): a band that walks on along one edge over seconds, gathers, probes and
    /// assaults on its clock, attacks early when poked, withdraws at half, and survives a save at
    /// every point on the way. Each rule is paired with the control that fails it when the rule is
    /// withheld.
    /// </summary>
    public class RaidTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        const int Mixed = 2;

        static ColonyWorld Board(uint seed = 7u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 3;
            ColonyWorld colony = ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: false);
            colony.World.Tick(30);
            return colony;
        }

        static IntentRejection Fire(ColonyWorld colony, int size, int mix = Mixed)
        {
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(new Intent(IntentKind.InvokeIncident, default, IncidentHandle.Raid, size, mix + 1));
            colony.World.Tick();
            var rejected = colony.World.Intents.Rejected;
            return rejected.Count == 0 ? IntentRejection.None : rejected[0].Reason;
        }

        static RaidGroup Raid(ColonyWorld colony) => colony.Pawns.Raids!.Groups.Single();

        static ulong Hash(ColonyWorld colony) => colony.World.ComputeStateHash().Value;

        static IEnumerable<Pawn> Members(ColonyWorld colony, RaidGroup group)
        {
            foreach (RaidMember member in group.Members)
            {
                Pawn? pawn = colony.Pawns.Pawns.Get(new PawnId(member.Pawn));
                if (pawn != null) yield return pawn;
            }
        }

        static int Side(CellRef at) =>
            at.X == 0 ? 0 : at.X == Size.SizeX - 1 ? 1 : at.Z == 0 ? 2 : at.Z == Size.SizeZ - 1 ? 3 : -1;

        static void TickUntil(ColonyWorld colony, Func<bool> done, int limit, string what)
        {
            for (int i = 0; i < limit && !done(); i++) colony.World.Tick();
            Assert.That(done(), Is.True, what);
        }

        // ---- the arrival ------------------------------------------------------------------------

        /// <summary>
        /// Every member's slot is on one side of the board, and they walk on over the arrival window
        /// rather than on one tick: the band is scheduled whole at the fire, and the system spawns it
        /// as it falls due. Ten in the Mixed band are seven bandits and three gunmen.
        /// </summary>
        [Test]
        public void ABandWalksOnAlongOneEdgeOverSeconds()
        {
            ColonyWorld colony = Board();
            int fired = colony.World.CurrentTick;
            Assert.That(Fire(colony, 10), Is.EqualTo(IntentRejection.None));

            RaidGroup group = Raid(colony);
            var arrivals = group.Members.Count + group.Pending.Count;
            Assert.That(arrivals, Is.EqualTo(10));
            Assert.That(group.StartingSize, Is.EqualTo(10));
            var sides = group.Pending.Select(a => Side(Size.FromIndex(a.Cell))).Distinct().ToList();
            Assert.That(sides, Has.Count.EqualTo(1), "slots on more than one side");
            Assert.That(sides[0], Is.GreaterThanOrEqualTo(0), "a slot off the edge");

            // The control: not all on one tick.
            Assert.That(group.Pending.Select(a => a.Tick).Distinct().Count(), Is.GreaterThan(1), "the whole band arrives at once");
            Assert.That(group.Pending.Max(a => a.Tick) - fired, Is.LessThanOrEqualTo(300 + 2));

            colony.World.Tick(5);
            Assert.That(group.Members.Count, Is.InRange(1, 9), "five ticks in, some and not all have arrived");

            colony.World.Tick(310);
            Assert.That(group.Pending, Is.Empty);
            Assert.That(group.Members, Has.Count.EqualTo(10));
            var kinds = Members(colony, group).Select(p => p.Kind).ToList();
            Assert.That(kinds.Count(k => k == PawnKindIndex.Bandit), Is.EqualTo(7));
            Assert.That(kinds.Count(k => k == PawnKindIndex.Gunman), Is.EqualTo(3));
            Assert.That(Members(colony, group).All(p => p.IsHostile), Is.True);
        }

        /// <summary>
        /// The loiter is drawn from the owner's two to four hours, and a raid fired on another tick
        /// comes from another edge: the draws are keyed on the tick, so twelve firings on twelve ticks
        /// of one world do not all land on one side or loiter one length.
        /// </summary>
        [Test]
        public void TheLoiterIsTwoToFourHoursAndAnotherTickIsAnotherEdge()
        {
            var sides = new HashSet<int>();
            var loiters = new HashSet<int>();
            for (int attempt = 0; attempt < 12; attempt++)
            {
                ColonyWorld colony = Board();
                colony.World.Tick(attempt * 37);
                Assert.That(Fire(colony, 3), Is.EqualTo(IntentRejection.None));
                RaidGroup group = Raid(colony);
                Assert.That(group.LoiterTicks, Is.InRange(2 * Calendar.TicksPerHour, 4 * Calendar.TicksPerHour));
                Assert.That(group.ProbeTicks, Is.EqualTo(Calendar.TicksPerHour));
                sides.Add(Side(Size.FromIndex(group.Pending.Count > 0 ? group.Pending[0].Cell : colony.Pawns.Pawns.Get(new PawnId(group.Members[0].Pawn))!.Cell)));
                loiters.Add(group.LoiterTicks);
            }
            Assert.That(sides.Count, Is.GreaterThan(1), "every raid came from one side");
            Assert.That(loiters.Count, Is.GreaterThan(1), "every raid loitered as long");
        }

        // ---- the phases -------------------------------------------------------------------------

        /// <summary>
        /// The clock (design 55 §3), with the loiter and the probe cut short and the early trigger
        /// taken away so only the clock can move the band: arriving until the last member is on,
        /// gathering for the loiter, probing for the probe, then the assault. Nobody in the band holds
        /// a fight while it stages, and the gathered band stands round its point.
        /// </summary>
        [Test]
        public void ABandStagesThenAssaultsOnItsClock()
        {
            ColonyWorld colony = Board();
            Assert.That(Fire(colony, 8), Is.EqualTo(IntentRejection.None));
            RaidGroup group = Raid(colony);
            group.LoiterTicks = 1_500;
            group.ProbeTicks = 900;
            group.EarlyTriggerCells = -1;

            TickUntil(colony, () => group.Phase == RaidPhase.Gathering, 400, "the band never finished arriving");
            int gathered = group.PhaseTick;

            while (group.Phase == RaidPhase.Gathering && colony.World.CurrentTick < gathered + 3_000)
            {
                colony.World.Tick();
                Assert.That(Members(colony, group).Any(p => p.CombatTarget != 0), Is.False, "a staging member took a fight");
            }
            Assert.That(group.Phase, Is.EqualTo(RaidPhase.Probing));
            Assert.That(group.PhaseTick - gathered, Is.InRange(1_500, 1_500 + RaidSystem.CheckTicks));

            int probing = group.PhaseTick;
            int near = Members(colony, group).Count(p => RaidThinkNode.Cells(Size, p.Cell, group.GatherCell) <= group.MillRadius + 2);
            Assert.That(near * 2, Is.GreaterThanOrEqualTo(group.StartingSize), "the gathered band was not round its point");

            TickUntil(colony, () => group.Phase != RaidPhase.Probing, 2_000, "the probe never ended");
            Assert.That(group.Phase, Is.EqualTo(RaidPhase.Assaulting));
            Assert.That(group.PhaseTick - probing, Is.InRange(900, 900 + RaidSystem.CheckTicks));

            // The assault closes on the colony: some member is fighting, or has reached the target.
            TickUntil(colony,
                () => Members(colony, group).Any(p => p.CombatTarget != 0
                    || RaidThinkNode.Cells(Size, p.Cell, group.TargetCell) <= RaidThinkNode.ArriveCells),
                6_000, "the assault never reached the colony");
            Assert.That(colony.Pawns.Pawns.All.Any(p => p.IsColonist && p.Drafted), Is.False, "a raid drafted a colonist");
        }

        /// <summary>
        /// A standing colonist within the band's reach starts the assault at the next look, and one
        /// a cell beyond it does not (the control): the reach is set, tick by tick, to exactly the
        /// nearest colonist's distance, or one short of it.
        /// </summary>
        [Test]
        public void AColonistWithinReachStartsTheAssaultAndOneBeyondDoesNot()
        {
            ColonyWorld colony = Board();
            Assert.That(Fire(colony, 6), Is.EqualTo(IntentRejection.None));
            RaidGroup group = Raid(colony);
            group.EarlyTriggerCells = -1;
            TickUntil(colony, () => group.Phase == RaidPhase.Gathering, 400, "the band never finished arriving");

            for (int i = 0; i < 3 * RaidSystem.CheckTicks; i++)
            {
                group.EarlyTriggerCells = Nearest(colony, group) - 1;
                colony.World.Tick();
                Assert.That(group.Phase, Is.EqualTo(RaidPhase.Gathering), "a colonist beyond reach started the assault");
            }

            for (int i = 0; i < 2 * RaidSystem.CheckTicks && group.Phase == RaidPhase.Gathering; i++)
            {
                group.EarlyTriggerCells = Nearest(colony, group);
                colony.World.Tick();
            }
            Assert.That(group.Phase, Is.EqualTo(RaidPhase.Assaulting), "a colonist within reach did not");
        }

        static int Nearest(ColonyWorld colony, RaidGroup group)
        {
            int best = int.MaxValue;
            foreach (Pawn member in Members(colony, group))
            foreach (Pawn colonist in colony.Pawns.Pawns.All.Where(p => p.IsColonist))
                best = Math.Min(best, RaidThinkNode.Cells(Size, member.Cell, colonist.Cell));
            return best;
        }

        /// <summary>A member struck while the band stages starts the assault; the untouched band beside it is the control.</summary>
        [Test]
        public void AMemberStruckWhileStagingStartsTheAssault()
        {
            ColonyWorld untouched = Board();
            ColonyWorld struck = Board();
            foreach (ColonyWorld colony in new[] { untouched, struck })
            {
                Assert.That(Fire(colony, 6), Is.EqualTo(IntentRejection.None));
                Raid(colony).EarlyTriggerCells = -1;
                TickUntil(colony, () => Raid(colony).Phase == RaidPhase.Gathering, 400, "the band never finished arriving");
            }

            Pawn colonist = struck.Pawns.Pawns.All.First(p => p.IsColonist);
            Pawn member = Members(struck, Raid(struck)).First();
            member.RetaliateAgainst = colonist.Id.Value;
            member.RetaliateUntilTick = struck.World.CurrentTick + 1_000;

            untouched.World.Tick(RaidSystem.CheckTicks + 1);
            struck.World.Tick(RaidSystem.CheckTicks + 1);
            Assert.That(Raid(untouched).Phase, Is.EqualTo(RaidPhase.Gathering));
            Assert.That(Raid(struck).Phase, Is.EqualTo(RaidPhase.Assaulting));
        }

        // ---- the end ----------------------------------------------------------------------------

        /// <summary>
        /// Four of ten down is not enough (the control); five is, and the rest walk off the board.
        /// The downed stay, the band ends, and nobody's leaving writes a "Bandit left" row.
        /// </summary>
        [Test]
        public void HalfTheBandDownSendsTheRestAway()
        {
            ColonyWorld colony = Board();
            Assert.That(Fire(colony, 10), Is.EqualTo(IntentRejection.None));
            RaidGroup group = Raid(colony);
            group.EarlyTriggerCells = -1;
            TickUntil(colony, () => group.Phase == RaidPhase.Gathering, 400, "the band never finished arriving");

            var members = Members(colony, group).ToList();
            CombatSystem combat = colony.Pawns.Combat!;
            for (int i = 0; i < 4; i++) combat.Down(members[i], null, -1, colony.World.CurrentTick);
            colony.World.Tick(RaidSystem.CheckTicks + 1);
            Assert.That(group.Phase, Is.Not.EqualTo(RaidPhase.Withdrawing), "four in ten sent the band away");

            combat.Down(members[4], null, -1, colony.World.CurrentTick);
            colony.World.Tick(RaidSystem.CheckTicks + 1);
            Assert.That(group.Phase, Is.EqualTo(RaidPhase.Withdrawing), "half the band down did not");

            int ledgerBefore = colony.Incidents.Ledger.Count;
            TickUntil(colony, () => colony.Pawns.Raids!.Count == 0, 4_000, "the band never left");
            Assert.That(colony.Pawns.Pawns.All.Count(p => p.IsHostile), Is.EqualTo(5), "the downed stay and the rest are gone");
            Assert.That(colony.Pawns.Pawns.All.Where(p => p.IsHostile).All(p => p.Downed), Is.True);
            for (int i = ledgerBefore; i < colony.Incidents.Ledger.Count; i++)
                Assert.That(colony.Incidents.Ledger[i].IncidentDef, Is.Not.EqualTo(IncidentHandle.BanditLeft),
                    "a raid member's leaving wrote a row");
        }

        // ---- the size, the mix, the ceiling ---------------------------------------------------

        [TestCase(3, 0, 3)]
        [TestCase(3, 10, 5)]
        [TestCase(0, 0, 1)]
        [TestCase(100, 0, 30)]
        public void TheAutoSizeIsHeadcountAndDays(int colonists, int day, int expected)
        {
            RaidParams p = ContentPack.Incidents().Defs[IncidentHandle.Raid].raid!;
            Assert.That(RaidBudget.AutoSize(colonists, day * Calendar.TicksPerDay, p), Is.EqualTo(expected));
        }

        /// <summary>Size 0 is the incident's own: three standing colonists on day nought is three.</summary>
        [Test]
        public void ARaidWithNoSizeTakesTheAutoSize()
        {
            ColonyWorld colony = Board();
            Assert.That(Fire(colony, 0), Is.EqualTo(IntentRejection.None));
            Assert.That(Raid(colony).StartingSize, Is.EqualTo(3));
        }

        /// <summary>
        /// A band that would take the board past the pawn ceiling is refused, not trimmed, and fires
        /// nothing; ten is the control.
        /// </summary>
        [Test]
        public void ABandPastTheCeilingIsRefusedNotTrimmed()
        {
            ColonyWorld colony = Board();
            int room = PawnRegistry.PawnCeiling - colony.Pawns.Pawns.Count;
            Assert.That(Fire(colony, room + 1), Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(colony.Pawns.Raids!.Count, Is.Zero);
            Assert.That(Fire(colony, 10), Is.EqualTo(IntentRejection.None));
        }

        [Test]
        public void ASizeOrAMixOutOfRangeIsOutOfBounds()
        {
            ColonyWorld colony = Board();
            Assert.That(Fire(colony, -1), Is.EqualTo(IntentRejection.OutOfBounds));
            Assert.That(Fire(colony, PawnRegistry.PawnCeiling + 1), Is.EqualTo(IntentRejection.OutOfBounds));
            Assert.That(Fire(colony, 3, mix: 99), Is.EqualTo(IntentRejection.OutOfBounds));
            Assert.That(colony.Pawns.Raids!.Count, Is.Zero);
        }

        /// <summary>A named mix is the band: every one of a Gunmen raid holds a pistol.</summary>
        [Test]
        public void TheMixTheCallerNamesIsTheBand()
        {
            ColonyWorld colony = Board();
            Assert.That(Fire(colony, 5, mix: 1), Is.EqualTo(IntentRejection.None));
            RaidGroup group = Raid(colony);
            Assert.That(group.Mix, Is.EqualTo(1));
            colony.World.Tick(310);
            Assert.That(Members(colony, group).All(p => p.Kind == PawnKindIndex.Gunman), Is.True);
        }

        /// <summary>The Events row carries the mix and the size (design 55 §7), in the entry's detail.</summary>
        [Test]
        public void TheLedgerRecordsTheMixAndTheSize()
        {
            ColonyWorld colony = Board();
            Assert.That(Fire(colony, 12), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Incidents.Ledger.LastFiredTick(IncidentHandle.Raid), Is.GreaterThanOrEqualTo(0));

            var views = new List<BulletinView>();
            colony.World.Tick();
            foreach (BulletinView view in colony.World.Views.Current.Bulletins) views.Add(view);
            BulletinView raid = views.Single(v => v.IncidentDef == IncidentHandle.Raid);
            Assert.That(raid.Subject, Is.EqualTo(Mixed));
            Assert.That(raid.Amount, Is.EqualTo(12));
        }

        // ---- the lone bandit, the hash, the save ----------------------------------------------

        /// <summary>A bandit spawned by the debug menu is in no raid, and hunts from its first think as it always did.</summary>
        [Test]
        public void ALoneBanditIsInNoRaid()
        {
            ColonyWorld colony = Board();
            Pawn bandit = colony.Pawns.Pawns.Spawn(colony.Pawns.Pawns.All[0].Cell, PawnKindIndex.Bandit);
            Assert.That(colony.Pawns.Raids!.GroupOf(bandit.Id.Value), Is.Null);
            colony.World.Tick(5);
            Assert.That(bandit.CurrentJob, Is.Not.Null);
            Assert.That(CombatJobs.IsAttack(bandit.CurrentJob!.DefIndex), Is.True, "a lone bandit beside a colonist did not attack");
        }

        /// <summary>A colony that has never been raided hashes as it did before raids: the system says nothing while empty.</summary>
        [Test]
        public void AnEmptyRaidSystemAddsNothingToTheHash()
        {
            StateHash hash = StateHash.New();
            ((IStateHashable)Board().Pawns.Raids!).ContributeTo(ref hash);
            Assert.That(hash.Value, Is.EqualTo(StateHash.New().Value));
        }

        [TestCase(40, TestName = "ASaveMidTrickleResumesTheSameRaid")]
        [TestCase(900, TestName = "ASaveMidLoiterResumesTheSameRaid")]
        public void ASaveResumesTheSameRaid(int after)
        {
            ColonyWorld original = Board();
            Assert.That(Fire(original, 12), Is.EqualTo(IntentRejection.None));
            original.World.Tick(after);

            ColonyWorld restored = ColonyWorld.Build(Size, 7u, BoardScenario(), barren: true, wooded: false);
            var header = restored.Load(original.Save());
            Assert.That(header.SkippedSections, Is.Empty, "a section this build wrote was not read back");
            Assert.That(Hash(restored), Is.EqualTo(Hash(original)), "the hash differs immediately after loading");
            Assert.That(restored.Pawns.Raids!.Count, Is.EqualTo(1));

            for (int i = 0; i < 20; i++)
            {
                original.World.Tick(100);
                restored.World.Tick(100);
                Assert.That(Hash(restored), Is.EqualTo(Hash(original)), $"the worlds parted {100 * (i + 1)} ticks after the load");
            }
            Assert.That(restored.Pawns.Raids!.Groups[0].Phase, Is.EqualTo(original.Pawns.Raids!.Groups[0].Phase));
        }

        static ScenarioDef BoardScenario()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 3;
            return scenario;
        }

        /// <summary>Two worlds fed the same raid on the same tick stay in lockstep through it.</summary>
        [Test]
        public void TwoWorldsRaidedAlikeStayInLockstep()
        {
            ColonyWorld a = Board(), b = Board();
            Assert.That(Fire(a, 15), Is.EqualTo(IntentRejection.None));
            Assert.That(Fire(b, 15), Is.EqualTo(IntentRejection.None));
            Raid(a).LoiterTicks = Raid(b).LoiterTicks = 600;
            for (int i = 0; i < 40; i++)
            {
                a.World.Tick(100);
                b.World.Tick(100);
                Assert.That(Hash(a), Is.EqualTo(Hash(b)), $"parted at tick {a.World.CurrentTick}");
            }
        }

        // ---- the review's findings (2026-09-26) ----------------------------------------------

        /// <summary>
        /// The published middle of the band is where the band stands. <see cref="CellRef"/> takes
        /// (x, z, y), and the view was built (x, y, z): on a 60 × 60 × 16 board the alert asked the
        /// slice for layer 40-odd and sent the camera to the band's layer as a depth. The control is
        /// the mean taken here from the members themselves.
        /// </summary>
        [Test]
        public void TheRaidViewIsPublishedWhereTheBandStands()
        {
            ColonyWorld colony = Board();
            Assert.That(Fire(colony, 6), Is.EqualTo(IntentRejection.None));
            RaidGroup group = Raid(colony);
            group.EarlyTriggerCells = -1;
            TickUntil(colony, () => group.Phase == RaidPhase.Gathering, 400, "the band never finished arriving");
            colony.World.Tick();

            long sx = 0, sy = 0, sz = 0;
            var standing = Members(colony, group).Where(Melee.IsStanding).ToList();
            foreach (Pawn member in standing)
            {
                CellRef at = Size.FromIndex(member.Cell);
                sx += at.X;
                sy += at.Y;
                sz += at.Z;
            }
            int x = (int)(sx / standing.Count), y = (int)(sy / standing.Count), z = (int)(sz / standing.Count);
            Assume.That(z, Is.Not.EqualTo(y), "the band's depth is its layer, so a swap would not show");

            RaidView view = colony.World.Views.Current.Raids[0];
            Assert.That(view.Standing, Is.EqualTo(standing.Count));
            Assert.That((view.Centre.X, view.Centre.Z, view.Centre.Y), Is.EqualTo((x, z, y)));
            Assert.That(Size.Contains(view.Centre), Is.True, "the centre is off the board");
        }

        /// <summary>
        /// A member that has handed over to its own mind in the assault is not marched on the target
        /// again (design 55 §5). Before this, one that chased a colonist away from the hearth was
        /// sent back to it the moment the chase re-chose, and handed over again on arriving: out and
        /// back for ever. The control is a second member of the same band, never engaged, which is
        /// given its leg. The withdrawal still takes the engaged one off the board.
        /// </summary>
        [Test]
        public void AMemberThatTurnsToFightIsNotSentBackToTheTarget()
        {
            ColonyWorld colony = Board();
            Assert.That(Fire(colony, 4), Is.EqualTo(IntentRejection.None));
            RaidGroup group = Raid(colony);
            group.EarlyTriggerCells = -1;
            TickUntil(colony, () => group.Phase == RaidPhase.Gathering, 400, "the band never finished arriving");

            // Nothing on the board to fight, so only the target decides where a member goes.
            CombatSystem combat = colony.Pawns.Combat!;
            foreach (Pawn colonist in colony.Pawns.Pawns.All.Where(p => p.IsColonist).ToList())
                combat.Down(colonist, null, -1, colony.World.CurrentTick);
            colony.Pawns.Raids!.SetPhase(group, RaidPhase.Assaulting, colony.World.CurrentTick);
            int target = group.TargetCell;

            var members = Members(colony, group).ToList();
            Pawn fighter = members[0], other = members[1];
            Assume.That(RaidThinkNode.Cells(Size, fighter.Cell, target), Is.GreaterThan(RaidThinkNode.ArriveCells));
            var node = new RaidThinkNode();
            var job = new Job();
            Assert.That(node.TryGiveJob(fighter, colony.Pawns, job), Is.True, "the control: a leg toward the target");
            Assert.That(job.DefIndex, Is.EqualTo(JobIndex.Wander));

            // It reaches the target and hands over: engaged.
            group.TargetCell = fighter.Cell;
            Assert.That(node.TryGiveJob(fighter, colony.Pawns, new Job()), Is.False, "at the target it did not hand over");
            Assert.That(group.Members.Single(m => m.Pawn == fighter.Id.Value).Engaged, Is.True);

            // The target is far again, as it is once a chase has carried the member away.
            group.TargetCell = target;
            Assert.That(node.TryGiveJob(fighter, colony.Pawns, new Job()), Is.False, "an engaged member was sent back to the target");
            Assert.That(node.TryGiveJob(other, colony.Pawns, new Job()), Is.True, "the control: a member never engaged is given its leg");

            colony.Pawns.Raids!.SetPhase(group, RaidPhase.Withdrawing, colony.World.CurrentTick);
            job = new Job();
            Assert.That(node.TryGiveJob(fighter, colony.Pawns, job), Is.True, "an engaged member did not withdraw");
            Assert.That(job.DefIndex, Is.EqualTo(JobIndex.Steal));
        }

        /// <summary>
        /// A member killed outright while the band stages starts the assault, as a struck one does:
        /// one blow that kills strikes no grudge, and before this a colonist picking raiders off from
        /// beyond the early-trigger reach went unnoticed until half the band was gone. Removal
        /// without leaving is what a death looks like to the band. The untouched band is the control.
        /// </summary>
        [Test]
        public void AMemberKilledOutrightWhileStagingStartsTheAssault()
        {
            ColonyWorld untouched = Board();
            ColonyWorld killed = Board();
            foreach (ColonyWorld colony in new[] { untouched, killed })
            {
                Assert.That(Fire(colony, 6), Is.EqualTo(IntentRejection.None));
                Raid(colony).EarlyTriggerCells = -1;
                TickUntil(colony, () => Raid(colony).Phase == RaidPhase.Gathering, 400, "the band never finished arriving");
            }

            killed.Pawns.Pawns.Despawn(Members(killed, Raid(killed)).First());

            untouched.World.Tick(RaidSystem.CheckTicks + 1);
            killed.World.Tick(RaidSystem.CheckTicks + 1);
            Assert.That(Raid(untouched).Phase, Is.EqualTo(RaidPhase.Gathering));
            Assert.That(Raid(killed).Phase, Is.EqualTo(RaidPhase.Assaulting));
        }

        /// <summary>
        /// A band that breaks while it is still walking on calls the rest off: nobody arrives after
        /// the withdrawal, and the band closes once those on the board have gone. The count at the
        /// withdrawal is the control.
        /// </summary>
        [Test]
        public void ABandThatBreaksWhileArrivingCallsTheRestOff()
        {
            ColonyWorld colony = Board();
            Assert.That(Fire(colony, 10), Is.EqualTo(IntentRejection.None));
            RaidGroup group = Raid(colony);
            colony.World.Tick(20);
            Assume.That(group.Pending, Is.Not.Empty, "the whole band had arrived already");
            int arrived = group.Members.Count;

            colony.Pawns.Raids!.SetPhase(group, RaidPhase.Withdrawing, colony.World.CurrentTick);
            Assert.That(group.Pending, Is.Empty);
            Assert.That(colony.Pawns.Raids!.PendingArrivals, Is.Zero);

            TickUntil(colony, () => colony.Pawns.Raids!.Count == 0, 4_000, "the band never closed");
            Assert.That(group.Members, Has.Count.EqualTo(arrived), "a member walked on after the withdrawal");
            Assert.That(colony.Pawns.Pawns.All.Count(p => p.IsHostile), Is.Zero);
        }

        /// <summary>A member's engagement is saved and hashed: a save mid-assault resumes it.</summary>
        [Test]
        public void AnEngagedMemberSurvivesASave()
        {
            ColonyWorld original = Board();
            Assert.That(Fire(original, 4), Is.EqualTo(IntentRejection.None));
            RaidGroup group = Raid(original);
            group.EarlyTriggerCells = -1;
            TickUntil(original, () => group.Phase == RaidPhase.Gathering, 400, "the band never finished arriving");
            original.Pawns.Raids!.SetPhase(group, RaidPhase.Assaulting, original.World.CurrentTick);
            Pawn member = Members(original, group).First();
            ulong before = Hash(original);
            original.Pawns.Raids!.Engage(member);
            Assert.That(Hash(original), Is.Not.EqualTo(before), "engaging a member did not move the hash");

            ColonyWorld restored = ColonyWorld.Build(Size, 7u, BoardScenario(), barren: true, wooded: false);
            restored.Load(original.Save());
            Assert.That(Hash(restored), Is.EqualTo(Hash(original)));
            Assert.That(Raid(restored).Members.Single(m => m.Pawn == member.Id.Value).Engaged, Is.True);
            for (int i = 0; i < 5; i++)
            {
                original.World.Tick(100);
                restored.World.Tick(100);
                Assert.That(Hash(restored), Is.EqualTo(Hash(original)), $"the worlds parted {100 * (i + 1)} ticks after the load");
            }
        }

        /// <summary>
        /// Room a raid has promised its members still to walk on is not the debug menu's to spend:
        /// with the band filling the board to the ceiling, a spawn is refused until they are on. A
        /// board with no raid is the control.
        /// </summary>
        [Test]
        public void ARaidStillArrivingHoldsItsRoomUnderTheCeiling()
        {
            ColonyWorld control = Board();
            ColonyWorld raided = Board();
            int room = PawnRegistry.PawnCeiling - raided.Pawns.Pawns.Count;
            Assert.That(Fire(raided, room), Is.EqualTo(IntentRejection.None));
            Assume.That(Raid(raided).Pending, Is.Not.Empty);

            foreach (ColonyWorld colony in new[] { control, raided })
            {
                colony.World.Intents.ClearRejected();
                colony.World.Intents.Submit(new Intent(IntentKind.SpawnPawn, Size.FromIndex(colony.Pawns.Pawns.All[0].Cell), PawnKindIndex.Bandit));
                colony.World.Tick();
            }
            Assert.That(control.World.Intents.Rejected, Is.Empty);
            Assert.That(raided.World.Intents.Rejected.Single().Reason, Is.EqualTo(IntentRejection.NotPermitted));
        }
    }
}
