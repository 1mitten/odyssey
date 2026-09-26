#nullable enable
using System;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The open colony's storyteller and difficulty, and the tension gauge (design 59 §7, §12).
    ///
    /// <para><b>A read of the simulation's <see cref="StorytellerView"/>, and a writer of
    /// intents.</b> The choice is colony state since ST1: saved, hashed and changed only by
    /// <see cref="IntentKind.SetStoryteller"/> and <see cref="IntentKind.SetDifficulty"/>. A press
    /// in Settings builds the next <see cref="StoryChoice"/> by the one rule that says what a press
    /// means, submits it through <see cref="Submit"/>, and shows it at once; <see cref="Sync"/>
    /// then takes whatever the view says, so a refused intent puts the row back. The presenters
    /// read <see cref="Choice"/> and did not change when the state moved into the simulation.</para>
    ///
    /// <para><b>The difficulty table stays here.</b> The intent carries the four lever values and
    /// the rung they came from, so the simulation stores numbers and never needs a copy of
    /// <see cref="StoryCatalogue.Rungs"/>.</para>
    ///
    /// <para><b>The gauge's band is the view's</b>, decided in the simulation. The debug menu can
    /// still preview a band and a cause over it, so all five can be looked at on a colony that is
    /// not living through them; the preview wins only while one is set.</para>
    ///
    /// <para>Session state, built fresh with every <see cref="HudDirectors"/>. Unity-free.</para>
    /// </summary>
    public sealed class StoryDirector
    {
        public StoryChoice Choice { get; private set; } = StoryChoice.Nobody;

        /// <summary>Whether a colony is open. The Story rows in Settings are live only then.</summary>
        public bool HasColony { get; private set; }

        /// <summary>Where a press sends its intent: the shell hands in the world's queue. Unset, a
        /// press still moves the row and the next <see cref="Sync"/> puts it back.</summary>
        public Action<Intent>? Submit { get; set; }

        /// <summary>Raised when the storyteller, the rung or a lever changes.</summary>
        public event Action? Changed;

        /// <summary>
        /// How many syncs a press is shown ahead of the view before the view is believed. An intent
        /// that may apply while paused is settled on the frame it is sent, and one sent at speed on
        /// the next tick, so the view normally agrees on the first sync; the rest is room for a
        /// slow frame. After that the press was refused and the row goes back.
        /// </summary>
        public const int PendingSyncs = 3;

        StoryChoice? _pending;
        int _pendingSyncs;

        /// <summary>
        /// A colony is open: a load, whose view says what its save holds. Nothing is submitted.
        /// </summary>
        public void Attach()
        {
            if (HasColony) return;
            HasColony = true;
            _noticeOwed = true;
        }

        /// <summary>
        /// A new colony with the choice from its New game page: both intents are submitted, since
        /// the simulation starts every colony with none (design 59 §7), and the choice is shown
        /// until the view has it.
        /// </summary>
        public void Begin(StoryChoice choice)
        {
            HasColony = true;
            _noticeOwed = false;
            if (choice.HasTeller) Send(TellerIntent(choice));
            Send(DifficultyIntent(choice));
            Show(choice);
        }

        /// <summary>No colony open: the rows grey and the gauge goes.</summary>
        public void End()
        {
            HasColony = false;
            _noticeOwed = false;
            _pending = null;
            Set(StoryChoice.Nobody);
            SetTension(TensionModel.NoBand, TensionCause.None, 0);
            SetTensionPreview(TensionModel.NoBand);
        }

        public bool ChooseStoryteller(int teller) => HasColony && Press(Choice.WithTeller(teller));
        public bool ChooseRung(int rung) => HasColony && Press(Choice.WithRung(rung));
        public bool SetThreat(int percent) => HasColony && Press(Choice.WithThreat(percent));
        public bool SetBigThreats(bool on) => HasColony && Press(Choice.WithBigThreats(on));
        public bool SetAdaptation(int percent) => HasColony && Press(Choice.WithAdaptation(percent));
        public bool SetGrace(int hundredths) => HasColony && Press(Choice.WithGrace(hundredths));

        /// <summary>
        /// Read the published storyteller: the choice, unless a press is still ahead of it, and the
        /// tension's band and cause. Called from the clock's refresh.
        /// </summary>
        public void Sync(in StorytellerView view)
        {
            StoryChoice seen = FromView(view);
            if (_pending is StoryChoice pending)
            {
                if (seen.Equals(pending) || ++_pendingSyncs > PendingSyncs) _pending = null;
            }
            if (_pending == null) Set(seen);

            SetTension(view.HasStoryteller ? view.Band : TensionModel.NoBand,
                (TensionCause)(int)view.Cause, view.CauseDays);

            // Said once per loaded colony, on the first sync that can know: an old save has no
            // storyteller and nothing will happen to it until one is chosen (design 59 §7).
            if (_noticeOwed)
            {
                _noticeOwed = false;
                if (!view.HasStoryteller) NoStorytellerNotice = true;
            }
        }

        bool Press(StoryChoice next)
        {
            if (next.Equals(Choice)) return false;
            if (next.Teller != Choice.Teller && next.HasTeller) Send(TellerIntent(next));
            if (!SameDifficulty(next, Choice)) Send(DifficultyIntent(next));
            Show(next);
            return true;
        }

        void Show(StoryChoice next)
        {
            _pending = next;
            _pendingSyncs = 0;
            Set(next);
        }

        void Send(Intent intent) => Submit?.Invoke(intent);

        bool Set(StoryChoice next)
        {
            if (next.Equals(Choice)) return false;
            Choice = next;
            Changed?.Invoke();
            return true;
        }

        static bool SameDifficulty(StoryChoice a, StoryChoice b) =>
            a.Rung == b.Rung && a.ThreatPercent == b.ThreatPercent && a.BigThreats == b.BigThreats
            && a.AdaptationPercent == b.AdaptationPercent && a.GraceHundredths == b.GraceHundredths;

        // ------------------------------------------------------------------ the intents

        /// <summary><see cref="IntentKind.SetStoryteller"/>: A is the storyteller.</summary>
        public static Intent TellerIntent(StoryChoice choice) =>
            new Intent(IntentKind.SetStoryteller, default, choice.Teller);

        /// <summary>
        /// <see cref="IntentKind.SetDifficulty"/>: A the rung, B the threat scale with the
        /// adaptation above it, C the grace stretch with big threats as bit 16.
        /// </summary>
        public static Intent DifficultyIntent(StoryChoice choice) =>
            new Intent(IntentKind.SetDifficulty, default, choice.Rung,
                choice.ThreatPercent | (choice.AdaptationPercent << 16),
                choice.GraceHundredths | (choice.BigThreats ? 1 << 16 : 0));

        /// <summary>The choice a view describes. No storyteller reads as <see cref="StoryChoice.None"/>.</summary>
        public static StoryChoice FromView(in StorytellerView view) =>
            new StoryChoice(view.HasStoryteller ? view.Storyteller : StoryChoice.None, view.Rung,
                view.ThreatPercent, view.BigThreats, view.AdaptationPercent, view.GraceHundredths);

        // ------------------------------------------------------------------ the old-save notice

        bool _noticeOwed;

        /// <summary>
        /// Set once when a loaded colony turns out to have no storyteller; the shell raises the
        /// toast (<see cref="ToastModel.NoStorytellerKey"/>) and clears it with
        /// <see cref="TakeNoStorytellerNotice"/>.
        /// </summary>
        public bool NoStorytellerNotice { get; private set; }

        public bool TakeNoStorytellerNotice()
        {
            bool owed = NoStorytellerNotice;
            NoStorytellerNotice = false;
            return owed;
        }

        // ------------------------------------------------------------------ the gauge

        /// <summary>The band the simulation publishes, or <see cref="TensionModel.NoBand"/>.</summary>
        public int TensionBand { get; private set; } = TensionModel.NoBand;

        public TensionCause TensionCause { get; private set; } = TensionCause.None;

        /// <summary>Days since the cause, or for a quiet run how many quiet days.</summary>
        public int CauseDays { get; private set; }

        /// <summary>The band the debug menu is previewing, or <see cref="TensionModel.NoBand"/>.</summary>
        public int TensionPreview { get; private set; } = TensionModel.NoBand;

        public TensionCause PreviewCause { get; private set; } = TensionCause.None;

        /// <summary>How many days ago a previewed cause was, or how many quiet days: 3 is enough to read.</summary>
        public const int PreviewDays = 3;

        public event Action? TensionChanged;

        /// <summary>The band the gauge draws: the preview while one is set, else the view's.</summary>
        public int ShownBand => TensionModel.IsBand(TensionPreview) ? TensionPreview : TensionBand;

        public TensionCause ShownCause => TensionModel.IsBand(TensionPreview) ? PreviewCause : TensionCause;

        public int ShownCauseDays => TensionModel.IsBand(TensionPreview) ? PreviewDays : CauseDays;

        /// <summary>Whether the clock draws the gauge: a storyteller, and a band to show.</summary>
        public bool GaugeShows => Choice.HasTeller && TensionModel.IsBand(ShownBand);

        void SetTension(int band, TensionCause cause, int days)
        {
            int next = TensionModel.IsBand(band) ? band : TensionModel.NoBand;
            if (next == TensionBand && cause == TensionCause && days == CauseDays) return;
            TensionBand = next;
            TensionCause = cause;
            CauseDays = days;
            TensionChanged?.Invoke();
        }

        public void SetTensionPreview(int band)
        {
            int next = TensionModel.IsBand(band) ? band : TensionModel.NoBand;
            if (next == TensionPreview) return;
            TensionPreview = next;
            TensionChanged?.Invoke();
        }

        /// <summary>Off, Reeling, Easing, Even, Building, Peak, Off: the debug row's step.</summary>
        public void NextTensionPreview() =>
            SetTensionPreview(TensionPreview + 1 >= TensionModel.BandCount ? TensionModel.NoBand : TensionPreview + 1);

        /// <summary>None, Died, Downed, Quiet, None.</summary>
        public void NextPreviewCause()
        {
            PreviewCause = (TensionCause)(((int)PreviewCause + 1) % 4);
            TensionChanged?.Invoke();
        }
    }
}
