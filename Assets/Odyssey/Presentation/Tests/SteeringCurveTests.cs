#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    public class SteeringCurveTests
    {
        [Test]
        public void Bell_BoundaryConditionsAndPeak()
        {
            Assert.That(SteeringCurve.Bell(0f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(SteeringCurve.Bell(1f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(SteeringCurve.Bell(0.5f), Is.EqualTo(1f).Within(1e-5f));

            // Symmetry
            Assert.That(SteeringCurve.Bell(0.25f), Is.EqualTo(SteeringCurve.Bell(0.75f)).Within(1e-5f));
            Assert.That(SteeringCurve.Bell(0.1f), Is.EqualTo(SteeringCurve.Bell(0.9f)).Within(1e-5f));

            // Out of bounds clamped to 0
            Assert.That(SteeringCurve.Bell(-0.1f), Is.EqualTo(0f));
            Assert.That(SteeringCurve.Bell(1.1f), Is.EqualTo(0f));
        }

        [Test]
        public void Bell_C1ContinuityAtBoundaries()
        {
            // Derivative is zero at boundaries and peak
            Assert.That(SteeringCurve.BellDerivative(0f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(SteeringCurve.BellDerivative(0.5f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(SteeringCurve.BellDerivative(1f), Is.EqualTo(0f).Within(1e-5f));

            // Numerical derivative matches analytical derivative
            float dt = 1e-4f;
            for (float t = 0.1f; t <= 0.9f; t += 0.1f)
            {
                float numDeriv = (SteeringCurve.Bell(t + dt) - SteeringCurve.Bell(t - dt)) / (2f * dt);
                float exactDeriv = SteeringCurve.BellDerivative(t);
                Assert.That(numDeriv, Is.EqualTo(exactDeriv).Within(1e-3f));
            }
        }

        [Test]
        public void LateralRight_OrthogonalityAndRightHandRule()
        {
            // North -> East
            Vector3 north = new Vector3(0f, 0f, 1f);
            Vector3 rightOfNorth = SteeringCurve.LateralRight(north);
            Assert.That(rightOfNorth, Is.EqualTo(new Vector3(1f, 0f, 0f)));

            // East -> South
            Vector3 east = new Vector3(1f, 0f, 0f);
            Vector3 rightOfEast = SteeringCurve.LateralRight(east);
            Assert.That(rightOfEast, Is.EqualTo(new Vector3(0f, 0f, -1f)));

            // South -> West
            Vector3 south = new Vector3(0f, 0f, -1f);
            Vector3 rightOfSouth = SteeringCurve.LateralRight(south);
            Assert.That(rightOfSouth, Is.EqualTo(new Vector3(-1f, 0f, 0f)));

            // West -> North
            Vector3 west = new Vector3(-1f, 0f, 0f);
            Vector3 rightOfWest = SteeringCurve.LateralRight(west);
            Assert.That(rightOfWest, Is.EqualTo(new Vector3(0f, 0f, 1f)));

            // Diagonal
            Vector3 diag = new Vector3(1f, 0f, 1f).normalized;
            Vector3 rightDiag = SteeringCurve.LateralRight(diag);
            Assert.That(Vector3.Dot(diag, rightDiag), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(rightDiag.magnitude, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void PassingDisplacement_ClampedToTileBounds()
        {
            Vector3 heading = new Vector3(0f, 0f, 1f);

            // Default peak is MaxLateralOffset (0.60 m)
            Vector3 peak = SteeringCurve.PassingDisplacement(heading, 0.5f);
            Assert.That(peak.x, Is.EqualTo(SteeringCurve.MaxLateralOffset).Within(1e-4f));
            Assert.That(peak.z, Is.EqualTo(0f).Within(1e-4f));

            // Even if requested 2.0 m, clamped to HardClampedMax (0.75 m)
            Vector3 excessive = SteeringCurve.PassingDisplacement(heading, 0.5f, maxOffset: 2.0f);
            Assert.That(excessive.magnitude, Is.LessThanOrEqualTo(SteeringCurve.HardClampedMax + 1e-5f));
        }

        [Test]
        public void AnticipatoryLeadIn_RampsDuringSecondHalfOfCell()
        {
            Assert.That(SteeringCurve.AnticipatoryLeadIn(0f), Is.EqualTo(0f));
            Assert.That(SteeringCurve.AnticipatoryLeadIn(0.25f), Is.EqualTo(0f));
            Assert.That(SteeringCurve.AnticipatoryLeadIn(0.5f), Is.EqualTo(0f));

            // Midpoint of lead-in (t=0.75) is 0.5
            Assert.That(SteeringCurve.AnticipatoryLeadIn(0.75f), Is.EqualTo(0.5f).Within(1e-5f));
            // End of lead-in (t=1.0) reaches 1.0
            Assert.That(SteeringCurve.AnticipatoryLeadIn(1.0f), Is.EqualTo(1.0f).Within(1e-5f));
        }
    }
}
