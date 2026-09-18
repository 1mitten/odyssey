#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The flat avatar's recipe (<c>docs/design/20-avatars.md</c>): a face is a pure function of a
    /// colonist's <c>RollSeed</c> and their id, and it is the same derivation the figure in the
    /// world is painted from.
    /// </summary>
    public class ColonistFaceTests
    {
        static WorldSnapshot FrameWithSeeds(params (int id, uint seed)[] pawns)
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(0, new GridSize(10, 10, 4), sliceLayer: 1);
            foreach ((int id, uint seed) in pawns)
            {
                var pawn = new PawnId(id);
                snapshot.AddPawn(new PawnView(pawn, new CellRef(id, 1, 1), food: 900, rest: 900, mood: 50));
                snapshot.AddPawnAspect(new PawnAspect(
                    pawn, AspectKey.Of(ColonistNames.RollSeedAspect), unchecked((int)seed)));
            }

            return snapshot;
        }

        [Test]
        public void TheSameColonistIsTheSameFaceForEver()
        {
            // The whole promise of a derived identity: no cache, no session, no order of calls.
            for (int id = 1; id <= 50; id++)
            {
                ColonistFace once = ColonistFace.Of(20260918u, new PawnId(id));
                ColonistFace again = ColonistFace.Of(20260918u, new PawnId(id));
                Assert.That(again, Is.EqualTo(once), $"pawn {id}");
            }
        }

        [Test]
        public void ReadingItOffTheFrameIsReadingItOffTheSeed()
        {
            // The seam that actually ships: the setup screen holds a candidate's seed in its hand
            // and calls the first overload, while the roster bar has only a published frame and
            // calls the second. They must be one answer, or a colonist changes face on Start.
            WorldSnapshot frame = FrameWithSeeds((1, 4242u), (2, 99u), (3, 20260918u));

            foreach ((int id, uint seed) in new[] { (1, 4242u), (2, 99u), (3, 20260918u) })
                Assert.That(ColonistFace.Of(frame, new PawnId(id)),
                            Is.EqualTo(ColonistFace.Of(seed, new PawnId(id))), $"pawn {id}");
        }

        [Test]
        public void AColonistFromAWorldTooOldToCarryASeedStillHasAFace()
        {
            // A save written before U40 has no RollSeed aspect at all, so RollSeedOf reads 0. Zero
            // is a legal seed and must deal a face rather than an exception or a blank tile.
            var bare = new WorldSnapshot();
            bare.BeginWrite(0, new GridSize(10, 10, 4), sliceLayer: 1);
            bare.AddPawn(new PawnView(new PawnId(1), new CellRef(1, 1, 1), food: 900, rest: 900, mood: 50));

            ColonistFace face = ColonistFace.Of(bare, new PawnId(1));
            Assert.That(face, Is.EqualTo(ColonistFace.Of(0u, new PawnId(1))));
            Assert.That(face.HairShape, Is.InRange(0, ColonistFace.HairShapes - 1));
            Assert.That(face.Build, Is.InRange(0, ColonistFace.Builds - 1));
        }

        [Test]
        public void RerollingACandidateGivesADifferentPerson()
        {
            // What the owner will actually do to this screen: press Reroll and watch. A hash
            // without an avalanche would step one place along each table, so a reroll would look
            // like a slider rather than like a new applicant.
            int changed = 0;
            for (uint seed = 1; seed <= 200; seed++)
                if (!ColonistFace.Of(seed, new PawnId(1)).Equals(ColonistFace.Of(seed + 1, new PawnId(1))))
                    changed++;

            Assert.That(changed, Is.EqualTo(200),
                "every neighbouring seed should deal a visibly different first candidate");
        }

        [Test]
        public void TheThreeOnTheSetupScreenAreThreeDifferentPeople()
        {
            // The candidates are drawn from three separate seeds, so this is really a statement
            // about the id not being swamped: all three cards must not come out alike.
            for (uint seed = 1; seed <= 200; seed++)
            {
                var faces = new HashSet<ColonistFace>();
                for (int slot = 0; slot < 3; slot++) faces.Add(ColonistFace.Of(seed, new PawnId(slot + 1)));
                Assert.That(faces.Count, Is.EqualTo(3), $"seed {seed} dealt a repeated face");
            }
        }

        [Test]
        public void TheOpeningColonyIsNotFiveCopiesOfOneSilhouette()
        {
            // The mirror of ColonistAppearanceTests.TheStartingFiveAreNotFiveCopiesOfOneMan, about
            // the two indices this file adds rather than about the colours. **Three is the measured
            // worst case over these 200 seeds, not a margin** — the first draft of this test
            // asserted four and failed, which is the number being measured rather than assumed.
            // Three of twenty-four silhouettes repeating among five people is what a fair draw
            // does; what stops those two reading as twins is that they are also differently
            // coloured, which is the test below this one.
            int worst = int.MaxValue;
            for (uint seed = 1; seed <= 200; seed++)
            {
                var silhouettes = new HashSet<int>();
                for (int id = 1; id <= 5; id++)
                {
                    ColonistFace face = ColonistFace.Of(seed, new PawnId(id));
                    silhouettes.Add(face.HairShape * ColonistFace.Builds + face.Build);
                }

                worst = Math.Min(worst, silhouettes.Count);
            }

            Assert.That(worst, Is.GreaterThanOrEqualTo(3),
                $"some opening colony had only {worst} distinct silhouettes among five colonists");
        }

        [Test]
        public void NoOpeningColonyContainsTwoIdenticalPeople()
        {
            // The claim that matters to a player, and the one the silhouette count above cannot
            // make on its own: a repeated crown on repeated shoulders is fine as long as the two
            // are not also the same colour from head to foot. Over 200 seeds, no colony is.
            for (uint seed = 1; seed <= 200; seed++)
            {
                var faces = new HashSet<ColonistFace>();
                for (int id = 1; id <= 5; id++) faces.Add(ColonistFace.Of(seed, new PawnId(id)));
                Assert.That(faces.Count, Is.EqualTo(5), $"seed {seed} dealt two identical colonists");
            }
        }

        [Test]
        public void EveryCrownAndEveryBuildIsReachable()
        {
            // A shape nothing can ever draw is a table that silently did nothing.
            var crowns = new HashSet<int>();
            var builds = new HashSet<int>();
            for (int id = 1; id <= 2000; id++)
            {
                ColonistFace face = ColonistFace.Of(7u, new PawnId(id));
                crowns.Add(face.HairShape);
                builds.Add(face.Build);
            }

            Assert.That(crowns.Count, Is.EqualTo(ColonistFace.HairShapes));
            Assert.That(builds.Count, Is.EqualTo(ColonistFace.Builds));
        }

        [Test]
        public void ShapeAndColourAreIndependent()
        {
            // The correlated-hash bug in its shape form: one avalanche read modulo two lengths and
            // everybody with a fringe is also broad. Asserted as conditional distribution, exactly
            // as the colour streams are — the spread of builds among one crown must look like the
            // spread of builds over everybody.
            var byCrown = new Dictionary<int, HashSet<int>>();
            var byHairColour = new Dictionary<uint, HashSet<int>>();

            for (int id = 1; id <= 4000; id++)
            {
                ColonistFace face = ColonistFace.Of(4242u, new PawnId(id));

                if (!byCrown.TryGetValue(face.HairShape, out HashSet<int>? builds))
                    byCrown[face.HairShape] = builds = new HashSet<int>();
                builds.Add(face.Build);

                if (!byHairColour.TryGetValue(face.Hair.Packed, out HashSet<int>? crowns))
                    byHairColour[face.Hair.Packed] = crowns = new HashSet<int>();
                crowns.Add(face.HairShape);
            }

            foreach (KeyValuePair<int, HashSet<int>> pair in byCrown)
                Assert.That(pair.Value.Count, Is.EqualTo(ColonistFace.Builds),
                    $"crown {pair.Key} only ever appears on {pair.Value.Count} builds");

            foreach (KeyValuePair<uint, HashSet<int>> pair in byHairColour)
                Assert.That(pair.Value.Count, Is.EqualTo(ColonistFace.HairShapes),
                    $"hair #{pair.Key:X6} only ever appears as {pair.Value.Count} crowns");
        }

        [Test]
        public void TheFaceIsTheColoursTheFigureInTheWorldIsPainted()
        {
            // The HopCost rule stated as an assertion. Not "the same palette" — the same call, so a
            // change to the appearance cannot leave the card promising a person in a red coat while
            // the colony delivers one in green.
            for (int id = 1; id <= 100; id++)
            {
                ColonistAppearance figure = ColonistAppearance.Of(31337u, id, lookCount: 1);
                ColonistFace face = ColonistFace.Of(31337u, new PawnId(id));

                Assert.That(face.Skin, Is.EqualTo(figure.Skin), $"pawn {id} skin");
                Assert.That(face.Hair, Is.EqualTo(figure.Hair), $"pawn {id} hair");
                Assert.That(face.Tile, Is.EqualTo(figure.Cloth), $"pawn {id} garment");
                Assert.That(face.Shoulders, Is.EqualTo(figure.Cloth2), $"pawn {id} shadow");
            }
        }

        [Test]
        public void TheShouldersAreAlwaysDarkerThanTheTileBehindThem()
        {
            // Legibility without this file owning a contrast rule: the figure reads against its own
            // background because the appearance already guarantees the second garment is the first
            // in shadow. If that guarantee ever goes, the avatar goes with it and says so here.
            for (int id = 1; id <= 500; id++)
            {
                ColonistFace face = ColonistFace.Of(99u, new PawnId(id));
                int tile = face.Tile.R + face.Tile.G + face.Tile.B;
                int shoulders = face.Shoulders.R + face.Shoulders.G + face.Shoulders.B;
                Assert.That(shoulders, Is.LessThan(tile), $"pawn {id}");
            }
        }
    }
}
