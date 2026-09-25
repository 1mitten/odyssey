#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Design 43 §4: a colonist kept home takes no work outside the colony's home, walks back in
    /// when idle outside it, and is released by a draft. The setting is saved and hashed only when
    /// it is not the default.
    ///
    /// <para><b>Every claim has its control</b>, because "she did not mine the far rock" is true of
    /// a colonist with no reason to: the same rock is mined by the same colonist at Anywhere, and
    /// the same colonist at Home mines it when there is no home to keep her in.</para>
    /// </summary>
    public class PawnAreaTests
    {
        static IntentRejection SetArea(ColonyWorld colony, Pawn pawn, PawnArea area) =>
            Send(colony, new Intent(IntentKind.SetPawnArea, default, pawn.Id.Value, (int)area));

        /// <summary>Ticks with the colonists fed, rested and unbroken: none of that is under test.</summary>
        static void Run(ColonyWorld colony, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                foreach (Pawn p in colony.Pawns.Pawns.All)
                {
                    if (!p.IsColonist) continue;
                    p.BreakTicksLeft = 0;
                    for (int n = 0; n < NeedIndex.Count; n++) p.Needs[n] = 800;
                }
                colony.World.Tick();
            }
        }

        /// <summary>Run until the condition holds or the cap is reached, whichever is first.</summary>
        static void RunUntil(ColonyWorld colony, Func<bool> done, int cap)
        {
            for (int t = 0; t < cap && !done(); t += 50) Run(colony, 50);
        }

        static HomeArea Home(ColonyWorld colony) => colony.Pawns.Home!;

        /// <summary>The ground under the standable cell this far from the start: something to mine.</summary>
        static int GroundNear(ColonyWorld colony, int dx, int dz) => Near(colony, dx, dz) - Size.LayerStride;

        static bool Marked(ColonyWorld colony, int cell) =>
            colony.Pawns.Designations!.At(cell) == DesignationKind.Mine;

        /// <summary>One colonist, one bed (so there is a home), and a mining mark far outside it.</summary>
        static (ColonyWorld colony, Pawn pawn, int rock) FarRock(int beds = 1)
        {
            var colony = Board(colonists: 1, beds: beds);
            Pawn pawn = colony.Pawns.Pawns.All[0];
            int rock = GroundNear(colony, 20, 0);
            Assume.That(colony.Pawns.Designations!.Designate(Size.FromIndex(rock), DesignationKind.Mine),
                Is.EqualTo(IntentRejection.None), "the far ground would not take a mining mark");
            if (beds > 0)
            {
                Assume.That(Home(colony).IsEmpty, Is.False, "the bed made no home");
                Assume.That(Home(colony).Contains(rock), Is.False, "the far rock is inside home");
            }
            return (colony, pawn, rock);
        }

        // ---- the setting (§4a) ----------------------------------------------------------------

        [Test]
        public void TheIntentSetsItAndRefusesWhatMeansNothing()
        {
            var colony = Board(colonists: 1);
            Pawn pawn = colony.Pawns.Pawns.All[0];

            Assert.That(SetArea(colony, pawn, PawnArea.Home), Is.EqualTo(IntentRejection.None));
            Assert.That(pawn.Area, Is.EqualTo(PawnArea.Home));
            Assert.That(SetArea(colony, pawn, PawnArea.Home), Is.EqualTo(IntentRejection.AlreadyInThatState));
            Assert.That(Send(colony, new Intent(IntentKind.SetPawnArea, default, pawn.Id.Value, 2)),
                Is.EqualTo(IntentRejection.NotPermitted), "an area that does not exist was taken");

            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 15, 0));
            Pawn hog = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, -15, 0));
            Assert.That(SetArea(colony, bandit, PawnArea.Home), Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(SetArea(colony, hog, PawnArea.Home), Is.EqualTo(IntentRejection.NotPermitted));
        }

        [Test]
        public void ItAppliesWhilePaused() =>
            Assert.That(PausedIntents.AppliesWhilePaused(IntentKind.SetPawnArea), Is.True);

        [Test]
        public void ItIsSavedAndHashedOnlyWhenNotTheDefault()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(60);
            Pawn a = colony.Pawns.Pawns.All[0], b = colony.Pawns.Pawns.All[1];
            ulong before = colony.World.ComputeStateHash().Value;
            byte[] saved = colony.Save();

            a.Area = PawnArea.Home;
            Assert.That(colony.World.ComputeStateHash().Value, Is.Not.EqualTo(before), "the hash cannot see Home");
            a.Area = PawnArea.Anywhere;
            Assert.That(colony.World.ComputeStateHash().Value, Is.EqualTo(before), "the default hashes differently from before");
            Assert.That(colony.Save(), Is.EqualTo(saved), "the default saves differently from before");

            b.Area = PawnArea.Home;
            var restored = Board(colonists: 2);
            restored.Load(colony.Save());
            Assert.That(restored.Pawns.Pawns.Get(a.Id)!.Area, Is.EqualTo(PawnArea.Anywhere));
            Assert.That(restored.Pawns.Pawns.Get(b.Id)!.Area, Is.EqualTo(PawnArea.Home));
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value));

            colony.World.Tick(300);
            restored.World.Tick(300);
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value),
                "the worlds parted after the load");
        }

        [Test]
        public void ItIsPublishedOnlyWhenNotTheDefault()
        {
            var colony = Board(colonists: 1);
            Pawn pawn = colony.Pawns.Pawns.All[0];
            colony.World.Tick();
            Assert.That(colony.World.Views.Current.TryGetPawnAspect(pawn.Id, AreaAspects.Area, out _), Is.False,
                "the default is published");

            SetArea(colony, pawn, PawnArea.Home);
            Assert.That(colony.World.Views.Current.TryGetPawnAspect(pawn.Id, AreaAspects.Area, out int value), Is.True);
            Assert.That(value, Is.EqualTo((int)PawnArea.Home));
        }

        // ---- the gate (§4b, §4c) ----------------------------------------------------------------

        [Test]
        public void AColonistKeptHomeLeavesTheFarRockAndOneAtAnywhereMinesIt()
        {
            var (home, homePawn, homeRock) = FarRock();
            SetArea(home, homePawn, PawnArea.Home);
            Run(home, 4_000);
            Assert.That(Marked(home, homeRock), Is.True, "she mined a rock outside home");

            // Control: the same colony, the same rock, the same colonist at Anywhere.
            var (anywhere, _, anywhereRock) = FarRock();
            Run(anywhere, 4_000);
            Assert.That(Marked(anywhere, anywhereRock), Is.False, "nobody mined the rock at all: the test proves nothing");
        }

        /// <summary>An empty home restricts nobody (§4d): a new colony has none.</summary>
        [Test]
        public void WithNoHomeAColonistKeptHomeStillWorks()
        {
            var (colony, pawn, rock) = FarRock(beds: 0);
            Assume.That(Home(colony).IsEmpty, Is.True);
            SetArea(colony, pawn, PawnArea.Home);
            Run(colony, 4_000);
            Assert.That(Marked(colony, rock), Is.False, "a colonist kept to a home that does not exist did nothing");
        }

        /// <summary>
        /// A forced order is gated like any other work (§4c). A build cannot show it, because a
        /// site is home by itself (§3a); fetching a weapon can.
        /// </summary>
        [Test]
        public void AForcedFetchOutsideHomeIsRefusedAndInsideIsTaken()
        {
            var colony = Board(colonists: 1);
            Pawn pawn = colony.Pawns.Pawns.All[0];
            colony.World.Tick();
            SetArea(colony, pawn, PawnArea.Home);

            int far = Near(colony, 22, 0), near = Near(colony, 2, 2);
            Assume.That(Home(colony).Contains(far), Is.False);
            Assume.That(Home(colony).Contains(near), Is.True);
            ThingId farBat = colony.Pawns.Items.Spawn(ItemIndex.Bat, far);
            ThingId nearBat = colony.Pawns.Items.Spawn(ItemIndex.Bat, near);

            Assert.That(Send(colony, new Intent(IntentKind.OrderEquip, Size.FromIndex(far), pawn.Id.Value, farBat.Value)),
                Is.EqualTo(IntentRejection.NotPermitted), "kept home, she was sent out for a weapon");
            Assert.That(Send(colony, new Intent(IntentKind.OrderEquip, Size.FromIndex(near), pawn.Id.Value, nearBat.Value)),
                Is.EqualTo(IntentRejection.None), "the weapon inside home was refused too: the test proves nothing");
        }

        /// <summary>A site is home by itself (§3a), so a builder kept home still goes to any site.</summary>
        [Test]
        public void ABuildSiteIsAlwaysInsideHome()
        {
            var colony = Board(colonists: 1);
            colony.World.Tick();
            int far = Near(colony, 22, 0);
            Assume.That(Home(colony).Contains(far), Is.False);
            Assume.That(colony.Construction.Place(Size.FromIndex(far), BuildingHandle.Wall, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.None));
            Assert.That(Home(colony).Contains(colony.Construction.Sites[0]), Is.True);
        }

        [Test]
        public void DraftedSheGoesWhereSheIsSentAndReleasedSheWalksHome()
        {
            var colony = Board(colonists: 1);
            Pawn pawn = colony.Pawns.Pawns.All[0];
            colony.World.Tick();
            SetArea(colony, pawn, PawnArea.Home);
            int far = Near(colony, 22, 0);
            Assume.That(Home(colony).Contains(far), Is.False);

            Draft(colony, pawn);
            Assert.That(Send(colony, new Intent(IntentKind.OrderMove, Size.FromIndex(far), pawn.Id.Value)),
                Is.EqualTo(IntentRejection.None), "a drafted colonist kept home could not be sent out");
            RunUntil(colony, () => pawn.Cell == far, 3_000);
            Assert.That(pawn.Cell, Is.EqualTo(far), "drafted, she did not go where she was sent");

            Draft(colony, pawn, on: false);
            RunUntil(colony, () => Home(colony).Contains(pawn.Cell), 5_000);
            Assert.That(Home(colony).Contains(pawn.Cell), Is.True, "released outside home, she did not walk back");
        }

        [Test]
        public void IdleOutsideHomeSheWalksBackAndAColonistAtAnywhereDoesNot()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick();
            Pawn kept = colony.Pawns.Pawns.All[0], free = colony.Pawns.Pawns.All[1];
            SetArea(colony, kept, PawnArea.Home);
            int far = Near(colony, 25, -10), farther = Near(colony, 25, 10);
            Assume.That(Home(colony).Contains(far) || Home(colony).Contains(farther), Is.False);

            Stand(colony, kept, far);
            Stand(colony, free, farther);
            // About a hundred ticks a cell at a walk (2.5 m, 60 ticks a second): twenty-odd cells
            // is a couple of thousand.
            RunUntil(colony, () => Home(colony).Contains(kept.Cell), 5_000);
            Assert.That(Home(colony).Contains(kept.Cell), Is.True, "kept home, she stayed outside");
            Assert.That(Home(colony).Contains(free.Cell), Is.False, "at Anywhere she went home anyway: the test proves nothing");
        }

        [Test]
        public void ANewSettingEndsAJobOutsideHomeAndLeavesOneInside()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick();
            Pawn a = colony.Pawns.Pawns.All[0], b = colony.Pawns.Pawns.All[1];
            int outside = Near(colony, 22, 0);
            int inside = a.Cell;
            Assume.That(Home(colony).Contains(outside), Is.False);
            Assume.That(Home(colony).Contains(inside), Is.True);

            Walk(colony, a, outside);
            Walk(colony, b, inside);
            SetArea(colony, a, PawnArea.Home);
            SetArea(colony, b, PawnArea.Home);
            Assert.That(a.CurrentJob?.TargetCell, Is.Not.EqualTo(outside), "a walk out of home survived the setting");
            Assert.That(b.CurrentJob?.TargetCell, Is.EqualTo(inside), "a walk inside home was ended by it");
        }

        /// <summary>A long walk nobody forced, as a job that names a cell.</summary>
        static void Walk(ColonyWorld colony, Pawn pawn, int cell)
        {
            colony.Jobs.EndJob(pawn, JobStatus.Failed);
            Job job = pawn.JobBuffer;
            job.Reset(JobIndex.Wait);
            job.TargetCell = cell;
            job.WorkTicks = 20_000;
            Assume.That(colony.Jobs.StartJob(pawn, job, colony.World.CurrentTick), Is.True);
        }

        /// <summary>
        /// The fight is never gated (§4b): the physical question is asked there, not the setting.
        /// The bandit strikes and steps back out of reach, so she has to go after it — through the
        /// self-defence node's remembered blow and then the attack driver's reach, both outside
        /// home. An adjacent attacker would prove nothing: it is fought without asking any reach.
        /// </summary>
        [Test]
        public void AColonistKeptHomeStillGoesAfterWhoeverStruckHerOutsideIt()
        {
            var colony = Board(colonists: 1);
            Pawn pawn = colony.Pawns.Pawns.All[0];
            colony.World.Tick();
            SetArea(colony, pawn, PawnArea.Home);
            int far = Near(colony, 22, 0), farther = Near(colony, 26, 0);
            Assume.That(Home(colony).Contains(far) || Home(colony).Contains(farther), Is.False);
            Stand(colony, pawn, far);
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 23, 0));
            bandit.Drafted = false;

            Strike(colony, bandit, pawn, 1);
            Assume.That(pawn.RetaliateAgainst, Is.EqualTo(bandit.Id.Value), "the blow left no memory to act on");
            Stand(colony, bandit, farther);
            colony.Jobs.EndJob(pawn, JobStatus.Failed);

            Run(colony, 1);
            Assert.That(pawn.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee), "struck outside home, she did not go after it");
            Run(colony, 60);
            Assert.That(pawn.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee), "the attack was dropped outside home");
            Assert.That(pawn.CombatTarget, Is.EqualTo(bandit.Id.Value));
        }

        /// <summary>
        /// Food is gated until she is starving (§4c): the genre's escape hatch. The only meal is
        /// outside home; hungry, she leaves it; starving, she goes for it.
        /// </summary>
        [Test]
        public void KeptHomeSheEatsAMealOutsideOnlyWhenStarving()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 1;
            scenario.mealPiles = 0;
            var colony = ColonyWorld.Build(Size, 7u, scenario, barren: true, wooded: false);
            Pawn pawn = colony.Pawns.Pawns.All[0];
            colony.World.Tick();
            SetArea(colony, pawn, PawnArea.Home);
            int far = Near(colony, 22, 0);
            Assume.That(Home(colony).Contains(far), Is.False);
            colony.Pawns.Items.Spawn(ItemIndex.Meal, far);

            pawn.Needs[NeedIndex.Food] = 0;
            pawn.StarvationSeverity = 0;
            colony.Jobs.EndJob(pawn, JobStatus.Failed);
            colony.World.Tick();
            Assert.That(pawn.CurrentJob?.DefIndex, Is.Not.EqualTo(JobIndex.Eat), "hungry but not starving, she left home to eat");

            pawn.StarvationSeverity = 100;
            colony.Jobs.EndJob(pawn, JobStatus.Failed);
            colony.World.Tick();
            Assert.That(pawn.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Eat), "starving, she would not eat the meal outside home");
        }

        [Test]
        public void TheGateIsTheOneQuestionAndTheDraftOpensIt()
        {
            var colony = Board(colonists: 1);
            Pawn pawn = colony.Pawns.Pawns.All[0];
            colony.World.Tick();
            int outside = Near(colony, 22, 0), inside = pawn.Cell;
            PawnContext ctx = colony.Pawns;

            Assert.That(ctx.MayWork(pawn, outside), Is.True, "at Anywhere the gate is shut");
            pawn.Area = PawnArea.Home;
            Assert.That(ctx.MayWork(pawn, outside), Is.False);
            Assert.That(ctx.MayWork(pawn, inside), Is.True);
            Assert.That(ctx.Reachable(pawn, outside), Is.False, "Reachable does not ask the gate");
            Assert.That(ctx.CanTravel(pawn, outside), Is.True, "CanTravel asks the gate");
            Assert.That(Draft(colony, pawn), Is.EqualTo(IntentRejection.None));
            Assert.That(ctx.MayWork(pawn, outside), Is.True, "the draft does not open the gate");
        }
    }
}
