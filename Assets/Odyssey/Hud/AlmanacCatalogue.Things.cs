#nullable enable
using System.Collections.Generic;

namespace Odyssey.Hud
{
    // Things that lie on the ground or in a store, by the six storage categories they are filed
    // under (Catalogue.cs). Numbers are Items.xml's, Recipes.xml's and Buildings.xml's; spoilage is
    // declared on the Defs and read by nothing yet (PawnContent: "until the cold store (K2)"), so no
    // entry here says anything rots.
    public static partial class AlmanacCatalogue
    {
        static IReadOnlyList<AlmanacEntry> MaterialEntries() => new List<AlmanacEntry>
        {
            Keyed("ui.res.wood", Materials, "Material", "Organic",
                "Felled timber: the first thing a colony builds with, the generator's fuel, and the campfire's cooking wood.",
                "Felled from trees; 150 in the starting kit", AlmanacAction.FindOnMap,
                new[] {
                    ("Stack", "75"), ("Storage", Materials),
                    ("Build work", "×1.0 (the baseline)"), ("Hit points", "×1.0"),
                    ("From trees", "Birch 22, fruit 28, meadow 32, giant 100"),
                    ("Burned", "In the generator; its hopper holds 75"),
                },
                Specs("Everything that can be ordered is built of wood or stone, and wood is the quick one: stone takes 1.7 times the work and gives half as many hit points again. A wooden building also shrugs off blunt blows better than a sharp edge.",
                    ("Generator fuel", "Up to 22 a day", "Burned in proportion to the load; hauled in below half a hopper"),
                    ("Campfire cooking", "1 per meal", "The campfire's warmth itself burns nothing"),
                    ("Damage taken", "Sharp ×1.25, blunt ×1.0", "Against stone's ×0.5 and ×1.25")),
                new[] { ("ui.terrain.tree.meadow", "where it comes from"), ("ui.res.stone", "the other building material"), ("ui.arch.tool.generator", "burns it for power"), ("ui.arch.tool.campfire", "cooks with it") }),

            Keyed("ui.res.stone", Materials, "Material", "Mineral",
                "Broken rock from a mined face, and the loose stones the meadow lays. Slower to build with than wood, and it stands up to more.",
                "Mined from rock; loose stones; 150 in the starting kit", AlmanacAction.FindOnMap,
                new[] {
                    ("Stack", "75"), ("Storage", Materials),
                    ("Build work", "×1.7, plus 15"), ("Hit points", "×1.5"),
                    ("From rock", "8 a cell"), ("Loose stones", "3 to 7 a pile"),
                },
                Specs("Stone is the durable choice: every building ordered in stone takes longer and lasts longer. Sandbags are always stone.",
                    ("Damage taken", "Sharp ×0.5, blunt ×1.25", "A blade does little; a club does more"),
                    ("Loose stones", "Beside rock, and scattered", "No order needed: they are hauled like any stone"),
                    (L("ui.arch.tool.sandbag"), "5 stone, always", "The one building with a fixed material")),
                new[] { ("ui.terrain.rock", "where it is mined"), ("ui.res.wood", "the quicker material"), ("ui.arch.tool.sandbag", "always built of it") }),

            Keyed("ui.res.scrap", Materials, "Material", "Metal",
                "Salvaged metal. Power lines, the generator, the heater and the cooker all take it as parts beside their material.",
                "Wreckage piles on the board; the scrap drop", AlmanacAction.FindOnMap,
                new[] {
                    ("Stack", "50"), ("Storage", Materials),
                    (L("ui.arch.tool.conduit"), "1 a cell"), (L("ui.arch.tool.generator"), "20"),
                    (L("ui.arch.tool.heater"), "5"), (L("ui.arch.tool.galley"), "10"),
                },
                Specs("Scrap is the colony's metal. Wreckage lies about the board from the start, seven piles to every ten thousand columns of 10 to 25 each, and the scrap drop brings more.",
                    ("A botched build", "Keeps half its parts", "Rounded up"),
                    ("A cancelled site", "Gives back everything delivered", "Parts and material alike"),
                    (L("ui.bulletin.scrapdrop"), "15 to 30 a drop", "From the debug menu's Events tab")),
                new[] { ("ui.arch.tool.conduit", "built of it"), ("ui.arch.tool.generator", "needs 20"), ("ui.bulletin.scrapdrop", "brings more") }),

            Keyed("ui.res.ironore", Materials, "Material", "Ore",
                "Iron ore, mined from seams inside the rock. Nothing uses it yet: there is no smelting.",
                "Seams 3 to 13 layers down", AlmanacAction.FindOnMap,
                new[] {
                    ("Stack", "75"), ("Mining work", Work(900)),
                    ("Yields", "15 a cell"), ("Depth", "3 to 13 layers below the surface"),
                    ("Used for", "Nothing yet"),
                },
                Specs("A seam looks like rock until a mined cell beside it lays it open. It can be stored and hauled; nothing builds with it or smelts it.",
                    ("Hidden", "Drawn as rock until uncovered", "Mining a cell uncovers the six touching it")),
                new[] { ("ui.terrain.rock", "the stone round it"), ("ui.res.coal", "the deeper seam"), ("ui.skill.mining", "the skill that digs it") }),

            Keyed("ui.res.coal", Materials, "Material", "Ore",
                "Coal, mined from seams deeper than iron. Nothing burns it yet: the generator runs on wood.",
                "Seams 7 to 22 layers down", AlmanacAction.FindOnMap,
                new[] {
                    ("Stack", "75"), ("Mining work", Work(760)),
                    ("Yields", "15 a cell"), ("Depth", "7 to 22 layers below the surface"),
                    ("Used for", "Nothing yet"),
                },
                Specs("Coal lies deeper than iron and is rarer. It can be stored and hauled; no building takes it as fuel.",
                    ("Hidden", "Drawn as rock until uncovered", "Mining a cell uncovers the six touching it")),
                new[] { ("ui.res.ironore", "the shallower seam"), ("ui.terrain.rock", "the stone round it"), ("ui.arch.tool.generator", "burns wood, not coal") }),

            // Gold (design 65 §2–§3): the currency is a thing, stored and hauled like any other, and
            // never a row in a ledger — it is the balance.
            Keyed("ui.res.gold", Materials, "Currency", "Traded",
                "The currency. A real item that stacks, is stored under Items and can be hauled or stolen; every other thing's value is a number of it. A trade's balance is paid in it and never listed as a line.",
                "Paid by a trader; the debug menu's Give gold", AlmanacAction.FindOnMap,
                new[] {
                    ("Stack", "500"), ("Storage", Registry.Label("ui.res.category.items")),
                    ("Value", "1, by definition"), ("Spent", "From the stores, and from stacks within 4 cells of the trader"),
                },
                Specs("A trader pays six tenths of a thing's value and charges fourteen tenths, always at least one gold above what it would pay, so buying and selling back always loses.",
                    (L("ui.res.wood") + ", " + Lc("ui.res.stone") + ", " + Lc("ui.res.carrots"), "1 each", "Pays 1, charges 2"),
                    (L("ui.res.medkit"), "20", "Pays 12, charges 28"),
                    (L("ui.item.pistol"), "150", "Pays 90, charges 210")),
                new[] { ("ui.pawn.trader", "who pays in it"), ("ui.bulletin.trader", "when one comes"), ("ui.item.pistol", "the dearest thing") }),
        };

