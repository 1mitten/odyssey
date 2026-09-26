#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Presentation.CameraRig;
using Odyssey.Sim;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Odyssey.Presentation.Bootstrap
{
    /// <summary>
    /// The player gives an order: arm a tool, drag a box over the world, and every cell in it gets
    /// a standing order.
    ///
    /// <para><b>This is the seam between an engine and a game.</b> Until it existed nothing in the
    /// interface issued a single <c>Designate</c> intent — every standing order in the build was
    /// seeded in code by a scenario, and <c>ScenarioDef.Playtest</c> marks the trees near the start
    /// only so that a colony has something to do at all. A player could watch a colony and never
    /// tell it anything.</para>
    ///
    /// <para><b>It decides nothing.</b> The geometry and the state machine are
    /// <see cref="DesignateDirector"/>'s, Unity-free and covered by the fast tier; the cell under a
    /// screen point is the rig's, because the rig holds the render mirror and the picker. What is
    /// left here is the wiring: which key arms which tool, and turning cells into intents. That is
    /// the ADR 0003 split, and it is why the only untested part of designation is the part that
    /// cannot be tested without a mouse.</para>
    ///
    /// <para><b>Keys, for now.</b> The Build palette is on the bottom bar and its tools are drawn
    /// disabled, so arming from there is the interface line's to wire and wants icons that do not
    /// exist yet. A key is the cheapest thing that makes the game playable today, and the director
    /// it drives is the same one a palette button would drive.</para>
    /// </summary>
    public sealed class DesignatePresenter : MonoBehaviour
    {
        OdysseyBootstrap? _bootstrap;
        SliceCameraRig? _rig;

        DesignateDirector? _fallback;
        HotkeyDirector? _hotkeysFallback;

        /// <summary>
        /// What the player is about to order.
        ///
        /// <para><b>The colony's one, not this presenter's.</b> It used to construct its own, which
        /// was correct while a key was the only way to arm a tool; the Build palette is the second
        /// way, and two directors would be two answers to "what is armed" — a palette button lit
        /// for a tool the world did not have. It lives on <see cref="HudDirectors"/> now and this
        /// borrows it. The fallback instance is for a scene with no HUD, which the screenshot
        /// harnesses build.</para>
        ///
        /// <para>Resolved every time rather than cached: the bootstrap builds its directors during
        /// its own startup, so a presenter that cached on the first frame would keep a throwaway
        /// for the life of the session and the palette would drive a director nobody reads.</para>
        /// </summary>
        public DesignateDirector Director =>
            _bootstrap?.Directors?.Designate ?? (_fallback ??= new DesignateDirector());

        /// <summary>
        /// The binding map, borrowed the same way the designate director is: the colony's
        /// when there is one, a defaults-only one when there is not, so the keys still arm
        /// tools in a scene built without a HUD.
        /// </summary>
        HotkeyDirector Hotkeys =>
            _bootstrap?.Directors?.Hotkeys ?? (_hotkeysFallback ??= new HotkeyDirector());

        SelectionPresenter? _selection;

        void Awake()
        {
            _bootstrap = GetComponent<OdysseyBootstrap>();
            _rig = _bootstrap != null ? _bootstrap.cameraRig : null;
            if (_rig == null) return;

            // The rig asks this before it decides whether a press belonged to selection.
            _rig.WorldToolArmed = () => Director.Tool != DesignateTool.None;
            _rig.ToolDrag += OnToolDrag;
            _rig.ToolDragging += OnToolDragging;
            _rig.ToolDragCancelled += OnToolDragCancelled;
            _rig.ToolHover += OnToolHover;
            _rig.ToolHoverLost += OnToolHoverLost;
            _rig.ToolClick += OnToolClick;
            _rig.WorldRightClicked += OnWorldRightClicked;
        }

        void OnDestroy()
        {
            if (_rig == null) return;
            _rig.ToolDrag -= OnToolDrag;
            _rig.ToolDragging -= OnToolDragging;
            _rig.ToolDragCancelled -= OnToolDragCancelled;
            _rig.ToolHover -= OnToolHover;
            _rig.ToolHoverLost -= OnToolHoverLost;
            _rig.ToolClick -= OnToolClick;
            _rig.WorldRightClicked -= OnWorldRightClicked;
            if (_rig.WorldToolArmed != null) _rig.WorldToolArmed = null;
        }

        /// <summary>
        /// The box is being drawn: keep the director's own state machine in step with the pointer
        /// so that <c>DesignateDirector.TryPreview</c> can be drawn.
        ///
        /// <para><b>The director is driven live rather than asked for a rectangle</b>, for the
        /// reason <see cref="OnToolDrag"/> already gives: one implementation of "which cells does
        /// this box cover", and it is the one the fast tier tests. It also means the preview cannot
        /// disagree with the order that follows, because they are literally the same object's
        /// answer a frame apart.</para>
        /// </summary>
        void OnToolDragging(CellRef anchor, CellRef head)
        {
            TellTheDirectorWhichLayerItIsWorkingOn();
            if (!Director.Dragging && !Director.Begin(anchor)) return;
            Director.DragTo(head);
        }

        /// <summary>
        /// A floor is ordered on the slice's layer; everything else is ordered where the pointer
        /// says.
        ///
        /// <para><b>Decided here because this is the only place that can see both halves.</b>
        /// <c>DesignateDirector</c> is Unity-free and references only the contracts, so it cannot
        /// ask <c>ConstructionContent</c> whether the armed building is a slab; the rig owns the
        /// active layer and knows nothing about buildings. So the presenter answers the one
        /// question and hands over a number, and the substitution itself stays in the class the
        /// fast tier can test.</para>
        ///
        /// <para>Why a floor is the exception is <see cref="DesignateDirector.WorkingLayer"/>: a
        /// pointer can only name a surface, and the cell a floor wants is open air over a room.
        /// </para>
        /// </summary>
        void TellTheDirectorWhichLayerItIsWorkingOn()
        {
            DesignateDirector director = Director;
            BuildingDef what = ConstructionContent.BuildingAt(director.Building);

            // **Structure only, and a covering is explicitly not it.** A slab takes its layer from
            // the slice because a pointer cannot name open air. Paving is always laid on a surface,
            // which is the one thing a pointer *can* name, so forcing it onto the slice layer would
            // break it the moment the player scrolled a layer up (U42, `18-paving.md` §4).
            bool structure = director.Tool == DesignateTool.Build && what.slab && !what.covering;
            director.WorkingLayer = structure && _rig != null ? _rig.ActiveLayer : (int?)null;

            // And what the ground is made of, the one other thing the director cannot see: a Mine
            // drag's run is decided from its start cell's terrain (design 62 §4), and the banner
            // names the order Dig or Mine by it. The render mirror is the terrain the player is
            // looking at — an undiscovered seam reads as the rock it is drawn as, and both are
            // rock-like, so the two cannot disagree about a drag.
            director.TerrainAt = _terrainAt ??= TerrainAt;
        }

        System.Func<CellRef, int>? _terrainAt;

        /// <summary>The terrain drawn at a cell, as a <c>TerrainHandle</c> value, or -1 off the board or with no world.</summary>
        int TerrainAt(CellRef cell)
        {
            Odyssey.Presentation.World.WorldRenderModel? model = _bootstrap != null ? _bootstrap.Model : null;
            if (model == null || !model.Size.Contains(cell)) return -1;
            return model.Terrain(model.Size.Index(cell));
        }

        void OnToolDragCancelled() => Director.Abandon();

        /// <summary>
        /// The pointer moved over a cell with a tool armed and no button down.
        ///
        /// <para><b>A hover is a one-cell drag that has not started</b>, and it is resolved through
        /// the director for exactly that reason: the layer rule, the lift and the cell the order
        /// will land in are all decided in one place, so the ghost the player sees and the site they
        /// get cannot come to disagree. That disagreement is the fault this line of work has now hit
        /// three times (`19-build-cursor.md` §6).</para>
        /// </summary>
        void OnToolHover(CellRef cell)
        {
            TellTheDirectorWhichLayerItIsWorkingOn();

            // A box anchored by a click follows the pointer with nothing held, which is the whole
            // point of the gesture: the hover IS the drag (owner, 2026-09-17).
            if (Director.AwaitingSecondClick) Director.DragTo(cell);
            else Director.HoverAt(cell);
        }

        void OnToolHoverLost() => Director.HoverNowhere();

        /// <summary>
        /// A click on the world with a tool armed: anchor a run, or finish the one in hand.
        ///
        /// <para><b>Click, move, click</b> — the default gesture. The director decides which of the
        /// two this click is, because it is the thing that knows whether a box is already open, and
        /// it hands back the cells when the click completed one. Holding and dragging still works
        /// and still arrives through <see cref="OnToolDrag"/>: the two gestures share a box and
        /// differ only in what ends it.</para>
        /// </summary>
        void OnToolClick(CellRef cell)
        {
            var world = _bootstrap?.World;
            if (world == null) return;

            TellTheDirectorWhichLayerItIsWorkingOn();
            Submit(world, Director.Tool, Director.Click(cell));
        }

        /// <summary>
        /// Right-click on the world: put the tool down, and nothing else.
        ///
        /// <para>A player holding a tool has one hand on the mouse and reaches for the nearest way
        /// to stop holding it, which is the button already under their finger — not a key across
        /// the keyboard. The rig has already decided this was a click and not an orbit
        /// (<c>PressGesture</c>), so swinging the camera around with a tool armed leaves the tool
        /// armed.</para>
        ///
        /// <para><b>With nothing armed it is an order</b> (design 33 §2f), handed to
        /// <see cref="SelectionPresenter.Order"/>, which owns the hit-test that says who is under
        /// the pointer. It was deliberately inert until the draft, held for the forced-order menu
        /// of <c>docs/design/15-building.md</c> §8 — and an order to a drafted colonist is that
        /// same act, the player overruling the scan for a colonist, so the reservation is being
        /// spent on what it was kept for. This presenter keeps first refusal: a tool in hand still
        /// only puts the tool down, so the gesture never means two things at once.</para>
        /// </summary>
        void OnWorldRightClicked(CellRef? cell, Ray ray)
        {
            if (Director.Tool == DesignateTool.None)
            {
                if (_selection == null) _selection = GetComponent<SelectionPresenter>();
                _selection?.Order(cell, ray);
                return;
            }

            // One press, one step of unwinding (owner, 2026-09-17). A half-drawn run is thrown
            // away and the tool stays in hand, so a misjudged anchor costs one click rather than a
            // trip back to the palette; only when there is nothing left to cancel does the same
            // button put the tool down. It is the order the Escape key already follows.
            if (Director.CancelPending()) return;
            PutToolAway();
        }

        void Update()
        {
            Keyboard? keys = Keyboard.current;
            if (keys == null) return;
            HotkeyDirector hotkeys = Hotkeys;

            // While the settings panel is waiting for a key, every press belongs to the
            // rebind: arming a tool with the very key being offered to the slot would be two
            // things on one key, which is the fault this assembly's clash test exists for. The
            // same holds while a text field has the keyboard — the C of a colony's name is not
            // the Cancel tool — and the director answers both in one question.
            if (!hotkeys.GameKeysLive) return;

            // Mine, cut, cancel. Pressing the armed tool's own key again disarms it, so a
            // player who picked one up can always put it down the way they picked it up. The
            // letters themselves are the binding map's business; these lines say only what
            // the key does.
            //
            // **Escape is deliberately not read here any more.** It used to be, and it was the
            // only consumer in the build; the settings panel made it the second, and two
            // components reading one key would have disarmed the tool and opened the panel on
            // the same keystroke. The unwind order is one rule (`09-ui-and-input.md` §6) so it lives
            // in one place — `SettingsDirector.Escape`, decided in the fast tier — and
            // `SettingsPresenter` calls `PutToolAway` when the answer is to disarm.
            if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.ToolMine)) Arm(DesignateTool.Mine);
            if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.ToolFell)) Arm(DesignateTool.Fell);
            if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.ToolCancel)) Arm(DesignateTool.Cancel);
            if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.ToolGrowZone)) Arm(DesignateTool.GrowZone);

            // The rotate key is the slice-up key, claimed by the tool while a rotatable thing is
            // armed (owner's answer, design 20 §5): R turns the bed's ghost, PageUp is always
            // slice-up, and R raises the slice whenever nothing rotatable is armed. The rig makes
            // the same check on its side of the bargain, so the key is one or the other in any
            // given frame and never both. There is no HotkeyAction for this on purpose — a second
            // action on R is a clash the binding map refuses, and a context rule stated in two
            // places is a rule one of them forgets; this claim is the one context this game has.
            if (Director.RotatableArmed && keys.WasPressedThisFrame(hotkeys, HotkeyAction.SliceUp))
                Director.Rotate();
        }

        /// <summary>Whether a tool is armed, for whoever is deciding what Escape means.</summary>
        public bool ToolArmed => Director.Tool != DesignateTool.None;

        /// <summary>Put the armed tool down. The Escape half of this presenter, called by whoever owns that key.</summary>
        public void PutToolAway() => Director.Tool = DesignateTool.None;

        void Arm(DesignateTool tool) =>
            Director.Tool = Director.Tool == tool ? DesignateTool.None : tool;

        /// <summary>
        /// A completed gesture becomes one intent per cell.
        ///
        /// <para>The director is driven through its own state machine rather than being asked for
        /// a rectangle, so the one implementation of "which cells does this box cover" is the one
        /// the fast tier tests. Begin and commit in the same breath because the rig only tells us
        /// about a gesture once it is finished — a live preview wants the intermediate cells and
        /// is the next thing to build, not this one.</para>
        ///
        /// <para><b>Nothing is filtered.</b> Whether a cell may actually be mined is the
        /// simulation's to say — <c>DesignationGrid</c> refuses water, bedrock and the ground under
        /// a standing tree — and a box dragged over a hillside is expected to contain cells that
        /// are rejected. The rejection is silent and correct, and duplicating the rule here would
        /// be duplicating a rule that has already moved twice.</para>
        /// </summary>
        void OnToolDrag(CellRef anchor, CellRef head)
        {
            var world = _bootstrap?.World;
            if (world == null) return;

            DesignateTool tool = Director.Tool;
            // Begun already by the live preview in the ordinary case; begun here for a press that
            // never moved a pixel, which raises no dragging frame at all — and that press is
            // exactly the one that has had no chance to be told its layer yet.
            TellTheDirectorWhichLayerItIsWorkingOn();
            if (!Director.Dragging && !Director.Begin(anchor)) return;
            Director.DragTo(head);
            Submit(world, tool, Director.Commit());
        }

        /// <summary>
        /// Turn a committed box into intents.
        ///
        /// <para>Shared by the two gestures — held-and-dragged, and click-move-click — so that the
        /// way a run was drawn cannot change what it orders. They differ only in what ends the box;
        /// everything after that is one path (owner, 2026-09-17).</para>
        /// </summary>
        void Submit(SimWorld world, DesignateTool tool, IReadOnlyList<CellRef> cells)
        {
            if (cells.Count == 0) return;

            // Cancel is three intents, because there are three kinds of order and the player is
            // holding one rubber. A cell cannot carry a designation, a building site and a zone
            // membership all at once, so at most one of the three does anything and the others
            // are refused with AlreadyInThatState — which is the cheapest possible way to make
            // one tool mean "whatever is here, stop". The zone intent is the newest of the three
            // (U46); before it existed a cancel drag over a field would have left the field
            // standing while the player believed they had cleared it.
            if (tool == DesignateTool.Cancel)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    world.Intents.Submit(new Intent(IntentKind.CancelDesignation, cells[i]));
                    world.Intents.Submit(new Intent(IntentKind.CancelBuilding, cells[i]));
                    world.Intents.Submit(new Intent(IntentKind.CancelZone, cells[i]));
                    // The fourth kind of order, and the newest (S1). A cell cannot carry a
                    // designation, a site, a growing zone and a store all at once, so at most one
                    // of the four does anything and the rest answer AlreadyInThatState — which is
                    // still the cheapest way to make one tool mean "whatever is here, stop".
                    world.Intents.Submit(new Intent(IntentKind.CancelStorage, cells[i]));
                }

                return;
            }

            if (tool == DesignateTool.Build)
            {
                // **One run, one layer.** The lift is per cell and conditional, so a box dragged
                // over a walled room used to resolve to two layers at once — the ring lifting on to
                // the storey above because it sits over walls, the middle staying put over open air
                // and being refused — and the player got a deck with a hole in it and no warning
                // that anything had happened on two floors. ConstructionGrid.RunLayerFor owns that
                // answer; the ghosts ask it too, so the preview and the order cannot disagree.
                ConstructionGrid? sites = _bootstrap?.Colony?.Construction;
                int y = sites?.RunLayerFor(cells, Director.Building) ?? cells[0].Y;

                for (int i = 0; i < cells.Count; i++)
                    world.Intents.Submit(new Intent(
                        IntentKind.PlaceBuilding,
                        new CellRef(cells[i].X, cells[i].Z, y),
                        Director.Building, Director.Stuff, Director.Facing));
                return;
            }

            // The zone order's A is one-based (0 stays "unset"), so the chosen crop rides as
            // PlantHandle + 1 — the same convention the simulation's own handler states.
            if (tool == DesignateTool.GrowZone)
            {
                int plant = Director.Plant + 1;
                for (int i = 0; i < cells.Count; i++)
                    world.Intents.Submit(new Intent(IntentKind.DesignateZone, cells[i], plant));
                return;
            }

            // A store carries the **anchor** of its own drag, which is how a rectangle becomes one
            // zone and how "extend the one I started in" is said without minting an id: the
            // simulation resolves the anchor against the zones it has, so a drag begun inside a
            // store extends that store and one begun outside founds a new one.
            //
            // `Director.LastAnchor` and not `cells[0]`: the committed cells come back in grid
            // order, so the first of them is the box's minimum corner and is the head rather than
            // the anchor whenever the drag ran up or left.
            if (tool == DesignateTool.Stockpile)
            {
                int anchor = world.Size.Index(Director.LastAnchor);
                for (int i = 0; i < cells.Count; i++)
                    world.Intents.Submit(new Intent(
                        IntentKind.DesignateStorage, cells[i], anchor, Odyssey.Sim.Storage.StoragePreset.Everything));
                return;
            }

            // Taking lines up (design 32 §2a): its own intent, never a designation, because a cell
            // with a line in it very often has a wall or a floor the deconstruct order would name
            // instead. Cells with no line in them are refused, silently and correctly.
            if (tool == DesignateTool.RemoveConduit)
            {
                for (int i = 0; i < cells.Count; i++)
                    world.Intents.Submit(new Intent(IntentKind.RemoveConduit, cells[i]));
                return;
            }

            int a = tool == DesignateTool.Mine ? (int)DesignationKind.Mine
                  : tool == DesignateTool.Fell ? (int)DesignationKind.Fell
                  : tool == DesignateTool.Harvest ? (int)DesignationKind.Harvest
                  : tool == DesignateTool.Prospect ? (int)DesignationKind.Prospect
                  : tool == DesignateTool.Deconstruct ? (int)DesignationKind.Deconstruct
                  : 0;

            // The run, decided once from the start cell when the gesture began (design 62 §4, P4)
            // and carried on every intent of it: a Mine drag begun on rock marks only rock. Not
            // decided here, and never per cell — the director answered it at Begin.
            int run = tool == DesignateTool.Mine ? Director.LastRun : DesignateRun.Everything;

            for (int i = 0; i < cells.Count; i++)
                world.Intents.Submit(new Intent(IntentKind.Designate, cells[i], a, run));
        }
    }
}
