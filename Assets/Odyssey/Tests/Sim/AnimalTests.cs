#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Animals: a pawn with a kind (design 29). What is asserted here is the MVP the owner asked
    /// for — spawn, wander, draw, click — minus the drawing and the clicking, which are Unity's
    /// and the HUD's to prove: an animal exists, walks the graph under its species' rules, is
    /// left alone by every system that is about being a person, survives a save, and moves the
    /// hash.
    ///
    /// <para>Every test that claims a difference between a person and an animal, or between a
    /// hog and a rat, carries its control: the person or the rat doing the thing the hog is
    /// refused.</para>
    /// </summary>
    public class AnimalTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 16);

        static ColonyWorld Board(uint seed = 1u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 1;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: false);
        }

        static Pawn TheColonist(ColonyWorld colony) => colony.Pawns.Pawns.All[0];

        static ulong Hash(ColonyWorld colony) => colony.World.ComputeStateHash().Value;

        /// <summary>A walkable cell a few cells from the colonist, so an animal never spawns in her.</summary>
        static int GroundNear(ColonyWorld colony, int dx, int dz)
        {
            CellRef start = Size.FromIndex(TheColonist(colony).Cell);
            int cell = Size.Index(start.X + dx, start.Z + dz, start.Y);
            Assume.That(colony.Grid.IsWalkable(cell), Is.True, "the fixture wants open ground here");
            return cell;
        }

        // ---- the content ----------------------------------------------------------------

        [Test]
        public void TheSpeciesTableArrivesWhole()
        {
            PawnContent content = ContentPack.Pawns();

            Assert.That(content.Kinds, Has.Length.EqualTo(PawnKindIndex.Count));
            Assert.That(content.Kinds[PawnKindIndex.Colonist].defName, Is.EqualTo("PawnKind_Colonist"));
            Assert.That(content.Kinds[PawnKindIndex.MiddenHog].defName, Is.EqualTo("PawnKind_MiddenHog"));
            Assert.That(content.Kinds[PawnKindIndex.DuctRat].defName, Is.EqualTo("PawnKind_DuctRat"));

            Assert.That(content.SpeciesOf(PawnKindIndex.Colonist).person, Is.True, "kind 0 is a person");
            Assert.That(content.SpeciesOf(PawnKindIndex.MiddenHog).person, Is.False);
            Assert.That(content.SpeciesOf(PawnKindIndex.DuctRat).person, Is.False);

            // Design 29 §4: rats climb anything, hogs never take a ladder.
            Assert.That(content.SpeciesOf(PawnKindIndex.MiddenHog).traverseMode, Is.EqualTo(TraverseMode.Animal));
            Assert.That(content.SpeciesOf(PawnKindIndex.DuctRat).traverseMode, Is.EqualTo(TraverseMode.Colonist));

            // And the colonist's own kind is still reachable by the name every needs rule uses.
            Assert.That(content.Kind, Is.SameAs(content.Kinds[0]));
        }

        [Test]
        public void AContentSetBuiltInCodeIsAPersonAndNothingElse()
        {
            // Every fixture that builds a PawnContent by hand rather than from Defs must keep
            // working, and its one pawn must be a colonist.
            var content = new PawnContent();
            var pawn = new Pawn(new PawnId(1), 0, content);
            Assert.That(pawn.IsPerson, Is.True);
            Assert.That(pawn.Mode, Is.EqualTo(TraverseMode.Colonist));
        }

        // ---- spawning ---------------------------------------------------------------------

        [Test]
        public void AnAnimalSpawnsAsItsKindAndACOlonistAsBefore()
        {
            ColonyWorld colony = Board();
            Pawn hog = colony.Pawns.Pawns.Spawn(GroundNear(colony, 3, 0), PawnKindIndex.MiddenHog);
            Pawn rat = colony.Pawns.Pawns.Spawn(GroundNear(colony, -3, 0), PawnKindIndex.DuctRat);
            Pawn person = colony.Pawns.Pawns.Spawn(GroundNear(colony, 0, 3));

            Assert.That(hog.Kind, Is.EqualTo(PawnKindIndex.MiddenHog));
            Assert.That(hog.IsPerson, Is.False);
            Assert.That(hog.Species.defName, Is.EqualTo("Species_MiddenHog"));
            Assert.That(rat.Species.defName, Is.EqualTo("Species_DuctRat"));
            Assert.That(person.Kind, Is.EqualTo(PawnKindIndex.Colonist));
            Assert.That(person.IsPerson, Is.True);
        }

        [Test]
        public void TheDebugMenuSpawnsAHogByKindAndRefusesAKindThatDoesNotExist()
        {
            ColonyWorld colony = Board();
            int before = colony.Pawns.Pawns.Count;
            CellRef at = Size.FromIndex(GroundNear(colony, 4, 4));

            colony.World.Intents.Submit(new Intent(IntentKind.SpawnPawn, at, PawnKindIndex.MiddenHog));
            colony.World.Intents.Submit(new Intent(IntentKind.SpawnPawn, at, PawnKindIndex.Count));
            colony.World.Intents.Submit(new Intent(IntentKind.SpawnPawn, at, -1));
            colony.World.Tick();

            Assert.That(colony.Pawns.Pawns.Count, Is.EqualTo(before + 1), "one hog, and neither of the two unknown kinds");
            Assert.That(colony.Pawns.Pawns.All[before].Kind, Is.EqualTo(PawnKindIndex.MiddenHog));
            Assert.That(colony.World.Intents.Rejected, Has.Count.EqualTo(2));
            Assert.That(colony.World.Intents.Rejected[0].Reason, Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(colony.World.Intents.Rejected[1].Reason, Is.EqualTo(IntentRejection.NotPermitted));
        }

        [Test]
        public void TheDebugMenuWithNoKindStillSpawnsAColonist()
        {
            // The control on the intent: nothing that sends SpawnPawn today says a kind, and it
            // must go on making the colonist it always made.
            ColonyWorld colony = Board();
            int before = colony.Pawns.Pawns.Count;
            colony.World.Intents.Submit(new Intent(IntentKind.SpawnPawn, Size.FromIndex(GroundNear(colony, 4, 4))));
            colony.World.Tick();

            Assert.That(colony.Pawns.Pawns.Count, Is.EqualTo(before + 1));
            Assert.That(colony.Pawns.Pawns.All[before].IsPerson, Is.True);
        }

        // ---- what an animal does not have -------------------------------------------------

        [Test]
        public void AnAnimalHasNoNeedsFallNoSkillsAndNoWork()
        {
            ColonyWorld colony = Board();
            Pawn hog = colony.Pawns.Pawns.Spawn(GroundNear(colony, 3, 0), PawnKindIndex.MiddenHog);
            Pawn person = TheColonist(colony);
            int[] hogNeedsAtStart = (int[])hog.Needs.Clone();
            int[] personNeedsAtStart = (int[])person.Needs.Clone();

            var hogJobs = new HashSet<int>();
            for (int tick = 0; tick < 3_000; tick++)
            {
                colony.World.Tick();
                if (hog.CurrentJob != null) hogJobs.Add(hog.CurrentJob.DefIndex);
            }

            Assert.That(hog.Needs, Is.EqualTo(hogNeedsAtStart), "an animal's needs never move (design 29 §2)");
            Assert.That(person.Needs, Is.Not.EqualTo(personNeedsAtStart), "the control: a person's do");

            for (int s = 0; s < hog.Skills.Length; s++)
            {
                Assert.That(hog.Skills[s], Is.Zero, "an animal rolls no starting skill");
                Assert.That(hog.Passions[s], Is.Zero, "and no passion");
            }
            Assert.That(person.Skills, Has.Some.GreaterThan(0), "the control: the colonist rolled hers on tick one");

            Assert.That(hogJobs, Is.SubsetOf(new[] { JobIndex.Wander, JobIndex.Wait }),
                "an animal's whole mind is a leg or a rest; it never sees a work giver");
        }

        // ---- the animal mind --------------------------------------------------------------

        [Test]
        public void AnAnimalWandersAndRests()
        {
            ColonyWorld colony = Board();
            Pawn hog = colony.Pawns.Pawns.Spawn(GroundNear(colony, 3, 0), PawnKindIndex.MiddenHog);
            int radius = hog.Species.wanderRadius;

            var visited = new HashSet<int>();
            int legs = 0, rests = 0;
            int lastJobStart = -1;
            int legFrom = hog.Cell;
            for (int tick = 0; tick < 6_000; tick++)
            {
                colony.World.Tick();
                visited.Add(hog.Cell);

                Job? job = hog.CurrentJob;
                if (job == null || hog.JobStartTick == lastJobStart) continue;
                lastJobStart = hog.JobStartTick;
                if (job.DefIndex == JobIndex.Wander)
                {
                    legs++;
                    CellRef from = Size.FromIndex(hog.Cell), to = Size.FromIndex(job.TargetCell);
                    Assert.That(System.Math.Abs(to.X - from.X), Is.LessThanOrEqualTo(radius), "a leg stays inside the species' radius");
                    Assert.That(System.Math.Abs(to.Z - from.Z), Is.LessThanOrEqualTo(radius));
                    Assert.That(job.Mode, Is.EqualTo(TraverseMode.Animal), "and is walked under the species' mode");
                    legFrom = hog.Cell;
                }
                else if (job.DefIndex == JobIndex.Wait)
                {
                    rests++;
                    Assert.That(job.WorkTicks, Is.InRange(hog.Species.restTicksMin, hog.Species.restTicksMax),
                        "a rest is jittered between the species' two bounds");
                }
            }

            Assert.That(visited.Count, Is.GreaterThan(3), "a hog left alone for a hundred seconds went somewhere");
            Assert.That(legs, Is.GreaterThan(0), "it walked");
            Assert.That(rests, Is.GreaterThan(0), "and it rested");
            Assert.That(legs, Is.LessThan(rests * 3), "and it did not pace without pause");
            Assert.That(colony.Grid.IsWalkable(hog.Cell), Is.True, "and it is standing somewhere it can stand");
            _ = legFrom;
        }

        [Test]
        public void ARestingAnimalCostsNoPathRequest()
        {
            // Design 29 §3: the mind scales with animals between jobs. Over a whole day one hog
            // starts on the order of a hundred jobs, not a thousand — the rest bounds cap it.
            ColonyWorld colony = Board();
            Pawn hog = colony.Pawns.Pawns.Spawn(GroundNear(colony, 3, 0), PawnKindIndex.MiddenHog);

            int starts = 0;
            int lastJobStart = -1;
            for (int tick = 0; tick < 60_000; tick++)
            {
                colony.World.Tick();
                if (hog.CurrentJob != null && hog.JobStartTick != lastJobStart)
                {
                    lastJobStart = hog.JobStartTick;
                    starts++;
                }
            }

            // The fastest an animal can cycle is a leg every think with the shortest rest between:
            // a day over restTicksMin, times a generous factor for the legs in between.
            int ceiling = 60_000 / hog.Species.restTicksMin * 3;
            Assert.That(starts, Is.InRange(20, ceiling), $"{starts} job starts in a day");
        }

        // ---- the third dimension ----------------------------------------------------------

        /// <summary>
        /// <b>Rats climb, hogs do not</b> (design 29 §4). The ladder fixture is
        /// <c>LadderTests</c>': a shaft with a landing beside it, reachable to a colonist once the
        /// ladder is up. The control is the rat; the hog is refused by the mask the graph already
        /// carried.
        /// </summary>
        [Test]
        public void ARatClimbsALadderAndAHogIsRefusedAtTheFoot()
        {
            ColonyWorld colony = Board();
            LadderTests.AShaftWithALandingBesideIt(colony, out int ground, out int shaft, out int landing);
            Assume.That(colony.Grid.IsWalkable(landing), Is.True);

            Pawn hog = colony.Pawns.Pawns.Spawn(GroundNear(colony, 3, 3), PawnKindIndex.MiddenHog);
            Pawn rat = colony.Pawns.Pawns.Spawn(GroundNear(colony, -3, 3), PawnKindIndex.DuctRat);

            Assert.That(colony.Pawns.Reachable(rat, landing, rat.Mode), Is.False, "the control: no ladder, no way up for anybody");
            Assert.That(colony.Pawns.Reachable(hog, landing, hog.Mode), Is.False);

            Assert.That(colony.Construction.Place(Size.FromIndex(ground), BuildingHandle.Ladder, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.None));
            colony.Construction.Raise(colony.Pawns, ground);
            colony.World.Tick();

            Assert.That(colony.Pawns.Reachable(rat, landing, rat.Mode), Is.True, "a rat climbs anything");
            Assert.That(colony.Pawns.Reachable(hog, landing, hog.Mode), Is.False, "a hog never takes a ladder");
            Assert.That(colony.Pawns.Reachable(hog, landing, TraverseMode.Colonist), Is.True,
                "and it is the mode that refuses it, not the board");
            _ = shaft;
        }

        // ---- pace -------------------------------------------------------------------------

        [Test]
        public void AColonistWalksAsSheDidAndAHogAmbles()
        {
            ColonyWorld colony = Board();
            Pawn person = TheColonist(colony);
            Pawn hog = colony.Pawns.Pawns.Spawn(GroundNear(colony, 3, 0), PawnKindIndex.MiddenHog);
            Pawn rat = colony.Pawns.Pawns.Spawn(GroundNear(colony, -3, 0), PawnKindIndex.DuctRat);

            int standard = person.Content.Movement.movePerTick * Rates.Scale;
            Assert.That(person.MoveRatePerMille(),
                Is.EqualTo(standard * person.InnatePacePerMille() / 1_000 * person.ConditionPerMille() / 1_000),
                "the species factor is exact for a person: no colonist's speed moved");
            Assert.That(hog.MoveRatePerMille(),
                Is.EqualTo(standard * hog.InnatePacePerMille() / 1_000 * hog.ConditionPerMille() / 1_000 * 700 / 1_000));
            Assert.That(rat.MoveRatePerMille(),
                Is.EqualTo(standard * rat.InnatePacePerMille() / 1_000 * rat.ConditionPerMille() / 1_000 * 900 / 1_000));
        }

        // ---- the hash and the save ----------------------------------------------------------

        [Test]
        public void TheHashSeesTheKind()
        {
            ColonyWorld a = Board(), b = Board(), c = Board();
            int cell = GroundNear(a, 3, 0);
            a.Pawns.Pawns.Spawn(cell, PawnKindIndex.MiddenHog);
            b.Pawns.Pawns.Spawn(cell, PawnKindIndex.Colonist);
            c.Pawns.Pawns.Spawn(cell, PawnKindIndex.MiddenHog);

            Assert.That(Hash(a), Is.Not.EqualTo(Hash(b)), "a hog and a colonist on the same cell are different worlds");
            Assert.That(Hash(a), Is.EqualTo(Hash(c)), "the control: two hogs on the same cell are the same world");
        }

        [Test]
        public void TwoRunsWithAnimalsAgree()
        {
            ColonyWorld a = Board(), b = Board();
            foreach (ColonyWorld colony in new[] { a, b })
            {
                for (int i = 0; i < 3; i++)
                {
                    colony.Pawns.Pawns.Spawn(GroundNear(colony, 3 + i, 2), PawnKindIndex.MiddenHog);
                    colony.Pawns.Pawns.Spawn(GroundNear(colony, -3 - i, 2), PawnKindIndex.DuctRat);
                }
            }
            a.World.Tick(2_000);
            b.World.Tick(2_000);
            Assert.That(Hash(a), Is.EqualTo(Hash(b)));
        }

        [Test]
        public void AnAnimalSurvivesASaveAndResumesIdentically()
        {
            ColonyWorld original = Board();
            original.Pawns.Pawns.Spawn(GroundNear(original, 3, 0), PawnKindIndex.MiddenHog);
            original.Pawns.Pawns.Spawn(GroundNear(original, -3, 0), PawnKindIndex.DuctRat);
            original.World.Tick(1_000);
            byte[] bytes = original.Save();

            ColonyWorld restored = Board();
            var header = restored.Load(bytes);
            Assert.That(header.SkippedSections, Is.Empty, "the kind section was written and read");
            Assert.That(Hash(restored), Is.EqualTo(Hash(original)), "immediately after loading");

            var kinds = new List<int>();
            foreach (Pawn pawn in restored.Pawns.Pawns.All) kinds.Add(pawn.Kind);
            Assert.That(kinds, Is.EqualTo(new[] { PawnKindIndex.Colonist, PawnKindIndex.MiddenHog, PawnKindIndex.DuctRat }));

            original.World.Tick(1_500);
            restored.World.Tick(1_500);
            Assert.That(Hash(restored), Is.EqualTo(Hash(original)), "and after both have run on");
        }

        [Test]
        public void ASaveFromBeforeAnimalsReadsEveryPawnAsAColonist()
        {
            // A file with no kind section (design 29 §6): written with every component but that
            // one, which is exactly the file a pre-animals writer produced, since sections are
            // keyed and skippable in both directions. The hog is there to prove the section is
            // what carries the kind: without it, even a hog comes back as the colonist a
            // pre-animals reader would have made of it.
            ColonyWorld original = Board();
            original.Pawns.Pawns.Spawn(GroundNear(original, 3, 0), PawnKindIndex.MiddenHog);
            original.World.Tick(500);

            var withoutKinds = new List<Odyssey.Sim.Saving.ISaveable>();
            foreach (var component in original.SaveComponents)
                if (component is not Odyssey.Sim.Saving.PawnKindSection) withoutKinds.Add(component);
            Assume.That(withoutKinds.Count, Is.EqualTo(original.SaveComponents.Count - 1), "the section is in the list");

            byte[] bytes;
            using (var stream = new System.IO.MemoryStream())
            {
                Odyssey.Sim.Saving.WorldSave.Save(original.World, stream, withoutKinds);
                bytes = stream.ToArray();
            }

            ColonyWorld restored = Board();
            var header = restored.Load(bytes);
            Assert.That(header.SkippedSections, Is.Empty, "nothing in the file is unknown to this build");
            Assert.That(restored.Pawns.Pawns.Count, Is.EqualTo(2));
            foreach (Pawn pawn in restored.Pawns.Pawns.All)
                Assert.That(pawn.IsPerson, Is.True, "every pawn from before animals is a colonist");
        }

        // ---- the published view -------------------------------------------------------------

        [Test]
        public void ThePublishedViewCarriesTheKindAndAnAnimalPublishesNoSkills()
        {
            ColonyWorld colony = Board();
            Pawn hog = colony.Pawns.Pawns.Spawn(GroundNear(colony, 3, 0), PawnKindIndex.MiddenHog);
            colony.World.Tick();

            WorldSnapshot snapshot = colony.World.Views.Current;
            PawnView? hogView = null, personView = null;
            foreach (PawnView view in snapshot.Pawns)
            {
                if (view.Id.Value == hog.Id.Value) hogView = view;
                if (view.Id.Value == TheColonist(colony).Id.Value) personView = view;
            }
            Assert.That(hogView, Is.Not.Null);
            Assert.That(hogView!.Value.Kind, Is.EqualTo(PawnKindIndex.MiddenHog));
            Assert.That(personView!.Value.Kind, Is.EqualTo(PawnKindIndex.Colonist));

            int hogAspects = 0, personAspects = 0;
            foreach (PawnAspect aspect in snapshot.PawnAspects)
            {
                if (aspect.Pawn.Value == hog.Id.Value) hogAspects++;
                if (aspect.Pawn.Value == TheColonist(colony).Id.Value) personAspects++;
            }
            Assert.That(hogAspects, Is.EqualTo(1), "the pace, and nothing else (design 29 §2)");
            Assert.That(personAspects, Is.GreaterThan(hogAspects), "the control: a colonist publishes her skills, work and day");
        }

        // ---- the gate -----------------------------------------------------------------------

        /// <summary>
        /// The plan's gate for AN2: twenty animals on the played board for ten days, on three
        /// seeds, and every one of them standing somewhere it can stand at the end, having done
        /// nothing but walk and rest.
        /// </summary>
        [Test, Category("Long")]
        [TestCase(1u)]
        [TestCase(2u)]
        [TestCase(3u)]
        public void TwentyAnimalsSurviveTenDays(uint seed)
        {
            ScenarioDef scenario = ScenarioDef.Playtest();
            ColonyWorld colony = ColonyWorld.Build(new GridSize(120, 120, 16), seed, scenario, barren: false, wooded: true);
            CellRef start = colony.Start;
            var animals = new List<Pawn>();
            for (int i = 0; i < 20; i++)
            {
                int cell = colony.Grid.NearestWalkableInColumn(start.X + (i % 5) * 2 - 4, start.Z + (i / 5) * 2 - 4, start.Y);
                Assume.That(cell, Is.GreaterThanOrEqualTo(0));
                animals.Add(colony.Pawns.Pawns.Spawn(cell, i % 2 == 0 ? PawnKindIndex.MiddenHog : PawnKindIndex.DuctRat));
            }

            var moved = new int[animals.Count];
            var lastCell = new int[animals.Count];
            for (int i = 0; i < animals.Count; i++) lastCell[i] = animals[i].Cell;

            const int Day = 60_000;
            for (int tick = 0; tick < 10 * Day; tick++)
            {
                colony.World.Tick();
                if (tick % 100 != 0) continue;
                for (int i = 0; i < animals.Count; i++)
                {
                    Pawn animal = animals[i];
                    if (animal.Cell != lastCell[i]) { moved[i]++; lastCell[i] = animal.Cell; }
                    Job? job = animal.CurrentJob;
                    if (job != null)
                        Assert.That(job.DefIndex, Is.EqualTo(JobIndex.Wander).Or.EqualTo(JobIndex.Wait),
                            $"animal {i} on tick {tick} took job {job.DefIndex}");
                }
            }

            for (int i = 0; i < animals.Count; i++)
            {
                Assert.That(colony.Grid.IsWalkable(animals[i].Cell), Is.True, $"animal {i} ended somewhere it cannot stand");
                Assert.That(moved[i], Is.GreaterThan(10), $"animal {i} barely moved in ten days");
            }
        }
    }
}
