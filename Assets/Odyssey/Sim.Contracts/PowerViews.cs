#nullable enable

namespace Odyssey.Sim.Contracts
{
    /// <summary>
    /// What a power net is doing (design 32 §5). The whole net is one of these: there is no
    /// shedding, so a net is never partly dark.
    /// </summary>
    public enum PowerNetState : byte
    {
        /// <summary>Nothing running and nothing wanted — lines with no generator and no demand.</summary>
        Idle = 0,

        /// <summary>What its running generators make covers what its switched-on consumers want.</summary>
        Live = 1,

        /// <summary>It wants more than it makes, so every consumer on it has stopped.</summary>
        Dark = 2,
    }
}
