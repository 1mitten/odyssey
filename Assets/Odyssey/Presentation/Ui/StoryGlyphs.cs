#nullable enable
using Odyssey.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// The tension gauge on the clock's line (design 68 §5a, mockup 25g): four columns on a
    /// 16 x 16 box, the band's own number of them standing full and the rest as stubs.
    ///
    /// <para>Painted from <see cref="TensionModel"/>'s columns rather than from its path string:
    /// the two are the same rectangles, the path is what the fast tier parses to hold the drawing
    /// to the mockup, and this is what the screen draws. Repainted only when the band moves.</para>
    /// </summary>
    public sealed class TensionGauge : VisualElement
    {
        int _band = TensionModel.NoBand;

        public TensionGauge()
        {
            style.width = HudLayout.ClockGauge;
            style.height = HudLayout.ClockGauge;
            style.flexShrink = 0;
            generateVisualContent += Paint;
        }

        public int Band
        {
            get => _band;
            set
            {
                if (_band == value) return;
                _band = value;
                MarkDirtyRepaint();
            }
        }

        void Paint(MeshGenerationContext context)
        {
            if (!TensionModel.IsBand(_band)) return;
            Rect r = contentRect;
            if (r.width <= 1f || r.height <= 1f) return;
            float scale = Mathf.Min(r.width, r.height) / TensionModel.Box;

            Painter2D painter = context.painter2D;
            painter.fillColor = HudTokens.Convert(TensionModel.TintOf(_band));
            for (int c = 0; c < TensionModel.ColumnX.Length; c++)
            {
                float h = TensionModel.HeightOf(_band, c);
                float x = TensionModel.ColumnX[c] * scale;
                float top = (TensionModel.Bottom - h) * scale;
                float w = TensionModel.ColumnWidth * scale;
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, top));
                painter.LineTo(new Vector2(x + w, top));
                painter.LineTo(new Vector2(x + w, top + h * scale));
                painter.LineTo(new Vector2(x, top + h * scale));
                painter.ClosePath();
                painter.Fill();
            }
        }
    }

    /// <summary>
    /// A storyteller's rhythm strip (mockup 25h): an illustrative 24-day season, a dashed baseline
    /// and a vertical mark for each threat, as tall as the threat. No axis, no number and no label,
    /// so it can never be read as this colony's history.
    ///
    /// <para>Drawn in the strip's own 190 x 40 units, scaled to the element's box; the compact
    /// page draws it 28 high, and the marks shrink with it rather than being cut.</para>
    /// </summary>
    public sealed class RhythmStrip : VisualElement
    {
        readonly StoryCatalogue.Mark[] _marks;
        Color _tint;

        const float MarkStroke = 3f;
        const float BaselineStroke = 1.5f;
        const float Dash = 3f;

        public RhythmStrip(StoryCatalogue.Mark[] marks, Color tint)
        {
            _marks = marks;
            _tint = tint;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
        }

        public Color Tint
        {
            get => _tint;
            set
            {
                if (_tint == value) return;
                _tint = value;
                MarkDirtyRepaint();
            }
        }

        void Paint(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (r.width <= 1f || r.height <= 1f) return;
            float sx = r.width / StoryCatalogue.StripWidth;
            float sy = r.height / StoryCatalogue.StripHeight;

            Painter2D painter = context.painter2D;
            painter.lineCap = LineCap.Butt;

            // The baseline: 1.5 wide in the control-border colour, dashed 3 3.
            painter.strokeColor = HudTokens.Convert(HudTheme.ControlBorder);
            painter.lineWidth = Mathf.Max(1f, BaselineStroke * sy);
            float y = StoryCatalogue.BaselineY * sy;
            for (float x = StoryCatalogue.BaselineStart; x < StoryCatalogue.BaselineEnd; x += 2f * Dash)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(x * sx, y));
                painter.LineTo(new Vector2(Mathf.Min(x + Dash, StoryCatalogue.BaselineEnd) * sx, y));
                painter.Stroke();
            }

            // The threats: 3 wide, rising from the baseline by their size.
            painter.strokeColor = _tint;
            painter.lineWidth = Mathf.Max(1f, MarkStroke * sx);
            foreach (StoryCatalogue.Mark mark in _marks)
            {
                float x = StoryCatalogue.MarkX(mark.Day) * sx;
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, y));
                painter.LineTo(new Vector2(x, (StoryCatalogue.BaselineY - mark.Size) * sy));
                painter.Stroke();
            }
        }
    }
}
