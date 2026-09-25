#nullable enable
using System;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Where the grass is pushed back: a top-down field over a window around the camera, into
    /// which anything that must not be hidden stamps itself, read by the grass shader at each
    /// clump's root.
    ///
    /// <para><b>Why this exists at all.</b> The owner asked for grass that never hides what is
    /// lying on the floor (`grass-interview.md`, answer 2), and the obvious place to do that — the
    /// mesher, which already refuses to strew grass under a floor or inside a zone — cannot. The
    /// mesher reads the <em>render mirror</em>, a cell-indexed copy rebuilt only when a chunk is
    /// dirtied, and the mirror carries terrain, floors, zones and edifices but <b>not items and not
    /// designations</b>. Those live in the per-frame snapshot, and <see cref="GroundScatter"/>'s own
    /// comment already says why they must stay there: re-meshing a chunk every time somebody walked
    /// across it would be a far worse cure than the disease.</para>
    ///
    /// <para>So the split is by <em>how often a thing moves</em>, not by what it is. Buildings and
    /// walls are static and in the mirror, so the mesher clears around them for nothing. Items and
    /// order marks move, so they come through here.</para>
    ///
    /// <para><b>The mechanism is worth more than the feature it was built for.</b> The same field
    /// is what parted grass needs — blades leaning away rather than vanishing — and what worn paths
    /// need, both of which the owner set aside as units of their own. Building it once buys all
    /// three, and that is the argument that justified its cost.</para>
    ///
    /// <para><b>Built on the CPU and uploaded, rather than splatted into a render target.</b> The
    /// stamps are a few hundred small discs a frame into sixty-four kilobytes, which is nothing next
    /// to a render target's bind, clear and pass. The decisive reason is a different one:
    /// <b>a byte array is ordinary logic and can be asserted in EditMode</b>, where a render target
    /// can only be photographed. Every rule this field encodes — how wide a ring is, whether it
    /// follows the camera, what happens at the window's edge — is a test rather than a screenshot.
    /// </para>
    ///
    /// <para><b>Nothing here is simulation.</b> The field is a pure function of where things are
    /// this frame: not saved, not hashed, unreadable from the simulation. Grass has never been in a
    /// cell and this does not put it there.</para>
    /// </summary>
    public sealed class GrassClearance : IDisposable
    {
        /// <summary>
        /// Texels across the window. 256 over <see cref="WindowMetres"/> is 0.375 m a texel, which
        /// is what a "tight" ring of half a metre needs to be a disc rather than a square — the
        /// owner asked for the ring to clear the item and no more (answer 12).
        /// </summary>
        public const int Resolution = 256;

        /// <summary>
        /// How much ground the window covers, in metres. Wide enough for the play camera's view at
        /// a normal zoom and no wider: everything outside reads as uncleared, and grass outside the
        /// window is grass the camera is not looking at.
        /// </summary>
        public const float WindowMetres = 96f;

        /// <summary>Metres a texel. The smallest ring that can be drawn is about twice this.</summary>
        public const float MetresPerTexel = WindowMetres / Resolution;

        static readonly int FieldId = Shader.PropertyToID("_OdysseyClearTex");
        static readonly int WindowId = Shader.PropertyToID("_OdysseyClear");

        readonly byte[] _field = new byte[Resolution * Resolution];
        Texture2D? _texture;

        /// <summary>World x and z of the corner of texel (0, 0).</summary>
        public Vector2 Origin { get; private set; }

        /// <summary>How many stamps went in this frame. For the debug readout and for tests.</summary>
        public int Stamps { get; private set; }

        /// <summary>
        /// Start a frame: clear the field and place the window on the camera.
        ///
        /// <para><b>The origin snaps to a whole texel</b>, and that is not tidiness. Without it the
        /// field slides continuously under the world as the camera pans, so every clump's sample
        /// point drifts across texel boundaries and the grass at the edge of a ring flickers in and
        /// out — a shimmer that would read as the grass being broken rather than as the window
        /// moving.</para>
        /// </summary>
        public void Begin(Vector3 focus)
        {
            Array.Clear(_field, 0, _field.Length);
            Stamps = 0;

            float half = WindowMetres * 0.5f;
            Origin = new Vector2(
                Mathf.Floor((focus.x - half) / MetresPerTexel) * MetresPerTexel,
                Mathf.Floor((focus.z - half) / MetresPerTexel) * MetresPerTexel);
        }

        /// <summary>Push the grass back in a disc about a point. Silently ignores anything outside
        /// the window, which is most of the board.</summary>
        public void Stamp(Vector3 at, float radiusMetres)
        {
            if (StampInto(_field, Origin, new Vector2(at.x, at.z), radiusMetres)) Stamps++;
        }

        /// <summary>
        /// The whole rule, as a pure function so a test can state it.
        ///
        /// <para>Returns whether anything was written, which is how <see cref="Stamps"/> counts
        /// only the stamps that landed — a count that included everything asked for would say a
        /// field was busy when the camera was looking somewhere else entirely.</para>
        ///
        /// <para>The disc is soft at its rim over one texel, because a hard edge on a field this
        /// coarse is a visible circle of full-height grass meeting bare ground. Squared distance
        /// throughout: no square roots in the inner loop, and the falloff is shaped by the compare
        /// rather than by the metric.</para>
        /// </summary>
        public static bool StampInto(byte[] field, Vector2 origin, Vector2 at, float radiusMetres)
        {
            if (radiusMetres <= 0f) return false;

            float localX = (at.x - origin.x) / MetresPerTexel;
            float localZ = (at.y - origin.y) / MetresPerTexel;
            float radius = radiusMetres / MetresPerTexel;

            int lowX = Mathf.FloorToInt(localX - radius);
            int highX = Mathf.CeilToInt(localX + radius);
            int lowZ = Mathf.FloorToInt(localZ - radius);
            int highZ = Mathf.CeilToInt(localZ + radius);

            if (highX < 0 || lowX >= Resolution || highZ < 0 || lowZ >= Resolution) return false;

            lowX = Mathf.Max(lowX, 0);
            lowZ = Mathf.Max(lowZ, 0);
            highX = Mathf.Min(highX, Resolution - 1);
            highZ = Mathf.Min(highZ, Resolution - 1);

            float inner = Mathf.Max(radius - 1f, 0f);
            bool wrote = false;

            for (int z = lowZ; z <= highZ; z++)
            {
                float dz = z + 0.5f - localZ;
                int row = z * Resolution;
                for (int x = lowX; x <= highX; x++)
                {
                    float dx = x + 0.5f - localX;
                    float distance = Mathf.Sqrt(dx * dx + dz * dz);
                    if (distance > radius) continue;

                    float strength = radius <= inner ? 1f
                        : Mathf.Clamp01((radius - distance) / Mathf.Max(radius - inner, 1e-4f));
                    var value = (byte)Mathf.RoundToInt(strength * 255f);

                    // Kept, not added: two items beside each other should clear their own ground
                    // and not gouge a deeper hole where their discs happen to overlap.
                    if (value > field[row + x])
                    {
                        field[row + x] = value;
                        wrote = true;
                    }
                }
            }

            return wrote;
        }

        /// <summary>How cleared a point is, 0 to 1. The shader's own read, in C#, for tests.</summary>
        /// <summary>
        /// Lay the grass flat over a rectangle in X and Z, at full strength inside it and falling
        /// off over <paramref name="margin"/> metres outside (design 45 §13): what a placement's
        /// footprint clears, one stamp however many cells it covers. True if anything was written.
        /// </summary>
        public bool StampRect(Vector2 low, Vector2 high, float margin)
        {
            if (!StampRectInto(_field, Origin, low, high, margin)) return false;
            Stamps++;
            return true;
        }

        /// <summary>The same, into any field: the pure half, for tests.</summary>
        public static bool StampRectInto(byte[] field, Vector2 origin, Vector2 low, Vector2 high, float margin)
        {
            float edge = Mathf.Max(margin, 1e-3f) / MetresPerTexel;
            float lx = (low.x - origin.x) / MetresPerTexel, lz = (low.y - origin.y) / MetresPerTexel;
            float hx = (high.x - origin.x) / MetresPerTexel, hz = (high.y - origin.y) / MetresPerTexel;

            int x0 = Mathf.Max(Mathf.FloorToInt(lx - edge), 0), x1 = Mathf.Min(Mathf.CeilToInt(hx + edge), Resolution - 1);
            int z0 = Mathf.Max(Mathf.FloorToInt(lz - edge), 0), z1 = Mathf.Min(Mathf.CeilToInt(hz + edge), Resolution - 1);
            if (x0 > x1 || z0 > z1) return false;

            // The inside is written flat, a row at a time, and only the margin band pays for a
            // distance: a large drag is tens of thousands of texels, and a square root each was two
            // milliseconds of a frame (design 45 §13, measured).
            int ix0 = Mathf.Max(Mathf.CeilToInt(lx - 0.5f), x0), ix1 = Mathf.Min(Mathf.FloorToInt(hx - 0.5f), x1);
            bool wrote = false;
            for (int z = z0; z <= z1; z++)
            {
                float pz = z + 0.5f;
                float dz = pz < lz ? lz - pz : pz > hz ? pz - hz : 0f;
                if (dz > edge) continue;
                int row = z * Resolution;
                bool insideRow = dz == 0f;
                for (int x = x0; x <= x1; x++)
                {
                    if (insideRow && x >= ix0 && x <= ix1)
                    {
                        for (; x <= ix1; x++)
                            if (field[row + x] != 255) { field[row + x] = 255; wrote = true; }
                        x = ix1;
                        continue;
                    }
                    float px = x + 0.5f;
                    float dx = px < lx ? lx - px : px > hx ? px - hx : 0f;
                    float outside = dx == 0f ? dz : dz == 0f ? dx : Mathf.Sqrt(dx * dx + dz * dz);
                    if (outside > edge) continue;
                    var value = (byte)(255f * (1f - outside / edge) + 0.5f);
                    if (value > field[row + x])
                    {
                        field[row + x] = value;
                        wrote = true;
                    }
                }
            }
            return wrote;
        }

        public float At(Vector3 world)
        {
            int x = Mathf.FloorToInt((world.x - Origin.x) / MetresPerTexel);
            int z = Mathf.FloorToInt((world.z - Origin.y) / MetresPerTexel);
            if (x < 0 || x >= Resolution || z < 0 || z >= Resolution) return 0f;
            return _field[z * Resolution + x] / 255f;
        }

        /// <summary>Hand the field to the shaders. Cheap enough to call every frame, and it is.</summary>
        public void Publish()
        {
            if (_texture == null)
            {
                _texture = new Texture2D(Resolution, Resolution, TextureFormat.R8, mipChain: false, linear: true)
                {
                    name = "Odyssey/GrassClearance",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }

            _texture.SetPixelData(_field, 0);
            _texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            Shader.SetGlobalTexture(FieldId, _texture);
            Shader.SetGlobalVector(WindowId,
                new Vector4(Origin.x, Origin.y, 1f / WindowMetres, 1f));
        }

        /// <summary>
        /// Switch the field off and give the texture back.
        ///
        /// <para>The <c>w</c> of the window vector is the shader's "there is a field at all" flag,
        /// and it is zeroed here rather than merely letting the texture go. A global outlives the
        /// object that set it: without this, the next thing this editor drew — a contact sheet, a
        /// portrait, an asset preview — would sample a destroyed texture through a window that
        /// still claimed to be valid.</para>
        /// </summary>
        public void Dispose()
        {
            Shader.SetGlobalVector(WindowId, Vector4.zero);
            Shader.SetGlobalTexture(FieldId, Texture2D.blackTexture);

            if (_texture == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(_texture);
            else UnityEngine.Object.DestroyImmediate(_texture);
            _texture = null;
        }
    }
}
