#nullable enable
using System.IO;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The catalogue build's standing guard (design 66 §2): a licensed pack installed outside
    /// <c>Assets/Synty</c> is found, named and refused, and a project without one passes. Run
    /// against a scratch folder, never the project, so the test cannot depend on what is installed.
    /// </summary>
    public class LicensedArtGuardTests
    {
        string _root = string.Empty;

        [SetUp]
        public void MakeRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "odyssey-guard-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_root, "Assets", "Synty", "SimpleForestAnimal"));
        }

        [TearDown]
        public void RemoveRoot()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        [Test]
        public void APackUnderSyntyIsNotStray()
        {
            Assert.That(LicensedArtGuard.Find(_root), Is.Empty,
                "the pack where it belongs, under the ignored Assets/Synty, passes");
        }

        [Test]
        public void ThePackWhereItInstallsItselfIsRefused()
        {
            Directory.CreateDirectory(Path.Combine(_root, "Assets", "SimpleForestAnimal", "Models"));

            var found = LicensedArtGuard.Find(_root);

            Assert.That(found, Is.EqualTo(new[] { "Assets/SimpleForestAnimal" }));
            string refusal = LicensedArtGuard.Refusal(found);
            Assert.That(refusal, Does.Contain("Assets/SimpleForestAnimal"), "it names the folder");
            Assert.That(refusal, Does.Contain("--remap"), "and says how to move it");
        }

        [Test]
        public void TheIgnoreFileCoversTheInstallPath()
        {
            // The backstop behind the guard: if the dialog puts the pack there anyway, git ignores it.
            string ignore = File.ReadAllText(".gitignore");
            Assert.That(ignore, Does.Contain("/Assets/SimpleForestAnimal/"));
        }
    }
}
