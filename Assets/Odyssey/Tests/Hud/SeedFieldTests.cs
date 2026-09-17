#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The seed as the New game screen holds it (U39): what is in the box, what number that names,
    /// and whether Start is allowed to act on it.
    ///
    /// <para><b>The rule worth the whole file</b> is that the field and the world may never
    /// disagree. <c>SeedEntry.TryParse</c> was written to refuse rather than guess, and the way that
    /// refusal reaches a player is here: a box that does not name a seed makes Start do nothing,
    /// rather than making Start build a world from some other number the player never saw.</para>
    ///
    /// <para>The randomness is handed in throughout, which is what lets the reroll — the one
    /// deliberately non-deterministic thing in the simulation assemblies — be asserted on at all.
    /// </para>
    /// </summary>
    public class SeedFieldTests
    {
        /// <summary>A draw that deals the given numbers in order and then repeats the last, so a
        /// test says exactly what the machine "rolled" and never runs dry.</summary>
        static System.Func<uint> Deals(params uint[] numbers)
        {
            int next = 0;
            return () => numbers[next < numbers.Length ? next++ : numbers.Length - 1];
        }

        static SeedField WithDraws(params uint[] numbers) => new SeedField(Deals(numbers));

        // ------------------------------------------------------------------ what is in the box

        [Test]
        public void ItOpensOnADrawnSeed()
        {
            var field = WithDraws(4242u);
            field.Draw();

            Assert.That(field.Seed, Is.EqualTo(4242u));
            Assert.That(field.Text, Is.EqualTo("4242"), "the box shows the seed it is holding");
            Assert.That(field.Usable, Is.True);
        }

        [Test]
        public void TheTextIsWhatTheRegistryWouldPrint()
        {
            var field = WithDraws(3829174463u);
            field.Draw();

            Assert.That(field.Text, Is.EqualTo(SeedEntry.Format(3829174463u)),
                "the screen must not format a seed its own way — a number copied out of a log has " +
                "to be a number that can be typed back in");
        }

        [Test]
        public void TypingANumberChangesTheSeed()
        {
            var field = WithDraws(1u);
            field.Draw();

            field.Type("77");

            Assert.That(field.Seed, Is.EqualTo(77u));
            Assert.That(field.Usable, Is.True);
        }

        [Test]
        public void ItIsForgivingAboutHowANumberIsWritten()
        {
            var field = WithDraws(1u);
            field.Draw();

            field.Type(" 3,829,174,463 ");

            Assert.That(field.Seed, Is.EqualTo(3829174463u),
                "a seed pasted out of a spreadsheet is still that seed");
            Assert.That(field.Text, Is.EqualTo(" 3,829,174,463 "),
                "and what the player typed stays in the box, because a field that rewrites itself " +
                "under the cursor cannot be typed in");
        }

        // ------------------------------------------------------------------ what it refuses

        [Test]
        public void TextThatNamesNoSeedCannotStartAWorld()
        {
            var field = WithDraws(4242u);
            field.Draw();

            field.Type("twelve");

            Assert.That(field.Usable, Is.False);
            Assert.That(field.Text, Is.EqualTo("twelve"));
        }

        /// <summary>
        /// The claim the unit exists for, stated as a test rather than as a comment: while the box
        /// says something that is not a seed, the seed it last knew is <b>not</b> quietly available
        /// to be built from.
        /// </summary>
        [Test]
        public void AnUnreadableBoxDoesNotLeaveTheOldSeedStandingBehindIt()
        {
            var field = WithDraws(4242u);
            field.Draw();
            Assert.That(field.Seed, Is.EqualTo(4242u));

            field.Type("twelve");

            Assert.That(field.Usable, Is.False,
                "if this were true the screen would build 4242 while showing 'twelve'");
        }

        [Test]
        public void AnEmptyBoxIsNotASeed()
        {
            var field = WithDraws(4242u);
            field.Draw();

            field.Type("");

            Assert.That(field.Usable, Is.False, "an empty field is a player mid-edit, not seed zero");
        }

        [Test]
        public void ZeroIsAnOrdinarySeed()
        {
            var field = WithDraws(4242u);
            field.Draw();

            field.Type("0");

            Assert.That(field.Usable, Is.True);
            Assert.That(field.Seed, Is.EqualTo(0u),
                "every consumer mixes the seed before the generator's state==0 guard sees it, so " +
                "zero is a world like any other and the field may show it honestly");
        }

        [Test]
        public void ANumberTooBigForASeedIsRefusedRatherThanWrapped()
        {
            var field = WithDraws(4242u);
            field.Draw();

            field.Type("4294967296");

            Assert.That(field.Usable, Is.False, "one past uint.MaxValue is not a seed");
        }

        // ------------------------------------------------------------------ reroll

        [Test]
        public void RerollingTakesTheNextNumber()
        {
            var field = WithDraws(1u, 2u);
            field.Draw();

            field.Reroll();

            Assert.That(field.Seed, Is.EqualTo(2u));
            Assert.That(field.Text, Is.EqualTo("2"));
        }

        /// <summary>
        /// A reroll that lands on the number already in the box reads as a broken button, and the
        /// player cannot see that it was chance rather than a bug. <see cref="SeedEntry.Draw"/>
        /// carries the guarantee; this proves the screen actually asks for it.
        /// </summary>
        [Test]
        public void RerollingNeverLandsOnTheSeedItStartedFrom()
        {
            var field = WithDraws(7u, 7u, 7u, 7u, 7u, 7u, 7u, 7u, 7u, 7u);
            field.Draw();

            field.Reroll();

            Assert.That(field.Seed, Is.Not.EqualTo(7u),
                "a machine that deals the same number forever must still move the field");
            Assert.That(field.Usable, Is.True);
        }

        [Test]
        public void RerollingAfterTypingRubbishStillGivesAUsableSeed()
        {
            var field = WithDraws(1u, 2u);
            field.Draw();
            field.Type("twelve");

            field.Reroll();

            Assert.That(field.Usable, Is.True, "reroll is the way out of a box you have spoiled");
            Assert.That(field.Seed, Is.EqualTo(2u));
        }

        // ------------------------------------------------------------------ what it tells the screen

        [Test]
        public void EveryChangeIsAnnouncedOnce()
        {
            var field = WithDraws(1u, 2u);
            int changes = 0;
            field.Changed += () => changes++;

            field.Draw();
            Assert.That(changes, Is.EqualTo(1));

            field.Type("5");
            Assert.That(changes, Is.EqualTo(2));

            field.Reroll();
            Assert.That(changes, Is.EqualTo(3));
        }

        /// <summary>
        /// The presenter echoes every keystroke, including the ones that change nothing — a
        /// <c>SetValueWithoutNotify</c> that races a callback, say. Redrawing on those would be
        /// work for nothing; the naming prompt makes the same bargain for the same reason.
        /// </summary>
        [Test]
        public void TypingWhatIsAlreadyThereIsNotAChange()
        {
            var field = WithDraws(1u);
            field.Draw();

            int changes = 0;
            field.Changed += () => changes++;
            field.Type("1");

            Assert.That(changes, Is.Zero);
        }

        // ------------------------------------------------------------------ the real machine

        /// <summary>
        /// The parameterless constructor reaches <see cref="SeedEntry"/>'s own entropy, which is
        /// the one thing above that a hand-fed draw cannot check. Two fields must not open on the
        /// same world.
        /// </summary>
        [Test]
        public void TheRealDrawIsNotAConstant()
        {
            var first = new SeedField();
            var second = new SeedField();
            first.Draw();
            second.Draw();

            Assert.That(first.Usable, Is.True);
            Assert.That(second.Usable, Is.True);
            Assert.That(first.Seed, Is.Not.EqualTo(second.Seed),
                "one chance in 2^32 of a false failure, against a real chance of shipping a " +
                "start screen that deals one world forever");
        }
    }
}
