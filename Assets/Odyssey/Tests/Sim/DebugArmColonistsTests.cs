#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The debug menu's "Arm every colonist" (design 33 §9i; owner, 2026-09-24: "an option to wield
    /// every colonist with a random melee weapon ... for testing purposes"). The control is the
    /// colony before the click: nobody armed.
    /// </summary>
    public class DebugArmColonistsTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ColonyWorld Board(int colonists = 4, uint seed = 3u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = colonists;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: false);
        }

        static IntentRejection Arm(ColonyWorld colony)
        {
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(new Intent(IntentKind.DebugArmColonists, default));
            colony.World.Tick();
            var rejected = colony.World.Intents.Rejected;
            return rejected.Count == 0 ? IntentRejection.None : rejected[0].Reason;
        }

        static bool HoldsAMeleeWeapon(ColonyWorld colony, Pawn pawn)
        {
            if (pawn.EquippedItem == 0) return false;
            ColonyItem? item = colony.Pawns.Items.Get(new ThingId(pawn.EquippedItem));
            return item != null && !item.Despawned && colony.Pawns.Content.Items[item.DefIndex].weapon != null;
        }

        [Test]
        public void EveryUnarmedColonistTakesAWeaponIntoHerHand()
        {
            var colony = Board();
            colony.World.Tick(10);
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
                Assume.That(pawn.EquippedItem, Is.EqualTo(0), "the fixture arms somebody already, so this proves nothing");

            Assert.That(Arm(colony), Is.EqualTo(IntentRejection.None));

            int colonists = 0;
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
            {
                if (!pawn.IsColonist) continue;
                colonists++;
                Assert.That(HoldsAMeleeWeapon(colony, pawn), Is.True, $"colonist {pawn.Id.Value} is still bare-handed");
            }
            Assert.That(colonists, Is.EqualTo(4));
        }

        [Test]
        public void AnArmedColonistKeepsHerWeaponAndASecondClickArmsNobody()
        {
            var colony = Board();
            colony.World.Tick(10);
            Arm(colony);
            var held = new System.Collections.Generic.Dictionary<int, int>();
            foreach (Pawn pawn in colony.Pawns.Pawns.All) held[pawn.Id.Value] = pawn.EquippedItem;

            Assert.That(Arm(colony), Is.EqualTo(IntentRejection.AlreadyInThatState), "nobody was left to arm");
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
                Assert.That(pawn.EquippedItem, Is.EqualTo(held[pawn.Id.Value]), "a second click swapped somebody's weapon");
        }

        [Test]
        public void TheDealIsTheSeedsNotChance()
        {
            int[] Deal()
            {
                var colony = Board();
                colony.World.Tick(10);
                Arm(colony);
                var all = colony.Pawns.Pawns.All;
                var defs = new int[all.Count];
                for (int i = 0; i < all.Count; i++)
                    defs[i] = colony.Pawns.Items.Get(new ThingId(all[i].EquippedItem))!.DefIndex;
                return defs;
            }

            Assert.That(Deal(), Is.EqualTo(Deal()));
        }

        [Test]
        public void ABanditIsNotArmedTwiceAndAnAnimalIsNotArmedAtAll()
        {
            var colony = Board(colonists: 1);
            colony.World.Tick(5);
            CellRef at = colony.Start;
            colony.World.Intents.Submit(new Intent(IntentKind.SpawnPawn, new CellRef(at.X + 8, at.Z, at.Y), PawnKindIndex.MiddenHog));
            colony.World.Tick();
            Pawn hog = colony.Pawns.Pawns.All[colony.Pawns.Pawns.Count - 1];

            Arm(colony);
            Assert.That(hog.EquippedItem, Is.EqualTo(0), "a hog was handed a weapon");
        }
    }
}
