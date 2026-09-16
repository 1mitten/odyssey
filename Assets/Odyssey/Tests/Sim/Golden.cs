#nullable enable
namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The committed golden-master table: one entry per scenario, holding the seed, tick count
    /// and the state hash that scenario is expected to reach.
    ///
    /// Values are baked on CoreCLR by <c>scripts/test-fast.sh</c> and the same test then has to
    /// pass under Unity's Mono runtime (<c>scripts/unity.sh test editmode</c>). Two processes and
    /// two runtimes agreeing on one hash is the actual proof of determinism; either alone only
    /// proves a runtime agrees with itself.
    ///
    /// Regenerate: run the fast tier with the environment variable <c>ODYSSEY_REGOLDEN=1</c> set,
    /// which prints the replacement lines instead of asserting, and paste them back in here. A
    /// value changing when a row did not mean to touch simulation state is a bug in that row, not
    /// a reason to re-bake (<c>docs/plans/overnight-queue.md</c>, notes on OQ-05 and OQ-14).
    /// </summary>
    public static class Golden
    {
        public readonly struct Entry
        {
            public readonly string Scenario;
            public readonly uint Seed;
            public readonly int Ticks;
            public readonly ulong Hash;

            public Entry(string scenario, uint seed, int ticks, ulong hash)
            {
                Scenario = scenario;
                Seed = seed;
                Ticks = ticks;
                Hash = hash;
            }
        }

        /// <summary>Natural map, 60x60x16, five colonists, the default tier's own workload.</summary>
        public static readonly Entry NaturalSmall =
            new Entry("natural-60x60x16-5c", seed: 1001, ticks: 5_000, hash: 0xac9f2bd8e137872bUL);

        /// <summary>Natural map at the M1 scale target, 120x120x16, five colonists.</summary>
        public static readonly Entry NaturalLarge =
            new Entry("natural-120x120x16-5c", seed: 1002, ticks: 10_000, hash: 0xf4ab0dfacce011ccUL);

        /// <summary>The ruined-city slice map, 60x60x5, five colonists.</summary>
        public static readonly Entry RuinedCitySlice =
            new Entry("ruined-city-60x60x5-5c", seed: 1003, ticks: 10_000, hash: 0x0c1ee2c6525de791UL);
    }
}
