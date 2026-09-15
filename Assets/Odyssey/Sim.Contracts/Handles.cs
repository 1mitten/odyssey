#nullable enable
using System;

namespace Odyssey.Sim.Contracts
{
    /// <summary>
    /// A cell address. Always (x, z, y) with y as the layer, never (x, y, z) — the simulation
    /// never speaks in metres and never assumes a Y-up world; that conversion belongs to
    /// presentation alone.
    /// </summary>
    public readonly struct CellRef : IEquatable<CellRef>
    {
        public readonly int X;
        public readonly int Z;
        public readonly int Y;

        public CellRef(int x, int z, int y) { X = x; Z = z; Y = y; }

        /// <summary>The cell one layer up. Adjacency is not connectivity: a slab may sit between them.</summary>
        public CellRef Above => new CellRef(X, Z, Y + 1);
        public CellRef Below => new CellRef(X, Z, Y - 1);

        public bool Equals(CellRef other) => X == other.X && Z == other.Z && Y == other.Y;
        public override bool Equals(object? obj) => obj is CellRef other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397 ^ Z) * 397 ^ Y);
        public static bool operator ==(CellRef a, CellRef b) => a.Equals(b);
        public static bool operator !=(CellRef a, CellRef b) => !a.Equals(b);
        public override string ToString() => $"({X},{Z},L{Y})";
    }

    /// <summary>
    /// A stable pawn identity. Handles, not references, cross the snapshot boundary and go into
    /// save files: a UI panel holding a dead pawn's id simply finds no entry in the next snapshot
    /// and closes cleanly, where a reference would dereference freed state.
    /// </summary>
    public readonly struct PawnId : IEquatable<PawnId>
    {
        public readonly int Value;
        public PawnId(int value) { Value = value; }
        public bool IsValid => Value > 0;
        public static PawnId None => new PawnId(0);

        public bool Equals(PawnId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is PawnId other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(PawnId a, PawnId b) => a.Value == b.Value;
        public static bool operator !=(PawnId a, PawnId b) => a.Value != b.Value;
        public override string ToString() => IsValid ? $"pawn#{Value}" : "pawn#none";
    }

    /// <summary>A stable identity for anything placeable that is not a pawn.</summary>
    public readonly struct ThingId : IEquatable<ThingId>
    {
        public readonly int Value;
        public ThingId(int value) { Value = value; }
        public bool IsValid => Value > 0;
        public static ThingId None => new ThingId(0);

        public bool Equals(ThingId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is ThingId other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(ThingId a, ThingId b) => a.Value == b.Value;
        public static bool operator !=(ThingId a, ThingId b) => a.Value != b.Value;
        public override string ToString() => IsValid ? $"thing#{Value}" : "thing#none";
    }
}
