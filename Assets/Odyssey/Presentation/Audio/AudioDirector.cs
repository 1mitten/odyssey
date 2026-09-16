#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.Audio
{
    /// <summary>
    /// Plays the game's sound: one-shot work sounds from wherever a tool lands, the environment
    /// beds the camera is sitting in, the music the clock says it is, and the alerts the
    /// published frame raises.
    ///
    /// <para><b>A director and not a subsystem</b> (<c>01-architecture.md</c> §3a), the same
    /// standing <see cref="World.ChipDirector">ChipDirector</see> has: presentation coordinating
    /// presentation. No sound is a simulation object, none is in a cell, none is in the save and
    /// none is in the state hash — the simulation has never heard of audio, exactly as it has
    /// never heard of wood chips. What it reads is the published frame and the render mirror,
    /// which is everything ADR 0004 allows presentation to know.</para>
    ///
    /// <para><b>One system for the whole colony.</b> Sixteen pooled voices serve every colonist,
    /// because the sounds are not per-colonist assets, they are moments — a blow lands, a chime
    /// fires — and a pool with priority stealing is the shape Unity's own voice management uses,
    /// applied one layer earlier so the culling (distance, cooldown, priority) happens before a
    /// source is spent rather than after. Creating an <see cref="AudioSource"/> per sound, the
    /// way <see cref="AudioSource.PlayClipAtPoint"/> does, is allocation and destruction on the
    /// frame somebody is listening hardest to.</para>
    ///
    /// <para><b>Everything is driven from <see cref="Sync"/>, on the director's own clock.</b>
    /// The clock is the accumulated delta of that call and nothing else, so a test steps the
    /// same code the game runs, in the same order, with the same numbers — no wall clock, no
    /// <c>Time.time</c>, nothing that differs between play mode and edit mode. A voice is
    /// book-kept busy by its clip's length and the pitch it was played at, not by asking Unity
    /// whether it is still playing, for the same reason.</para>
    ///
    /// <para><b>Buses in code, gains in dB</b> (ADR 0010): there is no mixer asset yet, so bus
    /// volumes multiply into each source's gain with the dB math the mixer would do. One-shots
    /// bake their gain when they spawn; the looping voices — music and the beds — get theirs
    /// reapplied every Sync, which is what makes a volume change or an alert's duck audible in
    /// something already playing.</para>
    /// </summary>
    public sealed class AudioDirector : IDisposable
    {
        /// <summary>
        /// Voices in the pool. Sixteen covers a busy moment — five colonists' tools, a chime, a
        /// bed and the music — with room for a collapse cascade, and the pool steals rather than
        /// grows, so a crowd is a busier mix and never a slower frame.
        /// </summary>
        public const int VoiceCount = 16;

        /// <summary>How long an alert ducks the music, from the moment it plays.</summary>
        public const float DuckSeconds = 2.5f;

        /// <summary>The linear gain the music ducks to. Half-ish, not a dip to nothing.</summary>
        public const float DuckGain = 0.45f;

        readonly AudioCatalogue? _catalogue;
        readonly ITerrainLookup? _terrain;
        readonly GridSize _size;
        readonly GameObject _root;
        readonly AudioSource[] _voices = new AudioSource[VoiceCount];
        readonly double[] _busyUntil = new double[VoiceCount];
        readonly int[] _voicePriority = new int[VoiceCount];
        readonly Dictionary<string, double> _lastPlayed = new();
        readonly System.Random _random = new(20260917);
        readonly float[] _busDb = { 0f, 0f, 0f, 0f, 0f };
        readonly bool[] _busMuted = new bool[5];

        /// <summary>The director's clock: the accumulated <c>deltaTime</c> of every Sync.</summary>
        float _time;

        // The listener's position, taken each Sync. One-shots are culled against it by hand
        // before a voice is spent; Unity's own spatialisation uses the scene's AudioListener,
        // which sits on the same camera.
        Vector3 _listener;

        // Music: two voices, ping-ponged, so a phase change is a crossfade and never a gap.
        readonly AudioSource _musicA;
        readonly AudioSource _musicB;
        AudioSource? _musicCurrent;
        AudioSource? _musicLeaving;
        MusicPhase _phase = MusicPhase.None;
        float _musicVolume;
        float _musicLeavingVolume;
        float _musicFadeSeconds = 3f;
        float _musicElapsed;
        float _musicLeavingElapsed;
        float _duckRemaining;
        float _duckGain = 1f;

        /// <summary>One looping environment bed. Water is the first; the dictionary keyed by the
        /// def's id is the whole generalisation and costs nothing while there is one.</summary>
        sealed class Bed
        {
            public AudioSource? Source;
            public float Level;
        }

        readonly Dictionary<string, Bed> _beds = new();

        readonly AlertWatch _watch = new();

        /// <summary>Diagnostic counters, for the developer overlay and the tests. A test that
        /// wants to know why nothing played reads these instead of guessing.</summary>
        public int OneShotsPlayed { get; private set; }
        public int DistanceCulled { get; private set; }
        public int CooldownSkipped { get; private set; }
        public int VoiceStarved { get; private set; }

        /// <summary>The smoothed water level the bed is playing at, 0 to 1.</summary>
        public float WaterLevel =>
            _beds.TryGetValue(SoundIds.AmbienceWater, out Bed? bed) ? bed.Level : 0f;

        /// <summary>The phase whose track is playing, or fading in to play.</summary>
        public MusicPhase MusicPhase => _phase;

        /// <summary>The current track's live volume, after fade, duck and bus: what the mix is
        /// actually doing, for the tests and the developer overlay.</summary>
        public float MusicVolume => _musicCurrent?.volume ?? 0f;

        /// <summary>
        /// Build the pool under <paramref name="parent"/>, on <paramref name="layer"/> so the
        /// voices live where the figures and chips do.
        ///
        /// A null catalogue or a null terrain is not an error: a clone without the audio assets
        /// gets a working game with no sound — the same bargain the art makes — and a null
        /// terrain simply leaves the beds silent, which is what tests want when they are driving
        /// everything except ambience.
        /// </summary>
        public AudioDirector(
            AudioCatalogue? catalogue, ITerrainLookup? terrain, GridSize size,
            Transform? parent, int layer)
        {
            _catalogue = catalogue;
            _terrain = terrain;
            _size = size;

            _root = new GameObject("Odyssey Audio");
            _root.transform.SetParent(parent, worldPositionStays: false);
            _root.layer = layer;

            for (int i = 0; i < VoiceCount; i++)
            {
                _voices[i] = Voice($"Voice {i}");
                _busyUntil[i] = -1d;
                _voicePriority[i] = int.MaxValue;
            }

            AudioSource Music()
            {
                AudioSource voice = Voice("Music");
                voice.loop = true;
                voice.spatialBlend = 0f; // music is nowhere; it is weather for the mood
                voice.priority = 64;
                voice.dopplerLevel = 0f;
                return voice;
            }

            _musicA = Music();
            _musicB = Music();
        }

        /// <summary>
        /// One voice, on a child object of its own.
        ///
        /// <b>The transform is the whole reason for the child.</b> An <see cref="AudioSource"/>
        /// is spatialised from the transform of the object it sits on, and a transform is one
        /// position however many sources share it: sixteen voices on the pool's own object would
        /// be sixteen sources at whichever place the last one to play wrote — the axe two hundred
        /// metres away would sound from the pick under the camera, and the water bed would drag
        /// every one-shot along with it as the river's centroid moved. A GameObject per voice is
        /// the cost of positional sound; it is paid once, at construction, and never in a frame.
        /// </summary>
        AudioSource Voice(string name)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(_root.transform, worldPositionStays: false);
            holder.layer = _root.layer;
            AudioSource voice = holder.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            return voice;
        }

        /// <summary>
        /// The whole frame's audio: step the clock, lay the ambience, ride the music and its
        /// duck, and raise any alert the published pawn list has crossed into.
        ///
        /// Called once per frame from the composition root's LateUpdate, after the figures have
        /// synced, so a blow that landed this frame sounds on the same frame its chips fly.
        /// </summary>
        /// <param name="deltaTime">Seconds since the last Sync. Drives the director's clock.</param>
        /// <param name="frame">The published world. Read for the pawn list (alerts) and the tick
        /// (music phase); never for anything the mirror owns.</param>
        /// <param name="listener">The camera's position — what "near" means for a one-shot.</param>
        /// <param name="focus">The camera's focus point — what "near" means for an environment.
        /// The focus and not the camera itself, because the camera is tens of metres up in the
        /// air and the water is not.</param>
        /// <param name="activeLayer">The layer the camera is looking at. The probe samples that
        /// layer's terrain: water under the floor of a shaft the player has descended into is
        /// water the ear should not hear.</param>
        public void Sync(float deltaTime, WorldSnapshot frame, Vector3 listener, Vector3 focus, int activeLayer)
        {
            _time += Mathf.Max(0f, deltaTime);
            _listener = listener;

            StepDuck(deltaTime);
            StepAmbience(deltaTime, focus, activeLayer);
            StepMusic(deltaTime, frame.Tick);

            AudioAlert fired = _watch.Step(frame.Pawns);
            if ((fired & AudioAlert.Starving) != 0)
                PlayAlert(SoundIds.AlertStarving);
        }

        /// <summary>
        /// Play a one-shot at a world position: a tool landing, a thing happening, somewhere.
        ///
        /// Returns whether a voice actually played it, because the three ways this can honestly
        /// decline — the sound does not exist in this clone, it is too far from the camera, or
        /// it played too recently — are exactly the things a test (and the developer overlay)
        /// wants to tell apart from each other and from a bug.
        /// </summary>
        public bool PlayOneShot(string id, Vector3 worldPosition)
        {
            AudioCatalogue.SoundDef? def = _catalogue?.Find(id);
            if (def == null) return false;

            // Culled before a voice is spent, by the def's own range: a sound past its max
            // distance is inaudible where it is and would only eat a voice where it is not.
            if (def.SpatialBlend > 0.5f
                && (worldPosition - _listener).sqrMagnitude > def.MaxDistance * def.MaxDistance)
            {
                DistanceCulled++;
                return false;
            }

            // The cooldown is per sound, not per colonist: five woodcutters in earshot of the
            // camera are one rhythm section, not five, and the ear is the arbiter of how many
            // chops a second is believable.
            if (_lastPlayed.TryGetValue(id, out double last) && _time - last < def.Cooldown)
            {
                CooldownSkipped++;
                return false;
            }

            int index = PickVoice(def.Priority);
            if (index < 0)
            {
                VoiceStarved++;
                return false;
            }

            AudioSource voice = _voices[index];

            // The variant choice, made once the sound has earned a voice. A def whose clips are
            // all missing — an asset deleted since the catalogue was built — is silent here
            // rather than an exception at a stranger's volume.
            AudioClip? clip = PickClip(def.Clips);
            if (clip == null) return false;

            float pitch = 1f + Range(-def.PitchVariance, def.PitchVariance);
            float gain = Mathf.Clamp01(
                def.Volume * (1f + Range(-def.VolumeVariance, def.VolumeVariance)))
                * AudioMath.DbToLinear(GainDb(def.Bus));

            voice.clip = clip;
            voice.pitch = pitch;
            voice.volume = gain;
            voice.spatialBlend = def.SpatialBlend;
            voice.minDistance = def.MinDistance;
            voice.maxDistance = def.MaxDistance;
            voice.priority = def.Priority;
            voice.dopplerLevel = 0f; // the camera flies; the world does not
            voice.rolloffMode = AudioRolloffMode.Custom;
            voice.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Rolloff);
            voice.transform.position = worldPosition;
            voice.Play();

            // Busy by arithmetic and not by isPlaying: the bookkeeping stays true in edit mode,
            // where nothing plays, and in a test, which steps the clock by hand.
            _busyUntil[index] = _time + clip.length / pitch + 0.05d;
            _voicePriority[index] = def.Priority;
            _lastPlayed[id] = _time;
            OneShotsPlayed++;
            return true;
        }

        /// <summary>
        /// An alert: the chime, and the duck that makes room for it. Alerts ride their own bus,
        /// are 2D by authorship, and start the duck rather than participate in it.
        /// </summary>
        public void PlayAlert(string id)
        {
            // The duck follows the chime rather than leading it: an alert that was starved of a
            // voice or gated by its own cooldown makes no room, because there is nothing to make
            // room for, and the music dipping for a silence is the one thing a player would hear
            // as a fault.
            if (PlayOneShot(id, _listener)) _duckRemaining = DuckSeconds;
        }

        /// <summary>Put a bus's fader at a dB value. Applies live to the looping voices at the
        /// next Sync and to every one-shot that spawns after it.</summary>
        public void SetBusDb(SoundBus bus, float db) =>
            _busDb[(int)bus] = Mathf.Clamp(db, AudioMath.SilenceDb, AudioMath.UnityDb);

        public float BusDb(SoundBus bus) => _busDb[(int)bus];

        /// <summary>Mute one bus (or master, which mutes everything) without disturbing its
        /// stored fader, so unmuting restores exactly what was there.</summary>
        public void SetBusMuted(SoundBus bus, bool muted) => _busMuted[(int)bus] = muted;

        /// <summary>The gain a sound on <paramref name="bus"/> plays at right now, in dB: its
        /// fader stacked under master's, or silence if either is muted.</summary>
        public float GainDb(SoundBus bus)
        {
            if (_busMuted[(int)SoundBus.Master] || _busMuted[(int)bus])
                return AudioMath.SilenceDb;
            return AudioMath.StackDb(_busDb[(int)SoundBus.Master], _busDb[(int)bus]);
        }

        // ---- the frame, in pieces ----

        void StepDuck(float deltaTime)
        {
            if (_duckRemaining > 0f) _duckRemaining -= deltaTime;
            float target = _duckRemaining > 0f ? DuckGain : 1f;
            // Fast enough that a chime carves its space, slow enough not to pump.
            _duckGain = Mathf.MoveTowards(_duckGain, target, deltaTime / 0.15f);
        }

        void StepAmbience(float deltaTime, Vector3 focus, int activeLayer)
        {
            AudioCatalogue.AmbienceDef? def = _catalogue?.FindAmbience(SoundIds.AmbienceWater);
            if (def?.Clip == null) return;

            Bed bed = BedOf(SoundIds.AmbienceWater);

            AmbienceField field = _terrain != null
                ? AmbienceProbe.Sample(_terrain, _size, activeLayer,
                    new Vector2(focus.x / CellMetrics.SizeXZ, focus.z / CellMetrics.SizeXZ))
                : AmbienceField.Silent;

            // Exponential approach, so each step covers the same fraction of the remaining
            // distance: fast when far, gentle as it arrives. Environment that snapped would read
            // as a switch, not as moving nearer the shore.
            float approach = 1f - Mathf.Exp(-deltaTime / Mathf.Max(0.01f, def.FadeSeconds));
            bed.Level += (field.WaterIntensity - bed.Level) * approach;

            if (bed.Source == null)
            {
                AudioSource source = Voice($"Bed {SoundIds.AmbienceWater}");
                source.loop = true;
                source.clip = def.Clip;
                source.spatialBlend = 0.7f; // a place, mostly; a little spread, so it is not a point
                source.priority = 224;      // first to go virtual when the mix is busy
                source.dopplerLevel = 0f;
                source.minDistance = def.MinDistance;
                source.maxDistance = def.MaxDistance;
                source.volume = 0f;
                source.Play();
                bed.Source = source;
            }

            bed.Source.volume = def.Volume * bed.Level * AudioMath.DbToLinear(GainDb(SoundBus.Ambience));

            if (field.WaterIntensity > 0.001f)
                bed.Source.transform.position = CellMetrics.FloorCentre(
                    (int)field.WaterCentreCell.x, (int)field.WaterCentreCell.y, activeLayer);
        }

        void StepMusic(float deltaTime, long tick)
        {
            MusicPhase phase = MusicClock.PhaseOf(tick);
            if (phase != _phase)
            {
                _phase = phase;
                AudioCatalogue.MusicDef? def = _catalogue?.FindMusic(phase);

                // The outgoing voice, if any, fades out on its own gain; the incoming takes the
                // other slot. Two voices, ping-ponged, so a phase change is a crossfade and
                // never a gap — and a phase with no track fades whatever was playing to nothing
                // rather than cutting it off.
                if (_musicCurrent != null)
                {
                    _musicLeaving = _musicCurrent;
                    _musicLeavingVolume = _musicVolume;
                    _musicLeavingElapsed = 0f;
                }

                if (def?.Clip != null)
                {
                    AudioSource next = ReferenceEquals(_musicCurrent, _musicA) ? _musicB : _musicA;
                    next.clip = def.Clip;
                    next.volume = 0f;
                    next.Play();
                    _musicCurrent = next;
                    _musicVolume = def.Volume;
                    _musicFadeSeconds = def.FadeSeconds;
                    _musicElapsed = 0f;
                }
                else
                {
                    _musicCurrent = null;
                }
            }

            _musicElapsed += deltaTime;

            if (_musicCurrent != null)
            {
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_musicElapsed / _musicFadeSeconds));
                _musicCurrent.volume =
                    _musicVolume * fade * _duckGain * AudioMath.DbToLinear(GainDb(SoundBus.Music));
            }

            if (_musicLeaving != null)
            {
                _musicLeavingElapsed += deltaTime;
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_musicLeavingElapsed / _musicFadeSeconds));
                _musicLeaving.volume = (1f - fade) * _musicLeavingVolume * _duckGain *
                                       AudioMath.DbToLinear(GainDb(SoundBus.Music));
                if (fade >= 1f)
                {
                    _musicLeaving.Stop();
                    _musicLeaving.clip = null;
                    _musicLeaving = null;
                }
            }
        }

        /// <summary>
        /// One variant of a sound, picked at random. A def whose clips have gone missing — a
        /// deleted asset leaves a null slot in the array — falls back to the first live one
        /// rather than throwing at play time.
        /// </summary>
        AudioClip? PickClip(AudioClip[] clips)
        {
            if (clips.Length == 0) return null;
            AudioClip pick = clips[_random.Next(clips.Length)];
            if (pick != null) return pick;
            foreach (AudioClip candidate in clips)
                if (candidate != null) return candidate;
            return null;
        }

        /// <summary>
        /// Find a voice: idle first; otherwise the soonest-done voice this sound outranks; none
        /// at all if every voice is busy with something more important. Unity's own priority
        /// virtualisation, applied before the source is spent instead of after.
        /// </summary>
        int PickVoice(int priority)
        {
            int idle = -1, steal = -1;
            double stealRemaining = double.MaxValue;

            for (int i = 0; i < VoiceCount; i++)
            {
                if (_busyUntil[i] <= _time) { idle = i; break; }
                if (_voicePriority[i] > priority && _busyUntil[i] < stealRemaining)
                {
                    steal = i;
                    stealRemaining = _busyUntil[i];
                }
            }

            return idle >= 0 ? idle : steal;
        }

        /// <summary>
        /// A custom rolloff curve: full to <c>MinDistance</c>, easing to silence at
        /// <c>MaxDistance</c>. Unity's logarithmic rolloff never quite reaches zero — the manual
        /// is explicit about it — which here would mean every axe on the map faintly chopping
        /// forever; the linear option reaches zero but does so with a corner you can hear. This
        /// curve does neither.
        /// </summary>
        static readonly AnimationCurve Rolloff = new(
            new Keyframe(0f, 1f),
            new Keyframe(0.35f, 0.55f),
            new Keyframe(1f, 0f));

        Bed BedOf(string id)
        {
            if (!_beds.TryGetValue(id, out Bed? bed))
            {
                bed = new Bed();
                _beds.Add(id, bed);
            }
            return bed;
        }

        float Range(float low, float high) => low + (float)_random.NextDouble() * (high - low);

        public void Dispose()
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(_root);
            else UnityEngine.Object.DestroyImmediate(_root);
        }
    }

    /// <summary>
    /// The render mirror as an <see cref="ITerrainLookup"/>: one line, but it keeps the probe
    /// free of the mirror's type and the director free of knowing where terrain comes from.
    /// </summary>
    public readonly struct MirrorTerrain : ITerrainLookup
    {
        readonly World.WorldRenderModel _model;

        public MirrorTerrain(World.WorldRenderModel model) => _model = model;

        public ushort TerrainAt(int cellIndex) => _model.Terrain(cellIndex);
    }
}
