#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// Every shader this game asks for by name at runtime is one the build has been told to keep.
    ///
    /// <para><b>The fault, measured 2026-09-19 on the first player build this project ever ran.</b>
    /// The owner's report was "there is no terrain — no graphics, terrain etc, apart from
    /// characters", and it happened only in a build. Everything in the world is drawn with
    /// <c>Graphics.RenderMeshInstanced</c> using materials created at runtime from
    /// <c>Shader.Find</c>; a runtime material is not an asset, so the build's collector never
    /// sees it, and Unity shipped only what assets referenced. Three of our four shaders were
    /// absent from the player altogether. Characters were the one visible thing because they
    /// alone are GameObjects wearing real material assets — and they drew in the pack's own
    /// colours, because the shader that recolours them had gone with the rest.</para>
    ///
    /// <para><b>No other test in this repository can catch it, and that is the point.</b> The
    /// editor has every shader and every variant, always; both tiers run in the editor's own
    /// domain. The only thing that fails on a stripped shader is a player build, and nothing had
    /// ever made one. So this reads the source instead — the same bargain
    /// <c>RegistryTests.NoPlayerFacingNameIsWrittenInCSharp</c> makes, for a rule that is
    /// otherwise only observable somewhere the tests do not run.</para>
    ///
    /// <para>It reads <c>ShaderInclusion.cs</c> as text rather than referencing it, because that
    /// class lives in the Editor assembly and this one cannot see it. The alternative — a list
    /// duplicated into the test — is the second source of truth the test exists to prevent.</para>
    /// </summary>
    public class ShaderInclusionTests
    {
        const string InclusionFile = "Assets/Editor/Odyssey/ShaderInclusion.cs";

        static readonly string[] SourceRoots =
        {
            "Assets/Odyssey/Presentation",
            "Assets/Editor/Odyssey",
        };

        [Test]
        public void EveryShaderFoundAtRuntimeIsKeptInTheBuild()
        {
            string? inclusion = Read(InclusionFile);
            Assert.That(inclusion, Is.Not.Null,
                $"{InclusionFile} is missing — the always-included list has no owner");

            var found = new SortedSet<string>();
            foreach (string file in Sources())
            {
                // The tests' own Shader.Find calls are not the game asking for a shader: a test
                // that wants URP/Lit to build a material with runs in an editor that has it.
                if (file.Replace('\\', '/').Contains("/Tests/")) continue;
                if (file.Replace('\\', '/').EndsWith("/ShaderInclusion.cs")) continue;

                foreach (Match match in Regex.Matches(File.ReadAllText(file), @"Shader\.Find\(""([^""]+)""\)"))
                    found.Add(match.Groups[1].Value);
            }

            Assert.That(found, Is.Not.Empty,
                "no Shader.Find call was found at all, so this test is proving nothing");

            var absent = found.Where(name => !inclusion!.Contains("\"" + name + "\"")).ToArray();

            Assert.That(absent, Is.Empty,
                "these shaders are asked for by name at runtime but are not in "
                + $"{InclusionFile}'s Required list, so a player build would draw nothing that "
                + "uses them while the editor looked perfect: " + string.Join(", ", absent));
        }

        [Test]
        public void TheFourShadersWeWroteOurselvesAreOnTheList()
        {
            // Named explicitly as well as derived, because the derivation above is only as good
            // as the Shader.Find calls it can see. These four are ours, they have no fallback
            // worth the name, and losing any one of them is a visible hole in the game.
            string? inclusion = Read(InclusionFile);
            Assert.That(inclusion, Is.Not.Null);

            foreach (string name in new[]
                     { "Odyssey/Character", "Odyssey/Tree", "Odyssey/Water", "Odyssey/Outline" })
                Assert.That(inclusion, Does.Contain("\"" + name + "\""), $"{name} is not kept");
        }

        [Test]
        public void TheInstancedVariantOfTheLitShaderIsKept()
        {
            // The expensive one, and the one that is easiest to argue away. URP/Lit *is* already
            // in the build without this — Synty's prefab materials reference it — so a reader
            // would reasonably conclude it need not be listed. It does: those materials are all
            // non-instanced, and every chunk, floor, wall and item in this game is drawn with a
            // variant none of them uses.
            string? inclusion = Read(InclusionFile);
            Assert.That(inclusion, Does.Contain("\"Universal Render Pipeline/Lit\""));
        }

        static IEnumerable<string> Sources()
        {
            foreach (string relative in SourceRoots)
            {
                string? root = Find(relative);
                if (root == null) continue;
                foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                    yield return file;
            }
        }

        static string? Read(string relative)
        {
            string? path = FindFile(relative);
            return path == null ? null : File.ReadAllText(path);
        }

        /// <summary>
        /// Walk up for a path relative to the repository root — the same trick
        /// <c>RegistryTests</c> uses, because the working directory is the project root under
        /// Unity and a build output several levels down in the fast tier.
        /// </summary>
        static string? Find(string relative) => Walk(relative, Directory.Exists);

        static string? FindFile(string relative) => Walk(relative, File.Exists);

        static string? Walk(string relative, System.Func<string, bool> exists)
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (int i = 0; i < 12 && directory != null; i++, directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (exists(candidate)) return candidate;
            }
            return null;
        }
    }
}
