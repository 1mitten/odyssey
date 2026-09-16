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
            Recolour();
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
