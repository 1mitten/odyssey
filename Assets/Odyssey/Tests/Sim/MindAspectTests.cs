#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The mood band, published by the simulation (design 44 §5a, TM1).
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
        public void TheBandAndTargetArePublishedForColonistsOnly()
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

            Assert.That(frame.TryGetPawnAspect(hog.Id, MindAspects.Band, out _), Is.False,
                "an animal's mood never moves (design 29 §2), so it has no band to report");
        }

        // ---- TM2: the Thoughts tab ---------------------------------------------------------

        [Test]
        public void EachThoughtsShareSumsToTheMemoryOffset()
        {
            var colony = Colony.Build();
            Pawn pawn = colony.Ctx.Pawns.Spawn(colony.Cell(8, 8, 0));
            pawn.AddMemory(ThoughtIndex.ColonistDied, 0);
            pawn.AddMemory(ThoughtIndex.ColonistDied, 10);
            pawn.AddMemory(ThoughtIndex.ColonistDied, 20);
            pawn.AddMemory(ThoughtIndex.AteMeal, 30);
            pawn.AddMemory(ThoughtIndex.SleptOnGround, 40);

            int sum = 0;
            for (int t = 0; t < ThoughtIndex.Count; t++) sum += pawn.MemoryContribution(t, 100, out _, out _);
            Assert.That(sum, Is.EqualTo(pawn.MemoryMoodOffset(100)),
                "the tab's rows must add up to what the mood is actually given, or it explains nothing");

            int died = pawn.MemoryContribution(ThoughtIndex.ColonistDied, 100, out int copies, out int soonest);
            Assert.That(copies, Is.EqualTo(3));
            Assert.That(died, Is.EqualTo(-60 + -45 + -33), "the stack at the usual diminishing multiplier (design 33 §12b)");
            Assert.That(soonest, Is.EqualTo(colony.Ctx.Content.Thoughts[ThoughtIndex.ColonistDied].durationTicks),
                "the soonest copy is the one added first");
        }

        [Test]
        public void MemoriesAndSituationalOffsetsArePublishedAsTheyCount()
        {
            var colony = Colony.Build();
            Pawn pawn = colony.Ctx.Pawns.Spawn(colony.Cell(8, 8, 0));
            pawn.Needs[NeedIndex.Food] = 60;     // the -120 band
            pawn.AddMemory(ThoughtIndex.AttackedByColonist, 0);
            colony.World.Tick();
            WorldSnapshot frame = colony.World.Views.Current;
            int tick = colony.World.CurrentTick;

            Assert.That(frame.TryGetPawnAspect(pawn.Id, MindAspects.Thought[ThoughtIndex.AttackedByColonist], out int worth), Is.True);
            Assert.That(worth, Is.EqualTo(-80));
            Assert.That(frame.TryGetPawnAspect(pawn.Id, MindAspects.ThoughtLeft[ThoughtIndex.AttackedByColonist], out int left), Is.True);
            Assert.That(left, Is.EqualTo(pawn.Memories[0].ExpiryTick - (tick - 1)).Or.EqualTo(pawn.Memories[0].ExpiryTick - tick));
            Assert.That(frame.TryGetPawnAspect(pawn.Id, MindAspects.ThoughtCount[ThoughtIndex.AttackedByColonist], out int count), Is.True);
            Assert.That(count, Is.EqualTo(1));

            Assert.That(frame.TryGetPawnAspect(pawn.Id, MindAspects.Need[NeedIndex.Food], out int hunger), Is.True);
            Assert.That(hunger, Is.EqualTo(colony.Ctx.Content.Needs[NeedIndex.Food].MoodOffset(pawn.Needs[NeedIndex.Food])));

            // The control: a thought she does not hold, and a need at no offset, publish nothing.
            Assert.That(frame.TryGetPawnAspect(pawn.Id, MindAspects.Thought[ThoughtIndex.Fell], out _), Is.False);
            Assert.That(frame.TryGetPawnAspect(pawn.Id, MindAspects.Need[NeedIndex.Rest], out _), Is.False,
                "sparse: a rested colonist's rest says nothing");
        }

        [Test]
        public void TheThoughtHandlesAreTheContentsOrder()
        {
            // The interface names a thought by ThoughtHandle; the save stores FromDefs's index. The
            // two orders must be one, and nothing but this can see both.
            var content = ContentPack.Pawns();
            Assert.That(content.Thoughts.Length, Is.EqualTo(ThoughtHandle.Count));
            Assert.That(ThoughtHandle.Names.Length, Is.EqualTo(ThoughtHandle.Count));
            for (int t = 0; t < ThoughtHandle.Count; t++)
                Assert.That(content.Thoughts[t].defName.ToLowerInvariant(),
                    Is.EqualTo("thought_" + ThoughtHandle.Names[t]), $"thought {t}");
        }

        [Test]
        public void TheAspectNamesAreTheOnesTheInterfaceSpells()
        {
            Assert.That(MindAspects.Need[NeedIndex.Food], Is.EqualTo(AspectKey.Of("odyssey.pawn.mood.need.food")));
            Assert.That(MindAspects.Need[NeedIndex.Rest], Is.EqualTo(AspectKey.Of("odyssey.pawn.mood.need.rest")));
            Assert.That(MindAspects.Need[NeedIndex.Joy], Is.EqualTo(AspectKey.Of("odyssey.pawn.mood.need.joy")));
            Assert.That(MindAspects.Temperature, Is.EqualTo(AspectKey.Of("odyssey.pawn.mood.temperature")));
            Assert.That(MindAspects.Thought[ThoughtIndex.Fell], Is.EqualTo(AspectKey.Of("odyssey.pawn.thought.fell")));
            Assert.That(MindAspects.ThoughtLeft[ThoughtIndex.Fell], Is.EqualTo(AspectKey.Of("odyssey.pawn.thought.fell.left")));
            Assert.That(MindAspects.ThoughtCount[ThoughtIndex.Fell], Is.EqualTo(AspectKey.Of("odyssey.pawn.thought.fell.count")));
            // Odyssey.Hud spells these in MindAspectNames and cannot reference this assembly.
            Assert.That(MindAspects.Band, Is.EqualTo(AspectKey.Of("odyssey.pawn.mood.band")));
            Assert.That(MindAspects.Target, Is.EqualTo(AspectKey.Of("odyssey.pawn.mood.target")));
            Assert.That(MindAspects.Break, Is.EqualTo(AspectKey.Of("odyssey.pawn.break")));
        }
    }
}
