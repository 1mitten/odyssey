#nullable enable
using System.Collections.Generic;

namespace Odyssey.Hud
{
    // People and animals, and what a colonist is made of: skills, work, needs, health. Rates are
    // per mille of the standard (1,000 = 100%); a needs interval is 150 ticks, 400 to the day.
    // Numbers are Skills.xml's, WorkTypes.xml's, Needs.xml's, Thoughts.xml's, Health.xml's and
    // Species.xml's.
    public static partial class AlmanacCatalogue
    {
        static readonly (string, string)[] SkillShared =
        {
            ("Levels", "0 to 20"), ("Passion", "None ×0.35, minor ×1, major ×1.5 to learning"),
            ("Decay", "Only from level 10, and never below it"),
        };

        static (string, string)[] WithShared(params (string, string)[] rows)
        {
            var all = new List<(string, string)>(rows);
            all.AddRange(SkillShared);
            return all.ToArray();
        }

        static IReadOnlyList<AlmanacEntry> PeopleEntries() => new List<AlmanacEntry>
        {
            Keyed("ui.pawn.colonist", People, "Person", "Ours",
                Icon("colonist", "#7fd0e0", "M12 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8z M4 21a8 8 0 0 1 16 0"),
                "One of the colony: rolled with a face, a name, a pace and her skills, and kept alive by food, rest and a roof.",
                "Chosen at the start of a game", AlmanacAction.OpenWork,
                new[] {
                    ("Health", "100 points; down at 0, dead at −50"), ("Walk", "1.5 m/s, rolled 85% to 115% of it"),
                    ("Needs", L("ui.need.food") + ", " + L("ui.need.rest") + ", " + L("ui.need.joy")),
                    ("When drafted", "Runs at twice the pace and takes orders"), ("Work", "22 work types, eight live"),
                },
                Effects("BEHAVIOUR",
                    "A colonist does the most urgent work she is set to, eats when hungry, sleeps when tired, and heals in bed. Her pane shows what she is doing and why; the Work tab says what she may do, the Assign tab where she may do it and what she does about danger.",
                    ("Pace", "The pane's Pace line multiplies what she was born with, her condition, the rain and the draft."),
                    ("Response", "Fight back, defend or flee when attacked, set on the Assign tab."),
                    ("An unordered fight", "Ends in downs, never deaths: nobody finishes off the fallen unless told to.")),
                new[] { ("ui.need.food", "a need"), ("ui.skill.construction", "a skill"), ("ui.combat.health", "her health"), ("ui.overlay.home", "where she may be kept") }),

            Keyed("ui.pawn.bandit", People, "Person", "Hostile",
                Icon("bandit", "#d04040", "M12 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8z M4 21a8 8 0 0 1 16 0 M8 7h8"),
                "A raider in a welding helmet and a red vest, with a crowbar or a bat. Hunts whoever is still standing; with nobody left to fight, carries something off.",
                "Raids, and the debug menu", AlmanacAction.FindOnMap,
                new[] {
                    ("Health", "100 points; down at 0, dead at −50"), ("Weapon", L("ui.item.crowbar") + " or " + Lc("ui.item.bat")),
                    ("Doors", "Treats a shut door as a wall and breaks it"), ("Climbs", "Ladders, hops and wading, like a colonist"),
                    ("Wants", "Loot"),
                },
                Effects("BEHAVIOUR",
                    "A bandit makes for the nearest reachable colonist who is still on her feet. A downed bandit stays down and drops nothing until dead; a dead one drops its weapon.",
                    (L("ui.bulletin.theft"), "With nobody to fight and nothing to break, it carries the nearest stack off the board."),
                    ("Raids", "In a raid it moves with the band and leaves when half the band is down.")),
                new[] { ("ui.pawn.gunman", "the armed kind"), ("ui.alert.raid", "how they come"), ("ui.bulletin.theft", "what they take") }),

            Keyed("ui.pawn.gunman", People, "Person", "Hostile",
                Icon("gunman", "#d04040", "M12 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8z M4 21a8 8 0 0 1 16 0 M15 14h6"),
                "A bandit with a pistol, dressed as the rest. Shoots from range and clubs whoever comes close.",
                "Raids with gunmen in the mix", AlmanacAction.FindOnMap,
                new[] {
                    ("Health", "100 points; down at 0, dead at −50"), ("Weapon", L("ui.item.pistol") + ", always"),
                    ("Range", "26 m"), ("Up close", "Clubs with the pistol"),
                },
                Effects("BEHAVIOUR",
                    "A gunman is a bandit in everything but its weapon. Put walls or sandbags between it and the colony.",
                    ("Mixed raids", "About three raiders in ten carry pistols.")),
                new[] { ("ui.pawn.bandit", "the rest of the band"), ("ui.item.pistol", "what it carries"), ("ui.arch.tool.sandbag", "cover against it") }),
        };

