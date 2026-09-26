#nullable enable
using Odyssey.Sim.Saving;

namespace Odyssey.Sim.Expeditions
{
    // The road (design 64 §6c): expeditions between boards. EX5 fills this part.
    public sealed partial class Campaign
    {
        internal void SaveRoad(SaveWriter writer)
        {
            writer.Write(0);
        }

        internal void LoadRoad(SaveReader reader)
        {
            int count = reader.ReadInt();
            if (count != 0) throw new SaveLoadException("this build cannot yet read an expedition on the road");
        }
    }
}
