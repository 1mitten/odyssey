#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// A player arms a tool, drags a box over the world, and the colony has orders.
    ///
    /// <para><b>This is the first thing that has ever executed designation.</b> The director's
    /// geometry is covered by the fast tier and the wiring was shipped compiled-but-unrun, because
    /// it needs a mouse — and the playtest that would have confirmed it reported "nothing happened"
    /// twice, once against a stale checkout and once against a scene built before the presenter
    /// existed. Neither was a fault in the feature and neither could tell you that. A test that
    /// drives the real pointer is the only thing that separates "absent" from "broken".</para>
    ///
    /// <para><b>The control matters as much as the test.</b> With no tool armed the same drag must
    /// leave the world untouched, because the same gesture belongs to selection then. Without that
    /// half, "a drag marks cells" would pass just as well if a drag <i>always</i> marked cells —
    /// which would mean every box-select scarring the map behind it.</para>
    /// </summary>
    public class DesignateDragTests
    {
        MouseHarness _mouse = null!;

        /// <summary>Frames to let the drag reach the rig, the intent reach the tick, and the tick reach the snapshot.</summary>
        const int SettleFrames = 12;

        [SetUp]
        public void AddAMouse() => _mouse = new MouseHarness();

        [TearDown]
        public void RemoveTheMouse() => _mouse.Dispose();

        [UnityTest]
        public IEnumerator ADragWithAToolArmedGivesTheColonyOrders()
        {
            GameObject root = BuildBareWorld(out OdysseyBootstrap boot, out SliceCameraRig rig,
                out DesignatePresenter designate);
            try
            {
                yield return RigWorld.WarmUp();
                yield return RigWorld.SettleCamera(rig);

                Assert.That(MarkedCells(boot), Is.Zero,
                    "the bare scenario is meant to start with no standing orders, so this test " +
                    "cannot tell its own drag from the scenario's");

                Camera camera = Camera.main!;
                Assume.That(TryFindATreeOnScreen(boot, camera, out Vector2 tree), Is.True,
                    "no tree is visible on this board, so a felling order has nothing to aim at");

                designate.Director.Tool = DesignateTool.Fell;
                yield return _mouse.Drag(tree - new Vector2(60f, 60f), tree + new Vector2(60f, 60f));
                for (int i = 0; i < SettleFrames; i++) yield return null;

                Assert.That(MarkedCells(boot), Is.GreaterThan(0),
                    "a drag with the cutting tool armed left no order anywhere. The gesture never " +
                    "reached DesignatePresenter, or the cells it resolved to were all refused.");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// The discriminator, and it comes straight from the owner's playtest: "C only works if
        /// you push C and click on a tree. No dragging or dropping works."
        ///
        /// <para>A click and a drag go through the same branch of the rig and the same presenter;
        /// the only difference is whether the two ends of the gesture are the same point. So if
        /// this passes while the drag fails, the fault is in the drag and the oracle below is
        /// sound. If <b>both</b> fail, the oracle is the fault — <c>MarkedCells</c> reads the
        /// snapshot's designation channel, and a channel that is never published reads as zero
        /// orders however well the feature works, which the no-tool control could never reveal
        /// because zero is what it asserts.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator AClickWithAToolArmedGivesOneOrder()
        {
            GameObject root = BuildBareWorld(out OdysseyBootstrap boot, out SliceCameraRig rig,
                out DesignatePresenter designate);
            try
            {
                yield return RigWorld.WarmUp();
                yield return RigWorld.SettleCamera(rig);
                Assume.That(MarkedCells(boot), Is.Zero);

                Camera camera = Camera.main!;
                Assume.That(TryFindATreeOnScreen(boot, camera, out Vector2 tree), Is.True,
                    "no tree is visible on this board, so a felling order has nothing to aim at");

                designate.Director.Tool = DesignateTool.Fell;
                yield return _mouse.Click(tree);
                for (int i = 0; i < SettleFrames; i++) yield return null;

                Assert.That(MarkedCells(boot), Is.GreaterThan(0),
                    "a click with the cutting tool armed left no order. If the drag test fails " +
                    "too then this oracle is what is broken, not designation: MarkedCells reads " +
                    "the snapshot's designation channel, and an unpublished channel reads as zero.");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// The control. The same drag, the same world, no tool: the world must be untouched,
        /// because the gesture belongs to selection when nothing is armed.
        /// </summary>
        [UnityTest]
        public IEnumerator ADragWithNoToolArmedMarksNothing()
        {
            GameObject root = BuildBareWorld(out OdysseyBootstrap boot, out SliceCameraRig rig,
                out DesignatePresenter designate);
            try
            {
                yield return RigWorld.WarmUp();
                yield return RigWorld.SettleCamera(rig);

                Assume.That(designate.Director.Tool, Is.EqualTo(DesignateTool.None));

                Camera camera = Camera.main!;
                Assume.That(TryFindATreeOnScreen(boot, camera, out Vector2 tree), Is.True);

                yield return _mouse.Drag(tree - new Vector2(60f, 60f), tree + new Vector2(60f, 60f));
                for (int i = 0; i < SettleFrames; i++) yield return null;

                Assert.That(MarkedCells(boot), Is.Zero,
                    "a drag with NO tool armed marked cells anyway. Every box-select would scar " +
                    "the map behind it, and the gate on SliceCameraRig.WorldToolArmed is not holding.");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// A world with nothing already marked, and a presenter to arm.
        ///
        /// <para>The bare scenario rather than the playtest one: the playtest marks every tree
        /// within ten cells of the start before the first tick, which is exactly the population a
        /// drag near the middle of the screen would cover, so the test could not tell its own
        /// orders from the scenario's.</para>
        /// </summary>
        static GameObject BuildBareWorld(out OdysseyBootstrap boot, out SliceCameraRig rig,
            out DesignatePresenter designate)
        {
            GameObject root = RigWorld.Build(out boot, out rig, b =>
            {
                b.scenario = StartingScenario.Bare;
                b.gameObject.AddComponent<DesignatePresenter>();
            });

            designate = boot.GetComponent<DesignatePresenter>();
            return root;
        }

        /// <summary>
        /// How many cells of the active layer carry a standing order, read off the published
        /// frame — the same channel the renderer draws from, so this asserts what a player would
        /// see rather than what the simulation privately knows.
        /// </summary>
        static int MarkedCells(OdysseyBootstrap boot)
        {
            WorldSnapshot? snapshot = boot.World?.Views.Current;
            if (snapshot == null) return 0;

            int marked = 0;
            foreach (byte order in snapshot.Designations)
                if (order != 0) marked++;
            return marked;
        }

        /// <summary>
        /// Where a tree is, on screen.
        ///
        /// <para><b>Both of these tests failed before this existed, and the reason was the test
        /// rather than the feature.</b> They clicked and dragged at fractions of the screen and
        /// asserted that orders appeared — but felling only applies to a tree, so a gesture that
        /// lands on grass is refused, correctly and silently, and the assertion reads exactly like
        /// a broken feature. An oracle has to aim at something it knows is there.</para>
        ///
        /// <para>The tree comes from the render mirror, which is what the camera is looking at, so
        /// projecting its floor centre gives a point the ray will meet. Cells behind the camera or
        /// off the screen are skipped: the board is bigger than the view.</para>
        /// </summary>
        static bool TryFindATreeOnScreen(OdysseyBootstrap boot, Camera camera, out Vector2 at)
        {
            at = default;
            WorldRenderModel? model = boot.Model;
            if (model == null) return false;

            GridSize size = model.Size;
            for (int i = 0; i < size.CellCount; i++)
            {
                if (!NaturalContent.IsTree(model.EdificeDef(i))) continue;

                Vector3 point = camera.WorldToScreenPoint(CellMetrics.FloorCentre(size.FromIndex(i)));
                if (point.z <= 0f) continue;
                if (point.x < 0f || point.y < 0f || point.x > Screen.width || point.y > Screen.height) continue;

                at = new Vector2(point.x, point.y);
                return true;
            }

            return false;
        }

        static Vector2 Near(float x, float y) => new Vector2(Screen.width * x, Screen.height * y);
    }
}
