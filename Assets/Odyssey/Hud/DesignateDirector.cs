#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The standing order the player is about to give. One tool at a time, or none.
    ///
    /// <para>Deliberately <b>not</b> <c>DesignationKind</c>, which lives in <c>Odyssey.Sim</c> and
    /// is invisible from here — this assembly sees only <c>Sim.Contracts</c> (ADR 0003), and that
    /// is the boundary working rather than an obstacle. A tool is an interface idea: <see
    /// cref="Cancel"/> is not a kind of designation at all, it is the absence of one, and the
    /// order the player is drawing is not a fact about the world until the simulation accepts it.
    /// The presenter on the Unity side turns a tool into an intent.</para>
    /// </summary>
    public enum DesignateTool
    {
        /// <summary>No tool. A click selects, which is what a click does when nothing is armed.</summary>
        None = 0,

        /// <summary>Dig it out.</summary>
        Mine = 1,

        /// <summary>Cut it down.</summary>
        Fell = 2,

        /// <summary>Take the order off, whatever it was.</summary>
        Cancel = 3,

        /// <summary>
        /// Put it here. See <see cref="DesignateDirector.Building"/> and
        /// <see cref="DesignateDirector.Stuff"/> for what, and what of.
        ///
        /// <para>The one tool that is not fully described by its own name. Mine and Fell are verbs
        /// applied to whatever is already in a cell, so a tool is all there is to say; a build
        /// order names a thing that is not there yet and has to carry which thing and which
        /// material with it.</para>
        /// </summary>
        Build = 4,
    }

    /// <summary>
    /// Drag a box over the world and give every cell in it the same order.
    ///
    /// <para><b>Why this exists at all.</b> Until now the colony could be watched and not played:
    /// nothing in the interface issued a single <c>Designate</c> intent, so every order in the game
    /// was seeded in code by a scenario. `ScenarioDef.Playtest` marks the trees near the start
    /// precisely so that a colony has something to do, and `CLAUDE.md` says it flips to `Bare` when
    /// this lands. This is the seam between an engine and a game.</para>
    ///
    /// <para><b>It decides, and it never acts.</b> Unity-free by construction, so the geometry and
    /// the state machine are covered by the fast tier; the presenter holds one of these and turns
    /// <see cref="Commit"/> into intents. That split is ADR 0003's and it is the reason a drag can
    /// be tested at all — the PlayMode input harness cannot yet deliver a synthetic mouse to the
    /// camera rig (`docs/plans/next-session-prompt.md` item 1), so anything that lived in the rig
    /// would ship untested.</para>
    ///
    /// <para><b>Nothing here validates.</b> Whether a cell may actually be mined is the
    /// simulation's to say — `DesignationGrid` refuses water, bedrock and the ground under a
    /// standing tree — and it says so on the intent. A drag over a hillside is expected to include
    /// cells that will be rejected, and the rejection is silent and correct. Filtering here would
    /// duplicate a rule that has already moved twice.</para>
    /// </summary>
    public sealed class DesignateDirector
    {
        CellRef _anchor;
        CellRef _head;

        /// <summary>What a drag would give the cells it covers. Setting it abandons any drag.</summary>
        public DesignateTool Tool
        {
            get => _tool;
            set
            {
                if (_tool == value) return;
                _tool = value;
                Abandon();
                ToolChanged?.Invoke(_tool);
            }
        }

        DesignateTool _tool = DesignateTool.None;

        /// <summary>
        /// What <see cref="DesignateTool.Build"/> would put down, as a <c>BuildingHandle</c> value.
        /// Meaningless under any other tool, and deliberately left alone when the tool changes: a
        /// player who puts the wall tool down and picks it up again wants the wall back.
        /// </summary>
        public int Building { get; private set; } = BuildingHandle.Wall;

        /// <summary>
        /// What to build it of, as a <c>StuffHandle</c> value. Wood by default because it is what a
        /// colony has first — felling is the job that works from the day it lands, and a stone wall
        /// needs a mine before it needs a builder.
        /// </summary>
        public int Stuff { get; private set; } = StuffHandle.Wood;

        /// <summary>Raised when the thing or the material changes, so the palette can mark it.</summary>
        public event Action<int, int>? BuildChoiceChanged;

        /// <summary>
        /// Arm the build tool on a thing, keeping the material already chosen. Pressing the same
        /// thing again puts the tool down, which is how every other tool behaves.
        /// </summary>
        public void ArmBuild(int building)
        {
            if (_tool == DesignateTool.Build && Building == building)
            {
                Tool = DesignateTool.None;
                return;
            }

            Building = building;
            BuildChoiceChanged?.Invoke(Building, Stuff);
            Tool = DesignateTool.Build;
        }

        /// <summary>
        /// Choose the material. It does <b>not</b> arm the tool: picking stone is a statement about
        /// the next wall, not an order to place one, and a material button that also armed a tool
        /// would leave the player holding something they only meant to configure.
        /// </summary>
        public void ChooseStuff(int stuff)
        {
            if (Stuff == stuff) return;
            Stuff = stuff;
            BuildChoiceChanged?.Invoke(Building, Stuff);
        }

        /// <summary>Is a box being drawn right now?</summary>
        public bool Dragging { get; private set; }

        /// <summary>Raised when the tool changes, so a readout does not have to poll.</summary>
        public event Action<DesignateTool>? ToolChanged;

        /// <summary>
        /// Start a box at this cell. Returns false when no tool is armed, which is the signal to
        /// the caller that the click belongs to selection instead.
        /// </summary>
        public bool Begin(CellRef cell)
        {
            if (_tool == DesignateTool.None) return false;
            _anchor = cell;
            _head = cell;
            Dragging = true;
            return true;
        }

        /// <summary>Move the far corner. Ignored unless a drag is running.</summary>
        public void DragTo(CellRef cell)
        {
            if (!Dragging) return;
            _head = cell;
        }

        /// <summary>
        /// Finish the box and hand back every cell it covers, or an empty span when there was no
        /// drag to finish.
        ///
        /// <para>A click with no movement is a one-cell box, not a special case — the anchor and
        /// the head are the same cell and the rectangle below is one cell wide. That is worth
        /// saying because "click to mark one, drag to mark many" is two code paths in most
        /// engines and one here.</para>
        /// </summary>
        public IReadOnlyList<CellRef> Commit()
        {
            if (!Dragging) return Array.Empty<CellRef>();

            var cells = new List<CellRef>();
            CoveredInto(cells);
            Abandon();
            return cells;
        }

        /// <summary>Throw the box away — the escape key, a layer change, a tool change.</summary>
        public void Abandon()
        {
            if (!Dragging) return;
            Dragging = false;
            _anchor = default;
            _head = default;
        }

        /// <summary>
        /// The box as it stands, for drawing it. False when nothing is being dragged.
        ///
        /// <para>Corners in either order, because a player drags in whatever direction suits them
        /// and a box drawn from the bottom right is the same box.</para>
        /// </summary>
        public bool TryPreview(out CellRef min, out CellRef max)
        {
            min = default;
            max = default;
            if (!Dragging) return false;

            min = new CellRef(Math.Min(_anchor.X, _head.X), Math.Min(_anchor.Z, _head.Z), _anchor.Y);
            max = new CellRef(Math.Max(_anchor.X, _head.X), Math.Max(_anchor.Z, _head.Z), _anchor.Y);
            return true;
        }

        /// <summary>How many cells the box covers, without building the list.</summary>
        public int PreviewCount =>
            TryPreview(out CellRef min, out CellRef max)
                ? (max.X - min.X + 1) * (max.Z - min.Z + 1)
                : 0;

        /// <summary>
        /// Every cell of the box, in a fixed order.
        ///
        /// <para>Row by row from the low corner, and the order is part of the contract rather than
        /// an accident: these become intents, intents are hashed into the state, and a set of
        /// orders that arrives in a different order on two machines is a divergence.</para>
        ///
        /// <para><b>The box is always on the anchor's layer</b>, and since 2026-09-16 that is a
        /// rule this class enforces rather than one it inherits. The picker used to be clipped to
        /// the active layer and now returns the nearest cell on any layer drawn solid, so a drag
        /// dragged up the face of an outcrop would otherwise climb a wall half way across. Where
        /// the drag *starts* is what it means: begin on the rock and the whole box is on the
        /// rock's layer.</para>
        /// </summary>
        void CoveredInto(List<CellRef> into)
        {
            if (!TryPreview(out CellRef min, out CellRef max)) return;
            for (int z = min.Z; z <= max.Z; z++)
            for (int x = min.X; x <= max.X; x++)
                into.Add(new CellRef(x, z, min.Y));
        }
    }
}
