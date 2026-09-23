#nullable enable
using System;
using Odyssey.Hud;
using Odyssey.Presentation.Audio;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.Bootstrap
{
    /// <summary>
    /// The one reader of <see cref="WorldSnapshot.CombatEvents"/> in presentation (design 33 §5):
    /// once a frame it walks the published tail, hands every event it has not seen to whoever
    /// draws or sounds it, and remembers the highest id. <b>Lane B's file</b>
    /// (<c>docs/plans/combat-contracts.md</c>); the bootstrap calls <see cref="Consume"/> once a
    /// frame after the draft marks.
    ///
    /// <para><b>The watermark rule is the channel's, and it is written here once</b>: an event is
    /// new when its id is above the highest this reader has handled, and the watermark resets when
    /// the world object changes — a new session or a load, whose ring starts again from 1. A world
    /// seen for the first time is taken as it is: its tail is marked handled and nothing plays, so
    /// loading a save mid-fight does not replay the last thirty-two blows.</para>
    ///
    /// <para><b>Where each event goes</b> (<see cref="Handle"/>): to the figures, which draw the
    /// swing's timing and the reactions (<see cref="PawnFigureDirector.OnCombatEvent"/>); to the
    /// sound (<see cref="SoundIds.ForCombat"/>), from where it happened; and to the floating words,
    /// whose text and ink are <see cref="CombatFeedbackModel"/>'s. <b>A word floats only over a
    /// fight on a drawn layer</b>, as the health bars do: the overlay draws above the whole world,
    /// so a fight in a cave below or above the cut-away would otherwise float its words over
    /// whatever hides it (review, 2026-09-23). The figures and the sound take every event.</para>
    ///
    /// <para><b>Per-frame cost scales with the events since the last frame</b>, at most the
    /// published tail of <see cref="CombatEventView.PublishedTail"/>, and never with the colony.</para>
    /// </summary>
    public sealed class CombatFeedback
    {
        object? _world;
        int _watermark;

        /// <summary>
        /// How high above a pawn's feet a floating word starts, in metres: over the head of a
        /// colonist drawn at the cast's scale. An animal's word starts lower, at its own height.
        /// </summary>
        public const float WordLift = 2.8f;

        /// <summary>The highest event id handled in the current world.</summary>
        public int Watermark => _watermark;

        /// <summary>How many events have been handed on since the world last changed. For tests.</summary>
        public int Handled { get; private set; }

        /// <summary>The floating words in the air, for the view to draw.</summary>
        public CombatFloaters Floaters { get; } = new CombatFloaters();

        /// <summary>Raised once for every event handed on, after it has been routed. For tests and diagnostics.</summary>
        public event Action<CombatEventView>? Handed;

        /// <param name="lowestLayer">The lowest layer drawn; a word below it is not floated.</param>
        /// <param name="highestLayer">The highest layer drawn; a word above it is not floated.</param>
        public void Consume(WorldSnapshot snapshot, object? world, PawnFigureDirector? figures, AudioDirector? audio,
            int lowestLayer = int.MinValue, int highestLayer = int.MaxValue)
        {
            var events = snapshot.CombatEvents;
            if (!ReferenceEquals(world, _world))
            {
                _world = world;
                _watermark = events.Length > 0 ? events[events.Length - 1].Id : 0;
                Handled = 0;
                Floaters.Clear();
                return;
            }

            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Id <= _watermark) continue;
                Handle(events[i], snapshot, figures, audio, lowestLayer, highestLayer);
                _watermark = events[i].Id;
                Handled++;
            }
        }

        /// <summary>One new event: the figures, the sound, the floating word.</summary>
        void Handle(in CombatEventView combatEvent, WorldSnapshot snapshot, PawnFigureDirector? figures,
            AudioDirector? audio, int lowestLayer, int highestLayer)
        {
            figures?.OnCombatEvent(combatEvent);

            Vector3 at = WhereOf(combatEvent, snapshot, figures, out float height, out int layer);

            string? sound = SoundIds.ForCombat(combatEvent.Kind);
            if (sound != null) audio?.PlayOneShot(sound, at + Vector3.up * (height * 0.5f));

            string text = CombatFeedbackModel.FloatingText(combatEvent);
            if (text.Length > 0 && layer >= lowestLayer && layer <= highestLayer)
                Floaters.Add(text, CombatFeedbackModel.FloatingColour(combatEvent), at + Vector3.up * height,
                    CombatFeedbackModel.FloatingSeconds(combatEvent));

            Handed?.Invoke(combatEvent);
        }

        /// <summary>
        /// Where a moment happened, at the feet of whoever it happened to: the struck pawn's drawn
        /// figure where it has one, else its cell, else the event's own cell. A swing is heard from
        /// the swinger. <paramref name="height"/> is how tall the pawn is drawn, for the word, and
        /// <paramref name="layer"/> the layer it happened on.
        /// </summary>
        static Vector3 WhereOf(in CombatEventView combatEvent, WorldSnapshot snapshot, PawnFigureDirector? figures,
            out float height, out int layer)
        {
            PawnId who = combatEvent.Kind == CombatEventKind.Swing ? combatEvent.Attacker : combatEvent.Target;
            height = WordLift;
            layer = combatEvent.Cell.Y;

            if (who.IsValid && snapshot.TryGetPawn(who, out PawnView pawn))
            {
                layer = pawn.Cell.Y;
                if (pawn.IsAnimal)
                    height = figures != null && figures.TryGetAnimalBox(who, out _, out Vector3 box)
                        ? box.y + 0.4f
                        : 1.0f;
                if (pawn.IsDowned) height = 1.0f;
                if (figures != null && figures.TryGetFeet(who, out Vector3 feet)) return feet;
                return GroundRelief.Lift(CellMetrics.FloorCentre(pawn.Cell));
            }

            return GroundRelief.Lift(CellMetrics.FloorCentre(combatEvent.Cell));
        }
    }
}
