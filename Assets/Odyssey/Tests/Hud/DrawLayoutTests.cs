#nullable enable
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The machine's geometry (design 41 §6.5): every row fits the grid it wraps, a window fits
    /// its row, and nothing in the spec's arithmetic has been lost fitting it to this page.
    /// </summary>
    public class DrawLayoutTests
    {
        [Test]
        public void AGambleSkillRowFitsItsNameItsWindowAndItsFlame()
        {
            int name = HudLayout.SetupSkillRowWidth
                       - HudLayout.SkillIconWidth - HudLayout.SkillIconGap - HudLayout.DrawSkillTail;
            Assert.That(name, Is.GreaterThanOrEqualTo(150),
                "the name column in a Gamble row has been squeezed below \"Construction\" at the setup step");
        }

        [Test]
        public void AWindowFitsItsRowWithAirAboveAndBelow()
        {
            Assert.That(HudLayout.DrawSkillRow, Is.GreaterThan(HudLayout.ReelWindowHeight));
            Assert.That(HudLayout.DrawSkillRow, Is.GreaterThanOrEqualTo(HudLayout.SetupSkillRow),
                "Gamble's row is shorter than Standard's, so the grid would jump when the mode changes");
            Assert.That(HudLayout.FlameHeight, Is.LessThanOrEqualTo(HudLayout.ReelWindowHeight));
        }

        [Test]
        public void TheTraitRowAndItsButtonFitTheMachine()
        {
            Assert.That(HudLayout.TraitRowWidth, Is.LessThanOrEqualTo(HudLayout.MachineInnerWidth),
                "three trait windows and the action button overrun the grid the machine wraps");
        }

        [Test]
        public void TheIdentityRowFitsTheMachine()
        {
            int row = HudLayout.PortraitWindow + HudLayout.IdentityGap + HudLayout.NameWindowWidth
                      + HudLayout.IdentityGap + HudLayout.ReelWindowWidth + HudLayout.IdentityGap
                      + 120; // the widest verdict tag, "JACKPOT" at 14/700 tracked, with its padding
            Assert.That(row, Is.LessThanOrEqualTo(HudLayout.MachineInnerWidth));
            Assert.That(HudLayout.PortraitWindow, Is.LessThanOrEqualTo(HudLayout.IdentityHeight));
        }

        [Test]
        public void TheGhostsSitOutsideTheWindowOnlyPartly()
        {
            // A ghost a full window away is never seen; one closer than half a window sits on top of
            // the figure. The spec's 21 in a 32 window, and 24 in 36, are in between.
            Assert.That(HudLayout.ReelPitch, Is.InRange(HudLayout.ReelWindowHeight / 2 + 1, HudLayout.ReelWindowHeight - 1));
            Assert.That(HudLayout.TraitPitch, Is.InRange(HudLayout.TraitWindowHeight / 2 + 1, HudLayout.TraitWindowHeight - 1));
            Assert.That(HudLayout.NamePitch, Is.InRange(HudLayout.IdentityHeight / 2 - 4, HudLayout.IdentityHeight - 1));
        }

        [Test]
        public void TheBulbsFitTheirRail()
        {
            int rail = HudLayout.MachineInnerWidth + 2 * HudLayout.MachinePadX - 2 * HudLayout.BulbInsetX;
            Assert.That(HudLayout.BulbsPerRail * HudLayout.Bulb, Is.LessThan(rail), "the bulbs touch");
            Assert.That(HudLayout.BulbInsetY + HudLayout.Bulb, Is.LessThan(HudLayout.MachinePadY),
                "a rail runs into the first row it frames");
        }
    }
}
