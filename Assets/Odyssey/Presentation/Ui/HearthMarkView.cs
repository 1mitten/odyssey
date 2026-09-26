#nullable enable
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// The house over the hearth (design 43 §5b, Claude Design's specification): the home glyph,
    /// 28 px, filled in the accent at 70%, standing <see cref="Height"/> above the hearth and
    /// always facing the screen. Shown only while the Home view is on, there is a hearth, and the
    /// hearth is on the active layer — a mark floating over a lower layer is a mark over nothing
    /// the player is looking at.
    ///
    /// <para><b>Beneath the HUD, not in it</b>, like the fight's floating words
    /// (<see cref="CombatFloaterView"/>): a first child of the document's root, never taking a
    /// click. One element, made once and moved.</para>
    /// </summary>
    public sealed class HearthMarkView
    {
        /// <summary>The glyph's size on screen, px.</summary>
        public const float Size = 28f;

        /// <summary>How far above the hearth's ground the mark stands, metres.</summary>
        public const float Height = 1.2f;

        /// <summary>The mark's strength.</summary>
        public const float Alpha = 0.70f;

        readonly VisualElement _layer;
        readonly PathGlyph _glyph;

        public HearthMarkView(VisualElement root)
        {
            _layer = new VisualElement { name = "hearth-mark", pickingMode = PickingMode.Ignore };
            _layer.style.position = Position.Absolute;
            _layer.style.left = 0;
            _layer.style.top = 0;
            _layer.style.right = 0;
            _layer.style.bottom = 0;
            _glyph = new PathGlyph(HudIcons.Home, Size, HudTokens.Convert(HudTheme.Accent.WithAlpha(Alpha)), fill: true);
            _glyph.style.position = Position.Absolute;
            // Centred on its point, as a floating word is.
            _glyph.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            _glyph.style.display = DisplayStyle.None;
            _layer.Add(_glyph);
            root.Insert(0, _layer);
        }

        /// <summary>Whether the mark was drawn on the last frame. For tests.</summary>
        public bool Showing { get; private set; }

        /// <summary>
        /// Place the mark over the hearth, or hide it. <paramref name="visible"/> is the Home view's
        /// switch; the rest of the rule is here.
        /// </summary>
        public void Draw(WorldSnapshot snapshot, bool visible, int activeLayer, Camera? camera)
        {
            Showing = false;
            IPanel? panel = _layer.panel;
            int hearth = snapshot.HearthCell;
            if (visible && camera != null && panel != null && hearth >= 0 && hearth < snapshot.Size.CellCount)
            {
                CellRef cell = snapshot.Size.FromIndex(hearth);
                if (cell.Y == activeLayer)
                {
                    Vector3 floor = CellMetrics.FloorCentre(cell);
                    Vector3 at = floor + Vector3.up * (GroundRelief.HeightAt(floor.x, floor.z) + Height);
                    if (Vector3.Dot(at - camera.transform.position, camera.transform.forward) > 0f)
                    {
                        Vector2 point = RuntimePanelUtils.CameraTransformWorldToPanel(panel, at, camera);
                        _glyph.style.left = point.x;
                        _glyph.style.top = point.y;
                        Showing = true;
                    }
                }
            }
            _glyph.style.display = Showing ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Dispose() => _layer.RemoveFromHierarchy();
    }
}
