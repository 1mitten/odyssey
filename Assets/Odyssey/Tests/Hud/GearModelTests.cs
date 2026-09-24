#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The gear seam (design 33 §9d): what a pawn holds, as the Gear tab will list it when it is
    /// built. One row today — the weapon in the hand, drawn or at the hip, or the bare hands.
    /// </summary>
    public class GearModelTests
    {
        static readonly PawnId Ada = new PawnId(1), Hog = new PawnId(2), Raider = new PawnId(3);

        /// <summary>Ada with <paramref name="flags"/> (a person besides) and <paramref name="weapon"/> in the hand if given.</summary>
        static WorldSnapshot Board(int? weapon, PawnFlags flags = PawnFlags.None)
        {
            WorldSnapshot frame = Frame.Write();
            frame.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), 800, 800, 800, flags: PawnFlags.Person | flags));
            frame.AddPawn(new PawnView(Hog, new CellRef(2, 1, 1), 800, 800, 800, kind: 1, flags: PawnFlags.None));
            if (weapon.HasValue) frame.AddPawnAspect(new PawnAspect(Ada, CombatAspectNames.WeaponKey, weapon.Value));
            return frame;
        }

        static GearRow Only(GearModel gear)
        {
            Assert.That(gear.Rows.Count, Is.EqualTo(1), "a person holds exactly one row of gear today");
            return gear.Rows[0];
        }

        [Test]
        public void ANakedHandIsTheBareHandsRow()
        {
            var gear = new GearModel();
            Assert.That(gear.Refresh(Board(weapon: null), Ada), Is.True);

            GearRow row = Only(gear);
            Assert.That(row.Slot, Is.EqualTo(GearSlot.Weapon));
            Assert.That(row.SlotName, Is.EqualTo(Registry.Label("ui.combat.weapon")));
            Assert.That(row.ItemDef, Is.EqualTo(-1));
            Assert.That(row.Name, Is.EqualTo(Registry.Label("ui.combat.barehands")));
            Assert.That(row.IconKey, Is.Empty);
            Assert.That(row.Carry, Is.EqualTo(GearCarry.None));
            Assert.That(row.CarryWord, Is.Empty);
        }

        /// <summary>Held and with no reason to fight: at the hip, named by the registry.</summary>
        [Test]
        public void AWeaponWithNoFightIsAtTheHip()
        {
            var gear = new GearModel();
            gear.Refresh(Board(ItemHandle.Bat), Ada);

            GearRow row = Only(gear);
            Assert.That(row.ItemDef, Is.EqualTo(ItemHandle.Bat));
            Assert.That(row.Name, Is.EqualTo(Registry.Label("ui.item.bat")));
            Assert.That(row.IconKey, Is.EqualTo("ui.item.bat"));
            Assert.That(row.Carry, Is.EqualTo(GearCarry.AtHip));
            Assert.That(row.CarryWord, Is.EqualTo(Registry.Label("ui.combat.athip")));
            Assert.That(row.CarryWord, Is.EqualTo("At the hip"));
        }

        /// <summary>The published <see cref="PawnFlags.Drawn"/> is the whole of "drawn" (design 33 §8b).</summary>
        [Test]
        public void TheDrawnFlagIsADrawnWeapon()
        {
            var gear = new GearModel();
            gear.Refresh(Board(ItemHandle.Machete, PawnFlags.Drawn | PawnFlags.Drafted), Ada);

            GearRow row = Only(gear);
            Assert.That(row.Name, Is.EqualTo(Registry.Label("ui.item.machete")));
            Assert.That(row.Carry, Is.EqualTo(GearCarry.Drawn));
            Assert.That(row.CarryWord, Is.EqualTo(Registry.Label("ui.combat.drawn")));
            Assert.That(row.CarryWord, Is.EqualTo("Drawn"));
        }

        /// <summary>
        /// The control for the one above: drafted and not drawn is still at the hip, so the carry
        /// is the flag's and not a second reading of the draft.
        /// </summary>
        [Test]
        public void DraftedWithoutTheFlagIsStillAtTheHip()
        {
            var gear = new GearModel();
            gear.Refresh(Board(ItemHandle.Machete, PawnFlags.Drafted), Ada);
            Assert.That(Only(gear).Carry, Is.EqualTo(GearCarry.AtHip));
        }

        /// <summary>A drawn flag over empty hands is still the bare hands: nothing to draw.</summary>
        [Test]
        public void TheDrawnFlagOverEmptyHandsIsTheBareHands()
        {
            var gear = new GearModel();
            gear.Refresh(Board(weapon: null, PawnFlags.Drawn), Ada);

            GearRow row = Only(gear);
            Assert.That(row.Carry, Is.EqualTo(GearCarry.None));
            Assert.That(row.Name, Is.EqualTo(Registry.Label("ui.combat.barehands")));
        }

        /// <summary>A bandit is a person with a weapon, always drawn: the model is not the colony's alone.</summary>
        [Test]
        public void ABanditsGearIsReadTheSameWay()
        {
            WorldSnapshot frame = Frame.Write();
            frame.AddPawn(new PawnView(Raider, new CellRef(3, 1, 1), 800, 800, 800, kind: 3,
                flags: PawnFlags.Person | PawnFlags.Hostile | PawnFlags.Drawn));
            frame.AddPawnAspect(new PawnAspect(Raider, CombatAspectNames.WeaponKey, ItemHandle.Crowbar));

            var gear = new GearModel();
            Assert.That(gear.Refresh(frame, Raider), Is.True);
            GearRow row = Only(gear);
            Assert.That(row.Name, Is.EqualTo(Registry.Label("ui.item.crowbar")));
            Assert.That(row.Carry, Is.EqualTo(GearCarry.Drawn));
        }

        /// <summary>An animal holds nothing; a pawn the frame no longer carries has nothing to list.</summary>
        [Test]
        public void AnAnimalHoldsNothingAndAGonePawnIsNotListed()
        {
            var gear = new GearModel();
            Assert.That(gear.Refresh(Board(ItemHandle.Bat), Hog), Is.True);
            Assert.That(gear.Rows, Is.Empty, "a hog has no gear");

            gear.Refresh(Board(ItemHandle.Bat), Ada);
            Assert.That(gear.Rows, Is.Not.Empty);
            Assert.That(gear.Refresh(Board(ItemHandle.Bat), new PawnId(99)), Is.False);
            Assert.That(gear.Rows, Is.Empty, "a refresh for somebody gone clears the last subject's rows");
        }

        /// <summary>
        /// The Gear tab and the Health tab's weapon row will stand side by side on one pane, so
        /// they must name the same thing. Across every weapon and the bare hands.
        /// </summary>
        [TestCase(-1)]
        [TestCase(ItemHandle.Bat)]
        [TestCase(ItemHandle.Crowbar)]
        [TestCase(ItemHandle.Machete)]
        [TestCase(ItemHandle.ArcBlade)]
        public void TheGearRowNamesWhatTheHealthTabNames(int weapon)
        {
            WorldSnapshot frame = Board(weapon >= 0 ? weapon : (int?)null);
            frame.AddPawnAspect(new PawnAspect(Ada, CombatAspectNames.HpMaxKey, 100_000));

            var pane = new InspectModel();
            pane.SetColonist(Ada);
            pane.Refresh(frame);
            string health = string.Empty;
            foreach (InspectRow row in pane.HealthRows)
                if (row.Name == Registry.Label("ui.combat.weapon")) health = row.Value;

            var gear = new GearModel();
            gear.Refresh(frame, Ada);
            Assert.That(health, Is.Not.Empty, "the Health tab has no weapon row");
            Assert.That(Only(gear).Name, Is.EqualTo(health));
        }
    }
}
