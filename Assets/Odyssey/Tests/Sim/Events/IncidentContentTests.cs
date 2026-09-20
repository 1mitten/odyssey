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
        const ulong ContentFingerprint = 18026134764698235758UL;

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

        [Test]
        public void AStackRangeWrittenBackwardsFailsAtLoad()
        {
            DefDatabase defs = IncidentContent.Register(new DefLoader())
                .AddSource(new InMemoryDefSource("Test").Add("Incidents.xml", Pack(stackMin: 20, stackMax: 10)))
                .Load();

            Assert.Throws<DefLoadException>(() => IncidentContent.FromDefs(defs, ContentPack.Pawns()));
        }

        /// <summary>
        /// A pack of one incident that pays out nothing, so the loader's own reference check —
        /// which wants the named item in the same database — stays out of tests about other
        /// things.
        /// </summary>
        static string Pack(string worker = "SupplyDrop", int stackMin = 1, int stackMax = 1) =>
            "<Defs><IncidentDef><defName>Incident_SupplyDrop</defName>" +
            "<bulletinKey>ui.bulletin.supplydrop</bulletinKey>" +
            $"<worker>{worker}</worker>" +
            $"<stackMin>{stackMin}</stackMin><stackMax>{stackMax}</stackMax>" +
            "</IncidentDef></Defs>";
    }
}
