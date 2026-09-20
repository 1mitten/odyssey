#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// Every character the interface writes has to exist in the font that draws it.
    ///
    /// <para><b>Why this test exists.</b> The Work tab shipped a Simple mode whose two readings
    /// were U+2713 CHECK MARK and U+2715 MULTIPLICATION X written into a label. Archivo Narrow's
    /// cmap contains neither and IBM Plex Mono contains only the first, so the legend drew two
    /// blanks and every "won't do" cell in the grid drew one. <b>Neither tier could see it</b> —
    /// the fast tier has no text engine at all, and the Unity tier runs the text engine but
    /// asserts no pixels, so a missing glyph is a silent nothing in both. It was found by reading
    /// the two <c>.ttf</c> files, which is what this test now does on every run.</para>
    ///
    /// <para><b>Both fonts, not the one that happens to draw it.</b> A label's face is chosen by
    /// its <c>HudTextRole</c> and by the <c>numeric</c> flag beside it, and roles move: the
    /// em-dash in an incapable cell is mono today because the digit beside it is. A character that
    /// lives in only one of the two faces is a fault waiting for somebody to change a role, and
    /// the whole set the interface actually uses — the middle dot, the multiplication sign, both
    /// dashes, the bullet, the ellipsis, the angle quote — is in both. So the rule is both, and it
    /// costs nothing to hold.</para>
    ///
    /// <para>The scan is <c>RegistryTests</c>'s: the same source roots, the same exclusion of
    /// generated and test files, and the same walk up to the repository root so that one test
    /// reads the same files under Unity and in the fast tier.</para>
    /// </summary>
    public class HudFontTests
    {
        const string UiFont = "Assets/Odyssey/Presentation/Ui/Fonts/ArchivoNarrow.ttf";
        const string MonoFont = "Assets/Odyssey/Presentation/Ui/Fonts/IBMPlexMono-Medium.ttf";

        /// <summary>A C# string literal, verbatim ones included, one per match.</summary>
        static readonly Regex Literal = new Regex("\"((?:[^\"\\\\\\n]|\\\\.)*)\"", RegexOptions.Compiled);

        [Test]
        public void EveryCharacterTheHudWritesExistsInBothFonts()
        {
            HashSet<int>? ui = CharactersIn(UiFont);
            HashSet<int>? mono = CharactersIn(MonoFont);
            Assert.That(ui, Is.Not.Null, UiFont + " could not be read");
            Assert.That(mono, Is.Not.Null, MonoFont + " could not be read");

            var offences = new List<string>();
            foreach (string file in Sources())
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = lines[i].TrimStart();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal)) continue;

                    foreach (Match match in Literal.Matches(lines[i]))
                    {
                        foreach (char c in match.Groups[1].Value)
                        {
                            if (c < 128) continue;
                            bool inUi = ui!.Contains(c);
                            bool inMono = mono!.Contains(c);
                            if (inUi && inMono) continue;

                            string missing = !inUi && !inMono ? "neither font"
                                : !inUi ? "Archivo Narrow"
                                : "IBM Plex Mono";
                            offences.Add($"{Short(file)}:{i + 1} writes U+{(int)c:X4} '{c}', " +
                                         $"which {missing} cannot draw");
                        }
                    }
                }
            }

            Assert.That(offences, Is.Empty,
                "a HUD string literal contains a character the shipped fonts have no glyph for. " +
                "It draws as a blank or a box, and no screenshot test and no assertion in either " +
                "tier can see it. Draw the shape as a HudGlyph instead, or use a character both " +
                "faces carry:\n  " + string.Join("\n  ", offences));
        }

        /// <summary>
        /// The characters a TrueType file's <c>cmap</c> maps, read directly.
        ///
        /// <para>Only the Unicode sub-tables and only formats 4 and 12, which is every sub-table
        /// either of these two files has. A format this does not know returns what it found rather
        /// than throwing: the test's job is to catch a character that is certainly missing, and
        /// reporting a present character as missing would be the worse failure. Null means the
        /// file was not found at all, which the test asserts on separately rather than passing
        /// vacuously.</para>
        /// </summary>
        static HashSet<int>? CharactersIn(string relative)
        {
            string? path = FindFile(relative);
            if (path == null) return null;

            byte[] d = File.ReadAllBytes(path);
            int tables = U16(d, 4);
            int cmap = -1;
            for (int i = 0; i < tables; i++)
            {
                int record = 12 + 16 * i;
                if (d[record] == 'c' && d[record + 1] == 'm' &&
                    d[record + 2] == 'a' && d[record + 3] == 'p')
                    cmap = (int)U32(d, record + 8);
            }
            if (cmap < 0) return null;

            int subtables = U16(d, cmap + 2);
            int best = -1;
            for (int i = 0; i < subtables; i++)
            {
                int record = cmap + 4 + 8 * i;
                int platform = U16(d, record);
                int encoding = U16(d, record + 2);
                bool unicode = (platform == 3 && (encoding == 1 || encoding == 10)) ||
                               (platform == 0 && (encoding == 3 || encoding == 4));
                if (unicode) best = cmap + (int)U32(d, record + 4);
            }
            if (best < 0) return null;

            var chars = new HashSet<int>();
            int format = U16(d, best);
            if (format == 4)
            {
                int segmentsX2 = U16(d, best + 6);
                int segments = segmentsX2 / 2;
                int ends = best + 14;
                int starts = ends + segmentsX2 + 2;
                for (int s = 0; s < segments; s++)
                {
                    int end = U16(d, ends + 2 * s);
                    int start = U16(d, starts + 2 * s);
                    if (start == 0xFFFF && end == 0xFFFF) continue;
                    for (int c = start; c <= end && c <= 0xFFFF; c++) chars.Add(c);
                }
            }
            else if (format == 12)
            {
                long groups = U32(d, best + 12);
                for (long g = 0; g < groups; g++)
                {
                    int record = best + 16 + (int)(12 * g);
                    long start = U32(d, record);
                    long end = U32(d, record + 4);
                    for (long c = start; c <= end && c <= 0x10FFFF; c++) chars.Add((int)c);
                }
            }
            return chars;
        }

        static int U16(byte[] d, int at) => (d[at] << 8) | d[at + 1];

        static long U32(byte[] d, int at) =>
            ((long)d[at] << 24) | ((long)d[at + 1] << 16) | ((long)d[at + 2] << 8) | d[at + 3];

        /// <summary>The same source set <c>RegistryTests</c> scans, and for the same reasons.</summary>
        static IEnumerable<string> Sources()
        {
            foreach (string root in new[] { "Assets/Odyssey/Hud", "Assets/Odyssey/Presentation" })
            {
                string? found = FindDirectory(root);
                if (found == null) continue;
                foreach (string file in Directory.GetFiles(found, "*.cs", SearchOption.AllDirectories))
                {
                    if (Path.GetFileName(file) == "Registry.g.cs") continue;
                    if (file.Replace('\\', '/').Contains("/Tests/")) continue;
                    yield return file;
                }
            }
        }

        static string? FindDirectory(string relative) => Find(relative, Directory.Exists);

        static string? FindFile(string relative) => Find(relative, File.Exists);

        /// <summary>Walk up for a path relative to the repository root — <c>RegistryTests.Find</c>'s
        /// trick, which is what lets one test read one file under Unity and in the fast tier.</summary>
        static string? Find(string relative, Func<string, bool> exists)
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (int i = 0; i < 12 && directory != null; i++, directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (exists(candidate)) return candidate;
            }
            return null;
        }

        static string Short(string file)
        {
            string path = file.Replace('\\', '/');
            int at = path.IndexOf("Assets/", StringComparison.Ordinal);
            return at < 0 ? path : path.Substring(at);
        }
    }
}
