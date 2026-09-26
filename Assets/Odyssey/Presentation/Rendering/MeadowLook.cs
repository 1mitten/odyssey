#nullable enable
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The pieces of the Meadow Forest pack the look pass draws the ground and grades the frame
    /// with (<c>docs/design/38-meadow-overhaul.md</c> §17): six terrain textures and the demo
    /// scene's URP volume profile.
    ///
    /// <para><b>An asset of references, like the module catalogue.</b> The textures live under the
    /// gitignored <c>Assets/Synty</c>; this committed asset points at them by GUID and is loaded
    /// from <c>Resources</c>, so a player build carries them and nothing in the scene has to change.
    /// On a clone without the packs every reference resolves to null, <see cref="HasGround"/> is
    /// false, and the ground draws with the stock material exactly as it did before — the designed
    /// degradation every licensed asset here follows. Rebuilt by
    /// <c>Odyssey &gt; Presentation &gt; Rebuild meadow look</c> on a machine with the packs.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Odyssey/Meadow look", fileName = "MeadowLook")]
    public sealed class MeadowLook : ScriptableObject
    {
        /// <summary>Where <see cref="Loaded"/> looks, under a <c>Resources</c> folder.</summary>
        public const string ResourcePath = "OdysseyLook/MeadowLook";

        public Texture2D? grassA;
        public Texture2D? grassB;
        public Texture2D? clover;
        public Texture2D? flowers;
        public Texture2D? leaves;
        public Texture2D? earth;

        /// <summary>The marsh round streams and ponds, blended over the meadow by the ground field
        /// rather than drawn cell by cell (design 38 §24). Optional: without it a marsh cell keeps
        /// its own tiled texture.</summary>
        public Texture2D? wet;

        /// <summary>Whether marsh is painted into the meadow by the ground field. On the same switch
        /// as the shoreline, so turning it off gives back the old look whole.</summary>
        public static bool PaintsMarsh => WaterShore.Enabled && GroundActive && Loaded!.wet != null;

        /// <summary>The Meadow demo scene's own URP volume profile: its grade, not ours.</summary>
        public VolumeProfile? grade;

        /// <summary>
        /// Whether the grass terrain is drawn by <c>Odyssey/MeadowGround</c>. On unless a
        /// measurement arm wants the stock ground to compare against; read when a module library
        /// resolves the grass terrain, so changing it takes a new session.
        /// </summary>
        public static bool GroundEnabled
        {
            get => _groundEnabled;
            set { _groundEnabled = value; _active = null; }
        }

        static bool _groundEnabled = true;
        static bool? _active;

        static MeadowLook? _loaded;
        static bool _tried;

        /// <summary>The committed asset, or null where it is missing.</summary>
        public static MeadowLook? Loaded
        {
            get
            {
                if (!_tried)
                {
                    _tried = true;
                    _loaded = Resources.Load<MeadowLook>(ResourcePath);
                }
                return _loaded;
            }
        }

        /// <summary>True when every texture the ground shader needs resolved to real art.</summary>
        public bool HasGround =>
            grassA != null && grassB != null && clover != null && flowers != null && leaves != null && earth != null;

        /// <summary>
        /// Whether the painted ground is what this session draws: enabled, the art present, and
        /// the shader in the build. Everything that depends on the swap — the grass tint, the
        /// material — asks this one question.
        ///
        /// <para>Asked for every ground bucket every frame (through the grass tint), so it is
        /// worked out once and kept: <c>Shader.Find</c> is a name lookup, not a field read.</para>
        /// </summary>
        public static bool GroundActive => _active ??=
            GroundEnabled && Loaded != null && Loaded.HasGround && Shader.Find("Odyssey/MeadowGround") != null;

        /// <summary>
        /// A new material for the painted ground, or null when <see cref="GroundActive"/> is false.
        /// The caller owns it and destroys it; the tint cache clones it per tint as it does any
        /// ground material.
        /// </summary>
        public static Material? NewGroundMaterial() => GroundActive ? NewGroundMaterial(Loaded!) : null;

        /// <summary>The painted-ground material for a given look, or null when it has no ground or
        /// the shader is missing. The overload tests use, since the committed asset is optional.</summary>
        public static Material? NewGroundMaterial(MeadowLook look)
        {
            Shader? shader = Shader.Find("Odyssey/MeadowGround");
            if (shader == null || !look.HasGround) return null;
            var material = new Material(shader)
            {
                name = "Odyssey_MeadowGround",
                enableInstancing = true,
            };
            material.SetTexture(GrassAId, look.grassA);
            material.SetTexture(GrassBId, look.grassB);
            material.SetTexture(CloverId, look.clover);
            material.SetTexture(FlowersId, look.flowers);
            material.SetTexture(LeavesId, look.leaves);
            material.SetTexture(EarthId, look.earth);
            if (look.wet != null) material.SetTexture(WetId, look.wet);
            return material;
        }

        /// <summary>
        /// One terrain texture drawn the meadow's way — projected from world position, with the
        /// slow drift over it — for every natural terrain that is not grass: earth, gravel, mud,
        /// marsh, sand, rock, the ore seams (design 38 §17c). Null when the look is not active, and
        /// the caller keeps the pack's own material.
        ///
        /// <para>Why every terrain and not only the pretty one: the ink line leaves a pixel alone
        /// when its visible surface wrote the terrain mark, and only this shader writes it. A sand
        /// bed on the pack's material kept a black line along every step of a stream.</para>
        ///
        /// <para><paramref name="top"/> null is a flat surface, coloured wholly by the tint the
        /// renderer resolves — which is how sand and the ore seams, which have no texture of their
        /// own, were already drawn.</para>
        /// </summary>
        public static Material? NewPlainGroundMaterial(Texture? top, float tileMetres)
        {
            if (!GroundActive) return null;
            Shader? shader = Shader.Find("Odyssey/MeadowGround");
            if (shader == null) return null;
            var material = new Material(shader)
            {
                name = "Odyssey_Ground_" + (top != null ? top.name : "plain"),
                enableInstancing = true,
            };
            material.SetFloat(SingleId, 1f);
            material.SetFloat(TileMetresId, Mathf.Max(0.5f, tileMetres));
            material.SetTexture(GrassAId, top != null ? top : Texture2D.whiteTexture);
            material.SetTexture(EarthId, top != null ? top : Texture2D.whiteTexture);
            return material;
        }

        static readonly int SingleId = Shader.PropertyToID("_Single");
        static readonly int TileMetresId = Shader.PropertyToID("_TileMetres");
        static readonly int GrassAId = Shader.PropertyToID("_GrassA");
        static readonly int GrassBId = Shader.PropertyToID("_GrassB");
        static readonly int CloverId = Shader.PropertyToID("_Clover");
        static readonly int FlowersId = Shader.PropertyToID("_Flowers");
        static readonly int LeavesId = Shader.PropertyToID("_Leaves");
        static readonly int EarthId = Shader.PropertyToID("_Earth");
        static readonly int WetId = Shader.PropertyToID("_Wet");
    }
}