        static IReadOnlyList<AlmanacEntry> FaunaEntries() => new List<AlmanacEntry>
        {
            Keyed(PawnKindLabels.IconKeys[PawnKindLabels.MiddenHogKind], Fauna, "Fauna", "Wild",
                Icon("fauna_hog", "#c49a7a", "M4 13c0-3 3-6 8-6s8 3 8 6v3H4v-3z M6 16v3 M18 16v3 M20 12l2-1"),
                "The midden hog is pig stock gone feral. It lives in sounders in the woodland, rests often, and will turn on anyone who hits it. No meat yet, and no taming.",
                "Wild " + Lc(PawnKindLabels.Wandering) + " or " + Lc(PawnKindLabels.Resting) + " on the Animals tab (F5)", AlmanacAction.OpenAnimals,
                new[] {
                    ("Pace", "60% of a colonist's walk"), ("Lives", "Within 2 cells of a tree, in sounders of 3 to 5"),
                    ("Day", "Out by day, rests three times longer at night"), ("Leg", "Within 8 cells, then a rest of 5 to 15 s"),
                    ("Climbs", "Terrace ramps only; never a ladder, a door or water"), ("Stays", "About two days, then walks off the edge"),
                    ("Health", "60 points; bites for 6"), ("Hit it", "Turns on you 7 times in 10"),
                },
                Effects("BEHAVIOUR",
                    "A sounder lands together and drifts apart across its wood. Each hog takes a short leg and then rests, and by night rests far longer. One that decides to go walks to the nearest board edge and is gone; another group walks in when the board is short.",
                    ("Fights back", "A struck hog turns on its attacker, or flees 12 cells."),
                    ("Rain", "In heavy rain it looks for cover within its range."),
                    ("No swimming", "Streams and ponds are walls to it.")),
                new[] { (PawnKindLabels.IconKeys[PawnKindLabels.DuctRatKind], "another animal on the board"), ("ui.terrain.tree.meadow", "the woods it keeps to"), ("ui.arch.tool.door", "stops it") }),

            Keyed(PawnKindLabels.IconKeys[PawnKindLabels.DuctRatKind], Fauna, "Fauna", "Vermin",
                Icon("fauna_duct_rat", "#8c7d75", "M3 13c1-2 4-3 7-3 4 0 8 2 10 6H2c0-1 0-2 1-3z M17 10a2 2 0 1 0 0-4 2 2 0 0 0 0 4z"),
                "The duct rat keeps to the rock: outcrops, faces and cuts. It is out at night, climbs ladders and opens doors, and will not swim.",
                "Wild " + Lc(PawnKindLabels.Wandering) + " or " + Lc(PawnKindLabels.Resting) + " on the Animals tab (F5)", AlmanacAction.OpenAnimals,
                new[] {
                    ("Pace", "90% of a colonist's walk"), ("Lives", "Beside rock at the surface, alone"),
                    ("Day", "Out at night, rests three times longer by day"), ("Leg", "Within 5 cells, then a rest of 2 to 8 s"),
                    ("Climbs", "Ladders and doors; terrace ramps but no other step; never water"), ("Stays", "About two days, then walks off the edge"),
                    ("Health", "15 points; bites for 2"), ("Body", "0.25 m long"),
                },
                Effects("BEHAVIOUR",
                    "A rat darts and stops: short legs, short rests, most of its living done in the dark. It takes the ladders a hog cannot, so a rat can turn up on a storey no hog reaches.",
                    ("Nocturnal", "By day it rests three times as long and takes a third as many legs."),
                    ("Timid", "Struck, it nearly always runs."),
                    ("Arrivals", "New rats walk in at the board's edge, one at a time.")),
                new[] { (PawnKindLabels.IconKeys[PawnKindLabels.MiddenHogKind], "another animal on the board"), ("ui.arch.tool.ladder", "a climb it takes"), ("ui.terrain.rock", "what it keeps beside") }),

            Keyed(PawnKindLabels.IconKeys[PawnKindLabels.CulvertFrogKind], Fauna, "Fauna", "Wild",
                Icon("fauna_culvert_frog", "#2f9e57", "M5 15c0-4 3-7 7-7s7 3 7 7H5z M8 8a2 2 0 1 0 0-4 2 2 0 0 0 0 4z M16 8a2 2 0 1 0 0-4 2 2 0 0 0 0 4z M4 18l3-3 M20 18l-3-3"),
                "The culvert frog, the commonest animal on the meadow, lives on the banks of its water in threes to fives. It hops rather than walks and stays out in the rain.",
                "Wild " + Lc(PawnKindLabels.Wandering) + " or " + Lc(PawnKindLabels.Resting) + " on the Animals tab (F5)", AlmanacAction.OpenAnimals,
                new[] {
                    ("Pace", "A colonist's walk, in hops"), ("Lives", "Within 3 cells of water, in threes to fives"),
                    ("Day", "Out by day, rests three times longer at night"), ("Leg", "Within 4 cells, then a rest of 3 to 12 s"),
                    ("Climbs", "Terrace ramps only; never a ladder, a door or water"), ("Stays", "About two days, then walks off the edge"),
                    ("Health", "8 points; never fights back"), ("Body", "About 0.9 m, emerald green"),
                },
                Effects("BEHAVIOUR",
                    "A frog takes short legs along its bank and sits between them. Put one down away from water and it heads for the nearest bank it can reach. New frogs walk in only where the water meets the edge of the board.",
                    ("Bank", "Every leg ends within three cells of water."),
                    ("Rain", "Stays out in it."),
                    ("No swimming", "It sits at the water's edge; streams and ponds are walls to it.")),
                new[] { (PawnKindLabels.IconKeys[PawnKindLabels.MiddenHogKind], "another animal on the board"), ("ui.terrain.water.shallow", "the water it keeps beside") }),
        };

