#nullable enable
using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// Two invariants behind the pointer, both of which can only be checked by reading the files.
    ///
    /// <para><b>Why source-reading, and why that is not laziness here.</b> A stylesheet asking a
    /// runtime panel for a keyword cursor is a <i>log line</i> — it compiles, it runs, it draws
    /// the right screen and it tells nobody. And the frame ordering that decides which camera a
    /// click is resolved against cannot be tested at all in this project, because the PlayMode
    /// harness still cannot deliver a synthetic mouse (see the ignored tests in
    /// <c>InputHarnessTests</c>). Both faults are invisible to every gate the project has, which
    /// is exactly the case <c>HudFontTests</c> and <c>RegistryTests</c> already answer by parsing
    /// files rather than running code.</para>
    ///
    /// <para><c>docs/design/28-pointer-cursor.md</c>.</para>
    /// </summary>
    public class PointerCursorTests
    {
        const string SheetPath = "Assets/Odyssey/Presentation/Ui/Hud.uss";
        const string RigPath = "Assets/Odyssey/Presentation/Camera/SliceCameraRig.cs";

        /// <summary>
        /// <b>A runtime panel cannot take a keyword cursor</b>, so a rule that asks for one does
        /// nothing except log
        /// <i>"Runtime cursors other than the default cursor need to be defined using a texture"</i>
        /// once per repaint. Eighteen of them had accumulated by 2026-09-21 and every one had been
        /// written believing it changed the pointer.
        ///
        /// <para><c>--unity-cursor-color</c> is not a cursor: it is the colour of the text caret
        /// in a field, it is a custom property rather than the <c>cursor</c> property, and it
        /// works. A <c>url(...)</c> form works too and is allowed — what is banned is the keyword
        /// that silently does not.</para>
        /// </summary>
        [Test]
        public void TheStylesheetAsksForNoKeywordCursor()
        {
            string sheet = Read(SheetPath);
            var offenders = new System.Collections.Generic.List<string>();

            foreach (Match match in Regex.Matches(sheet, @"(?<!-)\bcursor\s*:\s*([^;}]+)"))
            {
                string value = match.Groups[1].Value.Trim();
                if (value.StartsWith("url(", StringComparison.OrdinalIgnoreCase)) continue;
                offenders.Add(value);
            }

            Assert.That(offenders, Is.Empty,
                "Hud.uss asks a runtime panel for a keyword cursor, which does nothing but log. "
                + "The pointer is set in CursorDirector; see docs/design/28-pointer-cursor.md. "
                + "Offending values: " + string.Join(", ", offenders));
        }

        /// <summary>
        /// <b>A pick is resolved against the camera the player is looking through, not the one
        /// they were looking through last frame.</b>
        ///
        /// <para>Until 2026-09-21 <c>SliceCameraRig.Update</c> ran <c>ReadMouse</c> — which cast
        /// every pointer ray — <i>before</i> <c>ApplyTransform</c> moved the camera. The result was
        /// a constant one-frame offset for the whole time the camera was moving, which is the
        /// owner's <i>"the cursor doesn't seem super accurate"</i>. It is exact while the camera is
        /// still, which is how it survived this long.</para>
        ///
        /// <para>The assertion is the ordering rather than the outcome because the outcome cannot
        /// be observed: no test in this project can move a mouse. Slide <c>ResolvePointer</c> back
        /// above <c>ApplyTransform</c> — or put a <c>CellAt</c> call back into <c>ReadMouse</c> —
        /// and this fails.</para>
        /// </summary>
        [Test]
        public void ThePointerIsResolvedAfterTheCameraHasMoved()
        {
            string body = UpdateBody(Read(RigPath));

            int moved = body.IndexOf("ApplyTransform(", StringComparison.Ordinal);
            int resolved = body.IndexOf("ResolvePointer(", StringComparison.Ordinal);

            Assert.That(moved, Is.GreaterThanOrEqualTo(0), "SliceCameraRig.Update no longer applies the camera transform.");
            Assert.That(resolved, Is.GreaterThanOrEqualTo(0), "SliceCameraRig.Update no longer resolves the pointer.");
            Assert.That(resolved, Is.GreaterThan(moved),
                "SliceCameraRig.Update resolves the pointer before moving the camera, so every "
                + "hover, drag and click is cast through last frame's transform. "
                + "See docs/design/28-pointer-cursor.md section 4.");
        }

        /// <summary>
        /// <b>And no ray is cast anywhere else.</b> The ordering above is only worth anything if
        /// <see cref="ResolvePointer"/> is the only place that turns a screen point into a cell —
        /// a second <c>CellAt</c> back up in <c>ReadMouse</c> would restore the fault for whichever
        /// gesture it served, and would do it silently.
        /// </summary>
        [Test]
        public void OnlyTheResolvePassTurnsAScreenPointIntoACell()
        {
            string rig = Read(RigPath);
            string resolve = Section(rig, "void ResolvePointer()");

            int everywhere = Regex.Matches(rig, @"\bCellAt\s*\(").Count;
            int declaration = 1;                                   // `bool CellAt(...)` itself
            int inResolve = Regex.Matches(resolve, @"\bCellAt\s*\(").Count;

            Assert.That(everywhere - declaration, Is.EqualTo(inResolve),
                "SliceCameraRig resolves a cell outside ResolvePointer, which is where the "
                + "one-frame pointer lag came from. See docs/design/28-pointer-cursor.md section 4.");
        }

        // ------------------------------------------------------------------ reading

        /// <summary>The body of <c>Update</c>, so that a mention of either method elsewhere in a
        /// 900-line file cannot decide this.</summary>
        static string UpdateBody(string rig) => Section(rig, "void Update()");

        /// <summary>
        /// From a method's signature to the first line that dedents back to its own brace column.
        /// Crude, and sufficient: every method in the file is at one indent inside its class.
        /// </summary>
        static string Section(string source, string signature)
        {
            int at = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), $"SliceCameraRig has no `{signature}`.");

            int open = source.IndexOf('{', at);
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0)
                    return source.Substring(open, i - open + 1);
            }

            Assert.Fail($"`{signature}` in SliceCameraRig never closes.");
            return string.Empty;
        }

        static string Read(string relative)
        {
            string? found = Find(relative);
            Assert.That(found, Is.Not.Null, $"Could not find {relative} from {Directory.GetCurrentDirectory()}.");
            return File.ReadAllText(found!);
        }

        /// <summary>
        /// Walk up for a path relative to the repository root — the same trick
        /// <c>HudStyleSheetTests</c> uses, because under Unity the working directory is the project
        /// root and in the fast tier it is a build output several levels down.
        /// </summary>
        static string? Find(string relative)
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (int i = 0; i < 12 && directory != null; i++, directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }
    }
}
