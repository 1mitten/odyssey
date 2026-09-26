#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Audio;
using UnityEditor;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The demolition sounds (design 58 §9): wood coming down and a mined face collapsing, three
    /// takes each, placed at the cell. The clips are the project's own
    /// (<c>tools/audio/bake_demolition.sh</c>), so this runs on the runner too. When they play is
    /// <c>DemolitionWatchTests</c>' question, in the fast tier.
    /// </summary>
    public class DemolitionSoundTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/Audio/AudioCatalogue.asset";

        [TestCase(SoundIds.BreakWood)]
        [TestCase(SoundIds.BreakRock)]
        public void EachIsInTheCatalogueWithThreeTakesAtTheCell(string id)
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<AudioCatalogue>(CataloguePath);
            Assert.That(catalogue, Is.Not.Null, "the committed audio catalogue did not load");

            AudioCatalogue.SoundDef? sound = catalogue!.Sounds.Find(s => s.Id == id);
            Assert.That(sound, Is.Not.Null,
                $"no row for {id} — rebuild the audio catalogue (Odyssey > Presentation > Build audio catalogue)");
            Assert.That(sound!.Clips.Length, Is.EqualTo(3), "three takes, so a gallery is not one sample on repeat");
            foreach (AudioClip clip in sound.Clips)
            {
                Assert.That(clip, Is.Not.Null, $"a take of {id} did not resolve");
                Assert.That(clip.length, Is.LessThanOrEqualTo(1.75f), "a take outlasts the break it goes with");
            }
            Assert.That(sound.SpatialBlend, Is.EqualTo(1f), "it comes from the cell");
            Assert.That(sound.Bus, Is.EqualTo(SoundBus.Effects));
        }
    }
}
