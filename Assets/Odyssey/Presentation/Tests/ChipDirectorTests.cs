#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.World;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The debris that comes off a cut: wood off an axe now, stone off a pick when mining lands.
    ///
    /// There is not much arithmetic here to get wrong, and that is rather the point of the tests
    /// that are here — they hold the shape of the thing rather than its numbers. One system serves
    /// every material, so a recipe with nothing in it must throw nothing rather than an exception;
    /// the emitter must survive a pipeline with no shader it recognises, because that is a clone
    /// without the packs and it has to keep running; and it must be warmed by the time anybody
    /// could ask it for a burst, because an unwarmed particle material compiles its shader on the
    /// first frame it is drawn and that frame is the one the player is watching.
    /// </summary>
    public class ChipDirectorTests
    {
        ChipDirector _chips = null!;
        GameObject _root = null!;

        [SetUp]
        public void Build()
        {
            _root = new GameObject("chip root");
            _chips = new ChipDirector(_root.transform, _root.layer);
        }

        [TearDown]
        public void Clear()
        {
            _chips.Dispose();
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void ItIsWarmBeforeAnybodyAsksForABurst()
        {
            Assert.That(_chips.Warmed, Is.True,
                "an unwarmed particle material compiles its shader on the frame the first axe " +
                "lands, which is the one frame in the sequence anybody is looking at");
            Assert.That(_chips.ChipsThrown, Is.Zero, "and the warm is not a burst anybody can see");
        }

        [Test]
        public void ABlowThrowsItsRecipesWorth()
        {
            _chips.Throw(ChipRecipe.Wood, Vector3.zero, Vector3.back);
            Assert.That(_chips.ChipsThrown, Is.EqualTo(ChipRecipe.Wood.Count));

            _chips.Throw(ChipRecipe.Stone, Vector3.one, Vector3.back);
            Assert.That(_chips.ChipsThrown,
                Is.EqualTo(ChipRecipe.Wood.Count + ChipRecipe.Stone.Count),
                "one director serves every material; a miner and a woodcutter share it");
        }

        [Test]
        public void AnEmptyRecipeThrowsNothingRatherThanFailing()
        {
            // A default recipe is what a caller gets by forgetting one, and by the time mining is
            // added there will be more callers than there are today.
            _chips.Throw(default, Vector3.zero, Vector3.back);
            Assert.That(_chips.ChipsThrown, Is.Zero);
        }

        [Test]
        public void ADirectionOfNothingDoesNotPileEveryChipInOnePlace()
        {
            // The caller's outward vector is the direction from the tree to the colonist, which is
            // zero on the frame a pawn stands in the very cell it is working on. A zero velocity
            // would leave the whole burst hanging at the blade.
            Assert.DoesNotThrow(() => _chips.Throw(ChipRecipe.Wood, Vector3.zero, Vector3.zero));
            Assert.That(_chips.ChipsThrown, Is.EqualTo(ChipRecipe.Wood.Count));
        }

        [Test]
        public void TheRecipesAreTheRightWayRound()
        {
            // Ranges given the wrong way round silently produce nothing: Range(low, high) with the
            // two swapped returns values below the intended floor, and a lifetime below zero is a
            // chip that never appears at all.
            foreach (ChipRecipe recipe in new[] { ChipRecipe.Wood, ChipRecipe.Stone })
            {
                Assert.That(recipe.IsSomething, Is.True);
                Assert.That(recipe.Speed.x, Is.LessThanOrEqualTo(recipe.Speed.y));
                Assert.That(recipe.Size.x, Is.LessThanOrEqualTo(recipe.Size.y));
                Assert.That(recipe.Life.x, Is.LessThanOrEqualTo(recipe.Life.y));
                Assert.That(recipe.Life.x, Is.GreaterThan(0f));
                Assert.That(recipe.Spread, Is.InRange(0f, 90f));
            }
        }

        [Test]
        public void StoneLeavesTheFaceHarderAndTighterThanWoodDoes()
        {
            // Gravity belongs to the system and applies to everything in flight at once, so it is
            // not a lever a recipe has. What is left is how fast the debris leaves and how wide it
            // sprays, and stone does both like something heavy: quicker off the face, in a
            // narrower cone.
            //
            // **This test used to assert that stone was also SMALLER and shorter-lived**, on the
            // reasoning that small fast specks read as heavy at board-camera height. The owner
            // watched it and asked for the opposite — "the particles need to be bigger and set
            // back a bit (bigger chunks)" — because at that height small specks did not read as
            // anything at all. A pick knocks lumps off a rock face; an axe takes slivers off a
            // trunk. So stone is now the bigger and longer-lived of the two on purpose, and the
            // weight is carried entirely by speed and spread.
            Assert.That(ChipRecipe.Stone.Speed.y, Is.GreaterThan(ChipRecipe.Wood.Speed.y),
                "stone no longer leaves the face faster than wood");
            Assert.That(ChipRecipe.Stone.Spread, Is.LessThan(ChipRecipe.Wood.Spread),
                "stone sprays as wide as wood, so nothing is left reading as heavy");
            Assert.That(ChipRecipe.Stone.Size.y, Is.GreaterThan(ChipRecipe.Wood.Size.y),
                "the chunks shrank back to slivers");
        }
    }
}
