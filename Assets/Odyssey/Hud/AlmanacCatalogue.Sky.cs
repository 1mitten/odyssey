#nullable enable
using System.Collections.Generic;

namespace Odyssey.Hud
{
    // The sky, the year and what arrives. Weather.xml's spells and offsets, Climate.xml's default
    // year (a site picked on the planet rescales it), Temperature.xml's bands, Incidents.xml's
    // drops and RaidMixes.xml. Nothing fires an event but the debug menu yet: there is no storyteller.
    public static partial class AlmanacCatalogue
    {
        static IReadOnlyList<AlmanacEntry> WeatherEntries() => new List<AlmanacEntry>
        {
            Sky(WeatherLabels.ClearKey,
                "Open sky. The warmest weather and the commonest in every season.",
                "+1.5 °C", "16 to 40 hours", "45%, 65%, 55%",
                "Nothing slows anyone and nothing needs shelter."),
            Sky(WeatherLabels.CloudyKey,
                "Grey sky. A little colder, and the colours drain from the world.",
                "−1.5 °C", "10 to 30 hours", "30%, 20%, 40%",
                "It changes only the temperature — and through it mood, sleep and work — and the light."),
            Sky(WeatherLabels.RainKey,
                "Rain, from drizzle to downpour. Under open sky it slows everyone a little and waters the crops.",
                "−3 °C", "6 to 24 hours", "19%, 11%, 4%",
                "A roof or a tree keeps a colonist dry. Heavy rain sends hogs and rats to cover; the frogs stay out."),
            Sky(WeatherLabels.StormKey,
                "A storm: the heaviest rain and the coldest weather. The rarest.",
                "−5 °C", "4 to 12 hours", "6%, 4%, 1%",
                "Always heavy, so always the full rain penalty in the open, and animals looking for cover."),

            Named("The year", Weather, "Seventy-two days in three seasons", "Calendar", "Seasons", "ui.world.seasons",
                "The year is 72 days: six months of twelve, two to each season. Wash is spring, Glare summer, Rime winter.",
                "The clock", AlmanacAction.None,
                new[] {
                    ("Day", "24 hours of 2,500 ticks"), ("Month", "12 days"), ("Year", "6 months: 72 days"),
                    ("Wash", "Larkspur and Tansy: 15 and 19 °C"), ("Glare", "Bramble and Ember: 22 and 27 °C"),
                    ("Rime", "Hollow and Candle: 2 and −8 °C"),
                },
                Effects("SEASONS",
                    "Those are the default climate's month averages; a site picked on the planet shifts and stretches them. Each day swings about 5 °C either side, warmest at 14:00 and coldest at 02:00, and the weather adds its own.",
                    ("Rime", "Cold enough to stop the carrots and chill anyone out of doors: plan a warm room before it."),
                    ("Its weather", "Each season rolls its own mix of clear, cloudy, rain and storm.")),
                new[] { ("Temperature", "what the seasons do"), (WeatherLabels.ClearKey, "the commonest weather"), ("ui.terrain.carrot", "stops in the cold") }),

            new AlmanacEntry("ui.overlay.temperature", Registry.Label("ui.overlay.temperature"), Weather, "Warm, cold, and what each costs", "Climate", "Rooms", "ui.overlay.temperature",
                "Every enclosed room holds one temperature of its own; outside is the season, the hour and the weather. A colonist is comfortable from 16 to 26 °C.",
                "The Temperature overlay, and a tile's pane", AlmanacAction.None,
                new[] {
                    ("Comfortable", "16 to 26 °C"), ("Mild", "10 to 16, or 26 to 32: −10 mood, sleep 90%"),
                    ("Bad", "−3 to 10, or 32 to 35: −50 mood, sleep 75%"), ("Extreme", "Below −3 or above 35: −120 mood, sleep 55%"),
                    ("Work", "70% outside 8 to 35 °C"), ("Cold or heat stroke", "Slows her; does not kill yet"),
                },
                Effects("CLIMATE",
                    "Beyond −3 °C or 35 °C a colonist builds up cold or heat, and at worst it takes 30% off her pace and work. Rooms change it: walls, a roof, a campfire or a heater.",
                    ("Underground", "The ground damps the seasons: a cellar lags, and a deep mine holds the year's average."),
                    ("Warm air rises", "Heat climbs a stairwell four times as readily as cold falls down it."),
                    ("Crops", "Carrots stop at 0 °C and at 58 °C.")),
                new[] { ("ui.arch.tool.heater", "warms a room"), ("ui.arch.tool.campfire", "warms without power"), ("The year", "the seasons") }),
        };

        static AlmanacEntry Sky(string key, string definition,
            string offset, string spell, string chances, string paragraph) =>
            Keyed(key, Weather, "Sky", "Spell",
                definition, "The clock's weather glyph", AlmanacAction.None,
                new[] {
                    ("Temperature", offset + " at full strength"), ("A spell", spell),
                    ("Chance a spell", chances + " in Wash, Glare and Rime"),
                    ("In the open", key == WeatherLabels.RainKey || key == WeatherLabels.StormKey ? "Pace down to 90%; crops up to +25%" : "No effect"),
                },
                Effects("WEATHER",
                    "When a spell ends the next is rolled from the season's mix, and it blends in over two hours. " + paragraph,
                    ("Shelter", "Rain reaches only where the sky does: a roof, a slab or a tree crown keeps it off.")),
                new[] { ("The year", "sets the mix"), ("Temperature", "what it shifts") });

