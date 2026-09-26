#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Pawns.Wildlife;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The meadow's wildlife table after the forest (plan forest-animals §2): a ceiling by board,
    /// every kind the table names on the played board, the wolves and the bear in the far woods,
    /// the rabbits on open ground, and the hog gone to the city.
    /// </summary>
    public class ForestWildlifeTests
    {
        static readonly GridSize Standard = new GridSize(120, 120, 16);

        static ColonyWorld? _played;

        /// <summary>The played board: Standard, seed 1, the golden's own build (Golden.cs).</summary>
        static ColonyWorld Played() => _played ??= ColonyWorld.Build(Standard, 1u, ScenarioDef.Bare(), wooded: true);

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

        static int FromStart(ColonyWorld colony, int cell)
        {
            CellRef c = colony.Grid.Size.FromIndex(cell), s = colony.Outcome.StartCell;
            return System.Math.Max(System.Math.Abs(c.X - s.X), System.Math.Abs(c.Z - s.Z));
        }

        // ---- the table --------------------------------------------------------------------

        [Test]
        public void TheCeilingIsTheBoards()
        {
            // Answer 13: Small 32, Standard 48, Large 64, Huge 80 — the sizes the New game page offers.
            Assert.That(PlayedMap.Def(new GridSize(80, 80, 16)).wildlifeCeiling, Is.EqualTo(32));
            Assert.That(PlayedMap.Def(Standard).wildlifeCeiling, Is.EqualTo(48));
            Assert.That(PlayedMap.Def(new GridSize(180, 180, 24)).wildlifeCeiling, Is.EqualTo(64));
            Assert.That(PlayedMap.Def(new GridSize(240, 240, 16)).wildlifeCeiling, Is.EqualTo(80));
            // The ruined city keeps its 24.
            Assert.That(MapGenDef.For(new GridSize(60, 60, 5)).wildlifeCeiling, Is.EqualTo(24));
        }

        [Test]
        public void TheMeadowNamesEveryForestKindAndTheHogIsTheCitys()
        {
            PawnContent content = ContentPack.Pawns();
            var named = new HashSet<int>();
            foreach (WildlifeEntry entry in NaturalMapGenDef.MeadowWildlife())
            {
                entry.Validate();
                int kind = WildlifeSeeder.KindIndex(content, entry.kind);
                Assert.That(kind, Is.GreaterThan(0), entry.kind);
                named.Add(kind);
            }
            for (int k = PawnKindIndex.VergeRabbit; k <= PawnKindIndex.QuarryBear; k++)
                Assert.That(named.Contains(k), Is.True, content.Kinds[k].defName);
            Assert.That(named.Contains(PawnKindIndex.MiddenHog), Is.False, "answer 2: the boar is the meadow's, the hog the city's");

            var city = new HashSet<string>();
            foreach (WildlifeEntry entry in MapGenDef.For(new GridSize(60, 60, 5)).wildlife) city.Add(entry.kind);
            Assert.That(city.Contains("PawnKind_MiddenHog"), Is.True, "the city keeps its hog");
        }

        // ---- the played board ---------------------------------------------------------------

        [Test]
        public void ThePlayedBoardIsSeededToItsCeiling()
        {
            ColonyWorld colony = Played();
            SurfaceCensus census = Census(colony);
            int target = WildlifeSeeder.TargetFor(colony.Gen, census.Cells.Count);
            TestContext.Progress.WriteLine($"[forest] Standard seed 1: {census.Cells.Count} reachable columns, " +
                $"{census.Woodland.Count} woodland, {census.Open.Count} open, {census.Bank.Count} bank, {census.Rock.Count} rock; " +
                $"target {target}, seeded {Animals(colony).Count}");
            Assert.That(target, Is.EqualTo(48), "the density reaches the ceiling on the board the game plays");
            Assert.That(Animals(colony).Count, Is.EqualTo(48));
        }

        [Test]
        public void EveryKindTheMeadowNamesIsOnThePlayedBoard()
        {
            ColonyWorld colony = Played();
            var counts = new SortedDictionary<int, int>();
            foreach (Pawn animal in Animals(colony))
                counts[animal.Kind] = counts.TryGetValue(animal.Kind, out int n) ? n + 1 : 1;
            var line = new System.Text.StringBuilder("[forest] Standard seed 1 by kind:");
            foreach (var kv in counts) line.Append(' ').Append(colony.Pawns.Content.Kinds[kv.Key].defName.Replace("PawnKind_", "")).Append('=').Append(kv.Value);
            TestContext.Progress.WriteLine(line.ToString());

            foreach (WildlifeEntry entry in colony.Gen.wildlife)
            {
                int kind = WildlifeSeeder.KindIndex(colony.Pawns.Content, entry.kind);
                Assert.That(counts.ContainsKey(kind), Is.True, $"{entry.kind} is named and not on the board");
            }
        }

        [Test]
        public void TheWolvesAndTheBearBeginInTheFarWoods()
        {
            ColonyWorld colony = Played();
            int far = 0;
            foreach (Pawn animal in Animals(colony))
            {
                if (animal.Kind != PawnKindIndex.RidgeWolf && animal.Kind != PawnKindIndex.QuarryBear) continue;
                far++;
                // A pack's members scatter round a centre that kept the distance.
                Assert.That(FromStart(colony, animal.Cell), Is.GreaterThanOrEqualTo(NaturalMapGenDef.FarWoods - WildlifeSeeder.GroupRadius),
                    $"{colony.Pawns.Content.Kinds[animal.Kind].defName} {animal.Id.Value} began {FromStart(colony, animal.Cell)} cells from the start");
            }
            Assert.That(far, Is.GreaterThan(0), "the premise: wolves and a bear on the board");
        }

        [Test]
        public void OpenGroundIsWhereNoTreeIsNear()
        {
            ColonyWorld colony = Played();
            SurfaceCensus census = Census(colony);
            Assert.That(census.Open, Is.Not.Empty, "the meadow has open grass");
            var woodland = new HashSet<int>(census.Woodland);
            var rock = new HashSet<int>(census.Rock);
            foreach (int cell in census.Open)
            {
                Assert.That(woodland.Contains(cell), Is.False, "open and woodland are disjoint");
                Assert.That(rock.Contains(cell), Is.False, "open and rock are disjoint");
                Assert.That(FromStart(colony, cell), Is.GreaterThan(KeepClear(colony)), "nothing in the clearing");
            }
        }

        [Test]
        public void RabbitsLandOnOpenGround()
        {
            ColonyWorld colony = Played();
            SurfaceCensus census = Census(colony);
            var open = new HashSet<int>(census.Open);
            GridSize size = colony.Grid.Size;
            int rabbits = 0;
            foreach (Pawn animal in Animals(colony))
            {
                if (animal.Kind != PawnKindIndex.VergeRabbit) continue;
                rabbits++;
                CellRef c = size.FromIndex(animal.Cell);
                bool near = false;
                for (int dz = -WildlifeSeeder.GroupRadius; dz <= WildlifeSeeder.GroupRadius && !near; dz++)
                for (int dx = -WildlifeSeeder.GroupRadius; dx <= WildlifeSeeder.GroupRadius && !near; dx++)
                {
                    if (!size.Contains(c.X + dx, c.Z + dz, 0)) continue;
                    int top = SurfaceCensus.Topmost(colony.Grid, c.X + dx, c.Z + dz);
                    near = top >= 0 && open.Contains(top);
                }
                Assert.That(near, Is.True, $"rabbit {animal.Id.Value} at {c}, nowhere near open ground");
            }
            Assert.That(rabbits, Is.GreaterThan(0));
        }

        [Test]
        public void FarFromStartIsTriedForAndNeverEmptiesAHabitat()
        {
            var size = new GridSize(20, 20, 4);
            var start = new CellRef(10, 10, 1);
            var habitat = new List<int> { size.Index(9, 9, 1), size.Index(0, 0, 1), size.Index(19, 10, 1) };
            List<int> far = WildlifeSeeder.FarFromStart(habitat, 10, start, size);
            Assert.That(far, Is.EquivalentTo(new[] { size.Index(0, 0, 1) }), "only (0,0) is ten cells out; (19,10) is nine");
            Assert.That(WildlifeSeeder.FarFromStart(habitat, 50, start, size), Is.SameAs(habitat), "nothing that far: the habitat as it is");
            Assert.That(WildlifeSeeder.FarFromStart(habitat, 0, start, size), Is.SameAs(habitat));
        }

        // ---- every board -------------------------------------------------------------------

        /// <summary>
        /// Every board the New game page offers is seeded to its ceiling on seed 1, and says what
        /// it offered — the measurement <see cref="NaturalMapGenDef.MeadowDensity"/> is set from.
        /// </summary>
        [Test, Category("Long")]
        public void EveryBoardIsSeededToItsCeiling(
            [Values(80, 120, 180, 240)] int side)
        {
            var size = new GridSize(side, side, side == 180 ? 24 : 16);
            ColonyWorld colony = ColonyWorld.Build(size, 1u, ScenarioDef.Bare(), wooded: true);
            SurfaceCensus census = Census(colony);
            int target = WildlifeSeeder.TargetFor(colony.Gen, census.Cells.Count);
            int seeded = Animals(colony).Count;
            int density = (colony.Gen.wildlifeCeiling * 10_000 + census.Cells.Count - 1) / census.Cells.Count;
            TestContext.Progress.WriteLine($"[forest] {size}: {census.Cells.Count} reachable columns; ceiling {colony.Gen.wildlifeCeiling} " +
                $"needs density >= {density}; target {target}; seeded {seeded}");
            Assert.That(seeded, Is.EqualTo(colony.Gen.wildlifeCeiling));

            var kinds = new HashSet<int>();
            foreach (Pawn animal in Animals(colony)) kinds.Add(animal.Kind);
            var named = new HashSet<int>();
            foreach (WildlifeEntry entry in colony.Gen.wildlife) named.Add(WildlifeSeeder.KindIndex(colony.Pawns.Content, entry.kind));
            Assert.That(kinds, Is.EquivalentTo(named), "every kind the table names, on every board");
        }
    }
}
