#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Pawns.Wildlife;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// How a world gets its animals and keeps them (design 30). The wooded meadow is seeded to a
    /// census of its own surface, the bare board to nothing; an animal that decides to go walks
    /// off the edge and is removed there, and a short board is topped up at the edge.
    /// </summary>
    public class WildlifeTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ColonyWorld Wooded(uint seed = 1u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 1;
            scenario.startingFellRadius = 4;
            return ColonyWorld.Build(Size, seed, scenario, barren: false, wooded: true);
        }

        static ColonyWorld Bare(uint seed = 1u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 1;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: false);
        }

        static List<Pawn> Animals(ColonyWorld colony)
        {
            var list = new List<Pawn>();
            foreach (Pawn pawn in colony.Pawns.Pawns.All) if (!pawn.IsPerson) list.Add(pawn);
            return list;
        }

        static int KeepClear(ColonyWorld colony) => colony.Scenario.startingFellRadius + WildlifeSeeder.ClearingMargin;

        static SurfaceCensus Census(ColonyWorld colony) =>
            SurfaceCensus.Take(colony.Grid, colony.Pawns.Nav, colony.Designations, colony.Outcome.StartCell,
                KeepClear(colony), TraverseMode.Animal);

        // ---- the table ----------------------------------------------------------------------

        [Test]
        public void TheBareBoardHasNoAnimalsAndTheMeadowAndCityDo()
        {
            Assert.That(Animals(Bare()), Is.Empty, "anything that is not grass on the bare board is a bug");
            Assert.That(Animals(Wooded()), Is.Not.Empty);
            ColonyWorld city = ColonyWorld.Build(new GridSize(60, 60, 5), 9u, ScenarioDef.Bare(), mapType: MapType.RuinedCity);
            Assert.That(Animals(city), Is.Not.Empty, "the ruin has its rats");
        }

        [Test]
        public void EveryKindATableNamesExists()
        {
            PawnContent content = Odyssey.Sim.Defs.ContentPack.Pawns();
            foreach (MapGenDef gen in new MapGenDef[] { NaturalMapGenDef.Slice().MakeWooded(), MapGenDef.For(new GridSize(60, 60, 5)) })
            foreach (WildlifeEntry entry in gen.wildlife)
            {
                entry.Validate();
                Assert.That(WildlifeSeeder.KindIndex(content, entry.kind), Is.GreaterThan(0), $"{gen.defName} names {entry.kind}");
            }
        }

        // ---- seeding ------------------------------------------------------------------------

        [Test]
        public void TheMeadowIsSeededToACensusOfItsSurface()
        {
            ColonyWorld colony = Wooded();
            SurfaceCensus census = Census(colony);
            int target = WildlifeSeeder.TargetFor(colony.Gen, census.Cells.Count);
            Assume.That(target, Is.GreaterThan(2), $"the census offers {census.Cells.Count} columns");
            int population = Animals(colony).Count;
            Assert.That(population, Is.InRange(target - 4, target), $"target {target} from {census.Cells.Count} surface columns");
            Assert.That(population, Is.LessThanOrEqualTo(colony.Gen.wildlifeCeiling));
        }

        /// <summary>
        /// Every animal lands on (or within a group's reach of) a habitat one of its kind's lines
        /// names — the boar and the deer in the woodland, the rabbit on open ground, the rat by
        /// the rock, the frog on the bank itself (plan forest-animals §2, design 30 §8). Table-
        /// driven since the forest: twelve kinds, some on two lines.
        /// </summary>
        [Test]
        public void EveryAnimalLandsOnAHabitatItsTableNames()
        {
            ColonyWorld colony = Wooded();
            SurfaceCensus census = Census(colony);
            var byKind = new Dictionary<int, List<Habitat>>();
            foreach (WildlifeEntry entry in colony.Gen.wildlife)
            {
                int kind = WildlifeSeeder.KindIndex(colony.Pawns.Content, entry.kind);
                if (!byKind.TryGetValue(kind, out List<Habitat>? list)) byKind[kind] = list = new List<Habitat>();
                list.Add(entry.habitat);
            }
            int checkedAnimals = 0;
            foreach (Pawn animal in Animals(colony))
            {
                Assert.That(byKind.TryGetValue(animal.Kind, out List<Habitat>? habitats), Is.True,
                    $"animal {animal.Id.Value} is a kind the table does not name");
                bool home = false;
                foreach (Habitat habitat in habitats!)
                {
                    List<int> cells = WildlifeSeeder.HabitatCells(census, habitat, colony.Outcome.StartCell, KeepClear(colony), Size);
                    // A board with none of a habitat seeds that line anywhere (design 30 §2).
                    if (habitat == Habitat.Any || cells == census.Cells || cells.Count == 0) { home = true; break; }
                    // The frog is scattered on the bank itself; a group's other members fill the
                    // cells round a centre that was on its list.
                    home = habitat == Habitat.Bank
                        ? census.IsBank(animal.Cell)
                        : NearAny(colony, animal.Cell, new HashSet<int>(cells), WildlifeSeeder.GroupRadius);
                    if (home) break;
                }
                Assert.That(home, Is.True, $"{colony.Pawns.Content.Kinds[animal.Kind].defName} {animal.Id.Value} " +
                    $"landed at {Size.FromIndex(animal.Cell)}, off every habitat its lines name");
                checkedAnimals++;
            }
            Assert.That(checkedAnimals, Is.GreaterThan(0));
        }

        static bool NearAny(ColonyWorld colony, int cell, HashSet<int> set, int radius)
        {
            CellRef c = Size.FromIndex(cell);
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = c.X + dx, z = c.Z + dz;
                if (x < 0 || z < 0 || x >= Size.SizeX || z >= Size.SizeZ) continue;
                int top = SurfaceCensus.Topmost(colony.Grid, x, z);
                if (top >= 0 && set.Contains(top)) return true;
            }
            return false;
        }

        /// <summary>
        /// Groups land apart (owner, 2026-09-23: "the pigs were all together"): on the played
        /// board two sounders' centres keep their spacing, so the hogs' spread is at least that,
        /// and a sounder's members are scattered across their square rather than knotted on
        /// adjacent cells.
        /// </summary>
        [Test]
        public void SoundersLandApartFromEachOtherAndLooselyWithin()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 1;
            ColonyWorld colony = ColonyWorld.Build(new GridSize(120, 120, 16), 1u, scenario, barren: true, wooded: true);
            GridSize size = colony.Grid.Size;
            var hogs = new List<CellRef>();
            // The thicket boar is the meadow's sounder since the forest (answer 2: the hog is the city's).
            foreach (Pawn animal in Animals(colony)) if (animal.Kind == PawnKindIndex.ThicketBoar) hogs.Add(size.FromIndex(animal.Cell));
            Assume.That(hogs.Count, Is.GreaterThanOrEqualTo(6), "two sounders on the meadow");

            int widest = 0, adjacentPairs = 0, pairs = 0;
            for (int i = 0; i < hogs.Count; i++)
            for (int j = i + 1; j < hogs.Count; j++)
            {
                int d = System.Math.Max(System.Math.Abs(hogs[i].X - hogs[j].X), System.Math.Abs(hogs[i].Z - hogs[j].Z));
                widest = System.Math.Max(widest, d);
                pairs++;
                if (d <= 1) adjacentPairs++;
            }
            Assert.That(widest, Is.GreaterThanOrEqualTo(WildlifeSeeder.GroupSpacing - 2 * WildlifeSeeder.GroupRadius),
                "the two sounders are in different parts of the board");
            Assert.That(adjacentPairs, Is.LessThan(pairs / 2), "a sounder is loose, not a knot of neighbours");
        }

        [Test]
        public void NothingLandsInTheClearingAndEverythingCanReachTheStart()
        {
            ColonyWorld colony = Wooded();
            CellRef start = colony.Outcome.StartCell;
            int keepClear = KeepClear(colony);
            int startIndex = Size.Index(start.X, start.Z, start.Y);
            foreach (Pawn animal in Animals(colony))
            {
                CellRef c = Size.FromIndex(animal.Cell);
                int distance = System.Math.Max(System.Math.Abs(c.X - start.X), System.Math.Abs(c.Z - start.Z));
                Assert.That(distance, Is.GreaterThan(keepClear), $"animal {animal.Id.Value} landed {distance} cells from the start");
                Assert.That(colony.Pawns.Reachable(animal, startIndex), Is.True, $"animal {animal.Id.Value} is sealed away from the colony");
                Assert.That(colony.Grid.IsWalkable(animal.Cell), Is.True);
            }
        }

        [Test]
        public void SeedingIsDeterministicAndFollowsTheSeed()
        {
            ColonyWorld a = Wooded(7u), b = Wooded(7u), c = Wooded(8u);
            Assert.That(a.World.ComputeStateHash().Value, Is.EqualTo(b.World.ComputeStateHash().Value));
            var cellsA = new List<int>(); foreach (Pawn p in Animals(a)) cellsA.Add(p.Cell);
            var cellsB = new List<int>(); foreach (Pawn p in Animals(b)) cellsB.Add(p.Cell);
            var cellsC = new List<int>(); foreach (Pawn p in Animals(c)) cellsC.Add(p.Cell);
            Assert.That(cellsA, Is.EqualTo(cellsB));
            Assert.That(cellsC, Is.Not.EqualTo(cellsA), "another seed is another board and another scatter");
        }

        // ---- the level ----------------------------------------------------------------------

        [Test]
        public void ADepartingAnimalWalksToTheEdgeAndIsRemovedThere()
        {
            ColonyWorld colony = Wooded();
            Pawn hog = Animals(colony).Find(p => p.Kind == PawnKindIndex.MiddenHog)!;
            Assume.That(hog, Is.Not.Null);
            PawnId id = hog.Id;
            hog.Leaving = true;
            int before = colony.Pawns.Pawns.Count;

            int last = hog.Cell;
            int gone = -1;
            for (int tick = 0; tick < 120_000; tick++)
            {
                colony.World.Tick();
                Pawn? still = colony.Pawns.Pawns.Get(id);
                if (still == null) { gone = tick; break; }
                // Every step is one cell: a leaver walks off, it is not teleported off.
                CellRef a = Size.FromIndex(last), b = Size.FromIndex(still.Cell);
                Assert.That(System.Math.Max(System.Math.Abs(a.X - b.X), System.Math.Abs(a.Z - b.Z)), Is.LessThanOrEqualTo(1),
                    $"the hog jumped from {a} to {b} on tick {tick}");
                last = still.Cell;
            }
            Assert.That(gone, Is.GreaterThan(0), "the hog never left");
            Assert.That(WildlifeSystem.IsEdge(Size, last), Is.True, "and it was standing on the edge when it went");
            Assert.That(colony.Pawns.Pawns.Count, Is.EqualTo(before - 1));
            Assert.That(colony.Pawns.Pawns.Get(id), Is.Null);
            // The registry's order and index survive the removal.
            IReadOnlyList<Pawn> all = colony.Pawns.Pawns.All;
            for (int i = 1; i < all.Count; i++) Assert.That(all[i].Id.Value, Is.GreaterThan(all[i - 1].Id.Value));
            for (int i = 0; i < all.Count; i++) Assert.That(colony.Pawns.Pawns.Get(all[i].Id), Is.SameAs(all[i]));
        }

        [Test]
        public void AShortBoardIsToppedUpAtTheEdgeAndNeverPastTheCeiling()
        {
            ColonyWorld colony = Wooded();
            int maxId = 0;
            foreach (Pawn pawn in colony.Pawns.Pawns.All) maxId = System.Math.Max(maxId, pawn.Id.Value);
            foreach (Pawn animal in Animals(colony))
            {
                colony.Jobs.EndJob(animal, JobStatus.Succeeded);
                colony.Pawns.Pawns.Despawn(animal);
            }
            Assume.That(Animals(colony), Is.Empty);
            colony.Gen.wildlifeCeiling = 3;

            var seen = new HashSet<int>();
            int arrivals = 0;
            for (int tick = 0; tick < 60_000; tick++)
            {
                colony.World.Tick();
                foreach (Pawn animal in Animals(colony))
                {
                    if (animal.Id.Value <= maxId || !seen.Add(animal.Id.Value)) continue;
                    arrivals++;
                    // Seen on the tick it arrived (the rare tick), so it has not moved yet.
                    CellRef c = Size.FromIndex(animal.Cell);
                    int edge = System.Math.Min(System.Math.Min(c.X, c.Z), System.Math.Min(Size.SizeX - 1 - c.X, Size.SizeZ - 1 - c.Z));
                    Assert.That(edge, Is.LessThanOrEqualTo(WildlifeSeeder.GroupRadius), $"animal {animal.Id.Value} arrived at {c}, not at an edge");
                }
                Assert.That(Animals(colony).Count, Is.LessThanOrEqualTo(3), $"over the ceiling on tick {tick}");
            }
            Assert.That(arrivals, Is.GreaterThan(0), "nothing arrived in a day");
            Assert.That(Animals(colony).Count, Is.InRange(1, 3));
        }

        [Test]
        public void ABoardWithNoTableNeverGainsAnAnimal()
        {
            ColonyWorld colony = Bare();
            colony.World.Tick(30_000);
            Assert.That(Animals(colony), Is.Empty);
        }

        // ---- the night ----------------------------------------------------------------------

        /// <summary>
        /// A rat is out at night and a hog by day (design 30 §4). Both start at midnight on the
        /// bare board; over the night the rat walks more of the time than the hog, and over the
        /// day the hog walks more than the rat.
        /// </summary>
        [Test]
        public void ARatIsOutAtNightAndAHogByDay()
        {
            ColonyWorld colony = Bare();
            CellRef start = Size.FromIndex(colony.Pawns.Pawns.All[0].Cell);
            Pawn hog = colony.Pawns.Pawns.Spawn(Size.Index(start.X + 3, start.Z, start.Y), PawnKindIndex.MiddenHog);
            Pawn rat = colony.Pawns.Pawns.Spawn(Size.Index(start.X - 3, start.Z, start.Y), PawnKindIndex.DuctRat);
            int day = colony.Pawns.Content.DayTicks;
            Assume.That(AnimalIdleThinkNode.IsNight(colony.Pawns), Is.True, "tick zero is night");

            (int hogNight, int ratNight) = Walking(colony, hog, rat, day * AnimalIdleThinkNode.NightTo / 24);
            (int hogDay, int ratDay) = Walking(colony, hog, rat, day * (AnimalIdleThinkNode.NightFrom - AnimalIdleThinkNode.NightTo) / 24);
            Assert.That(ratNight, Is.GreaterThan(hogNight), $"night: rat walked {ratNight} ticks, hog {hogNight}");
            Assert.That(hogDay, Is.GreaterThan(ratDay), $"day: hog walked {hogDay} ticks, rat {ratDay}");
        }

        static (int, int) Walking(ColonyWorld colony, Pawn a, Pawn b, int ticks)
        {
            int wa = 0, wb = 0;
            for (int t = 0; t < ticks; t++)
            {
                colony.World.Tick();
                if (a.CurrentJob != null && a.CurrentJob.DefIndex == JobIndex.Wander) wa++;
                if (b.CurrentJob != null && b.CurrentJob.DefIndex == JobIndex.Wander) wb++;
            }
            return (wa, wb);
        }

        // ---- the save -----------------------------------------------------------------------

        [Test]
        public void LeavingSurvivesASaveAndAnOlderSaveHasNobodyLeaving()
        {
            ColonyWorld original = Wooded();
            // One that is not already standing on the edge: a leaver there is removed on the
            // first rare tick, which is the rule working rather than the save being tested.
            Pawn leaver = Animals(original).Find(a => !WildlifeSystem.IsEdge(Size, a.Cell))!;
            Assume.That(leaver, Is.Not.Null);
            leaver.Leaving = true;
            original.World.Tick(10);

            byte[] full;
            using (var stream = new System.IO.MemoryStream())
            {
                original.Save(stream);
                full = stream.ToArray();
            }
            ColonyWorld restored = Wooded();
            restored.Load(full);
            var ids = new List<int>();
            foreach (Pawn p in restored.Pawns.Pawns.All) ids.Add(p.Id.Value);
            Pawn? back = restored.Pawns.Pawns.Get(leaver.Id);
            Assert.That(back, Is.Not.Null, $"pawn {leaver.Id.Value} is not in the restored colony: ids {string.Join(",", ids)}");
            Assert.That(back!.Leaving, Is.True);
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(original.World.ComputeStateHash().Value));

            var without = new List<Odyssey.Sim.Saving.ISaveable>();
            foreach (var component in original.SaveComponents)
                if (component is not Odyssey.Sim.Saving.WildlifeSection) without.Add(component);
            byte[] older;
            using (var stream = new System.IO.MemoryStream())
            {
                Odyssey.Sim.Saving.WorldSave.Save(original.World, stream, without);
                older = stream.ToArray();
            }
            ColonyWorld fromOlder = Wooded();
            var header = fromOlder.Load(older);
            Assert.That(header.SkippedSections, Is.Empty);
            foreach (Pawn pawn in fromOlder.Pawns.Pawns.All) Assert.That(pawn.Leaving, Is.False);
        }

        // ---- the gate -----------------------------------------------------------------------

        /// <summary>Ten days on the wooded meadow: the level holds, animals come and go, nothing throws.</summary>
        [Test, Category("Long")]
        public void TenDaysOfWildlifeHoldsItsLevel()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 1;
            ColonyWorld colony = ColonyWorld.Build(new GridSize(120, 120, 16), 3u, scenario, barren: true, wooded: true);
            int day = colony.Pawns.Content.DayTicks;
            int maxId = 0;
            foreach (Pawn pawn in colony.Pawns.Pawns.All) maxId = System.Math.Max(maxId, pawn.Id.Value);
            int start = Animals(colony).Count;
            int low = start, high = start;
            for (int d = 0; d < 10; d++)
            {
                colony.World.Tick(day);
                int n = Animals(colony).Count;
                low = System.Math.Min(low, n);
                high = System.Math.Max(high, n);
            }
            int newest = 0;
            foreach (Pawn pawn in colony.Pawns.Pawns.All) newest = System.Math.Max(newest, pawn.Id.Value);
            Assert.That(high, Is.LessThanOrEqualTo(colony.Gen.wildlifeCeiling));
            Assert.That(low, Is.GreaterThan(0), $"the board was empty at the end of a day (started at {start})");
            Assert.That(newest, Is.GreaterThan(maxId), "something arrived in ten days");
            foreach (Pawn animal in Animals(colony))
                Assert.That(colony.Grid.IsWalkable(animal.Cell), Is.True, $"animal {animal.Id.Value} ended somewhere it cannot stand");
        }
    }
}
