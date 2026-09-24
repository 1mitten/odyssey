#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Hud;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The hair and beards a colonist can wear, resolved once and worn by every drawer
    /// (<c>docs/design/29-modular-colonists.md</c>, MC5).
    ///
    /// <para><b>This exists because three separate things draw a colonist and they must agree.</b>
    /// The live animated figures, the baked instanced form past the figure cap, and
    /// <see cref="PortraitStudio"/>, which photographs the roster card. That is the same argument
    /// <see cref="ColonistAppearanceBook"/> makes about the face and <c>NavGraph.HopCost</c> makes
    /// about a hop, and the failure is the same shape: a colonist with a beard in the world and a
    /// clean-shaven portrait on their own card, with both halves individually correct.</para>
    ///
    /// <para><b>A piece is one rigid mesh parented to the head bone with an identity
    /// transform.</b> Measured, in both packs (<c>docs/research/e-06-modular-colonists.md</c> §4):
    /// there is no offset to fit and no per-body special case.</para>
    ///
    /// <para><b>Missing is ordinary.</b> A pawn dealt no piece, an index out of range and a row
    /// whose art did not resolve are all the same thing here — the renderer is switched off. That
    /// is what lets a clone without the licensed packs draw a whole colony of bald colonists
    /// rather than throwing.</para>
    /// </summary>
    public sealed class ColonistAttachments
    {
        /// <summary>
        /// Off, no colonist wears hair or a beard — near or far.
        ///
        /// <para><b>A measurement control, not a setting.</b>
        /// <c>docs/design/06-rendering-and-camera.md</c> §6c.1 is emphatic that a pass is judged
        /// against the same run with it switched off, because this machine's frame numbers drift
        /// by more between runs than most passes cost. <c>ChunkRenderer.SubmitToGpu</c> exists for
        /// the same reason. Nothing in the game writes it; the frame tests do, and put it back.</para>
        /// </summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>One piece, resolved to the three things dressing a figure in it needs.</summary>
        public readonly struct Piece
        {
            public Piece(Mesh? mesh, Material? material, AppearanceCells? cells)
            {
                Mesh = mesh;
                Material = material;
                Cells = cells;
            }

            public readonly Mesh? Mesh;
            public readonly Material? Material;

            /// <summary>
            /// Which rectangle of the atlas to repaint, written by <c>CharacterSwatches</c>.
            ///
            /// <para>It is the <c>hair</c> slot for a beard as well as for hair, which is what
            /// makes a beard the hair colour <i>exactly</i> and unable to drift from it: one
            /// rectangle, one colour, both slots painted by the same number.</para>
            /// </summary>
            public readonly AppearanceCells? Cells;

            public bool Usable => Mesh != null;
        }

        readonly Piece[] _hair;
        readonly Piece[] _beards;
        readonly Piece[] _headgear;

        /// <summary>
        /// Resolve both families.
        ///
        /// <para>Eagerly, unlike the baked colonist bodies which are resolved on first use: there
        /// are a couple of dozen of these and each is one small mesh already loaded with its
        /// prefab, where a body costs a rigged instantiation and a bake. The asymmetry is
        /// deliberate rather than an oversight.</para>
        /// </summary>
        public ColonistAttachments(ModuleCatalogue? catalogue)
        {
            _hair = Resolve(catalogue, ModuleIds.HairBase);
            _beards = Resolve(catalogue, ModuleIds.BeardBase);
            _headgear = Resolve(catalogue, ModuleIds.HeadgearBase);
        }

        /// <summary>How many hair pieces there are, so a drawer can size a bucket per piece.</summary>
        public int HairCount => _hair.Length;

        /// <summary>How many beards there are.</summary>
        public int BeardCount => _beards.Length;

        /// <summary>How many pieces of headgear there are (design 42).</summary>
        public int HeadgearCount => _headgear.Length;

        public Piece Hair(int index) => At(_hair, index);

        public Piece Beard(int index) => At(_beards, index);

        /// <summary>
        /// A piece of headgear. Its rows carry no appearance, so <see cref="Piece.Cells"/> is null
        /// and <see cref="Wear"/> puts it on in the pack's own paint — the helmet is metal, and
        /// the owner chose it as painted (design 42 §2).
        /// </summary>
        public Piece Headgear(int index) => At(_headgear, index);

        /// <summary>
        /// Switch on the skinned overlay a row names — the bandit's vest — after
        /// <see cref="BareTheHead"/> has switched every <c>_Attach_</c> child off. The overlays
        /// are children of the rig and deform with it, so turning one on is the whole of wearing
        /// it. Returns the renderer, or null when the row names none or the rig has no such child.
        /// </summary>
        public static SkinnedMeshRenderer? ShowOverlay(GameObject instance, string overlayName)
        {
            if (string.IsNullOrEmpty(overlayName)) return null;
            foreach (SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true))
            {
                if (!string.Equals(skin.gameObject.name, overlayName, StringComparison.Ordinal)) continue;
                skin.gameObject.SetActive(true);
                return skin;
            }
            return null;
        }

        static Piece At(Piece[] pieces, int index) =>
            (uint)index < (uint)pieces.Length ? pieces[index] : default;

        static Piece[] Resolve(ModuleCatalogue? catalogue, string family)
        {
            if (catalogue == null) return Array.Empty<Piece>();

            List<ModuleEntry> rows = catalogue.FindFamily(family);
            var pieces = new Piece[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                GameObject? prefab = rows[i].prefab;
                if (prefab == null) continue;

                var filter = prefab.GetComponentInChildren<MeshFilter>(true);
                var renderer = prefab.GetComponentInChildren<MeshRenderer>(true);
                AppearanceCells cells = rows[i].appearance;
                pieces[i] = new Piece(
                    filter != null ? filter.sharedMesh : null,
                    renderer != null ? renderer.sharedMaterial : null,
                    cells.Any ? cells : null);
            }

            return pieces;
        }

        /// <summary>
        /// Switch off whatever the pack already put on this body's head.
        ///
        /// <para><b>PolygonGeneric ships hair, hats, hoods, headsets, sunglasses and beards as
        /// skinned children of the body, left active</b> — <c>CharacterSwatches</c> records the
        /// same thing, having been caught by it when the largest-mesh rule was written. So a body
        /// dealt one of our hair pieces wore two hairstyles at once, and a colonist could not be
        /// given a bare head at all.</para>
        ///
        /// <para>It is also how the owner's *"anyone with headwear for the time being"* is
        /// answered without losing the body: the cap comes off, the person stays in the colony
        /// (owner, 2026-09-22).</para>
        ///
        /// <para>Matched on <c>_Attach_</c>, which is the packs' own naming for a thing worn
        /// rather than a thing you are. A body child is named after the body — <c>Character_…</c>
        /// or <c>SM_Gen_Chr_Business_Female_01</c> — and <c>Eyes</c> and <c>Eyebrows</c> are named
        /// for themselves, so none of them match. Battle Royale's armour overlays do match and are
        /// already inactive, so switching them off changes nothing.</para>
        /// </summary>
        public static int BareTheHead(GameObject instance)
        {
            int off = 0;
            var renderers = instance.GetComponentsInChildren<Renderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                GameObject go = renderers[i].gameObject;
                if (go.name.IndexOf("_Attach_", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (!go.activeSelf) continue;

                go.SetActive(false);
                off++;
            }

            return off;
        }

        /// <summary>
        /// Hang an empty renderer off the head bone, ready to be dressed.
        ///
        /// <para>It starts disabled and is only switched on when something gives it a mesh, so a
        /// figure built for a bald colonist costs two disabled renderers and nothing else.</para>
        /// </summary>
        public static void MakeSlot(Transform head, string name, int layer,
            out MeshFilter filter, out MeshRenderer renderer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(head, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            filter = go.AddComponent<MeshFilter>();
            renderer = go.AddComponent<MeshRenderer>();
            renderer.enabled = false;

            // Hair on a head that is already lit as one piece. Shadows on a few hundred triangles
            // sitting flush against a scalp buy nothing and cost a pass.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// Put a piece on, or take it off.
        ///
        /// <para>Painted through the same cache and the same rectangles the body is
        /// (<see cref="ColonistMaterials"/>), so the hair a colonist wears is the hair colour they
        /// were dealt rather than a colour that merely agrees with it.</para>
        ///
        /// <para>Assigns <c>sharedMaterial</c> and never <c>material</c>: the latter silently
        /// instantiates a per-renderer copy that Unity then owns and never collects.</para>
        /// </summary>
        public static void Wear(MeshFilter? filter, MeshRenderer? renderer, in Piece piece,
            ColonistMaterials? materials, in ColonistAppearance look)
        {
            if (filter == null || renderer == null) return;

            if (!Enabled || !piece.Usable)
            {
                renderer.enabled = false;
                filter.sharedMesh = null;
                return;
            }

            filter.sharedMesh = piece.Mesh;

            // A piece with no appearance (the helmet) is worn in its own paint, but still as a
            // character, so it is inked and drawn after the outline pass like the head under it.
            Material? art = piece.Material;
            Material? painted = piece.Cells != null
                ? materials?.For(art, piece.Cells, look)
                : materials?.InOwnPaint(art);
            renderer.sharedMaterial = painted != null ? painted : art;
            renderer.enabled = true;
        }
    }
}
