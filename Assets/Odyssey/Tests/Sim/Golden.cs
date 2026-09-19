#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The committed state hashes: what these worlds came to last time somebody looked, written
    /// down so that the next change has to answer for moving them.
    ///
    /// <para><b>What this catches that the existing determinism tests do not.</b>
    /// <c>HeadlessRunTests</c> builds the same world twice in one process and requires the two to
    /// agree, which proves the simulation is deterministic but says nothing about whether it still
    /// does what it did yesterday — two runs of a broken build agree perfectly. A committed number
    /// is the other half: it compares today's run against a value baked on another machine, in
    /// another process, weeks earlier. That is the only test here that can notice a change nobody
    /// intended.</para>
    ///
    /// <para><b>Two hashes per case, and the pair is the diagnosis.</b> A single number says
    /// "something moved" and leaves you bisecting. <see cref="Generated"/> is taken before the
    /// first tick and covers worldgen alone; <see cref="Simulated"/> is taken after
    /// <see cref="Ticks"/> and covers everything. If both moved, the generator changed and the
    /// simulation inherited it. If only the second moved, the board is identical and a system
    /// changed. That distinction is most of the work of reading a failure, and it costs one
    /// <c>ulong</c>.</para>
    ///
    /// <para><b>Re-baking is meant to be deliberate.</b> Run with <c>ODYSSEY_REGOLDEN=1</c> and
    /// the tests print replacement values instead of asserting; paste them in and say in the
    /// commit message what you changed and why the numbers moved. A golden updated without that
    /// sentence is a golden that has stopped being a test.</para>
    ///
    /// <para><b>All six numbers moved on 2026-09-17, and here is the sentence.</b> The list of
    /// standing buildings entered the state hash (<c>EdificeSaveSection</c>): what a wall is made
    /// of was outside it until then, so a wooden wall and a stone wall in the same cell hashed
    /// identically — measured, not supposed, by
    /// <c>EdificeRoundTripTests.AWallsMaterialIsInTheStateHash</c>. <b>Both</b> numbers moved in
    /// every case, including the barren meadow that has nothing standing on it at all, which is
    /// the expected shape rather than a surprise: an empty list still contributes its count.
    /// Nothing about how any of these worlds is generated or simulated changed — the hash simply
    /// sees more of what was always there, which is the same thing that happened to all of them
    /// when OQ-50 put the cell grid in.</para>
    ///
    /// <para><b>And moved again the same day, for a second reason worth separating from the
    /// first.</b> Deconstruct added a tenth job. <c>JobSystem</c> hashes a completed-and-failed
    /// tally <i>per job</i>, sized from the job table, so a tenth job adds one more zero to that
    /// walk — which moves the <see cref="Case.Generated"/> number before a single tick has run.
    /// It looks alarming and is not: the failure message points at the generator, and the
    /// generator is untouched. Anything that changes the <i>length</i> of a hashed per-job or
    /// per-work-type array will do this, and the way to tell it apart from a real generator change
    /// is that the grid's own hash is unmoved.</para>
    ///
    /// <para><b>Moved a third time the same day by U37, starting skills, merged on top of
    /// deconstruct — and only the way the plan asked.</b> Every <c>Simulated</c> value below moved
    /// again and no <c>Generated</c> one did on top of the merge, proof that the roll
    /// (<see cref="Pawns.StartingSkillsSystem"/>) happens on the world's first tick rather than
    /// during <c>ColonyScenario.Place</c>, which runs before <c>Generated</c> is taken. See
    /// <c>StartingSkillsTests</c> for the direct, non-hash evidence of the same thing. Rebaked
    /// once, on top of deconstruct's numbers, not twice: the values below are the merge's own,
    /// not U37's original branch numbers, because those were baked against a
    /// <c>StartingSkill</c> salt that collided with <c>DeconstructRefund</c> — see
    /// <c>PawnPurpose</c>.</para>
    ///
    /// <para><b>Moved a fourth time by U40, and this one moved <i>both</i> numbers — which is the
    /// interesting part.</b> A pawn now carries its own <c>RollSeed</c>, and that seed is in
    /// <c>Pawn.ContributeTo</c>. U37's roll happens on the first tick and so moved only
    /// <c>Simulated</c>; the seed is assigned at <b>placement</b>, inside <c>ColonyScenario.Place</c>,
    /// which runs before <c>Generated</c> is taken — so every value below moved. Nothing about any
    /// of these worlds changed: each colonist's seed here is the world's own, which is what it
    /// always rolled from, and the skills these colonies come to are byte-for-byte the ones they
    /// came to yesterday. What changed is that the hash can now see the number they were rolled
    /// from. <c>StartingSkillsTests</c> carries the non-hash evidence.</para>
    ///
    /// <para>The same commit stopped <c>ScenarioDef.Playtest</c> giving starting orders, and that
    /// moved <b>nothing here</b>, because every case below builds on <c>ScenarioDef.Bare</c>, which
    /// has never given any.</para>
    ///
    /// <para><b>Moved a fifth time, 2026-09-17, by the pickup gaining a duration</b> (owner: "there
    /// should be time spent motion down, picking up object and standing up"). A colonist now spends
    /// <c>PawnContent.LiftTicks</c> — 48, the 0.8 s the drawn gesture always took — stooping,
    /// taking the thing and straightening up, where before that was one tick. Every haul and every
    /// delivery in these windows is therefore 47 ticks longer, so the colonies reach a different
    /// state.</para>
    ///
    /// <para><b>All three <see cref="Case.Simulated"/> values moved and no <see cref="Case.Generated"/>
    /// one did, which is the signature that says the re-bake is what it claims.</b> The duration is
    /// content, content is not hashed, and nothing about placement changed — so a moved
    /// <c>Generated</c> here would have meant something else had come along with it. Checked by
    /// running the table before re-baking and reading which assertion failed: all three failed on
    /// the second, which is the one that fires only after the first has passed.</para>
    ///
    /// <para><b>Moved a <b>sixth</b> time, in the same breath, by the beds, and only the two boards with
    /// something standing.</b> <c>PlacedEdifice</c> gained <c>Facing</c>, <c>Quality</c> and
    /// <c>Owner</c>, all hashed by <c>EdificeSaveSection</c>, and a construction site gained a
    /// hashed facing byte. The barren meadow did not move at all — it has an empty edifice list
    /// and never places a site, so the new fields contribute nothing — which is the control that
    /// says the generator itself is untouched. The wooded board's trees and the city's walls are
    /// generator-stamped records, and every one of them now contributes three more zeros to the
    /// walk; rebaked once on top of U40's numbers, not twice — see the values below.</para>
    ///
    /// <para><b>The two landed together and were baked once, and the shape of the re-bake is what
    /// says it is honest.</b> The pickup duration and the bed's three edifice fields reached this
    /// file from opposite branches. Read what moved: <b>the barren meadow did not move at all</b>
    /// — it keeps the number main gave it — because it has an empty edifice list and never places
    /// a site, so the beds contribute nothing to it. The wooded and city boards' <c>Generated</c>
    /// values are <b>exactly the beds branch's own</b>, unchanged by the merge, because a duration
    /// is content and content cannot move a hash taken before the first tick. Only their
    /// <c>Simulated</c> numbers are new to both branches, which is the one place two changes could
    /// combine. Any other pattern would have meant something had come along uninvited.</para>
    ///
    /// <para><b>Moved an eighth time, 2026-09-18, in review, and this one is the hash seeing more
    /// rather than the colony doing anything different.</b> Two things WS1 had left: the four
    /// accumulators were hashed divided back to whole ticks, which threw away three decimal places
    /// the moment a rate stopped being exactly 1,000; and the three toils with no rate — eat,
    /// sleep, wait — still counted plain ticks into the same field the work toils counted
    /// thousandths into, so they divided to zero and reached the hash not at all. Both are fixed
    /// together, because they are one question about one field.</para>
    ///
    /// <para><b>That the run itself is unchanged was measured, not argued.</b> With the review's
    /// other three fixes in place and only these two lines reverted, all three numbers come back
    /// to the values this table held before — 5397578720920683558, 7046263050932287688 and
    /// 9209903446312531288. So the colonists walked the same walks and swung the same swings;
    /// what moved is what the hash is able to notice about them. All three <see cref="Case.Generated"/>
    /// values are untouched, as they must be, since nothing here runs before the first tick.</para>
    ///
    /// <para><b>Moved a seventh time by WS3, and for the first time it is the colonists and not
    /// the hash that changed.</b> Every colonist now walks at a pace of her own — rolled from her
    /// seed and id inside ±15 per cent — and at a condition that starvation will learn to take
    /// from; the composed rate reaches <c>Pawn.MoveProgress</c>, and mid-step milliwork values
    /// differ the moment a colonist is not walking at exactly 1,000. Read the shape:
    /// <b>all three <see cref="Case.Simulated"/> values moved and no <see cref="Case.Generated"/>
    /// one did</b>, the signature of a simulation change with the generator untouched — the pace
    /// roll is keyed the way passions and starting skills are, but it is <i>drawn lazily on first
    /// read</i>, so placement's dice are exactly what they were. The golden windows hold no
    /// standing orders, so what moved is the walks themselves: idle colonists crossing their
    /// boards at 850 to 1,150 a tick instead of in step. WS2's rate work moved none of these by
    /// the same reasoning recorded two entries down, and this entry is the re-bake the unit's row
    /// promised would be deliberate.</para>
    ///
    /// <para><b>Moved for the growing zones (U46), 2026-09-18, and all six numbers again.</b>
    /// <c>GrowingZones</c> registers hashed state on every colony, and an empty contribution
    /// still contributes its count — the same shape as the edifice list's first re-bake. Nothing
    /// about generation or simulation moved: the fields are new and empty, and both numbers move
    /// in every case for it.</para>
    ///
    /// <para><b>Moved for the growing jobs (U47), 2026-09-18, and all six numbers again.</b>
    /// <c>Work_Growing</c> and <c>Skill_Growing</c> made every pawn's work-priority and skill
    /// arrays one slot longer, and the pawn hash walks both — the same shape as the tenth job's
    /// extra tally slot, seen from the pawn side this time. Both numbers move in every case,
    /// including the barren meadow, and nothing about generation or simulation moved.</para>
    ///
    /// <para><b>Moved a ninth time, 2026-09-18, by the terrace guard, and <i>only the wooded
    /// board</i>.</b> <c>TreePass</c> refuses a tree in a cell at the foot of a terrace step,
    /// because presentation fills that cell with a bank and the tree is sheared off by it — see
    /// <c>TerraceFoot</c>. So the played board grows a few dozen fewer trees, and both its numbers
    /// moved: <c>Generated</c> because a tree is an edifice in the grid and the grid is hashed
    /// before the first tick, and <c>Simulated</c> because it inherits that board.</para>
    ///
    /// <para><b>Moved a tenth time the same day, by the hop's price, and this time the two boards
    /// with steps in them moved and the flat one did not.</b> <c>MoveCost.JumpUp</c> went from 135
    /// to 240 because the owner saw a colonist climb a terrace faster than one walked beside it —
    /// the arithmetic is in the constant's own comment. A price is not content and nothing is
    /// placed differently, so <b>no <see cref="Case.Generated"/> value moved</b>; the wooded board
    /// and the ruined city both have one-block steps on them, so their colonists reach a different
    /// state over 10,000 ticks and both <c>Simulated</c> values did. <b>The barren meadow did not
    /// move at all</b> — <c>MakeBarren</c> is one flat table, there is no step on it to hop, and a
    /// colonist who never hops cannot notice what hopping costs. That is the control, and it is a
    /// sharper one than usual: it separates "the price changed" from "everything moved".</para>
    ///
    /// <para><b>Moved an eleventh time the same day, when the growing branch met main a second
    /// time, and the meadow is the control again.</b> Main had re-baked for the terrace guard and
    /// the hop’s price; the growing side had re-baked for the zones and the growing work. Each
    /// side’s numbers are true only of its own code, so neither side’s table survives and the
    /// merged values below are the merged run’s. Read the shape: <b>the barren meadow did not
    /// move at all from the growing side’s values</b> — both of main’s changes are step-priced,
    /// <c>MakeBarren</c> has no step to guard or hop, so its board and its walks are byte-for-byte
    /// what the growing side measured. The wooded board’s <see cref="Generated"/> is this
    /// merge’s own — the terrace guard’s few dozen fewer trees plus growing’s counted fields,
    /// each of which alone had already moved one parent — and its <see cref="Simulated"/> adds
    /// the hop’s price to the growing side’s walks. The ruined city’s <see cref="Generated"/>
    /// stayed on the growing side’s value because its generator has no <c>TreePass</c>, while its
    /// <see cref="Simulated"/> moved for the hop alone, which is exactly what the tenth entry
    /// predicts for it.</para>
    ///
    /// <para><b>The other two cases did not move at all, and that is the control.</b> The barren
    /// meadow grows no trees and the ruined city's generator has no <c>TreePass</c> in it, so a
    /// guard on tree placement can reach neither — measured by running the whole table and reading
    /// which assertions failed: one, the wooded board's, on the <c>Generated</c> value. Anything
    /// else moving would have meant something had come along uninvited.</para>
    /// </summary>
    public static class Golden
    {
        /// <summary>One world, pinned: how to build it, how long to run it, and what it came to.</summary>
        public sealed class Case
        {
            public string Name = string.Empty;
            public GridSize Size;
            public uint Seed;
            public int Ticks;
            public MapType Map = MapType.Natural;

            /// <summary>Trees, streams and ore, as the played board has. False is the bare board.</summary>
            public bool Wooded;

            /// <summary>
            /// The hash of the whole world <b>before the first tick</b>: the generated board and
            /// the colony placed on it, which is what <c>GoldenMasterTests.FullHash</c> covers.
            ///
            /// <para><b>Not worldgen alone, despite the name</b>, and that matters when it moves.
            /// It folds in <c>SimWorld.ComputeStateHash</c>, so anything hashed by any component —
            /// a pawn, its skills, the designation grid, the construction grid — moves this number
            /// without a single generator pass having changed. Measured 2026-09-17: the build
            /// pipeline moved all three cases here while the grid hash alone stayed byte-identical
            /// to main on all three boards.</para>
            ///
            /// <para>The pair is still a diagnosis, one step weaker than the class comment claims:
            /// if only <see cref="Simulated"/> moved, nothing about the starting world changed and a
            /// system did. If this one moved, compare the grid hash by hand before concluding the
            /// generator changed.</para>
            /// </summary>
            public ulong Generated;

            /// <summary>The hash after <see cref="Ticks"/> ticks.</summary>
            public ulong Simulated;

            public ColonyWorld Build() =>
                ColonyWorld.Build(Size, Seed, ScenarioDef.Bare(), mapType: Map, wooded: Wooded);

            public override string ToString() => Name;
        }

        /// <summary>
        /// The one that runs on every save. Small and short on purpose: the fast tier is a thing
        /// people run while working, and a gate nobody waits for is a gate nobody runs.
        /// </summary>
        public static readonly Case Meadow = new Case
        {
            Name = "meadow 60x60x16 barren, seed 4242, 5,000 ticks",
            Size = new GridSize(60, 60, 16),
            Seed = 4242u,
            Ticks = 5_000,
            Map = MapType.Natural,
            Wooded = false,
            Generated = 17287400559466587244UL,
            Simulated = 15649756692576152247UL,
        };

        /// <summary>
        /// The board the game actually loads — 120 x 120 x 16, wooded — at the size and shape
        /// <c>OdysseyBootstrap</c> builds. The bare board above is the clean baseline; this is the
        /// one whose regression a player would actually meet.
        /// </summary>
        public static readonly Case PlayedBoard = new Case
        {
            Name = "wooded meadow 120x120x16, seed 1, 10,000 ticks",
            Size = new GridSize(120, 120, 16),
            Seed = 1u,
            Ticks = 10_000,
            Map = MapType.Natural,
            Wooded = true,
            Generated = 8724219219982949137UL,
            Simulated = 7454526726644784434UL,
        };

        /// <summary>
        /// The ruined city, which is still generated and still tested even though the scene does
        /// not load it. Its passes are the ones nothing else exercises.
        ///
        /// <para><b>Re-baked twice on 2026-09-18, and the merge of the two is the interesting
        /// part.</b> Both branches moved this one number for different reasons — the ladder shaft
        /// rule on `main`, the rates work on its own branch — so the merge conflicted here and
        /// <i>neither</i> side's value was right for the merged code. It was baked afresh, which is
        /// the only honest resolution of a golden conflict: taking either side would have committed
        /// a number nothing had produced.</para>
        ///
        /// <para>It came back as the rates branch's number exactly, which looks wrong and is not.
        /// Connectors are <b>derived and not hashed</b> — the same rule that keeps support and the
        /// region graph out — so the shaft rule moves a hash only where it changes what a pawn
        /// actually <i>does</i>. On main's trajectory a colonist used one of the city's newly live
        /// ladders inside the window; on the rates trajectory, with every work and move rate
        /// shifted, none does. The ladder code is present in the merge — checked, not assumed.</para>
        ///
        /// <para><b>Simulated re-baked 2026-09-18</b> for the ladder shaft rule, and the failure
        /// named its own cause: the board generated identically and only the run diverged. A ladder
        /// used to need a slab <i>directly above</i> it to register a connector at all, so every
        /// ladder the city stamps under an open cell was dead; now a landing beside the top counts
        /// too, and the colony reaches places it could not. Generated is untouched, which is the
        /// evidence that no generator pass changed.</para>
        /// </summary>
        public static readonly Case City = new Case
        {
            Name = "ruined city 60x60x5, seed 9, 10,000 ticks",
            Size = new GridSize(60, 60, 5),
            Seed = 9u,
            Ticks = 10_000,
            Map = MapType.RuinedCity,
            Wooded = false,
            Generated = 13906133993818225881UL,
            Simulated = 2442616034903591755UL,
        };
    }
}
