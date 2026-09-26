#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.Worldgen.Planet
{
    /// <summary>
    /// What a site's hills do to the board it builds (design 59 §5): the one owner of the table.
    ///
    /// <para><b>Relief is set; outcrops and caverns are scaled.</b> Relief is a shape, so each band
    /// names its own. Outcrops and caverns are densities the board's preset already chose — the bare
    /// board has none, the played board the def's own — so a band multiplies what is there rather
    /// than overwriting it. That is what makes <see cref="HillBand.Rolling"/> today's board by
    /// construction: relief 2 is the played board's, and ×1 of anything is itself.</para>
    /// </summary>
    public static class SiteBoard
    {
        /// <summary>Relief, outcrops (per mille of the def's own) and caverns (per mille) for one band.</summary>
        public readonly struct Shape
        {
            public Shape(int relief, int outcropsPerMille, int cavernsPerMille)
            {
                Relief = relief;
                OutcropsPerMille = outcropsPerMille;
                CavernsPerMille = cavernsPerMille;
            }

            public readonly int Relief;
            public readonly int OutcropsPerMille;
            public readonly int CavernsPerMille;
        }

        /// <summary>
        /// The table, flattest first. Rolling is the played board: relief 2, ×1, ×1. Sheer has a row
        /// so the table is total, but a sheer site is never settled.
        /// </summary>
        public static Shape For(HillBand hills) => hills switch
        {
            HillBand.Flat => new Shape(1, 500, 1000),
            HillBand.Rolling => new Shape(2, 1000, 1000),
            HillBand.Hilly => new Shape(3, 1500, 1333),
            HillBand.Mountainous => new Shape(4, 2500, 2000),
            _ => new Shape(4, 2500, 2000),
        };

        /// <summary>
        /// Shape a natural board for a site, after its preset (<c>MakeWooded</c> or <c>MakeBarren</c>)
        /// has been applied, and re-derive its ground layer for the size it will be built at.
        /// </summary>
        public static void Apply(NaturalMapGenDef gen, HillBand hills, GridSize size)
        {
            Shape shape = For(hills);
            gen.surfaceRelief = shape.Relief;
            gen.outcropsPer10000Columns = Scale(gen.outcropsPer10000Columns, shape.OutcropsPerMille);
            gen.cavernsPer10000Columns = Scale(gen.cavernsPer10000Columns, shape.CavernsPerMille);
            gen.groundLayer = gen.GroundLayerFor(size);
        }

        /// <summary>
        /// A density times a per-mille factor, <b>rounded</b>. Truncating made ×1.333 of the played
        /// board's 3 caverns 3, so a Hilly board had Rolling's caverns and §5's table was wrong for
        /// that row; ×1000 is still exactly the value, so Rolling stays today's board.
        /// </summary>
        static int Scale(int value, int perMille) => (value * perMille + 500) / 1000;
    }
}
