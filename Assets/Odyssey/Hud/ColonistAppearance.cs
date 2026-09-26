#nullable enable
using System;

namespace Odyssey.Hud
{
    /// <summary>A colour as three bytes. Deliberately not <c>UnityEngine.Color</c>.</summary>
    /// <remarks>
    /// The whole appearance derivation is integer arithmetic over this struct: no float, no
    /// <c>System.Random</c>, no <c>UnityEngine.Random</c>. That is what makes a colonist's colours
    /// identical under Mono and CoreCLR, which is the standing worry <c>OQ-05</c> records, and it
    /// is why the type exists rather than reaching for <c>Color32</c>. It also means this file
    /// could move to a Unity-free assembly as a file move, if the fast tier ever wants it.
    /// </remarks>
    public readonly struct Rgb24 : IEquatable<Rgb24>
    {
        public readonly byte R;
        public readonly byte G;
        public readonly byte B;

        public Rgb24(byte r, byte g, byte b) { R = r; G = g; B = b; }

        /// <summary>From <c>0xRRGGBB</c>, which is how the palette tables below are written.</summary>
        public static Rgb24 FromHex(uint rgb) =>
            new Rgb24((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));

        /// <summary>Packed back to <c>0xRRGGBB</c>. The material cache keys on this.</summary>
        public uint Packed => ((uint)R << 16) | ((uint)G << 8) | B;

        /// <summary>
        /// The same colour at <paramref name="percent"/> of its value, rounded.
        ///
        /// Integer arithmetic on purpose (see the type's remarks). Used only to derive a garment's
        /// secondary colour from its primary, so it never needs to lighten.
        /// </summary>
        public Rgb24 Scaled(int percent) =>
            new Rgb24(Scale(R, percent), Scale(G, percent), Scale(B, percent));

        static byte Scale(byte c, int percent)
        {
            int v = (c * percent + 50) / 100;
            return (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
        }

        /// <summary>
        /// This colour moved <paramref name="percent"/> of the way towards another.
        ///
        /// Integer arithmetic on purpose (see the type's remarks); used to grey a colonist's hair
        /// with age, which is the one thing about an appearance that is a function of something
        /// other than the seed.
        /// </summary>
        public Rgb24 MixedWith(Rgb24 other, int percent)
        {
            if (percent <= 0) return this;
            if (percent >= 100) return other;
            return new Rgb24(Blend(R, other.R, percent), Blend(G, other.G, percent),
                Blend(B, other.B, percent));
        }

        static byte Blend(byte a, byte b, int percent) =>
            (byte)((a * (100 - percent) + b * percent + 50) / 100);

        public bool Equals(Rgb24 other) => R == other.R && G == other.G && B == other.B;
        public override bool Equals(object? obj) => obj is Rgb24 other && Equals(other);
        public override int GetHashCode() => (int)Packed;
        public override string ToString() => "#" + Packed.ToString("X6");
    }

    /// <summary>
    /// What one colonist looks like: which body, and the colour of its skin, hair and clothes.
    ///
    /// <para>Every field is a pure function of <c>(world seed, pawn id)</c>. Nothing here is saved,
    /// nothing is hashed, and the simulation cannot read any of it — a colonist's appearance is
    /// drawing, in exactly the sense the grass tufts and the axe chips are drawing. It is derived
    /// afresh on every load, and it is the *same* derivation, so the colony you come back to is
    /// the colony you left.</para>
    ///
    /// <para><b>Why the seed and not a session salt.</b> A face used to be
    /// <c>hash(pawn id, a number rolled at startup)</c>, because a plain hash of the id dealt the
    /// starting five — who are always pawns 1 to 5 — the same five faces every play, and a cast of
    /// sixty-one read as a cast of five. The world seed fixes that without the instability: it
    /// varies from world to world, so a new colony is still a new cast, while a given world always
    /// deals itself the same people.</para>
    /// </summary>
    public readonly struct ColonistAppearance : IEquatable<ColonistAppearance>
    {
        /// <summary>Index into the catalogue's colonist family. See <see cref="ColonistLook"/>.</summary>
        public readonly int Look;

        public readonly Rgb24 Skin;
        public readonly Rgb24 Hair;

        /// <summary>The main garment.</summary>
        public readonly Rgb24 Cloth;

        /// <summary>
        /// The second garment, always a darker shade of <see cref="Cloth"/>.
        ///
        /// Derived rather than drawn, because two unrelated saturated garments on one person is
        /// what actually reads as a clown. A darker shade of the same cloth reads as trousers to a
        /// jacket, which is what a colonist should look like.
        /// </summary>
        public readonly Rgb24 Cloth2;

        /// <summary>
        /// Which hair piece this colonist wears, as an index into the attachment family, or
        /// <see cref="NoPiece"/> for none.
        ///
        /// <para><b>None is bald, and it is a real outcome rather than a missing value.</b> It is
        /// also what every colonist gets on a machine with no licensed packs, which is why the
        /// drawers must treat it as ordinary.</para>
        /// </summary>
        public readonly int HairPiece;

        /// <summary>
        /// Which beard, or <see cref="NoPiece"/> for clean-shaven.
        ///
        /// <para><b>The beard is painted from the hair's own atlas cell</b>, so it is the hair
        /// colour exactly and there is no separate beard colour to carry. That is not a decision
        /// this code makes -- it is what the art does, measured
        /// (<c>docs/research/e-06-modular-colonists.md</c> section 6).</para>
        /// </summary>
        public readonly int BeardPiece;

        /// <summary>No piece in this slot: bald, or clean-shaven.</summary>
        public const int NoPiece = -1;

        /// <summary>
        /// What this person is wearing over themselves (<c>docs/design/42-bandits.md</c> §5).
        /// Everything above is the person; this, <see cref="HeadPiece"/>, and the body and cloth
        /// it chose are the clothes.
        /// </summary>
        public readonly PawnOutfit Outfit;

        /// <summary>
        /// What covers the head — the bandit's welding helmet — as an index into the headgear
        /// family, or <see cref="NoPiece"/>.
        ///
        /// <para><b>It hides the hair and the beard; it does not delete them.</b>
        /// <see cref="HairPiece"/> and <see cref="BeardPiece"/> keep the person's own, so the day
        /// the helmet comes off (a captured bandit) the face under it is the one they were rolled
        /// with. A drawer asks <see cref="HidesHair"/> and wears neither while it is true.</para>
        /// </summary>
        public readonly int HeadPiece;

        /// <summary>Whether the hair and beard slots go unworn because something covers the head.</summary>
        public bool HidesHair => HeadPiece != NoPiece;

        public ColonistAppearance(int look, Rgb24 skin, Rgb24 hair, Rgb24 cloth, Rgb24 cloth2)
            : this(look, skin, hair, cloth, cloth2, NoPiece, NoPiece)
        {
        }

        public ColonistAppearance(int look, Rgb24 skin, Rgb24 hair, Rgb24 cloth, Rgb24 cloth2,
            int hairPiece, int beardPiece)
            : this(look, skin, hair, cloth, cloth2, hairPiece, beardPiece, PawnOutfit.Issued, NoPiece)
        {
        }

        public ColonistAppearance(int look, Rgb24 skin, Rgb24 hair, Rgb24 cloth, Rgb24 cloth2,
            int hairPiece, int beardPiece, PawnOutfit outfit, int headPiece)
        {
            Look = look;
            Skin = skin;
            Hair = hair;
            Cloth = cloth;
            Cloth2 = cloth2;
            HairPiece = hairPiece;
            BeardPiece = beardPiece;
            Outfit = outfit;
            HeadPiece = headPiece;
        }

        /// <summary>
        /// The appearance of one pawn wearing <paramref name="outfit"/>
        /// (<c>docs/design/42-bandits.md</c> §5).
        ///
        /// <para><b>The person is rolled first, exactly as a colonist is</b>, by the overload below —
        /// every stream in the same order — and then dressed. So a bandit's sex, skin, hair and
        /// beard are the ones the same seed and id would deal a colonist, and a bandit taken
        /// prisoner and later dressed in the uniform is the person the gang had.</para>
        ///
        /// <para>A bandit's clothes replace the body (the gang's row for their sex and a vest cut
        /// rolled on a stream of its own), the cloth (a red from <see cref="BanditReds"/>, on
        /// another), the trousers (<see cref="BanditTrousers"/>, fixed) and the head
        /// (<see cref="ColonistCastPools.Headgear"/>). A catalogue with no bandit rows leaves the
        /// body as rolled and still paints the colours, so a clone without the packs degrades to a
        /// red-and-black colonist rather than to nothing.</para>
        /// </summary>
        public static ColonistAppearance Of(
            uint seed, int pawnId, ColonistCastPools pools, char gender, int age, PawnOutfit outfit)
        {
            ColonistAppearance person = Of(seed, pawnId, pools, gender, age);

            // The prison jumpsuit (design 60 §11d): the issued suit's cut in the prison's colours,
            // the face, hair and beard the person was dealt. No headgear — a prisoner is known by
            // her clothes and her face, not a helmet.
            if (outfit == PawnOutfit.Prisoner)
                return new ColonistAppearance(person.Look, person.Skin, person.Hair, PrisonCloth, PrisonTrim,
                    person.HairPiece, person.BeardPiece, PawnOutfit.Prisoner, NoPiece);

            if (outfit != PawnOutfit.Bandit) return person;

            char sex = SexOf(gender, seed, pawnId);
            int look = person.Look;
            int[] gang = pools.BanditBodiesFor(sex);
            if (gang.Length > 0) look = gang[(int)(Mix(seed, pawnId, VestStream) % (uint)gang.Length)];

            Rgb24 red = BanditReds[(int)(Mix(seed, pawnId, BanditRedStream) % (uint)BanditReds.Length)];
            int head = pools.Headgear.Length > 0 ? pools.Headgear[0] : NoPiece;

            return new ColonistAppearance(look, person.Skin, person.Hair, red, BanditTrousers,
                person.HairPiece, person.BeardPiece, PawnOutfit.Bandit, head);
        }

        /// <summary>
        /// The appearance of one pawn in one world.
        ///
        /// <paramref name="lookCount"/> is the size of the catalogue's colonist family — every row
        /// of it, including rows whose art failed to resolve. Holes are the caller's problem, and
        /// deliberately so: if the lottery ran over "rows that happen to have usable art" then
        /// installing three packs of four would silently re-deal the whole colony.
        /// </summary>
        public static ColonistAppearance Of(uint seed, int pawnId, int lookCount)
        {
            int look = ColonistLook.For(pawnId, lookCount, seed);

            // Three independent streams, not one hash taken modulo three different lengths. That
            // shortcut correlates the slots — everyone with red hair also wears red — and it is
            // invisible until somebody looks at fifty colonists at once. Each stream gets its own
            // mixing constant, and a test asserts the distributions really are independent.
            Rgb24 skin = Pick(ColonistPalette.Skin, seed, pawnId, SkinStream);
            Rgb24 hair = Pick(ColonistPalette.Hair, seed, pawnId, HairStream);
            Rgb24 cloth = Pick(ColonistPalette.Cloth, seed, pawnId, ClothStream);

            int[] shades = ColonistPalette.SecondShades;
            int shade = shades[(int)(Mix(seed, pawnId, ShadeStream) % (uint)shades.Length)];

            return new ColonistAppearance(look, skin, hair, cloth, cloth.Scaled(shade));
        }

        /// <summary>
        /// The appearance of one pawn, dealt from gendered pools and wearing hair and a beard
        /// (<c>docs/design/29-modular-colonists.md</c> section 6, MC3).
        ///
        /// <para><b>The body is a catalogue family index, not a position in the pool.</b> The
        /// lottery picks one of the legal indices and returns it unchanged, so a gendered pool
        /// never re-indexes the look space. See <see cref="ColonistCastPools"/>.</para>
        ///
        /// <para><b><paramref name="age"/> is the only input that is not the seed</b>, and it
        /// greys the hair. Nothing ages in this game yet -- <c>ColonistIdentity.Age</c> is itself
        /// a function of the roll seed -- so an appearance is still a pure function of
        /// (seed, pawn) and <see cref="ColonistAppearanceBook"/>'s cache needs nothing.
        /// <b>The day ageing lands, that cache must gain the age</b> or a colonist keeps the hair
        /// they were born with for ever.</para>
        /// </summary>
        public static ColonistAppearance Of(
            uint seed, int pawnId, ColonistCastPools pools, char gender, int age)
        {
            // A neutral name is resolved to a definite sex here, once, and everything below reads
            // that rather than the name. See SexOf: it is what makes a colonist coherent.
            gender = SexOf(gender, seed, pawnId);

            // The pool is indexed by the *existing* body lottery rather than by a stream of its
            // own. ColonistLook.For's distribution was tuned and is pinned by tests -- a plain
            // remainder deals the opening five the first five rows, which came out as the whole
            // starting colony being office workers -- and reusing it means a full pool deals
            // exactly what it dealt before gendered pools existed. The gendering is then a pure
            // filter, which is the claim ColonistCastPools makes.
            int[] bodies = pools.BodiesFor(gender);
            int look = bodies.Length == 0
                ? 0
                : bodies[ColonistLook.For(pawnId, bodies.Length, seed)];

            Rgb24 skin = Pick(ColonistPalette.Skin, seed, pawnId, SkinStream);
            Rgb24 hair = Pick(ColonistPalette.Hair, seed, pawnId, HairStream);
            Rgb24 cloth = Pick(ColonistPalette.Cloth, seed, pawnId, ClothStream);

            int[] shades = ColonistPalette.SecondShades;
            int shade = shades[(int)(Mix(seed, pawnId, ShadeStream) % (uint)shades.Length)];
            Rgb24 cloth2 = cloth.Scaled(shade);

            // The colony issues a uniform, so the body lottery and the colour roll do not run.
            // Everything above still does: the rolls stay in the same order and keep consuming
            // their own streams, so switching the uniform off gives back exactly the cast that
            // would have been dealt without it rather than a re-shuffled one.
            if (pools.HasUniform)
            {
                int issued = pools.UniformFor(gender);
                if (issued != ColonistCastPools.NoUniform) look = issued;
                cloth = UniformCloth;
                cloth2 = UniformTrim;
            }

            hair = hair.MixedWith(Grey, GreyingAt(age));

            return new ColonistAppearance(look, skin, hair, cloth, cloth2,
                HairPieceFor(seed, pawnId, pools, gender, age),
                BeardPieceFor(seed, pawnId, pools, gender));
        }

        /// <summary>
        /// The sex a colonist actually is, which for most of them is the one their name carries.
        ///
        /// <para><b>A neutral name is dealt one rather than defaulting to a side.</b> Thirty of the
        /// two hundred and forty names in <c>colonist-names.csv</c> are <c>n</c> - Avery, Riley,
        /// Rowan, Wren and the nicknames - and they are neutral because the <i>name</i> is, not
        /// because the person is. Treating <c>n</c> as "draw from both pools" separately in each
        /// slot made an incoherent colonist: a female body that could still grow a beard. Treating
        /// it as "male unless told otherwise", which the uniform did, was worse - it made all
        /// thirty of them men, in every colony, for ever (owner, 2026-09-22, asking why names and
        /// characters did not match).</para>
        ///
        /// <para>So it is one coin, flipped from the same seed everything else about them comes
        /// from. Rowan is a man in one colony and a woman in another, and is the same person on
        /// the setup card, on the board and after a reload - because the flip is a pure function
        /// of the pair, like the name and the age beside it.</para>
        ///
        /// <para>At the population level this is still "both pools", which is what it was always
        /// for. What it is not any more is both pools <i>at once</i>.</para>
        /// </summary>
        public static char SexOf(char gender, uint seed, int pawnId)
        {
            if (gender == 'm' || gender == 'f') return gender;
            return (Mix(seed, pawnId, SexStream) & 1u) == 0u ? 'm' : 'f';
        }

        /// <summary>
        /// What hair this colonist has, or <see cref="NoPiece"/> when they are bald.
        ///
        /// <para><b>Baldness is men only and rises with age.</b> A flat rate reads as a quirk;
        /// rising with age reads as a colony of people who have been alive for different lengths
        /// of time, which is the same argument greying makes. Women are never dealt none, because
        /// the female hair pool is thin enough already
        /// (<c>docs/research/e-06-modular-colonists.md</c> section 7) and losing one more option
        /// to baldness would show.</para>
        /// </summary>
        static int HairPieceFor(uint seed, int pawnId, ColonistCastPools pools, char gender, int age)
        {
            int[] hair = pools.HairFor(gender);
            if (hair.Length == 0) return NoPiece;

            if (gender != 'f')
            {
                // Per mille, like every other rate in this project, so the arithmetic stays
                // integer: 50 at eighteen, rising by 8 a year past forty, capped at 300.
                int chance = 50 + 8 * (age > 40 ? age - 40 : 0);
                if (chance > 300) chance = 300;
                if (Mix(seed, pawnId, BaldStream) % 1000u < (uint)chance) return NoPiece;
            }

            return hair[(int)(Mix(seed, pawnId, HairPieceStream) % (uint)hair.Length)];
        }

        /// <summary>
        /// What beard, or <see cref="NoPiece"/> for clean-shaven.
        ///
        /// <para>Clean-shaven is the commoner outcome at 55%, so a beard reads as a choice that
        /// person made rather than as the house style.</para>
        /// </summary>
        static int BeardPieceFor(uint seed, int pawnId, ColonistCastPools pools, char gender)
        {
            if (!pools.CanGrowABeard(gender)) return NoPiece;
            if (Mix(seed, pawnId, ShavenStream) % 1000u < 550u) return NoPiece;

            return pools.Beards[(int)(Mix(seed, pawnId, BeardStream) % (uint)pools.Beards.Length)];
        }

        /// <summary>
        /// How grey this colonist's hair is, as a percentage.
        ///
        /// <para>Nothing until forty-five, then linear. The endpoint is deliberately past the
        /// oldest a colonist can be: the pool tops out at sixty-five
        /// (<c>ColonistIdentity.MaximumAge</c>) and a colony whose eldest are snow-white reads as
        /// a retirement home rather than as people who have been working outdoors.</para>
        /// </summary>
        public static int GreyingAt(int age)
        {
            const int Starts = 45;
            const int FullyGreyAt = 80;
            if (age <= Starts) return 0;

            int percent = (age - Starts) * 100 / (FullyGreyAt - Starts);
            return percent > 100 ? 100 : percent;
        }

        /// <summary>What hair greys towards. Not white: a value grey that still takes light.</summary>
        static readonly Rgb24 Grey = Rgb24.FromHex(0xBFBCB6);

        /// <summary>
        /// The issued uniform: white with a slight blue tint (owner, 2026-09-22).
        ///
        /// <para><b>Not pure white</b>, which has nowhere to go under the golden-hour grading and
        /// reads as a hole in the frame rather than as cloth. This keeps a couple of per cent of
        /// blue in it, so it takes the warm light at dusk and the cold light at dawn and stays
        /// legibly a garment in both.</para>
        /// </summary>
        public static readonly Rgb24 UniformCloth = Rgb24.FromHex(0xE8EDF6);

        /// <summary>
        /// The uniform's second garment — collar, cuffs and boots.
        ///
        /// <para>Fixed rather than rolled, because a uniform whose trim varied per colonist is not
        /// a uniform. It is the same hue carried down to a blue-grey, so the two read as one
        /// garment rather than as two.</para>
        /// </summary>
        public static readonly Rgb24 UniformTrim = Rgb24.FromHex(0xA8B2C2);

        /// <summary>
        /// The prison jumpsuit (design 60 §11d): a burnt orange, a proposal the owner confirms at
        /// the first look. Chosen to be told from the colony's white and the gang's red at a glance,
        /// by every eye: it sits apart from both in lightness as well as hue.
        /// </summary>
        public static readonly Rgb24 PrisonCloth = Rgb24.FromHex(0xD9772E);

        /// <summary>The jumpsuit's trim — collar, cuffs and boots — its orange carried down.</summary>
        public static readonly Rgb24 PrisonTrim = Rgb24.FromHex(0x7A4524);

        /// <summary>
        /// Whether everybody drawn in body <paramref name="look"/> wears the same cloth, and which.
        ///
        /// <para>True for the issued uniform's bodies while a uniform is issued, and for nothing
        /// else: every other colour is rolled per colonist. This is what lets the baked far form
        /// wear the uniform. That form is one material per body, never per colonist
        /// (<c>docs/design/29-modular-colonists.md</c> §13), so it can wear a colour only
        /// when the colour belongs to the body rather than to the person. Until 2026-09-24 it wore the
        /// pack's own paint, and that jumpsuit's paint is **burnt orange** (<c>#B06F24</c>, sampled
        /// off the atlas at the uniform row's cloth rectangle). So every colonist past the 64-figure
        /// cap changed into an orange suit, and changed back on coming near the camera.</para>
        ///
        /// <para>Beside <see cref="Of"/>, which applies the same two constants on the same
        /// condition, so the two cannot come to disagree about what the uniform is.</para>
        /// </summary>
        public static bool IssuedCloth(ColonistCastPools pools, int look, out Rgb24 cloth, out Rgb24 cloth2)
        {
            bool issued = pools.HasUniform && look != ColonistCastPools.NoUniform &&
                          (look == pools.UniformMale || look == pools.UniformFemale);
            cloth = issued ? UniformCloth : default;
            cloth2 = issued ? UniformTrim : default;
            return issued;
        }

        /// <summary>
        /// The reds a bandit's vest is dealt, crimson to rust (owner, 2026-09-24: "dirt / wear on
        /// the red"), so a gang does not look freshly issued and every one of them still reads as
        /// red at the play camera. Fixed, not derived from a hue, because the rust end is where a
        /// derivation would drift into brown.
        /// </summary>
        public static readonly Rgb24[] BanditReds =
        {
            Rgb24.FromHex(0xA01C1C),
            Rgb24.FromHex(0x8E1B22),
            Rgb24.FromHex(0xA62B1E),
            Rgb24.FromHex(0x922A16),
            Rgb24.FromHex(0x7F2119),
        };

        /// <summary>
        /// A bandit's trousers, and everything else on the body that is not skin. Near-black
        /// rather than black, for the reason <see cref="UniformCloth"/> is not pure white: the
        /// golden-hour grade needs somewhere to go.
        /// </summary>
        public static readonly Rgb24 BanditTrousers = Rgb24.FromHex(0x1E1E22);

        /// <summary>
        /// Whether body <paramref name="look"/> is one of the gang's, and the colours its far
        /// form wears (<see cref="IssuedCloth"/>'s twin). The far form is one material per body,
        /// never per person, so it wears one red — the table's first — rather than each bandit's
        /// own; the difference is invisible at the distance that form is drawn from.
        /// </summary>
        public static bool BanditCloth(ColonistCastPools pools, int look, out Rgb24 cloth, out Rgb24 cloth2)
        {
            bool bandit = pools.IsBanditBody(look);
            cloth = bandit ? BanditReds[0] : default;
            cloth2 = bandit ? BanditTrousers : default;
            return bandit;
        }

        static Rgb24 Pick(Rgb24[] table, uint seed, int pawnId, uint stream) =>
            table[(int)(Mix(seed, pawnId, stream) % (uint)table.Length)];

        const uint SkinStream = 0x9E3779B9u;
        const uint HairStream = 0x85EBCA6Bu;
        const uint ClothStream = 0xC2B2AE35u;
        const uint ShadeStream = 0x27D4EB2Fu;

        // Four more streams, each with its own constant for the reason the first four have theirs:
        // a shortcut that takes one hash modulo several lengths correlates the slots, and the
        // correlation is invisible until somebody looks at fifty colonists at once.
        const uint HairPieceStream = 0xD3A2646Cu;
        const uint BeardStream = 0xFD7046C5u;
        const uint BaldStream = 0xB55A4F09u;
        const uint ShavenStream = 0x5BD1E995u;
        const uint SexStream = 0x9E3779BBu;

        // The bandit's two rolls (design 42 §5), after everything the person takes, so dressing
        // somebody moves nothing about who they are.
        const uint VestStream = 0x68E31DA4u;
        const uint BanditRedStream = 0xB5297A4Du;

        /// <summary>One avalanche over (seed, pawn, stream). Integers only, unchecked, no float.</summary>
        static uint Mix(uint seed, int pawnId, uint stream)
        {
            unchecked
            {
                uint h = seed ^ stream;
                h ^= (uint)pawnId * 2654435761u;
                h ^= h >> 16;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>
        /// Two appearances are the same person only if every visible thing about them matches.
        ///
        /// <para><b>The piece indices are part of this, and leaving them out was a real bug.</b>
        /// <see cref="PortraitStudio"/> caches a photograph <i>keyed on the appearance</i> — that
        /// is its whole performance argument, since two colonists who genuinely look alike should
        /// share one picture. When hair and beards were added and this was not, every colonist
        /// with the same body and colours collapsed onto one cache entry, and the first one
        /// photographed lent its face to all of them. The symptom was a contact sheet on which
        /// fifteen different hair pieces drew the identical frame.</para>
        ///
        /// <para>Anything added to this struct that a drawer can see belongs here too.</para>
        /// </summary>
        public bool Equals(ColonistAppearance other) =>
            Look == other.Look && Skin.Equals(other.Skin) && Hair.Equals(other.Hair) &&
            Cloth.Equals(other.Cloth) && Cloth2.Equals(other.Cloth2) &&
            HairPiece == other.HairPiece && BeardPiece == other.BeardPiece &&
            Outfit == other.Outfit && HeadPiece == other.HeadPiece;

        public override bool Equals(object? obj) => obj is ColonistAppearance other && Equals(other);

        public override int GetHashCode() =>
            unchecked(((((((((Look * 397) ^ (int)Skin.Packed) * 397 ^ (int)Hair.Packed) * 397 ^
                           (int)Cloth.Packed) * 397 ^ HairPiece) * 397) ^ BeardPiece) * 397 ^
                       ((int)Outfit << 16 | (HeadPiece & 0xFFFF))));

        public override string ToString() =>
            "look " + Look + ", skin " + Skin + ", hair " + Hair + ", cloth " + Cloth + "/" + Cloth2 +
            ", hairPiece " + HairPiece + ", beard " + BeardPiece +
            (Outfit == PawnOutfit.Issued ? string.Empty : ", outfit " + Outfit + ", head " + HeadPiece);
    }

    /// <summary>
    /// The colours a colonist can be given.
    ///
    /// <para><b>Drawn uniformly, on purpose, for now.</b> The owner's call (2026-09-16) was
    /// "random for now so we can get a feel, as we'll likely colour different races or types
    /// eventually". A uniform draw shows the whole range of each table, which is what makes a
    /// judgement possible; a weighted one would hide the ends. When races or types arrive they
    /// arrive as their own tables selected by kind, and the weighting argument can be had then
    /// against something real.</para>
    ///
    /// <para>These are <b>our</b> colours, not pixels read out of the licensed atlas. The shader
    /// replaces the colour inside a swatch rectangle rather than sampling a different swatch, so
    /// the palette is not limited to the cells Synty happened to paint — and nothing from
    /// <c>Assets/Synty</c> is copied into the repository, which is the rule in CLAUDE.md.</para>
    ///
    /// <para>Nothing here is named. An unnamed internal palette is not game content under the wiki
    /// obligation; the moment an appearance panel shows a colour by name, that same commit adds
    /// <c>ui.appearance.*</c> rows to <c>docs/design/icon-keys.csv</c> and regenerates.</para>
    /// </summary>
    public static class ColonistPalette
    {
        /// <summary>
        /// Skin as a ramp rather than a palette: one progression from pale to dark, so a draw is a
        /// position on it and can never be a *choice* of hue. That alone removes most of the way a
        /// randomised colony can read as a clown parade.
        /// </summary>
        public static readonly Rgb24[] Skin =
        {
            Rgb24.FromHex(0xF0C8A0),
            Rgb24.FromHex(0xE0B088),
            Rgb24.FromHex(0xC89870),
            Rgb24.FromHex(0xA87850),
            Rgb24.FromHex(0x8A5C3C),
            Rgb24.FromHex(0x6B462C),
            Rgb24.FromHex(0x4A301E),
        };

        /// <summary>
        /// Hair: seven naturals and two synthetics. The synthetics are in because this is a ruined
        /// sci-fi city whose cast already contains cyberpunks and cyborgs, not because a colony
        /// wants novelty.
        /// </summary>
        public static readonly Rgb24[] Hair =
        {
            Rgb24.FromHex(0x1A1512),
            Rgb24.FromHex(0x3B2A1E),
            Rgb24.FromHex(0x5C4028),
            Rgb24.FromHex(0x8A5A2B),
            Rgb24.FromHex(0xB98D4B),
            Rgb24.FromHex(0xD9C08A),
            Rgb24.FromHex(0x9A9A96),
            Rgb24.FromHex(0x2E6B7A),
            Rgb24.FromHex(0x7A3B5E),
        };

        /// <summary>
        /// Clothing: muted earths, greys, denims and olives, with a few accents. Mid-value and
        /// mostly desaturated because the Synty look is already flat and saturated — a colour that
        /// competes with the art reads as a costume rather than as clothes.
        /// </summary>
        public static readonly Rgb24[] Cloth =
        {
            Rgb24.FromHex(0x4A4F55),
            Rgb24.FromHex(0x6B7078),
            Rgb24.FromHex(0x3C4A5A),
            Rgb24.FromHex(0x54606E),
            Rgb24.FromHex(0x5A5245),
            Rgb24.FromHex(0x7A6B52),
            Rgb24.FromHex(0x4F5A3C),
            Rgb24.FromHex(0x6B7A4A),
            Rgb24.FromHex(0x7A4A3C),
            Rgb24.FromHex(0x8A5A42),
            Rgb24.FromHex(0x5A4358),
            Rgb24.FromHex(0x3F5F5A),
            Rgb24.FromHex(0x8A7A3C),
            Rgb24.FromHex(0x9A5240),
        };

        /// <summary>
        /// How much darker the second garment is than the first, as a percentage of value. Never
        /// above 100: a lighter second garment reads as a different outfit rather than as the rest
        /// of the same one.
        /// </summary>
        public static readonly int[] SecondShades = { 55, 65, 78 };
    }
}
