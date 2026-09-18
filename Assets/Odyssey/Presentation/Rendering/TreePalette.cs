#nullable enable

namespace Odyssey.Presentation.Rendering
{
    /// <summary>Which kind of tree a tone is for. A pine never wears an oak's colours.</summary>
    public enum TreeSpecies
    {
        Conifer,
        Broadleaf,
    }

    /// <summary>
    /// One colour of a tree in its two faces: the shaded one and the lit one.
    ///
    /// <para><b>Lit and shaded, not mass and accent — and getting that wrong is what made a white
    /// tree.</b> The owner's table was authored as a deep colour plus a "fresh leaf / highlight",
    /// which reads as a small bright accent on a mass of the deep colour. The mesh is the other way
    /// round: <c>TreeSwatchProbe</c> measures the broadleaf's *upper* canopy cell at <b>49.9%</b> of
    /// its vertices and the lower at 24.3%, so whatever colour goes on top <i>is</i> the tree. A
    /// pale sage highlight over half a tree is a pale sage tree, which is exactly what the owner
    /// reported: <i>"a shorter tree that was white/pale leaves that looked odd"</i> — the shorter
    /// tree being the 6.15 m broadleaf against the 9.47 m pine.</para>
    ///
    /// <para>So a tone is a pair of faces of <b>one</b> colour, and how far apart they may be is
    /// taken from the art rather than invented: the pack's own two canopy greens, #586644 and
    /// #6A7B52, are a step of about <b>1.21</b> in luminance. <see cref="TreeToneRules"/> holds
    /// that band and the tests enforce it, so no future entry can make a white tree again.</para>
    /// </summary>
    public readonly struct TreeTone
    {
        public TreeTone(string name, uint shaded, uint lit)
        {
            Name = name;
            Shaded = Rgb24.FromHex(shaded);
            Lit = Rgb24.FromHex(lit);
        }

        /// <summary>What it is called here. Not player-facing — no tree is named in the game.</summary>
        public string Name { get; }

        /// <summary>The face turned away from the sun: the lower canopy, or the shaded trunk.</summary>
        public Rgb24 Shaded { get; }

        /// <summary>The face the sun is on, which on a broadleaf is half the tree.</summary>
        public Rgb24 Lit { get; }
    }

    /// <summary>
    /// The bands a tone's two faces must sit inside, every one of them measured off the pack's own
    /// art by <c>TreeSwatchProbe</c> rather than chosen.
    ///
    /// <para>The broadleaf's two canopy cells are #586644 (luminance 91.4) and #6A7B52 (110.9), a
    /// step of <b>1.21</b>; the pine's are #4E543D (79.5) and #5E654A (96.0), a step of 1.21 again.
    /// The brightness ceiling sits a little above those, because the lit face is the one that
    /// covers half the tree and a lit face brighter than the art's own is a tree that glows — which
    /// is the fault the owner reported.</para>
    ///
    /// <para><b>Bark keeps a band of its own, and that was found rather than decided</b> — see
    /// <see cref="MaxBarkStep"/>. Holding trunks to the canopy's band rejected six entries
    /// including three of the owner's, and the art turned out to side with the owner.</para>
    /// </summary>
    public static class TreeToneRules
    {
        /// <summary>Least the lit face may exceed the shaded one. Below this the tree reads flat.</summary>
        public const float MinStep = 1.10f;

        /// <summary>
        /// Most a <b>leaf</b> tone's two faces may differ. The pack's own canopy step is 1.21 on
        /// the broadleaf and 1.21 on the pine, so this is a little over it.
        /// </summary>
        public const float MaxLeafStep = 1.45f;

