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
        /// <see cref="BuildingHandle"/>: nothing, wall, floor, deck plate, ladder, bed.
        /// </summary>
        public static readonly int[] Cells = { 1, 1, 1, 1, 1, 2 };

        /// <summary>
        /// Whether the ghost may be turned with the rotate key before placing. Parallel to
        /// <see cref="BuildingHandle"/>: nothing, wall, floor, deck plate, ladder, bed.
        /// </summary>
        public static readonly bool[] Rotates = { false, false, false, false, false, true };

        public static int CellsOf(int building) =>
            (uint)building < (uint)Cells.Length ? Cells[building] : 1;

        public static bool CanRotate(int building) =>
            (uint)building < (uint)Rotates.Length && Rotates[building];
    }
}
