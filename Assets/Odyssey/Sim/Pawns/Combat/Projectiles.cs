#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Every bullet in flight (design 47 §2c), modelled on <c>Skyfallers</c>: <b>the flight is
    /// simulated; only the drawing is not.</b> A shot is decided the tick it is fired — aimed or
    /// not, how hard, where it ends — and lands on the tick decided then, so a save taken mid-flight
    /// lands it on the same tick a run that was never saved would. What it hits is decided at the
    /// landing, against whoever is on its line then: a target can step out of a long shot.
    ///
    /// <para><b>Deliberately not ticked by itself</b>, unlike <c>Skyfallers</c>: <see cref="CombatSystem"/>
    /// lands the bullets due at the top of its own pass — the Pawns phase, after the jobs and before
    /// anybody steps — for the reason a swing lands there.</para>
    ///
    /// <para><b>Hashed only while something is in flight</b>, the corpse registry's rule, so a colony
    /// that has never fired hashes exactly as it did before guns and registering this moved no
    /// golden. <b>Saved</b> in its own keyed section, absent from an older file, which loads with
    /// nothing in the air.</para>
    /// </summary>
    public class Projectiles : IStateHashable, ISaveable, ISnapshotContributor
    {
        /// <summary>One bullet. A class so the list can hand out references; it holds no handles.</summary>
        public sealed class Entry
        {
            public int Shooter;

            /// <summary>Whom it was aimed at, or nought.</summary>
            public int Target;

            /// <summary>The gun, as an item def index.</summary>
            public int Weapon;

            public int StartCell;
            public int EndCell;
            public int FireTick;
            public int ImpactTick;

            /// <summary>The hit roll succeeded: <see cref="EndCell"/> is the target's cell as it stood when fired.</summary>
            public bool Aimed;

            /// <summary>
            /// The target was already down when she fired, on an order to finish it
            /// (<c>ToTheDeath</c>): a downed target is hit only on such an order.
            /// </summary>
            public bool ToTheDeath;

            public int DamageMilli;
        }

        readonly List<Entry> _inFlight = new List<Entry>();
        readonly GridSize _size;

        public Projectiles(GridSize size) => _size = size;

        /// <summary>Every bullet in the air, in the order fired.</summary>
        public IReadOnlyList<Entry> InFlight => _inFlight;

        public int Count => _inFlight.Count;

        public Entry Launch(int shooter, int target, int weapon, int startCell, int endCell, int fireTick, int impactTick,
            bool aimed, bool toTheDeath, int damageMilli)
        {
            if (impactTick <= fireTick) throw new ArgumentOutOfRangeException(nameof(impactTick));
            var entry = new Entry
            {
                Shooter = shooter,
                Target = target,
                Weapon = weapon,
                StartCell = startCell,
                EndCell = endCell,
                FireTick = fireTick,
                ImpactTick = impactTick,
                Aimed = aimed,
                ToTheDeath = toTheDeath,
                DamageMilli = damageMilli,
            };
            _inFlight.Add(entry);
            return entry;
        }

        /// <summary>
        /// Take out, in the order fired, every bullet due at or before <paramref name="tick"/>, into
        /// <paramref name="due"/> (cleared first). The list is compacted in place, so this allocates
        /// nothing once the caller's list has grown.
        /// </summary>
        public void TakeDue(int tick, List<Entry> due)
        {
            due.Clear();
            int keep = 0;
            for (int i = 0; i < _inFlight.Count; i++)
            {
                Entry entry = _inFlight[i];
                if (entry.ImpactTick <= tick) due.Add(entry);
                else _inFlight[keep++] = entry;
            }
            if (keep < _inFlight.Count) _inFlight.RemoveRange(keep, _inFlight.Count - keep);
        }

        public void ContributeTo(ref StateHash hash)
        {
            // Nothing at all while nothing is in the air: a colony that has never fired hashes as
            // it did before guns (design 47 §2c).
            if (_inFlight.Count == 0) return;
            hash.Add(_inFlight.Count);
            for (int i = 0; i < _inFlight.Count; i++)
            {
                Entry e = _inFlight[i];
                hash.Add(e.Shooter);
                hash.Add(e.Target);
                hash.Add(e.Weapon);
                hash.Add(e.StartCell);
                hash.Add(e.EndCell);
                hash.Add(e.FireTick);
                hash.Add(e.ImpactTick);
                hash.Add(e.Aimed ? 1 : 0);
                hash.Add(e.ToTheDeath ? 1 : 0);
                hash.Add(e.DamageMilli);
            }
        }

        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            for (int i = 0; i < _inFlight.Count; i++)
            {
                Entry e = _inFlight[i];
                writer.AddProjectile(new ProjectileView(new PawnId(e.Shooter), new PawnId(e.Target),
                    _size.FromIndex(e.StartCell), _size.FromIndex(e.EndCell), e.FireTick, e.ImpactTick, e.Weapon));
            }
        }

        public string SaveKey => "odyssey.projectiles";

        public void Save(SaveWriter writer)
        {
            writer.Write(_inFlight.Count);
            for (int i = 0; i < _inFlight.Count; i++)
            {
                Entry e = _inFlight[i];
                writer.Write(e.Shooter);
                writer.Write(e.Target);
                writer.Write(e.Weapon);
                writer.Write(e.StartCell);
                writer.Write(e.EndCell);
                writer.Write(e.FireTick);
                writer.Write(e.ImpactTick);
                writer.Write((e.Aimed ? 1 : 0) | (e.ToTheDeath ? 2 : 0));
                writer.Write(e.DamageMilli);
            }
        }

        public void Load(SaveReader reader)
        {
            _inFlight.Clear();
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                var e = new Entry
                {
                    Shooter = reader.ReadInt(),
                    Target = reader.ReadInt(),
                    Weapon = reader.ReadInt(),
                    StartCell = reader.ReadInt(),
                    EndCell = reader.ReadInt(),
                    FireTick = reader.ReadInt(),
                    ImpactTick = reader.ReadInt(),
                };
                int flags = reader.ReadInt();
                e.Aimed = (flags & 1) != 0;
                e.ToTheDeath = (flags & 2) != 0;
                e.DamageMilli = reader.ReadInt();
                _inFlight.Add(e);
            }
        }
    }
}
