#nullable enable

namespace Odyssey.Presentation.Rendering
{
    /// <summary>Which kind of tree a theme is for. A pine never wears an oak's colours.</summary>
    public enum TreeSpecies
    {
        Conifer,
        Broadleaf,
    }

    /// <summary>
    /// One tree's colours: two for the bark and two for the canopy.
    ///
    /// <para>Four rather than one because a tree is drawn from <b>one</b> Synty mesh with
    /// <b>one</b> material, and the pack paints its trunk and its leaves from different flat cells
    /// of the same 4096 x 4096 atlas. A single <c>_BaseColor</c> multiply — the lever every other
    /// module in the game is tinted with — cannot tell the two apart, so tinting a tree brown
    /// browns its leaves as well. The four colours are the four cells, repainted separately by
    /// <c>Odyssey/Tree</c>.</para>
    ///
    /// <para>Written as sRGB hex because that is what somebody types into a colour picker, and
    /// handed to the shader unconverted for the reason <c>ColonistMaterials.Colour</c> records:
    /// <c>Material.SetColor</c> converts a <c>Color</c> property into the active colour space, and
    /// converting here as well would apply it twice.</para>
    /// </summary>
    public readonly struct TreeTheme
    {
        public TreeTheme(string name, TreeSpecies species,
            uint deepBark, uint warmTrunk, uint deepCanopy, uint freshLeaf)
        {
            Name = name;
            Species = species;
            DeepBark = Rgb24.FromHex(deepBark);
            WarmTrunk = Rgb24.FromHex(warmTrunk);
            DeepCanopy = Rgb24.FromHex(deepCanopy);
            FreshLeaf = Rgb24.FromHex(freshLeaf);
        }

        /// <summary>What the owner called it. Not player-facing — no tree is named in the game.</summary>
        public string Name { get; }

        public TreeSpecies Species { get; }

        /// <summary>The shaded side of the trunk, and the darker of the two bark cells.</summary>
        public Rgb24 DeepBark { get; }

        /// <summary>The lit side of the trunk, its branches, and anything the pack painted lighter.</summary>
        public Rgb24 WarmTrunk { get; }

        /// <summary>The mass of the canopy: the colour the tree reads as from across the board.</summary>
        public Rgb24 DeepCanopy { get; }

        /// <summary>The new growth on top of it, and what keeps a wood from reading as one flat shape.</summary>
        public Rgb24 FreshLeaf { get; }
    }

