#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Events;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.WeaponFixture;

namespace Odyssey.Tests.Sim.Events
{
    /// <summary>
    /// Who a raid is made of (design 55 §8): the gunman kind, the three mixes, and the exact split
    /// of a band between a mix's rows.
    /// </summary>
    public class RaidMixTests
    {
        [Test]
        public void TheMixesBindInTheirOrderAndEachSumsToAThousand()
        {
            IncidentContent content = ContentPack.Incidents();
            Assert.That(content.Mixes, Has.Length.EqualTo(IncidentContent.MixOrder.Length));
            for (int m = 0; m < content.Mixes.Length; m++)
            {
                RaidMix mix = content.Mixes[m];
                Assert.That(mix.Def.defName, Is.EqualTo(IncidentContent.MixOrder[m]));
                int sum = 0;
                foreach (int share in mix.PerMille) sum += share;
                Assert.That(sum, Is.EqualTo(1000), mix.Def.defName);
                Assert.That(mix.Def.labelKey, Does.StartWith("ui.raid.mix."));
            }

            RaidMix mixed = content.Mixes[content.MixIndex("RaidMix_Mixed")];
            Assert.That(mixed.Kinds, Is.EqualTo(new[] { PawnKindIndex.Bandit, PawnKindIndex.Gunman }));
            Assert.That(content.MixIndex("RaidMix_Nothing"), Is.EqualTo(-1));
        }

        [TestCase(1, 1, 0)]
        [TestCase(3, 2, 1)]
        [TestCase(10, 7, 3)]
        [TestCase(200, 140, 60)]
        public void AMixedBandIsSplitByLargestRemainder(int size, int bandits, int gunmen)
        {
            RaidMix mixed = ContentPack.Incidents().Mixes[2];
            var counts = new int[2];
            mixed.Compose(size, counts);
            Assert.That(counts, Is.EqualTo(new[] { bandits, gunmen }));
        }

        /// <summary>
        /// Every band from nobody to two hundred adds up to itself, under every mix and under a
        /// three-way split whose shares cannot divide most sizes evenly.
        /// </summary>
        [Test]
        public void EveryBandAddsUpToItself()
        {
            var mixes = new List<RaidMix>(ContentPack.Incidents().Mixes)
            {
                new RaidMix(new RaidMixDef { defName = "Test" }, new[] { 3, 3, 4 }, new[] { 333, 333, 334 }),
            };
            foreach (RaidMix mix in mixes)
            {
                var counts = new int[mix.Kinds.Length];
                for (int size = 0; size <= 200; size++)
                {
                    mix.Compose(size, counts);
                    int sum = 0;
                    foreach (int c in counts)
                    {
                        Assert.That(c, Is.GreaterThanOrEqualTo(0));
                        sum += c;
                    }
                    Assert.That(sum, Is.EqualTo(size), $"{mix.Def.defName} at {size}");
                }
            }
        }

        [Test]
        public void AMixWhoseSharesMissAThousandFailsAtLoad()
        {
            var def = Mix(("PawnKind_Bandit", 600), ("PawnKind_Gunman", 300));
            var thrown = Assert.Throws<DefLoadException>(() => RaidMix.Bind(def, ContentPack.Pawns()));
            Assert.That(thrown!.Message, Does.Contain("RaidMix_Test").And.Contain("900"));
        }

        [Test]
        public void AMixNamingAnUnknownKindFailsAtLoad()
        {
            var thrown = Assert.Throws<DefLoadException>(() =>
                RaidMix.Bind(Mix(("PawnKind_Nobody", 1000)), ContentPack.Pawns()));
            Assert.That(thrown!.Message, Does.Contain("PawnKind_Nobody"));
        }

        /// <summary>A raid of hogs is not a raid: a mix may only name a hostile kind. The bandit is the control.</summary>
        [Test]
        public void AMixNamingAKindThatIsNotHostileFailsAtLoad()
        {
            var thrown = Assert.Throws<DefLoadException>(() =>
                RaidMix.Bind(Mix(("PawnKind_MiddenHog", 1000)), ContentPack.Pawns()));
            Assert.That(thrown!.Message, Does.Contain("not hostile"));
            Assert.DoesNotThrow(() => RaidMix.Bind(Mix(("PawnKind_Bandit", 1000)), ContentPack.Pawns()));
        }

        /// <summary>
        /// The gunman arrives with a pistol from its own table, through the same spawn every pawn
        /// takes — no weapon override. The bandit spawned beside it is the control: never a pistol.
        /// </summary>
        [Test]
        public void AGunmanArrivesWithAPistolAndABanditDoesNot()
        {
            var colony = Board();
            colony.World.Tick(30);
            PawnContext ctx = colony.Pawns;

            Pawn gunman = SpawnKind(colony, PawnKindIndex.Gunman);
            Pawn bandit = SpawnKind(colony, PawnKindIndex.Bandit);

            Assert.That(gunman.IsHostile && gunman.IsPerson, Is.True);
            Assert.That(ctx.Items.Get(new ThingId(gunman.EquippedItem))!.DefIndex, Is.EqualTo(ItemIndex.Pistol));
            Assert.That(ctx.Items.Get(new ThingId(bandit.EquippedItem))!.DefIndex, Is.Not.EqualTo(ItemIndex.Pistol));
        }

        static RaidMixDef Mix(params (string kind, int perMille)[] rows)
        {
            var def = new RaidMixDef { defName = "RaidMix_Test", labelKey = "ui.raid.mix.test" };
            foreach (var (kind, perMille) in rows) def.entries.Add(new RaidMixEntry { kind = kind, perMille = perMille });
            return def;
        }
    }
}
