#nullable enable
using System;
using Odyssey.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// A colonist's composed flat avatar, drawn (<c>docs/design/20-avatars.md</c>).
    ///
    /// <para><b>Four layers in the same 24-unit box every other drawn glyph uses:</b> the tile in
    /// the colonist's own garment colour, shoulders in that garment's shadow, head and neck in
    /// their skin, and one of eight crowns in their hair colour. <see cref="ColonistFace"/> decides
    /// all six of those; this file only knows where the paint goes.</para>
    ///
    /// <para><b>Why it is drawn rather than cut from a sheet.</b> `icon-map.csv` records
    /// <c>ui.pawn.colonist</c> as a gap and says why: *"a human figure. No sheet contains one, and
    /// this is the most-used icon in the HUD."* So the same answer the thirty-seven build-palette
    /// icons took, for the same reason — and written so the owner's ninth sheet can take it back
    /// one layer at a time, which is the mechanism <see cref="IconBadge"/> already is.</para>
    ///
    /// <para><b>It is not a <see cref="HudGlyph"/>, and the design said it would be.</b> That class
    /// paints from a private handler subscribed in its constructor, so a subclass cannot add to the
    /// drawing without going through <c>HudGlyphKind</c>, which is an enum of one-shape-per-value
    /// and an avatar is six values at once. Making the paint virtual to buy that would loosen the
    /// chrome set's own contract for a case that does not share it: an avatar is all fills and no
    /// strokes, so the stroke rule that makes a 17 px row icon and a 30 px one look like one set
    /// does not apply to it. It keeps the box, the scale and the <c>P</c> mapping, which is what
    /// makes it sit with them on a card.</para>
    ///
    /// <para><b>The ink hull underneath is a contrast guarantee</b>, not a style — see
    /// <see cref="HudTheme.AvatarInk"/>. Fourteen garments against seven skins means some pair is
    /// close, and on that colonist alone the head would dissolve into the tile.</para>
    ///
    /// <para><b>It allocates nothing at rest.</b> <see cref="SetFace"/> returns without touching
    /// anything when the face is the one already drawn, which is what
    /// <c>Adr0003_F1_TheDenseHudHoldsItsBudgetAndAllocatesNothing</c> requires: that test fails on
    /// any collection in the window, not merely on a byte delta, so a strip of twenty-six cards
    /// recomposing itself every refresh would fail it outright.</para>
    /// </summary>
    public sealed class AvatarGlyph : VisualElement
    {
        /// <summary>The class every avatar carries, so a test can find them all.</summary>
        public const string Class = "avatar";

        /// <summary>The design box, shared with <see cref="HudGlyph"/> so the two read as one set.</summary>
        const float Box = 24f;

        // ---- the figure, in design-box units. A bust: head, neck, shoulders.

        const float HeadCentreX = 12f;
        const float HeadCentreY = 9.4f;
        const float HeadRadius = 4.6f;
        const float NeckHalfWidth = 1.9f;
        const float NeckTop = 12.6f;
        const float ShoulderTop = 16.2f;

        /// <summary>
        /// Half the shoulder width at the top of the bust, per build. Narrow, ordinary, broad —
        /// three, because a fourth is not distinguishable at 26 px, which was the test for whether
        /// to have it (<see cref="ColonistFace.Builds"/>).
        /// </summary>
        /// <para><b>Measured on the contact sheet, not chosen.</b> The first set was
        /// {5.0, 6.3, 7.6} against a flare of 2.4, which put all three busts between 14.8 and 20.0
        /// units wide at the bottom of a 24-unit box — every one of them near enough the full width
        /// that the three builds were one build. Spread wider and flared less, they come out 11.8,
        /// 15.0 and 18.2, which reads as three people.</para>
        static readonly float[] ShoulderHalfWidth = { 4.0f, 5.6f, 7.2f };

        /// <summary>How much wider the bust is where it meets the bottom edge than at the top.</summary>
        const float ShoulderFlare = 1.9f;

        ColonistFace _face;
        bool _drawn;
        Texture2D? _portrait;

        public AvatarGlyph(float size)
        {
            AddToClassList(Class);
            pickingMode = PickingMode.Ignore;
            style.width = size;
            style.height = size;
            style.flexShrink = 0;

            // Rounded here rather than per site. An avatar is the same object on a roster card, in
            // the inspect header and on the setup page, so a corner radius written three times in
            // the stylesheet would be three chances to disagree — the same argument HudLayout
            // makes about every other number it owns.
            style.borderTopLeftRadius = HudTheme.ControlRadius;
            style.borderTopRightRadius = HudTheme.ControlRadius;
            style.borderBottomLeftRadius = HudTheme.ControlRadius;
            style.borderBottomRightRadius = HudTheme.ControlRadius;

            // The figure runs to the bottom edge of the box, so at the rounded corners it would
            // otherwise paint outside its own tile.
            style.overflow = Overflow.Hidden;

            generateVisualContent += Paint;
        }

        /// <summary>Who this is. A no-op when it is already who it was.</summary>
        public void SetFace(in ColonistFace face)
        {
            if (_drawn && _face.Equals(face)) return;

            _face = face;
            _drawn = true;
            style.backgroundColor = HudTokens.Of(face.Tile);
            MarkDirtyRepaint();
        }

        /// <summary>
        /// A photograph of the actual character, where one could be taken
        /// (<c>docs/design/20-avatars.md</c> §10).
        ///
        /// <para><b>The drawing is the fallback, not the other way round.</b> A portrait needs the
        /// licensed packs, so a clone without <c>Assets/Synty</c> gets the drawn figure and is
        /// still correct — the same bargain <see cref="IconBadge"/> strikes between a sheet and an
        /// outlined square, and the reason none of the drawing is wasted.</para>
        ///
        /// <para>The tile stays the colonist's garment colour underneath, because a portrait is
        /// rendered on a transparent background and sits in the same box.</para>
        /// </summary>
        public void SetPortrait(Texture2D? portrait)
        {
            if (ReferenceEquals(_portrait, portrait)) return;

            _portrait = portrait;
            if (portrait != null)
            {
                style.backgroundImage = new StyleBackground(portrait);
                style.backgroundRepeat = new StyleBackgroundRepeat(
                    new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat));
                style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
                style.backgroundPositionX = new StyleBackgroundPosition(
                    new BackgroundPosition(BackgroundPositionKeyword.Center));
                style.backgroundPositionY = new StyleBackgroundPosition(
                    new BackgroundPosition(BackgroundPositionKeyword.Center));
                style.unityBackgroundImageTintColor = Color.white;
            }
            else
            {
                style.backgroundImage = StyleKeyword.Null;
            }

            MarkDirtyRepaint();
        }

        /// <summary>Whether this slot is showing a photograph rather than the drawing.</summary>
        public bool HasPortrait => _portrait != null;

        /// <summary>The face on screen, for a test that wants to ask.</summary>
        public ColonistFace Face => _face;

        /// <summary>Whether anybody has been put in the box yet.</summary>
        public bool HasFace => _drawn;

        /// <summary>Resize in place, for a layout that scales.</summary>
        public void Resize(float size)
        {
            if (Mathf.Approximately(style.width.value.value, size)) return;

            style.width = size;
            style.height = size;
            MarkDirtyRepaint();
        }

        void Paint(MeshGenerationContext context)
        {
            // A drawn figure stroked over a photograph is a mask nobody asked for — the same rule
            // IconBadge.PaintSuppressed keeps between a sheet and the placeholder square.
            if (!_drawn || _portrait != null) return;

            Rect box = contentRect;
            if (box.width <= 1f || box.height <= 1f) return;

            float side = Mathf.Min(box.width, box.height);
            float scale = side / Box;
            float ox = (box.width - side) * 0.5f;
            float oy = (box.height - side) * 0.5f;

            Vector2 P(float x, float y) => new Vector2(ox + x * scale, oy + y * scale);

            Painter2D painter = context.painter2D;

            // Twice over: the whole silhouette in ink, standing a little proud, then the colours
            // on top of it. Drawn in this order rather than as an outline because a stroke around
            // a filled shape doubles every join, and at 26 px the joins are the picture.
            float ink = HudTokens.AvatarInkWidth;
            painter.fillColor = HudTokens.AvatarInk;
            Bust(painter, P, ink);
            Head(painter, P, ink);
            Crown(painter, P, _face.HairShape, ink);

            painter.fillColor = HudTokens.Of(_face.Shoulders);
            Bust(painter, P, 0f);

            painter.fillColor = HudTokens.Of(_face.Skin);
            Head(painter, P, 0f);

            painter.fillColor = HudTokens.Of(_face.Hair);
            Crown(painter, P, _face.HairShape, 0f);
        }

        /// <summary>Neck and shoulders, as one closed shape so the two cannot part company.</summary>
        void Bust(Painter2D painter, Func<float, float, Vector2> p, float grow)
        {
            float half = ShoulderHalfWidth[Mathf.Clamp(_face.Build, 0, ShoulderHalfWidth.Length - 1)] + grow;
            float flare = half + ShoulderFlare;
            float neck = NeckHalfWidth + grow;

            painter.BeginPath();
            painter.MoveTo(p(HeadCentreX - neck, NeckTop - grow));
            painter.LineTo(p(HeadCentreX + neck, NeckTop - grow));
            painter.LineTo(p(HeadCentreX + neck, ShoulderTop - grow));
            painter.LineTo(p(HeadCentreX + half, ShoulderTop - grow));
            painter.LineTo(p(HeadCentreX + flare, Box));
            painter.LineTo(p(HeadCentreX - flare, Box));
            painter.LineTo(p(HeadCentreX - half, ShoulderTop - grow));
            painter.LineTo(p(HeadCentreX - neck, ShoulderTop - grow));
            painter.ClosePath();
            painter.Fill();
        }

        void Head(Painter2D painter, Func<float, float, Vector2> p, float grow) =>
            FillArc(painter, p, HeadCentreX, HeadCentreY, HeadRadius + grow, -180f, 180f);

        /// <summary>
        /// One of eight crowns, each built out of the same two primitives — a wedge of the head's
        /// own circle, and a blob beside it — so that no crown can drift off the skull as the
        /// head's numbers are tuned.
        ///
        /// <para>They are a set rather than eight independent drawings: a cap everybody wears, and
        /// then what is added to it. That is what keeps them legible as *variations* at 26 px
        /// instead of eight unrelated marks.</para>
        /// </summary>
        void Crown(Painter2D painter, Func<float, float, Vector2> p, int shape, float grow)
        {
            float r = HeadRadius + grow + 0.35f;
            float cx = HeadCentreX;
            float cy = HeadCentreY;

            switch (shape)
            {
                case 0: // shaved: a band at the temples and nothing on top
                    FillArc(painter, p, cx, cy, r, 150f, 210f);
                    FillArc(painter, p, cx, cy, r, -30f, 30f);
                    break;

                case 1: // cropped: a plain cap
                    FillArc(painter, p, cx, cy, r, 15f, 165f);
                    break;

                case 2: // fringe: the cap pulled down over the brow on both sides
                    FillArc(painter, p, cx, cy, r, -10f, 190f);
                    break;

                case 3: // side part: the cap, swept lower on one side
                    FillArc(painter, p, cx, cy, r, 10f, 170f);
                    FillArc(painter, p, cx, cy, r, 150f, 205f);
                    break;

                case 4: // bun: the cap with a knot above and behind
                    FillArc(painter, p, cx, cy, r, 15f, 165f);
                    FillArc(painter, p, cx - r * 0.75f, cy - r * 0.95f, r * 0.52f, -180f, 180f);
                    break;

                case 5: // tall: the cap, standing well proud of the skull
                    FillArc(painter, p, cx, cy - r * 0.34f, r * 1.22f, 5f, 175f);
                    break;

                case 6: // long: the cap with panels down past the jaw
                    FillArc(painter, p, cx, cy, r, 10f, 170f);
                    Panel(painter, p, cx - r, cy - 2.6f, -1f, r * 0.95f, ShoulderTop + 1.2f - grow);
                    Panel(painter, p, cx + r, cy - 2.6f, 1f, r * 0.95f, ShoulderTop + 1.2f - grow);
                    break;

                default: // ponytail: the cap with a tail down one side only
                    FillArc(painter, p, cx, cy, r, 10f, 170f);
                    Panel(painter, p, cx - r, cy - 2.6f, -1f, r * 0.85f, ShoulderTop + 3.2f - grow);
                    break;
            }
        }

        /// <summary>A hank of hair hanging down one side of the head.</summary>
        static void Panel(Painter2D painter, Func<float, float, Vector2> p,
                          float x, float top, float side, float width, float bottom)
        {
            float outer = x + side * width * 0.35f;
            float inner = x - side * width * 0.65f;

            painter.BeginPath();
            painter.MoveTo(p(inner, top));
            painter.LineTo(p(outer, top));
            painter.LineTo(p(outer, bottom));
            painter.LineTo(p(inner, bottom));
            painter.ClosePath();
            painter.Fill();
        }

        /// <summary>
        /// A filled wedge of a circle, sampled into a polygon rather than handed to
        /// <c>Painter2D.Arc</c>.
        ///
        /// <para><b>Deliberate:</b> the arc call's angles are measured in the element's own
        /// coordinate space, where y runs down, so every "over the top of the head" would be
        /// written back to front and read as a chin. Sixteen samples over a half-turn is under a
        /// tenth of a pixel of chord error at 64 px and invisible at 26; angles here are the
        /// ordinary ones, measured anticlockwise from due right with y up.</para>
        /// </summary>
        static void FillArc(Painter2D painter, Func<float, float, Vector2> p,
                            float cx, float cy, float radius, float fromDegrees, float toDegrees)
        {
            const int Steps = 16;

            painter.BeginPath();
            painter.MoveTo(p(cx, cy));
            for (int i = 0; i <= Steps; i++)
            {
                float t = fromDegrees + (toDegrees - fromDegrees) * i / Steps;
                float a = t * Mathf.Deg2Rad;
                painter.LineTo(p(cx + Mathf.Cos(a) * radius, cy - Mathf.Sin(a) * radius));
            }

            painter.ClosePath();
            painter.Fill();
        }
    }
}
