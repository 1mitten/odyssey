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

        /// <summary>How fast the music steps out of a chime's way: fast enough to carve its space.</summary>
        public const float DuckAttackSeconds = 0.15f;

        /// <summary>
        /// How long the music takes to come back afterwards. It used to return at the attack's
        /// rate, which the owner heard as the sound snapping to silence and the music switching
        /// back on (2026-09-20); a second's swell reads as the room settling rather than a switch.
        /// </summary>
        public const float DuckReleaseSeconds = 1.0f;

        /// <summary>
        /// The last part of an alert chime is faded out rather than left to stop dead, over this
        /// many seconds. Alerts only: a chop or a pick is a transient and is meant to stop dead.
        /// </summary>
        public const float ChimeTailSeconds = 0.4f;

        /// <summary>
        /// The bed level under which the loop is stopped rather than played quietly. A thousandth
        /// of full is some sixty dB down: inaudible under anything, and the exponential approach
        /// would otherwise never reach nought at all.
        /// </summary>
        public const float AudibleLevel = 0.001f;

        readonly AudioCatalogue? _catalogue;
        readonly ITerrainLookup? _terrain;
        readonly GridSize _size;
        readonly GameObject _root;
        readonly AudioSource[] _voices = new AudioSource[VoiceCount];

        /// <summary>
        /// Looping sounds that belong to a thing at a place, by sound id — the third kind of
        /// emitter, which <c>SoundIds.Campfire</c> has been waiting for since its clip was
        /// imported. Separate voices from the one-shot pool on purpose: a loop holds its voice
        /// for as long as the thing exists, and a fire burning for an hour must not be able to
        /// starve the axe.
        /// </summary>
        readonly Dictionary<string, LoopEmitters> _loops =
            new Dictionary<string, LoopEmitters>(System.StringComparer.Ordinal);
        readonly double[] _busyUntil = new double[VoiceCount];
        readonly int[] _voicePriority = new int[VoiceCount];
        readonly float[] _voiceGain = new float[VoiceCount];
        readonly SoundBus[] _voiceBus = new SoundBus[VoiceCount];
        /// <summary>
        /// The catalogue's sounds by id, built once.
        ///
        /// <para><b>Measured, not assumed.</b> <see cref="AudioCatalogue.Find"/> walks the list
        /// comparing strings, which is nothing at the three sounds the placeholders ship and is
        /// the whole cost at the few hundred real audio brings: forty blows offered in a frame
        /// cost 0.0435 ms against 32 sounds and 0.1527 ms against 252, with the same plays and
        /// the same starves — every bit of the difference the scan. The index makes the lookup
        /// the same price whatever the table's size, which is the property worth having before
        /// the table grows rather than after.</para>
        /// </summary>
        readonly Dictionary<string, AudioCatalogue.SoundDef> _byId =
            new(System.StringComparer.Ordinal);

        /// <summary>The ambience def, resolved once: <see cref="StepAmbience"/> runs every frame
        /// and the lookup it used to do was the same linear scan.</summary>
        readonly AudioCatalogue.AmbienceDef? _waterDef;

        /// <summary>When each sound last played, keyed by the def rather than by its id: the
        /// cooldown gate runs on every offer, and a reference is cheaper to hash than a
        /// thirty-character string.</summary>
        readonly Dictionary<AudioCatalogue.SoundDef, double> _lastPlayed = new();
        readonly System.Random _random = new(20260917);
        readonly float[] _busDb = { 0f, 0f, 0f, 0f, 0f };
        readonly bool[] _busMuted = new bool[5];

        /// <summary>The director's clock: the accumulated <c>deltaTime</c> of every Sync.</summary>
        float _time;

        // The listener's position, taken each Sync. One-shots are culled against it by hand
        // before a voice is spent; Unity's own spatialisation uses the scene's AudioListener,
        // which sits on the same camera.
        Vector3 _listener;

        // The two things the clock changes: the music, and the sound of being outdoors. Both are
        // a looping track per phase, crossfaded on a pair of ping-ponged voices, so both are one
        // class used twice rather than the same forty lines written out again.
        readonly PhaseLoop _music;
        readonly PhaseLoop _outdoor;

        float _duckRemaining;
        float _duckGain = 1f;

        /// <summary>
        /// The layer at which the world stops being indoors-of-the-earth. The outdoor bed is not
        /// heard below it: a player who has followed a shaft down is under the sky, not in it,
        /// and the birds do not come with them. Same rule the water bed follows for its own
        /// layer, and the reason both exist — an environment is a *place*.
        /// </summary>
        readonly int _surfaceLayer;

        /// <summary>One looping environment bed. Water is the first; the dictionary keyed by the
        /// def's id is the whole generalisation and costs nothing while there is one.</summary>
        sealed class Bed
        {
            public AudioSource? Source;
            public float Level;

            /// <summary>Whether the loop is running. Book-kept rather than read off
            /// <c>isPlaying</c>, for the reason every other piece of this director's state is.</summary>
            public bool Playing;
        }

        readonly Dictionary<string, Bed> _beds = new();

        // The rain (design 43 §7): two beds, light and heavy, weighted against each other by
        // RainMix and started and stopped together, so a change of weight is never a restart.
        readonly AudioCatalogue.AmbienceDef? _rainLightDef, _rainHeavyDef;
        AudioSource? _rainLight, _rainHeavy;
        bool _rainPlaying;

        /// <summary>The light rain bed's smoothed weight, before its volume and the bus.</summary>
        public float RainLightLevel { get; private set; }

        /// <summary>The heavy rain bed's smoothed weight, the same.</summary>
        public float RainHeavyLevel { get; private set; }

        /// <summary>The gain the outdoor bed is held at under the rain: 1 on a dry day.</summary>
        public float OutdoorRainGain { get; private set; } = 1f;

        /// <summary>Whether the two rain beds are running. Book-kept, like <see cref="Bed.Playing"/>.</summary>
        public bool RainSounding => _rainPlaying;

        /// <summary>The live volumes of the two rain voices, after weight, catalogue and bus.</summary>
        public float RainLightVolume => _rainLight?.volume ?? 0f;
        public float RainHeavyVolume => _rainHeavy?.volume ?? 0f;


        /// <summary>Diagnostic counters, for the developer overlay and the tests. A test that
        /// wants to know why nothing played reads these instead of guessing.</summary>
        public int OneShotsPlayed { get; private set; }
        public int DistanceCulled { get; private set; }
        public int CooldownSkipped { get; private set; }
        public int VoiceStarved { get; private set; }

        /// <summary>
        /// Sounds that had a def, a voice and the range to be heard, and no clip to play — an
        /// asset deleted or a catalogue written while the import had not settled. Its own
        /// counter because every other reason for silence has one, and a director with all four
        /// at nought used to be indistinguishable from a director nobody ever called.
        /// </summary>
        public int ClipMissing { get; private set; }

        /// <summary>The smoothed water level the bed is playing at, 0 to 1.</summary>
        public float WaterLevel =>
            _beds.TryGetValue(SoundIds.AmbienceWater, out Bed? bed) ? bed.Level : 0f;

        /// <summary>The phase whose track is playing, or fading in to play.</summary>
        public MusicPhase MusicPhase => _music.Phase;

        /// <summary>The current track's live volume, after fade, duck and bus: what the mix is
        /// actually doing, for the tests and the developer overlay.</summary>
        public float MusicVolume => _music.Volume;

        /// <summary>The live volume of the outdoor bed — the day or night sound of the world
        /// itself, under everything else.</summary>
        public float OutdoorLevel => _outdoor.Volume;

        /// <summary>The phase the outdoor bed is playing, or <see cref="Audio.MusicPhase.None"/>
        /// underground, where there is no outdoors to hear.</summary>
        public MusicPhase OutdoorPhase => _outdoor.Phase;

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
            Transform? parent, int layer, int surfaceLayer = 0)
        {
            _catalogue = catalogue;
            _terrain = terrain;
            _size = size;
            _surfaceLayer = surfaceLayer;

            if (catalogue != null)
            {
                foreach (AudioCatalogue.SoundDef def in catalogue.Sounds)
                    _byId[def.Id] = def;
                _waterDef = catalogue.FindAmbience(SoundIds.AmbienceWater);
                _rainLightDef = catalogue.FindAmbience(SoundIds.AmbienceRainLight);
                _rainHeavyDef = catalogue.FindAmbience(SoundIds.AmbienceRainHeavy);
            }

            _root = new GameObject("Odyssey Audio");
            _root.transform.SetParent(parent, worldPositionStays: false);
            _root.layer = layer;

            for (int i = 0; i < VoiceCount; i++)
            {
                _voices[i] = Voice($"Voice {i}");
                _busyUntil[i] = -1d;
                _voicePriority[i] = int.MaxValue;
            }

            // Both loops are 2D. Music is nowhere by nature; the outdoor bed is everywhere,
            // which comes to the same thing — it is the air, not a thing in the air, and giving
            // it a position would make the whole sky sound like it was over there.
            AudioSource Loop(string name, int priority)
            {
                AudioSource voice = Voice(name);
                voice.loop = true;
                voice.spatialBlend = 0f;
                voice.priority = priority;
                voice.dopplerLevel = 0f;
                return voice;
            }

            _music = new PhaseLoop(Loop("Music A", 64), Loop("Music B", 64));
            _outdoor = new PhaseLoop(Loop("Outdoor A", 200), Loop("Outdoor B", 200));
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

            // Set here and not per play: the curve is one shared static instance and assigning it
            // copies it into the native source every time. It is normalised over the source's own
            // maxDistance, so it stays correct whatever a def's range turns out to be.
            voice.rolloffMode = AudioRolloffMode.Custom;
            voice.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Rolloff);
            return voice;
        }

        /// <summary>
        /// The whole frame's audio: step the clock, lay the ambience and ride the music and
        /// its duck.
        ///
        /// <para>Alerts are not raised here. A chime belongs to a row appearing in the alerts
        /// panel, and the panel owns when that is — see <see cref="AlertChimeWatch"/>, which
        /// calls <see cref="PlayAlert"/> from the HUD's own refresh.</para>
        ///
        /// Called once per frame from the composition root's LateUpdate, after the figures have
        /// synced, so a blow that landed this frame sounds on the same frame its chips fly.
        /// </summary>
        /// <param name="deltaTime">Seconds since the last Sync. Drives the director's clock.</param>
        /// <param name="frame">The published world. Read for the tick (music phase); never for
        /// anything the mirror owns.</param>
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
            StepChimeTails();
            StepAmbience(deltaTime, focus, activeLayer);
            StepRain(deltaTime, frame.Weather, activeLayer);
            StepPhaseLoops(deltaTime, frame.Tick, activeLayer);
            StepLandings(frame);
            StepSplashes(frame);
        }

        // ---- things landing (design 23 §6) ---------------------------------------------------

        readonly List<(CellRef cell, int landTick)> _airborne = new();
        readonly List<(CellRef cell, int landTick)> _airborneNow = new();

        /// <summary>
        /// A thing that was in the air last frame and is not this frame has landed, and lands
        /// audibly where the simulation said it would. The frame's tick must have reached the
        /// promised landing tick: a load that vanishes any other way — a fresh world, a save
        /// loaded over this one — did not hit anything.
        /// </summary>
        void StepLandings(WorldSnapshot frame)
        {
            _airborneNow.Clear();
            var falling = frame.Falling;
            for (int i = 0; i < falling.Length; i++) _airborneNow.Add((falling[i].Landing, falling[i].LandTick));

            for (int i = 0; i < _airborne.Count; i++)
            {
                (CellRef cell, int landTick) was = _airborne[i];
                if (_airborneNow.Contains(was) || frame.Tick < was.landTick) continue;
                PlayOneShot(SoundIds.DropLand, CellMetrics.FloorCentre(cell: was.cell));
            }

            _airborne.Clear();
            _airborne.AddRange(_airborneNow);
        }

        // ---- a jump falling short (design 46 §7) ----------------------------------------------

        readonly Dictionary<int, CellRef> _fallingShort = new();
        readonly Dictionary<int, CellRef> _fallingShortNow = new();

        /// <summary>
        /// A pawn that was falling short of a jump last frame and now stands in the water it was
        /// falling into has landed in it. Keyed on the water cell, so a jump dropped by anything
        /// else — an order, a load, a knock — makes no sound, because it never reached the water.
        /// </summary>
        void StepSplashes(WorldSnapshot frame)
        {
            _fallingShortNow.Clear();
            ReadOnlySpan<PawnView> pawns = frame.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                if (pawns[i].JumpingShort) _fallingShortNow[pawns[i].Id.Value] = pawns[i].NextCell;
                else if (_fallingShort.TryGetValue(pawns[i].Id.Value, out CellRef water) && pawns[i].Cell == water)
                    PlayOneShot(SoundIds.Splash,
                        CellMetrics.FloorCentre(water) + Vector3.up * (CellMetrics.SizeY * ChunkMesher.WaterSurface));
            }

            _fallingShort.Clear();
            foreach (var entry in _fallingShortNow) _fallingShort[entry.Key] = entry.Value;
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
            if (!_byId.TryGetValue(id, out AudioCatalogue.SoundDef def)) return false;

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
            if (_lastPlayed.TryGetValue(def, out double last) && _time - last < def.Cooldown)
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
            if (clip == null)
            {
                ClipMissing++;
                return false;
            }

            float pitch = 1f + Range(-def.PitchVariance, def.PitchVariance);
            float gain = Mathf.Clamp01(
                def.Volume * (1f + Range(-def.VolumeVariance, def.VolumeVariance)))
                * AudioMath.DbToLinear(GainDb(def.Bus));

            voice.clip = clip;
            voice.pitch = pitch;
            voice.volume = gain;
            _voiceGain[index] = gain;
            _voiceBus[index] = def.Bus;
            voice.spatialBlend = def.SpatialBlend;
            voice.minDistance = def.MinDistance;
            voice.maxDistance = def.MaxDistance;
            voice.priority = def.Priority;
            voice.dopplerLevel = 0f; // the camera flies; the world does not
            voice.transform.position = worldPosition;
            voice.Play();

            // Busy by arithmetic and not by isPlaying: the bookkeeping stays true in edit mode,
            // where nothing plays, and in a test, which steps the clock by hand.
            _busyUntil[index] = _time + clip.length / pitch + BusyPadSeconds;
            _voicePriority[index] = def.Priority;
            _lastPlayed[def] = _time;
            OneShotsPlayed++;
            return true;
        }

        /// <summary>
        /// Bring one looping sound's voices into line with the things making it.
        ///
        /// <para>Called once a frame by whoever owns those things — <c>FireDirector</c> for
        /// campfires — with everything of that kind that is currently <b>drawn</b>. Visibility is
        /// the caller's business, not this class's: a fire on a hidden storey is not audible for
        /// the same reason it is not lit, and the caller is the one that already knows.</para>
        ///
        /// <para>An unknown id, or one whose clips are all missing, is silent rather than an
        /// exception — the same bargain <see cref="PlayOneShot"/> makes, and the reason the game
        /// runs with no clips at all.</para>
        /// </summary>
        /// <returns>How many voices are sounding.</returns>
        public int SyncLoops(string id, IReadOnlyList<LoopPoint> points)
        {
            if (!_byId.TryGetValue(id, out AudioCatalogue.SoundDef def)) return 0;

            if (!_loops.TryGetValue(id, out LoopEmitters pool))
            {
                pool = new LoopEmitters();
                _loops[id] = pool;
            }

            if (points.Count == 0)
            {
                pool.StopAll();
                return 0;
            }

            AudioClip? clip = PickClip(def.Clips);
            if (clip == null)
            {
                ClipMissing++;
                pool.StopAll();
                return 0;
            }

            pool.Sync(points, _listener,
                () => Voice($"Loop {id}"),
                source =>
                {
                    source.clip = clip;
                    source.pitch = 1f;
                    source.volume = Mathf.Clamp01(def.Volume) * AudioMath.DbToLinear(GainDb(def.Bus));
                    source.spatialBlend = def.SpatialBlend;
                    source.minDistance = def.MinDistance;
                    source.maxDistance = def.MaxDistance;
                    source.priority = def.Priority;
                    source.dopplerLevel = 0f;
                });

            return pool.Sounding;
        }

        /// <summary>How many voices one looping sound has going. Diagnostic; a test counts it.</summary>
        public int LoopsSounding(string id) =>
            _loops.TryGetValue(id, out LoopEmitters pool) ? pool.Sounding : 0;

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

        /// <summary>Put a bus's fader at a dB value, silence to the boost ceiling. Applies live
        /// to the looping voices at the next Sync and to every one-shot that spawns after
        /// it.</summary>
        public void SetBusDb(SoundBus bus, float db) =>
            _busDb[(int)bus] = Mathf.Clamp(db, AudioMath.SilenceDb, AudioMath.BoostDb);

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
            // Down fast, so a chime carves its space; back up slowly, so the room settles
            // rather than switches. The two rates are the constants' own comments.
            float seconds = target < _duckGain ? DuckAttackSeconds : DuckReleaseSeconds;
            _duckGain = Mathf.MoveTowards(_duckGain, target, deltaTime / seconds);
        }

        /// <summary>
        /// Fade the last <see cref="ChimeTailSeconds"/> of every alert voice, so a chime ends
        /// as a decay and not as a cut. The end is the voice's own bookkeeping — the clip's
        /// length at its pitch — and the gain it fades from is the one it was played at, so a
        /// fader move during the tail is applied on top rather than fought.
        /// </summary>
        void StepChimeTails()
        {
            for (int i = 0; i < VoiceCount; i++)
            {
                if (_voiceBus[i] != SoundBus.Alerts || _busyUntil[i] <= _time) continue;
                double remaining = _busyUntil[i] - BusyPadSeconds - _time;
                if (remaining >= ChimeTailSeconds) continue;
                _voices[i].volume = _voiceGain[i] * Mathf.Clamp01((float)(remaining / ChimeTailSeconds));
            }
        }

        /// <summary>The slack past a clip's end that a voice stays booked for.</summary>
        const double BusyPadSeconds = 0.05d;

        void StepAmbience(float deltaTime, Vector3 focus, int activeLayer)
        {
            AudioCatalogue.AmbienceDef? def = _waterDef;
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

            // A bed is only made, and only kept running, while there is something to hear. A map
            // with the water generator switched off still carries the water def, and a loop
            // spinning at volume nought for the whole session is a decoded stream and one of the
            // platform's real voices spent on silence.
            bool audible = bed.Level > AudibleLevel || field.WaterIntensity > 0f;
            if (bed.Source == null && !audible) return;

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
                bed.Source = source;
            }

            if (audible && !bed.Playing)
            {
                bed.Source.Play();
                bed.Playing = true;
            }
            else if (!audible && bed.Playing)
            {
                bed.Source.Stop();
                bed.Playing = false;
            }

            bed.Source.volume = def.Volume * bed.Level * AudioMath.DbToLinear(GainDb(SoundBus.Ambience));

            if (field.WaterIntensity > 0.001f)
                bed.Source.transform.position = CellMetrics.FloorCentre(
                    (int)field.WaterCentreCell.x, (int)field.WaterCentreCell.y, activeLayer);
        }

        /// <summary>
        /// The rain (design 43 §7): <see cref="RainMix"/> says how much of each bed the published
        /// sky wants, and each level eases towards it over the light bed's
        /// <see cref="AudioCatalogue.AmbienceDef.FadeSeconds"/>, so a forced change of sky swells
        /// rather than steps. The same easing carries the outdoor bed's hush.
        ///
        /// <para><b>Both beds run whenever either is heard.</b> They start on the same frame and
        /// stop on the same frame, and between those only their volumes move — so the rain moving
        /// from light to heavy is a change of weight inside one continuous sound, never a clip
        /// starting. Each is 2D: rain is the air all round, like the outdoor bed, not a thing at
        /// a place like the water. Underground, <see cref="RainMix"/> answers silence, and the
        /// beds fade out on the ordinary path.</para>
        /// </summary>
        void StepRain(float deltaTime, in WeatherView sky, int activeLayer)
        {
            RainLevels target = RainMix.Of(sky, outdoors: activeLayer >= _surfaceLayer);

            float fade = _rainLightDef?.FadeSeconds ?? _rainHeavyDef?.FadeSeconds ?? 3f;
            float approach = 1f - Mathf.Exp(-deltaTime / Mathf.Max(0.01f, fade));
            RainLightLevel += (target.Light - RainLightLevel) * approach;
            RainHeavyLevel += (target.Heavy - RainHeavyLevel) * approach;
            OutdoorRainGain += (target.OutdoorGain - OutdoorRainGain) * approach;

            if (_rainLightDef?.Clip == null && _rainHeavyDef?.Clip == null) return;

            // Kept running while either is heard or either is wanted; a bed spinning at nought for
            // a whole dry week is a decoded stream and a real voice spent on silence.
            bool audible = RainLightLevel > AudibleLevel || RainHeavyLevel > AudibleLevel ||
                           target.Light > 0f || target.Heavy > 0f;

            if (audible && !_rainPlaying)
            {
                _rainLight ??= RainVoice("Rain light", _rainLightDef);
                _rainHeavy ??= RainVoice("Rain heavy", _rainHeavyDef);
                if (_rainLight?.clip != null) _rainLight.Play();
                if (_rainHeavy?.clip != null) _rainHeavy.Play();
                _rainPlaying = true;
            }
            else if (!audible && _rainPlaying)
            {
                _rainLight?.Stop();
                _rainHeavy?.Stop();
                _rainPlaying = false;
            }

            float bus = AudioMath.DbToLinear(GainDb(SoundBus.Ambience));
            if (_rainLight != null && _rainLightDef != null)
                _rainLight.volume = Mathf.Clamp01(_rainLightDef.Volume * RainLightLevel * bus);
            if (_rainHeavy != null && _rainHeavyDef != null)
                _rainHeavy.volume = Mathf.Clamp01(_rainHeavyDef.Volume * RainHeavyLevel * bus);
        }

        /// <summary>One 2D looping rain voice, made on the first rain of the session.</summary>
        AudioSource? RainVoice(string name, AudioCatalogue.AmbienceDef? def)
        {
            if (def?.Clip == null) return null;
            AudioSource source = Voice(name);
            source.loop = true;
            source.clip = def.Clip;
            source.spatialBlend = 0f;
            source.priority = 200;      // with the outdoor bed: the air, and first to go virtual
            source.dopplerLevel = 0f;
            source.volume = 0f;
            return source;
        }

        /// <summary>
        /// The two loops the clock drives: the music, and the sound of the world outdoors.
        ///
        /// <para>The catalogue is only consulted when the phase actually turns — twice a day —
        /// rather than every frame, because a lookup that answers the same thing 60 times a
        /// second is the cost the id index was added to stop paying.</para>
        /// </summary>
        void StepPhaseLoops(float deltaTime, long tick, int activeLayer)
        {
            MusicPhase phase = MusicClock.PhaseOf(tick);

            _music.Step(deltaTime, phase,
                phase != _music.Phase ? _catalogue?.FindMusic(phase) : null,
                _duckGain * AudioMath.DbToLinear(GainDb(SoundBus.Music)));

            // Underground there is no outdoors: the phase goes to None, which has no track, and
            // the bed fades out on the ordinary path rather than through a case of its own.
            MusicPhase outdoorPhase = activeLayer < _surfaceLayer ? MusicPhase.None : phase;

            // Under the rain the birds step back (RainMix): the hush is a gain on the bed, so it
            // rides the same fades and the same bus, and a dry day is exactly the old mix.
            _outdoor.Step(deltaTime, outdoorPhase,
                outdoorPhase != _outdoor.Phase ? _catalogue?.FindOutdoor(outdoorPhase) : null,
                OutdoorRainGain * AudioMath.DbToLinear(GainDb(SoundBus.Ambience)));
        }

        /// <summary>
        /// A looping track per clock phase, crossfaded on two ping-ponged voices.
        ///
        /// <para>Two voices and not one, so a phase change is a crossfade and never a gap. The
        /// voice taken for the incoming track is the one that is <b>not</b> fading out: picking
        /// "the other one from current" aliases them the moment a phase has no track at all, and
        /// then the fade-out ends by stopping the track that is supposed to be playing.</para>
        ///
        /// <para>The leaving track keeps its own fade length. They differ — the shipped music
        /// fades over three seconds by day and four by night — and a single shared field
        /// stretched every fade-out to the length of the fade-in that replaced it.</para>
        /// </summary>
        sealed class PhaseLoop
        {
            readonly AudioSource _a;
            readonly AudioSource _b;

            AudioSource? _current;
            AudioSource? _leaving;

            float _volume, _fadeSeconds = 3f, _elapsed;
            float _leavingVolume, _leavingFadeSeconds = 3f, _leavingElapsed;

            /// <summary>Whether nothing has played on this loop yet. The first fade of a session
            /// is an arrival and may be slower than any later change of phase.</summary>
            bool _arriving = true;

            public PhaseLoop(AudioSource a, AudioSource b)
            {
                _a = a;
                _b = b;
            }

            /// <summary>The phase whose track is playing, or fading in to play.</summary>
            public MusicPhase Phase { get; private set; } = MusicPhase.None;

            /// <summary>What the playing voice's gain actually is, after fade and bus.</summary>
            public float Volume => _current?.volume ?? 0f;

            /// <param name="def">The track for <paramref name="phase"/> — read only when the
            /// phase has turned, and null both when it has not and when the phase has no track,
            /// which are the same thing as far as this class is concerned: nothing new starts.</param>
            /// <param name="gain">Everything outside the fade: bus, mute and any duck.</param>
            public void Step(float deltaTime, MusicPhase phase,
                AudioCatalogue.PhaseTrackDef? def, float gain)
            {
                if (phase != Phase)
                {
                    Phase = phase;

                    if (_current != null)
                    {
                        _leaving = _current;
                        _leavingVolume = _volume;
                        _leavingElapsed = 0f;
                        _leavingFadeSeconds = _fadeSeconds;
                    }

                    if (def?.Clip != null)
                    {
                        AudioSource next = ReferenceEquals(_leaving, _a) ? _b : _a;
                        next.clip = def.Clip;
                        next.volume = 0f;
                        next.Play();
                        _current = next;
                        _volume = def.Volume;
                        _fadeSeconds = def.FadeFor(_arriving);
                        _elapsed = 0f;
                        _arriving = false;
                    }
                    else
                    {
                        _current = null;
                    }
                }

                _elapsed += deltaTime;

                if (_current != null)
                {
                    float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_elapsed / _fadeSeconds));
                    _current.volume = _volume * fade * gain;
                }

                if (_leaving == null) return;

                _leavingElapsed += deltaTime;
                float out_ = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(_leavingElapsed / _leavingFadeSeconds));
                _leaving.volume = (1f - out_) * _leavingVolume * gain;

                if (out_ < 1f) return;

                _leaving.Stop();
                _leaving.clip = null;
                _leaving = null;
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
