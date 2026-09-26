#nullable enable
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The wake-up preference (design 56 §9): on by default, remembered, reset with the Interface
    /// tab, and named in the registry. What either passage looks like is a playtest; that the
    /// choice is kept is not.
    /// </summary>
    public class WakeUpSettingTests
    {
        [Test]
        public void AFreshProfileWakesUp()
        {
            var settings = new SettingsDirector();
            settings.UseStore(new FakeSettingsStore());
            Assert.That(settings.WakeUp, Is.True);
        }

        [Test]
        public void TurningItOffIsRememberedAcrossAReload()
        {
            var store = new FakeSettingsStore();
            var first = new SettingsDirector();
            first.UseStore(store);
            first.SetWakeUp(false);

            var second = new SettingsDirector();
            second.UseStore(store);
            Assert.That(second.WakeUp, Is.False);
        }

        [Test]
        public void ChangingItRaisesOnceAndSettingItAgainRaisesNothing()
        {
            var settings = new SettingsDirector();
            int raised = 0;
            settings.WakeUpChanged += _ => raised++;
            settings.SetWakeUp(false);
            settings.SetWakeUp(false);
            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void ResettingTheInterfaceTabTurnsItBackOn()
        {
            var settings = new SettingsDirector();
            settings.SetWakeUp(false);
            settings.ResetTab(SettingsTab.Interface);
            Assert.That(settings.WakeUp, Is.True);
        }

        [Test]
        public void TheRowIsNamedInTheRegistry()
        {
            Assert.That(SettingsDirector.IconKeys, Does.Contain(SettingsDirector.WakeUpKey));
            Assert.That(Registry.Label(SettingsDirector.WakeUpKey), Is.EqualTo("Wake-up"));
        }

        [Test]
        public void ASuspendedKeyboardIsNotThePlayers()
        {
            var hotkeys = new HotkeyDirector();
            Assert.That(hotkeys.GameKeysLive, Is.True);
            hotkeys.Suspended = true;
            Assert.That(hotkeys.GameKeysLive, Is.False, "the wake took the keys and the camera still moved");

            // A text field coming and going must not hand the keys back while the wake holds them.
            var field = new object();
            hotkeys.BeginTyping(field);
            hotkeys.EndTyping(field);
            Assert.That(hotkeys.GameKeysLive, Is.False);

            hotkeys.Suspended = false;
            Assert.That(hotkeys.GameKeysLive, Is.True);
        }
    }
}
