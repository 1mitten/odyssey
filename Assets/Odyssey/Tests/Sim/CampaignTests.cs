#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Expeditions;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// EX2 (design 64 §4): the campaign's clock. Being in a campaign changes nothing about home, the
    /// boards tick in step, and the campaign hash is a function of what happened.
    /// </summary>
    public class CampaignTests
    {
        /// <summary>
        /// <b>A campaign with no expedition is invisible to home.</b> The committed golden is the
        /// oracle: home stepped by the campaign lands on the number home reaches ticking alone.
        /// </summary>
        [Test]
        public void ACampaignWithNoExpeditionLeavesHomeOnItsGolden()
        {
            ColonyWorld home = Golden.Meadow.Build();
            var campaign = new Campaign(home, worldSeed: 7u);
            campaign.Step(Golden.Meadow.Ticks);

            Assert.That(campaign.Tick, Is.EqualTo(Golden.Meadow.Ticks));
            Assert.That(home.World.ComputeStateHash().Value, Is.EqualTo(Golden.Meadow.Simulated),
                "home's hash moved by being wrapped in a campaign, so the campaign reached into it");
        }

        [Test]
        public void TwoCampaignsOnOneSeedHashTheSameAndADifferentSeedDoesNot()
        {
            var a = new Campaign(Golden.Meadow.Build(), 7u);
            var b = new Campaign(Golden.Meadow.Build(), 7u);
            var c = new Campaign(Golden.Meadow.Build(), 8u);
            Assert.That(a.ComputeHash(), Is.EqualTo(b.ComputeHash()));
            Assert.That(a.ComputeHash(), Is.Not.EqualTo(c.ComputeHash()),
                "the world seed is campaign state and the hash must see it");
            for (int hour = 0; hour < 3; hour++)
            {
                a.Step(500);
                b.Step(500);
                Assert.That(a.ComputeHash(), Is.EqualTo(b.ComputeHash()), $"diverged by step {a.Tick}");
            }
        }

        [Test]
        public void ABoardOffTheClockCannotJoin()
        {
            ColonyWorld home = Golden.Meadow.Build();
            var campaign = new Campaign(home, 7u);
            campaign.Step(10);
            ColonyWorld stranger = ColonyWorld.Build(new GridSize(30, 30, 8), 3u, ScenarioDef.Bare());
            Assert.Throws<InvalidOperationException>(() => campaign.AddBoard(stranger, place: 0));
        }

        [Test]
        public void HomeKeepsTheIdsItHadAndTheCampaignCountsOnFromThem()
        {
            ColonyWorld home = Golden.Meadow.Build();
            int before = home.Pawns.Pawns.Ids.Peek;
            var campaign = new Campaign(home, 7u);
            Assert.That(campaign.Ids.Peek, Is.EqualTo(before));
            Assert.That(home.Pawns.Pawns.Ids, Is.SameAs(campaign.Ids), "home now counts on the campaign's counter");
        }
    }
}
