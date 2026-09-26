#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Builds a <see cref="ColonistAppearanceBook"/> for the catalogue the game actually loaded.
    ///
    /// <para><b>All that is left behind by the move.</b> The book itself went down into
    /// <c>Odyssey.Hud</c> on 2026-09-18 so that the HUD could ask what a colonist looks like
    /// (<c>docs/design/20-avatars.md</c>) and so that its tests could run in the fast tier. The one
    /// thing it could not take with it was counting the colonist family, because
    /// <see cref="ModuleCatalogue"/> is Unity and <c>Odyssey.Hud</c> is not. That count is this
    /// file, and it is the whole of the difference.</para>
    ///
    /// <para><b>Every row, holes included</b>, exactly as before: if the lottery ran over rows that
    /// happen to have usable art, installing a pack would silently re-deal the entire colony. One
    /// when there is no catalogue at all, so a clone without the licensed packs still answers
    /// rather than dividing by zero.</para>
    /// </summary>
    public static class AppearanceBooks
    {
        public static ColonistAppearanceBook For(uint seed, ModuleCatalogue? catalogue) =>
            new ColonistAppearanceBook(
                seed,
                catalogue == null ? 1 : catalogue.FindFamily(ModuleIds.ColonistBase).Count,
                PoolsFrom(catalogue));

        /// <summary>
        /// The gendered pools, read off the catalogue
        /// (<c>docs/design/29-modular-colonists.md</c> §6, MC2/MC4).
        ///
        /// <para><b>The body arrays carry family indices, not positions.</b> A row that is not in
        /// the colonist pool is skipped and <i>the index still advances</i>, because the index is
        /// what the look space is made of. This is the one line in the whole unit where the fault
        /// <c>ColonistAppearanceBook</c> warns about would be introduced, and it is why the loop
        /// counts rather than adds.</para>
        ///
        /// <para><b>A piece that does not recolour is not in the pool at all.</b> Six of them span
        /// real texture instead of one swatch cell and repainting them throws art away
        /// (<c>docs/research/e-06-modular-colonists.md</c> §7). They are excluded by the content
        /// flag rather than by a rule here.</para>
        ///
        /// <para>Empty pools are legal and mean a clone without the licensed packs: every colonist
        /// is then dealt body 0, no hair and no beard, and nothing breaks.</para>
        /// </summary>
        public static ColonistCastPools PoolsFrom(ModuleCatalogue? catalogue)
        {
            if (catalogue == null) return ColonistCastPools.Empty;

            var maleBodies = new List<int>();
            var femaleBodies = new List<int>();
            var banditMale = new List<int>();
            var banditFemale = new List<int>();
            int uniformMale = ColonistCastPools.NoUniform;
            int uniformFemale = ColonistCastPools.NoUniform;
            List<ModuleEntry> bodies = catalogue.FindFamily(ModuleIds.ColonistBase);
            for (int i = 0; i < bodies.Count; i++)
            {
                ModuleEntry row = bodies[i];

                // The gang's rows (design 42): a bandit's body, never a colonist's, so they are
                // read into their own pools and skipped by the lottery below whatever else the
                // row says.
                if (row.bandit)
                {
                    if (row.sex == BodySex.Female) banditFemale.Add(i);
                    else banditMale.Add(i);
                    continue;
                }

                // The uniform is read whether or not its row is in the lottery: it is what a
                // colonist wears, not one of the things they might be dealt.
                if (row.uniform)
                {
                    if (row.sex != BodySex.Female && uniformMale == ColonistCastPools.NoUniform)
                        uniformMale = i;
                    if (row.sex != BodySex.Male && uniformFemale == ColonistCastPools.NoUniform)
                        uniformFemale = i;
                }

                if (!row.colonistPool) continue;
                if (row.sex != BodySex.Female) maleBodies.Add(i);
                if (row.sex != BodySex.Male) femaleBodies.Add(i);
            }

            // A catalogue with no colonist rows at all still has to answer, or the lottery divides
            // by zero on a machine with no packs.
            if (maleBodies.Count == 0) maleBodies.Add(0);
            if (femaleBodies.Count == 0) femaleBodies.Add(0);

            var maleHair = new List<int>();
            var femaleHair = new List<int>();
            List<ModuleEntry> hair = catalogue.FindFamily(ModuleIds.HairBase);
            for (int i = 0; i < hair.Count; i++)
            {
                ModuleEntry row = hair[i];
                if (!row.recolours) continue;
                if (row.sex != BodySex.Female) maleHair.Add(i);
                if (row.sex != BodySex.Male) femaleHair.Add(i);
            }

            var beards = new List<int>();
            List<ModuleEntry> beard = catalogue.FindFamily(ModuleIds.BeardBase);
            for (int i = 0; i < beard.Count; i++)
                if (beard[i].recolours)
                    beards.Add(i);

            // The headgear a bandit wears: every row that resolved art, by family index. Without
            // the packs it is empty and a bandit goes bare-headed with their own hair.
            var headgear = new List<int>();
            List<ModuleEntry> heads = catalogue.FindFamily(ModuleIds.HeadgearBase);
            for (int i = 0; i < heads.Count; i++)
                if (heads[i].prefab != null)
                    headgear.Add(i);

            return new ColonistCastPools(
                maleBodies.ToArray(), femaleBodies.ToArray(),
                maleHair.ToArray(), femaleHair.ToArray(), beards.ToArray(),
                uniformMale, uniformFemale,
                banditMale.ToArray(), banditFemale.ToArray(), headgear.ToArray());
        }
    }
}
