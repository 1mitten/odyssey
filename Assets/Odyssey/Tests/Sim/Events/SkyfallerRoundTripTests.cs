#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim.Events
{
    /// <summary>
    /// A save taken mid-air lands on the same tick a run that was never saved would have, and the
    /// ledger comes back with it (design 23 §7). On the real board through the real build, so the
    /// two new sections are proved to be in the file and not only in the hash.
    /// </summary>
    public class SkyfallerRoundTripTests
    {
        static readonly GridSize Size = new GridSize(24, 24, 8);

        static ColonyWorld Fresh() => ColonyWorld.Build(Size, seed: 23u, ScenarioDef.Bare());

        static ulong Hash(ColonyWorld colony) => colony.World.ComputeStateHash().Value;

        [Test]
        public void ASaveTakenMidAirLandsOnTheSameTick()
        {
            ColonyWorld original = Fresh();
            original.World.Intents.Submit(new Intent(IntentKind.InvokeIncident, default, IncidentHandle.SupplyDrop));
            original.World.Tick(10);
            Assert.That(original.World.Intents.Rejected, Is.Empty, "the meadow had nowhere to land a drop");
            Assert.That(original.Incidents.Skyfallers.InFlight, Has.Count.EqualTo(1), "still in the air at tick 10");
            int landTick = original.Incidents.Skyfallers.InFlight[0].LandTick;

            byte[] bytes = original.Save();
            ColonyWorld restored = Fresh();
            var header = restored.Load(bytes);

            Assert.That(header.SkippedSections, Is.Empty, "a section this build wrote was not read back");
            Assert.That(Hash(restored), Is.EqualTo(Hash(original)), "the hash differs immediately after loading");
            Assert.That(restored.Incidents.Skyfallers.InFlight, Has.Count.EqualTo(1));
            Assert.That(restored.Incidents.Ledger.Count, Is.EqualTo(1));
            Assert.That(restored.Incidents.Ledger.LastFiredTick(IncidentHandle.SupplyDrop), Is.EqualTo(0),
                "the per-incident memory is rebuilt from the entries on load");

            int remaining = landTick - original.World.CurrentTick + 5;
            original.World.Tick(remaining);
            restored.World.Tick(remaining);

            Assert.That(restored.Incidents.Skyfallers.InFlight, Is.Empty);
            Assert.That(restored.Incidents.Skyfallers.Landed, Is.EqualTo(1));
            Assert.That(Hash(restored), Is.EqualTo(Hash(original)),
                "the worlds parted somewhere between the load and the landing");
        }

        [Test]
        public void ASaveFromBeforeEventsExistedLoadsWithAnEmptyLedgerAndNothingInTheAir()
        {
            // A build that knew nothing of events wrote no such sections; loading its file into
            // this build must leave the ledger empty rather than refuse the colony. The nearest
            // thing to such a file is one this build wrote with nothing to say in either section.
            ColonyWorld original = Fresh();
            original.World.Tick(50);
            ColonyWorld restored = Fresh();
            restored.Load(original.Save());

            Assert.That(restored.Incidents.Ledger.Count, Is.Zero);
            Assert.That(restored.Incidents.Skyfallers.InFlight, Is.Empty);
            Assert.That(Hash(restored), Is.EqualTo(Hash(original)));
        }
    }
}
