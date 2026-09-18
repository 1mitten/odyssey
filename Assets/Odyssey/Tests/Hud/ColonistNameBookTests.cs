#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// Naming a colonist yourself (owner, 2026-09-18): what a typed name may be, and what happens
    /// to the one the pool dealt when there is one.
    /// </summary>
    public class ColonistNameBookTests
    {
        [TearDown]
        public void ForgetNames() => ColonistNames.Book.Clear();

        // ------------------------------------------------------------------ what a name may be

        [Test]
        public void ANameIsTrimmedAndItsBlankRunsCollapsed()
        {
            Assert.That(ColonistNameBook.Clean("  Ada  "), Is.EqualTo("Ada"));
            Assert.That(ColonistNameBook.Clean("Ada   Vance"), Is.EqualTo("Ada Vance"));
            Assert.That(ColonistNameBook.Clean("\tAda\nVance "), Is.EqualTo("Ada Vance"));
        }

        [Test]
        public void ANameIsCutToSixteenCharacters()
        {
            Assert.That(ColonistNameBook.Clean("abcdefghijklmnopqrst"),
                Is.EqualTo("abcdefghijklmnop"));
            Assert.That(ColonistNameBook.Clean("abcdefghijklmnop").Length,
                Is.EqualTo(ColonistNameBook.MaxLength));
        }

        /// <summary>
        /// The cut comes after the trim, and cannot leave a space on the end. Sixteen characters
        /// of which the last is blank would otherwise be a fifteen-character name with a tail —
        /// invisible on screen and not equal to the same name typed the other way round.
        /// </summary>
        [Test]
        public void CuttingANameCannotLeaveASpaceOnTheEnd()
        {
            // Seventeen characters, so the cut lands on the space before the Z.
            Assert.That(ColonistNameBook.Clean("Ada Vance Wrenn X"), Is.EqualTo("Ada Vance Wrenn"));

            // And the collapse happens first, so a long run of blank is not what gets cut: this
            // is five characters by the time the length is looked at, and survives whole.
            Assert.That(ColonistNameBook.Clean("Ada             Z"), Is.EqualTo("Ada Z"));
        }

        [Test]
        public void ANameOfNothingButBlankIsNoName()
        {
            Assert.That(ColonistNameBook.Clean("   "), Is.Empty);
            Assert.That(ColonistNameBook.Clean(null), Is.Empty);
            Assert.That(ColonistNameBook.Clean(""), Is.Empty);
        }

        // ------------------------------------------------------------------ the book

        [Test]
        public void ANamedColonistAnswersToTheirName()
        {
            var book = new ColonistNameBook();

            Assert.That(book.Rename(new PawnId(2), "Ada"), Is.True);
            Assert.That(book.Given(new PawnId(2)), Is.EqualTo("Ada"));
            Assert.That(book.Given(new PawnId(1)), Is.Null, "nobody else was named");
        }

        [Test]
        public void EmptyingTheBoxPutsTheDealtNameBack()
        {
            var book = new ColonistNameBook();
            book.Rename(new PawnId(2), "Ada");

            Assert.That(book.Rename(new PawnId(2), "   "), Is.True, "clearing is a change");
            Assert.That(book.Given(new PawnId(2)), Is.Null);
            Assert.That(book.Count, Is.Zero, "a cleared name should not sit in the book as empty");
        }

        [Test]
        public void RenamingToTheSameNameChangesNothing()
        {
            var book = new ColonistNameBook();
            book.Rename(new PawnId(2), "Ada");

            Assert.That(book.Rename(new PawnId(2), " Ada "), Is.False,
                "the same name spelled with spaces is the same name");
            Assert.That(book.Rename(new PawnId(3), ""), Is.False, "clearing nothing is not a change");
        }

        // ------------------------------------------------------------------ and the pool

        [Test]
        public void ANameOverridesThePoolAndNothingElseIsTouched()
        {
            string dealt = ColonistNames.Of(1234u, new PawnId(1));
            string neighbour = ColonistNames.Of(1234u, new PawnId(2));

            ColonistNames.Book.Rename(new PawnId(1), "Ada");

            Assert.That(ColonistNames.Of(1234u, new PawnId(1)), Is.EqualTo("Ada"));
            Assert.That(ColonistNames.Of(1234u, new PawnId(2)), Is.EqualTo(neighbour),
                "naming one colonist renamed another");
            Assert.That(ColonistNames.Rolled(1234u, new PawnId(1)), Is.EqualTo(dealt),
                "the dealt name is still what the pool says, which is what the select screen reads");
        }

        /// <summary>
        /// The select screen deals candidates by <c>PawnId</c>, and slot 0's id is the same 1 the
        /// last colony's first colonist had. If the deal read the book, a fresh stranger would
        /// arrive wearing a name typed in a game that is already over.
        /// </summary>
        [Test]
        public void TheDealtNameIgnoresWhoeverWasNamedInTheLastColony()
        {
            ColonistNames.Book.Rename(new PawnId(1), "Ada");

            Assert.That(ColonistNames.Rolled(99u, new PawnId(1)), Is.Not.EqualTo("Ada"));
        }
    }
}
