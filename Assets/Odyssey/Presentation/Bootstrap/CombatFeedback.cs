#nullable enable
using System;
using Odyssey.Hud;
using Odyssey.Presentation.Audio;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
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
    /// <para><b>And to the blood seam</b> (design 33 §7d): a landed hit is a spurt, sharp or blunt by
    /// the weapon (<see cref="Odyssey.Hud.BloodSides"/>), and a down or a death a pool, handed to
    /// <see cref="Blood"/> — a no-op until the blood unit fills it. Blood takes every event, on
    /// every layer, as the figures do: a mark on the ground must be there when the player looks.</para>
    ///
    /// <para><b>And the sound of a blow is timed, not merely played</b> (design 33 §9g). A swing
    /// with a weapon schedules its whoosh — or, for a sharp weapon's critical, the slice — in
    /// <see cref="Sounds"/>, and every frame starts whichever are due, heard from the swinger, so
    /// the whoosh peaks a tenth of a second before the blow and the slice on it
    /// (<see cref="CombatSoundTiming"/>). The thud plays on the hit's own frame, from the struck.
    /// Paused, nothing waiting starts; the world changing forgets it all.</para>
    ///
    /// <para><b>Per-frame cost scales with the events since the last frame</b>, at most the
    /// published tail of <see cref="CombatEventView.PublishedTail"/>, and the swings waiting to be
    /// heard, at most one per fighter — never with the colony. Nothing allocates.</para>
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

        /// <summary>
        /// Where blood goes (design 33 §7d): <see cref="NoBloodEffects"/> until the blood unit
        /// sets its own. Cleared with the world.
        /// </summary>
        public IBloodEffects Blood { get; set; } = NoBloodEffects.Instance;

        /// <summary>Which blows cut, read once off the content (<see cref="BloodSidesOf"/>); all blunt until it is.</summary>
        public BloodSides BloodSides { get; set; } = BloodSides.AllBlunt;

        /// <summary>
        /// How high on a standing person a blow lands, and on one lying down, in metres: the chest,
        /// and just off the ground. INVENTED, for the spurt's origin; an animal's is 0.6 of its
        /// drawn box.
        /// </summary>
        public const float PersonWoundHeight = 1.3f, DownedWoundHeight = 0.25f;

        /// <summary>
        /// The whooshes and slices waiting for their moment (design 33 §9g). Cleared with the
        /// world; the bootstrap clears it at teardown with the floaters and the blood.
        /// </summary>
        public CombatSoundSchedule Sounds { get; } = new CombatSoundSchedule();

        /// <summary>Raised once for every event handed on, after it has been routed. For tests and diagnostics.</summary>
        public event Action<CombatEventView>? Handed;

        /// <param name="lowestLayer">The lowest layer drawn; a word below it is not floated.</param>
        /// <param name="highestLayer">The highest layer drawn; a word above it is not floated.</param>
        /// <param name="tickAlpha">How far this frame sits between the last tick run and the next, 0 to 1.</param>
        /// <param name="nominalTicksPerSecond">
        /// Ticks a real second at speed one (the bootstrap's <c>ticksPerSecond</c>); the snapshot's
        /// speed multiplies it. 0 — the default, and every test that does not time sound — starts
        /// no whoosh or slice at all.
        /// </param>
        public void Consume(WorldSnapshot snapshot, object? world, PawnFigureDirector? figures, AudioDirector? audio,
            int lowestLayer = int.MinValue, int highestLayer = int.MaxValue,
            float tickAlpha = 0f, float nominalTicksPerSecond = 0f)
        {
            var events = snapshot.CombatEvents;
            if (!ReferenceEquals(world, _world))
            {
                _world = world;
                _watermark = events.Length > 0 ? events[events.Length - 1].Id : 0;
                Handled = 0;
                Floaters.Clear();
                Blood.Clear();
                Sounds.Clear();
                return;
            }

            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Id <= _watermark) continue;
                Handle(events[i], snapshot, figures, audio, lowestLayer, highestLayer);
                _watermark = events[i].Id;
                Handled++;
            }

            SoundTheDue(snapshot, figures, audio, tickAlpha, nominalTicksPerSecond);
        }

        /// <summary>
        /// Starts every whoosh and slice whose moment has come (design 33 §9g), heard from the
        /// swinger where the swinger is now — else where the blow was aimed. After the frame's new
        /// events, so a swing first read late can still start on the frame it is read.
        /// </summary>
        void SoundTheDue(WorldSnapshot snapshot, PawnFigureDirector? figures, AudioDirector? audio,
            float tickAlpha, float nominalTicksPerSecond)
        {
            if (Sounds.Count == 0) return;

            double now = snapshot.Tick + (double)Mathf.Clamp01(tickAlpha);
            float ticksPerSecond = CombatSoundTiming.TicksPerRealSecond(snapshot.GameSpeed, nominalTicksPerSecond);
            while (Sounds.TryTakeDue(now, ticksPerSecond, out PendingCue due))
            {
                string? sound = SoundIds.ForCue(due.Cue);
                if (sound == null || audio == null) continue;

                var swing = new CombatEventView(0, 0, CombatEventKind.Swing, due.Attacker, default, due.Cell);
                Vector3 at = WhereOf(swing, snapshot, figures, out float height, out _);
                audio.PlayOneShot(sound, at + Vector3.up * (height * 0.5f));
            }
        }

        /// <summary>One new event: the figures, the sound, the floating word.</summary>
        void Handle(in CombatEventView combatEvent, WorldSnapshot snapshot, PawnFigureDirector? figures,
            AudioDirector? audio, int lowestLayer, int highestLayer)
        {
            figures?.OnCombatEvent(combatEvent);

            Vector3 at = WhereOf(combatEvent, snapshot, figures, out float height, out int layer);
            Bleed(combatEvent, snapshot, figures, at);

            // The sound of a blow (design 33 §9g): a swing schedules its whoosh or slice against the
            // impact, and this frame plays only what belongs to it — the thud of a landed hit, or a
            // moment's own named sound. The attacker's kind decides a natural attack's edge.
            int attackerKind = combatEvent.Attacker.IsValid && snapshot.TryGetPawn(combatEvent.Attacker, out PawnView swinger)
                ? swinger.Kind : -1;
            CombatCue now = Sounds.Hear(combatEvent, BloodSides, attackerKind);
            string? sound = now != CombatCue.None ? SoundIds.ForCue(now) : SoundIds.ForCombat(combatEvent.Kind);
            if (sound != null) audio?.PlayOneShot(sound, at + Vector3.up * (height * 0.5f));

            string text = CombatFeedbackModel.FloatingText(combatEvent);
            if (text.Length > 0 && layer >= lowestLayer && layer <= highestLayer)
                Floaters.Add(text, CombatFeedbackModel.FloatingColour(combatEvent), at + Vector3.up * height,
                    CombatFeedbackModel.FloatingSeconds(combatEvent));

            Handed?.Invoke(combatEvent);
        }

        /// <summary>
        /// The blood seam (design 33 §7d): a landed hit spurts from the wound along the blow, a
        /// down or a death pools under the body, and nothing else bleeds
        /// (<see cref="BloodModel.For"/>). <paramref name="at"/> is the struck pawn's feet.
        /// </summary>
        void Bleed(in CombatEventView combatEvent, WorldSnapshot snapshot, PawnFigureDirector? figures, Vector3 at)
        {
            switch (BloodModel.For(combatEvent.Kind))
            {
                case BloodMark.Spurt:
                {
                    int attackerKind = -1;
                    Vector3 direction = Vector3.zero;
                    if (combatEvent.Attacker.IsValid && snapshot.TryGetPawn(combatEvent.Attacker, out PawnView attacker))
                    {
                        attackerKind = attacker.Kind;
                        Vector3 from = figures != null && figures.TryGetFeet(attacker.Id, out Vector3 feet)
                            ? feet
                            : GroundRelief.Lift(CellMetrics.FloorCentre(attacker.Cell));
                        direction = at - from;
                        direction.y = 0f;
                        direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.zero;
                    }

                    bool sharp = BloodSides.IsSharp(combatEvent.Weapon, attackerKind);
                    Vector3 wound = at + Vector3.up * WoundHeight(combatEvent.Target, snapshot, figures);
                    Blood.Spurt(wound, direction, combatEvent.Amount / 1000f, sharp);
                    break;
                }
                case BloodMark.Pool:
                    Blood.Pool(at, BloodModel.PoolSize(combatEvent.Kind));
                    break;
            }
        }

        /// <summary>How high on the struck body a blow lands: a person's chest, low on one lying down, an animal's flank.</summary>
        static float WoundHeight(PawnId who, WorldSnapshot snapshot, PawnFigureDirector? figures)
        {
            if (!who.IsValid || !snapshot.TryGetPawn(who, out PawnView pawn)) return PersonWoundHeight;
            if (pawn.IsDowned) return DownedWoundHeight;
            if (pawn.IsAnimal)
                return figures != null && figures.TryGetAnimalBox(who, out _, out Vector3 box) ? box.y * 0.6f : 0.4f;
            return PersonWoundHeight;
        }

        /// <summary>
        /// Which blows cut, by item def and by pawn kind, read once off the content — the Defs are
        /// the one owner of which weapon is sharp (<c>Items.xml</c>, <c>Species.xml</c>,
        /// <c>Combat.xml</c>'s fists). Resolved in <see cref="Odyssey.Hud.BloodSides.IsSharp"/> in the order
        /// <c>WeaponRules.ArmamentOf</c> arms a pawn.
        /// </summary>
        public static BloodSides BloodSidesOf(PawnContent? content)
        {
            if (content == null) return BloodSides.AllBlunt;

            var weapons = new bool?[content.Items.Length];
            for (int i = 0; i < weapons.Length; i++)
            {
                AttackDef? attack = content.Items[i]?.weapon;
                weapons[i] = attack == null ? (bool?)null : attack.damageKind == DamageKind.Sharp;
            }

            var naturals = new bool?[content.Kinds.Length];
            for (int kind = 0; kind < naturals.Length; kind++)
            {
                AttackDef? natural = content.SpeciesOf(kind).naturalAttack;
                naturals[kind] = natural == null ? (bool?)null : natural.damageKind == DamageKind.Sharp;
            }

            return new BloodSides(weapons, naturals, content.Combat.fists.damageKind == DamageKind.Sharp);
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
            PawnId who = combatEvent.Kind == CombatEventKind.Swing || combatEvent.Kind == CombatEventKind.SwingCritical
                ? combatEvent.Attacker
                : combatEvent.Target;
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