        /// <summary>
        /// Most a <b>bark</b> tone's may, and it is far wider — which was a discovery rather than a
        /// decision. Fitting bark to the leaf band failed six entries, including three of the
        /// owner's own (Scots Pine at 2.13, Redwood at 1.79, Ancient Oak at 1.58), and the art
        /// agrees with the owner rather than with the band: the pine's trunk cell is #554B40 at
        /// luminance 77.9 and the branch-stub cell it pairs with is #9B7E5A at 130.6, a step of
        /// <b>1.68</b>. A trunk really does have a lit face that much brighter than its shaded one;
        /// a canopy does not, because a canopy is a cloud of leaves and a trunk is a cylinder.
        /// </summary>
        public const float MaxBarkStep = 2.20f;

        /// <summary>
        /// The brightest a <b>leaf</b> tone's lit face may be — and it is a function of how
        /// saturated the colour is, not a flat number.
        ///
        /// <para><b>Why it had to become a curve.</b> A flat ceiling of 132 stopped the pale tree,
        /// and then stopped everything else too: the owner's next note was <i>"can we add some
        /// bright colours into the leaf — it seems a bit dull still"</i>, and the ceiling was what
        /// was holding the wood down. Re-reading the fault settles which way to move it. The two
        /// entries that caused it were not merely bright, they were bright <em>and nearly
        /// colourless</em> — the old Silver Birch highlight #8F9779 is luminance 145.2 at a chroma
        /// of 30, and the old Mossy Birch #9CAF88 is 164.9 at 39. What reads as "white" is a pale
        /// wash, and a pale wash is high luminance with no colour left in it.</para>
        ///
        /// <para>So the allowance rises with chroma. A saturated lime may be luminance 165 and a
        /// saturated gold 175, because they are unmistakably a colour; a sage at chroma 30 is still
        /// held to 127 and the two originals are still rejected, by 18 and 33 points. There is an
        /// absolute cap as well, because past it a leaf is a light source whatever its hue.</para>
        /// </summary>
        public static float MaxLeafLit(Rgb24 lit) =>
            System.Math.Min(LeafLitCap, LeafLitBase + LeafLitPerChroma * Chroma(lit));

        /// <summary>The allowance for a colour with no chroma at all: just under the pack's own.</summary>
        public const float LeafLitBase = 108f;

        /// <summary>How much brighter each point of chroma buys.</summary>
        public const float LeafLitPerChroma = 0.62f;

        /// <summary>The brightest any leaf may be, however saturated.</summary>
        public const float LeafLitCap = 195f;

        /// <summary>
        /// The luminance at which a leaf tone counts as <b>bright</b>, which is a little above the
        /// pack's own brightest canopy at 110.9.
        ///
        /// <para>It is a real threshold rather than a label: every stand is guaranteed one tone
        /// from above it. Adding bright rows to the table alone did not brighten the wood — with
        /// seven bright tones among twenty-one, a stand's handful of four drew one on average and
        /// often drew none, and the contact sheet came back warmer but no brighter. Reliability is
        /// the thing, not proportion.</para>
        /// </summary>
        public const float BrightLeaf = 130f;

        /// <summary>
        /// The same for <b>bark</b>, and it stays a flat number: a trunk is a cylinder of one
        /// colour where a canopy is a cloud, and a pale birch trunk is a real thing rather than a
        /// wash. Higher than the leaf base on purpose, and a quarter of the mesh rather than half.
        /// </summary>
        public const float MaxBarkLit = 150f;

        public static float Luminance(Rgb24 c) => 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;

        public static float Step(in TreeTone tone) =>
            Luminance(tone.Lit) / (Luminance(tone.Shaded) < 1f ? 1f : Luminance(tone.Shaded));

        /// <summary>How far from grey a colour is, 0 to 255. A leaf that is grey reads as dead.</summary>
        public static int Chroma(Rgb24 c)
        {
            int hi = c.R > c.G ? (c.R > c.B ? c.R : c.B) : (c.G > c.B ? c.G : c.B);
            int lo = c.R < c.G ? (c.R < c.B ? c.R : c.B) : (c.G < c.B ? c.G : c.B);
            return hi - lo;
        }
    }

