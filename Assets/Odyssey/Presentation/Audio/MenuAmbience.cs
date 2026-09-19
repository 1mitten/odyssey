#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.Audio
{
    /// <summary>
    /// The bed behind the title screen, and the one sound in the game that is not part of a
    /// colony.
    ///
    /// <para><b>Why it is not in <see cref="AudioDirector"/>.</b> The director is built when a
    /// world is, out of that world's grid size, terrain mirror and surface layer, and the
    /// composition root's frame loop returns before touching it while <c>_world</c> is null. The
    /// menu is precisely the state in which there is no world. Rather than make the director
    /// constructible without one — which would mean a half-built director whose beds and probe
    /// are waiting for a second initialisation nobody can see in the constructor — this is one
    /// looping voice with a fade, which is all a title screen wants.</para>
    ///
    /// <para><b>It reads the player's faders rather than keeping any.</b> The stored volumes in
    /// <see cref="AudioSettingsStore"/> are the one owner; this loads them each step while it is
    /// audible, so the Music slider on the settings page moves the bed under the page it is
    /// drawn on. A second copy of the five bus faders would be a second owner of a rule and this
    /// project has paid for that mistake more than once — see <c>docs/bug-patterns.md</c> P1.
    /// Loading is five PlayerPrefs lookups against an in-memory dictionary, on a menu, and it
    /// stops entirely once a world exists.</para>
    ///
    /// <para><b>Arriving and leaving are different lengths.</b> The bed fades in over
    /// <c>ArrivalFadeSeconds</c> — long, because it should already be the air by the time the
    /// player has read the menu — and out over <c>FadeSeconds</c>, which is short enough to be
    /// gone before the world it is handing over to has finished arriving. The outdoor bed's own
    /// arrival fade runs at the same time, so the two cross rather than queue.</para>
    /// </summary>
    public sealed class MenuAmbience : System.IDisposable
    {
        readonly AudioCatalogue.PhaseTrackDef? _def;
        readonly GameObject? _root;
        readonly AudioSource? _source;

        /// <summary>The player's stored faders, re-read while the bed is audible. Never written
        /// to: the settings panel owns them.</summary>
        AudioSettingsStore _faders = AudioSettingsStore.Defaults();

        float _level;
        bool _arrived;

        /// <summary>Whether the bed has ever reached full. The long arrival fade is used until
        /// it has, and the ordinary one every time after — quitting to the menu is the clock
        /// turning over, not the game arriving.</summary>
        public bool Arrived => _arrived;

        /// <summary>The bed's live volume after fade and bus, 0 to 1 — what the mix is actually
        /// doing, for the tests and the developer overlay.</summary>
        public float Level => _level;

        /// <summary>Whether a voice exists at all. False in a clone with no audio assets, and
        /// false in every headless test, both of which are silent and fine.</summary>
        public bool HasVoice => _source != null;

        /// <summary>
        /// Build the voice under <paramref name="parent"/>. A null catalogue, or a catalogue with
        /// no menu row, yields an object that does nothing at all — the same bargain every other
        /// piece of presentation makes about assets that are not there.
        /// </summary>
        public MenuAmbience(AudioCatalogue? catalogue, Transform? parent, int layer)
        {
            _def = catalogue?.Menu;
            if (_def?.Clip == null) return;

            _root = new GameObject("Odyssey Menu Bed");
            _root.transform.SetParent(parent, worldPositionStays: false);
            _root.layer = layer;

            _source = _root.AddComponent<AudioSource>();
            _source.clip = _def.Clip;
            _source.loop = true;
            _source.playOnAwake = false;
            // 2D: a title screen is nowhere. Priority high (low urgency) because nothing about a
            // menu should ever take a voice from the world it is fading into.
            _source.spatialBlend = 0f;
            _source.priority = 200;
            _source.volume = 0f;
        }

        /// <summary>
        /// One frame. <paramref name="wanted"/> is whether the bed should be playing at all —
        /// true on the menus, false the instant a world exists.
        /// </summary>
        /// <param name="deltaTime">Unscaled seconds. The fade is a piece of interface and must
        /// not care that the game behind it is paused, or running at six times speed.</param>
        public void Sync(float deltaTime, bool wanted)
        {
            if (_source == null || _def == null) return;

            float target = wanted ? 1f : 0f;
            float fade = wanted ? _def.FadeFor(!_arrived) : _def.FadeSeconds;

            _level = fade <= 1e-4f
                ? target
                : Mathf.MoveTowards(_level, target, Mathf.Max(0f, deltaTime) / fade);

            // Latched on *reaching* full, not on being synced once. The first version set it
            // on the first frame, so the eight-second arrival governed one sixtieth of a second
            // and the other 7.98 ran at the four-second leaving fade — a bed that was up in half
            // the time it was written to take, which only a test that watched it part-way
            // through could see.
            if (wanted && _level >= 1f) _arrived = true;

            if (_level <= AudioDirector.AudibleLevel && !wanted)
            {
                // Nothing to hear and nothing coming: stop the voice rather than spin it at
                // nought. The same rule the world's own beds follow, and the reason is the same —
                // a streamed voice at zero volume is still decoding.
                if (_source.isPlaying) _source.Stop();
                _level = 0f;
                return;
            }

            _faders = AudioSettingsStore.Load();
            _source.volume = _level * _def.Volume * AudioMath.DbToLinear(GainDb());

            if (!_source.isPlaying) _source.Play();
        }

        /// <summary>Stop at once, with no fade — quitting, or the scene going away.</summary>
        public void Silence()
        {
            _level = 0f;
            if (_source != null)
            {
                _source.volume = 0f;
                if (_source.isPlaying) _source.Stop();
            }
        }

        /// <summary>Master under Music, exactly as the director stacks the same two buses.</summary>
        float GainDb() =>
            AudioMath.StackDb(_faders.Db(SoundBus.Master), _faders.Db(SoundBus.Music));

        public void Dispose()
        {
            Silence();
            if (_root == null) return;
            if (Application.isPlaying) Object.Destroy(_root);
            else Object.DestroyImmediate(_root);
        }
    }
}
