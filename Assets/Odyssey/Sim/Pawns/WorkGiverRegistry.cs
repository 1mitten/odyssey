#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Which work givers exist at all.
    ///
    /// <para><b>Why this is not a list.</b> It was one: <c>JobSystem.DefaultGivers()</c> returned a
    /// hardcoded array, so every new kind of work — felling, then mining, and every job after them
    /// — had to edit the file that owns the job pipeline. That is the second chokepoint named in
    /// <c>docs/plans/vertical-slice.md</c>, "Where the seams are": the mining line was 73 files and
    /// six of them belonged to everybody, and this was one of the six. A giver now joins the scan
    /// by <em>existing</em>. Writing the class is the whole of adding it.</para>
    ///
    /// <para><b>Discovery order is not scan order and cannot become it.</b> This hands back the
    /// givers in a fixed order — by type name, so that the input to the sort is the same on every
    /// run and on every machine — and <see cref="JobSystem"/> then sorts them by the emergency
    /// flag, the work type's <c>order</c> from the Defs, the giver's own
    /// <see cref="WorkGiver.IntraPriority"/> and finally its name. Cutting scans before mining
    /// before hauling because <c>WorkTypes.xml</c> says 0, 1, 2, never because of the order
    /// anything was found or registered in. <c>WorkGiverRegistrationTests</c> pins both halves:
    /// the shipped order, and that no permutation of the input changes it.</para>
    ///
    /// <para><b>The one thing a giver owes.</b> A public parameterless constructor. A giver that
    /// wants arguments cannot introduce itself, so it must be handed to the composition instead
    /// (<see cref="SimWorldBuilder.AddWorkGiver"/>); this throws rather than skipping it, because
    /// a work giver that silently never runs is a colony that silently never does that job.</para>
    ///
    /// <para><b>Reflection and stripping.</b> These types are referenced by nothing but this scan,
    /// so a managed-code-stripped player build would be entitled to delete them.
    /// <c>Assets/Odyssey/Sim/link.xml</c> preserves the assembly for exactly that reason. The
    /// editor, the tests and both fast-tier mirror projects never strip, so the fault could only
    /// ever appear in a player build — which is the worst place to meet it, and the cheapest to
    /// forestall.</para>
    /// </summary>
    public static class WorkGiverRegistry
    {
        /// <summary>
        /// Cached because the answer is a property of the assembly, which cannot change while the
        /// process lives. It is derived state, not mutable state: every caller gets the same list
        /// of types and its own fresh instances, so no world can ever share a giver with another.
        /// </summary>
        static Type[]? _kinds;

        /// <summary>Every concrete <see cref="WorkGiver"/> in the simulation assembly, by type name.</summary>
        public static IReadOnlyList<Type> Kinds => _kinds ??= Scan();

        /// <summary>A fresh instance of every discovered giver, in the fixed discovery order.</summary>
        public static WorkGiver[] Discover()
        {
            var kinds = Kinds;
            var givers = new WorkGiver[kinds.Count];
            for (int i = 0; i < kinds.Count; i++)
                givers[i] = (WorkGiver)Activator.CreateInstance(kinds[i])!;
            return givers;
        }

        static Type[] Scan()
        {
            var found = new List<Type>();
            foreach (Type type in typeof(WorkGiver).Assembly.GetTypes())
            {
                if (!typeof(WorkGiver).IsAssignableFrom(type)) continue;
                if (type.IsAbstract || type.IsGenericTypeDefinition) continue;

                if (type.GetConstructor(Type.EmptyTypes) == null)
                    throw new InvalidOperationException(
                        $"{type.FullName} is a work giver with no public parameterless constructor, " +
                        "so it cannot introduce itself to the job scan. Give it one, or keep it out " +
                        "of the simulation assembly and register it through " +
                        "SimWorldBuilder.AddWorkGiver, which is the seam for a giver that needs " +
                        "something handed to it.");

                found.Add(type);
            }

            if (found.Count == 0)
                throw new InvalidOperationException(
                    "No work givers were found in the simulation assembly. Either every giver has " +
                    "been deleted, or a stripped build has removed types nothing references by " +
                    "name — see Assets/Odyssey/Sim/link.xml.");

            // Ordinal on the full name: a total order over distinct types, independent of the
            // locale and of whatever order the runtime happened to hand the types back in.
            found.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));
            return found.ToArray();
        }
    }
}
