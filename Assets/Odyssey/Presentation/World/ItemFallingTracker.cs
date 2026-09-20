#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// Tracks loose items falling between layers when ground or flooring beneath them is destroyed
    /// or collapses, providing smooth gravity-accelerated downward visual motion and landing audio.
    /// Design 26.
    /// </summary>
    public sealed class ItemFallingTracker
    {
        public struct FallRecord
        {
            public Vector3 StartPos;
            public Vector3 TargetPos;
            public float Elapsed;
            public float Duration;

            public FallRecord(Vector3 startPos, Vector3 targetPos, float elapsed, float duration)
            {
                StartPos = startPos;
                TargetPos = targetPos;
                Elapsed = elapsed;
                Duration = duration;
            }
        }

        readonly Dictionary<int, CellRef> _lastPositions = new Dictionary<int, CellRef>();
        readonly Dictionary<int, FallRecord> _activeFalls = new Dictionary<int, FallRecord>();
        readonly List<int> _activeKeys = new List<int>();
        readonly List<int> _toRemove = new List<int>();

        /// <summary>Fired on the exact frame an item touches down on the landing floor.</summary>
        public event Action<Vector3>? ItemLanded;

        /// <summary>Number of currently airborne items.</summary>
        public int ActiveFallCount => _activeFalls.Count;

        /// <summary>
        /// Examine the current snapshot's items and register new falls for items whose layer dropped.
        /// </summary>
        public void UpdateSnapshot(WorldSnapshot snapshot)
        {
            var things = snapshot.Things;
            for (int i = 0; i < things.Length; i++)
            {
                int id = things[i].Id.Value;
                CellRef current = things[i].Cell;

                if (_lastPositions.TryGetValue(id, out CellRef previous))
                {
                    if (current.Y < previous.Y)
                    {
                        Vector3 startPos = CellMetrics.FloorCentre(previous);
                        Vector3 targetPos = CellMetrics.FloorCentre(current);
                        float height = (previous.Y - current.Y) * CellMetrics.SizeY;
                        float duration = ItemFallMotion.Duration(height);
                        _activeFalls[id] = new FallRecord(startPos, targetPos, 0f, duration);
                    }
                }

                _lastPositions[id] = current;
            }
        }

        /// <summary>
        /// Advance active falls by delta time and trigger touchdown audio.
        /// </summary>
        public void Advance(float deltaTime)
        {
            if (deltaTime <= 0f || _activeFalls.Count == 0) return;

            _toRemove.Clear();
            _activeKeys.Clear();
            foreach (var key in _activeFalls.Keys)
            {
                _activeKeys.Add(key);
            }

            for (int i = 0; i < _activeKeys.Count; i++)
            {
                int id = _activeKeys[i];
                FallRecord fall = _activeFalls[id];
                float before = fall.Elapsed;
                fall.Elapsed += deltaTime;

                if (ItemFallMotion.FallLanded(before, fall.Elapsed, fall.Duration))
                {
                    ItemLanded?.Invoke(fall.TargetPos);
                }

                if (ItemFallMotion.FallFinished(fall.Elapsed, fall.Duration))
                {
                    _toRemove.Add(id);
                }
                else
                {
                    _activeFalls[id] = fall;
                }
            }

            for (int i = 0; i < _toRemove.Count; i++)
            {
                _activeFalls.Remove(_toRemove[i]);
            }
        }

        /// <summary>
        /// Get the vertical displacement offset for an item currently in flight.
        /// </summary>
        public bool TryGetFallingOffset(int thingId, out Vector3 offset)
        {
            if (_activeFalls.TryGetValue(thingId, out FallRecord fall))
            {
                offset = ItemFallMotion.FallingOffset(fall.StartPos, fall.TargetPos, fall.Elapsed, fall.Duration);
                return true;
            }

            offset = Vector3.zero;
            return false;
        }

        /// <summary>Clear all tracked state (e.g. on new game or reload).</summary>
        public void Reset()
        {
            _lastPositions.Clear();
            _activeFalls.Clear();
            _activeKeys.Clear();
            _toRemove.Clear();
        }
    }
}
