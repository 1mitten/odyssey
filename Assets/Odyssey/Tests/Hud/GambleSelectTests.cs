#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The select screen's two modes (design 41 §5): Standard deals and rerolls, Gamble pulls each
    /// slot once, the choice is final at the first pull, Start waits for three landed, and a pull
    /// survives Back.
    /// </summary>
    public class GambleSelectTests
    {
        static Func<uint> Counting()
        {
            uint next = 100;
            return () => next++;
        }

        /// <summary>A roll that says which tables it was asked for, so the profile can be checked.</summary>
        static Candidate Rolled(uint seed, int slot, RollProfile profile) =>
            new Candidate(seed, "person-" + seed, 30 + slot, "Scrapper", Array.Empty<SkillRow>(),
                new[] { TraitHandle.Diligent }, 1_000, profile);

        static ColonistSelect Gamble()
        {
            var select = new ColonistSelect(Rolled);
            select.Deal(Counting());
            Assert.That(select.SetMode(CreationMode.Gamble, Counting()), Is.True);
            return select;
        }

        [Test]
        public void StandardIsTheDefaultAndDealsStandardPeople()
        {
            var select = new ColonistSelect(Rolled);
            select.Deal(Counting());
            Assert.That(select.Mode, Is.EqualTo(CreationMode.Standard));
            for (int i = 0; i < ColonistSelect.Slots; i++)
            {
                Assert.That(select.StateOf(i), Is.EqualTo(SlotState.Dealt));
                Assert.That(select.Cards[i].Profile, Is.EqualTo(RollProfile.Standard));
            }

            Assert.That(select.CanStart, Is.True);
        }

        [Test]
        public void GambleLaysThreeFaceDownCardsAndHasNoKeepOrReroll()
        {
            ColonistSelect select = Gamble();
            for (int i = 0; i < ColonistSelect.Slots; i++)
                Assert.That(select.StateOf(i), Is.EqualTo(SlotState.Unpulled));
            Assert.That(select.CanReroll, Is.False);
            Assert.That(select.ToggleLock(0), Is.False);
            Assert.That(select.CanStart, Is.False, "Start with nobody pulled");
            Assert.That(select.Rename(0, "Nobody"), Is.False, "a face-down card has nobody to name");
        }

        [Test]
        public void APullDrawsTheColonistFromTheGambleTablesAtThePress()
        {
            ColonistSelect select = Gamble();
            Assert.That(select.Pull(1, Counting()), Is.True);
            Assert.That(select.StateOf(1), Is.EqualTo(SlotState.Spinning));
            Assert.That(select.Selected, Is.EqualTo(1), "the pulled card is the one being shown");
            Assert.That(select.Cards[1].Profile, Is.EqualTo(RollProfile.Gamble));
            Assert.That(select.Cards[1].Name, Is.Not.Empty, "drawn before a reel moved");
        }

        [Test]
        public void EachSlotIsPulledOnceAndOnlyOneRevealsAtATime()
        {
            ColonistSelect select = Gamble();
            Assert.That(select.Pull(0, Counting()), Is.True);
            Assert.That(select.Pull(1, Counting()), Is.False, "a second pull while the first is still revealing");
            Assert.That(select.Pull(0, Counting()), Is.False, "the same slot twice");
            Assert.That(select.Land(0), Is.True);
            uint was = select.Cards[0].Seed;
            Assert.That(select.Pull(0, Counting()), Is.False, "a landed slot pulled again");
            Assert.That(select.Cards[0].Seed, Is.EqualTo(was));
            Assert.That(select.NextUnpulled, Is.EqualTo(1));
        }

        [Test]
        public void TheModeIsFinalAtTheFirstPull()
        {
            ColonistSelect select = Gamble();
            Assert.That(select.CanChangeMode, Is.True);
            Assert.That(select.SetMode(CreationMode.Standard, Counting()), Is.True, "before a pull, it may be changed back");
            Assert.That(select.SetMode(CreationMode.Gamble, Counting()), Is.True);

            select.Pull(0, Counting());
            Assert.That(select.CanChangeMode, Is.False);
            Assert.That(select.SetMode(CreationMode.Standard, Counting()), Is.False);
            Assert.That(select.Mode, Is.EqualTo(CreationMode.Gamble));
        }

        [Test]
        public void StartWaitsForAllThreeToLand()
        {
            ColonistSelect select = Gamble();
            for (int i = 0; i < ColonistSelect.Slots; i++)
            {
                Assert.That(select.CanStart, Is.False, $"after {i} landed");
                select.Pull(i, Counting());
                Assert.That(select.CanStart, Is.False, "while one is still revealing");
                select.Land(i);
            }

            Assert.That(select.CanStart, Is.True);
            Assert.That(select.ChosenProfiles(), Is.All.EqualTo(RollProfile.Gamble));
            Assert.That(select.Rename(2, "Wrenn"), Is.True, "a landed colonist may still be named");
        }

        [Test]
        public void APullSurvivesDealingAgainAndAnInterruptedRevealIsFinished()
        {
            ColonistSelect select = Gamble();
            select.Pull(0, Counting());
            uint pulled = select.Cards[0].Seed;

            select.Deal(Counting()); // what entering New game again does
            Assert.That(select.Mode, Is.EqualTo(CreationMode.Gamble));
            Assert.That(select.Cards[0].Seed, Is.EqualTo(pulled), "Back and New game washed the pull out");
            Assert.That(select.StateOf(0), Is.EqualTo(SlotState.Landed), "the reveal Back cut short is not finished");
            Assert.That(select.StateOf(1), Is.EqualTo(SlotState.Unpulled));
        }

        [Test]
        public void AGambleNobodyPulledIsDealtBackToStandard()
        {
            ColonistSelect select = Gamble();
            select.Deal(Counting());
            Assert.That(select.Mode, Is.EqualTo(CreationMode.Standard), "the control: nothing pulled, nothing to keep");
        }

        [Test]
        public void ForgetClearsAGambleForTheNextGame()
        {
            ColonistSelect select = Gamble();
            select.Pull(0, Counting());
            select.Forget();
            Assert.That(select.Mode, Is.EqualTo(CreationMode.Standard));
            Assert.That(select.CanChangeMode, Is.True);
        }

        // ---- through the menu ------------------------------------------------------------------

        static MenuDirector Menu(out ColonistSelect select)
        {
            select = new ColonistSelect(Rolled);
            var menu = new MenuDirector(new SeedField(() => 4242u), select);
            menu.Show();
            menu.Choose(SessionCommands.NewGameKey);
            return menu;
        }

        [Test]
        public void TheMenuRefusesToStartAGambleUntilItHasLanded()
        {
            MenuDirector menu = Menu(out ColonistSelect select);
            select.SetMode(CreationMode.Gamble, Counting());
            int starts = 0;
            menu.StartRequested += _ => starts++;

            Assert.That(menu.Start(), Is.False);
            for (int i = 0; i < ColonistSelect.Slots; i++) { select.Pull(i, Counting()); select.Land(i); }

            NewGameChoice chosen = default;
            menu.StartRequested += c => chosen = c;
            Assert.That(menu.Start(), Is.True);
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(chosen.Profiles, Is.EqualTo(new[] { RollProfile.Gamble, RollProfile.Gamble, RollProfile.Gamble }));
            Assert.That(select.Mode, Is.EqualTo(CreationMode.Standard), "the next New game is a new decision");
        }

        [Test]
        public void BackAndNewGameKeepThePulls()
        {
            MenuDirector menu = Menu(out ColonistSelect select);
            select.SetMode(CreationMode.Gamble, Counting());
            select.Pull(0, Counting());
            select.Land(0);
            uint pulled = select.Cards[0].Seed;

            menu.Back();
            menu.Choose(SessionCommands.NewGameKey);
            Assert.That(select.Mode, Is.EqualTo(CreationMode.Gamble));
            Assert.That(select.Cards[0].Seed, Is.EqualTo(pulled));
            Assert.That(select.StateOf(0), Is.EqualTo(SlotState.Landed));
        }

        [Test]
        public void AStandardStartCarriesStandardProfiles()
        {
            MenuDirector menu = Menu(out _);
            NewGameChoice chosen = default;
            menu.StartRequested += c => chosen = c;
            Assert.That(menu.Start(), Is.True);
            Assert.That(chosen.Profiles, Is.All.EqualTo(RollProfile.Standard));
        }
    }
}
