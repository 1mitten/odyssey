#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Growing;
using Odyssey.Sim.Storage;
using Odyssey.Presentation.Ui;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// <b>Does clicking a sown field tile show the growing row?</b> The owner reported it does
    /// not (2026-09-18), while both halves of the chain pass their own tests — the sim publishes
    /// the zone in a CellDetail, and the model renders a zone's CellDetail. This is the join
    /// between them, driven the way the owner's click drives it: a QueryCell for the GROUND cell
    /// a surface click lands on, a republish, and a selection, then the pane's rows.
    /// </summary>
    public class ZoneInspectTests
    {
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
        /// <b>Unticking the last category must not move what you unticked it with.</b>
        ///
        /// <para>The owner cleared every category and reported that the warning which appeared
        /// "moved the controls/components" (2026-09-21). It did: the two notes were pushed in as
        /// the first children of the scrolling list, so every category row dropped by the height
        /// of the band — under a cursor that was in the middle of working down them. The band
        /// lives below the whole control now.</para>
        ///
        /// <para><b>Only this tier can see it.</b> The fast tier has no visual tree and the model
        /// does not know where anything is drawn, so a layout that shifts under the pointer is
        /// invisible to every other check — which is why it reached a playtest.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator ClearingEveryCategoryDoesNotMoveTheRowsThatDidIt()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out _, out _);
            try
            {
                yield return RigWorld.WarmUp();
                Assert.That(boot.Colony, Is.Not.Null);
                var colony = boot.Colony!;
                var size = colony.Grid.Size;

                // A one-cell store on the ground the colony starts on, painted through the intent
                // the tool sends, and then selected the way a click selects it.
                CellRef floor = colony.Start;
                var ground = new CellRef(floor.X, floor.Z, floor.Y - 1);
                boot.World!.Intents.Submit(new Intent(IntentKind.DesignateStorage, floor,
                    size.Index(floor.X, floor.Z, floor.Y), StoragePreset.Everything));
                boot.World.Tick();
                Assert.That(colony.Pawns.Storage!.IsStorage(size.Index(floor.X, floor.Z, floor.Y)),
                    Is.True, "the store was refused, so this test would prove nothing");

                boot.World.Intents.Submit(new Intent(IntentKind.QueryCell, ground));
                boot.World.RepublishViews();
                boot.Directors!.Selection.Pick(ground, default, boot.World.Views.Current);

                var doc = root.GetComponentInChildren<UIDocument>();
                VisualElement panel = doc!.rootVisualElement;

                VisualElement? pane = null;
                for (int i = 0; i < 240 && pane == null; i++)
                {
                    pane = panel.Q(className: HudShell.StoragePaneClass);
                    if (pane == null) yield return null;
                }
                Assert.That(pane, Is.Not.Null, "the pane never opened on the store");

                // Let the layout settle before anything is measured: a worldBound read on the
                // frame an element is built is nought, and two noughts are equal.
                for (int i = 0; i < 4; i++) yield return null;

                VisualElement? list = pane!.Q(className: HudShell.StorageListClass);
                VisualElement? warning = pane.Q(className: HudShell.StorageWarningClass);
                Assert.That(list, Is.Not.Null);
                Assert.That(warning, Is.Not.Null);
                Assert.That(warning!.resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                    "a store that takes everything is not a store that takes nothing");

                var rows = pane.Query(className: HudShell.StorageCategoryClass).ToList();
                Assert.That(rows, Is.Not.Empty, "no category rows to be moved");
                float listBefore = list!.worldBound.y;
                float paneBefore = pane.worldBound.y;
                float firstRowBefore = rows[0].worldBound.y;
                Assert.That(firstRowBefore, Is.GreaterThan(0f), "the layout had not settled");

                // Pressed, not submitted. The pane rebuilds its rows when *it* sends a command
                // and at no other time, so an intent posted behind its back would leave it
                // showing the old answer and prove nothing about the layout. Clear all is the
                // same command the last untick lands on.
                VisualElement? clearAll = pane.Q(className: HudShell.StorageClearAllClass);
                Assert.That(clearAll, Is.Not.Null, "no Clear all to press");
                using (var click = ClickEvent.GetPooled())
                {
                    click.target = clearAll;
                    clearAll!.SendEvent(click);
                }
                boot.World.Tick();

                for (int i = 0; i < 240; i++)
                {
                    yield return null;
                    warning = pane.Q(className: HudShell.StorageWarningClass);
                    if (warning != null && warning.resolvedStyle.display == DisplayStyle.Flex) break;
                }

                Assert.That(warning!.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex),
                    "a store that accepts nothing said nothing about it");

                for (int i = 0; i < 4; i++) yield return null;

                var rowsAfter = pane.Query(className: HudShell.StorageCategoryClass).ToList();
                Assert.That(rowsAfter, Has.Count.EqualTo(rows.Count), "the list lost or gained rows");
                Assert.That(list.worldBound.y, Is.EqualTo(listBefore).Within(0.5f),
                    "the list moved when the warning appeared");
                Assert.That(rowsAfter[0].worldBound.y, Is.EqualTo(firstRowBefore).Within(0.5f),
                    "the row the player was clicking moved out from under them");
                Assert.That(warning.worldBound.y, Is.GreaterThanOrEqualTo(list.worldBound.yMax - 0.5f),
                    "the warning is not below the control");

                // The pane is anchored to the bottom of the screen and grows upward, so a band
                // that made it taller would shove every control up — which is the same fault in
                // the other direction, and is what the first attempt at this actually did, by
                // 90 px. The band's height comes out of the list instead.
                Assert.That(pane.worldBound.y, Is.EqualTo(paneBefore).Within(0.5f),
                    "the pane changed height, so everything above it moved");

                // And the constant really does hold the sentences, so it is a reservation and
                // not a clip. Read from the children, because the band itself is the constant.
                float content = 0f;
                foreach (VisualElement child in warning.Children()) content += child.worldBound.height;
                Assert.That(content, Is.LessThanOrEqualTo(warning.contentRect.height + 0.5f),
                    $"the warning's {content} px of text does not fit the {warning.contentRect.height} px inside its band");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator ClickingSownGroundShowsTheGrowingRowInThePane()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out _, out _);
            try
            {
                yield return RigWorld.WarmUp();
                Assert.That(boot.World, Is.Not.Null);
                Assert.That(boot.Colony, Is.Not.Null);
                var colony = boot.Colony!;
                var zones = colony.Growing!;
                var size = colony.Grid.Size;
                CellRef plot = colony.Start;

                // A painted, SOWN cell - the owner's exact words: "after someone has sowed it".
                boot.World!.Intents.Submit(
                    new Intent(IntentKind.DesignateZone, plot, PlantHandle.Carrot + 1));
                boot.World.Tick();
                Assert.That(zones.ZonePlantAt(size.Index(plot.X, plot.Z, plot.Y)),
                    Is.EqualTo(PlantHandle.Carrot), "the field never took");
                zones.Sow(size.Index(plot.X, plot.Z, plot.Y));
                boot.World.Tick();

                // The click: on the GROUND under the field, which is where a surface click lands.
                var ground = new CellRef(plot.X, plot.Z, plot.Y - 1);
                boot.World.Intents.Submit(new Intent(IntentKind.QueryCell, ground));
                boot.World.RepublishViews();
                boot.Directors!.Selection.Pick(ground, default, boot.World.Views.Current);

                var doc = root.GetComponentInChildren<UIDocument>();
                VisualElement panel = doc!.rootVisualElement;

                VisualElement? growing = null;
                for (int i = 0; i < 240 && growing == null; i++)
                {
                    growing = RowNamed(panel, "growing");
                    if (growing == null) yield return null;
                }

                Assert.That(growing, Is.Not.Null,
                    "the pane never showed a growing row for the sown field");
                Label? value = growing!.Q<Label>(className: "inspect__rowvalue");
                Assert.That(value!.text, Does.Contain("Carrot").IgnoreCase,
                    $"the growing row says '{value.text}' instead of naming the crop");
                Assert.That(value.text, Does.Contain("% grown"),
                    $"the growing row says '{value.text}' without its ripeness");
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
        }
    }
}
