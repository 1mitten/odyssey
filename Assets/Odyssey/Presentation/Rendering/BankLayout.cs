#nullable enable

using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Whether a bank stands in a cell, which of the three shapes it is, and how high its surface
    /// is at a point — the whole decision, in one place, for everything that needs to know.
    ///
    /// <para><b>Why it left the mesher.</b> It was private to <see cref="ChunkMesher"/> while the
    /// only question anyone asked was "what do I draw here". A bank fills the cell it stands in
    /// from the floor to the rim, though, and that cell is walkable — it is the cell at the foot of
    /// a terrace, which is the take-off cell for the hop the bank is a picture of. So a colonist
    /// standing there was drawn waist-deep in the ramp, and the fix is for the figure to stand on
    /// the same surface the mesher draws. Two copies of this arithmetic would look identical until
    /// the day they disagreed, and the symptom — a figure sunk into a slope, or hovering over
    /// one — would be blamed on the animation.</para>
    ///
    /// <para><b>It is still a facade.</b> Nothing in <c>Odyssey.Sim</c> knows a bank exists: not
    /// pathable, not selectable, not in the save and not in the state hash, exactly as
    /// <see cref="GroundRelief"/> and <see cref="GroundScatter"/> are not. The hop a bank draws is
    /// real (<c>MoveCost.JumpUp</c>); the slope is only the picture of it, and lifting a figure
    /// onto that picture is a drawing offset like every other one here.</para>
    /// </summary>
    public static class BankLayout
    {
        /// <summary>
        /// Draw banks up terrace steps at all. On, and off is exactly the board as it was before
        /// there were any.
        ///
        /// <para><b>Static, like <see cref="GroundRelief.Amplitude"/> and
        /// <see cref="GroundMesh.MaxRipple"/>, and for the same reason.</b> It used to be a
        /// property of the mesher, which was right while meshing was the only thing that cared.
        /// A figure now stands on the surface the mesher draws, so a per-renderer lever would let
        /// the two disagree — banks off and colonists still hovering 1.5 m over the meadow — and
        /// that class of fault is exactly what pulling the decision out of the mesher was meant to
        /// stop. A harness sets these around a run and puts them back in a <c>finally</c>, as
        /// <c>SlopeCheck</c> already does for the mesh levers.</para>
        ///
        /// <para>Meshing bakes the answer into a bucket, so moving either lever only takes effect
        /// on chunks meshed afterwards.</para>
        /// </summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// Let banks grow inside a working, as they did before the cut-face rule. Off, and it
        /// exists so the check harness can photograph the fault rather than describe it.
        /// </summary>
        public static bool InWorkings { get; set; }

        /// <summary>
        /// Stand a drawn figure on a bank's surface rather than at its cell's floor. On.
        ///
        /// <para>A lever of its own rather than a corner of <see cref="Enabled"/>, because the
        /// question it answers is a different one: with banks off there is nothing to stand on and
        /// the comparison is meaningless, so the sheet that matters photographs the same slope
        /// with the figure on it and in it. <c>BankCheck</c> is the only caller that moves it.</para>
        /// </summary>
        public static bool LiftFigures { get; set; } = true;

        /// <summary>Back to the shipped defaults. For tests, which must not inherit each other's tuning.</summary>
        public static void Reset()
        {
            Enabled = true;
            InWorkings = false;
            LiftFigures = true;
        }

        /// <summary>A bank, or the absence of one. <see cref="Exists"/> first; the rest is junk without it.</summary>
        public readonly struct Bank
        {
            public Bank(BankMesh.Kind kind, int rotation, ushort terrain)
            {
                Exists = true;
                Kind = kind;
                Rotation = rotation;
                Terrain = terrain;
            }

            /// <summary>Is there a bank in this cell at all?</summary>
            public readonly bool Exists;

            /// <summary>Which of the three shapes.</summary>
            public readonly BankMesh.Kind Kind;

            /// <summary>Which of the four bearings the shape is turned onto. See <see cref="Directions.Yaw"/>.</summary>
            public readonly int Rotation;

            /// <summary>What it is made of: the terrain at the top of the step it climbs.</summary>
            public readonly ushort Terrain;
        }

        /// <summary>The bank in this cell, if there is one.</summary>
        public static Bank At(WorldRenderModel model, int x, int z, int y)
        {
            if (!CanBank(model, x, z, y)) return default;

            // **Which of the three shapes this cell wants, and which way round.**
            //
            // The bearing is what makes three meshes cover every case: local +z and +x are turned
            // onto the world directions a shape expects its steps to be, exactly as
            // Directions.Yaw is defined to do. A straight piece wants one step at local +z; a
            // corner piece wants steps at local +z and +x; a hip wants one on the diagonal between
            // them. So the rotation is always the lower-numbered direction of the pair.
            //
            // **One bank to a cell, even at an inside corner where two steps meet.**
            //
            // This is a z-fighting fix and the fault is worth recording, because every piece of it
            // is individually correct. A bank fills its cell in plan, so two banks in one cell are
            // two boxes turned ninety degrees to each other — and the side wall of the first lands
            // in the same plane as the *back* wall of the second, facing the same way. Coplanar
            // surfaces with opposite normals are harmless, because back-face culling removes one of
            // them from every viewpoint; coplanar surfaces facing the *same* way are two candidates
            // for the same pixel with nothing to separate them, and the depth buffer picks whichever
            // rounds higher. That is the flickering the owner saw, and it moves with the camera
            // because the rounding does.
            //
            // A straight run has no such problem: the touching walls of two neighbouring banks face
            // away from each other, so one is always culled. It is only the corner.
            //
            // Drawing one is also the better picture. Two stepped banks crossing at a corner put
            // their treads at different heights through one another, which reads as rubble rather
            // than as a path; one bank fills the cell, meets the other riser along its side, and
            // the corner is still somewhere a colonist can walk up.
            int steps = StepsAround(model, x, z, y);
            BankMesh.Kind kind;
            int rotation;

            if (steps != 0)
            {
                // Prefer a corner: a cell with steps on two adjacent sides is in a notch, and the
                // straight piece would leave one of them bare.
                rotation = AdjacentPair(steps);
                if (rotation >= 0)
                {
                    kind = BankMesh.Kind.Inner;
                }
                else
                {
                    kind = BankMesh.Kind.Straight;
                    rotation = FirstDirection(steps);
                }
            }
            else
            {
                // No step orthogonally, but one on a diagonal: the cell wrapping the outside of a
                // convex corner. It used to get nothing at all, which is why a run of banks had a
                // square bite taken out of it at every corner.
                rotation = DiagonalStep(model, x, z, y);
                if (rotation < 0) return default;
                kind = BankMesh.Kind.Outer;
            }

            // Made of the terrain at the top of the step it climbs, because that is the ground it
            // is spilling from.
            ushort terrain = StepTerrain(model, x, z, y, rotation);
            if (terrain == CoreContent.TerrainAir) return default;

            return new Bank(kind, rotation, terrain);
        }

        /// <summary>The same, by cell.</summary>
        public static Bank At(WorldRenderModel model, CellRef cell) =>
            At(model, cell.X, cell.Z, cell.Y);

        // ------------------------------------------------------------------ the surface

        /// <summary>
        /// How far above its own floor a bank's surface stands at a point, in metres, or zero where
        /// there is no bank.
        ///
        /// <para>The world point's height is ignored: a bank is a height field over its cell, so
        /// only where the point stands on the ground plane can matter. The point is expected to be
        /// inside the cell and is clamped rather than checked, because a caller a millimetre over
        /// the edge wants the edge and not an exception.</para>
        ///
        /// <para><b>The rotation is undone rather than applied.</b> The mesh is drawn by turning
        /// local <c>+z</c> onto the bearing, so reading it back means turning the world offset the
        /// other way — and getting that backwards is invisible on a straight run, where the slope
        /// happens to be symmetric about the axis it is turned around, and wrong at every corner.
        /// </para>
        /// </summary>
        public static float RiseAt(WorldRenderModel? model, CellRef cell, float worldX, float worldZ)
        {
            if (model == null || !LiftFigures) return 0f;
            if (!model.Size.Contains(cell.X, cell.Z, cell.Y)) return 0f;

            // The ground skin's ramp (design 38 §6, §20): the surface the mesher draws, read back by
            // the same corners and the same triangulation, so a figure stands on the drawn slope.
            if (GroundSkin.Enabled)
            {
                if (!GroundCorners(model, cell.X, cell.Z, cell.Y, out Ramp ramp)) return 0f;
                Vector3 corner = CellMetrics.FloorCentre(cell) - new Vector3(CellMetrics.HalfXZ, 0f, CellMetrics.HalfXZ);
                float u = Mathf.Clamp01((worldX - corner.x) / CellMetrics.SizeXZ);
                float v = Mathf.Clamp01((worldZ - corner.z) / CellMetrics.SizeXZ);
                return ramp.HeightAt(u, v) * CellMetrics.SizeY;
            }

            Bank bank = At(model, cell);
            return bank.Exists ? RiseAt(bank, cell, worldX, worldZ) : 0f;
        }

        // ------------------------------------------------------------------ the skin's ramp

        /// <summary>
        /// A ramp over one cell as four corner rises, in cell heights (0 = the cell's floor, 1 = the
        /// rim of the step above), and the diagonal its two triangles share.
        ///
        /// <para>Corners are numbered from the cell's low-x low-z corner anticlockwise seen from
        /// above: 0 (−x,−z), 1 (+x,−z), 2 (+x,+z), 3 (−x,+z). <see cref="SplitZeroTwo"/> says the
        /// quad is cut along 0–2, otherwise along 1–3.</para>
        /// </summary>
        public readonly struct Ramp
        {
            public Ramp(float r0, float r1, float r2, float r3, bool splitZeroTwo)
            {
                R0 = r0; R1 = r1; R2 = r2; R3 = r3; SplitZeroTwo = splitZeroTwo;
            }

            public readonly float R0, R1, R2, R3;
            public readonly bool SplitZeroTwo;

            public float Corner(int i) => i switch { 0 => R0, 1 => R1, 2 => R2, _ => R3 };

            /// <summary>
            /// The rise at (u, v) in the cell, both 0..1 from corner 0 — exactly the two triangles
            /// the mesher draws, so a reader and the picture cannot disagree.
            /// </summary>
            public float HeightAt(float u, float v)
            {
                if (SplitZeroTwo)
                    return u >= v
                        ? R0 + (R1 - R0) * u + (R2 - R1) * v
                        : R0 + (R2 - R3) * u + (R3 - R0) * v;
                return u + v <= 1f
                    ? R0 + (R1 - R0) * u + (R3 - R0) * v
                    : (R1 + R3 - R2) + (R2 - R3) * u + (R2 - R1) * v;
            }
        }

        // Corner i sits at (CornerX[i], CornerZ[i]) in half-cell units from the centre.
        static readonly int[] CornerX = { -1, 1, 1, -1 };
        static readonly int[] CornerZ = { -1, -1, 1, 1 };

        /// <summary>
        /// The skin's ramp in this cell, or false where the ground here is drawn flat.
        ///
        /// <para><b>The corner rule.</b> A corner rises to the rim where any of the three other
        /// cells that meet at it is a step (<see cref="IsStep"/>). That is the three bank shapes
        /// generalised — a straight run lifts the two corners on its step's side, an inner corner
        /// three, an outer corner one — and because two neighbouring cells ask the same cells about
        /// the corner they share, the surface is continuous without either knowing the other.</para>
        ///
        /// <para><b>The same cells, not new ones.</b> A cell gets a ramp only where
        /// <see cref="At"/> finds a bank, which is where <c>TerraceFoot.IsFoot</c> says a step rises
        /// out of it — the simulation's copy of the rule is unchanged, and so is every golden.</para>
        ///
        /// <para><b>All four corners high is drawn flat instead.</b> That is a trench between two
        /// terraces, a pit, or a cell ringed by diagonal steps, and the ramp would cap it flush with
        /// the ground above — hiding a hole the simulation still has. It keeps its floor and its
        /// sheer walls, as it always did; <see cref="GroundSkin"/> closes the edge with a skirt.</para>
        /// </summary>
        public static bool RampCorners(WorldRenderModel model, int x, int z, int y, out Ramp ramp)
        {
            ramp = default;
            if (!At(model, x, z, y).Exists) return false;

            float r0 = CornerHigh(model, x, z, y, 0) ? 1f : 0f;
            float r1 = CornerHigh(model, x, z, y, 1) ? 1f : 0f;
            float r2 = CornerHigh(model, x, z, y, 2) ? 1f : 0f;
            float r3 = CornerHigh(model, x, z, y, 3) ? 1f : 0f;

            int high = (int)(r0 + r1 + r2 + r3);
            if (high == 0 || high == 4) return false;

            // The diagonal: through the odd corner when one or three are high, which is what makes
            // an inner corner max(u,v) and an outer corner min(u,v) as the old banks were; along the
            // ridge when two opposite corners are high; otherwise any, and 0–2 by convention.
            bool split02;
            if (high == 1 || high == 3)
            {
                bool odd0 = (r0 != r1) && (r0 != r3);
                bool odd2 = (r2 != r1) && (r2 != r3);
                split02 = odd0 || odd2;
            }
            else
            {
                split02 = !(r1 > 0f && r3 > 0f && r0 == 0f && r2 == 0f);
            }

            ramp = new Ramp(r0, r1, r2, r3, split02);
            return true;
        }

        /// <summary>
        /// How far a stream bank's water-side corners drop below its top, in metres: to just above
        /// the water line of the layer (<see cref="ChunkMesher.WaterSurface"/>), so the meadow runs
        /// down into the stream rather than stopping at a square rim.
        /// </summary>
        public static float WaterBankDrop =>
            CellMetrics.SizeY * (1f - ChunkMesher.WaterSurface) - 0.05f;

        /// <summary>
        /// The skin's bank down into water: a solid earth cell whose every open side is water at its
        /// own layer (a stream or pond bank one layer above its bed), with each corner that touches
        /// open water lowered by <see cref="WaterBankDrop"/>. Rises are negative, in cell heights,
        /// measured from the cell's top — the floor of the cell above it, where a colonist walks.
        ///
        /// <para>False where the cell keeps its box: a riser with dry air beside it, a cut face,
        /// something built on it (the ground levels under anything built), or no water at a corner.
        /// The simulation never hears of it: the cell above is walkable at its floor as always, and
        /// a figure or an item there is drawn on the slope through <see cref="RiseAt"/>.</para>
        /// </summary>
        public static bool BankDips(WorldRenderModel model, int x, int z, int y, out Ramp dip)
        {
            dip = default;
            if (!GroundSkin.Enabled) return false;
            GridSize size = model.Size;
            if (!size.Contains(x, z, y) || y + 1 >= size.SizeY) return false;

            int index = size.Index(x, z, y);
            if (!model.IsSolid(index) || !model.IsEarth(index) || model.IsCutFace(index)) return false;
            int above = index + size.LayerStride;
            if (model.IsSolid(above) || model.Floor(above) != CoreContent.SlabNone) return false;
            // Air over it, where a colonist walks. The bed under a stream is earth beside water too
            // — the next stretch down a cascade — and dipping it pulled the water above it down with
            // it (measured: SlicePickerBoardTests lost the water on every cascade).
            if (model.Terrain(above) != CoreContent.TerrainAir) return false;
            ushort edifice = model.EdificeDef(above);
            if (edifice != 0 && !Odyssey.Sim.Worldgen.Natural.NaturalContent.IsTree(edifice)) return false;
            if (y > 0 && !model.IsSolid(index - size.LayerStride)) return false;

            // Every open side must be water: a dry riser beside it is a terrace, not a bank.
            for (int dir = 0; dir < Directions.Count; dir++)
            {
                int nx = x + Directions.DeltaX[dir], nz = z + Directions.DeltaZ[dir];
                if (!size.Contains(nx, nz, y)) continue;
                int n = size.Index(nx, nz, y);
                if (!model.IsSolid(n) && !OpenWater(model, n)) return false;
            }

            float drop = -WaterBankDrop / CellMetrics.SizeY;
            float r0 = CornerWet(model, x, z, y, 0) ? drop : 0f;
            float r1 = CornerWet(model, x, z, y, 1) ? drop : 0f;
            float r2 = CornerWet(model, x, z, y, 2) ? drop : 0f;
            float r3 = CornerWet(model, x, z, y, 3) ? drop : 0f;
            int low = (r0 < 0f ? 1 : 0) + (r1 < 0f ? 1 : 0) + (r2 < 0f ? 1 : 0) + (r3 < 0f ? 1 : 0);
            if (low == 0) return false;

            dip = new Ramp(r0, r1, r2, r3, SplitFor(r0, r1, r2, r3));
            return true;
        }

        /// <summary>
        /// The shape of the ground a colonist stands on in air cell (x, z, y): the ramp in it, or
        /// the bank dipping in the cell under it, as rises from that cell's floor.
        /// </summary>
        public static bool GroundCorners(WorldRenderModel model, int x, int z, int y, out Ramp shape)
        {
            if (RampCorners(model, x, z, y, out shape)) return true;
            return y > 0 && BankDips(model, x, z, y - 1, out shape);
        }

        static bool OpenWater(WorldRenderModel model, int index) =>
            !model.IsSolid(index) && Odyssey.Sim.Worldgen.Natural.NaturalContent.IsWater(model.Terrain(index));

        static bool CornerWet(WorldRenderModel model, int x, int z, int y, int corner)
        {
            GridSize size = model.Size;
            int cx = CornerX[corner], cz = CornerZ[corner];
            for (int k = 0; k < 3; k++)
            {
                int dx = k == 1 ? 0 : cx, dz = k == 0 ? 0 : cz;
                int nx = x + dx, nz = z + dz;
                if (!size.Contains(nx, nz, y)) continue;
                if (OpenWater(model, size.Index(nx, nz, y))) return true;
            }
            return false;
        }

        /// <summary>
        /// The diagonal a quad is cut on: through the odd corner when one or three corners differ
        /// from the rest, along the pair when two opposite corners do, else 0–2.
        /// </summary>
        static bool SplitFor(float r0, float r1, float r2, float r3)
        {
            bool odd0 = r0 != r1 && r0 != r3;
            bool odd1 = r1 != r0 && r1 != r2;
            bool odd2 = r2 != r1 && r2 != r3;
            bool odd3 = r3 != r2 && r3 != r0;
            int odd = (odd0 ? 1 : 0) + (odd1 ? 1 : 0) + (odd2 ? 1 : 0) + (odd3 ? 1 : 0);
            if (odd == 1) return odd0 || odd2;
            // Two opposite corners share a value unlike the other two: cut along that pair.
            if (r0 == r2 && r1 == r3 && r0 != r1) return r0 > r1;
            return true;
        }

        static bool CornerHigh(WorldRenderModel model, int x, int z, int y, int corner)
        {
            int cx = CornerX[corner], cz = CornerZ[corner];
            return IsStep(model, x, z, y, cx, 0) || IsStep(model, x, z, y, 0, cz) || IsStep(model, x, z, y, cx, cz);
        }

        /// <summary>The same for a bank already found, which is what a caller with one in hand wants.</summary>
        public static float RiseAt(in Bank bank, CellRef cell, float worldX, float worldZ)
        {
            if (!bank.Exists) return 0f;

            Vector3 centre = CellMetrics.FloorCentre(cell);
            float u = Mathf.Clamp((worldX - centre.x) / CellMetrics.SizeXZ, -0.5f, 0.5f);
            float v = Mathf.Clamp((worldZ - centre.z) / CellMetrics.SizeXZ, -0.5f, 0.5f);

            // Into the shape's own frame. Yaw turns local +z onto the bearing, so the inverse turns
            // the world offset back onto local +z.
            float radians = -Directions.Yaw[bank.Rotation] * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
            float localX = u * cos + v * sin;
            float localZ = -u * sin + v * cos;

            // HeightAt runs -0.5 at the cell floor to +0.5 at its ceiling, so the rise above the
            // floor is the height plus a half, in cell heights.
            return (BankMesh.HeightAt(bank.Kind, localX, localZ) + 0.5f) * CellMetrics.SizeY;
        }

        /// <summary>
        /// A shear matrix in bank-local coordinates representing the slope of a straight bank.
        /// The bank rises by SizeY (3.0 m) over SizeXZ (2.5 m) along local +z, centered at local y = 1.5 m.
        /// </summary>
        public static Matrix4x4 StraightBankShear()
        {
            var m = Matrix4x4.identity;
            m.m12 = CellMetrics.SizeY / CellMetrics.SizeXZ;
            m.m13 = CellMetrics.SizeY * 0.5f;
            return m;
        }

        // ------------------------------------------------------------------ the conditions

        /// <summary>
        /// Could a bank stand in this cell at all — is it empty, on ground, uncut, and under open
        /// sky?
        ///
        /// <para>Separate from which shape it wants, because the conditions are about the cell and
        /// the shape is about its neighbours, and mixing the two is what made an earlier version
        /// answer "no bank" and "a bank facing north" through the same integer.</para>
        /// </summary>
        static bool CanBank(WorldRenderModel model, int x, int z, int y)
        {
            if (!Enabled || y == 0) return false;

            GridSize size = model.Size;
            if (!size.Contains(x, z, y) || y + 1 >= size.SizeY) return false;

            int index = size.Index(x, z, y);
            if (model.Terrain(index) != CoreContent.TerrainAir) return false;

            // Something underfoot: the top of the lower terrace. Terrain rather than solidity, so a
            // bank may also shelve down into the water it stands beside — a channel is cut one
            // layer down, which makes every stream bank one of these steps.
            int floor = index - size.LayerStride;
            if (model.Terrain(floor) == CoreContent.TerrainAir) return false;

            // **Nothing grows inside a hole the colony cut**, and the floor is what says so: a
            // mined cell reveals all six of its solid neighbours, and the one below it is the
            // floor the miner is left standing on. So a cut floor is the mark of an excavation on
            // the very cell a bank would fill.
            //
            // The same test on the sides (see IsStep) is not enough on its own, and the shortfall
            // is worth recording because it is invisible in the obvious case. A cell cut out of
            // flat ground has all four of its sides revealed, so StepsAround comes back empty —
            // and the hip branch then went looking at the *diagonal* neighbours, which nothing
            // reveals, because a colonist who cuts past the corner of a seam has not seen into
            // it. So the hole filled with a hip piece instead of a corner one: measured, a single
            // cut cell still drew 1 bank and a four-cell bench still drew 2. Asking the floor
            // catches every shape of working at once, whatever its sides happen to say.
            if (!InWorkings && model.IsCutFace(floor)) return false;

            return model.OpenToTheSky(index, y);
        }

        /// <summary>Is the cell one step away in this direction a step this bank could climb?</summary>
        static bool IsStep(WorldRenderModel model, int x, int z, int y, int dx, int dz)
        {
            GridSize size = model.Size;
            int nx = x + dx, nz = z + dz;
            if (!size.Contains(nx, nz, y)) return false;

            int step = size.Index(nx, nz, y);
            if (!model.IsSolid(step) || !model.IsEarth(step)) return false;

            // **A face somebody cut stays sheer, even in soil.**
            //
            // The terrain test above is not enough, and the gap was a reported bug: grass, bare
            // earth and subsoil are all mineable (60, 60 and 160 ticks to clear), so a quarry sunk
            // into the meadow is a hole whose walls are earth with open tops — every condition a
            // terrace step has. A grassy ramp spilling down a quarry wall is exactly the same lie
            // about cut rock that the terrain test already rejects.
            if (!InWorkings && model.IsCutFace(step)) return false;

            // Its top has to be open, or this is the wall of a tunnel rather than a terrace.
            return !model.IsSolid(step + size.LayerStride);
        }

        /// <summary>Which of the four sides of this cell have a step against them, as a bitmask.</summary>
        static int StepsAround(WorldRenderModel model, int x, int z, int y)
        {
            int mask = 0;
            for (int dir = 0; dir < Directions.Count; dir++)
                if (IsStep(model, x, z, y, Directions.DeltaX[dir], Directions.DeltaZ[dir]))
                    mask |= 1 << dir;
            return mask;
        }

        /// <summary>The first direction in a mask, or -1 when it is empty.</summary>
        static int FirstDirection(int mask)
        {
            for (int dir = 0; dir < Directions.Count; dir++)
                if ((mask & (1 << dir)) != 0) return dir;
            return -1;
        }

        /// <summary>
        /// The lower direction of a pair of adjacent set bits, or -1 when the mask has no such pair.
        ///
        /// A corner piece is turned by this, because its two steps are at local +z and +x, which the
        /// bearing puts on directions <c>d</c> and <c>d + 1</c>.
        /// </summary>
        static int AdjacentPair(int mask)
        {
            for (int dir = 0; dir < Directions.Count; dir++)
                if ((mask & (1 << dir)) != 0 && (mask & (1 << ((dir + 1) & 3))) != 0) return dir;
            return -1;
        }

        /// <summary>
        /// The direction <c>d</c> such that the step lies on the diagonal between <c>d</c> and
        /// <c>d + 1</c>, or -1 when no diagonal neighbour is a step.
        /// </summary>
        static int DiagonalStep(WorldRenderModel model, int x, int z, int y)
        {
            for (int dir = 0; dir < Directions.Count; dir++)
            {
                int next = (dir + 1) & 3;
                int dx = Directions.DeltaX[dir] + Directions.DeltaX[next];
                int dz = Directions.DeltaZ[dir] + Directions.DeltaZ[next];
                if (IsStep(model, x, z, y, dx, dz)) return dir;
            }
            return -1;
        }

        /// <summary>
        /// The terrain at the top of the step this bank climbs, which is what it is made of.
        ///
        /// A hip has no orthogonal step, so it takes the terrain from the diagonal one it wraps.
        /// </summary>
        static ushort StepTerrain(WorldRenderModel model, int x, int z, int y, int rotation)
        {
            GridSize size = model.Size;
            int next = (rotation + 1) & 3;

            for (int i = 0; i < 2; i++)
            {
                int dir = i == 0 ? rotation : next;
                int nx = x + Directions.DeltaX[dir], nz = z + Directions.DeltaZ[dir];
                if (IsStep(model, x, z, y, Directions.DeltaX[dir], Directions.DeltaZ[dir]))
                    return model.Terrain(size.Index(nx, nz, y));
            }

            int cx = x + Directions.DeltaX[rotation] + Directions.DeltaX[next];
            int cz = z + Directions.DeltaZ[rotation] + Directions.DeltaZ[next];
            return size.Contains(cx, cz, y) ? model.Terrain(size.Index(cx, cz, y)) : CoreContent.TerrainAir;
        }
    }
}
