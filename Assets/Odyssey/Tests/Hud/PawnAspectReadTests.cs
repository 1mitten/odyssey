#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The other end of OQ-45, and the end that matters architecturally.
    ///
    /// <para>This assembly cannot reference <c>Odyssey.Sim</c> — that is the strongest structural
    /// guard in ADR 0004, and <c>Architecture_Hud_DoesNotReferenceUnityEngine_OrSim</c> enforces
    /// it. So the interface cannot see the feature that published an aspect, cannot name its type
    /// and cannot share an enum with it. All it has is the string, and this file is the proof that
    /// the string is enough.</para>
    ///
    /// <para>The fixture writes the frame directly rather than running a world, for the same
    /// reason <c>Frame</c> does above: what is under test is the contract, and the contract is a
    /// <see cref="WorldSnapshot"/>.</para>
    /// </summary>
    public class PawnAspectReadTests
    {
        static readonly PawnId Ada = new PawnId(1);
        static readonly PawnId Bram = new PawnId(2);

        static WorldSnapshot FrameWithThirst(int ada, int bram)
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(0, new GridSize(10, 10, 4), sliceLayer: 1);
            snapshot.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), food: 900, rest: 800, mood: 50));
            snapshot.AddPawn(new PawnView(Bram, new CellRef(2, 1, 1), food: 500, rest: 400, mood: 20));
            snapshot.AddPawnAspect(new PawnAspect(Ada, AspectKey.Of("test.pawn.thirst"), ada));
            snapshot.AddPawnAspect(new PawnAspect(Bram, AspectKey.Of("test.pawn.thirst"), bram));
            return snapshot;
        }

        [Test]
        public void TheInterfaceReadsAFeaturesValueKnowingOnlyItsName()
        {
            var frame = FrameWithThirst(ada: 40, bram: 10);
            var thirst = AspectKey.Of("test.pawn.thirst");

            Assert.That(frame.TryGetPawnAspect(Ada, thirst, out int a), Is.True);
            Assert.That(a, Is.EqualTo(40));
            Assert.That(frame.TryGetPawnAspect(Bram, thirst, out int b), Is.True);
            Assert.That(b, Is.EqualTo(10));
        }

        [Test]
        public void AFeatureThatIsNotInstalledSimplyReadsNothing()
        {
            // The control, and the case a panel must survive: a build without the feature, or a
            // pawn not doing the thing. There is no "missing field" for the interface to crash on.
            var frame = FrameWithThirst(ada: 40, bram: 10);

            Assert.That(frame.TryGetPawnAspect(Ada, AspectKey.Of("test.pawn.faith"), out int none), Is.False);
            Assert.That(none, Is.Zero);
        }

        [Test]
        public void APanelOpenOnAPawnThatDiedReadsNothingRatherThanStaleNumbers()
        {
            // The awkward case PawnView was shaped around, now asked of aspects: the panel holds an
            // id, the next frame lacks the pawn, and the read fails cleanly.
            var frame = FrameWithThirst(ada: 40, bram: 10);
            frame.BeginWrite(1, new GridSize(10, 10, 4), sliceLayer: 1);
            frame.AddPawn(new PawnView(Bram, new CellRef(2, 1, 1), food: 500, rest: 400, mood: 20));
            frame.AddPawnAspect(new PawnAspect(Bram, AspectKey.Of("test.pawn.thirst"), 11));

            Assert.That(frame.TryGetPawn(Ada, out _), Is.False);
            Assert.That(frame.TryGetPawnAspect(Ada, AspectKey.Of("test.pawn.thirst"), out _), Is.False);
            Assert.That(frame.TryGetPawnAspect(Bram, AspectKey.Of("test.pawn.thirst"), out int b), Is.True);
            Assert.That(b, Is.EqualTo(11));
        }

        [Test]
        public void WalkingEveryAspectIsOneSpanRatherThanALookupPerPawn()
        {
            // How a roster reads: one pass over the published rows, not TryGet per pawn per name.
            var frame = FrameWithThirst(ada: 40, bram: 10);

            int total = 0;
            foreach (PawnAspect aspect in frame.PawnAspects) total += aspect.Value;

            Assert.That(frame.AspectCount, Is.EqualTo(2));
            Assert.That(total, Is.EqualTo(50));
        }
    }
}
