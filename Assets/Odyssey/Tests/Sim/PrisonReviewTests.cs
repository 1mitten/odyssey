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
    /// The second review of the prisoner line (design 60 §16), one test per fault it found, each
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

        /// <summary>
        /// §16 #3. A doctor walks only to somebody lying still, and the prisoner's tree had no
        /// patient: a raider who surrendered bleeding stood in her cell untended until the blood
        /// loss put her down. She lies down on her own bed now, and is treated standing no longer.
        /// </summary>
        [Test]
        public void ABleedingPrisonerLiesDownAndIsTreatedBeforeSheDrops()
        {
            ColonyWorld colony = Board(colonists: 2);
            colony.World.Tick();
            Cell cell = BuildCell(colony);
            Pawn prisoner = HeldIn(colony, cell);
            foreach (Pawn p in colony.Pawns.Pawns.All)
                if (p.IsColonist) p.WorkPriorities[WorkTypeIndex.Doctor] = 1;
            colony.Pawns.Combat!.Hurt(prisoner, null, 3_000, AfflictionKind.Wound, HitSet.Melee, -1, colony.World.CurrentTick);
            Assume.That(Medical.IsBleeding(prisoner), Is.True, "the wound bleeds");
            Assume.That(prisoner.Downed, Is.False, "and she is on her feet");

            // The doctor fetches supplies and walks round to the cell door: treating by about tick
            // 3,600 on this board, measured 2026-09-26.
            bool laidDown = false;
            for (int i = 0; i < 6_000 && Medical.IsBleeding(prisoner) && !prisoner.Downed; i++)
            {
                colony.World.Tick();
                laidDown |= prisoner.CurrentJob?.DefIndex == JobIndex.Patient;
            }
            Assert.That(laidDown, Is.True, "she never lay down for the doctor");
            Assert.That(prisoner.Downed, Is.False, "the blood loss put her down first");
            Assert.That(Medical.IsBleeding(prisoner), Is.False, "nobody stopped the bleed");
        }

        /// <summary>
        /// §16 #5. The corpse read its side off the dead pawn's kind, past Allegiance, so a raider
        /// who had joined the colony died a bandit: dressed as one and called hostile. The corpse
        /// keeps whether she had joined, through a save.
        /// </summary>
        [Test]
        public void ARecruitDiesOnTheColonysSide()
        {
            ColonyWorld colony = Board();
            colony.World.Tick();
            Pawn raider = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 4, 3));
            Assume.That(Send(colony, new Intent(IntentKind.DebugImprison, default, raider.Id.Value, 0)),
                Is.EqualTo(IntentRejection.None));
            Recruitment.Join(raider, colony.Pawns);
            Assume.That(raider.IsColonist, Is.True, "she joined");

            Corpse corpse = WeaponFixture.Kill(colony, raider);
            Assert.That(corpse.Joined, Is.True);
            colony.World.Tick();
            Assert.That(colony.World.Views.Current.Corpses[0].Flags & PawnFlags.Hostile, Is.EqualTo(PawnFlags.None),
                "she died a bandit");

            ColonyWorld restored = Board();
            restored.Load(colony.Save());
            Assert.That(restored.Pawns.Corpses[0].Joined, Is.True, "the save forgot which side she died on");
            Assert.That(restored.World.ComputeStateHash(), Is.EqualTo(colony.World.ComputeStateHash()));
        }

        /// <summary>
        /// §16 #8. The two-second feed never asked whether she was still there: a prisoner who
        /// walked off mid-meal was fed from where the warden stood. He follows her with the plate
        /// now, and she is fed at his side.
        /// </summary>
        [Test]
        public void AWardenFeedsAPrisonerOnlyAtHerSide()
        {
            ColonyWorld colony = Board(colonists: 1, beds: 0);
            colony.World.Tick();
            int bed = ShackleBed(colony, -8);
            Pawn prisoner = HeldOn(colony, bed, Near(colony, -6, 0));
            Pawn warden = colony.Pawns.Pawns.All[0];
            colony.Pawns.Items.Spawn(ItemIndex.Meal, Near(colony, 3, 3), 4);
            int seek = colony.Pawns.Content.Needs[NeedIndex.Food].seekThreshold;
            prisoner.Needs[NeedIndex.Food] = seek / 4;
            warden.Needs[NeedIndex.Food] = colony.Pawns.Content.Needs[NeedIndex.Food].max;

            bool moved = false;
            int beside = 0, hunger = prisoner.Needs[NeedIndex.Food];
            for (int t = 0; t < 6_000 && prisoner.Needs[NeedIndex.Food] <= hunger; t++)
            {
                colony.World.Tick();
                if (moved || warden.CurrentJob?.DefIndex != JobIndex.FeedPrisoner || Chebyshev(colony, warden, prisoner) > 1)
                    continue;
                // A quarter of the way into the meal (FeedTicks is 120), she walks off.
                if (++beside < 30) continue;
                Stand(colony, prisoner, Near(colony, -6, 5));
                moved = true;
            }
            Assume.That(moved, Is.True, "the warden never reached her");
            Assert.That(prisoner.Needs[NeedIndex.Food], Is.GreaterThan(hunger), "never fed");
            Assert.That(Chebyshev(colony, warden, prisoner), Is.LessThanOrEqualTo(1), "fed from across the room");
        }

        static int Chebyshev(ColonyWorld colony, Pawn a, Pawn b)
        {
            CellRef p = Size.FromIndex(a.Cell), q = Size.FromIndex(b.Cell);
            return p.Y != q.Y ? int.MaxValue : System.Math.Max(System.Math.Abs(p.X - q.X), System.Math.Abs(p.Z - q.Z));
        }

        /// <summary>
        /// §16 H2, the simulation's half: whether a prison bed stands free is published, so the
        /// pane's Arrest can be dim with its reason instead of a press that does nothing.
        /// </summary>
        [Test]
        public void WhetherAPrisonBedStandsFreeIsPublished()
        {
            ColonyWorld colony = Board();
            colony.World.Tick();
            Assert.That(colony.World.Views.Current.PrisonBedFree, Is.False, "no prison bed at all");

            Cell cell = BuildCell(colony);
            colony.World.Tick();
            Assert.That(colony.World.Views.Current.PrisonBedFree, Is.True, "one marked and empty");

            HeldIn(colony, cell);
            colony.World.Tick();
            Assert.That(colony.World.Views.Current.PrisonBedFree, Is.False, "the only one given away");
        }
    }
}
