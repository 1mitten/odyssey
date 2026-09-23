#nullable enable
using System.Globalization;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// How the interface writes power (design 32 §10): watts, a net's balance, a building's state
    /// and a hopper. <b>The one owner of those words</b>, so the pane, the alerts and a tooltip
    /// cannot come to write "1000W", "1,000 W" and "1 kW" for the same generator — the
    /// temperature line's lesson (<c>TemperatureLabels</c>), taken before it was needed.
    /// </summary>
    public static class PowerLabels
    {
        /// <summary>Watts, grouped: "175 W", "1,000 W". Invariant, so the tests read what a player reads.</summary>
        public static string Watts(int watts) =>
            watts.ToString("N0", CultureInfo.InvariantCulture) + " W";

        /// <summary>A net's balance as the pane says it: what is wanted of what is made.</summary>
        public static string Balance(in PowerNetView net) =>
            Watts(net.DemandW) + " of " + Watts(net.SupplyW);

        /// <summary>A net's state in a word.</summary>
        public static string State(PowerNetState state) => state switch
        {
            PowerNetState.Live => "live",
            PowerNetState.Dark => "dark — short of power",
            _ => "idle",
        };

        /// <summary>
        /// What a power building is doing, in the order a player's next question follows: is it
        /// switched off, is it connected at all, is its net short, is it out of fuel — and only
        /// then what it is making or using.
        /// </summary>
        public static string Status(in PowerDeviceView device, bool netKnown, in PowerNetView net)
        {
            if (!device.On) return "switched off";
            if (device.NetKey < 0) return "not connected — lay a conduit beside it";

            if (device.Role == PowerRole.Generator)
            {
                if (device.BurnsFuel && device.FuelMilli <= 0) return "out of fuel";
                return device.LoadW > 0
                    ? "carrying " + Watts(device.LoadW) + " of " + Watts(device.Watts)
                    : "running, nothing drawing";
            }

            if (device.Powered) return Watts(device.Watts) + " — powered";
            return netKnown && net.State == PowerNetState.Dark
                ? Watts(device.Watts) + " — the net is short"
                : Watts(device.Watts) + " — no power";
        }

        /// <summary>"30 of 75 wood", in whole units, rounded down: a hopper reading one more than it holds is a lie.</summary>
        public static string Fuel(in PowerDeviceView device) =>
            device.FuelMilli / 1_000 + " of " + device.FuelCapacityMilli / 1_000 + " " + BuildLabels.Stuff(StuffHandle.Wood);

        /// <summary>The colour a net's state is drawn in, on the pane and on the lines.</summary>
        public static HudColour Colour(PowerNetState state) => state switch
        {
            PowerNetState.Live => HudTheme.Good,
            PowerNetState.Dark => HudTheme.Bad,
            _ => HudTheme.TextMeta,
        };
    }

    /// <summary>
    /// Whether the hidden power lines are shown (design 32 §9, decision 5): while the player is
    /// doing power work, while a power building is selected, or while the overlay is switched on.
    ///
    /// <para><b>A pure function, and the one owner of the rule.</b> The line pass draws when this
    /// says so and the watch intent follows it, so the lines and the rows they are drawn from
    /// cannot disagree about whether they are meant to be seen.</para>
    ///
    /// <para><b>Deconstruct and cancel count as power work.</b> A line runs through walls and under
    /// floors, and a player taking a wall down or cancelling a run of orders needs to see what is
    /// in the cells they are sweeping — the owner's own answer to "when should they show".</para>
    /// </summary>
    public static class PowerLinesVisibility
    {
        public static bool Visible(DesignateTool tool, int building, bool powerBuildingSelected, bool overlayOn) =>
            overlayOn
            || powerBuildingSelected
            || tool == DesignateTool.RemoveConduit
            || tool == DesignateTool.Deconstruct
            || tool == DesignateTool.Cancel
            || (tool == DesignateTool.Build && BuildShapes.IsPower(building));

        public static bool Visible(DesignateDirector designate, bool powerBuildingSelected, bool overlayOn) =>
            Visible(designate.Tool, designate.Building, powerBuildingSelected, overlayOn);
    }
}
