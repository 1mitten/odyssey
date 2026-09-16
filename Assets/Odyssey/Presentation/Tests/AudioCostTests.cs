#nullable enable
using System.Diagnostics;
using NUnit.Framework;
using Odyssey.Presentation.Audio;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// What the audio costs a frame, on the board that is played and with more going on than a
    /// colony of three can produce.
    ///
    /// <para>The budgets are deliberately loose — this is a guard against a regression of the
    /// order of magnitude, not a stopwatch on a shared CI machine — and the measured numbers are
    /// logged, because the number is the point and an assertion that merely passes says
    /// nothing.</para>
    /// </summary>
    public class AudioCostTests
    {
        static readonly GridSize Board = new(120, 120, 16);

        /// <summary>The wooded board's arrangement: ground at layer 10, a river across it, the
        /// camera looking at the middle of the water.</summary>
        sealed class RiverBoard : ITerrainLookup
        {
            readonly ushort[] _terrain;

            public RiverBoard()
            {
                _terrain = new ushort[Board.SizeX * Board.SizeZ * Board.SizeY];
                for (int z = 0; z < Board.SizeZ; z++)
                for (int x = 0; x < Board.SizeX; x++)
                {
                    bool wet = x > 52 && x < 62;
                    _terrain[Board.Index(x, z, 10)] = wet
                        ? NaturalContent.TerrainShallowWater
                        : NaturalContent.TerrainGrass;
                }
            }

            public ushort TerrainAt(int cellIndex) => _terrain[cellIndex];
        }

        GameObject _root = null!;
        AudioCatalogue _catalogue = null!;

        [SetUp]
        public void Build() => Build(fillers: 30);

        void Build(int fillers)
        {
            _root = new GameObject("cost root");
            _catalogue = ScriptableObject.CreateInstance<AudioCatalogue>();

            // The two sounds that play go in last, so a lookup walks the whole table: that is the
            // shape the catalogue has when the audio is real and the ids are a few hundred.
            for (int i = 0; i < fillers; i++)
                _catalogue.Sounds.Add(new AudioCatalogue.SoundDef
                {
                    Id = $"odyssey.sound.filler.{i}",
                    Clips = new[] { AudioClip.Create($"filler{i}", 4410, 1, 44100, false) },
                });

            _catalogue.Sounds.Add(new AudioCatalogue.SoundDef
            {
                Id = SoundIds.WorkChop,
                Clips = new[] { AudioClip.Create("chop", 4410, 1, 44100, false) },
                Volume = 0.85f, SpatialBlend = 1f, MinDistance = 5f, MaxDistance = 48f,
                Cooldown = 0f,
            });
            _catalogue.Sounds.Add(new AudioCatalogue.SoundDef
            {
                Id = SoundIds.WorkPick,
                Clips = new[] { AudioClip.Create("pick", 4410, 1, 44100, false) },
                Volume = 0.8f, SpatialBlend = 1f, MinDistance = 5f, MaxDistance = 52f,
                Cooldown = 0f,
            });
            _catalogue.Ambience.Add(new AudioCatalogue.AmbienceDef
            {
                Id = SoundIds.AmbienceWater,
                Clip = AudioClip.Create("water", 44100, 1, 44100, false),
                Volume = 0.75f, FadeSeconds = 2.5f,
            });
        }

        [TearDown]
        public void Clear()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_catalogue);
        }

        static WorldSnapshot Frame()
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(0, new GridSize(10, 10, 2), 0);
            return snapshot;
        }

        [Test]
        public void AFrameOfAudioCostsAFractionOfTheBudget()
        {
            var terrain = new RiverBoard();
            using var audio = new AudioDirector(_catalogue, terrain, Board, _root.transform, 0);
            WorldSnapshot frame = Frame();
            var focus = new Vector3(57f * 2.5f, 0f, 60f * 2.5f);

            for (int i = 0; i < 20; i++) audio.Sync(0.016f, frame, Vector3.zero, focus, 11);

            const int frames = 2000;
            var clock = Stopwatch.StartNew();
            for (int i = 0; i < frames; i++)
            {
                // The focus glides, so the probe cannot be measuring one cached answer.
                focus.x += 0.01f;
                audio.Sync(0.016f, frame, Vector3.zero, focus, 11);
            }
            clock.Stop();

            double perFrameMs = clock.Elapsed.TotalMilliseconds / frames;
            Debug.Log($"[audio cost] Sync over a 120x120x16 board with a river: " +
                      $"{perFrameMs:0.0000} ms/frame, water level {audio.WaterLevel:0.00}");

            Assume.That(audio.WaterLevel, Is.GreaterThan(0.1f), "the probe is doing its work");
            Assert.That(perFrameMs, Is.LessThan(0.25d),
                "the whole audio frame against a 5 ms budget");
        }

        [Test]
        public void ABusyColonyOfOneShotsCostsAFractionOfTheBudget() => BusyColony(30);

        /// <summary>
        /// The same work against a catalogue of a few hundred sounds — which is what "before we
        /// start adding real audio" means. The id lookup is the part that grows with the table,
        /// so this is the measurement that says whether it needs an index.
        /// </summary>
        [Test]
        public void ACatalogueTheSizeRealAudioWillBringCostsNoMore()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_catalogue);
            Build(fillers: 250);
            BusyColony(250);
        }

        /// <summary>
        /// The budget for one frame of one-shots. Generous against what is measured — the frame
        /// costs about 0.005 ms either side of 250 sounds — and the point of the number is that
        /// it is well under the 0.15 ms a linear scan of a real-sized catalogue cost, so losing
        /// the director's id index fails this rather than quietly costing 3% of the frame.
        /// </summary>
        const double FrameBudgetMs = 0.05d;

        void BusyColony(int fillers)
        {
            using var audio = new AudioDirector(_catalogue, null, Board, _root.transform, 0);
            WorldSnapshot frame = Frame();

            for (int i = 0; i < 20; i++) audio.Sync(0.016f, frame, Vector3.zero, Vector3.zero, 11);

            // Forty blows offered in a frame: a colony four times the size of anything the slice
            // will run, all swinging on the same tick, all inside the camera's range — and the
            // clock advancing, so voices finish their clips and are genuinely re-spent.
            const int frames = 500, blows = 40;
            var clock = Stopwatch.StartNew();
            for (int i = 0; i < frames; i++)
            {
                audio.Sync(0.016f, frame, Vector3.zero, Vector3.zero, 11);
                for (int b = 0; b < blows; b++)
                    audio.PlayOneShot(b % 2 == 0 ? SoundIds.WorkChop : SoundIds.WorkPick,
                        new Vector3(b, 0f, 0f));
            }
            clock.Stop();

            double perFrameMs = clock.Elapsed.TotalMilliseconds / frames;
            Debug.Log($"[audio cost] {blows} one-shots a frame against {fillers + 2} sounds: " +
                      $"{perFrameMs:0.0000} ms/frame (played {audio.OneShotsPlayed}, " +
                      $"starved {audio.VoiceStarved}, cooldown {audio.CooldownSkipped})");

            Assert.That(perFrameMs, Is.LessThan(FrameBudgetMs),
                "a colony four times the slice's size, all swinging at once — and the price of a " +
                "frame does not grow with the size of the catalogue");
        }
    }
}
