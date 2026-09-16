#nullable enable
using System;

namespace Odyssey.Hud
{
    /// <summary>
    /// Which overlays are painted, per <c>09-ui-and-input.md</c> §3 row 14. Today that is one
    /// switch: the developer overlay (A15), the immediate-mode readout of ticks, draw calls and
    /// frame time. Off by default because it sits on the picture the player is reading, and
    /// toggled by the backtick key; the world-data channels of A12 arrive with their renderer
    /// and will be state here too, so the icons that stand for them have one owner from the
    /// start.
    /// </summary>
    public sealed class OverlayDirector
    {
        public bool DeveloperVisible { get; private set; }

        /// <summary>Raised after every change, so a presenter can answer without polling.</summary>
        public event Action? Changed;

        public void ToggleDeveloper() => SetDeveloper(!DeveloperVisible);

        public void SetDeveloper(bool visible)
        {
            if (DeveloperVisible == visible) return;
            DeveloperVisible = visible;
            Changed?.Invoke();
        }
    }
}
