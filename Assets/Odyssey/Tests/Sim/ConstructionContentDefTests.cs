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
        /// table's third row, `Building_Bed` (footprint 2, passable, rotatable, takes quality).
        /// </summary>
        const ulong BuildingFingerprint = 14464343702532602950UL;

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
