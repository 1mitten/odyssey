#nullable enable
using System.IO;
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The three break lines and the taxonomy (design 44 §5c, TM5): which break a colonist falls
    /// into from the deepest line she is under, what each one does, how it ends, and that a break
    /// is saved, hashed and seen. Each behaviour is forced — the kind and the counter set on the
    /// pawn — so the test is about the break and not about the dice that start one.
    /// </summary>
    public class MentalBreakTests
    {
        static ColonyWorld Quiet(int colonists = 2)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = colonists;
            scenario.startingFellRadius = 0;
            // About breaks, not about who breaks: no traits, so no seed moves a line.
            scenario.traits = false;
            return ColonyWorld.Build(Size, 7u, scenario, barren: true, wooded: false);
        }

        static void Break(Pawn pawn, int kind, int ticks = 20_000)
        {
            pawn.BreakKind = kind;
            pawn.BreakTicksLeft = ticks;
        }

        static void Fed(ColonyWorld colony)
        {
            foreach (Pawn p in colony.Pawns.Pawns.All)
                for (int n = 0; n < NeedIndex.Count; n++) p.Needs[n] = 800;
        }

        static void RaiseWall(ColonyWorld colony, int cell)
        {
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), BuildingHandle.Wall, StuffHandle.Wood, 0),
                Is.EqualTo(IntentRejection.None), $"could not order a wall at {Size.FromIndex(cell)}");
            Assert.That(colony.Construction.Raise(colony.Pawns, cell), Is.True);
        }

        // ---- the choice -------------------------------------------------------------------

        [Test]
        public void TheDeepestLineSheIsUnderChoosesTheTier()
        {
            ColonyWorld colony = Quiet(colonists: 1);
            colony.World.Tick();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            PawnContext ctx = colony.Pawns;

            for (int seed = 0; seed < 200; seed++)
            {
                var rng = new DeterministicRandom((uint)seed + 1u);
                int extreme = MentalBreaks.Choose(pawn, ctx, BreakHandle.Extreme, ref rng);
                Assert.That(ctx.Content.Breaks[extreme].tier, Is.EqualTo(BreakHandle.Extreme),
                    "berserk requires nothing, so an extreme tier never falls through");

                rng = new DeterministicRandom((uint)seed + 1u);
                int minor = MentalBreaks.Choose(pawn, ctx, BreakHandle.Minor, ref rng);
                Assert.That(minor, Is.EqualTo(BreakHandle.Wander).Or.EqualTo(BreakHandle.Sulk));
            }
        }

        [Test]
        public void AMajorBreakWithNothingToEatOrStrikeFallsThroughToAMinorOne()
        {
            // The colony's meals are all it has to eat and nothing is built: take the meals away and
            // neither major break has anything to do, so the pick falls to the minor tier.
            ColonyWorld colony = Quiet(colonists: 1);
            colony.World.Tick();
            foreach (var item in colony.Pawns.Items.Items.ToList())
                if (colony.Pawns.Content.Items[item.DefIndex].nutrition > 0) colony.Pawns.Items.Despawn(item);
            Pawn pawn = colony.Pawns.Pawns.All[0];

            Assert.That(MentalBreaks.Eligible(pawn, colony.Pawns, BreakHandle.Binge), Is.False);
            Assert.That(MentalBreaks.Eligible(pawn, colony.Pawns, BreakHandle.Tantrum), Is.False);
            var rng = new DeterministicRandom(99);
            int kind = MentalBreaks.Choose(pawn, colony.Pawns, BreakHandle.Major, ref rng);
            Assert.That(colony.Pawns.Content.Breaks[kind].tier, Is.EqualTo(BreakHandle.Minor));
        }

        [Test]
        public void TheLinesAndClocksAreTheReferences()
        {
            var colony = Colony.Build();
            MoodDef mood = colony.Ctx.Content.Mood;
            Assert.That(mood.breakMtbTicks, Is.EqualTo(4 * 60_000), "four days below the minor line (a-19)");
            Assert.That(mood.majorMtbTicks, Is.EqualTo(48_000), "0.8 days below the major");
            Assert.That(mood.extremeMtbTicks, Is.EqualTo(30_000), "half a day below the extreme");
            Assert.That(colony.Ctx.Content.Breaks.Select(b => b.defName),
                Is.EqualTo(new[] { "Break_Wander", "Break_Sulk", "Break_Binge", "Break_Tantrum", "Break_Berserk" }),
                "the content's order is BreakHandle's, which a save rides on");
        }

        // ---- what each break does -----------------------------------------------------------

        [Test]
        public void ASulkerWithNoBedStandsWhereSheIs()
        {
            ColonyWorld colony = Quiet(colonists: 1);
            colony.World.Tick();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Break(pawn, BreakHandle.Sulk);
            colony.Jobs.EndJob(pawn, JobStatus.Failed);
            colony.World.Tick();

            Assert.That(pawn.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Wait), "she does nothing, which is the sulk");
        }

        [Test]
        public void ASulkerWalksToHerOwnBed()
        {
            ColonyWorld colony = Quiet(colonists: 1);
            colony.World.Tick();
            Pawn pawn = colony.Pawns.Pawns.All[0];

            int head = Near(colony, 4, 0);
            Assume.That(colony.Construction.Place(Size.FromIndex(head), BuildingHandle.Bed, StuffHandle.Wood, 0),
                Is.EqualTo(IntentRejection.None));
            Assume.That(colony.Construction.Raise(colony.Pawns, head), Is.True);
            Assume.That(colony.Construction.AssignOwner(Size.FromIndex(head), pawn.Id.Value), Is.EqualTo(IntentRejection.None));
            colony.World.Tick();

            Break(pawn, BreakHandle.Sulk, 40_000);
            colony.Jobs.EndJob(pawn, JobStatus.Failed);
            for (int i = 0; i < 3_000 && pawn.Cell != head; i++) { Fed(colony); colony.World.Tick(); }
            Assert.That(pawn.Cell, Is.EqualTo(head), "she went to her own bed");
            for (int i = 0; i < 10; i++) { Fed(colony); colony.World.Tick(); }
            Assert.That(pawn.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Wait), "and stays there doing nothing");
        }

        [Test]
        public void ABingerEatsWhatSheCanReachWhateverHerHunger()
        {
            ColonyWorld colony = Quiet(colonists: 1);
            colony.World.Tick();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            pawn.Needs[NeedIndex.Food] = 1_000;
            Break(pawn, BreakHandle.Binge);
            colony.Jobs.EndJob(pawn, JobStatus.Failed);
            colony.World.Tick();

            Assert.That(pawn.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Eat), "full, and eating anyway");

            // The control: the same colonist, not in a break, full, does not go to eat.
            Break(pawn, BreakHandle.Wander, 0);
            colony.Jobs.EndJob(pawn, JobStatus.Failed);
            colony.World.Tick();
            Assert.That(pawn.CurrentJob?.DefIndex, Is.Not.EqualTo(JobIndex.Eat));
        }

        [Test]
        public void ATantrumStrikesTheNearestBuildingInReach()
        {
            ColonyWorld colony = Quiet(colonists: 1);
            colony.World.Tick();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            int wall = Near(colony, 3, 0);
            RaiseWall(colony, wall);

            Break(pawn, BreakHandle.Tantrum);
            colony.Jobs.EndJob(pawn, JobStatus.Failed);
            colony.World.Tick();

            Assert.That(pawn.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee));
            Assert.That(pawn.CombatTarget, Is.EqualTo(0), "a building, which is a job with no pawn target");
            Assert.That(pawn.Drafted, Is.False);
        }

        [Test]
        public void ABerserkerStrikesTheNearestPawnAndABlowDownsRatherThanKills()
        {
            ColonyWorld colony = Quiet(colonists: 2);
            colony.World.Tick();
            Pawn berserker = colony.Pawns.Pawns.All[0], other = colony.Pawns.Pawns.All[1];
            Stand(colony, berserker, Near(colony, 0, 0));
            Stand(colony, other, Near(colony, 2, 0));

            Break(berserker, BreakHandle.Berserk);
            colony.Jobs.EndJob(berserker, JobStatus.Failed);
            colony.World.Tick();

            Assert.That(berserker.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee));
            Assert.That(berserker.CombatTarget, Is.EqualTo(other.Id.Value));

            for (int i = 0; i < 20_000 && !other.Downed && !berserker.Downed; i++) { Fed(colony); colony.World.Tick(); }
            Assert.That(other.Downed || berserker.Downed, Is.True, "somebody went down");
            Assert.That(Melee.IsDead(other) || Melee.IsDead(berserker), Is.False,
                "an unordered fight ends in downs, never deaths (design 33)");
        }

        [Test]
        public void ABerserkerDownedIsNoLongerInABreakAndGetsNoCatharsis()
        {
            ColonyWorld colony = Quiet(colonists: 1);
            colony.World.Tick();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Break(pawn, BreakHandle.Berserk);
            int memories = pawn.Memories.Count;

            colony.Pawns.Combat!.Down(pawn, null, -1, colony.World.CurrentTick);

            Assert.That(pawn.IsBroken, Is.False);
            Assert.That(pawn.BreakKind, Is.EqualTo(BreakHandle.Wander));
            Assert.That(pawn.Memories.Count, Is.EqualTo(memories), "a break cut short did not run its course");
        }

        [Test]
        public void ABreakThatRunsItsCourseEndsInCatharsis()
        {
            ColonyWorld colony = Quiet(colonists: 1);
            colony.World.Tick();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            pawn.Memories.Clear();
            Break(pawn, BreakHandle.Sulk, 5);

            for (int i = 0; i < 10; i++) colony.World.Tick();

            Assert.That(pawn.IsBroken, Is.False);
            Assert.That(pawn.BreakKind, Is.EqualTo(BreakHandle.Wander), "the kind is cleared with the break");
            Assert.That(pawn.Memories.Any(m => m.ThoughtIndex == ThoughtIndex.Catharsis), Is.True);
        }

        [Test]
        public void ABreaksOwnJobsAreKeptAndAnythingElseIsFailed()
        {
            PawnContent content = Odyssey.Sim.Defs.ContentPack.Pawns();
            var pawn = new Pawn(new PawnId(1), -1, content);
            var job = new Job();

            pawn.BreakKind = BreakHandle.Binge;
            job.Reset(JobIndex.Eat);
            Assert.That(MentalBreaks.IsBreakJob(pawn, job, content), Is.True);
            job.Reset(JobIndex.Wander);
            Assert.That(MentalBreaks.IsBreakJob(pawn, job, content), Is.True, "every break may fall back to wandering");
            job.Reset(JobIndex.Mine);
            Assert.That(MentalBreaks.IsBreakJob(pawn, job, content), Is.False);

            pawn.BreakKind = BreakHandle.Sulk;
            job.Reset(JobIndex.Eat);
            Assert.That(MentalBreaks.IsBreakJob(pawn, job, content), Is.False, "a sulker does not eat her way out of it");
        }

        // ---- seen, saved and hashed -------------------------------------------------------

        [Test]
        public void ABreakIsRecordedOnTheEventsPanelAndPublished()
        {
            ColonyWorld colony = Quiet(colonists: 1);
            colony.World.Tick();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            MoodDef shipped = colony.Pawns.Content.Mood;
            colony.Pawns.Content.Mood = new MoodDef
            {
                baseMood = shipped.baseMood, max = shipped.max, risePerInterval = shipped.risePerInterval,
                fallPerInterval = shipped.fallPerInterval, breakThreshold = shipped.breakThreshold,
                strainMargin = shipped.strainMargin, breakMtbTicks = 1, majorMtbTicks = 1, extremeMtbTicks = 1,
            };
            int before = colony.Pawns.Incidents!.Ledger.Fires(IncidentHandle.MentalBreak);

            for (int i = 0; i < 2_000 && !pawn.IsBroken; i++)
            {
                pawn.Needs[NeedIndex.Food] = 60;
                pawn.Needs[NeedIndex.Joy] = 0;
                pawn.Mood = 100;
                colony.World.Tick();
            }

            Assert.That(pawn.IsBroken, Is.True);
            Assert.That(colony.Pawns.Incidents.Ledger.Fires(IncidentHandle.MentalBreak), Is.EqualTo(before + 1),
                "a break is a visible event");
            colony.World.Tick();
            WorldSnapshot frame = colony.World.Views.Current;
            Assert.That(frame.TryGetPawnAspect(pawn.Id, MindAspects.Band, out int band), Is.True);
            Assert.That(band, Is.EqualTo(MoodBand.Broken));
            Assert.That(frame.TryGetPawnAspect(pawn.Id, MindAspects.Break, out int kind), Is.True);
            Assert.That(kind, Is.EqualTo(pawn.BreakKind));
        }

        [Test]
        public void ABreakSurvivesASaveTakenInTheMiddleOfIt()
        {
            ColonyWorld colony = Quiet(colonists: 1);
            colony.World.Tick();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Break(pawn, BreakHandle.Sulk, 9_000);
            colony.Jobs.EndJob(pawn, JobStatus.Failed);
            colony.World.Tick(5);
            ulong hash = colony.World.ComputeStateHash().Value;

            using var stream = new MemoryStream();
            colony.Save(stream);
            stream.Position = 0;
            ColonyWorld restored = Quiet(colonists: 1);
            restored.Load(stream);

            Pawn back = restored.Pawns.Pawns.All[0];
            Assert.That(back.BreakKind, Is.EqualTo(BreakHandle.Sulk));
            Assert.That(back.BreakTicksLeft, Is.EqualTo(pawn.BreakTicksLeft));
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(hash));

            // And on: both tick to the same place.
            colony.World.Tick(100);
            restored.World.Tick(100);
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value));
        }

        [Test]
        public void TheHashSeesWhichBreak()
        {
            ColonyWorld colony = Quiet(colonists: 1);
            colony.World.Tick();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Break(pawn, BreakHandle.Wander, 100);
            ulong wander = colony.World.ComputeStateHash().Value;
            pawn.BreakKind = BreakHandle.Tantrum;
            Assert.That(colony.World.ComputeStateHash().Value, Is.Not.EqualTo(wander),
                "two colonists in different breaks are different states");
        }
    }
}
