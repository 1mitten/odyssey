#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Presentation.Audio
{
    /// <summary>
    /// The player's volume settings: one fader per bus, in dB, kept in <c>PlayerPrefs</c>.
    ///
    /// This is the stub of panel B17 ("Settings and keybindings" in the panel catalogue), which
    /// the design already exempts from the intent queue: user settings are not simulation state,
    /// and the binary save — integers only, by design — is the wrong home for them. PlayerPrefs
    /// is Unity's store for exactly this. The graphics switches landed in the same panel from the
    /// other direction and keep their own booleans under <c>odyssey.ui.settings.*</c> through
    /// <c>PlayerPrefsSettingsStore</c>; the two prefixes cannot collide, and the faders belong in
    /// that panel beside them when B17 grows an audio section.
    ///
    /// Volumes are stored as dB rather than 0-to-1 floats because dB is what the buses keep (see
    /// <see cref="AudioMath"/>): what is stored is what is applied, with no conversion to
    /// disagree with itself, and a stored setting survives the day the faders move into a mixer
    /// asset unchanged.
    ///
    /// An instance and not a static: the project has no static mutable state, and a test that
    /// writes volumes should not leak them into the next test through some global.
    /// </summary>
    public sealed class AudioSettingsStore
    {
        const string KeyPrefix = "odyssey.audio.";

        /// <summary>The label each bus is stored under. The five keys are the contract: renaming
        /// one is losing every player's setting for that bus.</summary>
        static readonly Dictionary<SoundBus, string> Keys = new()
        {
            [SoundBus.Master] = KeyPrefix + "master",
            [SoundBus.Music] = KeyPrefix + "music",
            [SoundBus.Ambience] = KeyPrefix + "ambience",
            [SoundBus.Effects] = KeyPrefix + "effects",
            [SoundBus.Alerts] = KeyPrefix + "alerts",
        };

        readonly float[] _db = new float[5];

        /// <summary>A store with every fader at 0 dB — nothing attenuated, nothing boosted.</summary>
        public static AudioSettingsStore Defaults() => new();

        public float Db(SoundBus bus) => _db[(int)bus];

        public void SetDb(SoundBus bus, float db) =>
            _db[(int)bus] = Mathf.Clamp(db, AudioMath.SilenceDb, AudioMath.UnityDb);

        /// <summary>Read the player's faders, falling back to 0 dB for any that were never
        /// written. PlayerPrefs reads outside the player (editor, tests) simply find nothing,
        /// which is the defaults.</summary>
        public static AudioSettingsStore Load()
        {
            var store = new AudioSettingsStore();
            foreach (KeyValuePair<SoundBus, string> pair in Keys)
                if (PlayerPrefs.HasKey(pair.Value))
                    store.SetDb(pair.Key, PlayerPrefs.GetFloat(pair.Value));
            return store;
        }

        public void Save()
        {
            foreach (KeyValuePair<SoundBus, string> pair in Keys)
                PlayerPrefs.SetFloat(pair.Value, _db[(int)pair.Key]);
            PlayerPrefs.Save();
        }

        /// <summary>Put every fader onto a director in one call — the whole of startup.</summary>
        public void ApplyTo(AudioDirector director)
        {
            foreach (SoundBus bus in Keys.Keys)
                director.SetBusDb(bus, Db(bus));
        }
    }
}
