#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Audio;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The title screen's bed: when it plays, when it stops, and how long each takes.
    ///
    /// <para>Asserted on <see cref="MenuAmbience.Level"/> — the fade before the buses — and never
    /// on the AudioSource's own volume, which is the fade times the player's stored faders. A
    /// test that read the source would pass or fail on whatever volume the machine running it
    /// happens to have saved in PlayerPrefs, which is not a property of this code.</para>
    /// </summary>
    public class MenuAmbienceTests
    {
        GameObject _root = null!;
        AudioClip _bed = null!;
        AudioCatalogue _catalogue = null!;

        const float Arrival = 8f;
        const float Leaving = 4f;

        [SetUp]
        public void Build()
        {
            _root = new GameObject("menu bed root");
            _bed = AudioClip.Create("menu-bed", 44_100, 2, 44_100, false);
            _catalogue = ScriptableObject.CreateInstance<AudioCatalogue>();
            _catalogue.Menu = new AudioCatalogue.PhaseTrackDef
            {
                Phase = MusicPhase.None, Clip = _bed,
                Volume = 0.18f, FadeSeconds = Leaving, ArrivalFadeSeconds = Arrival,
            };
        }

        [TearDown]
        public void Drop()
        {
            Object.DestroyImmediate(_catalogue);
            Object.DestroyImmediate(_bed);
            Object.DestroyImmediate(_root);
        }

        MenuAmbience Make() => new(_catalogue, _root.transform, _root.layer);

        static void Advance(MenuAmbience bed, float seconds, bool wanted)
        {
            const float Step = 1f / 60f;
            for (float t = 0f; t < seconds; t += Step) bed.Sync(Step, wanted);
        }

        [Test]
        public void ArrivingOnTheMenuTakesTheLongFade()
        {
            using var bed = Make();
            Assume.That(bed.HasVoice, "no voice was built for a catalogue that has a menu row");

            Advance(bed, Arrival * 0.4f, wanted: true);
            Assert.That(bed.Level, Is.GreaterThan(0.2f).And.LessThan(0.6f),
                "four-tenths of the way into the arrival and not four-tenths up");

            Advance(bed, Arrival, wanted: true);
            Assert.That(bed.Level, Is.EqualTo(1f).Within(1e-3f), "the bed never reached full");
        }

        [Test]
        public void AWorldArrivingTakesItAwayFasterThanItCame()
        {
            using var bed = Make();
            Advance(bed, Arrival * 2f, wanted: true);
            Assume.That(bed.Level, Is.EqualTo(1f).Within(1e-3f));

            // The whole point of the asymmetry: the bed has to be gone before the world it is
            // handing over to has finished arriving, and the outdoor bed's own arrival fade is
            // four seconds. Leaving must therefore be no slower than that, and is.
            Advance(bed, Leaving * 0.5f, wanted: false);
            Assert.That(bed.Level, Is.LessThan(0.6f).And.GreaterThan(0.2f));

            Advance(bed, Leaving, wanted: false);
            Assert.That(bed.Level, Is.EqualTo(0f).Within(1e-4f), "the menu bed is still audible in the world");
        }

        [Test]
        public void ItNeverStartsUnderAWorldThatWasThereFirst()
        {
            // The case a player who boots straight into a colony gets, and the one where a bed
            // fading up under a world would be plainly wrong rather than merely late.
            using var bed = Make();
            Advance(bed, 30f, wanted: false);

            Assert.That(bed.Level, Is.EqualTo(0f).Within(1e-6f));
        }

        [Test]
        public void ComingBackToTheMenuUsesTheShortFadeAndNotTheArrival()
        {
            // Quit to menu is the clock turning over, not the game arriving. Eight seconds of
            // silence after a player has asked to leave a colony reads as the sound being broken.
            using var bed = Make();
            Advance(bed, Arrival * 2f, wanted: true);
            Advance(bed, Leaving * 2f, wanted: false);
            Assume.That(bed.Level, Is.EqualTo(0f).Within(1e-4f));

            Advance(bed, Leaving, wanted: true);
            Assert.That(bed.Level, Is.EqualTo(1f).Within(1e-3f),
                "the second arrival waited out the eight-second first-arrival fade");
        }

        [Test]
        public void AFrameAsLongAsAWorldBuildTakesOnlyOneStepOffTheFade()
        {
            using var bed = Make();
            Assume.That(bed.HasVoice, "no voice was built for a catalogue that has a menu row");
            Advance(bed, Arrival + 0.5f, wanted: true);
            Assume.That(bed.Level, Is.EqualTo(1f).Within(1e-4f), "the bed never arrived");

            // The frame after a world is built: one delta the length of the build itself.
            bed.Sync(1.2f, wanted: false);
            Assert.That(bed.Level, Is.EqualTo(1f - MenuAmbience.MaxStepSeconds / Leaving).Within(1e-4f),
                "a one-second frame took more than one clamped step off the leaving fade");

            // And a nonsense delta takes nothing at all.
            float before = bed.Level;
            bed.Sync(float.NaN, wanted: false);
            bed.Sync(-1f, wanted: false);
            Assert.That(bed.Level, Is.EqualTo(before).Within(1e-6f));
        }

        [Test]
        public void ACloneWithNoMenuRowIsSilentRatherThanBroken()
        {
            // The bargain every other piece of presentation makes about assets that are not
            // there. A catalogue is allowed to have no menu bed; a null reference is not.
            _catalogue.Menu = null;
            using var bed = Make();

            Assert.That(bed.HasVoice, Is.False);
            Assert.DoesNotThrow(() => Advance(bed, 5f, wanted: true));
            Assert.That(bed.Level, Is.EqualTo(0f));
        }

        [Test]
        public void TheShippedMenuBedSitsUnderTheWorldsOwnBeds()
        {
            // Owner, 2026-09-19: "keep it really low in the mix by default". A floor as well as
            // a ceiling, because the failure this guards is somebody tuning it by ear into either
            // a bed nobody can hear or one that competes with the colony it hands over to.
            AudioCatalogue? shipped = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioCatalogue>(
                "Assets/Odyssey/Presentation/Audio/AudioCatalogue.asset");
            Assume.That(shipped, Is.Not.Null, "the committed catalogue is missing");
            Assume.That(shipped!.Menu, Is.Not.Null, "no menu bed shipped");
            Assume.That(shipped.Outdoor.Count, Is.GreaterThan(0), "no outdoor beds shipped");

            foreach (AudioCatalogue.PhaseTrackDef world in shipped.Outdoor)
                Assert.That(shipped.Menu!.Volume, Is.LessThan(world.Volume),
                    $"the menu bed ships at {shipped.Menu.Volume:0.00}, at or above the " +
                    $"{world.Phase} outdoor bed's {world.Volume:0.00}");

            Assert.That(shipped.Menu!.Volume, Is.GreaterThanOrEqualTo(0.1f),
                $"the menu bed ships at {shipped.Menu.Volume:0.00}, which reads as off");
        }

        [Test]
        public void TheShippedMenuBedLeavesFasterThanItArrives()
        {
            AudioCatalogue? shipped = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioCatalogue>(
                "Assets/Odyssey/Presentation/Audio/AudioCatalogue.asset");
            Assume.That(shipped?.Menu, Is.Not.Null, "no menu bed shipped");

            Assert.That(shipped!.Menu!.FadeSeconds, Is.LessThan(shipped.Menu.ArrivalFadeSeconds),
                "arriving should be unhurried and leaving should not");

            foreach (AudioCatalogue.PhaseTrackDef world in shipped.Outdoor)
                Assert.That(shipped.Menu.FadeSeconds, Is.LessThanOrEqualTo(world.FadeFor(true)),
                    $"the menu bed outlasts the {world.Phase} bed's arrival, so the two queue " +
                    "rather than cross");
        }
    }
}
