#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>How loudly an alert is drawn, and which of the palette's signal colours it takes.</summary>
    public enum AlertSeverity
    {
        /// <summary>Something worth knowing. Info blue, an "i" in a circle.</summary>
        Notice,

        /// <summary>Something that will become a problem. Warn amber, a triangle.</summary>
        Warning,

        /// <summary>Something that is a problem now. Bad red, a triangle.</summary>
        Danger,
    }

    /// <summary>
    /// One line of the alerts panel, already split the way the spec asks it to be drawn:
    /// <b>the actionable clause first, the detail trailing in dimmer ink</b> — "No stockpile —
    /// <i>salvage lies where it fell</i>". A player scanning the panel should be able to read only
    /// the leads and know what to do.
    /// </summary>
    public readonly struct AlertRow
    {
        /// <summary>The symbolic key naming the condition, for the registry and later for art.</summary>
        public readonly string Key;

        /// <summary>The highlighted target name (e.g. "Wrenn" or "Colony").</summary>
        public readonly string TargetName;

        /// <summary>The message trailing the target (e.g. " is close to breaking").</summary>
        public readonly string TargetSuffix;

        /// <summary>Optional prefix before the target, if any.</summary>
        public readonly string TargetPrefix;

        /// <summary>The complete actionable lead string.</summary>
        public readonly string Lead;

        /// <summary>The qualifying detail, dimmed.</summary>
        public readonly string Detail;

        public readonly AlertSeverity Severity;

        /// <summary>How many subjects the alert covers, for a test and for a tooltip.</summary>
        public readonly int Count;

        /// <summary>The target pawn if this alert relates to a colonist, or None.</summary>
        public readonly PawnId Pawn;

        /// <summary>The target cell if this alert relates to a map location, or None.</summary>
        public readonly CellRef? Cell;

        /// <summary>Stable token identifying this alert for dismissal.</summary>
        public readonly int DismissKey;

        public AlertRow(
            string key,
            string targetName,
            string targetSuffix,
            AlertSeverity severity,
            int count = 1,
            PawnId pawn = default,
            CellRef? cell = null,
            string targetPrefix = "",
            string detail = "")
        {
            Key = key;
            TargetName = targetName;
            TargetSuffix = targetSuffix;
            TargetPrefix = targetPrefix;
            Lead = string.IsNullOrEmpty(targetPrefix) ? targetName + targetSuffix : targetPrefix + targetName + targetSuffix;
            Detail = detail;
            Severity = severity;
            Count = count;
            Pawn = pawn;
            Cell = cell;
            DismissKey = ComputeDismissKey(key, pawn, cell);
        }

        public AlertRow(string key, string lead, string detail, AlertSeverity severity, int count)
        {
            Key = key;
            TargetName = lead;
            TargetSuffix = string.Empty;
            TargetPrefix = string.Empty;
            Lead = lead;
            Detail = detail;
            Severity = severity;
            Count = count;
            Pawn = default;
            Cell = null;
            DismissKey = ComputeDismissKey(key, default, null);
        }

        public static int ComputeDismissKey(string key, PawnId pawn, CellRef? cell)
        {
            unchecked
            {
                int hash = (key != null ? key.GetHashCode() : 0) * 397;
                hash = (hash * 397) ^ pawn.Value;
                if (cell.HasValue)
                {
                    hash = (hash * 397) ^ cell.Value.X;
                    hash = (hash * 397) ^ (cell.Value.Y << 10);
                    hash = (hash * 397) ^ (cell.Value.Z << 20);
                }
                return hash;
            }
        }
    }

    /// <summary>
    /// What the alerts panel says, read off the published frame and nothing else.
    /// </summary>
    public sealed class AlertModel
    {
        /// <summary>Food at or under which a colonist counts as starving, in thousandths.</summary>
        public const int StarveAt = 120;

        /// <summary>Food a colonist must climb back to before the starving alert can clear.</summary>
        public const int StarveClearAt = 300;

        /// <summary>Mood at or under which a colonist counts as close to breaking.</summary>
        public const int BreakAt = MoodBands.Strained;

        /// <summary>Mood a colonist must climb back to before the breaking alert can clear.</summary>
        public const int BreakClearAt = MoodBands.Content;

        /// <summary>Seconds a colony must be idle before the panel says so.</summary>
        public const double IdleSustain = 3.0;

        public const string StarveKey = "ui.alert.starvation";
        public const string BreakKey = "ui.alert.mentalbreak";
        public const string IdleKey = "ui.alert.idle";

        /// <summary>Every key this panel can put on screen, for the registry test.</summary>
        public static readonly string[] IconKeys = { StarveKey, BreakKey, IdleKey };

        public readonly List<AlertRow> Rows = new List<AlertRow>();

        readonly HashSet<int> _starving = new HashSet<int>();
        readonly HashSet<int> _breaking = new HashSet<int>();
        readonly HashSet<int> _dismissed = new HashSet<int>();

        double _idleSince = -1.0;

        int _wasStarving = -1;
        int _wasBreaking = -1;
        bool _wasIdle;
        int _wasColony = -1;
        int _latchVersion;
        int _wasLatchVersion = -1;
        int _dismissVersion;
        int _wasDismissVersion = -1;

        /// <summary>Dismiss an active alert until its condition clears and re-occurs.</summary>
        public void Dismiss(int dismissKey)
        {
            if (_dismissed.Add(dismissKey))
            {
                _dismissVersion++;
                for (int i = 0; i < Rows.Count; i++)
                {
                    if (Rows[i].DismissKey == dismissKey)
                    {
                        Rows.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        /// <summary>Dismiss all currently visible alerts.</summary>
        public void DismissAll()
        {
            if (Rows.Count == 0) return;
            for (int i = 0; i < Rows.Count; i++)
                _dismissed.Add(Rows[i].DismissKey);
            Rows.Clear();
            _dismissVersion++;
        }

        public bool IsDismissed(int dismissKey) => _dismissed.Contains(dismissKey);

        /// <summary>
        /// Recompute from a frame. <paramref name="seconds"/> is wall-clock, not ticks.
        /// </summary>
        public void Refresh(WorldSnapshot snapshot, double seconds)
        {
            int starving = 0;
            int breaking = 0;
            int idle = 0;

            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                PawnView pawn = pawns[i];
                int id = pawn.Id.Value;

                if (Latch(_starving, id, pawn.Food, StarveAt, StarveClearAt, ref _latchVersion))
                {
                    starving++;
                }
                else
                {
                    _dismissed.Remove(AlertRow.ComputeDismissKey(StarveKey, pawn.Id, default));
                }

                if (Latch(_breaking, id, pawn.Mood, BreakAt, BreakClearAt, ref _latchVersion))
                {
                    breaking++;
                }
                else
                {
                    _dismissed.Remove(AlertRow.ComputeDismissKey(BreakKey, pawn.Id, default));
                }

                if (pawn.JobDef < 0) idle++;
            }

            Forget(_starving, snapshot, ref _latchVersion);
            Forget(_breaking, snapshot, ref _latchVersion);

            if (idle > 0 && idle == pawns.Length)
            {
                if (_idleSince < 0.0) _idleSince = seconds;
            }
            else
            {
                _idleSince = -1.0;
                _dismissed.Remove(AlertRow.ComputeDismissKey(IdleKey, default, default));
                for (int i = 0; i < pawns.Length; i++)
                    _dismissed.Remove(AlertRow.ComputeDismissKey(IdleKey, pawns[i].Id, default));
            }

            bool idleStands = _idleSince >= 0.0 && seconds - _idleSince >= IdleSustain;

            if (starving == _wasStarving && breaking == _wasBreaking &&
                idleStands == _wasIdle && pawns.Length == _wasColony &&
                _latchVersion == _wasLatchVersion && _dismissVersion == _wasDismissVersion)
                return;

            _wasStarving = starving;
            _wasBreaking = breaking;
            _wasIdle = idleStands;
            _wasColony = pawns.Length;
            _wasLatchVersion = _latchVersion;
            _wasDismissVersion = _dismissVersion;
            Rows.Clear();

            // Worst first: Danger (starving), then Warning (breaking), then Notice (idle).
            for (int i = 0; i < pawns.Length; i++)
            {
                PawnView pawn = pawns[i];
                if (_starving.Contains(pawn.Id.Value))
                {
                    int dismissKey = AlertRow.ComputeDismissKey(StarveKey, pawn.Id, default);
                    if (!_dismissed.Contains(dismissKey))
                    {
                        string name = ColonistNames.Of(snapshot, pawn.Id);
                        Rows.Add(new AlertRow(
                            StarveKey,
                            name,
                            " is starving",
                            AlertSeverity.Danger,
                            count: 1,
                            pawn: pawn.Id));
                    }
                }
            }

            for (int i = 0; i < pawns.Length; i++)
            {
                PawnView pawn = pawns[i];
                if (_breaking.Contains(pawn.Id.Value))
                {
                    int dismissKey = AlertRow.ComputeDismissKey(BreakKey, pawn.Id, default);
                    if (!_dismissed.Contains(dismissKey))
                    {
                        string name = ColonistNames.Of(snapshot, pawn.Id);
                        Rows.Add(new AlertRow(
                            BreakKey,
                            name,
                            " is close to breaking",
                            AlertSeverity.Warning,
                            count: 1,
                            pawn: pawn.Id));
                    }
                }
            }

            if (idleStands)
            {
                if (pawns.Length == 1)
                {
                    int dismissKey = AlertRow.ComputeDismissKey(IdleKey, pawns[0].Id, default);
                    if (!_dismissed.Contains(dismissKey))
                    {
                        string name = ColonistNames.Of(snapshot, pawns[0].Id);
                        Rows.Add(new AlertRow(
                            IdleKey,
                            name,
                            " is idle",
                            AlertSeverity.Notice,
                            count: 1,
                            pawn: pawns[0].Id));
                    }
                }
                else
                {
                    int dismissKey = AlertRow.ComputeDismissKey(IdleKey, default, default);
                    if (!_dismissed.Contains(dismissKey))
                    {
                        Rows.Add(new AlertRow(
                            IdleKey,
                            "Colony",
                            " is idle",
                            AlertSeverity.Notice,
                            count: idle));
                    }
                }
            }
        }

        static bool Latch(HashSet<int> raised, int id, int value, int at, int clearAt, ref int version)
        {
            bool already = raised.Contains(id);
            if (value <= at)
            {
                if (!already)
                {
                    raised.Add(id);
                    version++;
                }
                return true;
            }
            if (already && value < clearAt) return true;
            if (already)
            {
                raised.Remove(id);
                version++;
            }
            return false;
        }

        static void Forget(HashSet<int> raised, WorldSnapshot snapshot, ref int version)
        {
            if (raised.Count == 0) return;
            List<int>? gone = null;
            foreach (int id in raised)
                if (!snapshot.TryGetPawn(new PawnId(id), out _))
                    (gone ??= new List<int>()).Add(id);
            if (gone == null) return;
            for (int i = 0; i < gone.Count; i++)
            {
                raised.Remove(gone[i]);
                version++;
            }
        }
    }
}
