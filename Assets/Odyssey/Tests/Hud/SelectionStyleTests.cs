#nullable enable
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The selection style preference (<c>docs/design/44-selection-highlight.md</c>): the highlight
    /// by default, the brackets kept as the other rung, remembered, and reset with the Interface tab.
    /// What either looks like is a playtest; that the choice is kept is not.
    /// </summary>
    public class SelectionStyleTests
    {
        [Test]
        public void AFreshProfileGetsTheHighlight()
        {
            var settings = new SettingsDirector();
            settings.UseStore(new FakeSettingsStore());
            Assert.That(settings.SelectionStyle, Is.EqualTo(SelectionStyle.Highlight));
        }

        [Test]
        public void TheBracketsComeBackAfterAReload()
        {
            var store = new FakeSettingsStore();
            var first = new SettingsDirector();
            first.UseStore(store);
            first.SetSelectionStyle(SelectionStyle.Brackets);

            var second = new SettingsDirector();
            second.UseStore(store);
            Assert.That(second.SelectionStyle, Is.EqualTo(SelectionStyle.Brackets));
        }

        [Test]
        public void ANonsenseStoredStyleIsIgnored()
        {
            var store = new FakeSettingsStore();
            store.Preset(SettingsDirector.SelectionStyleKey, 41);
            var settings = new SettingsDirector();
            settings.UseStore(store);
            Assert.That(settings.SelectionStyle, Is.EqualTo(SettingsDirector.DefaultSelectionStyle));
        }

        [Test]
        public void ChangingItRaisesOnceAndSettingItAgainRaisesNothing()
        {
            var settings = new SettingsDirector();
            int raised = 0;
            settings.SelectionStyleChanged += _ => raised++;

            settings.SetSelectionStyle(SelectionStyle.Brackets);
            settings.SetSelectionStyle(SelectionStyle.Brackets);
            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void ResettingTheInterfaceTabPutsTheHighlightBack()
        {
            var settings = new SettingsDirector();
            settings.SetSelectionStyle(SelectionStyle.Brackets);
            settings.ResetTab(SettingsTab.Interface);
            Assert.That(settings.SelectionStyle, Is.EqualTo(SelectionStyle.Highlight));
        }

        [Test]
        public void EveryRungIsAStyleAndBothAreOffered()
        {
            Assert.That(SettingsDirector.SelectionStyles, Is.EquivalentTo(new[]
            {
                SelectionStyle.Highlight, SelectionStyle.Brackets,
            }));
            foreach (SelectionStyle style in SettingsDirector.SelectionStyles)
                Assert.That(SettingsDirector.IsSelectionStyle((int)style), Is.True);
            Assert.That(SettingsDirector.IsSelectionStyle(2), Is.False);
            Assert.That(SettingsDirector.IsSelectionStyle(-1), Is.False);
        }

        [Test]
        public void TheRowAndItsHeadingAreNamedInTheRegistry()
        {
            Assert.That(SettingsDirector.IconKeys, Does.Contain(SettingsDirector.SelectionStyleKey));
            Assert.That(SettingsLayout.IconKeys, Does.Contain(SettingsLayout.SelectionGroupKey));
            Assert.That(Registry.Label(SettingsDirector.SelectionStyleKey), Is.EqualTo("Selection style"));
            Assert.That(Registry.Label(SettingsLayout.SelectionGroupKey), Is.EqualTo("Selection"));
        }
    }
}
