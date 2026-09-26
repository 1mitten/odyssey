#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;

namespace Odyssey.Sim.Pawns
{
    /// <summary>One corpse: who it was and how it lies. See <see cref="CorpseRegistry"/>.</summary>
    public readonly struct Corpse
    {
        /// <summary>The registry's own number, from 1, never reused.</summary>
        public readonly int Id;

        /// <summary>The <see cref="PawnId"/> value the pawn had while alive.</summary>
        public readonly int Pawn;

        public readonly int Kind;
        public readonly uint RollSeed;

        /// <summary>Where it lies, as a whole-world cell index.</summary>
        public readonly int Cell;

        /// <summary>The tick it died on.</summary>
        public readonly int Tick;

        /// <summary>Which way it fell: one of eight headings, 0 is +Z, clockwise from above.</summary>
        public readonly byte Facing;

        /// <summary>
        /// She had joined the colony (design 60 §16 #5): her kind is a raider's but her side was
        /// ours, and the corpse is drawn and named as the side she died on. Asked of
        /// <see cref="Allegiance"/> at the death, never of the kind.
        /// </summary>
        public readonly bool Joined;

        public Corpse(int id, int pawn, int kind, uint rollSeed, int cell, int tick, byte facing, bool joined = false)
        {
            Id = id;
            Pawn = pawn;
            Kind = kind;
            RollSeed = rollSeed;
            Cell = cell;
            Tick = tick;
            Facing = facing;
            Joined = joined;
        }
    }

    /// <summary>
    /// The dead (design 33 §1, §3): <b>a corpse is not a pawn</b>. When a pawn dies it leaves the
    /// registry — every per-pawn loop would otherwise have to skip the dead, and wildlife already
    /// made every consumer survive a pawn vanishing — and a record here keeps who it was, what
    /// kind, which seed, where it lies, when it fell and which way.
    ///
    /// <para><b>Saved (<c>odyssey.corpses</c>) and hashed only while there is one</b>, so a colony
    /// in which nobody has died saves an empty section and hashes exactly as it did before combat.
    /// Corpses stay where they fell indefinitely (the C2 default): nothing hauls, buries or rots
    /// them yet, so the list only grows. Published whole, one <see cref="CorpseView"/> each.</para>
    ///
    /// <para><b>Lane A decides when a record is made</b> (<see cref="Add"/>, from the death it
    /// defers); this class only keeps them. <b>Scales with</b> the corpses on the board, which a
    /// colony counts on its fingers.</para>
    /// </summary>
    public sealed class CorpseRegistry : IStateHashable, ISaveable, ISnapshotContributor
    {
        readonly List<Corpse> _corpses = new List<Corpse>();
        readonly GridSize _size;
        readonly PawnContent _content;
        int _nextId = 1;

        public CorpseRegistry(GridSize size, PawnContent content)
        {
            _size = size;
            _content = content ?? throw new System.ArgumentNullException(nameof(content));
        }

        public int Count => _corpses.Count;

        public Corpse this[int index] => _corpses[index];

        /// <summary>Write one death down, as the pawn stood at the moment it died. Returns the corpse's id.</summary>
        public int Add(Pawn pawn, int tick, byte facing)
        {
            if (pawn == null) throw new System.ArgumentNullException(nameof(pawn));
            int id = _nextId++;
            _corpses.Add(new Corpse(id, pawn.Id.Value, pawn.Kind, pawn.RollSeed, pawn.Cell, tick, (byte)(facing & 7),
                pawn.Prison != null && pawn.Prison.Joined));
            return id;
        }

        /// <summary>The corpse with this id, or false. A scan: there are a handful.</summary>
        public bool TryGet(int id, out Corpse corpse)
        {
            for (int i = 0; i < _corpses.Count; i++)
            {
                if (_corpses[i].Id != id) continue;
                corpse = _corpses[i];
                return true;
            }
            corpse = default;
            return false;
        }

        public void ContributeTo(ref StateHash hash)
        {
            // Nothing at all while nobody has died: a colony that has never fought hashes as it
            // did before combat (design 33 §5).
            if (_corpses.Count == 0) return;

            hash.Add(_corpses.Count);
            hash.Add(_nextId);
            for (int i = 0; i < _corpses.Count; i++)
            {
                Corpse c = _corpses[i];
                hash.Add(c.Id);
                hash.Add(c.Pawn);
                hash.Add(c.Kind);
                hash.Add(unchecked((int)c.RollSeed));
                hash.Add(c.Cell);
                hash.Add(c.Tick);
                hash.Add(FacingWord(c));
            }
        }

        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            for (int i = 0; i < _corpses.Count; i++)
            {
                Corpse c = _corpses[i];
                PawnFlags flags = PawnFlags.None;
                if ((uint)c.Kind < (uint)System.Math.Max(1, _content.Kinds.Length))
                {
                    if (_content.SpeciesOf(c.Kind).person) flags |= PawnFlags.Person;
                    if (!c.Joined && _content.KindOf(c.Kind).faction == Faction.Hostile) flags |= PawnFlags.Hostile;
                }
                writer.AddCorpse(new CorpseView(c.Id, new PawnId(c.Pawn), c.Kind, c.RollSeed,
                    _size.FromIndex(c.Cell), c.Tick, c.Facing, flags));
            }
        }

        public string SaveKey => "odyssey.corpses";

        public void Save(SaveWriter writer)
        {
            writer.Write(_nextId);
            writer.Write(_corpses.Count);
            for (int i = 0; i < _corpses.Count; i++)
            {
                Corpse c = _corpses[i];
                writer.Write(c.Id);
                writer.Write(c.Pawn);
                writer.Write(c.Kind);
                writer.Write(unchecked((int)c.RollSeed));
                writer.Write(c.Cell);
                writer.Write(c.Tick);
                writer.Write(FacingWord(c));
            }
        }

        public void Load(SaveReader reader)
        {
            _corpses.Clear();
            _nextId = reader.ReadInt();
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int id = reader.ReadInt(), pawn = reader.ReadInt(), kind = reader.ReadInt();
                uint seed = unchecked((uint)reader.ReadInt());
                int cell = reader.ReadInt(), tick = reader.ReadInt(), word = reader.ReadInt();
                _corpses.Add(new Corpse(id, pawn, kind, seed, cell, tick, (byte)(word & 7), (word & JoinedBit) != 0));
            }
        }

        /// <summary>
        /// The facing's word also carries <see cref="Corpse.Joined"/>, above the three bits a heading
        /// uses (design 60 §16 #5). The facing was always written as a whole int, so a save from
        /// before reads unchanged, and a corpse that had not joined saves and hashes as it did —
        /// no format bump, no golden moved.
        /// </summary>
        const int JoinedBit = 1 << 8;

        static int FacingWord(Corpse c) => c.Facing | (c.Joined ? JoinedBit : 0);
    }
}
