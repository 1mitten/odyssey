#nullable enable
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The title screen's dock (design 40), as far as it can be answered without a panel: the
    /// buttons share the Settings rail's sources, the logo's offset follows the screen, and every
    /// word is ASCII. That the dock is 560 wide and flush left on a real screen is the PlayMode
    /// tier's (<c>StartScreenTests.TheTitleScreenIsADockFlushLeft</c>).
    /// </summary>
    public class TitleLayoutTests
    {
        [Test]
        public void TheFourButtonsAreTheMainScreensFourRowsInOrder()
        {
            var rows = SessionCommands.For(SessionContext.MainScreen);
            Assert.That(TitleLayout.Buttons.Length, Is.EqualTo(rows.Count));
            for (int i = 0; i < rows.Count; i++)
                Assert.That(TitleLayout.Buttons[i].Key, Is.EqualTo(rows[i].Key), $"button {i} is not the table's row {i}");
        }

        [Test]
        public void LoadSettingsAndExitDrawTheSettingsWindowsOwnIconsAndInks()
        {
            TitleLayout.Button load = TitleLayout.Buttons[1], settings = TitleLayout.Buttons[2], exit = TitleLayout.Buttons[3];

            Assert.That(load.Icon, Is.SameAs(SettingsLayout.ActionIcon(SessionCommands.LoadKey)));
            Assert.That(exit.Icon, Is.SameAs(SettingsLayout.ActionIcon(SessionCommands.QuitKey)));
            Assert.That(settings.Icon, Is.SameAs(SettingsLayout.GearIcon));

            Assert.That(load.Ink, Is.EqualTo(SettingsLayout.Ink(SettingsLayout.ToneOf(SessionCommands.LoadKey))));
            Assert.That(exit.Ink, Is.EqualTo(SettingsLayout.Ink(SettingsLayout.ToneOf(SessionCommands.QuitKey))));
            Assert.That(settings.Ink, Is.EqualTo(SettingsLayout.Hue(SettingsTab.Graphics)),
                "the Settings button and the window's Graphics hue are one violet");
            Assert.That(exit.NameTakesInk, Is.True, "Exit game's name is red at rest");
        }

        [Test]
        public void EveryButtonIconParsesInsideItsBox()
        {
            foreach (TitleLayout.Button button in TitleLayout.Buttons)
                foreach (SvgPath.Subpath path in SvgPath.Parse(button.Icon))
                    foreach (float v in path.Points)
                        Assert.That(v, Is.InRange(0f, SettingsLayout.IconBox), $"{button.Key}'s icon leaves its box");
        }

        [Test]
        public void TheLogoStartsTwoHundredDownAtTenEightyAndNeverAboveNinetySix()
        {
            Assert.That(TitleLayout.LogoTop(1080), Is.EqualTo(200));
            Assert.That(TitleLayout.LogoTop(1440), Is.EqualTo(266));
            Assert.That(TitleLayout.LogoTop(400), Is.EqualTo(TitleLayout.LogoTopMin));
        }

        [Test]
        public void TheContentWidthIsTheDockLessItsPadding()
        {
            Assert.That(TitleLayout.ContentWidth, Is.EqualTo(464));
        }

        [Test]
        public void TheMarkSitsInItsBox()
        {
            foreach (TitleLayout.Slab slab in TitleLayout.Mark)
            {
                Assert.That(slab.X + slab.Width, Is.LessThanOrEqualTo(TitleLayout.MarkBoxWidth));
                Assert.That(slab.Y + slab.Height, Is.LessThanOrEqualTo(TitleLayout.MarkBoxHeight));
            }
            Assert.That(TitleLayout.Mark[2].Colour, Is.EqualTo(HudTheme.Accent), "the middle slab is the lit one");
            Assert.That(TitleLayout.MarkWidth / (float)TitleLayout.MarkBoxWidth,
                Is.EqualTo(TitleLayout.MarkHeight / (float)TitleLayout.MarkBoxHeight).Within(1e-4f),
                "the mark is drawn at its own aspect");
        }

        [Test]
        public void EveryWordTheTitleScreenWritesIsAscii()
        {
            foreach (TitleLayout.Button button in TitleLayout.Buttons)
            {
                foreach (char c in button.Description + Registry.Label(button.Key))
                    Assert.That(c, Is.LessThan((char)128), $"{button.Key} writes a non-ASCII character");
            }
            Assert.That(TitleLayout.VersionLine("0.0.1"), Is.EqualTo("V0.0.1"));
        }
    }
}
