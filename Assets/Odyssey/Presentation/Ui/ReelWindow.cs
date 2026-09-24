#nullable enable
using Odyssey.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// One window of the draw's machine (design 41 §6.5): a clipped white box with a figure, a name
    /// or a face in it, and while it spins the neighbours above and below it ghosted in.
    ///
    /// <para><b>Nothing is built or re-texted per frame.</b> The three items are made once; a spin
    /// moves them with <c>style.translate</c> and swaps a text only when the symbol under it
    /// changes, from strings the presenter built before the pull. Motion blur is the spec's own
    /// answer for UI Toolkit: ghosted duplicates at reduced opacity, no filter.</para>
    ///
    /// <para>Items are laid out absolutely, each the window's full size and centred on both axes, so
    /// a figure sits in the middle of the box at every value — "--", one digit or two.</para>
    /// </summary>
    public sealed class ReelWindow : VisualElement
    {
        /// <summary>The window's fill, which is what a landed window says.</summary>
        public enum Tone
        {
            Face,
            Hot,
            Cold,
            Tease,
        }

        const int Above = 0, Middle = 1, Below = 2;

        readonly Label?[] _labels = new Label?[3];
        readonly AvatarGlyph?[] _faces = new AvatarGlyph?[3];
        readonly VisualElement _barLeft;
        readonly VisualElement _barRight;
        readonly float _pitch;
        readonly HudTextRole _role;

        Tone _tone = Tone.Face;
        float _hot = -1f;

        /// <param name="faces">True for the portrait window, whose items are faces not words.</param>
        public ReelWindow(float width, float height, float pitch, HudTextRole role, bool numeric,
            bool faces = false, string? modifier = null)
        {
            _pitch = pitch;
            _role = role;
            AddToClassList("draw__window");
            if (!string.IsNullOrEmpty(modifier)) AddToClassList(modifier);
            style.width = width;
            style.height = height;
            style.overflow = Overflow.Hidden;
            pickingMode = PickingMode.Ignore;

            for (int i = 0; i < 3; i++)
            {
                VisualElement item;
                if (faces)
                {
                    var face = new AvatarGlyph(Mathf.Min(width, height) - 8f);
                    _faces[i] = face;
                    item = face;
                }
                else
                {
                    Label label = HudText.Make(string.Empty, role, numeric, "draw__item");
                    _labels[i] = label;
                    item = label;
                }

                item.pickingMode = PickingMode.Ignore;
                item.style.position = Position.Absolute;
                item.style.left = faces ? (width - (Mathf.Min(width, height) - 8f)) / 2f : 0f;
                item.style.top = faces ? 4f : 0f;
                if (!faces)
                {
                    item.style.width = width;
                    item.style.height = height;
                }

                Add(item);
            }

            // The tease's two amber bars, inside the left and right edges (the spec's 3 px).
            _barLeft = Bar("draw__bar--left");
            _barRight = Bar("draw__bar--right");
            Add(_barLeft);
            Add(_barRight);

            SetResting(string.Empty);
        }

        static VisualElement Bar(string side)
        {
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("draw__bar");
            bar.AddToClassList(side);
            bar.style.display = DisplayStyle.None;
            return bar;
        }

        /// <summary>Before a pull: one dim item in the middle — "--", a silhouette, "Not pulled".</summary>
        public void SetResting(string text)
        {
            Show(Middle, 0f, 0.35f);
            Hide(Above);
            Hide(Below);
            SetText(Middle, text);
            if (_faces[Middle] != null) _faces[Middle]!.SetPortrait(null);
            Bars(false, 0f);
            SetTone(Tone.Face, 0f);
        }

        /// <summary>
        /// Spinning or braking: the symbol nearest the middle and its two neighbours, scrolled by
        /// <paramref name="fraction"/> of a symbol. The middle is at 60% while it races and full
        /// once it has slowed to a crawl, which is the eye's own blur.
        /// </summary>
        public void SetSpinning(string above, string middle, string below, float fraction, bool crawling)
        {
            float y = fraction * _pitch;
            Show(Above, y - _pitch, crawling ? 0.3f : 0.22f);
            Show(Middle, y, crawling ? 1f : 0.6f);
            Show(Below, y + _pitch, crawling ? 0f : 0.22f);
            SetText(Above, above);
            SetText(Middle, middle);
            SetText(Below, below);
        }

        /// <summary>The same for the portrait window: three faces, by the colonist each belongs to.</summary>
        public void SetSpinningFaces(in ColonistFace above, in ColonistFace middle, in ColonistFace below,
            float fraction, bool crawling)
        {
            float y = fraction * _pitch;
            Show(Above, y - _pitch, crawling ? 0.3f : 0.22f);
            Show(Middle, y, crawling ? 1f : 0.6f);
            Show(Below, y + _pitch, crawling ? 0f : 0.22f);
            _faces[Above]?.SetFace(above);
            _faces[Middle]?.SetFace(middle);
            _faces[Below]?.SetFace(below);
            _faces[Middle]?.SetPortrait(null);
        }

        /// <summary>Landed: one item, full, in the middle; <paramref name="settle"/> is the bounce, in symbols.</summary>
        public void SetLanded(string text, float settle)
        {
            Show(Middle, settle * _pitch, 1f);
            Hide(Above);
            Hide(Below);
            SetText(Middle, text);
            Bars(false, 0f);
        }

        /// <summary>The portrait window landed: the real face, and the portrait once there is one.</summary>
        public void SetLandedFace(in ColonistFace face, Texture2D? portrait, float settle)
        {
            Show(Middle, settle * _pitch, 1f);
            Hide(Above);
            Hide(Below);
            _faces[Middle]?.SetFace(face);
            _faces[Middle]?.SetPortrait(portrait);
        }

        /// <summary>The tease's bars: shown while the reel crawls, pulsing twice over <paramref name="progress"/>.</summary>
        public void Bars(bool on, float progress)
        {
            _barLeft.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            _barRight.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (!on) return;
            float pulse = 0.55f + 0.45f * Mathf.Cos(progress * Mathf.PI * 4f);
            _barLeft.style.opacity = pulse;
            _barRight.style.opacity = pulse;
        }

        /// <summary>
        /// The window's fill: the machine white, or blended toward a landed tone by
        /// <paramref name="amount"/> (the hot fade), or the tease's pale amber.
        /// </summary>
        public void SetTone(Tone tone, float amount)
        {
            if (tone == _tone && Mathf.Approximately(amount, _hot)) return;
            _tone = tone;
            _hot = amount;
            Color target = tone switch
            {
                Tone.Hot => HudTokens.MachineHot,
                Tone.Cold => HudTokens.MachineCold,
                Tone.Tease => HudTokens.MachineTease,
                _ => HudTokens.MachineFace,
            };
            style.backgroundColor = tone == Tone.Face || tone == Tone.Tease
                ? target
                : Color.Lerp(HudTokens.MachineFace, target, Mathf.Clamp01(amount));
        }

        void SetText(int item, string text)
        {
            Label? label = _labels[item];
            if (label == null) return;
            // Reference equality is the point: the presenter hands the same string instances back
            // every frame, so an unchanged symbol costs one comparison and no layout.
            if (ReferenceEquals(label.userData, text)) return;
            label.userData = text;
            HudText.Set(label, text, _role);
        }

        void Show(int item, float y, float opacity)
        {
            VisualElement? element = (VisualElement?)_labels[item] ?? _faces[item];
            if (element == null) return;
            element.style.display = opacity <= 0f ? DisplayStyle.None : DisplayStyle.Flex;
            element.style.opacity = opacity;
            element.style.translate = new Translate(0, y);
        }

        void Hide(int item)
        {
            VisualElement? element = (VisualElement?)_labels[item] ?? _faces[item];
            if (element != null) element.style.display = DisplayStyle.None;
        }
    }
}
