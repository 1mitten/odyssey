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
        /// <summary>80 x 80 x 16. Close quarters.</summary>
        public static GridSize Small => new GridSize(80, 80, 16);

        /// <summary>
        /// 120 x 120 x 16 — the default, and the board every number on record was taken on.
        /// Every arm that quotes a Huge figure quotes this one beside it, because a ratio between
        /// two boards measured in one run is the only figure this machine can be trusted for.
        /// </summary>
        public static GridSize Standard => new GridSize(120, 120, 16);

        /// <summary>180 x 180 x 24. Ships, and until 2026-09-21 nothing had ever run it.</summary>
        public static GridSize Large => new GridSize(180, 180, 24);

        /// <summary>
        /// 240 x 240 x 16 — twice Standard's ground at Standard's depth (2026-09-21,
        /// <c>docs/design/28-map-size.md</c>).
        ///
        /// <para>Four times Standard's cells, but only 1.19x Large's and 1.04x its render chunks,
        /// because chunks are 25 x 25 within one layer and Large carries eight more layers. The
        /// one axis where Huge clearly exceeds Large is the layer stride, 57,600 against 32,400 —
        /// which is <c>EnclosureGrid.SolveLayer</c> and the slice channel, and nothing else.</para>
        /// </summary>
        public static GridSize Huge => new GridSize(240, 240, 16);
    }
}
