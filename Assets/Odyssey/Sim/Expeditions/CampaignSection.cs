#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Saving;

namespace Odyssey.Sim.Expeditions
{
    /// <summary>
    /// <c>odyssey.campaign</c> (design 64 §12): everything above the boards, written into the home
    /// board's own file after its sections.
    ///
    /// <para>In order: the counters, home's tile, the chart, the places whole, the expeditions on the
    /// road, then every live site board as a complete board save of its own. Loading reads it all into
    /// the campaign and <b>stages</b> the site boards, because a board is rebuilt from its place and
    /// then loaded, and that is <see cref="Campaign.Load"/>'s to do once the home file is finished.</para>
    /// </summary>
    public sealed class CampaignSection : ISaveable
    {
        /// <summary>The section's own layout. A save contract.</summary>
        public const int Layout = 1;

        readonly Campaign _campaign;

        /// <summary>The site boards the file held: slot, place, and the board's own save.</summary>
        internal readonly List<(int Slot, int Place, byte[] Save)> StagedBoards = new List<(int, int, byte[])>();

        internal int SavedNextId { get; private set; }

        public CampaignSection(Campaign campaign) => _campaign = campaign;

        public string SaveKey => "odyssey.campaign";

        public void Save(SaveWriter writer)
        {
            writer.Write(Layout);
            writer.Write(_campaign.NextSlot);
            writer.Write(_campaign.Ids.Peek);
            writer.Write(_campaign.HomeTile);

            writer.Write(_campaign.Chart != null);
            if (_campaign.Chart != null) writer.Write(_campaign.Chart.Pack());

            writer.Write(_campaign.Places.Count);
            for (int i = 0; i < _campaign.Places.Count; i++)
            {
                Place place = _campaign.Places[i];
                writer.Write(place.Tile);
                writer.Write(WorldSituationName(place.Situation));
                writer.Write(place.Seed);
                writer.Write(place.Site);
                writer.Write((int)place.State);
                writer.Write(place.Visits);
            }

            _campaign.SaveRoad(writer);

            var boards = _campaign.Boards;
            writer.Write(boards.Count - 1);
            for (int i = 1; i < boards.Count; i++)
            {
                writer.Write(boards[i].Slot);
                writer.Write(boards[i].Place);
                writer.Write(boards[i].World.Save());
            }
        }

        public void Load(SaveReader reader)
        {
            int layout = reader.ReadInt();
            if (layout < 1 || layout > Layout)
                throw new SaveLoadException($"The campaign section has layout {layout} and this build reads up to {Layout}.");

            int nextSlot = reader.ReadInt();
            SavedNextId = reader.ReadInt();
            int homeTile = reader.ReadInt();
            byte[]? chart = reader.ReadBool() ? reader.ReadBytes() : null;

            int count = reader.ReadInt();
            var places = new List<Place>(count);
            for (int i = 0; i < count; i++)
            {
                int tile = reader.ReadInt();
                int situation = SituationIndex(reader.ReadString());
                uint seed = reader.ReadUInt();
                var site = reader.ReadSite();
                var state = (PlaceState)reader.ReadInt();
                int visits = reader.ReadInt();
                places.Add(new Place(tile, situation, seed, site, state, visits));
            }
            _campaign.RestorePlanet(homeTile, chart, places);
            _campaign.RestoreCounters(nextSlot, SavedNextId);

            _campaign.LoadRoad(reader);

            StagedBoards.Clear();
            int boards = reader.ReadInt();
            for (int i = 0; i < boards; i++)
                StagedBoards.Add((reader.ReadInt(), reader.ReadInt(), reader.ReadBytes()));
        }

        // A place names its situation by defName in the file, so a situation added or renamed in the
        // content does not silently move every saved place onto its neighbour.
        static string WorldSituationName(int index)
        {
            var all = Worldgen.WorldContent.Situations;
            return (uint)index < (uint)all.Length ? all[index].defName : string.Empty;
        }

        static int SituationIndex(string defName)
        {
            var all = Worldgen.WorldContent.Situations;
            for (int i = 0; i < all.Length; i++) if (all[i].defName == defName) return i;
            throw new SaveLoadException($"A saved place is a '{defName}', which this build does not have.");
        }
    }
}
