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

        /// <summary>
        /// Pull one of our own buildings down, for half of what it cost.
        ///
        /// <para>Not the opposite of <see cref="Cancel"/> and deliberately a separate tool: cancel
        /// removes an order that has not happened yet, and this orders work on something that is
        /// already standing. One rubber for both would mean a drag that went a row too far could
        /// demolish a colony.</para>
        /// </summary>
        Deconstruct = 5,
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
            _anchor = OnTheWorkingLayer(cell);
            _head = _anchor;
            _wide = false;
            Dragging = true;
            return true;
        }

        /// <summary>
        /// The layer this tool works on, when the pointer is not allowed to choose it.
        ///
        /// <para><b>Null for every tool but one, and the exception is the floor.</b> A click names
        /// a <em>surface</em> — the picker stops the ray at the first thing that occludes it — so
        /// a pointer can only ever name a cell one layer above something solid. That is right for
        /// a wall, which is put on the ground you clicked, and it cannot express the thing a floor
        /// is for: a cell of open air over a room, with nothing beneath it to aim at. Measured on
        /// 2026-09-17 with the real renderer — every one of the twelve clicks over a roofed room's
        /// interior named the floor of the room or missed entirely, so **the middle of a room could
        /// not be roofed at all**, only its walls capped.</para>
        ///
        /// <para>So a floor takes its column from the pointer and its layer from the slice: set the
        /// slice to the storey you are roofing and click inside the room (owner, 2026-09-17). It
        /// also answers the cursor question `17-floors-and-collapse.md` §9 left open — ordering a
        /// floor over a drop named the bottom of the drop, and now names the layer being worked.
        /// </para>
        ///
        /// <para>Set by whoever knows both the slice and the content, which is the presentation
        /// layer: this assembly cannot see <c>ConstructionContent</c> and has no business learning
        /// which buildings are slabs. What is here is only the substitution, so the geometry that
        /// results is still decided in the one class the fast tier can reach.</para>
        /// </summary>
        public int? WorkingLayer { get; set; }

        CellRef OnTheWorkingLayer(CellRef cell) =>
            WorkingLayer is int y ? new CellRef(cell.X, cell.Z, y) : cell;

        /// <summary>
        /// Move the far corner. Ignored unless a drag is running.
        ///
        /// <para><b>A build box does not widen until it is meant to.</b> The owner reported that
        /// building is "a tad sensitive and by accident you can build dual walls" (2026-09-17): a
        /// wall is dragged along one axis, the pointer wanders a single cell across it, and the
        /// rectangle quietly becomes two rows — two parallel walls, ordered and paid for, from a
        /// gesture that meant one. At this camera a cell is a small distance on screen and the
        /// board is drawn in perspective, so wandering one cell is not a mistake a player can
        /// simply stop making.</para>
        ///
        /// <para>The rule is hysteresis rather than a snap, so that a rectangle of wall is still
        /// one gesture: the box widens when the drag has gone <see cref="WidenAcross"/> cells clear
        /// across the run, and narrows again when it is back within <see cref="NarrowAcross"/>. Two
        /// thresholds, which is what stops a box flickering between one row and two while the
        /// pointer sits on the boundary — one threshold would do exactly that.</para>
        ///
        /// <para><b>It was loosened once, on a second report</b> (owner, 2026-09-17: still "too
        /// easy to create double walls"). Two faults, not one. The threshold was two cells, which
        /// is five metres of board and sounds like a lot until you drag twenty metres of wall at a
        /// camera looking down a slope. And the gate was <em>sticky</em>: it re-armed only on the
        /// anchor's exact row, so a single wander anywhere in a long drag latched the box wide for
        /// the rest of it, and the player would let go over a rectangle without ever seeing the
        /// moment it widened. Three to widen, back within one to narrow — so a trip recovers as
        /// soon as the pointer comes near the row again, rather than having to hit it exactly.</para>
        ///
        /// <para><b>Build only.</b> Mine, fell and cancel are area tools: a box one cell wider than
        /// intended marks one more cell to dig, which is a rounding error, while a wall one row
        /// wider than intended is a second wall built out of material the colony had to carry.
        /// The cost of the same slip is not the same, so the rule is not applied to the same
        /// tools.</para>
        /// </summary>
        public void DragTo(CellRef cell)
        {
            if (!Dragging) return;
            cell = OnTheWorkingLayer(cell);
            _head = cell;
            if (_tool != DesignateTool.Build) return;

            // Across the run, not along it: the gated axis is whichever one has travelled less,
            // decided afresh every frame, because a drag that starts east and turns north is one
            // gesture and the player never said which axis was the run.
            int across = Math.Min(Math.Abs(cell.X - _anchor.X), Math.Abs(cell.Z - _anchor.Z));
            if (across >= WidenAcross) _wide = true;
            else if (across <= NarrowAcross) _wide = false;
        }

        /// <summary>
        /// How many cells clear of the anchor's row a build drag must travel before the box widens
        /// into a rectangle.
        ///
        /// <para>Three — seven and a half metres of board. One cell is what a pointer picks up on
        /// its own from perspective, from the hand, and from the terrain under the cursor changing
        /// which cell a screen point names; two turned out to be inside what a long drag wanders
        /// by anyway. Three is a deliberate movement, and the cost of it being too coarse is only
        /// that an area of wall wants a slightly bigger gesture, while the cost of it being too
        /// fine is a wall nobody asked for and the wood to build it.</para>
        /// </summary>
        public const int WidenAcross = 3;

        /// <summary>
        /// How near the anchor's row the drag must come back for the gate to re-arm.
        ///
        /// <para>The second threshold, and the one that stops a box being latched wide by a wander
        /// it has long since recovered from. Not zero: requiring the exact row made a trip
        /// permanent in practice, because a pointer that has strayed three cells rarely returns to
        /// precisely the row it left. Not two either, or it would meet
        /// <see cref="WidenAcross"/> and the hysteresis would collapse back to one threshold and
        /// its flicker.</para>
        /// </summary>
        public const int NarrowAcross = 1;

        /// <summary>
        /// Whether this build drag has been widened on purpose. Per drag, cleared by
        /// <see cref="Begin"/> and by coming back within <see cref="NarrowAcross"/> of the row.
        /// </summary>
        bool _wide;

        /// <summary>Is the box being dragged an area rather than a run? For the tests, and for a readout.</summary>
        public bool Widened => _wide;

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
            _wide = false;
            _anchor = default;
            _head = default;
        }

        /// <summary>
        /// The box as it stands, for drawing it. False when nothing is being dragged.
        ///
        /// <para>Corners in either order, because a player drags in whatever direction suits them
        /// and a box drawn from the bottom right is the same box.</para>
        ///
        /// <para>This is where a build box is held to one row until it has earned its width — see
        /// <see cref="DragTo"/>. It is done here rather than in <see cref="DragTo"/> so that the
        /// head keeps the cell the pointer is actually over: the gate is about what the box
        /// <em>covers</em>, and a drag that has been narrowed must still be able to widen when the
        /// pointer goes on across, which it could not do if its own head had been rewritten.</para>
        /// </summary>
        public bool TryPreview(out CellRef min, out CellRef max)
        {
            min = default;
            max = default;
            if (!Dragging) return false;

            CellRef head = _head;
            if (_tool == DesignateTool.Build && !_wide)
                head = Math.Abs(head.X - _anchor.X) >= Math.Abs(head.Z - _anchor.Z)
                    ? new CellRef(head.X, _anchor.Z, head.Y)
                    : new CellRef(_anchor.X, head.Z, head.Y);

            min = new CellRef(Math.Min(_anchor.X, head.X), Math.Min(_anchor.Z, head.Z), _anchor.Y);
            max = new CellRef(Math.Max(_anchor.X, head.X), Math.Max(_anchor.Z, head.Z), _anchor.Y);
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
