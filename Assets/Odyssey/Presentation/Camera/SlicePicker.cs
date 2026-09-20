#nullable enable
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
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
    /// <para><b>A face belongs to whatever you clicked, and if nothing is there the click misses.</b>
    /// The owner's rule, 2026-09-16: <i>"I still wanted to select the tile below it or not at
    /// all."</i> So the top of the meadow is the <b>ground block</b>, not the empty cell standing
    /// on it, and the top of an outcrop is the rock. This replaced a first attempt that returned the air cell
    /// above a surface — the convention the picker had always used on one layer, because on one
    /// layer it was the only cell on offer. Carried up and down a stack it reads as clicking a
    /// rock and selecting the sky above it.</para>
    ///
        /// <para><b>An edifice standing in a cell is the thing you clicked, and that is the same rule
        /// rather than an exception to it.</b> A tree is an edifice that blocks nothing, standing in
        /// the walkable cell — so a literal reading of "select the tile below" would hand back the
        /// ground under every tree and <i>felling could never be ordered again</i>. What you are
        /// looking at there is the tree, and the tree owns the face. The same holds for a wall, a door
        /// and a built floor slab: each is drawn in its own cell and each is returned in it.</para>
        ///
        /// <para><b>Water owns its own floor.</b> Both waters fill their cell but neither is solid,
        /// so a click on a pond reaches the bed beneath it and the block-below rule would answer
        /// "what did I click?" with rock the player cannot see. The water claims the click first,
        /// and the pane can then say what it is and what crossing it costs (owner, 2026-09-17: a
        /// water tile did not tell him it was water). A bridge slab still wins — it is checked
        /// first, and a bridge is walked on, not waded through.</para>
    ///
    /// <para><b>A waiting order is one of the things in the world</b> (owner, 2026-09-18: a slab
    /// being built could not be cancelled, and over open air the whole layer answered nothing). A
    /// site has a cell and the player pointing at one means it; only the *cursor's* ghost is a cue,
    /// and that is still untouchable. <see cref="World.WorldRenderModel.SetSites"/> puts them where
    /// this can see them.</para>
    ///
    /// <para><b>Nearest along the ray wins</b>, with a thing beating bare ground at the same
    /// distance and, failing that, the layer nearer the slice.</para>
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
        /// The form for a caller with no slice policy: only the active layer is marched, which is
        /// what every call site did before the band existed.
        ///
        /// <para>Marched, not returned — a floor crossing on the active layer is the top of the
        /// block holding it up, so the answer may be the layer below. The one thing a null slice
        /// guarantees is that nothing is searched anywhere else.</para>
        /// </summary>
        public static bool Pick(Ray ray, WorldRenderModel model, int activeLayer, out CellRef cell) =>
            Pick(ray, model, activeLayer, null, out cell);

        /// <summary>
        /// The nearest thing the ray meets on any layer the slice draws solid.
        ///
        /// <para>A cell counts as hit when it holds something that occludes — wall, door, pillar,
        /// solid strata — or when the ray crosses a floor it owns. <b>Only layers drawn at full
        /// opacity are searched</b>, which is the guarantee that matters and the one ADR 0006's
        /// misclick argument rests on.</para>
        ///
        /// <para>The cell handed back may still be one layer under the lowest searched, because a
        /// floor crossing resolves to the block underneath and that block's top face is the
        /// surface the player aimed at. Refusing it would mean refusing to select the ground at
        /// the bottom of the drawn range, which is ground they can see.</para>
        /// </summary>
        public static bool Pick(
            Ray ray, WorldRenderModel model, int activeLayer, SliceSettings? slice, out CellRef cell)
        {
            cell = default;
            var size = model.Size;
            if (activeLayer < 0 || activeLayer >= size.SizeY) return false;

            Band(model, activeLayer, slice, out int lowest, out int highest);

            bool found = false;
            bool bestIsThing = false;
            float best = float.MaxValue;

            // Outwards from the slice, so that when two layers offer the same surface at the same
            // distance and neither holds a thing, the first one accepted is the one nearest the
            // layer being worked.
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
                //
                // RF1 adds the second half, and it is per cell rather than per layer. A roof two
                // or more layers up is always dropped, so it must not be clickable - but that rule
                // takes the SLABS and leaves the ground, so the layer as a whole still offers its
                // terrain. `floors` gates the horizontal pick for every cell alike, a hillside's
                // included, so switching it off for the whole layer would have left a terrace two
                // storeys up drawn and unclickable - the same disagreement between renderer and
                // picker, in the other direction. `slabsDropped` is the narrow one (27-roofs.md §4).
                bool floors = !(slice != null && layer == activeLayer + 1
                    && slice.SuppressCeilingAt(activeLayer));
                bool slabsDropped = slice != null
                    && SliceSettings.RoofIsAlwaysDropped(layer - activeLayer);

                if (!PickOnLayer(ray, model, layer, floors, slabsDropped,
                        out CellRef hit, out float t, out bool thing))
                    continue;

                // Strictly nearer, or the same surface with something standing on it. The second
                // clause is what keeps a tree clickable: the tree's cell and the ground block
                // underneath it offer the same face at the same distance, and the tree is what the
                // player is looking at.
                bool nearer = !found || t < best - SameSurface;
                bool sameSurfaceButAThing = found && !nearer
                    && Mathf.Abs(t - best) <= SameSurface && thing && !bestIsThing;
                if (!nearer && !sameSurfaceButAThing) continue;

                best = t;
                cell = hit;
                bestIsThing = thing;
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
            lowest = Mathf.Clamp(
                slice.LowestSelectableLayer(activeLayer, model.LowestOutdoorLayer), 0, activeLayer);
            highest = Mathf.Clamp(slice.HighestSelectableLayer(activeLayer, layers), activeLayer, layers - 1);

            // The same cap the renderer's loop uses. Above the surface the policy says "every
            // layer, solid", and solid has no fade to stop it, so without this the walk would
            // climb through however many layers of empty sky the map is tall.
            highest = Mathf.Min(highest, Mathf.Max(activeLayer, model.HighestOccupiedLayer));
        }

        /// <summary>
        /// The first thing the ray meets while marching one layer, the distance at which it met
        /// it, and whether what it met was an object rather than bare ground.
        ///
        /// <para>The ray is clipped analytically to this layer's own slab before a single cell is
        /// visited, so geometry on other layers cannot be returned by accident — only by the caller
        /// asking for that layer deliberately. <b>The cell returned may still be the one below</b>,
        /// because a floor crossing means the player has clicked the top of whatever holds this
        /// cell up, and that is the block underneath.</para>
        /// </summary>
        static bool PickOnLayer(
            Ray ray, WorldRenderModel model, int layer, bool floors, bool slabsDropped,
            out CellRef cell, out float hitAt, out bool thing)
        {
            cell = default;
            hitAt = float.MaxValue;
            thing = false;
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

                if (model.OccludesFace(index) || model.EdificeDef(index) == CoreContent.EdificeDoor)
                {
                    cell = new CellRef(x, z, layer);
                    thing = model.EdificeDef(index) != 0;

                    // The distance has to be the drawn surface, not the clip. A ray coming in
                    // steeply meets a solid cell at the slab entry, and that entry was opened up
                    // by the relief's reach a few lines above -- so an unclamped answer would put
                    // this cell up to half a metre nearer the camera than it is drawn.
                    //
                    // **It is what keeps a tree clickable on rolling ground.** The tree's cell and
                    // the ground block under it are one face at one distance, and the tie is
                    // broken in the tree's favour; half a metre of slack turns that tie into a
                    // win for the ground and the tree cannot be marked. Flat ground hides it --
                    // with relief off the reach is nought and the two agree anyway -- and the
                    // board that is played is not flat.
                    hitAt = ray.direction.y < 0f
                        ? Mathf.Max(t, FloorCrossing(ray, slabMax + FloorHeightAt(x, z)))
                        : t;
                    return true;
                }

                float floorY = slabMin + FloorHeightAt(x, z);

                // **A thing you walk over is still a thing you can see the top of.** A bed does
                // not occlude, so before this the only surface it offered a ray was the floor
                // underneath it — and at the play camera's 48° a surface 0.70 m up is drawn a
                // quarter of a cell nearer the viewer than the floor it stands on. Measured, on
                // 2026-09-19: aiming at the far end of a drawn bed picked the grass behind it,
                // aiming at its near half picked its other cell, and aiming at the grass in
                // front picked the bed. The owner's words were that clicking a bed "seems to be
                // really specific".
                //
                // So a standing edifice offers its own top plane as well, and offers it first
                // because it is the nearer of the two. The cell is claimed only where the ray
                // crosses that plane *inside this cell's own footprint*, which is what the two
                // bounds say — so a bed shadows the sliver of ground behind it exactly as it is
                // drawn to, and nothing else changes.
                float stand = model.StandHeight(index);
                if (stand > 0f)
                {
                    float tTop = FloorCrossing(ray, floorY + stand);
                    if (tTop >= t - 1e-4f && tTop <= tCellEnd)
                    {
                        cell = new CellRef(x, z, layer);
                        thing = true;
                        hitAt = tTop;
                        return true;
                    }

                    // **And the foot of it, for anything that climbs.** A stair's upper half
                    // begins half a layer above its own floor, so between the plane above and the
                    // floor below there is a band of the drawn flight that neither answers for —
                    // measured, the far two thirds of the upper half, which fell through to the
                    // ground behind. The two planes bracket the climb. Everything that sits on the
                    // floor answers 0 here and is unchanged. WorldRenderModel.StandFoot.
                    float foot = model.StandFoot(index);
                    if (foot > 0f)
                    {
                        float tFoot = FloorCrossing(ray, floorY + foot);
                        if (tFoot >= t - 1e-4f && tFoot <= tCellEnd)
                        {
                            cell = new CellRef(x, z, layer);
                            thing = true;
                            hitAt = tFoot;
                            return true;
                        }
                    }
                }

                // Per cell, against that cell's own drawn floor rather than once against the
                // layer's flat plane. This is the whole of the relief's effect on picking: the
                // ground the player is aiming at is the tilted one, so that is the surface the ray
                // has to meet.
                // `slabsDropped` is RF1's rule and is asked of the cell, not of the layer:
                // the renderer stopped drawing this layer's SLABS and went on drawing its ground,
                // so a cell carrying a slab offers nothing and a hillside beside it still does.
                bool hidden = !floors || (slabsDropped && model.Floor(index) != 0);
                float tFloor = hidden ? float.MaxValue : FloorCrossing(ray, floorY);

                if (tFloor >= t - 1e-4f && tFloor <= tCellEnd
                    && Owner(model, index, layer, out CellRef owner, out thing))
                {
                    cell = owner;
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

        /// <summary>
        /// Which cell owns the floor of this one — the thing the player has actually clicked when
        /// the ray crosses it — and whether that thing is an object rather than bare ground.
        ///
        /// <para>Five answers in order, and the order is the whole rule:</para>
        /// <list type="number">
        /// <item>an edifice standing here: the tree, the bed, whatever it is. It is drawn in this
        /// cell and it is what the player is looking at.</item>
        /// <item>a building site waiting here: the order the player gave, drawn as the thing it
        /// will be. Above the two below it, because a covering sits on a slab and a structural slab
        /// sits on nothing at all.</item>
        /// <item>a built floor slab: also drawn in this cell, so this cell owns it.</item>
        /// <item>water filling the cell: it is drawn here, it is what the player clicked, and the
        /// bed beneath it is not.</item>
        /// <item>solid terrain underneath: <b>the block below</b>, because its top face is the
        /// surface the ray just met. This is the owner's rule — "the tile below it or not at
        /// all" — and it is why clicking the meadow gives the ground rather than the air above
        /// it.</item>
        /// </list>
        ///
        /// <para>Nothing under it at all is a hole, and a click through a hole selects nothing.</para>
        /// </summary>
        static bool Owner(WorldRenderModel model, int index, int layer, out CellRef cell, out bool thing)
        {
            var size = model.Size;
            thing = false;
            cell = default;

            if (model.EdificeDef(index) != 0)
            {
                cell = size.FromIndex(index);
                thing = true;
                return true;
            }

            // A waiting order is a thing, and it stands in its own cell.
            //
            // **This is not the ghost rule being bent; it is the ghost rule being read properly.**
            // ADR 0006's argument is that a translucent *hint* must not be a pointer target,
            // because clicking a cue is the accident. A site is not a cue: it has a cell, a
            // material, a progress and a save record, the colony is walking to it, and a player
            // pointing at one is pointing at the order they gave. The build cursor's own ghost is
            // still untouchable, which is the half that was load-bearing.
            //
            // **Above the floor rule and above the block below**, and both matter. A covering laid
            // over an existing slab would otherwise answer with the slab it is going on top of, and
            // a structural slab ordered into open air has no floor and nothing underneath it at all
            // — which is the case that had the owner's whole layer answering nothing on 2026-09-18.
            // The floor plane of this cell is the surface the ray meets either way; all that was
            // missing was somebody to own it.
            if (model.HasSite(index))
            {
                cell = size.FromIndex(index);
                thing = true;
                return true;
            }

            if (model.Floor(index) != 0)
            {
                cell = size.FromIndex(index);
                return true;
            }

            // Water is drawn filling this cell and is not solid, so without this rule the bed
            // beneath it would own the click. It is the water the player is looking at and it is
            // the water the pane has something to say about. A slab over the water has already
            // claimed the click above: a bridge is walked on, not waded through.
            if (NaturalContent.IsWater(model.Terrain(index)))
            {
                cell = size.FromIndex(index);
                return true;
            }

            if (layer > 0 && model.IsSolid(index - size.LayerStride))
            {
                cell = size.FromIndex(index - size.LayerStride);
                return true;
            }

            return false;
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
