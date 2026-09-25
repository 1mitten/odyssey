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

        /// <summary>
        /// Never generated. The sentinel a save header reads back when the file did not record a
        /// map type — version 1, before <c>SaveRecipe</c> existed (U36).
        /// </summary>
        Unknown = 2,
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

        /// <summary>
        /// The meadow's animals (design 30 §1): hog sounders in the woodland, rats by the rock.
        /// The natural board's default whether or not <see cref="MakeWooded"/> is called, because
        /// the untouched def has trees and rock too; <see cref="MakeBarren"/> is what clears it.
        /// </summary>
        public static Pawns.Wildlife.WildlifeEntry[] MeadowWildlife() => new[]
        {
            new Pawns.Wildlife.WildlifeEntry("PawnKind_MiddenHog", 3, 3, 5, Pawns.Wildlife.Habitat.Woodland),
            new Pawns.Wildlife.WildlifeEntry("PawnKind_DuctRat", 2, 1, 1, Pawns.Wildlife.Habitat.Rock),
        };

        public NaturalMapGenDef()
        {
            wildlife = MeadowWildlife();
            wildlifePer10000Columns = 15;
        }

        /// <summary>
        /// A plain starting board: flat ground, grass everywhere, no trees, outcrops, ore or bare
        /// patches. The strata below are untouched, so digging still finds rock.
        ///
        /// This exists to be the baseline the prototype builds from. Judging a change to
        /// movement, rendering or the build pipeline against varied terrain means arguing about
        /// what is terrain and what is a bug; against a uniform board, anything that is not grass
        /// is a bug. <see cref="MakeBarren"/> applies it.
        /// </summary>
        public bool barren;

        /// <summary>Flatten the surface and switch off every scattered feature.</summary>
        public NaturalMapGenDef MakeBarren()
        {
            // No animals: a bare board with a hog on it is a hog to explain in every test that
            // counts pawns, and the baseline is the board on which nothing needs explaining.
            wildlife = System.Array.Empty<Pawns.Wildlife.WildlifeEntry>();
            wildlifePer10000Columns = 0;
            barren = true;
            surfaceRelief = 0;              // one flat surface layer, no terracing
            treeDensityPerMille = 0;
            bushPerMille = 0;
            outcropsPer10000Columns = 0;
            oreDepositsPer10000Columns = 0;
            cavernsPer10000Columns = 0;     // the strata stay solid: a hole in them is a bug here

            // The cover pass keeps grass when `cover >= barePatchThreshold`, so zero keeps grass
            // everywhere: noise is never negative, so the test always passes. Reaching for a huge
            // value instead does the exact opposite and strips the grass off the whole map, which
            // is the mistake this comment exists to stop the next person repeating. The generator
            // validates its own parameters and rejected it immediately, which is the system
            // working.
            barePatchThreshold = 0;

            // No water either. The bare board's whole value is that anything which is not grass
            // is a bug, and a pond is not grass. With this off the water pass returns before it
            // draws anything, so a barren map is bit-identical to one generated before water
            // existed — which is a test.
            water = false;
            return this;
        }

        /// <summary>
        /// The played board: grass in every cell, and everything the wilderness generator knows
        /// how to put on and under it — terraced ground, woodland, rock outcrops, ore and
        /// caverns. The start pass still clears a flat stand around the start location, so the
        /// colony begins on open ground with wood a short walk away and stone in sight.
        ///
        /// <para>This is a **cover** mode, not a feature switch. Its one departure from the
        /// generator's defaults is <see cref="barePatchThreshold"/>: the surface is grass
        /// everywhere rather than mottled with earth, gravel and sand, which is the look the
        /// owner chose on 2026-09-16 and the only part of the old wooded board that survives.
        /// Everything else is the def's own default, so tuning a default now reaches the board
        /// that is actually played instead of being zeroed on the way there.</para>
        ///
        /// <para>It used to be <see cref="MakeBarren"/> with the trees put back, which meant the
        /// board had no rock, no ore and a dead-flat surface. The mining MVP is exactly the
        /// decision to stop doing that (<c>docs/research/mining-interview.md</c>).
        /// <see cref="MakeBarren"/> itself is untouched and stays the test baseline on which
        /// anything that is not grass is a bug.</para>
        /// </summary>
        public NaturalMapGenDef MakeWooded()
        {
            barren = false;

            // The cover pass keeps grass where `cover >= barePatchThreshold`, and noise is never
            // negative, so zero keeps grass everywhere. Reaching for a huge value does the exact
            // opposite — see MakeBarren, where that mistake is recorded.
            barePatchThreshold = 0;

            // And its water, by owner decision on 2026-09-16: a stream to wade and a pond or two
            // to walk around, with the start clearing kept clear of both.
            //
            // This comment used to say the board was flat here because MakeBarren had zeroed the
            // relief, and that stopped being true the moment this became a cover mode rather than
            // a call to MakeBarren with the trees put back. The surface is terraced — surfaceRelief
            // keeps the def's own 2 — so the pond rule has real work to do: a pond is dropped
            // unless its whole footprint is one level terrace, which is what keeps a water surface
            // level and its banks a single step high.
            water = true;

            // And its animals (design 30 §1), which the bare board this may follow had cleared.
            wildlife = MeadowWildlife();
            wildlifePer10000Columns = 15;
            return this;
        }

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

        // ---- pass 5, trees -------------------------------------------------------------------

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

        /// <summary>Per mille chance a placed tree is a broadleaf - meadow, fruit or giant -
        /// rather than a birch (design 45 §3).</summary>
        public int broadleafChance = 420;

        // ---- pass 11, undergrowth ------------------------------------------------------------
        //
        // The Meadow dressing's bush rule at its shipped density, made real (design 45 §4):
        // bushes on the even-even lattice, where a 14-cell value-noise field stands above a
        // threshold, and at wood edges whatever the field says. Zero switches bushes off.

        /// <summary>Per mille chance at full want that a lattice cell grows a bush. The dressing's 0.4.</summary>
        public int bushPerMille = 400;

        /// <summary>Lattice period of the bush field, in cells.</summary>
        public int bushFieldPeriod = 14;

        /// <summary>Per mille of the field below which it wants no bush at all. The dressing's 0.45.</summary>
        public int bushFieldThreshold = 450;

        /// <summary>The want, per mille, at a cell beside a tree whatever the field says. The dressing's 0.7.</summary>
        public int bushWoodEdgeWant = 700;

        /// <summary>One bush in this many bears berries.</summary>
        public int berryBushOneIn = 6;

        /// <summary>No bush within this many cells of the start (the dressing's clearing radius).</summary>
        public int undergrowthClearRadius = 4;

        // ---- pass 4, rock outcrops -----------------------------------------------------------

        public int outcropsPer10000Columns = 16;
        public int minOutcropRadius = 1;
        public int maxOutcropRadius = 3;
        public int minOutcropHeight = 1;
        public int maxOutcropHeight = 3;

        // ---- pass 7, ore ---------------------------------------------------------------------

        public int oreDepositsPer10000Columns = 70;
        public int minOreBlob = 5;
        public int maxOreBlob = 20;

        // ---- pass 2, water -------------------------------------------------------------------

        /// <summary>
        /// The master switch. With it off the water plan pass returns before drawing anything
        /// from its random stream, so the map is byte-for-byte the one this generator made before
        /// water existed. That is what lets the barren board stay the baseline it is.
        /// </summary>
        public bool water = true;

        public int pondsPer10000Columns = 9;

        /// <summary>
        /// A pond is dropped unless the surface is one level terrace across its whole footprint.
        /// Raising this on relieved ground therefore produces *fewer* ponds, not bigger ones —
        /// the knob to reach for in that case is this one, never the level rule, which is what
        /// keeps the water surface level and the banks a single step high.
        /// </summary>
        public int maxPondRadius = 8;
        public int minPondRadius = 3;

        /// <summary>Cells the pond edge may wander in or out, so it is a pond and not a disc.</summary>
        public int pondEdgeJitter = 2;
        public int pondEdgePeriod = 7;

        /// <summary>Streams cut when the map has no river. Each is a separate path.</summary>
        public int streamCount = 2;

        /// <summary>Half-width bound: 1 gives streams of one to three cells, all of them wadeable.</summary>
        public int streamMaxHalfWidth = 1;

        /// <summary>How far a path wanders from its axis, in thousandths of the crossing size.</summary>
        public int streamMeanderAmplitudePerMille = 200;
        public int streamMeanderPeriod = 21;
        public int streamMeanderOctaves = 2;
        public int streamWidthPeriod = 9;

        /// <summary>Per mille chance the map gets a river instead of its streams. Rare, by intent.</summary>
        public int riverChancePerMille = 120;

        /// <summary>Half-widths of 2 to 5 give a river of five to eleven cells.</summary>
        public int riverMinHalfWidth = 2;
        public int riverMaxHalfWidth = 5;

        /// <summary>A big river meanders less across a map than a brook does, so this is lower.</summary>
        public int riverMeanderAmplitudePerMille = 120;

        /// <summary>Wadeable crossings cut across every river, so no bank is ever unreachable.</summary>
        public int riverFords = 2;

        /// <summary>Steps either side of a ford's centre, so 1 is a three-cell crossing.</summary>
        public int fordHalfLength = 1;

        /// <summary>
        /// Rings in from the shore that stay wadeable. Two is the value the whole depth model
        /// rests on: it makes a stream of up to three cells shallow end to end, and gives a
        /// five-wide river a single deep cell down the middle. **One is the obvious wrong
        /// default** — it puts a deep channel in the middle of a three-cell brook.
        /// </summary>
        public int deepShoreDistance = 2;

        /// <summary>
        /// Rings of wet ground around water. Marsh columns are never lowered.
        ///
        /// One, not two. Two rings around every water cell put more marsh on the board than
        /// water — measured, 427 columns against 382 — and a dark ribbon two cells wide either
        /// side of a stream one cell wide is not a fringe, it is the feature. A fringe should be
        /// the thing you notice second.
        /// </summary>
        public int marshFringe = 1;

        /// <summary>
        /// Cover noise, 0..1023, above which a fringe column is marsh, with
        /// <see cref="marshFalloff"/> added per ring beyond the first.
        ///
        /// Above zero on purpose: at zero the first ring is solid, and a solid ring of anything
        /// traces the water like a drawn outline. Breaking it up with the field the cover pass
        /// already uses — no new noise — leaves bog where the ground was going to be poor anyway
        /// and grass where it was not, which is both cheaper and more like a real margin. Set
        /// both to zero for a plain ring.
        /// </summary>
        public int marshThreshold = 300;
        public int marshFalloff = 340;

        // ---- pass 8, caverns -------------------------------------------------------------------

        /// <summary>
        /// Sealed voids in the rock, per ten thousand columns. Three puts four of them on the
        /// played 120 x 120 board.
        ///
        /// A cavern has **no mouth**: it is found by mining into it, which is the whole point of
        /// it (<c>docs/research/mining-interview.md</c>, answer 7). It is carved strictly inside
        /// the rock band, so it never undermines the surface and never breaks into the bedrock.
        /// </summary>
        public int cavernsPer10000Columns = 3;

        public int minCavernCells = 6;
        public int maxCavernCells = 20;

        // ---- pass 10, start -------------------------------------------------------------------

        /// <summary>Half-width of the starting clearing, so 2 asks for a flat, clear 5 x 5.</summary>
        public int startClearingRadius = 2;

        /// <summary>
        /// Columns of dry margin the start clearing keeps from any water or bog. Marsh counts as
        /// ground, so the ground check alone would happily land the colony in a swamp.
        /// </summary>
        public int startWaterClearance = 2;

        /// <summary>
        /// The share of walkable columns the start must be able to reach. Below it the generator
        /// cuts another ford rather than re-rolling the map: re-rolling makes generation take
        /// unbounded time on an unlucky seed, and quietly uses a seed other than the one it was
        /// handed, which is a determinism smell even when it is technically deterministic. The
        /// slack below 100 is deliberate — an islanded corner behind a pond is not a severed map.
        /// </summary>
        public int minReachablePercent = 80;

        /// <summary>
        /// Fords the reachability check may force before it gives up and throws. A map still cut
        /// in two after this many is a bug in the shape code, not an unlucky seed.
        /// </summary>
        public int maxForcedFords = 3;

        /// <summary>
        /// Parameters scaled to a grid. Unlike a city map, most of a wilderness map is sky: the
        /// ground sits about two fifths of the way up, which leaves a deep enough column to mine
        /// and plenty of headroom to build in.

        /// Layers of open air kept above the **highest** terrace. Everything left over goes
        /// underground, which is the rule <see cref="For"/> applies.
        ///
        /// Three is what the mining MVP settled on: nine metres, three storeys, and more than the
        /// colony has ever built upward. It is deliberately a small number, because headroom is
        /// the only thing depth can be bought with on a board whose layer count is fixed.
        /// </summary>
        public int headroomLayers = 3;

        /// <summary>
        /// Parameters scaled to a grid.
        ///
        /// <para><b>Depth is what is left after headroom.</b> The ground sits as high as it can
        /// while still leaving <see cref="headroomLayers"/> of sky above the tallest terrace, and
        /// everything below it is the mine. A deeper board is therefore a deeper mine rather than
        /// more sky, which is the right trade for a game about digging: nothing is ever built in
        /// the twentieth layer of empty air, and coal is 7 cells down.</para>
        ///
        /// <para>It used to be two fifths of the way up, on the reasoning that most of a
        /// wilderness map is sky. On the 120 x 120 x 16 board that is played that put the ground
        /// at layer 6 and left <em>two</em> layers of rock between the subsoil and the bedrock —
        /// too thin for the coal band to exist at all, so coal simply never generated. The bug was
        /// invisible because a map with no coal in it looks exactly like a map where nobody has
        /// dug deep enough yet (<c>docs/research/mining-interview.md</c> section 3).</para>
        /// </summary>
        public static new NaturalMapGenDef For(GridSize size)
        {
            var gen = new NaturalMapGenDef { defName = "MapGenNatural_" + size };
            gen.groundLayer = gen.GroundLayerFor(size);
            return gen;
        }

        /// <summary>
        /// The highest ground layer that still leaves <see cref="headroomLayers"/> of air above a
        /// terrace at full relief, floored at 1 so that a map too shallow to honour it generates
        /// something rather than throwing. The heightfield pass clamps per column on top of this,
        /// so a shallow board compresses rather than overflows.
        /// </summary>
        public int GroundLayerFor(GridSize size) =>
            Math.Max(1, size.SizeY - 1 - headroomLayers - surfaceRelief);

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
            if (headroomLayers < 1) throw new ArgumentOutOfRangeException(nameof(headroomLayers));
            if (surfacePeriod < 2) throw new ArgumentOutOfRangeException(nameof(surfacePeriod));
            if (subsoilDepth < 0) throw new ArgumentOutOfRangeException(nameof(subsoilDepth));
            if (bedrockLayers < 0) throw new ArgumentOutOfRangeException(nameof(bedrockLayers));
            if (barePatchThreshold < 0 || barePatchThreshold > ValueNoise.Scale)
                throw new ArgumentOutOfRangeException(nameof(barePatchThreshold));
            if (treeDensityPerMille < 0 || treeDensityPerMille > 1000)
                throw new ArgumentOutOfRangeException(nameof(treeDensityPerMille));
            if (treeClumpPeriod < 1) throw new ArgumentOutOfRangeException(nameof(treeClumpPeriod));
            if (bushPerMille < 0 || bushPerMille > 1000) throw new ArgumentOutOfRangeException(nameof(bushPerMille));
            if (bushFieldPeriod < 1) throw new ArgumentOutOfRangeException(nameof(bushFieldPeriod));
            if (bushFieldThreshold < 0 || bushFieldThreshold >= 1000)
                throw new ArgumentOutOfRangeException(nameof(bushFieldThreshold));
            if (berryBushOneIn < 1) throw new ArgumentOutOfRangeException(nameof(berryBushOneIn));
            if (minOutcropRadius < 0 || maxOutcropRadius < minOutcropRadius)
                throw new ArgumentOutOfRangeException(nameof(minOutcropRadius));
            if (minOutcropHeight < 1 || maxOutcropHeight < minOutcropHeight)
                throw new ArgumentOutOfRangeException(nameof(minOutcropHeight));
            if (minOreBlob < 1 || maxOreBlob < minOreBlob)
                throw new ArgumentOutOfRangeException(nameof(minOreBlob));
            if (minCavernCells < 1 || maxCavernCells < minCavernCells)
                throw new ArgumentOutOfRangeException(nameof(minCavernCells));
            if (startClearingRadius < 0) throw new ArgumentOutOfRangeException(nameof(startClearingRadius));

            if (pondsPer10000Columns < 0) throw new ArgumentOutOfRangeException(nameof(pondsPer10000Columns));
            if (minPondRadius < 1 || maxPondRadius < minPondRadius)
                throw new ArgumentOutOfRangeException(nameof(minPondRadius));
            if (pondEdgeJitter < 0 || pondEdgeJitter > 8)
                throw new ArgumentOutOfRangeException(nameof(pondEdgeJitter));
            if (pondEdgePeriod < 1) throw new ArgumentOutOfRangeException(nameof(pondEdgePeriod));
            if (streamCount < 0) throw new ArgumentOutOfRangeException(nameof(streamCount));
            if (streamMaxHalfWidth < 0 || streamMaxHalfWidth > 2)
                throw new ArgumentOutOfRangeException(nameof(streamMaxHalfWidth));
            if (streamMeanderAmplitudePerMille < 0 || streamMeanderAmplitudePerMille > 500)
                throw new ArgumentOutOfRangeException(nameof(streamMeanderAmplitudePerMille));
            if (streamMeanderPeriod < 2) throw new ArgumentOutOfRangeException(nameof(streamMeanderPeriod));
            if (streamMeanderOctaves < 1 || streamMeanderOctaves > 4)
                throw new ArgumentOutOfRangeException(nameof(streamMeanderOctaves));
            if (streamWidthPeriod < 1) throw new ArgumentOutOfRangeException(nameof(streamWidthPeriod));
            if (riverChancePerMille < 0 || riverChancePerMille > 1000)
                throw new ArgumentOutOfRangeException(nameof(riverChancePerMille));
            if (riverMinHalfWidth < 1 || riverMaxHalfWidth < riverMinHalfWidth)
                throw new ArgumentOutOfRangeException(nameof(riverMinHalfWidth));
            if (riverMeanderAmplitudePerMille < 0 || riverMeanderAmplitudePerMille > 500)
                throw new ArgumentOutOfRangeException(nameof(riverMeanderAmplitudePerMille));
            if (riverFords < 0) throw new ArgumentOutOfRangeException(nameof(riverFords));
            if (fordHalfLength < 0) throw new ArgumentOutOfRangeException(nameof(fordHalfLength));

            // One would put a deep channel down the middle of a three-cell stream. The depth
            // model has exactly one bound and this is it, so the range is checked rather than
            // left to whoever next tunes the water.
            if (deepShoreDistance < 1 || deepShoreDistance > 8)
                throw new ArgumentOutOfRangeException(nameof(deepShoreDistance));

            if (marshFringe < 0 || marshFringe > 6) throw new ArgumentOutOfRangeException(nameof(marshFringe));
            if (marshThreshold < 0 || marshThreshold > ValueNoise.Scale)
                throw new ArgumentOutOfRangeException(nameof(marshThreshold));
            if (marshFalloff < 0) throw new ArgumentOutOfRangeException(nameof(marshFalloff));
            if (startWaterClearance < 0) throw new ArgumentOutOfRangeException(nameof(startWaterClearance));
            if (minReachablePercent < 0 || minReachablePercent > 100)
                throw new ArgumentOutOfRangeException(nameof(minReachablePercent));
            if (maxForcedFords < 0) throw new ArgumentOutOfRangeException(nameof(maxForcedFords));
        }
    }
}
