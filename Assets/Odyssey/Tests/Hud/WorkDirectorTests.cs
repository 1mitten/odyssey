#nullable enable
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The Work tab's session state, and the two places the rest of the interface has to agree
    /// with it: the command bar's item and the key that opens it.
    ///
    /// <para>These are the half of "is the tab functional" that does not need Unity. Whether the
    /// panel <i>draws</i> is `HudSmokeTests`' business, in the tier that compiles Presentation.</para>
    /// </summary>
    public class WorkDirectorTests
    {
        [Test]
        public void ItOpensClosedAndOnTheSimpleReading()
        {
            var work = new WorkDirector();

            Assert.That(work.Open, Is.False);
            Assert.That(work.Mode, Is.EqualTo(WorkGridMode.Simple),
                "A tick and a cross are what anyone can read at a glance; four ranks of urgency " +
                "are what you go looking for once you want them (owner, 2026-09-20).");
        }

        [Test]
        public void ToggleRaisesChangedOnceEachWay()
        {
            var work = new WorkDirector();
            int changed = 0;
            work.Changed += () => changed++;

            work.Toggle();
            Assert.That(work.Open, Is.True);
            work.Toggle();
            Assert.That(work.Open, Is.False);
            Assert.That(changed, Is.EqualTo(2));

            // Setting the state it already has is silent, as every other director here is.
            work.SetOpen(false);
            Assert.That(changed, Is.EqualTo(2));
        }

        [Test]
        public void TheModeIsItsOwnEventSoOpeningDoesNotRedrawTheLegend()
        {
            var work = new WorkDirector();
            int modes = 0;
            work.ModeChanged += _ => modes++;

            work.Toggle();
            Assert.That(modes, Is.Zero, "Opening is not a mode change.");

            work.SetMode(WorkGridMode.Detailed);
            Assert.That(work.Mode, Is.EqualTo(WorkGridMode.Detailed));
            Assert.That(modes, Is.EqualTo(1));

            work.SetMode(WorkGridMode.Detailed);
            Assert.That(modes, Is.EqualTo(1), "setting what is already set says nothing");
        }

        // ------------------------------------------------------------------ the bar and the key

        [Test]
        public void TheWorkItemOnTheCommandBarIsLiveNow()
        {
            HudCommand work = default;
            bool found = false;
            foreach (HudCommand command in HudCommands.All)
            {
                if (command.Key != HudCommands.WorkKey) continue;
                work = command;
                found = true;
            }

            Assert.That(found, Is.True, "the bar must still carry a Work item");
            Assert.That(work.Live, Is.True,
                "Work carried the reason \"the work grid arrives with M7\" until design 27. A " +
                "reason draws the item cmd--off and OnCommand ignores it, so the panel would be " +
                "unreachable from the bar however well it was built.");
            Assert.That(work.Hotkey, Is.EqualTo("F1"));
        }

        [Test]
        public void F1OpensTheWorkTabAndIsBindableAtAll()
        {
            var hotkeys = new HotkeyDirector();

            Assert.That(hotkeys.Key(HotkeyAction.WorkTab, 0), Is.EqualTo(HudKey.F1),
                "the cap the command bar has advertised since it was built");
            Assert.That(hotkeys.Key(HotkeyAction.WorkTab, 1), Is.EqualTo(HudKey.None),
                "a panel key needs no alternate; the two-slot map leaves it empty");
        }

        [Test]
        public void TheKeyNamesItselfOnACapAndInTheRegistry()
        {
            Assert.That(HotkeyDirector.Display(HudKey.F1), Is.EqualTo("F1"),
                "Display falls through to the enum's own name, which is already right here.");
            Assert.That(Registry.Label(HotkeyDirector.KeyOf(HotkeyAction.WorkTab)),
                Is.EqualTo("Work tab"),
                "the rebind row is named from the CSV like every other row in that panel");
        }

        [Test]
        public void ThePanelAndTheBarItemAreOneName()
        {
            // Two keys for one panel is how a wiki and a screen come to disagree. They are the
            // same string on purpose.
            Assert.That(WorkDirector.PanelKey, Is.EqualTo(HudCommands.WorkKey));
            Assert.That(Registry.Label(WorkDirector.PanelKey), Is.EqualTo("Work"));
        }
    }
}
