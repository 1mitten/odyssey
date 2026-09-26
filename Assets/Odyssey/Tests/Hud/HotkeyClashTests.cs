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
    /// <para>Since <see cref="HotkeyDirector"/> landed (2026-09-17) the game reads no key by name
    /// at all: every poller asks the binding map for an action, and a clash between two actions is
    /// a state the director refuses to build. So this guard's question has changed shape once
    /// more. It still reads the source — the one place the question can be answered without a
    /// running game and a person pressing keys — but what it looks for now is a <i>regression</i>:
    /// a <c>keys.somethingKey</c> read is a key read past the binding map, and the only two left
    /// on purpose are Escape, which is the unwind rule and not bindable, and the capture loop,
    /// which offers every key to the rebind and names none of them.</para>
    ///
    /// <para>And the reserved set a command's cap is checked against is read out of the binding
    /// map's own defaults, so a cap cannot drift out of step with the keys the game actually
    /// ships reading.</para>
    /// </summary>
    public class HotkeyClashTests
    {
        /// <summary>Where the game reads its keyboard.</summary>
        static readonly string[] SourceRoots =
        {
            "Assets/Odyssey/Presentation",
        };

        /// <summary>
        /// The named key reads the game is allowed to keep, and the one file each is read in.
        /// A read anywhere else is a binding made behind the map's back.
        ///
        /// <para>Three reads, two reasons. Escape is the unwind rule (09 §6 case 6) and is not
        /// bindable; the capture loop offers every key to a rebind and names none; and Shift is
        /// the one modifier the game keeps for itself — the fast multiplier and the
        /// additive-selection modifier — which <see cref="HudKey"/>'s docs already refuse to
        /// bind.</para>
        /// </summary>
        static readonly Dictionary<string, string> Owners = new Dictionary<string, string>
        {
            { "escapeKey", "SettingsPresenter.cs" },  // the unwind rule, 09 §6 case 6 — fixed, not bindable
            { "allKeys", "SettingsPresenter.cs" },    // the rebind capture: every key offered, none named
            { "backspaceKey", "SettingsPresenter.cs" }, // empties a slot while it listens (design 39 §6); only then
            { "leftShiftKey", "SliceCameraRig.cs" },  // Shift: the fast modifier, deliberately unbindable
            { "rightShiftKey", "SliceCameraRig.cs" },
            // The wake into a world (design 56 §8): any key at all wakes you. It names no key, so
            // it binds nothing and clashes with nothing; it is read only while the wake holds the
            // keys (HotkeyDirector.Suspended), when no binding is live.
            { "anyKey", "HudShell.Wake.cs" },
            // Tab inside the trade window (design 57 §6): switches Sell and Buy. Fixed, as Escape is
            // — Tab is no HudKey, so nothing can bind it — and read only while that modal is up,
            // when it holds the game's keys (HotkeyDirector.Suspended).
            { "tabKey", "HudShell.Trade.cs" },
        };

        [Test]
        public void NoKeyIsReadByNameExceptEscapeAndTheCapture()
        {
            List<(string File, string Key)> reads = ScanForKeyReads();
            Assert.That(reads, Is.Not.Empty,
                "no keyboard reads were found in the source at all, so this test is scanning the " +
                "wrong place and proving nothing");

            foreach ((string file, string key) in reads)
            {
                Owners.TryGetValue(key, out string? owner);

                Assert.That(owner, Is.Not.Null,
                    $"{file} reads {key} by name. Read the binding map's actions instead: a named " +
                    "key read is a binding the player cannot change and a clash the director " +
                    "cannot see. If the key genuinely must be fixed, say so here and in " +
                    "HotkeyDirector's docs, as Escape does.");
                Assert.That(file, Is.EqualTo(owner),
                    $"{key} is read in {file} as well, and one key with two owners is the fault " +
                    "this test has stood against since the command bar shipped with five hotkeys " +
                    "already taken");
            }
        }

        [Test]
        public void EveryHotkeyCapIsOneThisTestCanCheck()
        {
            // A cap this test cannot map to a binding-map key is a cap it silently skips, which
            // is how the last version of this guard came to pass while five hotkeys clashed.
            foreach (HudCommand command in HudCommands.All)
                Assert.That(CapIsCheckable(command.Hotkey), Is.True,
                    $"'{command.Hotkey}' on {command.Key} is neither a key the binding map can " +
                    "name nor one of the caps this test knows it may not bind. Teach one or the " +
                    "other rather than leaving it unchecked.");
        }

        /// <summary>
        /// The commands whose cap is a <em>real binding</em> rather than a legend on a control
        /// that does not exist yet.
        ///
        /// <para><b>This was "Build, and only Build" until design 27.</b> The Work tab put a panel
        /// behind F1, so the binding map binds a function key for the first time and the old
        /// blanket "F1 to F9 are keys the map will not bind" stopped being true of one of them.
        /// The table is the honest form of that rule and it is what the remaining eight rows join
        /// as their panels arrive — each one turning a legend into a binding, and each one having
        /// to say so here.</para>
        /// </summary>
        static readonly (string CommandKey, HotkeyAction Action)[] BoundCommands =
        {
            (HudCommands.BuildKey, HotkeyAction.BuildPalette),
            (HudCommands.WorkKey, HotkeyAction.WorkTab),
            (HudCommands.InventoryKey, HotkeyAction.InventoryTab),
            (HudCommands.ResearchKey, HotkeyAction.ResearchTab),
            (HudCommands.AssignKey, HotkeyAction.AssignTab),
            (HudCommands.AnimalsKey, HotkeyAction.AnimalsTab),
            (HudCommands.AlmanacKey, HotkeyAction.Almanac),
        };

        static HotkeyAction? BoundActionFor(string commandKey)
        {
            foreach ((string key, HotkeyAction action) in BoundCommands)
                if (key == commandKey) return action;
            return null;
        }

        [Test]
        public void NoCommandCapNamesAKeyAnActionDefaultsTo()
        {
            var hotkeys = new HotkeyDirector();
            foreach (HudCommand command in HudCommands.All)
            {
                HotkeyAction? owner = DefaultOwnerOfCap(hotkeys, command.Hotkey);
                if (owner == null) continue;   // Esc and the unbound function keys

                HotkeyAction? mine = BoundActionFor(command.Key);
                Assert.That(mine, Is.Not.Null,
                    $"the command bar offers {command.Hotkey} for '{command.Label}', and the " +
                    "binding map ships that key to an action. Two things on one key is a binding " +
                    "the player cannot use and a bug that presents as one of them intermittently " +
                    "not working. If this command's cap has become a real binding, say so in " +
                    "BoundCommands.");
                Assert.That(owner, Is.EqualTo(mine),
                    $"'{command.Label}' caps {command.Hotkey}, but the map ships that key to " +
                    $"{owner} rather than to the action this command opens.");
            }
        }

        [Test]
        public void ACapThatIsARealBindingSaysTheKeyTheBindingMapShips()
        {
            var hotkeys = new HotkeyDirector();
            foreach (HudCommand command in HudCommands.All)
            {
                HotkeyAction? action = BoundActionFor(command.Key);
                if (action == null) continue;

                Assert.That(command.Hotkey,
                    Is.EqualTo(HotkeyDirector.Display(hotkeys.Key(action.Value, 0))),
                    $"'{command.Label}' caps a real binding; if the default moves, the cap moves " +
                    "with it or it is a lie on the bar");
                Assert.That(command.Live, Is.True,
                    $"'{command.Label}' has a working key but is drawn as a dead item, so the " +
                    "key opens something the bar says does not exist yet");
            }
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
            TestContext.WriteLine("The game reads by name: " + string.Join(", ", keys));
            Assert.That(keys, Is.Not.Empty);
        }

        /// <summary>
        /// Whether a cap is one this test can hold to the binding map: either it names a key
        /// the map could bind — so <see cref="DefaultOwnerOfCap"/> can check it against the
        /// defaults — or it is one of the caps the map refuses on purpose, which may not be
        /// claimed because nothing can be bound to them.
        /// </summary>
        static bool CapIsCheckable(string cap)
        {
            if (string.IsNullOrEmpty(cap)) return false;
            if (cap == "Esc") return true;                       // the unwind key, fixed
            if (Regex.IsMatch(cap, @"^F[1-9]$|^F1[0-2]$")) return true; // promised to panels
            return CapToKey(cap).HasValue;
        }

        /// <summary>
        /// The binding-map key a cap names, or null when the cap is not a single key this map
        /// can display.
        /// </summary>
        static HudKey? CapToKey(string cap)
        {
            foreach (HudKey key in Enum.GetValues(typeof(HudKey)))
                if (key != HudKey.None && HotkeyDirector.Display(key) == cap) return key;
            return null;
        }

        /// <summary>
        /// The action whose default a cap names, or null when no action ships on it — the
        /// reserved set, read out of the binding map rather than out of somebody's memory.
        /// </summary>
        static HotkeyAction? DefaultOwnerOfCap(HotkeyDirector hotkeys, string cap)
        {
            HudKey? key = CapToKey(cap);
            if (key == null) return null;

            foreach (HotkeyAction action in HotkeyDirector.All)
                for (int slot = 0; slot < HotkeyDirector.SlotCount; slot++)
                    if (hotkeys.Key(action, slot) == key) return action;
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
