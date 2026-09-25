#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The mood band, published by the simulation (design 43 §5a, TM1).
    ///
    /// <para><b>What this replaced.</b> The interface held two thresholds of its own, 600 and 350,
    /// and read the mood against them — so a colonist at the resting target of 500, fed, rested and
    /// with nothing on her mind, read <i>strained</i> for the whole game. The owner asked for a
    /// rested colonist to read content, and the lines are about to move with traits, which only the
    /// simulation knows. So the band is its answer, and these tests hold it to that.</para>
    /// </summary>
    public class MindAspectTests
    {
        static void Rested(Pawn pawn)
        {
            pawn.Needs[NeedIndex.Food] = 800;
            pawn.Needs[NeedIndex.Rest] = 800;
            // Joy's neutral band is 300 to 700: no offset either way.
            pawn.Needs[NeedIndex.Joy] = 500;
            pawn.Memories.Clear();
        }

        [Test]
        public void ARestedColonistWithNothingOnHerMindIsContent()
        {
            var colony = Colony.Build();
            Pawn pawn = colony.Ctx.Pawns.Spawn(colony.Cell(8, 8, 0));

            // Placed at 600, she drifts down to the base at five a needs interval.
            for (int i = 0; i < colony.Needs.IntervalTicks * 40; i++) { Rested(pawn); colony.World.Tick(); }

            Assert.That(pawn.MoodTarget, Is.EqualTo(colony.Ctx.Content.Mood.baseMood),
                "the fixture is meant to hold her at the resting target and nothing else");
            Assert.That(pawn.Mood, Is.EqualTo(pawn.MoodTarget), "and she has had time to arrive");
            Assert.That(pawn.Band(), Is.EqualTo(MoodBand.Content),
                "the owner's rule: a fed, rested colonist with nothing on her mind is content");
            Assert.That(colony.World.Views.Current.TryGetPawnAspect(pawn.Id, MindAspects.Band, out int band), Is.True);
            Assert.That(band, Is.EqualTo(MoodBand.Content), "and that is what the interface is told");

            // The control: the interface's old constant called this same mood strained. It is the
            // reason the band moved across the seam, so the test says so in a number.
            const int oldInterfaceContentLine = 600;
            Assert.That(pawn.Mood, Is.LessThan(oldInterfaceContentLine));
        }

        [Test]
        public void TheBandsFallAtHerOwnLines()
        {
            var colony = Colony.Build();
            Pawn pawn = colony.Ctx.Pawns.Spawn(colony.Cell(8, 8, 0));
            MoodDef mood = colony.Ctx.Content.Mood;
            int minor = pawn.MinorBreakLine();

            Assert.That(minor, Is.EqualTo(mood.breakThreshold), "an untraited colonist's line is the content's");
            Assert.That(pawn.MajorBreakLine(), Is.EqualTo(minor * 4 / 7), "the reference's ratio (a-19)");
            Assert.That(pawn.ExtremeBreakLine(), Is.EqualTo(minor / 7));

            (int mood, int band)[] cases =
            {
                (minor + mood.strainMargin, MoodBand.Content),
                (minor + mood.strainMargin - 1, MoodBand.Strained),
                (minor, MoodBand.Strained),
                (minor - 1, MoodBand.BreakingMinor),
                (pawn.MajorBreakLine(), MoodBand.BreakingMinor),
                (pawn.MajorBreakLine() - 1, MoodBand.BreakingMajor),
                (pawn.ExtremeBreakLine(), MoodBand.BreakingMajor),
                (pawn.ExtremeBreakLine() - 1, MoodBand.BreakingExtreme),
                (0, MoodBand.BreakingExtreme),
            };
            foreach ((int value, int band) in cases)
            {
                pawn.Mood = value;
                Assert.That(pawn.Band(), Is.EqualTo(band), $"mood {value}");
            }

            pawn.Mood = 900;
            pawn.BreakTicksLeft = 100;
            Assert.That(pawn.Band(), Is.EqualTo(MoodBand.Broken), "a break is its own band, whatever the mood");
        }

        [Test]
        public void TheBandAndLinesArePublishedForColonistsOnly()
        {
            var colony = Colony.Build();
            Pawn person = colony.Ctx.Pawns.Spawn(colony.Cell(4, 4, 0));
            Pawn hog = colony.Ctx.Pawns.Spawn(colony.Cell(10, 10, 0), kind: 1);
            Assume.That(hog.IsPerson, Is.False);

            person.Mood = 300;
            colony.World.Tick();
            WorldSnapshot frame = colony.World.Views.Current;

            Assert.That(frame.TryGetPawnAspect(person.Id, MindAspects.Band, out int band), Is.True);
            Assert.That(band, Is.EqualTo(person.Band()));
            Assert.That(frame.TryGetPawnAspect(person.Id, MindAspects.Target, out int target), Is.True);
            Assert.That(target, Is.EqualTo(person.MoodTarget));
            Assert.That(frame.TryGetPawnAspect(person.Id, MindAspects.Minor, out int minor), Is.True);
            Assert.That(minor, Is.EqualTo(person.MinorBreakLine()));
            Assert.That(frame.TryGetPawnAspect(person.Id, MindAspects.Major, out int major), Is.True);
            Assert.That(major, Is.EqualTo(person.MajorBreakLine()));
            Assert.That(frame.TryGetPawnAspect(person.Id, MindAspects.Extreme, out int extreme), Is.True);
            Assert.That(extreme, Is.EqualTo(person.ExtremeBreakLine()));

            Assert.That(frame.TryGetPawnAspect(hog.Id, MindAspects.Band, out _), Is.False,
                "an animal's mood never moves (design 29 §2), so it has no band to report");
        }

        [Test]
        public void TheAspectNamesAreTheOnesTheInterfaceSpells()
        {
            // Odyssey.Hud spells these in MindAspectNames and cannot reference this assembly.
            Assert.That(MindAspects.Band, Is.EqualTo(AspectKey.Of("odyssey.pawn.mood.band")));
            Assert.That(MindAspects.Target, Is.EqualTo(AspectKey.Of("odyssey.pawn.mood.target")));
            Assert.That(MindAspects.Minor, Is.EqualTo(AspectKey.Of("odyssey.pawn.mood.minor")));
            Assert.That(MindAspects.Major, Is.EqualTo(AspectKey.Of("odyssey.pawn.mood.major")));
            Assert.That(MindAspects.Extreme, Is.EqualTo(AspectKey.Of("odyssey.pawn.mood.extreme")));
        }
    }
}
