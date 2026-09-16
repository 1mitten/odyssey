#nullable enable
using System;
using System.Diagnostics;
using System.Text;
using NUnit.Framework;
using Odyssey.Sim.Defs;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// What the Def loader costs at content scale (OQ-17).
    ///
    /// <para><c>04-data-model.md</c> §9 listed startup cost as unmeasured, and the design that
    /// depends on the answer — a hash-keyed binary Def cache, and the source-generated binder that
    /// would replace load-time reflection — is deliberately unbuilt "until a measurement asks for
    /// it". This is that measurement, so the decision can be made on a number.</para>
    ///
    /// <para><b>Fixture shape.</b> 3,000 Defs across five types, inheritance three deep, which is
    /// meant to be a plausible shipped content set rather than a worst case. Every Def carries
    /// fields of each supported kind and a cross-reference to resolve, because parsing is only
    /// part of the cost: binding is reflective, and reference resolution and validation both walk
    /// everything again.</para>
    ///
    /// <para><b>What is not measured, and why.</b> The row asked for ten per cent of the set to be
    /// patched. Patch operations do not exist yet — <see cref="DefLoader.Load"/> carries a comment
    /// where pass P2 will go and nothing behind it — so a "patched" fixture would measure an empty
    /// pass and report a number that quietly becomes wrong the day patching lands. The figure
    /// below is therefore parse, inherit, bind, index, resolve and validate, and patching is the
    /// one cost still outstanding.</para>
    /// </summary>
    public class DefLoaderBenchmarkTests
    {
        public sealed class BenchAlphaDef : Def
        {
            public int work;
            public float factor = 1f;
            public bool flag;
        }

        public sealed class BenchBetaDef : Def
        {
            public int work;
            [DefReference(typeof(BenchAlphaDef), Optional = true)] public string alpha = string.Empty;
        }

        public sealed class BenchGammaDef : Def
        {
            public int work;
            public float factor = 1f;
        }

        public sealed class BenchDeltaDef : Def
        {
            public int work;
            [DefReference(typeof(BenchGammaDef), Optional = true)] public string gamma = string.Empty;
        }

        public sealed class BenchEpsilonDef : Def
        {
            public int work;
            public bool flag;
        }

        const int TotalDefs = 3_000;
        const int Types = 5;
        const int Depth = 3;

        /// <summary>
        /// Ten times a figure we would be happy with, in the same spirit as the M1 generation
        /// budget: a number that fails on a regression of the kind that matters rather than on a
        /// slow machine having a bad minute.
        /// </summary>
        const int BudgetMs = 2_000;

        [Test, Category("Long")]
        public void ThreeThousandDefsLoadInsideTheBudget()
        {
            var samples = new long[5];
            int loaded = 0;

            for (int run = 0; run < samples.Length; run++)
            {
                DefLoader loader = BuildLoader();

                var watch = Stopwatch.StartNew();
                DefDatabase db = loader.Load();
                watch.Stop();
                samples[run] = watch.ElapsedMilliseconds;

                loaded = db.Table<BenchAlphaDef>().Count + db.Table<BenchBetaDef>().Count +
                         db.Table<BenchGammaDef>().Count + db.Table<BenchDeltaDef>().Count +
                         db.Table<BenchEpsilonDef>().Count;
            }

            Array.Sort(samples);
            long median = samples[samples.Length / 2];

            TestContext.WriteLine(
                $"{loaded:N0} concrete Defs across {Types} types, inheritance {Depth} deep: " +
                $"median {median} ms of [{string.Join(", ", samples)}] ms " +
                $"({(loaded > 0 ? median * 1000.0 / loaded : 0):F1} us per Def). " +
                "Parse, inherit, bind, index, resolve and validate; no patch pass exists yet.");

            Assert.That(loaded, Is.GreaterThan(0), "the fixture produced no concrete Defs at all");
            Assert.That(median, Is.LessThan(BudgetMs),
                $"Def loading took a median of {median} ms for {loaded} Defs");
        }

        [Test]
        public void TheFixtureIsTheShapeTheBenchmarkClaims()
        {
            // A benchmark whose fixture is not what it says measures something else. Cheap to
            // check, and it runs in the default tier so a fixture that quietly stops inheriting
            // is caught even when nobody runs the slow tier.
            DefDatabase db = BuildLoader().Load();

            int concrete = db.Table<BenchAlphaDef>().Count + db.Table<BenchBetaDef>().Count +
                           db.Table<BenchGammaDef>().Count + db.Table<BenchDeltaDef>().Count +
                           db.Table<BenchEpsilonDef>().Count;
            Assert.That(concrete, Is.EqualTo(TotalDefs));

            // Depth three means a leaf inherits through two abstract ancestors. The leaf sets
            // neither field below, so both values can only have arrived by inheritance.
            var leaf = db.Table<BenchAlphaDef>().Get("BenchAlpha_0");
            Assert.That(leaf.work, Is.EqualTo(70), "the value from the root of the chain is missing");
            Assert.That(leaf.factor, Is.EqualTo(2.5f), "the value from the middle of the chain is missing");

            // And the cross-references resolved, which is the pass that walks everything twice.
            var beta = db.Table<BenchBetaDef>().Get("BenchBeta_0");
            Assert.That(beta.alpha, Is.Not.Empty);
        }

        // ------------------------------------------------------------------ the fixture

        static DefLoader BuildLoader()
        {
            var loader = new DefLoader()
                .Register<BenchAlphaDef>()
                .Register<BenchBetaDef>()
                .Register<BenchGammaDef>()
                .Register<BenchDeltaDef>()
                .Register<BenchEpsilonDef>();

            var source = new InMemoryDefSource("Bench");
            string[] types = { "BenchAlphaDef", "BenchBetaDef", "BenchGammaDef", "BenchDeltaDef", "BenchEpsilonDef" };
            int perType = TotalDefs / Types;

            for (int t = 0; t < types.Length; t++)
                source.Add($"bench_{t}.xml", Document(types[t], t, perType));

            return loader.AddSource(source);
        }

        /// <summary>
        /// One file per type: two abstract ancestors, then <paramref name="count"/> concrete
        /// leaves inheriting through them. Split across files rather than one enormous document,
        /// because that is the shape content actually ships in and file count is part of the cost.
        /// </summary>
        static string Document(string typeName, int typeIndex, int count)
        {
            string prefix = typeName.Substring(0, typeName.Length - "Def".Length);
            var xml = new StringBuilder(count * 200);
            xml.Append("<Defs>");

            // Depth 1: the root of the chain, carrying the field a leaf never sets.
            xml.Append($"<{typeName} Abstract=\"True\"><defName>{prefix}_Root</defName>")
               .Append("<work>70</work></").Append(typeName).Append('>');

            // Depth 2: adds the second inherited value.
            xml.Append($"<{typeName} Abstract=\"True\" ParentName=\"{prefix}_Root\">")
               .Append($"<defName>{prefix}_Middle</defName>");
            if (typeName == "BenchAlphaDef" || typeName == "BenchGammaDef") xml.Append("<factor>2.5</factor>");
            xml.Append("</").Append(typeName).Append('>');

            // Depth 3: the leaves, which set only what makes them distinguishable.
            for (int i = 0; i < count; i++)
            {
                xml.Append($"<{typeName} ParentName=\"{prefix}_Middle\">")
                   .Append($"<defName>{prefix}_{i}</defName>")
                   .Append($"<label>{prefix.ToLowerInvariant()} {i}</label>");

                // A cross-reference on two of the five types, so reference resolution has real
                // work rather than an empty pass.
                if (typeName == "BenchBetaDef") xml.Append($"<alpha>BenchAlpha_{i}</alpha>");
                if (typeName == "BenchDeltaDef") xml.Append($"<gamma>BenchGamma_{i}</gamma>");
                if (typeName == "BenchEpsilonDef") xml.Append("<flag>true</flag>");

                xml.Append("</").Append(typeName).Append('>');
            }

            xml.Append("</Defs>");
            return xml.ToString();
        }
    }
}
