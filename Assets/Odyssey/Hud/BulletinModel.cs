#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// One line of the Events panel: an incident that happened, when, and where to jump to.
    /// </summary>
    public readonly struct BulletinRow
    {
        /// <summary>The ledger's id, which is also the dismiss token.</summary>
        public readonly int Id;

        /// <summary>The symbolic key naming the incident, for the registry and the icon.</summary>
        public readonly string Key;

        /// <summary>What the registry calls it — "Supply drop".</summary>
        public readonly string Title;

        /// <summary>When, as the clock reads it — "Day 3 · 14h".</summary>
        public readonly string Stamp;

        /// <summary>Where, for the camera.</summary>
        public readonly CellRef Cell;

        public readonly int Tick;

        public readonly int IncidentDef;

        /// <summary>0 neutral, 1 good, 2 bad — see <see cref="BulletinView.Favourability"/>.</summary>
        public readonly int Favourability;

        public BulletinRow(int id, string key, string title, string stamp, CellRef cell, int tick,
            int incidentDef, int favourability)
        {
            Id = id;
            Key = key;
            Title = title;
            Stamp = stamp;
            Cell = cell;
            Tick = tick;
            IncidentDef = incidentDef;
            Favourability = favourability;
        }
    }

    /// <summary>
    /// What the Events panel says (A6, design 23 §5): the incidents that have happened and have
    /// not been dismissed, newest first, read off the published ledger tail and nothing else.
    ///
    /// <para><b>Not an alert, deliberately.</b> An alert is a condition the panel re-derives from
    /// the frame every refresh and that clears itself; a bulletin is an event, immutable once
    /// raised, that stays until the player dismisses it. <see cref="AlertModel"/> rebuilds its
    /// rows from scratch, which is right for conditions and would wipe an event on the next
    /// refresh — which is why this is a second model and not a fourth key in the first.</para>
    ///
    /// <para><b>The edge is the id.</b> The ledger only ever appends and ids are monotonic, so
    /// the model keeps the highest id it has seen and treats anything above it as new; a row
    /// can never be missed however many ticks pass between refreshes, and never raised twice.
    /// The first refresh after a world arrives is <em>priming</em>: whatever the tail already
    /// holds becomes history on the panel without announcing itself, so loading a save does not
    /// chime a dozen old events at the player.</para>
    /// </summary>
    public sealed class BulletinModel
    {
        /// <summary>Rows kept on the panel. Older ones fall off the bottom; the ledger keeps them.</summary>
        public const int MaxRows = 6;

        /// <summary>The rows to draw, newest first.</summary>
        public readonly List<BulletinRow> Rows = new List<BulletinRow>();

        readonly HashSet<int> _dismissed = new HashSet<int>();
        int _highestSeen;
        bool _primed;

        /// <summary>Bumped whenever <see cref="Rows"/> changes, so a view can skip a refresh that changed nothing.</summary>
        public int Version { get; private set; }

        /// <summary>
        /// The rows the last <see cref="Refresh"/> raised, not counting the priming one. Zero on
        /// almost every refresh; the view chimes when it is not.
        /// </summary>
        public int Arrived { get; private set; }

        /// <summary>The loudest thing that arrived last refresh, by favourability: 0 neutral, 1 good, 2 bad.</summary>
        public int ArrivedFavourability { get; private set; }

        public void Refresh(WorldSnapshot snapshot)
        {
            Arrived = 0;
            ArrivedFavourability = 0;

            var tail = snapshot.Bulletins;
            for (int i = 0; i < tail.Length; i++)
            {
                BulletinView view = tail[i];
                if (view.Id <= _highestSeen) continue;
                _highestSeen = view.Id;
                if (_dismissed.Contains(view.Id)) continue;

                Rows.Insert(0, Make(view));
                Version++;
                if (!_primed) continue;
                Arrived++;
                if (view.Favourability == 2 || ArrivedFavourability == 0)
                    ArrivedFavourability = view.Favourability;
            }

            while (Rows.Count > MaxRows)
            {
                Rows.RemoveAt(Rows.Count - 1);
                Version++;
            }

            _primed = true;
        }

        /// <summary>Take one row off the panel. The ledger keeps the entry; only the panel forgets.</summary>
        public void Dismiss(int id)
        {
            _dismissed.Add(id);
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].Id != id) continue;
                Rows.RemoveAt(i);
                Version++;
                return;
            }
        }

        public void DismissAll()
        {
            if (Rows.Count == 0) return;
            for (int i = 0; i < Rows.Count; i++) _dismissed.Add(Rows[i].Id);
            Rows.Clear();
            Version++;
        }

        public bool IsDismissed(int id) => _dismissed.Contains(id);

        static BulletinRow Make(in BulletinView view)
        {
            string key = IncidentLabels.IconKey(view.IncidentDef);
            return new BulletinRow(view.Id, key, Registry.Label(key), Stamp(view.Tick), view.Cell, view.Tick,
                view.IncidentDef, view.Favourability);
        }

        /// <summary>"Day 3 · 14h": the day as the clock counts it, and the hour. Built here so the
        /// format is testable and has one place to change.</summary>
        public static string Stamp(long tick) =>
            $"Day {GameClock.DayOfMonth(tick)} · {GameClock.HourOfDay(tick):00}h";
    }
}
