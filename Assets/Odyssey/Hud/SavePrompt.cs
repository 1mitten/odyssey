#nullable enable
using System;

namespace Odyssey.Hud
{
    /// <summary>
    /// What the presenter has found out about the name currently typed.
    ///
    /// <para>Three states rather than two bools, because two bools can be passed the wrong way
    /// round and three named states cannot, and because the pair <c>(unusable, collides)</c> has a
    /// fourth combination that means nothing — a name that cannot become a file cannot collide with
    /// one.</para>
    /// </summary>
    public enum SaveNameStatus
    {
        /// <summary>
        /// Nothing survives slugging: empty, whitespace, all punctuation, or written wholly in a
        /// script the file name cannot carry. There is no file at the end of this name, so it
        /// cannot be confirmed at all.
        /// </summary>
        Unusable,

        /// <summary>A legal name no file in the folder holds. One press saves.</summary>
        Free,

        /// <summary>A legal name an existing file holds. Saving means overwriting, so it asks
        /// twice.</summary>
        Taken,
    }

    /// <summary>
    /// The naming prompt: whether it is up, the name being typed, whether that name would land on a
    /// save that already exists, and the ask-twice arming that guards the overwrite.
    ///
    /// <para><b>Why it exists.</b> The owner played U38 and reported: <i>"I notice you keep saving a
    /// new game everytime. We should be able to name the save game (with a default) and then can
    /// overwrite that save if need be - otherwise lots of saves will be created."</i>
    /// `17-start-flow.md` §10 had already admitted the same thing — save always wrote a new file,
    /// so a folder was a history and only grew. Overwriting needs a name the player chose, and a
    /// name the player chose needs somewhere to be typed and a question to be asked before it
    /// destroys something. This is that, minus the drawing.</para>
    ///
    /// <para><b>The collision is told to it, not discovered by it.</b> This assembly is compiled
    /// without UnityEngine and without <c>Odyssey.Sim</c> (ADR 0003, and the Hud project references
    /// only <c>Odyssey.Sim.Contracts</c>), so it can see neither the saves folder nor
    /// <c>SaveCatalogue</c>, which is what turns a typed name into a file name. The presenter can
    /// see both, and it already holds the text field, so it knows the name before this type does —
    /// which is why <see cref="Type"/> takes the verdict alongside the name rather than the name
    /// alone. One call, so there is no window in which the prompt holds a name nobody has checked
    /// and no order of two calls to get wrong. It is the same bargain
    /// <see cref="MenuDirector.ShowSaves"/> makes with the load listing, for the same reason: what
    /// is on screen and what it means arrive together.</para>
    ///
    /// <para><b>It raises and never performs</b>, like every director here. Writing a file needs a
    /// filesystem; "the prompt asked for this name" is a sentence the fast tier can assert without
    /// one.</para>
    ///
    /// <para><b>Which of Save and Save as… put it up is not decided here.</b> Save writes over the
    /// file the session is bound to and only asks when there is nothing to bind to; Save as… always
    /// asks. Both of those are facts about the session, which lives in the presenter — so the
    /// presenter decides whether to show this at all, and this type's whole subject is what happens
    /// once it is up.</para>
    ///
    /// <para>Unity-free by construction (ADR 0003): everything here runs in the fast tier.</para>
    /// </summary>
    public sealed class SavePrompt
    {
        /// <summary>The registry key naming the prompt itself — its title, and the label on the
        /// text field.</summary>
        public const string TitleKey = "ui.prompt.savename";

        /// <summary>The registry key naming the confirm button once it is armed. The word changes
        /// from Save to Overwrite because the act has changed, and a button that says the same
        /// thing before and after the question is a button that did not ask one.</summary>
        public const string OverwriteKey = "ui.prompt.overwrite";

        /// <summary>The registry key naming the way out.</summary>
        public const string CancelKey = "ui.prompt.cancel";

        /// <summary>
        /// The registry key naming the confirm button in its ordinary state. Deliberately the
        /// session table's own Save key rather than a near-duplicate <c>ui.prompt.save</c>, for the
        /// reason <see cref="SessionCommands.OptionsKey"/> is the settings panel's key: one thing,
        /// one word, one row in the naming CSV for the owner to correct.
        /// </summary>
        public const string ConfirmKey = SessionCommands.SaveKey;

        /// <summary>
        /// Every key this prompt can put on screen, so <c>RegistryTests</c> can hold it to the
        /// naming CSV the way it already holds the settings panel and the session table. A label
        /// invented in C# is a label the owner cannot correct.
        /// </summary>
        public static readonly string[] IconKeys =
        {
            TitleKey,
            ConfirmKey,
            OverwriteKey,
            CancelKey,
        };

        /// <summary>Whether the prompt is up. It is modal while it is: the presenter draws nothing
        /// else clickable behind it.</summary>
        public bool Showing { get; private set; }

        /// <summary>The name as it currently stands, offered or typed. Never null; an empty string
        /// is a real state, and it is one <see cref="CanConfirm"/> refuses.</summary>
        public string Name { get; private set; } = string.Empty;

        /// <summary>What the presenter last said about <see cref="Name"/>.</summary>
        public SaveNameStatus Status { get; private set; } = SaveNameStatus.Unusable;

        /// <summary>Whether this name would land on a save that already exists.</summary>
        public bool Collides => Status == SaveNameStatus.Taken;

