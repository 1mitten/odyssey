#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// <b>Can a player give a bed to a colonist?</b>
    ///
    /// <para><b>Written because the answer was no, twice, and nothing could tell us.</b> The owner
    /// reported being unable to assign a bed on 2026-09-17 and again on 2026-09-18, the second
    /// time having found and clicked the button. Both fixes in between were made without any test
    /// that could exercise the path — the model tests stop at <c>InspectModel</c>, the simulation
    /// tests submit the intent directly, and between them lies every part that was actually
    /// broken: whether the row's click reaches the shell, whether the popover appears anywhere a
    /// player would see it, and whether picking a name reaches the simulation.</para>
    ///
    /// <para>This is that middle. It drives the live panel — a real <c>ClickEvent</c> on the real
    /// row — and asserts on the world afterwards, which is the only shape of test that could have
    /// caught either report. <c>CLAUDE.md</c>'s standing note that "nothing tests that a click
    /// reaches the game" is about the <i>input system</i>, whose presses a PlayMode test cannot
    /// fake; a UI Toolkit event is not subject to that and can be sent outright.</para>
    /// </summary>
    public class BedOwnerPickerTests
    {
        /// <summary>Send the click a player's mouse would, to the element they would hit.</summary>
        static void Click(VisualElement element)
        {
            using var click = ClickEvent.GetPooled();
            click.target = element;
            element.SendEvent(click);
        }

        /// <summary>The pane row whose name is this, or null. The rows are reused, so it is found
        /// by the label the model wrote rather than by an index.</summary>
        static VisualElement? RowNamed(VisualElement root, string name)
        {
            foreach (VisualElement row in root.Query(className: "inspect__row").ToList())
            {
                Label? label = row.Q<Label>(className: "inspect__rowname");
                if (label != null && label.text == name) return row;
            }
            return null;
        }

        /// <summary>
        /// Order and raise a bed on the first pair of cells near the start that will take one,
        /// and answer with its head cell. Straight through the construction grid: what this
        /// fixture is about begins at the pane, not at the build tool.
        /// </summary>
        static int RaiseABedNearTheStart(Odyssey.Sim.Pawns.ColonyWorld colony)
        {
            GridSize size = colony.Grid.Size;
            CellRef start = colony.Start;

            for (int radius = 1; radius < 10; radius++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius) continue;
                int x = start.X + dx, z = start.Z + dz;
                if (!size.Contains(x, z, start.Y)) continue;

                int head = size.Index(x, z, start.Y);
                if (!colony.Construction.Allows(head, BuildingHandle.Bed)) continue;

                int foot = EdificeFootprint.SecondCell(head, CoreContent.EdificeBed, 0, size);
                if (foot < 0 || !colony.Construction.Allows(foot, BuildingHandle.Bed)) continue;

                if (colony.Construction.Place(size.FromIndex(head), BuildingHandle.Bed,
                        StuffHandle.Wood, facing: 0) != IntentRejection.None) continue;

                colony.Construction.Raise(colony.Pawns, head, (byte)QualityHandle.Normal);
                return head;
            }

            return -1;
        }

        [UnityTest]
        public IEnumerator ClickingAssignAndPickingAColonistGivesThemTheBed()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out _, out _);
            try
            {
                yield return RigWorld.WarmUp();
                Assert.That(boot.World, Is.Not.Null, "the bootstrap never built a world");
                Assert.That(boot.Colony, Is.Not.Null, "the bootstrap never built a colony");

                // A bed on the board, raised the way the game raises one.
                int head = RaiseABedNearTheStart(boot.Colony!);
                Assert.That(head, Is.GreaterThanOrEqualTo(0), "no room for a bed near the start");

                // **Selected the way a click selects**, which is three steps and not one. The
                // pane's facts come from a CellDetail row, and that row exists only because
                // SelectionPresenter asks a QueryCell question and republishes before changing the
                // selection. Calling Selection.Pick on its own skips the question, so the pane
                // gets no bed facts and shows no owner row — which is how the first run of this
                // fixture failed, and it was the test that was wrong rather than the game.
                //
                // Mirrored here rather than driven through the presenter because the presenter's
                // own entry point is a pointer event from the camera rig, and those are input
                // system presses that a PlayMode test genuinely cannot fake.
                CellRef cell = boot.Colony!.Grid.Size.FromIndex(head);
                boot.World!.Intents.Submit(new Intent(IntentKind.QueryCell, cell));
                boot.World.RepublishViews();
                boot.Directors!.Selection.Pick(cell, default, boot.World.Views.Current);

                var doc = root.GetComponentInChildren<UIDocument>();
                VisualElement panel = doc!.rootVisualElement;

                // The cell question is answered on the next drained tick, and the pane is rebuilt
                // from the answer. Waited for by the row appearing rather than by a flag on the
                // model: the row is what a player has, and the model is private to the shell for
                // good reason.
                VisualElement? owner = null;
                for (int i = 0; i < 240 && owner == null; i++)
                {
                    owner = RowNamed(panel, "owner");
                    if (owner == null) yield return null;
                }

                Assert.That(owner, Is.Not.Null, "the pane never showed an owner row for the bed");

                // Armed before it is clicked. Split out so a failure says which half is wrong:
                // a row that was never armed is a model or sync fault, and a row that is armed and
                // does nothing is the handler's.
                Label? armed = owner!.Q<Label>(className: "inspect__rowvalue");
                Assert.That(owner.ClassListContains("inspect__row--pick"), Is.True,
                    $"the owner row was never armed as pickable (its value reads '{armed?.text}')");

                // ---- the click the owner made, twice, over two sessions -----------------------
                Click(owner);
                for (int i = 0; i < 10; i++) yield return null;

                VisualElement? picker = panel.Q(className: "bedowner");
                Assert.That(picker, Is.Not.Null, "clicking the owner row raised no picker at all");
                Assert.That(picker!.style.display.value, Is.EqualTo(DisplayStyle.Flex),
                    "the picker was built and left hidden");

                // **Somewhere a player would actually see it.** A popover that opens off the
                // bottom of the screen, or three hundred pixels from the row that raised it, is
                // indistinguishable from one that never opened — which is what both reports
                // described.
                Rect where = picker.worldBound;
                Rect screen = panel.worldBound;
                Assert.That(where.width, Is.GreaterThan(1f), "the picker has no width");
                Assert.That(where.height, Is.GreaterThan(1f), "the picker has no height");
                Assert.That(where.yMin, Is.GreaterThanOrEqualTo(screen.yMin - 0.5f),
                    "the picker hangs off the top of the screen");
                Assert.That(where.yMax, Is.LessThanOrEqualTo(screen.yMax + 0.5f),
                    "the picker hangs off the bottom of the screen");

                Rect row = owner!.worldBound;
                string placed =
                    $"picker {where} row {row} screen {screen} bottom {picker.style.bottom.value.value}";

                // Against the row it was raised by, on whichever side it fitted. Both edges are
                // allowed because PopoverBottomFor flips the popover under the row when there is
                // not the height for it above.
                float above = System.Math.Abs(where.yMax - row.yMin);
                float below = System.Math.Abs(where.yMin - row.yMax);
                Assert.That(System.Math.Min(above, below), Is.LessThan(row.height + 4f),
                    $"the picker does not sit against the row that raised it - {placed}");

                // ---- and the pick itself ------------------------------------------------------
                PawnId pawn = boot.World.Views.Current.Pawns[0].Id;
                string name = ColonistNames.Of(boot.World.Views.Current, pawn);

                VisualElement? choice = null;
                foreach (VisualElement candidate in picker.Query(className: "bedowner__row").ToList())
                {
                    Label? label = candidate.Q<Label>(className: "bedowner__name");
                    if (label != null && label.text == name) choice = candidate;
                }

                Assert.That(choice, Is.Not.Null, $"the picker does not list {name}");
                Click(choice!);

                // The intent lands on the next drained tick, or at once while paused.
                for (int i = 0; i < 240; i++)
                {
                    if (boot.Colony!.Construction.BedOwnerAt(head) == pawn.Value) break;
                    yield return null;
                }

                Assert.That(boot.Colony!.Construction.BedOwnerAt(head), Is.EqualTo(pawn.Value),
                    "picking a colonist in the popover did not give them the bed");

                // And the pane says so, which is the half the player reads.
                for (int i = 0; i < 120; i++)
                {
                    VisualElement? again = RowNamed(panel, "owner");
                    Label? value = again?.Q<Label>(className: "inspect__rowvalue");
                    if (value != null && value.text == name) yield break;
                    yield return null;
                }

                Assert.Fail("the bed was assigned but the owner row never said so");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
