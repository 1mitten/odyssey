#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Presentation.CameraRig;
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
            _rig.WorldRightClicked += OnWorldRightClicked;
        }

        void OnDestroy()
        {
            if (_rig == null) return;
            _rig.ToolDrag -= OnToolDrag;
            _rig.ToolDragging -= OnToolDragging;
            _rig.ToolDragCancelled -= OnToolDragCancelled;
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
            if (!Director.Dragging && !Director.Begin(anchor)) return;
            Director.DragTo(head);
        }

        void OnToolDragCancelled() => Director.Abandon();

        /// <summary>
        /// Right-click on the world: put the tool down, and nothing else.
        ///
        /// <para>A player holding a tool has one hand on the mouse and reaches for the nearest way
        /// to stop holding it, which is the button already under their finger — not a key across
        /// the keyboard. The rig has already decided this was a click and not an orbit
        /// (<c>PressGesture</c>), so swinging the camera around with a tool armed leaves the tool
        /// armed.</para>
        ///
        /// <para><b>With nothing armed this does nothing, on purpose.</b> That gesture is reserved
        /// for the forced-order context menu — right-click a site with a colonist selected and pick
        /// "build this now" (<c>docs/design/15-building.md</c> §8). Giving it a second meaning here
        /// would have to be taken back then, and a gesture that means two things depending on state
        /// nobody can see is the fault the Escape key already taught this project once.</para>
        /// </summary>
        void OnWorldRightClicked()
        {
            if (Director.Tool == DesignateTool.None) return;
            PutToolAway();
        }

        void Update()
        {
            Keyboard? keys = Keyboard.current;
            if (keys == null) return;
            HotkeyDirector hotkeys = Hotkeys;

            // While the settings panel is waiting for a key, every press belongs to the
            // rebind: arming a tool with the very key being offered to the slot would be two
            // things on one key, which is the fault this assembly's clash test exists for.
            if (hotkeys.Listening != null) return;

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
            // never moved a pixel, which raises no dragging frame at all.
            if (!Director.Dragging && !Director.Begin(anchor)) return;
            Director.DragTo(head);
            IReadOnlyList<CellRef> cells = Director.Commit();

            // Cancel is two intents, because there are two kinds of order and the player is holding
            // one rubber. A cell cannot carry both a designation and a building site, so exactly
            // one of the pair does anything and the other is refused with AlreadyInThatState —
            // which is the cheapest possible way to make one tool mean "whatever is here, stop".
            if (tool == DesignateTool.Cancel)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    world.Intents.Submit(new Intent(IntentKind.CancelDesignation, cells[i]));
                    world.Intents.Submit(new Intent(IntentKind.CancelBuilding, cells[i]));
                }

                return;
            }

            if (tool == DesignateTool.Build)
            {
                for (int i = 0; i < cells.Count; i++)
                    world.Intents.Submit(new Intent(
                        IntentKind.PlaceBuilding, cells[i], Director.Building, Director.Stuff));
                return;
            }

            int a = tool == DesignateTool.Mine ? (int)DesignationKind.Mine
                  : tool == DesignateTool.Fell ? (int)DesignationKind.Fell
                  : tool == DesignateTool.Deconstruct ? (int)DesignationKind.Deconstruct
                  : 0;

            for (int i = 0; i < cells.Count; i++)
                world.Intents.Submit(new Intent(IntentKind.Designate, cells[i], a));
        }
    }
}
