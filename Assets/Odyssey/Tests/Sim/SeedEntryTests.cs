#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The seed a new game starts from: drawn, written out, typed back in.
    ///
    /// <para>These are the rules a start screen's seed field will rest on (U39), pinned here
    /// rather than there because a text field is never testable in a tier with no Unity in it,
    /// and because the rules are about what a seed <i>is</i> rather than about how it is shown.
    /// The screen itself is blocked on the menu shell (U38) and the v2 save header (U36); this is
    /// the half of U39 that stands on its own.</para>
    /// </summary>
    public class SeedEntryTests
    {
        // A spread rather than a handful of small numbers: the two ends of the range, the byte and
        // short boundaries either side, and the ten-digit case that fills the field.
        static readonly uint[] Spread =
        {
            0u, 1u, 7u, 42u, 255u, 256u, 65_535u, 65_536u,
            1_000_000u, 2_147_483_647u, 2_147_483_648u, 4_294_967_294u, uint.MaxValue,
        };

        [Test]
        public void AFormattedSeedReadsBackAsItself()
        {
            // The whole contract of a seed you can read and type: the number on the screen and
            // the number the world is built from are the same number.
            foreach (uint seed in Spread)
            {
                Assert.That(SeedEntry.TryParse(SeedEntry.Format(seed), out uint read), Is.True,
                    $"a seed this code formatted itself was refused: {seed}");
                Assert.That(read, Is.EqualTo(seed));
            }
        }

        [Test]
        public void TheRoundTripHoldsAcrossTheWholeRangeAndNotOnlyAtItsEdges()
        {
            // Deterministic draws, so a failure here is a failure every run rather than a rumour.
            var rng = new DeterministicRandom(0xC0FFEEu);
            for (int i = 0; i < 2_000; i++)
            {
                uint seed = rng.NextUInt();
                Assert.That(SeedEntry.TryParse(SeedEntry.Format(seed), out uint read), Is.True,
                    $"the round trip refused {seed}");
                Assert.That(read, Is.EqualTo(seed));
            }
        }

        [Test]
        public void ASeedIsWrittenAsPlainDecimalDigitsAndNothingElse()
        {
            // Pins the form itself, not just the round trip: a change of representation — hex, a
            // grouped number, a word code — has to be a deliberate edit to this test. Whatever a
            // player copies out of a log has to be what they can paste back in.
            Assert.That(SeedEntry.Format(0u), Is.EqualTo("0"));
            Assert.That(SeedEntry.Format(42u), Is.EqualTo("42"));
            Assert.That(SeedEntry.Format(3_829_174_463u), Is.EqualTo("3829174463"));
            Assert.That(SeedEntry.Format(uint.MaxValue), Is.EqualTo("4294967295"));
        }

        [Test]
        public void TheWidestSeedFitsTheWidthTheFieldIsToldToExpect()
        {
            // MaxDigits exists so a start screen can size its field. It is the sort of constant
            // that goes stale silently, so it is derived from the type here rather than trusted.
            Assert.That(SeedEntry.Format(uint.MaxValue).Length, Is.EqualTo(SeedEntry.MaxDigits));
        }

        [Test]
        public void ZeroIsAnOrdinarySeedRatherThanAnAliasForAnother()
        {
            // The reason nothing here special-cases zero, asserted rather than assumed. The
            // generator itself maps a state of 0 to 1, so a seed used raw would make 0 and 1 the
            // same world — but every consumer reaches its stream through ForTick, which mixes
            // first. If that ever stopped being true, a start screen showing "0" would be lying
            // about which world it was about to build.
            for (int tick = 0; tick < 8; tick++)
            {
                DeterministicRandom fromZero = DeterministicRandom.ForTick(0u, tick);
                DeterministicRandom fromOne = DeterministicRandom.ForTick(1u, tick);
                Assert.That(fromZero.State, Is.Not.EqualTo(fromOne.State),
                    $"seed 0 and seed 1 gave the same stream at tick {tick}");
            }
        }

        // ---- reroll ----------------------------------------------------------------------

        /// <summary>Hands out a fixed script of draws, so a draw's rules can be tested exactly.</summary>
        sealed class ScriptedEntropy
        {
            readonly uint[] _values;
            public int Calls;

            public ScriptedEntropy(params uint[] values) { _values = values; }

            // Repeats the last value forever once the script runs out, so a test can say "and
            // then it never changes again" without writing the tail out.
            public uint Next()
            {
                uint value = _values[Calls < _values.Length ? Calls : _values.Length - 1];
                Calls++;
                return value;
            }
        }

        [Test]
        public void ADrawWithNothingToAvoidTakesTheFirstNumberItIsGiven()
        {
            var entropy = new ScriptedEntropy(99u, 100u);

            Assert.That(SeedEntry.Draw(entropy.Next, avoid: null), Is.EqualTo(99u));
            Assert.That(entropy.Calls, Is.EqualTo(1), "a draw with no constraint drew twice");
        }

        [Test]
        public void ARerollSkipsPastTheSeedItIsReplacing()
        {
            // A reroll button that lands back on the number already in the field reads as a
            // broken button, and the player cannot tell the difference between that and one in
            // four billion.
            var entropy = new ScriptedEntropy(7u, 7u, 7u, 8u);

            Assert.That(SeedEntry.Draw(entropy.Next, avoid: 7u), Is.EqualTo(8u));
            Assert.That(entropy.Calls, Is.EqualTo(4));
        }

        [Test]
        public void ARerollAgainstASourceThatNeverChangesStillReturnsSomethingElse()
        {
            // The bound on the retry, and why it is there: a source stuck on one value must fail
            // the caller's expectation loudly-by-being-correct rather than hang the game. The
            // guarantee is absolute, not merely likely.
            var entropy = new ScriptedEntropy(7u);

            uint drawn = SeedEntry.Draw(entropy.Next, avoid: 7u);

            Assert.That(drawn, Is.Not.EqualTo(7u), "a reroll handed back the seed it was replacing");
            Assert.That(entropy.Calls, Is.LessThanOrEqualTo(8), "the retry is not bounded");
        }

        [Test]
        public void ARerollOffTheTopOfTheRangeWrapsRatherThanOverflowing()
        {
            // The fallback adds one, and the seed above uint.MaxValue is 0. Unchecked arithmetic
            // makes that a wrap; checked arithmetic would make it a crash on the one seed a
            // player is most likely to type in to see what happens.
            var entropy = new ScriptedEntropy(uint.MaxValue);

            uint drawn = SeedEntry.Draw(entropy.Next, avoid: uint.MaxValue);

            Assert.That(drawn, Is.EqualTo(0u));
        }

        [Test]
        public void TheMachineDealsADifferentSeedEachTimeItIsAsked()
        {
            // Catches the two ways a live entropy source goes wrong: constant, and quantised to a
            // clock so that calls in the same millisecond agree. Four collisions are tolerated
            // because the assertion has to be about the source and never about luck — over 2^32
            // even one collision in 256 draws is a one-in-130,000 event, so four is unreachable
            // by chance and still fails instantly on a source that repeats.
            var seen = new HashSet<uint>();
            for (int i = 0; i < 256; i++) seen.Add(SeedEntry.Draw());

            Assert.That(seen.Count, Is.GreaterThanOrEqualTo(252),
                "the machine's seeds repeat — the source is constant or clock-quantised");
        }

        [Test]
        public void ADealtSeedUsesTheWholeRangeRatherThanACornerOfIt()
        {
            // The claim MachineEntropy's comment makes, tested: a GUID carries six fixed version
            // and variant bits, so folding it badly — or truncating it — would leave bits that
            // never move and quietly narrow the worlds a player can ever be dealt. Over 256 draws
            // a genuinely uniform bit is stuck by chance with probability 2^-255, so this cannot
            // flake; a stuck bit fails it every run.
            uint everSet = 0u;
            uint everClear = 0u;
            for (int i = 0; i < 256; i++)
            {
                uint seed = SeedEntry.Draw();
                everSet |= seed;
                everClear |= ~seed;
            }

            Assert.That(everSet, Is.EqualTo(uint.MaxValue), "some bit of a dealt seed is never set");
            Assert.That(everClear, Is.EqualTo(uint.MaxValue), "some bit of a dealt seed is never clear");
        }

        [Test]
        public void ARerollFromTheLiveSourceStillHonoursTheGuarantee()
        {
            // The parameterless entry point is what the screen will call, so it is worth one test
            // of its own rather than only the seam beneath it.
            uint current = SeedEntry.Draw();
            for (int i = 0; i < 64; i++)
            {
                uint next = SeedEntry.Reroll(current);
                Assert.That(next, Is.Not.EqualTo(current));
                current = next;
            }
        }

        // ---- what the player types -------------------------------------------------------

        [Test]
        public void SurroundingWhitespaceAndGroupingAreIgnored()
        {
            // A pasted seed arrives grouped as often as not — out of a document, a spreadsheet or
            // a line of code — and the separators mean nothing to the number.
            var forms = new[]
            {
                "3829174463", " 3829174463 ", "\t3829174463\n",
                "3 829 174 463", "3,829,174,463", "3_829_174_463",
            };

            foreach (string form in forms)
            {
                Assert.That(SeedEntry.TryParse(form, out uint seed), Is.True, $"refused: '{form}'");
                Assert.That(seed, Is.EqualTo(3_829_174_463u), $"misread: '{form}'");
            }
        }

        [Test]
        public void LeadingZerosAreReadRatherThanRefused()
        {
            // Nothing in the parser counts digits, so a padded seed is the seed it pads. The
            // alternative — a width rule — would refuse a number that is perfectly well formed.
            Assert.That(SeedEntry.TryParse("0000000042", out uint seed), Is.True);
            Assert.That(seed, Is.EqualTo(42u));
        }

        [Test]
        public void WhatIsNotASeedIsRefusedRatherThanGuessedAt()
        {
            // Every one of these is something a player could plausibly leave in the field, and
            // every one of them has a wrong answer that would be worse than a refusal: "-1" read
            // as 1 or as uint.MaxValue, "12.5" read as 12, an empty field read as seed 0 and
            // silently starting a world nobody chose.
            var notSeeds = new string?[]
            {
                null, "", "   ", "-1", "+1", "12.5", "1e3", "meadow", "0x1f", "#42",
                "4294967296",           // one past the top of the range
                "9999999999",           // ten digits, still over the top
                "99999999999999999999", // far over, and a different code path in TryParse
                "4 2 x",
            };

            foreach (string? text in notSeeds)
            {
                Assert.That(SeedEntry.TryParse(text, out uint seed), Is.False,
                    $"accepted something that is not a seed: '{text ?? "<null>"}'");
                Assert.That(seed, Is.Zero,
                    $"a refused seed left a usable number behind: '{text ?? "<null>"}'");
            }
        }

        [Test]
        public void AnUnboundedPasteIsRefusedOnItsLengthBeforeAnythingIsAllocatedToCleanIt()
        {
            // The guard is about the size of a paste, not about the value: forty zeros in front
            // of a 42 is a number the parser would otherwise read quite happily. A seed field can
            // receive an entire document, and that is not a reason to build a copy of one.
            string padded = new string('0', 40) + "42";

            Assert.That(SeedEntry.TryParse(padded, out uint seed), Is.False);
            Assert.That(seed, Is.Zero);

            // And the bound is generous enough that every honest form gets through: ten digits
            // grouped in threes is thirteen characters.
            Assert.That(SeedEntry.TryParse("4,294,967,295", out uint widest), Is.True);
            Assert.That(widest, Is.EqualTo(uint.MaxValue));
        }

        [Test]
        public void ATypedSeedSurvivesBeingShownBackToThePlayer()
        {
            // The loop a field actually runs: the player types, it is read, and it is written
            // back into the same field as the canonical form. That form has to parse again, or
            // the field drifts every time it is touched.
            Assert.That(SeedEntry.TryParse(" 3,829,174,463 ", out uint first), Is.True);
            string shown = SeedEntry.Format(first);

            Assert.That(SeedEntry.TryParse(shown, out uint second), Is.True);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(SeedEntry.Format(second), Is.EqualTo(shown));
        }
    }
}
