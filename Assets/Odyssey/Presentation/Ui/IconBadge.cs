#nullable enable
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// A placeholder icon for a symbolic key, pending the owner's pixel-art sheets.
    ///
    /// Exactly the mechanism design 09 §7 specifies for the period before art exists: a rounded
    /// square whose hue comes from a stable hash of the key, with a two-to-three character
    /// abbreviation of the key's last segment. Deterministic, so a screenshot of the HUD is
    /// comparable week to week and a slot can be discussed by its key. When the sheets land in
    /// <c>art-source/icons/sheets/</c> this element becomes a sprite lookup on the same key and
    /// nothing about the HUD's layout changes — the key, not the picture, is the contract.
    /// </summary>
    public sealed class IconBadge : TextElement
    {
        public const string Class = "icon";
        public const string ClassLarge = "icon--32";
        public const string ClassAvatar = "icon--avatar";

        public string Key { get; private set; } = string.Empty;

        /// <param name="key">Symbolic icon key, e.g. <c>ui.speed.pause</c>.</param>
        /// <param name="sizeClass">USS size class; defaults to the 16 px slot.</param>
        public IconBadge(string key, string? sizeClass = null)
        {
            AddToClassList(Class);
            if (sizeClass != null) AddToClassList(sizeClass);
            SetKey(key);
        }

        /// <summary>
        /// Point the badge at a different key, recomputing badge and colour. Roster cards carry
        /// a job glyph that changes with the pawn's job; replacing the element per change would
        /// churn the tree, so the badge retargets in place. A same-key call does nothing.
        /// </summary>
        public void SetKey(string key)
        {
            if (key == Key) return;
            Key = key;

            text = Abbreviation(key);
            style.backgroundColor = PlaceholderColour(key);
            style.color = new Color(0.05f, 0.06f, 0.08f);
            tooltip = key;
        }

        /// <summary>
        /// Two or three characters from the key's last segment: <c>ui.speed.pause</c> becomes
        /// PAU. Uppercase so the badge reads as a sigil rather than as a clipped word.
        /// </summary>
        public static string Abbreviation(string key)
        {
            int dot = key.LastIndexOf('.');
            string tail = dot >= 0 ? key.Substring(dot + 1) : key;
            int take = Mathf.Min(3, tail.Length);
            return tail.Substring(0, take).ToUpperInvariant();
        }

        /// <summary>
        /// The key's colour: FNV-1a hash of the key mapped into hue and saturation/lightness
        /// bands narrow enough that every placeholder sits in the same family, the way the
        /// mockup's placeholders do. Stable across runs and platforms because the hash is.
        /// </summary>
        public static Color PlaceholderColour(string key)
        {
            uint hash = 2166136261;
            foreach (char c in key)
            {
                hash ^= c;
                hash = unchecked(hash * 16777619);
            }

            float hue = hash % 360u / 360f;
            float saturation = 0.42f + (hash >> 9) % 26u / 100f;
            float value = 0.48f + (hash >> 17) % 14u / 100f;
            return Color.HSVToRGB(hue, saturation, value);
        }
    }
}
