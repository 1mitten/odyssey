#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The candidate on the select screen is the colonist the world goes on to build (U40).
    ///
    /// <para><b>This is the unit's one real claim.</b> Everything else about colonist select is a
    /// screen: slots, locks, a reroll. If this is wrong the screen is a lie — it promises a miner
    /// and hands over a cook — and it would be a quiet lie, because both halves would be
    /// individually correct and neither wrong enough to throw. The rule that prevents it is that
    /// there is one roll and both sides call it; these tests are what say so out loud.</para>
    /// </summary>
    public class ColonistDrawTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ColonyWorld BuiltWith(params uint[] chosen) =>
            ColonyWorld.Build(new ColonyRequest
            {
                Size = Size,
                Seed = 4242u,
                Scenario = new ScenarioDef
                {
                    defName = "Scenario_Bare", label = "bare",
                    colonists = chosen.Length,
                    startingFellRadius = 0, startingMineRadius = 0, startingMineOutcrops = 0,
                },
                Barren = true,
                Wooded = true,
                Colonists = chosen,
            });

        /// <summary>
        /// Roll three candidates, build the colony from those three seeds, and require every
        /// colonist to be the candidate that was shown — every skill, every passion.
        /// </summary>
        [Test]
        public void TheColonyIsTheColonistsTheScreenShowed()
        {
            uint[] chosen = { 11u, 22u, 33u };

            var cards = new Pawn[chosen.Length];
            for (int slot = 0; slot < chosen.Length; slot++)
                cards[slot] = ColonistDraw.Roll(chosen[slot], slot);

            ColonyWorld colony = BuiltWith(chosen);
            colony.World.Tick();   // the starting-skill roll happens on the first tick

            // The world seeds its own animals beside the colonists (design 30); they are not on a card.
            var placed = new List<Pawn>();
            foreach (Pawn pawn in colony.Pawns.Pawns.All) if (pawn.IsPerson) placed.Add(pawn);
            Assert.That(placed.Count, Is.EqualTo(chosen.Length), "the colony is not the size chosen");

            for (int slot = 0; slot < chosen.Length; slot++)
            {
                Pawn card = cards[slot];
                Pawn real = placed[slot];

                Assert.That(real.Id.Value, Is.EqualTo(card.Id.Value),
                    $"slot {slot} was previewed under a different id than it was given, so every " +
                    "roll on the card was made for a different person");
                Assert.That(real.RollSeed, Is.EqualTo(chosen[slot]));
                Assert.That(real.Skills, Is.EqualTo(card.Skills),
                    $"slot {slot}: the colonist the player got is not the one on the card");
                Assert.That(real.Passions, Is.EqualTo(card.Passions), $"slot {slot}: passions differ");
            }
        }

        /// <summary>
        /// <b>The control.</b> If the previous test passed because every colonist is the same
        /// person, it would prove nothing at all — so the three have to differ from each other.
        /// </summary>
        [Test]
        public void ThreeSeedsGiveThreeDifferentPeople()
        {
            Pawn a = ColonistDraw.Roll(11u, 0);
            Pawn b = ColonistDraw.Roll(22u, 1);
            Pawn c = ColonistDraw.Roll(33u, 2);

            Assert.That(a.Skills, Is.Not.EqualTo(b.Skills));
            Assert.That(b.Skills, Is.Not.EqualTo(c.Skills));
            Assert.That(a.Skills, Is.Not.EqualTo(c.Skills));
        }

        /// <summary>
        /// The same seed in a different slot is a different person, because both draws mix the
        /// pawn's id in. Worth pinning: it is the reason <see cref="ColonistDraw.IdForSlot"/> has
        /// to be right rather than merely plausible, and a preview that guessed the id would show
        /// somebody who never arrives.
        /// </summary>
        [Test]
        public void TheSlotIsPartOfWhoSomebodyIs()
        {
            Pawn first = ColonistDraw.Roll(11u, 0);
            Pawn second = ColonistDraw.Roll(11u, 1);

            Assert.That(second.Skills, Is.Not.EqualTo(first.Skills));
        }

        [Test]
        public void RollingTheSameSeedTwiceGivesTheSamePerson()
        {
            Pawn once = ColonistDraw.Roll(4242u, 0);
            Pawn again = ColonistDraw.Roll(4242u, 0);

            Assert.That(again.Skills, Is.EqualTo(once.Skills));
            Assert.That(again.Passions, Is.EqualTo(once.Passions));
        }

        /// <summary>
        /// A colony nobody chose is untouched by any of this: no seeds handed in, every colonist on
        /// the world seed, exactly as before the unit existed.
        /// </summary>
        [Test]
        public void AColonyNobodyChoseRollsFromTheWorldSeed()
        {
            ColonyWorld colony = ColonyWorld.Build(Size, 4242u, ScenarioDef.Bare(), wooded: true);

            foreach (Pawn pawn in colony.Pawns.Pawns.All)
                Assert.That(pawn.RollSeed, Is.EqualTo(4242u),
                    "a colonist nobody chose is rolling from something other than the world seed");
        }

        /// <summary>
        /// The seed reaches the interface, under the name the interface spells for itself.
        ///
        /// <para>The Hud assembly cannot reference this one, so the two halves of
        /// <c>odyssey.pawn.rollseed</c> are two string literals that have to agree. This is the Sim
        /// half — that the aspect is published at all, with the pawn's real seed in it — and
        /// <c>ColonistSelectTests</c> is the other, which pins the spelling from a project that
        /// cannot see this one.</para>
        /// </summary>
        [Test]
        public void TheSeedIsPublishedToTheInterface()
        {
            uint[] chosen = { 11u, 22u, 33u };
            ColonyWorld colony = BuiltWith(chosen);
            colony.World.Tick();

            WorldSnapshot frame = colony.World.Views.Current;
            AspectKey key = AspectKey.Of("odyssey.pawn.rollseed");

            for (int slot = 0; slot < chosen.Length; slot++)
            {
                PawnId id = ColonistDraw.IdForSlot(slot);
                Assert.That(frame.TryGetPawnAspect(id, key, out int published), Is.True,
                    $"slot {slot} publishes no roll seed, so the interface cannot name them");
                Assert.That(unchecked((uint)published), Is.EqualTo(chosen[slot]));
            }
        }

        /// <summary>
        /// Fewer seeds than colonists covers the ones it names and leaves the rest alone, rather
        /// than refusing — the scenario decides how many colonists there are, and the screen only
        /// decides who some of them are.
        /// </summary>
        [Test]
        public void SeedsForSomeOfThemLeaveTheRestOnTheWorldSeed()
        {
            ColonyWorld colony = ColonyWorld.Build(new ColonyRequest
            {
                Size = Size,
                Seed = 4242u,
                Scenario = new ScenarioDef
                {
                    defName = "Scenario_Bare", label = "bare", colonists = 3,
                    startingFellRadius = 0, startingMineRadius = 0, startingMineOutcrops = 0,
                },
                Barren = true,
                Wooded = true,
                Colonists = new[] { 11u },
            });

            var placed = colony.Pawns.Pawns.All;
            Assert.That(placed[0].RollSeed, Is.EqualTo(11u));
            for (int i = 1; i < placed.Count; i++)
                Assert.That(placed[i].RollSeed, Is.EqualTo(4242u));
        }
    }
}