        static IReadOnlyList<AlmanacEntry> EventEntries() => new List<AlmanacEntry>
        {
            Drop("ui.bulletin.supplydrop", "ui.res.rations", "10 to 20"),
            Drop("ui.bulletin.scrapdrop", "ui.res.scrap", "15 to 30"),
            Drop("ui.bulletin.medicaldrop", "ui.res.medkit", "4 to 8"),

            Keyed("ui.alert.raid", Events, "Event", "Threat",
                "A band of hostiles that behaves as one: it walks on from one edge, gathers, probes, and then assaults the hearth. When half of it is down, the rest leave.",
                "The debug menu's Events tab", AlmanacAction.None,
                new[] {
                    ("Size", "1 a colonist, plus 1 every 5 days survived; 1 to 30"), ("Mix", "Bandits, gunmen, or mixed 7 to 3"),
                    ("Gathers", "2 to 4 hours, 12 cells in from its edge"), ("Probes", "An hour, halfway in"),
                    ("Assaults", "The hearth, else where the colony began"), ("Leaves", "When half the band is down"),
                },
                Effects("PHASES",
                    "The war horn sounds as the band walks on; the Raid alert stands while it assaults. Hitting a raider, or a colonist coming within 15 cells, starts the assault at once.",
                    ("Arrive", "Walks on along one random edge."),
                    ("Gather", "Mills about for two to four hours."),
                    ("Probe", "Moves halfway to the target for an hour."),
                    ("Assault", "Goes for the hearth and fights whoever it meets."),
                    ("Retreat", "At half down, the rest walk off the nearest edge with what they took.")),
                new[] { ("ui.pawn.bandit", "most of the band"), ("ui.pawn.gunman", "the armed ones"), ("ui.arch.tool.campfire", "the hearth they go for") },
                alsoKeys: new[] { "ui.bulletin.raidincoming" }),

            Keyed("ui.bulletin.trader", Events, "Event", Registry.Label("ui.bulletin.arrival"),
                "A trader walks in from one edge and waits by the hearth for about a day. Send a colonist to it to trade.",
                "The debug menu's Events tab", AlmanacAction.None,
                new[] { ("Brings", "One trader, a purse of 400 to 900 gold and 4 to 7 kinds of goods"), ("Stays", "About a day"), ("Written", "When it arrives, with the arrival chime") },
                Effects("EVENT",
                    "One trader at a time: the event refuses while a visit is on the board. There is no storyteller yet, so only the debug menu sends one.",
                    ("Ends early", "A raid arriving, or the trader being hurt by accident, sends it home."),
                    ("Refused", "While another trader is on the board, or the board has no reachable edge.")),
                new[] { ("ui.pawn.trader", "who comes"), ("ui.res.gold", "what it pays in"), ("ui.bulletin.raidincoming", "what sends it home") }),

            Keyed("ui.bulletin.theft", Events, "Event", "Loss",
                "A bandit with nobody left to fight and nothing to break carries the nearest stack off the board.",
                "Written on the Events panel when it happens", AlmanacAction.None,
                new[] { ("Takes", "The nearest stack of anything"), ("Written", "When it leaves the board with it") },
                Effects("EVENT", "Recorded, never sent: it happens because a bandit was left standing."),
                new[] { ("ui.pawn.bandit", "the thief"), ("ui.bulletin.banditleft", "left with nothing") }),

            Keyed("ui.bulletin.banditleft", Events, "Event", "Departure",
                "A bandit walked off the board with nothing. Only a lone bandit is written down: a raid's retreat is not.",
                "Written on the Events panel when it happens", AlmanacAction.None,
                new[] { ("Written", "When a bandit outside a raid leaves empty-handed") },
                Effects("EVENT", "Recorded, never sent."),
                new[] { ("ui.pawn.bandit", "who left"), ("ui.bulletin.theft", "left with something") }),
        };

        static AlmanacEntry Drop(string key, string itemKey, string count) =>
            Keyed(key, Events, "Event", "Drop",
                $"{count} {Lc(itemKey)} fall from the sky on to a random spot anywhere on the board, and the colony hauls them.",
                "The debug menu's Events tab", AlmanacAction.None,
                new[] {
                    ("Brings", $"{count} " + Lc(itemKey)), ("Lands", "Anywhere on the board, on the topmost floor with room"),
                    ("Falls", "For six seconds"),
                },
                Effects("EVENT",
                    "Nothing fires a drop on its own yet: there is no storyteller, only the debug menu. The Events panel lists it, and its row jumps the camera to where it came down.",
                    ("A tree in the way", "A load aimed at a tree lands beside the trunk.")),
                new[] { (itemKey, "what it brings"), ("ui.work.hauling", "brings it in") });
    }
}
