#nullable enable
using System.IO;
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;

namespace Odyssey.Tests.Sim
{
    /// <summary>A tickable whose state actually has to survive a round trip.</summary>
    sealed class SaveableCounter : ITickable, IStateHashable, ISaveable
    {
        public TickGroup TickGroup => TickGroup.Normal;
        public int TickPhaseOffset => 0;
        public int Ticks { get; private set; }
        public CellRef Where { get; private set; } = new CellRef(1, 2, 3);

        public void Tick(SimWorld world)
        {
            Ticks++;
            Where = new CellRef(Ticks % 7, Ticks % 5, Ticks % 3);
        }

        public void ContributeTo(ref StateHash hash)
        {
            hash.Add(Ticks);
            hash.Add(Where);
        }

        public string SaveKey => "test.counter";

        public void Save(SaveWriter writer)
        {
            writer.Write(Ticks);
            writer.Write(Where);
        }

        public void Load(SaveReader reader)
        {
            Ticks = reader.ReadInt();
            Where = reader.ReadCell();
        }
    }

    public class SaveTests
    {
        static (SimWorld world, SaveableCounter counter) Build(uint seed = 123)
        {
            var counter = new SaveableCounter();
            var world = new SimWorldBuilder()
                .WithSeed(seed)
                .WithSize(new GridSize(8, 8, 4))
                .AddTickable(_ => counter)
                .Build();
            return (world, counter);
        }

        static byte[] SaveToBytes(SimWorld world, ISaveable component)
        {
            using var stream = new MemoryStream();
            WorldSave.Save(world, stream, new[] { component });
            return stream.ToArray();
        }

        [Test]
        public void RoundTripPreservesTheStateHash()
        {
            // This is the M0 gate in miniature: save at tick N, load into a fresh world, and the
            // two must be indistinguishable.
            var (original, originalCounter) = Build();
            original.Tick(500);
            ulong expected = original.ComputeStateHash().Value;

            byte[] bytes = SaveToBytes(original, originalCounter);

            var (restored, restoredCounter) = Build();
            using var stream = new MemoryStream(bytes);
            WorldSave.Load(restored, stream, new[] { restoredCounter });

            Assert.That(restored.CurrentTick, Is.EqualTo(500));
            Assert.That(restored.ComputeStateHash().Value, Is.EqualTo(expected));
        }

        [Test]
        public void ResumingAfterALoadMatchesAnUnbrokenRun()
        {
            // The real gate: 1000 ticks unbroken versus 500, save, load, 500 more.
            var (unbroken, unbrokenCounter) = Build();
            unbroken.Tick(1000);

            var (first, firstCounter) = Build();
            first.Tick(500);
            byte[] bytes = SaveToBytes(first, firstCounter);

            var (resumed, resumedCounter) = Build();
            using (var stream = new MemoryStream(bytes))
            {
                WorldSave.Load(resumed, stream, new[] { resumedCounter });
            }
            resumed.Tick(500);

            Assert.That(resumed.ComputeStateHash().Value, Is.EqualTo(unbroken.ComputeStateHash().Value));
        }

        [Test]
        public void SavingTheSameStateTwiceProducesIdenticalBytes()
        {
            // The cheapest detector for unordered iteration, which is the most common way a
            // simulation quietly loses determinism.
            var (world, counter) = Build();
            world.Tick(250);

            byte[] first = SaveToBytes(world, counter);
            byte[] second = SaveToBytes(world, counter);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void AnUnknownSectionIsSkippedRatherThanFatal()
        {
            // How a save written with a mod installed still loads without that mod.
            var (world, counter) = Build();
            world.Tick(10);

            using var stream = new MemoryStream();
            WorldSave.Save(world, stream, new ISaveable[] { counter, new ModSection() });
            stream.Position = 0;

            var (restored, restoredCounter) = Build();
            var header = WorldSave.Load(restored, stream, new ISaveable[] { restoredCounter });

            Assert.That(header.SkippedSections, Is.EqualTo(new[] { "mod.absent" }));
            Assert.That(restored.CurrentTick, Is.EqualTo(10), "the known sections still loaded");
        }

        [Test]
        public void ANonSaveFileIsRejectedClearly()
        {
            var (world, counter) = Build();
            using var stream = new MemoryStream(new byte[64]);
            var ex = Assert.Throws<SaveLoadException>(() => WorldSave.Load(world, stream, new[] { counter }));
            Assert.That(ex!.Message, Does.Contain("Not an Odyssey save"));
        }

        [Test]
        public void ATruncatedFileIsReportedNotCrashed()
        {
            var (world, counter) = Build();
            world.Tick(10);
            byte[] bytes = SaveToBytes(world, counter);

            using var stream = new MemoryStream(bytes.Take(bytes.Length / 2).ToArray());
            var (restored, restoredCounter) = Build();
            var ex = Assert.Throws<SaveLoadException>(
                () => WorldSave.Load(restored, stream, new[] { restoredCounter }));
            Assert.That(ex!.Message, Does.Contain("truncated"));
        }

        [Test]
        public void ASaveFromANewerBuildIsRefusedWithAnExplanation()
        {
            var (world, counter) = Build();
            byte[] bytes = SaveToBytes(world, counter);

            // Bump the format version in place: it sits directly after the 8-byte magic.
            byte[] tampered = (byte[])bytes.Clone();
            tampered[8] = (byte)(WorldSave.CurrentFormatVersion + 99);

            using var stream = new MemoryStream(tampered);
            var (restored, restoredCounter) = Build();
            var ex = Assert.Throws<SaveLoadException>(
                () => WorldSave.Load(restored, stream, new[] { restoredCounter }));
            Assert.That(ex!.Message, Does.Contain("newer than this build"));
        }

        [Test]
        public void LoadingIntoTheWrongWorldIsRefused()
        {
            var (world, counter) = Build(seed: 111);
            world.Tick(5);
            byte[] bytes = SaveToBytes(world, counter);

            var (other, otherCounter) = Build(seed: 222);
            using var stream = new MemoryStream(bytes);
            var ex = Assert.Throws<SaveLoadException>(
                () => WorldSave.Load(other, stream, new[] { otherCounter }));
            Assert.That(ex!.Message, Does.Contain("seed"));
        }

        [Test]
        public void StringsAndBytesSurviveARoundTrip()
        {
            using var stream = new MemoryStream();
            using (var binary = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                var writer = typeof(SaveWriter)
                    .GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)[0]
                    .Invoke(new object[] { binary }) as SaveWriter;
                writer!.Write("a string with unicode: éü");
                writer.Write(new byte[] { 1, 2, 3 });
                writer.Write(new CellRef(-4, 5, 6));
            }

            stream.Position = 0;
            using var readerBinary = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            // The reader's constructor grew a format-version parameter when sections started
            // reading the layout they were written in; picked by parameter count rather than
            // position so this test is not coupled to the ctor list's order.
            var readerCtor = typeof(SaveReader)
                .GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Single(c => c.GetParameters().Length == 2);
            var reader = readerCtor.Invoke(new object[] { readerBinary, WorldSave.CurrentFormatVersion }) as SaveReader;

            Assert.That(reader!.ReadString(), Is.EqualTo("a string with unicode: éü"));
            Assert.That(reader.ReadBytes(), Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(reader.ReadCell(), Is.EqualTo(new CellRef(-4, 5, 6)));
        }

        sealed class ModSection : ISaveable
        {
            public string SaveKey => "mod.absent";
            public void Save(SaveWriter writer) => writer.Write(42);
            public void Load(SaveReader reader) => reader.ReadInt();
        }
    }
}
