#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Audio;
using UnityEditor;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The draft's blade (design 33 §2i), as the committed catalogue ships it. The clip is the
    /// project's own (baked by <c>tools/audio/bake_draft.sh</c> and committed), so unlike a pack's
    /// art it resolves on the runner too, and this runs everywhere.
    /// </summary>
    public class DraftSoundTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/Audio/AudioCatalogue.asset";

        [Test]
        public void TheDraftSoundIsInTheCatalogueAsAnIndicator()
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<AudioCatalogue>(CataloguePath);
            Assert.That(catalogue, Is.Not.Null, "the committed audio catalogue did not load");

            AudioCatalogue.SoundDef? draft = catalogue!.Sounds.Find(s => s.Id == SoundIds.Draft);
            Assert.That(draft, Is.Not.Null, "no row for the draft: the blade would be silent");

            Assert.That(draft!.Clips.Length, Is.GreaterThan(0));
            Assert.That(draft.Clips[0], Is.Not.Null, "the draft row points at a clip that did not resolve");

            // An indicator: 2D, steady, and on Effects so it does not duck the music like an alert.
            Assert.That(draft.SpatialBlend, Is.EqualTo(0f), "a confirmation must not fade with the camera");
            Assert.That(draft.Bus, Is.EqualTo(SoundBus.Effects));
            Assert.That(draft.PitchVariance, Is.EqualTo(0f));
            Assert.That(draft.VolumeVariance, Is.EqualTo(0f));
            Assert.That(draft.Cooldown, Is.GreaterThan(0f), "a box of five drafted at once would clatter");

            // Short enough to answer a click, which is what it is for.
            Assert.That(draft.Clips[0].length, Is.LessThan(1.0f));
        }
    }
}
