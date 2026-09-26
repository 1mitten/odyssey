#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The Gear tab's model beyond the hand (design 47 §2, §4): the worn slots, the jumpsuit, the
    /// kit, the effects line and the loadout — what the tab shows today with nothing worn, and what
    /// it shows under the preview, where the hand stays real.
    /// </summary>
    public class GearTabModelTests
    {
        static readonly PawnId Ada = new PawnId(1), Ben = new PawnId(2);

        static WorldSnapshot Board(int? weapon = null, PawnFlags flags = PawnFlags.None)
        {
            WorldSnapshot frame = Frame.Write();
            frame.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), 800, 800, 800, flags: PawnFlags.Person | flags));
            frame.AddPawn(new PawnView(Ben, new CellRef(2, 1, 1), 800, 800, 800, flags: PawnFlags.Person));
            if (weapon.HasValue) frame.AddPawnAspect(new PawnAspect(Ada, CombatAspectNames.WeaponKey, weapon.Value));
            return frame;
        }

        static GearModel Model(GearPreview? preview = null) => new GearModel { Preview = preview };

        // ---- today: nothing worn --------------------------------------------------------------

        [Test]
        public void TheRowsAreTheDollsOrder()
        {
            GearModel gear = Model();
            gear.Refresh(Board(), Ada);
            Assert.That(gear.Rows.Select(r => r.Slot), Is.EqualTo(new[]
                { GearSlot.Head, GearSlot.Face, GearSlot.Body, GearSlot.Back, GearSlot.Armour, GearSlot.Weapon }),
                "left column Head, Face, Body; right column Back, Armour, then the weapon at the hand");
        }

        /// <summary>The empty Body slot is the issued jumpsuit, a real entry: nobody is ever naked.</summary>
        [Test]
        public void AnEmptyBodyIsTheIssuedJumpsuit()
        {
            GearModel gear = Model();
            gear.Refresh(Board(), Ada);
            GearRow body = gear.Row(GearSlot.Body);
            Assert.That(body.State, Is.EqualTo(GearSlotState.Jumpsuit));
            Assert.That(body.Name, Is.EqualTo("Issued jumpsuit"));
            Assert.That(body.QualityWord, Is.EqualTo("Always worn"));
            Assert.That(body.SlotName, Is.EqualTo("Body"));
        }

        [TestCase(GearSlot.Head)]
        [TestCase(GearSlot.Face)]
        [TestCase(GearSlot.Back)]
        [TestCase(GearSlot.Armour)]
        public void EveryOtherWornSlotIsNothingWorn(GearSlot slot)
        {
            GearModel gear = Model();
            gear.Refresh(Board(), Ada);
            GearRow row = gear.Row(slot);
            Assert.That(row.State, Is.EqualTo(GearSlotState.Empty));
            Assert.That(row.Name, Is.EqualTo("Nothing worn"));
            Assert.That(row.IconKey, Is.Empty);
            Assert.That(row.Preview, Is.False);
        }

        /// <summary>Two belt slots open, four pack slots locked, and the hint shown once.</summary>
        [Test]
        public void WithNoPackTheBeltIsOpenAndThePackSlotsAreLocked()
        {
            GearModel gear = Model();
            gear.Refresh(Board(), Ada);
            Assert.That(gear.Kit.Select(k => k.State), Is.EqualTo(new[]
            {
                KitTileState.Empty, KitTileState.Empty,
                KitTileState.Locked, KitTileState.Locked, KitTileState.Locked, KitTileState.Locked,
            }));
            Assert.That(gear.PackHint, Is.True);
        }

        /// <summary>
        /// Every effect is on the line at its bare value, so the line never reflows, and each says
        /// which figure it is so it can be judged (design 59): 0 % armour is red.
        /// </summary>
        [Test]
        public void TheBareColonistsEffectsAreTheDefaultsAndStayOnTheLine()
        {
            GearModel gear = Model();
            gear.Refresh(Board(), Ada);
            Assert.That(gear.Effects.Select(e => e.Label), Is.EqualTo(new[] { "Armour", "Warmth", "Rain", "Kit" }));
            Assert.That(gear.Effects.Select(e => e.Value), Is.EqualTo(new[] { "0%", "16 to 26 °C", "0%", "0 of 2" }));
            Assert.That(gear.Effects.Select(e => e.Kind), Is.EqualTo(new[]
                { GearEffectKind.Armour, GearEffectKind.Warmth, GearEffectKind.Rain, GearEffectKind.Kit }));
            Assert.That(GearModel.Ink(gear.Effects[0], null), Is.EqualTo(HudTheme.Bad), "no armour is red");
        }

        [Test]
        public void NoLoadoutIsNone()
        {
            GearModel gear = Model(new GearPreview());
            gear.Refresh(Board(), Ada);
            Assert.That(gear.LoadoutName, Is.EqualTo("None"));
            Assert.That(gear.HasLoadout, Is.False);
        }

        [Test]
        public void ADownedColonistsTabSaysSo()
        {
            GearModel gear = Model();
            gear.Refresh(Board(ItemHandle.Bat, PawnFlags.Downed), Ada);
            Assert.That(gear.Downed, Is.True);
            Assert.That(gear.DownedReason, Is.EqualTo("Downed. Only the Strip order can take this."));

            gear.Refresh(Board(ItemHandle.Bat), Ada);
            Assert.That(gear.Downed, Is.False, "the control: standing");
        }

        /// <summary>The pane refreshes fifteen times a second: the same frame builds nothing.</summary>
        [Test]
        public void TheSameFrameBuildsNothing()
        {
            GearModel gear = Model();
            WorldSnapshot frame = Board(ItemHandle.Bat);
            gear.Refresh(frame, Ada);
            int version = gear.Version;
            gear.Refresh(frame, Ada);
            Assert.That(gear.Version, Is.EqualTo(version));

            gear.Refresh(Board(ItemHandle.Bat, PawnFlags.Drawn), Ada);
            Assert.That(gear.Version, Is.GreaterThan(version), "the control: drawing it is a change");
        }

        // ---- the preview -------------------------------------------------------------------------

        /// <summary>The specification's 21c, laid over everything but the hand.</summary>
        [Test]
        public void ThePreviewIsTheSpecificationsFullKit()
        {
            var preview = new GearPreview();
            preview.Set(true);
            GearModel gear = Model(preview);
            gear.Refresh(Board(ItemHandle.Crowbar, PawnFlags.Drawn), Ada);

            Assert.That(gear.Rows.Select(r => r.Name), Is.EqualTo(new[]
                { "Wool cap", "Gas mask", "Field coat", "Pack", "Padded vest", "Crowbar" }));
            Assert.That(gear.Rows.Select(r => r.QualityWord), Is.EqualTo(new[]
                { "Normal", "Decent", "Uber", "Poor", "Normal", "" }),
                "a weapon has no quality in the simulation, so none is shown");
            Assert.That(gear.Row(GearSlot.Weapon).CarryWord, Is.EqualTo("Drawn"));
            Assert.That(gear.Rows.Take(5).All(r => r.Preview), Is.True);
            Assert.That(gear.Row(GearSlot.Weapon).Preview, Is.False, "the hand is never made up");

            Assert.That(gear.Kit.Select(k => k.State), Is.EqualTo(new[]
            {
                KitTileState.Filled, KitTileState.Filled, KitTileState.Filled, KitTileState.Filled,
                KitTileState.Empty, KitTileState.Empty,
            }));
            Assert.That(gear.Kit.Take(4).Select(k => k.CountText), Is.EqualTo(new[] { "4", "2", "1", "1" }));
            Assert.That(gear.Kit[0].Name, Is.EqualTo("Medical supplies"));
            Assert.That(gear.PackHint, Is.False);

            Assert.That(gear.Effects.Select(e => e.Value), Is.EqualTo(new[] { "24%", "4 to 26 °C", "50%", "4 of 6" }));
            Assert.That(gear.Effects[0].Armour, Is.EqualTo(24));
            Assert.That((gear.Effects[1].WarmthLow, gear.Effects[1].WarmthHigh), Is.EqualTo((400, 2600)));
            Assert.That(gear.LoadoutName, Is.EqualTo("Doctor"));
            Assert.That(gear.HasLoadout, Is.True);
        }

        /// <summary>A bare hand under the preview is still the bare hand.</summary>
        [Test]
        public void ThePreviewDoesNotArmAnybody()
        {
            var preview = new GearPreview();
            preview.Set(true);
            GearModel gear = Model(preview);
            gear.Refresh(Board(), Ada);
            Assert.That(gear.Row(GearSlot.Weapon).State, Is.EqualTo(GearSlotState.Empty));
            Assert.That(gear.Row(GearSlot.Weapon).Name, Is.EqualTo("Bare hands"));
        }

        /// <summary>Taking the pack off locks its four slots and loses what was in them.</summary>
        [Test]
        public void RemovingThePackLocksItsSlots()
        {
            var preview = new GearPreview();
            preview.Set(true);
            GearModel gear = Model(preview);
            gear.Refresh(Board(), Ada);

            preview.Remove(Ada, GearSlot.Back);
            gear.Refresh(Board(), Ada);

            Assert.That(gear.Row(GearSlot.Back).State, Is.EqualTo(GearSlotState.Empty));
            Assert.That(gear.Kit.Select(k => k.State), Is.EqualTo(new[]
            {
                KitTileState.Filled, KitTileState.Filled,
                KitTileState.Locked, KitTileState.Locked, KitTileState.Locked, KitTileState.Locked,
            }));
            Assert.That(gear.PackHint, Is.True);
            Assert.That(gear.Effects[3].Value, Is.EqualTo("2 of 2"));
        }

        /// <summary>Taking the coat off gives back the jumpsuit, and its warmth and rain go with it.</summary>
        [Test]
        public void RemovingTheCoatGivesBackTheJumpsuit()
        {
            var preview = new GearPreview();
            preview.Set(true);
            GearModel gear = Model(preview);
            preview.Remove(Ada, GearSlot.Body);
            gear.Refresh(Board(), Ada);

            Assert.That(gear.Row(GearSlot.Body).State, Is.EqualTo(GearSlotState.Jumpsuit));
            Assert.That(gear.Effects.Select(e => e.Value).Take(3), Is.EqualTo(new[] { "18%", "14 to 26 °C", "0%" }));
        }

        /// <summary>An edit is one colonist's; switching off forgets every edit and turns the tab back.</summary>
        [Test]
        public void EditsArePerColonistAndSwitchingOffForgetsThem()
        {
            var preview = new GearPreview();
            preview.Set(true);
            preview.Remove(Ada, GearSlot.Head);
            preview.SetLoadout(Ada, 2);

            GearModel gear = Model(preview);
            gear.Refresh(Board(), Ben);
            Assert.That(gear.Row(GearSlot.Head).Name, Is.EqualTo("Wool cap"), "Ben kept his cap");
            gear.Refresh(Board(), Ada);
            Assert.That(gear.Row(GearSlot.Head).State, Is.EqualTo(GearSlotState.Empty));
            Assert.That(gear.LoadoutName, Is.EqualTo("Winter"));

            preview.Set(false);
            gear.Refresh(Board(), Ada);
            Assert.That(gear.Row(GearSlot.Face).State, Is.EqualTo(GearSlotState.Empty), "off is today's tab");
            Assert.That(gear.LoadoutName, Is.EqualTo("None"));

            preview.Set(true);
            gear.Refresh(Board(), Ada);
            Assert.That(gear.Row(GearSlot.Head).Name, Is.EqualTo("Wool cap"), "switched on again, it starts at 21c");
        }

        [Test]
        public void ANewSessionStartsWithThePreviewOff()
        {
            var preview = new GearPreview();
            preview.Set(true);
            preview.Reset();
            Assert.That(preview.On, Is.False);
            Assert.That(preview.Kit(Ada), Is.Empty);
            Assert.That(preview.Loadout(Ada), Is.Zero);
        }

        /// <summary>A popover's lines say what one thing does, and nothing for a thing that does nothing.</summary>
        [Test]
        public void APopoverSaysWhatOneThingDoes()
        {
            var preview = new GearPreview();
            preview.Set(true);
            GearModel gear = Model(preview);
            gear.Refresh(Board(), Ada);

            GearRow coat = gear.Row(GearSlot.Body);
            Assert.That((coat.EffectA, coat.EffectAValue), Is.EqualTo(("Armour", "6%")));
            Assert.That((coat.EffectB, coat.EffectBValue), Is.EqualTo(("Warmth", "6 to 26 °C")));
            GearRow mask = gear.Row(GearSlot.Face);
            Assert.That(mask.EffectA, Is.Empty);
        }

        [Test]
        public void TheKitCountIsTheRegistrysWords() =>
            Assert.That(GearModel.KitCount(4, 6), Is.EqualTo("4 of 6"));

        [Test]
        public void EverySlotHasAWord()
        {
            foreach (GearSlot slot in GearModel.Order)
                Assert.That(GearModel.SlotLabel(slot), Is.Not.Empty.And.Not.Contains("ui."), slot.ToString());
            Assert.That(GearModel.Order.Distinct().Count(), Is.EqualTo(GearModel.SlotCount));
        }
    }
}