        static IReadOnlyList<AlmanacEntry> SkillEntries() => new List<AlmanacEntry>
        {
            Skill("ui.skill.construction", "M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.8-3.8a6 6 0 0 1-7.9 7.9l-6.9 6.9a2.1 2.1 0 0 1-3-3l6.9-6.9a6 6 0 0 1 7.9-7.9z",
                "Building: how fast, whether it goes wrong, and how good a bed comes out.",
                "Building, delivering, deconstructing, laying and lifting power lines",
                "A builder at level 0 botches 15 jobs in 100, losing the work and half the material; from level 3 she never does. The quality of a bed is rolled from this skill when it is finished.",
                new[] { ("0", "70% speed", "Botches 15 in 100"), ("3", "92% speed", "Never botches"), ("4", "100% speed", "The standard"), ("10", "145% speed", "Beds mostly Decent"), ("20", "220% speed", "The only level that makes Epic") },
                ("ui.work.construction", "ui.arch.tool.bed")),
            Skill("ui.skill.mining", "M4 20l16-16M14 4l6 6M4 14l6 6",
                "Digging: how fast rock, ore and rubble come out.",
                "Digging, and clearing rubble",
                "Mining buys speed. Every rock cell gives 8 stone and every seam cell 15 ore, whoever digs it.",
                new[] { ("0", "55% speed", ""), ("5", "108% speed", ""), ("10", "160% speed", ""), ("20", "265% speed", "") },
                ("ui.work.mining", "ui.terrain.rock")),
            Skill("ui.skill.cutting", "M5 21l7-7 M14 4l6 6-8 8-6-6z",
                "Felling: how fast the axe comes down and the tree goes over.",
                L("ui.arch.tool.fell"),
                "Chopping buys speed. What a tree gives is the tree's, not the chopper's.",
                new[] { ("0", "60% speed", ""), ("4", "100% speed", "The standard"), ("10", "160% speed", ""), ("20", "260% speed", "") },
                ("ui.work.cutting", "ui.terrain.tree.giant")),
            Skill("ui.skill.growing", "M12 22v-9 M12 13a5 5 0 0 0 5-5c0-4-5-6-5-6s-5 2-5 6a5 5 0 0 0 5 5z",
                "Sowing, harvesting and picking: how fast.",
                "Sowing, harvesting, picking berries",
                "Growing buys speed at the hoe. It does not change what a crop yields: a carrot plant gives five whoever harvests it.",
                new[] { ("0", "60% speed", ""), ("4", "100% speed", "The standard"), ("10", "160% speed", ""), ("20", "260% speed", "") },
                ("ui.work.growing", "ui.terrain.carrot")),
            Skill("ui.skill.cooking", "M4 13h16a8 8 0 0 1-16 0z M3 13h18 M9 9c0-2 2-2 2-4 M13 9c0-2 2-2 2-4",
                "Cooking: how fast a meal is made, and how seldom it burns.",
                "Cooking a bill",
                "A meal is 300 ticks of work at full speed. The burn chance falls with every level and is gone from 14; a campfire burns half as often again.",
                new[] { ("0", "40% speed", "Burns 30 in 100"), ("5", "70% speed", "Burns 10 in 100"), ("8", "88% speed", "Burns 5 in 100"), ("10", "100% speed", "Burns 3 in 100"), ("14", "124% speed", "Never burns") },
                ("ui.work.cooking", "ui.res.meal.burnt")),
            Skill("ui.skill.medicine", "M4 7h16v13H4z M9 7V4h6v3 M12 10v7 M8.5 13.5h7",
                "Treating the hurt: how fast, and how well a tend is done.",
                "Treating a patient, or herself",
                "The tend's quality comes from this skill, times the supplies used, and a better tend heals faster: 4 to 12 points a day.",
                new[] { ("0", "60% speed", "Tends at 20%"), ("5", "110% speed", "Tends at 70%"), ("10", "160% speed", "Tends at 110%"), ("20", "260% speed", "Tends at 155%") },
                ("ui.work.doctor", "ui.res.medkit")),
            Skill("ui.skill.melee", "M14.5 3.5l6 6-11 11-3-3z M3.5 20.5l3-3",
                "Close combat: landing a blow, and getting out of the way of one.",
                "Every swing, landed or not",
                "Every swing trains it. A critical blow does half as much again.",
                new[] { ("0", "Hits 50%", "Dodges 0%"), ("10", "Hits 80%", "Dodges 10%"), ("20", "Hits 90%", "Dodges 30%") },
                ("ui.item.machete", "ui.pawn.bandit")),
            Skill("ui.skill.shooting", "M12 2v4 M12 18v4 M2 12h4 M18 12h4 M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8z",
                "Aim: how much of a shot's accuracy survives each cell of distance.",
                "Every shot, hit or miss",
                "Accuracy is multiplied by the skill's factor once per cell to the target, then by the weapon's own curve and any cover.",
                new[] { ("0", "87.6% a cell", ""), ("10", "94.3% a cell", ""), ("20", "98.3% a cell", "") },
                ("ui.item.pistol", "ui.pawn.gunman")),
        };

