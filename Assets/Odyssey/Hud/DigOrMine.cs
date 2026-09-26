#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// Dig or Mine (design 62 §4): one order, two words. The Mine order on soft ground — grass,
    /// earth, sand, gravel, subsoil — reads <b>Dig</b>; on rock or ore it reads <b>Mine</b>. The job,
    /// the work and the yield are the same; only the word differs, and it differs everywhere the
    /// order is named: the armed banner while the tool is held, the tile pane's order line, and the
    /// colonist's activity line.
    ///
    /// <para><b>The rule is not here.</b> Which terrain is rock-like is
    /// <see cref="TerrainHandle.IsRockLike"/>'s alone, because the simulation asks it too — of a
    /// drag's start cell, to decide whether the run marks only rock. This class only turns that
    /// answer into registry keys and a run, so the word on screen and the cells a drag marks cannot
    /// come to disagree.</para>
    /// </summary>
    public static class DigOrMine
    {
        /// <summary>The order's name on rock, ore and rubble. The palette's own Mine chip.</summary>
        public const string MineKey = PaletteTools.Mine;

        /// <summary>The order's name on soft ground.</summary>
        public const string DigKey = "ui.arch.tool.dig";

        /// <summary>A colonist cutting rock.</summary>
        public const string MiningKey = "ui.status.mining";

        /// <summary>A colonist cutting soft ground.</summary>
        public const string DiggingKey = "ui.status.digging";

        /// <summary>
        /// The name the simulation publishes the digging flag under
        /// (<c>MineJobDriver.DiggingName</c>), a literal on <see cref="JobLabels.CarryingAspect"/>'s
        /// bargain. Sparse: present at 1 while a colonist's Mine job is on soft ground.
        /// </summary>
        public const string DiggingAspect = "odyssey.pawn.digging";

        static readonly AspectKey DiggingAspectKey = AspectKey.Of(DiggingAspect);

        /// <summary>Does the Mine order read Dig on this terrain? Unknown (-1), air and water read Mine.</summary>
        public static bool IsDig(int terrain) => TerrainHandle.IsSoftGround(terrain);

        /// <summary>The order's registry key on this terrain.</summary>
        public static string OrderKey(int terrain) => IsDig(terrain) ? DigKey : MineKey;

        /// <summary>The activity's registry key for a cut on this terrain.</summary>
        public static string ActivityKey(int terrain) => IsDig(terrain) ? DiggingKey : MiningKey;

        /// <summary>
        /// What a Mine drag begun on this terrain marks, as a <see cref="DesignateRun"/> value:
        /// only rock when it was begun on rock, everything the order can take otherwise. Asked
        /// <b>once</b>, of the start cell, by <see cref="DesignateDirector.Begin"/> — never per cell
        /// (<c>docs/bug-patterns.md</c> P4).
        /// </summary>
        public static int RunFor(int anchorTerrain) =>
            TerrainHandle.IsRockLike(anchorTerrain) ? DesignateRun.RockOnly : DesignateRun.Everything;

        /// <summary>Is this colonist digging soft ground on this frame? One O(1) lookup.</summary>
        public static bool IsDigging(WorldSnapshot snapshot, PawnId id) =>
            snapshot.TryGetPawnAspect(id, DiggingAspectKey, out _);
    }
}
