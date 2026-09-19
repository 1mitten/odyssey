#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// How a loose stack of rubble is drawn: as several small rocks scattered over the cell floor,
    /// rather than as one prop standing in the middle of it.
    ///
    /// <para><b>Why this exists.</b> Every other item is one prop — a crate of rations, a pile of
    /// logs — and that is right for things that come in containers and bundles. Stone does not.
    /// The first version used <c>SM_Prop_StonePile_01</c>, a cairn 1.48 m tall, and eight stone
    /// knocked off a rock face duly appeared as a chest-high monument to themselves. The owner's
    /// words were "just a few gray rocks would be fine and not a weird pile", and a few grey rocks
    /// is not a prop we own: it is several copies of one small boulder, put down in different
    /// places.</para>
    ///
    /// <para><b>And it is how the amount is read.</b> The count grows with the stack, so a fresh
    /// drop is two or three rocks and a full stockpile square is a heap of them. That was the
    /// owner's second decision — a bigger pile should look bigger — and count carries it better
    /// than size would: eight stone scaled down to a quarter of a boulder reads as one small rock,
    /// where three rocks read as three rocks. Nothing else in the game does this, which is the
    /// cost; rubble is the only thing we own that is genuinely a heap of identical lumps.</para>
    ///
    /// <para><b>Free, or nearly.</b> Items are already grouped by def and submitted instanced, so
    /// seven rocks in a cell are seven matrices in a buffer that was going to be submitted anyway,
    /// not seven draw calls. <see cref="Most"/> caps it at a number a stockpile can afford.</para>
    /// </summary>
    public static class ItemHeap
    {
        /// <summary>The most rocks any one stack will ever draw. A cap on the instance count.</summary>
        public const int Most = 7;

        /// <summary>
        /// What one kind of rubble looks like on the floor.
        ///
        /// <see cref="Full"/> is presentation's own idea of a full stack, not the simulation's
        /// <c>ItemDef.stackLimit</c>: the contract publishes a stack size and not the limit, and
        /// making the renderer depend on a content number to draw a rock would be the wrong
        /// coupling for a cosmetic ramp. If the two drift, the heap simply reaches its full size
        /// early or late, which is a difference of one rock.
        /// </summary>
        public readonly struct Recipe
        {
            /// <summary>Rocks drawn for the smallest possible stack. Never fewer than one.</summary>
            public readonly int Fewest;

            /// <summary>Rocks drawn at <see cref="Full"/> and beyond.</summary>
            public readonly int Biggest;

            /// <summary>The stack size at which the heap stops growing.</summary>
            public readonly int Full;

            /// <summary>How far from the cell centre the outermost rock sits, in metres.</summary>
            public readonly float Spread;

            /// <summary>How much bigger or smaller than its catalogue size a rock may be drawn.</summary>
            public readonly float SizeJitter;

            /// <summary>
            /// Whether a carried load of this is drawn as a heap in the arms too.
            ///
            /// <para>True for rubble, which is what this class was written for: three rocks
            /// cradled read as an armful. False for wood, whose prop is a bound log pile — one
            /// bundle is a load a person carries and three bundles stacked in two hands is a
            /// circus act. The ground heap and the armful are separate questions and were only
            /// ever the same answer because rubble was the only thing that scattered.</para>
            /// </summary>
            public readonly bool CarriedAsHeap;

            public Recipe(int fewest, int biggest, int full, float spread, float sizeJitter,
                bool carriedAsHeap = true)
            {
                Fewest = fewest;
                Biggest = biggest;
                Full = full;
                Spread = spread;
                SizeJitter = sizeJitter;
                CarriedAsHeap = carriedAsHeap;
            }
        }

        /// <summary>
        /// The rubble recipes, by item def index, in <c>ItemIndex</c> order.
        ///
        /// What a mine leaves is a heap, and so is wood — a null row means "one prop, in the
        /// middle of the cell", which is what every item did before this existed and what rations
        /// in a crate still do — as does a pulled harvest, which is loose carrots and
        /// nothing more: a pile saying "one carrot" where five came out of the plot is the
        /// fault the carrots row below exists to fix.
        ///
        /// The three that are heaps share a shape and differ in silhouette, because items carry
        /// no per-item tint and shape is the only axis there is: stone is squat boulders, iron ore
        /// is taller shards, coal is low rubble. The spreads differ with them — a shard needs less
        /// floor than a boulder.
        /// </summary>
        static readonly Recipe?[] Recipes =
        {
            null,                                        // meal
            null,                                        // salvage

            // **Wood, and the reason its numbers are not stone's.** The owner could not tell a
            // tile of 3 wood from a tile of 75 (2026-09-19) — and could not, because every stack
            // drew the one LogPile prop at the one place. It scatters now like the rubble does,
            // but one to three bundles rather than two to seven: a bound pile of logs is a wide
            // prop where a boulder is a small one, and seven of them in a 2.5 m cell is a
            // log-jam rather than a stock. One, two, three is also the ramp asked for in the
            // same breath — a third, two thirds, full — and a tree yields 27 into a limit of 75,
            // so those three steps land near one tree, two trees and a full square.
            new Recipe(1, 3, 75, 0.52f, 0.10f, carriedAsHeap: false),   // wood

            new Recipe(2, Most, 75, 0.62f, 0.22f),       // stone
            new Recipe(2, 6, 75, 0.55f, 0.20f),          // iron ore
            new Recipe(3, Most, 75, 0.66f, 0.18f),       // coal
            // **Carrots, and the one number that is not like the others.** A pulled harvest is
            // loose carrots and nothing contains them (owner, 2026-09-19: the pile drew one
            // prop where five had come out of the plot). Alone of the heaps its Full is the
            // count a harvest actually drops - the carrot's own yieldCount - so the ramp from
            // one to a yield is the identity: five grew, five lie there. Carried as the rubble
            // armful, which three cradled carrots read as naturally.
            new Recipe(1, Most, 7, 0.45f, 0.18f),        // carrots
        };

        /// <summary>Whether this item kind is drawn as scattered rubble at all.</summary>
        public static bool IsHeap(int defIndex) => TryRecipe(defIndex, out _);

        /// <summary>The recipe for an item def, or false when the item is drawn as a single prop.</summary>
        public static bool TryRecipe(int defIndex, out Recipe recipe)
        {
            if (defIndex >= 0 && defIndex < Recipes.Length && Recipes[defIndex].HasValue)
            {
                recipe = Recipes[defIndex]!.Value;
                return true;
            }
            recipe = default;
            return false;
        }

        /// <summary>
        /// How many rocks a stack of this size draws as.
        ///
        /// Linear between <see cref="Recipe.Fewest"/> at one and <see cref="Recipe.Biggest"/> at
        /// <see cref="Recipe.Full"/>, and never outside that band however odd the stack. A stack
        /// of zero should not exist, but if one is ever published it still draws as something
        /// rather than as nothing: an item you can see and cannot pick up is confusing, and an
        /// item you can pick up and cannot see is worse.
        /// </summary>
        public static int RockCount(int stack, in Recipe recipe)
        {
            if (recipe.Biggest <= recipe.Fewest) return Mathf.Max(1, recipe.Fewest);
            if (stack >= recipe.Full) return recipe.Biggest;
            if (stack <= 1) return Mathf.Max(1, recipe.Fewest);

            float through = (stack - 1f) / Mathf.Max(1f, recipe.Full - 1f);
            int rocks = recipe.Fewest + Mathf.RoundToInt(through * (recipe.Biggest - recipe.Fewest));
            return Mathf.Clamp(rocks, Mathf.Max(1, recipe.Fewest), recipe.Biggest);
        }

        /// <summary>
        /// Place the rocks of one stack and return how many were written.
        ///
        /// <para><b>The pattern is a sunflower, not a random scatter.</b> Rock <c>i</c> goes at the
        /// golden angle from rock <c>i-1</c>, at a radius proportional to the square root of its
        /// index, which is the arrangement that spaces points evenly over a disc for any count.
        /// Independent random offsets do not: at seven rocks in a 2.5 m cell they clump often
        /// enough to be noticed, and two boulders in the same place read as one bad boulder. The
        /// whole heap is then turned by an angle taken from the item's own id, so that two piles
        /// side by side are not the same pile twice.</para>
        ///
        /// <para>Everything here is derived from <paramref name="seed"/> and nothing from a
        /// generator: presentation must never consume simulation randomness, and a heap has to
        /// come back the same way round after a reload.</para>
        /// </summary>
        public static int Place(int stack, uint seed, Vector3 floorCentre, in Recipe recipe,
            System.Span<Matrix4x4> into)
        {
            int rocks = Mathf.Min(RockCount(stack, recipe), into.Length);

            // 137.5 degrees: turn by this each time and points never line up into spokes.
            const float GoldenAngle = 137.507764f;
            float turn = Fraction(seed, 11u) * 360f;

            for (int i = 0; i < rocks; i++)
            {
                float angle = turn + i * GoldenAngle;

                // sqrt spacing: equal area per rock, so the middle does not fill up first.
                float radius = rocks <= 1
                    ? 0f
                    : recipe.Spread * Mathf.Sqrt((i + 0.35f) / rocks);

                float radians = angle * Mathf.Deg2Rad;
                var at = new Vector3(
                    floorCentre.x + Mathf.Sin(radians) * radius,
                    floorCentre.y,
                    floorCentre.z + Mathf.Cos(radians) * radius);

                // Each rock its own bearing and its own size, or seven copies of one boulder in a
                // neat spiral read as a garden feature rather than as spoil.
                float yaw = Fraction(seed, (uint)(101 + i * 7)) * 360f;
                float size = 1f + (Fraction(seed, (uint)(211 + i * 13)) * 2f - 1f) * recipe.SizeJitter;

                into[i] = Matrix4x4.TRS(at, Quaternion.Euler(0f, yaw, 0f), new Vector3(size, size, size));
            }

            return rocks;
        }

        /// <summary>
        /// Rocks in an armful. Design 24 §3a: the same number whatever the stack.
        ///
        /// <para><b>Constant, where the heap on the floor is not</b>, and the asymmetry is the
        /// owner's decision rather than an oversight. The floor's count says how much is there
        /// because it can — seven rocks in a 2.5 m cell is a legible range. Two arms cannot hold
        /// seventy-five stone at true scale, so any honest scaling would run from one rock to
        /// three, and the bottom of that is a colonist walking forty metres holding a single
        /// pebble, which reads as a fault rather than as a light load. The amount moved to the
        /// activity line instead.</para>
        /// </summary>
        public const int ArmfulRocks = 3;

        /// <summary>How far from the middle of the armful the outermost rock sits, in metres.</summary>
        public const float ArmfulSpread = 0.17f;

        /// <summary>
        /// How much higher each rock after the first sits, in metres, so an armful reads as a
        /// heap held rather than as three rocks on an invisible shelf.
        /// </summary>
        public const float ArmfulStagger = 0.055f;

        /// <summary>
        /// Place the rocks of a carried armful about a point, turned to face the way its carrier
        /// is, and return how many were written.
        ///
        /// <para>The same sunflower as <see cref="Place"/>, from the same seed, so the rocks in a
        /// colonist's arms are the rocks that were in the pile: a heap picked up does not
        /// rearrange itself on the way. Only the spread tightens and the count is fixed.</para>
        ///
        /// <para><b>The whole armful turns about <paramref name="at"/>, not each rock about
        /// itself</b> (owner, 2026-09-19: a carried load should turn with its carrier). The
        /// sunflower is laid out on the world axes, so rotating the rocks in place would leave
        /// the cluster's shape pointing the same way however the colonist turned — which is the
        /// bug one layer up, in the same words.</para>
        /// </summary>
        public static int Armful(uint seed, Vector3 at, Quaternion facing, in Recipe recipe,
            System.Span<Matrix4x4> into)
        {
            var held = new Recipe(ArmfulRocks, ArmfulRocks, 1, ArmfulSpread, recipe.SizeJitter);
            int rocks = Place(1, seed, Vector3.zero, held, into);

            Matrix4x4 frame = Matrix4x4.TRS(at, facing, Vector3.one);

            for (int i = 0; i < rocks; i++)
            {
                Vector4 column = into[i].GetColumn(3);
                column.y += ArmfulStagger * i;
                into[i].SetColumn(3, column);

                // Composed, so a rock's own bearing and size survive the turn: the frame carries
                // the offset round and the rock keeps whatever face it was showing.
                into[i] = frame * into[i];
            }

            return rocks;
        }

        /// <summary>A stable 0..1 from an id and a salt. Cheap, and the same on every machine.</summary>
        static float Fraction(uint seed, uint salt)
        {
            uint h = seed * 2654435761u + salt * 2246822519u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            h *= 3266489917u;
            h ^= h >> 16;
            return (h & 0xFFFFFFu) / (float)0x1000000u;
        }
    }
}