        static IReadOnlyList<AlmanacEntry> FoodEntries() => new List<AlmanacEntry>
        {
            Eaten("ui.res.meal", 900, 10, "Cooked", "+50",
                "A cooked meal with meat in it, the best food there is. There is no meat in the game yet, so a cook makes vegetable meals.",
                "Cooked, once there is meat",
                new[] { ("ui.res.meal.veg", "what the cook makes today"), ("ui.arch.tool.galley", "where it is cooked"), ("ui.need.food", "what it fills") }),
            Eaten("ui.res.meal.veg", 900, 10, "Cooked", "+50",
                "A meal cooked from any raw food. As filling and as welcome as one with meat.",
                "Cooked at an electric cooker or a campfire",
                new[] { ("ui.arch.tool.galley", "cooked here"), ("ui.skill.cooking", "the skill that cooks it"), ("ui.res.carrots", "an ingredient"), ("ui.res.meal.burnt", "what a slip makes") }),
            Eaten("ui.res.rations", 900, 20, "Sealed", "+20",
                "Sealed food from before the collapse. Eaten after a cooked meal and before anything burnt or raw.",
                "36 in the starting kit; the supply drop",
                new[] { ("ui.bulletin.supplydrop", "brings more"), ("ui.res.meal.veg", "eaten first when there is one"), ("ui.need.food", "what it fills") }),
            Eaten("ui.res.meal.burnt", 700, 10, "Burnt", "−40",
                "A meal the cook let catch. Less filling, and eating one sours the mood.",
                "A cook's slip",
                new[] { ("ui.skill.cooking", "the higher, the fewer"), ("ui.arch.tool.campfire", "burns half as often again") }),
            Eaten("ui.res.carrots", 180, 75, "Raw", "−50",
                "The field crop. Eaten raw in a pinch, and three of them make a meal.",
                "Harvested from a growing zone, 5 a plant",
                new[] { ("ui.terrain.carrot", "the crop"), ("ui.res.meal.veg", "three make one"), ("ui.arch.tool.growzone", "where it grows") }),
            Eaten("ui.res.berries", 60, 75, "Raw", "−50",
                "Picked from a ripe berry bush. Eaten raw, or cooked with the rest.",
                "Harvest a berry bush, 8 a picking",
                new[] { ("ui.terrain.bush.berry", "where they grow"), ("ui.arch.tool.harvest", "the order that picks them") }),
            Eaten("ui.res.mushrooms", 70, 75, "Raw", "−50",
                "Found in the grass under the trees. Eaten raw, or cooked with the rest; more come up somewhere else.",
                "Beside trees, 3 to 5 at a time",
                new[] { ("ui.terrain.tree.meadow", "found beside trees"), ("ui.res.meal.veg", "cooked into one") }),
        };

