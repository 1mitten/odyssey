#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// One passing toast: something that happened, worth saying once and then forgetting.
    /// </summary>
    public readonly struct ToastRow
    {
        /// <summary>The registry key naming the kind of toast, for the words and later the art.</summary>
        public readonly string Key;

        /// <summary>The whole line, already built.</summary>
        public readonly string Lead;

        /// <summary>The colonist it is about, so clicking the row can select her.</summary>
        public readonly PawnId Pawn;

        /// <summary>Wall-clock second the row appeared, which is what expires it.</summary>
        public readonly double Raised;

        /// <summary>
        /// How loudly it is drawn, reusing the alerts' own scale so the chime table and the ink
        /// table do not need a second copy. A toast is good news by virtue of being a toast;
        /// <see cref="AlertSeverity.Notice"/> is the level it is said at.
        /// </summary>
        public readonly AlertSeverity Severity;

        /// <summary>Monotonic within a session, so a view can tell one row from the next.</summary>
        public readonly int Serial;

        public ToastRow(string key, string lead, PawnId pawn, double raised,
                          AlertSeverity severity, int serial)
        {
            Key = key;
            Lead = lead;
            Pawn = pawn;
            Raised = raised;
            Severity = severity;
            Serial = serial;
        }
    }

    /// <summary>
    /// The transient toasts: something happened, said once, gone by itself (SK4).
    ///
    /// <para><b>This is the channel design 09 §2.3 already named and nothing had built.</b> That
    /// section settles an intent rejected against a dead target by saying it "surfaces as a
    /// transient toast rather than a bulletin", so the word and the distinction are the design's
    /// own. Rejections are still only counted into a log (<c>OdysseyBootstrap.ReportRejections</c>)
    /// and are this stack's obvious second customer.</para>
    ///
    /// <para><b>Three channels, and the difference between them is not cosmetic.</b> Design 09
    /// §3.1 keeps alerts and bulletins apart because "merging them produces a system that is wrong
    /// for both", and a toast is the third:</para>
    /// <list type="bullet">
    /// <item><b>An alert</b> is a <i>condition</i>, re-evaluated every refresh and able to clear
    /// itself — a job the player has not done yet. <see cref="AlertModel"/>.</item>
    /// <item><b>A bulletin</b> is an <i>event worth keeping</i>: raised once, dismissed by hand and
    /// archived (panel A6, <c>ui.bulletin.*</c> — an arrival, a death, a raid).</item>
    /// <item><b>A toast</b> is an <i>event worth mentioning</i>. It expires on its own and cannot be
    /// dismissed, because there is nothing to dismiss: it is already leaving.</item>
    /// </list>
    ///
    /// <para><b>Why a level-up is a toast and not a bulletin</b>, which was the one real decision
    /// here. A bulletin is a card that stays until the player clears it, and that is right for the
    /// fifteen things <c>ui.bulletin.*</c> names, all of which happen a handful of times in a
    /// colony's life. A skill level lands every couple of minutes per colonist at low levels
    /// (docs/design/15-skills.md §8), so as a bulletin it would be a stack of cards the player
    /// clears as a chore, and the chore would teach them to clear the raid warning beside it
    /// without reading it. Frequency is what decides the channel.</para>
    ///
    /// <para><b>Not an alert severity either.</b> <see cref="AlertModel"/> exists to recompute
    /// standing conditions with a latch per condition; a level-up has nothing to latch on and
    /// nothing in a later frame to recompute it from, so it would mean faking a condition and then
    /// faking its clearance.</para>
    ///
    /// <para><b>Not saved.</b> A toast is presentation, like a gesture: the state that produced it
    /// is saved and hashed, and the saying of it is not.</para>
    /// </summary>
    public sealed class ToastModel
    {
        /// <summary>Seconds a toast stays on screen. Long enough to read twice.</summary>
        public const double LifetimeSeconds = 6.0;

        /// <summary>
        /// The most rows drawn at once. Four, because the stack sits under the alerts panel and a
        /// colony that levels six skills in one minute must not push a starving colonist off the
        /// screen. Beyond the cap the oldest goes, since the newest is the news.
        /// </summary>
        public const int MaxRows = 4;

        /// <summary>The key a skill level-up is raised under.</summary>
        public const string LevelUpKey = "ui.toast.skillup";

        /// <summary>Every key this stack can put on screen, for the registry test.</summary>
        public static readonly string[] IconKeys = { LevelUpKey };

        /// <summary>Oldest first, so the newest arrives at the bottom nearest the world.</summary>
        public readonly List<ToastRow> Rows = new List<ToastRow>();

        readonly SkillLevelWatch _levels = new SkillLevelWatch();

        int _serial;

        /// <summary>
        /// How many rows the last <see cref="Refresh"/> added. The chime reads this rather than
        /// detecting anything itself: <c>AlertChimeWatch</c>'s own remarks record what it cost
        /// when the audio kept a second copy of a rule a model already owned — the two drifted and
        /// the sound fired at a hundredth of the intended threshold, which is to say never.
        /// </summary>
        public int Added { get; private set; }

        /// <summary>
        /// The loudest severity among the rows the last <see cref="Refresh"/> added. One chime is
        /// played for a refresh however many rows arrived in it, the way the alerts panel sounds
        /// once for several conditions crossing together.
        /// </summary>
        public AlertSeverity LoudestAdded { get; private set; }

        /// <summary>
        /// Recompute from a frame. <paramref name="seconds"/> is wall-clock and not ticks, so a
        /// toast stays readable for the same time at every game speed and while paused.
        /// </summary>
        public void Refresh(WorldSnapshot snapshot, double seconds)
        {
            Added = 0;
            LoudestAdded = AlertSeverity.Notice;

            Expire(seconds);

            IReadOnlyList<SkillLevelUp> risen = _levels.Step(snapshot);
            for (int i = 0; i < risen.Count; i++)
            {
                SkillLevelUp up = risen[i];
                string skill = Registry.Label(SkillCatalogue.All[up.Skill].Key);
                string who = ColonistNames.Of(snapshot, up.Pawn);

                // The words are the registry's, with the two things that vary put into it. A
                // literal here would be the one RegistryTests forbids, and the wiki and the screen
                // would disagree the first time somebody corrected either copy.
                string lead = Registry.Label(LevelUpKey)
                    .Replace("{name}", who)
                    .Replace("{skill}", skill)
                    .Replace("{level}", up.Level.ToString());

                Raise(new ToastRow(LevelUpKey, lead, up.Pawn, seconds,
                    AlertSeverity.Notice, ++_serial));
            }
        }

        /// <summary>Put a row up now, dropping the oldest if the stack is full.</summary>
        public void Raise(ToastRow row)
        {
            Rows.Add(row);
            while (Rows.Count > MaxRows) Rows.RemoveAt(0);

            Added++;
            if (row.Severity > LoudestAdded) LoudestAdded = row.Severity;
        }

        /// <summary>Drop every row that has had its time.</summary>
        void Expire(double seconds)
        {
            for (int i = Rows.Count - 1; i >= 0; i--)
            {
                // Guarded both ways: a clock that went backwards — a reloaded session, a rig whose
                // unscaled time restarted — would otherwise leave a row up for ever rather than
                // clearing it, and a stuck toast is worse than a missed one.
                double age = seconds - Rows[i].Raised;
                if (age >= LifetimeSeconds || age < 0.0) Rows.RemoveAt(i);
            }
        }

        /// <summary>
        /// Forget everything, for a world going away.
        ///
        /// <para><b>The watch is cleared too, and that is the point of this method.</b> The rows
        /// would drain by themselves in six seconds; the levels the watch is holding would not,
        /// because nothing steps it between two colonies. A colonist in the next one carries the
        /// same <c>PawnId</c> as somebody in the last, so a kept mark makes a stranger's starting
        /// roll read as a rise she just earned.</para>
        /// </summary>
        public void Clear()
        {
            Rows.Clear();
            _levels.Clear();
            Added = 0;
            LoudestAdded = AlertSeverity.Notice;
        }
    }
}
