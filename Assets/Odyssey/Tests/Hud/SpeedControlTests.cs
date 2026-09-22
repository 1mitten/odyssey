#nullable enable
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// What an unpause comes back to.
    ///
    /// The owner's report, 2026-09-21: "if I select x3, pause and then unpause — I expect to be
    /// back at X3". The toggle used to answer the literal 1, so every pause taken to give an
    /// order quietly undid the speed the player had chosen a moment before.
    /// </summary>
    public class SpeedControlTests
    {
        [Test]
        public void APauseComesBackToTheSpeedItStopped()
        {
            var speed = new SpeedControl();

            Assert.That(speed.Resolve(3, 1), Is.EqualTo(3), "the player asks for triple");
            Assert.That(speed.Resolve(0, 3), Is.EqualTo(0), "and pauses");
            Assert.That(speed.Resolve(0, 0), Is.EqualTo(3), "and is back at triple, not at normal");
        }

        [Test]
        public void AWorldThatHasOnlyEverBeenPausedStartsAtNormal()
        {
            var speed = new SpeedControl();
            Assert.That(speed.Resume, Is.EqualTo(1));
            Assert.That(speed.Resolve(0, 0), Is.EqualTo(1));
        }

        [Test]
        public void TheMemoryIsOfRunningAndNotOfStopping()
        {
            // Two pauses in a row cannot leave a zero behind, or the toggle would resume into a
            // pause and the clock would look stuck.
            var speed = new SpeedControl();
            speed.Resolve(2, 1);
            speed.Resolve(0, 2);
            speed.Resolve(0, 0);        // the unpause
            speed.Resolve(0, 2);        // paused again from the speed it resumed at
            Assert.That(speed.Resolve(0, 0), Is.EqualTo(2));
        }

        [Test]
        public void PickingASpeedWhileAlreadyPausedBothRunsAndIsRemembered()
        {
            // Pressing 2 on a paused world is not a toggle: it is a choice, and the next pause
            // has to come back to it.
            var speed = new SpeedControl();
            speed.Resolve(3, 1);
            Assert.That(speed.Resolve(0, 3), Is.EqualTo(0));
            Assert.That(speed.Resolve(2, 0), Is.EqualTo(2));
            Assert.That(speed.Resolve(0, 2), Is.EqualTo(0));
            Assert.That(speed.Resolve(0, 0), Is.EqualTo(2));
        }

        [Test]
        public void ASpeedSetBehindTheToggleIsStillRemembered()
        {
            // The restored view writes the world's speed directly. A colony saved at triple and
            // then paused comes back to triple.
            var speed = new SpeedControl();
            speed.Remember(3);
            Assert.That(speed.Resolve(0, 3), Is.EqualTo(0));
            Assert.That(speed.Resolve(0, 0), Is.EqualTo(3));
        }

        [Test]
        public void ARestoredPauseDoesNotBecomeTheMemory()
        {
            var speed = new SpeedControl();
            speed.Resolve(2, 1);
            speed.Remember(0);          // a colony saved paused
            Assert.That(speed.Resume, Is.EqualTo(2));
        }
    }
}
