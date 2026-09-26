#nullable enable
using System;

namespace Odyssey.Hud
{
    /// <summary>
    /// The open colony's storyteller and difficulty, and the tension gauge's preview (design 59
    /// §12).
    ///
    /// <para><b>Interface state only, until the storyteller is built.</b> The owner asked for the
    /// screens first (2026-09-26, mockups 25a–25h), so the choice is carried from the New game page
    /// into this session and edited in Settings, but it is not saved, not hashed and nothing in the
    /// simulation reads it: the Research tab's precedent (design 34). When ST1 lands the choice
    /// becomes colony state changed by intent and this becomes a read of the snapshot; the
    /// presenters read <see cref="Choice"/> and do not change. Until then a loaded save has
    /// <see cref="StoryChoice.None"/>, which is design 59 §7's old-save rule anyway.</para>
    ///
    /// <para><b>The gauge's band is a debug preview</b> for the same reason: nothing drives tension
    /// until ST3. The debug menu's Events tab picks a band and a cause, so all five can be looked
    /// at. The gauge still shows only with a storyteller chosen, which is the rule it keeps.</para>
    ///
    /// <para>Session state, built fresh with every <see cref="HudDirectors"/>. Unity-free.</para>
    /// </summary>
    public sealed class StoryDirector
    {
        public StoryChoice Choice { get; private set; } = StoryChoice.Nobody;

        /// <summary>Whether a colony is open. The Story rows in Settings are live only then.</summary>
        public bool HasColony { get; private set; }

        /// <summary>Raised when the storyteller, the rung or a lever changes.</summary>
        public event Action? Changed;

        /// <summary>A new colony's choice, from its New game page; or none, for a load.</summary>
        public void Begin(StoryChoice choice)
        {
            HasColony = true;
            Set(choice);
        }

        /// <summary>No colony open: the rows grey and the gauge goes.</summary>
        public void End()
        {
            HasColony = false;
            Set(StoryChoice.Nobody);
            SetTensionPreview(TensionModel.NoBand);
        }

        public bool ChooseStoryteller(int teller) => HasColony && Set(Choice.WithTeller(teller));
        public bool ChooseRung(int rung) => HasColony && Set(Choice.WithRung(rung));
        public bool SetThreat(int percent) => HasColony && Set(Choice.WithThreat(percent));
        public bool SetBigThreats(bool on) => HasColony && Set(Choice.WithBigThreats(on));
        public bool SetAdaptation(int percent) => HasColony && Set(Choice.WithAdaptation(percent));
        public bool SetGrace(int hundredths) => HasColony && Set(Choice.WithGrace(hundredths));

        bool Set(StoryChoice next)
        {
            if (next.Equals(Choice)) return false;
            Choice = next;
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ the gauge preview

        /// <summary>The band the debug menu is previewing, or <see cref="TensionModel.NoBand"/>.</summary>
        public int TensionPreview { get; private set; } = TensionModel.NoBand;

        public TensionCause PreviewCause { get; private set; } = TensionCause.None;

        /// <summary>How many days ago the cause was, or how many quiet days: 3 is enough to read.</summary>
        public const int PreviewDays = 3;

        public event Action? TensionChanged;

        /// <summary>Whether the clock draws the gauge: a storyteller, and a band to show.</summary>
        public bool GaugeShows => Choice.HasTeller && TensionModel.IsBand(TensionPreview);

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
