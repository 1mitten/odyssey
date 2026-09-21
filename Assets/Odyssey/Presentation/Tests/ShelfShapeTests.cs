#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The shelf's geometry, and the two things a copy of <see cref="BedShape"/> gets wrong.
    ///
    /// <para>Relations rather than numbers, in <c>BedShapeTests</c>' idiom: the posts stand on the
    /// floor, the decks hang between them, the rail stands on the upper deck, every stack stands on
    /// a real deck, and the whole thing stays inside its own cell. Asserting the literals back would
    /// only say that the table has not been retyped.</para>
    /// </summary>
    public class ShelfShapeTests
    {
        /// <summary>
        /// One part's box in world axes.
        ///
        /// <para><b>The eight corners, not the scale.</b> <c>Matrix4x4.lossyScale</c> gives the
        /// extents along the part's <em>own</em> axes, so a shelf turned a quarter turn comes back
        /// with its width and depth swapped and the box is wrong by the difference — which is how
        /// the first cut of this test failed the shape rather than itself.</para>
        /// </summary>
        static Bounds BoxOf(int part, int facing = 0)
        {
            Matrix4x4 placed = ShelfShape.Part(Matrix4x4.identity, facing, part);
            ModuleLibrary.GetFallbackBox(ModuleShape.SolidBlock, out Vector3 box, out Vector3 boxCentre);

            Vector3 half = box * 0.5f;
            var bounds = new Bounds(placed.MultiplyPoint3x4(boxCentre), Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
            {
                var local = new Vector3(
                    boxCentre.x + ((corner & 1) == 0 ? -half.x : half.x),
                    boxCentre.y + ((corner & 2) == 0 ? -half.y : half.y),
                    boxCentre.z + ((corner & 4) == 0 ? -half.z : half.z));
                bounds.Encapsulate(placed.MultiplyPoint3x4(local));
            }

            return bounds;
        }

        const int PostBackLeft = 0, PostFrontLeft = 2, LowerDeck = 4, UpperDeck = 5, Rail = 6;

        /// <summary>Every part of the rack in one box, which is what a bracket has to cover.</summary>
        static Bounds WholeBox(int facing = 0)
        {
            Bounds whole = BoxOf(0, facing);
            for (int part = 1; part < ShelfShape.PartCount; part++) whole.Encapsulate(BoxOf(part, facing));
            return whole;
        }

        [Test]
        public void ThePostsCarryBothDecksAndTheRailStandsOnTheUpperOne()
        {
            Bounds post = BoxOf(PostBackLeft), lower = BoxOf(LowerDeck);
            Bounds upper = BoxOf(UpperDeck), rail = BoxOf(Rail);

            Assert.That(post.min.y, Is.EqualTo(0f).Within(0.001f), "the posts stand on the floor");
            Assert.That(post.max.y, Is.GreaterThanOrEqualTo(upper.max.y - 0.001f),
                "and they reach the deck they carry");

            Assert.That(lower.min.y, Is.GreaterThan(0.1f), "the lower deck is off the floor");
            Assert.That(upper.min.y, Is.GreaterThan(lower.max.y),
                "the upper deck is above the lower one, not resting on it");
            Assert.That(rail.min.y, Is.EqualTo(upper.max.y).Within(0.001f),
                "and the rail stands on the upper deck");

            Assert.That(ShelfShape.LowerDeckTop, Is.EqualTo(lower.max.y).Within(0.001f),
                "LowerDeckTop is the lower deck's own top, derived rather than written down twice");
            Assert.That(ShelfShape.DeckTop, Is.EqualTo(upper.max.y).Within(0.001f),
                "DeckTop is the upper deck's own top, which is what StandHeight answers");
            Assert.That(ShelfShape.Top, Is.EqualTo(rail.max.y).Within(0.001f));
        }

        [Test]
        public void ItIsARackAndNotACrate()
        {
            // What the owner asked for on 2026-09-21, and what a later tidy would undo first: a
            // solid side or a single deck would each pass every other test in this file.
            Bounds lower = BoxOf(LowerDeck), upper = BoxOf(UpperDeck);

            Assert.That(BoxOf(PostBackLeft).size.x, Is.LessThan(0.2f),
                "the uprights are posts, not sides: the daylight through them is what reads as a rack");
            Assert.That(BoxOf(PostBackLeft).center.z, Is.LessThan(BoxOf(PostFrontLeft).center.z),
                "and there is a post at each end of the depth, not one plane of them");

            Assert.That(lower.size.y, Is.LessThan(0.2f), "a deck is a board, not a block");
            Assert.That(upper.size.y, Is.LessThan(0.2f));
            Assert.That(upper.min.y - lower.max.y, Is.GreaterThan(0.4f),
                "with room between them for the goods that stand on the lower one");
        }

        [Test]
        public void AShelfStaysInsideItsOwnCell()
        {
            // **The assertion a copy of BedShape.Origin fails.** A bed spans two cells and is
            // measured about the point between them, so its origin carries a half-cell offset along
            // the facing; a shelf is one cell. Carrying that offset over would push every shelf half
            // a cell into its neighbour and nothing else would complain.
            float half = CellMetrics.SizeXZ * 0.5f;

            for (int facing = 0; facing < 4; facing++)
            {
                Bounds whole = WholeBox(facing);

                Assert.That(whole.min.x, Is.GreaterThanOrEqualTo(-half - 0.001f), $"facing {facing}");
                Assert.That(whole.max.x, Is.LessThanOrEqualTo(half + 0.001f), $"facing {facing}");
                Assert.That(whole.min.z, Is.GreaterThanOrEqualTo(-half - 0.001f), $"facing {facing}");
                Assert.That(whole.max.z, Is.LessThanOrEqualTo(half + 0.001f), $"facing {facing}");
            }
        }

        [Test]
        public void TheRackStandsAgainstTheBackAndLeavesTheFrontClear()
        {
            // A shelf is passable — a colonist walks through its cell — and a box filling its own
            // cell would say the opposite of that. The whole frame, not one part of it: the fault
            // this guards against is a part that creeps forward on its own.
            Bounds whole = WholeBox();
            float half = CellMetrics.SizeXZ * 0.5f;

            Assert.That(whole.max.z, Is.LessThan(0f), "nothing of the rack is in the front half");
            Assert.That(half - whole.max.z, Is.GreaterThan(0.5f),
                "and there is half a metre of clear floor in front of it");
        }

        [Test]
        public void TurningTheShelfTurnsItsBox()
        {
            // What makes the rotate key visible. A cube looks the same all four ways round, which
            // is the argument BedShape's own doc makes for the pillow.
            Bounds north = WholeBox(0);
            Bounds east = WholeBox(1);

            Assert.That(east.size.x, Is.EqualTo(north.size.z).Within(0.001f));
            Assert.That(east.size.z, Is.EqualTo(north.size.x).Within(0.001f));
            Assert.That(Mathf.Abs(east.center.x), Is.GreaterThan(0.1f),
                "and it stands against a side of the cell rather than in the middle of it");
            Assert.That(Mathf.Abs(east.center.z), Is.LessThan(0.001f),
                "the side it stood against has turned with it");
        }

        [Test]
        public void EverySlotStandsOnOneOfTheTwoDecksAndNoTwoOverlap()
        {
            Matrix4x4 root = Matrix4x4.identity;
            Bounds lower = BoxOf(LowerDeck), upper = BoxOf(UpperDeck);

            var places = new Vector3[ShelfShape.Slots];
            for (int slot = 0; slot < ShelfShape.Slots; slot++)
            {
                bool onLower = slot < ShelfShape.SlotsPerDeck;
                Bounds deck = onLower ? lower : upper;

                places[slot] = ShelfShape.SlotCentre(root, 0, slot);
                Assert.That(places[slot].y,
                    Is.EqualTo(onLower ? ShelfShape.LowerDeckTop : ShelfShape.DeckTop).Within(0.001f),
                    $"slot {slot} stands on the {(onLower ? "lower" : "upper")} deck");
                Assert.That(deck.Contains(new Vector3(places[slot].x, deck.center.y, places[slot].z)),
                    Is.True, $"slot {slot} is over that deck's own footprint");
            }

            for (int a = 0; a < places.Length; a++)
            for (int b = a + 1; b < places.Length; b++)
                Assert.That(Vector3.Distance(places[a], places[b]),
                    Is.GreaterThanOrEqualTo(ShelfShape.SlotSpread * 2f),
                    $"slots {a} and {b} would interleave their heaps");
        }

        [Test]
        public void TheFrontRowIsNotHiddenUnderTheUpperDeck()
        {
            // **The one relation nothing else protects.** Every part can sit inside the cell, every
            // stack can stand on a real deck, and half a shelf's goods can still be invisible —
            // because at the play camera's 48 degrees a deck hides what is behind its own leading
            // edge on the deck below. The goods are the fill tell (docs/design/30-shelves.md 8b),
            // so four of eight going dark is the feature quietly half-failing with nothing to show.
            //
            // Asserted off the drawn boxes rather than off LowerSlotsClearTheUpperDeck, so that the
            // published margin has to agree with the geometry instead of standing in for it.
            Matrix4x4 root = Matrix4x4.identity;
            Bounds upper = BoxOf(UpperDeck);

            for (int slot = 0; slot < ShelfShape.SlotsPerDeck; slot++)
            {
                Vector3 place = ShelfShape.SlotCentre(root, 0, slot);
                Assert.That(place.z - ShelfShape.SlotSpread, Is.GreaterThan(upper.max.z),
                    $"the back rim of slot {slot}'s heap is under the upper deck");
            }

            Assert.That(ShelfShape.LowerSlotsClearTheUpperDeck, Is.GreaterThan(0f),
                "and the published margin agrees with the boxes");
            Assert.That(ShelfShape.LowerSlotsClearTheUpperDeck,
                Is.EqualTo(ShelfShape.SlotCentre(root, 0, 0).z - ShelfShape.SlotSpread - upper.max.z)
                    .Within(0.001f));
        }

        // ---------------------------------------------------------------- picking

        const int ShelfX = 4, ShelfZ = 4, Layer = 1;
        const float PlayCameraDegrees = 48f;

        static RenderTestWorld ShelfOnFlatGround()
        {
            var world = new RenderTestWorld(8, 8, 4);
            for (int z = 0; z < 8; z++)
            for (int x = 0; x < 8; x++)
                world.Solid(x, z, 0, CoreContent.TerrainRock);
            return world.Shelf(ShelfX, ShelfZ, Layer, facing: 0).Publish();
        }

        static Ray AtTheDeck(float cellX, float cellZ)
        {
            var target = new Vector3(
                cellX * CellMetrics.SizeXZ,
                Layer * CellMetrics.SizeY + ShelfShape.DeckTop,
                cellZ * CellMetrics.SizeXZ);
            float radians = PlayCameraDegrees * Mathf.Deg2Rad;
            var direction = new Vector3(0f, -Mathf.Sin(radians), Mathf.Cos(radians)).normalized;
            return new Ray(target - direction * 60f, direction);
        }

        static CellRef Pick(RenderTestWorld world, float cellX, float cellZ)
        {
            Assert.That(
                SlicePicker.Pick(AtTheDeck(cellX, cellZ), world.Model, Layer, new SliceSettings(),
                    out CellRef cell),
                Is.True, $"a ray aimed at the shelf at ({cellX}, {cellZ}) hit nothing at all");
            return cell;
        }

        [Test]
        public void EveryPointAlongADrawnShelfPicksItsOwnCell()
        {
            // **The ends, not the middle.** The bed's bug was a quarter cell of drift, and a centre
            // has half a cell of slack either side — so a check of the middle alone passes before
            // the fix and says nothing.
            RenderTestWorld world = ShelfOnFlatGround();
            var expected = new CellRef(ShelfX, ShelfZ, Layer);

            for (int step = 1; step < 10; step++)
            {
                float z = ShelfZ + step * 0.1f;
                Assert.That(Pick(world, ShelfX + 0.5f, z), Is.EqualTo(expected),
                    $"aiming at the shelf {step / 10f:0.0} cells along it");
            }
        }

        [Test]
        public void TheGroundInFrontOfAShelfIsStillTheGround()
        {
            RenderTestWorld world = ShelfOnFlatGround();
            Assert.That(Pick(world, ShelfX + 0.5f, ShelfZ - 0.5f).Z, Is.EqualTo(ShelfZ - 1),
                "a click a whole cell in front of the shelf must not reach it");
        }

        [Test]
        public void AShelfsMarkSitsOnTopOfItAndItsPickSitsOnTheDeck()
        {
            // The first thing in the game for which the two differ: a deconstruct mark at deck
            // height would be buried under a full shelf.
            RenderTestWorld world = ShelfOnFlatGround();
            int index = world.Model.Size.Index(ShelfX, ShelfZ, Layer);

            Assert.That(world.Model.StandHeight(index), Is.EqualTo(ShelfShape.DeckTop).Within(0.001f));
            Assert.That(world.Model.MarkHeight(index), Is.EqualTo(ShelfShape.Top).Within(0.001f));
            Assert.That(world.Model.MarkHeight(index), Is.GreaterThan(world.Model.StandHeight(index)));
        }

        [Test]
        public void AShelfDoesNotOccludeItsCell()
        {
            // An occluding shelf would break the picker, the wall-panel logic and the roof test at
            // once — and it is passable in the simulation, so it must not look solid here.
            RenderTestWorld world = ShelfOnFlatGround();
            Assert.That(world.Model.OccludesFace(world.Model.Size.Index(ShelfX, ShelfZ, Layer)), Is.False);
        }
    }
}
