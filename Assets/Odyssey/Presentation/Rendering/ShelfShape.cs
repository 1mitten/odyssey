#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// What a shelf looks like, in one place: an open timber rack — four posts, two decks and a
    /// back rail — and where the goods on it stand.
    ///
    /// <para><b>One place, for the reason <see cref="BedShape"/> was extracted.</b> The mesher
    /// draws the built thing, the ghost draws the thing being placed, the selection bracket draws
    /// the box round it and the renderer stands the goods on its decks. Four readers of the same
    /// numbers, and four copies is three chances for the ghost and the shelf to disagree about
    /// where a shelf is.</para>
    ///
    /// <para><b>Placeholder geometry, deliberately and temporarily.</b> No pack contains a one-cell
    /// shelf at this grid size, so <c>ModuleIds.Shelf</c> resolves to the fallback block and a
    /// shelf is a handful of scaled instances of it, tinted by the stuff it was built of. The day
    /// real art lands, one catalogue row on that id upgrades every shelf in the game with no code
    /// change — and this class is deleted rather than kept beside it.</para>
    ///
    /// <para><b>Two decks on open posts, from 2026-09-21</b> (owner, with a photograph of a timber
    /// garage rack: <i>"maybe there should be 2 shelves and not 1 thick container … something like
    /// this is more suitable than just 1 thick ledge"</i>). The first cut was a solid carcass with
    /// a single deck on top, and it read as a crate with a lid — the 8 cm the deck overhung the
    /// carcass by was the only thing distinguishing the two, and 8 cm is nothing at the play
    /// camera's 48°. What makes a rack legible is the daylight through it: posts rather than
    /// sides, and a gap you can see between two loaded decks.</para>
    ///
    /// <para><b>The upper deck is half the depth of the lower one, and that is the load-bearing
    /// number.</b> Looking down at 48°, a deck directly above another hides everything on the one
    /// below it that lies further back than the clearance affords — so a rack drawn as two decks of
    /// equal depth would have shown four stacks and hidden four, and the goods are the fill tell
    /// (<c>docs/design/30-shelves.md</c> §8b). Setting the upper deck back and standing its goods
    /// at the back of it, while the lower deck's goods stand at the front of theirs, puts the two
    /// rows in front of one another like seats in a stand: <see cref="LowerSlotsClearTheUpperDeck"/>
    /// is the arithmetic, and <c>ShelfShapeTests</c> asserts it rather than trusting it.</para>
    /// </summary>
    public static class ShelfShape
    {
        /// <summary>Four posts, the lower deck, the upper deck, the back rail.</summary>
        public const int PartCount = 7;

        /// <summary>How many stacks a shelf shows a place for. Matches the def's <c>storageSlots</c>.</summary>
        public const int Slots = 8;

        /// <summary>How many of those stand on each deck: four across, two decks.</summary>
        public const int SlotsPerDeck = 4;

        // ---- the frame, in metres from the shelf's origin ---------------------------------------
        //
        // The origin is the centre of its one cell's floor, with local -Z the back and +Z the open
        // front.
        //
        // The rack stands AGAINST THE BACK of the cell rather than filling it, leaving well over a
        // metre of clear floor at the front. A shelf is passable — a colonist walks through its
        // cell and reaches into it from where she stands — and a box filling its own cell would say
        // the opposite of that.
        //
        // 1.45 m overall. Raised from 1.24 with the second deck, because two decks inside the old
        // envelope left 0.5 m between them and drawn goods are about 0.3 m tall, so the rack read
        // as cramped rather than open. Still well below a colonist on purpose: a 1.7 m thing you
        // walk through reads as a fault rather than as furniture, and that is the constraint the
        // height answers to rather than the cell's own 3 m.
        //
        // The back rail is what gives the silhouette a front and a back. Without it a rack is very
        // nearly symmetric end to end and the rotate key appears broken.

        /// <summary>Half the frame's width; the footprint runs from -X to +X of this.</summary>
        const float HalfWidth = 1.13f;

        /// <summary>The footprint's back and front edges in local Z.</summary>
        const float BackEdge = -1.06f, FrontEdge = -0.04f;

        /// <summary>The middle of the footprint in local Z, which is what a bracket centres on.</summary>
        const float FootprintCentreZ = (BackEdge + FrontEdge) * 0.5f;

        /// <summary>How deep the footprint is, front to back.</summary>
        const float FootprintDepth = FrontEdge - BackEdge;

        /// <summary>The upper deck's front edge. Half the depth of the lower deck, set at the back.</summary>
        const float UpperDeckFront = -0.54f;

        const float PostSection = 0.09f, DeckThickness = 0.08f;

        static readonly Vector3[] Sizes =
        {
            new Vector3(PostSection, 1.25f, PostSection),   // post, back left
            new Vector3(PostSection, 1.25f, PostSection),   // post, back right
            new Vector3(PostSection, 1.25f, PostSection),   // post, front left
            new Vector3(PostSection, 1.25f, PostSection),   // post, front right
            new Vector3(2.26f, DeckThickness, FootprintDepth),                 // lower deck
            new Vector3(2.26f, DeckThickness, UpperDeckFront - BackEdge),      // upper deck
            new Vector3(2.26f, 0.26f, 0.10f),                                  // back rail
        };

        static readonly Vector3[] Centres =
        {
            new Vector3(-1.085f, 0.625f, -1.015f),  // posts: 0.00 .. 1.25, inset into the corners
            new Vector3(1.085f, 0.625f, -1.015f),
            new Vector3(-1.085f, 0.625f, -0.085f),
            new Vector3(1.085f, 0.625f, -0.085f),
            new Vector3(0f, 0.46f, FootprintCentreZ),                          // lower deck: .. 0.50
            new Vector3(0f, 1.15f, (BackEdge + UpperDeckFront) * 0.5f),        // upper deck: .. 1.19
            new Vector3(0f, 1.32f, -1.05f),                                    // rail:  1.19 .. 1.45
        };

        const int LowerDeck = 4, UpperDeck = 5, BackRail = 6;

        /// <summary>
        /// The top of the upper deck above the shelf's own floor — the highest surface of the
        /// thing, what a player aims at when they click one, and what <c>StandHeight</c> answers.
        ///
        /// <para>Derived from the deck's own box rather than written down beside it, so tuning the
        /// deck moves the goods and the click plane with it. Two copies of a height is how one of
        /// them gets corrected on its own.</para>
        /// </summary>
        public static float DeckTop => Centres[UpperDeck].y + Sizes[UpperDeck].y * 0.5f;

        /// <summary>The top of the lower deck, where the front four stacks stand.</summary>
        public static float LowerDeckTop => Centres[LowerDeck].y + Sizes[LowerDeck].y * 0.5f;

        /// <summary>The top of the whole thing — where an order mark on a shelf sits.</summary>
        public static float Top => Centres[BackRail].y + Sizes[BackRail].y * 0.5f;

        /// <summary>The box a shelf occupies, for a selection bracket.</summary>
        public static Vector3 Size => new Vector3(HalfWidth * 2f, Top, FootprintDepth + 0.12f);

        /// <summary>
        /// How far a heap of goods on one slot may spread, in metres.
        ///
        /// <para>Its own number rather than the recipe's, because <c>ItemHeap</c>'s spreads are
        /// sized for a 2.5 m cell floor — wood's is 0.52 m — and the slots below are 0.55 m apart.
        /// At cell-floor spread the stacks interleave and a shelf reads as a heap.</para>
        /// </summary>
        public const float SlotSpread = 0.17f;

        /// <summary>
        /// The most lumps one slot's heap is drawn with, however big the stack on it.
        ///
        /// <para><b>The other half of tightening the recipe to a slot.</b> Only the spread was
        /// tightened at first, and a heap's count and size were left at their cell-floor values —
        /// so a full shelf drew twenty-four wood bundles inside one cell's footprint and the rack
        /// vanished under its own goods (photographed 2026-09-21, <c>ShelfCheck</c>). A stack on a
        /// deck is a stack in a bay, and a bay reads as one or two bundles; how full the shelf is
        /// gets told by how many bays are taken, which is what a shelf meters anyway.</para>
        /// </summary>
        public const int SlotLumps = 2;

        /// <summary>How much smaller a thing is drawn on a shelf than on the floor, for the same reason.</summary>
        public const float GoodsScale = 0.45f;

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
        /// One part of the shelf, placed and turned. <paramref name="part"/> is 0 to
        /// <see cref="PartCount"/> - 1.
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
        /// <para>Four across each of the two decks, which is the eight the def declares. The point
        /// comes back turned with the shelf, so the goods ride the facing with the thing holding
        /// them rather than staying square to the world.</para>
        ///
        /// <para><b>Slots 0-3 are the lower deck and 4-7 the upper</b>, so a rack fills from the
        /// bottom the way a person loads one, and a nearly-empty shelf puts what little it holds on
        /// the deck nearest the camera.</para>
        /// </summary>
        // Static, not locals: this is asked once per stack on every shelf on every frame, and
        // arrays built inside it were heap allocations a stack a frame — a warehouse of 320 stacks
        // is hundreds of allocations a frame and a gen-0 collection every few seconds, for tables
        // that never change.
        static readonly float[] Across = { -0.82f, -0.27f, 0.27f, 0.82f };

        /// <summary>
        /// Where each deck's row of goods stands, front to back.
        ///
        /// <para>The lower deck's row is at the FRONT of its deck and the upper deck's at the BACK
        /// of its own shallower one, which is what keeps either row from hiding the other at the
        /// play camera's 48°. Changing either number without the other is how four of a shelf's
        /// eight stacks quietly stop being visible.</para>
        /// </summary>
        static readonly float[] Along = { -0.28f, -0.80f };

        public static Vector3 SlotCentre(in Matrix4x4 root, int facing, int slot)
        {
            if (slot < 0) slot = 0;
            slot %= Slots;

            int deck = (slot / SlotsPerDeck) & 1;
            float height = deck == 0 ? LowerDeckTop : DeckTop;

            var local = new Vector3(Across[slot % SlotsPerDeck], height, Along[deck]);
            Matrix4x4 yaw = Matrix4x4.Rotate(Quaternion.Euler(0f, Directions.Yaw[facing], 0f));
            return root.MultiplyPoint3x4(yaw.MultiplyPoint3x4(local));
        }

        /// <summary>
        /// How much clear air the lower deck's goods have in front of the upper deck's leading
        /// edge, in metres — the margin that keeps the front row visible from above.
        ///
        /// <para>Positive means the lower row, heap spread and all, stands entirely clear of
        /// anything above it, so no amount of looking down can hide it. It is exposed rather than
        /// left implicit because it is the one relation in this class that no other number
        /// protects: every part could stay inside the cell, every slot could sit on a real deck,
        /// and half the goods could still be invisible.</para>
        /// </summary>
        public static float LowerSlotsClearTheUpperDeck => (Along[0] - SlotSpread) - UpperDeckFront;

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

            Matrix4x4 yaw = Matrix4x4.Rotate(Quaternion.Euler(0f, Directions.Yaw[facing], 0f));
            Vector3 shift = yaw.MultiplyPoint3x4(new Vector3(0f, 0f, FootprintCentreZ));

            centre = GroundRelief.Lift(Origin(x, z, y)) + shift + Vector3.up * (half * 0.5f);

            // Turned with the shelf: a quarter turn swaps the wide axis for the deep one.
            bool alongX = facing == Directions.East || facing == Directions.West;
            size = alongX
                ? new Vector3(Size.z, half, Size.x)
                : new Vector3(Size.x, half, Size.z);
        }
    }
}
