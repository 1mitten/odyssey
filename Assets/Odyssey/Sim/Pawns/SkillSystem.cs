#nullable enable
namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Skill decay, on the Long cadence.
    ///
    /// <para>Gaining is done by the job drivers on the ticks that are work, and needs no system;
    /// losing is done here, because disuse is the absence of an event and something has to
    /// notice it. Once every 2,000 ticks, thirty times a day, every skill at or above
    /// <see cref="SkillDef.decayFromLevel"/> loses a thirtieth of its level's daily figure
    /// (a-01-pawns.md: about 30 points a day at 10, about 3,600 at 20). The effect the reference
    /// describes and this reproduces is an equilibrium: a skill settles at the level where a
    /// colonist's rate of use pays for its rate of decay.</para>
    ///
    /// <para>The cadence is the world's Long tick group rather than a modulo of its own, so it is
    /// phase-spread and saved like everything else in that group. There is no per-pawn spreading
    /// here as there is in <see cref="NeedsSystem"/>: the cost is a few integer subtractions per
    /// pawn thirty times a day, which is nothing to spread.</para>
    /// </summary>
    public sealed class SkillSystem : ITickable
    {
        readonly PawnContext _ctx;

        public SkillSystem(PawnContext ctx) { _ctx = ctx; }

        public TickGroup TickGroup => TickGroup.Long;

        public int TickPhaseOffset => 0;

        public void Tick(SimWorld world)
        {
            var content = _ctx.Content;
            int interval = (int)TickGroup;
            var pawns = _ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                var pawn = pawns[i];
                for (int skill = 0; skill < content.Skills.Length; skill++)
                {
                    int decay = content.Skills[skill].DecayPerInterval(pawn.SkillLevel(skill), interval, content.DayTicks);
                    if (decay > 0) pawn.DecayExperience(skill, decay);
                }
            }
        }
    }
}
