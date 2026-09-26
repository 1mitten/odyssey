#nullable enable
using System.Collections.Generic;
using System.IO;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;

namespace Odyssey.Sim.Expeditions
{
    // Saving and loading the whole campaign (design 64 §12): one file, the home board's, with the
    // campaign section appended after home's own. A format-11 file has no such section and loads as
    // a campaign with no expedition, which is exactly what every colony before this was.
    public sealed partial class Campaign
    {
        /// <summary>Write the whole campaign: home's sections, then <c>odyssey.campaign</c>.</summary>
        public byte[] Save(SaveRecipe? recipe = null)
        {
            using var buffer = new MemoryStream();
            Home.Save(buffer, recipe, new ISaveable[] { new CampaignSection(this) });
            return buffer.ToArray();
        }

        /// <summary>
        /// Load a campaign over this one, which was built round a fresh home from the same seed and
        /// size (the rule every board load has): home first, then every site board rebuilt from its
        /// place and loaded from its own save, then the shared id counter set last, because each
        /// board's own load set it to that board's count.
        /// </summary>
        public SaveHeader Load(byte[] bytes)
        {
            var section = new CampaignSection(this);
            SaveHeader header;
            using (var buffer = new MemoryStream(bytes, writable: false))
                header = Home.Load(buffer, new ISaveable[] { section });

            while (_boards.Count > 1) _boards.RemoveAt(_boards.Count - 1);
            for (int i = 0; i < section.StagedBoards.Count; i++)
            {
                var staged = section.StagedBoards[i];
                ColonyWorld board = RebuildSite(staged.Place);
                board.Load(staged.Save);
                _boards.Add(new Board(staged.Slot, staged.Place, board));
                board.Pawns.Pawns.Ids = Ids;
            }
            Ids.Reset(section.SavedNextId);
            FinishLoad();

            for (int i = 1; i < _boards.Count; i++)
                if (_boards[i].World.World.CurrentTick != Tick)
                    throw new SaveLoadException(
                        $"the saved site board in slot {_boards[i].Slot} is at tick {_boards[i].World.World.CurrentTick} and home at {Tick}");
            return header;
        }

        // Build the board a place's site stands on, before its save is loaded over it; EX7 fills it.
        ColonyWorld RebuildSite(int place)
        {
            ColonyWorld? built = null;
            BuildSiteForLoad(place, ref built);
            if (built == null) throw new SaveLoadException("this build cannot yet rebuild a site board");
            return built;
        }

        partial void BuildSiteForLoad(int place, ref ColonyWorld? built);

        // Anything the road holds that points at boards or pawns, rewired once every board is back.
        partial void FinishLoad();
    }
}
