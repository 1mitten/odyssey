#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The weapons a colony starts with (design 33 §1: "one or two in the starting kit", §6D): a
    /// bat and a machete on the ground in the played scenario, in no hand, and <b>none at all in
    /// <see cref="ScenarioDef.Bare"/></b>, on which every golden builds.
    /// </summary>
    public class StartingKitTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ColonyWorld Wooded(ScenarioDef scenario) =>
            ColonyWorld.Build(Size, seed: 1u, scenario, barren: true, wooded: true);

        static int[] WeaponsOnTheGround(ColonyWorld colony) =>
            colony.Pawns.Items.Items
                .Where(i => !i.Despawned && colony.Pawns.Content.Items[i.DefIndex].weapon != null)
                .Select(i => i.DefIndex).OrderBy(d => d).ToArray();

        [Test]
        public void ThePlayedScenarioStartsWithABatAndAMacheteOnTheGround()
        {
            ColonyWorld colony = Wooded(ScenarioDef.Playtest());

            Assert.That(colony.Placement.Weapons, Is.EqualTo(2), colony.Placement.ToString());
            Assert.That(WeaponsOnTheGround(colony), Is.EqualTo(new[] { ItemIndex.Bat, ItemIndex.Machete }));
            foreach (ColonyItem item in colony.Pawns.Items.Items.Where(i => !i.Despawned && colony.Pawns.Content.Items[i.DefIndex].weapon != null))
            {
                Assert.That(item.Cell, Is.GreaterThanOrEqualTo(0), "a starting weapon has no cell");
                Assert.That(item.CarriedBy, Is.Zero, "a starting weapon is in a hand");
                Assert.That(item.Forbidden, Is.False);
            }
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
                Assert.That(pawn.EquippedItem, Is.Zero, "a colonist starts armed");
        }

        /// <summary>The golden baseline: no weapon anywhere, so no golden sees the kit.</summary>
        [Test]
        public void BareStartsWithNoWeapon()
        {
            ColonyWorld colony = Wooded(ScenarioDef.Bare());
            Assert.That(colony.Placement.Weapons, Is.Zero);
            Assert.That(WeaponsOnTheGround(colony), Is.Empty);
        }

        /// <summary>
        /// A starting weapon is an ordinary thing: a colonist can be sent for it at once. The
        /// control that the kit is not decoration.
        /// </summary>
        [Test]
        public void AStartingWeaponCanBeTakenUp()
        {
            ColonyWorld colony = Wooded(ScenarioDef.Playtest());
            colony.World.Tick(30);
            Pawn colonist = colony.Pawns.Pawns.All[0];
            ColonyItem bat = colony.Pawns.Items.Items.First(i => !i.Despawned && i.DefIndex == ItemIndex.Bat);
            Assert.That(colony.Pawns.WeaponRules.CanEquip(colonist, bat, colony.Pawns), Is.True);
        }
    }
}
