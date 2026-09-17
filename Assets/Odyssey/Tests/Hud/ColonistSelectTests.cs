#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The three you take into the game (U40): who is on screen, which of them you kept, and what
    /// Start hands over.
    ///
    /// <para>The roll is handed in, because rolling needs <c>Odyssey.Sim</c> and this assembly is
    /// compiled without it — which is the architectural point rather than an inconvenience. Every
    /// rule below is about a screen of three and is provable without a world.</para>
    /// </summary>
    public class ColonistSelectTests
    {
        /// <summary>A machine that deals the given seeds in order and then repeats the last.</summary>
        static Func<uint> Deals(params uint[] seeds)
        {
            int next = 0;
            return () => seeds[next < seeds.Length ? next++ : seeds.Length - 1];
        }

        /// <summary>A roll that names somebody after their seed, so a test can read the screen.</summary>
        static Candidate Named(uint seed, int slot) =>
            new Candidate(seed, "person-" + seed,
                new[] { new CandidateSkill("ui.skill.mining", "Mining", (int)(seed % 20)) });

        static ColonistSelect Dealt(params uint[] seeds)
        {
            var select = new ColonistSelect(Named);
            select.Deal(Deals(seeds));
            return select;
        }

        // ------------------------------------------------------------------ what is on screen

        [Test]
        public void ItDealsThreeAndNoneIsLocked()
        {
            ColonistSelect select = Dealt(1u, 2u, 3u);

            Assert.That(select.Cards.Count, Is.EqualTo(ColonistSelect.Slots));
            Assert.That(select.Cards[0].Seed, Is.EqualTo(1u));
            Assert.That(select.Cards[1].Seed, Is.EqualTo(2u));
            Assert.That(select.Cards[2].Seed, Is.EqualTo(3u));
            Assert.That(select.LockedCount, Is.Zero);
            Assert.That(select.CanReroll, Is.True);
        }

        [Test]
        public void TheSeedsHandedToTheWorldAreTheOnesOnScreen()
        {
            ColonistSelect select = Dealt(11u, 22u, 33u);

            Assert.That(select.ChosenSeeds(), Is.EqualTo(new[] { 11u, 22u, 33u }),
                "the colony would be built from people the player never saw");
        }

        /// <summary>
        /// <b>Each card is rolled for the slot it will occupy.</b> Both draws behind a colonist mix
        /// the pawn's id in and the id comes from the slot, so the same seed in slot 0 and slot 2 is
        /// two different people. A screen that rolled every card as slot 0 would show the right
        /// names and the wrong skills for two of the three, and the player would find out after
        /// pressing Start.
        ///
        /// <para>Caught by reading rather than by failing, which is exactly why it is pinned here:
        /// the seam took a seed and no slot, and nothing in this file could have noticed.</para>
        /// </summary>
        [Test]
        public void EachCardIsRolledForTheSlotItWillOccupy()
        {
            var asked = new List<int>();
            Candidate Recording(uint seed, int slot)
            {
                asked.Add(slot);
                return new Candidate(seed, "person-" + slot, Array.Empty<CandidateSkill>());
            }

            var select = new ColonistSelect(Recording);
            select.Deal(Deals(1u, 2u, 3u));

            Assert.That(asked, Is.EqualTo(new[] { 0, 1, 2 }),
                "the cards were not rolled for the slots they are drawn in");
        }

        // ------------------------------------------------------------------ locks and reroll

        [Test]
        public void RerollingKeepsWhatIsLockedAndReplacesTheRest()
        {
            var select = new ColonistSelect(Named);
            select.Deal(Deals(1u, 2u, 3u));
            select.ToggleLock(1);

            select.Reroll(Deals(7u, 8u));

            Assert.That(select.Cards[0].Seed, Is.EqualTo(7u), "an unlocked slot was not rerolled");
            Assert.That(select.Cards[1].Seed, Is.EqualTo(2u), "a locked slot was rerolled anyway");
            Assert.That(select.Cards[2].Seed, Is.EqualTo(8u));
        }

        [Test]
        public void ALockCanBeTakenOffAgain()
        {
            ColonistSelect select = Dealt(1u, 2u, 3u);

            select.ToggleLock(0);
            Assert.That(select.IsLocked(0), Is.True);

            select.ToggleLock(0);
            Assert.That(select.IsLocked(0), Is.False);
            Assert.That(select.LockedCount, Is.Zero);
        }

        /// <summary>
        /// With everything locked Reroll can do nothing, and the screen has to say so rather than
        /// accept the press and sit there — a row that silently does nothing reads as broken, and
        /// the player presses it harder instead of unlocking somebody.
        /// </summary>
        [Test]
        public void WithEveryoneLockedRerollIsRefusedRatherThanSilent()
        {
            ColonistSelect select = Dealt(1u, 2u, 3u);
            for (int i = 0; i < ColonistSelect.Slots; i++) select.ToggleLock(i);

            Assert.That(select.CanReroll, Is.False);

            int changes = 0;
            select.Changed += () => changes++;
            select.Reroll(Deals(9u));

            Assert.That(select.ChosenSeeds(), Is.EqualTo(new[] { 1u, 2u, 3u }));
            Assert.That(changes, Is.Zero, "a reroll that changed nothing still redrew the screen");
        }

        [Test]
        public void ASlotThatIsNotOnScreenCannotBeLocked()
        {
            ColonistSelect select = Dealt(1u, 2u, 3u);

            Assert.That(select.ToggleLock(-1), Is.False);
            Assert.That(select.ToggleLock(ColonistSelect.Slots), Is.False);
            Assert.That(select.LockedCount, Is.Zero);
        }

        /// <summary>
        /// Dealing a fresh set forgets the locks, because a lock is an opinion about a particular
        /// three and those three are gone.
        /// </summary>
        [Test]
        public void DealingAgainForgetsTheLocks()
        {
            var select = new ColonistSelect(Named);
            select.Deal(Deals(1u, 2u, 3u));
            select.ToggleLock(0);

            select.Deal(Deals(4u, 5u, 6u));

            Assert.That(select.LockedCount, Is.Zero);
            Assert.That(select.ChosenSeeds(), Is.EqualTo(new[] { 4u, 5u, 6u }));
        }

        // ------------------------------------------------------------------ distinct names

        /// <summary>
        /// <b>The promise this screen makes that a name cannot.</b> Three candidates carry three
        /// different seeds, and a name keyed on the seed can perfectly well land twice; two people
        /// called Wrenn standing side by side reads as a broken roll. The draw goes again.
        /// </summary>
        [Test]
        public void NoTwoCandidatesShareAName()
        {
            // A roll that insists on one name until the fourth seed it is handed.
            Candidate Stubborn(uint seed, int slot) =>
                new Candidate(seed, seed < 4u ? "Wrenn" : "Odile-" + seed, Array.Empty<CandidateSkill>());

            var select = new ColonistSelect(Stubborn);
            select.Deal(Deals(1u, 2u, 3u, 4u, 5u, 6u, 7u, 8u, 9u));

            var names = new HashSet<string>();
            foreach (Candidate card in select.Cards)
                Assert.That(names.Add(card.Name), Is.True, $"two candidates are called {card.Name}");
        }

        /// <summary>
        /// <b>And it gives up rather than spinning.</b> The pool is small, so a roll that can only
        /// ever answer one name is a real possibility; on the one screen a player cannot leave, a
        /// repeated name is a blemish and a hang is not.
        /// </summary>
        [Test]
        public void ARollThatOnlyEverAnswersOneNameStillFinishes()
        {
            Candidate Always(uint seed, int slot) => new Candidate(seed, "Wrenn", Array.Empty<CandidateSkill>());

            var select = new ColonistSelect(Always);
            select.Deal(Deals(1u));

            Assert.That(select.Cards.Count, Is.EqualTo(ColonistSelect.Slots));
        }

        // ------------------------------------------------------------------ what it tells the screen

        [Test]
        public void EveryChangeIsAnnouncedOnce()
        {
            var select = new ColonistSelect(Named);
            int changes = 0;
            select.Changed += () => changes++;

            select.Deal(Deals(1u, 2u, 3u));
            Assert.That(changes, Is.EqualTo(1), "dealing three redrew three times");

            select.ToggleLock(0);
            Assert.That(changes, Is.EqualTo(2));

            select.Reroll(Deals(4u));
            Assert.That(changes, Is.EqualTo(3));
        }

        [Test]
        public void TheRollIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => new ColonistSelect(null!));
        }

        /// <summary>
        /// Every word this screen draws is a key the owner can rename in the CSV, held here the way
        /// <c>RegistryTests</c> holds every other surface.
        /// </summary>
        [Test]
        public void EveryWordIsARegisteredName()
        {
            foreach (string key in ColonistSelect.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
        }

        /// <summary>
        /// The name the seed arrives under is spelled the same on both sides of a wall this
        /// assembly cannot see over. The Sim half of the pair is pinned by a Sim test; this is the
        /// half that would otherwise be a literal nobody checks.
        /// </summary>
        [Test]
        public void TheSeedArrivesUnderTheNameTheSimulationPublishes()
        {
            Assert.That(ColonistNames.RollSeedAspect, Is.EqualTo("odyssey.pawn.rollseed"));
            Assert.That(AspectKey.Of(ColonistNames.RollSeedAspect),
                Is.EqualTo(AspectKey.Of("odyssey.pawn.rollseed")));
        }
    }
}
