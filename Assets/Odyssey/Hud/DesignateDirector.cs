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

        /// <summary>
        /// Paint soil for planting. Every cell of the box becomes a growing-zone cell, sown and
        /// re-sown for as long as the zone stands.
        ///
        /// <para>Like <see cref="Build"/>, it is not fully described by its own name: which crop
        /// goes in rides with it, as <see cref="DesignateDirector.Plant"/>. And like every order,
        /// whether a cell can actually be farmed is the simulation's answer, not this one — a box
        /// over a stream is refused cell by cell and silently.</para>
        /// </summary>
        GrowZone = 6,

        /// <summary>
        /// Set ground aside for things to be put down on. Every cell of the box joins one zone,
        /// and that zone accepts everything until the player narrows it.
        ///
        /// <para><b>One tool, and the zone it makes is a dumping zone</b> (owner, 2026-09-20:
        /// <i>"make a stockpile a default dumping zone, anything goes but you can then choose"</i>).
        /// The palette's Zones category has carried a second <c>ui.arch.tool.dumping</c> chip since
        /// before either existed; it is gone from the strip, because under that decision it would
        /// arm a tool that makes exactly what this one makes. The key stays in the registry for a
        /// real dumping zone later — one that also takes rubble, never re-stows out and sits at
        /// Last.</para>
        ///
        /// <para>Unlike <see cref="GrowZone"/> it carries no rider: what a store accepts is
        /// decided after it exists, in its own panel, because a filter is seven rows and a tool
        /// cannot hold one.</para>
        /// </summary>
        Stockpile = 7,

        /// <summary>
        /// Take power lines up (design 32 §2a). A tool of its own rather than a use of
        /// <see cref="Deconstruct"/>, because deconstruct takes one thing a cell — the building,
        /// then the floor — and a line runs through walls and under floors: folding it in would
        /// make rerouting a wire under a floor cost the floor. This takes the line and nothing else.
        /// </summary>
        RemoveConduit = 8,

        /// <summary>
        /// Pick a ripe berry bush (design 45 §6). A verb applied to what is already in a cell, like
        /// <see cref="Fell"/>; a bush that has been picked, or anything that is not a berry bush,
        /// is refused by the simulation cell by cell.
        /// </summary>
        Harvest = 9,

        /// <summary>
        /// Read the rock round an exposed face (design 62 §7). A verb applied to what is already
        /// in a cell, like <see cref="Mine"/>; a face nobody has cut, soft ground and bedrock are
        /// refused by the simulation cell by cell. Armed from the palette's Structure row, not the
        /// strip (<c>PaletteTools.Prospect</c>).
        /// </summary>
        Prospect = 10,
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

        /// <summary>
        /// What <see cref="DesignateTool.GrowZone"/> plants, as a <c>PlantHandle</c> value. Carrot
        /// because it is the only crop there is; the field stays chosen when the tool is put down,
        /// on the same reasoning as <see cref="Building"/> and <see cref="Stuff"/>.
        /// </summary>
        public int Plant { get; private set; } = PlantHandle.Carrot;

        /// <summary>
        /// The facing a rotatable thing will be placed at, 0–3: north, east, south, west. Kept
        /// while the thing is armed and reset when a different thing is picked up.
        /// </summary>
        public int Facing => _facing;

        int _facing;

        /// <summary>
        /// Whether the armed thing can be turned before it is placed — the one condition under
        /// which the rotate key belongs to the tool rather than to the slice (design 20 §5, and
        /// the owner's answer: R rotates the ghost, PageUp is always slice-up, and R raises the
        /// slice whenever nothing rotatable is armed).
        /// </summary>
        public bool RotatableArmed =>
            _tool == DesignateTool.Build && BuildShapes.CanRotate(Building);

        /// <summary>
        /// Whether the armed thing is placed one per click rather than dragged as a box. Anything
        /// that occupies more than one cell is: a run of walls is an order about every cell it
        /// covers, a bed is one thing that happens to be wide, and dragging it would be a gesture
        /// with nothing to say.
        /// </summary>
        public bool SinglePlacement =>
            _tool == DesignateTool.Build && BuildShapes.CellsOf(Building) > 1;

        /// <summary>Raised when the facing changes, so a preview can redraw without polling.</summary>
        public event Action<int>? FacingChanged;

        /// <summary>
        /// Turn the armed thing a quarter turn clockwise. Does nothing when the armed thing does
        /// not rotate, which is what lets the presenter offer the key unconditionally: the
        /// director decides whether the press was meant for it.
        /// </summary>
        public void Rotate()
        {
            if (!RotatableArmed) return;
            _facing = (_facing + 1) & 3;
            FacingChanged?.Invoke(_facing);
        }

        /// <summary>Raised when the thing or the material changes, so the palette can mark it.</summary>
        public event Action<int, int>? BuildChoiceChanged;

        /// <summary>
        /// Arm the build tool on a thing, keeping the material already chosen. Pressing the same
        /// thing again puts the tool down, which is how every other tool behaves.
        ///
        /// <para>Picking a <i>different</i> thing up resets <see cref="Facing"/> to north: the
        /// rotation belongs to the gesture, not to the player, and a bed picked up after a wall
        /// that was never rotated should not inherit a facing nothing showed.</para>
        /// </summary>
        public void ArmBuild(int building)
        {
            if (_tool == DesignateTool.Build && Building == building)
            {
                Tool = DesignateTool.None;
                return;
            }

            if (Building != building)
            {
                _facing = 0;
                FacingChanged?.Invoke(_facing);
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

        /// <summary>Raised when the crop changes, so the plant picker can mark the chosen one.</summary>
        public event Action<int>? PlantChanged;

        /// <summary>
        /// Choose the crop. It does <b>not</b> arm the tool, exactly as <see cref="ChooseStuff"/>
        /// does not: choosing what a zone grows is a statement about the next box, not an order to
        /// paint one.
        /// </summary>
        public void ChoosePlant(int plant)
        {
            if (Plant == plant) return;
            Plant = plant;
            PlantChanged?.Invoke(plant);
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
            Hover = null;

            // What a Mine drag marks is decided here, once, from the cell it was begun on, and
            // nowhere else (design 62 §4, P4): begun on rock it marks only rock, begun on soft
            // ground it marks everything. DragTo never asks again, so a drag that wanders across
            // grass and back on to rock is still the run it was when it began.
            _anchorTerrain = TerrainOf(_anchor);
            _run = _tool == DesignateTool.Mine ? DigOrMine.RunFor(_anchorTerrain) : DesignateRun.Everything;
            return true;
        }

        /// <summary>
        /// What the world is made of at a cell, as a <see cref="TerrainHandle"/> value, or -1 where
        /// it cannot say.
        ///
        /// <para>Set by the presenter from the render mirror, on <see cref="WorkingLayer"/>'s
        /// bargain: this assembly cannot see the world, and the one question it needs answered —
        /// is the cell a drag begins on rock? — is a number the presenter can hand over. Null (a
        /// test, a scene with no world) reads every cell as unknown, which is a drag that marks
        /// everything and a banner that says Mine: exactly what the tool did before it knew.</para>
        /// </summary>
        public Func<CellRef, int>? TerrainAt { get; set; }

        int TerrainOf(CellRef cell) => TerrainAt?.Invoke(cell) ?? -1;

        int _anchorTerrain = -1;
        int _run = DesignateRun.Everything;

        /// <summary>
        /// The running drag's answer to "what does it mark", as a <see cref="DesignateRun"/> value.
        /// Only a Mine drag begun on rock is ever <see cref="DesignateRun.RockOnly"/>.
        /// </summary>
        public int Run => _run;

        /// <summary>
        /// The run of the last committed gesture, written by <see cref="Commit"/> beside
        /// <see cref="LastAnchor"/> for the same reason: the box is thrown away before the
        /// presenter turns its cells into intents, and every one of them carries this.
        /// </summary>
        public int LastRun { get; private set; } = DesignateRun.Everything;

        /// <summary>
        /// The terrain the order is being given on right now, or -1: the start cell while a drag
        /// runs — the cell its run was decided from, so the word and what is marked agree for the
        /// whole drag — and the cell under the pointer otherwise. What the armed banner reads its
        /// word from (<see cref="BuildPaletteModel.ArmedWordKey"/>).
        /// </summary>
        public int PointerTerrain =>
            Dragging ? _anchorTerrain : Hover is CellRef hover ? TerrainOf(hover) : -1;

        /// <summary>
        /// Is the box waiting for a second click rather than for a button to be let go?
        ///
        /// <para><b>Click, move, click</b> (owner, 2026-09-17, and the default): one click anchors
        /// the run, the pointer then moves with nothing held, a second click places it, and a
        /// right-click throws it away. Holding a button down while steering a pointer precisely
        /// across a board drawn in perspective is the awkward part of the old gesture, and it is
        /// awkward in a way practice does not fix.</para>
        ///
        /// <para><b>Both ways in still work, and the hand decides which</b> — a press that travels
        /// past the widen threshold is a held drag and finishes when the button comes up, exactly
        /// as it always has. So a player reaching for the old gesture is never punished and has
        /// nothing to unlearn. That is why this is a state the box is in rather than a mode the
        /// whole director is in: the same box can be begun either way.</para>
        /// </summary>
        public bool AwaitingSecondClick { get; private set; }

        /// <summary>
        /// A click landed on the world with a tool armed: anchor the run, or finish it.
        ///
        /// <para>Returns the cells to order when this click completed a box, and empty when it
        /// only started one. One method rather than two so that the caller cannot get the order of
        /// the two halves wrong, which is the same argument <see cref="Commit"/> already makes.
        /// </para>
        /// </summary>
        public IReadOnlyList<CellRef> Click(CellRef cell)
        {
            if (_tool == DesignateTool.None) return Array.Empty<CellRef>();

            // **The press that opens a box has usually opened it already.** While the button is
            // down the rig reports the pointer every frame, so a box exists by the time the release
            // arrives even for a click that never moved. Both shapes therefore mean "anchor": no
            // box at all, or a box this very press created and has not been told to wait on.
            if (!Dragging && !Begin(cell)) return Array.Empty<CellRef>();

            // **One click places one cell and closes the run.** It used to place the cell and leave
            // the run open for a second click to extend, and that cost the owner the cursor: while
            // a run is open `TryPreview` succeeds, so the composition root draws the run's box and
            // never calls `DrawHoverGhost` again. A click therefore replaced the cursor that
            // follows the pointer with a box anchored to the last thing placed, for as long as the
            // player did not happen to click a second time — and the second click, when it came,
            // placed the whole line between (owner, 2026-09-17: *"the build cursor doesn't appear
            // for walls, floors etc - when I move around in the world - that cursor is no longer
            // there"*).
            //
            // A run is still a run: press, move, release is a drag and arrives through `Drag`. What
            // is gone is the click-move-click half, which asked the player to remember that a
            // gesture was open with nothing but a box to say so. "It should just place the ladder
            // with a click (no need to do many)" is the instruction this follows, and the cursor
            // being live at all times is the rest of it.
            DragTo(cell);
            AwaitingSecondClick = false;
            return Commit();
        }

        /// <summary>
        /// The anchoring click: <b>place the one cell under the pointer, and keep the run open.</b>
        ///
        /// <para><b>One click places one thing</b> (owner, 2026-09-17: *"it should just place the
        /// ladder with a click, no need to do many"*). Anchoring without placing made a single
        /// ladder cost two clicks, which is most of what anybody places — a ladder, a door, a
        /// bench are all one cell, and runs are the exception rather than the rule.</para>
        ///
        /// <para><b>And the run is still open</b>, so click-move-click is untouched: the second
        /// click places from this cell to wherever the pointer went. This cell is ordered twice and
        /// that costs nothing — the simulation answers <c>AlreadyInThatState</c> the second time,
        /// which is the whole reason that rejection exists.</para>
        ///
        /// <para><b>The wrinkle, said out loud rather than discovered:</b> a right-click after this
        /// throws away the <em>rest</em> of the run, and the cell this click placed stays placed.
        /// A click placed it; cancelling something that has not happened yet cannot unplace
        /// something that has. Cancel takes it off like any other order.</para>
        /// </summary>
        IReadOnlyList<CellRef> Anchored()
        {
            AwaitingSecondClick = true;
            _oneCell[0] = _anchor;
            return _oneCell;
        }

        readonly CellRef[] _oneCell = new CellRef[1];

        /// <summary>
        /// Right-click, or Escape: throw away the half-drawn box and say whether there was one.
        ///
        /// <para><b>The answer is what decides whether the tool is also put down.</b> False means
        /// nothing was pending, and the caller unwinds one step further. So the same button cancels
        /// a mis-anchored run first and disarms only when there is nothing left to cancel — one
        /// press, one step, which is the rule the Escape key already follows
        /// (`09-ui-and-input.md` §6).</para>
        /// </summary>
        public bool CancelPending()
        {
            if (!Dragging) return false;
            Abandon();
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

        /// <summary>
        /// Lift a cell to the working layer — <b>and only ever lift it</b>.
        ///
        /// <para>The working layer is a <b>floor under the order, not an override of it</b>, and the
        /// difference is the whole of the owner's third report that the slab tool "never wants to
        /// build" (2026-09-17). It overrode unconditionally, and the played meadow is terraced
        /// across five layers: a click on a wall standing one terrace above the slice was rewritten
        /// down to the slice's layer, which is inside the hillside, and refused. The tool worked
        /// only on the columns whose ground happened to sit at exactly the slice's height, which
        /// from a player's seat is never.</para>
        ///
        /// <para>Taking the higher of the two keeps the thing it was added for — a pointer cannot
        /// name open air over a room, so a slice raised above the surface still decides — and
        /// removes the thing it was never meant to do, which is drag an order down into the
        /// ground.</para>
        /// </summary>
        /// <remarks>
        /// <para><b>A growing zone lifts one, unconditionally.</b> The zone's cell is not the soil
        /// the pointer names but the air above it, because that is where the sower stands and where
        /// the crop grows — the simulation reads fertility from the cell below and refuses the cell
        /// that is walked into (<c>GrowingZones.SiteAllows</c>). The pointer can only ever name a
        /// surface, so the +1 is the smallest correction that puts the order where it means; and it
        /// is a lift, never a drop, which keeps the rule above intact when a slice is raised.</para>
        /// </remarks>
        CellRef OnTheWorkingLayer(CellRef cell)
        {
            if (_tool == DesignateTool.GrowZone)
                return new CellRef(cell.X, cell.Z, cell.Y + 1);
            return WorkingLayer is int y && y > cell.Y ? new CellRef(cell.X, cell.Z, y) : cell;
        }

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

            // A power run is a path, never an area (design 32 §10): a box of lines is a slab of
            // copper nobody asked for, so a line drag stays one row however far it wanders.
            if (BuildShapes.IsLineOnly(Building))
            {
                _wide = false;
                return;
            }

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
        ///
        /// <para><b>A single-placement thing returns its anchor alone</b> (see
        /// <see cref="SinglePlacement"/>): the facing — not the drag — says where the rest of it
        /// goes, and the simulation derives the second cell from the pair of them, so the intent
        /// is one cell and one facing rather than two cells that could drift apart.</para>
        /// </summary>
        public IReadOnlyList<CellRef> Commit()
        {
            if (!Dragging) return Array.Empty<CellRef>();

            var cells = new List<CellRef>();
            if (SinglePlacement)
            {
                cells.Add(_anchor);
            }
            else
            {
                CoveredInto(cells);
            }

            LastAnchor = _anchor;
            LastRun = _run;
            Abandon();
            return cells;
        }

        /// <summary>
        /// Where the pointer is resting with a tool armed and nothing pressed, put through the same
        /// layer rule an order gets, or null when there is nothing under it.
        ///
        /// <para><b>A hover is a one-cell drag that has not started.</b> It is held here rather than
        /// in the renderer so that the cell the cursor draws and the cell the order lands in are the
        /// same answer from the same object — which is the rule the whole class exists to keep, and
        /// the one that has been broken three times in this line of work.</para>
        ///
        /// <para>Null while a drag is running: the box is then the thing being shown, and a lone
        /// cursor cell hanging off the head of it would be a second answer to "where is this
        /// going".</para>
        /// </summary>
        public CellRef? Hover { get; private set; }

        /// <summary>The pointer is over this cell. Ignored mid-drag, for the reason above.</summary>
        public void HoverAt(CellRef cell)
        {
            if (_tool == DesignateTool.None || Dragging) { Hover = null; return; }
            Hover = OnTheWorkingLayer(cell);
        }

        /// <summary>Nothing under the pointer, or nothing that should be shown.</summary>
        public void HoverNowhere() => Hover = null;

        /// <summary>
        /// The cell the last committed gesture was <b>begun</b> on — where the player pressed.
        ///
        /// <para><b>Not a corner of the box</b>, and that is the whole reason it exists. The
        /// committed cells come back in grid order from <see cref="CoveredInto"/>, so the first of
        /// them is the box's minimum corner, and that is the <i>head</i> rather than the anchor
        /// whenever the drag ran up or left. The storage tool needs the cell that was pressed,
        /// because a drag begun inside a store extends <i>that</i> store — and answering that with
        /// a corner would make which zone you extend depend on which direction you happened to
        /// drag in.</para>
        ///
        /// <para>Written by <see cref="Commit"/> before the box is thrown away, because
        /// <see cref="Abandon"/> clears the anchor and a caller reading it afterwards would get a
        /// default <see cref="CellRef"/> — which is cell (0, 0, 0), a real cell, and therefore a
        /// bug that looks like "my drag joined a zone in the corner of the map".</para>
        /// </summary>
        public CellRef LastAnchor { get; private set; }

        /// <summary>Throw the box away — the escape key, a layer change, a tool change.</summary>
        public void Abandon()
        {
            if (!Dragging) return;
            Dragging = false;
            AwaitingSecondClick = false;
            _wide = false;
            _anchor = default;
            _head = default;
            _anchorTerrain = -1;
            _run = DesignateRun.Everything;
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

            // A single-placement thing's ghost is its own footprint, not the drag's: where the
            // pointer wandered is nothing to do with where a bed's far cell falls, and drawing a
            // rectangle because the pointer moved a pixel would be a preview of an order that
            // cannot exist.
            if (SinglePlacement)
            {
                int fx = _facing == 1 ? 1 : _facing == 3 ? -1 : 0;
                int fz = _facing == 0 ? 1 : _facing == 2 ? -1 : 0;
                min = new CellRef(Math.Min(_anchor.X, _anchor.X + fx), Math.Min(_anchor.Z, _anchor.Z + fz), _anchor.Y);
                max = new CellRef(Math.Max(_anchor.X, _anchor.X + fx), Math.Max(_anchor.Z, _anchor.Z + fz), _anchor.Y);
                return true;
            }

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
