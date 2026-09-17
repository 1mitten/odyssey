#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// Clicking a blueprint tells you what is going up, whether it has its material, and how much
    /// longer.
    ///
    /// <para>All three were missing: a click on a site said "Ground · cell", exactly as a click on
    /// bare grass did, so the one thing on the board that is <i>about</i> a plan had nothing to say
    /// about it (owner, 2026-09-17).</para>
    /// </summary>
    public class InspectSiteTests
    {
        static readonly GridSize Size = new GridSize(8, 8, 4);

        static readonly CellRef At = new CellRef(3, 4, 1);

        static WorldSnapshot Frame(ushort delivered, ushort cost, int workDone, int workTotal,
            byte stuff = StuffHandle.Wood)
        {
            var frame = new WorldSnapshot();
            frame.BeginWrite(tick: 0, Size, sliceLayer: 1);
            frame.AddSite(new SiteView(
                Size.Index(At), BuildingHandle.Wall, stuff, delivered, cost, workDone, workTotal));
            return frame;
        }

        static InspectModel Looking(WorldSnapshot frame)
        {
            var model = new InspectModel();
            model.SetCell(At);
            model.Refresh(frame);
            return model;
        }

        [Test]
        public void ASiteWaitingForMaterialSaysWhatItIsWaitingFor()
        {
            InspectModel model = Looking(Frame(delivered: 2, cost: 5, workDone: 0, workTotal: 135));

            Assert.That(model.Title, Is.EqualTo("Wall"));
            Assert.That(model.Subtitle, Is.EqualTo("planned · wood"));
            Assert.That(model.Site, Is.EqualTo("2 of 5 wood delivered"),
                "a site with no wood is not slow, it is stuck, so the material leads");
            Assert.That(model.SiteDelivered, Is.EqualTo(2));
            Assert.That(model.SiteCost, Is.EqualTo(5));
        }

        [Test]
        public void AFedSiteSaysHowMuchLongerInsteadOfWhatItHolds()
        {
            // 135 - 15 = 120 ticks, which is two seconds at the composition root's 60 a second.
            InspectModel model = Looking(Frame(delivered: 5, cost: 5, workDone: 15, workTotal: 135));

            Assert.That(model.Site, Is.EqualTo("about 2s left"));
            Assert.That(model.SiteProgress, Is.EqualTo(15f / 135f).Within(0.0001f));
        }

        [Test]
        public void ALongBuildIsReadInMinutes()
        {
            InspectModel model = Looking(Frame(delivered: 5, cost: 5, workDone: 0, workTotal: 9_000));

            Assert.That(model.Site, Is.EqualTo("about 2m 30s left"));
        }

        [Test]
        public void TheMaterialIsNamedInTheSubtitle()
        {
            InspectModel model = Looking(
                Frame(delivered: 0, cost: 5, workDone: 0, workTotal: 229, stuff: StuffHandle.Stone));

            Assert.That(model.Subtitle, Is.EqualTo("planned · stone"));
            Assert.That(model.Site, Is.EqualTo("0 of 5 stone delivered"));
        }

        /// <summary>
        /// The negative control. Without it every assertion above could be satisfied by a model
        /// that says "Wall" about anything it is pointed at.
        /// </summary>
        [Test]
        public void BareGroundStillSaysNothingAboutBuilding()
        {
            var frame = new WorldSnapshot();
            frame.BeginWrite(tick: 0, Size, sliceLayer: 1);

            var model = new InspectModel();
            model.SetCell(At);
            model.Refresh(frame);

            Assert.That(model.Title, Is.EqualTo("Ground"));
            Assert.That(model.Subtitle, Is.EqualTo("cell"));
            Assert.That(model.Site, Is.Empty);
        }

        /// <summary>
        /// A site somewhere else is not this cell's site. The lookup is by cell index and this is
        /// what says so — a loop that took the first entry would pass every test above.
        /// </summary>
        [Test]
        public void ASiteOnAnotherCellIsNotReported()
        {
            var frame = new WorldSnapshot();
            frame.BeginWrite(tick: 0, Size, sliceLayer: 1);
            frame.AddSite(new SiteView(
                Size.Index(new CellRef(6, 6, 1)), BuildingHandle.Wall, StuffHandle.Wood, 1, 5, 0, 135));

            var model = new InspectModel();
            model.SetCell(At);
            model.Refresh(frame);

            Assert.That(model.Title, Is.EqualTo("Ground"));
            Assert.That(model.Site, Is.Empty);
        }

        /// <summary>
        /// The line is the same instance until it would read differently, which is what lets the
        /// view compare by reference and what keeps the pane from allocating fifteen times a
        /// second (ADR 0003, flip condition F1).
        /// </summary>
        [Test]
        public void TheLineIsNotRebuiltWhileItWouldReadTheSame()
        {
            var model = new InspectModel();
            model.SetCell(At);

            model.Refresh(Frame(delivered: 5, cost: 5, workDone: 0, workTotal: 9_000));
            string first = model.Site;

            // One tick of work later: the same whole second, so the same sentence.
            model.Refresh(Frame(delivered: 5, cost: 5, workDone: 1, workTotal: 9_000));
            Assert.That(ReferenceEquals(model.Site, first), Is.True,
                "the pane rebuilt a string that reads identically");

            // A whole second later it must move, or the countdown is frozen.
            model.Refresh(Frame(delivered: 5, cost: 5, workDone: 60, workTotal: 9_000));
            Assert.That(model.Site, Is.Not.EqualTo(first));
        }
    }
}
