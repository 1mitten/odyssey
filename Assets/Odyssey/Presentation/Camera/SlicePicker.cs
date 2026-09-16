#nullable enable
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.CameraRig
{
    /// <summary>
    /// Turns a screen ray into a cell — and refuses to look at anything that is not drawn solid.
    ///
    /// <para><b>The rule was "never above the slice"; the owner changed it on 2026-09-16.</b> The
    /// report: <i>"on my default depth level I can only select objects/things on my level — I
    /// couldn't select the stones for mining for example, I should be able to click on an object
    /// in 3D space"</i>. That is the same correction that made the layers above the surface draw
    /// solid rather than x-rayed. An outcrop standing two cells proud of the meadow is a rock, and
    /// the player who can see a rock and not click it has been handed a picture, not a world.</para>
    ///
    /// <para><b>What survives of the old rule, and why.</b> Going Medieval's single most-reported
    /// complaint is misclicking something on another floor, with players reporting buildings
    /// deconstructed by accident (<c>b-going-medieval.md</c>), and ADR 0006 answered it by making
    /// geometry above the slice a depth cue and nothing else. The half of that which was really
    /// load-bearing is <b>a ghost is never a pointer target</b>: a translucent hint of a wall is a
    /// cue, and clicking a cue is the accident. So the band this picker walks is exactly the band
    /// the renderer draws <i>solid</i> — underground, where the layer above is x-rayed, the old
    /// behaviour is unchanged and a click cannot leave the active layer upwards.</para>
    ///
    /// <para><b>Nearest along the ray wins, and the active layer breaks a tie.</b> The top face of
    /// a solid cell and the floor of the air cell above it are the same surface at the same
    /// distance, so the two candidates arrive together; whichever layer is nearer the slice is
    /// returned. That reproduces the old single-layer answers exactly — clicking the meadow gives
    /// the air cell you stand in, clicking an outcrop gives the rock — and extends them upwards and
    /// downwards without a second convention.</para>
    ///
    /// It is a pure function over the render mirror: no Unity scene, no physics, and therefore
    /// testable in EditMode without a camera existing.
    /// </summary>
    public static class SlicePicker
    {
        /// <summary>
        /// Two hits closer together than this are the same surface, in metres. Ray directions come
        /// from <c>Camera.ScreenPointToRay</c> and are unit length, so the parameter is a distance.
        /// </summary>
        const float SameSurface = 1e-3f;

        /// <summary>
        /// The first cell the ray meets on the active layer, and on no other. The form the tests
        /// and any caller without a slice policy use; equivalent to passing a
        /// <see cref="SliceSettings"/> that draws nothing above or below.
        /// </summary>
        public static bool Pick(Ray ray, WorldRenderModel model, int activeLayer, out CellRef cell) =>
            Pick(ray, model, activeLayer, null, out cell);

        /// <summary>
        /// The nearest cell the ray meets on any layer the slice draws solid.
        ///
        /// A cell counts as hit when it holds something that occludes — wall, door, pillar, solid
        /// strata — or when the ray crosses that cell's floor. The returned cell's layer is always
        /// one <paramref name="slice"/> would draw at full opacity; that is a guarantee, not a
        /// consequence.
        /// </summary>
        public static bool Pick(
            Ray ray, WorldRenderModel model, int activeLayer, SliceSettings? slice, out CellRef cell)
        {
            cell = default;
            var size = model.Size;
            if (activeLayer < 0 || activeLayer >= size.SizeY) return false;

            Band(model, activeLayer, slice, out int lowest, out int highest);

            bool found = false;
            float best = float.MaxValue;

            // Outwards from the slice, so that when two layers offer the same surface at the same
            // distance the first one accepted is the one nearest the layer being worked. The
            // strict improvement below is what makes the visit order the tie-break.
            for (int step = 0; step <= Mathf.Max(activeLayer - lowest, highest - activeLayer); step++)
            for (int side = 0; side < 2; side++)
            {
                int layer = side == 0 ? activeLayer - step : activeLayer + step;
                if (step == 0 && side == 1) continue;
                if (layer < lowest || layer > highest) continue;

                // The layer immediately above the slice is the active layer's ceiling and the
                // renderer drops its slab so the player can see in. A surface that is not drawn
                // must not be clickable, so that layer offers only what occludes — the rock over
                // your head stays pickable, the floor slab that was meshed away does not.
                bool floors = !(slice != null && layer == activeLayer + 1 && slice.SuppressCeilingAt(activeLayer));

                if (!PickOnLayer(ray, model, layer, floors, out CellRef hit, out float t)) continue;
                if (found && t >= best - SameSurface) continue;

                best = t;
                cell = hit;
                found = true;
            }

            return found;
        }

        /// <summary>
        /// The layers a click may land on: those the renderer draws at full opacity, capped at the
        /// top of the geometry.
        ///
        /// <para>A null slice means the caller has no policy and wants the active layer alone,
        /// which is what every call site did before the band existed.</para>
        /// </summary>
        static void Band(WorldRenderModel model, int activeLayer, SliceSettings? slice, out int lowest, out int highest)
        {
            if (slice == null)
            {
                lowest = highest = activeLayer;
                return;
            }

            int layers = model.Size.SizeY;
            lowest = Mathf.Clamp(slice.LowestSelectableLayer(activeLayer), 0, activeLayer);
            highest = Mathf.Clamp(slice.HighestSelectableLayer(activeLayer, layers), activeLayer, layers - 1);

            // The same cap the renderer's loop uses. Above the surface the policy says "every
            // layer, solid", and solid has no fade to stop it, so without this the walk would
            // climb through however many layers of empty sky the map is tall.
            highest = Mathf.Min(highest, Mathf.Max(activeLayer, model.HighestOccupiedLayer));
        }

        /// <summary>
        /// The first cell of one layer the ray meets, with the distance at which it met it.
        ///
        /// <para>This is the original picker, unchanged but for the two outputs and the
        /// <paramref name="floors"/> switch: the ray is clipped analytically to the layer's own
        /// slab before a single cell is visited, so geometry on other layers cannot be returned by
        /// accident — only by the caller asking for that layer deliberately.</para>
        /// </summary>
        static bool PickOnLayer(
            Ray ray, WorldRenderModel model, int layer, bool floors, out CellRef cell, out float hitAt)
        {
            cell = default;
            hitAt = float.MaxValue;
            var size = model.Size;
            if (layer < 0 || layer >= size.SizeY) return false;

            float slabMin = layer * CellMetrics.SizeY;
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
                int index = size.Index(x, z, layer);
                float tCellEnd = tEnter + Mathf.Min(tMaxX, tMaxZ);

                if (model.OccludesFace(index))
                {
                    cell = new CellRef(x, z, layer);

                    // The distance has to be the drawn surface, not the clip. A ray coming in
                    // steeply meets a solid cell at the slab entry, and that entry was opened up
                    // by the relief's reach a few lines above -- so an unclamped answer would put
                    // this cell up to half a metre nearer the camera than it is drawn, and a
                    // buried rock would out-bid the meadow standing on top of it. Clamping to the
                    // cell's own drawn top face makes the two exactly equal, which is what they
                    // are: one surface, and the tie-break picks which cell owns it.
                    hitAt = ray.direction.y < 0f
                        ? Mathf.Max(t, FloorCrossing(ray, slabMax + FloorHeightAt(x, z)))
                        : t;
                    return true;
                }

                // Per cell, against that cell's own drawn floor rather than once against the
                // layer's flat plane. This is the whole of the relief's effect on picking: the
                // ground the player is aiming at is the tilted one, so that is the surface the ray
                // has to meet.
                float tFloor = floors ? FloorCrossing(ray, slabMin + FloorHeightAt(x, z)) : float.MaxValue;

                if (tFloor >= t - 1e-4f && tFloor <= tCellEnd && HasFloor(model, index, layer))
                {
                    cell = new CellRef(x, z, layer);
                    hitAt = tFloor;
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
