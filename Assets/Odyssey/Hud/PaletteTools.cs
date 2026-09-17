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
        /// The floor tool, under the catalogue's own key for it. The key says "roof" because a slab
        /// is both — it floors the layer it is in and roofs the one below — and keys are forever,
        /// so the label moved and the key did not (U29).
        /// </summary>
        public const string Floor = "ui.arch.tool.roof";

        /// <summary>
        /// Paving: a floor laid on ground that is already there (U42). Under <c>Floors</c> and
        /// never under <c>Structure</c> — with both tools saying "floor", the category is what
        /// tells a player which one they are holding (`18-paving.md` §7).
        /// </summary>
        public const string DeckPlate = "ui.arch.tool.deckplate";
        public const string Mine = "ui.arch.tool.mine";
        public const string Fell = "ui.arch.tool.fell";
        public const string Cancel = "ui.arch.tool.cancel";
        public const string Deconstruct = "ui.arch.tool.deconstruct";

        /// <summary>
        /// Categories in catalogue order, each with a few of its tools. Every icon key exists in
        /// the registry; a tool not in <see cref="Live"/> is drawn and disabled, so the shape of
        /// the game is visible before the thing behind a key exists.
        /// </summary>
        public static readonly (string key, string label, string[] tools)[] Categories =
        {
            ("ui.arch.category.structure", "Structure", new[] { Wall, "ui.arch.tool.door", "ui.arch.tool.stair", "ui.arch.tool.ladder", "ui.arch.tool.roof", "ui.arch.tool.reclaim" }),
            ("ui.arch.category.orders", "Orders", new[] { Mine, Fell, "ui.arch.tool.forbid", "ui.arch.tool.clearrubble" }),
            ("ui.arch.category.zones", "Zones", new[] { "ui.arch.tool.stockpile", "ui.arch.tool.growzone", "ui.arch.tool.dumping" }),
            ("ui.arch.category.production", "Production", new[] { "ui.arch.tool.fabricator", "ui.arch.tool.galley", "ui.arch.tool.reclaimer", "ui.arch.tool.bench" }),
            ("ui.arch.category.furniture", "Furniture", new[] { "ui.arch.tool.bunk", "ui.arch.tool.table", "ui.arch.tool.lamp", "ui.arch.tool.shelf" }),
            ("ui.arch.category.power", "Power", new[] { "ui.arch.tool.conduit", "ui.arch.tool.battery", "ui.arch.tool.generator", "ui.arch.tool.reactor" }),
            ("ui.arch.category.security", "Security", new[] { "ui.arch.tool.turret", "ui.arch.tool.trap", "ui.arch.tool.barricade" }),
            ("ui.arch.category.salvage", "Salvage", new[] { "ui.arch.tool.salvage", "ui.arch.tool.reclaim" }),
            ("ui.arch.category.floors", "Floors", new[] { DeckPlate, "ui.arch.tool.grating", "ui.arch.tool.tile" }),
            ("ui.arch.category.recreation", "Recreation", new[] { "ui.arch.tool.gamestable", "ui.arch.tool.viewscreen", "ui.arch.tool.planter" }),
        };

        /// <summary>
        /// Tools that belong to no category and are always on show, under everything else.
        ///
        /// <para><b>Cancel is not a kind of thing to build.</b> Every other chip in this palette
        /// answers "what would you like to put down"; this one answers "stop", about whatever is
        /// already on the board, and it is as relevant to a wall as to a mine mark. Filing it under
        /// Orders was tidy and wrong in practice (owner, 2026-09-17: *"would be a good idea to be
        /// able to access the cancel button on the build sub menu"*): the moment a player wants it
        /// is while they are holding <em>another</em> tool, which is exactly when the category row
        /// is showing something else and reaching for it costs two clicks and a hunt.</para>
        ///
        /// <para><b>Deconstruct joined it</b> (owner, 2026-09-17, having failed to get it to work
        /// from Orders: <i>"could you put deconstruct next to cancel as a button so we can at least
        /// deconstruct this way"</i>). It belongs by the same argument, which is worth stating
        /// because it was filed under Orders on exactly the reasoning that put Cancel there: both
        /// are about <em>what is already on the board</em> rather than about what to put down next,
        /// and both are wanted at the moment a player is holding something else. Two chips is also
        /// the sensible limit — a pinned row that grows is a second palette.</para>
        ///
        /// <para>They are in no category at all rather than pinned <i>and</i> listed, because the
        /// same chip appearing twice in one open panel is a question the player has to stop and
        /// answer: whether the two do the same thing.</para>
        /// </summary>
        public static readonly string[] Pinned = { Cancel, Deconstruct };

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
            new PaletteTool(Floor,
                d => d.ArmBuild(BuildingHandle.Floor),
                d => d.Tool == DesignateTool.Build && d.Building == BuildingHandle.Floor,
                wantsMaterial: true),
            new PaletteTool(DeckPlate,
                d => d.ArmBuild(BuildingHandle.DeckPlate),
                d => d.Tool == DesignateTool.Build && d.Building == BuildingHandle.DeckPlate,
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
                foreach (var (key, _, tools) in Categories)
                {
                    keys.Add(key);
                    keys.AddRange(tools);
                }
                return keys;
            }
        }
    }
}
