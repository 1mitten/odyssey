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
        // 2026-09-21: Appended Building_Shelf at handle 7 — edifice 13 (CoreContent.EdificeShelf),
        // the colony's first buildable *store*. Passable and needsClearCell like the bed, 5 stuff
        // and 180 ticks like the bed, and rotatable because a shelf has a front and a back: it is
        // drawn with its carcass against one side of its cell, so a shelf that could not be turned
        // would face the same way in every room. It takes no quality — a container's tier would be
        // read by nothing, and the pane's owner row keys off a non-zero quality.
        // Same day, same row: BuildingDef gained `storageSlots`, and the shelf declares 8. A field
        // rather than a rule keyed off the edifice id, for the reason `needsClearCell` is one — it
        // is a fact about the shape of the thing — and it is what makes a second, larger store one
        // row of content rather than a second code path.
        // 2026-09-22: Appended Building_Campfire at handle 8 — edifice 14, the first heat source
        // (design 28 §7) and the one building whose `heatPerPass` is not zero. Blocking, wanting a
        // clear cell like the bed, 3 stuff and 60 ticks. Written at handle 7 / edifice 13 and moved
        // here on the merge with main: the shelf reached main first and both numbers are contracts,
        // so the later branch is the one that moves — the same rule Building_Bed records at 5.
        // The same merge added `heatPerPass` to BuildingDef, which is nought on every other row.
        // 2026-09-23: Appended Building_Conduit, Building_Generator and Building_Heater at handles
        // 9, 10 and 11 (design 32, power). The conduit is edifice 0 and `conduit`: a line in its
        // own layer, always wood (then), 1 a cell, 40 ticks. The generator is edifice 15,
        // two cells, rotatable, 1,000 W, a 75-wood hopper burning 22 a day at full load, 400 heat
        // at full load, 30 stuff and 600 ticks. The heater is edifice 16, 175 W, 1,000 heat while
        // powered, 10 stuff and 240 ticks. BuildingDef gained `conduit`, `fixedStuff` (since gone),
        // `powerOutputW`, `powerDrawW`, `fuelItem`, `fuelCapacity` and `fuelPerDay`, nought or -1
        // on every other row.
        // Same day, second interview (design 32 §14): `fixedStuff` is gone and `partItem` /
        // `partCount` replace it — a second payment in one fixed item beside the chosen material.
        // The conduit is all part (costCount 0, one scrap metal); the generator takes 20 scrap
        // metal and the heater 5 beside their wood or stone.
        // Same day, third look (design 32 §14c): the heater rotates. Its facing is drawing only
        // and backs on to a wall where there is one; nothing in the simulation reads it.
        // 2026-09-23, the combat contracts step (design 33 §4, §5): BuildingDef gained
        // `maxHitPoints`, what a finished thing has when it is struck (C6) — wall 300, floor 250,
        // deck plate 150, ladder 80, bed 120, door 160, shelf 100, all INVENTED and C6's to tune.
        // Added to the XML and the code oracle together, and taken from a freshly loaded pack.
        // Moved again, 2026-09-24, merging main (power) into the combat line: neither side's number
        // covers the merged table, so it is re-taken from a freshly loaded pack. Power's three rows
        // and the campfire carry no maxHitPoints yet (0); C6 gives them one when anything strikes.
        const ulong BuildingFingerprint = 1802851213417361357UL;

        /// <summary>
        /// The material table as it stands, U27's <c>workOffsetTicks</c> included (wood 0, stone
        /// 15). Update this only when you meant to change a material's numbers, and say what moved
        /// in the commit message.
        /// </summary>
        const ulong StuffFingerprint = 4054578596745551293UL;

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
