#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The Almanac's numbers, held to the Defs they were copied from. The HUD cannot read the Defs
    /// (it references the contracts only), so the catalogue writes its numbers out; this reads the
    /// XML from disk and fails the day a Def changes and the page does not — the shape of
    /// <c>RegistryTests</c> reading the C# sources.
    ///
    /// <para>Each row names an entry, a property on it, and the Def value that property quotes. The
    /// property must contain the value as a player reads it (thousands separated). Retune a Def,
    /// and this names the page to correct.</para>
    /// </summary>
    public class AlmanacFactsTests
    {
        static readonly (string Entry, string Property, string File, string Def, string Element, Func<int, string> Shown)[] Facts =
        {
            // ground
            ("ui.terrain.rock", "Mining work", "World/Terrain.xml", "Rock", "workToClear", N),
            ("ui.terrain.subsoil", "Dig", "World/Terrain.xml", "Subsoil", "workToClear", N),
            ("ui.terrain.grass", "Dig", "World/Terrain.xml", "Grass", "workToClear", N),
            ("ui.terrain.marsh", "Dig", "World/Terrain.xml", "Marsh", "workToClear", N),
            ("ui.terrain.sand", "Dig", "World/Terrain.xml", "Sand", "workToClear", N),
            ("ui.terrain.packedgravel", "Dig", "World/Terrain.xml", "PackedGravel", "workToClear", N),
            ("ui.res.rubble", "Clear", "World/Terrain.xml", "Rubble", "workToClear", N),
            ("ui.res.ironore", "Mining work", "World/Terrain.xml", "IronOre", "workToClear", N),
            ("ui.res.coal", "Mining work", "World/Terrain.xml", "CoalSeam", "workToClear", N),
            ("ui.terrain.grass", "Fertility", "World/Terrain.xml", "Grass", "fertility", N),
            ("ui.terrain.marsh", "Fertility", "World/Terrain.xml", "Marsh", "fertility", N),

            // flora
            ("ui.terrain.tree.birch", "Fell", "World/WildPlants.xml", "WildPlant_Birch", "clearWorkTicks", N),
            ("ui.terrain.tree.birch", "Yields", "World/WildPlants.xml", "WildPlant_Birch", "clearYieldCount", N),
            ("ui.terrain.tree.meadow", "Fell", "World/WildPlants.xml", "WildPlant_MeadowTree", "clearWorkTicks", N),
            ("ui.terrain.tree.meadow", "Yields", "World/WildPlants.xml", "WildPlant_MeadowTree", "clearYieldCount", N),
            ("ui.terrain.tree.fruit", "Fell", "World/WildPlants.xml", "WildPlant_FruitTree", "clearWorkTicks", N),
            ("ui.terrain.tree.fruit", "Yields", "World/WildPlants.xml", "WildPlant_FruitTree", "clearYieldCount", N),
            ("ui.terrain.tree.giant", "Fell", "World/WildPlants.xml", "WildPlant_GiantTree", "clearWorkTicks", N),
            ("ui.terrain.tree.giant", "Yields", "World/WildPlants.xml", "WildPlant_GiantTree", "clearYieldCount", N),
            ("ui.terrain.bush", "Clear", "World/WildPlants.xml", "WildPlant_Bush", "clearWorkTicks", N),
            ("ui.terrain.bush.berry", "Pick", "World/WildPlants.xml", "WildPlant_BerryBush", "fruitWorkTicks", N),
            ("ui.terrain.bush.berry", "Yields", "World/WildPlants.xml", "WildPlant_BerryBush", "fruitCount", N),
            ("ui.terrain.bush.berry", "Grows back", "World/WildPlants.xml", "WildPlant_BerryBush", "fruitRegrowTicks", t => $"{t / 60000} days"),
            ("ui.terrain.carrot", "Grows", "World/Plants.xml", "Plant_Carrot", "growTicks", N),
            ("ui.terrain.carrot", "Sow", "World/Plants.xml", "Plant_Carrot", "sowWorkTicks", N),
            ("ui.terrain.carrot", "Harvest work", "World/Plants.xml", "Plant_Carrot", "harvestWorkTicks", N),
            ("ui.terrain.carrot", "Yields", "World/Plants.xml", "Plant_Carrot", "yieldCount", N),
            ("ui.terrain.carrot", "Needs", "World/Plants.xml", "Plant_Carrot", "minFertility", N),

            // things
            ("ui.res.wood", "Stack", "Pawns/Items.xml", "Item_Wood", "stackLimit", N),
            ("ui.res.stone", "Stack", "Pawns/Items.xml", "Item_Stone", "stackLimit", N),
            ("ui.res.scrap", "Stack", "Pawns/Items.xml", "Item_Salvage", "stackLimit", N),
            ("ui.res.ironore", "Stack", "Pawns/Items.xml", "Item_IronOre", "stackLimit", N),
            ("ui.res.coal", "Stack", "Pawns/Items.xml", "Item_Coal", "stackLimit", N),
            ("ui.res.medkit", "Stack", "Pawns/Items.xml", "Item_MedicalSupplies", "stackLimit", N),
            ("ui.res.rations", "Nutrition", "Pawns/Items.xml", "Item_Meal", "nutrition", N),
            ("ui.res.rations", "Stack", "Pawns/Items.xml", "Item_Meal", "stackLimit", N),
            ("ui.res.meal", "Nutrition", "Pawns/Items.xml", "Item_CookedMeal", "nutrition", N),
            ("ui.res.meal.veg", "Nutrition", "Pawns/Items.xml", "Item_VegetableMeal", "nutrition", N),
            ("ui.res.meal.burnt", "Nutrition", "Pawns/Items.xml", "Item_BurntMeal", "nutrition", N),
            ("ui.res.meal.burnt", "Stack", "Pawns/Items.xml", "Item_BurntMeal", "stackLimit", N),
            ("ui.res.carrots", "Nutrition", "Pawns/Items.xml", "Item_Carrots", "nutrition", N),
            ("ui.res.berries", "Nutrition", "Pawns/Items.xml", "Item_Berries", "nutrition", N),
            ("ui.res.mushrooms", "Nutrition", "Pawns/Items.xml", "Item_Mushrooms", "nutrition", N),
            ("ui.item.bat", "Damage", "Pawns/Items.xml", "Item_Bat", "weapon/damage", N),
            ("ui.item.bat", "Between swings", "Pawns/Items.xml", "Item_Bat", "weapon/cooldownTicks", N),
            ("ui.item.crowbar", "Damage", "Pawns/Items.xml", "Item_Crowbar", "weapon/damage", N),
            ("ui.item.crowbar", "Between swings", "Pawns/Items.xml", "Item_Crowbar", "weapon/cooldownTicks", N),
            ("ui.item.machete", "Damage", "Pawns/Items.xml", "Item_Machete", "weapon/damage", N),
            ("ui.item.machete", "Between swings", "Pawns/Items.xml", "Item_Machete", "weapon/cooldownTicks", N),
            ("ui.item.arcblade", "Damage", "Pawns/Items.xml", "Item_ArcBlade", "weapon/damage", N),
            ("ui.item.arcblade", "Between swings", "Pawns/Items.xml", "Item_ArcBlade", "weapon/cooldownTicks", N),
            ("ui.item.pistol", "Damage", "Pawns/Items.xml", "Item_Pistol", "weapon/damage", N),
            ("ui.item.pistol", "Between shots", "Pawns/Items.xml", "Item_Pistol", "weapon/cooldownTicks", N),
            ("ui.item.pistol", "Aim", "Pawns/Items.xml", "Item_Pistol", "weapon/windupTicks", N),
            ("ui.item.pistol", "Range", "Pawns/Items.xml", "Item_Pistol", "weapon/ranged/rangeMm", mm => $"{mm / 1000} m"),

            // buildings
            ("ui.arch.tool.wall", "Cost", "World/Buildings.xml", "Building_Wall", "costCount", N),
            ("ui.arch.tool.wall", "Work", "World/Buildings.xml", "Building_Wall", "workToBuild", N),
            ("ui.arch.tool.wall", "Hit points", "World/Buildings.xml", "Building_Wall", "maxHitPoints", N),
            ("ui.arch.tool.door", "Cost", "World/Buildings.xml", "Building_Door", "costCount", N),
            ("ui.arch.tool.door", "Work", "World/Buildings.xml", "Building_Door", "workToBuild", N),
            ("ui.arch.tool.door", "Hit points", "World/Buildings.xml", "Building_Door", "maxHitPoints", N),
            ("ui.arch.tool.roof", "Cost", "World/Buildings.xml", "Building_Floor", "costCount", N),
            ("ui.arch.tool.roof", "Work", "World/Buildings.xml", "Building_Floor", "workToBuild", N),
            ("ui.arch.tool.deckplate", "Cost", "World/Buildings.xml", "Building_DeckPlate", "costCount", N),
            ("ui.arch.tool.deckplate", "Work", "World/Buildings.xml", "Building_DeckPlate", "workToBuild", N),
            ("ui.arch.tool.ladder", "Cost", "World/Buildings.xml", "Building_Ladder", "costCount", N),
            ("ui.arch.tool.ladder", "Work", "World/Buildings.xml", "Building_Ladder", "workToBuild", N),
            ("ui.arch.tool.ladder", "Hit points", "World/Buildings.xml", "Building_Ladder", "maxHitPoints", N),
            ("ui.arch.tool.bed", "Cost", "World/Buildings.xml", "Building_Bed", "costCount", N),
            ("ui.arch.tool.bed", "Work", "World/Buildings.xml", "Building_Bed", "workToBuild", N),
            ("ui.arch.tool.bed", "Hit points", "World/Buildings.xml", "Building_Bed", "maxHitPoints", N),
            ("ui.arch.tool.shelf", "Cost", "World/Buildings.xml", "Building_Shelf", "costCount", N),
            ("ui.arch.tool.shelf", "Work", "World/Buildings.xml", "Building_Shelf", "workToBuild", N),
            ("ui.arch.tool.shelf", "Holds", "World/Buildings.xml", "Building_Shelf", "storageSlots", N),
            ("ui.arch.tool.campfire", "Cost", "World/Buildings.xml", "Building_Campfire", "costCount", N),
            ("ui.arch.tool.campfire", "Work", "World/Buildings.xml", "Building_Campfire", "workToBuild", N),
            ("ui.arch.tool.conduit", "Work", "World/Buildings.xml", "Building_Conduit", "workToBuild", N),
            ("ui.arch.tool.generator", "Cost", "World/Buildings.xml", "Building_Generator", "costCount", N),
            ("ui.arch.tool.generator", "Work", "World/Buildings.xml", "Building_Generator", "workToBuild", N),
            ("ui.arch.tool.generator", "Output", "World/Buildings.xml", "Building_Generator", "powerOutputW", N),
            ("ui.arch.tool.generator", "Hopper", "World/Buildings.xml", "Building_Generator", "fuelCapacity", N),
            ("ui.arch.tool.generator", "Burns", "World/Buildings.xml", "Building_Generator", "fuelPerDay", N),
            ("ui.arch.tool.heater", "Cost", "World/Buildings.xml", "Building_Heater", "costCount", N),
            ("ui.arch.tool.heater", "Draws", "World/Buildings.xml", "Building_Heater", "powerDrawW", N),
            ("ui.arch.tool.galley", "Cost", "World/Buildings.xml", "Building_Galley", "costCount", N),
            ("ui.arch.tool.galley", "Work", "World/Buildings.xml", "Building_Galley", "workToBuild", N),
            ("ui.arch.tool.galley", "Draws", "World/Buildings.xml", "Building_Galley", "powerDrawW", N),

            // animals
            ("ui.pawn.hog", "Pace", "Pawns/Species.xml", "Species_MiddenHog", "movePerMille", Percent),
            ("ui.pawn.rat", "Pace", "Pawns/Species.xml", "Species_DuctRat", "movePerMille", Percent),
            ("ui.pawn.hog", "Leg", "Pawns/Species.xml", "Species_MiddenHog", "wanderRadius", N),
            ("ui.pawn.rat", "Leg", "Pawns/Species.xml", "Species_DuctRat", "wanderRadius", N),
            ("ui.pawn.frog", "Leg", "Pawns/Species.xml", "Species_CulvertFrog", "wanderRadius", N),
            ("ui.pawn.frog", "Lives", "Pawns/Species.xml", "Species_CulvertFrog", "bankRadius", N),
        };

        static string N(int value) => value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        static string Percent(int perMille) => $"{perMille / 10}%";

        [Test]
        public void EveryNumberTheAlmanacQuotesIsTheDefs()
        {
            string? defs = Find("Assets/Odyssey/Defs/Core");
            Assert.That(defs, Is.Not.Null, "the Defs were not found");

            var wrong = new System.Collections.Generic.List<string>();
            foreach (var fact in Facts)
            {
                AlmanacEntry? entry = AlmanacCatalogue.ForKey(fact.Entry);
                Assert.That(entry, Is.Not.Null, fact.Entry);
                string? shown = entry!.Properties.Where(p => p.Key == fact.Property).Select(p => p.Value).FirstOrDefault();
                Assert.That(shown, Is.Not.Null, $"{fact.Entry} has no property {fact.Property}");

                int value = Value(Path.Combine(defs!, fact.File), fact.Def, fact.Element);
                string expected = fact.Shown(value);
                if (!shown!.Contains(expected))
                    wrong.Add($"{entry.Name} · {fact.Property} says \"{shown}\"; {fact.Def}.{fact.Element} is {value} (\"{expected}\")");
            }

            Assert.That(wrong, Is.Empty, "the Almanac disagrees with the Defs:\n  " + string.Join("\n  ", wrong));
        }

        static int Value(string file, string defName, string element)
        {
            XDocument doc = XDocument.Load(file);
            XElement? def = doc.Descendants().FirstOrDefault(e => (string?)e.Element("defName") == defName);
            Assert.That(def, Is.Not.Null, $"{defName} is not in {Path.GetFileName(file)}");
            XElement? node = def;
            foreach (string step in element.Split('/')) node = node?.Element(step);
            Assert.That(node, Is.Not.Null, $"{defName} has no {element}");
            return int.Parse(node!.Value.Trim(), System.Globalization.CultureInfo.InvariantCulture);
        }

        static string? Find(string relative)
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (int i = 0; i < 12 && directory != null; i++, directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (Directory.Exists(candidate)) return candidate;
            }
            return null;
        }
    }
}
