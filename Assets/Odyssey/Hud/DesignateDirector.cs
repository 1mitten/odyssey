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
        /// orders that arrives in a different order on two machines is a divergence. The box is
        /// always on the anchor's layer — the picker cannot return a cell above the active one, so
        /// a drag cannot climb a wall half way across.</para>
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
