#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    public class AlmanacDirectorTests
    {
        [Test]
        public void AlmanacCatalogueContainsAllTwelveRequiredCategories()
        {
            var expectedCategories = new[]
            {
                "Terrain", "Materials", "Structures", "Items", "Skills",
                "Work types", "Needs", "Traits", "Health", "Fauna", "Flora", "Events"
            };

            Assert.That(AlmanacCatalogue.Categories.Count, Is.EqualTo(12));
            for (int i = 0; i < expectedCategories.Length; i++)
            {
                Assert.That(AlmanacCatalogue.Categories[i].Name, Is.EqualTo(expectedCategories[i]));
                Assert.That(AlmanacCatalogue.Categories[i].Entries, Is.Not.Empty);
            }
        }

        [Test]
        public void EveryAlmanacEntryCompliesWithUnifiedTemplateStructure()
        {
            foreach (AlmanacCategory category in AlmanacCatalogue.Categories)
            {
                Assert.That(category.IconPath, Is.Not.Empty);
                foreach (AlmanacEntry entry in category.Entries)
                {
                    Assert.That(entry.Name, Is.Not.Empty);
                    Assert.That(entry.Summary, Is.Not.Empty);
                    Assert.That(entry.TypeChip, Is.Not.Empty);
                    Assert.That(entry.NatureChip, Is.Not.Empty);
                    Assert.That(entry.Definition, Is.Not.Empty);
                    Assert.That(entry.LiveState, Is.Not.Empty);
                    Assert.That(entry.PrimaryAction, Is.Not.Empty);
                    Assert.That(entry.Properties.Count, Is.GreaterThanOrEqualTo(4));
                    Assert.That(entry.Icon, Is.Not.Null);
                    Assert.That(entry.Icon.Path, Is.Not.Empty);
                    Assert.That(entry.Body, Is.Not.Null);
                    Assert.That(entry.Body.Paragraph, Is.Not.Empty);
                    Assert.That(entry.Related.Count, Is.GreaterThanOrEqualTo(2));
                }
            }
        }

        [Test]
        public void AlmanacNavigationAndHistoryStackWorkBidirectionally()
        {
            var director = new AlmanacDirector();
            Assert.That(director.Open, Is.False);
            Assert.That(director.CanGoBack, Is.False);
            Assert.That(director.CanGoForward, Is.False);

            director.Toggle();
            Assert.That(director.Open, Is.True);

            director.SelectCategory("Materials");
            Assert.That(director.CurrentCategory, Is.EqualTo("Materials"));
            Assert.That(director.CurrentEntry, Is.EqualTo("Wood"));
            Assert.That(director.CanGoBack, Is.True);

            director.SelectEntry("Materials", "Stone");
            Assert.That(director.CurrentEntry, Is.EqualTo("Stone"));

            director.GoBack();
            Assert.That(director.CurrentEntry, Is.EqualTo("Wood"));
            Assert.That(director.CanGoForward, Is.True);

            director.GoForward();
            Assert.That(director.CurrentEntry, Is.EqualTo("Stone"));
        }

        [Test]
        public void SelectionResolverMapsItemsToAlmanac()
        {
            var inspect = new InspectModel();

            // Wood item
            inspect.Subject = InspectSubject.Item;
            inspect.Title = "Wood × 27";
            inspect.ItemIconKey = "ui.res.wood";
            var resolved = AlmanacDirector.ResolveSelection(inspect);
            Assert.That(resolved, Is.Not.Null);
            Assert.That(resolved!.Value.Category, Is.EqualTo("Materials"));
            Assert.That(resolved.Value.Entry, Is.EqualTo("Wood"));

            // Ration Pack item
            inspect.Title = "Ration pack";
            inspect.ItemIconKey = "ui.res.meal";
            resolved = AlmanacDirector.ResolveSelection(inspect);
            Assert.That(resolved, Is.Not.Null);
            Assert.That(resolved!.Value.Category, Is.EqualTo("Items"));
            Assert.That(resolved.Value.Entry, Is.EqualTo("Ration Pack"));

            // Carrots item
            inspect.Title = "Carrots × 5";
            inspect.ItemIconKey = "ui.res.carrots";
            resolved = AlmanacDirector.ResolveSelection(inspect);
            Assert.That(resolved, Is.Not.Null);
            Assert.That(resolved!.Value.Category, Is.EqualTo("Items"));
            Assert.That(resolved.Value.Entry, Is.EqualTo("Carrots"));
        }

        [Test]
        public void SelectionResolverMapsCellsAndTerrainToAlmanac()
        {
            var inspect = new InspectModel();
            inspect.Subject = InspectSubject.Cell;

            // Grass terrain
            inspect.Title = "Grass";
            var resolved = AlmanacDirector.ResolveSelection(inspect);
            Assert.That(resolved, Is.Not.Null);
            Assert.That(resolved!.Value.Category, Is.EqualTo("Terrain"));
            Assert.That(resolved.Value.Entry, Is.EqualTo("Grass"));

            // Wall structure
            inspect.Title = "Wall";
            resolved = AlmanacDirector.ResolveSelection(inspect);
            Assert.That(resolved, Is.Not.Null);
            Assert.That(resolved!.Value.Category, Is.EqualTo("Structures"));
            Assert.That(resolved.Value.Entry, Is.EqualTo("Wall"));

            // Pine tree flora
            inspect.Title = "Conifer";
            resolved = AlmanacDirector.ResolveSelection(inspect);
            Assert.That(resolved, Is.Not.Null);
            Assert.That(resolved!.Value.Category, Is.EqualTo("Flora"));
            Assert.That(resolved.Value.Entry, Is.EqualTo("Pine"));
        }

        [Test]
        public void SelectionResolverMapsColonistsToAlmanac()
        {
            var inspect = new InspectModel();
            inspect.Subject = InspectSubject.Colonist;
            inspect.Tabs.Add(new InspectTab { Name = "Needs", Enabled = true });
            inspect.ShowTab(0); // Needs

            var resolved = AlmanacDirector.ResolveSelection(inspect);
            Assert.That(resolved, Is.Not.Null);
            Assert.That(resolved!.Value.Category, Is.EqualTo("Needs"));
        }

        [Test]
        public void AlmanacDirectorPanelKeyAgreesWithHudCommands()
        {
            Assert.That(AlmanacDirector.PanelKey, Is.EqualTo(HudCommands.AlmanacKey));
            HudCommand cmd = HudCommands.All.First(c => c.Key == HudCommands.AlmanacKey);
            Assert.That(cmd.Hotkey, Is.EqualTo("F9"));
            Assert.That(cmd.Live, Is.True);
        }
    }
}
