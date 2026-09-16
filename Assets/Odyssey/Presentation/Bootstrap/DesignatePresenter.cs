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

        /// <summary>
        /// What the player is about to order. Public so a palette button can set it once one
        /// exists, and so a readout can show it.
        /// </summary>
        public DesignateDirector Director { get; } = new DesignateDirector();

        void Awake()
        {
            _bootstrap = GetComponent<OdysseyBootstrap>();
            _rig = _bootstrap != null ? _bootstrap.cameraRig : null;
            if (_rig == null) return;

            // The rig asks this before it decides whether a press belonged to selection.
            _rig.WorldToolArmed = () => Director.Tool != DesignateTool.None;
            _rig.ToolDrag += OnToolDrag;
        }

        void OnDestroy()
        {
            if (_rig == null) return;
            _rig.ToolDrag -= OnToolDrag;
            if (_rig.WorldToolArmed != null) _rig.WorldToolArmed = null;
        }

        void Update()
        {
            Keyboard? keys = Keyboard.current;
            if (keys == null) return;

            // M mine, C cut, X cancel. Pressing the armed tool's own key again disarms it, so a
            // player who picked one up can always put it down the way they picked it up.
            //
            // **Escape is deliberately not read here any more.** It used to be, and it was the
            // only consumer in the build; the settings panel made it the second, and two
            // components reading one key would have disarmed the tool and opened the panel on the
            // same keystroke. The unwind order is one rule (`09-ui-and-input.md` §6) so it lives
            // in one place — `SettingsDirector.Escape`, decided in the fast tier — and
            // `SettingsPresenter` calls `PutToolAway` when the answer is to disarm.
            if (keys.mKey.wasPressedThisFrame) Arm(DesignateTool.Mine);
            if (keys.cKey.wasPressedThisFrame) Arm(DesignateTool.Fell);
            if (keys.xKey.wasPressedThisFrame) Arm(DesignateTool.Cancel);
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

            if (!Director.Begin(anchor)) return;
            Director.DragTo(head);
            IReadOnlyList<CellRef> cells = Director.Commit();

            IntentKind kind = Director.Tool == DesignateTool.Cancel
                ? IntentKind.CancelDesignation
                : IntentKind.Designate;
            int a = Director.Tool == DesignateTool.Mine ? (int)DesignationKind.Mine
                  : Director.Tool == DesignateTool.Fell ? (int)DesignationKind.Fell
                  : 0;

            for (int i = 0; i < cells.Count; i++)
                world.Intents.Submit(new Intent(kind, cells[i], a));
        }
    }
}