        static AlmanacEntry Eaten(string key, int nutrition, int stack, string tier, string mood,
            string definition, string source, (string, string)[] related) =>
            Keyed(key, Food, "Eaten", tier,
                definition, source, AlmanacAction.FindOnMap,
                new[] {
                    ("Nutrition", $"{nutrition} of the need's 1,000"), ("Stack", $"{stack}"),
                    ("Eaten", tier == "Cooked" ? "First" : tier == "Sealed" ? "Second" : tier == "Burnt" ? "Third" : "Last"),
                    ("Mood", $"{mood} for a quarter of a day"), ("Spoils", "Not yet: nothing rots"),
                },
                Effects("EATING",
                    "A hungry colonist takes the best food she can reach: a cooked meal, then rations, then a burnt meal, then anything raw — the nearest of the best. One portion is a sitting.",
                    ("Mood", "Each kind of food leaves its own thought for 15,000 ticks; a meal's and a ration's stack twice."),
                    ("To cook", "A cook turns 500 nutrition of any raw food into one meal.")),
                related);

        static IReadOnlyList<AlmanacEntry> MedicineEntries() => new List<AlmanacEntry>
        {
            Keyed("ui.res.medkit", Medicine, "Medical", "Supplies",
                "Dressings and drugs. A doctor uses one unit per treatment, for forty points of health and a proper tend.",
                "6 in the starting kit; the medical drop", AlmanacAction.FindOnMap,
                new[] {
                    ("Stack", "10"), ("Storage", Medicine),
                    ("Heals", "+40 a treatment"), ("Without it", "A bare dressing, +10"),
                    ("Never past", "80% of the patient's health"), ("Tend quality", "×1.0, against bare hands' ×0.3"),
                },
                Specs("Treatment heals and tends at once: every untended injury is tended and every bleed stops. The same colonist cannot be healed again for a quarter of a day, though she can still be tended.",
                    ("Treating herself", "Half the healing, three times as slow", "And never past 60%"),
                    ("Patients", "Below 50% or bleeding", "She goes to bed until 80%"),
                    (L("ui.bulletin.medicaldrop"), "4 to 8 a drop", "The only source after the kit")),
                new[] { ("ui.work.doctor", "the work that uses it"), ("ui.skill.medicine", "sets the tend's quality"), ("ui.bulletin.medicaldrop", "brings more"), ("ui.health.tended", "what a treatment leaves") }),
        };

