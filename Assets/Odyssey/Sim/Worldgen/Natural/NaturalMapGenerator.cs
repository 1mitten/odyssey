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
    /// The ten passes:
    ///   1. heightfield — gentle terracing from integer value noise;
    ///   2. water plan — where the ponds, streams and rivers go, and the columns they lower;
    ///   3. strata — bedrock, rock, subsoil, soil surface, air;
    ///   4. water fill — the beds and the water itself, in the channels pass 2 cut;
    ///   5. surface cover — grass, with patches of bare earth, gravel and sand;
    ///   6. rock outcrops — above-ground stone worth mining;
    ///   7. trees — clumped on grass, harvestable, non-blocking;
    ///   8. caverns — sealed voids in the rock, with no way in but a pick;
    ///   9. ore — depth-weighted lumps inside the rock, hung on cavern walls where there are any;
    ///  10. start — a flat, clear, dry landing site that can reach the map, plus the checks.
    ///
    /// Each is a separately constructible <see cref="INaturalGenPass"/>, so a test can run the
    /// first three and assert on the strata rather than on the finished map.
    ///
    /// Water is two passes rather than one because it is two decisions. Where the water goes is a
    /// *column* decision and has to be made before the strata are laid, so that the one full-grid
    /// loop builds a correct column under every bed by construction. What a cell is made of is a
    /// *cell* decision and can only be made after. They therefore sit either side of pass 3.
    /// </summary>
    public static class NaturalMapGenerator
    {
        public const int PassCount = 10;

        /// <summary>The passes in order. A new one is inserted here and nowhere else.</summary>
        public static INaturalGenPass[] CreatePasses() =>
            new INaturalGenPass[]
            {
                new HeightfieldPass(),
                // Before the strata, because it lowers the columns the strata are laid down.
                new WaterPlanPass(),
                new NaturalStrataPass(),
                // And after them, because the water itself is a cell the strata would overwrite.
                new WaterFillPass(),
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
