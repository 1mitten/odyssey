#nullable enable
using Odyssey.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// The icon slot for a symbolic key, pending real art.
    ///
    /// <para><b>It draws a square, not a word.</b> Until 2026-09-16 this element put two or three
    /// characters of the key's last segment into a hashed colour tile — MEA for meals, WOO for
    /// wood, SCR for scrap. The interface acceptance criteria strike that out on two counts: the
    /// letters read as truncated data rather than as a deliberate stand-in, and a filled colour
    /// tile behind a value outshouts the value it belongs to. What is left is a single-colour
    /// outlined square, and the colour is the key's <see cref="HudCategory"/> rather than a hash
    /// of its characters — so the colours on screen mean something, and there are eight of them
    /// rather than one per key.</para>
    ///
    /// <para><b>The key is still the contract, and that has not changed.</b> When the owner's
    /// sheets land this element becomes a sprite lookup on the same key, at the same three sizes,
    /// and nothing about the HUD's layout moves.</para>
    ///
    /// <para><b>That lookup exists now</b> (<see cref="IconArt"/>), and it arrives one key at a
    /// time rather than eight sheets at once: a key with art draws the art, a key without draws
    /// the square, and the two live side by side on the same screen without either knowing about
    /// the other. So the interface is correct at every stage between no art and all of it, which
    /// is what lets a single icon be judged in the running game before the set is drawn.</para>
    ///
    /// <para><b>Three sizes exist and no others</b> (spec): <see cref="RowSize"/> in a list row,
    /// <see cref="BarSize"/> in the command bar, <see cref="AvatarSize"/> for the selected
    /// thing.</para>
    /// </summary>
    public sealed class IconBadge : HudGlyph
    {
        public const string Class = "icon";

        /// <summary>17 px: an icon in a list row.</summary>
        public const float RowSize = 17f;

        /// <summary>16 px: an icon in the command bar.</summary>
        public const float BarSize = 16f;

        /// <summary>30 px: the selected thing's avatar.</summary>
        public const float AvatarSize = 30f;

        public string Key { get; private set; } = string.Empty;

        /// <summary>
        /// Whether this slot takes its key's category colour. Only stores and the command bar may
        /// (spec: "category colour lives in the icon stroke, never a filled background, and only
        /// in stores and the command bar"); everywhere else an icon is drawn in the ink of the
        /// text beside it, which is what <c>false</c> means here.
        /// </summary>
        public bool Categorised { get; }

        Color _inherited = HudTokens.TextMeta;

        public IconBadge(string key, float size = RowSize, bool categorised = false)
            : base(HudGlyphKind.Placeholder, size, HudTokens.TextMeta)
        {
            Categorised = categorised;
            AddToClassList(Class);
            SetKey(key);
        }

        /// <summary>
        /// Point the slot at a different key. A same-key call does nothing, so a row whose job
        /// glyph changes with the pawn's job retargets in place rather than churning the tree.
        /// </summary>
        public void SetKey(string key)
        {
            if (key == Key) return;
            Key = key;
            tooltip = key;
            Dress();
            Recolour();
        }

        /// <summary>
        /// Put the key's art in the box, or take it out again. Real art is drawn **untinted** —
        /// the category colour exists to say what a placeholder stands for, and a picture says
        /// that for itself; tinting it would mean the colour of wood on screen was a HUD
        /// decision rather than the artist's.
        /// </summary>
        void Dress()
        {
            Texture2D? art = IconArt.For(Key);
            PaintSuppressed = art != null;

            if (art == null)
            {
                style.backgroundImage = StyleKeyword.Null;
                return;
            }

            style.backgroundImage = new StyleBackground(art);
            style.unityBackgroundImageTintColor = Color.white;

            // Every one of these is explicit because UI Toolkit's own defaults would tile a
            // 64 px drawing inside a 17 px row and show the player its top-left corner.
            style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
        }

        /// <summary>
        /// The colour an uncategorised slot takes: the ink of the row it sits in. Set by whoever
        /// builds the row, because UI Toolkit has no inherited stroke colour to read.
        /// </summary>
        public void Inherit(Color ink)
        {
            _inherited = ink;
            Recolour();
        }

        void Recolour() =>
            Tint = Categorised ? HudTokens.Category(HudTheme.CategoryOf(Key)) : _inherited;
    }
}
