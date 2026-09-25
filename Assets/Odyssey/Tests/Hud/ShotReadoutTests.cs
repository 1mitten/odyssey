#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The hover readout's words (design 50 §8b): the chance first, then what moved it, cover only
    /// when there is some, the descent only when it cost something, and every character one the two
    /// shipped fonts can draw.
    /// </summary>
    public class ShotReadoutTests
    {
        static ShotReportView Report(int total, int cover = 0, int elevation = 1_000, int topCover = 0,
            bool inRange = true, bool inSight = true) =>
            new ShotReportView(new PawnId(1), new PawnId(2), 800, cover, total, 12_400, 10, ItemHandle.Pistol,
                elevation, topCover, cover > 0 ? 1 : 0, inRange, inSight);

        [Test]
        public void InTheOpenItIsTheChanceTheSkillTheDistanceAndTheGun()
        {
            string text = ShotReadout.Text(Report(630));
            Assert.That(text, Does.StartWith("63% " + Registry.Label(ShotReadout.ToHitKey)));
            Assert.That(text, Does.Contain(Registry.Label(ShotReadout.ShootingKey) + " 10"));
            Assert.That(text, Does.Contain("12 m"));
            Assert.That(text, Does.Contain(ItemLabels.Label(ItemHandle.Pistol).ToLowerInvariant()));
            Assert.That(text, Does.Not.Contain(Registry.Label(ShotReadout.CoverKey)), "no cover, no word for it");
        }

        [Test]
        public void BehindSandbagsItNamesThemAndFromAboveItSaysWhatTheDescentLeft()
        {
            string text = ShotReadout.Text(Report(360, cover: 550, elevation: 760, topCover: EdificeHandle.Sandbags));
            Assert.That(text, Does.Contain(Registry.Label(ShotReadout.CoverKey) + " -55%"));
            Assert.That(text, Does.Contain("(" + EdificeLabels.Label(EdificeHandle.Sandbags) + ")"));
            Assert.That(text, Does.Contain(Registry.Label(ShotReadout.AboveKey) + " x0.76"));
        }

        [Test]
        public void OutOfRangeOrSightItSaysSoAndNothingElse()
        {
            Assert.That(ShotReadout.Text(Report(0, inRange: false)), Is.EqualTo(Registry.Label(ShotReadout.OutOfRangeKey)));
            Assert.That(ShotReadout.Text(Report(0, inSight: false)), Is.EqualTo(Registry.Label(ShotReadout.NoSightKey)));
        }

        [Test]
        public void EveryCharacterIsAscii()
        {
            string text = ShotReadout.Text(Report(360, cover: 550, elevation: 760, topCover: -1));
            foreach (char c in text) Assert.That(c < 128, Is.True, $"'{c}' may not be in the shipped fonts");
        }
    }
}
