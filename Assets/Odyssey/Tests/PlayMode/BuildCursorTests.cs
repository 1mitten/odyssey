#nullable enable
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// Does the build cursor draw in a running game, and if not, which guard stopped it.
    ///
    /// <para><b>Why this is a PlayMode test and not a unit one.</b> The cursor has now been
    /// reported missing four times and found by reading exactly once. Everything it depends on
    /// passes in isolation: the director arms, the rig's hover branch is sound, the palette's every
    /// chip arms its tool, the preview gate is false at rest. The cursor is drawn by
    /// <c>OdysseyBootstrap</c> in a real frame, from state three other components own, and nothing
    /// below that level can see the four of them disagree.</para>
    ///
    /// <para><b>It reads the log rather than the screen.</b> Every early return in the cursor path
    /// reports itself (<c>OdysseyBootstrap.WhyNoCursor</c>), and a drawn cursor says where it went.
    /// Those lines are the measurement; this test runs a frame with a tool armed and a hover set
    /// and prints whatever they say, so a fault that has cost four rounds of guesses costs one
    /// test run instead. <b>The mouse is not used</b> — <c>docs/lessons.md</c> records that a
    /// PlayMode test cannot press a button — so the hover is set on the director directly, which is
    /// exactly what the rig would do.</para>
    /// </summary>
    public class BuildCursorTests
    {
        readonly List<string> _cursorLines = new List<string>();

        void Collect(string message, string stack, LogType type)
        {
            if (message.StartsWith("[Cursor]")) _cursorLines.Add(message);
        }

        [UnityTest]
        public IEnumerator TheBuildCursorDrawsWithAWallArmedAndThePointerOverGround()
        {
            _cursorLines.Clear();
            Application.logMessageReceived += Collect;

            GameObject root = RigWorld.Build(out OdysseyBootstrap boot, out SliceCameraRig rig);
            boot.gameObject.AddComponent<DesignatePresenter>();
            try
            {
                yield return RigWorld.WarmUp();
                yield return RigWorld.SettleCamera(rig);

                ColonyWorld colony = boot.Colony!;
                CellRef ground = colony.Grid.Size.FromIndex(WalkableNear(colony));

                boot.Directors!.Designate.ArmBuild(BuildingHandle.Wall);

                // The rig re-answers the hover every frame from a pointer this test cannot move, so
                // the hover is re-stated every frame too. Whichever of the two writes last, one
                // frame in six will have had it set when the cursor was drawn — and if none did,
                // that is itself the finding and the log will say so.
                for (int i = 0; i < 6; i++)
                {
                    boot.Directors!.Designate.HoverAt(ground);
                    yield return null;
                }

                foreach (string line in _cursorLines) Debug.Log($"[CursorProbe] {line}");

                Assert.That(boot.Directors!.Designate.Tool, Is.EqualTo(DesignateTool.Build),
                    "the wall tool did not stay armed for a single frame");
                Assert.That(_cursorLines, Is.Not.Empty,
                    "the cursor path said nothing at all, so DrawHoverGhost is never being reached: " +
                    "DrawToolPreview returned before it, or the render pass did not run");
                Assert.That(_cursorLines.Exists(l => l.Contains("drawn:")), Is.True,
                    "the cursor never drew. The reason is the line above this one: " +
                    string.Join(" | ", _cursorLines));
            }
            finally
            {
                Application.logMessageReceived -= Collect;
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// The cursor still draws after a session is torn down and rebuilt — which is what every
        /// New game and every Load now does.
        ///
        /// <para><b>This is the test that was missing.</b> <c>TeardownSession</c> dropped
        /// <c>_designate</c> along with the renderer and the colony, and it was the one line in
        /// that list that was wrong: the presenter is a sibling component found once in
        /// <c>Start</c>, and <c>Start</c> does not run twice. From the first teardown onwards
        /// <c>DrawToolPreview</c> returned on its first line, so the cursor, the drag box and the
        /// run's ghosts were all silently gone.</para>
        ///
        /// <para>It did not matter while a session was built once and never torn down, which is
        /// why it survived every test here: they all build one session. The start menu made
        /// teardown-and-rebuild the ordinary path into a game, and this asserts the ordinary
        /// path.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheCursorSurvivesATeardownAndRebuild()
        {
            _cursorLines.Clear();
            Application.logMessageReceived += Collect;

            GameObject root = RigWorld.Build(out OdysseyBootstrap boot, out SliceCameraRig rig);
            boot.gameObject.AddComponent<DesignatePresenter>();
            try
            {
                yield return RigWorld.WarmUp();
                yield return RigWorld.SettleCamera(rig);

                // What the start menu does on New game and on Load.
                boot.TeardownSession();
                yield return null;
                boot.BuildSession();
                yield return RigWorld.WarmUp();

                ColonyWorld colony = boot.Colony!;
                CellRef ground = colony.Grid.Size.FromIndex(WalkableNear(colony));

                _cursorLines.Clear();
                boot.Directors!.Designate.ArmBuild(BuildingHandle.Wall);
                for (int i = 0; i < 6; i++)
                {
                    boot.Directors!.Designate.HoverAt(ground);
                    yield return null;
                }

                foreach (string line in _cursorLines) Debug.Log($"[CursorProbe] rebuilt: {line}");

                Assert.That(_cursorLines, Is.Not.Empty,
                    "after a rebuild the cursor path says nothing at all, which means " +
                    "DrawToolPreview is returning before it — the presenter reference was dropped");
                Assert.That(_cursorLines.Exists(l => l.Contains("drawn:")), Is.True,
                    "the cursor did not draw after a rebuild: " + string.Join(" | ", _cursorLines));
            }
            finally
            {
                Application.logMessageReceived -= Collect;
                Object.Destroy(root);
            }
        }

        static int WalkableNear(ColonyWorld colony)
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
                if (colony.Grid.IsWalkable(cell) && colony.Construction.Allows(cell)) return cell;
            }

            return size.Index(start.X, start.Z, start.Y);
        }
    }
}
