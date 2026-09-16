#nullable enable
using System.IO;
using Odyssey.Presentation.Audio;
using UnityEditor;
using UnityEngine;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Builds the placeholder audio: six synthesised WAVs and the catalogue that points the
    /// sound ids at them.
    ///
    /// <para><b>Why synthesise at all.</b> The project licenses no audio, and a sound framework
    /// that cannot be heard cannot be judged — "is the water louder near the water" is a question
    /// only ears answer. The placeholders are deliberately crude (filtered noise, decaying sines,
    /// two chords) and deliberately generated: they are committed assets, they cost nothing in
    /// licences, and when real audio is licensed the catalogue is re-pointed at the new clips and
    /// every one of these files is deleted, with no code change anywhere. The same bargain the
    /// primitive fallback modules make for art.</para>
    ///
    /// <para><b>The import settings are the point of the tool as much as the waves are.</b> Each
    /// clip is imported the way Unity's clip documentation says its class of sound should be:
    /// short impacts as PCM decompressed on load (cheapest to play), the water bed as ADPCM
    /// (noisy, frequent, 3.5× smaller), music as Vorbis streamed from disc (long, continuous,
    /// minimal memory). Real clips that replace these inherit the right settings because they
    /// replace the files, not the importers.</para>
    ///
    /// Headless: <c>scripts/unity.sh exec Odyssey.EditorTools.AudioSetup.Build</c>.
    /// </summary>
    public static class AudioSetup
    {
        internal const string ClipFolder = "Assets/Odyssey/Presentation/Audio/Clips";
        internal const string CataloguePath = PlayScene.AudioCataloguePath;

        const int Rate = 44_100;

        [MenuItem("Odyssey/Presentation/Build audio catalogue")]
        public static void BuildFromMenu() =>
            Build(exitWhenDone: false, overwritePlaceholders: false);

        /// <summary>
        /// Throw away the clips folder's base clips and put the synthesised stand-ins back.
        /// Named for what it does, because under those filenames there may be audio somebody
        /// paid for.
        /// </summary>
        [MenuItem("Odyssey/Presentation/Overwrite audio clips with placeholders")]
        public static void RebuildPlaceholdersFromMenu()
        {
            if (EditorUtility.DisplayDialog(
                    "Overwrite audio clips?",
                    $"This replaces the base clip of every sound in {ClipFolder} with its " +
                    "synthesised placeholder. Sourced audio under those names is lost.",
                    "Overwrite", "Cancel"))
                Build(exitWhenDone: false, overwritePlaceholders: true);
        }

        /// <summary>Batchmode entry point. Exits 0 on success, 1 on failure.</summary>
        public static void Build() =>
            Build(exitWhenDone: Application.isBatchMode, overwritePlaceholders: false);

        static void Build(bool exitWhenDone, bool overwritePlaceholders)
        {
            int exit = 0;
            try
            {
                GenerateClips(overwritePlaceholders);
                BuildCatalogue();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AudioSetup] failed: {e}");
                exit = 1;
            }
            finally
            {
                if (exitWhenDone) EditorApplication.Exit(exit);
            }
        }

        // ---- the clips ----

        /// <summary>
        /// One clip the game expects to find, and how it is to be imported.
        ///
        /// <para>The table is the contract with whoever sources the real audio: a file of this
        /// name in this folder becomes this sound, imported as the class its use demands, with
        /// no code change. <c>docs/reference/audio-sourcing.md</c> is the same table written for
        /// a person holding a sound library.</para>
        /// </summary>
        internal readonly struct ClipSpec
        {
            public readonly string Name;
            public readonly AudioCompressionFormat Format;
            public readonly AudioClipLoadType LoadType;

            /// <summary>Whether the file is flattened to one channel on import. A spatialised
            /// source is placed in the world and has to be mono to be placed at all; music is
            /// nowhere, and keeps the stereo image it was mixed with.</summary>
            public readonly bool Mono;

            public readonly bool LoadInBackground;

            /// <summary>
            /// The synthesised stand-in, used only when no file of this name exists — or
            /// <c>null</c> for a sound the game does without rather than fakes.
            ///
            /// <para>The difference is whether a crude stand-in is better than silence. For a
            /// chop it plainly is: the work needs to be audible to be judged at all. For music it
            /// plainly is not — three sine waves held as a chord is a test tone, it plays
            /// continuously under everything, and what it tells a listener is that something is
            /// broken. Silence is the honest placeholder for music.</para>
            /// </summary>
            public readonly System.Func<float[]>? Placeholder;

            public ClipSpec(string name, AudioCompressionFormat format, AudioClipLoadType loadType,
                bool mono, bool loadInBackground, System.Func<float[]>? placeholder)
            {
                Name = name;
                Format = format;
                LoadType = loadType;
                Mono = mono;
                LoadInBackground = loadInBackground;
                Placeholder = placeholder;
            }
        }

        /// <summary>
        /// Every clip the shipped catalogue names.
        ///
        /// Impacts are PCM decompressed on load, so there is no decode at the moment they play.
        /// The water bed is ADPCM, the manual's own answer for noisy sounds played in quantity.
        /// Music is Vorbis streamed from disc, because decompressed Vorbis costs some ten times
        /// its compressed size in memory and a track is long.
        /// </summary>
        internal static readonly ClipSpec[] Clips =
        {
            new("water", AudioCompressionFormat.ADPCM, AudioClipLoadType.DecompressOnLoad,
                mono: true, loadInBackground: false, () => Water(6.0f)),
            new("chop", AudioCompressionFormat.PCM, AudioClipLoadType.DecompressOnLoad,
                mono: true, loadInBackground: false, Chop),
            new("pick", AudioCompressionFormat.PCM, AudioClipLoadType.DecompressOnLoad,
                mono: true, loadInBackground: false, Pick),
            new("alert", AudioCompressionFormat.PCM, AudioClipLoadType.DecompressOnLoad,
                mono: false, loadInBackground: false, Alert),
            // The forest beds are minutes long, so they stream rather than sit in memory: a
            // three-minute stereo bed decompressed on load is eighteen megabytes of RAM to play
            // something the player is not supposed to notice. Streaming costs ~200 KB a voice.
            new("ambience-day", AudioCompressionFormat.Vorbis, AudioClipLoadType.Streaming,
                mono: false, loadInBackground: true, () => Outdoor(12f, day: true)),
            new("ambience-night", AudioCompressionFormat.Vorbis, AudioClipLoadType.Streaming,
                mono: false, loadInBackground: true, () => Outdoor(12f, day: false)),

            // A fire is a place you stand near, so it is mono. Compressed in memory rather than
            // decompressed, because it is a long loop that plays continuously: ADPCM decodes for
            // almost nothing and keeps a thirty-five-second loop under a megabyte.
            new("campfire", AudioCompressionFormat.ADPCM, AudioClipLoadType.CompressedInMemory,
                mono: true, loadInBackground: false, Fire),
            // No placeholder: see ClipSpec.Placeholder. A phase with no track is a case the
            // director already handles — the bed plays alone and nothing fades in over it.
            new("music-day", AudioCompressionFormat.Vorbis, AudioClipLoadType.Streaming,
                mono: false, loadInBackground: true, placeholder: null),
            new("music-night", AudioCompressionFormat.Vorbis, AudioClipLoadType.Streaming,
                mono: false, loadInBackground: true, placeholder: null),
        };

        /// <summary>How many numbered takes of one sound the tool looks for. The felling
        /// recording split into twenty-four blows, so sixteen was already too few.</summary>
        internal const int MaxVariants = 48;

        /// <summary>
        /// Write a placeholder for every clip that is missing, and set the import class on every
        /// clip that is there.
        ///
        /// <para><b>A file that exists is never overwritten.</b> Real audio arrives under these
        /// names — that is what the naming is for — and a tool that lays its placeholders back
        /// over the top destroys work somebody paid for. Wiping the folder back to the
        /// synthesised stand-ins has to be asked for, and the menu item that asks says so.</para>
        ///
        /// <para>The import settings are applied either way, so a sourced file gets the class its
        /// use demands without anyone remembering the inspector, and so does a variant dropped in
        /// beside its sibling.</para>
        /// </summary>
        static void GenerateClips(bool overwritePlaceholders)
        {
            Directory.CreateDirectory(ClipFolder);

            int written = 0, kept = 0, absent = 0;
            foreach (ClipSpec spec in Clips)
            {
                foreach (string path in PathsFor(spec.Name, includeMissingBase: true))
                {
                    bool isBase = path == BasePath(spec.Name);
                    bool missing = !File.Exists(path);

                    if (missing && spec.Placeholder == null)
                    {
                        absent++;
                        continue;
                    }

                    if (missing || (overwritePlaceholders && isBase))
                    {
                        WriteWav(path, Rate, spec.Placeholder!());
                        written++;
                    }
                    else
                    {
                        kept++;
                    }

                    Import(path, spec.Format, spec.LoadType, spec.Mono, spec.LoadInBackground);
                }
            }

            Debug.Log($"[AudioSetup] {written} placeholder clip(s) written, {kept} existing " +
                      $"clip(s) kept and re-imported, {absent} absent and done without, " +
                      $"in {ClipFolder}.");
        }

        static string BasePath(string name) => $"{ClipFolder}/{name}.wav";

        /// <summary>
        /// Every file standing for one sound: <c>chop.wav</c>, and any <c>chop_01.wav</c>,
        /// <c>chop_02.wav</c> … beside it.
        ///
        /// Variants are how a colony stops sounding like a typewriter, and they are a file rather
        /// than a code change: the director already picks one at random per play, so three takes
        /// of an axe in wood are three files with a suffix and nothing else.
        /// </summary>
        internal static System.Collections.Generic.List<string> PathsFor(
            string name, bool includeMissingBase = false)
        {
            var paths = new System.Collections.Generic.List<string>();

            string root = BasePath(name);
            if (includeMissingBase || File.Exists(root)) paths.Add(root);

            for (int i = 1; i <= MaxVariants; i++)
            {
                string variant = $"{ClipFolder}/{name}_{i:00}.wav";
                if (File.Exists(variant)) paths.Add(variant);
            }

            return paths;
        }

        /// <summary>
        /// A fire: a broad hiss with sharp little cracks through it, at random. Crude, and only
        /// ever reached by a clone that has no recording of one.
        /// </summary>
        static float[] Fire()
        {
            int count = (int)(8f * Rate);
            var random = new System.Random(4_411);

            float[] body = NoiseBand(count, random, 1_400f, 0.05f);
            var samples = new float[count];
            System.Array.Copy(body, samples, count);

            // The cracks. Each is a short decaying burst, thrown down at random and left to ring.
            for (int n = 0; n < 220; n++)
            {
                int at = random.Next(count);
                int length = Rate / 100 + random.Next(Rate / 40);
                float loud = 0.12f + (float)random.NextDouble() * 0.35f;
                for (int i = 0; i < length && at + i < count; i++)
                {
                    float decay = 1f - (float)i / length;
                    samples[at + i] += ((float)random.NextDouble() * 2f - 1f)
                                       * loud * decay * decay;
                }
            }

            for (int i = 0; i < count; i++) samples[i] = Mathf.Clamp(samples[i], -1f, 1f);
            return CrossfadeTail(samples, 1.0f);
        }

        /// <summary>
        /// The outdoor bed: moving air, and something living in it.
        ///
        /// <para>Day is a brighter band of air with a slow swell in it and a sparse, high,
        /// three-note figure standing in for birds. Night drops the air an octave, takes the
        /// birds away and puts a dry pulsing band where the insects are. Neither is remotely
        /// convincing, and neither is meant to be: what they have to establish is that the world
        /// has a floor of sound, that the floor is different after dark, and that the crossfade
        /// between them reads — all of which can be judged from a placeholder, and none of which
        /// can be judged from silence.</para>
        ///
        /// <para>Looped by crossfading the tail into the head, the same way the water bed is, so
        /// there is no seam to hear on a bed that plays for hours.</para>
        /// </summary>
        static float[] Outdoor(float seconds, bool day)
        {
            int count = (int)(seconds * Rate);
            var random = new System.Random(day ? 8_112 : 8_113);

            float[] air = NoiseBand(count, random, day ? 900f : 420f, day ? 0.055f : 0.040f);
            float[] life = NoiseBand(count, random, day ? 2_600f : 5_200f, day ? 0.020f : 0.026f);

            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / Rate;

                // The air breathes: a long swell, and a second one out of step with it, so the
                // pattern does not land on the loop point.
                float breath = 0.72f + 0.28f * Mathf.Sin(2f * Mathf.PI * t / 9.4f)
                                     * Mathf.Cos(2f * Mathf.PI * t / 5.1f);

                // What is alive in it. By day a sparse chirp that is mostly not there; by night a
                // steady pulse, because that is exactly the difference between birds and insects.
                float pulse = day
                    ? Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * t / 3.7f) - 0.86f) * 7f
                    : 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t * 11f);

                samples[i] = air[i] * breath + life[i] * pulse;
            }

            return CrossfadeTail(samples, 1.2f);
        }

        /// <summary>White noise through a one-pole lowpass — the workhorse of every placeholder
        /// that has to sound like a surface rather than a tone.</summary>
        static float[] NoiseBand(int count, System.Random random, float cutoffHz, float gain)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoffHz / Rate);
            var samples = new float[count];
            float state = 0f;
            for (int i = 0; i < count; i++)
            {
                state += a * ((float)random.NextDouble() * 2f - 1f - state);
                samples[i] = state * gain;
            }
            return samples;
        }

        static float[] Water(float seconds)
        {
            var random = new System.Random(20260917);
            int count = (int)(seconds * Rate);
            float[] low = NoiseBand(count, random, 480f, 0.55f);
            float[] mid = NoiseBand(count, random, 2100f, 0.22f);

            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                // Two slow LFOs at whole cycles per loop, so the breathing loops with the noise.
                float breath = 0.75f
                               + 0.15f * Mathf.Sin(2f * Mathf.PI * 2f * i / count)
                               + 0.10f * Mathf.Sin(2f * Mathf.PI * 3f * i / count + 1.7f);
                samples[i] = (low[i] + mid[i]) * breath;
            }

            return CrossfadeTail(samples, 0.25f);
        }

        static float[] Chop()
        {
            int count = (int)(0.22f * Rate);
            var random = new System.Random(1);
            float[] bite = NoiseBand(count, random, 3200f, 1f);

            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / Rate;
                float body = Mathf.Sin(2f * Mathf.PI * 88f * t) * Mathf.Exp(-t * 21f) * 0.75f
                             + Mathf.Sin(2f * Mathf.PI * 176f * t) * Mathf.Exp(-t * 30f) * 0.18f;
                float strike = bite[i] * Mathf.Exp(-t * 190f) * 0.65f;
                samples[i] = Mathf.Clamp((body + strike) * 0.9f, -1f, 1f);
            }
            return samples;
        }

        static float[] Pick()
        {
            int count = (int)(0.28f * Rate);
            var random = new System.Random(2);
            float[] click = NoiseBand(count, random, 6000f, 1f);

            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / Rate;
                float ring = Mathf.Sin(2f * Mathf.PI * 1900f * t) * Mathf.Exp(-t * 11f) * 0.30f
                             + Mathf.Sin(2f * Mathf.PI * 2670f * t) * Mathf.Exp(-t * 7f) * 0.20f
                             + Mathf.Sin(2f * Mathf.PI * 3400f * t) * Mathf.Exp(-t * 4.5f) * 0.12f;
                float hit = click[i] * Mathf.Exp(-t * 480f) * 0.5f;
                samples[i] = Mathf.Clamp(hit + ring, -1f, 1f);
            }
            return samples;
        }

        static float[] Alert()
        {
            int count = (int)(0.6f * Rate);
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / Rate;
                float freq = t < 0.18f ? 830f : 622f;
                // Phase-continuous across the step would be nicer, but a re-articulated second
                // tone is what a chime does anyway.
                float gate = t < 0.18f ? Envelope(t, 0.008f, 0.18f, 0.05f) : Envelope(t - 0.18f, 0.008f, 0.30f, 0.08f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * gate * 0.55f;
            }
            return samples;
        }

        static float[] Music(float seconds, float root, float fifth, float third, float wobbleHz)
        {
            int count = (int)(seconds * Rate);
            var samples = new float[count];

            // Every component is locked to an integer number of cycles over the loop, so the
            // last sample hands to the first without a seam and the 24 s loop is honest.
            float Lock(float hz) => Mathf.Round(hz * seconds) / seconds;

            float a = Lock(root), b = Lock(fifth), c = Lock(third);
            float lfo = Mathf.Round(wobbleHz * seconds) / seconds;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / Rate;
                float swell = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * lfo * t);
                samples[i] = (
                    Mathf.Sin(2f * Mathf.PI * a * t) * 0.34f +
                    Mathf.Sin(2f * Mathf.PI * b * t) * 0.27f +
                    Mathf.Sin(2f * Mathf.PI * c * t) * 0.20f) * swell;
            }
            return samples;
        }

        /// <summary>Blend the buffer's tail into its head so a filter's state does not click at
        /// the loop point. The blended tail is then dropped: the loop shortens by the fade length
        /// and stays seamless, which is the property that matters.</summary>
        static float[] CrossfadeTail(float[] loop, float seconds)
        {
            int fade = (int)(seconds * Rate);
            int kept = loop.Length - fade;
            var samples = new float[kept];
            for (int i = 0; i < kept; i++)
            {
                float head = loop[i];
                if (i < fade) head = Mathf.Lerp(loop[kept + i], head, (float)i / fade);
                samples[i] = head;
            }
            return samples;
        }

        static float Envelope(float t, float attack, float hold, float release)
        {
            if (t < attack) return t / attack;
            if (t < attack + hold) return 1f;
            return Mathf.Max(0f, 1f - (t - attack - hold) / release);
        }

        // ---- the catalogue ----

        static void BuildCatalogue()
        {
            AudioClip? Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{ClipFolder}/{name}.wav");

            // A clip that did not load is a catalogue that is silent at runtime and cheerful at
            // build time: the null goes into the array behind a `!`, every gate in the director
            // passes, and the sound simply never arrives. Fail the build instead — the tool has
            // just written these files, so a null here means the import did not settle and the
            // answer is to run it again, not to ship the table.
            AudioClip Require(string name) =>
                Clip(name) ?? throw new System.InvalidOperationException(
                    $"[AudioSetup] {ClipFolder}/{name}.wav did not import; the catalogue was not written.");

            // Every take of one sound, in the order the folder gives them: the base file and any
            // numbered siblings. Three files named chop.wav, chop_01.wav and chop_02.wav are
            // three variants of the felling blow, and the director picks one per swing.
            AudioClip[] Variants(string name)
            {
                var clips = new System.Collections.Generic.List<AudioClip>();
                foreach (string path in PathsFor(name))
                {
                    AudioClip? clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip != null) clips.Add(clip);
                }

                if (clips.Count == 0)
                    throw new System.InvalidOperationException(
                        $"[AudioSetup] no clip for '{name}' imported from {ClipFolder}; " +
                        "the catalogue was not written.");

                return clips.ToArray();
            }

            var catalogue = AssetDatabase.LoadAssetAtPath<AudioCatalogue>(CataloguePath);
            if (catalogue == null)
            {
                catalogue = ScriptableObject.CreateInstance<AudioCatalogue>();
                AssetDatabase.CreateAsset(catalogue, CataloguePath);
            }

            // **Ranges are distances from the camera, not from the ground.** The AudioListener
            // lives on the camera, which the rig parks 32 m back at its closest and 160 m at its
            // furthest, so a sound authored to die at 48 m — a sensible distance between two
            // people — is a sound that is already faint at the default zoom and culled outright
            // the moment the player pulls back. Every range here is therefore about four times
            // what the same sound would be given in a first-person game. The alternative is to
            // move the ears to the camera's focus, which would let these be ground distances
            // again; it is written up as open in CLAUDE.md.
            catalogue.Sounds.Clear();
            catalogue.Sounds.AddRange(new[]
            {
                new AudioCatalogue.SoundDef
                {
                    Id = SoundIds.WorkChop,
                    Clips = Variants("chop"),
                    Bus = SoundBus.Effects,
                    Volume = 0.85f, VolumeVariance = 0.15f, PitchVariance = 0.07f,
                    SpatialBlend = 1f, MinDistance = 20f, MaxDistance = 200f,
                    Priority = 120, Cooldown = 0.15f,
                },
                new AudioCatalogue.SoundDef
                {
                    Id = SoundIds.WorkPick,
                    Clips = Variants("pick"),
                    Bus = SoundBus.Effects,
                    Volume = 0.8f, VolumeVariance = 0.14f, PitchVariance = 0.06f,
                    SpatialBlend = 1f, MinDistance = 20f, MaxDistance = 210f,
                    Priority = 120, Cooldown = 0.12f,
                },
                new AudioCatalogue.SoundDef
                {
                    Id = SoundIds.AlertStarving,
                    Clips = Variants("alert"),
                    Bus = SoundBus.Alerts,
                    Volume = 0.9f, VolumeVariance = 0f, PitchVariance = 0f,
                    SpatialBlend = 0f, MinDistance = 1f, MaxDistance = 500f,
                    Priority = 16, Cooldown = 2f,
                },
            });

            // The campfire: in the library, played by nothing. A looping sound that belongs to a
            // thing at a place is a kind of emitter the director does not have — see
            // SoundIds.Campfire. The row is here so the day fires arrive the sound is already
            // named, imported and mixed, and the work is whatever plays it.
            catalogue.Sounds.Add(new AudioCatalogue.SoundDef
            {
                Id = SoundIds.Campfire,
                Clips = Variants("campfire"),
                Bus = SoundBus.Ambience,
                Volume = 0.55f, VolumeVariance = 0f, PitchVariance = 0f,
                SpatialBlend = 1f, MinDistance = 15f, MaxDistance = 110f,
                Priority = 180, Cooldown = 0f,
            });

            catalogue.Ambience.Clear();
            catalogue.Ambience.Add(new AudioCatalogue.AmbienceDef
            {
                Id = SoundIds.AmbienceWater,
                Clip = Require("water"),
                Volume = 0.30f, FadeSeconds = 2.5f, MinDistance = 60f, MaxDistance = 300f,
            });

            // The outdoor bed, and it is *quiet*. It is the floor of the mix — the thing you
            // stop hearing and would only notice the absence of — so it has to sit under the
            // work, not beside it. These are about eleven dB down on where they started, which
            // was loud enough to be the loudest thing in the game. Night sits lower still,
            // because the world is quieter after dark and the bed should say so before any
            // individual sound does.
            catalogue.Outdoor.Clear();
            catalogue.Outdoor.AddRange(new[]
            {
                new AudioCatalogue.PhaseTrackDef
                {
                    Phase = MusicPhase.Day, Clip = Require("ambience-day"),
                    Volume = 0.09f, FadeSeconds = 6f, ArrivalFadeSeconds = 10f,
                },
                new AudioCatalogue.PhaseTrackDef
                {
                    Phase = MusicPhase.Night, Clip = Require("ambience-night"),
                    Volume = 0.07f, FadeSeconds = 8f, ArrivalFadeSeconds = 10f,
                },
            });

            // Music is optional. A row is written only when there is a track to point it at, so
            // a project with no music licensed plays the world and nothing else — which is a
            // state the director handles and, until there is real music, the better one.
            catalogue.Music.Clear();
            AddTrack(catalogue.Music, MusicPhase.Day, "music-day", 0.5f, 3f);
            AddTrack(catalogue.Music, MusicPhase.Night, "music-night", 0.42f, 4f);

            void AddTrack(System.Collections.Generic.List<AudioCatalogue.PhaseTrackDef> into,
                MusicPhase phase, string name, float volume, float fade)
            {
                AudioClip? clip = Clip(name);
                if (clip == null) return;
                into.Add(new AudioCatalogue.PhaseTrackDef
                {
                    Phase = phase, Clip = clip, Volume = volume, FadeSeconds = fade,
                });
            }

            EditorUtility.SetDirty(catalogue);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AudioSetup] catalogue at {CataloguePath}: {catalogue.Sounds.Count} sounds, " +
                      $"{catalogue.Ambience.Count} beds, {catalogue.Outdoor.Count} outdoor, " +
                      $"{catalogue.Music.Count} tracks.");
        }

        // ---- plumbing ----

        static void Import(string path, AudioCompressionFormat format, AudioClipLoadType loadType,
            bool mono, bool loadInBackground)
        {
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is not AudioImporter importer) return;

            var settings = importer.defaultSampleSettings;
            settings.loadType = loadType;
            settings.compressionFormat = format;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            // Only where the sound has a position. A spatialised source is placed in the world
            // and a stereo file cannot be placed; music is nowhere, and forcing every clip to
            // mono threw away the image it was mixed with.
            importer.forceToMono = mono;
            importer.loadInBackground = loadInBackground;
            importer.SaveAndReimport();
        }

        /// <summary>A 16-bit PCM mono WAV. Nothing compressed, nothing platform-specific: the
        /// importer decides what the engine actually keeps.</summary>
        static void WriteWav(string path, int sampleRate, float[] samples)
        {
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream);
            int dataBytes = samples.Length * 2;

            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + dataBytes);
            writer.Write("WAVE".ToCharArray());
            writer.Write("fmt ".ToCharArray());
            writer.Write(16);
            writer.Write((short)1);   // PCM
            writer.Write((short)1);   // mono
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);
            writer.Write((short)2);   // block align
            writer.Write((short)16);  // bits per sample
            writer.Write("data".ToCharArray());
            writer.Write(dataBytes);
            for (int i = 0; i < samples.Length; i++)
                writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(samples[i], -1f, 1f) * 32767f));
        }
    }
}
