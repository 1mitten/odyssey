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

        public ColonistAppearance(int look, Rgb24 skin, Rgb24 hair, Rgb24 cloth, Rgb24 cloth2)
            : this(look, skin, hair, cloth, cloth2, NoPiece, NoPiece)
        {
        }

        public ColonistAppearance(int look, Rgb24 skin, Rgb24 hair, Rgb24 cloth, Rgb24 cloth2,
            int hairPiece, int beardPiece)
        {
            Look = look;
            Skin = skin;
            Hair = hair;
            Cloth = cloth;
            Cloth2 = cloth2;
            HairPiece = hairPiece;
            BeardPiece = beardPiece;
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

            hair = hair.MixedWith(Grey, GreyingAt(age));

            return new ColonistAppearance(look, skin, hair, cloth, cloth.Scaled(shade),
                HairPieceFor(seed, pawnId, pools, gender, age),
                BeardPieceFor(seed, pawnId, pools, gender));
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
            HairPiece == other.HairPiece && BeardPiece == other.BeardPiece;

        public override bool Equals(object? obj) => obj is ColonistAppearance other && Equals(other);

        public override int GetHashCode() =>
            unchecked(((((((Look * 397) ^ (int)Skin.Packed) * 397 ^ (int)Hair.Packed) * 397 ^
                         (int)Cloth.Packed) * 397 ^ HairPiece) * 397) ^ BeardPiece);

        public override string ToString() =>
            "look " + Look + ", skin " + Skin + ", hair " + Hair + ", cloth " + Cloth + "/" + Cloth2 +
            ", hairPiece " + HairPiece + ", beard " + BeardPiece;
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
