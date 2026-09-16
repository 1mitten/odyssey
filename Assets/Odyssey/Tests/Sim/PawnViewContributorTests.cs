#nullable enable
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// A whole feature in miniature, and the point is where it does <em>not</em> live: not in
    /// <c>Sim.Contracts</c>, not in <c>Sim.Pawns</c>, not in any file the rest of the project
    /// owns. It keeps a number per pawn, ticks it, contributes it to the state hash, writes it to
    /// the save and publishes it to the interface — the whole loop a real feature runs — and it
    /// was added to the world without editing <see cref="PawnView"/>.
    /// </summary>
    sealed class ThirstFeature : ITickable, IStateHashable, ISaveable, ISnapshotContributor
    {
        /// <summary>The name this feature publishes under. A reader needs the string and nothing else.</summary>
        public const string KeyName = "test.pawn.thirst";

        /// <summary>Minted once, not inside the publish loop. See <see cref="AspectKey"/>.</summary>
        public static readonly AspectKey Key = AspectKey.Of(KeyName);

        // Insertion order is kept beside the map so the hash, the save and the published frame
        // all walk the pawns in one defined sequence. A dictionary's own order is not one.
        readonly List<PawnId> _order = new List<PawnId>();
        readonly Dictionary<PawnId, int> _thirst = new Dictionary<PawnId, int>();

        /// <summary>The negative control: with this off the feature holds its state and says nothing.</summary>
        public bool Publishing = true;

        public TickGroup TickGroup => TickGroup.Normal;
        public int TickPhaseOffset => 0;

        public void Track(PawnId pawn, int thirst)
        {
            if (!_thirst.ContainsKey(pawn)) _order.Add(pawn);
            _thirst[pawn] = thirst;
        }

        public int Of(PawnId pawn) => _thirst.TryGetValue(pawn, out int value) ? value : 0;

        public void Tick(SimWorld world)
        {
            for (int i = 0; i < _order.Count; i++) _thirst[_order[i]]++;
        }

        public void ContributeTo(ref StateHash hash)
        {
            hash.Add(_order.Count);
            for (int i = 0; i < _order.Count; i++)
            {
                hash.Add(_order[i].Value);
                hash.Add(_thirst[_order[i]]);
            }
        }

        public string SaveKey => "test.thirst";

        public void Save(SaveWriter writer)
        {
            writer.Write(_order.Count);
            for (int i = 0; i < _order.Count; i++)
            {
                writer.Write(_order[i].Value);
                writer.Write(_thirst[_order[i]]);
            }
        }

        public void Load(SaveReader reader)
        {
            _order.Clear();
            _thirst.Clear();
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                var pawn = new PawnId(reader.ReadInt());
                _order.Add(pawn);
                _thirst[pawn] = reader.ReadInt();
            }
        }

        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            if (!Publishing) return;
            for (int i = 0; i < _order.Count; i++)
                writer.AddPawnAspect(_order[i], Key, _thirst[_order[i]]);
        }
    }

    /// <summary>A second feature, so "each feature owns its own vocabulary" is tested, not asserted.</summary>
    sealed class MoraleFeature : ISnapshotContributor
    {
        public static readonly AspectKey Key = AspectKey.Of("test.pawn.morale");
        public PawnId Pawn;
        public int Value;

        public void Contribute(SimWorld world, SnapshotWriter writer) =>
            writer.AddPawnAspect(Pawn, Key, Value);
    }

    /// <summary>Publishes the pawn rows themselves, standing in for <c>PawnRegistry</c>.</summary>
    sealed class PawnRowSource : ISnapshotContributor
    {
        public readonly List<PawnId> Pawns = new List<PawnId>();

        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            for (int i = 0; i < Pawns.Count; i++)
                writer.AddPawn(new PawnView(Pawns[i], new CellRef(i, 0, 0), food: 900, rest: 800, mood: 50));
        }
    }

    /// <summary>
    /// OQ-45. A feature can publish what the interface needs to know about a pawn without widening
    /// <see cref="PawnView"/>, which is the contract every consumer reads and which every feature
    /// so far has had to edit.
    ///
    /// <para><b>Every positive here has a control beside it</b>, because a seam test is exactly
    /// the kind that passes while measuring nothing: unregister the feature and the read must
    /// fail, ask for a name nobody published and the read must fail, ask about a pawn nobody
    /// published and the read must fail. Each of those was seen to fail before the positive was
    /// trusted.</para>
    /// </summary>
    public class PawnViewContributorTests
    {
        static readonly PawnId Ada = new PawnId(1);
        static readonly PawnId Bram = new PawnId(2);

        sealed class Harness
        {
            public SimWorld World = null!;
            public ThirstFeature Thirst = null!;
            public PawnRowSource Rows = null!;

            public WorldSnapshot Frame => World.Views.Current;

            public static Harness Build(bool registerFeature = true, uint seed = 7u)
            {
                var thirst = new ThirstFeature();
                var rows = new PawnRowSource();
                rows.Pawns.Add(Ada);
                rows.Pawns.Add(Bram);
                thirst.Track(Ada, 40);
                thirst.Track(Bram, 10);

                var builder = new SimWorldBuilder()
                    .WithSeed(seed)
                    .WithSize(new GridSize(8, 8, 4))
                    .AddSnapshotContributor(rows);

                // The feature adds itself through the seam that already existed for the frame.
                // Nothing in the composition root knows what an aspect is.
                if (registerFeature)
                {
                    builder.AddTickable(_ => thirst);
                    builder.AddSnapshotContributor(thirst);
                }

                return new Harness { World = builder.Build(), Thirst = thirst, Rows = rows };
            }
        }

        // ---- the seam itself ---------------------------------------------------------------

        [Test]
        public void AFeatureOutsideTheContractPublishesAboutAPawn()
        {
            var h = Harness.Build();
            h.World.Tick();

            Assert.That(h.Frame.TryGetPawnAspect(Ada, ThirstFeature.Key, out int value), Is.True);
            Assert.That(value, Is.EqualTo(h.Thirst.Of(Ada)));
        }

        [Test]
        public void TheReaderNeedsOnlyTheNameNotTheFeature()
        {
            // What the interface actually has: a string. It never references ThirstFeature, and
            // there is no shared enum for it to have been added to. This is the whole mechanism.
            var h = Harness.Build();
            h.World.Tick();

            var byName = AspectKey.Of("test.pawn.thirst");
            Assert.That(h.Frame.TryGetPawnAspect(Bram, byName, out int value), Is.True);
            Assert.That(value, Is.EqualTo(h.Thirst.Of(Bram)));
        }

        [Test]
        public void ThePawnRowsAreUnchangedBesideIt()
        {
            // An aspect is a row of its own, so the views everyone already reads are untouched.
            var h = Harness.Build();
            h.World.Tick();

            Assert.That(h.Frame.PawnCount, Is.EqualTo(2));
            Assert.That(h.Frame.TryGetPawn(Ada, out PawnView view), Is.True);
            Assert.That(view.Mood, Is.EqualTo(50));
        }

        [Test]
        public void TwoFeaturesDoNotSeeEachOther()
        {
            var morale = new MoraleFeature { Pawn = Ada, Value = 77 };
            var thirst = new ThirstFeature();
            thirst.Track(Ada, 40);

            var world = new SimWorldBuilder()
                .WithSeed(3u).WithSize(new GridSize(8, 8, 4))
                .AddSnapshotContributor(thirst)
                .AddSnapshotContributor(morale)
                .Build();
            world.Tick();

            var frame = world.Views.Current;
            Assert.That(frame.TryGetPawnAspect(Ada, MoraleFeature.Key, out int m), Is.True);
            Assert.That(m, Is.EqualTo(77));
            Assert.That(frame.TryGetPawnAspect(Ada, ThirstFeature.Key, out int t), Is.True);
            Assert.That(t, Is.EqualTo(40));
            Assert.That(frame.AspectCount, Is.EqualTo(2));
        }

        // ---- the controls ------------------------------------------------------------------

        [Test]
        public void WithTheFeatureUnregisteredNothingIsPublished()
        {
            var h = Harness.Build(registerFeature: false);
            h.World.Tick();

            Assert.That(h.Frame.AspectCount, Is.EqualTo(0));
            Assert.That(h.Frame.TryGetPawnAspect(Ada, ThirstFeature.Key, out _), Is.False);
        }

        [Test]
        public void ANameNobodyPublishedReadsNothing()
        {
            var h = Harness.Build();
            h.World.Tick();

            Assert.That(h.Frame.TryGetPawnAspect(Ada, AspectKey.Of("test.pawn.hunger"), out _), Is.False);
        }

        [Test]
        public void APawnNobodyPublishedReadsNothing()
        {
            var h = Harness.Build();
            h.World.Tick();

            Assert.That(h.Frame.TryGetPawnAspect(new PawnId(99), ThirstFeature.Key, out _), Is.False);
        }

        [Test]
        public void AFeatureThatFallsSilentStopsBeingRead()
        {
            // A pawn that stops doing the thing, and the frame after. Stale rows are impossible
            // because the buffer is rewritten from nothing every publish.
            var h = Harness.Build();
            h.World.Tick();
            Assert.That(h.Frame.TryGetPawnAspect(Ada, ThirstFeature.Key, out _), Is.True);

            h.Thirst.Publishing = false;
            h.World.Tick();
            Assert.That(h.Frame.AspectCount, Is.EqualTo(0));
            Assert.That(h.Frame.TryGetPawnAspect(Ada, ThirstFeature.Key, out _), Is.False);
        }

        // ---- the hash and the save carry the state; the published frame is not state ---------

        [Test]
        public void TheStateHashCarriesWhatTheFeatureHolds()
        {
            var h = Harness.Build();
            ulong before = h.World.ComputeStateHash().Value;

            h.Thirst.Track(Ada, h.Thirst.Of(Ada) + 1);
            Assert.That(h.World.ComputeStateHash().Value, Is.Not.EqualTo(before));
        }

        [Test]
        public void PublishingAnAspectDoesNotEnterTheStateHash()
        {
            // The other half of the previous test, and the one that keeps the look of the game out
            // of the determinism contract: the feature's state is hashed, its report is not.
            var h = Harness.Build();
            h.World.Tick();
            ulong publishing = h.World.ComputeStateHash().Value;

            h.Thirst.Publishing = false;
            h.World.Tick();
            ulong silent = h.World.ComputeStateHash().Value;

            var control = Harness.Build();
            control.World.Tick(2);

            Assert.That(h.Frame.AspectCount, Is.EqualTo(0), "the control must actually differ in what it published");
            Assert.That(control.Frame.AspectCount, Is.EqualTo(2));
            Assert.That(silent, Is.EqualTo(control.World.ComputeStateHash().Value));
            Assert.That(silent, Is.Not.EqualTo(publishing), "thirst advanced a tick between the two readings");
        }

        [Test]
        public void TheSaveCarriesWhatTheFeatureHoldsAndTheFrameComesBackWithIt()
        {
            var original = Harness.Build();
            original.World.Tick(50);
            ulong before = original.World.ComputeStateHash().Value;

            byte[] bytes;
            using (var stream = new MemoryStream())
            {
                WorldSave.Save(original.World, stream, new ISaveable[] { original.Thirst });
                bytes = stream.ToArray();
            }

            var restored = Harness.Build();
            using (var stream = new MemoryStream(bytes))
            {
                SaveHeader header = WorldSave.Load(restored.World, stream, new ISaveable[] { restored.Thirst });
                Assert.That(header.SkippedSections, Is.Empty);
            }

            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(before));

            // And the loaded world publishes the loaded numbers, which is the half a round-trip
            // test on its own would miss.
            restored.World.Tick();
            original.World.Tick();
            Assert.That(restored.Frame.TryGetPawnAspect(Ada, ThirstFeature.Key, out int after), Is.True);
            Assert.That(original.Frame.TryGetPawnAspect(Ada, ThirstFeature.Key, out int expected), Is.True);
            Assert.That(after, Is.EqualTo(expected));
        }

        // ---- the shape of the mechanism -----------------------------------------------------

        [Test]
        public void TheKeyForANameIsFixedForEver()
        {
            // A golden, deliberately. The key is minted from a name at run time, so the hash
            // function is a contract between whatever publishes and whatever reads — including
            // across the two runtimes this suite runs on, CoreCLR in the fast tier and Mono in the
            // Unity tier. Changing the arithmetic must be a decision, not a side effect.
            Assert.That(AspectKey.Of("test.pawn.thirst").Value, Is.EqualTo(14085512674370715067UL));
            Assert.That(AspectKey.Of(string.Empty).Value, Is.EqualTo(14695981039346656037UL));
            Assert.That(AspectKey.Of("test.pawn.hunger"), Is.Not.EqualTo(AspectKey.Of("test.pawn.thirst")));
            Assert.That(AspectKey.Of("test.pawn.thirst"), Is.EqualTo(ThirstFeature.Key));
        }

        [Test]
        public void AnUnsetKeyIsNeverAKeyAnyNameMints()
        {
            Assert.That(default(AspectKey).Value, Is.EqualTo(0UL));
            Assert.That(AspectKey.Of(string.Empty), Is.Not.EqualTo(default(AspectKey)));
        }

        [Test]
        public void AnAspectRowIsPlainDataTheContractsAssemblyCanCarry()
        {
            // ADR 0004 requires the contracts assembly to hold plain blittable types, because both
            // sides reference it and one day the tick moves off the main thread. A row that grew a
            // string or an object would break that quietly.
            Assert.That(typeof(PawnAspect).IsValueType, Is.True);
            foreach (var field in typeof(PawnAspect).GetFields())
                Assert.That(field.FieldType.IsValueType, Is.True, $"{field.Name} is not a value type");
        }

        [Test]
        public void RepublishingAspectsDoesNotGrowBuffersOnceWarm()
        {
            // Steady-state publishing must not allocate; the aspect buffer is pooled like the rest.
            var h = Harness.Build();
            h.World.Tick(4);
            long before = System.GC.GetTotalMemory(true);
            h.World.Tick(200);
            long after = System.GC.GetTotalMemory(true);

            Assert.That(h.Frame.AspectCount, Is.EqualTo(2));
            Assert.That(after - before, Is.LessThan(64 * 1024));
        }

        [Test]
        public void AspectsArePublishedInContributorOrderSoAFrameIsReproducible()
        {
            // Contributors run in the order the composition added them, which is what lets two
            // runs of the same world be compared frame for frame at all.
            var h = Harness.Build();
            h.World.Tick();

            var first = h.Frame.PawnAspects.ToArray();
            h.World.Tick();
            var second = h.Frame.PawnAspects.ToArray();

            Assert.That(first.Length, Is.EqualTo(2));
            for (int i = 0; i < first.Length; i++)
            {
                Assert.That(second[i].Pawn, Is.EqualTo(first[i].Pawn));
                Assert.That(second[i].Key, Is.EqualTo(first[i].Key));
            }
        }
    }
}
