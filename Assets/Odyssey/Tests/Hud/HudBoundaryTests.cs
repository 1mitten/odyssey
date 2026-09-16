#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The structural guard from design 09 §4.7: <c>Odyssey.Hud</c> references neither
    /// UnityEngine nor the simulation. The asmdef already makes the first a compile error and
    /// the reference list makes the second impossible to add silently; these tests are the guard
    /// against someone quietly undoing either, and they run in both test tiers.
    /// </summary>
    public class HudBoundaryTests
    {
        [Test]
        public void HudDoesNotReferenceUnity()
        {
            var offenders = typeof(RosterModel).Assembly.GetReferencedAssemblies()
                .Select(a => a.Name ?? string.Empty)
                .Where(n => n.StartsWith("UnityEngine") || n.StartsWith("UnityEditor"))
                .ToArray();

            Assert.That(offenders, Is.Empty,
                $"Odyssey.Hud references {string.Join(", ", offenders)}");
        }

        [Test]
        public void HudDoesNotReferenceTheSimulation()
        {
            var references = typeof(RosterModel).Assembly.GetReferencedAssemblies()
                .Select(a => a.Name ?? string.Empty)
                .ToArray();

            Assert.That(references, Has.No.Member("Odyssey.Sim"),
                "the interface reads published frames; it never touches a simulation object");
        }
    }
}
