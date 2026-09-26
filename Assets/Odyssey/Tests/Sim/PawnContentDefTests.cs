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
        // Growing's additions stand beside the rate integers, and the value is taken from a
        // freshly loaded pack.
        //
        // Moved a tenth time, 2026-09-19, when Work_Growing gained a curve: rateSkill 4, base 600,
        // slope 100 — cutting's numbers exactly, because they are the two plant work types and a
        // difference would be a claim needing a measurement. The move arrived with the growing work
        // ("the hoe pays by skill") and had no paragraph of its own, so this is it written down
        // late rather than a second account of it.
        //
        // Moved an eleventh time, 2026-09-20, by storage S1: `ItemDef` gained `category`, and all
        // seven commodities declare one — ration pack and carrots are Food, the other five are
        // Materials, including salvage, which is reclaimer feedstock rather than a made thing
        // (docs/plans/storage.md decision 25). The field defaults to Materials rather than to
        // nought on purpose: nought is Food, so a commodity that forgot to declare itself would
        // quietly join the pantry.
        //
        // This line conflicted on the skills merge, 2026-09-21, and the rule for next time is worth
        // more than the outcome. A fingerprint is a hash of the whole content set, so when two
        // branches have each moved it, neither number is the merged one and picking a side is
        // always wrong: re-measure. Here the re-measurement returned main's value unchanged, and
        // the reason is the point — the skills branch had edited only the XML COMMENTS in
        // WorkTypes.xml, and a comment is not loaded, so it is not hashed. Its own older number
        // predated storage's category field. Checking cost one filtered test run; assuming would
        // have been wrong in either direction.
        //
        // NO GOLDEN MOVED for either change, and that was measured rather than assumed. A rate
        // change to a work type usually moves every Simulated hash and the growing curve moved
        // none, because the golden worlds are bare seeds with no growing zone painted on them, so
        // no sow or harvest job is created and the curve is never consulted. A category is read
        // only by a storage filter, and every filter in a golden colony accepts everything. The
        // starting zone moving to a real `StorageZones` does move them, and that is measured
        // separately. The day a golden window includes a zone, the curve will move a hash and that
        // will be correct.
        //
        // Moved a twelfth time, 2026-09-22, by animals (design 29 §1): SpeciesDef arrived with
        // three rows — the person, the midden hog and the duct rat — and PawnKindDef gained
        // `species`, with two animal kinds beside the colonist's. Taken from a freshly loaded
        // pack. Every Simulated AND Generated golden moved with it, and not for this: the pawn's
        // kind entered the hash the same day (§6), and the golden colony probe run on both
        // branches diffs clean in every number — the hash sees one more zero per colonist.
        //
        // Moved a thirteenth time, 2026-09-22, the same day: the midden hog's movePerMille went
        // 700 to 600 after the owner's first look at the trot ("way too fast"). No golden moved:
        // no golden world has an animal in it.
        //
        // Moved a fourteenth time, 2026-09-22: the duct rat's traverseMode went Colonist to the
        // new Climber, which climbs anything a colonist can and does not swim (owner: "animals
        // can't swim by default"). No golden moved.
        // 2026-09-23: SpeciesDef gained `nocturnal` and the duct rat sets it (design 30 §4).
        //
        // Moved a fifteenth time, 2026-09-23, by the draft (design 33 §2c): Job_DraftHold and
        // Job_Goto at drivers 12 and 13, and PawnTuningDef gained draftQuietTicks (10,000 — four
        // in-game hours, the reference's auto-undraft). Taken from a freshly loaded pack.
        //
        // Moved a sixteenth time, 2026-09-23, after the first draft playtest: MovementDef gained
        // draftedPacePerMille (2,000 — a drafted colonist runs; owner: "when you are drafted you
        // should walk faster/run"). No golden moved: nobody in a golden window is drafted.
        //
        // Moved again, 2026-09-23, merging temperature into a main that had taken combat and
        // wildlife. Neither side's number covers the merged pack - main's has the combat and
        // wildlife tuning, this branch's has TemperatureDef - so it is re-taken from a freshly
        // loaded pack rather than adopted from either. Third pass of the same resolution in one
        // day; main is moving under this branch faster than it is being reviewed.
        //
        // 2026-09-23, power (design 32): three JobDefs appended — Job_LayConduit (driver 14),
        // Job_RemoveConduit (15) and Job_Refuel (16), after the draft's two. The first two train construction and settle
        // as building does; refuelling trains hauling. Every golden moved with them, because the
        // job system hashes a completed and failed counter for every def — measured to be those
        // six zeros and nothing else (Golden.cs).
        //
        // 2026-09-23, the same branch's second interview (design 32 §14): Item_Salvage is scrap
        // metal now — labelled so, and stacking to 50 where it lay one to a cell — because power
        // lines and machines are built from it. No golden moved: the starting kit's scatter still
        // places one piece to an empty cell, which is what it always placed.
        //
        // Moved again, 2026-09-24, merging main (combat, wildlife, temperature) into power: the
        // draft's two jobs keep drivers 12 and 13 and power's three follow at 14-16. Neither
        // side's number covers the merged pack, so it is re-taken from a freshly loaded pack.
        // Moved a seventeenth time, 2026-09-23, by the combat contracts step (design 33 §5), which
        // claims every handle the combat line needs at once: five jobs (Job_AttackMelee, Job_Flee,
        // Job_Downed, Job_Equip, Job_Rescue at drivers 14 to 18), Skill_Melee, Work_Rescue, four
        // weapons (Item_Bat, Item_Crowbar, Item_Machete, Item_ArcBlade, each with a weapon block),
        // PawnKind_Bandit with PawnKindDef.faction (the two animal kinds Wild), SpeciesDef's
        // combat fields with the owner's pools (person 100, hog 60, rat 15), death at -500 per mille,
        // revenge (hog 700, rat 50) and the two natural attacks, and a new CombatDef carrying the
        // owner's hit and dodge curves and fists. Taken from a freshly loaded pack. The goldens
        // moved in the same commit, and not for any of the numbers: see Golden.cs.
        //
        // Moved an eighteenth time, 2026-09-23, by the seam review of the same step: PawnKindDef
        // gained `weapon` and PawnKind_Bandit names Item_Machete (design 33 §1: "debug-spawned,
        // armed"; IWeaponRules.ArmOnSpawn puts it in the hand). No golden moved: no golden spawns
        // a bandit.
        //
        // Moved a nineteenth time, 2026-09-23, at the C2/C3 integration: CombatDef gained
        // `rechooseTicks` (300), lane A's constant on the attack driver, proposed for the Def in its
        // hand-over because Defs were frozen while the lanes ran. Same value, so no behaviour and no
        // golden moved.
        //
        // Moved a twentieth time, 2026-09-24, by the third playtest's round (design 33 §9b): CombatDef
        // gained the owner's critical and knockback numbers — critChancePerMille 100,
        // critPerMillePerFourLevels 10, critDamagePerMille 1,500, knockbackPerMille 500,
        // knockbackBluntPerMille 750 — and knockedDownTicks 90. No golden moved: no golden window
        // fights, so no swing is ever decided in one.
        //
        // Moved again, 2026-09-24, merging main (power, research) into the combat line: power's three
        // jobs keep drivers 14-16, so the combat five move from 14-18 to 17-21 (nothing combat shipped
        // had saved them). Neither side's number covers the merged pack, so it is re-taken from a
        // freshly loaded pack rather than adopted from either.
        //
        // Moved again, 2026-09-24, by medical supplies (design 37, MD1): Item_MedicalSupplies appended
        // at item 11 (Medicine, stackLimit 10, healPerUnit 40), and ItemDef gained healPerUnit, zero
        // on every other item. Taken from a freshly loaded pack.
        //
        // And again the same day by MD2 (design 37): Skill_Medicine, Work_Doctor (rateSkill 6 on
        // growing's curve), Job_Treat and Job_Patient, and CombatDef's eight treatment integers.
        // Taken from a freshly loaded pack.
        //
        // Moved a twenty-first time, 2026-09-24, by C5, friendly fire (design 33 §12): two thoughts
        // appended at indices 6 and 7 — Thought_AttackedByColonist (-80, one day, once) and
        // Thought_ColonistDied (-60, three days, three deep), the owner's -8 and -6 on our scale of
        // thousandths. No golden moved: no golden window has a colonist hurt by a colonist or a
        // death, and a memory is hashed only once a pawn has one.
        //
        // Moved a twenty-second time, deliberately, 2026-09-24, by the owner's answers to Phase 4
        // (design 33 §14e): ThoughtDef gained renewsOnRepeat, false everywhere but
        // Thought_AttackedByColonist, so a second swing renews the day rather than being dropped.
        // No golden moved: no other thought renews, and no golden window has friendly fire.
        //
        // Moved a twenty-third time, deliberately, 2026-09-24, by drafted colonists helping (design
        // 33 §15): CombatDef gained helpRadiusCells (8, INVENTED), how near another colonist's
        // fight must be for a drafted colonist on her hold to join it. No golden moved: no golden
        // window drafts anybody.
        // And a twenty-fourth, the same day, by doors holding bandits out (design 33 §16):
        // PawnKindDef gained traverseMode, empty everywhere but PawnKind_Bandit (Bandit), and
        // PawnContent the resolved KindMode table. No golden moved: no golden has a bandit, and
        // every other kind resolves to its species' mode exactly as before. The value below is the
        // two together, measured on the merge rather than taken from either side.
        //
        // Moved a twenty-fifth time, deliberately, 2026-09-24, by bandits stealing (design 33
        // §17): Job_Steal appended at 22, PawnKindDef gained motive (None everywhere but
        // PawnKind_Bandit, Loot), and PawnContent the KindMotive table. No golden moved: no
        // golden has a bandit, and the job system hashes a job appended after the combat line's
        // only once it has run (JobSystem.HashedAlways).
        //
        // Moved a twenty-sixth time, deliberately, 2026-09-24, by the bandit (design 42): the kind
        // renamed PawnKind_Bandit -> PawnKind_Bandit (index 3 unchanged, so no save moves), its
        // traverse mode likewise, and PawnKindDef.weapon became weapons, the bandit's being a
        // crowbar or a bat where it was a machete. No golden moved: no golden has a bandit.
        //
        // Moved a twenty-seventh time, 2026-09-25, at the merge of medical supplies (design 37)
        // with main: Job_Treat and Job_Patient renumbered 22-23 -> 23-24, after Job_Steal, since
        // bandits shipped first. Neither side's number covers the merged pack, so it is re-taken
        // from a freshly loaded pack rather than adopted from either.
        //
        // 2026-09-25, design 46 §6: MovementDef gained jumpFailPerMille (30) and
        // jumpFailCarryingPerMille (2,000) — the jump over a one-cell stream falling short.
        //
        // Moved a twenty-eighth time, 2026-09-25, at the merge with main (weather, the stream
        // jump): neither side's number covers the merged pack, re-taken fresh.
        // Moved again, 2026-09-25, by design 45 (the scenery made real): the tuning's woodPerTree
        // is gone, because what a felled tree yields is its species' own now, in
        // World/WildPlants.xml. Taken from a freshly loaded pack.
        // 2026-09-25, design 46 §6: MovementDef gained jumpFailPerMille (30) and
        // jumpFailCarryingPerMille (2,000) — the jump over a one-cell stream falling short.
        // Both, 2026-09-25, on merging main into the scenery line: re-taken from the merged pack.
        //
        // And again the same day by M13 (design 45 §6): Job_Forage appended at 23, and
        // Item_Berries and Item_Mushrooms at 11 and 12 (Food, 60 and 70, stacks of 75). Taken from
        // a freshly loaded pack.
        // M13 on the merged line, 2026-09-25: re-taken from the merged pack.
        // The scenery line merged with medical supplies, 2026-09-25: Job_Forage renumbered 23 -> 25
        // and Item_Berries/Item_Mushrooms 11-12 -> 12-13, after main's. Re-taken from the merged pack.
        //
        // Moved a twenty-ninth time, deliberately, 2026-09-25, by the kitchen (design 48 §4-§5):
        // Skill_Cooking, Work_Cooking and Job_Cook appended; the three meals appended as items;
        // Recipe_Meal the first RecipeDef; ItemDef gained foodTier, rawIngredient, meat, ticksToRot
        // and ateThought, set on the ration pack and the carrots; Thought_AteMeal went from +20 to
        // +50 as the cooked meal's, and AteRation (+20), AteBurnt (-40) and AteRaw (-50) were
        // appended; and the work types' scan ranks moved to put cooking between growing and
        // cutting. Every golden moves with it, measured in the same commit.
        // Both lines together, 2026-09-25: the kitchen merged with the wild foods; Job_Cook
        // renumbered 25 -> 26 and the meals 12-14 -> 14-16. Re-taken from the merged pack.
        // 2026-09-25, design 47 §3 (R0, the ranged line's contracts): Job_AttackRanged, Skill_Shooting
        // and Item_Pistol appended; AttackDef gained a ranged block, CombatDef the shooting numbers
        // (the per-cell curve, the floor, cover, the dead zone, the scatter, the scan cadence) and
        // SpeciesDef interceptPerMille (person 400, hog 500, rat 40).
        // 2026-09-25, design 47 on the owner's first play ("keep it more accurate"): shootingPerCell
        // 876/943/983 and the pistol's bands 950/850/650/450; the pistol's label "pistol" (was sidearm).
        // 2026-09-25, design 47 §12: the pistol's ranged block gained its own melee blow (blunt, 5,
        // the fists' cadence) — an enemy within reach is clubbed, never shot.
        // The ranged line merged with main (medical supplies, the scenery), 2026-09-25: re-taken from the merged pack.
        // And merged with the kitchen (design 48), 2026-09-25: the ranged handles to 27 / 17 / 8; re-taken.
        //
        // Health (design 43), merged onto all of that, 2026-09-25: a new HealthDef, Health_Person,
        // carrying the six regions and the pain, blood, tend and fall numbers, named by
        // Species_Person's new `health` field. Its own Job_Tend, Item_Medkit and Doctor and
        // Medicine rows were dropped for design 37's (design 43 §15), so no handle moved. No golden
        // moved: the ledger is hashed only while a pawn has anything on it. Re-taken from the
        // merged pack.
        // 2026-09-25, design 53 §2-§3 (cover, CV1): CombatDef's coverPerMille slot deleted, and the cover
        // numbers added — fullFillCoverPerMille 750, the low and tall descent tangents 176/700 and 577/1732,
        // coverInterceptPerMille 500, coverCrouchPerMille 200; WildPlantDef coverPerMille and coverTall
        // (trees 250 tall, bushes 150 low).
        // Cover merged onto health, 2026-09-25: its three streams moved to the 22nd-24th; re-taken.
        //
        // 2026-09-25, raids (design 55 §8): PawnKind_Gunman appended at kind 4 — a hostile person
        // armed from a table of one, Item_Pistol, with the bandit's traverse mode and motive. No
        // golden moved: no golden spawns a hostile.
        // Raids merged onto cover, 2026-09-26: re-taken from the merged pack.
        // 2026-09-25, the culvert frog (design 30 §8): Species_CulvertFrog and PawnKind_CulvertFrog
        // appended (kind 4, species 3; kind 5 since the merge with raids), and SpeciesDef gained bankRadius and ignoresRain, both
        // nought/false on every other species. Taken from a freshly loaded pack.
        // 2026-09-26, the owner's first ask on the frog: bodyLengthMm 400 -> 870 and movePerMille
        // 800 -> 1,000, so the bigger frog hops a body and a half. Taken from a freshly loaded pack.
        // And SpeciesDef gained divergeRadius (frog 6; design 30 §8e), the same day.
        // The frog merged with cover, 2026-09-26: re-taken from the merged pack.
        // The frog merged with raids, 2026-09-26: the gunman keeps kind 4 and the frog moves to 5;
        // re-taken from the merged pack.
        // 2026-09-26, prisoners (design 59 P3, the contracts step): Skill_Social, Work_Warden
        // (order 8), Job_Capture to Job_Arrest (drivers 28 to 35) and Thought_Imprisoned,
        // Thought_ColonistArrested and Thought_WasArrested appended.
        // Merged with main (the frog), 2026-09-26: re-taken from the merged pack.
        const ulong ContentFingerprint = 10777248070777323500UL;


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
