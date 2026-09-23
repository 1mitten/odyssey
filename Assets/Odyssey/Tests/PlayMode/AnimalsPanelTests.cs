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
    /// The Animals tab under the real bootstrap (design 30 §6, the brief of 2026-09-23): F5's
    /// director opens it, the window is exactly the brief's width, the rows are the animals the
    /// frame carries with a page at most, exactly one heading carries the sort mark, the pager is
    /// absent at twelve or fewer, a selection puts the tab away, and a refresh with the panel
    /// open allocates nothing the collector has to run for.
    /// </summary>
    public class AnimalsPanelTests
    {
        [UnityTest]
        public IEnumerator TheTabListsTheAnimalsAtTheBriefsWidthAndYieldsToTheInspectPane()
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
                Pawn? aHog = null;
                foreach (Pawn pawn in colony.Pawns.Pawns.All)
                {
                    if (pawn.Kind == PawnKindIndex.MiddenHog) { hogs++; aHog ??= pawn; }
                    else if (pawn.Kind == PawnKindIndex.DuctRat) rats++;
                }
                int animals = hogs + rats;
                Assert.That(animals, Is.InRange(3, AnimalsLayout.RowsPerPage), "a page's worth, so no pager");

                directors!.Animals.SetOpen(true);
                // Two mid buckets, so the refresh has run with the animals in the frame.
                yield return new WaitForSecondsRealtime(1.2f);

                VisualElement? panel = doc!.rootVisualElement.Q("animals");
                Assert.That(panel, Is.Not.Null, "the Animals tab is not in the tree");
                Assert.That(panel!.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "the tab did not open");
                Assert.That(panel.resolvedStyle.width, Is.EqualTo(AnimalsLayout.TabWidth).Within(1f),
                    "the window is the brief's 560 whatever the board carries");

                int visibleRows = 0;
                panel.Query(className: "animals__row").ForEach(row =>
                {
                    if (row.resolvedStyle.display == DisplayStyle.Flex) visibleRows++;
                });
                Assert.That(visibleRows, Is.EqualTo(animals), "one visible row per animal");

                var labels = new System.Collections.Generic.List<string>();
                panel.Query<Label>().ForEach(label => { if (label.text.Length > 0) labels.Add(label.text); });
                Assert.That(labels, Has.Member(Registry.Label("ui.pawn.hog")), "the count strip names the hog");
                Assert.That(labels, Has.Member(hogs.ToString()), "and counts it");
                Assert.That(labels, Has.Member(Registry.Label("ui.pawn.rat")));
                Assert.That(labels, Has.Member(rats.ToString()));
                foreach (string text in labels)
                    foreach (char c in text)
                        Assert.That(c, Is.LessThan((char)128), $"a non-ASCII character in \"{text}\": the shipped fonts draw nothing else");

                int marks = 0;
                panel.Query(className: "animals__sortmark").ForEach(mark =>
                {
                    if (mark.resolvedStyle.display == DisplayStyle.Flex) marks++;
                });
                Assert.That(marks, Is.EqualTo(1), "exactly one heading carries the sort mark");

                VisualElement? pager = panel.Q(className: "animals__pager");
                Assert.That(pager, Is.Not.Null);
                Assert.That(pager!.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "no pager at a page or fewer");

                int elements = Count(panel);
                Assert.That(elements, Is.LessThan(200),
                    $"the open tab built {elements} elements; a page is twelve rows whatever the board carries");

                // Steady and open: no collections over a hundred frames.
                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
                System.GC.Collect();
                yield return null;
                int before = System.GC.CollectionCount(0);
                for (int i = 0; i < 100; i++) yield return null;
                Assert.That(System.GC.CollectionCount(0) - before, Is.Zero,
                    "the collector ran with the tab open and steady: something allocates every refresh");

                // A selection — the row click's outcome — puts the tab away for the inspect pane.
                Assert.That(aHog, Is.Not.Null);
                directors.ChooseColonist(aHog!.Id, world.Views.Current);
                yield return null;
                Assert.That(directors.Animals.Open, Is.False, "the tab and the pane never show together");
                Assert.That(panel.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));

                // And opening the tab again clears that selection.
                directors.Animals.SetOpen(true);
                yield return null;
                Assert.That(directors.Selection.IsEmpty, Is.True, "opening the tab put the selection away");
                Assert.That(directors.Animals.Open, Is.True);
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
