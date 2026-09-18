#nullable enable
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using UnityEngine;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// <b>Does a floor stop being drawn when it is taken up?</b>
    ///
    /// <para>The owner deconstructed a wood floor, got the wood back, and a grey plated floor stayed
    /// on screen (2026-09-18). Two explanations fit that and they want opposite fixes: either the
    /// render mirror kept the old slab (a stale chunk, a drawing bug), or the thing still on screen
    /// is a <i>different surface one layer down</i> that was never removed (no bug at all, just a
    /// gap to see through).</para>
    ///
    /// <para>Reading the code says the second: <c>RemoveSlab</c> marks a 3x3x3 of chunks dirty. This
    /// measures it instead, through the real mirror, which is the half the fast tier cannot compile.
    /// It builds a floor, takes it up, and prints what the grid says and what the mirror says after
    /// each step. They must agree.</para>
    ///
    /// <para><c>scripts/unity.sh exec Odyssey.EditorTools.StaleFloorProbe.Run</c></para>
    /// </summary>
    public static class StaleFloorProbe
    {
        public static void Run()
        {
            var size = new GridSize(40, 40, 16);
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 1;
            scenario.startingFellRadius = 0;

            ColonyWorld colony = ColonyWorld.Build(size, 1u, scenario, barren: true, wooded: false);
            var chunks = new ChunkGrid(size);
            var model = new WorldRenderModel(size, chunks, Library());

            CellRef start = colony.Start;
            int ground = size.Index(start.X + 3, start.Z + 3, start.Y);
            int wall = ground + 1;
            int slab = ground + size.LayerStride;

            Raise(colony, wall, BuildingHandle.Wall);
            Report(colony, model, slab, "before any floor");

            Raise(colony, slab, BuildingHandle.Floor);
            Report(colony, model, slab, "wood floor built");

            bool gone = colony.Construction.RemoveSlab(colony.Pawns, slab, out ushort was);
            colony.World.Tick();
            Debug.Log($"[Stale] RemoveSlab returned {gone}, gave back stuff {was}");
            Report(colony, model, slab, "floor taken up");

            // And the cell one layer DOWN, which is the surface the owner may actually be seeing.
            Report(colony, model, ground, "the cell below, for contrast");
        }

        static ModuleLibrary Library()
        {
            var catalogue = UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(
                "Assets/Odyssey/Presentation/ModuleCatalogue.asset");
            return new ModuleLibrary(catalogue);
        }

        static void Raise(ColonyWorld colony, int cell, int building)
        {
            IntentRejection r = colony.Construction.Place(
                colony.Grid.Size.FromIndex(cell), building, StuffHandle.Wood);
            Debug.Log($"[Stale] order {building} at {colony.Grid.Size.FromIndex(cell)} => {r}");
            if (r != IntentRejection.None) return;
            colony.Construction.Raise(colony.Pawns, cell);
            colony.World.Tick();
        }

        /// <summary>
        /// The grid's answer and the mirror's, side by side. The mirror is refreshed exactly as the
        /// renderer refreshes it, so a disagreement here is a disagreement on screen.
        /// </summary>
        static void Report(ColonyWorld colony, WorldRenderModel model, int cell, string when)
        {
            model.RefreshAll(colony.Grid, colony.Outcome.Edifices);

            CellRef at = colony.Grid.Size.FromIndex(cell);
            Debug.Log(
                $"[Stale] {when,-28} {at}  " +
                $"grid.Floor={colony.Grid.Floor[cell]} grid.FloorStuff={colony.Grid.FloorStuff[cell]}  " +
                $"mirror.FloorModule={model.FloorModule(cell)} mirror.FloorStuff={model.FloorStuff(cell)}");
        }
    }
}
