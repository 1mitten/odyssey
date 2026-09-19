#nullable enable
using System.IO;
using NUnit.Framework;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The pawn tuning lives in XML and the simulation loads it, which is the whole of OQ-15 and
    /// the half of it that was left open until 2026-09-17.
    ///
    /// <para><b>Why this is a fingerprint now and was an oracle before.</b> While the game built
    /// its content from <c>PawnContent.Core()</c>, the right test was "the XML says the same as
    /// the code", because the code was the specification every soak hash and tuning decision had
    /// been measured against; the file said so, and said the oracle would stand <i>"until the
    /// bootstrap loads content at startup"</i>. It does now. There is no second copy left to
    /// disagree with — keeping one would have meant writing every new item, job and work type
    /// twice for ever, which is the cost the row existed to remove — so the question changes from
    /// "do the two copies agree" to the one a golden answers: <b>has the content moved without
    /// anybody saying so?</b></para>
    ///
    /// <para>The fingerprint walks the loaded record by reflection, the same walk
    /// <see cref="DefComparison.Differences"/> uses, so a field added to a Def tomorrow is
    /// covered without anybody remembering to come back here. A legitimate content change costs
    /// one deliberate line; an accidental one fails loudly. When it fails and you did not expect
    /// it, <c>git diff Assets/Odyssey/Defs/Core</c> is the answer to "what moved" — that is the
    /// only way content can change.</para>
    ///
    /// <para>The controls matter more than usual: a reflective walk that quietly reached nothing
    /// would return the bare offset basis and pass for ever. Each one perturbs a single value at
    /// a different depth and requires the number to move, and each was seen to fail first.</para>
    /// </summary>
    public class PawnContentDefTests
    {
        /// <summary>
        /// A pack of its own, loaded fresh.
        ///
        /// <para>Not <see cref="ContentPack.Pawns"/>, deliberately: that shares one parsed
        /// database between every caller, and a control here writes to a Def reached through the
        /// record it is given. Through the shared database that write would land in every test
        /// that ran afterwards.</para>
        /// </summary>
        static DefDatabase LoadCore() => ContentPack.LoadCore(RepoPaths.CoreDefs);

        /// <summary>
        /// The content as it stands. Update this number only when you meant to change the
        /// content, and say what moved in the commit message.
        ///
        /// <para>Moved 2026-09-17 by the build pipeline: two jobs (<c>Job_Deliver</c>,
        /// <c>Job_Build</c>), a work type (<c>Work_Construction</c>, which also renumbered the other
        /// three so it could scan first) and a skill (<c>Skill_Construction</c>).</para>
        ///
        /// <para>Moved again the same day by deconstruct: one job, <c>Job_Deconstruct</c>. It adds
        /// no work type and no skill — taking a wall apart is the builder's knowledge and trains
        /// construction, because the colonist who put it up is the one who knows where it comes
        /// apart.</para>
        ///
        /// <para>Moved a third time by U37, merged on top of deconstruct: <c>PawnKindDef</c>
        /// gained <c>startingSkillLevelWeights</c>, the invented distribution starting skill
        /// levels are rolled from.</para>
        ///
        /// <para>Moved a fourth time, 2026-09-17, by the pickup gaining a duration:
        /// <c>PawnTuningDef</c> gained <c>liftTicks</c> (48, which is the 0.8 s the drawn gesture
        /// has always taken) and <c>liftGraspTicks</c> (24, the middle of that gesture's hold).
        /// Owner: <i>"there should be time spent motion down, picking up object and standing
        /// up"</i>. It is the first content change here that moves a golden as well, because a
        /// haul now costs the colony 48 ticks it did not spend before.</para>
        /// </summary>
        // U29 added Thought_Fell: the memory a colonist keeps of riding a floor down. It stands in
        // for an injury that cannot exist until there is a health model to apply one to.
        //
        // The value below is neither branch's: U29 and the pickup's two tuning fields both moved
        // this number, so the merged content has a fingerprint neither of them ever computed. It
        // was taken by running the test against the merged pack rather than by picking a side,
        // which is the only thing that could have produced a correct answer here.
        //
        // Moved a fifth time, 2026-09-17, by U26's last line: Work_Construction gained the two
        // success-roll integers — successBasePerMille 850, successSlopePerLevel 50 — so a
        // completed build rolls against the finishing builder's skill and can botch. The value was
        // taken from a freshly loaded pack after the write-through in ConstructionTests' first
        // version had been found and removed: a fingerprint of a polluted database is the wrong
        // number to pin, and it is not the number a clean load produces.
        // Moved a sixth time, 2026-09-18, by the growing zones: Item_Carrots joins the item
        // table (nutrition 180, stackLimit 40) as the yield of Plant_Carrot. One new commodity
        // at the end of the handle order, nothing existing moved.
        // Moved a seventh time, 2026-09-18, by the growing jobs (U47): Job_Sow and Job_Harvest at
        // drivers 10 and 11, Work_Growing at scan order 1 (cutting, mining and hauling each
        // shifted one rank to keep every pair distinct), and Skill_Growing — which is why the
        // meadow golden moved with it: every pawn's priority and skill arrays are one slot
        // longer, and the pawn hash walks both.
        //
        // Moved a sixth time, 2026-09-18, by WS2's curve: the three skilled work types gained the
        // four rate integers each — rateSkill, workRateBasePerMille, workRateSlopePerLevel and
        // workRateFloorPerMille — so a colonist pays work at her skill's pace and not at the flat
        // speed everything was tuned at (design 17 §3a). Taken from a freshly loaded pack.
        //
        // Moved a seventh time, the same day, by WS3's movement: MovementDef gained the two innate
        // pace bounds — 850 to 1,150, capped by the walk cycle's 2 m/s — and PawnKindDef gained
        // starvationPerInterval, the speed of the bar that starving fills and eating drains
        // (design 17 §4b, §4c). Taken from a freshly loaded pack.
        //
        // Moved an eighth time, 2026-09-18, in review: starvationPerInterval went 2 to 1. The
        // comment beside it read the needs cadence as 200 intervals a day when it is 400 — a
        // 60,000-tick day over the 150-tick cadence — so the bar filled four times faster than
        // every sentence describing it said, and severe malnutrition arrived on the fourth day
        // rather than the fifth. Nothing had ever measured it: the bands were pinned by setting
        // the bar by hand, and TheBarFillsAtTheCadenceItsCommentClaims is the test that now
        // holds the arithmetic and the tick path together. No golden moved, because no golden
        // window lets a need reach zero.
        //
        // Moved a ninth time, 2026-09-18, by the merge of the growing and rates branches: both
        // had moved the fingerprint that day, so neither parent's value described the union.
        // Growing's additions stand beside the rate integers unchanged — Work_Growing carries
        // no curve yet (design 22 §5) — and the value is taken from a freshly loaded pack.
        const ulong ContentFingerprint = 16616720228185092649UL;


        [Test]
        public void TheContentIsStillWhatItWas()
        {
            ulong actual = DefComparison.Fingerprint(ContentPack.Pawns(), "PawnContent");

            Assert.That(actual, Is.EqualTo(ContentFingerprint),
                "the pawn content has moved. If that was deliberate, set ContentFingerprint to " +
                $"{actual}UL and say what changed. If it was not, `git diff Assets/Odyssey/Defs/Core` " +
                "is what moved.");
        }

        /// <summary>
        /// The seam this row opened: the simulation finds its content without being handed a path,
        /// from the fast tier's bin directory and from Unity's ScriptAssemblies alike. Everything
        /// else in this file loads a pack explicitly, so nothing else would notice if the walk up
        /// to the repository root broke.
        /// </summary>
        [Test]
        public void TheContentPackIsFoundWithoutBeingToldWhereItIs()
        {
            PawnContent content = ContentPack.Pawns();

            Assert.That(content.Items, Has.Length.GreaterThan(0));
            Assert.That(content.Items[ItemIndex.Wood].defName, Is.EqualTo("Item_Wood"));
            Assert.That(content.Movement.movePerTick, Is.GreaterThan(0));
        }

        /// <summary>
        /// Each caller gets its own record, because <c>MineJobTests</c> writes to one it was
        /// handed. Sharing the record would leak that into every test that ran afterwards, and the
        /// leak would look like a flaky test rather than a cache.
        /// </summary>
        [Test]
        public void EveryCallerGetsItsOwnRecord()
        {
            PawnContent first = ContentPack.Pawns();
            PawnContent second = ContentPack.Pawns();
            Assert.That(first, Is.Not.SameAs(second));

            int was = first.StoneChanceOneIn;
            first.StoneChanceOneIn = was + 1;

            Assert.That(ContentPack.Pawns().StoneChanceOneIn, Is.EqualTo(was));
        }

        /// <summary>
        /// The seam a built player will need. Nothing builds one yet, so this is the only thing
        /// standing between <see cref="ContentPack.UseRoot"/> and being discovered broken on the
        /// day somebody first points it at <c>StreamingAssets</c>.
        /// </summary>
        [Test]
        public void ThePackCanBeToldWhereItIsInsteadOfFindingItself()
        {
            try
            {
                ContentPack.UseRoot(RepoPaths.CoreDefs);
                Assert.That(DefComparison.Fingerprint(ContentPack.Pawns(), "PawnContent"),
                    Is.EqualTo(ContentFingerprint), "an explicit root loaded different content");

                ContentPack.UseRoot(Path.Combine(RepoPaths.Root, "no", "such", "pack"));
                Assert.Throws<DefLoadException>(() => _ = ContentPack.Pawns());
            }
            finally
            {
                // Global state: leave it as the rest of the suite expects to find it.
                ContentPack.Reset();
            }

            Assert.That(DefComparison.Fingerprint(ContentPack.Pawns(), "PawnContent"),
                Is.EqualTo(ContentFingerprint), "Reset did not restore the repository's own pack");
        }

        /// <summary>
        /// The control. One value is changed in the loaded content and the fingerprint must move
        /// — otherwise the test above passes because the walk reaches nothing, which is the
        /// failure mode a reflective walk has.
        /// </summary>
        [Test]
        public void TheFingerprintNoticesAChangedField()
        {
            PawnContent content = PawnContent.FromDefs(LoadCore());
            ulong before = DefComparison.Fingerprint(content, "PawnContent");

            content.Items[ItemIndex.Wood].stackLimit += 1;

            Assert.That(DefComparison.Fingerprint(content, "PawnContent"), Is.Not.EqualTo(before));
            Assert.That(before, Is.EqualTo(ContentFingerprint), "the freshly loaded pack is the shipped one");
        }

        /// <summary>
        /// A deeper control: the walk must reach inside a list of nested records, which is where
        /// the need bands live and where a walk that stopped at the first object would silently
        /// stop looking.
        /// </summary>
        [Test]
        public void TheFingerprintReachesInsideNestedLists()
        {
            PawnContent content = PawnContent.FromDefs(LoadCore());
            ulong before = DefComparison.Fingerprint(content, "PawnContent");

            content.Needs[NeedIndex.Rest].bands[2].fallPerInterval += 1;

            Assert.That(DefComparison.Fingerprint(content, "PawnContent"), Is.Not.EqualTo(before));
        }

        /// <summary>
        /// The walk must be reading real values rather than folding a constant, which the two
        /// controls above would not catch if it returned the same number for everything.
        /// </summary>
        [Test]
        public void TheFingerprintIsNotTheEmptyWalk()
        {
            Assert.That(DefComparison.Fingerprint(ContentPack.Pawns(), "PawnContent"),
                Is.Not.EqualTo(DefComparison.Fingerprint(new PawnContent(), "PawnContent")));
        }

        /// <summary>
        /// Every skill's tables came through the loader's array parsing, which did not exist
        /// before this row. Asserted on their own because an empty array compares equal to an
        /// empty array, so a table that failed to parse would look like agreement if both sides
        /// were empty — they are not, but the day one is, this says so.
        /// </summary>
        [Test]
        public void TheSkillTablesArrivedWhole()
        {
            PawnContent fromXml = PawnContent.FromDefs(LoadCore());

            foreach (SkillDef skill in fromXml.Skills)
            {
                Assert.That(skill.experienceToAdvance.Length, Is.EqualTo(skill.maxLevel), skill.defName);
                Assert.That(skill.decayPerDay.Length, Is.EqualTo(skill.maxLevel + 1), skill.defName);
                Assert.That(skill.Level(0), Is.Zero, skill.defName);
                Assert.That(skill.Level(skill.MaxExperience), Is.EqualTo(20), skill.defName);
                // a-01's cumulative figure, in thousandths: the whole reason the second slope is
                // what it is. If the table is ever edited into disagreeing with it, say so here.
                Assert.That(skill.MaxExperience, Is.EqualTo(265_000_000), skill.defName);
            }
        }

        /// <summary>
        /// Both skills inherit one abstract parent, so the tables are written once. Proving the
        /// inheritance actually ran matters: without it both children would bind with empty
        /// tables and <see cref="SkillDef.Level"/> would throw on its first index.
        /// </summary>
        [Test]
        public void TheAbstractSkillParentIsNotItselfContent()
        {
            DefDatabase defs = LoadCore();

            Assert.That(defs.Table<SkillDef>().Count, Is.EqualTo(SkillIndex.Count),
                "the abstract parent reached the game, or a skill is missing from the content");
        }

        /// <summary>
        /// A content error names the pack, the file and the line. Three in-memory documents
        /// rather than the real pack, because the real pack is required to be correct.
        /// </summary>
        [Test]
        public void AMisspeltFieldNamesTheFileAndTheLine()
        {
            var loader = new DefLoader();
            PawnContent.Register(loader);
            loader.AddSource(new InMemoryDefSource("Test").Add("Broken.xml", @"<Defs>
  <ItemDef>
    <defName>Item_Typo</defName>
    <stackLmit>4</stackLmit>
  </ItemDef>
</Defs>"));

            var error = Assert.Throws<DefLoadException>(() => loader.Load())!;

            Assert.That(error.Message, Does.Contain("Broken.xml"));
            Assert.That(error.Message, Does.Contain("(2)"), "the line of the offending Def is not reported");
            Assert.That(error.Message, Does.Contain("stackLmit"));
        }

        /// <summary>
        /// A Def the handle order requires and the content does not have is refused by name at
        /// world construction, rather than leaving a null in an array for the first tick to trip
        /// over half an hour into a headless run.
        /// </summary>
        [Test]
        public void AMissingDefIsRefusedByName()
        {
            var loader = new DefLoader();
            PawnContent.Register(loader);
            loader.AddSource(new InMemoryDefSource("Test").Add("Thin.xml", @"<Defs>
  <ItemDef><defName>Item_Meal</defName></ItemDef>
</Defs>"));

            var error = Assert.Throws<DefLoadException>(() => PawnContent.FromDefs(loader.Load()))!;

            Assert.That(error.Message, Does.Contain("Item_Salvage").Or.Contain("NeedDef"));
        }

    }
}
