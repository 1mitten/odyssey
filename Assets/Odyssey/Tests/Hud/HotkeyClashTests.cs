#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// No command-bar hotkey is a key something else in the game already reads.
    ///
    /// <para><b>This test exists because its predecessor did not work.</b> The rebuilt command bar
    /// shipped with a hand-written list of nine "keys the game already uses" — M, C, X, R, F, V and
    /// the three speed digits — and asserted that no hotkey collided with it. The list was missing
    /// WASD, which pans the camera, B, which cycled the below-slice mode, and Q and E, which turn
    /// it. Five of the eleven hotkeys clashed and the test passed. A reserved list written from
    /// memory is a reserved list that is wrong the moment somebody binds a key without updating
    /// it.</para>
    ///
    /// <para>So the reserved set is <b>read out of the source</b>. Every
    /// <c>keys.somethingKey</c> in the Presentation assembly is a key the game reads, and the only
    /// ones a command may claim are the ones its own component reads on its behalf. It is an
    /// unusual shape of test — it greps the codebase — and it is the only shape that can answer
    /// the question at all without a running game and a person pressing keys.</para>
    /// </summary>
    public class HotkeyClashTests
    {
        /// <summary>Where the game reads its keyboard.</summary>
        static readonly string[] SourceRoots =
        {
            "Assets/Odyssey/Presentation",
        };

        /// <summary>
        /// The commands that are allowed to be read, and the one file each is read in. Anything
        /// else reading these is the clash this test is for.
        /// </summary>
        static readonly Dictionary<string, string> Owners = new Dictionary<string, string>
        {
            { "bKey", "HudShell.Bar.cs" },          // opens the Build palette
            { "escapeKey", "SettingsPresenter.cs" },// opens Menu, per 09 §6 case 6
        };

        [Test]
        public void NoCommandHotkeyIsAlreadyBoundToSomethingElse()
        {
            List<(string File, string Key)> reads = ScanForKeyReads();
            Assert.That(reads, Is.Not.Empty,
                "no keyboard reads were found in the source at all, so this test is scanning the " +
                "wrong place and proving nothing");

            foreach (HudCommand command in HudCommands.All)
            {
                string? property = InputSystemProperty(command.Hotkey);
                if (property == null) continue;   // a cap this test cannot map is reported below

                foreach ((string file, string key) in reads)
                {
                    if (key != property) continue;
                    Owners.TryGetValue(property, out string? owner);

                    Assert.That(file, Is.EqualTo(owner),
                        $"the command bar offers {command.Hotkey} for '{command.Label}', and " +
                        $"{file} reads {key} as well. Two things on one key is a binding the " +
                        "player cannot use and a bug that presents as one of them intermittently " +
                        "not working.");
                }
            }
        }

        [Test]
        public void EveryHotkeyCapIsOneThisTestCanCheck()
        {
            // A cap this test cannot map to an Input System property is a cap it silently skips,
            // which is how the last version of this guard came to pass while five hotkeys clashed.
            foreach (HudCommand command in HudCommands.All)
                Assert.That(InputSystemProperty(command.Hotkey), Is.Not.Null,
                    $"'{command.Hotkey}' on {command.Key} is not a cap this test knows how to " +
                    "check for clashes. Teach InputSystemProperty about it rather than leaving it " +
                    "unchecked.");
        }

        [Test]
        public void HotkeysAreDistinct()
        {
            var seen = new List<string>();
            foreach (HudCommand command in HudCommands.All)
            {
                Assert.That(command.Hotkey, Is.Not.Empty, $"{command.Key} has no hotkey cap");
                Assert.That(seen, Has.No.Member(command.Hotkey),
                    $"two command-bar items both claim {command.Hotkey}");
                seen.Add(command.Hotkey);
            }
        }

        /// <summary>
        /// The keys the game reads, reported so that a reader of a failure can see the whole
        /// picture rather than the one line that broke.
        /// </summary>
        [Test]
        public void TheKeysTheGameReadsAreReported()
        {
            var keys = new SortedSet<string>();
            foreach ((string _, string key) in ScanForKeyReads()) keys.Add(key);
            TestContext.WriteLine("The game reads: " + string.Join(", ", keys));
            Assert.That(keys, Is.Not.Empty);
        }

        /// <summary>The Input System property for a hotkey cap, or null if this test cannot say.</summary>
        static string? InputSystemProperty(string cap)
        {
            if (string.IsNullOrEmpty(cap)) return null;
            if (cap == "Esc") return "escapeKey";
            if (cap.Length == 1 && char.IsLetter(cap[0])) return char.ToLowerInvariant(cap[0]) + "Key";
            if (cap.Length == 1 && char.IsDigit(cap[0])) return "digit" + cap + "Key";
            if (Regex.IsMatch(cap, @"^F[1-9]$|^F1[0-2]$")) return cap.ToLowerInvariant() + "Key";
            return null;
        }

        static List<(string File, string Key)> ScanForKeyReads()
        {
            var reads = new List<(string, string)>();
            foreach (string root in SourceRoots)
            {
                string? directory = Find(root);
                if (directory == null) continue;

                foreach (string file in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
                foreach (Match match in Regex.Matches(File.ReadAllText(file), @"keys\.([a-zA-Z0-9]+Key)\b"))
                    reads.Add((Path.GetFileName(file), match.Groups[1].Value));
            }
            return reads;
        }

        /// <summary>
        /// The project root differs between the two tiers: Unity runs with it as the working
        /// directory, the fast tier runs from a build output several levels below it.
        /// </summary>
        static string? Find(string relative)
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var directory = new DirectoryInfo(start);
                while (directory != null)
                {
                    string candidate = Path.Combine(directory.FullName, relative);
                    if (Directory.Exists(candidate)) return candidate;
                    directory = directory.Parent;
                }
            }
            return null;
        }
    }
}
