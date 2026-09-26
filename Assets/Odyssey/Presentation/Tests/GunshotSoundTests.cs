#nullable enable
using System.IO;
using NUnit.Framework;
using Odyssey.Presentation.Audio;
using UnityEditor;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The gunshot as the committed catalogue ships it (design 47 §4c-bis): two sounds by distance,
    /// three near takes and two far, placed in the world, and <b>imported without normalising</b>
    /// — the importer's normalise raises every take to a 0 dBFS peak and throws away the level the
    /// bake set. The clips are the project's own (<c>tools/audio/bake_gunshot.sh</c>), so this runs
    /// on the runner too. <b>Red until <c>Odyssey.EditorTools.AudioSetup.Build</c> has been run.</b>
    /// </summary>
    public class GunshotSoundTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/Audio/AudioCatalogue.asset";
        const string ClipFolder = "Assets/Odyssey/Presentation/Audio/Clips";

        static AudioCatalogue.SoundDef Row(string id)
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<AudioCatalogue>(CataloguePath);
            Assert.That(catalogue, Is.Not.Null, "the committed audio catalogue did not load");
            AudioCatalogue.SoundDef? row = catalogue!.Sounds.Find(s => s.Id == id);
            Assert.That(row, Is.Not.Null, $"no row for {id}: a shot would be silent");
            foreach (AudioClip clip in row!.Clips)
                Assert.That(clip, Is.Not.Null, $"{id} points at a clip that did not resolve");
            return row;
        }

        [Test]
        public void BothShotRowsResolveTheirTakes()
        {
            AudioCatalogue.SoundDef near = Row(SoundIds.CombatShot);
            AudioCatalogue.SoundDef far = Row(SoundIds.CombatShotFar);
            Assert.That(near.Clips.Length, Is.EqualTo(3), "the bake makes three near takes");
            Assert.That(far.Clips.Length, Is.EqualTo(2), "the bake makes two far takes");

            foreach (AudioCatalogue.SoundDef row in new[] { near, far })
            {
                Assert.That(row.SpatialBlend, Is.EqualTo(1f), $"{row.Id} is not placed in the world");
                Assert.That(row.Bus, Is.EqualTo(SoundBus.Effects), row.Id);
                foreach (AudioClip clip in row.Clips)
                    Assert.That(clip.channels, Is.EqualTo(1), $"{clip.name} is not mono and cannot be placed");
            }

            // §4c-bis's table.
            Assert.That(near.MaxDistance, Is.EqualTo(150f));
            Assert.That(far.MaxDistance, Is.EqualTo(400f));
            Assert.That(near.Priority, Is.LessThan(100), "a shot must outrank every blow for a voice");
            Assert.That(near.Cooldown, Is.EqualTo(0.03f).Within(1e-6f));
            Assert.That(far.Cooldown, Is.EqualTo(0.06f).Within(1e-6f));
        }

        [Test]
        public void EveryGunshotClipImportsWithNormaliseOff()
        {
            string[] files = Directory.GetFiles(ClipFolder, "combat-shot*.wav");
            Assert.That(files.Length, Is.EqualTo(5), "three near takes and two far were expected on disk");

            foreach (string file in files)
            {
                string path = file.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                Assert.That(importer, Is.Not.Null, $"{path} is not imported as audio");

                var serialized = new SerializedObject(importer);
                SerializedProperty? normalize = serialized.FindProperty("m_Normalize") ?? serialized.FindProperty("normalize");
                Assert.That(normalize, Is.Not.Null, "the importer's normalise field could not be found, so this proves nothing");
                Assert.That(normalize!.boolValue, Is.False,
                    $"{path} imports normalised: every take is raised to a 0 dBFS peak and the bake's level is gone");
                Assert.That(importer!.forceToMono, Is.True, $"{path} is not mono");
            }
        }
    }
}
