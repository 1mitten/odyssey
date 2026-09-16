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

        [MenuItem("Odyssey/Presentation/Build audio placeholders")]
        public static void BuildFromMenu() => Build(exitWhenDone: false);

        /// <summary>Batchmode entry point. Exits 0 on success, 1 on failure.</summary>
        public static void Build() => Build(exitWhenDone: Application.isBatchMode);

        static void Build(bool exitWhenDone)
        {
            int exit = 0;
            try
            {
                GenerateClips();
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

        static void GenerateClips()
        {
            Directory.CreateDirectory(ClipFolder);

            // Water: two bands of low-passed noise, breathing slowly, looped seamlessly by
            // crossfading the tail into the head. A bed, not an event: nothing may happen in it.
            WriteWav($"{ClipFolder}/water.wav", Rate, Water(6.0f));
            Import($"{ClipFolder}/water.wav", AudioCompressionFormat.ADPCM,
                AudioClipLoadType.DecompressOnLoad);

            // A chop: a soft body thump under a short burst of bite. Wood is dull; the pitch
            // variance in the catalogue does the rest of the work.
            WriteWav($"{ClipFolder}/chop.wav", Rate, Chop());
            Import($"{ClipFolder}/chop.wav", AudioCompressionFormat.PCM,
                AudioClipLoadType.DecompressOnLoad);

            // A pick strike: a click and a ring. Stone rings; wood does not.
            WriteWav($"{ClipFolder}/pick.wav", Rate, Pick());
            Import($"{ClipFolder}/pick.wav", AudioCompressionFormat.PCM,
                AudioClipLoadType.DecompressOnLoad);

            // The alert chime: two tones, falling, clean attack and decay so it reads as a chime
            // and not as a beep that was cut off.
            WriteWav($"{ClipFolder}/alert.wav", Rate, Alert());
            Import($"{ClipFolder}/alert.wav", AudioCompressionFormat.PCM,
                AudioClipLoadType.DecompressOnLoad);

            // The music: one chord each, every partial an integer number of cycles in the loop
            // length so the loop point is sample-exact. Day is a major triad, night the same
            // shape a third lower and darker; 24 s each, streamed.
            WriteWav($"{ClipFolder}/music-day.wav", Rate, Music(24f, 130.81f, 196.00f, 329.63f, 0.11f));
            Import($"{ClipFolder}/music-day.wav", AudioCompressionFormat.Vorbis,
                AudioClipLoadType.Streaming, loadInBackground: true);

            WriteWav($"{ClipFolder}/music-night.wav", Rate, Music(24f, 110.00f, 164.81f, 261.63f, 0.07f));
            Import($"{ClipFolder}/music-night.wav", AudioCompressionFormat.Vorbis,
                AudioClipLoadType.Streaming, loadInBackground: true);

            Debug.Log($"[AudioSetup] wrote six placeholder clips to {ClipFolder}.");
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

            var catalogue = AssetDatabase.LoadAssetAtPath<AudioCatalogue>(CataloguePath);
            if (catalogue == null)
            {
                catalogue = ScriptableObject.CreateInstance<AudioCatalogue>();
                AssetDatabase.CreateAsset(catalogue, CataloguePath);
            }

            catalogue.Sounds.Clear();
            catalogue.Sounds.AddRange(new[]
            {
                new AudioCatalogue.SoundDef
                {
                    Id = SoundIds.WorkChop,
                    Clips = new[] { Require("chop") },
                    Bus = SoundBus.Effects,
                    Volume = 0.85f, VolumeVariance = 0.15f, PitchVariance = 0.07f,
                    SpatialBlend = 1f, MinDistance = 5f, MaxDistance = 48f,
                    Priority = 120, Cooldown = 0.15f,
                },
                new AudioCatalogue.SoundDef
                {
                    Id = SoundIds.WorkPick,
                    Clips = new[] { Require("pick") },
                    Bus = SoundBus.Effects,
                    Volume = 0.8f, VolumeVariance = 0.14f, PitchVariance = 0.06f,
                    SpatialBlend = 1f, MinDistance = 5f, MaxDistance = 52f,
                    Priority = 120, Cooldown = 0.12f,
                },
                new AudioCatalogue.SoundDef
                {
                    Id = SoundIds.AlertStarving,
                    Clips = new[] { Require("alert") },
                    Bus = SoundBus.Alerts,
                    Volume = 0.9f, VolumeVariance = 0f, PitchVariance = 0f,
                    SpatialBlend = 0f, MinDistance = 1f, MaxDistance = 500f,
                    Priority = 16, Cooldown = 2f,
                },
            });

            catalogue.Ambience.Clear();
            catalogue.Ambience.Add(new AudioCatalogue.AmbienceDef
            {
                Id = SoundIds.AmbienceWater,
                Clip = Require("water"),
                Volume = 0.75f, FadeSeconds = 2.5f, MinDistance = 30f, MaxDistance = 110f,
            });

            catalogue.Music.Clear();
            catalogue.Music.AddRange(new[]
            {
                new AudioCatalogue.MusicDef { Phase = MusicPhase.Day, Clip = Require("music-day"), Volume = 0.5f, FadeSeconds = 3f },
                new AudioCatalogue.MusicDef { Phase = MusicPhase.Night, Clip = Require("music-night"), Volume = 0.42f, FadeSeconds = 4f },
            });

            EditorUtility.SetDirty(catalogue);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AudioSetup] catalogue at {CataloguePath}: {catalogue.Sounds.Count} sounds, " +
                      $"{catalogue.Ambience.Count} beds, {catalogue.Music.Count} tracks.");
        }

        // ---- plumbing ----

        static void Import(string path, AudioCompressionFormat format, AudioClipLoadType loadType,
            bool loadInBackground = false)
        {
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is not AudioImporter importer) return;

            var settings = importer.defaultSampleSettings;
            settings.loadType = loadType;
            settings.compressionFormat = format;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
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
