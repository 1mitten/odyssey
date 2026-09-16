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

        /// <summary>The actionable clause, in full ink.</summary>
        public readonly string Lead;

        /// <summary>The qualifying detail, dimmed.</summary>
        public readonly string Detail;

        public readonly AlertSeverity Severity;

        /// <summary>How many subjects the alert covers, for a test and for a tooltip.</summary>
        public readonly int Count;

        public AlertRow(string key, string lead, string detail, AlertSeverity severity, int count)
        {
            Key = key;
            Lead = lead;
            Detail = detail;
            Severity = severity;
            Count = count;
        }
    }

    /// <summary>
    /// What the alerts panel says, read off the published frame and nothing else.
    ///
    /// <para><b>The panel was a placeholder and this is the smallest honest replacement.</b> It
    /// used to print "No active alerts." above a sentence explaining that conditions arrive with
    /// M2, which is a dev note sitting in a player-facing region; the acceptance criteria delete
    /// both and say the panel is hidden outright when there is nothing to say. That only works if
    /// something can put a line in it, so this raises the three conditions the frame can actually
    /// support — a colonist starving, a colonist about to break, and a colony standing
    /// idle.</para>
    ///
    /// <para><b>Every one has hysteresis, for the reason <c>AlertWatch</c> already gives about
    /// chimes:</b> a need sitting on its threshold flaps either side of it for hours of game time,
    /// and a panel that appears and disappears with the flapping is worse than no panel. A
    /// condition raises at its threshold and clears only once it has recovered past a wider
    /// re-arm band. Idle is different in kind — it is momentary rather than a level — so it is
    /// sustained instead: a colony has to be idle continuously for <see cref="IdleSustain"/>
    /// seconds before it is worth saying, which is what stops the panel blinking every time
    /// somebody finishes a haul.</para>
    ///
    /// <para>Needs are read on the simulation's own 0–1000 scale, like every other need in this
    /// assembly. <c>AlertWatch</c> in the audio director describes the same field as "0–100" and
    /// thresholds at 12, which on the real scale is 1.2% — deep enough to be nearly dead. That is
    /// its bug to fix, not this class's to copy.</para>
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

        double _idleSince = -1.0;

        // What the rows currently say. The panel refreshes four times a second and every line it
        // holds is an interpolated string, so a model that rebuilt them on every pass would
        // allocate for as long as an alert stood — which is exactly the condition ADR 0003's flip
        // condition F1 forbids, and exactly when the player is least able to afford a hitch.
        int _wasStarving = -1;
        int _wasBreaking = -1;
        bool _wasIdle;
        int _wasColony = -1;

        /// <summary>
        /// Recompute from a frame. <paramref name="seconds"/> is wall-clock, not ticks: an alert
        /// that appeared for a third of a second at speed three and vanished again would be an
        /// alert the player never read, and the sustain has to be measured in the time they are
        /// actually looking at the screen.
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

                if (Latch(_starving, id, pawn.Food, StarveAt, StarveClearAt)) starving++;
                if (Latch(_breaking, id, pawn.Mood, BreakAt, BreakClearAt)) breaking++;
                if (pawn.JobDef < 0) idle++;
            }

            // Anyone who left the frame leaves their latch behind; a colony of a dozen makes this
            // a dozen comparisons a refresh, and the alternative is a set that grows for ever.
            Forget(_starving, snapshot);
            Forget(_breaking, snapshot);

            if (idle > 0 && idle == pawns.Length)
            {
                if (_idleSince < 0.0) _idleSince = seconds;
            }
            else _idleSince = -1.0;

            bool idleStands = _idleSince >= 0.0 && seconds - _idleSince >= IdleSustain;

            // Nothing has changed, so the lines already in Rows are still the right lines. This
            // is the whole of the allocation guard: the strings below are built once per genuine
            // change of state rather than four times a second for as long as the alert stands.
            if (starving == _wasStarving && breaking == _wasBreaking &&
                idleStands == _wasIdle && pawns.Length == _wasColony)
                return;

            _wasStarving = starving;
            _wasBreaking = breaking;
            _wasIdle = idleStands;
            _wasColony = pawns.Length;
            Rows.Clear();

            // Worst first, because the panel is read from the top and a player who reads one line
            // should have read the one that matters most.
            if (starving > 0)
                Rows.Add(new AlertRow(
                    StarveKey,
                    starving == 1 ? "A colonist is starving" : $"{starving} colonists are starving",
                    "no meal has been reached in time",
                    AlertSeverity.Danger, starving));

            if (breaking > 0)
                Rows.Add(new AlertRow(
                    BreakKey,
                    breaking == 1 ? "A colonist is close to breaking" : $"{breaking} colonists are close to breaking",
                    "mood has fallen into the strained band",
                    AlertSeverity.Warning, breaking));

            if (idleStands)
                Rows.Add(new AlertRow(
                    IdleKey,
                    pawns.Length == 1 ? "The colonist has nothing to do" : "The colony has nothing to do",
                    "give an order, or mark something to cut or mine",
                    AlertSeverity.Notice, idle));
        }

        /// <summary>True while the condition stands for this subject.</summary>
        static bool Latch(HashSet<int> raised, int id, int value, int at, int clearAt)
        {
            bool already = raised.Contains(id);
            if (value <= at)
            {
                raised.Add(id);
                return true;
            }
            if (already && value < clearAt) return true;
            raised.Remove(id);
            return false;
        }

        static void Forget(HashSet<int> raised, WorldSnapshot snapshot)
        {
            if (raised.Count == 0) return;
            List<int>? gone = null;
            foreach (int id in raised)
                if (!snapshot.TryGetPawn(new PawnId(id), out _))
                    (gone ??= new List<int>()).Add(id);
            if (gone == null) return;
            foreach (int id in gone) raised.Remove(id);
        }
    }
}
