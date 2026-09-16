#nullable enable
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.CameraRig
{
    /// <summary>
    /// Turns a screen ray into a cell — and refuses to look above the active layer.
    ///
    /// **This is the rule the whole picker exists to enforce.** Going Medieval's single
    /// most-reported complaint is misclicking something on another floor, with players reporting
    /// buildings deconstructed by accident (<c>b-going-medieval.md</c>), and
    /// <c>06-rendering-and-camera.md</c> settles it: geometry above the slice is a depth cue and
    /// nothing else. So the picker does not raycast the scene at all. There are no colliders to
    /// hit — the world is instanced geometry with no GameObjects — and the ray is clipped
    /// analytically to the active layer's slab before a single cell is visited. A ghosted wall two
    /// storeys up cannot be returned because the ray is never tested against it.
    ///
    /// It is a pure function over the render mirror: no Unity scene, no physics, and therefore
    /// testable in EditMode without a camera existing.
    /// </summary>
    public static class SlicePicker
    {
        /// <summary>
        /// The first cell of <paramref name="activeLayer"/> the ray meets, if any.
        ///
        /// A cell counts as hit when it holds something that occludes — wall, door, pillar, solid
        /// strata — or when the ray crosses that cell's floor. The returned cell's layer is always
        /// <paramref name="activeLayer"/>; that is a guarantee, not a consequence.
        /// </summary>
        public static bool Pick(Ray ray, WorldRenderModel model, int activeLayer, out CellRef cell)
        {
            cell = default;
            var size = model.Size;
            if (activeLayer < 0 || activeLayer >= size.SizeY) return false;

            float slabMin = activeLayer * CellMetrics.SizeY;
            float slabMax = slabMin + CellMetrics.SizeY;

            // The relief draws the ground away from its layer, and a click has to land on what the
            // player can see rather than on the flat grid underneath it. Relief moves nothing
            // horizontally, so the walk below is untouched -- but the slab the ray is clipped to
            // has to be opened up by however far the ground can travel, or a ray aimed at a hill
            // top is discarded before a single cell is visited.
            float reliefReach = ReliefReach();
            float clipMin = slabMin - reliefReach;
            float clipMax = slabMax + reliefReach;
            float worldMaxX = size.SizeX * CellMetrics.SizeXZ;
            float worldMaxZ = size.SizeZ * CellMetrics.SizeXZ;

            float tEnter = 0f, tExit = float.MaxValue;
            if (!Slab(ray.origin.y, ray.direction.y, clipMin, clipMax, ref tEnter, ref tExit)) return false;
            if (!Slab(ray.origin.x, ray.direction.x, 0f, worldMaxX, ref tEnter, ref tExit)) return false;
            if (!Slab(ray.origin.z, ray.direction.z, 0f, worldMaxZ, ref tEnter, ref tExit)) return false;
            if (tExit <= tEnter) return false;

            // Nudge inside so a ray entering exactly on a cell boundary starts in the right cell.
            Vector3 entry = ray.origin + ray.direction * (tEnter + 1e-4f);
            int x = Mathf.Clamp(Mathf.FloorToInt(entry.x / CellMetrics.SizeXZ), 0, size.SizeX - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(entry.z / CellMetrics.SizeXZ), 0, size.SizeZ - 1);

            int stepX = ray.direction.x > 0f ? 1 : -1;
            int stepZ = ray.direction.z > 0f ? 1 : -1;
            float tMaxX = NextBoundary(entry.x, ray.direction.x, x, stepX);
            float tMaxZ = NextBoundary(entry.z, ray.direction.z, z, stepZ);
            float tDeltaX = Mathf.Abs(ray.direction.x) > 1e-6f
                ? CellMetrics.SizeXZ / Mathf.Abs(ray.direction.x) : float.MaxValue;
            float tDeltaZ = Mathf.Abs(ray.direction.z) > 1e-6f
                ? CellMetrics.SizeXZ / Mathf.Abs(ray.direction.z) : float.MaxValue;

            float t = tEnter;
            int guard = size.SizeX + size.SizeZ + 4;

            while (guard-- > 0)
            {
                int index = size.Index(x, z, activeLayer);
                float tCellEnd = tEnter + Mathf.Min(tMaxX, tMaxZ);

                if (model.OccludesFace(index))
                {
                    cell = new CellRef(x, z, activeLayer);
                    return true;
                }

                // Per cell, against that cell's own drawn floor rather than once against the
                // layer's flat plane. This is the whole of the relief's effect on picking: the
                // ground the player is aiming at is the tilted one, so that is the surface the ray
                // has to meet.
                float tFloor = FloorCrossing(ray, slabMin + FloorHeightAt(x, z));

                if (tFloor >= t - 1e-4f && tFloor <= tCellEnd && HasFloor(model, index, activeLayer))
                {
                    cell = new CellRef(x, z, activeLayer);
                    return true;
                }

                if (tCellEnd >= tExit) return false;

                if (tMaxX < tMaxZ)
                {
                    x += stepX;
                    if ((uint)x >= (uint)size.SizeX) return false;
                    tMaxX += tDeltaX;
                }
                else
                {
                    z += stepZ;
                    if ((uint)z >= (uint)size.SizeZ) return false;
                    tMaxZ += tDeltaZ;
                }

                t = tCellEnd;
            }

            return false;
        }

        /// <summary>How far the relief has carried this cell's floor out of its layer.</summary>
        static float FloorHeightAt(int x, int z)
        {
            Vector3 centre = CellMetrics.FloorCentre(x, z, 0);
            return GroundRelief.HeightAt(centre.x, centre.z);
        }

        /// <summary>
        /// The most the relief can move a cell's floor, in metres. Matches the mesher's own reach:
        /// the lift, plus the tilt carrying a corner higher still.
        /// </summary>
        static float ReliefReach()
        {
            float amplitude = Mathf.Abs(GroundRelief.Amplitude);
            if (amplitude == 0f) return 0f;
            return amplitude + GroundRelief.MaxSlope(amplitude) * CellMetrics.SizeXZ;
        }

        static bool HasFloor(WorldRenderModel model, int index, int layer)
        {
            if (model.Floor(index) != 0) return true;
            if (layer == 0) return false;
            return model.IsSolid(index - model.Size.LayerStride);
        }

        /// <summary>Where the ray crosses the layer's floor plane, or "never".</summary>
        static float FloorCrossing(Ray ray, float floorY)
        {
            if (Mathf.Abs(ray.direction.y) < 1e-6f) return float.MaxValue;
            float t = (floorY - ray.origin.y) / ray.direction.y;
            return t < 0f ? float.MaxValue : t;
        }

        /// <summary>Clip a ray parameter range to one axis-aligned slab. Standard slab test.</summary>
        static bool Slab(float origin, float direction, float min, float max, ref float tEnter, ref float tExit)
        {
            if (Mathf.Abs(direction) < 1e-6f) return origin >= min && origin <= max;
            float t0 = (min - origin) / direction;
            float t1 = (max - origin) / direction;
            if (t0 > t1) { float swap = t0; t0 = t1; t1 = swap; }
            if (t0 > tEnter) tEnter = t0;
            if (t1 < tExit) tExit = t1;
            return tEnter <= tExit;
        }

        static float NextBoundary(float position, float direction, int cell, int step)
        {
            if (Mathf.Abs(direction) < 1e-6f) return float.MaxValue;
            float boundary = (step > 0 ? cell + 1 : cell) * CellMetrics.SizeXZ;
            return (boundary - position) / direction;
        }
    }
}