    /// <summary>
    /// The colours a tree can be dealt.
    ///
    /// <para><b>Why there is more than one, and why it is not one per tree.</b> Every tree on the
    /// board used to draw in the pack's own single green over the pack's own single brown, because
    /// a natural edifice is deliberately given no stuff tint at all (see
    /// <c>ChunkMesher.EmitEdifice</c>: a tree is not *built* of wood, so browning the wood stuff
    /// would brown every tree). Two meshes, two colours, a whole wood. The owner's complaint was
    /// that the world reads dull, and this is the largest single reason it does.</para>
    ///
    /// <para>The obvious fix — roll a colour per tree — is the expensive one. Drawing is bucketed
    /// per <i>(module, part, tint)</i> within a chunk, so a colour rolled per tree multiplies the
    /// tree buckets in every chunk by the size of this table, on the second most numerous thing on
    /// the board. <see cref="TreeLook"/> is the other half of this feature and the reason the
    /// table can afford to be this long: a colour is dealt to a <b>stand</b> of trees rather than
    /// to a tree, so a chunk holds one or two of these however many rows are written here.</para>
    ///
    /// <para><b>The first six rows are the owner's, verbatim</b> (2026-09-18), down to the hex. The
    /// rest are ours, added under the same instruction — <i>"mix in different shades brown and
    /// variation into this list as the world feels dull and we need to brighten up the trees"</i> —
    /// and they are the ones to veto first if the board comes out gaudy. They lean deliberately
    /// brighter and warmer than the owner's six, which are a muted, earthy set: a copper beech and
    /// a golden aspen put brown *in the canopy* rather than only in the trunk, which is the thing
    /// a wood of pure greens cannot do.</para>
    /// </summary>
    public static class TreePalette
    {
        static readonly TreeTheme[] Themes =
        {
            // ---- the owner's six, 2026-09-18 ------------------------------------------------
            new TreeTheme("Ancient Oak", TreeSpecies.Broadleaf,
                0x3E2723, 0x5D4037, 0x2E5A27, 0x689F38),
            new TreeTheme("Silver Birch", TreeSpecies.Broadleaf,
                0x4E443A, 0x8D7B68, 0x4B5320, 0x8F9779),
            new TreeTheme("Scots Pine", TreeSpecies.Conifer,
                0x4A2511, 0x8C533C, 0x1B4D3E, 0x355E3B),
            new TreeTheme("Weeping Willow", TreeSpecies.Broadleaf,
                0x5A3D28, 0x8B5A2B, 0x4F7942, 0x87A96B),
            new TreeTheme("Redwood", TreeSpecies.Conifer,
                0x3B1E19, 0x7D2E22, 0x1E3F20, 0x4A7C59),
            new TreeTheme("Mossy Birch", TreeSpecies.Broadleaf,
                0x3D312A, 0x796B58, 0x556B2F, 0x9CAF88),

            // ---- ours, under the same instruction -------------------------------------------
            // Brighter than anything above: the canopy is the yellow-green the reference art uses
            // for grass, which is the colour the board is lifted towards everywhere else
            // (StuffPalette.TerrainTints multiplies the meadow's blue by 1.55 to reach it) and
            // which no tree on the board has ever carried.
            new TreeTheme("Golden Aspen", TreeSpecies.Broadleaf,
                0x4A3A2A, 0xA08A63, 0x5E7A2B, 0xB5C44E),
            // Brown in the canopy, not only in the trunk. A wood of nothing but greens is what
            // "dull" actually describes, and one tree in a stand of six that is the colour of its
            // own bark does more for the picture than a seventh green would.
            new TreeTheme("Copper Beech", TreeSpecies.Broadleaf,
                0x3A251E, 0x6B4531, 0x5A3A2E, 0xA5613C),
            new TreeTheme("Hazel Thicket", TreeSpecies.Broadleaf,
                0x4B3826, 0x93744B, 0x3F6B2A, 0x7FBF4A),
            // Conifers were two of the owner's six against four broadleaves, and the board
            // rolls roughly as many of one as the other, so a pine wood had half the variety of a
            // broadleaf one. These two even it up.
            new TreeTheme("Blue Cedar", TreeSpecies.Conifer,
                0x45291A, 0x8A6244, 0x24423F, 0x4E7E6E),
            new TreeTheme("Larch", TreeSpecies.Conifer,
                0x52381F, 0x9A7040, 0x3A5E2A, 0x7FA34B),
        };

        /// <summary>How many themes there are. A tint code carries an index into this table.</summary>
        public static int Count => Themes.Length;

        public static TreeTheme At(int index) =>
            index >= 0 && index < Themes.Length ? Themes[index] : Themes[0];

        static readonly int[] ConiferThemes = IndicesOf(TreeSpecies.Conifer);
        static readonly int[] BroadleafThemes = IndicesOf(TreeSpecies.Broadleaf);

        /// <summary>The rows a tree of this species may be dealt, as indices into the table.</summary>
        public static int[] For(TreeSpecies species) =>
            species == TreeSpecies.Conifer ? ConiferThemes : BroadleafThemes;

        static int[] IndicesOf(TreeSpecies species)
        {
            int n = 0;
            for (int i = 0; i < Themes.Length; i++) if (Themes[i].Species == species) n++;
            var found = new int[n];
            n = 0;
            for (int i = 0; i < Themes.Length; i++) if (Themes[i].Species == species) found[n++] = i;
            return found;
        }
    }
}
