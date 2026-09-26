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
        public void TheRailIsTheCategoriesInTheirOrderAndNoneIsEmpty()
        {
            var expected = new[]
            {
                "Terrain", "Flora", AlmanacCatalogue.Materials, AlmanacCatalogue.Food, AlmanacCatalogue.Medicine,
                AlmanacCatalogue.Weapons, "Structures", AlmanacCatalogue.Furniture, AlmanacCatalogue.Production,
                AlmanacCatalogue.Power, "Zones and orders", "People", "Fauna", "Skills", "Work types", "Needs",
                "Health", "Weather and seasons", "Events",
            };

            Assert.That(AlmanacCatalogue.Categories.Select(c => c.Name), Is.EqualTo(expected));
            foreach (AlmanacCategory category in AlmanacCatalogue.Categories)
                Assert.That(category.Entries, Is.Not.Empty, category.Name);
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
                    Assert.That(entry.CategoryName, Is.EqualTo(category.Name), entry.Name);
                    Assert.That(entry.Summary, Is.Not.Empty, entry.Name);
                    Assert.That(entry.TypeChip, Is.Not.Empty, entry.Name);
                    Assert.That(entry.NatureChip, Is.Not.Empty, entry.Name);
                    Assert.That(entry.Definition, Is.Not.Empty, entry.Name);
                    Assert.That(entry.Source, Is.Not.Empty, entry.Name);
                    Assert.That(entry.Properties, Is.Not.Empty, entry.Name);
                    Assert.That(IconGlyphs.For(entry.IconKey), Is.Not.Empty, entry.Name);
                    Assert.That(entry.Body.Paragraph, Is.Not.Empty, entry.Name);
                    Assert.That(entry.Related, Is.Not.Empty, entry.Name);
                    Assert.That(entry.PrimaryAction.Length == 0, Is.EqualTo(entry.Action == AlmanacAction.None), entry.Name);
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

            string wood = Registry.Label("ui.res.wood"), stone = Registry.Label("ui.res.stone");
            director.SelectCategory(AlmanacCatalogue.Materials);
            Assert.That(director.CurrentCategory, Is.EqualTo(AlmanacCatalogue.Materials));
            Assert.That(director.CurrentEntry, Is.EqualTo(wood));
            Assert.That(director.CanGoBack, Is.True);

            director.SelectEntry(AlmanacCatalogue.Materials, stone);
            Assert.That(director.CurrentEntry, Is.EqualTo(stone));

            director.GoBack();
            Assert.That(director.CurrentEntry, Is.EqualTo(wood));
            Assert.That(director.CanGoForward, Is.True);

            director.GoForward();
            Assert.That(director.CurrentEntry, Is.EqualTo(stone));
        }

        [Test]
        public void AnItemOpensTheEntryForItsOwnKey()
        {
            var inspect = new InspectModel { Subject = InspectSubject.Item, Title = "Wood × 27", ItemIconKey = "ui.res.wood" };
            Assert.That(AlmanacDirector.ResolveSelection(inspect), Is.EqualTo((AlmanacCatalogue.Materials, Registry.Label("ui.res.wood"))));

            // The ration pack is ui.res.rations; ui.res.meal is the cooked meal, which it used to open.
            inspect.ItemIconKey = "ui.res.rations";
            Assert.That(AlmanacDirector.ResolveSelection(inspect), Is.EqualTo((AlmanacCatalogue.Food, Registry.Label("ui.res.rations"))));

            inspect.ItemIconKey = "ui.item.crowbar";
            Assert.That(AlmanacDirector.ResolveSelection(inspect), Is.EqualTo((AlmanacCatalogue.Weapons, Registry.Label("ui.item.crowbar"))));
        }

        [Test]
        public void ATileOpensTheEntryForWhatStandsOnItOrItsGround()
        {
            var inspect = new InspectModel { Subject = InspectSubject.Cell };

            inspect.CellIconKey = "ui.terrain.grass";
            Assert.That(AlmanacDirector.ResolveSelection(inspect), Is.EqualTo(("Terrain", Registry.Label("ui.terrain.grass"))));

            inspect.CellIconKey = "ui.arch.tool.wall";
            Assert.That(AlmanacDirector.ResolveSelection(inspect), Is.EqualTo(("Structures", Registry.Label("ui.arch.tool.wall"))));

            // A picked berry bush is the berry bush's page, and an ore seam is the ore's.
            inspect.CellIconKey = "ui.terrain.bush.picked";
            Assert.That(AlmanacDirector.ResolveSelection(inspect), Is.EqualTo(("Flora", Registry.Label("ui.terrain.bush.berry"))));
            inspect.CellIconKey = "ui.res.coal";
            Assert.That(AlmanacDirector.ResolveSelection(inspect), Is.EqualTo((AlmanacCatalogue.Materials, Registry.Label("ui.res.coal"))));

            // A tile the pane has no name for opens nothing, rather than grass.
            inspect.CellIconKey = "ui.overlay.zones";
            Assert.That(AlmanacDirector.ResolveSelection(inspect), Is.Null);
        }

        [Test]
        public void AColonistOpensHerOwnPageOrTheOneForTheTabSheIsOn()
        {
            var inspect = new InspectModel { Subject = InspectSubject.Colonist };
            Assert.That(AlmanacDirector.ResolveSelection(inspect), Is.EqualTo(("People", Registry.Label("ui.pawn.colonist"))));

            inspect.Tabs.Add(new InspectTab { Name = "Needs", Enabled = true });
            inspect.ShowTab(0);
            Assert.That(AlmanacDirector.ResolveSelection(inspect), Is.EqualTo(("Needs", Registry.Label("ui.need.food"))));
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
