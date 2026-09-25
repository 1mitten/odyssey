#nullable enable
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Which piece of art a tree wears, and how it stands in its cell — decided by the
    /// simulation's species (design 45 §3) rather than by a hash of the cell.
    ///
    /// <para>The catalogue has two tree families, each a run of rows under one module id
    /// (<c>PlayScene</c>): the conifer family is the three birches, and the broadleaf family is
    /// <c>Meadow_02</c>, the three fruit trees, and <c>Meadow_01</c>, the fifteen-metre giant, in
    /// that order. A species owns some of its family's rows. Until the species were the
    /// simulation's, a hash picked the row and made one broadleaf in forty a giant; the tree pass
    /// now deals the same mixture as species (<c>TreePass.SpeciesOf</c>), so the woods look as they
    /// did and a tree drawn as a giant is one that fells like a giant.</para>
    ///
    /// <para>The chunk mesher and the topple both ask here, so a toppling tree is the one that was
    /// standing: the same row, the same bearing, the same size.</para>
    /// </summary>
    public static class TreeArt
    {
        /// <summary>The broadleaf family's rows, as <c>PlayScene</c> orders them.</summary>
        public const int MeadowRow = 0, FirstFruitRow = 1, LastFruitRow = 3, GiantRow = 4;

        /// <summary>
        /// The row of its family a tree of this species wears at this cell, given how many rows
        /// the family resolved to. A family short of rows — no packs, or an older catalogue —
        /// clamps to what it has, so a checkout with one tree still draws that tree.
        /// </summary>
        public static int VariantFor(ushort def, int x, int z, int variants)
        {
            if (variants <= 1) return 0;
            switch (def)
            {
                case NaturalContent.EdificeTreeMeadow:
                    return MeadowRow;
                case NaturalContent.EdificeTreeGiant:
                    return Mathf.Min(GiantRow, variants - 1);
                case NaturalContent.EdificeTreeFruit:
                {
                    int last = Mathf.Min(LastFruitRow, variants - 1);
                    if (last < FirstFruitRow) return 0;
                    return FirstFruitRow + Pick(x, z, last - FirstFruitRow + 1);
                }
                default:
                    // The birch: any of its family's rows.
                    return Pick(x, z, variants);
            }
        }

        /// <summary>Which way a tree faces, in degrees, and how big it is against its art.</summary>
        public static void Stance(int x, int z, out float yaw, out float size)
        {
            yaw = GroundScatter.Unit(x, z, 0x6A09u) * 360f;
            size = 0.9f + GroundScatter.Unit(x, z, 0x6A0Bu) * 0.25f;
        }

        static int Pick(int x, int z, int count) =>
            count <= 1 ? 0 : Mathf.Min((int)(GroundScatter.Unit(x, z, 0x3D41u) * count), count - 1);
    }
}
