#nullable enable
using System.IO;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// A colony saved on a 16-layer board comes back 16 deep after every offered board went to 32
    /// (design 62, DM2): the depth a board has is the one its header carries, never the page's.
    ///
    /// <para>The composition root decides the size once, and a load takes it from the header
    /// (<c>OdysseyBootstrap.BuildSession</c>: <c>from != null ? from.Size : …</c>). What this pins is
    /// the simulation's half — the header says 16, a world rebuilt from the header loads, and a
    /// world built at the offered depth refuses the file outright — so a caller that reached for
    /// the offered depth instead would fail loudly rather than open a mangled colony.</para>
    /// </summary>
    public class OldSaveDepthTests
    {
        static readonly GridSize Old = new GridSize(40, 40, 16);
        const uint Seed = 626u;

        static ColonyRequest Request(GridSize size) => new ColonyRequest
        {
            Size = size,
            Seed = Seed,
            Scenario = ScenarioDef.Bare(),
            Barren = true,
            Wooded = true,
        };

        [Test]
        public void ASixteenLayerSaveComesBackSixteenDeep()
        {
            Assert.That(GridSize.OfferedLayers, Is.Not.EqualTo(Old.SizeY),
                "the fixture is only a control while the offered depth differs from the old one");

            ColonyWorld first = ColonyWorld.Build(Request(Old));
            first.World.Tick(200);
            ulong before = first.World.ComputeStateHash().Value;
            byte[] bytes = first.Save(first.Recipe(day: 1));

            SaveHeader header;
            using (var stream = new MemoryStream(bytes, writable: false))
                header = WorldSave.ReadHeaderOnly(stream);
            Assert.That(header.Size, Is.EqualTo(Old), "the header forgot the depth it was saved at");

            ColonyWorld second = ColonyWorld.Build(Request(header.Size));
            second.Load(bytes);
            Assert.That(second.Grid.Size.SizeY, Is.EqualTo(16));
            Assert.That(second.World.ComputeStateHash().Value, Is.EqualTo(before),
                "the old colony did not come back as it was put down");
        }

        [Test]
        public void AWorldAtTheOfferedDepthRefusesAnOldSave()
        {
            ColonyWorld first = ColonyWorld.Build(Request(Old));
            byte[] bytes = first.Save(first.Recipe(day: 1));

            ColonyWorld deeper = ColonyWorld.Build(Request(new GridSize(Old.SizeX, Old.SizeZ, GridSize.OfferedLayers)));
            Assert.Throws<SaveLoadException>(() => deeper.Load(bytes),
                "a 16-layer save opened into a 32-layer world instead of being refused");
        }
    }
}
