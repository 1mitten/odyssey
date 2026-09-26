#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// <see cref="TemperatureLabels"/> is how the interface writes a temperature, and the point
    /// of it is that it is the only one.
    /// </summary>
    public class TemperatureLabelsTests
    {
        /// <summary>
        /// The form itself: signed, one decimal, truncating, with the unit. Each row is a
        /// decision the pane makes — a reading either side of zero is a different decision, and
        /// the sign is the whole of the difference.
        /// </summary>
        [TestCase(0, "0.0 °C")]
        [TestCase(1_250, "12.5 °C")]
        [TestCase(-1_250, "-12.5 °C")]
        [TestCase(-50, "-0.5 °C")]
        [TestCase(1_259, "12.5 °C")]
        [TestCase(-1_259, "-12.5 °C")]
        [TestCase(2_000, "20.0 °C")]
        [TestCase(-800, "-8.0 °C")]
        public void ATemperatureReadsTheSameWhereverItIsWritten(int centiC, string expected) =>
            Assert.That(TemperatureLabels.Describe(centiC), Is.EqualTo(expected));

        /// <summary>
        /// Nothing else spells the unit out.
        ///
        /// <para><b>Written because the rule had two owners and they agreed by luck.</b> The
        /// pane's tile row and the clock's outdoor reading each carried their own copy of the
        /// same four operations, in two assemblies. Nothing was wrong with either on the day it
        /// was written — that is the shape of the fault (<c>docs/bug-patterns.md</c> P1, the two
        /// order-colour tables that disagreed about deconstruct for months). It becomes a bug the
        /// first time somebody is asked for whole degrees and corrects one copy, and the
        /// difference between the pane and the clock is the sort of thing nobody reports because
        /// it reads as a mistake of their own.</para>
        ///
        /// <para>The file is read rather than the behaviour asserted, for the same reason
        /// <see cref="RegistryTests.NoPlayerFacingNameIsWrittenInCSharp"/> and
        /// <c>HudFontTests</c> read files: two copies of a rule that happen to agree cannot be
        /// caught by running either of them.</para>
        ///
        /// <para><b>What this does not watch, and why.</b> <c>AlmanacCatalogue</c> is exempt. It
        /// carries fixed encyclopedia prose — "14 days at 20°C", "-25°C below seasonal average" —
        /// which is authored text about content, not a rendering of a reading, and a literal
        /// cannot drift from a rule it never applied. It is worth knowing that the two
        /// consequently use different house styles for the same unit: the pane says "20.0 °C"
        /// and the almanac says "20°C". That is a content question for whoever owns the
        /// almanac's voice, not a formatting bug, and it is recorded here rather than silently
        /// tidied because rewording the exemption away is how a guard stops meaning
        /// anything.</para>
        /// </summary>
        [Test]
        public void TheUnitIsWrittenInExactlyOnePlace()
        {
            var offences = new List<string>();

            foreach (string file in Sources())
            {
                string name = Path.GetFileName(file);
                if (name == "TemperatureLabels.cs") continue;
                if (name.StartsWith("AlmanacCatalogue", StringComparison.Ordinal)) continue;   // prose, not a rendering

                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    // The unit inside a string literal. A doc comment may say °C as often as it
                    // likes — it is prose, and prose is not a second implementation.
                    string line = lines[i];
                    if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
                    if (line.TrimStart().StartsWith("///", StringComparison.Ordinal)) continue;
                    if (!line.Contains("°C", StringComparison.Ordinal)) continue;
                    if (!line.Contains("\"", StringComparison.Ordinal)
                        && !line.Contains("$\"", StringComparison.Ordinal)) continue;

                    offences.Add($"{Short(file)}:{i + 1}: {line.Trim()}");
                }
            }

            Assert.That(offences, Is.Empty,
                "a temperature is written for the player somewhere other than " +
                "TemperatureLabels. Two copies of one form drift, and the symptom is the tile " +
                "and the clock disagreeing about the same reading. Call " +
                "TemperatureLabels.Describe(centiC) instead:\n  " + string.Join("\n  ", offences));
        }

        /// <summary>Every C# file the HUD and the presentation layer are built from, less the
        /// tests — which name expected values, because that is what a test is for.</summary>
        static IEnumerable<string> Sources()
        {
            foreach (string root in new[] { "Assets/Odyssey/Hud", "Assets/Odyssey/Presentation" })
            {
                string? found = Find(root);
                if (found == null) continue;
                foreach (string file in Directory.GetFiles(found, "*.cs", SearchOption.AllDirectories))
                {
                    if (file.Replace('\\', '/').Contains("/Tests/")) continue;
                    yield return file;
                }
            }
        }

        /// <summary>Walk up for a path relative to the repository root — the working directory is
        /// the project root under Unity and a build output several levels down in the fast tier.
        /// </summary>
        static string? Find(string relative)
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (int i = 0; i < 12 && directory != null; i++, directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (Directory.Exists(candidate)) return candidate;
            }
            return null;
        }

        static string Short(string file)
        {
            string path = file.Replace('\\', '/');
            int at = path.IndexOf("Assets/", StringComparison.Ordinal);
            return at >= 0 ? path.Substring(at) : path;
        }
    }
}
