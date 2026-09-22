#nullable enable
using System;

namespace Odyssey.Hud
{
    /// <summary>
    /// Which bodies, hair pieces and beards a colonist may be dealt, split by gender
    /// (<c>docs/design/29-modular-colonists.md</c> §6, MC3).
    ///
    /// <para><b>The body arrays hold catalogue family indices, never positions in a compacted
    /// list.</b> That is the rule <see cref="ColonistAppearanceBook"/> states at length and
    /// <c>ColonistLookAgreementTests</c> pins: the look index space is the catalogue family index,
    /// always. A gendered pool is a <i>filter</i> over those indices — the lottery picks one of
    /// the legal indices and returns it unchanged. Compacting the survivors and returning a
    /// position is precisely the bug that made look <c>i</c> stop being row <c>i</c> the moment
    /// anything was missing, and it was invisible until a colony crossed the figure cap.</para>
    ///
    /// <para><b>Unity-free by construction</b>, like everything else in the derivation: the
    /// Presentation side builds one of these out of the module catalogue and hands it over, in
    /// exactly the way it already hands over a look count. The whole lottery and its tests then
    /// run in the fast tier.</para>
    ///
    /// <para><b>Empty is legal and means "no attachment".</b> A clone without the licensed packs
    /// resolves no hair and no beards, and every colonist is simply dealt none — a missing piece
    /// drops that slot, never the colonist.</para>
    /// </summary>
    public sealed class ColonistCastPools
    {
        /// <summary>
        /// The pool a world with no catalogue at all uses: one body, no hair, no beards.
        ///
        /// Deliberately not "every body": a caller that has not been given real pools should deal
        /// a visibly minimal cast rather than a plausible-looking wrong one.
        /// </summary>
        public static readonly ColonistCastPools Empty = new ColonistCastPools(
            new[] { 0 }, new[] { 0 }, Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>());

        /// <summary>
        /// Every body in a family of <paramref name="lookCount"/>, in both pools, with no hair and
        /// no beards.
        ///
        /// <para>What a caller who knows only how many rows there are gets — which is the state
        /// the game was in before this unit, and is exactly what it dealt then. Kept as a named
        /// factory rather than an implicit default so that "this colony has no gender information"
        /// is a thing somebody wrote down.</para>
        /// </summary>
        public static ColonistCastPools AllBodies(int lookCount)
        {
            if (lookCount < 1) lookCount = 1;
            var all = new int[lookCount];
            for (int i = 0; i < lookCount; i++) all[i] = i;

            return new ColonistCastPools(
                all, all, Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>());
        }

        public ColonistCastPools(
            int[] maleBodies, int[] femaleBodies,
            int[] maleHair, int[] femaleHair, int[] beards,
            int uniformMale = NoUniform, int uniformFemale = NoUniform)
        {
            MaleBodies = maleBodies;
            FemaleBodies = femaleBodies;
            MaleHair = maleHair;
            FemaleHair = femaleHair;
            Beards = beards;
            UniformMale = uniformMale;
            UniformFemale = uniformFemale;
        }

        /// <summary>No uniform in this catalogue, so every colonist is dealt from the pool.</summary>
        public const int NoUniform = -1;

        /// <summary>The issued uniform's male body, as a catalogue family index.</summary>
        public int UniformMale { get; }

        /// <summary>The issued uniform's female body.</summary>
        public int UniformFemale { get; }

        /// <summary>
        /// Whether this colony issues a uniform at all.
        ///
        /// <para><b>While it does, the body lottery does not run</b> — every colonist wears the
        /// uniform and tells themselves apart by face, hair and beard, which is what the identity
        /// work is for (owner, 2026-09-22). The full pool is still here and still correct; it is
        /// what clothing-as-equipment will draw from (<c>docs/design/29-modular-colonists.md</c>
        /// §9), which is why it is kept rather than emptied.</para>
        /// </summary>
        public bool HasUniform => UniformMale != NoUniform || UniformFemale != NoUniform;

        /// <summary>
        /// The uniform body for a gender, or <see cref="NoUniform"/> where there is none.
        ///
        /// <para>A neutral name takes the male cut, and that is a placeholder rather than a
        /// judgement: the jumpsuit is one garment with two cuts and there is no third. When
        /// clothing becomes an item the garment will carry both and the cut will follow the body,
        /// not the name.</para>
        /// </summary>
        public int UniformFor(char gender) =>
            gender == 'f'
                ? (UniformFemale != NoUniform ? UniformFemale : UniformMale)
                : (UniformMale != NoUniform ? UniformMale : UniformFemale);

        /// <summary>Catalogue family indices of the male-shaped bodies a colonist may wear.</summary>
        public int[] MaleBodies { get; }

        /// <summary>Catalogue family indices of the female-shaped bodies.</summary>
        public int[] FemaleBodies { get; }

        /// <summary>Attachment indices of the hair a man may be dealt.</summary>
        public int[] MaleHair { get; }

        /// <summary>Attachment indices of the hair a woman may be dealt.</summary>
        public int[] FemaleHair { get; }

        /// <summary>Attachment indices of the beards. Only drawn for men and neutral names.</summary>
        public int[] Beards { get; }

        /// <summary>
        /// The bodies this gender may wear.
        ///
        /// <para><c>n</c> draws from <b>both</b> pools, which is why this concatenates rather than
        /// picking a side. It is a real value in <c>colonist-names.csv</c> and not a fallback
        /// (<c>docs/design/29-modular-colonists.md</c> §6), so silence here would have become an
        /// accident — a neutral-named colonist quietly always male.</para>
        /// </summary>
        public int[] BodiesFor(char gender) => Pick(gender, MaleBodies, FemaleBodies, ref _bothBodies);

        /// <summary>The hair this gender may be dealt, on the same rule.</summary>
        public int[] HairFor(char gender) => Pick(gender, MaleHair, FemaleHair, ref _bothHair);

        /// <summary>
        /// Whether this gender is dealt a beard at all.
        ///
        /// Men and neutral names; never women. A neutral name draws from both pools everywhere
        /// else, and a beard is the one slot where "both" has no meaning — so it is offered, and
        /// the ordinary clean-shaven roll decides.
        /// </summary>
        public bool CanGrowABeard(char gender) => gender != 'f' && Beards.Length > 0;

        int[]? _bothBodies;
        int[]? _bothHair;

        // Concatenated once and kept, because the neutral pools are asked for per colonist and
        // allocating a joined array per question would put a garbage collection in the middle of
        // drawing a colony.
        static int[] Pick(char gender, int[] male, int[] female, ref int[]? both)
        {
            if (gender == 'm') return male;
            if (gender == 'f') return female;

            if (both == null)
            {
                var joined = new int[male.Length + female.Length];
                Array.Copy(male, 0, joined, 0, male.Length);
                Array.Copy(female, 0, joined, male.Length, female.Length);
                both = joined;
            }

            return both;
        }
    }
}
