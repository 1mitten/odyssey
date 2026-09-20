#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;

namespace Odyssey.Sim.Events
{
    /// <summary>
    /// Everything in the air on its way down (design 23 §6): the reference's skyfaller.
    ///
    /// <para><b>The flight is simulated; only the drawing is not.</b> A thing does not exist
    /// until it lands — nothing can haul, eat, count or reserve it in flight — and the tick it
    /// lands on is decided at launch, so a save taken mid-air lands it on the same tick a run
    /// that was never saved would have. Presentation reads <see cref="FallingView"/> and draws
    /// the thing wherever along that line the frame falls, exactly as it draws a pawn between two
    /// cells; the height, the arc and the easing are its business and none are here.</para>
    ///
    /// <para><b>Landing is the ordinary way a thing arrives.</b> The cell was checked for room
    /// at launch, but two seconds is long enough for a hauler to have set a load down there, so
    /// the landing widens outward the way every other spawn does. A load that finds no room
    /// within three cells is lost, and <see cref="Lost"/> counts it: it is state, because it is
    /// history, so it is hashed and saved like the rest.</para>
    /// </summary>
    public sealed class Skyfallers : ITickable, IStateHashable, ISaveable, ISnapshotContributor
    {
        /// <summary>One thing in the air. A class so the list can hand out references; it holds no handles.</summary>
        public sealed class Entry
        {
            public int IncidentDef;
            public int ItemDef;
            public int Stack;
            public int LandingCell;
            public int LaunchTick;
            public int LandTick;
        }

        readonly List<Entry> _inFlight = new List<Entry>();
        readonly PawnContext _pawns;

        public Skyfallers(PawnContext pawns) =>
            _pawns = pawns ?? throw new ArgumentNullException(nameof(pawns));

        /// <summary>Everything in the air, in launch order.</summary>
        public IReadOnlyList<Entry> InFlight => _inFlight;

        /// <summary>Loads that found no room when they came down. Hashed and saved.</summary>
        public int Lost { get; private set; }

        /// <summary>Loads that landed and became things. Hashed and saved, for the same reason.</summary>
        public int Landed { get; private set; }

        public void Launch(int incidentDef, int itemDef, int stack, int landingCell, int launchTick, int landTick)
        {
            if (stack < 1) throw new ArgumentOutOfRangeException(nameof(stack));
            if (landTick < launchTick) throw new ArgumentOutOfRangeException(nameof(landTick));
            _inFlight.Add(new Entry
            {
                IncidentDef = incidentDef,
                ItemDef = itemDef,
                Stack = stack,
                LandingCell = landingCell,
                LaunchTick = launchTick,
                LandTick = landTick,
            });
        }

        public TickGroup TickGroup => TickGroup.Normal;

        public int TickPhaseOffset => 0;

        public void Tick(SimWorld world)
        {
            for (int i = 0; i < _inFlight.Count;)
            {
                Entry entry = _inFlight[i];
                if (world.CurrentTick < entry.LandTick)
                {
                    i++;
                    continue;
                }
                Land(entry);
                _inFlight.RemoveAt(i);
            }
        }

        void Land(Entry entry)
        {
            int cell = _pawns.Items.NearestCellWithSpace(
                _pawns.Cells, entry.LandingCell, entry.ItemDef, entry.Stack, maxRadius: 3);
            if (cell < 0)
            {
                Lost++;
                return;
            }
            _pawns.Items.Spawn(entry.ItemDef, cell, entry.Stack);
            Landed++;
        }

        public void ContributeTo(ref StateHash hash)
        {
            hash.Add(_inFlight.Count);
            for (int i = 0; i < _inFlight.Count; i++)
            {
                Entry entry = _inFlight[i];
                hash.Add(entry.IncidentDef);
                hash.Add(entry.ItemDef);
                hash.Add(entry.Stack);
                hash.Add(entry.LandingCell);
                hash.Add(entry.LaunchTick);
                hash.Add(entry.LandTick);
            }
            hash.Add(Lost);
            hash.Add(Landed);
        }

        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            GridSize size = _pawns.Size;
            for (int i = 0; i < _inFlight.Count; i++)
            {
                Entry entry = _inFlight[i];
                writer.AddFalling(new FallingView(
                    entry.ItemDef, entry.Stack, size.FromIndex(entry.LandingCell),
                    entry.LaunchTick, entry.LandTick));
            }
        }

        public string SaveKey => "odyssey.skyfallers";

        public void Save(SaveWriter writer)
        {
            writer.Write(_inFlight.Count);
            for (int i = 0; i < _inFlight.Count; i++)
            {
                Entry entry = _inFlight[i];
                writer.Write(entry.IncidentDef);
                writer.Write(entry.ItemDef);
                writer.Write(entry.Stack);
                writer.Write(entry.LandingCell);
                writer.Write(entry.LaunchTick);
                writer.Write(entry.LandTick);
            }
            writer.Write(Lost);
            writer.Write(Landed);
        }

        public void Load(SaveReader reader)
        {
            _inFlight.Clear();
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                _inFlight.Add(new Entry
                {
                    IncidentDef = reader.ReadInt(),
                    ItemDef = reader.ReadInt(),
                    Stack = reader.ReadInt(),
                    LandingCell = reader.ReadInt(),
                    LaunchTick = reader.ReadInt(),
                    LandTick = reader.ReadInt(),
                });
            }
            Lost = reader.ReadInt();
            Landed = reader.ReadInt();
        }
    }
}
