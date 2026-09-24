#nullable enable

namespace Odyssey.Hud
{
    /// <summary>
    /// Whether the walls are lowered this frame (design 42): the player's choice, overruled by
    /// build mode.
    ///
    /// <para><b>The one owner of the rule.</b> The composition root asks this once a frame and
    /// writes the answer to the slice, and the renderer, the picker, the order marks, the door
    /// leaves and every actor pass read that one field. A renderer and a picker each working build
    /// mode out for themselves would disagree the first time a tool was added (P1).</para>
    ///
    /// <para><b>Build mode is the palette open, or Build or Deconstruct armed</b> (owner,
    /// 2026-09-24). Mine, Fell, Cancel and the zone tools keep the stumps: seeing into a building
    /// helps there, and none of them is about the shape of a wall.</para>
    /// </summary>
    public static class WallsView
    {
        /// <summary>
        /// True when walls should be drawn as stumps: the player has them down, and nothing being
        /// built needs to see them standing.
        /// </summary>
        public static bool Lowered(bool chosen, bool paletteOpen, DesignateTool tool) =>
            chosen && !BuildMode(paletteOpen, tool);

        /// <summary>Is the player building — so that walls stand whatever the choice?</summary>
        public static bool BuildMode(bool paletteOpen, DesignateTool tool) =>
            paletteOpen || tool == DesignateTool.Build || tool == DesignateTool.Deconstruct;
    }
}
