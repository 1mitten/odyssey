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
        }

        public Piece Hair(int index) => At(_hair, index);

        public Piece Beard(int index) => At(_beards, index);

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

            if (!piece.Usable)
            {
                renderer.enabled = false;
                filter.sharedMesh = null;
                return;
            }

            filter.sharedMesh = piece.Mesh;

            Material? art = piece.Material;
            Material? painted = materials?.For(art, piece.Cells, look);
            renderer.sharedMaterial = painted != null ? painted : art;
            renderer.enabled = true;
        }
    }
}
