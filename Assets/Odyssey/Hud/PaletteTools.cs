#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// One tool on the Build palette that does something: what it is called, how to arm it, and
    /// how to ask whether it is armed.
    ///
    /// <para><b>The third of those is the point of this type.</b> The shell used to answer "is
    /// this chip lit" with a hard-coded chain of key comparisons, one branch per live tool, sitting
    /// a hundred lines away from the table that armed them. Two lists of the same three tools, and
    /// the failure mode when they drifted was not a compile error — it was a chip that arms a tool
    /// and never lights, which reads as the click having missed. A tool is one row now, and a row
    /// carries both halves or it does not exist.</para>
    /// </summary>
    public readonly struct PaletteTool
    {
        /// <summary>The registry key, which is also the chip's icon and its label.</summary>
        public readonly string Key;

        readonly Action<DesignateDirector> _arm;
        readonly Func<DesignateDirector, bool> _armed;

        /// <summary>
        /// Whether arming this tool makes the "made of" row meaningful. True for anything built out
        /// of something; false for an order, which is a verb applied to what is already there.
        /// </summary>
        public readonly bool WantsMaterial;

        public PaletteTool(string key, Action<DesignateDirector> arm,
            Func<DesignateDirector, bool> armed, bool wantsMaterial = false)
        {
            Key = key;
            _arm = arm;
            _armed = armed;
            WantsMaterial = wantsMaterial;
        }

        /// <summary>Pick this tool up, or put it down if it is already held.</summary>
        public void Arm(DesignateDirector director) => _arm(director);

        /// <summary>Is this the tool the player is holding?</summary>
        public bool IsArmed(DesignateDirector director) => _armed(director);
    }

    /// <summary>
    /// The Build palette's contents: which categories it offers, which tools each holds, and which
    /// of those tools are live.
    ///
    /// <para><b>Here rather than in the shell</b>, for the reason <see cref="HudCommands"/> is —
    /// the command bar's order and labels are data, the fast tier can see this assembly and cannot
    /// see a <c>MonoBehaviour</c>, and a palette that claims to arm a tool is a claim worth
    /// testing. The shell draws what this says and decides nothing.</para>
    ///
    /// <para><b>Every key here exists in the naming registry</b>, and nothing in this file is named
    /// in C#: a chip's label is <c>Registry.Label(key)</c>, so a name the owner corrects in
    /// <c>docs/design/icon-keys.csv</c> reaches the screen without anyone retyping it.</para>
    /// </summary>
    public static class PaletteTools
    {
        // The keys the shell and the tests need by name. Everything else is a string in the table
        // below and is never compared against.
        public const string Wall = "ui.arch.tool.wall";

        /// <summary>
        /// The <b>slab</b>: an upper floor, on a wall or bridging out from one. Never on the
        /// ground, which is already a floor.
        ///
        /// <para>The key says "roof" because a slab is both — it floors the layer it is in and
        /// roofs the one below — and keys are forever, so only the label has ever moved (U29, then
        /// again when it stopped being called "Floor" in 2026-09-17's rename).</para>
        ///
        /// <para><b>Named `Slab` here and not `Floor`, deliberately.</b> The identifier the player
        /// sees and the identifier in the code used to disagree, which cost three rounds of
        /// confusion in one afternoon. Note that <c>BuildingHandle.Floor</c> is still this one:
        /// handle <i>values</i> are a save contract and were not worth the risk of a swap that
        /// would compile silently and mean the other thing.</para>
        /// </summary>
        public const string Slab = "ui.arch.tool.roof";

        /// <summary>
        /// <b>Paving</b>, which is what the player simply calls a floor: laid on ground that is
        /// already there (U42). <c>BuildingHandle.DeckPlate</c> behind it, for the reason
        /// <see cref="Slab"/> gives about handle values.
        /// </summary>
        public const string Paving = "ui.arch.tool.deckplate";

        /// <summary>
        /// The way up (U43). Under <c>Structure</c>, beside the wall and the slab, because a wall,
        /// its floor and the ladder onto it are one job.
        /// </summary>
        public const string Ladder = "ui.arch.tool.ladder";

        public const string Bed = "ui.arch.tool.bed";
        public const string Mine = "ui.arch.tool.mine";
        public const string Fell = "ui.arch.tool.fell";
        public const string Cancel = "ui.arch.tool.cancel";
        public const string Deconstruct = "ui.arch.tool.deconstruct";

        /// <summary>
        /// The seven categories the palette offers, in the order they are drawn, each with a few
        /// of its tools. Every icon key exists in the registry; a tool not in <see cref="Live"/>
        /// is drawn and disabled, so the shape of the game is visible before the thing behind a
        /// key exists.
        ///
        /// <para><b>Seven, not ten</b> (specification, 2026-09-17). Orders, Zones and Salvage came
        /// out. All three were answering a different question from the other seven: those seven
        /// are kinds of thing to <i>put down</i>, and a palette whose tiles do not all answer one
        /// question is a palette the player has to read rather than aim at. Zones and Salvage take
        /// nothing live with them. Orders did, and <see cref="Pinned"/> is where its two live
        /// tools went.</para>
        ///
        /// <para><b>A category has no label here, and that is the point</b> (owner, 2026-09-17:
        /// <i>"ensure that consistency can be enforced using a centralised place"</i>). It carried
        /// one until then — "Structure", "Production", seven words written in C# beside the seven
        /// keys that already name them — and the two copies happened to agree, which is what a
        /// silent duplicate looks like right up until somebody corrects one of them.
        /// <c>Registry.Label(key)</c> is the only answer now, and
        /// <c>RegistryTests.NoPlayerFacingNameIsWrittenInCSharp</c> is what stops the second copy
        /// coming back.</para>
        /// </summary>
        public static readonly (string key, string[] tools)[] Categories =
        {
            ("ui.arch.category.structure", new[] { Wall, Paving, "ui.arch.tool.door", "ui.arch.tool.stair", Ladder, Slab, "ui.arch.tool.reclaim" }),
            ("ui.arch.category.production", new[] { "ui.arch.tool.fabricator", "ui.arch.tool.galley", "ui.arch.tool.reclaimer", "ui.arch.tool.bench" }),
            ("ui.arch.category.furniture", new[] { Bed, "ui.arch.tool.bunk", "ui.arch.tool.table", "ui.arch.tool.lamp", "ui.arch.tool.shelf" }),
            ("ui.arch.category.power", new[] { "ui.arch.tool.conduit", "ui.arch.tool.battery", "ui.arch.tool.generator", "ui.arch.tool.reactor" }),
            ("ui.arch.category.security", new[] { "ui.arch.tool.turret", "ui.arch.tool.trap", "ui.arch.tool.barricade" }),
            ("ui.arch.category.floors", new[] { Paving, "ui.arch.tool.grating", "ui.arch.tool.tile" }),
            ("ui.arch.category.recreation", new[] { "ui.arch.tool.gamestable", "ui.arch.tool.viewscreen", "ui.arch.tool.planter" }),
        };

        /// <summary>
        /// What a thing may be made of, in the order the player meets them — and only what the
        /// colony can actually build with.
        ///
        /// <para>Two entries, because <c>ConstructionContent.IsBuildable</c> admits two. The other
        /// four <see cref="StuffHandle"/> values are what the ruined city is made <i>of</i> rather
        /// than what a colony builds <i>with</i>; <see cref="BuildLabels.StuffKeys"/> leaves them
        /// unnamed for the same reason. <see cref="HudTheme.MaterialTintOf"/> already holds tints
        /// for four, so a third and fourth buildable material is one row here.</para>
        /// </summary>
        public static readonly int[] Materials = { StuffHandle.Wood, StuffHandle.Stone };

        /// <summary>
        /// The tools that belong to no category and are always on show. They are the orders strip
        /// down the right-hand gutter, under the depth rail.
        ///
        /// <para><b>None of these is a kind of thing to build.</b> Every tile in the seven
        /// categories answers "what would you like to put down"; each of these answers a question
        /// about <em>what is already on the board</em> — stop that, take that apart, dig that out,
        /// cut that down — and each is wanted at the moment the player is holding something else,
        /// which is exactly when the category tier is showing something different and reaching for
        /// it would cost two clicks and a hunt. Filing them under a category was tidy and wrong in
        /// practice (owner, 2026-09-17, of Cancel: <i>"would be a good idea to be able to access
        /// the cancel button on the build sub menu"</i>, and of Deconstruct, having failed to
        /// reach it from Orders: <i>"could you put deconstruct next to cancel as a button so we
        /// can at least deconstruct this way"</i>).</para>
        ///
        /// <para><b>Chop and Mine joined them when Orders was dropped</b> (2026-09-17). The
        /// specification takes the Orders category off the palette, and those two were the only
        /// live tools in it — so taken literally it would have left the game's mining and felling
        /// reachable by the <c>M</c> and <c>C</c> keys and by nothing a player could see. That is
        /// not a hypothetical failure: it is precisely what had already happened to Cancel, which
        /// <i>"was never missing — every way of finding it was missing"</i>, and which cost a
        /// playtest to find. They belong here on the same test the other two pass, which is the
        /// reason this list is allowed to have grown rather than an exception to it: all four are
        /// verbs applied to what is there, not nouns to place.</para>
        ///
        /// <para>They are in no category at all rather than pinned <i>and</i> listed, because the
        /// same chip appearing twice in one open panel is a question the player has to stop and
        /// answer: whether the two do the same thing.</para>
        ///
        /// <para><b>They left the palette header on 2026-09-17</b> (owner: <i>"the small buttons
        /// on the build menu for Chop Trees, Mine, Deconstruct, Cancel should be a vertical button
        /// strip that sits below the depth control … this enables us to quickly give orders
        /// without having to click the build button — we can use this in future for more
        /// orders"</i>). In the header they were always on show <i>within a panel that was
        /// usually shut</i>, so giving an order cost opening the palette first and the cost was
        /// paid on every order. They are a column in the right-hand gutter now, on screen whether
        /// or not anything else is.</para>
        ///
        /// <para><b>And that lifted the ceiling.</b> Four was the limit while this was a row in a
        /// header with a switcher and a way out beside it; a column down an otherwise empty
        /// gutter is bounded by the screen. Adding a fifth order is one entry here and one in
        /// <see cref="HudTheme.PinnedActionHue"/>, and <see cref="HudLayout.OrdersHeight"/> reads
        /// the length of this array rather than a number somebody wrote down beside it.</para>
        /// </summary>
        public static readonly string[] Pinned = { Fell, Mine, Deconstruct, Cancel };

        /// <summary>
        /// The word the armed banner uses for an order: the order's own name, the one the wiki
        /// prints, the palette's breadcrumb says and the strip's tooltip repeats.
        ///
        /// <para><b>One name for one thing, everywhere</b> (owner, 2026-09-17: <i>"rename
        /// 'Cancelling orders' to Cancel … rename this to 'Deconstruct' … keep the consistent in
        /// the wiki and the language and UI"</i>). The banner was the odd one out: it said
        /// "Chopping", "Mining", "Deconstructing" and — in a C# literal, because cancel has no
        /// activity to borrow — "Cancelling orders", while every other surface in the game called
        /// the same four things <b>Chop trees, Mine, Deconstruct, Cancel</b>. It now reads the
        /// <c>ui.arch.tool.*</c> names, so the wiki, the palette, the strip and the banner cannot
        /// disagree and a rename in <c>icon-keys.csv</c> reaches all four at once.</para>
        ///
        /// <para><b>The gerunds did not go away; they were never this.</b> "Chopping" is what a
        /// <i>colonist</i> is doing and belongs to <c>ui.status.*</c>, which the roster card
        /// draws. An order is an imperative and an activity is a gerund — that is the rule the
        /// banner was breaking by mixing the two namespaces.</para>
        ///
        /// <para>This method rather than a bare <c>Registry.Label</c> call at each site, because
        /// <see cref="HudLayout.ArmedWidth"/> has to walk exactly these words to size the banner
        /// to the longest of them, and "exactly these words" needs one owner.</para>
        /// </summary>
        public static string OrderWord(string key) => Registry.Label(key);

        /// <summary>
        /// The tools that actually do something. Anything absent is drawn disabled, which is most
        /// of the palette until the thing behind a key exists.
        /// </summary>
        public static readonly PaletteTool[] Live =
        {
            new PaletteTool(Wall,
                d => d.ArmBuild(BuildingHandle.Wall),
                d => d.Tool == DesignateTool.Build && d.Building == BuildingHandle.Wall,
                wantsMaterial: true),
            new PaletteTool(Slab,
                d => d.ArmBuild(BuildingHandle.Floor),
                d => d.Tool == DesignateTool.Build && d.Building == BuildingHandle.Floor,
                wantsMaterial: true),
            new PaletteTool(Paving,
                d => d.ArmBuild(BuildingHandle.DeckPlate),
                d => d.Tool == DesignateTool.Build && d.Building == BuildingHandle.DeckPlate,
                wantsMaterial: true),
            new PaletteTool(Ladder,
                d => d.ArmBuild(BuildingHandle.Ladder),
                d => d.Tool == DesignateTool.Build && d.Building == BuildingHandle.Ladder,
                wantsMaterial: true),

            // The first furniture, and the palette's first single-placement, rotatable thing:
            // one per click, turned with the rotate key while it is armed (design 20 §5).
            new PaletteTool(Bed,
                d => d.ArmBuild(BuildingHandle.Bed),
                d => d.Tool == DesignateTool.Build && d.Building == BuildingHandle.Bed,
                wantsMaterial: true),
            new PaletteTool(Mine, Toggle(DesignateTool.Mine), Holding(DesignateTool.Mine)),
            new PaletteTool(Fell, Toggle(DesignateTool.Fell), Holding(DesignateTool.Fell)),
            new PaletteTool(Cancel, Toggle(DesignateTool.Cancel), Holding(DesignateTool.Cancel)),
            new PaletteTool(Deconstruct, Toggle(DesignateTool.Deconstruct), Holding(DesignateTool.Deconstruct)),
        };

        /// <summary>
        /// Pick a tool up, or put it down when it is already held. Every order tool behaves this
        /// way and the build tool does too (<see cref="DesignateDirector.ArmBuild"/>), so a player
        /// can always put a tool down the way they picked it up.
        /// </summary>
        static Action<DesignateDirector> Toggle(DesignateTool tool) =>
            d => d.Tool = d.Tool == tool ? DesignateTool.None : tool;

        static Func<DesignateDirector, bool> Holding(DesignateTool tool) => d => d.Tool == tool;

        /// <summary>
        /// How many of a category's tools do something yet.
        ///
        /// <para><b>Counted rather than listed</b> (owner, 2026-09-18: <i>"disable the top groups
        /// that have nothing to build … so we understand what we can build"</i>). The tool tier has
        /// drawn its dead entries disabled since it existed, but the category above it did not, so
        /// four of the seven — Production, Power, Security, Recreation — held nothing at all and
        /// were painted in full category colour beside Structure's four live tools. A category is
        /// dim when this is zero, and the count is what its tooltip says.</para>
        ///
        /// <para>A count and not a flag, because the tooltip wants "4 of 7 built" and a flag would
        /// have been a second walk of the same table to get it.</para>
        /// </summary>
        public static int LiveToolsIn(int index)
        {
            if (index < 0 || index >= Categories.Length) return 0;

            int live = 0;
            foreach (string tool in Categories[index].tools)
                if (TryGet(tool, out _)) live++;
            return live;
        }

        /// <summary>Whether a category holds anything the player can actually put down.</summary>
        public static bool HasLiveTool(int index) => LiveToolsIn(index) > 0;

        /// <summary>The live tool for a key, or false when the chip is one of the drawn-disabled ones.</summary>
        public static bool TryGet(string key, out PaletteTool tool)
        {
            for (int i = 0; i < Live.Length; i++)
            {
                if (Live[i].Key != key) continue;
                tool = Live[i];
                return true;
            }

            tool = default;
            return false;
        }

        /// <summary>Every key the palette can draw, for the registry test.</summary>
        public static IReadOnlyList<string> IconKeys
        {
            get
            {
                var keys = new List<string>(Pinned);
                foreach (var (key, tools) in Categories)
                {
                    keys.Add(key);
                    keys.AddRange(tools);
                }
                return keys;
            }
        }
    }
}
