#nullable enable
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Standing gate rule 3 on the real world (OQ-09): tick to N, save, load into a fresh build,
    /// tick both on to N+K, and require the hashes to agree.
    ///
    /// <para>The round trip on its own is the weaker half. A save can restore every field it
    /// wrote and still produce a world that <i>behaves</i> differently, because what it did not
    /// write is rebuilt wrongly or not at all — a stale region graph, a support array full of
    /// zeros, a reservation table that no longer matches what the pawns think they hold. Ticking
    /// both worlds on afterwards is what catches that, and it is why the gate is written as
    /// resume equivalence rather than as a field-by-field comparison.</para>
    /// </summary>
    public class WorldRoundTripTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);
        const uint Seed = 4242;
        static ColonyWorld Fresh() => ColonyWorld.Build(Size, Seed, ScenarioDef.Bare());

        static ulong Hash(ColonyWorld world) => world.World.ComputeStateHash().Value;

        [Test]
        public void SavingTwiceProducesIdenticalBytes()
        {
            // Byte stability on the real world, not on a fixture. Everything that iterates
            // unordered is in here: the reservation dictionary, the item list, the pawn registry.
            var world = Fresh();
            world.World.Tick(1_500);

            Assert.That(world.Save(), Is.EqualTo(world.Save()));
        }

        [Test]
        public void TheRoundTripReproducesTheStateExactly()
        {
            var original = Fresh();
            original.World.Tick(3_000);
            ulong before = Hash(original);

            var restored = Fresh();
            var header = restored.Load(original.Save());

            Assert.That(header.SkippedSections, Is.Empty, "a section this build wrote was not read back");
            Assert.That(header.Tick, Is.EqualTo(3_000));
            Assert.That(restored.World.CurrentTick, Is.EqualTo(3_000));
            Assert.That(Hash(restored), Is.EqualTo(before), "the hash differs immediately after loading");
        }

        [Test]
        public void ALoadedWorldResumesIdentically()
        {
            const int SaveAt = 3_000;
            const int ThenFor = 3_000;

            var original = Fresh();
            original.World.Tick(SaveAt);
            byte[] bytes = original.Save();

            var restored = Fresh();
            restored.Load(bytes);

            original.World.Tick(ThenFor);
            restored.World.Tick(ThenFor);

            Assert.That(restored.World.CurrentTick, Is.EqualTo(SaveAt + ThenFor));
            Assert.That(Hash(restored), Is.EqualTo(Hash(original)),
                $"the worlds parted somewhere in the {ThenFor} ticks after the load");
        }

        [Test]
        public void TheDerivedStateIsRebuiltRatherThanRestored()
        {
            // Support is not in the file, so this is the assertion that the load path recomputes
            // it. Removing the rebuild fails this test, which is how it is known to bite rather
            // than to agree by luck.
            //
            // The non-zero guard is not decoration. A barren meadow has no slabs on it, so it
            // would be easy to assume every support value is zero and that comparing them proves
            // nothing — but a full solve gives S_max to every cell standing on solid ground,
            // which is most of the map. The guard is what keeps that true if the map ever
            // changes underneath this test.
            //
            // Reachability is not compared here because it is not on the grid: NavGraph keeps
            // its own region array, and the resume half of this test is what proves it was
            // rebuilt. (A per-cell region field that nothing wrote was removed by OQ-38.)
            var original = Fresh();
            original.World.Tick(1_000);

            var restored = Fresh();
            restored.Load(original.Save());

            Assert.That(original.Grid.Support, Is.Not.All.Zero, "no support anywhere — the check proves nothing");
            Assert.That(restored.Grid.Support, Is.EqualTo(original.Grid.Support),
                "support was not rebuilt to match a freshly generated world");
        }

        [Test]
        public void AWorldWhoseGridHasChangedStillResumesIdentically()
        {
            // The case the rebuild actually exists for, and the one the tests above cannot reach.
            // Everywhere else the saved grid is identical to what the seed generates, so a stale
            // navigation graph built at construction happens to still be right and a missing
            // rebuild goes unnoticed. Here the grid is changed after generation — a wall dropped
            // across the colony, which is what a build order will do in M3 — so the graph the
            // fresh world constructed is wrong for the world being loaded into it.
            var original = Fresh();
            Wall(original);
            original.World.Tick(2_000);
            byte[] bytes = original.Save();

            var restored = Fresh();
            restored.Load(bytes);

            Assert.That(Hash(restored), Is.EqualTo(Hash(original)), "the wall did not survive the round trip");

            original.World.Tick(2_000);
            restored.World.Tick(2_000);

            Assert.That(Hash(restored), Is.EqualTo(Hash(original)),
                "the worlds parted after resuming from a grid that had been changed");
        }

        /// <summary>
        /// Drop a blocking edifice across the middle of the colony's layer, the way a built wall
        /// would. Written straight into the grid because the build order does not exist until M3;
        /// what matters here is only that the grid stops matching what the seed generates.
        /// </summary>
        static void Wall(ColonyWorld world)
        {
            var grid = world.Grid;
            int y = world.Start.Y;
            int z = world.Start.Z;

            for (int x = 0; x < grid.Size.SizeX; x++)
            {
                if (x == world.Start.X) continue; // Leave a gap, so nothing is sealed in.
                grid.Flags[grid.Index(x, z, y)] |= CellFlags.BlockingEdifice;
            }

            world.RebuildDerived();
        }

        /// <summary>
        /// A wall the colony built is a wall to the paths after a load (design 33 §19a). The board a
        /// save is read over is the one the seed generates, and its navigation graph was built for
        /// that board: every block the loaded grid differs in must be flooded again, not only the
        /// ones something on the load path happens to dirty. Measured in the owner's save
        /// (2026-09-24): seven walls of a house on the first column of a navigation block were
        /// walkable after the load, and marauders walked through them and chose sides inside them.
        ///
        /// <para><see cref="AWorldWhoseGridHasChangedStillResumesIdentically"/> cannot see this: its
        /// wall is written straight into the grid and nothing marks the graph dirty in either
        /// world, so the original is exactly as stale as the copy and the hashes agree. Here the
        /// walls go up through <c>ConstructionGrid.Raise</c>, which dirties the graph, so the
        /// original is right and the copy is compared against it cell by cell.</para>
        /// </summary>
        [Test]
        public void ABuiltWallIsStillAWallToThePathsAfterTheLoad()
        {
            var original = Fresh();
            original.World.Tick();
            CellRef at = original.Start;
            var walls = new System.Collections.Generic.List<int>();
            for (int dz = -2; dz <= 2; dz++)
            {
                int cell = original.Pawns.Cells.NearestWalkableInColumn(at.X + 12, at.Z + dz, at.Y);
                Assert.That(original.Construction.Place(Size.FromIndex(cell), BuildingHandle.Wall, StuffHandle.Wood, 0),
                    Is.EqualTo(IntentRejection.None), $"could not order a wall at {Size.FromIndex(cell)}");
                Assert.That(original.Construction.Raise(original.Pawns, cell), Is.True);
                walls.Add(cell);
            }
            original.World.Tick();
            foreach (int wall in walls)
                Assert.That(original.Pawns.Nav.Grid.CanEnter(wall, TraverseMode.Colonist), Is.False,
                    $"the control: the wall at {Size.FromIndex(wall)} is a wall to the world that built it");

            var restored = Fresh();
            restored.Load(original.Save());

            foreach (int wall in walls)
                Assert.That(restored.Pawns.Nav.Grid.CanEnter(wall, TraverseMode.Colonist), Is.False,
                    $"the wall at {Size.FromIndex(wall)} can be walked through after the load");
            Assert.That(restored.Pawns.Nav.Grid.Flags, Is.EqualTo(original.Pawns.Nav.Grid.Flags),
                "the loaded world's navigation disagrees with the world that was saved");
        }

        [Test]
        public void ReservationsComeBackAgreeingWithWhatThePawnsHold()
        {
            // The table is rebuilt from each pawn's held list on load. If the two ever disagree a
            // pawn is holding a claim nothing knows about, which deadlocks that target for the
            // rest of the run — silently, and only under save and load.
            var original = Fresh();
            original.World.Tick(2_000);

            var restored = Fresh();
            restored.Load(original.Save());

            int held = 0;
            var pawns = restored.Pawns.Pawns.All;
            for (int i = 0; i < pawns.Count; i++) held += pawns[i].HeldReservations.Count;

            Assert.That(restored.Pawns.Reservations.ActiveClaims, Is.EqualTo(held),
                "the reservation table and the pawns disagree about what is claimed");
            Assert.That(restored.Pawns.Reservations.ActiveClaims,
                Is.EqualTo(original.Pawns.Reservations.ActiveClaims),
                "a different number of claims survived the round trip");
        }

        [Test]
        public void ASaveIsRefusedByAWorldItDoesNotDescribe()
        {
            var original = Fresh();
            original.World.Tick(100);
            byte[] bytes = original.Save();

            var otherSeed = ColonyWorld.Build(Size, Seed + 1, ScenarioDef.Bare());
            Assert.Throws<SaveLoadException>(() => otherSeed.Load(bytes), "a foreign seed was accepted");

            var otherSize = ColonyWorld.Build(new GridSize(40, 40, 16), Seed, ScenarioDef.Bare());
            Assert.Throws<SaveLoadException>(() => otherSize.Load(bytes), "a different map size was accepted");
        }
    }
}
