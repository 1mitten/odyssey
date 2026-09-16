#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Worldgen
{
    /// <summary>
    /// Pass 1 — the street grid.
    ///
    /// Streets are laid **first**, as the skeleton everything else hangs from, because they are
    /// the map's circulation and the player's mental map (section 6, pass 1). Block sizes come
    /// from a small uniform distribution rather than noise: a city block is a decision someone
    /// made, not a natural feature, and the regularity is what makes the grid legible.
    ///
    /// The layout is separable — a set of street lines along x, a set along z — which has one
    /// property worth stating: every north-south street crosses every east-west street, so the
    /// street network is connected by construction as long as at least one line exists on each
    /// axis, and that is forced below rather than hoped for.
    /// </summary>
    public sealed class StreetGridPass : IWorldGenPass
    {
        public int Order => 1;
        public string Name => "StreetGrid";

        public void Run(WorldGenContext ctx)
        {
            var rng = ctx.Random(WorldGenPurpose.Streets);
            var gen = ctx.Gen;

            ctx.Report.StreetsX = LayOutLines(ref rng, ctx.IsStreetX, gen);
            ctx.Report.StreetsZ = LayOutLines(ref rng, ctx.IsStreetZ, gen);

            int streetColumns = 0;
            for (int z = 0; z < ctx.Size.SizeZ; z++)
            {
                bool streetZ = ctx.IsStreetZ[z];
                for (int x = 0; x < ctx.Size.SizeX; x++)
                {
                    bool street = streetZ || ctx.IsStreetX[x];
                    ctx.IsStreet[ctx.Column(x, z)] = street;
                    if (street) streetColumns++;
                }
            }
            ctx.Report.StreetColumns = streetColumns;

            CollectBlocks(ctx);
        }

        static int LayOutLines(ref DeterministicRandom rng, bool[] mask, MapGenDef gen)
        {
            int extent = mask.Length;
            int lines = 0;
            int pos = rng.NextInt(0, gen.minBlock);
            while (pos < extent)
            {
                int width = rng.NextInt(gen.minStreetWidth, gen.maxStreetWidth + 1);
                for (int i = 0; i < width && pos + i < extent; i++) mask[pos + i] = true;
                lines++;
                pos += width + rng.NextInt(gen.minBlock, gen.maxBlock + 1);
            }

            // A map with no street on one axis would have a disconnected street network, which no
            // later pass could repair. Cheaper to guarantee it here than to detect it later.
            if (lines == 0)
            {
                mask[extent / 2] = true;
                lines = 1;
            }
            return lines;
        }

        /// <summary>The runs of non-street cells between the lines, in a fixed z-then-x order.</summary>
        static void CollectBlocks(WorldGenContext ctx)
        {
            var spansX = Spans(ctx.IsStreetX);
            var spansZ = Spans(ctx.IsStreetZ);
            for (int iz = 0; iz < spansZ.Count; iz++)
            {
                var sz = spansZ[iz];
                for (int ix = 0; ix < spansX.Count; ix++)
                {
                    var sx = spansX[ix];
                    ctx.Blocks.Add(new Block(sx.start, sz.start, sx.end, sz.end));
                }
            }
            ctx.Report.Blocks = ctx.Blocks.Count;
        }

        static List<(int start, int end)> Spans(bool[] mask)
        {
            var spans = new List<(int, int)>();
            int start = -1;
            for (int i = 0; i < mask.Length; i++)
            {
                if (!mask[i])
                {
                    if (start < 0) start = i;
                }
                else if (start >= 0)
                {
                    if (i - start >= 2) spans.Add((start, i - 1));
                    start = -1;
                }
            }
            if (start >= 0 && mask.Length - start >= 2) spans.Add((start, mask.Length - 1));
            return spans;
        }
    }

    /// <summary>
    /// Pass 2 — plots.
    ///
    /// Blocks are split along their longer axis until every parcel is within the plot-size band,
    /// then each plot picks a template by weight from those that fit — both horizontally and
    /// **vertically**, because a template carries its own layer extent and a five-layer slice map
    /// has no room for a tower. A plot with no fitting template stays vacant, which is a perfectly
    /// good ruined-city outcome rather than a failure.
    /// </summary>
    public sealed class PlotPass : IWorldGenPass
    {
        public int Order => 2;
        public string Name => "Plots";

        public void Run(WorldGenContext ctx)
        {
            var rng = ctx.Random(WorldGenPurpose.Plots);
            var parcels = new List<Block>();

            for (int i = 0; i < ctx.Blocks.Count; i++)
            {
                parcels.Clear();
                Subdivide(ctx.Blocks[i], 0, ref rng, ctx.Gen, parcels);
                for (int p = 0; p < parcels.Count; p++)
                {
                    var parcel = parcels[p];
                    int template = ChooseTemplate(ctx, parcel, ref rng);
                    ctx.Plots.Add(new Plot(parcel.X0, parcel.Z0, parcel.X1, parcel.Z1, template));
                    if (template < 0) ctx.Report.VacantPlots++;
                }
            }
            ctx.Report.Plots = ctx.Plots.Count;
        }

        static void Subdivide(Block block, int depth, ref DeterministicRandom rng, MapGenDef gen, List<Block> outParcels)
        {
            int sizeX = block.SizeX, sizeZ = block.SizeZ;
            bool canSplitX = sizeX >= 2 * gen.minPlot;
            bool canSplitZ = sizeZ >= 2 * gen.minPlot;
            bool tooBig = sizeX > gen.maxPlot || sizeZ > gen.maxPlot;

            if (depth < gen.maxPlotSplitDepth && tooBig && (canSplitX || canSplitZ))
            {
                bool splitAlongX = canSplitX && (!canSplitZ || sizeX >= sizeZ);
                if (splitAlongX)
                {
                    int cut = rng.NextInt(gen.minPlot, sizeX - gen.minPlot + 1);
                    Subdivide(new Block(block.X0, block.Z0, block.X0 + cut - 1, block.Z1), depth + 1, ref rng, gen, outParcels);
                    Subdivide(new Block(block.X0 + cut, block.Z0, block.X1, block.Z1), depth + 1, ref rng, gen, outParcels);
                }
                else
                {
                    int cut = rng.NextInt(gen.minPlot, sizeZ - gen.minPlot + 1);
                    Subdivide(new Block(block.X0, block.Z0, block.X1, block.Z0 + cut - 1), depth + 1, ref rng, gen, outParcels);
                    Subdivide(new Block(block.X0, block.Z0 + cut, block.X1, block.Z1), depth + 1, ref rng, gen, outParcels);
                }
                return;
            }

            outParcels.Add(block);
        }

        static int ChooseTemplate(WorldGenContext ctx, Block parcel, ref DeterministicRandom rng)
        {
            int total = 0;
            for (int i = 0; i < ctx.Templates.Count; i++)
            {
                var template = ctx.Templates[i];
                if (template.Fits(parcel.SizeX, parcel.SizeZ, ctx.LayersBelow, ctx.LayersAbove))
                    total += template.Weight;
            }
            if (total <= 0) return -1;

            int roll = rng.NextInt(total);
            for (int i = 0; i < ctx.Templates.Count; i++)
            {
                var template = ctx.Templates[i];
                if (!template.Fits(parcel.SizeX, parcel.SizeZ, ctx.LayersBelow, ctx.LayersAbove)) continue;
                roll -= template.Weight;
                if (roll < 0) return i;
            }
            return -1;
        }
    }

    /// <summary>
    /// Pass 3 — stamp shells.
    ///
    /// The template is centred in its plot, so a shell always lands strictly inside the parcel it
    /// was chosen for and two shells can never overlap. Stamping writes the slab, the edifice and
    /// the terrain of every non-void template cell and claims it, which is what stops the strata
    /// and intactness passes writing over a building later.
    /// </summary>
    public sealed class StampPass : IWorldGenPass
    {
        public int Order => 3;
        public string Name => "StampShells";

        public void Run(WorldGenContext ctx)
        {
            for (int p = 0; p < ctx.Plots.Count; p++)
            {
                var plot = ctx.Plots[p];
                if (plot.IsVacant) continue;

                var template = ctx.Templates[plot.TemplateIndex];
                int x0 = plot.X0 + (plot.SizeX - template.SizeX) / 2;
                int z0 = plot.Z0 + (plot.SizeZ - template.SizeZ) / 2;

                Stamp(ctx, template, x0, z0);
                ctx.Shells.Add(new ShellPlacement(plot.TemplateIndex, x0, z0, p));
            }
            ctx.Report.ShellsStamped = ctx.Shells.Count;
        }

        static void Stamp(WorldGenContext ctx, ShellTemplate template, int x0, int z0)
        {
            ushort slabKind = template.Stuff == CoreContent.StuffSteel
                ? CoreContent.SlabDeck
                : CoreContent.SlabStructural;

            for (int layer = template.BottomLayer; layer <= template.TopLayer; layer++)
            {
                int y = ctx.GroundLayer + layer;
                for (int tz = 0; tz < template.SizeZ; tz++)
                for (int tx = 0; tx < template.SizeX; tx++)
                {
                    var kind = template.Cell(layer, tx, tz);
                    if (kind == ShellCellKind.Void) continue;

                    int index = ctx.Index(x0 + tx, z0 + tz, y);
                    ctx.Claim(index);
                    ctx.SetTerrain(index, CoreContent.TerrainAir);
                    ctx.SetSlab(index,
                        ShellTemplate.HasSlab(kind) ? slabKind : CoreContent.SlabNone,
                        template.Stuff);

                    ushort edifice = ShellTemplate.EdificeFor(kind);
                    if (edifice != CoreContent.EdificeNone)
                        ctx.PlaceEdifice(index, edifice, template.Stuff, ShellTemplate.Blocks(kind));

                    ctx.Report.StampedCells++;
                }
            }

            if (!template.Roof) return;

            int roofY = ctx.GroundLayer + template.TopLayer + 1;
            for (int tz = 0; tz < template.SizeZ; tz++)
            for (int tx = 0; tx < template.SizeX; tx++)
            {
                if (template.Cell(template.TopLayer, tx, tz) == ShellCellKind.Void) continue;
                int index = ctx.Index(x0 + tx, z0 + tz, roofY);
                ctx.Claim(index);
                ctx.SetTerrain(index, CoreContent.TerrainAir);
                ctx.SetSlab(index, CoreContent.SlabRoof, template.Stuff);
                ctx.Report.StampedCells++;
            }
        }
    }

    /// <summary>
    /// Pass 4 — damage.
    ///
    /// This is the pass that earns its keep: it is what makes every ruin unique from a handful of
    /// templates, and it is the cheapest variety in the whole generator (section 6, pass 4).
    ///
    /// Intensity is per district — a coarse noise field banded into the Def's range — scaled by
    /// the template's own tolerance and by height, because the upper storeys of a ruin are always
    /// the worst. A minority of shells topple: everything above a drawn layer simply goes.
    /// </summary>
    public sealed class DamagePass : IWorldGenPass
    {
        public int Order => 4;
        public string Name => "Damage";

        /// <summary>Coarse enough that a district is tens of cells across, not a few.</summary>
        const int DistrictPeriod = 48;
        const int DistrictOctaves = 2;

        public void Run(WorldGenContext ctx)
        {
            BuildDistrictField(ctx);

            for (int s = 0; s < ctx.Shells.Count; s++)
            {
                var shell = ctx.Shells[s];
                var rng = ctx.Random(WorldGenPurpose.Damage, s);
                DamageShell(ctx, ctx.Templates[shell.TemplateIndex], shell, ref rng);
            }
        }

        static void BuildDistrictField(WorldGenContext ctx)
        {
            uint seed = ctx.Seed ^ 0x51ED2701u;
            var gen = ctx.Gen;
            for (int z = 0; z < ctx.Size.SizeZ; z++)
            for (int x = 0; x < ctx.Size.SizeX; x++)
            {
                int noise = ValueNoise.Fractal2D(seed, x, z, DistrictPeriod, DistrictOctaves);
                ctx.DistrictIntensity[ctx.Column(x, z)] =
                    ValueNoise.Band(noise, gen.minDamageIntensity, gen.maxDamageIntensity);
            }
        }

        static void DamageShell(WorldGenContext ctx, ShellTemplate template, ShellPlacement shell,
                                ref DeterministicRandom rng)
        {
            var gen = ctx.Gen;
            int district = ctx.DistrictIntensity[ctx.Column(shell.X0, shell.Z0)];
            int baseChance = district * template.DamageTolerance / 1000;

            int toppleFrom = int.MaxValue;
            if (template.TopLayer > 0 && rng.NextInt(1000) < gen.toppleChance)
                toppleFrom = rng.NextInt(1, template.TopLayer + 1);

            for (int layer = template.BottomLayer; layer <= template.TopLayer; layer++)
            {
                int storey = layer > 0 ? layer : 0;
                int chance = baseChance + baseChance * storey * gen.damagePerStorey / 1000;
                if (chance > 900) chance = 900;
                int slabChance = chance * gen.slabHoleFraction / 1000;
                bool toppled = layer >= toppleFrom;
                int y = ctx.GroundLayer + layer;

                for (int tz = 0; tz < template.SizeZ; tz++)
                for (int tx = 0; tx < template.SizeX; tx++)
                {
                    var kind = template.Cell(layer, tx, tz);
                    if (kind == ShellCellKind.Void) continue;
                    int index = ctx.Index(shell.X0 + tx, shell.Z0 + tz, y);

                    if (toppled)
                    {
                        Demolish(ctx, index);
                        continue;
                    }

                    bool changed = false;
                    if (ShellTemplate.Damageable(kind) && rng.NextInt(1000) < chance)
                    {
                        ctx.RemoveEdifice(index);
                        ctx.Report.RemovedEdifices++;
                        changed = true;
                        if (rng.NextInt(1000) < gen.rubbleFromWallChance)
                        {
                            ctx.SetTerrain(index, CoreContent.TerrainRubble);
                            ctx.Report.RubbleCells++;
                        }
                    }

                    // A pillar's own floor patch, and the doorstep around it, are part of the
                    // column rather than panels to lose on their own — otherwise a slab hole could
                    // moat a pillar off from everything it was placed to hold up, even though the
                    // pillar's edifice never moved.
                    if (!IsPillarOrAdjacent(template, layer, tx, tz) &&
                        ctx.Grid.Floor[index] != CoreContent.SlabNone && rng.NextInt(1000) < slabChance)
                    {
                        ctx.SetSlab(index, CoreContent.SlabNone, CoreContent.StuffNone);
                        ctx.Report.SlabHoles++;
                        changed = true;
                    }

                    if (changed) ctx.Report.DamagedCells++;
                }
            }

            if (!template.Roof) return;

            int roofY = ctx.GroundLayer + template.TopLayer + 1;
            bool roofToppled = template.TopLayer + 1 >= toppleFrom;
            int roofChance = baseChance + baseChance * (template.TopLayer + 1) * gen.damagePerStorey / 1000;
            if (roofChance > 900) roofChance = 900;
            for (int tz = 0; tz < template.SizeZ; tz++)
            for (int tx = 0; tx < template.SizeX; tx++)
            {
                var roofKind = template.Cell(template.TopLayer, tx, tz);
                if (roofKind == ShellCellKind.Void) continue;
                int index = ctx.Index(shell.X0 + tx, shell.Z0 + tz, roofY);
                // Same exemption as the per-floor loop above: a full topple still takes the deck
                // over a pillar with it, but the per-cell roll never punches a hole in it or its
                // doorstep alone.
                bool roofExempt = IsPillarOrAdjacent(template, template.TopLayer, tx, tz);
                if (roofToppled || (!roofExempt && rng.NextInt(1000) < roofChance))
                {
                    if (ctx.Grid.Floor[index] == CoreContent.SlabNone) continue;
                    ctx.SetSlab(index, CoreContent.SlabNone, CoreContent.StuffNone);
                    ctx.Report.SlabHoles++;
                    ctx.Report.DamagedCells++;
                }
            }
        }

        /// <summary>
        /// Is this cell a pillar, or does it touch one? Four neighbours only — a pillar's own
        /// diagonal is not its doorstep — checked against the template's authored plan rather
        /// than the grid, so it costs nothing more than the plan lookups the caller already does.
        /// </summary>
        static bool IsPillarOrAdjacent(ShellTemplate template, int layer, int tx, int tz)
        {
            if (template.Cell(layer, tx, tz) == ShellCellKind.Pillar) return true;
            if (tx > 0 && template.Cell(layer, tx - 1, tz) == ShellCellKind.Pillar) return true;
            if (tx < template.SizeX - 1 && template.Cell(layer, tx + 1, tz) == ShellCellKind.Pillar) return true;
            if (tz > 0 && template.Cell(layer, tx, tz - 1) == ShellCellKind.Pillar) return true;
            if (tz < template.SizeZ - 1 && template.Cell(layer, tx, tz + 1) == ShellCellKind.Pillar) return true;
            return false;
        }

        /// <summary>Everything in the cell goes: the slab, the wall, the lot.</summary>
        static void Demolish(WorldGenContext ctx, int index)
        {
            bool changed = false;
            if (ctx.Grid.Edifice[index] >= 0)
            {
                ctx.RemoveEdifice(index);
                ctx.Report.RemovedEdifices++;
                changed = true;
            }
            if (ctx.Grid.Floor[index] != CoreContent.SlabNone)
            {
                ctx.SetSlab(index, CoreContent.SlabNone, CoreContent.StuffNone);
                ctx.Report.SlabHoles++;
                changed = true;
            }
            if (changed) ctx.Report.DamagedCells++;
        }
    }

    /// <summary>
    /// Pass 5 — the intactness grid.
    ///
    /// RimWorld's fertility grid doing a different job (section 6, pass 5): one noise field over
    /// the street layer, banded into intact pavement, cracked pavement, rubble and exposed soil.
    /// Streets are biased toward intact because a road surface is the last thing to break up, and
    /// the soil band is the only place anything can be grown — which is why this field is the one
    /// that later decides where a farm can go.
    ///
    /// Cells a shell claimed are skipped: a building's ground floor is its own slab, not street
    /// surface.
    /// </summary>
    public sealed class IntactnessPass : IWorldGenPass
    {
        public int Order => 5;
        public string Name => "Intactness";

        public void Run(WorldGenContext ctx)
        {
            var gen = ctx.Gen;
            uint seed = ctx.Seed ^ 0x1B3F77A9u;
            int y = ctx.GroundLayer;

            for (int z = 0; z < ctx.Size.SizeZ; z++)
            for (int x = 0; x < ctx.Size.SizeX; x++)
            {
                int column = ctx.Column(x, z);
                int noise = ValueNoise.Fractal2D(seed, x, z, gen.intactnessPeriod, gen.intactnessOctaves);
                if (ctx.IsStreet[column]) noise += gen.streetIntactnessBonus;
                if (noise > ValueNoise.Scale - 1) noise = ValueNoise.Scale - 1;
                ctx.Intactness[column] = noise;

                int index = ctx.Index(x, z, y);
                if (ctx.IsClaimed(index)) continue;

                ushort terrain =
                    noise >= gen.pavementThreshold ? CoreContent.TerrainPavement :
                    noise >= gen.crackedThreshold ? CoreContent.TerrainCrackedPavement :
                    noise >= gen.rubbleThreshold ? CoreContent.TerrainRubble :
                    CoreContent.TerrainSoil;

                ctx.SetTerrain(index, terrain);
                if (terrain == CoreContent.TerrainRubble) ctx.Report.RubbleCells++;
            }
        }
    }
}
