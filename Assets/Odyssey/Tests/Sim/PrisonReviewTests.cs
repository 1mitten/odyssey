#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;
using static Odyssey.Tests.Sim.PrisonFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The second review of the prisoner line (design 59 §16), one test per fault it found, each
    /// failing on the code before its fix.
    /// </summary>
    public class PrisonReviewTests
    {
        static Pawn Colonist(ColonyWorld colony, int i) => colony.Pawns.Pawns.All[i];

        /// <summary>
        /// §16 #1. The owner sweep remembered what it last saw in three fields nobody saved, so the
        /// first tick after a load always swept again and raised <c>BedOwnershipChanged</c>, which
        /// wakes anybody asleep away from the bed she owns — and the twin that never saved did not.
        /// A load now primes the sweep from what it loaded instead of treating it as a change.
        /// </summary>
        [Test]
        public void ALoadDoesNotSweepTheBedsAsIfTheirPurposesHadChanged()
        {
            ColonyWorld colony = Board();
            BuildCell(colony);
            for (int i = 0; i < 30; i++) colony.World.Tick();

            ColonyWorld restored = Board();
            restored.Load(colony.Save());

            // The control: the colony that never saved has nothing to sweep.
            colony.Construction.SweepBedPurposes();
            Assert.That(colony.Construction.BedOwnershipChanged, Is.False, "the control swept with nothing changed");

            restored.Construction.SweepBedPurposes();
            Assert.That(restored.Construction.BedOwnershipChanged, Is.False,
                "the first sweep after a load took the loaded purposes for a change");

            // And the two go on as one.
            for (int i = 0; i < 400; i++)
            {
                colony.World.Tick();
                restored.World.Tick();
            }
            Assert.That(restored.World.ComputeStateHash(), Is.EqualTo(colony.World.ComputeStateHash()),
                "the loaded colony parted from the one that never saved");
        }

        /// <summary>
        /// §16 #2. Her meal and her sleep were planned as a colonist's walk, which opens doors: a
        /// cell with two doors on one corridor was a route out of one and in at the other, and a
        /// door she held open doubled her own escape risk. Both jobs now go in her own mode.
        /// </summary>
        [Test]
        public void APrisonersMealAndSleepAreWalkedInHerOwnMode()
        {
            ColonyWorld colony = Board();
            Cell cell = BuildCell(colony);
            Pawn prisoner = HeldIn(colony, cell);
            Assume.That(prisoner.OwnMode, Is.Not.EqualTo(TraverseMode.Colonist), "a bandit walks as a bandit");
            colony.Pawns.Items.Spawn(ItemIndex.Meal, cell.Inside, 1);

            var node = new PrisonerNeedsThinkNode();
            var job = prisoner.JobBuffer;
            prisoner.Needs[NeedIndex.Food] = 1;
            prisoner.Needs[NeedIndex.Rest] = colony.Pawns.Content.Needs[NeedIndex.Rest].max;
            Assert.That(node.TryGiveJob(prisoner, colony.Pawns, job), Is.True);
            Assert.That(job.DefIndex, Is.EqualTo(JobIndex.Eat));
            Assert.That(job.Mode, Is.EqualTo(prisoner.OwnMode), "she went for her meal as a colonist");

            prisoner.Needs[NeedIndex.Food] = colony.Pawns.Content.Needs[NeedIndex.Food].max;
            prisoner.Needs[NeedIndex.Rest] = 1;
            Assert.That(node.TryGiveJob(prisoner, colony.Pawns, job), Is.True);
            Assert.That(job.DefIndex, Is.EqualTo(JobIndex.Sleep));
            Assert.That(job.Mode, Is.EqualTo(prisoner.OwnMode), "she went to bed as a colonist");
        }

        /// <summary>
        /// §16 #6. A bed in a walled yard with a gate and no roof is a shackle bed, and a shackled
        /// prisoner walked to it in her own mode, which does not open the gate: she waited where
        /// she stood for good, sent the same walk every think. She is walked there as ToMyCell walks a
        /// prisoner into her cell.
        /// </summary>
        [Test]
        public void AShackledPrisonerIsWalkedThroughTheGateToHerBed()
        {
            ColonyWorld colony = Board();
            CellRef c = Size.FromIndex(Near(colony, 10, 0));
            int x0 = c.X - 1, x1 = c.X + 2, z0 = c.Z - 2, z1 = c.Z + 2;
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                if (x != x0 && x != x1 && z != z0 && z != z1) continue;
                RaiseAt(colony, Size.Index(x, z, c.Y), (x, z) == (x1, c.Z) ? BuildingHandle.Door : BuildingHandle.Wall);
                colony.World.Tick();
            }
            int bed = Size.Index(c.X, c.Z - 1, c.Y);
            RaiseAt(colony, bed, BuildingHandle.Bed);
            colony.World.Tick();
            Assume.That(Send(colony, new Intent(IntentKind.SetBedPurpose, Size.FromIndex(bed), (int)BedPurpose.Prison)),
                Is.EqualTo(IntentRejection.None));
            Assume.That(colony.Construction.Purposes!.IsShackled(bed), Is.True, "an unroofed yard is no cell");

            Pawn prisoner = HeldOn(colony, bed, Size.Index(c.X + 4, c.Z, c.Y));
            Assume.That(colony.Pawns.CanTravel(prisoner, bed, TraverseMode.Bandit), Is.False,
                "the gate does not hold her: the yard is no test of it");
            var job = prisoner.JobBuffer;
            Assert.That(new ShackledThinkNode().TryGiveJob(prisoner, colony.Pawns, job), Is.True);
            Assert.That(job.DefIndex, Is.EqualTo(JobIndex.Goto), "she waited outside the gate");
            Assert.That(job.TargetCell, Is.EqualTo(bed));
            Assert.That(colony.Pawns.CanTravel(prisoner, bed, job.Mode), Is.True,
                "she was sent in a mode the gate holds, and would be sent again for ever");
        }

        /// <summary>
        /// §16 #4. The free prison bed was asked for when the arrest was ordered and not when the
        /// arrester arrived, so a capture that filled the last bed on his way left her a prisoner
        /// with no bed, loose in the colony for good. At the touch the bed is asked again, and with
        /// none she is not taken.
        /// </summary>
        [Test]
        public void AnArrestWithNoBedLeftWhenHeArrivesTakesNobody()
        {
            ColonyWorld colony = Bodiless(Board(colonists: 3));
            colony.World.Tick();
            Cell cell = BuildCell(colony);
            Pawn warden = Colonist(colony, 0), target = Colonist(colony, 1);
            Stand(colony, warden, Near(colony, -15, -15));
            Stand(colony, target, Near(colony, 0, 0));
            int arrested = colony.Incidents.Ledger.Fires(IncidentHandle.Arrested);

            Assert.That(Send(colony, new Intent(IntentKind.OrderArrest, default, warden.Id.Value, target.Id.Value)),
                Is.EqualTo(IntentRejection.None));
            // The last prison bed goes to somebody else while he walks over.
            HeldIn(colony, cell);
            for (int i = 0; i < 4_000 && warden.CurrentJob?.DefIndex == JobIndex.Arrest; i++) colony.World.Tick();

            Assert.That(warden.CurrentJob?.DefIndex, Is.Not.EqualTo(JobIndex.Arrest), "the arrest never ended");
            Assert.That(target.Custody, Is.EqualTo(PawnCustody.Free), "taken with nowhere to put her");
            Assert.That(target.IsColonist, Is.True);
            Assert.That(colony.Incidents.Ledger.Fires(IncidentHandle.Arrested), Is.EqualTo(arrested));
        }

        /// <summary>
        /// §16 #10, found writing the test above. The arrester picked a new place beside her every
        /// tick she moved, and each new destination snapped his step in hand back: after a colonist
        /// going about her day he covered two cells in 400 ticks and the arrest never landed. The
        /// side is chosen again only at a step boundary, as the melee chase does.
        /// </summary>
        [Test]
        public void AnArresterCatchesAColonistWhoWalksOn()
        {
            ColonyWorld colony = Bodiless(Board(colonists: 3));
            colony.World.Tick();
            BuildCell(colony);
            Pawn warden = Colonist(colony, 0), target = Colonist(colony, 1);
            Stand(colony, warden, Near(colony, -15, -15));
            Stand(colony, target, Near(colony, 0, 0));
            target.Mood = 1_000;

            Assert.That(Send(colony, new Intent(IntentKind.OrderArrest, default, warden.Id.Value, target.Id.Value)),
                Is.EqualTo(IntentRejection.None));
            int start = colony.World.CurrentTick;
            for (int i = 0; i < 4_000 && target.Custody == PawnCustody.Free; i++) colony.World.Tick();
            int took = colony.World.CurrentTick - start;
            TestContext.WriteLine($"the arrest landed after {took} ticks");
            Assert.That(target.Custody, Is.Not.EqualTo(PawnCustody.Free), $"not caught in {took} ticks");
            Assert.That(took, Is.LessThan(CatchWithinTicks), "caught, but at a crawl");
        }

        /// <summary>
        /// Measured 2026-09-26, 21 cells apart with her walking on: 2,261 ticks with the step kept, both
        /// walking about a cell a hundred ticks; not caught in 4,000 without, at half that pace.
        /// </summary>
        const int CatchWithinTicks = 3_000;
    }
}
