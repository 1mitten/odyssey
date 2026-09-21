#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// What a shelf looks like, in one place: three boxes, and where the goods on it stand.
    ///
    /// <para><b>One place, for the reason <see cref="BedShape"/> was extracted.</b> The mesher
    /// draws the built thing, the ghost draws the thing being placed, the selection bracket draws
    /// the box round it and the renderer stands the goods on its deck. Four readers of the same
    /// numbers, and four copies is three chances for the ghost and the shelf to disagree about
    /// where a shelf is.</para>
    ///
    /// <para><b>Placeholder geometry, deliberately and temporarily.</b> No pack contains a one-cell
    /// shelf at this grid size, so <c>ModuleIds.Shelf</c> resolves to the fallback block and a
    /// shelf is three scaled instances of it, tinted by the stuff it was built of. The day real
    /// art lands, one catalogue row on that id upgrades every shelf in the game with no code
    /// change — and this class is deleted rather than kept beside it.</para>
    /// </summary>
    public static class ShelfShape
    {
        /// <summary>Carcass, deck, back lip.</summary>
        public const int PartCount = 3;

        /// <summary>How many stacks a shelf shows a place for. Matches the def's <c>storageSlots</c>.</summary>
        public const int Slots = 8;

        // In metres from the shelf's origin, which is the centre of its one cell's floor, with
        // local -Z the back and +Z the open front.
        //
        // The carcass stands AGAINST THE BACK of the cell rather than filling it, leaving 0.6 m of
        // clear floor at the front. A shelf is passable — a colonist walks through its cell and
        // reaches into it from where she stands — and a box filling its own cell would say the
        // opposite of that. The deck overhangs the carcass by 8 cm each way so the thing reads as a
        // shelf rather than a crate, and the lip gives it a silhouette that differs by facing:
        // without one a cube looks the same all four ways round and the rotate key appears broken.
        //
        // 1.24 m overall, which is below a colonist on purpose. A 1.7 m thing you walk through
        // reads as a fault rather than as furniture.
        static readonly Vector3[] Sizes =
        {
            new Vector3(2.10f, 0.88f, 0.90f), // carcass
            new Vector3(2.26f, 0.10f, 1.02f), // deck
            new Vector3(2.26f, 0.26f, 0.10f), // back lip
        };

        static readonly Vector3[] Centres =
        {
            new Vector3(0f, 0.44f, -0.55f), // carcass: 0.00 .. 0.88
            new Vector3(0f, 0.93f, -0.55f), // deck:    0.88 .. 0.98
            new Vector3(0f, 1.11f, -1.05f), // lip:     0.98 .. 1.24, at the back edge
        };

        /// <summary>
        /// The top of the deck above the shelf's own floor — where the goods stand, and what a
        /// player aims at when they click one.
        ///
        /// <para>Derived from the deck's own box rather than written down beside it, so tuning the
        /// deck moves the goods and the click plane with it. Two copies of a height is how one of
        /// them gets corrected on its own.</para>
        /// </summary>
        public static float DeckTop => Centres[1].y + Sizes[1].y * 0.5f;

        /// <summary>The top of the whole thing — where an order mark on a shelf sits.</summary>
        public static float Top => Centres[2].y + Sizes[2].y * 0.5f;

        /// <summary>The box a shelf occupies, for a selection bracket.</summary>
        public static Vector3 Size => new Vector3(Sizes[2].x, Top, Sizes[1].z + 0.12f);

        /// <summary>
        /// How far a heap of goods on one slot may spread, in metres.
        ///
        /// <para>Its own number rather than the recipe's, because <c>ItemHeap</c>'s spreads are
        /// sized for a 2.5 m cell floor — wood's is 0.52 m — and the slots below are 0.55 m apart.
        /// At cell-floor spread the stacks interleave and a shelf reads as a heap.</para>
        /// </summary>
        public const float SlotSpread = 0.17f;

        /// <summary>How much smaller a thing is drawn on a shelf than on the floor, for the same reason.</summary>
        public const float GoodsScale = 0.55f;

        /// <summary>
        /// The shelf's origin: the draped floor point at the centre of its one cell.
        ///
        /// <para><b>No half-cell offset along the facing</b>, which is the one line a copy of
        /// <see cref="BedShape"/> gets wrong. A bed spans two cells and is measured about the point
        /// between them; a shelf is one cell and is measured about its middle. Copying the bed's
        /// offset would push every shelf half a cell into its neighbour, and nothing would fail —
        /// which is why <c>ShelfShapeTests</c> asserts the box stays inside its own cell.</para>
        ///
        /// <para>Draped, because a shelf is fixed to the grid: anything fixed to the grid is
        /// draped, only what moves over it is lifted.</para>
        /// </summary>
        public static Matrix4x4 Root(int x, int z, int y, int facing) =>
            GroundRelief.Drape(Origin(x, z, y));

        /// <summary>The same point, undraped — what a selection bracket is centred on.</summary>
        public static Vector3 Origin(int x, int z, int y) => CellMetrics.FloorCentre(x, z, y);

        /// <summary>
        /// One part of the shelf, placed and turned. <paramref name="part"/> is 0 to 2.
        ///
        /// <para>The metre figures above are divided by the module's own box, which is not a unit
        /// cube. Getting that wrong is silent — the part is simply the wrong size — so it is
        /// derived here once rather than written out per part.</para>
        /// </summary>
        public static Matrix4x4 Part(in Matrix4x4 root, int facing, int part)
        {
            ModuleLibrary.GetFallbackBox(ModuleShape.SolidBlock, out Vector3 box, out Vector3 boxCentre);

            Vector3 size = Sizes[part];
            var scale = new Vector3(size.x / box.x, size.y / box.y, size.z / box.z);

            // The module's own local centre rides the scale, so the offset that lands the part on
            // its intended centre has to take it back off.
            Vector3 offset = Centres[part] - Vector3.Scale(scale, boxCentre);

            Matrix4x4 yaw = Matrix4x4.Rotate(Quaternion.Euler(0f, Directions.Yaw[facing], 0f));
            return root * yaw * Matrix4x4.Translate(offset) * Matrix4x4.Scale(scale);
        }

        /// <summary>
        /// Where one slot's goods stand, in world space and already draped.
        ///
        /// <para>Four across the deck by two deep, which is the eight the def declares. The point
        /// comes back turned with the shelf, so the goods ride the facing with the thing holding
        /// them rather than staying square to the world.</para>
        /// </summary>
        public static Vector3 SlotCentre(in Matrix4x4 root, int facing, int slot)
        {
            if (slot < 0) slot = 0;
            slot %= Slots;

            float[] across = { -0.82f, -0.27f, 0.27f, 0.82f };
            float[] along = { -0.78f, -0.32f };

            var local = new Vector3(across[slot & 3], DeckTop, along[(slot >> 2) & 1]);
            Matrix4x4 yaw = Matrix4x4.Rotate(Quaternion.Euler(0f, Directions.Yaw[facing], 0f));
            return root.MultiplyPoint3x4(yaw.MultiplyPoint3x4(local));
        }

        /// <summary>
        /// The shelf's bounding box in world space, for the selection bracket.
        ///
        /// <para><b>The in-plane offset is not optional here, and it is where a copy of
        /// <see cref="BedShape.WorldBounds"/> goes wrong.</b> A bed is symmetric about its origin,
        /// so its bracket needs no offset in plan; a shelf stands against the back of its cell, so
        /// a bracket centred on the cell would sit half over the empty floor in front of it — and
        /// only on three of the four facings, which is exactly the kind of fault nobody
        /// reproduces.</para>
        /// </summary>
        public static void WorldBounds(int x, int z, int y, int facing, out Vector3 centre, out Vector3 size)
        {
            float half = Top;
            float back = (Centres[0].z + Centres[1].z) * 0.5f;

            Matrix4x4 yaw = Matrix4x4.Rotate(Quaternion.Euler(0f, Directions.Yaw[facing], 0f));
            Vector3 shift = yaw.MultiplyPoint3x4(new Vector3(0f, 0f, back));

            centre = GroundRelief.Lift(Origin(x, z, y)) + shift + Vector3.up * (half * 0.5f);

            // Turned with the shelf: a quarter turn swaps the wide axis for the deep one.
            bool alongX = facing == Directions.East || facing == Directions.West;
            size = alongX
                ? new Vector3(Size.z, half, Size.x)
                : new Vector3(Size.x, half, Size.z);
        }
    }
}
