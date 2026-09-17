#nullable enable

using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The two sheets a body of water is drawn from: the surface it presents to the sky, and the
    /// face it shows where there is nothing beside it to hold it in.
    ///
    /// <para><b>Sheets, not boxes, and that is the whole point of this class.</b> Water used to be
    /// drawn with the shared unit cube squashed to a 0.15 m slab, which gave every water cell four
    /// side faces and an underside that nothing wanted. Opaque geometry gets away with that because
    /// the neighbouring tile hides them; water writes no depth (ADR 0009, decision 8), so the
    /// coincident side faces of two tiles either side of a shared edge each added their own alpha
    /// and the board came out ruled into dark squares along every cell boundary.
    /// <c>OdysseyWater.shader</c> answered that by clipping every fragment whose geometric normal
    /// was not pointing up — one instruction, and it worked, and it also made a vertical face of
    /// water <i>undrawable</i>. A surface with no sides needs no clip and cannot rule the board,
    /// so the sheet is the fix the clip was standing in for.</para>
    ///
    /// <para>Both are one quad. There is no variant table and no cache key beyond the two of them,
    /// because water has a shader rather than art (ADR 0009, decision 8) and a clone without the
    /// licensed packs therefore draws exactly the same water as a machine with them.</para>
    /// </summary>
    public static class WaterMesh
    {
        static Mesh? _surface;
        static Mesh? _fall;

        /// <summary>
        /// The surface: a unit square in plan at local <c>y = 0</c>, facing up.
        ///
        /// <para>At <c>y = 0</c> rather than standing on a slab of its own thickness, so the water
        /// lies exactly at <see cref="ChunkMesher.WaterSurface"/> and not 0.15 m over it. The old
        /// slab's top face was the surface the player saw, so the drawn water drops by that 0.15 m
        /// — a twentieth of a cell — to the height the constant has always claimed for it.</para>
        /// </summary>
        public static Mesh Surface =>
            _surface != null ? _surface : (_surface = Quad(
                "Odyssey/WaterSurface", Vector3.up, Vector3.right, Vector3.zero));

        /// <summary>
        /// The face: a unit square standing in the local <c>xy</c> plane, hanging from
        /// <c>y = 0</c> down to <c>y = -1</c> and facing local <c>+z</c>.
        ///
        /// <para>It hangs <i>downwards from its origin</i> because that is what the caller knows:
        /// the surface height is where the water is, and how far the sheet falls is whatever is
        /// below it — its own bed, or the surface of the water in the next cell down. One local
        /// unit is one metre of fall, so the instance matrix supplies the drop as a plain Y scale
        /// and this mesh never has to be rebuilt for a new height.</para>
        ///
        /// <para>It faces local <c>+z</c> because <see cref="Directions.Yaw"/> is defined to turn
        /// local <c>+z</c> onto a bearing, so after the yaw the sheet faces the neighbour it is
        /// being shown to — outwards, away from the water.</para>
        /// </summary>
        public static Mesh Fall =>
            _fall != null ? _fall : (_fall = Quad(
                "Odyssey/WaterFall", Vector3.forward, Vector3.right, new Vector3(0f, -0.5f, 0f)));

        /// <summary>Drop the built meshes. For tests, which must not inherit each other's.</summary>
        public static void Invalidate()
        {
            _surface = null;
            _fall = null;
        }

        /// <summary>
        /// One quad, one unit across each way, centred on <paramref name="centre"/> and facing
        /// <paramref name="normal"/>.
        ///
        /// <para>The winding is <see cref="PrimitiveMeshes"/>'s, deliberately and to the letter:
        /// the corners go anticlockwise about the outward normal, so the triangles are taken
        /// <c>0-2-1</c> and <c>0-3-2</c> rather than the obvious order. Taking them the obvious way
        /// builds a quad that faces backwards, which is valid geometry that reports no error and
        /// simply is not there when you look at it — the fault that once turned every primitive in
        /// the renderer inside out.</para>
        /// </summary>
        static Mesh Quad(string name, Vector3 normal, Vector3 along, Vector3 centre)
        {
            Vector3 v = Vector3.Cross(normal, along);

            var vertices = new[]
            {
                centre - along * 0.5f - v * 0.5f,
                centre - along * 0.5f + v * 0.5f,
                centre + along * 0.5f + v * 0.5f,
                centre + along * 0.5f - v * 0.5f,
            };

            var mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.normals = new[] { normal, normal, normal, normal };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 0f),
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            mesh.UploadMeshData(markNoLongerReadable: false);
            return mesh;
        }
    }
}
