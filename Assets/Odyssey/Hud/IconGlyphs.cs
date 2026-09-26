#nullable enable
using System.Collections.Generic;

namespace Odyssey.Hud
{
    /// <summary>
    /// <b>The one place an icon's line art lives</b>, by registry key: every <c>IconBadge</c> in the
    /// game draws the owner's pixel art for its key where there is some (<c>IconArt</c>) and this
    /// otherwise — the inspect pane's avatar, a list row, the palette, the Inventory, the Almanac.
    /// Change a shape here and it changes everywhere it is drawn.
    ///
    /// <para>Owner, 2026-09-26: <i>"these icons used need to match (and come from the same place if
    /// possible so we don't have to update several places) the panel info when you are clicking
    /// around"</i>. Until then the Almanac held its own paths and the pane drew a placeholder square
    /// for the same key, so a bush had a picture on its page and none when clicked.</para>
    ///
    /// <para>Paths are on the 24-unit grid, stroked (design 39 §4, <c>PathGlyph</c>). When the
    /// owner's sheets arrive a key's pixel art simply wins; nothing here needs removing.
    /// <c>IconGlyphsTests</c> holds every key to the registry and every path to the parser, and
    /// <c>AlmanacCatalogueTests</c> holds every Almanac page to a glyph here.</para>
    /// </summary>
    public static class IconGlyphs
    {
        /// <summary>The line art for a key, or empty when the key has none.</summary>
        public static string For(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (Paths.TryGetValue(key, out string? path)) return path;
            return Aliases.TryGetValue(key, out string? same) && Paths.TryGetValue(same, out path) ? path : string.Empty;
        }

        /// <summary>Keys drawn as another key: one shape, two names for it.</summary>
        public static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>
        {
            { "ui.terrain.bush.picked", "ui.terrain.bush.berry" },
            { "ui.combat.condition", "ui.combat.health" },
            { "ui.bulletin.raidincoming", "ui.alert.raid" },
            { "ui.health.head", "ui.health.torso" },
            { "ui.health.arm", "ui.health.torso" },
            { "ui.health.leg", "ui.health.torso" },
            { "ui.health.arm.left", "ui.health.torso" },
            { "ui.health.arm.right", "ui.health.torso" },
            { "ui.health.leg.left", "ui.health.torso" },
            { "ui.health.leg.right", "ui.health.torso" },
        };

