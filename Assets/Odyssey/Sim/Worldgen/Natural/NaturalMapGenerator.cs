#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Worldgen.Natural
{
    /// <summary>
    /// The wilderness generator: an empty, natural map of grass, trees, stone and ore, which the
    /// player builds on from nothing.
    ///
    /// Shaped exactly like <see cref="WorldGenerator"/>, and for the same reasons. Determinism is
    /// the whole contract: the same seed and the same <see cref="NaturalMapGenDef"/> produce a
    /// byte-identical grid on any machine and in any process, because every draw comes from
    /// <see cref="DeterministicRandom"/>, every field is integer, every loop runs in index order
    /// and nothing iterates a dictionary. <see cref="NaturalMapResult.GridHash"/> is the single
    /// number a test compares.
    ///
    /// The eight passes:
    ///   1. heightfield — gentle terracing from integer value noise;
    ///   2. strata — bedrock, rock, subsoil, soil surface, air;
    ///   3. surface cover — grass, with patches of bare earth, gravel and sand;
    ///   4. rock outcrops — above-ground stone worth mining;
    ///   5. trees — clumped on grass, harvestable, non-blocking;
    ///   6. caverns — sealed voids in the rock, with no way in but a pick;
    ///   7. ore — depth-weighted lumps inside the rock, hung on cavern walls where there are any;
    ///   8. start — a flat, clear landing site, plus the consistency check.
    ///
    /// Each is a separately constructible <see cref="INaturalGenPass"/>, so a test can run the
    /// first two and assert on the strata rather than on the finished map.
    /// </summary>
    public static class NaturalMapGenerator
    {
        public const int PassCount = 8;

        /// <summary>The passes in order. A new one is inserted here and nowhere else.</summary>
        public static INaturalGenPass[] CreatePasses() =>
            new INaturalGenPass[]
            {
                new HeightfieldPass(),
                new NaturalStrataPass(),
                new SurfaceCoverPass(),
                // Outcrops before trees, deliberately: a mound turns the ground it stands on to
                // rock, and a tree only grows on grass, so ordering them this way means no tree is
                // ever buried under a rock that arrived after it.
                new RockOutcropPass(),
                new TreePass(),
                // Caverns before ore, so a deposit can be hung on a chamber wall. Both only ever
                // touch rock, so neither can disturb anything the surface passes decided.
                new CavernPass(),
                new OrePass(),
                new NaturalStartPass(),
            };

        /// <summary>Generate a full wilderness map with parameters scaled to the grid.</summary>
        public static NaturalMapResult Generate(CellGrid grid, uint seed) =>
            Generate(grid, seed, NaturalMapGenDef.For(grid?.Size ?? throw new ArgumentNullException(nameof(grid))));

        public static NaturalMapResult Generate(CellGrid grid, uint seed, NaturalMapGenDef gen) =>
            Generate(grid, seed, gen, PassCount);

        /// <summary>
        /// Generate, optionally stopping after a given pass. Stopping early is what makes the
        /// individual passes testable: run pass 2 and assert the strata are contiguous, with no
        /// tree or ore lump in sight.
        /// </summary>
        public static NaturalMapResult Generate(CellGrid grid, uint seed, NaturalMapGenDef gen, int throughPass)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (gen == null) throw new ArgumentNullException(nameof(gen));
            if (throughPass < 0 || throughPass > PassCount)
                throw new ArgumentOutOfRangeException(nameof(throughPass));

            var context = new NaturalGenContext(grid, seed, gen);
            var passes = CreatePasses();

            for (int i = 0; i < passes.Length; i++)
            {
                if (passes[i].Order > throughPass) break;
                passes[i].Run(context);
                context.Report.PassesRun++;
            }

            return new NaturalMapResult(context);
        }
    }

    /// <summary>
    /// Everything a generated wilderness map knows about itself beyond the cell grid: where the
    /// features landed, which presentation modules it needs, and one hash to compare runs by.
    /// </summary>
    public sealed class NaturalMapResult
    {
        internal NaturalMapResult(NaturalGenContext context)
        {
            Context = context;
            Report = context.Report;
            GridHash = ComputeGridHash(context.Grid);
        }

        /// <summary>The generation context, kept for tests and for the map-inspection tooling.</summary>
        public NaturalGenContext Context { get; }

        public NaturalGenReport Report { get; }

        /// <summary>
        /// FNV-1a over the authoritative cell fields. Two runs of the same seed must produce the
        /// same value; two different seeds all but certainly must not.
        /// </summary>
        public ulong GridHash { get; }

        public CellRef StartCell => Report.StartCell;

        /// <summary>The standing trees. A felled one keeps its edifice handle but leaves this list.</summary>
        public IReadOnlyList<TreePlacement> Trees => Context.Trees;

        public IReadOnlyList<RockOutcrop> Outcrops => Context.Outcrops;
        public IReadOnlyList<CavernChamber> Caverns => Context.Caverns;
        public IReadOnlyList<OreDeposit> OreDeposits => Context.OreDeposits;

        /// <summary>
        /// Every presentation module id this map can ask for, to be added to the catalogue. Ids
        /// only: nothing here resolves a module, so a clone without the licensed art generates and
        /// simulates the same map.
        /// </summary>
        public IReadOnlyList<string> ModuleIds => NaturalContent.ModuleIds;

        static ulong ComputeGridHash(CellGrid grid)
        {
            var hash = StateHash.New();
            grid.ContributeTo(ref hash);
            return hash.Value;
        }

        public override string ToString() => $"{Report} hash {GridHash:x16}";
    }
}
