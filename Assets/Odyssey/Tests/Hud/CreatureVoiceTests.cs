#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The butcher's voice and its cleaver's whoosh (design 62 §8d; owner, 2026-09-26: a grunt or a
    /// squeal "when the pig gets a hit or makes a strike ... loud when he knocks people back or
    /// even when hit", the oink "when they get knocked down or killed", and "a swoosh like a sword
    /// ... deeper").
    /// </summary>
    public class CreatureVoiceTests
    {
        [TestCase(CombatEventKind.Swing, true, false, VoiceCue.Strike, true)]
        [TestCase(CombatEventKind.SwingCritical, true, false, VoiceCue.Strike, true)]
        [TestCase(CombatEventKind.KnockedBack, true, false, VoiceCue.Fling, true)]
        [TestCase(CombatEventKind.Slam, true, false, VoiceCue.Fling, true)]
        [TestCase(CombatEventKind.Hit, false, true, VoiceCue.Hurt, false)]
        [TestCase(CombatEventKind.Downed, false, true, VoiceCue.Down, false)]
        [TestCase(CombatEventKind.Died, false, true, VoiceCue.Down, false)]
        public void EachMomentIsVoicedByTheOneItHappensTo(CombatEventKind kind, bool attacker, bool target,
            VoiceCue expected, bool fromAttacker)
        {
            Assert.That(CreatureVoice.For(kind, attacker, target, out bool from), Is.EqualTo(expected));
            Assert.That(from, Is.EqualTo(fromAttacker));
        }

        /// <summary>The butcher striking a colonist is not a hurt squeal, and a colonist's blow on nobody voiced is silent.</summary>
        [TestCase(CombatEventKind.Hit, true, false)]
        [TestCase(CombatEventKind.Swing, false, true)]
        [TestCase(CombatEventKind.KnockedBack, false, true)]
        [TestCase(CombatEventKind.Downed, true, false)]
        [TestCase(CombatEventKind.Miss, true, true)]
        [TestCase(CombatEventKind.Stun, true, true)]
        public void TheWrongOneIsSilent(CombatEventKind kind, bool attacker, bool target)
        {
            Assert.That(CreatureVoice.For(kind, attacker, target, out _), Is.EqualTo(VoiceCue.None));
        }

        [Test]
        public void EveryMomentHasASuffix()
        {
            Assert.That(CreatureVoice.Suffix(VoiceCue.Strike), Is.EqualTo("strike"));
            Assert.That(CreatureVoice.Suffix(VoiceCue.Hurt), Is.EqualTo("hurt"));
            Assert.That(CreatureVoice.Suffix(VoiceCue.Fling), Is.EqualTo("fling"));
            Assert.That(CreatureVoice.Suffix(VoiceCue.Down), Is.EqualTo("down"));
            Assert.That(CreatureVoice.Suffix(VoiceCue.None), Is.Empty);
        }

        /// <summary>
        /// A weapon of its own whooshes deep; a held weapon keeps the sword's whoosh; teeth and fists
        /// stay silent; a sharp critical is still the slice.
        /// </summary>
        [Test]
        public void ANaturalWeaponWhooshesDeep()
        {
            Assert.That(CombatSoundTiming.SwingCue(CombatEventKind.Swing, heldWeapon: false, sharp: true, wieldsNatural: true),
                Is.EqualTo(CombatCue.HeavyWhoosh));
            Assert.That(CombatSoundTiming.SwingCue(CombatEventKind.SwingCritical, false, sharp: true, wieldsNatural: true),
                Is.EqualTo(CombatCue.Slice));
            Assert.That(CombatSoundTiming.SwingCue(CombatEventKind.Swing, heldWeapon: true, sharp: false),
                Is.EqualTo(CombatCue.Whoosh));
            Assert.That(CombatSoundTiming.SwingCue(CombatEventKind.Swing, heldWeapon: false, sharp: true),
                Is.EqualTo(CombatCue.None), "a bite is silent");
        }

        /// <summary>The deep whoosh is scheduled exactly as the whoosh: the bake put its peak at the same 40 ms.</summary>
        [Test]
        public void TheDeepWhooshIsTimedAsTheWhoosh()
        {
            Assert.That(CombatSoundTiming.PeakSeconds(CombatCue.HeavyWhoosh), Is.EqualTo(CombatSoundTiming.PeakSeconds(CombatCue.Whoosh)));
            Assert.That(CombatSoundTiming.LeadSeconds(CombatCue.HeavyWhoosh), Is.EqualTo(CombatSoundTiming.LeadSeconds(CombatCue.Whoosh)));
            Assert.That(CombatSoundTiming.Decide(CombatCue.HeavyWhoosh, 100, 90, 60f),
                Is.EqualTo(CombatSoundTiming.Decide(CombatCue.Whoosh, 100, 90, 60f)));
        }

        /// <summary>The table says who wields: a kind past it, or one without, does not.</summary>
        [Test]
        public void TheWieldsTableIsReadByKind()
        {
            var sides = new BloodSides(new bool?[0], new bool?[] { null, false, true }, false, new[] { false, false, true });
            Assert.That(sides.WieldsNatural(2), Is.True);
            Assert.That(sides.WieldsNatural(1), Is.False);
            Assert.That(sides.WieldsNatural(9), Is.False);
            Assert.That(sides.WieldsNatural(-1), Is.False);
        }
    }
}
