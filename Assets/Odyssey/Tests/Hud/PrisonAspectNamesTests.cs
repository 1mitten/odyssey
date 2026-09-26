#nullable enable
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The interface's half of the prison aspects' agreement (design 60 §16): the names and bits
    /// <see cref="PrisonAspectNames"/> copies, held to the literals the simulation publishes.
    /// <c>PrisonContractTests.TheAspectNamesAreSpelledAsTheInterfaceReadsThem</c> holds the other
    /// side, so neither copy can move without failing one of the two — the pattern
    /// <c>CombatAspectNamesTests</c> set. "The risk shown is the risk rolled" stopped at the
    /// simulation until these were written.
    /// </summary>
    public class PrisonAspectNamesTests
    {
        [Test]
        public void TheAspectNamesAreSpelledAsTheSimulationPublishesThem()
        {
            Assert.That(PrisonAspectNames.NoBed, Is.EqualTo("odyssey.pawn.prison.nobed"));
            Assert.That(PrisonAspectNames.Stray, Is.EqualTo("odyssey.pawn.prison.stray"));
            Assert.That(PrisonAspectNames.CaptureMark, Is.EqualTo("odyssey.pawn.prison.capture"));
            Assert.That(PrisonAspectNames.Mode, Is.EqualTo("odyssey.pawn.prison.mode"));
            Assert.That(PrisonAspectNames.Willing, Is.EqualTo("odyssey.pawn.prison.willing"));
            Assert.That(PrisonAspectNames.Hours, Is.EqualTo("odyssey.pawn.prison.hours"));
            Assert.That(PrisonAspectNames.Blockers, Is.EqualTo("odyssey.pawn.prison.blockers"));
            Assert.That(PrisonAspectNames.Shackled, Is.EqualTo("odyssey.pawn.prison.shackled"));
            Assert.That(PrisonAspectNames.Escape, Is.EqualTo("odyssey.pawn.prison.escape"));
            Assert.That(PrisonAspectNames.EscapeWhy, Is.EqualTo("odyssey.pawn.prison.escapewhy"));
        }

        [Test]
        public void TheBitsAreTheSimulationsByValue()
        {
            Assert.That(new[]
            {
                PrisonAspectNames.Reason.Miserable, PrisonAspectNames.Reason.Unhappy, PrisonAspectNames.Reason.Content,
                PrisonAspectNames.Reason.DoorOpen, PrisonAspectNames.Reason.Shackled, PrisonAspectNames.Reason.Unwatched,
                PrisonAspectNames.Reason.Unhurt, PrisonAspectNames.Reason.Hurt, PrisonAspectNames.Reason.WellKept,
            }, Is.EqualTo(new[] { 1, 2, 4, 8, 16, 32, 64, 128, 256 }));
            Assert.That(new[]
            {
                PrisonAspectNames.Blocker.NoWarden, PrisonAspectNames.Blocker.Hungry, PrisonAspectNames.Blocker.Untended,
                PrisonAspectNames.Blocker.Shackled, PrisonAspectNames.Blocker.LowMood, PrisonAspectNames.Blocker.LowSocial,
            }, Is.EqualTo(new[] { 1, 2, 4, 8, 16, 32 }));
        }
    }
}
