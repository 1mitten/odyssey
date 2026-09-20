#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Events
{
    /// <summary>
    /// What a caller asks for when it fires an incident: which one, with what budget, and — one
    /// day — where. The same record serves the debug menu, a storyteller and a quest, which is
    /// the reason the worker takes it rather than a def index (design 23 §4).
    /// </summary>
    public readonly struct IncidentParms
    {
        /// <summary>The incident, as an index into <see cref="IncidentContent.Defs"/>.</summary>
        public readonly int Def;

        /// <summary>
        /// The severity budget. Zero today and read by nothing: the points curve is the
        /// storyteller's, and there is no storyteller. Declared so a worker that scales does not
        /// have to widen this struct on the day one exists.
        /// </summary>
        public readonly int Points;

        /// <summary>A landing cell the caller insists on, or null to let the worker choose.</summary>
        public readonly CellRef? Cell;

        public IncidentParms(int def, int points = 0, CellRef? cell = null)
        {
            Def = def;
            Points = points;
            Cell = cell;
        }
    }

    /// <summary>
    /// Everything a worker may reach while it decides and while it acts: the world it is in, the
    /// colony's cells and items, the content it was bound from, the ledger to write to and the
    /// air to launch into. Built per firing by <see cref="Incidents"/>; a worker holds nothing
    /// between calls.
    /// </summary>
    public sealed class IncidentContext
    {
        public IncidentContext(SimWorld world, PawnContext pawns, IncidentContent content,
            IncidentLedger ledger, Skyfallers skyfallers)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            Pawns = pawns ?? throw new ArgumentNullException(nameof(pawns));
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            Skyfallers = skyfallers ?? throw new ArgumentNullException(nameof(skyfallers));
        }

        public SimWorld World { get; }
        public PawnContext Pawns { get; }
        public IncidentContent Content { get; }
        public IncidentLedger Ledger { get; }
        public Skyfallers Skyfallers { get; }

        public CellGrid Cells => Pawns.Cells;
        public ColonyItems Items => Pawns.Items;
        public GridSize Size => Pawns.Size;
        public int Tick => World.CurrentTick;

        /// <summary>The stream for this tick and one named purpose. See <see cref="IncidentPurpose"/>.</summary>
        public DeterministicRandom Random(uint purpose) => World.RandomForTick(purpose);
    }

    /// <summary>
    /// What an incident does, separated from what it is (design 23 §4). The Def is data; this is
    /// the behaviour it names.
    ///
    /// <para><b>Two methods, and the split is the point.</b> <see cref="CanFireNow"/> is cheap and
    /// changes nothing: it answers whether the world can take this event right now — is there a
    /// column a drop can land in — so a scheduler that draws it can re-roll for free, and a debug
    /// row can say why it refused. <see cref="TryExecute"/> does it, and returns whether it did.
    /// A caller always asks the first before the second; a worker may assume so.</para>
    ///
    /// <para><b>A worker is discovered, not registered.</b> Like a <see cref="WorkGiver"/>, a
    /// concrete worker in this assembly joins by existing; <see cref="IncidentWorkerRegistry"/>
    /// finds it by <see cref="Name"/>, which is what a Def writes. It therefore owes a public
    /// parameterless constructor and must hold no state — one instance serves every world in the
    /// process.</para>
    /// </summary>
    public abstract class IncidentWorker
    {
        /// <summary>The name a Def's <c>worker</c> field spells. Stable; a save never stores it.</summary>
        public abstract string Name { get; }

        /// <summary>
        /// Check the fields of a Def that names this worker, at load, and throw a
        /// <see cref="DefLoadException"/> naming the culprit if they cannot be fired with. The
        /// worker owns its own parameters: the loader checks what every incident has — a key, a
        /// worker, an item the content carries — and leaves a stack range or a fall time to the
        /// one worker that reads them, so a second kind of incident is not held to the first's
        /// rules. The default accepts anything.
        /// </summary>
        public virtual void Validate(IncidentDef def, PawnContent pawns) { }

        public abstract bool CanFireNow(IncidentContext ctx, in IncidentParms parms);

        public abstract bool TryExecute(IncidentContext ctx, in IncidentParms parms);
    }

    /// <summary>
    /// Which incident workers exist at all, found the way <see cref="WorkGiverRegistry"/> finds
    /// work givers and for the same reason: adding an event must not mean editing a list somebody
    /// else owns. <c>Assets/Odyssey/Sim/link.xml</c> keeps a stripped build from deleting them.
    /// </summary>
    public static class IncidentWorkerRegistry
    {
        static Dictionary<string, IncidentWorker>? _byName;

        static Dictionary<string, IncidentWorker> ByName => _byName ??= Scan();

        /// <summary>Every worker's name, in ordinal order, for an error message.</summary>
        public static IReadOnlyCollection<string> Names
        {
            get
            {
                var names = new List<string>(ByName.Keys);
                names.Sort(string.CompareOrdinal);
                return names;
            }
        }

        /// <summary>The worker answering to a Def's <c>worker</c> field, or null.</summary>
        public static IncidentWorker? Resolve(string name) =>
            ByName.TryGetValue(name, out IncidentWorker worker) ? worker : null;

        static Dictionary<string, IncidentWorker> Scan()
        {
            var found = new Dictionary<string, IncidentWorker>(StringComparer.Ordinal);
            foreach (Type type in typeof(IncidentWorker).Assembly.GetTypes())
            {
                if (!typeof(IncidentWorker).IsAssignableFrom(type)) continue;
                if (type.IsAbstract || type.IsGenericTypeDefinition) continue;

                if (type.GetConstructor(Type.EmptyTypes) == null)
                    throw new InvalidOperationException(
                        $"{type.FullName} is an incident worker with no public parameterless constructor, " +
                        "so no Def can name it. Give it one; a worker holds no state.");

                var worker = (IncidentWorker)Activator.CreateInstance(type)!;
                if (found.TryGetValue(worker.Name, out IncidentWorker other))
                    throw new InvalidOperationException(
                        $"{type.FullName} and {other.GetType().FullName} both answer to '{worker.Name}', " +
                        "so a Def naming it would fire whichever the scan met first.");
                found[worker.Name] = worker;
            }

            if (found.Count == 0)
                throw new InvalidOperationException(
                    "No incident workers were found in the simulation assembly. Either every worker has " +
                    "been deleted, or a stripped build has removed types nothing references by " +
                    "name — see Assets/Odyssey/Sim/link.xml.");
            return found;
        }
    }
}
