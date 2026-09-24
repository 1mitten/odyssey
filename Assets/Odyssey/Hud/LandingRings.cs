#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// <b>Where a selected colonist was sent, as a ring on the ground</b> (design 33 §20; owner,
    /// 2026-09-24: <i>"instead of using a square to indicate where to land when drafting people,
    /// can it be a ring that flashes temporarily or has a transition effect that makes sense"</i>).
    /// It replaces the floor bracket that stood on a drafted move's destination (§2g) and on the
    /// weapon's cell of an Equip (§7a), which share the order cell.
    ///
    /// <para><b>One ring per colonist, on her own cell.</b> A squad sent in one click is spread
    /// over several cells (§2d), so each wears its own. Keyed on the colonist <i>and</i> the cell:
    /// sent somewhere else, the old ring fades where it was while the new one snaps in.</para>
    ///
    /// <para><b>The lock-on ring's clock, not a copy of it</b> (§7b): it snaps in from
    /// <see cref="LockOnRing.StartScale"/> times its size over <see cref="LockOnRing.SnapSeconds"/>,
    /// flashes once as it lands, holds faint while she walks and fades over
    /// <see cref="LockOnRing.FadeSeconds"/> — every curve is <see cref="LockOnRing.Evaluate"/>, so
    /// the two rings read as one family and a retune of one retunes both. What is its own is the
    /// size (<see cref="Radius"/>) and the colour (<see cref="OrderColours.Move"/>, pale, so it can
    /// never be read as the red attack ring).</para>
    ///
    /// <para><b>Read off the frame, not the click</b>, as the lock-on is: a ring starts on the
    /// frame a selected colonist's published order cell (<c>odyssey.pawn.order.cell</c>) changes
    /// to a new cell, so a refused move draws nothing and the same move clicked again is quiet. An
    /// order already under way when the player first sees it — a colonist selected mid-walk, a
    /// world just loaded — is <b>adopted at rest</b>, not snapped. <b>It fades</b> when the cell
    /// stops being published — she arrived, she was undrafted, the order became something else —
    /// or when she is deselected, since only the selection's orders are drawn (§2g).</para>
    ///
    /// <para><b>Cost</b> scales with the selection, never the colony: one indexed aspect lookup per
    /// selected pawn and a walk of the rings. Nothing allocates once the lists have grown.</para>
    /// </summary>
    public sealed class LandingRings
    {
        /// <summary>
        /// The ring's outer radius at rest, in metres: 1.6 m across, well inside the 2.5 m cell
        /// and wider than a person (the lock-on's 0.58 m), so it reads as a <i>place</i> rather
        /// than a body. INVENTED, for the playtest.
        /// </summary>
        public const float Radius = 0.8f;

        /// <summary>One ring as it is to be drawn this frame.</summary>
        public readonly struct Ring
        {
            /// <summary>Whose destination it is.</summary>
            public readonly PawnId Pawn;

            /// <summary>The cell index she was sent to.</summary>
            public readonly int Cell;

            /// <summary>The ring's size as a multiple of <see cref="Radius"/>.</summary>
            public readonly float Scale;

            /// <summary>Its opacity, 0 to 1, quantised (<see cref="LockOnRing.Quantise"/>).</summary>
            public readonly float Alpha;

            /// <summary>True while she is still walking to it; false while it fades.</summary>
            public readonly bool Holding;

            public Ring(PawnId pawn, int cell, float scale, float alpha, bool holding)
            {
                Pawn = pawn;
                Cell = cell;
                Scale = scale;
                Alpha = alpha;
                Holding = holding;
            }
        }

        struct State
        {
            public int Pawn;
            public int Cell;
            public float OrderedAt;
            public float ReleasedAt; // NaN while held
            public bool HeldThisFrame;
        }

        readonly List<State> _states = new List<State>();
        readonly List<Ring> _rings = new List<Ring>();
        Dictionary<int, int> _lastCells = new Dictionary<int, int>();
        Dictionary<int, int> _nextCells = new Dictionary<int, int>();
        object? _world;
        bool _seenWorld;

        /// <summary>The rings to draw this frame, in the order they started.</summary>
        public IReadOnlyList<Ring> Rings => _rings;

        /// <summary>
        /// One frame. <paramref name="now"/> is seconds on a clock that only goes forwards — the
        /// bootstrap passes unscaled real time, so the snap reads the same at every game speed and
        /// while paused. A different <paramref name="world"/> clears every ring and memory, and
        /// the frame that sees it adopts rather than snaps.
        /// </summary>
        public void Update(WorldSnapshot snapshot, IReadOnlyList<PawnId> selection, float now, object? world)
        {
            bool first = !_seenWorld || !ReferenceEquals(world, _world);
            if (first)
            {
                _world = world;
                _seenWorld = true;
                _states.Clear();
                _lastCells.Clear();
            }

            for (int i = 0; i < _states.Count; i++)
            {
                State s = _states[i];
                s.HeldThisFrame = false;
                _states[i] = s;
            }

            _nextCells.Clear();
            for (int i = 0; i < selection.Count; i++)
            {
                PawnId pawn = selection[i];
                int cell = CellOf(snapshot, pawn);
                _nextCells[pawn.Value] = cell;
                if (cell < 0) continue;

                bool fresh = !first && _lastCells.TryGetValue(pawn.Value, out int before) && before != cell;
                Hold(pawn.Value, cell, fresh, now);
            }

            for (int i = _states.Count - 1; i >= 0; i--)
            {
                State s = _states[i];
                if (s.HeldThisFrame) continue;
                if (float.IsNaN(s.ReleasedAt))
                {
                    s.ReleasedAt = now;
                    _states[i] = s;
                }
                else if (now - s.ReleasedAt >= LockOnRing.FadeSeconds)
                {
                    _states.RemoveAt(i);
                }
            }

            (_lastCells, _nextCells) = (_nextCells, _lastCells);

            _rings.Clear();
            for (int i = 0; i < _states.Count; i++)
            {
                State s = _states[i];
                bool holding = float.IsNaN(s.ReleasedAt);
                float sinceRelease = holding ? -1f : now - s.ReleasedAt;
                if (!LockOnRing.Evaluate(now - s.OrderedAt, sinceRelease, out float scale, out float alpha)) continue;
                _rings.Add(new Ring(new PawnId(s.Pawn), s.Cell, scale, LockOnRing.Quantise(alpha), holding));
            }
        }

        /// <summary>
        /// The cell a selected colonist is walking to under the player's orders, or -1: the one
        /// aspect the order line already reads (<see cref="OrderModel.OrderCell"/>), which the
        /// simulation publishes for a drafted move and a forced Equip and for nothing else. Asked
        /// of colonists only, as the line is.
        /// </summary>
        static int CellOf(WorldSnapshot snapshot, PawnId pawn)
        {
            if (!snapshot.TryGetPawn(pawn, out PawnView view) || !view.IsColonist) return -1;
            return OrderModel.OrderCell(snapshot, pawn);
        }

        void Hold(int pawn, int cell, bool fresh, float now)
        {
            for (int i = 0; i < _states.Count; i++)
            {
                State s = _states[i];
                if (s.Pawn != pawn || s.Cell != cell) continue;

                if (float.IsNaN(s.ReleasedAt))
                {
                    // Sent back to a cell whose ring is still showing: a new order, and it gets
                    // its snap once the last one has settled.
                    if (fresh && now - s.OrderedAt >= LockOnRing.SettleSeconds) s.OrderedAt = now;
                }
                else
                {
                    // Fading and ordered again: a fresh snap. Fading and adopted — reselected —
                    // straight back to rest.
                    s.OrderedAt = fresh ? now : now - LockOnRing.SettleSeconds;
                    s.ReleasedAt = float.NaN;
                }

                s.HeldThisFrame = true;
                _states[i] = s;
                return;
            }

            _states.Add(new State
            {
                Pawn = pawn,
                Cell = cell,
                OrderedAt = fresh ? now : now - LockOnRing.SettleSeconds,
                ReleasedAt = float.NaN,
                HeldThisFrame = true,
            });
        }
    }
}
