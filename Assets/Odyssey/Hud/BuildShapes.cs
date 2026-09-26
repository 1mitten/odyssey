#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The shape of what is being placed — how many cells it occupies, and whether it may be
    /// turned before it is put down — as this assembly's parallel to <see cref="BuildingHandle"/>.
    ///
    /// <para>Parallel by necessity, exactly as <see cref="BuildLabels"/> is: the real table lives
    /// in <c>Odyssey.Sim.Construction</c>, which this assembly cannot see (ADR 0003), and the
    /// interface needs the answer before any intent exists. The two tables are held together the
    /// same way the labels are — a test walks both — and the day the buildable set outgrows a
    /// hand-written row per thing, this is the second file that turns into generated content
    /// beside the one <c>BuildLabels</c>' own comment already names.</para>
    /// </summary>
    public static class BuildShapes
    {
        /// <summary>
        /// Cells the thing occupies, in a line along its facing. Parallel to
        /// <see cref="BuildingHandle"/>: nothing, wall, floor, deck plate, ladder, bed, door,
        /// shelf, campfire, conduit, generator, heater, galley, sandbags, stair.
        /// </summary>
        public static readonly int[] Cells = { 1, 1, 1, 1, 1, 2, 1, 1, 1, 1, 2, 1, 1, 1, 2 };

        /// <summary>
        /// Whether the ghost may be turned with the rotate key before placing. Parallel to
        /// <see cref="BuildingHandle"/>: nothing, wall, floor, deck plate, ladder, bed, door,
        /// shelf, campfire, conduit, generator, heater.
        ///
        /// <para><b>The ladder joined the list on 2026-09-18 and this row is why the change was not
        /// finished when the def said it was.</b> A def gaining <c>rotates</c> does nothing on its
        /// own: this table decides whether the rotate key belongs to the tool, so a ladder marked
        /// rotatable in the Defs and not here would have had R raise the slice instead of turning
        /// the ghost — the key doing the wrong one of its two jobs, silently, with every simulation
        /// test still green. <c>BuildShapesAgreeWithTheDefs</c> now walks both tables rather than
        /// spot-checking two rows, which is what would have caught it.</para>
        /// </summary>
        public static readonly bool[] Rotates = { false, false, false, false, true, true, true, true, false, false, true, true, true, false, true };

        /// <summary>
        /// Whether a drag places a <b>line</b> and never widens into a box. Parallel to
        /// <see cref="BuildingHandle"/>; true for the conduit (design 32 §10) and for cover (design
        /// 50 §4). A wall drag widens after three cells across because a room is a box; a power run
        /// is a path, and a box of lines is a slab of copper nobody asked for — as a box of sandbags
        /// would be a pile of them. A line of cover is drawn joined, piece to piece.
        /// </summary>
        public static readonly bool[] LineOnly = { false, false, false, false, false, false, false, false, false, true, false, false, false, true, false };

        /// <summary>
        /// Whether arming this is power work, so the hidden lines are shown while it is armed
        /// (design 32 §9, decision 5). Parallel to <see cref="BuildingHandle"/>: the conduit, the
        /// generator, the heater, and the galley (design 48), which is placed where a line can reach it.
        /// </summary>
        public static readonly bool[] Power = { false, false, false, false, false, false, false, false, false, true, true, true, true, false, false };

        public static int CellsOf(int building) =>
            (uint)building < (uint)Cells.Length ? Cells[building] : 1;

        public static bool CanRotate(int building) =>
            (uint)building < (uint)Rotates.Length && Rotates[building];

        public static bool IsLineOnly(int building) =>
            (uint)building < (uint)LineOnly.Length && LineOnly[building];

        public static bool IsPower(int building) =>
            (uint)building < (uint)Power.Length && Power[building];
    }
}
