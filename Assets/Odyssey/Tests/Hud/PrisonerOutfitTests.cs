#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// Which outfit a pawn wears once custody exists (design 60 §11d): the one owner,
    /// <see cref="PawnOutfits"/>, asks custody first. A prisoner laid in her cell wears the jumpsuit,
    /// escaping or not; a recruit is not hostile, so she wears the colony's suit with no rule of
    /// her own; a bandit at large is dressed as a bandit, as before.
    /// </summary>
    public class PrisonerOutfitTests
    {
        static PawnView View(PawnFlags flags, PawnCustody custody = PawnCustody.Free, bool dressed = false, int kind = 3) =>
            new PawnView(new PawnId(5), default, 0, 0, 0, kind: kind, flags: flags, custody: custody, dressed: dressed);

        const PawnFlags Person = PawnFlags.Person;
        const PawnFlags Hostile = PawnFlags.Person | PawnFlags.Hostile;

        [Test]
        public void ABanditAtLargeIsDressedAsABandit() =>
            Assert.That(PawnOutfits.For(View(Hostile)), Is.EqualTo(PawnOutfit.Bandit));

        /// <summary>
        /// Held but not yet dressed, she wears what she came in (review 2026-09-26): a surrendered
        /// raider walking to her cell in her gang's clothes, not the colony's suit — custody has
        /// cleared her Hostile flag, so the flags alone would have dressed her as one of ours.
        /// </summary>
        [Test]
        public void AHeldRaiderNotYetInHerCellKeepsHerGangClothes() =>
            Assert.That(PawnOutfits.For(View(Person, PawnCustody.Prisoner)), Is.EqualTo(PawnOutfit.Bandit));

        [Test]
        public void AnArrestedColonistNotYetInHerCellKeepsTheColonysSuit()
        {
            Assert.That(PawnOutfits.For(View(Person, PawnCustody.Prisoner, kind: 0)), Is.EqualTo(PawnOutfit.Issued));
            Assert.That(PawnOutfits.For(View(Hostile, PawnCustody.Escaping, kind: 0)), Is.EqualTo(PawnOutfit.Issued),
                "resisting makes her hostile, and never dresses her as a bandit");
        }

        [Test]
        public void APrisonerInHerCellWearsTheJumpsuit() =>
            Assert.That(PawnOutfits.For(View(Person, PawnCustody.Prisoner, dressed: true)), Is.EqualTo(PawnOutfit.Prisoner));

        [Test]
        public void AnEscapeeStillWearsTheJumpsuitSoABreakoutShows() =>
            Assert.That(PawnOutfits.For(View(Hostile, PawnCustody.Escaping, dressed: true)), Is.EqualTo(PawnOutfit.Prisoner));

        [Test]
        public void ARecruitWearsTheColonysSuit() =>
            Assert.That(PawnOutfits.For(View(Person)), Is.EqualTo(PawnOutfit.Issued));

        [Test]
        public void APrisonerIsNotOnTheRosterAndSleepsFromThePrisonPool()
        {
            PawnView held = View(Person, PawnCustody.Prisoner);
            Assert.That(held.IsColonist, Is.False);
            Assert.That(held.IsPrisoner, Is.True);
            Assert.That(BedRule.UserOf(held), Is.EqualTo(BedUser.Prisoner));
            Assert.That(BedRule.UserOf(View(Hostile, PawnCustody.Escaping)), Is.EqualTo(BedUser.Prisoner),
                "an escapee keeps the bed she is carried back to");
            Assert.That(BedRule.UserOf(View(Person, PawnCustody.Released)), Is.EqualTo(BedUser.None));
            Assert.That(BedRule.UserOf(View(Person)), Is.EqualTo(BedUser.Colonist));
        }
    }
}
