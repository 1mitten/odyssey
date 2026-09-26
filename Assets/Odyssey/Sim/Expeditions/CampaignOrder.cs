#nullable enable

namespace Odyssey.Sim.Expeditions
{
    /// <summary>What a campaign order asks for (design 64 §4b). Appended to, never renumbered.</summary>
    public enum CampaignOrderKind
    {
        None,
    }

    /// <summary>
    /// An order to the campaign rather than to one board: set out on an expedition, answer a choice
    /// on the road. Queued with <see cref="Campaign.Submit"/> and applied at the start of the next
    /// step, never in the middle of one.
    /// </summary>
    public readonly struct CampaignOrder
    {
        public readonly CampaignOrderKind Kind;

        public CampaignOrder(CampaignOrderKind kind)
        {
            Kind = kind;
        }
    }
}
