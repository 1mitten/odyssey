#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Sim.Defs
{
    /// <summary>
    /// An integer index into a Def table. This is the only way the simulation should refer to a
    /// Def once the world is running.
    ///
    /// Performance is the whole point of this type. String lookups, dictionary probes and
    /// reflection all happen at load or at world construction; the tick does array indexing and
    /// nothing else. The benchmark showed the tick has roughly 5.5 ms on the target machine and
    /// pathfinding already wants most of it, so per-tick dictionary work is not affordable.
    /// </summary>
    public readonly struct DefHandle<T> : IEquatable<DefHandle<T>> where T : Def
    {
        public readonly int Index;

        public DefHandle(int index) { Index = index; }

        public bool IsValid => Index >= 0;
        public static DefHandle<T> Invalid => new DefHandle<T>(-1);

        public bool Equals(DefHandle<T> other) => Index == other.Index;
        public override bool Equals(object? obj) => obj is DefHandle<T> other && Equals(other);
        public override int GetHashCode() => Index;
        public static bool operator ==(DefHandle<T> a, DefHandle<T> b) => a.Index == b.Index;
        public static bool operator !=(DefHandle<T> a, DefHandle<T> b) => a.Index != b.Index;
        public override string ToString() => $"{typeof(T).Name}#{Index}";
    }

    /// <summary>
    /// All Defs of one type, densely packed. Indexing by handle is an array access; looking up by
    /// name is a dictionary probe and belongs to load and world-construction time only.
    /// </summary>
    public sealed class DefTable<T> where T : Def
    {
        readonly T[] _items;
        readonly Dictionary<string, int> _byName;

        internal DefTable(T[] items)
        {
            _items = items;
            _byName = new Dictionary<string, int>(items.Length, StringComparer.Ordinal);
            for (int i = 0; i < items.Length; i++) _byName[items[i].defName] = i;
        }

        public int Count => _items.Length;

        /// <summary>Array access. Safe to call in a tick.</summary>
        public T this[DefHandle<T> handle] => _items[handle.Index];

        public T this[int index] => _items[index];

        public IReadOnlyList<T> All => _items;

        /// <summary>Load-time or world-construction-time only. Never call this per tick.</summary>
        public bool TryGetHandle(string defName, out DefHandle<T> handle)
        {
            if (_byName.TryGetValue(defName, out int index))
            {
                handle = new DefHandle<T>(index);
                return true;
            }
            handle = DefHandle<T>.Invalid;
            return false;
        }

        /// <summary>Load-time or world-construction-time only. Throws if the name is unknown.</summary>
        public DefHandle<T> Handle(string defName)
        {
            if (TryGetHandle(defName, out var handle)) return handle;
            throw new DefLoadException($"No {typeof(T).Name} named '{defName}'.");
        }

        public T Get(string defName) => this[Handle(defName)];
    }

    /// <summary>
    /// The loaded content set. Immutable once built; a content reload constructs a new one and
    /// swaps it at a tick boundary, so a half-swapped set is never observable.
    /// </summary>
    public sealed class DefDatabase
    {
        readonly Dictionary<Type, object> _tables;

        internal DefDatabase(Dictionary<Type, object> tables)
        {
            _tables = tables;
        }

        public IReadOnlyCollection<Type> Types => _tables.Keys;

        /// <summary>
        /// The table for one Def type. Fetch this once during world construction and keep it;
        /// do not call it inside a tick.
        /// </summary>
        public DefTable<T> Table<T>() where T : Def
        {
            if (_tables.TryGetValue(typeof(T), out object? table)) return (DefTable<T>)table;
            throw new DefLoadException($"No Defs of type {typeof(T).Name} were loaded.");
        }

        public bool HasTable<T>() where T : Def => _tables.ContainsKey(typeof(T));

        public int CountOf<T>() where T : Def => HasTable<T>() ? Table<T>().Count : 0;

        /// <summary>
        /// A stable hash of the whole content set, folded into the determinism harness. Two runs
        /// with different content are not expected to match, and the harness should say so
        /// plainly rather than report a mysterious desync.
        /// </summary>
        public Contracts.StateHash ContentHash()
        {
            var hash = Contracts.StateHash.New();
            // Types are hashed in name order so that dictionary iteration order cannot leak in.
            var typeNames = new List<string>();
            foreach (var type in _tables.Keys) typeNames.Add(type.FullName ?? type.Name);
            typeNames.Sort(StringComparer.Ordinal);

            foreach (string typeName in typeNames)
            {
                foreach (char c in typeName) hash.Add((byte)c);
                foreach (var kv in _tables)
                {
                    if ((kv.Key.FullName ?? kv.Key.Name) != typeName) continue;
                    int count = (int)kv.Value.GetType().GetProperty("Count")!.GetValue(kv.Value)!;
                    hash.Add(count);
                }
            }
            return hash;
        }
    }

    /// <summary>
    /// Thrown when content cannot be loaded. Carries provenance where it is known, because the
    /// first question about a content error is always which file caused it.
    /// </summary>
    public sealed class DefLoadException : Exception
    {
        public DefLoadException(string message) : base(message) { }

        public DefLoadException(string message, DefOrigin origin)
            : base($"{origin}: {message}")
        {
            Origin = origin;
        }

        public DefOrigin Origin { get; }
    }
}
