#nullable enable
using System.IO;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The draft (design 33 §2): a colonist taken off the work list, held where it stands or sent
    /// somewhere, and given back.
    ///
    /// <para><b>Every claim here has its control beside it</b>, because the failure mode of a
    /// draft test is a pass with the feature gone: a colonist on a bare board with nothing to do
    /// stands still anyway, and "she did not eat" is true of anybody who was not hungry. So the
    /// board has meals and work on it, the colonist is made hungry, and the undrafted colonist
    /// next to her is the proof that the thing withheld would otherwise have happened.</para>
    /// </summary>
    public class DraftTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ColonyWorld Board(int colonists = 2, uint seed = 7u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = colonists;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: false);
        }

        static IntentRejection Send(ColonyWorld colony, Intent intent)
        {
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(intent);
            colony.World.Tick();
            var rejected = colony.World.Intents.Rejected;
            return rejected.Count == 0 ? IntentRejection.None : rejected[0].Reason;
        }

        static IntentRejection Draft(ColonyWorld colony, Pawn pawn, bool on = true) =>
            Send(colony, new Intent(IntentKind.SetDrafted, default, pawn.Id.Value, on ? 1 : 0));

        static IntentRejection Move(ColonyWorld colony, Pawn pawn, int cell) =>
            Send(colony, new Intent(IntentKind.OrderMove, Size.FromIndex(cell), pawn.Id.Value));

        /// <summary>A standable cell <paramref name="dx"/> east of the pawn on its own layer, or -1.</summary>
        static int CellEast(ColonyWorld colony, Pawn pawn, int dx)
        {
            CellRef at = Size.FromIndex(pawn.Cell);
            return colony.Pawns.Cells.NearestWalkableInColumn(at.X + dx, at.Z, at.Y);
        }

        static int JobOf(Pawn pawn) => pawn.CurrentJob?.DefIndex ?? -1;

        static ulong Hash(ColonyWorld colony) => colony.World.ComputeStateHash().Value;

        // ---- the hold ------------------------------------------------------------------------

        [Test]
        public void ADraftedColonistHoldsWhereSheStands()
        {
            var colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            colony.World.Tick(60);

            Assert.That(Draft(colony, pawn), Is.EqualTo(IntentRejection.None));
            Assert.That(pawn.Drafted, Is.True);
            Assert.That(JobOf(pawn), Is.EqualTo(JobIndex.DraftHold));

            // Wherever the interrupted step lands her, she stays there. A diagonal step is 141
            // ticks at the standard pace, so the landing is waited for rather than guessed at.
            for (int t = 0; t < 400 && pawn.HasPath; t++) colony.World.Tick();
            Assert.That(pawn.HasPath, Is.False, "the kept step never landed");
            int stood = pawn.Cell;
            colony.World.Tick(3_000);
            Assert.That(pawn.Cell, Is.EqualTo(stood), "a drafted colonist wandered off");
            Assert.That(JobOf(pawn), Is.EqualTo(JobIndex.DraftHold));
        }

        [Test]
        public void ADraftedColonistDoesNotEatAndHerUndraftedNeighbourDoes()
        {
            var colony = Board();
            Pawn drafted = colony.Pawns.Pawns.All[0];
            Pawn control = colony.Pawns.Pawns.All[1];
            Assert.That(Draft(colony, drafted), Is.EqualTo(IntentRejection.None));

            drafted.Needs[NeedIndex.Food] = 0;
            control.Needs[NeedIndex.Food] = 0;

            bool controlAte = false;
            for (int t = 0; t < 2_000; t++)
            {
                colony.World.Tick();
                Assert.That(JobOf(drafted), Is.Not.EqualTo(JobIndex.Eat), $"the drafted colonist went to eat at tick {t}");
                if (JobOf(control) == JobIndex.Eat) controlAte = true;
            }

            Assert.That(controlAte, Is.True, "the control never ate, so the drafted colonist's fast proves nothing");
            Assert.That(drafted.Needs[NeedIndex.Food], Is.EqualTo(0), "her hunger went somewhere");
        }

        [Test]
        public void ReleasingTheDraftHandsTheColonistBackToTheWorkList()
        {
            var colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Draft(colony, pawn);
            colony.World.Tick(100);

            Assert.That(Draft(colony, pawn, on: false), Is.EqualTo(IntentRejection.None));
            Assert.That(pawn.Drafted, Is.False);
            Assert.That(JobOf(pawn), Is.Not.EqualTo(JobIndex.DraftHold));
            Assert.That(JobOf(pawn), Is.Not.EqualTo(JobIndex.Goto));
        }

        [Test]
        public void DraftingTwiceIsQuietAndAnAnimalCannotBeDrafted()
        {
            var colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Assert.That(Draft(colony, pawn), Is.EqualTo(IntentRejection.None));
            Assert.That(Draft(colony, pawn), Is.EqualTo(IntentRejection.AlreadyInThatState));

            Pawn hog = colony.Pawns.Pawns.Spawn(colony.Pawns.Pawns.All[1].Cell, PawnKindIndex.MiddenHog);
            Assert.That(Draft(colony, hog), Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(hog.Drafted, Is.False);
        }

        // ---- the three ways a draft ends by itself ---------------------------------------------

        [Test]
        public void FourQuietHoursGiveTheColonistBack()
        {
            var colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Draft(colony, pawn);
            int quiet = colony.Pawns.Content.DraftQuietTicks;
            Assert.That(quiet, Is.EqualTo(10_000), "the reference's four hours");

            colony.World.Tick(quiet - 20);
            Assert.That(pawn.Drafted, Is.True, "released early");

            colony.World.Tick(40);
            Assert.That(pawn.Drafted, Is.False, "never released");
            Assert.That(JobOf(pawn), Is.Not.EqualTo(JobIndex.DraftHold));
        }

        [Test]
        public void AnOrderRestartsTheQuietHours()
        {
            var colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Draft(colony, pawn);
            int quiet = colony.Pawns.Content.DraftQuietTicks;

            colony.World.Tick(quiet / 2);
            Assert.That(Move(colony, pawn, CellEast(colony, pawn, 3)), Is.EqualTo(IntentRejection.None));
            colony.World.Tick(quiet / 2 + 200);

            Assert.That(pawn.Drafted, Is.True, "the order did not reset the clock");
        }

        [Test]
        public void AMentalBreakEndsTheDraft()
        {
            var colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Draft(colony, pawn);

            pawn.BreakTicksLeft = 500;
            colony.World.Tick();

            Assert.That(pawn.Drafted, Is.False);
            Assert.That(JobOf(pawn), Is.EqualTo(JobIndex.Wander).Or.EqualTo(JobIndex.Wait));
            Assert.That(Draft(colony, pawn), Is.EqualTo(IntentRejection.NotPermitted), "a broken colonist was drafted");
        }

        [Test]
        public void ExhaustionEndsTheDraftAndTheColonistGoesDown()
        {
            var colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Draft(colony, pawn);
            colony.World.Tick(10);

            pawn.Needs[NeedIndex.Rest] = 0;
            colony.World.Tick(2);

            Assert.That(pawn.Drafted, Is.False);
            Assert.That(JobOf(pawn), Is.EqualTo(JobIndex.Sleep), "held upright at zero rest");
        }

        // ---- moving ------------------------------------------------------------------------

        [Test]
        public void AMoveOrderWalksThereAndHolds()
        {
            var colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Draft(colony, pawn);
            colony.World.Tick(60);
            int target = CellEast(colony, pawn, 6);
            Assume.That(target, Is.GreaterThanOrEqualTo(0));

            Assert.That(Move(colony, pawn, target), Is.EqualTo(IntentRejection.None));
            Assert.That(JobOf(pawn), Is.EqualTo(JobIndex.Goto));
            Assert.That(pawn.CurrentJob!.PlayerForced, Is.True);

            for (int t = 0; t < 2_000 && pawn.Cell != target; t++) colony.World.Tick();
            colony.World.Tick(5);

            Assert.That(pawn.Cell, Is.EqualTo(target), "never arrived");
            Assert.That(JobOf(pawn), Is.EqualTo(JobIndex.DraftHold), "did not hold on arrival");
        }

        [Test]
        public void AnUndraftedColonistCannotBeSentAnywhere()
        {
            var colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Assert.That(Move(colony, pawn, CellEast(colony, pawn, 4)), Is.EqualTo(IntentRejection.NotPermitted));
        }

        [Test]
        public void TwoColonistsSentToOneCellStandApart()
        {
            var colony = Board();
            Pawn a = colony.Pawns.Pawns.All[0];
            Pawn b = colony.Pawns.Pawns.All[1];
            Draft(colony, a);
            Draft(colony, b);
            int target = CellEast(colony, a, 5);

            Move(colony, a, target);
            Move(colony, b, target);
            Assert.That(a.CurrentJob!.TargetCell, Is.EqualTo(target));
            Assert.That(b.CurrentJob!.TargetCell, Is.Not.EqualTo(target), "the second was sent on top of the first");

            CellRef want = Size.FromIndex(target), got = Size.FromIndex(b.CurrentJob!.TargetCell);
            Assert.That(System.Math.Max(System.Math.Abs(want.X - got.X), System.Math.Abs(want.Z - got.Z)),
                Is.LessThanOrEqualTo(JobSystem.SpreadRings), "spread too far from where it was aimed");
        }

        /// <summary>
        /// The snap (design 33 §2d). An order given mid-step lets the colonist land the step she
        /// is on before she turns, so the first cell she reaches after the order is the one she
        /// was already stepping into — however the new order points. Without the kept step it is
        /// the first cell of the new path, which is where the figure snaps back from.
        /// </summary>
        [Test]
        public void AnOrderGivenMidStepLandsTheStepBeforeTurning()
        {
            var colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Draft(colony, pawn);
            colony.World.Tick(60);

            Move(colony, pawn, CellEast(colony, pawn, 8));
            for (int t = 0; t < 400 && !(pawn.HasPath && pawn.MoveProgress > 0); t++) colony.World.Tick();
            Assume.That(pawn.HasPath && pawn.MoveProgress > 0, "never caught her mid-step");

            int from = pawn.Cell;
            int into = pawn.Path[pawn.PathIndex];

            // The opposite way: the new path's first step leads away from `into`.
            Assert.That(Move(colony, pawn, CellEast(colony, pawn, -8)), Is.EqualTo(IntentRejection.None));

            int firstNew = -1;
            for (int t = 0; t < 400 && firstNew < 0; t++)
            {
                if (pawn.Cell != from) firstNew = pawn.Cell;
                else colony.World.Tick();
            }

            Assert.That(firstNew, Is.EqualTo(into), "the step in progress was thrown away");
            Assert.That(pawn.FinishingStepTo, Is.EqualTo(-1), "the kept step outlived its landing");
        }

        /// <summary>
        /// A click names the ground block, and the order means the surface on top of it — even
        /// with a cavern under it. The first version lifted the click with the debug spawn's
        /// column search, which looks down before up, so it found the cavern, found it
        /// unreachable and refused the order: a right-click on any ground over a hollow did
        /// nothing at all.
        /// </summary>
        [Test]
        public void AClickOnGroundOverACavernSendsHerToTheSurface()
        {
            var colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Draft(colony, pawn);
            colony.World.Tick(200);

            int surface = CellEast(colony, pawn, 5);
            CellRef at = Size.FromIndex(surface);
            Assume.That(at.Y, Is.GreaterThanOrEqualTo(3), "no room for a cavern under this board");
            int ground = Size.Index(at.X, at.Z, at.Y - 1);
            int hollow = Size.Index(at.X, at.Z, at.Y - 2);
            Assume.That(colony.Grid.IsSolidTerrain(ground) && colony.Grid.IsSolidTerrain(hollow));

            // Hollow out the block under the ground: a sealed pocket with a floor, which the
            // debug spawn's search would find first.
            colony.Grid.Flags[hollow] &= ~CellFlags.SolidTerrain;
            colony.Pawns.Nav.MarkDirty(hollow);
            colony.RebuildDerived();
            Assume.That(colony.Grid.IsWalkable(hollow), "the pocket is not standable, so it tests nothing");

            Assert.That(Send(colony, new Intent(IntentKind.OrderMove, Size.FromIndex(ground), pawn.Id.Value)),
                Is.EqualTo(IntentRejection.None), "a click on the ground over a hollow was refused");
            Assert.That(pawn.CurrentJob!.TargetCell, Is.EqualTo(surface), "sent somewhere other than the surface clicked");
        }

        /// <summary>
        /// Sent back to the cell she is stepping off, she lands the step and walks back — she does
        /// not hold one cell away from where she was told to stand. Found in review: the handler
        /// compared the order with the cell she was leaving and gave her the hold.
        /// </summary>
        [Test]
        public void SentBackToTheCellSheIsLeavingSheReturnsToIt()
        {
            var colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Draft(colony, pawn);
            colony.World.Tick(60);
            Move(colony, pawn, CellEast(colony, pawn, 8));
            for (int t = 0; t < 400 && !(pawn.HasPath && pawn.MoveProgress > 0); t++) colony.World.Tick();
            Assume.That(pawn.HasPath && pawn.MoveProgress > 0, "never caught her mid-step");

            int leaving = pawn.Cell;
            Assert.That(Move(colony, pawn, leaving), Is.EqualTo(IntentRejection.None));
            for (int t = 0; t < 800 && !(pawn.Cell == leaving && !pawn.HasPath); t++) colony.World.Tick();
            colony.World.Tick(5);

            Assert.That(pawn.Cell, Is.EqualTo(leaving), "held a cell away from where she was sent");
            Assert.That(JobOf(pawn), Is.EqualTo(JobIndex.DraftHold));
        }

        [Test]
        public void AColonistWithNoRestLeftCannotBeDrafted()
        {
            var colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            pawn.Needs[NeedIndex.Rest] = 0;
            Assert.That(Draft(colony, pawn), Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(pawn.Drafted, Is.False);
        }

        /// <summary>
        /// Released mid-step, she lands the step before her next job does anything — whatever that
        /// job is. The kept step is held by the job loop for every driver, not by the walk toil,
        /// because a job that begins by lying down or working where she stands never walks.
        /// </summary>
        [Test]
        public void ReleasedMidStepSheLandsTheStepBeforeTheNextJobActs()
        {
            var colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Draft(colony, pawn);
            colony.World.Tick(60);
            Move(colony, pawn, CellEast(colony, pawn, 8));
            for (int t = 0; t < 400 && !(pawn.HasPath && pawn.MoveProgress > 0); t++) colony.World.Tick();
            Assume.That(pawn.HasPath && pawn.MoveProgress > 0, "never caught her mid-step");

            int from = pawn.Cell;
            int into = pawn.Path[pawn.PathIndex];

            // Spent, so the job she is given the moment she is released is a collapse where she
            // stands — a job with no walk in it at all.
            pawn.Needs[NeedIndex.Rest] = 0;
            Assert.That(Draft(colony, pawn, on: false), Is.EqualTo(IntentRejection.None));

            for (int t = 0; t < 400 && pawn.Cell == from; t++)
            {
                Assert.That(pawn.Asleep, Is.False, "she lay down part way through a step");
                colony.World.Tick();
            }
            Assert.That(pawn.Cell, Is.EqualTo(into), "the step in progress was not landed");
        }

        // ---- the record ----------------------------------------------------------------------

        [Test]
        public void TheDraftIsInTheHashAndSurvivesTheRoundTrip()
        {
            var colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            colony.World.Tick(60);
            ulong undrafted = Hash(colony);
            pawn.Drafted = true;
            pawn.DraftQuietSinceTick = colony.World.CurrentTick;
            Assert.That(Hash(colony), Is.Not.EqualTo(undrafted), "the hash cannot see the draft");
            pawn.Drafted = false;
            Assert.That(Hash(colony), Is.EqualTo(undrafted), "an undrafted colonist hashes differently from before");

            Draft(colony, pawn);
            colony.World.Tick(30);
            var restored = Board();
            restored.Load(colony.Save());

            Pawn back = restored.Pawns.Pawns.Get(pawn.Id)!;
            Assert.That(back.Drafted, Is.True);
            Assert.That(back.DraftQuietSinceTick, Is.EqualTo(pawn.DraftQuietSinceTick));
            Assert.That(Hash(restored), Is.EqualTo(Hash(colony)));
        }

        [Test]
        public void ASaveTakenMidStepResumesIdentically()
        {
            var colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Draft(colony, pawn);
            colony.World.Tick(60);
            Move(colony, pawn, CellEast(colony, pawn, 8));
            for (int t = 0; t < 400 && !(pawn.HasPath && pawn.MoveProgress > 0); t++) colony.World.Tick();
            Move(colony, pawn, CellEast(colony, pawn, -8));
            Assume.That(pawn.FinishingStepTo, Is.GreaterThanOrEqualTo(0), "not saving mid-step");

            var restored = Board();
            restored.Load(colony.Save());
            Assert.That(Hash(restored), Is.EqualTo(Hash(colony)), "differs immediately after loading");

            colony.World.Tick(600);
            restored.World.Tick(600);
            Assert.That(Hash(restored), Is.EqualTo(Hash(colony)), "the worlds parted after the load");
        }

        /// <summary>
        /// A save from before the draft has two fewer job defs. The job table is append-only, so
        /// the missing ones are the newest and start at nought; a save from a newer build, with
        /// more, is still refused.
        /// </summary>
        [Test]
        public void ASaveWithFewerJobDefsLoadsAndOneWithMoreIsRefused()
        {
            var colony = Board();
            int now = colony.Pawns.Content.Jobs.Length;

            colony.Jobs.Load(Section(started: 9, failed: 2, defs: now - 2, completedEach: 3));
            Assert.That(colony.Jobs.JobsStarted, Is.EqualTo(9));
            Assert.That(colony.Jobs.CompletedOf(0), Is.EqualTo(3));
            Assert.That(colony.Jobs.CompletedOf(now - 1), Is.EqualTo(0));

            Assert.Throws<SaveLoadException>(() => colony.Jobs.Load(Section(0, 0, now + 1, 0)));
        }

        static SaveReader Section(int started, int failed, int defs, int completedEach)
        {
            var bytes = new MemoryStream();
            using (var binary = new BinaryWriter(bytes, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                var writer = new SaveWriter(binary);
                writer.Write(started);
                writer.Write(failed);
                writer.Write(defs);
                for (int i = 0; i < defs; i++)
                {
                    writer.Write(completedEach);
                    writer.Write(0);
                }
            }
            bytes.Position = 0;
            return new SaveReader(new BinaryReader(bytes), WorldSave.CurrentFormatVersion);
        }

        [Test]
        public void TheDraftIsPublishedUnderTheNamesTheInterfaceReads()
        {
            // Held to the literals the interface reads; OrderModelTests holds the other side.
            Assert.That(CombatAspects.DraftedName, Is.EqualTo("odyssey.pawn.drafted"));
            Assert.That(CombatAspects.OrderCellName, Is.EqualTo("odyssey.pawn.order.cell"));

            var colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            colony.World.Tick(60);
            var before = colony.World.Views.Current;
            Assert.That(before.TryGetPawnAspect(pawn.Id, CombatAspects.Drafted, out _), Is.False,
                "an undrafted colonist published the draft");

            Draft(colony, pawn);
            Move(colony, pawn, CellEast(colony, pawn, 6));
            var after = colony.World.Views.Current;
            Assert.That(after.TryGetPawnAspect(pawn.Id, CombatAspects.Drafted, out int drafted) && drafted == 1, Is.True);
            Assert.That(after.TryGetPawnAspect(pawn.Id, CombatAspects.OrderCell, out int cell), Is.True);
            Assert.That(cell, Is.EqualTo(pawn.CurrentJob!.TargetCell));
        }

        [Test]
        public void BothOrdersLandWhilePaused()
        {
            Assert.That(PausedIntents.AppliesWhilePaused(IntentKind.SetDrafted), Is.True);
            Assert.That(PausedIntents.AppliesWhilePaused(IntentKind.OrderMove), Is.True);
        }
    }
}
