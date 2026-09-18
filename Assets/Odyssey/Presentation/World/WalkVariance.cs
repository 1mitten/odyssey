#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// What makes one colonist walk unlike another: build, stride and the small bow a walker puts
    /// into a straight line.
    ///
    /// <para><b>Computed from the pawn's id, drawn, and nowhere near the simulation.</b> Nothing
    /// here is in a cell, a save or the hash — the standing rule, and the same bargain
    /// <see cref="SwimPose"/> and <see cref="WorkStroke"/> already take. A colonist is in its cell
    /// throughout, exactly as a working figure is while <c>WorkStance.StandAt</c> steps it 0.8 m
    /// off that cell to swing an axe.</para>
    ///
    /// <para><b>Why anything was needed.</b> Before this, every one of the sixty-one colonists was
    /// drawn at exactly <c>scale 1.4</c> and the only per-pawn variation anywhere in presentation
    /// was the gait clip's start phase and the work stroke's period. Five colonists walking the
    /// same errand were five copies of one walk, in single file, down one line.</para>
    ///
    /// <para><b>Streams, not one number taken three ways.</b> Each dial gets its own mixing
    /// constant, for the reason <see cref="Rendering.ColonistAppearance"/> gives: a shortcut that
    /// reuses one hash correlates the slots — everyone who is tall also bows the same way — and it
    /// is invisible until fifty colonists are on screen at once.</para>
    /// </summary>
    public static class WalkVariance
    {
        /// <summary>
        /// How much a colonist's build may differ from the drawn standard, either way.
        ///
        /// <para>0.03 is about five centimetres on a 1.8 m figure at the drawn scale — enough that
        /// a group reads as different people, not enough that anyone stood beside anyone else
        /// looks like a different species. The owner's number, 2026-09-18, chosen over a more
        /// obvious 0.06 which starts to read as adults and teenagers.</para>
        ///
        /// <para>Set to zero and every colonist is the same size again, which is what it was
        /// before this existed.</para>
        /// </summary>
        public static float Build { get; set; } = 0.03f;

        /// <summary>
        /// How far off the straight line between two cell centres a walker may drift, in metres.
        ///
        /// <para><b>Capped well inside the cell, and that is what makes it safe without asking the
        /// world anything.</b> A cell is 2.5 m, so its centre line is 1.25 m from the wall of the
        /// next cell along; a colonist is about half a metre across. At 0.25 m the figure plus its
        /// own width stays half a metre clear of anything solid, whatever is beside it — so the
        /// bow needs no walkability query, no fade near walls and no special case in a corridor.
        /// On a diagonal it is safer still: the corner rule
        /// (<c>NavGrid.DiagonalAllowed</c>) will not permit the step unless <em>both</em> flanking
        /// cells are open, so all four cells around the chord are clear.</para>
        ///
        /// <para>Raising this past about 0.6 m would end that argument and want the query.</para>
        /// </summary>
        public static float Bow { get; set; } = 0.25f;

        /// <summary>How many bows a colonist puts into one cell of travel. Under one, so the drift
        /// reads as a wander rather than a weave.</summary>
        public static float BowsPerCell { get; set; } = 0.35f;

        public static void Reset()
        {
            Build = 0.03f;
            Bow = 0.25f;
            BowsPerCell = 0.35f;
        }

        // Stream constants. Arbitrary odd numbers, different from each other and from the four
        // ColonistAppearance already uses, so build does not correlate with colour either.
        const uint BuildStream = 0x7F4A7C15u;
        const uint BowStream = 0x165667B1u;
        const uint LookStream = 0xD3A2646Cu;

        /// <summary>
        /// How large this colonist is drawn, as a multiple of the catalogue's own scale.
        ///
        /// <para><b>A taller figure takes a longer stride</b>, because the gait clip is played on a
        /// scaled skeleton — which is the whole reason this is the right dial rather than playing
        /// the clip faster. <c>PawnFigureDirector.Blend</c> divides measured ground speed by this
        /// before choosing gait weights, or the feet skate: the look's cached gait speeds were
        /// computed at the catalogue scale, and a figure 3% larger covers 3% more ground per
        /// cycle. See <c>GroundSpeeds</c>, which makes the same correction for the 1.4 the whole
        /// cast is drawn at.</para>
        /// </summary>
        public static float StrideScale(int pawnId) =>
            1f + Build * Signed(Mix(pawnId, BuildStream));

        /// <summary>
        /// How far this colonist drifts off the straight line, and where in its wander it is.
        ///
        /// <para><paramref name="travelled"/> is distance covered along the journey in metres, not
        /// time — <b>a standing colonist must not drift</b>, and a clock would move it while it
        /// stood still working. It is continuous across a step boundary by construction, because
        /// the phase is a function of how far the pawn has come and not of which cell it is in.
        /// </para>
        ///
        /// <para>Two sine terms at incommensurable rates rather than one, so the path does not
        /// read as a regular weave; the amplitudes sum to one so the cap is exact.</para>
        /// </summary>
        public static float BowOffset(int pawnId, float travelled)
        {
            float amplitude = Bow * (0.45f + 0.55f * Unit(Mix(pawnId, BowStream)));
            float phase = Unit(Mix(pawnId, BowStream ^ 0x9E3779B9u)) * Mathf.PI * 2f;
            float w = BowsPerCell * Mathf.PI * 2f / CellMetres;

            return amplitude * (0.62f * Mathf.Sin(travelled * w + phase)
                                + 0.38f * Mathf.Sin(travelled * w * 0.41f + phase * 1.7f));
        }

        /// <summary>The cell size this reasons in. Presentation's own copy of a fixed decision
        /// (ADR 0002), kept here so the arithmetic is testable with no world to hand.</summary>
        public const float CellMetres = 2.5f;

        /// <summary>A per-colonist offset into the head-look wander, so no two look about together.</summary>
        public static float LookPhase(int pawnId) => Unit(Mix(pawnId, LookStream));

        /// <summary>A per-colonist rate multiplier for the head-look, around one.</summary>
        public static float LookRate(int pawnId) =>
            1f + 0.35f * Signed(Mix(pawnId, LookStream ^ 0x85EBCA6Bu));

        /// <summary>The avalanche <see cref="Rendering.ColonistAppearance"/> uses, on id alone.
        ///
        /// <para><b>The world seed is deliberately not mixed in.</b> Appearance takes it so that a
        /// world deals the same faces every load; how a person walks is not something a save has an
        /// opinion about, and leaving the seed out keeps this callable from
        /// <see cref="Rendering.PawnPose"/>, which has no seed and must not grow a parameter for
        /// one.</para>
        /// </summary>
        static uint Mix(int pawnId, uint stream)
        {
            unchecked
            {
                uint h = stream;
                h ^= (uint)pawnId * 2654435761u;
                h ^= h >> 16;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>0 to 1.</summary>
        static float Unit(uint h) => (h >> 8) * (1f / 16777216f);

        /// <summary>-1 to 1.</summary>
        static float Signed(uint h) => Unit(h) * 2f - 1f;
    }
}
