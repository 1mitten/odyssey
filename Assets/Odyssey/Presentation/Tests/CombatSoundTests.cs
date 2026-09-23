#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Audio;
using UnityEditor;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The sound of a blow (design 33 §9g), as the committed catalogue ships it, and the clips
    /// checked against the offsets <see cref="CombatSoundTiming"/> schedules by. The clips are the
    /// project's own (baked by <c>tools/audio/bake_combat.sh</c> and committed), so this runs on
    /// the runner too. <b>Red until <c>Odyssey.EditorTools.AudioSetup.Build</c> has been run</b>
    /// over the new clips: the rows are written by it.
    /// </summary>
    public class CombatSoundTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/Audio/AudioCatalogue.asset";

        static AudioCatalogue.SoundDef Row(string id)
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<AudioCatalogue>(CataloguePath);
            Assert.That(catalogue, Is.Not.Null, "the committed audio catalogue did not load");
            AudioCatalogue.SoundDef? row = catalogue!.Sounds.Find(s => s.Id == id);
            Assert.That(row, Is.Not.Null, $"no row for {id}: that part of a blow would be silent");
            Assert.That(row!.Clips.Length, Is.GreaterThan(0));
            foreach (AudioClip clip in row.Clips)
                Assert.That(clip, Is.Not.Null, $"{id} points at a clip that did not resolve");
            return row;
        }

        [Test]
        public void EveryPartOfABlowIsPlacedInTheWorldLikeTheAxe()
        {
            foreach (string id in new[] { SoundIds.CombatSwing, SoundIds.CombatCritSlice, SoundIds.CombatHit })
            {
                AudioCatalogue.SoundDef row = Row(id);
                Assert.That(row.SpatialBlend, Is.EqualTo(1f), $"{id} is not placed in the world");
                Assert.That(row.Bus, Is.EqualTo(SoundBus.Effects), id);
                // A cooldown swallows a second blow in the same frame, and in a group fight a
                // second blow in the same frame is a real blow.
                Assert.That(row.Cooldown, Is.EqualTo(0f), $"{id} would swallow a second fighter's blow");
                foreach (AudioClip clip in row.Clips)
                    Assert.That(clip.channels, Is.EqualTo(1), $"{clip.name} is not mono and cannot be placed");
            }

            Assert.That(Row(SoundIds.CombatSwing).Clips.Length, Is.EqualTo(2), "the owner supplied two whooshes");
            Assert.That(Row(SoundIds.CombatSwing).PitchVariance, Is.GreaterThan(0f));
            Assert.That(Row(SoundIds.CombatCritSlice).PitchVariance, Is.EqualTo(0f),
                "a pitched slice moves its peak off the impact");
        }

        [Test]
        public void EachClipIsLoudestWhereTheTimingSaysItIs()
        {
            // Within 5 ms: the bake measures on a 2 ms grid, and a frame is 7 to 17 ms.
            AssertLoudestAt(Row(SoundIds.CombatSwing).Clips, CombatSoundTiming.WhooshPeakSeconds);
            AssertLoudestAt(Row(SoundIds.CombatCritSlice).Clips, CombatSoundTiming.SlicePeakSeconds);
            AssertLoudestAt(Row(SoundIds.CombatHit).Clips, CombatSoundTiming.ThudPeakSeconds);
        }

        static void AssertLoudestAt(AudioClip[] clips, float expected)
        {
            foreach (AudioClip clip in clips)
            {
                var samples = new float[clip.samples * clip.channels];
                Assert.That(clip.GetData(samples, 0), Is.True, $"{clip.name}'s samples could not be read");

                int window = Mathf.Max(1, clip.frequency / 100);
                double power = 0, best = -1;
                int bestStart = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    power += samples[i] * samples[i];
                    if (i >= window) power -= samples[i - window] * samples[i - window];
                    if (i >= window - 1 && power > best)
                    {
                        best = power;
                        bestStart = i - window + 1;
                    }
                }

                float middle = (bestStart + window * 0.5f) / clip.frequency;
                Assert.That(middle, Is.EqualTo(expected).Within(0.005f),
                    $"{clip.name} is loudest at {middle:0.000} s, not {expected:0.000} s: re-measure and change CombatSoundTiming");
            }
        }
    }
}
