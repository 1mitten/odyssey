#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The simulation's half of the body's aspect contract (design 43 §9): the names the interface
    /// reads by literal, held here to the same literals <c>HealthTabTests</c> holds the interface's
    /// copy to.
    /// </summary>
    public class HealthContractTests
    {
        [Test]
        public void TheAspectNamesAreSpelledAsTheInterfaceReadsThem()
        {
            Assert.That(HealthAspects.PainName, Is.EqualTo("odyssey.pawn.health.pain"));
            Assert.That(HealthAspects.ConsciousnessName, Is.EqualTo("odyssey.pawn.health.consciousness"));
            Assert.That(HealthAspects.MovingName, Is.EqualTo("odyssey.pawn.health.moving"));
            Assert.That(HealthAspects.ManipulationName, Is.EqualTo("odyssey.pawn.health.manipulation"));
            Assert.That(HealthAspects.BloodName, Is.EqualTo("odyssey.pawn.health.blood"));
            Assert.That(HealthAspects.BleedHoursName, Is.EqualTo("odyssey.pawn.health.bleed.hours"));
            Assert.That(HealthAspects.InjuriesName, Is.EqualTo("odyssey.pawn.health.injuries"));
            Assert.That(HealthAspects.TendedName, Is.EqualTo("odyssey.pawn.health.tended"));
            Assert.That(HealthAspects.RegionName(4), Is.EqualTo("odyssey.pawn.health.region.4"));
            Assert.That(HealthAspects.InjuryName(4, 0), Is.EqualTo("odyssey.pawn.health.injury.4.0"));
            Assert.That(HealthAspects.CareName(4, 0), Is.EqualTo("odyssey.pawn.health.injury.4.0.care"));
        }

        [Test]
        public void TheRegionAndKindCountsAreTheInterfacesTables()
        {
            Assert.That(HealthAspects.Regions, Is.EqualTo(6));
            Assert.That(HealthAspects.Kinds, Is.EqualTo(3));
            Assert.That(System.Enum.GetValues(typeof(AfflictionKind)).Length, Is.EqualTo(HealthAspects.Kinds),
                "a kind of injury with no aspect, or an aspect for a kind that does not exist");
            var body = Odyssey.Sim.Defs.ContentPack.Pawns().HealthOf(PawnKindIndex.Colonist)!;
            Assert.That(body.regions.Count, Is.EqualTo(HealthAspects.Regions));
        }
    }
}