        static AlmanacEntry Skill(string key, string path, string definition, string trainedBy, string paragraph,
            (string, string, string)[] levels, (string Work, string Thing) related) =>
            Keyed(key, Skills, "Skill", "Learned", Icon(key.Substring(9), "#7fd0e0", path),
                definition, "Every colonist, on her Skills tab", AlmanacAction.OpenWork,
                WithShared(("Trained by", trainedBy)),
                Levels(paragraph + " Levels cost more as they climb; a day's learning in one skill past 4,000 points counts a fifth.", levels),
                new[] { (related.Work, "the work it speeds"), (related.Thing, "what it touches"), ("ui.pawn.colonist", "who has it") });

        static IReadOnlyList<AlmanacEntry> WorkEntries() => new List<AlmanacEntry>
        {
            WorkType("ui.work.construction", "ui.skill.construction", false,
                "Delivering material, building, deconstructing, laying and lifting power lines.",
                new[] { ("ui.arch.tool.wall", "built"), ("ui.arch.tool.deconstruct", "taken down") }),
            WorkType("ui.work.growing", "ui.skill.growing", false,
                "Sowing and harvesting growing zones, and picking ripe berry bushes.",
                new[] { ("ui.arch.tool.growzone", "worked"), ("ui.arch.tool.harvest", "picked") }),
            WorkType("ui.work.cooking", "ui.skill.cooking", false,
                "Working the bills at an electric cooker or a campfire.",
                new[] { ("ui.arch.tool.galley", "worked"), ("ui.res.meal.veg", "made") }),
            WorkType("ui.work.cutting", "ui.skill.cutting", false,
                "Felling marked trees and clearing marked bushes.",
                new[] { ("ui.arch.tool.fell", "the order"), ("ui.res.wood", "what it brings in") }),
            WorkType("ui.work.mining", "ui.skill.mining", false,
                "Digging marked rock, ore and ground, and clearing rubble.",
                new[] { ("ui.arch.tool.mine", "the order"), ("ui.res.stone", "what it brings in") }),
            WorkType("ui.work.hauling", "", false,
                "Carrying loose things to the best store that will take them, emptying stores of what they refuse, and refuelling generators.",
                new[] { ("ui.arch.tool.stockpile", "where it carries"), ("ui.arch.tool.generator", "refuelled") }),
            WorkType("ui.work.doctor", "ui.skill.medicine", true,
                "Treating the hurt: with medical supplies for 40, or with a bare dressing for 10.",
                new[] { ("ui.res.medkit", "what it uses"), ("ui.health.tended", "what it leaves") }),
            WorkType("ui.work.rescue", "", true,
                "Carrying the downed to a bed.",
                new[] { ("ui.arch.tool.bed", "where they are carried"), ("ui.combat.health", "down at 0") }),
        };