    /// <summary>One tree's whole colour: a bark tone and a leaf tone.</summary>
    public readonly struct TreeTheme
    {
        public TreeTheme(TreeSpecies species, in TreeTone bark, in TreeTone leaf)
        {
            Species = species;
            Bark = bark;
            Leaf = leaf;
        }

        public TreeSpecies Species { get; }
        public TreeTone Bark { get; }
        public TreeTone Leaf { get; }

        public string Name => Bark.Name + " / " + Leaf.Name;
    }

    /// <summary>
    /// The colours a tree can be dealt: every bark against every leaf.
    ///
    /// <para><b>Why a cross product rather than a list of themes.</b> The first version of this
    /// file held eleven hand-written themes of four colours each, and the owner's answer to it was
    /// <i>"it all needs a much larger variation of bark and leaf colours, really vary it up as much
    /// as possible"</i>. Writing eighty themes by hand would be eighty chances to author a white
    /// tree; writing twenty-one leaf tones and eight bark tones gives <b>a hundred and sixty-eight</b>
    /// broadleaf combinations and seventy-two conifer ones from thirty-five lines anybody can read
    /// down and correct.</para>
    ///
    /// <para><b>And it costs nothing.</b> The length of this table has never been what a wood costs
    /// — see <see cref="TreeLook"/> — because a tree's colour is an instancing bucket key and what
    /// bounds the bucket count is how many colours stand <i>in one chunk</i>, not how many exist.
    /// The table was always free to grow. This is it growing.</para>
    ///
    /// <para><b>The owner's six are still in here</b>, as the tones they were built from: Ancient
    /// Oak's bark and canopy, Silver Birch's, Scots Pine's, Weeping Willow's, Redwood's and Mossy
    /// Birch's are each a row below, and the cross product contains their original pairings along
    /// with every other. Two of their canopies were **changed**, and that is the fault they
    /// reported: Silver Birch's highlight was #8F9779 and Mossy Birch's #9CAF88, which at luminance
    /// 145 and 165 over half a tree are the pale, washed-out canopies the complaint names. They are
    /// the same hues, taken down to where the art's own greens sit.</para>
    /// </summary>
    public static class TreePalette
    {
        // ------------------------------------------------------------------ broadleaf

        /// <summary>
        /// Broadleaf trunks. Only the shaded face is drawn on these — the broadleaf meshes paint
        /// their whole trunk from one atlas cell (<see cref="TreeSwatches"/>) — so the variety here
        /// is in the first column, and it runs from near-black to a pale ash.
        /// </summary>
        static readonly TreeTone[] BroadleafBarks =
        {
            new TreeTone("oak",       0x3E2723, 0x5D4037),   // the owner's Ancient Oak
            new TreeTone("birch",     0x4E443A, 0x6B5E4E),   // the owner's Silver Birch
            new TreeTone("willow",    0x5A3D28, 0x7A5537),   // the owner's Weeping Willow
            new TreeTone("taupe",     0x3D312A, 0x55463B),   // the owner's Mossy Birch
            new TreeTone("hazel",     0x4B3826, 0x6B5136),
            new TreeTone("chocolate", 0x2E1E18, 0x452D24),
            new TreeTone("ash",       0x55504A, 0x736C63),
            new TreeTone("red brown", 0x4A2A20, 0x6A3D2E),
        };

