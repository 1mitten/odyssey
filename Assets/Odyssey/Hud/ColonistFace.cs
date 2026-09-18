#nullable enable
using System;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The recipe for one colonist's flat avatar: four colours and two shapes
    /// (<c>docs/design/20-avatars.md</c>).
    ///
    /// <para><b>The colours are not drawn again here.</b> They come straight out of
    /// <see cref="ColonistAppearance.Of"/> — the same call the figure in the world is painted from,
    /// not a second derivation that agrees with it. That is <c>NavGraph.HopCost</c>'s rule, and the
    /// failure it forecloses is the worst kind on this screen: a card promising a person in a red
    /// coat and a colony delivering one in green, with both halves individually correct. It is also
    /// the whole reason the appearance moved into this assembly on 2026-09-18.</para>
    ///
    /// <para><b>What is drawn here is the part a 3D mesh already answers and a drawing does not:</b>
    /// which hair shape, and how broad the shoulders are. Colour alone is 7 skins x 9 hairs x 14
    /// garments, which is plenty of variety and no *recognisability* — at 26 px two colonists in the
    /// same three colours are the same person. Eight crowns and three builds make twenty-four
    /// silhouettes underneath that.</para>
    ///
    /// <para><b>There is no face, and that is a limit rather than a stage.</b> No eyes, no mouth,
    /// no nose. This project has already measured that its own pixel art loses its grooves at 17 px
    /// and goes to noise at 16 (<c>Logs/skill-icons.png</c>), and a roster card's avatar is 26.
    /// Two dots and a line at that size read as damage, not as a person. The avatar is a silhouette
    /// portrait — which is also what lets one drawing serve 26, 30 and 64 px unaltered.</para>
    ///
    /// <para><b>Derived, not stored</b>, exactly like <see cref="ColonistIdentity"/>: a pure
    /// function of the pawn's <c>RollSeed</c> and its id, so nothing here is saved, nothing is in
    /// the state hash, and no golden moves. Unity-free by construction (ADR 0003), so every rule
    /// about it is a fast-tier test.</para>
    /// </summary>
    public readonly struct ColonistFace : IEquatable<ColonistFace>
    {
        /// <summary>
        /// How many crowns there are to choose from. Eight, matching the given-name pool — not
        /// because the two are related, but because eight is what the drawing can keep distinct at
        /// 26 px without any of them being a near-copy of another.
        /// </summary>
        public const int HairShapes = 8;

        /// <summary>
        /// How many shoulder widths. Three: narrow, ordinary, broad. A fourth is not
        /// distinguishable at the card size, which was the test for whether to have it.
        /// </summary>
        public const int Builds = 3;

        /// <summary>The tile behind the figure: this colonist's own garment colour (owner's
        /// decision 5). Fifty cards are then fifty fills rather than one category hue fifty
        /// times.</summary>
        public readonly Rgb24 Tile;

        /// <summary>The shoulders, in the garment's shadow — <see cref="ColonistAppearance.Cloth2"/>,
        /// which the appearance guarantees is always darker than <see cref="Tile"/>. That
        /// guarantee is what keeps the figure legible against its own background without this file
        /// choosing a contrast rule of its own.</summary>
        public readonly Rgb24 Shoulders;

        /// <summary>Head and neck.</summary>
        public readonly Rgb24 Skin;

        /// <summary>The crown.</summary>
        public readonly Rgb24 Hair;

        /// <summary>Which crown, 0 to <see cref="HairShapes"/> - 1.</summary>
        public readonly int HairShape;

        /// <summary>How broad, 0 to <see cref="Builds"/> - 1.</summary>
        public readonly int Build;

        public ColonistFace(Rgb24 tile, Rgb24 shoulders, Rgb24 skin, Rgb24 hair,
                            int hairShape, int build)
        {
            Tile = tile;
            Shoulders = shoulders;
            Skin = skin;
            Hair = hair;
            HairShape = hairShape;
            Build = build;
        }

        /// <summary>
        /// Distinct salts per attribute, the same reason <see cref="ColonistIdentity"/> gives:
        /// two facts about one person drawn from one stream correlate, and the correlation is
        /// invisible until a screen full of colonists all have the same haircut on the same
        /// shoulders.
        /// </summary>
        const uint HairShapeSalt = 0x5CA1Fu;
        const uint BuildSalt = 0xB0DEu;

        /// <summary>
        /// The face this colonist wears.
        ///
        /// <para><b>The look count handed to the appearance is one, deliberately.</b>
        /// <see cref="ColonistAppearance.Look"/> is an index into the catalogue's 3D colonist
        /// meshes and means nothing to a drawing, so this asks for a single-body catalogue and
        /// throws the index away. The colours are unaffected: the garment, hair and skin streams do
        /// not consult the body table's length, which is not an assumption but the subject of
        /// <c>ChangingOnePaletteDoesNotRepaintTheOthers</c>.</para>
        /// </summary>
        public static ColonistFace Of(uint rollSeed, PawnId id)
        {
            ColonistAppearance colours = ColonistAppearance.Of(rollSeed, id.Value, 1);

            return new ColonistFace(
                colours.Cloth, colours.Cloth2, colours.Skin, colours.Hair,
                (int)(Mix(rollSeed, id, HairShapeSalt) % HairShapes),
                (int)(Mix(rollSeed, id, BuildSalt) % Builds));
        }

        /// <summary>The face of a colonist in the published frame — the ordinary way to ask.</summary>
        public static ColonistFace Of(WorldSnapshot snapshot, PawnId id) =>
            Of(ColonistNames.RollSeedOf(snapshot, id), id);

        /// <summary>
        /// FNV-1a then the project's avalanche, character for character what
        /// <see cref="ColonistIdentity"/> uses. Copied rather than shared because the two are
        /// separate streams over the same inputs and sharing the function would be the first step
        /// towards sharing a salt; the shape is the convention, not the code.
        /// </summary>
        static uint Mix(uint seed, PawnId id, uint salt)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ seed) * 16777619u;
                h = (h ^ (uint)id.Value) * 16777619u;
                h = (h ^ salt) * 16777619u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }

        public bool Equals(ColonistFace other) =>
            Tile.Equals(other.Tile) && Shoulders.Equals(other.Shoulders) &&
            Skin.Equals(other.Skin) && Hair.Equals(other.Hair) &&
            HairShape == other.HairShape && Build == other.Build;

        public override bool Equals(object? obj) => obj is ColonistFace other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = (int)Tile.Packed;
                h = (h * 397) ^ (int)Shoulders.Packed;
                h = (h * 397) ^ (int)Skin.Packed;
                h = (h * 397) ^ (int)Hair.Packed;
                h = (h * 397) ^ HairShape;
                return (h * 397) ^ Build;
            }
        }

        public override string ToString() =>
            $"face(hair {HairShape}, build {Build}, {Skin}/{Hair}/{Tile})";
    }
}
