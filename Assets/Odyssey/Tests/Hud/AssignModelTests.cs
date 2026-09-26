#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The Assign tab's model (design 43 §6): every colonist in the roster's order, her area and
    /// her response, twelve to a page, and a press on a cell sending the next value for her alone.
    /// </summary>
    public class AssignModelTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 4);

        /// <summary>
        /// <paramref name="colonists"/> colonists with ids 1 up, a hog (kind 1) and a bandit (kind
        /// 3); colonist 2 kept home and told to flee.
        /// </summary>
        static WorldSnapshot Board(int colonists, int hearth = 5)
        {
            var frame = new WorldSnapshot();
            frame.BeginWrite(0, Size, 1);
            for (int i = 1; i <= colonists; i++)
                frame.AddPawn(new PawnView(new PawnId(i), new CellRef(i % 40, 3, 1), 800, 800, 800));
            frame.AddPawn(new PawnView(new PawnId(100), new CellRef(20, 20, 1), 800, 800, 800, kind: 1));
            frame.AddPawn(new PawnView(new PawnId(101), new CellRef(21, 20, 1), 800, 800, 800, kind: 3));
            frame.AddPawnAspect(new PawnAspect(new PawnId(2), AreaAspectNames.AreaKey, AssignModel.Home));
            frame.AddPawnAspect(new PawnAspect(new PawnId(2), CombatAspectNames.ResponseKey, ResponseModel.Flee));
            frame.SetHearthCell(hearth);
            return frame;
        }

        static List<PawnId> Order(params int[] ids)
        {
            var order = new List<PawnId>();
            foreach (int id in ids) order.Add(new PawnId(id));
            return order;
        }

        static List<int> Ids(AssignModel model)
        {
            var ids = new List<int>();
            foreach (AssignRow row in model.Rows) ids.Add(row.Id.Value);
            return ids;
        }

        /// <summary>The two sides number the areas alike: <c>PawnArea.Anywhere</c> 0 and <c>Home</c> 1.</summary>
        [Test]
        public void TheAreasAreTheSimulationsNumbers()
        {
            Assert.That(AssignModel.Anywhere, Is.EqualTo(0));
            Assert.That(AssignModel.Home, Is.EqualTo(1));
            Assert.That(AreaAspectNames.Area, Is.EqualTo("odyssey.pawn.area"));
        }

        [Test]
        public void TheRowsAreTheColonistsInTheRostersOrder()
        {
            var model = new AssignModel();
            // The roster's order, with an animal and a bandit slipped in: neither is a row.
            model.Refresh(Board(3), Order(3, 100, 1, 101, 2), selected: null);
            Assert.That(Ids(model), Is.EqualTo(new[] { 3, 1, 2 }), "not the roster's order, or a non-colonist got a row");
            Assert.That(model.TotalCount, Is.EqualTo(3));
        }

        [Test]
        public void EachRowCarriesHerAreaAndResponseAndWhichAreCautious()
        {
            var model = new AssignModel();
            model.Refresh(Board(3), Order(1, 2, 3), selected: Order(3));

            AssignRow ada = model.Rows[0], kept = model.Rows[1], picked = model.Rows[2];
            Assert.That(ada.Area, Is.EqualTo(AssignModel.Anywhere), "no aspect is Anywhere");
            Assert.That(ada.AreaKey, Is.EqualTo(AssignDirector.AnywhereKey));
            Assert.That(ada.ResponseKey, Is.EqualTo(ResponseModel.FightBackKey));
            Assert.That(ada.AreaCautious || ada.ResponseCautious, Is.False);

            Assert.That(kept.AreaKey, Is.EqualTo(AssignDirector.HomeKey));
            Assert.That(kept.ResponseKey, Is.EqualTo(ResponseModel.FleeKey));
            Assert.That(kept.AreaCautious, Is.True, "Home is drawn in the warning ink");
            Assert.That(kept.ResponseCautious, Is.True, "Flee is drawn in the warning ink");

            Assert.That(picked.Selected, Is.True);
            Assert.That(ada.Selected, Is.False);
            Assert.That(ada.Name, Is.Not.Empty);
        }

        [Test]
        public void APressSendsTheNextValueForThatColonistAlone()
        {
            WorldSnapshot frame = Board(3);

            Assert.That(AssignModel.TryCycleArea(frame, new PawnId(1), out Intent area), Is.True);
            Assert.That(area.Kind, Is.EqualTo(IntentKind.SetPawnArea));
            Assert.That(area.A, Is.EqualTo(1));
            Assert.That(area.B, Is.EqualTo(AssignModel.Home), "Anywhere goes to Home");

            Assert.That(AssignModel.TryCycleArea(frame, new PawnId(2), out Intent back), Is.True);
            Assert.That(back.B, Is.EqualTo(AssignModel.Anywhere), "Home goes round to Anywhere");

            Assert.That(AssignModel.TryCycleResponse(frame, new PawnId(1), out Intent defend), Is.True);
            Assert.That(defend.Kind, Is.EqualTo(IntentKind.SetHostilityResponse));
            Assert.That(defend.B, Is.EqualTo(ResponseModel.Defend));
            Assert.That(AssignModel.TryCycleResponse(frame, new PawnId(2), out Intent round), Is.True);
            Assert.That(round.B, Is.EqualTo(ResponseModel.FightBack), "Flee goes round to Fight back");

            // Control: an animal and a bandit are not set from here.
            Assert.That(AssignModel.TryCycleArea(frame, new PawnId(100), out _), Is.False);
            Assert.That(AssignModel.TryCycleResponse(frame, new PawnId(101), out _), Is.False);
        }

        [Test]
        public void TwelveAPageAndNoPagerAtTwelve()
        {
            var model = new AssignModel();
            model.Refresh(Board(12), Order(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12), selected: null);
            Assert.That(model.Rows, Has.Count.EqualTo(12));
            Assert.That(model.Paged, Is.False, "twelve fit one page");

            var ids = new List<int>();
            for (int i = 1; i <= 14; i++) ids.Add(i);
            model.Refresh(Board(14), Order(ids.ToArray()), selected: null);
            Assert.That(model.Paged, Is.True);
            Assert.That(model.PageCount, Is.EqualTo(2));
            Assert.That(model.Rows, Has.Count.EqualTo(12));

            model.SetPage(1);
            model.Refresh(Board(14), Order(ids.ToArray()), selected: null);
            Assert.That(Ids(model), Is.EqualTo(new[] { 13, 14 }));

            // The colony shrinks under the second page: back to the one that exists.
            model.Refresh(Board(12), Order(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12), selected: null);
            Assert.That(model.Page, Is.Zero);
            Assert.That(model.EnsurePageFor(new PawnId(99)), Is.False);
        }

        [Test]
        public void NoHearthIsSaidBesideTheArea()
        {
            var model = new AssignModel();
            model.Refresh(Board(2, hearth: -1), Order(1, 2), selected: null);
            Assert.That(model.NoHearth, Is.True);
            model.Refresh(Board(2, hearth: 5), Order(1, 2), selected: null);
            Assert.That(model.NoHearth, Is.False, "the control: a hearth, and no note");
        }

        /// <summary>The spec's arithmetic: 192 + 9 + 150 + 9 + 150 is the window's 536 less its chrome.</summary>
        [Test]
        public void TheColumnsFillTheWindowLessItsChrome()
        {
            Assert.That(AssignLayout.GridWidth, Is.EqualTo(510));
            Assert.That(AssignLayout.ExpectedGridWidth, Is.EqualTo(AssignLayout.GridWidth),
                "a UI Toolkit width is a border box; the columns must add up to the window less its chrome");
            Assert.That(AssignLayout.TabWidth, Is.EqualTo(536));
            Assert.That(AssignLayout.PanelHeight(12, paged: true), Is.GreaterThan(AssignLayout.PanelHeight(12, paged: false)));
            Assert.That(AssignLayout.PanelHeight(3, false), Is.LessThan(AssignLayout.PanelHeight(12, false)),
                "the height follows the page");
        }

        [Test]
        public void EscapeClosesTheAssignTabAtTheDockedTabsRung()
        {
            var settings = new SettingsDirector();
            Assert.That(settings.Escape(false, false, false, false, false, false, false, false, false,
                assignOpen: true, null), Is.EqualTo(EscapeAction.CloseAssign));
            Assert.That(settings.Escape(false, true, false, false, false, false, false, false, false,
                assignOpen: true, null), Is.EqualTo(EscapeAction.DisarmTool), "a tool in the hand comes off first");
            Assert.That(settings.Escape(true, false, false, false, false, false, false, false, false,
                assignOpen: true, null), Is.EqualTo(EscapeAction.CloseContextMenu));
            Assert.That(settings.Escape(false, false, false, false, false, false, false, false, false,
                assignOpen: false, null), Is.EqualTo(EscapeAction.OpenPanel), "the control: nothing open");
        }

        /// <summary>A Gear tab popover goes before anything but a right-click's menu (design 47 §3).</summary>
        [Test]
        public void EscapeClosesAGearPopoverBeforeThePaneOrATool()
        {
            var settings = new SettingsDirector();
            Assert.That(settings.Escape(false, true, false, true, false, false, false, false, false, false, false,
                assignOpen: false, null), Is.EqualTo(EscapeAction.CloseGearPopover), "before the tool");
            Assert.That(settings.Escape(false, true, true, false, false, false, false, false, false, false, false,
                assignOpen: false, null), Is.EqualTo(EscapeAction.CloseContextMenu), "the right-click's menu first");
            Assert.That(settings.Escape(true, true, true, false, false, false, false, false, false, false, false,
                assignOpen: false, null), Is.EqualTo(EscapeAction.LeaveRide), "a ride above the popover");
            Assert.That(settings.Escape(false, false, false, true, false, false, false, false, false, false, false,
                assignOpen: false, null), Is.EqualTo(EscapeAction.DisarmTool), "the control: no popover");
        }

        [Test]
        public void TheBarsAssignItemIsLiveOnF4()
        {
            HudCommand? assign = null;
            foreach (HudCommand command in HudCommands.All)
                if (command.Key == HudCommands.AssignKey) assign = command;
            Assert.That(assign, Is.Not.Null, "no Assign item on the bar");
            Assert.That(assign!.Value.Live, Is.True);
            Assert.That(assign.Value.Hotkey, Is.EqualTo("F4"));
            Assert.That(new HotkeyDirector().Key(HotkeyAction.AssignTab, 0), Is.EqualTo(HudKey.F4));
            Assert.That(HudCommands.IconOf(HudCommands.AssignKey), Is.EqualTo("ui.tab.colonists"),
                "the Colonists slot's own art");
            Assert.That(HudCommands.IconOf(HudCommands.WorkKey), Is.EqualTo(HudCommands.WorkKey));
        }

        [Test]
        public void EveryAssignKeyIsARegisteredName()
        {
            foreach (string key in AssignDirector.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
            Assert.That(Registry.Labels, Does.ContainKey(HotkeyDirector.KeyOf(HotkeyAction.AssignTab)));
        }
    }
}