        /// <summary>
        /// Broadleaf canopies. Greens first, then the browns and golds the owner asked for —
        /// <i>"mix in different shades brown"</i> — because a wood of nothing but greens is what
        /// "dull" actually describes, and a copper or an amber among them does more for the picture
        /// than a ninth green would.
        /// </summary>
        static readonly TreeTone[] BroadleafLeaves =
        {
            new TreeTone("oak green",    0x2E5A27, 0x3E7434),  // the owner's Ancient Oak
            new TreeTone("deep forest",  0x1F4A23, 0x2C6431),
            new TreeTone("meadow",       0x3F6B2A, 0x567F38),
            new TreeTone("willow",       0x4F7942, 0x6A9356),  // the owner's Weeping Willow
            new TreeTone("moss",         0x556B2F, 0x6E8640),  // Mossy Birch, highlight brought down
            new TreeTone("olive",        0x4B5320, 0x64682D),  // Silver Birch, highlight brought down
            new TreeTone("spring",       0x5E7A2B, 0x749236),
            new TreeTone("lime",         0x587219, 0x728F26),
            new TreeTone("sea green",    0x3A6250, 0x4E7F68),
            new TreeTone("copper",       0x5A3A2E, 0x7A4E3B),
            new TreeTone("amber",        0x7A5A1E, 0x9C7529),
            new TreeTone("rust",         0x6E3B22, 0x8E4F2D),
            new TreeTone("plum",         0x4A2A33, 0x653B45),
            new TreeTone("old gold",     0x6E6A24, 0x8C8730),

            // The bright rows, added 2026-09-18 on the owner's *"add some bright colours into the
            // leaf … it seems a bit dull still"*. Every one of them clears the pale test by being
            // **saturated** rather than by being allowed through: the chroma of these lit faces
            // runs 141 to 189, against the 30 and 39 of the two entries that read as white. They
            // are a third of the broadleaf table, so a stand's handful of four draws about one of
            // them on average, which is what lifts a wood rather than repainting it.
            new TreeTone("bright lime",  0x6F9A22, 0x92C62D),
            new TreeTone("spring yellow",0x86A61E, 0xAFD52C),
            new TreeTone("emerald",      0x1E8A4E, 0x2AB768),
            new TreeTone("golden",       0xB08A16, 0xDFB222),
            new TreeTone("flame",        0xA8501A, 0xD9701F),
            new TreeTone("cherry",       0x94301F, 0xC24428),
            new TreeTone("bright teal",  0x1C8C7A, 0x27B79E),
        };

        // -------------------------------------------------------------------- conifer

        static readonly TreeTone[] ConiferBarks =
        {
            new TreeTone("pine",      0x4A2511, 0x8C533C),   // the owner's Scots Pine
            new TreeTone("redwood",   0x3B1E19, 0x7D2E22),   // the owner's Redwood
            new TreeTone("cedar",     0x45291A, 0x633C28),
            new TreeTone("grey pine", 0x423A33, 0x5C524A),
            new TreeTone("dark",      0x2C1C14, 0x432B1F),
            new TreeTone("sienna",    0x52381F, 0x74512E),
        };

        /// <summary>
        /// Needles. Darker and cooler than broadleaf canopies, which is what a conifer is, with one
        /// rusty row so that a stand of them is not all the same note.
        /// </summary>
        static readonly TreeTone[] ConiferLeaves =
        {
            new TreeTone("pine dark",   0x1B4D3E, 0x266A55),  // the owner's Scots Pine
            new TreeTone("spruce",      0x1E3F20, 0x2B5A2D),  // the owner's Redwood
            new TreeTone("blue cedar",  0x24423F, 0x325A56),
            new TreeTone("fir",         0x234A2C, 0x31663C),
            new TreeTone("larch",       0x3A5E2A, 0x4F7E39),
            new TreeTone("sea pine",    0x2B5348, 0x3B7061),
            new TreeTone("deep teal",   0x17403C, 0x215953),
            new TreeTone("olive",       0x3B4B24, 0x526733),
            new TreeTone("rusty",       0x4A3A22, 0x66512F),

            // Three bright needles, for the same reason. Conifers are the commoner tree on the
            // board, so a wood lifted only in its broadleaves would still read dark.
            new TreeTone("bright larch", 0x4E8A2A, 0x68B238),
            new TreeTone("jade",         0x2C8F6B, 0x3BBB8C),
            new TreeTone("gold needle",  0x8A7A22, 0xB5A12D),
        };

        // ------------------------------------------------------------------- the table

        static readonly TreeTheme[] Themes = Cross();

