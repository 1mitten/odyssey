#nullable enable
using System;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The seed as the New game screen holds it (U39): the text in the box, the number that text
    /// names, and whether it names one at all.
    ///
    /// <para><b>Why there is a type here rather than a few fields on the text control.</b>
    /// <see cref="SeedEntry"/> already owns what a seed is — drawn, formatted, parsed — and it lives
    /// in <c>Odyssey.Sim.Contracts</c> because the number is the world's, not the screen's. What was
    /// still missing was the little state machine *between* a keystroke and that number: what is
    /// showing, what it parses to, what Start is allowed to do with it. Written into a
    /// <c>RegisterValueChangedCallback</c> it would have been correct exactly as long as nobody
    /// touched it and provable never; here it is thirty lines the fast tier reads.</para>
    ///
    /// <para><b>It raises and never performs</b>, like every director in this assembly. Building a
    /// world needs the bootstrap; "the screen asked for seed 4242" is a sentence this tier can
    /// assert without one.</para>
    ///
    /// <para><b>The one rule that matters:</b> while <see cref="Usable"/> is false the screen must
    /// not start anything. <see cref="SeedEntry.TryParse"/> was deliberately written to refuse
    /// rather than guess, and this is where that refusal reaches a player — a box reading
    /// <c>twelve</c> over a world built from some other number is the single way this screen could
    /// lie about the only thing it exists to show. <see cref="Seed"/> is therefore meaningful only
    /// while <see cref="Usable"/> holds, the same bargain <see cref="SavePrompt.Name"/> and
    /// <see cref="SavePrompt.CanConfirm"/> already make with the presenter.</para>
    ///
    /// <para>Unity-free by construction (ADR 0003): everything here runs in the fast tier.</para>
    /// </summary>
    public sealed class SeedField
    {
        /// <summary>
        /// Where a fresh number comes from.
        ///
        /// <para>Defaulted to <see cref="SeedEntry.Draw()"/> itself — the machine draw is a
        /// <c>Func&lt;uint&gt;</c> already, so the real source and a test's hand-dealt one go
        /// through exactly the same path rather than through a production branch and a test branch
        /// that can come to differ.</para>
        /// </summary>
        readonly Func<uint> _entropy;

        /// <summary>The registry key naming the field itself — the word over the box.</summary>
        public const string SeedKey = "ui.newgame.seed";

        /// <summary>The registry key naming the row that deals another world.</summary>
        public const string RerollKey = "ui.newgame.reroll";

        /// <summary>
        /// The registry key naming the row that commits.
        ///
        /// <para><b>Not <see cref="SessionCommands.NewGameKey"/>, and that is deliberate</b> — the
        /// one place on this screen where a near-duplicate key is the right answer rather than the
        /// wrong one (§11.3). That key is on the root row and means *open this screen*; this means
        /// *build this world from this number*. One opens a question and the other answers it, and
        /// labelling both "New game" would put the same word twice in one navigation with no way to
        /// tell which press committed.</para>
        /// </summary>
        public const string StartKey = "ui.newgame.start";

        /// <summary>
        /// Every key this screen can put on screen, so <c>RegistryTests</c> can hold it to the
        /// naming CSV the way it already holds the session table and the naming prompt. A label
        /// invented in C# is a label the owner cannot correct.
        /// </summary>
        public static readonly string[] IconKeys =
        {
            SeedKey,
            RerollKey,
            StartKey,
        };

        public SeedField() : this(SeedEntry.Draw) { }

        /// <summary>The seam a test drives: the draw with its randomness handed in.</summary>
        public SeedField(Func<uint> entropy) =>
            _entropy = entropy ?? throw new ArgumentNullException(nameof(entropy));

        /// <summary>
        /// Exactly what is in the box, as the player left it — grouping, spaces and all.
        ///
        /// <para>Never rewritten to the canonical spelling while they are typing: a field that
        /// reformats itself under the cursor is a field that cannot be typed in. The cleaning is
        /// <see cref="SeedEntry.TryParse"/>'s business and it is done on the way *out*.</para>
        /// </summary>
        public string Text { get; private set; } = string.Empty;

        /// <summary>
        /// The number the box names. <b>Meaningful only while <see cref="Usable"/> is true</b> —
        /// see the type's note. Kept across an unreadable edit so <see cref="Reroll"/> has something
        /// to differ from, and for no other purpose.
        /// </summary>
        public uint Seed { get; private set; }

        /// <summary>Whether <see cref="Text"/> currently names a seed, and so whether the screen may
        /// act on it.</summary>
        public bool Usable { get; private set; }

        /// <summary>
        /// Raised whenever anything the screen draws has changed. One event rather than three,
        /// because there is one small box and every listener redraws all of it.
        /// </summary>
        public event Action? Changed;

        /// <summary>
        /// Open on a fresh world.
        ///
        /// <para>Called every time the screen is entered rather than once, by §11.2 decision 3:
        /// pressing New game twice and being dealt the same world both times reads as a reroll that
        /// does not work, and nothing on screen could tell the player otherwise.</para>
        /// </summary>
        public void Draw() => Take(SeedEntry.Draw(_entropy, avoid: null));

        /// <summary>
        /// Deal another one, guaranteed different from the seed the field is holding.
        ///
        /// <para>The guarantee is <see cref="SeedEntry.Draw"/>'s and is bounded there, so a machine
        /// that deals one number forever still moves the box instead of hanging. From an unreadable
        /// box this is also the way out: whatever was typed is replaced by a number that parses.
        /// </para>
        /// </summary>
        public void Reroll() => Take(SeedEntry.Draw(_entropy, avoid: Seed));

        /// <summary>
        /// The player typed. The verdict is taken here rather than asked for, because unlike a save
        /// name — which needs a folder nobody in this assembly can see — what counts as a seed is
        /// entirely <see cref="SeedEntry"/>'s and is on this side of the wall.
        ///
        /// <para>A call that changes nothing is a no-op, so a presenter echoing every keystroke,
        /// including the ones that alter no character, neither redraws nor disturbs the field.</para>
        /// </summary>
        public void Type(string? typed)
        {
            string text = typed ?? string.Empty;
            if (text == Text) return;

            Text = text;
            if (SeedEntry.TryParse(text, out uint seed))
            {
                Seed = seed;
                Usable = true;
            }
            else
            {
                // Seed is deliberately left standing: it is what Reroll differs from, and the
                // type's contract already says it means nothing while Usable is false.
                Usable = false;
            }

            Changed?.Invoke();
        }

        void Take(uint seed)
        {
            Seed = seed;
            Text = SeedEntry.Format(seed);
            Usable = true;
            Changed?.Invoke();
        }
    }
}