        /// <summary>
        /// Whether the confirm button does anything at all.
        ///
        /// <para><b>This is how the presenter learns to grey the button.</b> An unusable name must
        /// not merely fail to save — a press that silently does nothing reads as a broken button,
        /// and the player's next move is to press it harder rather than to change the name. So the
        /// state is published, the button is drawn disabled from it, and <see cref="Confirm"/>
        /// refuses as well, because a rule enforced only by whoever draws it is a rule the next
        /// caller does not have.</para>
        /// </summary>
        public bool CanConfirm => Showing && Status != SaveNameStatus.Unusable;

        /// <summary>
        /// Whether the overwrite has been asked once and is waiting for the second press.
        ///
        /// <para>Only a <see cref="SaveNameStatus.Taken"/> name ever arms: a free name destroys
        /// nothing, and asking "are you sure?" about it would teach the player to press twice
        /// without reading, which is exactly the habit that makes the real question useless.</para>
        /// </summary>
        public bool Armed { get; private set; }

        /// <summary>The registry key whose label belongs on the confirm button right now: Save
        /// ordinarily, Overwrite once the destructive press has been asked for.</summary>
        public string ActionKey => Armed ? OverwriteKey : ConfirmKey;

        /// <summary>
        /// Raised whenever anything the prompt draws has changed — it appearing or going away, the
        /// name, the verdict on the name, the arming.
        ///
        /// <para>One event rather than four because the prompt is one small box with four things in
        /// it, and every listener there will ever be redraws the lot. Four events would be four
        /// subscriptions to write, three of which fire in the same call as the first.</para>
        /// </summary>
        public event Action? Changed;

        /// <summary>
        /// Raised when the player has settled on a name, with the name they settled on — once for a
        /// free name, and only on the second press for one that overwrites. The presenter writes
        /// the file; see the type's note on raising rather than performing.
        /// </summary>
        public event Action<string>? Confirmed;

        /// <summary>
        /// Put the prompt up with a name already in it, and the presenter's verdict on that name.
        ///
        /// <para>Prefilled rather than empty because the default is the whole point of the owner's
        /// ask: the common case is a player who wants to keep saving over the same file and should
        /// have to type nothing to do it. A suggestion may perfectly well collide — it usually will
        /// from the second save onwards — so the status is asked for here exactly as it is on every
        /// keystroke.</para>
        ///
        /// <para>Showing it again re-seats it: new name, new verdict, nothing armed. A prompt that
        /// came back holding the previous session's armed overwrite would be a trap set by a box
        /// that is not even on screen.</para>
        /// </summary>
        public void Show(string? suggested, SaveNameStatus status)
        {
            Showing = true;
            Name = suggested ?? string.Empty;
            Status = status;
            Armed = false;
            Changed?.Invoke();
        }

        /// <summary>
        /// The name changed, and here is what the presenter makes of the new one.
        ///
        /// <para><b>Any change stands the arming down.</b> The armed state is an answer to one
        /// question — "overwrite <i>this</i> file?" — and the file is decided by the name. Leaving
        /// it armed across an edit would let the second press answer a question about a file the
        /// player is no longer naming, which is the one failure this whole prompt exists to
        /// prevent. The verdict counts as part of the change for the same reason: a name that was
        /// taken a moment ago and is free now is not the same question, even spelled the same
        /// way.</para>
        ///
        /// <para>A call that changes neither is a no-op, so a presenter that echoes every keystroke
        /// — including the ones that do not alter the field — neither disarms nor redraws.</para>
        ///
        /// <para>Ignored while the prompt is away: a keystroke arriving then is a late echo from a
        /// box the player has already dismissed, and obeying it would leave a name and a verdict
        /// waiting behind the next <see cref="Show"/>.</para>
        /// </summary>
        public void Type(string? name, SaveNameStatus status)
        {
            if (!Showing) return;

            string typed = name ?? string.Empty;
            if (typed == Name && status == Status) return;

            Name = typed;
            Status = status;
            Armed = false;
            Changed?.Invoke();
        }

        /// <summary>
        /// Press the confirm button.
        ///
        /// <para>Returns true when the press saved, false when it did not — because the prompt is
        /// away, because the name cannot become a file, or because it was the first of the two
        /// presses an overwrite wants. A presenter that redraws on <see cref="Changed"/> needs no
        /// other answer, and it is the same shape <see cref="MenuDirector.Choose"/> already
        /// has.</para>
        ///
        /// <para><b>A free name saves on one press.</b> Making every save ask twice would be the
        /// same mistake as never asking: the question stops carrying information the moment it is
        /// asked about something harmless.</para>
        ///
        /// <para>The prompt closes <i>before</i> <see cref="Confirmed"/> is raised, so the presenter
        /// writing the file is never doing so with a modal still up over the screen it is about to
        /// redraw.</para>
        /// </summary>
        public bool Confirm()
        {
            if (!CanConfirm) return false;

            if (Collides && !Armed)
            {
                Armed = true;
                Changed?.Invoke();
                return false;
            }

            string name = Name;
            Close();
            Changed?.Invoke();
            Confirmed?.Invoke(name);
            return true;
        }

        /// <summary>
        /// Back out. Nothing is written, nothing stays armed, and the name is forgotten — the next
        /// <see cref="Show"/> brings its own, which for a bound session is the name it is bound to
        /// rather than whatever was half-typed when the player changed their mind.
        ///
        /// <para>Where Escape lands on this is `09-ui-and-input.md` §6's order and
        /// <see cref="SettingsDirector.Escape(bool, bool, bool)"/>'s business, not this type's; a
        /// modal is the top of that order and Escape reaches it here.</para>
        /// </summary>
        public void Cancel()
        {
            if (!Showing) return;
            Close();
            Changed?.Invoke();
        }

        void Close()
        {
            Showing = false;
            Name = string.Empty;
            Status = SaveNameStatus.Unusable;
            Armed = false;
        }
    }
}