        public static readonly IReadOnlyDictionary<string, string> Paths = new Dictionary<string, string>
        {
            // terrain
            { "ui.terrain.grass", "M4 20c2-6 5-11 8-16 3 5 6 10 8 16M8 20c1-4 3-7 5-10M16 20c-1-4-3-7-5-10" },
            { "ui.terrain.marsh", "M3 18h18M5 18c0-4 1-7 2-9M9 18c0-3 0-6-1-8M15 18c0-3 1-6 2-8M19 18c0-4-1-7-2-9M3 14c3-1 5 1 8 0" },
            { "ui.terrain.water.shallow", "M2 15c4-2 6 2 10 0s6 2 10 0M2 9c4-2 6 2 10 0s6 2 10 0" },
            { "ui.terrain.water.deep", "M2 8c4-2 6 2 10 0s6 2 10 0M2 13c4-2 6 2 10 0s6 2 10 0M2 18c4-2 6 2 10 0s6 2 10 0" },
            { "ui.terrain.sand", "M3 17c3-1 6-1 9 0s6 1 9 0M6 12h.01M11 10h.01M16 12h.01M9 14h.01M14 14h.01" },
            { "ui.terrain.packedgravel", "M4 17h16M6 14a2 1.5 0 1 0 4 0 2 1.5 0 1 0-4 0M13 13a2.5 1.5 0 1 0 5 0 2.5 1.5 0 1 0-5 0M9 10a2 1.5 0 1 0 4 0 2 1.5 0 1 0-4 0" },
            { "ui.terrain.subsoil", "M2 8h20M2 14h20M5 11h.01M10 11h.01M15 11h.01M19 11h.01M7 17h.01M13 17h.01M18 17h.01" },
            { "ui.terrain.rock", "M4 18l3-11 9-3 5 7-2 7z M7 7l6 5 4-5" },
            { "ui.terrain.bedrock", "M2 20h20M2 16h20M4 16l2-4 4 2 4-5 4 3 2-2v6" },
            { "ui.res.rubble", "M3 19l3-4 3 2 3-5 4 3 5-2v6H3z M7 10l2-2 2 2-2 2z M15 8l2-1 1 2-2 1z" },
            { "ui.terrain.bareearth", "M2 18c4-3 8-3 10 0 2-3 6-3 10 0M4 12c3-2 6-2 8 0 2-2 5-2 8 0" },
            { "ui.terrain.gravel", "M6 16a3 2 0 1 0 6 0 3 2 0 1 0-6 0M14 12a4 2.5 0 1 0 8 0 4 2.5 0 1 0-8 0M8 8a2.5 2 0 1 0 5 0 2.5 2 0 1 0-5 0" },
            // flora
            { "ui.terrain.tree.birch", "M12 2c-2 0-4 3-4 7s2 6 4 6 4-2 4-6-2-7-4-7z M12 15v7 M11 18h2" },
            { "ui.terrain.tree.meadow", "M12 2c-4 0-7 3-7 7 0 3 2 5 4 6h6c2-1 4-3 4-6 0-4-3-7-7-7z M12 15v7 M9 22h6" },
            { "ui.terrain.tree.fruit", "M12 4c-5 0-9 3-9 6 0 2 2 4 5 4h8c3 0 5-2 5-4 0-3-4-6-9-6z M12 14v8 M8 9h.01 M15 8h.01 M12 11h.01" },
            { "ui.terrain.tree.giant", "M12 1c-6 0-10 4-10 8 0 3 3 6 6 6h8c3 0 6-3 6-6 0-4-4-8-10-8z M10 15v7 M14 15v7 M8 22h8" },
            { "ui.terrain.bush", "M4 19h16M5 19c-2-3 0-7 3-7 0-3 3-5 5-4 2-2 6 0 5 3 3 0 4 5 1 8" },
            { "ui.terrain.bush.berry", "M4 19h16M5 19c-2-3 0-7 3-7 0-3 3-5 5-4 2-2 6 0 5 3 3 0 4 5 1 8M9 14h.01M13 12h.01M15 15h.01" },
            { "ui.terrain.carrot", "M12 22V10 M8 10c0-3 4-6 4-6s4 3 4 6 M7 15h10" },
            // materials
            { "ui.res.wood", "M4 6h16M4 12h16M4 18h16M7 3v18M17 3v18" },
            { "ui.res.stone", "M3 8l9-5 9 5v8l-9 5-9-5V8z" },
            { "ui.res.scrap", "M4 7l8-4 8 4v10l-8 4-8-4V7z M9 12l3 3 5-5" },
            { "ui.res.ironore", "M12 2l8 5v10l-8 5-8-5V7l8-5z M8 10l4 3 4-3" },
            { "ui.res.coal", "M5 9l5-5 9 3 2 7-6 6-8-2z" },
            // food
            { "ui.res.meal", "M4 13h16a8 8 0 0 1-16 0z M3 13h18 M9 9c0-2 2-2 2-4 M13 9c0-2 2-2 2-4" },
            { "ui.res.meal.veg", "M4 13h16a8 8 0 0 1-16 0z M3 13h18 M9 9c0-2 2-2 2-4 M13 9c0-2 2-2 2-4" },
            { "ui.res.rations", "M5 4h14v16H5z M5 9h14 M9 4v5" },
            { "ui.res.meal.burnt", "M4 13h16a8 8 0 0 1-16 0z M3 13h18 M10 5c1 1 1 2 0 3 M14 5c1 1 1 2 0 3" },
            { "ui.res.carrots", "M12 21l-4-9c-1-2 0-5 3-5s4 3 3 5l-2 9z M12 7V2 M9 4l3 3 3-3" },
            { "ui.res.berries", "M5 15a3 3 0 1 0 6 0 3 3 0 1 0-6 0 M11 11a3 3 0 1 0 6 0 3 3 0 1 0-6 0 M10 18a3 3 0 1 0 6 0 3 3 0 1 0-6 0 M15 8l3-4" },
            { "ui.res.mushrooms", "M4 12a8 6 0 0 1 16 0z M10 12v8h4v-8" },
            // medicine
            { "ui.res.medkit", "M4 7h16v13H4z M9 7V4h6v3 M12 10v7 M8.5 13.5h7" },
            // weapons
            { "ui.item.bat", "M5 19l2 2 11-11-2-2z M16 8l2-5 3 3-5 2" },
            { "ui.item.crowbar", "M6 21L18 5l3 1 M6 21l-2-1" },
            { "ui.item.machete", "M4 20l3-3M7 17l11-11 2-3-3 2L6 16z M5 15l4 4" },
            { "ui.item.arcblade", "M4 20l3-3M7 17l11-11 2-3-3 2L6 16z M5 15l4 4 M18 12l2 1-1 2 2 1" },
            { "ui.item.pistol", "M3 9h15l2-2h1v5h-6l-1 2h-3l-1 5H6l1-5H3z" },
            // structures
            { "ui.arch.tool.wall", "M3 3h18v18H3z M3 9h18 M3 15h18 M9 3v6 M15 3v6 M6 9v6 M12 9v6 M18 9v6 M9 15v6 M15 15v6" },
            { "ui.arch.tool.door", "M5 3h14v18H5z M15 12h2" },
            { "ui.arch.tool.roof", "M2 9h20v4H2z M5 13v8 M19 13v8" },
            { "ui.arch.tool.pillar", "M8 3h8M8 21h8M10 3v18M14 3v18" },
            { "ui.arch.tool.deckplate", "M3 3h8v8H3z M13 3h8v8h-8z M3 13h8v8H3z M13 13h8v8h-8z" },
            { "ui.arch.tool.ladder", "M7 2v20 M17 2v20 M7 6h10 M7 11h10 M7 16h10" },
            { "ui.arch.tool.stair", "M3 21h4v-4h4v-4h4V9h4V5h2" },
            { "ui.arch.tool.sandbag", "M3 19h18 M4 19c0-2 1-3 4-3s4 1 4 3 M12 19c0-2 1-3 4-3s4 1 4 3 M8 16c0-2 1-3 4-3s4 1 4 3" },
            // furniture
            { "ui.arch.tool.bed", "M3 7v11M21 7v11M3 13h18M3 9h8a2 2 0 0 1 2 2v2H3z" },
            { "ui.arch.tool.shelf", "M4 3v18M20 3v18M4 8h16M4 13h16M4 18h16" },
            { "ui.arch.tool.campfire", "M12 3c2 3 4 5 4 8a4 4 0 0 1-8 0c0-2 1-3 2-4 0 2 1 3 2 3 0-3-1-5 0-7z M4 21l16-3 M4 18l16 3" },
            // production
            { "ui.arch.tool.galley", "M4 8h16v12H4z M4 12h16 M8 5v3 M12 5v3 M16 5v3 M8 16h.01M12 16h.01" },
            // power
            { "ui.arch.tool.generator", "M3 8h18v10H3z M7 18v2 M17 18v2 M13 9l-3 4h3l-2 4" },
            { "ui.arch.tool.heater", "M5 4h14v16H5z M9 8c1 1 1 2 0 3s-1 2 0 3 M15 8c1 1 1 2 0 3s-1 2 0 3" },
            { "ui.arch.tool.conduit", "M3 12h6l2-3 2 6 2-3h6" },
            // zones and orders
            { "ui.arch.tool.stockpile", "M3 3h18v18H3z M3 3l18 18 M21 3L3 21" },
            { "ui.arch.tool.growzone", "M3 21h18 M6 21v-6 M12 21v-9 M18 21v-6 M6 15c-2 0-3-2-3-3 2 0 3 1 3 3z M12 12c-2 0-3-2-3-3 2 0 3 1 3 3z M18 15c-2 0-3-2-3-3 2 0 3 1 3 3z" },
            { "ui.overlay.home", "M12 2.5 1.5 11.5H4.5V21.5H10V15.5H14V21.5H19.5V11.5H22.5Z" },
            { "ui.arch.tool.mine", "M4 20l16-16M14 4l6 6M4 14l6 6" },
            { "ui.arch.tool.fell", "M5 21l7-7 M14 4l6 6-8 8-6-6z" },
            { "ui.arch.tool.harvest", "M6 12a6 6 0 0 0 12 0 M12 6v6 M9 3h6" },
            { "ui.arch.tool.deconstruct", "M4 20L20 4 M4 4l6 6 M14 14l6 6" },
            { "ui.arch.tool.cancel", "M6 6l12 12M18 6L6 18" },
            { "ui.arch.tool.unwire", "M3 12h6 M15 12h6 M9 9l6 6 M15 9l-6 6" },
            // people
            { "ui.pawn.colonist", "M12 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8z M4 21a8 8 0 0 1 16 0" },
            { "ui.pawn.bandit", "M12 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8z M4 21a8 8 0 0 1 16 0 M8 7h8" },
            { "ui.pawn.gunman", "M12 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8z M4 21a8 8 0 0 1 16 0 M15 14h6" },
            // the butcher's four levels (design 62): a pig's head and a cleaver; scarred, bloodied, crowned
            { "ui.pawn.butcher", "M5 12c0-4 3-7 7-7s7 3 7 7-3 7-7 7-7-3-7-7z M10 13h4v3h-4z M7 5L5 2 M17 5l2-3 M20 14l3-4v8z" },
            { "ui.pawn.butcher.scarred", "M5 12c0-4 3-7 7-7s7 3 7 7-3 7-7 7-7-3-7-7z M10 13h4v3h-4z M7 5L5 2 M17 5l2-3 M8 7l4 4" },
            { "ui.pawn.butcher.blood", "M5 12c0-4 3-7 7-7s7 3 7 7-3 7-7 7-7-3-7-7z M10 13h4v3h-4z M7 5L5 2 M17 5l2-3 M21 17c0 1.5-1 2.5-2 2.5s-2-1-2-2.5 2-4 2-4 2 2.5 2 4z" },
            { "ui.pawn.butcher.king", "M5 13c0-4 3-7 7-7s7 3 7 7-3 7-7 7-7-3-7-7z M10 14h4v3h-4z M7 5l2 2 3-4 3 4 2-2v2H7z" },
            // fauna
            { "ui.pawn.hog", "M4 13c0-3 3-6 8-6s8 3 8 6v3H4v-3z M6 16v3 M18 16v3 M20 12l2-1" },
            { "ui.pawn.rat", "M3 13c1-2 4-3 7-3 4 0 8 2 10 6H2c0-1 0-2 1-3z M17 10a2 2 0 1 0 0-4 2 2 0 0 0 0 4z" },
            { "ui.pawn.frog", "M5 15c0-4 3-7 7-7s7 3 7 7H5z M8 8a2 2 0 1 0 0-4 2 2 0 0 0 0 4z M16 8a2 2 0 1 0 0-4 2 2 0 0 0 0 4z M4 18l3-3 M20 18l-3-3" },
            // skills
            { "ui.skill.construction", "M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.8-3.8a6 6 0 0 1-7.9 7.9l-6.9 6.9a2.1 2.1 0 0 1-3-3l6.9-6.9a6 6 0 0 1 7.9-7.9z" },
            { "ui.skill.mining", "M4 20l16-16M14 4l6 6M4 14l6 6" },
            { "ui.skill.cutting", "M5 21l7-7 M14 4l6 6-8 8-6-6z" },
            { "ui.skill.growing", "M12 22v-9 M12 13a5 5 0 0 0 5-5c0-4-5-6-5-6s-5 2-5 6a5 5 0 0 0 5 5z" },
            { "ui.skill.cooking", "M4 13h16a8 8 0 0 1-16 0z M3 13h18 M9 9c0-2 2-2 2-4 M13 9c0-2 2-2 2-4" },
            { "ui.skill.medicine", "M4 7h16v13H4z M9 7V4h6v3 M12 10v7 M8.5 13.5h7" },
            { "ui.skill.melee", "M14.5 3.5l6 6-11 11-3-3z M3.5 20.5l3-3" },
            { "ui.skill.shooting", "M12 2v4 M12 18v4 M2 12h4 M18 12h4 M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8z" },
            // work types
            { "ui.work.construction", "M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.8-3.8a6 6 0 0 1-7.9 7.9l-6.9 6.9a2.1 2.1 0 0 1-3-3l6.9-6.9a6 6 0 0 1 7.9-7.9z" },
            { "ui.work.growing", "M12 22v-9 M12 13a5 5 0 0 0 5-5c0-4-5-6-5-6s-5 2-5 6a5 5 0 0 0 5 5z" },
            { "ui.work.cooking", "M4 13h16a8 8 0 0 1-16 0z M3 13h18 M9 9c0-2 2-2 2-4 M13 9c0-2 2-2 2-4" },
            { "ui.work.cutting", "M5 21l7-7 M14 4l6 6-8 8-6-6z" },
            { "ui.work.mining", "M4 20l16-16M14 4l6 6M4 14l6 6" },
            { "ui.work.hauling", "M5 8h14M5 8a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v2a2 2 0 0 1-2 2M5 8v10a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8" },
            { "ui.work.doctor", "M4 7h16v13H4z M9 7V4h6v3 M12 10v7 M8.5 13.5h7" },
            { "ui.work.rescue", "M3 15h18 M5 15v3 M19 15v3 M8 11a2 2 0 1 0 0-4 2 2 0 0 0 0 4z M11 12h8" },
            // needs
            { "ui.need.food", "M18 8h1a4 4 0 0 1 0 8h-1 M2 8h16v9a4 4 0 0 1-4 4H6a4 4 0 0 1-4-4V8z M6 1v3 M10 1v3 M14 1v3" },
            { "ui.need.rest", "M21 12.8A9 9 0 1 1 11.2 3 7 7 0 0 0 21 12.8z" },
            { "ui.need.joy", "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z M8 14c1 2 2.5 3 4 3s3-1 4-3 M9 9h.01 M15 9h.01" },
            { "ui.need.mood", "M12 2a10 10 0 1 0 10 10A10 10 0 0 0 12 2zm0 18a8 8 0 1 1 8-8 8 8 0 0 1-8 8zm-3.5-9a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3zm7 0a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3zm-7 4.5a5.5 5.5 0 0 0 7 0" },
            // health
            { "ui.combat.health", "M12 21l-1.4-1.3C5.4 15.4 2 12.3 2 8.5 2 5.4 4.4 3 7.5 3c1.7 0 3.4.8 4.5 2.1C13.1 3.8 14.8 3 16.5 3 19.6 3 22 5.4 22 8.5c0 3.8-3.4 6.9-8.6 11.5z" },
            { "ui.health.pain", "M12 2v10 M12 16h.01 M4 21h16L12 3z" },
            { "ui.health.consciousness", "M12 4.5a4.5 4.5 0 0 0-4.5 4.5c0 1.9 1.1 3.5 2.7 4.1v2.9h3.6v-2.9c1.6-.6 2.7-2.2 2.7-4.1a4.5 4.5 0 0 0-4.5-4.5z M9 19h6v2H9z" },
            { "ui.health.moving", "M13.5 5.5a2 2 0 1 0 0-4 2 2 0 0 0 0 4zM9.8 8.9L7 23h2.1l1.8-8 2.1 2v6h2v-7.5l-2.1-2 .6-3C14.8 12 16.8 13 19 13v-2c-1.9 0-3.5-1-4.3-2.4l-1-1.6c-.4-.6-1-1-1.7-1l-6 2.3V13h2V9.6l1.8-.7" },
            { "ui.health.manipulation", "M8 13V5a1.5 1.5 0 0 1 3 0v6 M11 11V4a1.5 1.5 0 0 1 3 0v7 M14 11V5.5a1.5 1.5 0 0 1 3 0V14c0 4-3 7-6 7s-5-2-6-4l-2-4a1.5 1.5 0 0 1 2.6-1.5L8 14" },
            { "ui.health.blood", "M12 3c3 4 6 7 6 11a6 6 0 0 1-12 0c0-4 3-7 6-11z" },
            { "ui.health.wound", "M4 20L20 4 M8 20l12-12" },
            { "ui.health.bruise", "M12 4a8 8 0 1 0 0 16 8 8 0 0 0 0-16z M9 10h.01 M14 13h.01" },
            { "ui.health.fracture", "M5 19l5-5-2-2 4-4-2-2 5-5 M14 10l5 5" },
            { "ui.health.tended", "M5 12l5 5L20 7" },
            // weather and seasons
            { "ui.weather.clear", "M12 7a5 5 0 1 0 0 10 5 5 0 0 0 0-10z M12 1v2 M12 21v2 M4.2 4.2l1.4 1.4 M18.4 18.4l1.4 1.4 M1 12h2 M21 12h2 M4.2 19.8l1.4-1.4 M18.4 5.6l1.4-1.4" },
            { "ui.weather.cloudy", "M7 18a5 5 0 1 1 1-9.9A6 6 0 0 1 19 10a4 4 0 0 1 0 8z" },
            { "ui.weather.rain", "M7 14a5 5 0 1 1 1-9.9A6 6 0 0 1 19 6a4 4 0 0 1 0 8z M8 17l-1 3 M12 17l-1 3 M16 17l-1 3" },
            { "ui.weather.storm", "M7 14a5 5 0 1 1 1-9.9A6 6 0 0 1 19 6a4 4 0 0 1 0 8z M13 14l-3 4h3l-2 4" },
            { "ui.world.seasons", "M4 5h16v16H4z M4 10h16 M8 3v4 M16 3v4" },
            { "ui.overlay.temperature", "M14 14.8V4a2 2 0 0 0-4 0v10.8a4 4 0 1 0 4 0z" },
            // events
            { "ui.bulletin.supplydrop", "M12 2v10 M12 12l4-4 M12 12l-4-4 M4 16h16v4H4z" },
            { "ui.bulletin.scrapdrop", "M12 2v8 M12 10l4-4 M12 10l-4-4 M4 15l8-3 8 3v5l-8 3-8-3z" },
            { "ui.bulletin.medicaldrop", "M12 2v8 M12 10l4-4 M12 10l-4-4 M4 13h16v9H4z M12 15v5 M9.5 17.5h5" },
            { "ui.alert.raid", "M12 2l9 4v6c0 5-4 9-9 10-5-1-9-5-9-10V6z M12 8v5 M12 16h.01" },
            { "ui.bulletin.theft", "M4 7h16v13H4z M9 7V4h6v3 M15 14h6 M18 11l3 3-3 3" },
            { "ui.bulletin.banditleft", "M9 21H5V3h4 M16 17l5-5-5-5 M21 12H9" },
            // the body: the Almanac's Body page, and every region by alias
            { "ui.health.torso", "M12 2a3 3 0 1 0 0 6 3 3 0 0 0 0-6z M8 9h8l1 7h-2l-1 6h-4l-1-6H7z M5 10l3-1 M19 10l-3-1" },
        };
    }
}
