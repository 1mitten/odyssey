#nullable enable

using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The shape of the land — drawn, never simulated.
    ///
    /// The board is dead flat: the wooded board sets the generator's relief to zero, and a ground
    /// cell is one instanced 2.5 x 3.0 x 2.5 box placed by a bare translate, so every top face is
    /// a perfectly flat quad at exactly the layer height. That reads as a carpet of blocks rather
    /// than as ground. This class is the facade that fixes it, in exactly the sense the grass tufts
    /// and the axe chips are facades: it is drawn, and it is in no cell, no save and no hash.
    ///
    /// **The governing rule is that relief is a drawing offset and never a position.** Nothing in
    /// <c>Odyssey.Sim</c> learns about it, <see cref="CellMetrics.FloorCentre"/> is untouched, and
    /// every caller applies it explicitly at the point of drawing. It is the rule <c>WorkStance</c>
    /// already follows: only the drawn figure steps in; the pawn stays in its cell for the
    /// simulation. The one place that must follow the drawn ground rather than the flat grid is
    /// picking, because a click has to land on what the player can see — see <c>SlicePicker</c>.
    ///
    /// Like <see cref="GroundScatter"/>, and for the same reason, everything here is a pure
    /// function of position: a chunk is re-meshed whenever anything in it changes, so a field that
    /// depended on anything else would make the land crawl about whenever a wall went up.
    ///
    /// **Why sinusoids rather than the generator's value noise.** <c>ValueNoise</c> is integer by
    /// construction — it exists so that worldgen contains no floats — so it can only be sampled at
    /// whole lattice coordinates and offers no derivative. Sampling it on a sub-cell lattice gives
    /// a staircase, not a surface: every point within one lattice square shares a height, which a
    /// pawn gliding between cells would show up as a judder. A small sum of sine waves is smooth
    /// everywhere, gives an exact analytic gradient instead of a finite difference, costs a handful
    /// of transcendentals, and can be checked by arithmetic rather than by screenshot. The no-float
    /// rule exists to protect the state hash, and none of this is in the state hash.
    /// </summary>
    public static class GroundRelief
    {
        /// <summary>
        /// How far the drawn ground may rise or fall from its layer, in metres.
        ///
        /// Zero is the flat board exactly as it was, and is the default so that nothing changes
        /// until a composition root asks for it.
        /// </summary>
        public static float Amplitude { get; set; }

        /// <summary>
        /// The board's amplitude, in metres.
        ///
        /// Sized to be *seen*, which is a stronger constraint than it sounds. The sun sits at 72
        /// degrees and the scene carries a strong trilight ambient, so tilting a surface normal by
        /// a degree or two moves the lit value by well under one per cent — invisible. A 0.35 m
        /// roll over this period slopes by 1.7 degrees and would have shipped as no visible change
        /// at all. Two metres slopes by about 8 degrees, which reads as shading and as silhouette,
        /// and stays well under the 3 m layer so relief can never be mistaken for a step up.
        /// </summary>
        public const float BoardAmplitude = 2.0f;

        /// <summary>
        /// The wavelength of the longest wave in the field, in metres.
        ///
        /// Long enough that the board carries two or three swells across its 300 m rather than a
        /// corrugation, short enough that there is more than one of them to see. The cost of
        /// shortening it is at the seams: neighbouring cells are drawn as tangent planes of this
        /// field, so they part company at their shared corners by about
        /// <c>(A/2) * (2*pi*cell/P)^2</c> — about 14 mm at these values, which is a pixel at the
        /// closest the camera can get.
        /// </summary>
        public static float Period { get; set; } = 150f;

        /// <summary>Reset to the shipped defaults. For tests, which must not inherit each other's tuning.</summary>
        public static void Reset()
        {
            Amplitude = 0f;
            Period = 150f;
            HillAmplitude = 50f;
            HillPeriod = 1000f;
            HillRampMetres = 700f;
        }

        /// <summary>
        /// The waves that make up the land: relative wavelength, relative amplitude, direction and
        /// phase.
        ///
        /// Fixed rather than seeded, and deliberately so. The relief is not simulation state, so it
        /// must not vary with the map — two boards generated from different seeds are meant to be
        /// drawn by the same land, and a facade that changed shape with the save would be state
        /// pretending not to be.
        ///
        /// The directions are deliberately not axis-aligned and not at right angles to one another,
        /// because waves that share an axis produce a plaid the eye picks out instantly as a
        /// pattern rather than as country. The wavelengths are in an irrational-ish ratio for the
        /// same reason: it stops the sum repeating on any short distance.
        /// </summary>
        static readonly Wave[] Waves =
        {
            //         wavelength  amplitude   bearing (deg)  phase
            new Wave(1.000f, 0.560f, 23f, 0.00f),
            new Wave(0.618f, 0.270f, 107f, 1.31f),
            new Wave(0.336f, 0.120f, 61f, 2.72f),
            new Wave(0.187f, 0.050f, 154f, 4.05f),
        };

        readonly struct Wave
        {
            public Wave(float wavelength, float amplitude, float bearingDegrees, float phase)
            {
                Amplitude = amplitude;
                Phase = phase;
                float r = bearingDegrees * Mathf.Deg2Rad;
                // The direction the wave travels in, scaled by its wavenumber at unit period.
                DirX = Mathf.Cos(r) / wavelength;
                DirZ = Mathf.Sin(r) / wavelength;
            }

            public readonly float Amplitude;
            public readonly float Phase;
            public readonly float DirX;
            public readonly float DirZ;
        }

        /// <summary>
        /// The height of the drawn ground above its layer at a point, in metres.
        ///
        /// A function of x and z only, and that is a requirement rather than an accident: the same
        /// column must be displaced by the same amount at every layer, or the surface cell would
        /// slide off the strata beneath it and the player would see a gap between them on slicing
        /// down.
        /// </summary>
        public static float HeightAt(float worldX, float worldZ) =>
            HeightAt(worldX, worldZ, Amplitude);

        /// <summary>
        /// The same field at an explicit amplitude, which is how the surround grows into hills
        /// without becoming a second field with its own shape.
        /// </summary>
        public static float HeightAt(float worldX, float worldZ, float amplitude) =>
            FieldAt(worldX, worldZ, amplitude, Period);

        /// <summary>
        /// The field itself, at an explicit amplitude and wavelength.
        ///
        /// Taking the wavelength as an argument is what lets the surround carry hills without
        /// becoming a different shape: the same four waves at a long wavelength and a large
        /// amplitude are hills, and at a short one and a small amplitude are the roll across the
        /// board. A single global wavelength could not do both - raising the amplitude to hill
        /// height at the board's own wavelength gives slopes of sixty degrees and spikes, not
        /// country.
        /// </summary>
        public static float FieldAt(float worldX, float worldZ, float amplitude, float period)
        {
            if (amplitude == 0f) return 0f;

            float k = 2f * Mathf.PI / Mathf.Max(1f, period);
            float sum = 0f;
            for (int i = 0; i < Waves.Length; i++)
            {
                Wave w = Waves[i];
                sum += w.Amplitude * Mathf.Sin(k * (w.DirX * worldX + w.DirZ * worldZ) + w.Phase);
            }

            return sum * amplitude;
        }

        /// <summary>
        /// The slope of the field at a point, as a rise per metre along each axis.
        ///
        /// Differentiated rather than sampled. A central difference would have been quantised by
        /// whatever step it used and would disagree, very slightly, with the height it was meant to
        /// be the slope of; the derivative of a sum of sines is another sum of sines and is exact.
        /// </summary>
        public static void SlopeAt(float worldX, float worldZ, out float slopeX, out float slopeZ) =>
            SlopeAt(worldX, worldZ, Amplitude, out slopeX, out slopeZ);

        /// <summary>The slope of the field at an explicit amplitude.</summary>
        public static void SlopeAt(float worldX, float worldZ, float amplitude,
            out float slopeX, out float slopeZ) =>
            FieldSlopeAt(worldX, worldZ, amplitude, Period, out slopeX, out slopeZ);

        /// <summary>The exact gradient of <see cref="FieldAt"/>.</summary>
        public static void FieldSlopeAt(float worldX, float worldZ, float amplitude, float period,
            out float slopeX, out float slopeZ)
        {
            slopeX = 0f;
            slopeZ = 0f;
            if (amplitude == 0f) return;

            float k = 2f * Mathf.PI / Mathf.Max(1f, period);
            for (int i = 0; i < Waves.Length; i++)
            {
                Wave w = Waves[i];
                float d = w.Amplitude * k *
                          Mathf.Cos(k * (w.DirX * worldX + w.DirZ * worldZ) + w.Phase);
                slopeX += d * w.DirX;
                slopeZ += d * w.DirZ;
            }

            slopeX *= amplitude;
            slopeZ *= amplitude;
        }

        /// <summary>
        /// The steepest the field can ever be at a given amplitude, as a rise per metre.
        ///
        /// Every wave's contribution bounded and summed, which is what the culling bounds need: a
        /// sheared cell reaches half a cell times this above its own centre, and a box that does
        /// not allow for it gets culled while it is still on screen.
        /// </summary>
        public static float MaxSlope(float amplitude) => MaxSlope(amplitude, Period);

        /// <summary>The steepest the field can be at an explicit amplitude and wavelength.</summary>
        public static float MaxSlope(float amplitude, float period)
        {
            float k = 2f * Mathf.PI / Mathf.Max(1f, period);
            float sum = 0f;
            for (int i = 0; i < Waves.Length; i++)
            {
                Wave w = Waves[i];
                sum += w.Amplitude * k * Mathf.Sqrt(w.DirX * w.DirX + w.DirZ * w.DirZ);
            }

            return sum * Mathf.Abs(amplitude);
        }

        // ---------------------------------------------------------------- the surround

        /// <summary>
        /// The wavelength of the hills outside the board, in metres.
        ///
        /// Long, because amplitude and wavelength together are what decide a slope, and hills are
        /// tall. Thirty-five metres of rise over the board's own 150 m wavelength would stand at
        /// sixty degrees; over 700 m it stands at seventeen, which is a hillside.
        /// </summary>
        public static float HillPeriod { get; set; } = 1000f;

        /// <summary>How tall the hills are allowed to get, in metres, once far enough out.</summary>
        public static float HillAmplitude { get; set; } = 50f;

        /// <summary>
        /// How far out the hills reach their full height, in metres beyond the board's rim.
        ///
        /// Early, and then held. The temptation is to grow the land all the way to the far edge of
        /// the surround, but fog is opaque by 1,100 m from the camera and the board's own rim is
        /// already 150 m or so away, so anything past about 900 m is drawn in exactly the colour of
        /// the sky. Worse, at the camera's default 48-degree pitch the horizon is not in frame at
        /// all; the far land only appears below about 25 degrees. So the height is spent where it
        /// can be seen and not beyond it.
        /// </summary>
        public static float HillRampMetres { get; set; } = 700f;

        /// <summary>
        /// The hills' amplitude at a distance outside the board.
        ///
        /// Zero at the rim, and that is the load-bearing part: the surround's first ring is drawn
        /// from the same cells the board's own rim is, so if the hills did not start at nothing
        /// there would be a step at the join - which is the exact tell the whole surround exists
        /// to remove. Smoothstepped rather than linear so there is no crease where they begin.
        /// </summary>
        public static float HillAmplitudeAt(float metresOutsideBoard)
        {
            if (metresOutsideBoard <= 0f) return 0f;
            float t = Mathf.Clamp01(metresOutsideBoard / Mathf.Max(1f, HillRampMetres));
            return HillAmplitude * (t * t * (3f - 2f * t));
        }

        /// <summary>
        /// The height of the drawn ground outside the board: the same roll the board has, with the
        /// hills laid over it.
        ///
        /// Both layers are continuous everywhere, so the surround cannot disagree with the board at
        /// the seam - at the rim the hill term is zero and this is exactly <see cref="HeightAt"/>.
        /// </summary>
        public static float SurroundHeightAt(float worldX, float worldZ, float metresOutsideBoard) =>
            HeightAt(worldX, worldZ) +
            FieldAt(worldX, worldZ, HillAmplitudeAt(metresOutsideBoard), HillPeriod);

        /// <summary>The gradient of <see cref="SurroundHeightAt"/>, both layers summed.</summary>
        public static void SurroundSlopeAt(float worldX, float worldZ, float metresOutsideBoard,
            out float slopeX, out float slopeZ)
        {
            SlopeAt(worldX, worldZ, Amplitude, out slopeX, out slopeZ);
            FieldSlopeAt(worldX, worldZ, HillAmplitudeAt(metresOutsideBoard), HillPeriod,
                out float hillX, out float hillZ);
            slopeX += hillX;
            slopeZ += hillZ;
        }

        /// <summary>The steepest the surround can be at a distance, for sizing its boxes and bounds.</summary>
        public static float SurroundMaxSlope(float metresOutsideBoard) =>
            MaxSlope(Amplitude, Period) +
            MaxSlope(HillAmplitudeAt(metresOutsideBoard), HillPeriod);

        /// <summary>
        /// The placement for a piece of surround: draped on the board's roll and its hills together.
        /// </summary>
        public static Matrix4x4 DrapeSurround(Vector3 centre, float metresOutsideBoard)
        {
            float height = SurroundHeightAt(centre.x, centre.z, metresOutsideBoard);
            SurroundSlopeAt(centre.x, centre.z, metresOutsideBoard,
                out float slopeX, out float slopeZ);

            var m = Matrix4x4.identity;
            m.m10 = slopeX;
            m.m12 = slopeZ;
            m.m03 = centre.x;
            m.m13 = centre.y + height;
            m.m23 = centre.z;
            return m;
        }

        /// <summary>A point lifted onto the surround's ground.</summary>
        public static Vector3 LiftSurround(Vector3 at, float metresOutsideBoard) =>
            new Vector3(at.x, at.y + SurroundHeightAt(at.x, at.z, metresOutsideBoard), at.z);

        // ---------------------------------------------------------------- the board

        /// <summary>
        /// The placement for a piece of ground: lifted onto the field and tilted to lie along it.
        ///
        /// **This is a shear, not a rotation**, and that is what makes the whole approach cost
        /// nothing. The matrix carries the tangent plane of the field at <paramref name="centre"/>,
        /// so it fits in the instance matrix a ground cell already had: no extra instance, no extra
        /// draw call, no new mesh and no shader change.
        ///
        /// Three things fall out of choosing a shear over anything cleverer. Vertical edges stay
        /// vertical, because a shear in Y by x and z leaves a vertical edge's offset constant — so
        /// each cell stays a full-height prism and neighbours cannot open a gap between them. The
        /// determinant is one, so winding is preserved and no face flips to a backface. And the top
        /// face's normal tilts with the slope under the ordinary inverse-transpose, so the lighting
        /// that makes the relief visible is free.
        ///
        /// Two neighbours are tangent planes of one smooth field, so they part company at their
        /// shared corners only to second order — see <see cref="Period"/> for the millimetres, and
        /// note that the gap is filled anyway by the lower neighbour's own side face.
        /// </summary>
        public static Matrix4x4 Drape(Vector3 centre) => Drape(centre, Amplitude);

        /// <summary>The drape at an explicit amplitude, for the surround.</summary>
        public static Matrix4x4 Drape(Vector3 centre, float amplitude)
        {
            if (amplitude == 0f) return Matrix4x4.Translate(centre);

            float height = HeightAt(centre.x, centre.z, amplitude);
            SlopeAt(centre.x, centre.z, amplitude, out float slopeX, out float slopeZ);

            // Two things here are easy to get backwards and neither announces itself.
            //
            // The row: Unity's Matrix4x4 is mRowColumn, so a shear that moves Y according to x and
            // z sets m10 and m12 — the Y row. Setting m01 and m21 is the transpose of what is
            // wanted, and leans the cubes sideways while leaving their tops horizontal, which
            // reads as a rotation bug rather than as ground.
            //
            // The space: this is an object-to-world matrix, so it is handed mesh coordinates that
            // are already relative to the cell's centre. The shear therefore acts on local x and z,
            // and the translation is simply where the cell goes. Subtracting the centre out of the
            // Y row as well — as one would for a shear written in world coordinates — takes it off
            // twice, and neighbouring cells then disagree by whole metres rather than millimetres.
            var m = Matrix4x4.identity;
            m.m10 = slopeX;
            m.m12 = slopeZ;
            m.m03 = centre.x;
            m.m13 = centre.y + height;
            m.m23 = centre.z;
            return m;
        }

        /// <summary>
        /// A point lifted onto the drawn ground.
        ///
        /// What a colonist, a tuft, a log pile or a selection cursor gets. They are lifted and never
        /// sheared: ground lies along the slope, but a person standing on a hillside stands up.
        /// </summary>
        public static Vector3 Lift(Vector3 at) => Lift(at, Amplitude);

        /// <summary>A point lifted at an explicit amplitude.</summary>
        public static Vector3 Lift(Vector3 at, float amplitude)
        {
            if (amplitude == 0f) return at;
            return new Vector3(at.x, at.y + HeightAt(at.x, at.z, amplitude), at.z);
        }
    }
}
