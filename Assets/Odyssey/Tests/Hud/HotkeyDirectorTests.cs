#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The binding map: defaults, rebinds, refusals and what a stored file full of collisions
    /// does. Everything here is the fast tier's to hold because every rule is a promise the
    /// panel makes on screen — "that key is taken", "Escape gets you out of the rebind" —
    /// and a promise only an engine run can check is a promise checked once per milestone.
    /// </summary>
    public class HotkeyDirectorTests
    {
        [Test]
        public void TheDefaultsAreTheKeysTheGameShipsReading()
        {
            var hotkeys = new HotkeyDirector();

            // This table is the one the settings panel shows as "the default", so it is the
            // one the commit that changes a binding has to change on purpose, beside the
            // camera code that reads it.
            Assert.That(hotkeys.Key(HotkeyAction.CameraForward, 0), Is.EqualTo(HudKey.W));
            Assert.That(hotkeys.Key(HotkeyAction.CameraForward, 1), Is.EqualTo(HudKey.Up));
            Assert.That(hotkeys.Key(HotkeyAction.CameraBack, 0), Is.EqualTo(HudKey.S));
            Assert.That(hotkeys.Key(HotkeyAction.CameraBack, 1), Is.EqualTo(HudKey.Down));
            Assert.That(hotkeys.Key(HotkeyAction.CameraRight, 0), Is.EqualTo(HudKey.D));
            Assert.That(hotkeys.Key(HotkeyAction.CameraRight, 1), Is.EqualTo(HudKey.Right));
            Assert.That(hotkeys.Key(HotkeyAction.CameraLeft, 0), Is.EqualTo(HudKey.A));
            Assert.That(hotkeys.Key(HotkeyAction.CameraLeft, 1), Is.EqualTo(HudKey.Left));
            Assert.That(hotkeys.Key(HotkeyAction.CameraTurnLeft, 0), Is.EqualTo(HudKey.Q));
            Assert.That(hotkeys.Key(HotkeyAction.CameraTurnRight, 0), Is.EqualTo(HudKey.E));
            Assert.That(hotkeys.Key(HotkeyAction.SliceUp, 0), Is.EqualTo(HudKey.R));
            Assert.That(hotkeys.Key(HotkeyAction.SliceUp, 1), Is.EqualTo(HudKey.PageUp));
            Assert.That(hotkeys.Key(HotkeyAction.SliceDown, 0), Is.EqualTo(HudKey.F));
            Assert.That(hotkeys.Key(HotkeyAction.SliceDown, 1), Is.EqualTo(HudKey.PageDown));
            Assert.That(hotkeys.Key(HotkeyAction.CycleAbove, 0), Is.EqualTo(HudKey.V));
            Assert.That(hotkeys.Key(HotkeyAction.FrameMap, 0), Is.EqualTo(HudKey.Home));
            Assert.That(hotkeys.Key(HotkeyAction.Pause, 0), Is.EqualTo(HudKey.Space));
            Assert.That(hotkeys.Key(HotkeyAction.Speed1, 0), Is.EqualTo(HudKey.Digit1));
            Assert.That(hotkeys.Key(HotkeyAction.Speed2, 0), Is.EqualTo(HudKey.Digit2));
            Assert.That(hotkeys.Key(HotkeyAction.Speed3, 0), Is.EqualTo(HudKey.Digit3));
            Assert.That(hotkeys.Key(HotkeyAction.ToolMine, 0), Is.EqualTo(HudKey.M));
            Assert.That(hotkeys.Key(HotkeyAction.ToolFell, 0), Is.EqualTo(HudKey.C));
            Assert.That(hotkeys.Key(HotkeyAction.ToolCancel, 0), Is.EqualTo(HudKey.X));
            Assert.That(hotkeys.Key(HotkeyAction.BuildPalette, 0), Is.EqualTo(HudKey.B));
            Assert.That(hotkeys.Key(HotkeyAction.DebugMenu, 0), Is.EqualTo(HudKey.Backquote));
        }

        [Test]
        public void NoTwoDefaultsShareAKeyAndEveryActionIsCovered()
        {
            var hotkeys = new HotkeyDirector();
            var seen = new HashSet<HudKey>();

            foreach (HotkeyAction action in HotkeyDirector.All)
                for (int slot = 0; slot < HotkeyDirector.SlotCount; slot++)
                {
                    HudKey key = hotkeys.Key(action, slot);
                    if (key == HudKey.None) continue;
                    Assert.That(seen, Has.No.Member(key),
                        $"{key} is the default for more than one action; the shipping game has a clash");
                    seen.Add(key);
                }

            Assert.That(seen, Has.Count.GreaterThan(0), "the action list came back empty");
        }

        [Test]
        public void EveryKeyHasANameWorthPuttingOnACap()
        {
            // An empty display for a bound key is a blank cap, which reads as "unbound".
            foreach (HotkeyAction action in HotkeyDirector.All)
                for (int slot = 0; slot < HotkeyDirector.SlotCount; slot++)
                {
                    HudKey key = new HotkeyDirector().Key(action, slot);
                    if (key == HudKey.None) continue;
                    Assert.That(HotkeyDirector.Display(key), Is.Not.Empty,
                        $"{key} draws as an empty cap");
                }

            Assert.That(HotkeyDirector.Display(HudKey.PageUp), Is.EqualTo("PgUp"));
            Assert.That(HotkeyDirector.Display(HudKey.Digit1), Is.EqualTo("1"));
            Assert.That(HotkeyDirector.Display(HudKey.None), Is.Empty);
        }

        [Test]
        public void OfferingAKeyToAListeningSlotBindsItAndWritesItDown()
        {
            var hotkeys = new HotkeyDirector();
            var store = new FakeSettingsStore();
            hotkeys.UseStore(store);
            var changed = new List<HotkeyAction>();
            hotkeys.BindingChanged += changed.Add;

            hotkeys.Listen(HotkeyAction.CameraTurnLeft, 0);
            Assert.That(hotkeys.Listening, Is.EqualTo((HotkeyAction.CameraTurnLeft, 0)));

            Assert.That(hotkeys.Capture(HudKey.Z), Is.EqualTo(RebindResult.Bound));
            Assert.That(hotkeys.Key(HotkeyAction.CameraTurnLeft, 0), Is.EqualTo(HudKey.Z));
            Assert.That(hotkeys.Listening, Is.Null, "a bound key closes the door behind it");
            Assert.That(changed, Is.EqualTo(new[] { HotkeyAction.CameraTurnLeft }));
            Assert.That(store.ReadString(HotkeyDirector.KeyOf(HotkeyAction.CameraTurnLeft)), Is.EqualTo("Z"),
                "the binding is written as the key's name, readable by eye in the registry");
        }

        [Test]
        public void NothingIsCapturedWhileNobodyIsListening()
        {
            var hotkeys = new HotkeyDirector();
            Assert.That(hotkeys.Capture(HudKey.Z), Is.EqualTo(RebindResult.UnknownKey));
            Assert.That(hotkeys.Key(HotkeyAction.CameraTurnLeft, 0), Is.EqualTo(HudKey.Q));
        }

        [Test]
        public void AKeyAnotherActionOwnsIsRefusedNotStolen()
        {
            var hotkeys = new HotkeyDirector();
            hotkeys.Listen(HotkeyAction.CameraTurnLeft, 0);

            // E turns the camera the other way. Handing it to the turn-left slot would fix
            // the clash on screen and plant one the player cannot see.
            Assert.That(hotkeys.Capture(HudKey.E), Is.EqualTo(RebindResult.Conflict));
            Assert.That(hotkeys.Key(HotkeyAction.CameraTurnLeft, 0), Is.EqualTo(HudKey.Q),
                "a refused key changes nothing");
            Assert.That(hotkeys.OwnerOf(HudKey.E), Is.EqualTo(HotkeyAction.CameraTurnRight),
                "the panel can still say who has the key");
            Assert.That(hotkeys.Listening, Is.EqualTo((HotkeyAction.CameraTurnLeft, 0)),
                "a refused key is a wrong answer, not the end of the question");
            Assert.That(hotkeys.LastResult, Is.EqualTo(RebindResult.Conflict));
            Assert.That(hotkeys.LastConflictOwner, Is.EqualTo(HotkeyAction.CameraTurnRight),
                "the panel can name the owner without asking the map again");
        }

        [Test]
        public void TheDefaultsStayReadableWhateverThePlayerDidToThem()
        {
            var hotkeys = new HotkeyDirector();
            hotkeys.Listen(HotkeyAction.SliceUp, 0);
            hotkeys.Capture(HudKey.Z);

            Assert.That(hotkeys.Key(HotkeyAction.SliceUp, 0), Is.EqualTo(HudKey.Z));
            Assert.That(HotkeyDirector.DefaultKey(HotkeyAction.SliceUp, 0), Is.EqualTo(HudKey.R),
                "the default is a fact about the shipped game, not about this machine");
            Assert.That(HotkeyDirector.DefaultKey(HotkeyAction.SliceUp, 1), Is.EqualTo(HudKey.PageUp));
            Assert.That(HotkeyDirector.DefaultKey(HotkeyAction.Pause, 1), Is.EqualTo(HudKey.None));
        }

        [Test]
        public void BindingAnActionToItsOwnOtherSlotMovesTheKey()
        {
            var hotkeys = new HotkeyDirector();
            hotkeys.Listen(HotkeyAction.SliceUp, 1);

            // R is SliceUp's primary. Offering it to the alternate slot is a move, not a
            // clash, and must not leave the action reading one key twice.
            Assert.That(hotkeys.Capture(HudKey.R), Is.EqualTo(RebindResult.Bound));
            Assert.That(hotkeys.Key(HotkeyAction.SliceUp, 0), Is.EqualTo(HudKey.None));
            Assert.That(hotkeys.Key(HotkeyAction.SliceUp, 1), Is.EqualTo(HudKey.R));
        }

        [Test]
        public void EscapeCancelsARebindBeforeItUnwindsAnything()
        {
            var hotkeys = new HotkeyDirector();
            hotkeys.Listen(HotkeyAction.ToolMine, 0);
            Assert.That(hotkeys.ConsumeEscape(), Is.True, "the rebind is the top of the stack");
            Assert.That(hotkeys.Listening, Is.Null);
            Assert.That(hotkeys.Key(HotkeyAction.ToolMine, 0), Is.EqualTo(HudKey.M));

            Assert.That(hotkeys.ConsumeEscape(), Is.False,
                "with nothing listening, Escape belongs to the unwind order, not to this director");
        }

        [Test]
        public void AClearedSlotLeavesTheActionOnItsOtherKey()
        {
            var hotkeys = new HotkeyDirector();
            hotkeys.ClearSlot(HotkeyAction.SliceUp, 0);

            Assert.That(hotkeys.Key(HotkeyAction.SliceUp, 0), Is.EqualTo(HudKey.None));
            Assert.That(hotkeys.Key(HotkeyAction.SliceUp, 1), Is.EqualTo(HudKey.PageUp));
            Assert.That(hotkeys.OwnerOf(HudKey.R), Is.Null, "the freed key is nobody's now");
        }

        [Test]
        public void ResetPutsEveryActionBackAndLeavesNoHalfStateBehind()
        {
            var hotkeys = new HotkeyDirector();
            var store = new FakeSettingsStore();
            hotkeys.UseStore(store);

            hotkeys.Listen(HotkeyAction.SliceUp, 0);
            hotkeys.Capture(HudKey.Z);
            hotkeys.ClearSlot(HotkeyAction.Pause, 0);

            hotkeys.ResetKeys();
            Assert.That(hotkeys.Key(HotkeyAction.SliceUp, 0), Is.EqualTo(HudKey.R));
            Assert.That(hotkeys.Key(HotkeyAction.Pause, 0), Is.EqualTo(HudKey.Space));
            Assert.That(store.ReadString(HotkeyDirector.KeyOf(HotkeyAction.SliceUp)), Is.EqualTo("R+PageUp"),
                "the store says what the map says");
            Assert.That(store.ReadString(HotkeyDirector.KeyOf(HotkeyAction.Pause)), Is.EqualTo("Space"));
            Assert.That(hotkeys.Listening, Is.Null);
        }

        [Test]
        public void StoredBindingsAreLaidOverTheDefaultsAndSurviveARestart()
        {
            var first = new HotkeyDirector();
            var store = new FakeSettingsStore();
            first.UseStore(store);
            first.Listen(HotkeyAction.ToolFell, 0);
            first.Capture(HudKey.Z);

            // A second director over the same store is a restart: nothing in memory, everything
            // in the file.
            var second = new HotkeyDirector();
            second.UseStore(store);
            Assert.That(second.Key(HotkeyAction.ToolFell, 0), Is.EqualTo(HudKey.Z));
            Assert.That(second.Key(HotkeyAction.ToolMine, 0), Is.EqualTo(HudKey.M),
                "an action the file never mentions keeps its default");
        }

        [Test]
        public void AStoredKeyADefaultShipsOnIsRefusedAndKeepsItsDefault()
        {
            var store = new FakeSettingsStore();
            store.Preset(HotkeyDirector.KeyOf(HotkeyAction.CameraTurnLeft), "E");

            var hotkeys = new HotkeyDirector();
            hotkeys.UseStore(store);

            // E is the turn-right key by default, and the stored line does not get to take a
            // key off another action to keep its wish. The slot falls back to its own
            // default — the action stays reachable — and the refusal is on the record.
            Assert.That(hotkeys.Key(HotkeyAction.CameraTurnLeft, 0), Is.EqualTo(HudKey.Q));
            Assert.That(hotkeys.Key(HotkeyAction.CameraTurnRight, 0), Is.EqualTo(HudKey.E));
            Assert.That(hotkeys.LoadConflicts, Is.EqualTo(new[] { (HotkeyAction.CameraTurnLeft, 0) }));
        }

        [Test]
        public void TwoStoredLinesOnOneKeySettleInDrawOrder()
        {
            var store = new FakeSettingsStore();
            store.Preset(HotkeyDirector.KeyOf(HotkeyAction.ToolMine), "Z");
            store.Preset(HotkeyDirector.KeyOf(HotkeyAction.ToolFell), "Z");

            var hotkeys = new HotkeyDirector();
            hotkeys.UseStore(store);

            // Mine draws before Fell, so mine keeps the disputed key and fell falls back to
            // its default. Somebody has to win, and "whoever the panel lists first" is a
            // rule a reader can reconstruct without the code.
            Assert.That(hotkeys.Key(HotkeyAction.ToolMine, 0), Is.EqualTo(HudKey.Z));
            Assert.That(hotkeys.Key(HotkeyAction.ToolFell, 0), Is.EqualTo(HudKey.C));
            Assert.That(hotkeys.LoadConflicts, Is.EqualTo(new[] { (HotkeyAction.ToolFell, 0) }));
        }

        [Test]
        public void AStoredLineNobodyCanParseIsDroppedNotObeyed()
        {
            var store = new FakeSettingsStore();
            store.Preset(HotkeyDirector.KeyOf(HotkeyAction.Pause), "VolumeKnob");

            var hotkeys = new HotkeyDirector();
            hotkeys.UseStore(store);
            Assert.That(hotkeys.Key(HotkeyAction.Pause, 0), Is.EqualTo(HudKey.Space),
                "a line that parsed to nothing is garbage, and garbage keeps the default");
            Assert.That(hotkeys.LoadConflicts, Is.Empty);
        }

        [Test]
        public void AnEmptyStoredLineMeansBothSlotsWereClearedOnPurpose()
        {
            var store = new FakeSettingsStore();
            store.Preset(HotkeyDirector.KeyOf(HotkeyAction.SliceUp), "");

            var hotkeys = new HotkeyDirector();
            hotkeys.UseStore(store);
            Assert.That(hotkeys.Key(HotkeyAction.SliceUp, 0), Is.EqualTo(HudKey.None));
            Assert.That(hotkeys.Key(HotkeyAction.SliceUp, 1), Is.EqualTo(HudKey.None));
            Assert.That(hotkeys.LoadConflicts, Is.Empty);
        }
    }
}
