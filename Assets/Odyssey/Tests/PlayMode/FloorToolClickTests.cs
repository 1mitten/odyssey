#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using UnityEngine;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// <b>Does clicking with the floor tool armed put a floor down?</b>
    ///
    /// <para>The owner has now reported twice that it does not, and both tiers have been green
    /// throughout. That is the shape of a fault living in the wiring rather than in any rule: the
    /// Sim tier proves <c>ConstructionGrid.Place</c>, the EditMode tier proves
    /// <c>SlicePicker</c> and the seam between them, and <b>nothing at all</b> proves that a press
    /// on a mouse button reaches either. Between the two there are a rig, a presenter, a director
    /// and a gesture, and every one of them can swallow a click in silence.</para>
    ///
    /// <para>So this is the whole chain, driven by a real device: a pointer at a screen position,
    /// through <c>SliceCameraRig</c>'s own gesture handling, through
    /// <c>DesignatePresenter.OnToolDrag</c>, through <c>DesignateDirector</c>, onto the intent
    /// queue, into the simulation, and back out as a site on the grid. It is the first test in the
    /// project that asks whether the game can be played rather than whether it is correct.</para>
    ///
    /// <para><b>Both are ignored, and the reason is the finding.</b> The harness cannot press a
    /// button at all: a PlayMode test's input update type is <c>Editor</c> and every edge property
    /// is gated on a player update, so <c>wasPressedThisFrame</c> never fires for game code
    /// (<see cref="MouseHarness"/>, failure four; <c>InputHarnessTests</c> has carried an ignored
    /// test saying so). <c>SliceCameraRig</c> reads exactly that edge, so a click cannot reach the
    /// game from here.</para>
    ///
    /// <para><b>That gap is why this class of fault keeps escaping.</b> The floor tool was
    /// armable, draggable and inert for a day, and the thing that would have caught it in a second
    /// is the one thing no tier can do. Written and ignored rather than not written, so the day
    /// the harness can press a button there is something to un-ignore — and ignored rather than
    /// <c>Assume</c>d, because an <c>Assume</c> skips in silence, which is how six mining tests sat
    /// dead on main.</para>
    ///
    /// <para>What <i>is</i> covered meanwhile, one level down:
    /// <c>Odyssey.Tests.Presentation.FloorToolReachTests</c> feeds <c>SlicePicker</c>'s answer
    /// straight into <c>ConstructionGrid.Place</c>, which is the seam the two rules meet at. What
    /// remains untested is only the stretch between a mouse button and that cell.</para>
    /// </summary>
    public class FloorToolClickTests
    {
        MouseHarness _mouse = null!;

        [SetUp]
        public void AddAMouse() => _mouse = new MouseHarness();

        [TearDown]
        public void RemoveTheMouse() => _mouse.Dispose();

        /// <summary>
        /// The control, and it has to come first: a <b>wall</b> ordered by clicking is what the
        /// owner has already played and accepted, so if this fails the fault is in the harness or
        /// the fixture and nothing below it means anything.
        /// </summary>
        [UnityTest]
        [Ignore("The harness cannot press a button - a PlayMode test's input update type is "
                + "Editor and every edge property is gated on a player update. Paired with "
                + "InputHarnessTests.AClickIsSeenAsAPressAndARelease; un-ignore both together.")]
        public IEnumerator AClickWithTheWallToolArmedOrdersAWall()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out SliceCameraRig rig);
            try
            {
                yield return Ready(rig);

                ColonyWorld colony = boot.Colony!;
                CellRef ground = WalkableNearTheStart(colony);
                boot.Directors!.Designate.ArmBuild(BuildingHandle.Wall);

                yield return ClickOn(rig, ground);
                yield return Ticks();

                Assert.That(colony.Construction.At(colony.Grid.Size.Index(ground.X, ground.Z, ground.Y)),
                    Is.EqualTo(BuildingHandle.Wall),
                    "a click with the wall tool armed must put a wall site down — if this fails, " +
                    "nothing else in this fixture is evidence of anything");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The subject. A wall is raised first, because a slab has to rest on something, and the
        /// click lands on that wall — which is what <c>StandingOver</c> exists to lift.
        /// </summary>
        [UnityTest]
        [Ignore("The harness cannot press a button - a PlayMode test's input update type is "
                + "Editor and every edge property is gated on a player update. Paired with "
                + "InputHarnessTests.AClickIsSeenAsAPressAndARelease; un-ignore both together.")]
        public IEnumerator AClickWithTheFloorToolArmedOrdersAFloorOnTopOfAWall()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out SliceCameraRig rig);
            try
            {
                yield return Ready(rig);

                ColonyWorld colony = boot.Colony!;
                GridSize size = colony.Grid.Size;
                CellRef ground = WalkableNearTheStart(colony);
                int groundCell = size.Index(ground.X, ground.Z, ground.Y);

                // A wall, put up the way the last stroke of a build job puts one up.
                Assume.That(colony.Construction.Place(ground, BuildingHandle.Wall, StuffHandle.Wood),
                    Is.EqualTo(IntentRejection.None));
                colony.Construction.Raise(colony.Pawns, groundCell);
                yield return Ticks();

                boot.Directors!.Designate.ArmBuild(BuildingHandle.Floor);
                yield return ClickOn(rig, ground);
                yield return Ticks();

                int above = groundCell + size.LayerStride;
                Assert.That(colony.Construction.At(above), Is.EqualTo(BuildingHandle.Floor),
                    "a click on a wall with the floor tool armed must put a floor site on top of it");
                Assert.That(colony.Construction.At(groundCell), Is.EqualTo(BuildingHandle.None),
                    "and nothing inside the wall");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---- the fixture ---------------------------------------------------------------------

        /// <summary>
        /// The rig world with a <see cref="DesignatePresenter"/> on it, which is the component
        /// under test as much as anything else here: it is what turns a gesture into an intent,
        /// and <see cref="RigWorld"/> does not add it because no test has needed it until now.
        /// </summary>
        static GameObject Build(out OdysseyBootstrap boot, out SliceCameraRig rig)
        {
            GameObject root = RigWorld.Build(out boot, out rig);
            boot.gameObject.AddComponent<DesignatePresenter>();

            // The director is deliberately NOT taken here. `HudDirectors` is built when the world
            // is, which is a frame away, so reading it now hands back null and the first version
            // of this fixture failed on exactly that - a NullReferenceException that looked, for
            // one alarming minute, like the wiring fault it was written to find.
            return root;
        }

        static IEnumerator Ready(SliceCameraRig rig)
        {
            yield return RigWorld.WarmUp();
            yield return RigWorld.SettleCamera(rig);
        }

        static IEnumerator Ticks()
        {
            for (int i = 0; i < 6; i++) yield return null;
        }

        /// <summary>A cell near the start that a colonist could stand in, so a wall may go there.</summary>
        static CellRef WalkableNearTheStart(ColonyWorld colony)
        {
            GridSize size = colony.Grid.Size;
            CellRef start = colony.Start;
            for (int radius = 1; radius <= 6; radius++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = start.X + dx, z = start.Z + dz;
                if (!size.Contains(x, z, start.Y)) continue;

                int cell = size.Index(x, z, start.Y);
                if (!colony.Grid.IsWalkable(cell)) continue;
                if (colony.Construction.Allows(cell, BuildingHandle.Wall))
                    return new CellRef(x, z, start.Y);
            }

            throw new AssertionException("no cell near the start will take a wall");
        }

        /// <summary>
        /// Click the middle of a cell, by asking the camera where that cell is on screen.
        ///
        /// <para>Deliberately not a screen position picked by hand: the point of the test is that
        /// the pointer and the world agree about which cell is under it, and a hard-coded pixel
        /// would be asserting something else. The rig's own camera does the projection, so this is
        /// the inverse of the ray the picker will cast.</para>
        /// </summary>
        IEnumerator ClickOn(SliceCameraRig rig, CellRef cell)
        {
            var camera = rig.GetComponent<Camera>();
            Vector3 world = Odyssey.Presentation.Rendering.GroundRelief.Lift(
                Odyssey.Presentation.Rendering.CellMetrics.Centre(cell.X, cell.Z, cell.Y));
            Vector3 screen = camera.WorldToScreenPoint(world);

            Assume.That(screen.z, Is.GreaterThan(0f), "the cell must be in front of the camera");
            yield return _mouse.Click(new Vector2(screen.x, screen.y));
        }
    }
}
