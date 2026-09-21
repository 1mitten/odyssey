#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;

// DefLoaderTests declares its own fixture StuffDef in this namespace, which wins over a using-alias
// of the same name (see ConstructionTests.cs). This file's tests spell the real type
// ConstructionStuffDef instead.
using ConstructionStuffDef = Odyssey.Sim.Construction.StuffDef;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The buildable tables have not made the move <see cref="PawnContentDefTests"/> and
    /// <see cref="WorldContentDefTests"/> already made: <see cref="ConstructionContent"/>'s
    /// in-code arrays are still the oracle and <c>Defs/Core/World/Buildings.xml</c> is a mirror of
    /// them, which is the arrangement <see cref="ConstructionContent"/>'s own class comment and the
    /// XML file's own header both describe as already guarded. Neither guard existed until this
    /// file — U27 found the gap while adding <see cref="StuffDef.workOffsetTicks"/>, which is
    /// exactly the kind of value that could have been added to one table and not the other with
    /// every existing test still green.
    ///
    /// <para><b>Two guards, because the two tables can drift in different ways.</b> The comparison
    /// against the code oracle catches the XML and the code disagreeing with each other, field by
    /// field, the way the reference's own pre-migration <c>PawnContentDefTests</c> did. The pinned
    /// fingerprint catches either side moving without anyone saying so — the failure
    /// <c>WorldContentDefTests</c> found once the world tables stopped having a second copy to
    /// disagree with: editing rock's <c>workToClear</c> left every other test green, because the
    /// oracle and the content had become the same file agreeing with itself.</para>
    /// </summary>
    public class ConstructionContentDefTests
    {
        static DefDatabase LoadCore() => ContentPack.LoadCore(RepoPaths.CoreDefs);

        /// <summary>
        /// The building table as it stands. Update this only when you meant to change what can be
        /// built, and say what moved in the commit message. Moved 2026-09-17 by the bed: the
        /// table's third row, `Building_Bed` (footprint 2, passable, rotatable, takes quality) —
        /// twice in one day, the second time for its edifice id: 10 is the conifer's, and the bed
        /// moved to the next free id, 12.
        /// </summary>
        // U29 added Building_Floor: a slab at the cell's lower boundary, 4 stuff and 120 ticks,
        // and the `slab` field that tells Raise which of the two kinds of thing it is making.
        // U42 added Building_DeckPlate: the same slab laid on ground that is already there, 3
        // stuff and 60 ticks, and the `covering` field that inverts one question — it wants a cell
        // that IS floored and never asks the support rule, because it cannot fall.
        // U43 added Building_Ladder: an edifice like a wall, blocking false so it can be stood in,
        // 4 stuff and 90 ticks. It is the first buildable thing that goes up rather than sideways.
        // U45 appended Building_Bed at handle 5 — two cells, rotatable, quality-bearing, 5 stuff
        // and 180 ticks. It was written at handle 2 and moved here on the merge: the three above
        // reached main first and a handle position is a save contract.
        // 2026-09-18: Building_Ladder gained `rotates`. The owner reported a built ladder on the
        // wrong side of its cell — a free-standing one had nowhere to take a facing from and fell
        // back to north, which the player could neither predict nor change. A ladder fixed to a
        // wall still hugs it; the rotation only decides where there is nothing to hug.
        // 2026-09-20: Appended Building_Door at handle 6 — edifice 2 (CoreContent.EdificeDoor),
        // passable (blocking false), 5 stuff and 135 ticks matching a wall. Gained `rotates`
        // so doors can be oriented by the player before placement and at wall corners/reveals.
        //
        // RF1 appended Building_Pillar at handle 7 - a column in one cell, blocking, 3 stuff and
        // 90 ticks, whose only job is to hold up the slab above it. It needed no change to the
        // support solver at all: IsGrounded already ends at `Edifice[below] >= 0`, so a pillar has
        // grounded the slab over it for as long as the solver has run and there was simply nothing
        // that could build one.
        //
        // U44 appended Building_Stair at handle 8 - rotatable, blocking false, 6 stuff and 150
        // ticks. It landed as TWO adjacent cells on one layer finishing as two edifice values
        // (`secondEdifice`: EdificeStairLower at the head, EdificeStairUpper at the far cell,
        // which is what worldgen stamps). The new field went on every row, so the whole table's
        // fingerprint moved and not only the stair's.
        //
        // Seven and eight, not six and seven: the door reached main first and took 6, so this
        // branch moved down by one when it merged. Handle order is the save contract and positions
        // are append-only; it is safe only because no save with a pillar or a stair in it has ever
        // left this branch.
        //
        // **2026-09-21: the stair became ONE cell and the fingerprint moved again.** The owner
        // played the two-cell flight and asked for one square, flush with the floor above; the
        // pack's SM_Bld_Base_Stairs_02 rises a full 3.00 m in one cell and had been listed as an
        // optional variant in the research all along. So on the stair's row: `footprint` 2 -> 1,
        // `secondEdifice` EdificeStairUpper -> 0, and `edifice` EdificeStairLower -> the new
        // EdificeStairFull (13). Nothing else in the table moved, and the costs did not: a stair
        // still costs 6 wood and 150 ticks. docs/design/28-stairs.md 10.
        const ulong BuildingFingerprint = 4885284094868351627UL;

        /// <summary>
        /// The material table as it stands, U27's <c>workOffsetTicks</c> included (wood 0, stone
        /// 15). Update this only when you meant to change a material's numbers, and say what moved
        /// in the commit message.
        /// </summary>
        const ulong StuffFingerprint = 5872933115437906559UL;

        /// <summary>
        /// The quality tiers as they stand: Poor 85, Normal 100, Decent 112, Uber 125, Epic 140
        /// per cent of a plain bed's rest — the owner's interview answers, 2026-09-17. Update this
        /// only when the owner retunes a tier, and say which one moved.
        /// </summary>
        const ulong QualityFingerprint = 11231177996547656315UL;

        [Test]
        public void TheBuildingTableIsStillWhatItWas()
        {
            ulong actual = DefComparison.Fingerprint(ConstructionContent.Buildings, "Buildings");

            Assert.That(actual, Is.EqualTo(BuildingFingerprint),
                "the building table has moved. If that was deliberate, set BuildingFingerprint to " +
                $"{actual}UL and say what changed. If it was not, `git diff Assets/Odyssey/Sim/Construction` " +
                "is what moved.");
        }

        [Test]
        public void TheStuffTableIsStillWhatItWas()
        {
            ulong actual = DefComparison.Fingerprint(ConstructionContent.Stuffs, "Stuffs");

            Assert.That(actual, Is.EqualTo(StuffFingerprint),
                "the material table has moved. If that was deliberate, set StuffFingerprint to " +
                $"{actual}UL and say what changed. If it was not, `git diff Assets/Odyssey/Sim/Construction` " +
                "is what moved.");
        }

        [Test]
        public void TheQualityTableIsStillWhatItWas()
        {
            ulong actual = DefComparison.Fingerprint(QualityContent.Qualities, "Qualities");

            Assert.That(actual, Is.EqualTo(QualityFingerprint),
                "a quality tier's rest effectiveness moved. If the owner asked for it, set " +
                $"QualityFingerprint to {actual}UL and name the tier in the commit message; if not, " +
                "`git diff Assets/Odyssey/Sim/Construction` is what moved.");
        }

        /// <summary>
        /// The load-bearing test of this row: the XML that ships beside the game says the same
        /// thing as the code that runs it, field by field, including the two new offset fields.
        /// </summary>
        [Test]
        public void TheXmlIsTheSameContentAsTheCodeOracle()
        {
            DefDatabase defs = LoadCore();

            var differences = new List<string>();
            differences.AddRange(DefComparison.Differences(
                ConstructionContent.Buildings, ConstructionContent.BuildingsFromDefs(defs), "Buildings"));
            differences.AddRange(DefComparison.Differences(
                ConstructionContent.Stuffs, ConstructionContent.StuffsFromDefs(defs), "Stuffs"));
            differences.AddRange(DefComparison.Differences(
                QualityContent.Qualities, QualityContent.QualitiesFromDefs(defs), "Qualities"));

            Assert.That(differences, Is.Empty,
                "the XML mirror and the code oracle have parted:" + Environment.NewLine +
                string.Join(Environment.NewLine, differences));
        }

        /// <summary>
        /// The control the comparison needs to be believed: a reflective walk that quietly reached
        /// nothing would pass the test above forever. This perturbs exactly the new offset field
        /// U27 added and requires the walk to name it.
        /// </summary>
        [Test]
        public void TheComparisonNoticesTheOffsetChanging()
        {
            ConstructionStuffDef[] fromXml = ConstructionContent.StuffsFromDefs(LoadCore());
            fromXml[StuffHandle.Stone].workOffsetTicks += 1;

            var differences = DefComparison.Differences(ConstructionContent.Stuffs, fromXml, "Stuffs");

            Assert.That(differences, Has.Count.EqualTo(1), string.Join(Environment.NewLine, differences));
            Assert.That(differences[0], Does.Contain("workOffsetTicks"));
        }

        /// <summary>As above, for the fingerprint rather than the field-by-field comparison.</summary>
        [Test]
        public void TheFingerprintNoticesTheOffsetChanging()
        {
            ConstructionStuffDef[] fromXml = ConstructionContent.StuffsFromDefs(LoadCore());
            ulong before = DefComparison.Fingerprint(fromXml, "Stuffs");
            Assert.That(before, Is.EqualTo(StuffFingerprint), "the freshly loaded pack is the shipped one");

            fromXml[StuffHandle.Stone].workOffsetTicks += 1;

            Assert.That(DefComparison.Fingerprint(fromXml, "Stuffs"), Is.Not.EqualTo(before));
        }

        /// <summary>
        /// The running game reads the shipped numbers, factor and offset together — the arithmetic
        /// itself is <c>ConstructionTests</c>' job, this only pins that the numbers WorkFor
        /// multiplies and adds are the ones this file otherwise guards.
        /// </summary>
        [Test]
        public void TheRunningGameReadsTheFactorAndTheOffsetTogether()
        {
            Assert.That(ConstructionContent.WorkFor(BuildingHandle.Wall, StuffHandle.Wood), Is.EqualTo(135));
            Assert.That(ConstructionContent.WorkFor(BuildingHandle.Wall, StuffHandle.Stone), Is.EqualTo(244));
        }

        /// <summary>
        /// The whole pack loads through the one registration point, so a file added to it that
        /// nobody registered a type for fails here rather than in whichever test happens to load
        /// the pack next.
        /// </summary>
        [Test]
        public void TheWholeCorePackLoads()
        {
            DefDatabase defs = LoadCore();

            Assert.That(defs.Table<BuildingDef>().Count, Is.EqualTo(ConstructionContent.BuildingOrder.Length));
            Assert.That(defs.Table<ConstructionStuffDef>().Count, Is.EqualTo(ConstructionContent.StuffOrder.Length));
            Assert.That(defs.Table<QualityDef>().Count, Is.EqualTo(QualityContent.QualityOrder.Length));
        }

        /// <summary>
        /// The tier table the running game reads — the levels the owner chose, not a factor the
        /// formula computes. Pinned beside the fingerprint for the same reason the wall's own
        /// work numbers are.
        /// </summary>
        [Test]
        public void TheTiersRestAtTheOwnersNumbers()
        {
            Assert.That(QualityContent.RestEffectiveness(QualityHandle.Poor), Is.EqualTo(85));
            Assert.That(QualityContent.RestEffectiveness(QualityHandle.Normal), Is.EqualTo(100));
            Assert.That(QualityContent.RestEffectiveness(QualityHandle.Decent), Is.EqualTo(112));
            Assert.That(QualityContent.RestEffectiveness(QualityHandle.Uber), Is.EqualTo(125));
            Assert.That(QualityContent.RestEffectiveness(QualityHandle.Epic), Is.EqualTo(140));
            Assert.That(QualityContent.RestEffectiveness(QualityHandle.None), Is.EqualTo(100),
                "tier 0 is \"takes no quality\" and answers a plain bed's rest, never an index error");
        }
    }
}
