#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// A line-art icon written as an SVG path on a 24-unit grid, stroked with round caps and
    /// joins — the settings window's icons, given by its design as paths (design 39 §4).
    ///
    /// <para><b>Parsed once, at construction</b>, into polylines by <see cref="SvgPath"/>; a
    /// repaint strokes cached points and builds no string and no list, so the HUD's
    /// no-allocation rule in steady state (ADR 0003, F1) holds.</para>
    ///
    /// <para>The stroke scales with the element: <see cref="SettingsLayout.IconStroke"/> is in the
    /// path's own units, so an 18 px icon and a 20 px one are the same drawing.</para>
    /// </summary>
    public sealed class PathGlyph : VisualElement
    {
        readonly IReadOnlyList<SvgPath.Subpath> _paths;
        readonly float _box;
        readonly bool _fill;
        readonly float _stroke;
        Color _tint;

        /// <param name="d">The path, in a box of <paramref name="box"/> units.</param>
        /// <param name="fill">Fill the closed shapes rather than stroke them — the select's
        /// triangle, the home glyph.</param>
        /// <param name="stroke">The line's width in path units; the Assign tab's cycle mark is
        /// heavier than the settings window's icons (design 43 §6).</param>
        public PathGlyph(string d, float size, Color tint, float box = SettingsLayout.IconBox,
            bool fill = false, float stroke = SettingsLayout.IconStroke)
            : this(d, size, size, tint, box, fill, stroke)
        {
        }

        public PathGlyph(string d, float width, float height, Color tint, float box, bool fill,
            float stroke = SettingsLayout.IconStroke)
        {
            _paths = SvgPath.Parse(d);
            _box = box;
            _fill = fill;
            _stroke = stroke;
            _tint = tint;
            pickingMode = PickingMode.Ignore;
            style.width = width;
            style.height = height;
            style.flexShrink = 0;
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

        bool _solid;

        /// <summary>
        /// Fill the closed shapes in the tint <b>and</b> stroke the outline over them: the inspect
        /// header's toggles when on (design 61 §2), so the same icon reads as an outline off and a
        /// solid on without swapping. The fill is even-odd across every subpath at once, which is
        /// what keeps the eye's pupil a hole. Ignored for a glyph built with <c>fill</c>.
        /// </summary>
        public bool Solid
        {
            get => _solid;
            set
            {
                if (_solid == value) return;
                _solid = value;
                MarkDirtyRepaint();
            }
        }

        void Paint(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (r.width <= 1f || r.height <= 1f) return;

            // A stroked icon is square and centred; a filled shape is fitted to the element's
            // width, so the select's 10 x 6 triangle fills a 10 x 6 element.
            float scale = _fill ? r.width / _box : Mathf.Min(r.width, r.height) / _box;
            float ox = _fill ? 0f : (r.width - _box * scale) * 0.5f;
            float oy = _fill ? 0f : (r.height - _box * scale) * 0.5f;

            Painter2D painter = context.painter2D;
            painter.strokeColor = _tint;
            painter.fillColor = _tint;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            painter.lineWidth = Mathf.Max(1f, _stroke * scale);

            // One path holding every closed subpath, so even-odd sees the pupil inside the lid.
            if (_solid && !_fill)
            {
                painter.BeginPath();
                for (int p = 0; p < _paths.Count; p++)
                {
                    if (!_paths[p].Closed) continue;
                    float[] pts = _paths[p].Points;
                    painter.MoveTo(new Vector2(ox + pts[0] * scale, oy + pts[1] * scale));
                    for (int i = 2; i < pts.Length; i += 2)
                        painter.LineTo(new Vector2(ox + pts[i] * scale, oy + pts[i + 1] * scale));
                    painter.ClosePath();
                }
                painter.Fill(FillRule.OddEven);
            }

            for (int p = 0; p < _paths.Count; p++)
            {
                float[] pts = _paths[p].Points;
                painter.BeginPath();
                painter.MoveTo(new Vector2(ox + pts[0] * scale, oy + pts[1] * scale));
                for (int i = 2; i < pts.Length; i += 2)
                    painter.LineTo(new Vector2(ox + pts[i] * scale, oy + pts[i + 1] * scale));
                if (_paths[p].Closed) painter.ClosePath();
                if (_fill) painter.Fill();
                else painter.Stroke();
            }
        }
    }

    /// <summary>
    /// A key slot with nothing bound to it: an empty chip outlined in dashes (design 39 §6).
    ///
    /// <para>Drawn rather than bordered because UI Toolkit has no dashed border, and drawn rather
    /// than lettered because the dash character it replaces is exactly the kind of font-drawn
    /// mark the window has given up. The dashes are fixed in pixels, so every empty slot on the
    /// tab carries the same rhythm whatever its width.</para>
    /// </summary>
    public sealed class DashedOutline : VisualElement
    {
        const float Dash = 3f;
        const float Gap = 3f;

        Color _colour = HudTokens.Convert(HudTheme.EmptySlot);

        public DashedOutline()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;
            generateVisualContent += Paint;
        }

        /// <summary>
        /// The dash's ink. The settings window's empty slots keep <see cref="HudTheme.EmptySlot"/>;
        /// the Gear tab draws an empty slot in the control border and a locked pack slot in the
        /// panel border (design 47 §2).
        /// </summary>
        public Color Colour
        {
            get => _colour;
            set
            {
                if (_colour == value) return;
                _colour = value;
                MarkDirtyRepaint();
            }
        }

        void Paint(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (r.width <= 2f || r.height <= 2f) return;

            Painter2D painter = context.painter2D;
            painter.strokeColor = _colour;
            painter.lineWidth = 1f;
            painter.lineCap = LineCap.Butt;

            float l = 0.5f, t = 0.5f, rr = r.width - 0.5f, b = r.height - 0.5f;
            Side(painter, new Vector2(l, t), new Vector2(rr, t));
            Side(painter, new Vector2(rr, t), new Vector2(rr, b));
            Side(painter, new Vector2(rr, b), new Vector2(l, b));
            Side(painter, new Vector2(l, b), new Vector2(l, t));
        }

        static void Side(Painter2D painter, Vector2 from, Vector2 to)
        {
            float length = Vector2.Distance(from, to);
            Vector2 dir = (to - from) / length;
            for (float at = 0; at < length; at += Dash + Gap)
            {
                painter.BeginPath();
                painter.MoveTo(from + dir * at);
                painter.LineTo(from + dir * Mathf.Min(length, at + Dash));
                painter.Stroke();
            }
        }
    }
}
