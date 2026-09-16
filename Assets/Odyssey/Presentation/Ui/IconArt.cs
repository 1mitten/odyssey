#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// The lookup from a symbolic icon key to real art, where real art exists.
    ///
    /// <para><b>This is the sprite lookup ADR 0007 promised.</b> "When the owner's sheets land
    /// <c>IconBadge</c> becomes a sprite lookup on the same key with nothing about the HUD's
    /// layout moving" — this is that, arriving one key at a time rather than eight sheets at
    /// once. A key with no art is not an error and is not logged: it draws the outlined square
    /// it drew before, so the HUD is correct at every stage between no art and all of it.</para>
    ///
    /// <para><b>Why <c>Resources</c> and why under <c>Assets/Art/Ui/</c>.</b> ADR 0007 puts every
    /// interface texture under <c>Assets/Art/Ui/</c> and says plainly that a carve-out in an
    /// absolute rule is how the rule dies. A folder named <c>Resources</c> inside it satisfies
    /// both that rule and the engine's, and it means the HUD needs no serialised catalogue asset
    /// to find its icons — which matters here because a catalogue is a second thing to keep in
    /// step with the registry, and the project has already been bitten once by an art catalogue
    /// drifting from the code that builds it.</para>
    ///
    /// <para><b>Misses are cached too.</b> Nearly every key in the registry is a miss today, and
    /// <see cref="Resources.Load"/> on a missing path is not free; a badge that retargets on
    /// every frame would pay it every frame.</para>
    /// </summary>
    public static class IconArt
    {
        /// <summary>
        /// The folder, relative to any <c>Resources</c> root.
        ///
        /// <para><b>The namespace is load-bearing.</b> <c>Resources</c> is one flat namespace
        /// shared with every installed package, and a plain <c>icons/</c> collided immediately:
        /// <c>Resources.LoadAll&lt;Texture2D&gt;("icons")</c> returned Shader Graph's own
        /// <c>blackboard</c> at 16 px and bilinear, and the ADR-rule test failed on somebody
        /// else's art. A single-key <c>Load</c> would never have shown it.</para>
        /// </summary>
        public const string Folder = "odyssey/icons";

        static readonly Dictionary<string, Texture2D?> Cache = new();

        /// <summary>The art for a key, or null where the key has none yet.</summary>
        public static Texture2D? For(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (Cache.TryGetValue(key, out Texture2D? cached)) return cached;

            Texture2D? art = Resources.Load<Texture2D>($"{Folder}/{key}");
            Cache[key] = art;
            return art;
        }

        /// <summary>Whether a key draws real art rather than the placeholder square.</summary>
        public static bool Has(string key) => For(key) != null;

        /// <summary>
        /// Forget what has been looked up. For editor tooling that reimports art while the HUD is
        /// alive; a running game never needs it.
        /// </summary>
        public static void Forget() => Cache.Clear();
    }
}
