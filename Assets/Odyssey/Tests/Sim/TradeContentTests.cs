#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Gold and what things are worth (design 65 §2–§3, T1): the content a trade is made from.
    /// </summary>
    public class TradeContentTests
    {
        [Test]
        public void GoldIsAnItemOfFiveHundredToAStackFiledUnderItems()
        {
            ItemDef gold = ContentPack.Pawns().Items[ItemIndex.Gold];

            Assert.That(gold.defName, Is.EqualTo("Item_Gold"));
            Assert.That(gold.stackLimit, Is.EqualTo(500));
            Assert.That(gold.category, Is.EqualTo(ItemCategory.Items));
            Assert.That(gold.haulable, Is.True);
            Assert.That(gold.nutrition, Is.Zero, "gold is not food");
        }

        [Test]
        public void GoldIsWorthOneBecauseEveryOtherValueIsANumberOfIt()
        {
            Assert.That(ContentPack.Pawns().Items[ItemIndex.Gold].marketValue, Is.EqualTo(1));
        }

        /// <summary>
        /// Every item has a value: a trader can only be offered what can be priced, and an item that
        /// forgot its value would silently drop out of every ledger.
        /// </summary>
        [Test]
        public void EveryItemHasAValue()
        {
            ItemDef[] items = ContentPack.Pawns().Items;
            Assert.That(items, Has.Length.EqualTo(ItemHandle.Count));
            foreach (ItemDef item in items)
                Assert.That(item.marketValue, Is.GreaterThan(0), item.defName + " has no market value");
        }

        [Test]
        public void TheValuesAreDesignFiftySevensTable()
        {
            ItemDef[] items = ContentPack.Pawns().Items;
            Assert.That(items[ItemIndex.Wood].marketValue, Is.EqualTo(1));
            Assert.That(items[ItemIndex.Meal].marketValue, Is.EqualTo(6));
            Assert.That(items[ItemIndex.MedicalSupplies].marketValue, Is.EqualTo(20));
            Assert.That(items[ItemIndex.Pistol].marketValue, Is.EqualTo(150));
        }
    }
}
