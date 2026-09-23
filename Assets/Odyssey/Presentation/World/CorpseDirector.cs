#nullable enable
using System;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// The dead, drawn (design 33 §1, §3): every <see cref="CorpseView"/> on a drawn layer, lying
    /// where it fell in the death pose — the Sword Combat pack's <c>Death_*_Pose</c> where the pack
    /// is present, a computed lie-down where it is not — with the face and colours its seed deals,
    /// so the colonist who fell is the colonist who lies there. <b>Lane B's file</b>
    /// (<c>docs/plans/combat-contracts.md</c>).
    ///
    /// <para><b>A stub from the contracts step.</b> The bootstrap builds it beside the doors, syncs
    /// it once a frame after them and disposes it with them, so lane B fills this file and edits no
    /// line of <c>OdysseyBootstrap</c>. It draws nothing yet.</para>
    ///
    /// <para><b>Per-frame cost scales with the corpses on drawn layers</b> (<c>docs/process.md</c>
    /// §3), which a colony counts on its fingers.</para>
    /// </summary>
    public sealed class CorpseDirector : IDisposable
    {
        readonly WorldRenderModel _model;
        readonly ModuleCatalogue? _catalogue;
        readonly PawnFigureDirector? _figures;
        readonly Transform _parent;
        readonly int _layer;

        public CorpseDirector(WorldRenderModel model, ModuleCatalogue? catalogue, PawnFigureDirector? figures,
            Transform parent, int layer)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _catalogue = catalogue;
            _figures = figures;
            _parent = parent;
            _layer = layer;
        }

        /// <summary>Once a frame, after the doors. Draws nothing until lane B writes it.</summary>
        public void Sync(WorldSnapshot snapshot, int activeLayer, SliceSettings slice)
        {
        }

        public void Dispose()
        {
        }
    }
}
