#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Events;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Pawns.Wildlife;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Bandits steal and leave (design 33 §17; the owner, 2026-09-24: <i>"It will thieve items or
    /// kidnap people depending on their motivation creating a negative event (but they could be
    /// rescued later) - seam this later but for now - thieve items"</i>). With nobody standing that
    /// it can reach and nothing it may break, a bandit lifts the nearest stack, walks to the
    /// nearest edge and leaves the board with it — removed, not killed — and the ledger records a
    /// theft. The fight still comes first; a thief struck down drops what it carries.
    /// </summary>
    public class TheftTests
    {
        // ---- the board ----------------------------------------------------------------------

        /// <summary>
        /// A bare board with colonists and nothing else: no beds, no meals, no salvage. Every stack
        /// on it is one a test put there, so "the nearest" is a thing the test chose.
        /// </summary>
        static ColonyWorld Empty(int colonists = 1)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = 0;
            scenario.mealPiles = 0;
            scenario.salvage = 0;
            var colony = ColonyWorld.Build(Size, 7u, scenario, barren: true, wooded: false);
            colony.World.Tick();
            return colony;
        }

        static ulong Hash(ColonyWorld colony) => colony.World.ComputeStateHash().Value;

        static ColonyItem Put(ColonyWorld colony, int item, int cell, int stack)
        {
            ThingId id = colony.Pawns.Items.Spawn(item, cell, stack);
            return colony.Pawns.Items.Get(id)!;
        }

        /// <summary>One colonist on the start, down; a bandit four cells off. Nobody left standing.</summary>
        static (ColonyWorld colony, Pawn colonist, Pawn bandit) AllDown(ColonyWorld? colony = null)
        {
            colony ??= Empty();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            Stand(colony, colonist, Near(colony, 0, 0));
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 0, 4));
            Strike(colony, bandit, colonist, colonist.HpMilli);
            Assert.That(colonist.Downed, Is.True, "the control: she is down");
            return (colony, colonist, bandit);
        }

        static bool Gone(ColonyWorld colony, Pawn pawn) => colony.Pawns.Pawns.Get(pawn.Id) == null;

        static bool Stealing(Pawn pawn) => pawn.CurrentJob?.DefIndex == JobIndex.Steal;

        static int Carried(Pawn pawn) => pawn.CurrentJob?.CarriedItem ?? -1;

        /// <summary>Tick until the bandit has gone, noting the last cell it stood on and what it last carried.</summary>
        static (int lastCell, int lastCarried) RunUntilGone(ColonyWorld colony, Pawn bandit, int limit = 20_000)
        {
            int lastCell = bandit.Cell, lastCarried = -1;
            for (int t = 0; t < limit && !Gone(colony, bandit); t++)
            {
                lastCell = bandit.Cell;
                if (Carried(bandit) >= 0) lastCarried = Carried(bandit);
                colony.World.Tick();
            }
            Assert.That(Gone(colony, bandit), Is.True, $"the bandit was still on the board after {limit} ticks");
            return (lastCell, lastCarried);
        }

        static List<IncidentLedger.Entry> Ledger(ColonyWorld colony)
        {
            var list = new List<IncidentLedger.Entry>();
            for (int i = 0; i < colony.Incidents.Ledger.Count; i++) list.Add(colony.Incidents.Ledger[i]);
            return list;
        }

        // ---- the theft ------------------------------------------------------------------------

        /// <summary>
        /// The whole of it: the nearer of two stacks is lifted, carried to the edge nearest it, and
        /// leaves with the bandit, whose machete goes too. The colony has one stack fewer; the
        /// bandit is gone, with no corpse, no death reported and nobody remembering one.
        /// </summary>
        [Test]
        public void WithEveryoneDownItCarriesOffTheNearestStackAndLeaves()
        {
            var (colony, colonist, bandit) = AllDown();
            ColonyItem near = Put(colony, ItemIndex.Meal, Near(colony, 3, 4), 12);
            ColonyItem far = Put(colony, ItemIndex.Stone, Near(colony, -8, 4), 30);
            int nearCell = near.Cell;
            int weapon = bandit.EquippedItem;
            Assert.That(weapon, Is.Not.Zero, "the control: it arrived armed");
            int pawnsBefore = colony.Pawns.Pawns.All.Count;
            var tape = new Tape();

            int expectedEdge = Theft.EdgeFrom(colony.Pawns, nearCell, bandit.OwnMode);
            bool sawItStealing = false, stoodOnTheStack = false;
            int lastCell = bandit.Cell, lastCarried = -1, ticks = 0;
            for (int t = 0; t < 20_000 && !Gone(colony, bandit); t++, ticks++)
            {
                if (Stealing(bandit)) sawItStealing = true;
                if (bandit.Cell == nearCell) stoodOnTheStack = true;
                lastCell = bandit.Cell;
                if (Carried(bandit) >= 0) lastCarried = Carried(bandit);
                colony.World.Tick();
                tape.Read(colony);
            }

            Assert.That(Gone(colony, bandit), Is.True, "it never left");
            Assert.That(sawItStealing, Is.True, "it left without a theft job");
            Assert.That(stoodOnTheStack, Is.True, "it lifted a stack it never stood on");
            TestContext.WriteLine($"gone after {ticks} ticks, from {Size.FromIndex(lastCell)}; the stack was at {Size.FromIndex(nearCell)}");
            Assert.That(lastCarried, Is.EqualTo(near.Id.Value), "it carried something other than the nearer stack");
            Assert.That(WildlifeSystem.IsEdge(Size, lastCell), Is.True, "it left from somewhere that is not the edge");
            Assert.That(lastCell, Is.EqualTo(expectedEdge), "it left by an edge other than the one nearest the stack");

            Assert.That(near.Despawned, Is.True, "the stolen stack is still in the colony's things");
            Assert.That(far.Despawned, Is.False, "the other stack went too");
            Assert.That(far.Cell, Is.Not.EqualTo(-1));
            Assert.That(colony.Pawns.Items.Get(new ThingId(weapon)), Is.Null, "its weapon was left behind");

            Assert.That(colony.Pawns.Pawns.All.Count, Is.EqualTo(pawnsBefore - 1));
            Assert.That(colony.Pawns.Corpses.Count, Is.Zero, "leaving the board left a corpse");
            Assert.That(tape.Of(CombatEventKind.Died), Is.Empty, "leaving the board was reported as a death");
            Assert.That(colonist.Memories.Exists(m => m.ThoughtIndex == ThoughtIndex.ColonistDied), Is.False,
                "somebody mourned a bandit that walked off");
        }

        /// <summary>
        /// The negative event: one ledger entry, a theft, at the edge it left from, naming the item
        /// and the stack; published on the next frame for the Events panel with the same two numbers.
        /// </summary>
        [Test]
        public void TheLedgerRecordsTheTheftAndTheBulletinSaysWhat()
        {
            var (colony, _, bandit) = AllDown();
            Put(colony, ItemIndex.Meal, Near(colony, 3, 4), 12);
            Assert.That(colony.Incidents.Ledger.Count, Is.Zero, "the control: nothing has happened yet");

            var (lastCell, _) = RunUntilGone(colony, bandit);
            colony.World.Tick();

            var entries = Ledger(colony);
            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].IncidentDef, Is.EqualTo(IncidentHandle.Theft));
            Assert.That(entries[0].Cell, Is.EqualTo(lastCell));
            Assert.That(colony.Incidents.Ledger.TryGetDetail(entries[0].Id, out var detail), Is.True);
            Assert.That(detail.Subject, Is.EqualTo(ItemIndex.Meal));
            Assert.That(detail.Amount, Is.EqualTo(12));
            Assert.That(colony.Incidents.Ledger.Fires(IncidentHandle.Theft), Is.EqualTo(1));

            var bulletins = colony.World.Views.Current.Bulletins;
            Assert.That(bulletins.Length, Is.EqualTo(1));
            Assert.That(bulletins[0].IncidentDef, Is.EqualTo(IncidentHandle.Theft));
            Assert.That(bulletins[0].Subject, Is.EqualTo(ItemIndex.Meal));
            Assert.That(bulletins[0].Amount, Is.EqualTo(12));
            Assert.That(bulletins[0].Favourability, Is.EqualTo((int)IncidentFavourability.Bad), "a theft is a blow");
        }

        /// <summary>With nothing on the board to take, it walks off by the nearest edge empty-handed, and the ledger says a bandit left.</summary>
        [Test]
        public void WithNothingToStealItLeavesEmptyHanded()
        {
            var (colony, _, bandit) = AllDown();
            int expectedEdge = Theft.EdgeFrom(colony.Pawns, bandit.Cell, bandit.OwnMode);

            var (lastCell, lastCarried) = RunUntilGone(colony, bandit);
            colony.World.Tick();

            Assert.That(lastCarried, Is.EqualTo(-1), "it carried something off an empty board");
            Assert.That(lastCell, Is.EqualTo(expectedEdge));
            var entries = Ledger(colony);
            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].IncidentDef, Is.EqualTo(IncidentHandle.BanditLeft));
            Assert.That(colony.Incidents.Ledger.TryGetDetail(entries[0].Id, out _), Is.False, "an empty-handed leaving is about nothing");
            Assert.That(colony.World.Views.Current.Bulletins[0].Subject, Is.EqualTo(-1));
            Assert.That(colony.Pawns.Corpses.Count, Is.Zero);
        }

        /// <summary>
        /// Two stacks the same distance off: the lower item id, the older stack, whichever lister holds
        /// it first. The same choice after a load.
        /// </summary>
        [Test]
        public void OnATieItTakesTheOlderStack()
        {
            var (colony, _, bandit) = AllDown();
            ColonyItem first = Put(colony, ItemIndex.Wood, Near(colony, 3, 4), 5);
            ColonyItem second = Put(colony, ItemIndex.Stone, Near(colony, -3, 4), 5);
            Assert.That(colony.Pawns.Distance(bandit.Cell, first.Cell), Is.EqualTo(colony.Pawns.Distance(bandit.Cell, second.Cell)),
                "the control: the two are the same distance off");
            Assert.That(first.Id.Value, Is.LessThan(second.Id.Value));

            ColonyItem? chosen = Theft.NearestLoot(colony.Pawns, bandit, bandit.OwnMode, out int at);
            Assert.That(chosen, Is.SameAs(first));
            Assert.That(at, Is.EqualTo(first.Cell));
        }

        // ---- the fight first ------------------------------------------------------------------

        /// <summary>
        /// A colonist standing that it can reach comes first, and a colony building it may break
        /// comes next: with either, it never takes the stack beside it. The control is the same
        /// board with both gone, where it does.
        /// </summary>
        [Test]
        public void AColonistThenABuildingComeBeforeTheft()
        {
            var colony = Empty(colonists: 2);
            Pawn standing = colony.Pawns.Pawns.All[1];
            var (_, _, bandit) = AllDown(colony);
            Stand(colony, standing, Near(colony, -6, 0));
            ColonyItem meal = Put(colony, ItemIndex.Meal, Near(colony, 1, 4), 6);

            colony.World.Tick();
            Assert.That(bandit.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee), "a standing colonist did not come first");
            Assert.That(bandit.CombatTarget, Is.EqualTo(standing.Id.Value));

            // Down her too, and put a wall up: the wall now comes before the meal.
            Strike(colony, bandit, standing, standing.HpMilli);
            int wall = Near(colony, 6, 4);
            Assert.That(colony.Construction.Place(Size.FromIndex(wall), BuildingHandle.Wall, StuffHandle.Wood, 0), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Construction.Raise(colony.Pawns, wall), Is.True);
            for (int t = 0; t < 30 && !(bandit.CurrentJob?.DefIndex == JobIndex.AttackMelee && bandit.CombatTarget == 0); t++)
                colony.World.Tick();
            Assert.That(bandit.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee), "the wall did not come before the meal");
            Assert.That(bandit.CombatTarget, Is.Zero, "the control: it is on the building, not a pawn");
            Assert.That(meal.Despawned || meal.Cell < 0, Is.False, "it took the meal while it had a wall to break");

            // The control: nothing left to fight or break, and it steals.
            for (int t = 0; t < 20_000 && colony.Grid.Edifice[wall] >= 0 && BuildingTargets.TryFind(colony.Pawns, wall, out _); t++)
                colony.World.Tick();
            for (int t = 0; t < 60 && !Stealing(bandit); t++) colony.World.Tick();
            Assert.That(Stealing(bandit), Is.True, "with nothing left to fight or break it did not turn thief");
            Assert.That(bandit.CurrentJob!.TargetItem, Is.EqualTo(meal.Id));
        }

        /// <summary>
        /// A thief that finds a colonist standing and reachable again drops what it carries and
        /// goes back to the fight, within one look (<see cref="CombatDef.rechooseTicks"/>).
        /// </summary>
        [Test]
        public void AThiefThatCanReachAColonistAgainDropsTheLoadAndFights()
        {
            var (colony, _, bandit) = AllDown();
            ColonyItem meal = Put(colony, ItemIndex.Meal, Near(colony, 2, 4), 9);
            for (int t = 0; t < 2_000 && Carried(bandit) < 0; t++) colony.World.Tick();
            Assert.That(Carried(bandit), Is.EqualTo(meal.Id.Value), "the control: it is carrying the meal");

            Pawn newcomer = Spawn(colony, PawnKindIndex.Colonist, Near(colony, -4, 0));
            int limit = colony.Pawns.Content.Combat.rechooseTicks + 120;
            for (int t = 0; t < limit && Stealing(bandit); t++) colony.World.Tick();

            Assert.That(Stealing(bandit), Is.False, $"it kept stealing for {limit} ticks with a colonist to fight");
            Assert.That(Gone(colony, bandit), Is.False);
            Assert.That(meal.Despawned, Is.False, "the meal went with it");
            Assert.That(meal.Cell, Is.GreaterThanOrEqualTo(0), "the meal was not put down");
            for (int t = 0; t < 30 && bandit.CombatTarget != newcomer.Id.Value; t++) colony.World.Tick();
            Assert.That(bandit.CombatTarget, Is.EqualTo(newcomer.Id.Value), "it did not go for the newcomer");
        }

        // ---- struck down with it --------------------------------------------------------------

        /// <summary>Downed while carrying, it drops the load where it falls, like any carrier; it does not leave.</summary>
        [Test]
        public void DownedWhileCarryingItDropsTheLoad()
        {
            var (colony, colonist, bandit) = AllDown();
            ColonyItem meal = Put(colony, ItemIndex.Meal, Near(colony, 2, 4), 9);
            for (int t = 0; t < 2_000 && Carried(bandit) < 0; t++) colony.World.Tick();
            Assert.That(Carried(bandit), Is.EqualTo(meal.Id.Value), "the control: it is carrying the meal");
            colony.World.Tick(20);

            Strike(colony, colonist, bandit, bandit.HpMilli);
            Assert.That(bandit.Downed, Is.True);
            Assert.That(Carried(bandit), Is.EqualTo(-1));
            Assert.That(meal.Despawned, Is.False, "the load vanished with a downed carrier");
            Assert.That(meal.Cell, Is.GreaterThanOrEqualTo(0), "the load is nowhere");
            Assert.That(colony.Pawns.Distance(meal.Cell, bandit.Cell), Is.LessThanOrEqualTo(141 * 2), "the load fell far from the carrier");

            colony.World.Tick(600);
            Assert.That(Gone(colony, bandit), Is.False, "a downed thief left the board");
            Assert.That(Ledger(colony), Is.Empty, "a theft was recorded that did not happen");
        }

        /// <summary>Killed while carrying: the load drops, and the corpse is the death's, not the theft's.</summary>
        [Test]
        public void KilledWhileCarryingItDropsTheLoad()
        {
            var (colony, colonist, bandit) = AllDown();
            ColonyItem meal = Put(colony, ItemIndex.Meal, Near(colony, 2, 4), 9);
            for (int t = 0; t < 2_000 && Carried(bandit) < 0; t++) colony.World.Tick();
            Assert.That(Carried(bandit), Is.EqualTo(meal.Id.Value), "the control: it is carrying the meal");

            Strike(colony, colonist, bandit, bandit.HpMilli - bandit.DeathAtMilli + 1_000);
            colony.World.Tick();
            Assert.That(Gone(colony, bandit), Is.True);
            Assert.That(colony.Pawns.Corpses.Count, Is.EqualTo(1));
            Assert.That(meal.Despawned, Is.False, "the load went with the dead");
            Assert.That(meal.Cell, Is.GreaterThanOrEqualTo(0));
            Assert.That(Ledger(colony), Is.Empty, "a death was recorded as a theft");
        }

        // ---- no way off --------------------------------------------------------------------------

        /// <summary>
        /// With no edge it can reach — shut in by walls it may not break — it takes nothing and stays,
        /// thinking again; open a way out and it goes.
        /// </summary>
        [Test]
        public void WithNoEdgeToReachItStays()
        {
            var (colony, _, bandit) = AllDown();
            CellRef at = Size.FromIndex(bandit.Cell);
            ColonyItem meal = Put(colony, ItemIndex.Meal, Size.Index(at.X + 1, at.Z, at.Y), 4);

            // A ring of the city's walls: not colony-built, so nothing it may choose to break.
            int gap = -1;
            var records = colony.Construction.Edifices.Records;
            for (int dz = -2; dz <= 2; dz++)
            for (int dx = -2; dx <= 2; dx++)
            {
                if (System.Math.Abs(dx) != 2 && System.Math.Abs(dz) != 2) continue;
                int cell = Size.Index(at.X + dx, at.Z + dz, at.Y);
                Assert.That(colony.Construction.Place(Size.FromIndex(cell), BuildingHandle.Wall, StuffHandle.Wood, 0), Is.EqualTo(IntentRejection.None));
                Assert.That(colony.Construction.Raise(colony.Pawns, cell), Is.True);
                int h = colony.Grid.Edifice[cell];
                var placed = records[h];
                placed.Built = false;
                records[h] = placed;
                if (dx == 2 && dz == 0) gap = cell;
            }
            colony.World.Tick();
            Assert.That(Theft.EdgeFrom(colony.Pawns, bandit.Cell, bandit.OwnMode), Is.EqualTo(-1), "the control: no edge can be reached");

            for (int t = 0; t < 3_000; t++)
            {
                colony.World.Tick();
                Assert.That(Stealing(bandit), Is.False, $"tick {t}: it set out to steal with no way off the board");
            }
            Assert.That(Gone(colony, bandit), Is.False);
            Assert.That(meal.Cell, Is.GreaterThanOrEqualTo(0), "it picked the meal up with nowhere to take it");

            // A way out, and it goes — with the meal.
            Assert.That(colony.Construction.Demolish(colony.Pawns, gap, out _), Is.True);
            colony.World.Tick();
            var (_, lastCarried) = RunUntilGone(colony, bandit);
            Assert.That(lastCarried, Is.EqualTo(meal.Id.Value));
        }

        // ---- the motive ----------------------------------------------------------------------

        /// <summary>
        /// The kidnap seam (design 33 §17f): a bandit that came to kidnap does exactly what one
        /// that came to loot does today, tick for tick — the same hash at every hundredth tick of
        /// the whole theft. One that came for nothing (the control) stays and idles.
        /// </summary>
        [Test]
        public void AKidnapperStealsAsALooterDoes()
        {
            ColonyWorld Run(Motive motive, out Pawn bandit)
            {
                var colony = Empty();
                colony.Pawns.Content.KindMotive[PawnKindIndex.Bandit] = motive;
                var (_, _, m) = AllDown(colony);
                Put(colony, ItemIndex.Meal, Near(colony, 3, 4), 12);
                bandit = m;
                return colony;
            }

            Assert.That(ContentPackMotive(), Is.EqualTo(Motive.Loot), "the control: the shipped bandit came to loot");

            var looter = Run(Motive.Loot, out Pawn l);
            var kidnapper = Run(Motive.Kidnap, out Pawn k);
            var nobody = Run(Motive.None, out Pawn n);
            for (int t = 0; t < 20_000 && !Gone(looter, l); t++)
            {
                looter.World.Tick();
                kidnapper.World.Tick();
                nobody.World.Tick();
                if (t % 100 == 0) Assert.That(Hash(kidnapper), Is.EqualTo(Hash(looter)), $"tick {t}: the kidnapper parted from the looter");
            }
            Assert.That(Gone(looter, l), Is.True, "the control: the looter left");
            Assert.That(Gone(kidnapper, k), Is.True, "the kidnapper did not leave with the looter");
            Assert.That(Hash(kidnapper), Is.EqualTo(Hash(looter)));
            Assert.That(Gone(nobody, n), Is.False, "one that came for nothing left anyway");
            Assert.That(Ledger(nobody), Is.Empty);
        }

        static Motive ContentPackMotive() =>
            Odyssey.Sim.Defs.ContentPack.Pawns().MotiveOf(PawnKindIndex.Bandit);

        // ---- the hash -------------------------------------------------------------------------

        /// <summary>
        /// The job system hashes a job appended after the combat line's only once it has run
        /// (<see cref="JobSystem.HashedAlways"/>): a colony that never stole hashes its counters as
        /// the goldens were baked — the first twenty-two, zero or not, and nothing more — and one
        /// that has stolen hashes the thief's counters with their index.
        /// </summary>
        [Test]
        public void AJobAppendedAfterTheCombatLineIsHashedOnlyOnceItHasRun()
        {
            Assert.That(JobSystem.HashedAlways, Is.EqualTo(22), "the control: every golden was baked with twenty-two job defs");
            var colony = Empty();
            colony.World.Tick(50);

            StateHash asBaked = StateHash.New();
            asBaked.Add(colony.Jobs.JobsStarted);
            asBaked.Add(colony.Jobs.JobsFailed);
            for (int i = 0; i < JobSystem.HashedAlways; i++)
            {
                asBaked.Add(colony.Jobs.CompletedOf(i));
                asBaked.Add(colony.Jobs.FailedOf(i));
            }
            StateHash now = StateHash.New();
            colony.Jobs.ContributeTo(ref now);
            Assert.That(colony.Jobs.CompletedOf(JobIndex.Steal) + colony.Jobs.FailedOf(JobIndex.Steal), Is.Zero);
            Assert.That(now.Value, Is.EqualTo(asBaked.Value), "a job nobody ran moved the hash");

            var (_, _, bandit) = AllDown(colony);
            RunUntilGone(colony, bandit);
            Assert.That(colony.Jobs.CompletedOf(JobIndex.Steal), Is.EqualTo(1), "the control: the theft was counted");
            StateHash after = StateHash.New();
            colony.Jobs.ContributeTo(ref after);
            StateHash without = StateHash.New();
            without.Add(colony.Jobs.JobsStarted);
            without.Add(colony.Jobs.JobsFailed);
            for (int i = 0; i < JobSystem.HashedAlways; i++)
            {
                without.Add(colony.Jobs.CompletedOf(i));
                without.Add(colony.Jobs.FailedOf(i));
            }
            Assert.That(after.Value, Is.Not.EqualTo(without.Value), "a theft that ran is not in the hash");
        }

        // ---- a save in the middle ---------------------------------------------------------------

        /// <summary>
        /// Saved mid-carry, the load resumes the same theft: the same job and toil, the same stack in
        /// the same arms, the same hash, and the same ending — the same edge, the same tick, the same
        /// ledger entry.
        /// </summary>
        [Test]
        public void ATheftSavedMidCarryResumesIdentically()
        {
            var (colony, _, bandit) = AllDown();
            ColonyItem meal = Put(colony, ItemIndex.Meal, Near(colony, 3, 4), 12);
            for (int t = 0; t < 2_000 && Carried(bandit) < 0; t++) colony.World.Tick();
            colony.World.Tick(40);
            Assert.That(Carried(bandit), Is.EqualTo(meal.Id.Value), "the control: it is carrying the meal");

            var restored = Empty();
            restored.Load(colony.Save());
            Pawn back = restored.Pawns.Pawns.Get(bandit.Id)!;
            Assert.That(back.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Steal));
            Assert.That(Carried(back), Is.EqualTo(meal.Id.Value));
            Assert.That(restored.Pawns.Items.Get(meal.Id)!.CarriedBy, Is.EqualTo(back.Id.Value));
            Assert.That(Hash(restored), Is.EqualTo(Hash(colony)), "the loaded world is not the saved one");

            var (cellA, _) = RunUntilGone(colony, bandit);
            var (cellB, _) = RunUntilGone(restored, back);
            Assert.That(restored.World.CurrentTick, Is.EqualTo(colony.World.CurrentTick), "the two left on different ticks");
            Assert.That(cellB, Is.EqualTo(cellA));
            Assert.That(Hash(restored), Is.EqualTo(Hash(colony)));

            // And the ledger's detail survives a save of its own.
            var again = Empty();
            again.Load(restored.Save());
            Assert.That(again.Incidents.Ledger.TryGetDetail(1, out var detail), Is.True);
            Assert.That(detail.Amount, Is.EqualTo(12));
            Assert.That(Hash(again), Is.EqualTo(Hash(restored)));
        }
    }
}
