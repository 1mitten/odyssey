#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>One bill as the pane draws it (design 48 §5).</summary>
    public sealed class BillRow
    {
        /// <summary>Its place in the station's list, which is what every command about it names.</summary>
        public int Index;

        /// <summary>What it makes, in the registry's words.</summary>
        public string Recipe = string.Empty;

        /// <summary>A <see cref="BillModeHandle"/> value, and the words for it.</summary>
        public int Mode;
        public string ModeLabel = string.Empty;

        /// <summary>The number the mode counts to, as the pane prints it. Empty for a bill that never stops.</summary>
        public string Target = string.Empty;

        /// <summary>What the mode is counting now, against the target: "3 / 10". Empty for forever.</summary>
        public string Progress = string.Empty;

        public bool Suspended;

        /// <summary>Nothing to do: the mode's count is met.</summary>
        public bool Satisfied;

        /// <summary>The line of state beside it: suspended, done, or nothing.</summary>
        public string State = string.Empty;

        public bool IsFirst, IsLast;
    }

    /// <summary>
    /// The bill list on a cooking station's pane (design 48 §5), as data the shell draws and the
    /// commands its buttons send. No UnityEngine, so every rule here is proven in the fast tier;
    /// the shell only lays rows out and hands the intents on.
    ///
    /// <para><b>Built from the published frame, never from a click.</b> A station nobody has used is
    /// not published at all (<see cref="StationView"/>), so whether the pane shows a bill list is
    /// decided by what stands in the cell — a galley or a campfire — and the rows by the frame. A
    /// row the player just added appears when the next frame carries it, which is the same tick
    /// while paused (the intent applies at once, design 48 §5).</para>
    /// </summary>
    public sealed class BillsModel
    {
        /// <summary>The most bills a station holds (<c>Kitchen.MaxBills</c>): the pane is laid out for exactly this many rows.</summary>
        public const int MaxRows = 5;

        /// <summary>The recipes' names, in <see cref="RecipeHandle"/> order.</summary>
        public static readonly string[] RecipeKeys = { "ui.recipe.meal" };

        /// <summary>The modes' names, in <see cref="BillModeHandle"/> order.</summary>
        public static readonly string[] ModeKeys = { "ui.bill.mode.until", "ui.bill.mode.times", "ui.bill.mode.forever" };

        public readonly List<BillRow> Rows = new List<BillRow>();

        /// <summary>Is the thing under the pane a cooking station at all.</summary>
        public bool Showing { get; private set; }

        /// <summary>Could it cook right now. True for a station the frame has not published yet, which is one nobody has used.</summary>
        public bool Ready { get; private set; } = true;

        /// <summary>The station's cell, as every command names it.</summary>
        public CellRef Cell { get; private set; }

        /// <summary>What one meal takes here (design 48 §14): raw food, and a campfire's wood.</summary>
        public string Needs { get; private set; } = string.Empty;

        /// <summary>
        /// How many meals the raw food on the map would make, or that there is none. Empty until the
        /// station has been published — which it is from its first bill.
        /// </summary>
        public string Supply { get; private set; } = string.Empty;

        /// <summary>There is no raw food at all: the supply line is a warning.</summary>
        public bool NoSupply { get; private set; }

        /// <summary>One more bill would be refused: the list is full.</summary>
        public bool Full => Rows.Count >= MaxRows;

        /// <summary>Is this edifice something a recipe can be made at. The galley and the campfire.</summary>
        public static bool IsStation(int edifice) => edifice == EdificeHandle.Galley || edifice == EdificeHandle.Campfire;

        /// <summary>A galley, which wants power; a campfire does not.</summary>
        public static bool NeedsPower(int edifice) => edifice == EdificeHandle.Galley;

        /// <summary>
        /// Rebuild the rows for the station standing in <paramref name="cell"/>, if one does.
        /// <paramref name="edifice"/> is what the cell's own answer says stands there.
        /// </summary>
        public void Refresh(WorldSnapshot snapshot, CellRef cell, int cellIndex, int edifice)
        {
            Rows.Clear();
            Cell = cell;
            Showing = IsStation(edifice);
            Ready = true;
            Supply = string.Empty;
            NoSupply = false;
            Needs = !Showing ? string.Empty
                : Registry.Label(edifice == EdificeHandle.Campfire ? "ui.bill.needs.fire" : "ui.bill.needs");
            if (!Showing) return;

            ReadOnlySpan<StationView> stations = snapshot.Stations;
            for (int s = 0; s < stations.Length; s++)
            {
                if (stations[s].CellIndex != cellIndex) continue;
                StationView station = stations[s];
                Ready = station.Ready;
                NoSupply = station.RawMeals <= 0;
                Supply = NoSupply ? Registry.Label("ui.bill.nosupply")
                    : Registry.Label("ui.bill.supply").Replace("{n}",
                        station.RawMeals.ToString(System.Globalization.CultureInfo.InvariantCulture));

                ReadOnlySpan<BillView> bills = snapshot.Bills;
                for (int b = 0; b < station.BillCount; b++)
                {
                    int at = station.FirstBill + b;
                    if ((uint)at >= (uint)bills.Length) break;
                    Rows.Add(RowFor(bills[at], b, station.BillCount));
                }
                return;
            }
        }

        static BillRow RowFor(in BillView bill, int index, int count)
        {
            var row = new BillRow
            {
                Index = index,
                Recipe = (uint)bill.Recipe < (uint)RecipeKeys.Length ? Registry.Label(RecipeKeys[bill.Recipe]) : string.Empty,
                Mode = bill.Mode,
                ModeLabel = bill.Mode < ModeKeys.Length ? Registry.Label(ModeKeys[bill.Mode]) : string.Empty,
                Suspended = bill.Suspended,
                Satisfied = bill.Satisfied,
                IsFirst = index == 0,
                IsLast = index == count - 1,
            };

            if (bill.Mode != BillModeHandle.Forever)
            {
                row.Target = bill.Target.ToString(System.Globalization.CultureInfo.InvariantCulture);
                row.Progress = bill.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " / " + row.Target;
            }

            row.State = bill.Suspended ? Registry.Label("ui.bill.suspended")
                : bill.Satisfied ? Registry.Label("ui.bill.done")
                : string.Empty;
            return row;
        }

        // ---- commands ------------------------------------------------------------------------------

        Intent Edit(int op, int b = 0, int c = 0) => new Intent(IntentKind.EditBill, Cell, op, b, c);

        /// <summary>Add a bill for the meal at the bottom of the list, or nothing when the list is full.</summary>
        public bool PressAdd(out Intent command)
        {
            command = Edit(BillEdit.Add, RecipeHandle.Meal);
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
            int target = 0;
            int.TryParse(row.Target, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out target);
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

        /// <summary>The line under the heading: no power for a dark galley, no bills for an empty list, else nothing.</summary>
        public string Status =>
            !Showing ? string.Empty
            : !Ready ? Registry.Label("ui.bill.unpowered")
            : Rows.Count == 0 ? Registry.Label("ui.bill.none")
            : string.Empty;
    }
}