        static IReadOnlyList<AlmanacEntry> WeaponEntries() => new List<AlmanacEntry>
        {
            Melee("ui.item.bat", 7, 120, "Blunt", "20% chance to stun for 1 s",
                "A wooden bat. One is in the starting kit, and bandits carry them.", "The starting kit; dropped by bandits",
                new[] { ("ui.item.crowbar", "the bandit's other weapon"), ("ui.pawn.bandit", "carries one") }),
            Melee("ui.item.crowbar", 8, 132, "Blunt", "25% chance to stun for 1.5 s",
                "A crowbar: heavier and slower than a bat, and it stuns more often. Only bandits bring them.", "Dropped by bandits",
                new[] { ("ui.item.bat", "lighter and quicker"), ("ui.pawn.bandit", "carries one") }),
            Melee("ui.item.machete", 8, 96, "Sharp", "No stun; its cuts bleed",
                "A machete, sharp and the quickest to swing. One is in the starting kit.", "The starting kit",
                new[] { ("ui.item.arcblade", "hits harder"), ("ui.health.wound", "what a sharp blow leaves") }),
            Melee("ui.item.arcblade", 10, 114, "Sharp", "No stun; its cuts bleed",
                "An arc blade, the hardest-hitting thing a colonist can hold. It is found only through the debug menu.", "The debug menu",
                new[] { ("ui.item.machete", "quicker, lighter"), ("ui.skill.melee", "the skill that swings it") }),

            Keyed("ui.item.pistol", Weapons, "Weapon", "Ranged",
                "A pistol: ten damage a shot out to 26 metres, and a club when an enemy is within reach. Gunmen carry them.",
                "Dropped by gunmen", AlmanacAction.FindOnMap,
                new[] {
                    ("Damage", "10 a shot"), ("Range", "26 m, about 10 cells"),
                    ("Aim", "30 ticks"), ("Between shots", "60 ticks"),
                    ("Accuracy", "95% at 3 m, 85% at 12 m, 65% at 25 m"), ("Up close", "Clubs for 5, blunt"),
                },
                Specs("A shot needs a clear line of sight in three dimensions. A miss carries on into the ground; a hit lands on the body and can bleed. Within reach the pistol is swung, on the Melee skill.",
                    ("Quality", "Poor to Epic", "Damage ×0.90 to ×1.35, accuracy ×0.90 to ×1.15"),
                    ("Cover", "Sandbags and walls stop shots", "Crouching behind cover is worth taking"),
                    ("Skill", L("ui.skill.shooting"), "Trained by every shot, hit or miss")),
                new[] { ("ui.skill.shooting", "the skill that aims it"), ("ui.pawn.gunman", "carries one"), ("ui.arch.tool.sandbag", "the cover that stops it") }),
        };

        static AlmanacEntry Melee(string key, int damage, int cooldown, string kind, string special,
            string definition, string source, (string, string)[] related) =>
            Keyed(key, Weapons, "Weapon", "Melee",
                definition, source, AlmanacAction.FindOnMap,
                new[] {
                    ("Damage", $"{damage}, {kind.ToLowerInvariant()}"), ("Between swings", $"{cooldown} ticks ({cooldown / 60.0:0.0} s)"),
                    ("Special", special), ("Variance", "±20% a blow"),
                    ("Skill", L("ui.skill.melee")),
                },
                Specs("Right-click a weapon and choose Equip to put it in a colonist's hand; she fights with it when drafted or attacked. Bare hands do 4 a blow every two seconds.",
                    ("Quality", "Poor to Epic", "Damage ×0.90 to ×1.35"),
                    ("Dropped", "When the holder dies", "The downed keep theirs")),
                related);
    }
}
