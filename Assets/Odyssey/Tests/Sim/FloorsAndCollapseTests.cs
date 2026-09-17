#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// U29: floors, and what happens when one is not held up.
    ///
    /// <para><b>A roof is a floor is a slab</b> — one thing, of a material, at the boundary between
    /// two layers (`02-world-and-layers.md` §4). So building one is not a second pipeline: it is
    /// <c>Building_Wall</c> with <c>slab = true</c>, going through the same order, delivery, work
    /// and raise. Most of the tests here are therefore about the rule rather than the plumbing, and
    /// the rule is the support model, which was built in M1 and switched off until now.</para>
    ///
    /// <para>Design and the eleven decisions behind it: `docs/design/17-floors-and-collapse.md`.</para>
    /// </summary>
    public class FloorsAndCollapseTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 16);

        static ColonyWorld Board(int colonists = 1, uint seed = 1u, bool wooded = false)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = colonists;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: wooded);
        }

        static Pawn TheColonist(ColonyWorld colony) => colony.Pawns.Pawns.All[0];

        /// <summary>A walkable cell near the start: open air with ground beneath it.</summary>
        static int GroundLevelCellNear(ColonyWorld colony, int radius)
        {
            CellRef start = colony.Start;
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius) continue;
                int x = start.X + dx, z = start.Z + dz;
                if (!Size.Contains(x, z, start.Y)) continue;

                int cell = Size.Index(x, z, start.Y);
                if (colony.Grid.IsWalkable(cell)) return cell;
            }

            return -1;
        }

        /// <summary>Put a thing up where it is ordered, with no colonist and no waiting.</summary>
        static void RaiseNow(ColonyWorld colony, int cell, int building, int stuff = StuffHandle.Wood)
        {
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), building, stuff),
                Is.EqualTo(IntentRejection.None), $"the order for {cell} was refused");
            colony.Construction.Raise(colony.Pawns, cell);
        }

        static int Above(int cell) => cell + Size.LayerStride;

        static int OnTheGround(ColonyWorld colony, int item)
        {
            int total = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == item) total += items[i].Stack;
            return total;
        }

        // ---- a floor is the same pipeline ----------------------------------------------------

        /// <summary>
        /// The whole journey, and deliberately the wall journey with one word changed: order,
        /// material carried, work applied, and a slab standing where the order was. If this needed
        /// anything the wall test did not, the claim that a floor is one field on a `BuildingDef`
        /// would be false.
        /// </summary>
        [Test]
        public void AnOrderedFloorIsFedWorkedAndRaised()
        {
            ColonyWorld colony = Board();
            int ground = GroundLevelCellNear(colony, 3);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            // A wall to hang the first slab off: the cell above a wall is grounded, so a floor
            // there has full support and needs no neighbour.
            RaiseNow(colony, ground, BuildingHandle.Wall);
            int site = Above(ground);

            int pile = colony.Pawns.Items.NearestCellWithSpace(
                colony.Grid, ground, ItemIndex.Wood, 20, JobDriver.DropSearchRadius);
            Assume.That(pile, Is.GreaterThanOrEqualTo(0));
            colony.Pawns.Items.Spawn(ItemIndex.Wood, pile, 20);

            colony.World.Intents.Submit(new Intent(
                IntentKind.PlaceBuilding, Size.FromIndex(site), BuildingHandle.Floor, StuffHandle.Wood));
            colony.World.Tick();
            Assert.That(colony.Construction.At(site), Is.EqualTo(BuildingHandle.Floor),
                "the order became a site");

            int raisedAt = -1;
            for (int tick = 0; tick < 20_000 && raisedAt < 0; tick++)
            {
                colony.World.Tick();
                if (colony.Grid.Floor[site] != CoreContent.SlabNone) raisedAt = tick;
            }

            Assert.That(raisedAt, Is.GreaterThanOrEqualTo(0), "a colonist built the floor");
            Assert.That(colony.Grid.Floor[site], Is.EqualTo(CoreContent.SlabBuilt),
                "and it is ours, not one of the generator's three kinds");
            Assert.That(colony.Grid.FloorStuff[site], Is.EqualTo(NaturalContent.StuffWood),
                "it remembers what it was made of");
            Assert.That(colony.Grid.Edifice[site], Is.LessThan(0), "a floor is not an edifice");
            Assert.That(colony.Grid.IsWalkable(site), Is.True, "and you can stand on it");
            Assert.That(OnTheGround(colony, ItemIndex.Wood), Is.EqualTo(16), "four wood went into it");
        }

        // ---- the support rule, seen from the side ---------------------------------------------

        /// <summary>
        /// <b>The support rule made visible in one test.</b> A slab over something solid has
        /// <c>S_max</c>; every step out from there takes the best of its neighbours minus one. With
        /// <c>S_max</c> of 4 that is a bridge of exactly four cells, and the fifth order is refused
        /// with a reason rather than accepted and collapsed the moment it is finished.
        ///
        /// <para>Nobody tuned the number 4 as an overhang. It is <see cref="SupportSolver.MaxSupport"/>
        /// seen sideways, which is why the test reads it off the solver rather than spelling it.</para>
        /// </summary>
        [Test]
        public void ABridgeReachesAsFarAsSupportAndNoFurther()
        {
            ColonyWorld colony = Board();
            int ground = GroundLevelCellNear(colony, 4);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            RaiseNow(colony, ground, BuildingHandle.Wall);
            colony.World.Tick();

            int reach = colony.Pawns.Support!.MaxSupport;
            int first = Above(ground);

            // Out along +x, one cell at a time, settling the solver between each: the next order is
            // judged against the support the last one actually earned.
            for (int step = 0; step < reach; step++)
            {
                int cell = first + step;
                Assert.That(colony.Construction.Allows(cell, BuildingHandle.Floor), Is.True,
                    $"cell {step} out from the wall is within reach of it");
                RaiseNow(colony, cell, BuildingHandle.Floor);
                colony.World.Tick();
                Assert.That(colony.Grid.Floor[cell], Is.EqualTo(CoreContent.SlabBuilt),
                    $"the slab {step} out stayed up");
            }

            int tooFar = first + reach;
            Assert.That(colony.Construction.Allows(tooFar, BuildingHandle.Floor), Is.False,
                "the span runs out exactly where the support rule says it does");
            Assert.That(colony.Construction.Place(Size.FromIndex(tooFar), BuildingHandle.Floor, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.NotPermitted),
                "and the refusal has a reason rather than being silence");
        }

        /// <summary>
        /// The control for the test above: the run of slabs it built really is standing on the
        /// wall, and not on some accident of the board. Take the wall away and the whole run comes
        /// down in one tick, which is the cascade.
        /// </summary>
        [Test]
        public void PullingTheSupportOutBringsTheWholeRunDownAtOnce()
        {
            ColonyWorld colony = Board();
            int ground = GroundLevelCellNear(colony, 4);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            RaiseNow(colony, ground, BuildingHandle.Wall);
            colony.World.Tick();

            int first = Above(ground);
            int reach = colony.Pawns.Support!.MaxSupport;
            for (int step = 0; step < reach; step++)
            {
                RaiseNow(colony, first + step, BuildingHandle.Floor);
                colony.World.Tick();
            }

            for (int step = 0; step < reach; step++)
                Assume.That(colony.Grid.Floor[first + step], Is.EqualTo(CoreContent.SlabBuilt));

            int before = colony.World.Systems.WorldSystems.Count;
            Assume.That(before, Is.GreaterThan(0));

            // The wall goes, and with it the only thing holding any of them up.
            Assert.That(colony.Construction.Demolish(colony.Pawns, ground, out _), Is.True);
            colony.World.Tick();

            for (int step = 0; step < reach; step++)
                Assert.That(colony.Grid.Floor[first + step], Is.EqualTo(CoreContent.SlabNone),
                    $"the slab {step} out came down with the rest");
        }

        // ---- the ruined shell -------------------------------------------------------------------

        /// <summary>
        /// <b>The case the unit exists to prove</b>, and the setting's central engineering tension:
        /// a colonist cuts rock out for salvage and the floor above the next room quietly loses its
        /// last support.
        ///
        /// <para>Built by hand rather than hunted for on a city map, so the test says what it means:
        /// a slab stamped standing by "construction trust", with nothing under it but one column of
        /// rock. Mining the rock revokes the trust — which is what <c>SupportSolver.MarkDirty</c>
        /// has always done and what nothing ever called until now — and the slab has to earn its
        /// support like anything else. It cannot.</para>
        /// </summary>
        [Test]
        public void MiningAwayWhatHeldAStampedSlabUpBringsItDown()
        {
            ColonyWorld colony = Board();
            int ground = GroundLevelCellNear(colony, 3);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            // A column of rock where the wall of a ruin would be, and a stamped deck on top of it.
            colony.Grid.Terrain[ground] = CoreContent.TerrainRock;
            colony.Grid.Flags[ground] |= CellFlags.SolidTerrain;
            int slab = Above(ground);
            colony.Grid.Floor[slab] = CoreContent.SlabStructural;
            colony.Grid.FloorStuff[slab] = CoreContent.StuffConcrete;
            colony.Pawns.Support!.MarkSupportedByConstruction(slab);
            colony.RebuildDerived();
            colony.World.Tick();

            Assume.That(colony.Grid.Floor[slab], Is.EqualTo(CoreContent.SlabStructural),
                "the stamped deck is standing before anything is mined");

            MineJobDriver.MineCell(colony.Pawns, ground, CoreContent.TerrainRock);
            colony.World.Tick();

            Assert.That(colony.Grid.Floor[slab], Is.EqualTo(CoreContent.SlabNone),
                "the deck lost its last support and came down");
        }

        /// <summary>
        /// The control without which the test above proves only that slabs fall down. The same dig,
        /// with a second column still holding the deck: nothing comes down, because the rule is
        /// about support and not about digging.
        /// </summary>
        [Test]
        public void ASlabWithAnotherSupportLeftStandsAfterTheDig()
        {
            ColonyWorld colony = Board();
            int ground = GroundLevelCellNear(colony, 3);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            colony.Grid.Terrain[ground] = CoreContent.TerrainRock;
            colony.Grid.Flags[ground] |= CellFlags.SolidTerrain;

            // The second column, one cell along, holding the same slab from beside it.
            int neighbourGround = ground + 1;
            colony.Grid.Terrain[neighbourGround] = CoreContent.TerrainRock;
            colony.Grid.Flags[neighbourGround] |= CellFlags.SolidTerrain;

            int slab = Above(ground);
            colony.Grid.Floor[slab] = CoreContent.SlabStructural;
            colony.Grid.FloorStuff[slab] = CoreContent.StuffConcrete;
            colony.Grid.Floor[Above(neighbourGround)] = CoreContent.SlabStructural;
            colony.Grid.FloorStuff[Above(neighbourGround)] = CoreContent.StuffConcrete;
            colony.RebuildDerived();
            colony.World.Tick();

            MineJobDriver.MineCell(colony.Pawns, ground, CoreContent.TerrainRock);
            colony.World.Tick();

            Assert.That(colony.Grid.Floor[slab], Is.EqualTo(CoreContent.SlabStructural),
                "one support went and another was still there");
        }

        // ---- what a collapse leaves ---------------------------------------------------------------

        /// <summary>
        /// A colonist standing on a floor when it goes rides it down, to the first real floor below
        /// rather than one layer, and remembers it. Nobody is hurt — there is no health model for
        /// `a-02`'s fall-damage number to act on, and inventing one to apply it to would be
        /// inventing a number in order to throw it away (owner, 2026-09-17).
        /// </summary>
        [Test]
        public void AColonistOnACollapsingFloorRidesItDownAndRemembersIt()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);
            int ground = GroundLevelCellNear(colony, 3);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            RaiseNow(colony, ground, BuildingHandle.Wall);
            colony.World.Tick();
            int slab = Above(ground) + 1;          // beside the wall top, held by it
            RaiseNow(colony, slab, BuildingHandle.Floor);
            colony.World.Tick();
            Assume.That(colony.Grid.Floor[slab], Is.EqualTo(CoreContent.SlabBuilt));

            // Stand the colonist on it. Moving a pawn by hand is what a test may do and a system
            // may not; nothing here is asking the mover to walk up there.
            pawn.Cell = slab;
            Assume.That(pawn.Memories.Exists(m => m.ThoughtIndex == ThoughtIndex.Fell), Is.False,
                "it has nothing to remember yet");

            Assert.That(colony.Construction.Demolish(colony.Pawns, ground, out _), Is.True);
            colony.World.Tick();

            Assert.That(colony.Grid.Floor[slab], Is.EqualTo(CoreContent.SlabNone), "the floor went");
            Assert.That(pawn.Cell, Is.Not.EqualTo(slab), "and the colonist did not stay in mid-air");
            Assert.That(colony.Grid.IsWalkable(pawn.Cell), Is.True,
                "it landed somewhere it can actually stand");
            // The memory rather than the mood: mood *drifts* toward the sum of live thoughts on the
            // needs interval, so one tick after a fright it has not moved yet and asserting on it
            // would be asserting on the drift rate.
            Assert.That(pawn.Memories.Exists(m => m.ThoughtIndex == ThoughtIndex.Fell), Is.True,
                "and it remembers the fall");
            Assert.That(pawn.MemoryMoodOffset(colony.World.CurrentTick), Is.LessThan(0),
                "which is worth something to be miserable about");
        }

        /// <summary>
        /// The control for the memory: a colonist standing safely beside a collapse has nothing to
        /// remember. A thought handed out for standing still is how a mood becomes noise, and it is
        /// also how "everybody is sad" passes a test that meant to say "the one who fell is sad".
        /// </summary>
        [Test]
        public void AColonistWhoDidNotFallRemembersNothing()
        {
            ColonyWorld colony = Board();
            Pawn pawn = TheColonist(colony);
            int ground = GroundLevelCellNear(colony, 3);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            RaiseNow(colony, ground, BuildingHandle.Wall);
            colony.World.Tick();
            int slab = Above(ground) + 1;
            RaiseNow(colony, slab, BuildingHandle.Floor);
            colony.World.Tick();

            // On the ground beside it all, where it started, rather than on the thing that falls.
            Assume.That(pawn.Cell, Is.Not.EqualTo(slab));

            Assert.That(colony.Construction.Demolish(colony.Pawns, ground, out _), Is.True);
            colony.World.Tick();

            Assume.That(colony.Grid.Floor[slab], Is.EqualTo(CoreContent.SlabNone), "it did collapse");
            Assert.That(pawn.Memories.Exists(m => m.ThoughtIndex == ThoughtIndex.Fell), Is.False,
                "a colonist that did not move has nothing to remember");
        }

        /// <summary>
        /// The mess. Rubble lands where the debris fell — not where the slab was — is <b>not
        /// solid</b>, so it buries neither the colonist nor a stack, and refuses to be built on
        /// until somebody clears it.
        /// </summary>
        [Test]
        public void ACollapseLeavesRubbleThatIsNotSolidAndCannotBeBuiltOn()
        {
            ColonyWorld colony = Board();
            int ground = GroundLevelCellNear(colony, 3);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            RaiseNow(colony, ground, BuildingHandle.Wall);
            colony.World.Tick();
            int slab = Above(ground) + 1;
            RaiseNow(colony, slab, BuildingHandle.Floor);
            colony.World.Tick();

            int under = slab - Size.LayerStride;
            Assume.That(colony.Grid.Terrain[under], Is.EqualTo(CoreContent.TerrainAir));

            Assert.That(colony.Construction.Demolish(colony.Pawns, ground, out _), Is.True);
            colony.World.Tick();

            Assert.That(colony.Grid.Terrain[under], Is.EqualTo(CoreContent.TerrainRubble),
                "the debris landed on the floor below");
            Assert.That(colony.Grid.IsSolidTerrain(under), Is.False, "rubble buries nothing");
            Assert.That(colony.Grid.IsWalkable(under), Is.True, "a colonist stands in it");
            Assert.That(colony.Construction.Allows(under, BuildingHandle.Wall), Is.False,
                "and nothing is built on it until it is cleared");
        }

        /// <summary>
        /// Clearing it is an ordinary Mine order, which it could not be before: mining wanted solid
        /// terrain and rubble is a heap on a floor. <c>TerrainDef.clearable</c> is the one flag that
        /// distinguishes it from the water, marsh and soil that are also not solid.
        /// </summary>
        [Test]
        public void RubbleCanBeClearedWithAMineOrderAndNothingElseCan()
        {
            ColonyWorld colony = Board();
            int cell = GroundLevelCellNear(colony, 3);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));

            colony.Grid.Terrain[cell] = CoreContent.TerrainRubble;
            Assert.That(colony.Designations.CanMine(cell), Is.True, "a heap can be cleared");

            colony.Grid.Terrain[cell] = CoreContent.TerrainAir;
            Assert.That(colony.Designations.CanMine(cell), Is.False,
                "and open air cannot, which is the control for the flag");
        }

        // ---- taking one away ----------------------------------------------------------------------

        [Test]
        public void AFloorOfOursComesApartAndHalfComesBack()
        {
            ColonyWorld colony = Board();
            int ground = GroundLevelCellNear(colony, 3);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            RaiseNow(colony, ground, BuildingHandle.Wall);
            colony.World.Tick();
            int slab = Above(ground);
            RaiseNow(colony, slab, BuildingHandle.Floor);
            colony.World.Tick();

            Assert.That(colony.Designations.CanDeconstruct(slab), Is.True, "a floor of ours");
            Assert.That(colony.Designations.TryTakeApart(slab, out int building, out int stuff), Is.True);
            Assert.That(building, Is.EqualTo(BuildingHandle.Floor));
            Assert.That(stuff, Is.EqualTo(StuffHandle.Wood));

            int before = OnTheGround(colony, ItemIndex.Wood);
            DeconstructJobDriver.TakeApart(colony.Pawns, slab, building, stuff, tick: 7);

            Assert.That(colony.Grid.Floor[slab], Is.EqualTo(CoreContent.SlabNone), "the floor is gone");
            // Four is even, so half of it is exactly two and no coin is flipped.
            Assert.That(OnTheGround(colony, ItemIndex.Wood), Is.EqualTo(before + 2),
                "half of a four-unit floor came back");
        }

        /// <summary>
        /// The control: a deck the generator stamped is the city's, exactly as a wall it stamped is.
        /// <c>SlabBuilt</c> is what says which, and only <c>ConstructionGrid.Raise</c> writes it.
        /// </summary>
        [Test]
        public void AStampedDeckIsNotOursToTakeApart()
        {
            ColonyWorld colony = Board();
            int ground = GroundLevelCellNear(colony, 3);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            int slab = Above(ground);
            colony.Grid.Floor[slab] = CoreContent.SlabStructural;
            colony.Grid.FloorStuff[slab] = CoreContent.StuffConcrete;

            Assert.That(colony.Designations.CanDeconstruct(slab), Is.False);
            Assert.That(colony.Designations.Designate(Size.FromIndex(slab), DesignationKind.Deconstruct),
                Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(colony.Construction.RemoveSlab(colony.Pawns, slab, out _), Is.False,
                "and the edit itself refuses, not only the order");
        }

        // ---- state ------------------------------------------------------------------------------

        [Test]
        public void ABuiltFloorIsHashedAndSurvivesASaveAndReload()
        {
            ColonyWorld colony = Board();
            int ground = GroundLevelCellNear(colony, 3);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            ulong bare = colony.World.ComputeStateHash().Value;
            RaiseNow(colony, ground, BuildingHandle.Wall);
            colony.World.Tick();
            int slab = Above(ground);
            RaiseNow(colony, slab, BuildingHandle.Floor);
            colony.World.Tick();

            ulong built = colony.World.ComputeStateHash().Value;
            Assert.That(built, Is.Not.EqualTo(bare), "a floor the colony built is state");

            byte[] saved = colony.Save();
            ColonyWorld reloaded = Board();
            reloaded.Load(saved);

            Assert.That(reloaded.Grid.Floor[slab], Is.EqualTo(CoreContent.SlabBuilt));
            Assert.That(reloaded.Grid.FloorStuff[slab], Is.EqualTo(NaturalContent.StuffWood));
            Assert.That(reloaded.World.ComputeStateHash().Value, Is.EqualTo(built));
        }

        /// <summary>
        /// The query the order rule is built on, on its own: full support over something solid,
        /// one less per step out, and nothing at all in open air away from everything.
        /// </summary>
        [Test]
        public void SupportIfSlabAtAnswersTheRuleAndChangesNothing()
        {
            ColonyWorld colony = Board();
            int ground = GroundLevelCellNear(colony, 3);
            Assume.That(ground, Is.GreaterThanOrEqualTo(0));

            SupportSolver solver = colony.Pawns.Support!;
            ulong before = colony.World.ComputeStateHash().Value;

            // Over solid ground: the cell above the walkable one is not grounded, but the walkable
            // cell itself is — there is earth directly beneath it.
            Assert.That(solver.SupportIfSlabAt(ground), Is.EqualTo(solver.MaxSupport),
                "a slab on solid ground has full support");

            // Two layers up in open air, away from anything: nothing to hold it.
            int inTheAir = Above(Above(ground));
            Assert.That(solver.SupportIfSlabAt(inTheAir), Is.Zero, "and one in mid-air has none");

            Assert.That(colony.World.ComputeStateHash().Value, Is.EqualTo(before),
                "asking changed nothing");
        }
    }
}
