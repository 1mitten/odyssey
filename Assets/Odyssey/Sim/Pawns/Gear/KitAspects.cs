#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// What a colonist's kit publishes (design 54 §6): for each <b>filled</b> slot, the thing's def
    /// and how many, as two sparse pawn aspects — <c>odyssey.pawn.kit.&lt;slot&gt;</c> and
    /// <c>odyssey.pawn.kit.&lt;slot&gt;.count</c>. An empty slot publishes nothing, so a colony with
    /// no kits publishes exactly what it did before them. The Hud keeps its own copy of the names,
    /// held to these by a contract test, as the weapon's are.
    /// </summary>
    public static class KitAspects
    {
        public const string Prefix = "odyssey.pawn.kit.";

        static readonly AspectKey[] Defs = Build(string.Empty);
        static readonly AspectKey[] Counts = Build(".count");

        static AspectKey[] Build(string suffix)
        {
            var keys = new AspectKey[Kit.Slots];
            for (int slot = 0; slot < keys.Length; slot++) keys[slot] = AspectKey.Of(Name(slot) + suffix);
            return keys;
        }

        /// <summary>The name of <paramref name="slot"/>'s def aspect.</summary>
        public static string Name(int slot) => Prefix + slot.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The def of the thing in <paramref name="slot"/>.</summary>
        public static AspectKey Def(int slot) => Defs[slot];

        /// <summary>How many are in <paramref name="slot"/>.</summary>
        public static AspectKey Count(int slot) => Counts[slot];

        /// <summary>Publish <paramref name="pawn"/>'s filled slots.</summary>
        public static void Publish(SnapshotWriter writer, Pawn pawn, PawnContext ctx)
        {
            for (int slot = 0; slot < Kit.Slots; slot++)
            {
                ColonyItem? held = Kit.Held(pawn, ctx, slot);
                if (held == null) continue;
                writer.AddPawnAspect(pawn.Id, Defs[slot], held.DefIndex);
                writer.AddPawnAspect(pawn.Id, Counts[slot], held.Stack);
            }
        }
    }
}