        static AlmanacEntry WorkType(string key, string skillKey, bool emergency, string covers, (string, string)[] related)
        {
            var rel = new List<(string, string)>(related);
            if (skillKey.Length > 0) rel.Insert(0, (skillKey, "the skill that sets its pace"));
            return Keyed(key, WorkTypes, "Work type", emergency ? "Emergency" : "Priority",
                Icon(key.Substring(8), "#7fd0e0", emergency ? "M12 3v12 M12 19h.01 M4 21h16L12 3z" : "M4 6h16M4 12h16M4 18h10"),
                covers, "A column on the Work tab (F1)", AlmanacAction.OpenWork,
                new[] {
                    ("Skill", skillKey.Length > 0 ? L(skillKey) : "None: anyone does it at one pace"),
                    ("Priority", "1 most urgent to 4, or never; every colonist starts at 3"),
                    ("Emergency", emergency ? "Yes: scanned ahead of ordinary work at the same priority" : "No"),
                },
                Effects("BEHAVIOUR",
                    "A colonist looks for work in priority order, 1 first; at equal priority she takes emergency work first, then the work types in the Work tab's order. A new priority decides her next job, not the one in her hands.",
                    ("Covers", covers)),
                rel.ToArray());
        }

        static IReadOnlyList<AlmanacEntry> NeedEntries() => new List<AlmanacEntry>
        {
            Keyed("ui.need.food", Needs, "Need", "Body",
                Icon("need_food", "#e0a05a", "M18 8h1a4 4 0 0 1 0 8h-1 M2 8h16v9a4 4 0 0 1-4 4H6a4 4 0 0 1-4-4V8z M6 1v3 M10 1v3 M14 1v3"),
                "How fed a colonist is, 0 to 1,000. It falls all day and night, and she eats the best food she can reach once it is under 300.",
                "Every colonist's pane", AlmanacAction.None,
                new[] {
                    ("Full to empty", "About 22 game hours"), ("Eats", "Below 300"),
                    ("Mood", "−60 at 250, −120 at 125, −200 at 0"), ("At 0", "Starvation builds over 2.5 days"),
                    ("Starvation", "Slows her by up to 30%; it does not kill yet"),
                },
                Effects("PHYSIOLOGY",
                    "Hunger falls fastest while she is fed and slows as she empties. Starving, she loses condition in three steps and is kept at 70% of her pace and work at worst.",
                    ("Best first", "A cooked meal, then rations, then a burnt meal, then raw food."),
                    ("Kept home", "A colonist kept Home eats outside it only when starving.")),
                new[] { ("ui.res.meal.veg", "the best food"), ("ui.res.rations", "the second best"), ("ui.need.mood", "what hunger costs") }),

            Keyed("ui.need.rest", Needs, "Need", "Body",
                Icon("need_rest", "#8f9fe0", "M21 12.8A9 9 0 1 1 11.2 3 7 7 0 0 0 21 12.8z"),
                "How rested a colonist is, 0 to 1,000: the bar is what she has left. Under 280 she goes to bed; at nothing she drops where she stands.",
                "Every colonist's pane", AlmanacAction.None,
                new[] {
                    ("Full to empty", "About 25 game hours awake"), ("Sleeps", "Below 280; wakes at 950"),
                    ("Mood", "−60 at 280, −120 at 200, −180 at 100"), ("At 0", "Collapses where she stands, even drafted"),
                    ("In bed", "85% to 140% by quality"), ("On the ground", "80%, and −40 mood"),
                },
                Effects("PHYSIOLOGY",
                    "Sleep restores rest at the bed's rate, times how comfortable the room's temperature is: a cold or hot bedroom rests her at 90%, 75% or 55%.",
                    ("Without a bed", "The ground rests at 80% and leaves the thought of it."),
                    ("Tiredness", "Does not slow her work or her walk; it only costs mood, until she drops.")),
                new[] { ("ui.arch.tool.bed", "where she rests best"), ("ui.need.mood", "what tiredness costs") }),

            Keyed("ui.need.joy", Needs, "Need", "Mind",
                Icon("need_joy", "#e0d05a", "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z M8 14c1 2 2.5 3 4 3s3-1 4-3 M9 9h.01 M15 9h.01"),
                "Recreation: time spent idle, 0 to 1,000. It rises whenever a colonist has nothing to do and falls while she works. It is not on her pane yet.",
                "Simulated, not yet shown", AlmanacAction.None,
                new[] {
                    ("Rises", "While she wanders or waits"), ("Sought", "Never: she does not go looking for it"),
                    ("Mood", "−200 at 0, up to +100 above 850"),
                },
                Effects("PSYCHOLOGY",
                    "A colonist who never stops working loses recreation and, with it, mood. Give her slack in the day and it comes back by itself.",
                    ("Bands", "−200 at 0, −100 to 150, −50 to 300, none to 700, +50 to 850, +100 above.")),
                new[] { ("ui.need.mood", "what it moves") }),

            Keyed("ui.need.mood", Needs, "Need", "Mind",
                Icon("need_mood", "#7fd0e0", "M12 2a10 10 0 1 0 10 10A10 10 0 0 0 12 2zm0 18a8 8 0 1 1 8-8 8 8 0 0 1-8 8zm-3.5-9a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3zm7 0a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3zm-7 4.5a5.5 5.5 0 0 0 7 0"),
                "How good a colonist feels, 0 to 1,000. It drifts towards a target made of her needs, the temperature and what has happened to her lately.",
                "Every colonist's pane", AlmanacAction.None,
                new[] {
                    ("Target", "500, plus needs, temperature and thoughts"), ("Moves", "Up 7 or down 5 an interval"),
                    ("Breaks", "Below 350, on average once in 10 days"), ("A break", "Wanders for 3 game hours"),
                    ("After", "Catharsis, +150 for half a day"),
                },
                Effects("PSYCHOLOGY",
                    "Every thought is worth a set amount for a set time; a second copy of the same thought is worth three quarters of the first.",
                    ("Ate a meal", "+50 for a quarter day (stacks twice)"),
                    ("Ate a ration", "+20 for a quarter day (stacks twice)"),
                    ("Ate burnt or raw food", "−40 or −50 for a quarter day"),
                    ("Slept on the ground", "−40 for a quarter day"),
                    ("Slept cold or hot", "−30 for a third of a day"),
                    ("Fell", "−60 for a third of a day"),
                    ("Attacked by a colonist", "−80 for a day"),
                    ("A colonist died", "−60 for three days (stacks three times)")),
                new[] { ("ui.need.food", "a need"), ("ui.need.rest", "a need"), ("Temperature", "comfort costs mood") }),
        };

