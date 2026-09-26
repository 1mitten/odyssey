#nullable enable
using System.IO;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Audio;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;
using UnityEditor;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The butcher's voice and its cleaver's deep whoosh as the committed catalogue ships them
    /// (design 62 §8d): four moments at four loudnesses, each several takes, placed in the world and
    /// <b>imported without normalising</b>, which would flatten the bake's ladder from the grunt to
    /// the bellow. The clips are the project's own (<c>tools/audio/bake_butcher.sh</c>), so this runs
    /// on the runner too.
    /// </summary>
    public class ButcherSoundTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/Audio/AudioCatalogue.asset";
        const string ClipFolder = "Assets/Odyssey/Presentation/Audio/Clips";

        static AudioCatalogue.SoundDef Row(string id)
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<AudioCatalogue>(CataloguePath);
            Assert.That(catalogue, Is.Not.Null, "the committed audio catalogue did not load");
            AudioCatalogue.SoundDef? row = catalogue!.Sounds.Find(s => s.Id == id);
            Assert.That(row, Is.Not.Null, $"no row for {id}: the butcher would be silent there");
            Assert.That(row!.Clips.Length, Is.GreaterThan(0), id);
            foreach (AudioClip clip in row.Clips)
            {
                Assert.That(clip, Is.Not.Null, $"{id} points at a clip that did not resolve");
                Assert.That(clip.channels, Is.EqualTo(1), $"{clip.name} is not mono and cannot be placed");
            }
            Assert.That(row.SpatialBlend, Is.EqualTo(1f), $"{id} is not placed in the world");
            return row;
        }

        /// <summary>Every voice the content names has all four moments, and each is several takes.</summary>
        [Test]
        public void EveryVoiceTheContentNamesHasItsFourMoments()
        {
            var (voices, pitch) = CombatFeedback.VoicesOf(ContentPack.Pawns());
            int voiced = 0;
            for (int kind = 0; kind < voices.Length; kind++)
            {
                if (voices[kind] == null) continue;
                voiced++;
                foreach (VoiceCue cue in new[] { VoiceCue.Strike, VoiceCue.Hurt, VoiceCue.Fling, VoiceCue.Down })
                    Assert.That(Row(SoundIds.Voice(voices[kind], cue)!).Clips.Length, Is.GreaterThanOrEqualTo(3),
                        $"{voices[kind]} {cue}: too few takes, a fight would repeat one");
                Assert.That(pitch[kind], Is.InRange(0.5f, 1.5f), $"kind {kind}'s voice pitch");
            }
            Assert.That(voiced, Is.EqualTo(4), "the four butcher levels, and nobody else, have a voice");
        }

        /// <summary>
        /// Loud when it throws somebody, softer on its own swing (owner: "loud when he knocks people
        /// back or even when hit"): the catalogue keeps the order the bake set.
        /// </summary>
        [Test]
        public void TheFlingIsLouderThanTheHurtAndTheHurtThanTheStrike()
        {
            float strike = Row(SoundIds.Voice("butcher", VoiceCue.Strike)!).Volume;
            float hurt = Row(SoundIds.Voice("butcher", VoiceCue.Hurt)!).Volume;
            float fling = Row(SoundIds.Voice("butcher", VoiceCue.Fling)!).Volume;
            Assert.That(fling, Is.GreaterThan(hurt));
            Assert.That(hurt, Is.GreaterThan(strike));
        }

        [Test]
        public void TheDeepWhooshIsTwoTakesAndTheCleaverIsHeardAsOne()
        {
            Assert.That(Row(SoundIds.CombatSwingHeavy).Clips.Length, Is.EqualTo(2));
            Assert.That(SoundIds.ForCue(CombatCue.HeavyWhoosh), Is.EqualTo(SoundIds.CombatSwingHeavy));
            BloodSides sides = CombatFeedback.BloodSidesOf(ContentPack.Pawns());
            Assert.That(sides.WieldsNatural(PawnKindIndex.Butcher), Is.True, "the cleaver does not whoosh");
            Assert.That(sides.WieldsNatural(PawnKindIndex.MiddenHog), Is.False, "a hog's bite whooshes");
            Assert.That(sides.WieldsNatural(PawnKindIndex.Colonist), Is.False, "a colonist's fists whoosh");
        }

        [Test]
        public void EveryButcherClipImportsWithNormaliseOff()
        {
            string[] files = Directory.GetFiles(ClipFolder, "butcher-*.wav");
            string[] heavy = Directory.GetFiles(ClipFolder, "combat-whoosh-heavy*.wav");
            Assert.That(files.Length, Is.EqualTo(15), "the bake makes fifteen voice takes");
            Assert.That(heavy.Length, Is.EqualTo(2));
            foreach (string file in System.Linq.Enumerable.Concat(files, heavy))
            {
                string path = file.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                Assert.That(importer, Is.Not.Null, $"{path} is not imported as audio");
                var serialized = new SerializedObject(importer);
                SerializedProperty? normalize = serialized.FindProperty("m_Normalize") ?? serialized.FindProperty("normalize");
                Assert.That(normalize, Is.Not.Null, "the importer's normalise field could not be found, so this proves nothing");
                Assert.That(normalize!.boolValue, Is.False, $"{path} imports normalised: the bake's ladder is gone");
            }
        }
    }
}
