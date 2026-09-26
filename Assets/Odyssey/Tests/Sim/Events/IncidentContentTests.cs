#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Events;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim.Events
{
    /// <summary>
    /// The events content (design 23 §4): what the pack declares, that it binds, and that it has
    /// not moved without anybody saying so.
    /// </summary>
    public class IncidentContentTests
    {
        // The pinned shape of Assets/Odyssey/Defs/Core/Events/Incidents.xml. Baked 2026-09-20 with
        // the one incident, the supply drop: ten to twenty meals, first day quiet, two days
        // between refires. The gates are read by nothing yet and are pinned anyway, so the day a
        // storyteller reads them it reads the numbers that were written.
        //
        // Moved the same day, after the first look: fallTicks 120 to 360, because two seconds
        // landed almost before it had been seen falling, and a description, which is the debug
        // menu's Events tab tooltip.
        //
        // 2026-09-23, power (design 32 §14): Incident_ScrapDrop appended at index 1 — the supply
        // drop's own worker with scrap metal for cargo, fifteen to thirty, weight 60, the supply
        // drop's gates otherwise. The owner's second source of scrap metal beside the wreckage.
        //
        // 2026-09-24, bandits stealing (design 33 §17): Incident_Theft (Bad) and
        // Incident_BanditLeft (Neutral) appended at 2 and 3, both naming the Recorded worker —
        // written down by the world when a bandit leaves the board, never fired. Default gates.
        //
        // 2026-09-24, the bandit (design 42): Incident_BanditLeft renamed Incident_BanditLeft,
        // its label and bulletin key with it. Index 3 unchanged; the ledger keeps indices.
        //
        // 2026-09-25, medical supplies (design 37 §5), at the merge with main: Incident_MedicalDrop
        // appended at index 4, after the bandit's two — the supply drop's worker again, four to
        // eight medical supplies, weight 40 (invented).
        //
        // 2026-09-25, raids (design 55): Incident_Raid appended at index 5 — Bad, ThreatBig, worker
        // Raid, the first Def with a per-worker block (<raid>), gates earliestDay 3 and
        // minRefireDays 4 (invented). No golden moved: no golden fires an incident.
        //
        // 2026-09-26, the storyteller (design 59 §3): every Def gains populationGain (false), and
        // Incident_Raid's minRefireDays goes 4 -> 2 (design 59 §2 ruling 16: the storyteller paces,
        // the Def is a floor). No golden moved: no golden chooses a storyteller.
        //
        // 2026-09-26, colony strength (design 59 §4b): the raid block's perColonist and
        // daysPerExtra are gone for raidersPerStrengthPerMille 3300 and a day ramp (700 per mille
        // on day 0 to 1000 by day 48, then +50 a season to 1500). No golden fires a raid.
        const ulong ContentFingerprint = 1162007863741320009UL;

        [Test]
        public void TheContentIsStillWhatItWas()
        {
            ulong actual = DefComparison.Fingerprint(ContentPack.Incidents().Defs, "IncidentContent");

            Assert.That(actual, Is.EqualTo(ContentFingerprint),
                "the events content has moved. If that was deliberate, set ContentFingerprint to " +
                $"{actual}UL and say what changed. If it was not, `git diff Assets/Odyssey/Defs/Core/Events` " +
                "is what moved.");
        }

        /// <summary>
        /// The simulation's list and the contracts assembly's handle table are two spellings of
        /// one order, and a save carries the index. Held to the same length and the same names.
        /// </summary>
        [Test]
        public void TheOrderIsTheHandleTable()
        {
            Assert.That(IncidentContent.Order.Length, Is.EqualTo(IncidentHandle.Count));
            Assert.That(IncidentContent.Order[IncidentHandle.SupplyDrop], Is.EqualTo("Incident_SupplyDrop"));

            IncidentContent content = ContentPack.Incidents();
            Assert.That(content.Count, Is.EqualTo(IncidentHandle.Count));
            for (int i = 0; i < content.Count; i++)
                Assert.That(content.Defs[i].defName, Is.EqualTo(IncidentContent.Order[i]));
        }

        [Test]
        public void TheSupplyDropIsBoundToMealsAndItsWorker()
        {
            IncidentContent content = ContentPack.Incidents();
            IncidentDef def = content.Defs[IncidentHandle.SupplyDrop];

            Assert.That(content.ItemIndex[IncidentHandle.SupplyDrop], Is.EqualTo(ItemIndex.Meal));
            Assert.That(content.Workers[IncidentHandle.SupplyDrop], Is.InstanceOf<SupplyDropWorker>());
            Assert.That(def.bulletinKey, Is.EqualTo("ui.bulletin.supplydrop"));
            Assert.That(def.favourability, Is.EqualTo(IncidentFavourability.Good));
            Assert.That(def.fallTicks, Is.GreaterThan(0), "a drop that lands on the tick it is launched is not seen falling");
            Assert.That(def.stackMax, Is.LessThanOrEqualTo(content.Defs.Length > 0
                ? ContentPack.Pawns().Items[ItemIndex.Meal].stackLimit : int.MaxValue),
                "a drop bigger than a stack is a drop that cannot land in one cell");
        }

        /// <summary>
        /// Scanned again here, independently of the registry's own filter, so a filter that
        /// quietly stopped matching something would fail rather than agree with itself — the
        /// shape of <c>WorkGiverRegistrationTests</c>.
        /// </summary>
        [Test]
        public void EveryWorkerInTheSimulationAssemblyAnswersToItsName()
        {
            var expected = new List<Type>();
            foreach (Type type in typeof(IncidentWorker).Assembly.GetTypes())
                if (typeof(IncidentWorker).IsAssignableFrom(type) && !type.IsAbstract) expected.Add(type);

            Assert.That(expected, Is.Not.Empty);
            foreach (Type type in expected)
            {
                var worker = (IncidentWorker)Activator.CreateInstance(type)!;
                Assert.That(IncidentWorkerRegistry.Resolve(worker.Name), Is.InstanceOf(type),
                    $"{type.Name} does not answer to its own name '{worker.Name}'");
            }
            Assert.That(IncidentWorkerRegistry.Resolve("NoSuchWorker"), Is.Null);
        }

        [Test]
        public void AnIncidentNamingAnUnknownWorkerFailsAtLoadNamingIt()
        {
            DefDatabase defs = IncidentContent.Register(new DefLoader())
                .AddSource(new InMemoryDefSource("Test").Add("Incidents.xml", Pack(worker: "Nothing")))
                .Load();

            var thrown = Assert.Throws<DefLoadException>(() => IncidentContent.FromDefs(defs, ContentPack.Pawns()));
            Assert.That(thrown!.Message, Does.Contain("Nothing").And.Contain("SupplyDrop"));
        }

        /// <summary>
        /// The worker's own rules are checked at load, by the worker (2026-09-20): a stack range
        /// written backwards, or a drop that names nothing to drop, both throw naming the Def.
        /// </summary>
        [Test]
        public void AStackRangeWrittenBackwardsFailsAtLoad()
        {
            var thrown = Assert.Throws<DefLoadException>(() =>
                new SupplyDropWorker().Validate(new IncidentDef { defName = "Incident_Test", item = "Item_Meal", stackMin = 20, stackMax = 10 }, ContentPack.Pawns()));
            Assert.That(thrown!.Message, Does.Contain("Incident_Test").And.Contain("20–10"));
        }

        [Test]
        public void ASupplyDropThatPaysOutNothingFailsAtLoad()
        {
            Assert.Throws<DefLoadException>(() =>
                new SupplyDropWorker().Validate(new IncidentDef { defName = "Incident_Test" }, ContentPack.Pawns()));
        }

        /// <summary>
        /// A second kind of incident is not held to the first's rules: the base worker accepts a
        /// Def with none of the supply drop's fields set, which is the shape a raid or an
        /// encounter will take (design 23 §8). The registry scans the simulation assembly only,
        /// so this worker cannot be named from content and is exercised directly.
        /// </summary>
        [Test]
        public void AWorkerWithNoRulesOfItsOwnAcceptsABareDef()
        {
            Assert.DoesNotThrow(() =>
                new BareWorker().Validate(new IncidentDef { defName = "Incident_Test" }, ContentPack.Pawns()));
        }

        /// <summary>
        /// A pack of one incident that pays out nothing, so the loader's own reference check —
        /// which wants the named item in the same database — stays out of the unknown-worker
        /// test, which fails before any worker is asked.
        /// </summary>
        static string Pack(string worker) =>
            "<Defs><IncidentDef><defName>Incident_SupplyDrop</defName>" +
            "<bulletinKey>ui.bulletin.supplydrop</bulletinKey>" +
            $"<worker>{worker}</worker>" +
            "</IncidentDef></Defs>";
    }

    /// <summary>The smallest worker there is: no rules, never fires. Test assembly only.</summary>
    public sealed class BareWorker : IncidentWorker
    {
        public override string Name => "TestBare";
        public override bool CanFireNow(IncidentContext ctx, in IncidentParms parms) => false;
        public override bool TryExecute(IncidentContext ctx, in IncidentParms parms) => false;
    }
}
