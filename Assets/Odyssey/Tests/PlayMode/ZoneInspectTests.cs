#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Growing;
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
