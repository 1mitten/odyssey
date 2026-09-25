#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The settings window's frame, icons and the two rules it added (design 39): what can be
    /// answered without a panel to lay out. That the window is the same box on every tab is the
    /// PlayMode tier's (<c>SettingsWindowTests</c>); that the tallest tab fits it is here.
    /// </summary>
    public class SettingsLayoutTests
    {
        // ------------------------------------------------------------------ the frame

        [Test]
        public void TheKeysTabFitsTheFrameWithNoScrolling()
        {
            Assert.That(SettingsLayout.ColumnsHeight, Is.GreaterThan(0));
            foreach (SettingsLayout.KeyGroup[] column in SettingsLayout.KeyColumns)
            {
                var rows = new List<int>();
                foreach (SettingsLayout.KeyGroup group in column) rows.Add(group.Actions.Length);
                int height = SettingsLayout.ColumnHeight(rows);
                Assert.That(height, Is.LessThanOrEqualTo(SettingsLayout.ColumnsHeight),
                    $"a Keys column is {height} px against {SettingsLayout.ColumnsHeight} px of room, " +
                    "so the tallest tab would scroll or the frame would have to grow");
            }
        }

        [Test]
        public void TheGraphicsTabFitsTheFrameToo()
        {
            // The Quality band across the top takes a row and a section gap from both columns.
            int band = SettingsLayout.RowHeight + SettingsLayout.SectionGap;
            int left = band + SettingsLayout.ColumnHeight(new[]
            {
                // The display ladders and the resolution row, then the performance ladders.
                SettingsLayout.DisplayLadders.Length + 1, SettingsLayout.PerformanceLadders.Length,
            });
            // The grass ladders, then the switches.
            int right = band + SettingsLayout.ColumnHeight(new[]
                { SettingsDirector.DetailLadders.Count + SettingsDirector.All.Count });
            Assert.That(left, Is.LessThanOrEqualTo(SettingsLayout.ColumnsHeight));
            Assert.That(right, Is.LessThanOrEqualTo(SettingsLayout.ColumnsHeight));
        }

        /// <summary>
        /// Display and Performance between them are every ladder the director calls a display
        /// ladder, each once. The split is this window's; the set is the director's, and a ladder
        /// added there (the grass came in on 2026-09-24) must not fall off the page.
        /// </summary>
        [Test]
        public void TheDisplayAndPerformanceSectionsHoldEveryDisplayLadder()
        {
            var drawn = new List<GraphicsLadder>(SettingsLayout.DisplayLadders);
            drawn.AddRange(SettingsLayout.PerformanceLadders);
            Assert.That(drawn, Is.Unique);
            Assert.That(drawn, Is.EquivalentTo(SettingsDirector.DisplayLadders));
        }

        [Test]
        public void TheFrameIsCentredAtThreeScreens()
        {
            // At 1920 x 1080 the design puts the frame at 340 / 180; the offsets are half the
            // frame, so the same holds on any canvas.
            Assert.That(1920 / 2 - SettingsLayout.Width / 2, Is.EqualTo(340));
            Assert.That(1080 / 2 - SettingsLayout.Height / 2, Is.EqualTo(180));
        }

        [Test]
        public void EveryActionOnTheKeysTabIsListedOnce()
        {
            var seen = new HashSet<HotkeyAction>();
            foreach (SettingsLayout.KeyGroup[] column in SettingsLayout.KeyColumns)
                foreach (SettingsLayout.KeyGroup group in column)
                    foreach (HotkeyAction action in group.Actions)
                        Assert.That(seen.Add(action), Is.True, $"{action} is on the Keys tab twice");
            Assert.That(seen.Count, Is.EqualTo(26),
                "the Keys tab lists the 24 actions it listed before the rebuild, walls-down (H, design 42) " +
                "and the Assign tab (F4, design 43)");
        }

        [Test]
        public void EverySessionActionHasAnIconAndATone()
        {
            foreach (SessionCommand command in SessionCommands.For(SessionContext.InGame))
                Assert.That(SettingsLayout.ActionIcon(command.Key), Is.Not.EqualTo(SettingsLayout.CloseIcon),
                    $"{command.Key} has no icon of its own in the rail");

            Assert.That(SettingsLayout.ToneOf(SessionCommands.QuitKey), Is.EqualTo(SettingsLayout.ActionTone.Bad));
            Assert.That(SettingsLayout.ToneOf(SessionCommands.QuitToMenuKey), Is.EqualTo(SettingsLayout.ActionTone.Warn));
            Assert.That(SettingsLayout.ToneOf(SessionCommands.LoadKey), Is.EqualTo(SettingsLayout.ActionTone.Info));
            Assert.That(SettingsLayout.ToneOf(SessionCommands.SaveKey), Is.EqualTo(SettingsLayout.ActionTone.Good));
        }

        // ------------------------------------------------------------------ icons

        static IEnumerable<string> Icons()
        {
            foreach (SettingsTab tab in SettingsLayout.Tabs) yield return SettingsLayout.IconOf(tab);
            foreach (SessionCommand command in SessionCommands.For(SessionContext.InGame))
                yield return SettingsLayout.ActionIcon(command.Key);
            yield return SettingsLayout.GearIcon;
            yield return SettingsLayout.CloseIcon;
            yield return SettingsLayout.ResetIcon;
            yield return HudIcons.Home;
            yield return HudIcons.Cycle;
            yield return HudIcons.ChevronLeft;
            yield return HudIcons.ChevronRight;
        }

        [Test]
        public void EveryIconParsesAndStaysInItsBox()
        {
            foreach (string d in Icons())
            {
                IReadOnlyList<SvgPath.Subpath> paths = SvgPath.Parse(d);
                Assert.That(paths, Is.Not.Empty, $"\"{d}\" drew nothing");
                foreach (SvgPath.Subpath path in paths)
                    for (int i = 0; i < path.Points.Length; i++)
                        Assert.That(path.Points[i], Is.InRange(0f, SettingsLayout.IconBox),
                            $"\"{d}\" leaves its 24-unit box at {path.Points[i]}");
            }
        }

        [Test]
        public void AnArcIsDrawnRoundItsCentre()
        {
            // The eye's pupil: two half-circle arcs of radius 3 round (12, 12).
            IReadOnlyList<SvgPath.Subpath> paths = SvgPath.Parse("M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6");
            Assert.That(paths, Has.Count.EqualTo(1));
            float[] p = paths[0].Points;
            for (int i = 0; i < p.Length; i += 2)
            {
                float dx = p[i] - 12f, dy = p[i + 1] - 12f;
                Assert.That(dx * dx + dy * dy, Is.EqualTo(9f).Within(0.05f), "a point strayed off the circle");
            }
            Assert.That(p[p.Length - 2], Is.EqualTo(12f).Within(1e-3f));
            Assert.That(p[p.Length - 1], Is.EqualTo(15f).Within(1e-3f), "the second arc does not return to the start");
        }

        [Test]
        public void CompactNumbersAndRelativeCommandsParse()
        {
            // "h.5", a sign starting the next number, and a close followed by more drawing.
            IReadOnlyList<SvgPath.Subpath> paths = SvgPath.Parse("M17 10.5h.5M16 8l4 4-4 4M3 4h18v16H3zM3 9h18");
            Assert.That(paths, Has.Count.EqualTo(4));
            Assert.That(paths[0].Points, Is.EqualTo(new[] { 17f, 10.5f, 17.5f, 10.5f }));
            Assert.That(paths[1].Points, Is.EqualTo(new[] { 16f, 8f, 20f, 12f, 16f, 16f }));
            Assert.That(paths[2].Closed, Is.True);
        }

        [Test]
        public void AnUnknownCommandFailsLoudly()
        {
            Assert.Throws<System.FormatException>(() => SvgPath.Parse("M0 0 X4 4"));
        }

        // ------------------------------------------------------------------ ASCII

        static void AssertAscii(string text, string where)
        {
            foreach (char c in text)
                Assert.That(c, Is.LessThan((char)128), $"{where} says \"{text}\", which is not ASCII");
        }

        [Test]
        public void EveryWordTheWindowWritesIsAscii()
        {
            foreach (SettingsTab tab in SettingsLayout.Tabs)
            {
                AssertAscii(SettingsLayout.Subtitle(tab), $"{tab}'s subtitle");
                AssertAscii(SettingsLayout.ResetLabel(Registry.Label(SettingsDirector.TabKey(tab))), $"{tab}'s reset");
            }
            AssertAscii(SettingsLayout.KeysHint, "the Keys hint");
            foreach (GraphicsLadder ladder in SettingsDirector.AllLadders)
                foreach (int rung in SettingsDirector.RungsOf(ladder))
                    AssertAscii(SettingsDirector.RungLabel(ladder, rung), $"{ladder}'s {rung} rung");
            foreach (int days in AutosaveClock.DayRungs) AssertAscii(AutosaveClock.RungLabel(days), "an autosave rung");
            foreach (BuildPaletteLayout layout in BuildPaletteModel.Layouts)
                AssertAscii(BuildPaletteModel.LayoutName(layout), "a palette layout");
            foreach (HotkeyAction action in HotkeyDirector.All)
                for (int slot = 0; slot < HotkeyDirector.SlotCount; slot++)
                    AssertAscii(HotkeyDirector.Display(HotkeyDirector.DefaultKey(action, slot)), $"{action}'s key");
        }

        [Test]
        public void AntiAliasingSaysItsMultiplierWithAnX()
        {
            Assert.That(SettingsDirector.RungLabel(GraphicsLadder.AntiAliasing, 4), Is.EqualTo("4x"));
            Assert.That(SettingsDirector.RungLabel(GraphicsLadder.AntiAliasing, 1), Is.EqualTo("Off"));
        }

        [Test]
        public void EveryHeadingIsARegisteredName()
        {
            foreach (string key in SettingsLayout.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
        }

        // ------------------------------------------------------------------ reset

        [Test]
        public void ResettingATabPutsItsSettingsBackAndLeavesTheOthers()
        {
            var settings = new SettingsDirector { DefaultUiScale = 110 };
            settings.SetUiScale(150);
            settings.SetCameraSpeed(60);
            settings.SetBuildPaletteLayout(BuildPaletteLayout.Bar);
            settings.SetValue(GraphicsLadder.RenderScale, 70);
            settings.Set(GraphicsOption.Surround, false);
            settings.SetBusDb(SettingsBus.Music, -20);
            settings.SetAutosaveDays(3);

            settings.ResetTab(SettingsTab.Interface);
            Assert.That(settings.UiScale, Is.EqualTo(110), "the scale goes back to the screen's own default");
            Assert.That(settings.CameraSpeed, Is.EqualTo(100));
            Assert.That(settings.BuildPaletteLayout, Is.EqualTo(BuildPaletteModel.Default));
            Assert.That(settings.Value(GraphicsLadder.RenderScale), Is.EqualTo(70), "Interface's reset reached Graphics");

            settings.ResetTab(SettingsTab.Graphics);
            Assert.That(settings.Value(GraphicsLadder.RenderScale), Is.EqualTo(SettingsDirector.DefaultOf(GraphicsLadder.RenderScale)));
            Assert.That(settings.IsOn(GraphicsOption.Surround), Is.True);
            Assert.That(settings.IsOn(GraphicsOption.CutAwayCeiling), Is.False, "the one option that ships off");
            Assert.That(settings.BusDb(SettingsBus.Music), Is.EqualTo(-20), "Graphics' reset reached Audio");

            settings.ResetTab(SettingsTab.Audio);
            Assert.That(settings.BusDb(SettingsBus.Music), Is.EqualTo(SettingsDirector.UnityDb));
            Assert.That(settings.AutosaveDays, Is.EqualTo(3), "Audio's reset reached Gameplay");

            settings.ResetTab(SettingsTab.Gameplay);
            Assert.That(settings.AutosaveDays, Is.EqualTo(AutosaveClock.DefaultDays));
        }

        [Test]
        public void ResettingATabAtItsDefaultsRaisesNothing()
        {
            var settings = new SettingsDirector();
            int raised = 0;
            settings.OptionChanged += _ => raised++;
            settings.LadderChanged += _ => raised++;
            settings.ResetTab(SettingsTab.Graphics);
            Assert.That(raised, Is.EqualTo(0), "a reset announced levers that did not move");
        }

        // ------------------------------------------------------------------ Backspace

        [Test]
        public void BackspaceEmptiesTheListeningSlotAndStopsListening()
        {
            var hotkeys = new HotkeyDirector();
            Assert.That(hotkeys.Key(HotkeyAction.CameraForward, 1), Is.Not.EqualTo(HudKey.None),
                "the test needs a second slot that ships bound");

            hotkeys.Listen(HotkeyAction.CameraForward, 1);
            hotkeys.ClearListening();

            Assert.That(hotkeys.Key(HotkeyAction.CameraForward, 1), Is.EqualTo(HudKey.None));
            Assert.That(hotkeys.Key(HotkeyAction.CameraForward, 0), Is.Not.EqualTo(HudKey.None),
                "clearing one slot emptied the other");
            Assert.That(hotkeys.Listening, Is.Null, "the slot is still waiting after it was cleared");
        }

        [Test]
        public void BackspaceWithNothingListeningDoesNothing()
        {
            var hotkeys = new HotkeyDirector();
            HudKey before = hotkeys.Key(HotkeyAction.Pause, 0);
            hotkeys.ClearListening();
            Assert.That(hotkeys.Key(HotkeyAction.Pause, 0), Is.EqualTo(before));
        }
    }
}
