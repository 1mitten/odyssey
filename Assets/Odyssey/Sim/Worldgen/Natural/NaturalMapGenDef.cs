#nullable enable
using System;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Worldgen.Natural
{
    /// <summary>
    /// Which generator makes the map. The numeric values are content, so they are fixed: a save
    /// records the type it was generated with.
    /// </summary>
    public enum MapType
    {
        /// <summary>The ten-pass ruined-city generator. Unchanged, and still the default.</summary>
        RuinedCity = 0,

        /// <summary>Empty wilderness: grass, trees, stone and ore, and the player builds from nothing.</summary>
        Natural = 1,
    }

    /// <summary>
    /// The parameter object for a wilderness map: every <see cref="MapGenDef"/> field, plus a
    /// <see cref="mapType"/> and the natural passes' own tuning.
    ///
    /// **Why a subclass rather than a field on MapGenDef.** The map type belongs on
    /// <see cref="MapGenDef"/> — one def, one field, one switch — and that is where it should end
    /// up. <c>WorldGenDefs.cs</c> is owned by another line of work and is not edited here, so the
    /// field lives on this subclass instead and <see cref="MapGenerator.TypeOf"/> is the single
    /// place that reads it. The migration is mechanical: move <see cref="mapType"/> up to
    /// <see cref="MapGenDef"/> with a default of <see cref="MapType.RuinedCity"/>, change
    /// <c>TypeOf</c> to <c>gen.mapType</c>, and nothing else moves — this class stays as the place
    /// the natural-only parameters live, which is where they belong anyway.
    ///
    /// Everything tunable about a wilderness map is here and nothing is a constant buried in a
    /// pass, so the slice map and the scale-target map differ only by this record.
    /// </summary>
    public class NaturalMapGenDef : MapGenDef
    {
        /// <summary>Which generator <see cref="MapGenerator.Generate"/> dispatches to.</summary>
        public MapType mapType = MapType.Natural;

        // ---- pass 1, heightfield -------------------------------------------------------------

        /// <summary>
        /// Layers the surface may rise or fall from <see cref="MapGenDef.groundLayer"/>. The cell
        /// model is discrete and has no slopes, so this is terracing, not hills: keep it small.
        /// Two gives a five-step surface, which reads as rolling ground and still leaves every
        /// step walkable from the one beside it.
        /// </summary>
        public int surfaceRelief = 2;

        /// <summary>Lattice period of the height noise, in cells. Large: gentle, long undulations.</summary>
        public int surfacePeriod = 34;
        public int surfaceOctaves = 2;

        // ---- pass 2, strata ------------------------------------------------------------------

        /// <summary>Cells of subsoil between the surface cell and the rock beneath it.</summary>
        public int subsoilDepth = 2;

        /// <summary>Layers of bedrock at the very bottom of the map.</summary>
        public int bedrockLayers = 2;

        // ---- pass 3, surface cover -----------------------------------------------------------

        /// <summary>
        /// Cover noise, 0..1023, below which the surface is bare rather than grassed. Higher
        /// means more bare patches.
        /// </summary>
        public int barePatchThreshold = 330;
        public int coverPeriod = 13;
        public int coverOctaves = 2;

        /// <summary>Within a bare patch: above this the patch is sand, above the gravel one it is gravel.</summary>
        public int sandThreshold = 660;
        public int gravelThreshold = 400;
        public int patchPeriod = 9;

        // ---- pass 4, trees -------------------------------------------------------------------

        /// <summary>
        /// Nominal trees per thousand grass cells, before clumping. The clump field modulates it
        /// per cell, and because that field averages below its midpoint the realised density
        /// comes out lower than the nominal figure — it is a dial, not a count.
        /// </summary>
        public int treeDensityPerMille = 260;

        /// <summary>Lattice period of the clump field. Small: tight copses. Large: broad woods.</summary>
        public int treeClumpPeriod = 11;
        public int treeClumpOctaves = 2;

        /// <summary>
        /// Clump value, 0..1023 after the squaring bias, below which nothing grows at all. Without
        /// a floor a sparse area is still lightly wooded everywhere and the map has no true
        /// clearings; with one, the thin tail of the field becomes open ground.
        /// </summary>
        public int treeClumpFloor = 170;

        /// <summary>Per mille chance a placed tree is broadleaf rather than conifer.</summary>
        public int broadleafChance = 420;

        // ---- pass 5, rock outcrops -----------------------------------------------------------

        public int outcropsPer10000Columns = 16;
        public int minOutcropRadius = 1;
        public int maxOutcropRadius = 3;
        public int minOutcropHeight = 1;
        public int maxOutcropHeight = 3;

        // ---- pass 6, ore ---------------------------------------------------------------------

        public int oreDepositsPer10000Columns = 70;
        public int minOreBlob = 5;
        public int maxOreBlob = 20;

        // ---- pass 7, start -------------------------------------------------------------------

        /// <summary>Half-width of the starting clearing, so 2 asks for a flat, clear 5 x 5.</summary>
        public int startClearingRadius = 2;

        /// <summary>
        /// Parameters scaled to a grid. Unlike a city map, most of a wilderness map is sky: the
        /// ground sits about two fifths of the way up, which leaves a deep enough column to mine
        /// and plenty of headroom to build in.
        /// </summary>
        public static new NaturalMapGenDef For(GridSize size)
        {
            var gen = new NaturalMapGenDef { defName = "MapGenNatural_" + size };
            gen.groundLayer = Math.Max(1, Math.Min(14, size.SizeY * 2 / 5));
            return gen;
        }

        /// <summary>The slice-sized wilderness map, for tests and the look-check scene.</summary>
        public static new NaturalMapGenDef Slice() => For(new GridSize(60, 60, 16));

        /// <summary>
        /// Everything <see cref="MapGenDef.Validate"/> checks, plus the natural parameters. The
        /// vertical budget is deliberately *not* checked here: a map too shallow for the full
        /// stratum stack is compressed by the heightfield pass rather than rejected, so a 60 x 60
        /// x 5 slice still generates something playable.
        /// </summary>
        public void ValidateNatural(GridSize size)
        {
            Validate(size);
            if (size.SizeY < 3)
                throw new ArgumentOutOfRangeException(nameof(size), "A natural map needs at least three layers.");
            if (surfaceRelief < 0) throw new ArgumentOutOfRangeException(nameof(surfaceRelief));
            if (surfacePeriod < 2) throw new ArgumentOutOfRangeException(nameof(surfacePeriod));
            if (subsoilDepth < 0) throw new ArgumentOutOfRangeException(nameof(subsoilDepth));
            if (bedrockLayers < 0) throw new ArgumentOutOfRangeException(nameof(bedrockLayers));
            if (barePatchThreshold < 0 || barePatchThreshold > ValueNoise.Scale)
                throw new ArgumentOutOfRangeException(nameof(barePatchThreshold));
            if (treeDensityPerMille < 0 || treeDensityPerMille > 1000)
                throw new ArgumentOutOfRangeException(nameof(treeDensityPerMille));
            if (treeClumpPeriod < 1) throw new ArgumentOutOfRangeException(nameof(treeClumpPeriod));
            if (minOutcropRadius < 0 || maxOutcropRadius < minOutcropRadius)
                throw new ArgumentOutOfRangeException(nameof(minOutcropRadius));
            if (minOutcropHeight < 1 || maxOutcropHeight < minOutcropHeight)
                throw new ArgumentOutOfRangeException(nameof(minOutcropHeight));
            if (minOreBlob < 1 || maxOreBlob < minOreBlob)
                throw new ArgumentOutOfRangeException(nameof(minOreBlob));
            if (startClearingRadius < 0) throw new ArgumentOutOfRangeException(nameof(startClearingRadius));
        }
    }
}
