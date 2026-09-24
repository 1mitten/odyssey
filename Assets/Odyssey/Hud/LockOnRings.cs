#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// <b>Which targets wear a lock-on ring, and how far through its animation each is</b> (design
    /// 33 §7b). Fed once a frame with the snapshot, the selection and a clock; presentation reads
    /// the rings back and draws one flat annulus under each target.
    ///
    /// <para><b>A ring starts on the frame a selected colonist's published target changes to that
    /// pawn</b> — <c>odyssey.pawn.order.target</c> — not on the right-click. A refused order
    /// publishes nothing, so it draws nothing; the same order clicked again is quiet in the
    /// simulation (<c>AlreadyInThatState</c>) and so is quiet here. <b>At most one ring per
    /// target</b>: two colonists sent at one marauder share it, and the second order restarts the
    /// snap only once the first has settled, so a squad ordered in one click locks on once.</para>
    ///
    /// <para><b>A ring that was not ordered in front of the player is adopted, not snapped</b>: a
    /// colonist selected while already attacking, or a world just loaded, shows its target's ring
    /// at rest, faint, straight away. Loading a save mid-fight is not an order given.</para>
    ///
    /// <para><b>It holds</b> while any selected, drafted colonist's target is that pawn, the pawn
    /// is still in the frame — a corpse is not a pawn, so a death ends it — and it has not gone
    /// down since the ring started. An order given on a pawn already down (to finish it) holds
    /// until it dies. <b>It fades</b> over <see cref="LockOnRing.FadeSeconds"/> otherwise:
    /// the target down or dead, the order changed, the colonist deselected.</para>
    ///
    /// <para><b>Cost</b> scales with the selection and the rings, never the colony: one indexed
    /// aspect lookup per selected pawn, and a walk of the rings — at most one per target and in
    /// practice a handful. Nothing allocates after the first frames: the two memories swap and
    /// clear, and the ring list is reused.</para>
    /// </summary>
    public sealed class LockOnRings
    {
        /// <summary>One ring as it is to be drawn this frame.</summary>
        public readonly struct Ring
        {
            /// <summary>Who the ring is under.</summary>
            public readonly PawnId Target;

            /// <summary>The ring's size as a multiple of the target's footprint.</summary>
            public readonly float Scale;

            /// <summary>Its opacity, 0 to 1, quantised (<see cref="LockOnRing.Quantise"/>).</summary>
            public readonly float Alpha;

            /// <summary>True while the order holds; false while it fades.</summary>
            public readonly bool Holding;

            public Ring(PawnId target, float scale, float alpha, bool holding)
            {
                Target = target;
                Scale = scale;
                Alpha = alpha;
                Holding = holding;
            }
        }

        struct State
        {
            public int Target;
            public float OrderedAt;
            public float ReleasedAt; // NaN while held
            public bool DownedAtLock;
            public bool HeldThisFrame;
        }

        readonly List<State> _states = new List<State>();
        readonly List<Ring> _rings = new List<Ring>();
        Dictionary<int, int> _lastTargets = new Dictionary<int, int>();
        Dictionary<int, int> _nextTargets = new Dictionary<int, int>();
        object? _world;
        bool _seenWorld;

        /// <summary>The rings to draw this frame, one per target, in the order they started.</summary>
        public IReadOnlyList<Ring> Rings => _rings;

        /// <summary>
        /// One frame. <paramref name="now"/> is in seconds on any clock that only goes forwards —
        /// the bootstrap passes unscaled real time, so the snap reads the same at every game
        /// speed. <paramref name="world"/> is the world object: a different one clears every ring
        /// and memory, and the frame that sees it adopts rather than snaps.
        /// </summary>
        public void Update(WorldSnapshot snapshot, IReadOnlyList<PawnId> selection, float now, object? world)
        {
            bool first = !_seenWorld || !ReferenceEquals(world, _world);
            if (first)
            {
                _world = world;
                _seenWorld = true;
                _states.Clear();
                _lastTargets.Clear();
            }

            for (int i = 0; i < _states.Count; i++)
            {
                State s = _states[i];
                s.HeldThisFrame = false;
                _states[i] = s;
            }

            _nextTargets.Clear();
            for (int i = 0; i < selection.Count; i++)
            {
                PawnId pawn = selection[i];
                int target = TargetOf(snapshot, pawn);
                _nextTargets[pawn.Value] = target;
                if (target == 0) continue;
                if (!snapshot.TryGetPawn(new PawnId(target), out PawnView victim)) continue;

                bool fresh = !first && _lastTargets.TryGetValue(pawn.Value, out int before) && before != target;
                Hold(target, victim.IsDowned, fresh, now);
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

            (_lastTargets, _nextTargets) = (_nextTargets, _lastTargets);

            _rings.Clear();
            for (int i = 0; i < _states.Count; i++)
            {
                State s = _states[i];
                bool holding = float.IsNaN(s.ReleasedAt);
                float sinceRelease = holding ? -1f : now - s.ReleasedAt;
                if (!LockOnRing.Evaluate(now - s.OrderedAt, sinceRelease, out float scale, out float alpha)) continue;
                _rings.Add(new Ring(new PawnId(s.Target), scale, LockOnRing.Quantise(alpha), holding));
            }
        }

        /// <summary>
        /// The pawn a selected colonist is attacking, or 0. Only a drafted colonist's counts: an
        /// attack order needs a draft (design 33 §5j), and an undrafted colonist hitting back is
        /// not an order the player gave.
        /// </summary>
        static int TargetOf(WorldSnapshot snapshot, PawnId pawn)
        {
            if (!snapshot.TryGetPawn(pawn, out PawnView view) || !view.IsColonist || !view.IsDrafted) return 0;
            return snapshot.TryGetPawnAspect(pawn, CombatAspectNames.OrderTargetKey, out int target) ? target : 0;
        }

        void Hold(int target, bool downed, bool fresh, float now)
        {
            for (int i = 0; i < _states.Count; i++)
            {
                State s = _states[i];
                if (s.Target != target) continue;

                bool holding = float.IsNaN(s.ReleasedAt);
                if (holding)
                {
                    // Gone down since it locked: the order is over, whatever the aspect says this
                    // frame. Left unheld, it fades below.
                    if (downed && !s.DownedAtLock && !fresh) return;
                    // A second order on a ring still snapping is the same lock-on; one on a
                    // settled ring is a new order, and gets its snap.
                    if (fresh && now - s.OrderedAt >= LockOnRing.SettleSeconds) s.OrderedAt = now;
                    if (fresh) s.DownedAtLock = downed;
                }
                else
                {
                    if (downed && !s.DownedAtLock && !fresh) return;
                    // Fading and ordered again: a fresh snap. Fading and adopted — reselected —
                    // straight back to rest.
                    s.OrderedAt = fresh ? now : now - LockOnRing.SettleSeconds;
                    s.ReleasedAt = float.NaN;
                    s.DownedAtLock = downed;
                }

                s.HeldThisFrame = true;
                _states[i] = s;
                return;
            }

            _states.Add(new State
            {
                Target = target,
                OrderedAt = fresh ? now : now - LockOnRing.SettleSeconds,
                ReleasedAt = float.NaN,
                DownedAtLock = downed,
                HeldThisFrame = true,
            });
        }
    }
}
