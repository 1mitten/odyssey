#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// <b>Where a bird can sit, answered from the render mirror</b> (design 50 §6), so a rook lands on
    /// what is drawn: the top of a tree's crown, or a roof.
    ///
    /// <para><b>The column is the sky rule's</b> (<see cref="SkyColumnRule.Walk{T}"/>, design 43 §6),
    /// the same walk the rain stops on. A walk that meets a trunk is a tree, and its perch is the top
    /// of the crown as <see cref="TreeArt.CrownOf"/> places the art, not a guessed height. A walk that
    /// lands on a slab with air beneath it is a roof. A floor laid on the ground is also a slab to the
    /// rain, but it is not a perch, or a flock would come down and sit on a patio.</para>
    ///
    /// <para>A perch <see cref="Holds"/> while its tree or roof is still there and its layer is
    /// still drawn. The director tells this class the highest layer the slice draws each frame, so a
    /// rook is never left sitting on a roof the slice has cut away. The flock asks twice a second.</para>
    /// </summary>
    public sealed class BirdPerches : IBirdPerches
    {
        /// <summary>A crown's height above its trunk's floor when there is no art to measure (no packs).</summary>
        public const float FallbackCrown = 6f;

        /// <summary>The farthest from a crown's middle a bird may sit, whatever the art's reach.</summary>
        public const float MaxCrownRadius = 3f;

        readonly WorldRenderModel _model;
        readonly Dictionary<int, int[]> _variants = new Dictionary<int, int[]>();
        readonly List<(float Distance, BirdPerch Perch)> _candidates = new List<(float, BirdPerch)>();

        public BirdPerches(WorldRenderModel model) => _model = model;

        /// <summary>The highest layer the slice draws this frame. Nothing above it is a perch.</summary>
        public int HighestVisibleLayer { get; set; } = int.MaxValue;

        public int Near(float x, float z, float radius, BirdPerch[] into)
        {
            _candidates.Clear();
            var size = _model.Size;
            int cx = Mathf.FloorToInt(x / CellMetrics.SizeXZ), cz = Mathf.FloorToInt(z / CellMetrics.SizeXZ);
            int reach = Mathf.CeilToInt(radius / CellMetrics.SizeXZ);
            // Every other column past forty metres: a rookery search is once a night, and a crown
            // covers more than one column anyway.
            int stride = radius > 40f ? 2 : 1;
            float r2 = radius * radius;

            for (int dz = -reach; dz <= reach; dz += stride)
            for (int dx = -reach; dx <= reach; dx += stride)
            {
                int px = cx + dx, pz = cz + dz;
                if (px < 0 || pz < 0 || px >= size.SizeX || pz >= size.SizeZ) continue;
                if (!TryPerchAt(px, pz, out BirdPerch perch)) continue;
                float ex = perch.X - x, ez = perch.Z - z;
                float d2 = ex * ex + ez * ez;
                if (d2 > r2) continue;
                _candidates.Add((d2, perch));
            }

            _candidates.Sort((a, b) => a.Distance != b.Distance
                ? a.Distance.CompareTo(b.Distance)
                : a.Perch.Key.CompareTo(b.Perch.Key));
            int n = Mathf.Min(into.Length, _candidates.Count);
            for (int i = 0; i < n; i++) into[i] = _candidates[i].Perch;
            return n;
        }

        public float Ceiling(float x, float z)
        {
            var size = _model.Size;
            int cx = Mathf.Clamp(Mathf.FloorToInt(x / CellMetrics.SizeXZ), 0, size.SizeX - 1);
            int cz = Mathf.Clamp(Mathf.FloorToInt(z / CellMetrics.SizeXZ), 0, size.SizeZ - 1);
            SkyColumn landing = SkyColumnRule.Walk(new MirrorSkySource(_model), cx, cz, out int trunk);
            float top = SkyHeightMap.Metres(landing, out _);
            if (trunk >= 0) top = Mathf.Max(top, CrownTop(cx, cz, trunk, out _, out _));
            return top;
        }

        public bool Holds(in BirdPerch perch)
        {
            Decode(perch.Key, out int x, out int z, out int layer, out bool tree);
            var size = _model.Size;
            if (x < 0 || z < 0 || layer < 0 || x >= size.SizeX || z >= size.SizeZ || layer >= size.SizeY) return false;
            if (layer > HighestVisibleLayer) return false;
            int index = _model.Index(x, z, layer);
            return tree ? NaturalContent.IsTree(_model.EdificeDef(index)) : IsRoof(x, z, layer);
        }

        /// <summary>The perch over column (x, z), if it has one: a crown or a roof, and never the ground.</summary>
        public bool TryPerchAt(int x, int z, out BirdPerch perch)
        {
            perch = default;
            SkyColumn landing = SkyColumnRule.Walk(new MirrorSkySource(_model), x, z, out int trunk);

            if (trunk >= 0)
            {
                if (trunk > HighestVisibleLayer) return false;
                float top = CrownTop(x, z, trunk, out Vector3 crown, out float reach);
                float radius = Mathf.Clamp(reach * 0.6f, 0.5f, MaxCrownRadius);
                perch = new BirdPerch(crown.x, top, crown.z, radius, Encode(x, z, trunk, tree: true));
                return true;
            }

            if (landing.Kind == SkyStop.Built && landing.StopLayer <= HighestVisibleLayer &&
                IsRoof(x, z, landing.StopLayer))
            {
                Vector3 centre = CellMetrics.FloorCentre(x, z, landing.StopLayer);
                float y = SkyHeightMap.Metres(landing, out _);
                perch = new BirdPerch(centre.x, y, centre.z, CellMetrics.HalfXZ * 0.7f,
                    Encode(x, z, landing.StopLayer, tree: false));
                return true;
            }

            return false;
        }

        /// <summary>The crown's top in metres, through the art where it resolved and a stand-in where not.</summary>
        float CrownTop(int x, int z, int trunk, out Vector3 crown, out float reach)
        {
            int index = _model.Index(x, z, trunk);
            ushort def = _model.EdificeDef(index);
            int module = _model.EdificeModule(index);
            if (module != 0)
            {
                if (!_variants.TryGetValue(module, out int[]? variants))
                {
                    variants = TreeArt.VariantsOf(_model.Library, module);
                    _variants[module] = variants;
                }
                if (TreeArt.CrownOf(_model.Library, variants, def, x, z, trunk, out crown, out reach))
                    return crown.y;
            }

            crown = CellMetrics.FloorCentre(x, z, trunk) + Vector3.up * FallbackCrown;
            reach = 2.5f;
            return crown.y;
        }

        /// <summary>A slab with open air under it: a roof, not a floor laid on the ground.</summary>
        bool IsRoof(int x, int z, int layer)
        {
            if (layer <= 0) return false;
            if (_model.Floor(_model.Index(x, z, layer)) == 0) return false;
            return !_model.IsSolid(_model.Index(x, z, layer - 1));
        }

        int Encode(int x, int z, int layer, bool tree)
        {
            var size = _model.Size;
            return (((layer * size.SizeZ) + z) * size.SizeX + x) * 2 + (tree ? 1 : 0);
        }

        void Decode(int key, out int x, out int z, out int layer, out bool tree)
        {
            var size = _model.Size;
            tree = (key & 1) != 0;
            int rest = key >> 1;
            x = rest % size.SizeX;
            rest /= size.SizeX;
            z = rest % size.SizeZ;
            layer = rest / size.SizeZ;
        }
    }
}
