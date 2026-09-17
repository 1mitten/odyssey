#nullable enable
using Odyssey.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// Every piece of text in the HUD is made here, so that the type scale is the only way to set
    /// a font size.
    ///
    /// <para><b>Why this is C# rather than a stylesheet class per role.</b> Either would work, and
    /// a <c>.t-row</c> class would be the idiomatic answer. The deciding argument is that the
    /// scale is an acceptance criterion — six steps, "no others" — and a criterion has to be
    /// testable. A size written in USS can only be read back by parsing the sheet; a size written
    /// here comes out of <see cref="HudType"/>, which lives in the Unity-free assembly the fast
    /// tier already runs. So the sheet keeps colour, spacing and state, and the type comes from
    /// one table with one test around it.</para>
    ///
    /// <para><b>Weight is where the faces have to be honest.</b> Archivo Narrow ships from Google
    /// only as a variable font, and Unity's importer takes its default instance, so 400 and 500
    /// both draw as regular and 600 and 700 are handed to the renderer as bold for it to
    /// synthesise. <see cref="HudType.BoldFrom"/> is the single place that split lives.</para>
    ///
    /// <para><b>A missing font is not a broken HUD.</b> The two faces are committed under
    /// <c>Assets/Odyssey/Presentation/Ui/Fonts/</c>, but a clone that has not imported them, or a
    /// harness that builds the shell by hand, simply leaves the panel's own theme font in place —
    /// sizes, weights and tracking still apply, so the layout is identical and only the letter
    /// shapes differ.</para>
    /// </summary>
    public static class HudText
    {
        /// <summary>Archivo Narrow. Set once by the shell from its serialised field.</summary>
        public static Font? Ui;

        /// <summary>IBM Plex Mono. Set once by the shell from its serialised field.</summary>
        public static Font? Mono;

        /// <summary>
        /// A label in one of the scale's roles.
        /// </summary>
        /// <param name="numeric">True for anything that is a figure — a count, the clock, a
        /// coordinate, a percentage, a hotkey cap. Switches the family to the mono face at weight
        /// 500 without moving the step.</param>
        public static Label Make(string text, HudTextRole role, bool numeric = false,
            string? ussClass = null)
        {
            var label = new Label();
            if (!string.IsNullOrEmpty(ussClass)) label.AddToClassList(ussClass!);
            Apply(label, role, numeric);
            Set(label, text, role);
            return label;
        }

        /// <summary>Set a label's text, upper-casing it when its role says so.</summary>
        public static void Set(Label label, string text, HudTextRole role)
        {
            HudTextStyle style = HudType.Of(role);
            label.text = style.Uppercase ? (text ?? string.Empty).ToUpperInvariant() : text ?? string.Empty;
        }

        /// <summary>
        /// The class every hotkey cap carries.
        ///
        /// <para><b>It exists so that one rule can be stated once.</b> A key cap is a legend, not
        /// a word — "ESC", "F3", "M" — and it is the one thing on this screen allowed to be a
        /// two-or-three-letter capitalised fragment, which everything else is forbidden to be by
        /// <c>NoLabelIsAThreeLetterPlaceholder</c>. That test used to name the classes it would
        /// excuse, one per place a cap happened to be drawn, and the Build palette's ESC hint is
        /// what showed the cost of that: a fourth cap in a fourth place failed a test about
        /// placeholder names, correctly by the letter of the rule and wrongly by its meaning.
        /// Marking the role rather than listing the sites means a fifth cannot.</para>
        /// </summary>
        public const string KeyCapClass = "keycap";

        /// <summary>Apply a role's size, weight, tracking and family to any text element.</summary>
        public static void Apply(TextElement element, HudTextRole role, bool numeric = false)
        {
            HudTextStyle style = HudType.Of(role, numeric);

            if (role == HudTextRole.Hotkey) element.AddToClassList(KeyCapClass);

            element.style.fontSize = style.Size;
            element.style.letterSpacing = style.LetterSpacing;
            element.style.unityFontStyleAndWeight =
                style.Weight >= HudType.BoldFrom ? FontStyle.Bold : FontStyle.Normal;

            Font? face = style.Mono ? Mono : Ui;
            if (face != null) element.style.unityFontDefinition = FontDefinition.FromFont(face);
        }

        /// <summary>
        /// What the text renderer's line box actually comes to for a role, as a multiple of the
        /// point size.
        ///
        /// <para><b>Measured, not derived.</b> A 13 px label left to size itself in this panel came
        /// out 26 px tall — twice the point size, where a reasonable guess would have been about
        /// 1.35 of it. That was nine pixels more than <see cref="HudLayout"/> had allowed the whole
        /// empty inspect pane, and it is the only place in this HUD where the renderer's metrics
        /// reach the layout arithmetic: every other row declares its own height. Kept here rather
        /// than in <c>HudLayout</c> because it is a fact about the renderer, which the Unity-free
        /// assembly cannot see.</para>
        /// </summary>
        public const float LineBoxFactor = 2.0f;

        /// <summary>The height a role's text occupies if nothing constrains it.</summary>
        public static float LineHeight(HudTextRole role) =>
            Mathf.Ceil(HudType.Of(role).Size * LineBoxFactor);
    }
}