        static IReadOnlyList<AlmanacEntry> HealthEntries() => new List<AlmanacEntry>
        {
            Keyed("ui.combat.health", Health, "Health", "Pool",
                Icon("health", "#e05a5a", "M12 21l-1.4-1.3C5.4 15.4 2 12.3 2 8.5 2 5.4 4.4 3 7.5 3c1.7 0 3.4.8 4.5 2.1C13.1 3.8 14.8 3 16.5 3 19.6 3 22 5.4 22 8.5c0 3.8-3.4 6.9-8.6 11.5z"),
                "A person's pool of 100 points. At 0 she is down; at −50 she is dead. She gets up again only when the pool is full.",
                "The pane's Health bar and the Health tab", AlmanacAction.None,
                new[] {
                    ("Pool", "100 for a person"), ("Down", "At 0, or when her body gives out"),
                    ("Dead", "At −50"), ("Heals", "20 a day, in bed only"),
                    ("Gets up", "At a full pool"),
                },
                Effects("HEALTH",
                    "Blows, bullets and falls come off the pool and land on the body's regions as injuries. She can also be downed with points to spare, by pain, by losing consciousness or by her legs giving out.",
                    ("Once down", "Stays where she fell until a colonist carries her to a bed."),
                    ("Animals", "Heal 12 a day wherever they lie, and get up at 15%.")),
                new[] { ("Body", "where blows land"), ("ui.health.pain", "can down her early"), ("ui.work.rescue", "carries her to bed"), ("ui.res.medkit", "heals it") },
                alsoKeys: new[] { "ui.combat.condition" }),

            new AlmanacEntry(string.Empty, "Body", Health, "Six regions over one pool", "Health", "Regions",
                Icon("body", "#e05a5a", "M12 2a3 3 0 1 0 0 6 3 3 0 0 0 0-6z M8 9h8l1 7h-2l-1 6h-4l-1-6H7z M5 10l3-1 M19 10l-3-1"),
                "A person is six regions: head, torso, two arms, two legs. A blow lands on one by how much of the body it covers; a limb's excess passes to the torso.",
                "The Health tab", AlmanacAction.None,
                new[] {
                    ("Head", "25 points, vital; drives consciousness"), ("Torso", "40 points, vital"),
                    ("Arms", "30 points each; drive manipulation"), ("Legs", "30 points each; drive moving"),
                    ("Blows land", "Torso 40%, head 10%, each limb 12.5%"), ("Falls land", "Legs 35% each, torso 30%"),
                    ("A fall", "15 × layers^1.5: 15 for one, 168 for five"),
                },
                Effects("HEALTH",
                    "Injuries of one kind in one region merge. A vital region at nothing takes consciousness to nothing, which downs her; it does not kill by itself.",
                    ("Falls", "Land as two to four hits; a fall of two layers or more breaks something."),
                    ("Limbs", "Damage past a limb's points goes on into the torso.")),
                new[] { ("ui.health.wound", "a kind of injury"), ("ui.health.fracture", "from a fall"), ("ui.health.moving", "what the legs drive") },
                new[] { "ui.health.head", "ui.health.torso", "ui.health.arm", "ui.health.leg", "ui.health.arm.left", "ui.health.arm.right", "ui.health.leg.left", "ui.health.leg.right" }),

            Capacity("ui.health.pain", "M12 2v10 M12 16h.01 M4 21h16L12 3z",
                "Every point of injury hurts: 1.25% each. At 80% she goes down in pain shock — 64 points of injury, whatever her pool says.",
                new[] { ("Per point", "1.25%"), ("Shock", "At 80%: down"), ("Also", "Takes up to 40% off consciousness") },
                new[] { ("ui.health.consciousness", "what pain takes"), ("ui.health.tended", "does not stop it") }),
            Capacity("ui.health.consciousness", "M12 4.5a4.5 4.5 0 0 0-4.5 4.5c0 1.9 1.1 3.5 2.7 4.1v2.9h3.6v-2.9c1.6-.6 2.7-2.2 2.7-4.1a4.5 4.5 0 0 0-4.5-4.5z M9 19h6v2H9z",
                "How awake she is. Pain above 10% takes some of it, blood loss more, and a vital region at nothing all of it. Under 30% she goes down.",
                new[] { ("Pain", "Takes (pain − 10%) × 4/9, at most 40%"), ("Blood loss", "×0.9 at 15%, ×0.8 at 30%, ×0.6 from 45%"), ("Down", "Under 30%") },
                new[] { ("ui.health.pain", "takes some"), ("ui.health.blood", "takes more"), ("ui.health.moving", "scaled by it") }),
            Capacity("ui.health.moving", "M13.5 5.5a2 2 0 1 0 0-4 2 2 0 0 0 0 4zM9.8 8.9L7 23h2.1l1.8-8 2.1 2v6h2v-7.5l-2.1-2 .6-3C14.8 12 16.8 13 19 13v-2c-1.9 0-3.5-1-4.3-2.4l-1-1.6c-.4-.6-1-1-1.7-1l-6 2.3V13h2V9.6l1.8-.7",
                "Her legs, times how awake she is. It scales how fast she walks; at 15% or less she cannot stand.",
                new[] { ("From", "What is left of both legs, averaged, × consciousness"), ("Walk", "Scales her pace"), ("Down", "At 15% or less") },
                new[] { ("Body", "the legs"), ("ui.health.manipulation", "the arms' twin") }),
            Capacity("ui.health.manipulation", "M8 13V5a1.5 1.5 0 0 1 3 0v6 M11 11V4a1.5 1.5 0 0 1 3 0v7 M14 11V5.5a1.5 1.5 0 0 1 3 0V14c0 4-3 7-6 7s-5-2-6-4l-2-4a1.5 1.5 0 0 1 2.6-1.5L8 14",
                "Her arms, times how awake she is. It scales how fast she works.",
                new[] { ("From", "What is left of both arms, averaged, × consciousness"), ("Work", "Scales every work rate") },
                new[] { ("Body", "the arms"), ("ui.health.moving", "the legs' twin") }),
            Capacity("ui.health.blood", "M12 3c3 4 6 7 6 11a6 6 0 0 1-12 0c0-4 3-7 6-11z",
                "What bleeding has taken. Every point of untended cut loses blood by the day; all of it, and she dies.",
                new[] { ("Loses", "6% a day per point of untended cut"), ("A 10-point cut", "Kills in about 40 hours"), ("Comes back", "33% a day once nothing bleeds") },
                new[] { ("ui.health.wound", "what bleeds"), ("ui.health.tended", "stops it"), ("ui.health.consciousness", "what it takes") }),
            Capacity("ui.health.wound", "M4 20L20 4 M8 20l12-12",
                "A cut from a sharp blow or a bullet: the one kind of injury that bleeds.",
                new[] { ("From", "Blades and bullets"), ("Bleeds", "Until tended") },
                new[] { ("ui.health.blood", "what it drains"), ("ui.health.bruise", "the blunt kind") }),
            Capacity("ui.health.bruise", "M12 4a8 8 0 1 0 0 16 8 8 0 0 0 0-16z M9 10h.01 M14 13h.01",
                "An injury from a blunt blow or a short fall. It hurts and never bleeds.",
                new[] { ("From", "Clubs, fists and falls"), ("Bleeds", "Never") },
                new[] { ("ui.health.wound", "the sharp kind"), ("ui.health.fracture", "a worse fall") }),
            Capacity("ui.health.fracture", "M5 19l5-5-2-2 4-4-2-2 5-5 M14 10l5 5",
                "A broken bone: the worst hit of a fall of two layers or more.",
                new[] { ("From", "A fall of two layers or more"), ("Bleeds", "Never") },
                new[] { ("Body", "falls land on the legs"), ("ui.health.bruise", "a shorter fall") }),
            Capacity("ui.health.tended", "M5 12l5 5L20 7",
                "Treated. A treatment tends every untended injury at once and stops every bleed; a tended injury heals wherever she is.",
                new[] { ("Heals", "4 to 12 points a day, by the tend's quality"), ("Quality", "Medicine skill × supplies; bare hands at most 70%") },
                new[] { ("ui.work.doctor", "tends"), ("ui.res.medkit", "a better tend"), ("ui.skill.medicine", "sets the quality") }),
        };

        static AlmanacEntry Capacity(string key, string path, string definition, (string, string)[] properties, (string, string)[] related) =>
            Keyed(key, Health, "Health", key.EndsWith("wound") || key.EndsWith("bruise") || key.EndsWith("fracture") ? "Injury" : key.EndsWith("tended") ? "State" : "Capacity",
                Icon(key.Substring(10), "#e05a5a", path),
                definition, "The Health tab", AlmanacAction.None,
                properties,
                Effects("HEALTH", definition),
                related);
    }
}