        static TreeTheme[] Cross()
        {
            int conifer = ConiferBarks.Length * ConiferLeaves.Length;
            int broadleaf = BroadleafBarks.Length * BroadleafLeaves.Length;
            var all = new TreeTheme[conifer + broadleaf];

            int n = 0;
            foreach (TreeTone bark in ConiferBarks)
            foreach (TreeTone leaf in ConiferLeaves)
                all[n++] = new TreeTheme(TreeSpecies.Conifer, bark, leaf);
            foreach (TreeTone bark in BroadleafBarks)
            foreach (TreeTone leaf in BroadleafLeaves)
                all[n++] = new TreeTheme(TreeSpecies.Broadleaf, bark, leaf);

            return all;
        }

        /// <summary>
        /// How many themes there are.
        ///
        /// <para>A tint code carries this index in a twelve-bit field of its own
        /// (<c>TintCode.TreeValue</c>), so the ceiling is 4,095. It used to be the low byte, and at
        /// 240 themes this table came within one extra bark tone of wrapping silently and drawing
        /// one wood in another's colours — which is why the field was widened rather than the
        /// ceiling merely asserted. <c>TreePaletteTests</c> still asserts it.</para>
        /// </summary>
        public static int Count => Themes.Length;

        public static TreeTheme At(int index) =>
            index >= 0 && index < Themes.Length ? Themes[index] : Themes[0];

        static readonly int[] ConiferThemes = IndicesOf(TreeSpecies.Conifer);
        static readonly int[] BroadleafThemes = IndicesOf(TreeSpecies.Broadleaf);

        static readonly int[] ConiferBright = BrightIndices(ConiferLeaves);
        static readonly int[] BroadleafBright = BrightIndices(BroadleafLeaves);

        /// <summary>
        /// The theme index for one bark against one leaf. The table is a cross product laid out
        /// bark-major within each species, so this is arithmetic rather than a search — which is
        /// what lets <c>TreeLook</c> choose a bark and a leaf separately and still hand the mesher
        /// a single small number.
        /// </summary>
        public static int ThemeOf(TreeSpecies species, int bark, int leaf)
        {
            TreeTone[] barks = Barks(species);
            TreeTone[] leaves = Leaves(species);
            if (barks.Length == 0 || leaves.Length == 0) return 0;

            int b = ((bark % barks.Length) + barks.Length) % barks.Length;
            int l = ((leaf % leaves.Length) + leaves.Length) % leaves.Length;
            int within = b * leaves.Length + l;
            return species == TreeSpecies.Conifer
                ? within
                : ConiferBarks.Length * ConiferLeaves.Length + within;
        }

        /// <summary>
        /// Which leaf tones of this species are bright, as indices into <see cref="Leaves"/>.
        /// Every stand is dealt one of these; see <see cref="TreeToneRules.BrightLeaf"/>.
        /// </summary>
        public static int[] BrightLeaves(TreeSpecies species) =>
            species == TreeSpecies.Conifer ? ConiferBright : BroadleafBright;

        static int[] BrightIndices(TreeTone[] leaves)
        {
            int n = 0;
            for (int i = 0; i < leaves.Length; i++)
                if (TreeToneRules.Luminance(leaves[i].Lit) >= TreeToneRules.BrightLeaf) n++;
            var found = new int[n];
            n = 0;
            for (int i = 0; i < leaves.Length; i++)
                if (TreeToneRules.Luminance(leaves[i].Lit) >= TreeToneRules.BrightLeaf) found[n++] = i;
            return found;
        }

        /// <summary>The rows a tree of this species may be dealt, as indices into the table.</summary>
        public static int[] For(TreeSpecies species) =>
            species == TreeSpecies.Conifer ? ConiferThemes : BroadleafThemes;

        /// <summary>The tones a species is built from, for a test that wants to check them all.</summary>
        public static TreeTone[] Barks(TreeSpecies species) =>
            species == TreeSpecies.Conifer ? ConiferBarks : BroadleafBarks;

        public static TreeTone[] Leaves(TreeSpecies species) =>
            species == TreeSpecies.Conifer ? ConiferLeaves : BroadleafLeaves;

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
