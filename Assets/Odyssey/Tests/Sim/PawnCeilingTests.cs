#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// **A debug command may not run the session into a state nobody designed for.**
    ///
    /// <para>Owner, 2026-09-23: spawning colonists from the debug menu past a certain number
    /// produced colonists "in an orange suit", textures that "kept switching", and a session that
    /// "got buggy". Nothing in the game stopped it — the spawn intent refused an unreachable
    /// column and nothing else.</para>
    ///
    /// <para><b>The ceiling is a rail, not the fix, and these tests say so.</b> A barren board was
    /// measured healthy at 384 colonists on the same day (3.90 ms a frame, no stand-ins drawn, 23
    /// materials, two faces), so <see cref="PawnRegistry.PawnCeiling"/> is not the number anything
    /// was found to break at. It is four times the audit's scale target of fifty and three times
    /// the figure ceiling, and it exists so that the menu cannot keep going for ever.</para>
    /// </summary>
    public class PawnCeilingTests
    {
        /// <summary>
        /// **Moving the ceiling is a measurement, not an edit** — the same rule
        /// <c>FigureCeilingTests</c> holds for the figure cap. If this fails, somebody has raised
        /// the number: take a frame measurement at the new one and say what it cost, in
        /// <c>docs/design/29-modular-colonists.md</c>, before changing this line.
        /// </summary>
        [Test]
        public void TheCeilingIsWhereItWasMeasuredToBe()
        {
            // 200 until 2026-09-25, then 400 for a raid of two hundred beside a full colony (owner,
            // design 53 §11): the tick measured with RaidBenchmarkTests at 243 pawns, the frame with
            // FrameTimeTests.TheFrameWithARaidOfTwoHundred, and 384 colonists already measured healthy.
            Assert.That(PawnRegistry.PawnCeiling, Is.EqualTo(400),
                "the pawn ceiling moved. It is a rail against the debug menu, not a tuning knob: " +
                "raise it only with a frame measurement at the new number");
            Assert.That(PawnRegistry.PawnCeiling, Is.GreaterThan(64),
                "the ceiling must clear the figure cap, or the far form would never be drawn at all");
        }

        /// <summary>
        /// The spawn intent stops at the ceiling, and **refuses rather than clamps** — the rule the
        /// rest of <see cref="PawnRegistry"/> follows. A command that silently declines to act
        /// while reporting success is how a debug menu comes to lie about what is in the world.
        /// </summary>
        [Test]
        public void TheSpawnIntentRefusesAtTheCeiling()
        {
            Colony colony = Colony.Build(sx: 32, sz: 32, sy: 2);

            // Straight past the ceiling, through the registry rather than the intent bus, because
            // the bus has a capacity of its own and this test is about the ceiling.
            while (colony.Ctx.Pawns.Count < PawnRegistry.PawnCeiling)
                colony.Ctx.Pawns.Spawn(colony.Cell(
                    2 + colony.Ctx.Pawns.Count % 28,
                    2 + colony.Ctx.Pawns.Count / 28 % 28, 0));

            Assert.That(colony.Ctx.Pawns.Count, Is.EqualTo(PawnRegistry.PawnCeiling));

            IntentRejection refused = colony.Ctx.Pawns.HandleSpawnPawn(
                new Intent(IntentKind.SpawnPawn, colony.Size.FromIndex(colony.Cell(10, 10, 0)), 0));

            Assert.That(refused, Is.EqualTo(IntentRejection.NotPermitted),
                "the spawn intent kept going past the ceiling");
            Assert.That(colony.Ctx.Pawns.Count, Is.EqualTo(PawnRegistry.PawnCeiling),
                "a refused spawn still added a pawn");
        }

        /// <summary>
        /// **The negative control.** Under the ceiling nothing changes at all — if this ever fails,
        /// the rail has started governing ordinary play, which is the one thing it must not do.
        /// </summary>
        [Test]
        public void UnderTheCeilingNothingIsRefused()
        {
            Colony colony = Colony.Build(sx: 32, sz: 32, sy: 2);

            for (int i = 0; i < 20; i++)
            {
                IntentRejection allowed = colony.Ctx.Pawns.HandleSpawnPawn(
                    new Intent(IntentKind.SpawnPawn,
                        colony.Size.FromIndex(colony.Cell(2 + i, 4, 0)), 0));
                Assert.That(allowed, Is.EqualTo(IntentRejection.None),
                    $"spawn {i} was refused well under the ceiling");
            }

            Assert.That(colony.Ctx.Pawns.Count, Is.EqualTo(20));
        }

        /// <summary>
        /// The ceiling bounds the <i>intent</i>, not <see cref="PawnRegistry.Spawn(int, int)"/>.
        /// Worldgen and the scenario call that directly, and a starting colony is nowhere near the
        /// ceiling — but a rule that bounded the constructor would be a rule that could refuse to
        /// build a world, which is a much worse failure than a debug menu that stops.
        /// </summary>
        [Test]
        public void TheCeilingDoesNotGovernWorldgen()
        {
            Colony colony = Colony.Build(sx: 32, sz: 32, sy: 2);

            while (colony.Ctx.Pawns.Count < PawnRegistry.PawnCeiling + 5)
                colony.Ctx.Pawns.Spawn(colony.Cell(
                    2 + colony.Ctx.Pawns.Count % 28,
                    2 + colony.Ctx.Pawns.Count / 28 % 28, 0));

            Assert.That(colony.Ctx.Pawns.Count, Is.EqualTo(PawnRegistry.PawnCeiling + 5),
                "Spawn itself refused, which would make a world unbuildable rather than a menu polite");
        }
    }
}
