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

        const string KeepAliveFolder = "Assets/Resources/OdysseyKeepAlive";

        const string GlobalSettingsFile =
            "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset";

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
        public void TheLitShaderIsOnTheAlwaysIncludedList()
        {
            // The expensive one, and the one that is easiest to argue away. URP/Lit *is* already
            // in the build without this — Synty's prefab materials reference it — so a reader
            // would reasonably conclude it need not be listed. It does: those materials are all
            // non-instanced, and every chunk, floor, wall and item in this game is drawn with a
            // variant none of them uses.
            //
            // **This test was called TheInstancedVariantOfTheLitShaderIsKept until 2026-09-19,
            // and that name was false.** Being on the always-included list keeps the *shader*.
            // It does not keep the instancing *variant*, which is a separate mechanism and was
            // the third cause of the empty player — see the test below. A test whose name claims
            // more than its assertion is worse than no test, because it is why nobody looked
            // here for two of the three causes.
            string? inclusion = Read(InclusionFile);
            Assert.That(inclusion, Does.Contain("\"Universal Render Pipeline/Lit\""));
        }

        [Test]
        public void EveryKeptShaderAlsoHasAnInstancingKeepAliveMaterial()
        {
            // **The mechanism that actually keeps INSTANCING_ON**, measured 2026-09-19. Unity's
            // built-in variant stripping drops the instancing axis unless a *material asset* has
            // instancing switched on, and every instanced material in this game is built at
            // runtime from Shader.Find. Without these the player submits 1731 draw calls and
            // 43921 instances a frame into a variant that is not there, draws nothing, and does
            // not warn. InstancingKeepAlive holds the reasoning.
            string? inclusion = Read(InclusionFile);
            Assert.That(inclusion, Is.Not.Null);

            var required = Regex.Matches(inclusion!, @"^\s*""([^""]+)"",\s*$",
                                         RegexOptions.Multiline)
                                .Select(match => match.Groups[1].Value)
                                .ToArray();

            Assert.That(required, Is.Not.Empty,
                "no shader names were parsed out of the Required list, so this proves nothing");

            string? folder = Find(KeepAliveFolder);
            Assert.That(folder, Is.Not.Null,
                $"{KeepAliveFolder} is missing — nothing keeps the instancing variants alive, and "
                + "the player will draw an empty world with the colonists still in it");

            var missing = new List<string>();
            var notInstanced = new List<string>();

            foreach (string name in required)
            {
                string file = Path.Combine(folder!, name.Replace('/', '_') + ".mat");
                if (!File.Exists(file)) { missing.Add(name); continue; }

                // Present is not the same as right: switching Enable GPU Instancing off turns it
                // back into an ordinary material asset and it stops keeping anything alive.
                // The serialised name, not the C# property name: `Material.enableInstancing`
                // is written to the asset as `m_EnableInstancingVariants`. Reading for the
                // property name compiles, runs, and fails every material — which this test did
                // on its first run, and is why it reads a real file rather than trusting a
                // constant.
                if (!File.ReadAllText(file).Contains("m_EnableInstancingVariants: 1"))
                    notInstanced.Add(name);
            }

            Assert.That(missing, Is.Empty,
                "these shaders are kept in the build but have no instancing keep-alive material, "
                + "so anything drawn with them instanced is invisible in a player and perfect in "
                + "the editor: " + string.Join(", ", missing));

            Assert.That(notInstanced, Is.Empty,
                "these keep-alive materials have instancing switched off, which makes them "
                + "ordinary materials that keep nothing: " + string.Join(", ", notInstanced));
        }

        [Test]
        public void UrpVariantStrippingIsSwitchedOn()
        {
            // **Not a style preference — a build that finishes.** With
            // `m_StripUnusedVariants: 0` the scriptable stripper returns its input untouched and
            // URP/Lit's ForwardLit fragment pass goes from 64 variants to **884,736**, which
            // measured out at roughly a day and a half of shader compilation. It had been flipped
            // off in the working tree on 2026-09-19, uncommitted, and the only symptom was a
            // build that died overnight with "Internal error communicating with the shader
            // compiler process" — which reads like a flaky tool.
            //
            // This reads the asset as text on purpose. The value that matters is the one on disk
            // in *this* working tree, not the one in git and not the one a live
            // GraphicsSettings object would report after Unity has migrated it in memory.
            string? settings = Read(GlobalSettingsFile);
            Assert.That(settings, Is.Not.Null, $"{GlobalSettingsFile} is missing");

            Assert.That(settings, Does.Not.Contain("m_StripUnusedVariants: 0"),
                "URP's unused-variant stripping is switched off in "
                + $"{GlobalSettingsFile}. Every shader variant of every kept shader will be "
                + "compiled: this took one pass from 64 variants to 884,736 and the player build "
                + "from 12 seconds to an estimated day and a half. If variants you need are being "
                + "stripped, the answer is an instancing keep-alive material, not this flag — see "
                + "InstancingKeepAlive and docs/bug-patterns.md.");

            Assert.That(settings, Does.Contain("m_StripUnusedVariants: 1"),
                $"{GlobalSettingsFile} no longer carries the stripping setting at all, so this "
                + "test is proving nothing — find where it moved to.");
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
