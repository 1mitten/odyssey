#nullable enable
using Odyssey.Hud;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// Where the fight's marks stand over a pawn and what ink they are in (design 33 §1): the
    /// health bar over the hurt and the drafted, and the hostile marker. <b>Whether</b> a bar is
    /// owed and how full it is are <see cref="CombatFeedbackModel"/>'s answers, and the bar's
    /// pieces are <see cref="HealthBarLayout"/>'s; this puts each piece in the world, kept apart
    /// so a test can hold it without a renderer.
    ///
    /// <para>Every ink is a Hud token, never a colour written here: the bar's fill is
    /// <see cref="CombatFeedbackModel.HealthBarColour"/>, its plate and outline
    /// <see cref="CombatFeedbackModel.HealthBarPlate"/> and
    /// <see cref="CombatFeedbackModel.HealthBarOutline"/>, and the hostile marker is
    /// <see cref="HudTheme.Bad"/> — brighter than the draft's deep red, so an enemy and one of ours
    /// cannot be confused at a glance.</para>
    ///
    /// <para><b>The bar faces the camera and its pieces never overlap</b> (design 33 §8a). It was a
    /// translucent fill box inside a translucent track box, and which covered the other was the
    /// transparent sort's call — a tie on every frame of a full bar, which is what flickered.
    /// Coplanar pieces that do not cover each other look the same in any order.</para>
    /// </summary>
    public static class CombatMarks
    {
        /// <summary>How far above the top of a pawn's cursor box the bar's centre sits, in metres: under the draft diamond.</summary>
        public const float BarLift = 0.02f;

        /// <summary>How high a downed pawn's marks stand above its feet: it is lying down, not standing.</summary>
        public const float DownedTop = 0.8f;

        /// <summary>
        /// How deep each piece's box is, towards the camera: thin enough to read as a flat plate,
        /// so the side faces of two neighbouring pieces are edge-on and never show as a seam.
        /// </summary>
        public const float PieceDepth = 0.004f;

        /// <summary>The share of the pool left, 0 to 1: <see cref="HealthBarLayout.Fraction"/>.</summary>
        public static float Fraction(int hpMilli, int hpMaxMilli) => HealthBarLayout.Fraction(hpMilli, hpMaxMilli);

        /// <summary>The hostile marker's ink (design 33 §1: "a red marker").</summary>
        public static HudColour HostileInk => HudTheme.Bad;

        /// <summary>
        /// One piece of a bar centred on <paramref name="centre"/>, in the plane
        /// <paramref name="facing"/> turns to — the camera's own rotation, so the bar is level and
        /// square on the screen: the unit cube scaled to the piece and set at its place.
        /// </summary>
        public static Matrix4x4 Place(in HealthBarPiece piece, Vector3 centre, Quaternion facing)
        {
            Vector3 at = centre + facing * new Vector3(piece.CentreU, piece.CentreV, 0f);
            return Matrix4x4.TRS(at, facing, new Vector3(piece.Width, piece.Height, PieceDepth));
        }
    }
}
