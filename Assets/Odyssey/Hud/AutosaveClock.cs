#nullable enable

namespace Odyssey.Hud
{
    /// <summary>
    /// When the colony saves itself.
    ///
    /// <para><b>Why there is an autosave at all.</b> The owner asked for one (2026-09-21) beside
    /// the quit confirmation, and the two answer the same fear from opposite ends: the confirm
    /// stops you throwing a colony away by pressing the wrong row, and this stops you losing one
    /// to a crash, a power cut or an afternoon in which you simply never thought to press Save.
    /// It writes to <b>the same file</b> the session is bound to, which is the rule
    /// <c>SessionCommands.SaveKey</c> already keeps — a folder that fills up with dated files is
    /// the thing the binding was invented to stop (`17-start-flow.md` §3).</para>
    ///
    /// <para><b>A game day, not a wall-clock minute.</b> Five real minutes means something
    /// different at ×1 and at ×3, and a save that lands in the middle of an afternoon has nothing
    /// to say for where it landed. A day boundary is on the clock the player is already reading,
    /// it is the same amount of <i>colony</i> whatever speed they run at, and while the game is
    /// paused it never comes round at all — which is right, because nothing changed.</para>
    ///
    /// <para><b>What it counts is the day, not the crossing.</b> Ticks are consumed in batches and
    /// a frame at ×3 retires several; a rule that watched for "the tick where the day changed"
    /// would miss it the frame a batch stepped over midnight. So the day is read and compared, and
    /// a missed boundary is still a boundary.</para>
    ///
    /// <para>Unity-free by construction (ADR 0003): it runs in the fast tier.</para>
    /// </summary>
    public sealed class AutosaveClock
    {
        /// <summary>The registry key naming the row in the settings panel.</summary>
        public const string SettingKey = "ui.settings.autosave";

        /// <summary>
        /// The registry key naming the line the Events panel shows after a save — its word and its
        /// icon. Its own key rather than <see cref="SessionCommands.SaveKey"/>, because "Save" is
        /// a row you press and "Autosaved" is a thing that happened to you, and the wiki lists
        /// both so the owner can correct either.
        /// </summary>
        public const string NoticeKey = "ui.session.autosave";

        /// <summary>
        /// How many game days between saves, as the panel offers them. <b>Zero is off</b> and is a
        /// rung rather than a separate toggle: one control that reads "Off · Every day · Every 2
        /// days · Every 3 days" answers both questions at once, and a toggle beside a ladder would
        /// be two controls that can disagree (off, but every 2 days).
        /// </summary>
        public static readonly int[] DayRungs = { 0, 1, 2, 3 };

        /// <summary>
        /// Every day, and on by default — the owner's call: *"have it on by default saving to the
        /// same game"*.
        /// </summary>
        public const int DefaultDays = 1;

        /// <summary>What a rung says on the panel.</summary>
        public static string RungLabel(int days) => days switch
        {
            0 => "Off",
            1 => "Every day",
            _ => $"Every {days} days",
        };

        /// <summary>What a rung says when the pointer rests on it.</summary>
        public static string RungTooltip(int days) => days switch
        {
            0 => "Nothing is written unless you press Save",
            1 => "Written over this colony's own save each morning",
            _ => $"Written over this colony's own save every {days} days",
        };

        /// <summary>Whether a value names a rung this ladder offers.</summary>
        public static bool IsRung(int days)
        {
            foreach (int rung in DayRungs) if (rung == days) return true;
            return false;
        }

        /// <summary>No day has been seen yet. Not zero: day zero is a real day.</summary>
        const int NotStarted = int.MinValue;

        int _lastDay = NotStarted;

        /// <summary>Which day the last save was taken on, or null before the first one.</summary>
        public int? LastDay => _lastDay == NotStarted ? null : _lastDay;

        /// <summary>
        /// A colony arrived — new, or opened from a file. The day it arrives on counts as already
        /// saved, so loading a save does not immediately write one back over it.
        /// </summary>
        public void Begin(long tick) => _lastDay = Day(tick);

        /// <summary>The colony went away. The next one starts its own count.</summary>
        public void Forget() => _lastDay = NotStarted;

        /// <summary>
        /// Is a save due now? Asked every frame, so it is arithmetic and nothing else, and it
        /// records the day it answers yes on — one save a boundary however many times it is asked.
        ///
        /// <para>An unstarted clock answers no and starts itself, so a caller that never called
        /// <see cref="Begin"/> gets the sane behaviour rather than a save on its first frame.</para>
        /// </summary>
        public bool Due(long tick, int everyDays)
        {
            if (everyDays <= 0) return false;

            int day = Day(tick);
            if (_lastDay == NotStarted) { _lastDay = day; return false; }

            // Behind rather than equal: a clock that went backwards — an older save loaded into a
            // running session — is not a reason to write over it.
            if (day - _lastDay < everyDays) return false;

            _lastDay = day;
            return true;
        }

        /// <summary>The day this tick falls on, counted from the colony's first. Deliberately
        /// <c>GameClock</c>'s own count rather than a second one.</summary>
        public static int Day(long tick) => GameClock.DayOfMonthsStart(tick);
    }
}
