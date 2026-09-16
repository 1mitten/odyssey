#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Audio;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The director: what plays, what declines to play and why, and what the mix does about it.
    ///
    /// Every test steps the director's own clock through <see cref="AudioDirector.Sync"/> and
    /// asserts on the state of the AudioSources it owns — which in edit mode do not play and
    /// do not advance, which is exactly why the bookkeeping is arithmetic rather than
    /// <c>isPlaying</c>: the same test run under the player loop answers the same way.
    ///
    /// The clips are synthesised in-memory (<see cref="AudioClip.Create"/>) and the catalogue is
    /// built by hand, so nothing here depends on a generated asset existing — the same
    /// property the rest of the suite has of not depending on Synty.
    /// </summary>
    public class AudioDirectorTests
    {
        GameObject _root = null!;
        AudioCatalogue _catalogue = null!;
        AudioClip _chop = null!;
        AudioClip _pick = null!;
        AudioClip _chime = null!;
        AudioClip _water = null!;
        AudioClip _outdoorDay = null!;
        AudioClip _outdoorNight = null!;
        AudioClip _day = null!;
        AudioClip _night = null!;

        /// <summary>Seven in the morning: the first tick of daytime music.</summary>
        const long DayTick = 7 * 2500;

        /// <summary>Eleven at night: safely inside the night phase.</summary>
        const long NightTick = 23 * 2500;

        [SetUp]
        public void Build()
        {
            _root = new GameObject("audio root");

            // 0.1 s at 44.1 kHz; lengths only matter to the voice bookkeeping.
            _chop = AudioClip.Create("chop", 4_410, 1, 44_100, false);
            _pick = AudioClip.Create("pick", 4_410, 1, 44_100, false);
            _chime = AudioClip.Create("chime", 22_050, 1, 44_100, false);
            _water = AudioClip.Create("water", 44_100, 1, 44_100, false);
            _outdoorDay = AudioClip.Create("outdoor-day", 44_100, 1, 44_100, false);
            _outdoorNight = AudioClip.Create("outdoor-night", 44_100, 1, 44_100, false);
            _day = AudioClip.Create("day", 441_000, 1, 44_100, false);
            _night = AudioClip.Create("night", 441_000, 1, 44_100, false);

            _catalogue = ScriptableObject.CreateInstance<AudioCatalogue>();
            _catalogue.Sounds.AddRange(new[]
            {
                new AudioCatalogue.SoundDef
                {
                    Id = SoundIds.WorkChop, Clips = new[] { _chop },
                    Volume = 0.85f, VolumeVariance = 0f, PitchVariance = 0f,
                    SpatialBlend = 1f, MinDistance = 5f, MaxDistance = 48f, Cooldown = 0.15f,
                },
                new AudioCatalogue.SoundDef
                {
                    Id = SoundIds.WorkPick, Clips = new[] { _pick },
                    Volume = 0.8f, VolumeVariance = 0f, PitchVariance = 0f,
                    SpatialBlend = 1f, MinDistance = 5f, MaxDistance = 52f, Cooldown = 0.12f,
                },
                new AudioCatalogue.SoundDef
                {
                    Id = SoundIds.AlertStarving, Clips = new[] { _chime },
                    Bus = SoundBus.Alerts, Volume = 0.9f, SpatialBlend = 0f,
                    Priority = 16, Cooldown = 2f,
                },
            });
            _catalogue.Ambience.Add(new AudioCatalogue.AmbienceDef
            {
                Id = SoundIds.AmbienceWater, Clip = _water, Volume = 0.75f, FadeSeconds = 0.5f,
            });
            _catalogue.Outdoor.AddRange(new[]
            {
                new AudioCatalogue.PhaseTrackDef
                {
                    Phase = MusicPhase.Day, Clip = _outdoorDay, Volume = 0.34f, FadeSeconds = 1f,
                },
                new AudioCatalogue.PhaseTrackDef
                {
                    Phase = MusicPhase.Night, Clip = _outdoorNight, Volume = 0.26f, FadeSeconds = 1f,
                },
            });
            _catalogue.Music.AddRange(new[]
            {
                new AudioCatalogue.PhaseTrackDef { Phase = MusicPhase.Day, Clip = _day, Volume = 0.5f, FadeSeconds = 1f },
                new AudioCatalogue.PhaseTrackDef { Phase = MusicPhase.Night, Clip = _night, Volume = 0.5f, FadeSeconds = 1f },
            });
        }

        [TearDown]
        public void Clear()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_catalogue);
        }

        /// <summary>The surface is layer 1, so layer 0 is underground and the two can be told
        /// apart. <see cref="Advance"/> drives layer 1 unless a test says otherwise.</summary>
        const int Surface = 1;

        AudioDirector Make(ITerrainLookup? terrain = null) =>
            new(_catalogue, terrain, new GridSize(48, 48, 2), _root.transform, 0, Surface);

        static WorldSnapshot Frame(long tick = 0, params PawnView[] pawns)
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite((int)tick, new GridSize(10, 10, 2), 0);
            foreach (PawnView pawn in pawns) snapshot.AddPawn(pawn);
            return snapshot;
        }

        static void Advance(AudioDirector audio, float seconds, float step = 0.05f,
            long tick = 0, Vector3? focus = null, int layer = Surface)
        {
            int steps = Mathf.CeilToInt(seconds / step);
            for (int i = 0; i < steps; i++)
                audio.Sync(step, Frame(tick), Vector3.zero, focus ?? Vector3.zero, layer);
        }

        /// <summary>The voice playing <paramref name="clip"/>, if any: the test's ear.</summary>
        AudioSource? VoicePlaying(AudioClip clip) =>
            Playing().Find(voice => voice.clip == clip);

        List<AudioSource> Playing()
        {
            var voices = new List<AudioSource>(_root.GetComponentsInChildren<AudioSource>(includeInactive: true));
            return voices;
        }

        [Test]
        public void ACloneWithoutACatalogueRunsSilentRatherThanBroken()
        {
            using var audio = new AudioDirector(null, null, new GridSize(8, 8, 2), _root.transform, 0);

            Assert.That(audio.PlayOneShot(SoundIds.WorkChop, Vector3.zero), Is.False);
            audio.Sync(0.05f, Frame(), Vector3.zero, Vector3.zero, 0);

            Assert.That(audio.OneShotsPlayed, Is.Zero);
            Assert.That(audio.MusicVolume, Is.EqualTo(0f));
        }

        [Test]
        public void ABlowPlaysFromWhereItLanded()
        {
            using var audio = Make();
            var where = new Vector3(30f, 0f, 12f);

            Assert.That(audio.PlayOneShot(SoundIds.WorkChop, where), Is.True);

            AudioSource? voice = VoicePlaying(_chop);
            Assert.That(voice, Is.Not.Null, "a played sound must be on a voice");
            Assert.That(voice!.transform.position, Is.EqualTo(where),
                "the sound comes from the edge the chips left, not from the camera");
            Assert.That(voice.volume, Is.EqualTo(0.85f).Within(0.001f),
                "variance is zero in this catalogue, so the gain is the def's volume at bus 0 dB");
        }

        [Test]
        public void TwoBlowsAtOnceComeFromTwoPlacesAndNotFromOne()
        {
            var terrain = new WaterAt(new GridSize(48, 48, 2), 24, 24, 4);
            using var audio = Make(terrain);
            var axe = new Vector3(30f, 0f, 12f);
            var pick = new Vector3(-8f, 3f, -20f);

            Assert.That(audio.PlayOneShot(SoundIds.WorkChop, axe), Is.True);
            Assert.That(audio.PlayOneShot(SoundIds.WorkPick, pick), Is.True);

            // A voice is spatialised from the transform of the object it sits on, so voices that
            // shared one object would both report whichever position was written last.
            Assert.That(VoicePlaying(_chop)!.transform.position, Is.EqualTo(axe),
                "the axe still sounds from the tree");
            Assert.That(VoicePlaying(_pick)!.transform.position, Is.EqualTo(pick),
                "and the pick from the rock");

            // And the bed, which moves to the water's centroid every frame, drags neither.
            Advance(audio, 1f, focus: new Vector3(24f * 2.5f, 0f, 24f * 2.5f));
            Assume.That(audio.WaterLevel, Is.GreaterThan(0.1f), "the bed is up and placed");
            Assert.That(VoicePlaying(_chop)!.transform.position, Is.EqualTo(axe),
                "the river does not move the axe");
        }

        [Test]
        public void ASoundBeyondItsRangeIsCulledBeforeAVoiceIsSpent()
        {
            using var audio = Make();

            Assert.That(audio.PlayOneShot(SoundIds.WorkChop, new Vector3(500f, 0f, 0f)), Is.False);
            Assert.That(audio.DistanceCulled, Is.EqualTo(1));
            Assert.That(audio.OneShotsPlayed, Is.Zero);
            Assert.That(VoicePlaying(_chop), Is.Null, "no voice was spent on the inaudible");
        }

        [Test]
        public void AColonyOfWoodcuttersIsOneRhythmSection()
        {
            using var audio = Make();

            Assert.That(audio.PlayOneShot(SoundIds.WorkChop, Vector3.zero), Is.True);
            Assert.That(audio.PlayOneShot(SoundIds.WorkChop, new Vector3(2f, 0f, 0f)), Is.False,
                "a second blow inside the cooldown is declined");
            Assert.That(audio.CooldownSkipped, Is.EqualTo(1));

            Advance(audio, 0.2f);
            Assert.That(audio.PlayOneShot(SoundIds.WorkChop, new Vector3(2f, 0f, 0f)), Is.True,
                "past the cooldown the rhythm continues");
        }

        [Test]
        public void ABusFaderAtSilenceSilencesWhatItFeeds()
        {
            using var audio = Make();
            audio.SetBusDb(SoundBus.Effects, AudioMath.SilenceDb);

            Assert.That(audio.PlayOneShot(SoundIds.WorkChop, Vector3.zero), Is.True);
            Assert.That(VoicePlaying(_chop)!.volume, Is.EqualTo(0f));

            // Past the cooldown (a fader test is not also a rhythm test), the same sound at the
            // restored fader plays at its own volume again.
            Advance(audio, 0.2f);
            audio.SetBusDb(SoundBus.Effects, 0f);
            Assert.That(audio.PlayOneShot(SoundIds.WorkChop, Vector3.zero), Is.True);
            Assert.That(VoicePlaying(_chop)!.volume, Is.EqualTo(0.85f).Within(0.001f));
        }

        [Test]
        public void MutingMasterMutesWhatIsAlreadyPlaying()
        {
            using var audio = Make();
            Advance(audio, 3f, tick: DayTick);
            Assume.That(audio.MusicVolume, Is.GreaterThan(0.4f), "day music is up first");

            audio.SetBusMuted(SoundBus.Master, true);
            audio.Sync(0.05f, Frame(DayTick), Vector3.zero, Vector3.zero, 0);

            Assert.That(audio.MusicVolume, Is.EqualTo(0f),
                "the loops take their gain live, so a mute is heard in something already playing");
        }

        [Test]
        public void TheWaterBedFadesInNearWaterAndOutAwayFromIt()
        {
            var terrain = new WaterAt(new GridSize(48, 48, 2), 24, 24, 4);
            using var audio = Make(terrain);

            // Sitting beside the pond: the bed approaches the probe's intensity.
            Advance(audio, 3f, focus: new Vector3(24f * 2.5f, 0f, 24f * 2.5f));
            Assert.That(audio.WaterLevel, Is.GreaterThan(0.1f), "water near the focus is heard");

            // A screen away: the same bed fades back out, and does not snap.
            Advance(audio, 8f, focus: new Vector3(4f * 2.5f, 0f, 4f * 2.5f));
            Assert.That(audio.WaterLevel, Is.LessThan(0.02f), "far from the water, silence");
        }

        [Test]
        public void MusicFollowsTheClockAndCrossfadesBetweenPhases()
        {
            using var audio = Make();

            Advance(audio, 3f, tick: NightTick);
            Assert.That(audio.MusicPhase, Is.EqualTo(MusicPhase.Night));
            Assert.That(audio.MusicVolume, Is.GreaterThan(0.4f), "the night track has faded in");

            // Morning: the day track fades in over the night track, not after it.
            Advance(audio, 0.4f, tick: DayTick);
            Assert.That(audio.MusicPhase, Is.EqualTo(MusicPhase.Day));
            int tracksUp = 0;
            foreach (AudioSource voice in Playing())
                if (voice.clip == _day || voice.clip == _night) tracksUp++;
            Assert.That(tracksUp, Is.EqualTo(2),
                "mid-crossfade both voices are carrying: no gap between night and day");

            Advance(audio, 5f, tick: DayTick);
            Assert.That(VoicePlaying(_night), Is.Null,
                "the track that left has stopped and let go of its clip");
        }

        [Test]
        public void APhaseWithNoTrackDoesNotCostTheNextOneItsVoice()
        {
            // A clip that failed to import leaves a phase with no track: the playing track is
            // handed to the fade-out and nothing replaces it. If the phase turns again *before*
            // that fade has finished, the voice picked for the new track must not be the one
            // still fading — it would be both halves of the crossfade at once, and the fade-out
            // would end by stopping the track that is supposed to be playing.
            //
            // The shipped phases are hours apart and the fades are seconds, so this needs a
            // phase shorter than a fade to reach. That is a hazard rather than a live fault,
            // and it is held here because the thing that would create it — a dawn or dusk phase,
            // which the music design already calls "a constant away" — is a constant away.
            _catalogue.Music.Find(track => track.Phase == MusicPhase.Night)!.Clip = null;
            using var audio = Make();

            Advance(audio, 3f, tick: DayTick);
            Advance(audio, 0.3f, tick: NightTick);
            Advance(audio, 3f, tick: DayTick);

            Assert.That(audio.MusicPhase, Is.EqualTo(MusicPhase.Day));
            Assert.That(VoicePlaying(_day), Is.Not.Null,
                "the day track came back on a voice that was not the one fading out");
            Assert.That(audio.MusicVolume, Is.GreaterThan(0.4f),
                "and it is up, not stopped by the fade that belonged to the track it replaced");
        }

        [Test]
        public void ATrackFadesOutAtItsOwnRateAndNotTheOneReplacingIt()
        {
            _catalogue.Music.Find(track => track.Phase == MusicPhase.Day)!.FadeSeconds = 1f;
            _catalogue.Music.Find(track => track.Phase == MusicPhase.Night)!.FadeSeconds = 4f;
            using var audio = Make();

            Advance(audio, 4f, tick: NightTick);
            Assume.That(VoicePlaying(_night), Is.Not.Null, "the night track is up");

            // Morning. The night track owns a four-second fade; the day track's one second is
            // the incoming half of the crossfade and has nothing to do with it.
            Advance(audio, 1.5f, tick: DayTick);

            Assert.That(VoicePlaying(_night), Is.Not.Null,
                "a second and a half into a four-second fade, the night track is still carrying");
            Assert.That(VoicePlaying(_night)!.volume, Is.GreaterThan(0f));
        }

        [Test]
        public void TheWorldHasAFloorOfSoundByDayAndADifferentOneAfterDark()
        {
            using var audio = Make();

            Advance(audio, 3f, tick: DayTick);
            Assert.That(audio.OutdoorPhase, Is.EqualTo(MusicPhase.Day));
            Assert.That(audio.OutdoorLevel, Is.GreaterThan(0.2f),
                "standing outdoors in the day is an audible thing");
            Assert.That(VoicePlaying(_outdoorDay), Is.Not.Null);

            // Dusk. A different world rather than a quieter one, and crossfaded rather than cut.
            Advance(audio, 0.4f, tick: NightTick);
            int bedsUp = 0;
            foreach (AudioSource voice in Playing())
                if (voice.clip == _outdoorDay || voice.clip == _outdoorNight) bedsUp++;
            Assert.That(bedsUp, Is.EqualTo(2), "mid-crossfade both beds carry: no gap at dusk");

            Advance(audio, 4f, tick: NightTick);
            Assert.That(audio.OutdoorPhase, Is.EqualTo(MusicPhase.Night));
            Assert.That(VoicePlaying(_outdoorDay), Is.Null, "the day has gone and let go of its clip");
            Assert.That(audio.OutdoorLevel, Is.GreaterThan(0.1f));
        }

        [Test]
        public void ThereIsNoOutdoorsUnderground()
        {
            using var audio = Make();

            Advance(audio, 3f, tick: DayTick);
            Assume.That(audio.OutdoorLevel, Is.GreaterThan(0.2f), "the bed is up at the surface");

            // Down the shaft. The sky does not follow.
            Advance(audio, 4f, tick: DayTick, layer: Surface - 1);
            Assert.That(audio.OutdoorPhase, Is.EqualTo(MusicPhase.None));
            Assert.That(audio.OutdoorLevel, Is.EqualTo(0f));

            // And back up into it.
            Advance(audio, 4f, tick: DayTick);
            Assert.That(audio.OutdoorPhase, Is.EqualTo(MusicPhase.Day));
            Assert.That(audio.OutdoorLevel, Is.GreaterThan(0.2f));
        }

        [Test]
        public void TheOutdoorBedRidesTheAmbienceFaderAndNotTheMusicOne()
        {
            using var audio = Make();
            Advance(audio, 3f, tick: DayTick);
            float music = audio.MusicVolume;
            Assume.That(audio.OutdoorLevel, Is.GreaterThan(0.2f));

            audio.SetBusDb(SoundBus.Ambience, AudioMath.SilenceDb);
            Advance(audio, 0.1f, tick: DayTick);

            Assert.That(audio.OutdoorLevel, Is.EqualTo(0f), "the ambience fader owns the bed");
            Assert.That(audio.MusicVolume, Is.EqualTo(music).Within(0.001f),
                "and the music is not on that fader");
        }

        [Test]
        public void ADryMapNeverSpinsUpTheWaterLoop()
        {
            using var audio = Make();   // no terrain at all: the clone case, and a map with no water

            Advance(audio, 4f);

            Assert.That(audio.WaterLevel, Is.EqualTo(0f));
            Assert.That(Playing().Find(voice => voice.clip == _water), Is.Null,
                "a loop spinning at volume nought is a real voice spent on silence");
        }

        [Test]
        public void AnAlertChimesAndDucksTheMusic()
        {
            using var audio = Make();
            Advance(audio, 3f, tick: DayTick);
            float before = audio.MusicVolume;
            Assume.That(before, Is.GreaterThan(0.4f));

            // A starving colonist in the published frame: chime + duck, driven end to end.
            var starving = new PawnView(new PawnId(1), new CellRef(1, 1, 0), food: 0, rest: 50, mood: 20);
            audio.Sync(0.05f, Frame(DayTick, starving), Vector3.zero, Vector3.zero, 0);
            audio.Sync(0.5f, Frame(DayTick, starving), Vector3.zero, Vector3.zero, 0);

            Assert.That(audio.OneShotsPlayed, Is.EqualTo(1), "the chime played");
            Assert.That(audio.MusicVolume, Is.LessThan(before * 0.7f),
                "the music stepped out of the chime's way");

            Advance(audio, 6f, tick: DayTick);
            Assert.That(audio.MusicVolume, Is.GreaterThan(before * 0.9f),
                "and came back once the alert had passed");
        }

        /// <summary>Terrain with one circular pond, grass elsewhere.</summary>
        sealed class WaterAt : ITerrainLookup
        {
            readonly ushort[] _terrain;
            readonly GridSize _size;

            public WaterAt(GridSize size, int x, int z, int radius)
            {
                _size = size;
                _terrain = new ushort[size.SizeX * size.SizeZ * size.SizeY];
                for (int i = 0; i < _terrain.Length; i++) _terrain[i] = NaturalContent.TerrainGrass;
                for (int dz = -radius; dz <= radius; dz++)
                for (int dx = -radius; dx <= radius; dx++)
                    if (dx * dx + dz * dz <= radius * radius)
                        _terrain[size.Index(x + dx, z + dz, 0)] = NaturalContent.TerrainShallowWater;
            }

            public ushort TerrainAt(int cellIndex) => _terrain[cellIndex];
        }
    }
}
