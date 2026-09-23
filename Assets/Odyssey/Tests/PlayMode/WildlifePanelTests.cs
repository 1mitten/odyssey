#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Ui;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// The Wildlife panel under the real bootstrap (design 30 §6): F6's director opens it, the
    /// rows are the animals the frame carries, the elements are bounded by the page and not by
    /// the board, and a refresh with the panel open allocates nothing the collector has to run
    /// for. The pattern is <c>WorkTabCostTests</c>, minus the timing, which that test already
    /// guards for the same code paths.
    /// </summary>
    public class WildlifePanelTests
    {
        [UnityTest]
        public IEnumerator ThePanelListsTheAnimalsAndBuildsOnlyAPage()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell _, buildOnPlay: true);
            try
            {
                yield return new WaitForSecondsRealtime(0.3f);
                for (int i = 0; i < 10; i++) yield return null;

                Assert.That(boot.World, Is.Not.Null, "the bootstrap never built a world");
                Assert.That(boot.Colony, Is.Not.Null, "the bootstrap never built a colony");
                HudDirectors? directors = boot.Directors;
                Assert.That(directors, Is.Not.Null, "no directors");
                var doc = root.GetComponentInChildren<UIDocument>();
                Assert.That(doc?.rootVisualElement, Is.Not.Null, "the HUD has no panel");

                // The rig world seeds its own animals (design 30); three more by the debug path
                // make the count strip say more than one of a kind whatever the seed gave.
                var colony = boot.Colony!;
                var world = boot.World!;
                var at = new CellRef(colony.Start.X, colony.Start.Z, colony.Start.Y);
                world.Intents.Submit(new Intent(IntentKind.SpawnPawn, at, PawnKindIndex.MiddenHog));
                world.Intents.Submit(new Intent(IntentKind.SpawnPawn, at, PawnKindIndex.MiddenHog));
                world.Intents.Submit(new Intent(IntentKind.SpawnPawn, at, PawnKindIndex.DuctRat));
                world.Tick();
                Assert.That(world.Intents.Rejected, Is.Empty);
                int hogs = 0, rats = 0;
                foreach (Pawn pawn in colony.Pawns.Pawns.All)
                {
                    if (pawn.Kind == PawnKindIndex.MiddenHog) hogs++;
                    else if (pawn.Kind == PawnKindIndex.DuctRat) rats++;
                }
                int animals = hogs + rats;
                Assert.That(animals, Is.GreaterThanOrEqualTo(3));

                directors!.Wildlife.SetOpen(true);
                // Two mid buckets, so the refresh has run with the animals in the frame.
                yield return new WaitForSecondsRealtime(1.2f);

                VisualElement? panel = doc!.rootVisualElement.Q("wildlife");
                Assert.That(panel, Is.Not.Null, "the Wildlife panel is not in the tree");
                Assert.That(panel!.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "the panel did not open");

                int visibleRows = 0;
                var labels = new System.Collections.Generic.List<string>();
                panel.Query<Label>().ForEach(label => { if (label.text.Length > 0) labels.Add(label.text); });
                panel.Query(className: "wildlife__row").ForEach(row =>
                {
                    if (row.resolvedStyle.display == DisplayStyle.Flex) visibleRows++;
                });
                Assert.That(visibleRows, Is.EqualTo(Mathf.Min(animals, WildlifeLayout.RowsPerPage)),
                    "one visible row per animal, a page at most: " + string.Join(" | ", labels));
                Assert.That(labels, Has.Member(Registry.Label("ui.pawn.hog") + " " + hogs), "the count strip counts the hogs");
                Assert.That(labels, Has.Member(Registry.Label("ui.pawn.rat") + " " + rats), "and the rats");

                int elements = Count(panel);
                Assert.That(elements, Is.LessThan(200),
                    $"the open panel built {elements} elements; a page is twelve rows of four cells whatever the board carries");

                // Steady and open: no collections over a hundred frames.
                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
                System.GC.Collect();
                yield return null;
                int before = System.GC.CollectionCount(0);
                for (int i = 0; i < 100; i++) yield return null;
                Assert.That(System.GC.CollectionCount(0) - before, Is.Zero,
                    "the collector ran with the panel open and steady: something allocates every refresh");

                // Escape's rung and the roster path are the fast tier's; the close is checked here.
                directors.Wildlife.SetOpen(false);
                yield return null;
                Assert.That(panel.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static int Count(VisualElement element)
        {
            int total = 1;
            for (int i = 0; i < element.childCount; i++) total += Count(element[i]);
            return total;
        }
    }
}
