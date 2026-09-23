#nullable enable
using System.Linq;
using System.Text;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The interface half of power (design 32 §9, §10): when the hidden lines are shown, how a
    /// line is dragged, what the pane says about a power building, and the two alerts.
    /// </summary>
    public class PowerHudTests
    {
        static readonly GridSize Size = new GridSize(10, 10, 4);
        static readonly CellRef At = new CellRef(3, 4, 1);

        // ---- when the lines show (§9, decision 5) -------------------------------------------------

        [Test]
        public void TheLinesShowForPowerWorkAndNothingElse()
        {
            Assert.That(PowerLinesVisibility.Visible(DesignateTool.Build, BuildingHandle.Conduit, false, false), Is.True);
            Assert.That(PowerLinesVisibility.Visible(DesignateTool.Build, BuildingHandle.Generator, false, false), Is.True);
            Assert.That(PowerLinesVisibility.Visible(DesignateTool.Build, BuildingHandle.Heater, false, false), Is.True);
            Assert.That(PowerLinesVisibility.Visible(DesignateTool.RemoveConduit, BuildingHandle.Wall, false, false), Is.True);
            Assert.That(PowerLinesVisibility.Visible(DesignateTool.Deconstruct, BuildingHandle.Wall, false, false), Is.True,
                "a wall coming down may have a line in it");
            Assert.That(PowerLinesVisibility.Visible(DesignateTool.Cancel, BuildingHandle.Wall, false, false), Is.True);
            Assert.That(PowerLinesVisibility.Visible(DesignateTool.None, BuildingHandle.Wall, powerBuildingSelected: true, false), Is.True);
            Assert.That(PowerLinesVisibility.Visible(DesignateTool.None, BuildingHandle.Wall, false, overlayOn: true), Is.True);

            // The controls: every other way of holding the game hides them.
            Assert.That(PowerLinesVisibility.Visible(DesignateTool.None, BuildingHandle.Wall, false, false), Is.False);
            Assert.That(PowerLinesVisibility.Visible(DesignateTool.Build, BuildingHandle.Wall, false, false), Is.False,
                "a wall tool is not power work, whatever the last building armed was");
            Assert.That(PowerLinesVisibility.Visible(DesignateTool.Mine, BuildingHandle.Conduit, false, false), Is.False);
            Assert.That(PowerLinesVisibility.Visible(DesignateTool.Fell, BuildingHandle.Conduit, false, false), Is.False);
        }

        [Test]
        public void ThePowerOverlayIsASwitchThatSaysWhenItMoves()
        {
            var overlay = new OverlayDirector();
            int changes = 0;
            overlay.Changed += () => changes++;

            overlay.TogglePower();
            Assert.That(overlay.PowerVisible, Is.True);
            overlay.SetPower(true);
            Assert.That(changes, Is.EqualTo(1), "setting it to what it is changes nothing");
            overlay.TogglePower();
            Assert.That(overlay.PowerVisible, Is.False);
        }

        /// <summary>
        /// The views strip's Power switch is the overlay's own: pressing it keeps the lines shown
        /// whatever is armed, and pressing again hands them back to the tools (design 32 §14).
        /// </summary>
        [Test]
        public void ThePowerViewIsTheOverlaysSwitch()
        {
            var overlays = new OverlayDirector();
            Assert.That(HudViews.Keys, Does.Contain(HudViews.Power));
            Assert.That(HudViews.IsOn(overlays, HudViews.Power), Is.False);

            HudViews.Toggle(overlays, HudViews.Power);
            Assert.That(overlays.PowerVisible, Is.True);
            Assert.That(PowerLinesVisibility.Visible(DesignateTool.None, BuildingHandle.Wall, false, overlays.PowerVisible),
                Is.True, "on with nothing armed");

            HudViews.Toggle(overlays, HudViews.Power);
            Assert.That(PowerLinesVisibility.Visible(DesignateTool.Build, BuildingHandle.Conduit, false, overlays.PowerVisible),
                Is.True, "off, a power tool still shows them");
            Assert.That(PowerLinesVisibility.Visible(DesignateTool.None, BuildingHandle.Wall, false, overlays.PowerVisible),
                Is.False, "and putting the tool down hides them");
        }

        // ---- the drag (§10) -------------------------------------------------------------------------

        /// <summary>A line drag stays one row however far it wanders — and the control, a wall, widens.</summary>
        [TestCase(BuildingHandle.Conduit, false)]
        [TestCase(BuildingHandle.Wall, true)]
        public void ALineDragNeverWidensAndAWallDragStillDoes(int building, bool widens)
        {
            var designate = new DesignateDirector();
            designate.ArmBuild(building);
            designate.Begin(new CellRef(1, 1, 1));
            designate.DragTo(new CellRef(8, 6, 1));

            Assert.That(designate.Widened, Is.EqualTo(widens));
            Assert.That(designate.TryPreview(out CellRef min, out CellRef max), Is.True);
            Assert.That(max.Z - min.Z + 1 == 1 || max.X - min.X + 1 == 1, Is.EqualTo(!widens));
        }

        [Test]
        public void TheRemoveToolIsItsOwnOrderAndNamesItself()
        {
            var designate = new DesignateDirector();
            var palette = new BuildPaletteModel(designate);
            Assert.That(PaletteTools.TryGet(PaletteTools.Unwire, out PaletteTool unwire), Is.True);
            unwire.Arm(designate);

            Assert.That(designate.Tool, Is.EqualTo(DesignateTool.RemoveConduit));
            Assert.That(palette.ArmedOrder, Is.EqualTo(PaletteTools.Unwire), "the banner names the order");
            Assert.That(HudTheme.ArmedOrderHue(PaletteTools.Unwire), Is.EqualTo(OrderColours.Hue(DesignateTool.RemoveConduit)));
            Assert.That(HudTheme.PinnedActionHue(PaletteTools.Unwire), Is.Null, "it lights no button on the strip");
        }

        [Test]
        public void ThePowerRowHasItsLiveTools()
        {
            int power = System.Array.FindIndex(PaletteTools.Categories, c => c.key == "ui.arch.category.power");
            Assert.That(PaletteTools.LiveToolsIn(power), Is.EqualTo(4), "conduit, remove, generator, heater");
        }

        // ---- the words (§10) -------------------------------------------------------------------------

        [Test]
        public void WattsAreWrittenOneWay()
        {
            Assert.That(PowerLabels.Watts(175), Is.EqualTo("175 W"));
            Assert.That(PowerLabels.Watts(1_000), Is.EqualTo("1,000 W"));
            Assert.That(PowerLabels.Balance(new PowerNetView(5, 1_000, 350, PowerNetState.Live)), Is.EqualTo("350 W of 1,000 W"));
        }

        // ---- the pane (§10) ------------------------------------------------------------------------

        static WorldSnapshot Frame(PowerDeviceView device, PowerNetView? net = null, ConduitView? line = null)
        {
            var frame = new WorldSnapshot();
            frame.BeginWrite(tick: 0, Size, sliceLayer: 1);
            frame.AddCellDetail(new CellDetail(Size.Index(At), TerrainHandle.Grass, (byte)EdificeHandle.Heater,
                StuffHandle.None, 0, 1000, 0));
            frame.AddPowerDevice(device);
            if (net is PowerNetView n) frame.AddPowerNet(n);
            if (line is ConduitView l) frame.AddConduit(l);
            return frame;
        }

        static PowerDeviceView Heater(bool on = true, bool powered = true, int net = 7) =>
            new PowerDeviceView(Size.Index(At), -1, BuildingHandle.Heater, PowerRole.Consumer, on, powered, net,
                175, 0, 0, 0);

        static PowerDeviceView Generator(int fuelMilli, bool on = true, int load = 175, int net = 7) =>
            new PowerDeviceView(Size.Index(At), Size.Index(At) + 1, BuildingHandle.Generator, PowerRole.Generator,
                on, on && fuelMilli > 0, net, 1_000, load, fuelMilli, 75_000);

        static InspectModel Looking(WorldSnapshot frame)
        {
            var model = new InspectModel();
            model.SetCell(At);
            model.Refresh(frame);
            return model;
        }

        static string Rows(InspectModel model)
        {
            var text = new StringBuilder();
            foreach (InspectRow row in model.CellRows)
            {
                if (text.Length > 0) text.Append(" | ");
                text.Append(row.Name).Append('=').Append(row.Value);
            }
            return text.ToString();
        }

        [Test]
        public void APoweredHeaterSaysSoAndOffersItsSwitch()
        {
            InspectModel model = Looking(Frame(Heater(), new PowerNetView(7, 1_000, 175, PowerNetState.Live)));

            Assert.That(Rows(model), Does.Contain("power=175 W — powered"));
            Assert.That(Rows(model), Does.Contain("net=175 W of 1,000 W — live"));
            Assert.That(Rows(model), Does.Contain("switch=" + Registry.Label("ui.command.switchoff")));
            Assert.That(model.PowerSwitchUnderPane && model.PowerSwitchOn, Is.True);
        }

        [Test]
        public void AHeaterOnADarkNetSaysTheNetIsShort()
        {
            InspectModel model = Looking(Frame(Heater(powered: false), new PowerNetView(7, 1_000, 1_050, PowerNetState.Dark)));
            Assert.That(Rows(model), Does.Contain("power=175 W — the net is short"));
            Assert.That(Rows(model), Does.Contain("dark"));
        }

        [Test]
        public void AnUnconnectedHeaterSaysWhatToDo()
        {
            InspectModel model = Looking(Frame(Heater(powered: false, net: -1)));
            Assert.That(Rows(model), Does.Contain("not connected — lay a conduit beside it"));
            Assert.That(Rows(model), Does.Not.Contain("net="), "no net, no balance");
        }

        [Test]
        public void AGeneratorSaysWhatItCarriesAndHowMuchWoodItHas()
        {
            InspectModel model = Looking(Frame(Generator(30_500), new PowerNetView(7, 1_000, 175, PowerNetState.Live)));
            Assert.That(Rows(model), Does.Contain("power=carrying 175 W of 1,000 W"));
            Assert.That(Rows(model), Does.Contain("fuel=30 of 75 wood"), "whole units, rounded down");

            model = Looking(Frame(Generator(0), new PowerNetView(7, 0, 175, PowerNetState.Dark)));
            Assert.That(Rows(model), Does.Contain("power=out of fuel"));
        }

        [Test]
        public void TheSwitchStaysPressableAfterTheRowsHaveSettled()
        {
            WorldSnapshot frame = Frame(Heater(on: false), new PowerNetView(7, 1_000, 0, PowerNetState.Live));
            var model = new InspectModel();
            model.SetCell(At);
            model.Refresh(frame);
            model.Refresh(frame);   // the rows are unchanged, so the rebuild is skipped
            Assert.That(model.PowerSwitchUnderPane, Is.True, "the bed's fault, not repeated");
            Assert.That(model.PowerSwitchOn, Is.False);
            Assert.That(Rows(model), Does.Contain("switch=" + Registry.Label("ui.command.switchon")));
        }

        [Test]
        public void ALineInTheCellIsNamedOnlyWhenItIsDrawn()
        {
            var line = new ConduitView(Size.Index(At), ConduitKind.Built, PowerNetState.Live, 0, 7);
            InspectModel model = Looking(Frame(Heater(), new PowerNetView(7, 1_000, 175, PowerNetState.Live), line));
            Assert.That(Rows(model), Does.Contain("conduit=laid — live, 175 W of 1,000 W"));

            model = Looking(Frame(Heater(), new PowerNetView(7, 1_000, 175, PowerNetState.Live)));
            Assert.That(Rows(model), Does.Not.Contain("conduit="), "a hidden line is not announced");
        }

        /// <summary>A frame holding one line in the pane's cell and nothing else — an order over open ground.</summary>
        static WorldSnapshot LineFrame(ConduitKind kind)
        {
            var frame = new WorldSnapshot();
            frame.BeginWrite(tick: 0, Size, sliceLayer: 1);
            frame.AddCellDetail(new CellDetail(Size.Index(At), TerrainHandle.Air, EdificeHandle.None,
                StuffHandle.None, 0, 1000, 0));
            frame.AddConduit(new ConduitView(Size.Index(At), kind,
                kind == ConduitKind.Built ? PowerNetState.Idle : PowerNetState.Idle, 0, kind == ConduitKind.Ordered ? -1 : 9));
            return frame;
        }

        /// <summary>
        /// The owner's report (2026-09-23): an ordered line could not be selected again to cancel
        /// it. Selected, it is titled a conduit and offers its own Cancel — and a laid one offers
        /// Remove conduit, a marked one Keep it.
        /// </summary>
        [TestCase(ConduitKind.Ordered, IntentKind.CancelConduit)]
        [TestCase(ConduitKind.Built, IntentKind.RemoveConduit)]
        [TestCase(ConduitKind.Marked, IntentKind.CancelConduit)]
        public void ASelectedLineIsTitledAndOffersItsAction(ConduitKind kind, IntentKind action)
        {
            InspectModel model = Looking(LineFrame(kind));

            Assert.That(model.Title, Is.EqualTo(Registry.Label(PaletteTools.Conduit)));
            Assert.That(model.OrderActionUnderPane, Is.True);
            Assert.That(model.OrderAction, Is.EqualTo(action));
            Assert.That(Rows(model), Does.Contain(InspectModel.OrderActionRow + "="));
        }

        [Test]
        public void TheOrderActionStaysPressableAfterTheRowsHaveSettled()
        {
            WorldSnapshot frame = LineFrame(ConduitKind.Ordered);
            var model = new InspectModel();
            model.SetCell(At);
            model.Refresh(frame);
            model.Refresh(frame);
            Assert.That(model.OrderActionUnderPane, Is.True, "the bed's fault, not repeated a third time");

            var empty = new WorldSnapshot();
            empty.BeginWrite(tick: 0, Size, sliceLayer: 1);
            empty.AddCellDetail(new CellDetail(Size.Index(At), TerrainHandle.Grass, EdificeHandle.None,
                StuffHandle.None, 0, 1000, 0));
            model.Refresh(empty);
            Assert.That(model.OrderActionUnderPane, Is.False, "the control: with the line gone the row is dead");
        }

        /// <summary>
        /// A generator's order says what it is waiting for: its wood while that is the stuck half,
        /// then its scrap metal (design 32 §14).
        /// </summary>
        [TestCase(12, 0, "12 of 30 wood delivered")]
        [TestCase(30, 4, "4 of 20 scrap metal delivered")]
        public void AGeneratorsOrderSaysWhichMaterialItIsWaitingFor(int wood, int scrap, string says)
        {
            var frame = new WorldSnapshot();
            frame.BeginWrite(tick: 0, Size, sliceLayer: 1);
            frame.AddSite(new SiteView(Size.Index(At), (byte)BuildingHandle.Generator, (byte)StuffHandle.Wood,
                (ushort)wood, 30, 0, 600, 1, 2, (ushort)scrap, 20, (short)ItemHandle.Salvage));

            InspectModel model = Looking(frame);
            Assert.That(model.Site, Is.EqualTo(says));
        }

        /// <summary>
        /// The owner's second ask (2026-09-23): a Cancel in red, and the same on every building
        /// order's pane. A selected wall order carries a red Cancel that takes the building order
        /// alone; a line order's Cancel is the same red; taking a laid line up is the remove tool's
        /// amber.
        /// </summary>
        [Test]
        public void EveryOrdersPaneOffersARedCancel()
        {
            var frame = new WorldSnapshot();
            frame.BeginWrite(tick: 0, Size, sliceLayer: 1);
            frame.AddSite(new SiteView(Size.Index(At), (byte)BuildingHandle.Wall, (byte)StuffHandle.Wood, 0, 5, 0, 135));
            InspectModel wall = Looking(frame);

            Assert.That(wall.OrderActionUnderPane, Is.True, "a building order can be cancelled from its pane");
            Assert.That(wall.OrderAction, Is.EqualTo(IntentKind.CancelBuilding));
            Assert.That(wall.OrderActionA, Is.EqualTo(1), "the building order only, never a line beside it");
            InspectRow cancel = wall.CellRows.Single(r => r.Name == InspectModel.OrderActionRow);
            Assert.That(cancel.Value, Is.EqualTo(Registry.Label(PaletteTools.Cancel)));
            Assert.That(cancel.Tint, Is.EqualTo(OrderColours.Hue(DesignateTool.Cancel)), "red, the cancel tool's own");

            InspectModel line = Looking(LineFrame(ConduitKind.Ordered));
            Assert.That(line.CellRows.Single(r => r.Name == InspectModel.OrderActionRow).Tint,
                Is.EqualTo(OrderColours.Hue(DesignateTool.Cancel)));

            InspectModel laid = Looking(LineFrame(ConduitKind.Built));
            Assert.That(laid.CellRows.Single(r => r.Name == InspectModel.OrderActionRow).Tint,
                Is.EqualTo(OrderColours.Hue(DesignateTool.RemoveConduit)), "taking a line up is not a cancel");
        }

        // ---- the alerts (§10) -----------------------------------------------------------------------

        static WorldSnapshot AlertFrame(PowerNetView net, params PowerDeviceView[] devices)
        {
            var frame = new WorldSnapshot();
            frame.BeginWrite(tick: 0, Size, sliceLayer: 1);
            frame.AddPowerNet(net);
            foreach (var d in devices) frame.AddPowerDevice(d);
            return frame;
        }

        [Test]
        public void ADarkNetRaisesPowerFailureAndClearsWhenItRelights()
        {
            var alerts = new AlertModel();
            alerts.Refresh(AlertFrame(new PowerNetView(7, 1_000, 1_050, PowerNetState.Dark), Heater(powered: false)), 0);

            AlertRow row = alerts.Rows.Single(r => r.Key == AlertModel.PowerLossKey);
            Assert.That(row.Severity, Is.EqualTo(AlertSeverity.Danger));
            Assert.That(row.Detail, Is.EqualTo("50 W short"));
            Assert.That(row.Cell, Is.EqualTo(At), "a click goes to what stopped");

            alerts.Refresh(AlertFrame(new PowerNetView(7, 1_000, 875, PowerNetState.Live), Heater()), 1);
            Assert.That(alerts.Rows.Any(r => r.Key == AlertModel.PowerLossKey), Is.False);
        }

        [Test]
        public void AnEmptyGeneratorOnANetThatWantsPowerRaisesOutOfFuel()
        {
            var alerts = new AlertModel();
            alerts.Refresh(AlertFrame(new PowerNetView(7, 0, 175, PowerNetState.Dark), Generator(0)), 0);
            Assert.That(alerts.Rows.Any(r => r.Key == AlertModel.NoFuelKey), Is.True);

            // The controls: switched off is the player's choice, and a net that wants nothing is
            // not short of anything.
            alerts = new AlertModel();
            alerts.Refresh(AlertFrame(new PowerNetView(7, 0, 175, PowerNetState.Dark), Generator(0, on: false)), 0);
            Assert.That(alerts.Rows.Any(r => r.Key == AlertModel.NoFuelKey), Is.False);

            alerts = new AlertModel();
            alerts.Refresh(AlertFrame(new PowerNetView(7, 0, 0, PowerNetState.Idle), Generator(0, load: 0)), 0);
            Assert.That(alerts.Rows.Any(r => r.Key == AlertModel.NoFuelKey), Is.False);
        }
    }
}
