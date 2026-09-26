#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Pawns.Wildlife;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The culvert frog (design 30 §8): an animal that keeps to the banks of the water. What is
    /// asserted is the part that is the frog's own — seeded on a bank, every leg ending on one, a
    /// stray going back to one, never in the water — each beside a control that is not a frog.
    /// Everything it shares with the hog (the mind, the save, the hash, the hop rule) is
    /// <see cref="AnimalTests"/>' and is not asserted twice.
    /// </summary>
    public class FrogTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 16);

        /// <summary>08:00 on the first day: a frog keeps day hours, and a board at midnight is a frog asleep.</summary>
        const int Morning = 20_000;

        static ColonyWorld Board()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 1;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(new ColonyRequest
            {
                Size = Size,
                Seed = 1u,
                Scenario = scenario,
                Barren = true,
                StartTick = Morning,
            });
        }

        static CellRef Start(ColonyWorld colony) => Size.FromIndex(colony.Pawns.Pawns.All[0].Cell);

        static int GroundNear(ColonyWorld colony, int dx, int dz)
        {
            CellRef start = Start(colony);
            int cell = Size.Index(start.X + dx, start.Z + dz, start.Y);
            Assume.That(colony.Grid.IsWalkable(cell), Is.True, "the fixture wants open ground here");
            return cell;
        }

        /// <summary>A stream of shallow water across the whole board, <paramref name="dz"/> rows from the colonist.</summary>
        static int Stream(ColonyWorld colony, int dz)
        {
            CellRef start = Start(colony);
            int z = start.Z + dz;
            for (int x = 0; x < Size.SizeX; x++)
            {
                int cell = Size.Index(x, z, start.Y);
                colony.Grid.Terrain[cell] = NaturalContent.TerrainShallowWater;
                for (int nz = z - 1; nz <= z + 1; nz++)
                    colony.Pawns.Nav.MarkDirty(Size.Index(x, nz, start.Y));
            }
            colony.Pawns.Nav.Rebuild();
            return z;
        }

        static int FrogRadius => ContentPack.Pawns().SpeciesOf(PawnKindIndex.CulvertFrog).bankRadius;

        [Test]
        public void TheFrogIsAWildAnimalThatKeepsToTheBank()
        {
            PawnContent content = ContentPack.Pawns();
            SpeciesDef frog = content.SpeciesOf(PawnKindIndex.CulvertFrog);
            Assert.That(content.Kinds[PawnKindIndex.CulvertFrog].defName, Is.EqualTo("PawnKind_CulvertFrog"));
            Assert.That(frog.person, Is.False);
            Assert.That(frog.bankRadius, Is.GreaterThan(0));
            Assert.That(frog.ignoresRain, Is.True);
            Assert.That(frog.figureKey, Is.EqualTo("animal.frog"));
            Assert.That(content.ModeOf(PawnKindIndex.CulvertFrog), Is.EqualTo(TraverseMode.Animal),
                "no swimming mode: a sixth traverse mode is a flood on every nav rebuild (design 33 §16b)");
            // And no other species keeps to a bank or stays out in the rain.
            foreach (int kind in new[] { PawnKindIndex.Colonist, PawnKindIndex.MiddenHog, PawnKindIndex.DuctRat, PawnKindIndex.Bandit })
            {
                Assert.That(content.SpeciesOf(kind).bankRadius, Is.Zero, content.Kinds[kind].defName);
                Assert.That(content.SpeciesOf(kind).ignoresRain, Is.False, content.Kinds[kind].defName);
            }
        }

        [Test]
        public void WaterBankSeesWaterOnItsOwnLayerAndTheOneBelowAndNotAbove()
        {
            ColonyWorld colony = Board();
            CellRef start = Start(colony);
            int z = Stream(colony, 6);
            int bank = Size.Index(start.X, z - 2, start.Y);
            Assert.That(WaterBank.Near(colony.Grid, bank, 2), Is.True);
            Assert.That(WaterBank.Near(colony.Grid, bank, 1), Is.False, "two rows off is not within one");
            // The same column a layer up sees the stream below it; a layer down does not see it above.
            Assert.That(WaterBank.Near(colony.Grid, Size.Index(start.X, z - 2, start.Y + 1), 2), Is.True);
            Assert.That(WaterBank.Near(colony.Grid, Size.Index(start.X, z - 2, start.Y - 1), 2), Is.False);
        }

        /// <summary>
        /// <b>A world seeds its frogs on the banks</b>, every one of them and not only a group's
        /// centre. The played meadow on several seeds, so that a seed whose picks happen to
        /// miss the frog does not make the test vacuous.
        /// </summary>
        [Test]
        public void TheMeadowSeedsItsFrogsOnTheBanks()
        {
            int frogs = 0;
            for (uint seed = 1; seed <= 6; seed++)
            {
                ScenarioDef scenario = ScenarioDef.Bare();
                scenario.colonists = 1;
                scenario.beds = 1;
                scenario.startingFellRadius = 4;
                ColonyWorld colony = ColonyWorld.Build(new GridSize(60, 60, 16), seed, scenario, barren: false, wooded: true);
                int keepClear = scenario.startingFellRadius + WildlifeSeeder.ClearingMargin;
                SurfaceCensus census = SurfaceCensus.Take(colony.Grid, colony.Pawns.Nav, colony.Designations,
                    colony.Outcome.StartCell, keepClear, TraverseMode.Animal);
                foreach (Pawn pawn in colony.Pawns.Pawns.All)
                {
                    if (pawn.Kind != PawnKindIndex.CulvertFrog) continue;
                    frogs++;
                    Assert.That(census.IsBank(pawn.Cell), Is.True, $"seed {seed}: a frog at {colony.Grid.Size.FromIndex(pawn.Cell)} is off the bank");
                }
            }
            Assert.That(frogs, Is.GreaterThan(0), "six meadows and not one frog");
        }

        /// <summary>
        /// <b>Every leg a frog takes ends on its bank</b>, and it never stands in the water. The
        /// hog on the same bank is the control: its legs wander well past the frog's radius.
        /// </summary>
        [Test]
        public void AFrogKeepsToItsBankAndAHogDoesNot()
        {
            ColonyWorld colony = Board();
            int streamZ = Stream(colony, 4);
            Pawn frog = colony.Pawns.Pawns.Spawn(GroundNear(colony, 3, 2), PawnKindIndex.CulvertFrog);
            Pawn hog = colony.Pawns.Pawns.Spawn(GroundNear(colony, -3, 2), PawnKindIndex.MiddenHog);
            Assume.That(WaterBank.Near(colony.Grid, frog.Cell, FrogRadius), Is.True, "the frog starts on the bank");

            int frogLegs = 0, hogFar = 0;
            int lastFrogTarget = -1;
            for (int tick = 0; tick < 12_000; tick++)
            {
                colony.World.Tick();
                Assert.That(Size.FromIndex(frog.Cell).Z, Is.Not.EqualTo(streamZ), $"the frog stood in the stream on tick {tick}");
                Job? job = frog.CurrentJob;
                if (job != null && job.DefIndex == JobIndex.Wander && job.TargetCell != lastFrogTarget)
                {
                    lastFrogTarget = job.TargetCell;
                    frogLegs++;
                    Assert.That(WaterBank.Near(colony.Grid, job.TargetCell, FrogRadius), Is.True,
                        $"a leg on tick {tick} ends at {Size.FromIndex(job.TargetCell)}, off the bank");
                }
                if (!WaterBank.Near(colony.Grid, hog.Cell, FrogRadius)) hogFar++;
            }
            Assert.That(frogLegs, Is.GreaterThan(5), "the frog hardly moved, so its legs prove nothing");
            Assert.That(hogFar, Is.GreaterThan(0), "the control: a hog is not held to the bank");
        }

        /// <summary>
        /// <b>A stray goes back to the water</b>: a frog put down twelve rows from the stream — a
        /// debug spawn in a dry field, or one run off by a fight — heads for the nearest bank and
        /// stays on it once there.
        /// </summary>
        [Test]
        public void AStrayFrogHeadsBackToTheWater()
        {
            ColonyWorld colony = Board();
            Stream(colony, 6);
            Pawn frog = colony.Pawns.Pawns.Spawn(GroundNear(colony, 0, -6), PawnKindIndex.CulvertFrog);
            Assume.That(WaterBank.Near(colony.Grid, frog.Cell, BankTargetSearchRadius), Is.True, "within the search");
            Assume.That(WaterBank.Near(colony.Grid, frog.Cell, FrogRadius), Is.False, "and off the bank");

            int home = -1;
            for (int tick = 0; tick < 4_000; tick++)
            {
                colony.World.Tick();
                bool near = WaterBank.Near(colony.Grid, frog.Cell, FrogRadius);
                if (home < 0 && near && !frog.HasPath) home = tick;
            }
            Assert.That(home, Is.GreaterThanOrEqualTo(0), "the frog never reached the bank");
            Assert.That(WaterBank.Near(colony.Grid, frog.Cell, FrogRadius + 1), Is.True, "and it did not stay there");
        }

        /// <summary>
        /// <b>A group's frogs hop different ways</b> (owner, 2026-09-26: "make sure they jump in
        /// different directions as some were very similar"). Four frogs round a pond, where the
        /// bank runs every way so a heading is free to choose; every leg a frog starts while
        /// another within six cells is mid-hop is compared with that hop, and fewer than one in
        /// five may be within 60 degrees of it. <c>divergeRadius</c> is a preference, so the
        /// bound is a rate rather than never.
        /// </summary>
        [Test]
        public void AGroupsFrogsHopInDifferentDirections()
        {
            ColonyWorld colony = Board();
            CellRef start = Start(colony);
            // A pond five cells across, eight rows off the colonist, with bank all round it.
            int cx = start.X, cz = start.Z + 8;
            for (int dz = -2; dz <= 2; dz++)
            for (int dx = -2; dx <= 2; dx++)
            {
                int cell = Size.Index(cx + dx, cz + dz, start.Y);
                colony.Grid.Terrain[cell] = NaturalContent.TerrainShallowWater;
                for (int nz = -1; nz <= 1; nz++)
                for (int nx = -1; nx <= 1; nx++)
                    colony.Pawns.Nav.MarkDirty(Size.Index(cx + dx + nx, cz + dz + nz, start.Y));
            }
            colony.Pawns.Nav.Rebuild();

            var frogs = new List<Pawn>();
            foreach (var (dx, dz) in new[] { (-3, 0), (3, 0), (0, -3), (0, 3) })
            {
                int cell = Size.Index(cx + dx, cz + dz, start.Y);
                Assume.That(colony.Grid.IsWalkable(cell), Is.True);
                frogs.Add(colony.Pawns.Pawns.Spawn(cell, PawnKindIndex.CulvertFrog));
            }
            int radius = ContentPack.Pawns().SpeciesOf(PawnKindIndex.CulvertFrog).divergeRadius;
            Assume.That(radius, Is.GreaterThan(0));

            var lastTarget = new int[frogs.Count];
            for (int i = 0; i < lastTarget.Length; i++) lastTarget[i] = -1;
            int withNeighbour = 0, alike = 0;
            for (int tick = 0; tick < 20_000; tick++)
            {
                colony.World.Tick();
                for (int i = 0; i < frogs.Count; i++)
                {
                    Job? leg = frogs[i].CurrentJob;
                    if (leg == null || leg.DefIndex != JobIndex.Wander || leg.TargetCell == lastTarget[i]) continue;
                    lastTarget[i] = leg.TargetCell;
                    CellRef a = Size.FromIndex(frogs[i].Cell), at = Size.FromIndex(leg.TargetCell);
                    long ax = at.X - a.X, az = at.Z - a.Z;
                    bool neighbour = false, close = false;
                    for (int j = 0; j < frogs.Count; j++)
                    {
                        if (j == i) continue;
                        Job? other = frogs[j].CurrentJob;
                        if (other == null || other.DefIndex != JobIndex.Wander || other.TargetCell < 0) continue;
                        CellRef b = Size.FromIndex(frogs[j].Cell), bt = Size.FromIndex(other.TargetCell);
                        if (System.Math.Max(System.Math.Abs(b.X - a.X), System.Math.Abs(b.Z - a.Z)) > radius) continue;
                        long bx = bt.X - b.X, bz = bt.Z - b.Z;
                        if (bx == 0 && bz == 0) continue;
                        neighbour = true;
                        long dot = ax * bx + az * bz;
                        if (dot > 0 && 4 * dot * dot > (ax * ax + az * az) * (bx * bx + bz * bz)) close = true;
                    }
                    if (!neighbour) continue;
                    withNeighbour++;
                    if (close) alike++;
                }
            }
            TestContext.WriteLine($"{alike} of {withNeighbour} legs started beside a hopping neighbour were within 60 degrees of it");
            Assert.That(withNeighbour, Is.GreaterThanOrEqualTo(10), "too few legs beside a neighbour to judge");
            Assert.That(alike * 5, Is.LessThan(withNeighbour), $"{alike} of {withNeighbour} set off the same way as a neighbour");
        }

        /// <summary>The search radius in <c>BankTarget</c>, which is internal to the job system.</summary>
        const int BankTargetSearchRadius = 20;

        /// <summary>
        /// <b>With no water to find, a frog wanders like anyone</b> rather than standing still for
        /// ever: the bare board has none.
        /// </summary>
        [Test]
        public void AFrogWithNoWaterAnywhereStillWanders()
        {
            ColonyWorld colony = Board();
            Pawn frog = colony.Pawns.Pawns.Spawn(GroundNear(colony, 3, 0), PawnKindIndex.CulvertFrog);
            int start = frog.Cell;
            var visited = new HashSet<int>();
            for (int tick = 0; tick < 6_000; tick++)
            {
                colony.World.Tick();
                visited.Add(frog.Cell);
            }
            Assert.That(visited.Count, Is.GreaterThan(3), $"the frog stayed within {visited.Count} cells of {Size.FromIndex(start)}");
        }
    }
}
