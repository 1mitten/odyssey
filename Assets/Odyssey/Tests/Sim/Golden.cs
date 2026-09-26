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
    /// <para><b>All six moved again on 2026-09-21, the temperature review's fixes (design 28
    /// §12), and here is the sentence.</b> Two more fields entered the hash — a colonist's
    /// last-felt ambient (F8) and each room's energy residual (F7) — which is the whole of the
    /// <c>Generated</c> move on all three: with those two lines disabled every tick-zero hash
    /// came back to the committed value. <c>Simulated</c> moved on the two boards that have
    /// caverns and not on the barren one, and it was measured component by component: cells,
    /// pawns and edifices hash identically before and after on all three; only the thermal
    /// section differs, because a cavern now converges instead of stopping a cell count short
    /// (the residual) and a cavern under a cavern is no longer charged to the sky (F6). Nothing
    /// a colonist did changed.</para>
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
    /// <para><b>The other two cases did not move at all, and that is the control.</b> The barren
    /// meadow grows no trees and the ruined city's generator has no <c>TreePass</c> in it, so a
    /// guard on tree placement can reach neither — measured by running the whole table and reading
    /// which assertions failed: one, the wooded board's, on the <c>Generated</c> value. Anything
    /// else moving would have meant something had come along uninvited.</para>
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
    /// <para><b>Moved a twelfth time the same day, by main's own carry sounds and the title
    /// screen's bed (PR #133), and only the simulation moved.</b> All three
    /// <see cref="Case.Simulated"/> values are this merge's own measurement — the growing
    /// side's walks and #133's sounds in one run, neither parent's. And <b>every
    /// <see cref="Case.Generated"/> value landed on the growing side's own numbers to the
    /// digit</b>, which is the cleanest signature this file has ever shown: PR #133 touched no
    /// board, so the union's generated worlds are byte-for-byte the ones the eleventh entry
    /// below measured. The merge that sat between — main's #129 and #130 — moved nothing here
    /// at all, carried items and player shaders being hash-silent, which is why it has no
    /// entry of its own.</para>
    ///
    /// <para><b>Moved an eleventh time, 2026-09-20, by the events (design 23), all six numbers,
    /// and for the dullest of reasons: the hash sees more.</b> Two new components joined every
    /// colony — the incident ledger and the things in the air — and both hash their state before
    /// the first tick runs, which on a fresh board is four integers all reading zero. So every
    /// <c>Generated</c> value moved without a generator pass or a placement changing, and every
    /// <c>Simulated</c> value inherited it. Nothing in a tick draws a number or moves a thing
    /// unless an incident is fired, and none is fired without the debug menu's intent, so the
    /// colonies did nothing different — the round trips, the headless runs and the soak all
    /// agree with themselves as before. The control this time is the shape of the failure:
    /// all three <c>Generated</c> values moved together, including the barren meadow's, which
    /// no gameplay change has ever touched.</para>    /// </summary>
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
        /// <remarks>
        /// <b>All six numbers re-baked 2026-09-22 for animals</b> (design 29 section 6): a pawn's
        /// kind entered <c>Pawn.ContributeTo</c> beside its roll seed, so every Generated and
        /// Simulated hash moved by the hash seeing one more zero per colonist. Measured, not
        /// assumed: <see cref="GoldenColonyProbe"/> run on <c>main</c> and on the branch, same
        /// file, diffs clean in every number on all three boards. No golden world has an animal
        /// in it (the debug menu is the only spawner), so nothing here walks differently.
        /// <para><b>Two Simulated numbers re-baked again the same day, and this time the colony
        /// did change.</b> A job's expiry waits for the next cell boundary for every pawn now
        /// (design 29 section 3a), so a mental-break wander ends one step later than it did. The
        /// played board and the city moved; the bare meadow, where nobody breaks in the window,
        /// did not. The probe on <c>main</c> and here differs in one number on each of the two
        /// boards - the sum of the pawns' cells - and in nothing else: food, rest, items and
        /// orders identical. That is a step taken, not a hash seeing more.</para>
        /// </remarks>
        ///
        /// <remarks>
        /// <b>All three cases re-baked together on 2026-09-20, both halves of each.</b> A
        /// scenario's starting beds stopped being bare cells in the sleep chooser's list and
        /// became real two-cell beds raised through the construction grid
        /// (<c>ColonyScenario.RaiseAStartingBed</c> holds the measurement that forced it). A bed
        /// record is in the edifice list, which is hashed, so a colony that has five of them
        /// hashes differently from one that has none — and it does so <b>before a single tick
        /// runs</b>, which is why <c>Generated</c> moved on all three and is the evidence that
        /// this is the change and not a simulation system drifting underneath it. <c>Simulated</c>
        /// followed for the ordinary reason a divergent start diverges further: the colonists now
        /// sleep in beds they previously walked past, so their nights are spent in different
        /// cells.
        /// <para>No generator pass changed. The board is identical; what stands on it is not.</para>
        /// <para><b>And a second reason in the same commit, which is why the numbers here are not
        /// the ones the bed change alone produced.</b> <c>ColonyItems</c> hashed its things and
        /// not its stockpile zones or its bed list, both of which it had been saving since they
        /// existed — found by <c>OrdersSurviveASaveTests.EachOrderMovesTheStateHash</c>, which
        /// flips one bit of a zone's filter and asks whether the world noticed. It did not. Both
        /// are in the hash now, so every colony with a starting stockpile hashes differently
        /// again; the colony is not doing anything new, the hash is seeing more of it. That
        /// distinction is the whole of whether a re-bake is honest.</para>
        /// <para><b>Re-baked a third time the same day, on the merge with the events layer</b>
        /// (PR #140). Both branches had moved every number here for their own reasons — beds and
        /// zones on this side, the incident layer's hashed state on <c>main</c>'s — so the merge
        /// conflicted on all six and neither side's value was right for the merged code. Baked
        /// afresh from the merge, as the 2026-09-18 entry below says a golden conflict must be.
        /// <c>Generated</c> moved on all three because the events layer hashes before the first
        /// tick, exactly as it did on <c>main</c>. The played scenario losing its starting beds
        /// the same session moved <b>nothing</b> here: every case builds on <c>Bare</c>, which
        /// keeps its five.</para>
        ///
        /// <para><b>Re-baked a fourth time, 2026-09-20, by storage S1 — and measured rather than
        /// assumed, which is the whole of the paragraph above about honesty.</b> Two things moved
        /// the numbers and only one of them moved the colony.</para>
        ///
        /// <para>The hash sees a different shape: an item record gained <c>ContainerId</c>, and
        /// the zones left <c>ColonyItems</c> for <c>StorageZones</c> and
        /// <c>StorageSettingsTable</c>, which hash cells-with-priority and the filter table rather
        /// than the old per-pile walk. That alone moves <c>Generated</c> on all three, before a
        /// tick runs.</para>
        ///
        /// <para><b>The measurement.</b> A throwaway probe printed what each colony actually
        /// <i>does</i> — live things, per-def stacks, the sum of item cells, the loose and stored
        /// lister counts, the sum of pawn cells, total food and rest, standing orders — on this
        /// branch and on <c>main</c>, at generation and after the full run. <b>The meadow and the
        /// ruined city are identical in every one of those numbers, generated and simulated.</b>
        /// Their hashes moved and their colonies did not.</para>
        ///
        /// <para><b>The played board is not, and the difference is one cell.</b> It reads
        /// <c>loose=18 stored=2</c> where <c>main</c> read <c>loose=17 stored=3</c>. The cause was
        /// measured, not guessed: of the nine cells the scenario hands the starting zone, cell
        /// 180436 at (76, 63, L12) is refused by <c>StorageZones.SiteAllows</c> because <b>a tree
        /// stands in it</b> — walkable, not water, edifice 753. <c>AddStockpile</c> asked nothing
        /// of a cell, so that cell was in the zone and a starting item that landed on it counted
        /// as stored in a place nothing could ever be stored. It is loose now, and a hauler
        /// collects it. Every other number on that board matches <c>main</c> at generation, and
        /// <c>Simulated</c> follows for the ordinary reason a divergent start diverges further.
        /// The meadow and the city have no tree in their starting zones, which is exactly why
        /// they are unchanged.</para>
        ///
        /// <para><b>Re-baked a fifth time the same day, and this one is a rule change rather than
        /// a hash change.</b> A colony no longer starts with a stockpile (owner, on seeing S1's
        /// first build: <i>"there shouldn't be a default stockpile zone"</i>), so
        /// <c>ScenarioDef.stockpileCells</c> is nought and all three golden colonies now have
        /// <b>nowhere to haul anything to</b>. They fell, mine, eat and sleep as before and then
        /// leave what they cut where it fell, which is a different colony and rightly a different
        /// number. The tree of §7 is moot: there is no starting zone for it to stand in.</para>
        ///
        /// <para>The rest of the starting kit was measured either side of it rather than assumed:
        /// 5 colonists, 12 meals, 5 beds and 8 salvage on the wooded board, identical before and
        /// after. The ruined city places 7 salvage rather than 8, because the scatter retries once
        /// per spot in the pool and the pool is nine spots shorter — a retry artefact on the
        /// tighter board, not a space problem, and not worth engineering around for one piece of
        /// scrap. <c>ScenarioDefTests.AScenarioThatNamesNoStoreyPlacesExactlyWhereItAlwaysDid</c>
        /// carries the same note beside the two placement signatures it pins.</para>
        /// <para><b>Re-baked a sixth time, 2026-09-21, by storage S2 — and it is the first kind
        /// again, not the second.</b> <c>StorageUnits</c> is a hashed component, so every board in
        /// the game now contributes one more count to the walk, including the three below, none of
        /// which has a shelf on it. That moves all six numbers before a tick runs.</para>
        ///
        /// <para><b>Measured, and the instrument is committed this time.</b>
        /// <c>GoldenColonyProbe</c> prints what each colony is made of — live things, per-def
        /// stacks, the sum of item cells, the two lister counts, the sum of pawn cells, total food
        /// and rest, standing orders and zones — at generation and after the full run. It is
        /// written against nothing newer than <c>main</c> on purpose, so the same file runs on both
        /// branches; it was run on each and the two outputs <b>diff clean</b>. All three colonies
        /// are identical in every one of those numbers. The hash sees one more zero and the
        /// colonies do not know it. Earlier re-bakes used a throwaway probe and had to describe it
        /// afterwards; this one leaves the probe behind so the next re-bake starts with it.</para>
        ///
        /// <para><b>Re-baked a seventh time, 2026-09-23, by the draft (design 33 §2c) — the first
        /// kind again.</b> Two jobs joined the job table, and <c>JobSystem</c> hashes a completed
        /// and a failed counter per job def, so every board in the game contributes four more
        /// zeros before a tick runs: all six numbers move, the three here included. The drafted
        /// flag itself is hashed only while it is set, so it moved nothing. Measured with
        /// <c>GoldenColonyProbe</c> run on <c>claude/wildlife</c> and on this branch: the two
        /// outputs diff clean for all three colonies.</para>
        ///
        /// <para><b>Re-baked an eighth time, 2026-09-23, by power (design 32) — the first kind
        /// again.</b> Three job defs were appended (lay a line, take one up, refuel), and the job
        /// system hashes a completed and a failed counter for every def, so every board's walk
        /// gains six zeros and all six numbers move before a tick runs. The power grid itself adds
        /// nothing: it is empty on all three boards, and an empty grid contributes nothing to the
        /// hash by design (<c>PowerTests.AnEmptyGridAddsNothingToTheHashAndOneLineDoes</c>).</para>
        ///
        /// <para><b>Measured by the sharpest instrument available rather than the census:</b> the
        /// job system's hash was cut back, uncommitted, to the first twelve defs — the pre-power
        /// set — and all three boards then matched the <i>previous</i> committed values exactly,
        /// generated and simulated. So nothing but those six zeros moved: the new givers never
        /// fired and the new scan order changed no colonist's job on any board.</para>
        ///
        /// <para><b>Re-baked again on 2026-09-24, merging main into power.</b> Main had re-baked
        /// for the draft's two jobs and this branch for power's three; the merged job table has
        /// all five (drafting keeps 12 and 13, power follows at 14 to 16), so neither side's
        /// numbers were produced by the merged code. <c>GoldenColonyProbe</c> run on the merge and
        /// on <c>origin/main</c> (ee1f9fdc) <b>diffs clean</b> on all three boards: the hash sees
        /// three more pairs of zeros, and no colony does anything different.</para>
        ///
        /// <para><b>Re-baked an eighth time, 2026-09-23, by the combat contracts step (design 33
        /// §5) — the first kind again, and the last time the combat line moves these.</b> Every
        /// handle the line needs was claimed at once: five job defs (ten more zeros in the job
        /// counters), <c>Skill_Melee</c> (a sixth experience, passion and daily-gain slot on every
        /// colonist, rolled at spawn and on the first tick on streams that draw the first five
        /// exactly as before), <c>Work_Rescue</c> (a sixth priority) and four weapons (four more
        /// slots in every storage allow list). The combat state on a pawn, the corpse registry and
        /// the edifice damage store are hashed only while set, and nothing in these windows fights,
        /// so they moved nothing. <b>Measured</b>: <c>GoldenColonyProbe</c>, widened the same day to
        /// print mood, mid-step progress, the first five skills' experience and passions, jobs
        /// started and failed and every job def's completed and failed counts (the first fourteen,
        /// so the file runs unchanged on both sides), was run on <c>origin/main</c> (33525521) and
        /// on this branch: the two outputs diff clean for all three colonies. From here every
        /// combat lane asserts these six numbers unchanged (<c>docs/plans/combat-contracts.md</c>).</para>
        /// <para><b>All six moved again on 2026-09-23, on the merge of temperature into a main
        /// that had gained animals, and neither side's numbers were right for the merged code.</b>
        /// Both branches had moved all six — main for the pawn's kind entering the hash, this one
        /// for the colonist's last-felt ambient and each room's energy residual — so taking either
        /// side would have committed a number nothing had produced. Re-baked afresh, which is the
        /// only honest resolution of a golden conflict.</para>
        ///
        /// <para><b>Measured before they were written.</b> <c>GoldenColonyProbe</c> run on the
        /// merged branch and on <c>main</c>: the two outputs <b>diff clean</b>. Every census
        /// number — live things, per-def stacks, item cells, the two lister counts, pawn cells,
        /// total food, total rest, standing orders, zones — is identical on all three boards. The
        /// hash sees more; no colony does anything different.</para>
        ///
        /// <para><b>All six moved again on 2026-09-24, merging main (power, research) into the
        /// combat line's second and third units.</b> Both sides had moved them: main for power's
        /// three job defs (the job system hashes a completed and failed counter for every def),
        /// this branch for the combat state it hashes. Re-baked afresh from the merged code, for
        /// the reason given above. <b>Measured</b>: <c>GoldenColonyProbe</c>, reading the first
        /// seventeen job defs so it runs unchanged on both sides, was run on <c>main</c> (54df119a)
        /// and on the merge; the outputs are identical on all three boards.</para>
        ///
        /// <para><b>All six moved again on 2026-09-24, with medical supplies (design 37).</b>
        /// Every colonist carries a seventh work priority (<c>Work_Doctor</c>) and a seventh skill
        /// (<c>Skill_Medicine</c>), and the job system two more counter pairs (<c>Job_Treat</c>,
        /// <c>Job_Patient</c>); all are hashed. The twelfth item moved nothing: none of these
        /// colonies has a storage zone, so no allow list grew. The
        /// treatment cooldown is hashed only while set and nobody in these windows is hurt, so it
        /// moved nothing, and neither did the starting kit, which Bare does not carry.
        /// <b>Measured</b>: <c>GoldenColonyProbe</c> run on <c>main</c> (52f53112) and on this
        /// branch; the outputs are identical on all three boards, experience and passions
        /// included. The hash sees more; no colony does anything different.</para>
        ///
        /// <para><b>All six moved on 2026-09-25, by the weather (design 43, <c>weather-core</c>):
        /// the first change here that is a colony doing something different, and meant to be.</b>
        /// A new system is hashed (the sky: kind, intensity, the hand-over and the spell's end),
        /// which moves the generated numbers before a tick runs. It writes the outdoor curve's
        /// weather term, which moves the simulated numbers. <b>Measured</b>: <c>GoldenColonyProbe</c>
        /// run on <c>origin/main</c> and on the branch differs in <b>mood alone</b>, 200 lower on each
        /// board, as the rolled sky's cooler air crosses a comfort band. Food, rest, work
        /// progress, experience, passions, jobs started and finished, and where every colonist
        /// stands are identical on all three boards. The weather changed how the colonies feel,
        /// and nothing they did.</para>
        ///
        /// <para><b>One moved on 2026-09-25, the ruined city's <see cref="Case.Simulated"/>, by the
        /// rain touching the world (design 43, <c>weather-world</c>): pace and animals, and
        /// nothing else, as intended.</b> The sky map is derived and unhashed, so no
        /// <c>Generated</c> number moved, and neither did the meadow or the played board, because
        /// neither sees a drop inside its window: seed 4242 and seed 1 both roll a cloudy spell
        /// that lasts the whole run. Seed 9 rolls rain at intensity 459 from the first tick to the
        /// last — past the animals' gate of 400, and a pace of 955 for anyone out in it (30,129 of
        /// the 50,000 colonist-ticks were). <b>Measured</b> with <c>GoldenColonyProbe</c> on
        /// <c>origin/main</c> (fccdebcd) and on the branch, and on the branch twice more with one
        /// half switched off at a time. Food, rest, mood, experience and passions are identical in
        /// all four: nobody ate, slept, felt or worked differently. <b>Pace alone</b> leaves the
        /// jobs where they were (119 started against 118; 81 wanders and 29 waits against 79 and
        /// 30) and takes the summed move progress from 519,595 to 341,744 and every pawn to a
        /// different cell. <b>Shelter alone</b> is the jobs: 303 started, and waits from 30 to 220,
        /// which are the four animals standing under cover in the 120-tick waits
        /// <c>AnimalShelterThinkNode</c> gives them. Both together: 300 started, 74 wanders, 217
        /// waits, 341,744 progress.</para>
        ///
        /// <para><b>All six moved on 2026-09-25, with the kitchen (design 48, K1).</b> Every
        /// colonist carries an eighth work priority (<c>Work_Cooking</c>) and an eighth skill
        /// (<c>Skill_Cooking</c>), and the job system one more counter pair (<c>Job_Cook</c>); all
        /// are hashed. The kitchen itself is hashed only once a station has been used, and none of
        /// these colonies has one. Eating changed too — the best tier first, and a thought that is
        /// the food's own — but nobody in these windows gets hungry enough to eat.
        /// <b>Measured</b>: <c>GoldenColonyProbe</c> run on <c>main</c> (837c895a) and on this
        /// branch; the outputs are identical on all three boards, food, rest, mood, experience and
        /// every job counted included. The hash sees more; no colony does anything different.</para>
        ///
        /// <para><b>All six moved on 2026-09-25, by the ranged line's contracts step (design 47 R0):
        /// the hash seeing more, and no colony doing anything different.</b> Every person carries a
        /// seventh skill, passion and daily-gain slot — Shooting — and each colonist is dealt a
        /// level in it on the first tick, from the same stream the first six are dealt from and
        /// after them, so the six come out exactly as they were. That moves the generated numbers
        /// (the arrays are longer before a tick runs) and the simulated ones (the dealt level is
        /// hashed). <b>Measured</b>: <c>GoldenColonyProbe</c> on <c>origin/main</c> (804c2e6a) and on
        /// the branch is identical on all three boards, line for line — food, rest, mood, work
        /// progress, the shared skills' experience and passions, jobs, and where every pawn
        /// stands.</para>
        ///
        /// <para><b>And again at the ranged line's merge with <c>main</c> (2a1cfa63), for the same
        /// reason.</b> Medical supplies had taken the seventh skill slot first, so Shooting is the
        /// eighth, <c>Job_AttackRanged</c> the twenty-seventh job and the pistol the fifteenth
        /// item: longer hashed tables, dealt after everything main deals. <b>Measured</b> the same
        /// way: <c>GoldenColonyProbe</c> on <c>origin/main</c> and on the merge is identical on all
        /// three boards, all nine census lines.</para>
        ///
        /// <para><b>And a third time at the merge with the kitchen (<c>origin/main</c> fc6b8ba6).</b>
        /// Cooking took the eighth skill, so Shooting is the ninth, <c>Job_AttackRanged</c> the
        /// twenty-eighth job and the pistol the eighteenth item. <b>Measured</b> the same way; the
        /// merge commit says what the probe found.</para>
        /// </remarks>

        public static readonly Case Meadow = new Case
        {
            Name = "meadow 60x60x16 barren, seed 4242, 5,000 ticks",
            Size = new GridSize(60, 60, 16),
            Seed = 4242u,
            Ticks = 5_000,
            Map = MapType.Natural,
            Wooded = false,
            // Re-baked 2026-09-25 at the merge of medical supplies (design 37) with main
            // (ODYSSEY_REGOLDEN=1), on top of the weather's own re-bake: the combined job, item,
            // skill and incident tables moved the hash the same way any content append does.
            // And merged with the kitchen (fc6b8ba6), 2026-09-25: Shooting the ninth skill; the probe diffs clean.
            Generated = 14881731598722590511UL,
            Simulated = 5300718913518332604UL,
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
            // 2026-09-23, wildlife (design 30): the meadow is seeded with nine animals at tick
            // zero, so both values move; the bare meadow did not, and the colony probe says the
            // colonists' economy is identical — the difference is the animals and nothing else.
            // And again the same afternoon for the scatter (design 30 §2: sounders apart and
            // loose); the probe read the same as the first time — the animals' cells, nothing else.
            // 2026-09-23 again, the draft's two job defs (see the meadow's remarks): four more
            // zeros in the job counters; the probe diffs clean.
            // 2026-09-23, the combat contracts step (the meadow's remarks): the combat line's
            // handles, hashed as more zeros; the widened probe diffs clean against origin/main.
            // 2026-09-25, the scenery made real (design 45, M5): the trees carry their species
            // and the undergrowth pass placed bushes, each an edifice with its flag, so the
            // generated board moved. The probe against main: the generated census is identical
            // (the same people, the same items, the same starting cells) and the simulated one
            // differs only where it should - the fifteen colonists wander through bushes at a
            // price, so they made 95 wanders in the ten thousand ticks where they made 105, and
            // ended on different cells. Food, rest, mood and experience are unchanged. The bare
            // meadow and the city did not move: neither grows a bush.
            // 2026-09-25, jumping a one-cell stream (design 46): Simulated only, and this board
            // only — the bare meadow and the city have no stream and did not move, nor did
            // Generated. The colonists behaved differently, and in one way: they take different
            // routes. The probe against main is identical in items, needs, mood, experience,
            // passions and failed jobs; only where they stand, the progress into their steps and
            // the wander legs started (105 -> 98 in the window) differ. Re-baked again on merging
            // the weather from main: Generated is main's, and the probe against main (0dff2b36)
            // differs in exactly the same three numbers and nothing else.
            // Re-baked again 2026-09-25 at the merge of medical supplies (design 37) with main
            // (ODYSSEY_REGOLDEN=1): the combined job, item, skill and incident tables.
            // 2026-09-25, both at once on merging main into the scenery line: the bushes' wander
            // price and the stream jump together. Re-taken from the merged code, not adopted from
            // either side; the probe against main is in the merge commit's message.
            // 2026-09-25 again, M13 (design 45 §6): the map lays its loose stones and first
            // mushrooms - 680 stone in 136 stacks and 89 mushrooms in 22, by the probe - and one
            // more stack of mushrooms came up in the ten thousand ticks. Every colonist number is
            // the M5 run's to the digit (cells, food, rest, mood, progress, the 95 wanders): the
            // colony has no store, so nobody carried a stone, and nobody was hungry enough to
            // walk to a mushroom. The bare meadow and the city did not move.
            // Both lines together, 2026-09-25: M13's stones and mushrooms on the merged M5-and-jump
            // board; re-taken from the merged code.
            // The scenery line merged with medical supplies (design 37), 2026-09-25: re-taken from the
            // merged code; the probe against main is in the merge commit's message.
            // The kitchen merged with the wild foods, 2026-09-25: an eighth work priority and skill
            // on every colonist and Job_Cook's counter pair, on the wild-food board. Re-taken from
            // the merged code; GoldenColonyProbe on main (2a1cfa63) and on the merge is identical on
            // all three boards, so the hash sees more and no colony does anything different.
            // And merged with the kitchen (fc6b8ba6), 2026-09-25: Shooting the ninth skill; the probe diffs clean.
            // 2026-09-25, nobody over anybody and nothing in a tree (design 31 §20, 20 §14, 23 §11):
            // Simulated only. A wander leg now refuses a cell another pawn stands on or is heading
            // to, so people and animals take different legs. GoldenColonyProbe against the branch's
            // base: items, food, rest, mood, experience, passions and failed jobs identical; only
            // where they stand, the progress into their steps and the wanders (99 -> 100) differ.
            // 2026-09-25, the culvert frog (design 30 §8): the meadow's table gained frogs on the
            // bank and its density went 15 -> 21, so the board is seeded with three more animals
            // (pawns 15 -> 18). GoldenColonyProbe against main (4c40e189): items, experience, passions and
            // failed jobs identical; food, rest and mood differ by exactly three animals' untouched
            // needs (3 x 800 / 800 / 600), and the rest — pawn cells, progress, wanders and waits
            // started — is the animals' own. The colonists did the same things. The bare meadow
            // and the city did not move: neither table has a frog.
            // 2026-09-26, "more of them": the frog's weight 2 -> 4, groups 3-5, density 21 -> 27,
            // and its pace 800 -> 1,000. Pawns 15 -> 22 against main (4c40e189); items,
            // experience, passions and failed jobs identical, food/rest/mood differ by exactly
            // seven animals' untouched needs (7 x 800 / 800 / 600). The colonists did the same things.
            // Then the frogs' legs diverge from each other (design 30 §8e): Simulated only; the
            // probe differs from the line above in the animals' wander count alone (217 -> 216).
            // Merged with main (wander claims, design 31 §20), 2026-09-26: re-taken from the merged code.
            Generated = 5971917863521832692UL,
            Simulated = 6269988894017776259UL,
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
        /// <para><b>Simulated re-baked 2026-09-20</b> for functional doors and the auto-closing
        /// <see cref="DoorSystem"/>: ruined city templates stamp <see cref="CoreContent.EdificeDoor"/>,
        /// which now rebuilds <see cref="Pathing.NavFlags.Door"/> on the nav graph via
        /// <see cref="Construction.ConstructionGrid.RebuildDoors"/> and ticks through the door system,
        /// charging opening movement cost and managing auto-close timeouts during traversal.
        /// Generated is untouched, confirming worldgen is unchanged.</para>
        /// </summary>
        public static readonly Case City = new Case
        {
            Name = "ruined city 60x60x5, seed 9, 10,000 ticks",
            Size = new GridSize(60, 60, 5),
            Seed = 9u,
            Ticks = 10_000,
            Map = MapType.RuinedCity,
            Wooded = false,
            // 2026-09-23, wildlife (design 30): the ruin is seeded with its rats and hogs.
            // 2026-09-23 again, the draft's two job defs; the probe diffs clean.
            // 2026-09-23, the combat contracts step; the widened probe diffs clean.
            // 2026-09-25, weather-world: it rains here all run — pace and animal shelter (remarks).
            // Re-baked again 2026-09-25 at the merge of medical supplies (design 37) with main
            // (ODYSSEY_REGOLDEN=1): the combined job, item, skill and incident tables.
            // And merged with the kitchen (fc6b8ba6), 2026-09-25: Shooting the ninth skill; the probe diffs clean.
            // 2026-09-25, nobody over anybody (design 31 §20): Simulated only, the wander legs
            // (74 -> 75) and where everybody stands; every other census number identical.
            Generated = 14180088319569370523UL,
            Simulated = 6894148259867231222UL,
        };
    }
}
