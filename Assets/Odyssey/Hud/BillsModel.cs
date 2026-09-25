#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>Which ink a bill's status line is written in (design 49 §3).</summary>
    public enum BillTone
    {
        /// <summary>The ordinary state of a bill: its count is met.</summary>
        Meta,

        /// <summary>The station cannot work it: no power.</summary>
        Warn,

        /// <summary>Paused by the player.</summary>
        Dim,
    }

    /// <summary>One bill as the pane draws it (design 48 §5, design 49 §3).</summary>
    public sealed class BillRow
    {
        /// <summary>Its place in the station's list, which is what every command about it names.</summary>
        public int Index;

        /// <summary>The number printed at the row's left: its place counting from one.</summary>
        public string Number = string.Empty;

        /// <summary>What it makes, in the registry's words.</summary>
        public string Recipe = string.Empty;

        /// <summary>The storage category of what it makes, for the hue under its tile (<see cref="HudTheme.ItemCategoryHues"/>).</summary>
        public int ProductCategory;

        /// <summary>A <see cref="BillModeHandle"/> value, and the words for it.</summary>
        public int Mode;
        public string ModeLabel = string.Empty;

        /// <summary>The number the mode counts to, as the stepper prints it: "--" for a bill that never stops.</summary>
        public string Target = string.Empty;

        /// <summary>The number the mode counts to. Nought for forever.</summary>
        public int TargetValue;

        /// <summary>What the mode is counting now, against the target: "3 / 10". "--" for forever.</summary>
        public string Progress = string.Empty;

        /// <summary>How far along the track is filled, per mille, never past the end. Nought for forever.</summary>
        public int ProgressPerMille;

        public bool Suspended;

        /// <summary>Nothing to do: the mode's count is met.</summary>
        public bool Satisfied;

        /// <summary>The line under the name: waiting for power, paused, done, or nothing.</summary>
        public string State = string.Empty;
        public BillTone Tone;

        /// <summary>The stepper's two buttons. Both off for forever, and the minus off at one.</summary>
        public bool CanLess, CanMore;

        public bool IsFirst, IsLast;
    }

    /// <summary>
    /// A thing that takes bills: what it is, whether it needs power to work them, and what it can
    /// be told to make. One row per station kind — a crafting bench is a row here, not a second
    /// pane (design 49 §2).
    /// </summary>
    public readonly struct BillStation
    {
        /// <summary>An <see cref="EdificeHandle"/> value.</summary>
        public readonly int Edifice;

        /// <summary>A station that stops working without power: the pane shows the status strip when it has none.</summary>
        public readonly bool NeedsPower;

        /// <summary>What <see cref="BillsModel.PressAdd"/> adds, a <see cref="RecipeHandle"/> value.</summary>
        public readonly int DefaultRecipe;

        /// <summary>
        /// The registry key of the line saying what one of its products takes here (design 48 §14):
        /// a campfire's says wood as well as food, because it burns one a meal.
        /// </summary>
        public readonly string NeedsKey;

        public BillStation(int edifice, bool needsPower, int defaultRecipe, string needsKey)
        {
            Edifice = edifice;
            NeedsPower = needsPower;
            DefaultRecipe = defaultRecipe;
            NeedsKey = needsKey;
        }
    }

    /// <summary>
    /// The bill list on a station's pane (design 48 §5, laid out by design 49), as data the shell
    /// draws and the commands its buttons send. No UnityEngine, so every rule here is proven in the
    /// fast tier; the view (<c>BillList</c>) only lays rows out and hands the intents on.
    ///
    /// <para><b>General, not a cooker's.</b> Nothing in here knows what a meal is except the two
    /// tables at the top: <see cref="Stations"/> says what takes bills and <see cref="Recipes"/>
    /// what each recipe is called and which category it makes. A crafting bench adds a row to
    /// each and gets the same pane.</para>
    ///
    /// <para><b>Built from the published frame, never from a click.</b> A station nobody has used is
    /// not published at all (<see cref="StationView"/>), so whether the pane shows a bill list is
    /// decided by what stands in the cell, and the rows by the frame. A row the player just added
    /// appears when the next frame carries it, which is the same tick while paused (the intent
    /// applies at once, design 48 §5).</para>
    /// </summary>
    public sealed class BillsModel
    {
        /// <summary>The most bills a station holds (<c>Kitchen.MaxBills</c>).</summary>
        public const int MaxRows = 5;

        /// <summary>Every kind of station, and what it needs.</summary>
        public static readonly BillStation[] Stations =
        {
            new BillStation(EdificeHandle.Galley, needsPower: true, RecipeHandle.Meal, "ui.bill.needs"),
            new BillStation(EdificeHandle.Campfire, needsPower: false, RecipeHandle.Meal, "ui.bill.needs.fire"),
        };

        /// <summary>The recipes' names, in <see cref="RecipeHandle"/> order.</summary>
        public static readonly string[] RecipeKeys = { "ui.recipe.meal" };

        /// <summary>What each recipe makes, as a storage category (0 is food), in <see cref="RecipeHandle"/> order.</summary>
        public static readonly int[] RecipeCategories = { 0 };

        /// <summary>The modes' names, in <see cref="BillModeHandle"/> order.</summary>
        public static readonly string[] ModeKeys = { "ui.bill.mode.until", "ui.bill.mode.times", "ui.bill.mode.forever" };

        /// <summary>What the stepper and the count print for a bill that never stops.</summary>
        public const string NoFigure = "--";

        /// <summary>The widest progress line that keeps its spaces, "10 / 10": past it the spaces go, so "100/120" still fits the column.</summary>
        public const int SpacedProgressMax = 7;

        public readonly List<BillRow> Rows = new List<BillRow>();

        /// <summary>Is the thing under the pane a station at all.</summary>
        public bool Showing { get; private set; }

        /// <summary>Could it work right now. True for a station the frame has not published yet, which is one nobody has used.</summary>
        public bool Ready { get; private set; } = true;

        /// <summary>This kind of station stops without power.</summary>
        public bool NeedsPower { get; private set; }

        /// <summary>The station's power switch, where it has one: on, or thrown off by the player.</summary>
        public bool HasSwitch { get; private set; }
        public bool SwitchOn { get; private set; }

        /// <summary>The station's cell, as every command names it.</summary>
        public CellRef Cell { get; private set; }

        int _defaultRecipe;

        /// <summary>What one product takes here (design 48 §14): raw food, and a campfire's wood.</summary>
        public string Needs { get; private set; } = string.Empty;

        /// <summary>
        /// How many meals the raw food on the map would make, or that there is none. Empty until the
        /// station has been published, which it is from its first bill.
        /// </summary>
        public string Supply { get; private set; } = string.Empty;

        /// <summary>There is no raw food at all: the supply line is a warning.</summary>
        public bool NoSupply { get; private set; }

        /// <summary>One more bill would be refused: the list is full.</summary>
        public bool Full => Rows.Count >= MaxRows;

        /// <summary>The status strip is up: a station that needs power and has none.</summary>
        public bool HasProblem => Showing && NeedsPower && !Ready;

        /// <summary>The strip's one word (no explanation under it: the switch beside it is the explanation).</summary>
        public string ProblemLabel => HasProblem ? Registry.Label("ui.bill.unpowered") : string.Empty;

        /// <summary>The switch's words: what pressing it will do.</summary>
        public string SwitchLabel => Registry.Label(SwitchOn ? "ui.command.switchoff" : "ui.command.switchon");

        /// <summary>The count beside the section heading: "(3)".</summary>
        public string CountLabel => "(" + Rows.Count.ToString(CultureInfo.InvariantCulture) + ")";

        /// <summary>Is this edifice something a recipe can be made at.</summary>
        public static bool IsStation(int edifice) => TryStation(edifice, out _);

        /// <summary>A station that wants power; a campfire does not.</summary>
        public static bool StationNeedsPower(int edifice) => TryStation(edifice, out BillStation s) && s.NeedsPower;

        public static bool TryStation(int edifice, out BillStation station)
        {
            foreach (BillStation s in Stations)
            {
                if (s.Edifice != edifice) continue;
                station = s;
                return true;
            }
            station = default;
            return false;
        }

        /// <summary>
        /// Rebuild the rows for the station standing in <paramref name="cell"/>, if one does.
        /// <paramref name="edifice"/> is what the cell's own answer says stands there.
        /// </summary>
        public void Refresh(WorldSnapshot snapshot, CellRef cell, int cellIndex, int edifice)
        {
            Rows.Clear();
            Cell = cell;
            Showing = TryStation(edifice, out BillStation kind);
            NeedsPower = kind.NeedsPower;
            _defaultRecipe = kind.DefaultRecipe;
            Ready = true;
            HasSwitch = false;
            SwitchOn = false;
            Supply = string.Empty;
            NoSupply = false;
            Needs = Showing ? Registry.Label(kind.NeedsKey) : string.Empty;
            if (!Showing) return;

            if (snapshot.TryGetPowerDevice(cellIndex, out PowerDeviceView device))
            {
                HasSwitch = true;
                SwitchOn = device.On;
            }

            ReadOnlySpan<StationView> stations = snapshot.Stations;
            for (int s = 0; s < stations.Length; s++)
            {
                if (stations[s].CellIndex != cellIndex) continue;
                StationView station = stations[s];
                Ready = station.Ready;
                NoSupply = station.RawMeals <= 0;
                Supply = NoSupply ? Registry.Label("ui.bill.nosupply")
                    : Registry.Label("ui.bill.supply").Replace("{n}", station.RawMeals.ToString(CultureInfo.InvariantCulture));

                ReadOnlySpan<BillView> bills = snapshot.Bills;
                for (int b = 0; b < station.BillCount; b++)
                {
                    int at = station.FirstBill + b;
                    if ((uint)at >= (uint)bills.Length) break;
                    Rows.Add(RowFor(bills[at], b, station.BillCount, NeedsPower && !Ready));
                }
                return;
            }
        }

        static BillRow RowFor(in BillView bill, int index, int count, bool unpowered)
        {
            var row = new BillRow
            {
                Index = index,
                Number = (index + 1).ToString(CultureInfo.InvariantCulture),
                Recipe = (uint)bill.Recipe < (uint)RecipeKeys.Length ? Registry.Label(RecipeKeys[bill.Recipe]) : string.Empty,
                ProductCategory = (uint)bill.Recipe < (uint)RecipeCategories.Length ? RecipeCategories[bill.Recipe] : -1,
                Mode = bill.Mode,
                ModeLabel = bill.Mode < ModeKeys.Length ? Registry.Label(ModeKeys[bill.Mode]) : string.Empty,
                Suspended = bill.Suspended,
                Satisfied = bill.Satisfied,
                IsFirst = index == 0,
                IsLast = index == count - 1,
            };

            if (bill.Mode == BillModeHandle.Forever)
            {
                row.Target = NoFigure;
                row.Progress = NoFigure;
            }
            else
            {
                row.TargetValue = bill.Target;
                row.Target = bill.Target.ToString(CultureInfo.InvariantCulture);
                row.Progress = ProgressText(bill.Count, bill.Target);
                row.ProgressPerMille = bill.Target <= 0 ? 0 : (int)Math.Min(1000L, Math.Max(0L, bill.Count * 1000L / bill.Target));
                row.CanLess = bill.Target > 1;
                row.CanMore = true;
            }

            // Power first: a paused bill at a dark cooker is still waiting for power when it is resumed.
            if (unpowered)
            {
                row.State = Registry.Label("ui.bill.waiting");
                row.Tone = BillTone.Warn;
            }
            else if (bill.Suspended)
            {
                row.State = Registry.Label("ui.bill.suspended");
                row.Tone = BillTone.Dim;
            }
            else if (bill.Satisfied)
            {
                row.State = Registry.Label("ui.bill.done");
                row.Tone = BillTone.Meta;
            }
            return row;
        }

        /// <summary>"3 / 10", or "100/120" once the spaced form would be wider than the column holds.</summary>
        public static string ProgressText(int count, int target)
        {
            string c = count.ToString(CultureInfo.InvariantCulture);
            string t = target.ToString(CultureInfo.InvariantCulture);
            string spaced = c + " / " + t;
            return spaced.Length <= SpacedProgressMax ? spaced : c + "/" + t;
        }

        // ---- commands ------------------------------------------------------------------------------

        Intent Edit(int op, int b = 0, int c = 0) => new Intent(IntentKind.EditBill, Cell, op, b, c);

        /// <summary>Add a bill for the station's recipe at the bottom of the list, or nothing when the list is full.</summary>
        public bool PressAdd(out Intent command)
        {
            command = Edit(BillEdit.Add, _defaultRecipe);
            return Showing && !Full;
        }

        /// <summary>The next mode round: until you have, make, forever, and back.</summary>
        public Intent PressMode(BillRow row) => Edit(BillEdit.SetMode, row.Index, (row.Mode + 1) % BillModeHandle.Count);

        /// <summary>
        /// Move the target by <paramref name="delta"/>, never below one. Refused for a bill that
        /// never stops, which has no target to move.
        /// </summary>
        public bool PressNudge(BillRow row, int delta, out Intent command)
        {
            int target = row.TargetValue;
            int next = Math.Max(1, target + delta);
            command = Edit(BillEdit.SetTarget, row.Index, next);
            return row.Mode != BillModeHandle.Forever && next != target;
        }

        public Intent PressSuspend(BillRow row) => Edit(BillEdit.SetSuspended, row.Index, row.Suspended ? 0 : 1);

        public bool PressUp(BillRow row, out Intent command)
        {
            command = Edit(BillEdit.MoveUp, row.Index);
            return !row.IsFirst;
        }

        public bool PressDown(BillRow row, out Intent command)
        {
            command = Edit(BillEdit.MoveDown, row.Index);
            return !row.IsLast;
        }

        public Intent PressRemove(BillRow row) => Edit(BillEdit.Remove, row.Index);

        /// <summary>
        /// Throw the station's power switch (design 32 §5): the same intent as the tile's Switch
        /// row, applied at once. Refused for a station with no switch.
        /// </summary>
        public bool PressSwitch(out Intent command)
        {
            command = new Intent(IntentKind.SetPowerSwitch, Cell, SwitchOn ? 0 : 1);
            return Showing && HasSwitch;
        }

        /// <summary>The one row an empty list shows.</summary>
        public string EmptyLabel => Showing && Rows.Count == 0 ? Registry.Label("ui.bill.none") : string.Empty;

        /// <summary>
        /// A number that moves whenever anything the pane draws moves, so the view rewrites its
        /// words only then — the pane refreshes fifteen times a second.
        /// </summary>
        public int Signature()
        {
            unchecked
            {
                int hash = Showing ? 3 : 5;
                hash = hash * 31 + (Ready ? 17 : 19);
                hash = hash * 31 + (HasSwitch ? (SwitchOn ? 7 : 11) : 13);
                hash = hash * 31 + Needs.GetHashCode();
                hash = hash * 31 + Supply.GetHashCode();
                hash = hash * 31 + Rows.Count;
                for (int i = 0; i < Rows.Count; i++)
                {
                    BillRow row = Rows[i];
                    hash = hash * 31 + row.Mode;
                    hash = hash * 31 + row.Recipe.GetHashCode();
                    hash = hash * 31 + row.TargetValue;
                    hash = hash * 31 + row.Progress.GetHashCode();
                    hash = hash * 31 + (row.Suspended ? 1 : 0) + (row.Satisfied ? 2 : 0);
                }
                return hash;
            }
        }
    }
}
