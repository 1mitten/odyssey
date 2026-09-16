#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Worldgen
{
    /// <summary>
    /// The ruined-city generator: the ten ordered passes of
    /// docs/design/02-world-and-layers.md section 6, behind one entry point.
    ///
    /// Determinism is the whole contract. The same seed, the same <see cref="MapGenDef"/> and the
    /// same template set produce a byte-identical grid on any machine and in any process: every
    /// draw comes from <see cref="DeterministicRandom"/>, every field is integer, every loop runs
    /// in index order and nothing iterates a dictionary. <see cref="WorldGenResult.GridHash"/> is
    /// the single number a test compares.
    ///
    /// Each pass is a separately constructible <see cref="IWorldGenPass"/>, so a test can run the
    /// first three and assert on the intermediate state rather than on the finished map.
    /// </summary>
    public static class WorldGenerator
    {
        public const int PassCount = 10;

        /// <summary>The passes in order. A new one is inserted here and nowhere else.</summary>
        public static IWorldGenPass[] CreatePasses(IStructuralConsistencyCheck? structuralCheck = null) =>
            new IWorldGenPass[]
            {
                new StreetGridPass(),
                new PlotPass(),
                new StampPass(),
                new DamagePass(),
                new IntactnessPass(),
                new StrataPass(),
                new SalvagePass(),
                new UtilityTapPass(),
                new SealedVaultPass(),
                new StartPass(structuralCheck),
            };

        /// <summary>Generate a full map with the slice template set.</summary>
        public static WorldGenResult Generate(CellGrid grid, uint seed, MapGenDef gen) =>
            Generate(grid, seed, gen, TemplateLibrary.Slice());

        public static WorldGenResult Generate(CellGrid grid, uint seed, MapGenDef gen, TemplateSet templates) =>
            Generate(grid, seed, gen, templates, PassCount, null);

        /// <summary>
        /// Generate, optionally stopping after a given pass.
        ///
        /// <paramref name="throughPass"/> is the design document's pass number, 1 to 10. Stopping
        /// early is what makes the individual passes testable: run pass 1 and assert the street
        /// network is connected, without a stamped shell or a stratum in sight.
        ///
        /// A null <paramref name="structuralCheck"/> means the default
        /// <see cref="World.SupportConsistencyCheck"/>, not "no check": every generated map is
        /// proved to stand on the ordinary support rule before it is returned.
        /// </summary>
        public static WorldGenResult Generate(
            CellGrid grid,
            uint seed,
            MapGenDef gen,
            TemplateSet templates,
            int throughPass,
            IStructuralConsistencyCheck? structuralCheck)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (throughPass < 0 || throughPass > PassCount)
                throw new ArgumentOutOfRangeException(nameof(throughPass));

            var context = new WorldGenContext(grid, seed, gen, templates);
            var passes = CreatePasses(structuralCheck);

            for (int i = 0; i < passes.Length; i++)
            {
                if (passes[i].Order > throughPass) break;
                passes[i].Run(context);
                context.Report.PassesRun++;
            }

            return new WorldGenResult(context);
        }
    }

    /// <summary>
    /// Everything a generated map knows about itself beyond the cell grid: the plan it was built
    /// from, the features it placed, and one hash to compare runs by.
    /// </summary>
    public sealed class WorldGenResult
    {
        internal WorldGenResult(WorldGenContext context)
        {
            Context = context;
            Report = context.Report;
            GridHash = ComputeGridHash(context.Grid);
        }

        /// <summary>The generation context, kept for tests and for the map-inspection tooling.</summary>
        public WorldGenContext Context { get; }

        public WorldGenReport Report { get; }

        /// <summary>
        /// FNV-1a over the authoritative cell fields. Two runs of the same seed must produce the
        /// same value; two different seeds all but certainly must not.
        /// </summary>
        public ulong GridHash { get; }

        public CellRef StartCell => Report.StartCell;
        public IReadOnlyList<Plot> Plots => Context.Plots;
        public IReadOnlyList<ShellPlacement> Shells => Context.Shells;
        public IReadOnlyList<SalvageDeposit> SalvageDeposits => Context.SalvageDeposits;
        public IReadOnlyList<UtilityTap> UtilityTaps => Context.UtilityTaps;
        public IReadOnlyList<SealedVault> SealedVaults => Context.SealedVaults;

        static ulong ComputeGridHash(CellGrid grid)
        {
            var hash = StateHash.New();
            grid.ContributeTo(ref hash);
            return hash.Value;
        }

        public override string ToString() => $"{Report} hash {GridHash:x16}";
    }
}
