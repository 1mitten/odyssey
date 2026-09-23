#nullable enable
using Odyssey.Presentation.Audio;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;

namespace Odyssey.Presentation.Bootstrap
{
    /// <summary>
    /// The one reader of <see cref="WorldSnapshot.CombatEvents"/> in presentation (design 33 §5):
    /// once a frame it walks the published tail, hands every event it has not seen to whoever
    /// draws or sounds it, and remembers the highest id. <b>Lane B's file</b>
    /// (<c>docs/plans/combat-contracts.md</c>); the bootstrap calls <see cref="Consume"/> once a
    /// frame after the draft marks and does nothing else with it.
    ///
    /// <para><b>The watermark rule is the channel's, and it is written here once</b>: an event is
    /// new when its id is above the highest this reader has handled, and the watermark resets when
    /// the world object changes — a new session or a load, whose ring starts again from 1. A world
    /// seen for the first time is taken as it is: its tail is marked handled and nothing plays, so
    /// loading a save mid-fight does not replay the last thirty-two blows.</para>
    ///
    /// <para><b>A stub from the contracts step</b> in one respect: <see cref="Handle"/> calls
    /// nothing. Lane B routes an event to the figures (<see cref="PawnFigureDirector.OnCombatEvent"/>),
    /// the sounds and the floating text there.</para>
    /// </summary>
    public sealed class CombatFeedback
    {
        object? _world;
        int _watermark;

        /// <summary>The highest event id handled in the current world.</summary>
        public int Watermark => _watermark;

        /// <summary>How many events have been handed on since the world last changed. For tests.</summary>
        public int Handled { get; private set; }

        public void Consume(WorldSnapshot snapshot, object? world, PawnFigureDirector? figures, AudioDirector? audio)
        {
            var events = snapshot.CombatEvents;
            if (!ReferenceEquals(world, _world))
            {
                _world = world;
                _watermark = events.Length > 0 ? events[events.Length - 1].Id : 0;
                Handled = 0;
                return;
            }

            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Id <= _watermark) continue;
                Handle(events[i], figures, audio);
                _watermark = events[i].Id;
                Handled++;
            }
        }

        /// <summary>One new event. Lane B: figures, sounds, floating text.</summary>
        void Handle(in CombatEventView combatEvent, PawnFigureDirector? figures, AudioDirector? audio)
        {
        }
    }
}
