#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// Which outfit a pawn wears once custody exists (design 58 §11d): the one owner,
    /// <see cref="PawnOutfits"/>, asks custody first. A prisoner laid in her cell wears the jumpsuit,
    /// escaping or not; a recruit is not hostile, so she wears the colony's suit with no rule of
    /// her own; a bandit at large is dressed as a bandit, as before.
    /// </summary>
    public class PrisonerOutfitTests
    {
        static PawnView View(PawnFlags flags, PawnCustody custody = PawnCustody.Free, bool dressed = false) =>
            new PawnView(new PawnId(5), default, 0, 0, 0, kind: 3, flags: flags, custody: custody, dressed: dressed);

        const PawnFlags Person = PawnFlags.Person;
        const PawnFlags Hostile = PawnFlags.Person | PawnFlags.Hostile;

        [Test]
        public void ABanditAtLargeIsDressedAsABandit() =>
            Assert.That(PawnOutfits.For(View(Hostile)), Is.EqualTo(PawnOutfit.Bandit));

        [Test]
        public void AHeldPrisonerNotYetInHerCellKeepsHerClothes() =>
            Assert.That(PawnOutfits.For(View(Person, PawnCustody.Prisoner)), Is.EqualTo(PawnOutfit.Issued));

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
            Assert.That(BedRule.UserOf(View(Hostile, PawnCustody.Escaping)), Is.EqualTo(BedUser.None));
            Assert.That(BedRule.UserOf(View(Person)), Is.EqualTo(BedUser.Colonist));
        }
    }
}
