#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Audio;
using Odyssey.Presentation.World;
using UnityEditor;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The swim stroke (design 20 §9): one sound per arm, timed so its loudest moment is the hand
    /// going into the water, heard only with the camera close. The clips are the project's own
    /// (<c>tools/audio/bake_swim.sh</c>), so the catalogue test runs on the runner too.
    /// </summary>
    public class SwimSoundTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/Audio/AudioCatalogue.asset";

        [SetUp, TearDown]
        public void Reset() => SwimPose.Reset();

        static float Cycle => 1f / SwimPose.StrokesPerSecond;

        [Test]
        public void EachArmIsHeardOnceACycle()
        {
            for (int k = 0; k < 7; k++)
            {
                float start = k * 0.37f;
                Assert.That(SwimPose.StrokeSoundsBetween(start, start + Cycle), Is.EqualTo(2),
                    $"a cycle from {start} s did not sound both arms exactly once");
            }
        }

        [Test]
        public void FrameByFrameAddsUpToTheSameStrokes()
        {
            // Sixty frames a second for ten cycles: nothing dropped at a frame boundary, nothing
            // sounded twice.
            int heard = 0;
            float clock = 0f;
            int frames = Mathf.RoundToInt(10f * Cycle * 60f);
            for (int f = 0; f < frames; f++)
            {
                float was = clock;
                clock += 1f / 60f;
                heard += SwimPose.StrokeSoundsBetween(was, clock);
            }
            Assert.That(heard, Is.InRange(19, 21), "ten cycles are twenty strokes");
        }

        [Test]
        public void TheSoundPeaksAsAHandReachesIntoTheWater()
        {
            // Walk the clock finely; each start, plus the file's peak, must land where one arm is
            // fully forward — the stroke's sine at plus or minus one.
            float step = 0.001f;
            int checkedStarts = 0;
            for (float clock = 0f; clock < 3f * Cycle; clock += step)
            {
                if (SwimPose.StrokeSoundsBetween(clock, clock + step) == 0) continue;
                float peak = clock + step + SwimPose.StrokeSoundPeakSeconds;
                float swing = SwimPose.Swing(SwimPose.Phase(peak));
                Assert.That(Mathf.Abs(swing), Is.GreaterThan(0.99f),
                    $"a stroke started at {clock:F3} s peaks with the arms at swing {swing:F3}, not at a reach");
                checkedStarts++;
            }
            Assert.That(checkedStarts, Is.EqualTo(6));
        }

        [Test]
        public void NothingIsHeardWhileTheClockStands()
        {
            Assert.That(SwimPose.StrokeSoundsBetween(1.3f, 1.3f), Is.EqualTo(0), "a paused swimmer splashed");
        }

        [Test]
        public void TheStrokeIsInTheCatalogueAndHeardOnlyClose()
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<AudioCatalogue>(CataloguePath);
            Assert.That(catalogue, Is.Not.Null, "the committed audio catalogue did not load");

            AudioCatalogue.SoundDef? stroke = catalogue!.Sounds.Find(s => s.Id == SoundIds.SwimStroke);
            Assert.That(stroke, Is.Not.Null,
                "no row for the swim stroke — rebuild the audio catalogue (Odyssey > Presentation > Build audio catalogue)");
            Assert.That(stroke!.Clips.Length, Is.EqualTo(3), "three takes, so a crossing is not one sample on a loop");
            foreach (AudioClip clip in stroke.Clips)
            {
                Assert.That(clip, Is.Not.Null, "a swim take did not resolve");
                Assert.That(clip.length, Is.LessThan(1f / SwimPose.StrokesPerSecond * 0.5f + 0.05f),
                    "a take outlasts the gap to the next arm by more than a fade");
            }

            Assert.That(stroke.SpatialBlend, Is.EqualTo(1f), "a stroke is at the swimmer");
            Assert.That(stroke.MaxDistance, Is.LessThanOrEqualTo(40f),
                "heard from further than a close zoom: the camera starts at 48 m");
            Assert.That(stroke.Cooldown, Is.LessThan(0.5f / SwimPose.StrokesPerSecond),
                "the cooldown would swallow one arm of a single swimmer");
        }
    }
}
