#nullable enable
using Odyssey.Hud;
using UnityEngine;

namespace Odyssey.Presentation.Bootstrap
{
    /// <summary>
    /// Graphics preferences, kept on the machine rather than in the colony.
    ///
    /// <para><b>Never in the save.</b> A graphics setting is presentation, exactly like the grass
    /// tufts and the axe chips: no cell, no save section, no hash. Putting it in the colony file
    /// would mean a save carried the machine it was made on, and two players opening one save
    /// would disagree about what the board looks like. It would also, far worse, put a drawing
    /// decision inside the thing the determinism tests compare.</para>
    ///
    /// <para>This is the only class in the project that touches <c>PlayerPrefs</c>, and it exists
    /// so that <see cref="SettingsDirector"/> need not: that assembly is compiled without
    /// UnityEngine so it can run in the fast tier, and an interface is the price of keeping it
    /// there.</para>
    /// </summary>
    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        // Namespaced so a preference cannot collide with anything else this machine has stored,
        // and so the whole set can be found by eye in the registry when one goes wrong.
        const string Prefix = "odyssey.";

        public bool? Read(string key)
        {
            string name = Prefix + key;
            // Absent is not false. A machine that has never been told must fall through to
            // whatever the scene was built with, or every fresh install would silently override
            // the board the scene author configured.
            if (!PlayerPrefs.HasKey(name)) return null;
            return PlayerPrefs.GetInt(name, 1) != 0;
        }

        public void Write(string key, bool value)
        {
            PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
            // Written through rather than left to the editor's own flush, because the common way
            // to end a play session is to press Stop, which is not a clean quit.
            PlayerPrefs.Save();
        }
    }
}
