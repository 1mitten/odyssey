#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.World;
using UnityEngine;

namespace Odyssey.Presentation.Tests
{
    /// <summary>
    /// <see cref="PawnFigureDirector.FigureCeiling"/> is a hard ceiling, not a default
    /// (owner, 2026-09-20: *"make the absolute cap 64 for safety for now"*).
    ///
    /// <para>The cap is settable because a harness wants a small crowd cheaply — <c>FigureCapTests</c>
    /// runs at eight rather than instantiating sixty-four Synty characters to prove a rule about
    /// ordering. A setter that also accepted a *large* number would make the ceiling a suggestion,
    /// and the whole point of the owner's word "absolute" is that it is not one. So the setter
    /// clamps and this says so.</para>
    ///
    /// <para>No catalogue, no art and no world: the cap is a number, and a director built with an
    /// empty catalogue has one like any other.</para>
    /// </summary>
    public class FigureCeilingTests
    {
        static PawnFigureDirector Director(GameObject parent) =>
            new PawnFigureDirector(null, parent.transform, 0);

        [Test]
        public void TheCapStartsAtTheCeiling()
        {
            var parent = new GameObject("figures");
            try
            {
                Assert.That(Director(parent).MaxFigures,
                    Is.EqualTo(PawnFigureDirector.FigureCeiling));
                Assert.That(PawnFigureDirector.FigureCeiling, Is.EqualTo(64),
                    "the ceiling moved; that is a measurement, not an edit");
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void NothingCanRaiseTheCapAboveTheCeiling()
        {
            var parent = new GameObject("figures");
            try
            {
                PawnFigureDirector director = Director(parent);

                director.MaxFigures = PawnFigureDirector.FigureCeiling + 1;
                Assert.That(director.MaxFigures, Is.EqualTo(PawnFigureDirector.FigureCeiling),
                    "one over the ceiling was accepted");

                director.MaxFigures = 10_000;
                Assert.That(director.MaxFigures, Is.EqualTo(PawnFigureDirector.FigureCeiling),
                    "a wild number was accepted");

                Assert.That(director.MaxFigures, Is.LessThanOrEqualTo(PawnFigureDirector.FigureCeiling));
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void LoweringTheCapStillWorks()
        {
            // The harness case, and the reason the property is settable at all.
            var parent = new GameObject("figures");
            try
            {
                PawnFigureDirector director = Director(parent);

                director.MaxFigures = 8;
                Assert.That(director.MaxFigures, Is.EqualTo(8));

                // Zero is a legal thing to ask for: the whole colony drawn as baked stand-ins.
                director.MaxFigures = 0;
                Assert.That(director.MaxFigures, Is.Zero);

                // Below zero is not a smaller cap, it is nonsense, and it becomes zero rather
                // than a negative that every `< MaxFigures` comparison would read as "none".
                director.MaxFigures = -5;
                Assert.That(director.MaxFigures, Is.Zero);
            }
            finally { Object.DestroyImmediate(parent); }
        }
    }
}
