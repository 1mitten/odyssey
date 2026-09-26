using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The boards the game offers, as the measurement arms in this assembly need them.
    ///
    /// <para><b>Why this is written twice.</b> The list a player picks from is
    /// <c>Odyssey.Hud.MenuDirector.MapSizes</c>, and that class says in its own summary why it
    /// lives in the interface assembly: a size is three integers the simulation will take from
    /// anybody, and which three a <i>menu</i> offers is an interface question. <c>Odyssey.Sim</c>
    /// and its tests do not reference <c>Odyssey.Hud</c> and must not start, so the benchmarks
    /// cannot read that list. They mirror it here instead.</para>
    ///
    /// <para>The mirror is guarded rather than trusted:
    /// <c>Odyssey.Tests.Hud.MenuDirectorTests.TheMeasuredBoardsAreTheBoardsOffered</c> asserts
    /// these numbers against <c>MapSizes.All</c>, and fails the fast tier the day the two drift.
    /// That is the only thing standing between "the benchmark measures the shipped board" and
    /// "the benchmark measures a board nobody plays" — so if one of these numbers changes, change
    /// it in both places or the test will say so.</para>
    /// </summary>
    public static class BoardSizes
    {
        /// <summary>80 x 80 x 32. Close quarters. Every board is <see cref="GridSize.OfferedLayers"/>
        /// deep since the deep-mining work (design 62, DM2); the figures in
        /// <c>docs/design/28-map-size.md</c> §2–§11 were taken at 16 (Large at 24) and §12 has the
        /// 32-layer set.</summary>
        public static GridSize Small => new GridSize(80, 80, GridSize.OfferedLayers);

        /// <summary>
        /// 120 x 120 x 32 — the default. Every number on record before 2026-09-26 was taken at
        /// 120 x 120 x 16.
        /// Every arm that quotes a Huge figure quotes this one beside it, because a ratio between
        /// two boards measured in one run is the only figure this machine can be trusted for.
        /// </summary>
        public static GridSize Standard => new GridSize(120, 120, GridSize.OfferedLayers);

        /// <summary>180 x 180 x 32 (24 until 2026-09-26). Ships, and until 2026-09-21 nothing had ever run it.</summary>
        public static GridSize Large => new GridSize(180, 180, GridSize.OfferedLayers);

        /// <summary>
        /// 240 x 240 x 32 — twice Standard's ground at Standard's depth (2026-09-21 at 16 layers,
        /// <c>docs/design/28-map-size.md</c>; 32 since 2026-09-26).
        ///
        /// <para>Four times Standard's cells. At 16 layers it was only 1.19x Large's cells and
        /// 1.04x its render chunks, because Large then carried eight more layers; at one depth it
        /// is 1.78x both. The layer stride, 57,600 against 32,400, is
        /// <c>EnclosureGrid.SolveLayer</c> and the slice channel.</para>
        /// </summary>
        public static GridSize Huge => new GridSize(240, 240, GridSize.OfferedLayers);
    }
}
