#nullable enable
using Odyssey.Hud;
using Odyssey.Presentation.Ui;
using UnityEngine;

namespace Odyssey.Presentation.Bootstrap
{
    /// <summary>What the pointer is wearing.</summary>
    public enum CursorLook
    {
        /// <summary>The plain arrow: the world, with nothing in hand.</summary>
        Default = 0,

        /// <summary>
        /// The pointer is over the HUD. The same arrow as <see cref="Default"/> today.
        ///
        /// <para>A separate state rather than an absence, because the question it answers — does
        /// this click reach the world? — is one the HUD already asks in three other places, and
        /// this is the seam a distinct panel cursor would go on. Keeping it costs nothing; folding
        /// it into <see cref="Default"/> would have to be undone to get it back.</para>
        /// </summary>
        Interface = 1,

        /// <summary>An order is armed and the pointer is over the world: the crosshair, in that
        /// order's hue.</summary>
        Tool = 2,
    }

    /// <summary>
    /// <b>The one place the mouse cursor is set.</b>
    ///
    /// <para><b>Why it exists.</b> <c>Hud.uss</c> carried nineteen <c>cursor:</c> declarations and
    /// every one of them was inert: keyword cursors are an Editor-only feature of UI Toolkit, so in
    /// the runtime panel the game actually has they set nothing and logged
    /// <i>"Runtime cursors other than the default cursor need to be defined using a texture"</i>
    /// once per repaint. The game had run the bare OS arrow since the HUD was written. See
    /// <c>docs/design/28-pointer-cursor.md</c> §1 for why they were deleted rather than converted
    /// to nineteen texture URLs.</para>
    ///
    /// <para><b>The decision is a pure function</b> (<see cref="Decide"/>) and the setting is not,
    /// which is what lets the state machine be tested at all: the PlayMode harness still cannot
    /// deliver a synthetic mouse, so nothing in this project can move a pointer and look at the
    /// result. The arithmetic is therefore kept somewhere a test can reach it, the same split
    /// <c>PressGesture</c> made for the same reason.</para>
    ///
    /// <para><b>Set on change, never per frame.</b> <c>Cursor.SetCursor</c> talks to the platform;
    /// calling it every frame with the same texture is a per-frame platform call for nothing.</para>
    /// </summary>
    public sealed class CursorDirector
    {
        CursorLook _look = CursorLook.Default;
        DesignateTool _tool = DesignateTool.None;
        bool _applied;

        /// <summary>What the pointer is wearing now.</summary>
        public CursorLook Look => _look;

        /// <summary>The tool the crosshair is coloured for, or <c>None</c> when it is the arrow.</summary>
        public DesignateTool Tool => _tool;

        /// <summary>
        /// What the pointer should be, given where it is and what is in hand.
        ///
        /// <para><b>Over the HUD, an armed tool reverts to the arrow</b>, and that is the one place
        /// the cursor carries something a player cannot get anywhere else: with a tool in hand over
        /// a panel the world ghost is suppressed (<c>ToolHoverLost</c>) and until now nothing said
        /// why. The arrow coming back is that sentence.</para>
        /// </summary>
        public static CursorLook Decide(bool pointerOverInterface, DesignateTool tool) =>
            pointerOverInterface ? CursorLook.Interface
            : tool != DesignateTool.None ? CursorLook.Tool
            : CursorLook.Default;

        /// <summary>The texture a look wears, for a given tool.</summary>
        public static Texture2D TextureFor(CursorLook look, DesignateTool tool) =>
            look == CursorLook.Tool ? CursorArt.Crosshair(tool) : CursorArt.Arrow;

        /// <summary>Where that texture's point is, in pixels from its top-left.</summary>
        public static Vector2 HotspotFor(CursorLook look) =>
            look == CursorLook.Tool ? CursorArt.CrosshairHotspot : CursorArt.ArrowHotspot;

        /// <summary>
        /// Called once a frame with the two facts that decide the pointer. Does nothing at all
        /// when neither has changed.
        /// </summary>
        public void Update(bool pointerOverInterface, DesignateTool tool)
        {
            CursorLook look = Decide(pointerOverInterface, tool);

            // Only the crosshair is coloured, so a tool change with the arrow showing is not a
            // change to anything visible and must not reach the platform.
            DesignateTool wanted = look == CursorLook.Tool ? tool : DesignateTool.None;
            if (_applied && look == _look && wanted == _tool) return;

            _look = look;
            _tool = wanted;
            _applied = true;
            Apply();
        }

        /// <summary>
        /// Hand the pointer back to the operating system.
        ///
        /// <para>Owed on teardown, and owed in the editor especially: <c>Cursor.SetCursor</c>
        /// outlives play mode, so a session that exits holding a crosshair leaves the editor
        /// wearing one.</para>
        /// </summary>
        public void Release()
        {
            _look = CursorLook.Default;
            _tool = DesignateTool.None;
            _applied = false;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        void Apply() => Cursor.SetCursor(
            TextureFor(_look, _tool), HotspotFor(_look), CursorMode.Auto);
    }
}
