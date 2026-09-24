#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The blood seam's rules (design 33 §7d): which moments bleed, and whether a blow was sharp
    /// or blunt, resolved in the order the simulation arms a pawn — the weapon, the species' own
    /// attack, bare hands.
    /// </summary>
    public class BloodModelTests
    {
        [Test]
        public void EveryLandedHitSpurtsAndNothingElseDoes()
        {
            Assert.That(BloodModel.For(CombatEventKind.Hit), Is.EqualTo(BloodMark.Spurt));

            foreach (CombatEventKind quiet in new[]
                     {
                         CombatEventKind.None, CombatEventKind.Swing, CombatEventKind.Miss, CombatEventKind.Dodge,
                         CombatEventKind.Stun, CombatEventKind.Recovered,
                     })
                Assert.That(BloodModel.For(quiet), Is.EqualTo(BloodMark.None), $"a {quiet} bled");
        }

        [Test]
        public void ADownAndADeathLeaveAPoolAndADeathsIsTheLarger()
        {
            Assert.That(BloodModel.For(CombatEventKind.Downed), Is.EqualTo(BloodMark.Pool));
            Assert.That(BloodModel.For(CombatEventKind.Died), Is.EqualTo(BloodMark.Pool));
            Assert.That(BloodModel.PoolSize(CombatEventKind.Died), Is.EqualTo(1f));
            Assert.That(BloodModel.PoolSize(CombatEventKind.Downed),
                Is.GreaterThan(0f).And.LessThan(BloodModel.PoolSize(CombatEventKind.Died)));
            Assert.That(BloodModel.PoolSize(CombatEventKind.Hit), Is.EqualTo(0f));
        }

        // Item defs: 0 a bat, 1 a machete, 2 something that is not a weapon.
        const int Bat = 0, Machete = 1, Plank = 2;

        // Pawn kinds: 0 a colonist (a person, no natural attack), 1 a hog (blunt tusks), 2 a rat
        // (sharp teeth), 3 a marauder (a person).
        const int Colonist = 0, HogKind = 1, RatKind = 2, Marauder = 3;

        static BloodSides Sides() => new BloodSides(
            new bool?[] { false, true, null },
            new bool?[] { null, false, true, null },
            fistsSharp: false);

        [Test]
        public void TheWeaponInTheHandDecides()
        {
            BloodSides sides = Sides();
            Assert.That(sides.IsSharp(Machete, Colonist), Is.True, "a machete cut blunt");
            Assert.That(sides.IsSharp(Bat, Colonist), Is.False, "a bat cut sharp");
            Assert.That(sides.IsSharp(Machete, Marauder), Is.True);
            // The weapon outranks the species, as it does in the simulation.
            Assert.That(sides.IsSharp(Bat, RatKind), Is.False);
        }

        [Test]
        public void WithNoWeaponTheSpeciesDecidesAndAPersonHasFists()
        {
            BloodSides sides = Sides();
            Assert.That(sides.IsSharp(-1, RatKind), Is.True, "a rat's bite was blunt");
            Assert.That(sides.IsSharp(-1, HogKind), Is.False, "the hog's tusks are blunt in the content");
            Assert.That(sides.IsSharp(-1, Colonist), Is.False, "fists were sharp");
            Assert.That(sides.IsSharp(-1, Marauder), Is.False);
        }

        /// <summary>
        /// An item that is not a weapon, a def past the table and an attacker who has left the
        /// frame all fall through to the next rule rather than throwing — the mapping is total.
        /// </summary>
        [Test]
        public void TheResolutionIsTotal()
        {
            BloodSides sides = Sides();
            Assert.That(sides.IsSharp(Plank, RatKind), Is.True, "a non-weapon did not fall through to the species");
            Assert.That(sides.IsSharp(99, Colonist), Is.False);
            Assert.That(sides.IsSharp(-1, -1), Is.False, "an attacker gone from the frame fights with fists");
            Assert.That(sides.IsSharp(-1, 99), Is.False);
            Assert.That(BloodSides.AllBlunt.IsSharp(Machete, RatKind), Is.False);

            var sharpFists = new BloodSides(new bool?[0], new bool?[0], fistsSharp: true);
            Assert.That(sharpFists.IsSharp(-1, Colonist), Is.True, "the fists' answer is the content's, not a literal");
        }
    }
}
