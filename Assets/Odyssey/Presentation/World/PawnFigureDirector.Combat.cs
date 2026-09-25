#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// The fight, on the figures (design 33 §1, §3): the swing started by a
    /// <see cref="PawnGesture.Strike"/> on the gesture serial, the reactions a
    /// <see cref="CombatEventView"/> asks for — hit, stagger, dodge — and the downed and stunned
    /// loops read off <see cref="PawnView.Flags"/>. The Sword Combat pack's Polygon clips where the
    /// catalogue has their rows (<see cref="ModuleIds.CombatRows"/>), and <see cref="CombatPose"/>
    /// where it does not. <b>Lane B's file</b> (<c>docs/plans/combat-contracts.md</c>).
    ///
    /// <para><b>The clip layer.</b> A colonist's graph is the gait mixer on input 0 of an
    /// <see cref="AnimationLayerMixerPlayable"/> and a two-slot action mixer on input 1. A combat
    /// clip is put in the free slot and cross-faded against the other, and the layer's weight
    /// eases the whole action in over the walk and back out. Every action clip plays at speed
    /// nought and has its time <em>set</em> each frame — from the simulation's ticks for a swing,
    /// from the frame's own seconds for a reaction — so a pause holds it and the game speed
    /// cannot run it away from the blow it is drawing.</para>
    ///
    /// <para><b>A clip that is not there is a computed pose</b>, exactly as the work stroke is: laid
    /// over whatever the graph produced by <see cref="ApplyCombatPose"/>, in the same pass and on
    /// the same terms. The punch and the bite are always computed; a downed pawn with no clip lies
    /// down as a sleeper does.</para>
    ///
    /// <para><b>A blow taken is a reaction beside the one-shot, not instead of it</b> (design 33
    /// §9a). The struck body's flinch or stagger runs on its own track
    /// (<see cref="ReactionTrack"/>), and <see cref="CombatReactions.Arbitrate"/> decides each frame
    /// whether the layer shows it or the body's own swing; while the swing has the layer, round its
    /// impact, the computed flinch is laid over it. Lane B refused a react during the body's own
    /// swing and let the next swing cut one off, which in a fight drew nothing for most blows.</para>
    ///
    /// <para><b>Per-frame cost scales with the live figures</b> (<c>docs/process.md</c> §3), which
    /// the ceiling holds at 64, plus the corpses being laid down; nothing here walks the board.</para>
    /// </summary>
    public sealed partial class PawnFigureDirector
    {
        /// <summary>
        /// The computed work stroke never plays for <c>Job_AttackMelee</c> (design 33 §5j), even
        /// though its driver reports a work focus during the wind-up: that focus is for turning the
        /// figure to face its target, and the blow is the fight's own clip or computed swing.
        /// </summary>
        public static bool PlaysWorkStroke(in PawnView pawn) =>
            pawn.Working && pawn.JobDef != JobHandle.AttackMelee;

        /// <summary>
        /// Every item def's attack style, or null for an item that is not a weapon — the family a
        /// swing is drawn in (<see cref="CombatPose.StyleFor"/>). Set once by the composition root
        /// from the content; empty means everybody punches or bites.
        /// </summary>
        public AttackStyle?[] WeaponStyles { get; set; } = Array.Empty<AttackStyle?>();

        /// <summary>
        /// The wind-up a swing is assumed to have until its <see cref="CombatEventKind.Swing"/>
        /// event says otherwise, in ticks. The gesture and the event are published by the same
        /// tick and read in the same frame, so this is overwritten before a single frame is drawn
        /// with it; it exists for a swing whose event was never seen.
        /// </summary>
        public const int DefaultWindupTicks = 20;

        /// <summary>How long the action layer takes to come in and go out, in seconds.</summary>
        public const float CombatEaseSeconds = 0.12f;

        /// <summary>How long two action clips cross-fade, in seconds.</summary>
        public const float CombatCrossFadeSeconds = 0.1f;

        /// <summary>How many figures are showing a combat clip on the last pass. Diagnostic.</summary>
        public int FightingFigures { get; private set; }

        /// <summary>
        /// The frame's own clock in simulation ticks: the published tick plus the part-tick the
        /// frame is drawn at. What a swing is timed on.
        /// </summary>
        float _frameTicks;

        /// <summary>This frame's part-tick and the Defs' fallback pace, for <c>PawnPose.StepProgress</c> (design 43 §7).</summary>
        float _tickAlpha;
        int _movePerTick;

        /// <summary>The combat rows' usable clips by row id, read out of the catalogue once.</summary>
        Dictionary<string, List<CombatClipEntry>>? _combatRows;

        Dictionary<string, List<CombatClipEntry>> CombatRows
        {
            get
            {
                if (_combatRows != null) return _combatRows;
                _combatRows = new Dictionary<string, List<CombatClipEntry>>(StringComparer.Ordinal);
                if (_catalogue == null) return _combatRows;
                foreach (string id in ModuleIds.CombatRows)
                {
                    ModuleEntry? row = _catalogue.Find(id);
                    if (row == null) continue;
                    var usable = new List<CombatClipEntry>();
                    foreach (CombatClipEntry entry in row.combat)
                        if (entry.clip != null) usable.Add(entry);
                    if (usable.Count > 0) _combatRows[id] = usable;
                }
                return _combatRows;
            }
        }

        /// <summary>
        /// True when at least one combat row resolved to a clip — the Sword Combat pack is here.
        /// The question a clip test asks, rather than whether there is a catalogue at all.
        /// </summary>
        public bool HasCombatClips => CombatRows.Count > 0;

        /// <summary>
        /// The clip a role draws for a variant, or null for the computed pose. A variant the row
        /// lacks falls back to the row's first clip — a light swing whose C step is missing still
        /// swings — but a row with no clips at all is null, which is the pack absent.
        /// </summary>
        public CombatClipEntry? CombatClip(CombatRole role, string variant)
        {
            string? id = CombatPose.RowOf(role);
            if (id == null || !CombatRows.TryGetValue(id, out List<CombatClipEntry>? clips)) return null;
            for (int i = 0; i < clips.Count; i++)
                if (string.Equals(clips[i].variant, variant, StringComparison.Ordinal)) return clips[i];
            return clips[0];
        }

        /// <summary>
        /// What a pawn's figure is drawing in the fight: its one-shot (or none), how much of the
        /// action layer is showing over the walk, how much of the work stroke is, and whether a
        /// computed combat pose is laid on. False for a pawn with no figure. Diagnostic — a contact
        /// sheet prints it, and a test asks it.
        /// </summary>
        public bool TryGetFight(PawnId pawn, out CombatRole action, out float clipWeight, out float workWeight,
            out bool computed)
        {
            if (_byPawn.TryGetValue(pawn.Value, out Figure? figure))
            {
                action = figure.Fight.Shown;
                clipWeight = figure.Fight.HasLayer ? figure.Fight.Weight : 0f;
                workWeight = figure.WorkWeight;
                computed = !figure.Fight.Computed.IsRest || figure.Fight.Lying;
                return true;
            }
            action = CombatRole.None;
            clipWeight = 0f;
            workWeight = 0f;
            computed = false;
            return false;
        }

        /// <summary>
        /// The reaction a pawn's figure is drawing to the last blow it took (design 33 §9a): what it
        /// is, how far in, whether it is laid over the figure's own swing as the computed flinch
        /// this frame, and whether the figure is sliding from a knock-back. False for a pawn with no
        /// figure. Diagnostic — a test asks it.
        /// </summary>
        public bool TryGetReaction(PawnId pawn, out HitReaction reaction, out float seconds, out bool overlaid,
            out bool sliding)
        {
            if (_byPawn.TryGetValue(pawn.Value, out Figure? figure))
            {
                CombatState fight = figure.Fight;
                reaction = fight.Reaction.Live ? fight.Reaction.Kind : HitReaction.None;
                seconds = fight.Reaction.Seconds;
                overlaid = fight.Overlaid;
                sliding = fight.Sliding;
                return true;
            }
            reaction = HitReaction.None;
            seconds = 0f;
            overlaid = false;
            sliding = false;
            return false;
        }

        /// <summary>Where a pawn's figure's head is, in the world, if it has a figure with a head. Diagnostic.</summary>
        public bool TryGetHead(PawnId pawn, out Vector3 head)
        {
            if (_byPawn.TryGetValue(pawn.Value, out Figure? figure) && figure.Head != null)
            {
                head = figure.Head.position;
                return true;
            }
            head = default;
            return false;
        }

        /// <summary>
        /// A combat role's clip only if it has exactly this variant. For the phases of a held state
        /// (begin, loop, end), where the first clip of the row would be the wrong half of it.
        /// </summary>
        CombatClipEntry? ExactClip(CombatRole role, string variant)
        {
            string? id = CombatPose.RowOf(role);
            if (id == null || !CombatRows.TryGetValue(id, out List<CombatClipEntry>? clips)) return null;
            for (int i = 0; i < clips.Count; i++)
                if (string.Equals(clips[i].variant, variant, StringComparison.Ordinal)) return clips[i];
            return null;
        }

        /// <summary>
        /// One figure's fight: the action it is drawing, the held states it has seen, and the two
        /// slots of its action mixer. Presentation state only — discarded on a lease like the rest
        /// of a figure.
        /// </summary>
        sealed class CombatState
        {
            public bool HasLayer;
            public AnimationLayerMixerPlayable Layer;
            public AnimationMixerPlayable Actions;
            public readonly AnimationClipPlayable[] Slots = new AnimationClipPlayable[2];
            public readonly CombatClipEntry?[] SlotClips = new CombatClipEntry?[2];
            public int Active;

            /// <summary>The active slot's share of the action mix, easing to one after a swap.</summary>
            public float Fade = 1f;

            /// <summary>The action layer's weight over the walk, eased.</summary>
            public float Weight;

            /// <summary>The one-shot being drawn, or <see cref="CombatRole.None"/>.</summary>
            public CombatRole Action;

            /// <summary>Its variant: a direction, a combo step, or a phase of a held state.</summary>
            public string Variant = string.Empty;

            /// <summary>The clip it plays, or null for the computed pose.</summary>
            public CombatClipEntry? Clip;

            public BlowSide Side;

            /// <summary>A swing's start, in simulation ticks, and its wind-up.</summary>
            public float StartTick;
            public int Windup;

            /// <summary>Seconds into a one-shot that is not a swing.</summary>
            public float Seconds;

            /// <summary>The next combo step a swing of each family will take.</summary>
            public int Combo;

            /// <summary>The held states as last drawn, so their edges can start the begin and end clips.</summary>
            public bool Downed;
            public bool Stunned;

            /// <summary>Seconds in the current held state, for its loop.</summary>
            public float HeldSeconds;

            /// <summary>This frame's computed pose, laid on by the pose pass; rest when a clip is showing.</summary>
            public CombatShape Computed;

            /// <summary>Downed with no clip to lie down in: the sleeper's lie stands in.</summary>
            public bool Lying;

            /// <summary>How far the feet are released from the ground by the clip showing: a knocked-down body's are in the air.</summary>
            public float Unplanted;

            /// <summary>
            /// The reaction to the last blow taken (design 33 §9a), beside the one-shot in
            /// <see cref="Action"/>: a swing and a reaction are live at once, and
            /// <see cref="CombatReactions.Arbitrate"/> says which has the layer.
            /// </summary>
            public ReactionTrack Reaction;

            /// <summary>The pack's hit react or stagger for it, from its side, or null to compute it.</summary>
            public CombatClipEntry? ReactionClip;

            /// <summary>What the layer shows this frame: a swing's role, a reaction's, a held state's, or none. Diagnostic.</summary>
            public CombatRole Shown;

            /// <summary>Whether the computed flinch is laid over a swing this frame. Diagnostic.</summary>
            public bool Overlaid;

            /// <summary>
            /// A knock-back's slide (design 33 §9a): the from-position's offset from the landing
            /// position, and seconds into the slide. Drawn only; the pawn is on the landing tile.
            /// </summary>
            public bool Sliding;
            public Vector3 SlideFrom;
            public float SlideSeconds;

            /// <summary>
            /// The draw and the sheathe's layer (design 33 §8b): input 2 of <see cref="Layer"/>,
            /// masked to the upper body so the legs keep walking, with one clip slot. Absent where
            /// the pack's sheath rows did not resolve, and the weapon then snaps between hip and hand.
            /// </summary>
            public bool HasSheathLayer;
            public AnimationClipPlayable SheathSlot;
            public CombatClipEntry? SheathSlotClip;

            /// <summary>Forget everything but the graph: a figure lent to a new pawn starts its fight afresh.</summary>
            public void Forget()
            {
                if (HasSheathLayer) Layer.SetInputWeight(PawnFigureDirector.SheathLayerInput, 0f);
                Action = CombatRole.None;
                Variant = string.Empty;
                Clip = null;
                Seconds = 0f;
                Downed = false;
                Stunned = false;
                HeldSeconds = 0f;
                Computed = CombatShape.Rest;
                Lying = false;
                Unplanted = 0f;
                Reaction.Clear();
                ReactionClip = null;
                Shown = CombatRole.None;
                Overlaid = false;
                Sliding = false;
                Weight = 0f;
                if (HasLayer) Layer.SetInputWeight(1, 0f);
            }
        }

        /// <summary>
        /// Build the clip layer on a new figure's graph: the gait mixer into input 0, a two-slot
        /// action mixer into input 1 at weight nought. Returns what the output should read — the
        /// layer where there is one, the gait mixer where the figure cannot play a combat clip
        /// (an animal, whose rig the pack's humanoid clips cannot drive, or a checkout without the
        /// pack, whose graphs stay exactly as they were).
        /// </summary>
        Playable BuildCombatLayer(PlayableGraph graph, AnimationMixerPlayable gaits, bool animal, CombatState fight)
        {
            // A jump's clips use the same slot (design 43 §7), so the layer is built for either.
            if (animal || !(HasCombatClips || HasJumpClips)) return gaits;

            bool sheath = HasSheathClips;
            fight.Layer = AnimationLayerMixerPlayable.Create(graph, sheath ? 3 : 2);
            graph.Connect(gaits, 0, fight.Layer, 0);
            fight.Layer.SetInputWeight(0, 1f);
            fight.Actions = AnimationMixerPlayable.Create(graph, 2);
            graph.Connect(fight.Actions, 0, fight.Layer, 1);
            fight.Layer.SetInputWeight(1, 0f);
            fight.HasLayer = true;

            // The draw and the sheathe on top (design 33 §8b), arms and body only: a colonist
            // drawing on the move keeps walking. Empty until a clip is put in it.
            if (sheath)
            {
                fight.Layer.SetLayerMaskFromAvatarMask((uint)SheathLayerInput, UpperBody);
                fight.Layer.SetInputWeight(SheathLayerInput, 0f);
                fight.HasSheathLayer = true;
            }
            return fight.Layer;
        }

        /// <summary>
        /// A new moment of a fight, delivered once, in id order, by <c>CombatFeedback</c> after the
        /// frame's <see cref="Sync"/>. A figure that is not on screen has nothing to draw and the
        /// moment is let go: the state it leaves behind — hurt, down, stunned — arrives on the pawn
        /// itself and is drawn from there whenever a figure is next lent to it.
        /// </summary>
        public void OnCombatEvent(in CombatEventView combatEvent)
        {
            switch (combatEvent.Kind)
            {
                case CombatEventKind.Swing:
                case CombatEventKind.SwingCritical: // a swing whose blow will be critical: drawn alike (design 33 §9g)
                    if (_byPawn.TryGetValue(combatEvent.Attacker.Value, out Figure? attacker))
                        TimeSwing(attacker, combatEvent);
                    return;

                // Every landed blow flinches; a heavy one, a critical and a stun stagger (design 33
                // §9a). Which, and whether it ends the struck body's own swing, is the model's.
                case CombatEventKind.Hit:
                case CombatEventKind.Critical:
                case CombatEventKind.Stun:
                    if (_byPawn.TryGetValue(combatEvent.Target.Value, out Figure? struck))
                        React(struck, CombatReactions.For(combatEvent.Kind, combatEvent.Amount), combatEvent.Attacker,
                            CombatReactions.CancelsSwing(combatEvent.Kind));
                    return;

                case CombatEventKind.KnockedBack:
                    if (_byPawn.TryGetValue(combatEvent.Target.Value, out Figure? knocked))
                        KnockBack(knocked, combatEvent);
                    return;

                case CombatEventKind.Dodge:
                    if (_byPawn.TryGetValue(combatEvent.Target.Value, out Figure? dodger))
                        Dodge(dodger, combatEvent.Attacker);
                    return;
            }

            // Miss draws nothing on either figure: the swing already followed through. Downed and
            // Recovered are drawn off the pawn's own flag, so a figure lent mid-fight shows them
            // too, and so is the knock-down (PawnFlags.KnockedDown); Died is the corpse's, and the
            // pawn is already gone from the frame.
        }

        /// <summary>
        /// A swing seen on the gesture serial. Its family comes from the weapon in the hand as the
        /// frame publishes it, its wind-up from the default until the event refines both.
        /// </summary>
        void BeginStrike(Figure figure, in PawnView pawn)
        {
            int weapon = _frame != null && _frame.TryGetPawnAspect(pawn.Id, CombatAspectNames.WeaponKey, out int held)
                ? held : -1;
            AttackStyle style = CombatPose.StyleFor(weapon, WeaponStyles, pawn.IsPerson);
            StartSwing(figure, style, _frame != null ? _frame.Tick : _frameTicks, DefaultWindupTicks);
        }

        /// <summary>
        /// The swing's own report: when its wind-up began, how long it is and what it is swung
        /// with. It arrives in the frame the gesture was seen, and refines that swing rather than
        /// starting a second one; a swing whose gesture was never seen starts here.
        /// </summary>
        void TimeSwing(Figure figure, in CombatEventView swing)
        {
            if (!(_frame != null && _frame.TryGetPawn(swing.Attacker, out PawnView pawn))) return;
            AttackStyle style = CombatPose.StyleFor(swing.Weapon, WeaponStyles, pawn.IsPerson);
            CombatRole role = CombatPose.SwingRole(style);
            CombatState fight = figure.Fight;

            bool same = IsSwing(fight.Action) && Mathf.Abs(fight.StartTick - swing.Tick) <= DefaultWindupTicks;
            if (same && fight.Action == role)
            {
                fight.StartTick = swing.Tick;
                fight.Windup = swing.Amount;
                return;
            }

            if (same) fight.Combo--;   // the gesture's guess took a step it should not have
            StartSwing(figure, style, swing.Tick, swing.Amount);
        }

        void StartSwing(Figure figure, AttackStyle style, float startTick, int windup)
        {
            CombatState fight = figure.Fight;
            // A swing is the figure's own and nothing interrupts it but going down; nor does a
            // downed figure begin one. Getting up does give way: the simulation has the pawn on its
            // feet and swinging while the pack's two-second get-up would still be playing.
            if (fight.Downed || (IsHeldPhase(fight.Action) && fight.Variant != CombatVariant.End)) return;

            CombatRole role = CombatPose.SwingRole(style);
            string variant = CombatVariant.Combo[Mathf.Abs(fight.Combo) % CombatVariant.Combo.Length];
            fight.Combo++;

            fight.Action = role;
            fight.Variant = variant;
            fight.Clip = fight.HasLayer ? CombatClip(role, variant) : null;
            fight.StartTick = startTick;
            fight.Windup = windup;
            fight.Seconds = 0f;
        }

        /// <summary>
        /// A blow taken (design 33 §9a): a flinch or a stagger, from the side it came, on the
        /// figure's reaction track — <b>whatever the figure is doing</b>. It does not replace the
        /// figure's own swing, which the simulation will still land on its tick; each frame
        /// <see cref="CombatReactions.Arbitrate"/> decides which of the two the layer shows. A stun
        /// does end the swing, because a stunned attacker's wound-up swing does not land (§5j).
        /// On the ground the held clips draw the body and a blow adds nothing; getting up is cut
        /// short by one.
        /// </summary>
        void React(Figure figure, HitReaction reaction, PawnId attacker, bool cancelsSwing)
        {
            CombatState fight = figure.Fight;
            if (fight.Downed || (IsHeldPhase(fight.Action) && fight.Variant != CombatVariant.End)) return;
            if (!fight.Reaction.Accepts(reaction)) return;

            BlowSide side = SideOfBlow(figure, attacker);
            CombatClipEntry? clip = fight.HasLayer ? CombatClip(CombatPose.RoleOf(reaction), CombatPose.VariantOf(side)) : null;
            float span = clip != null && clip.clip != null ? clip.clip.length : CombatReactions.ComputedSeconds(reaction);
            if (!fight.Reaction.Take(reaction, (HitSide)side, span, cancelsSwing)) return;
            fight.ReactionClip = clip;

            // A stun ends the swing; a get-up is cut short; a dodge gives way to the blow that landed.
            if ((cancelsSwing && IsSwing(fight.Action)) || IsHeldPhase(fight.Action) || fight.Action == CombatRole.Dodge)
                fight.Action = CombatRole.None;
        }

        /// <summary>A blow dodged: a step away from it, never cutting off the figure's own swing.</summary>
        void Dodge(Figure figure, PawnId attacker)
        {
            CombatState fight = figure.Fight;
            if (fight.Downed || IsHeldPhase(fight.Action) || IsSwing(fight.Action)) return;

            BlowSide side = SideOfBlow(figure, attacker);
            string variant = CombatPose.DodgeVariant(side);
            fight.Action = CombatRole.Dodge;
            fight.Side = side;
            fight.Variant = variant;
            fight.Clip = !fight.HasLayer ? null : CombatClip(CombatRole.Dodge, variant);
            fight.Seconds = 0f;
        }

        /// <summary>Which side of the figure the attacker is on: its figure where it has one, else its cell.</summary>
        BlowSide SideOfBlow(Figure figure, PawnId attacker)
        {
            Vector3 from = figure.Transform.position;
            if (_byPawn.TryGetValue(attacker.Value, out Figure? other) && other.Transform != null)
                from = other.Transform.position;
            else if (_frame != null && _frame.TryGetPawn(attacker, out PawnView them))
                from = GroundRelief.Lift(CellMetrics.FloorCentre(them.Cell));
            return CombatPose.SideOf(figure.Transform.forward, from - figure.Transform.position);
        }

        /// <summary>
        /// Knocked back a tile (design 33 §9a, §9b): slid from where the blow found it
        /// (<see cref="CombatEventView.Amount"/>, a cell index) to where it landed
        /// (<see cref="CombatEventView.Cell"/>) along the line of the blow, over
        /// <see cref="KnockbackSlide.Seconds"/>, dropping if it went a layer down. The knock-down
        /// clip is the flag's (<see cref="PawnFlags.KnockedDown"/>), drawn by the held states. The
        /// pawn is on the landing tile from the tick of the blow; this is only the drawing, and a
        /// knock-back from further than a tile and a step is not drawn as one.
        /// </summary>
        void KnockBack(Figure figure, in CombatEventView knock)
        {
            CombatState fight = figure.Fight;
            fight.Reaction.Clear();
            fight.ReactionClip = null;
            if (_frame == null) return;

            GridSize size = _frame.Size;
            if (knock.Amount < 0 || knock.Amount >= size.SizeX * size.SizeZ * size.SizeY) return;
            Vector3 offset = GroundRelief.Lift(CellMetrics.FloorCentre(size.FromIndex(knock.Amount)))
                             - GroundRelief.Lift(CellMetrics.FloorCentre(knock.Cell));
            if (new Vector2(offset.x, offset.z).magnitude > CellMetrics.SizeXZ * 1.5f
                || Mathf.Abs(offset.y) > CellMetrics.SizeY * 1.5f)
                return;

            fight.SlideFrom = offset;
            fight.SlideSeconds = 0f;
            fight.Sliding = true;

            // This frame was posed on the landing tile before its events were read: put it back
            // where the blow found it, and forget the speed that one-frame jump measured, or the
            // legs would break into a run under the fall.
            figure.Transform.position += offset;
            figure.Speed = 0f;
        }

        static bool IsSwing(CombatRole role) =>
            role == CombatRole.SwingLight || role == CombatRole.SwingHeavy
            || role == CombatRole.Punch || role == CombatRole.Bite;

        /// <summary>The begin and end of the held states: going down, getting up, the stun's two edges.</summary>
        static bool IsHeldPhase(CombatRole role) => role == CombatRole.Downed || role == CombatRole.Stun;

        /// <summary>
        /// Once a frame per figure, from <see cref="Pose"/> after the gesture serial has been read:
        /// advance the action, follow the held states' edges, decide what is showing — a clip in
        /// the action layer or a computed pose — and put the clip at its time.
        /// </summary>
        void PoseCombat(Figure figure, in PawnView pawn, float deltaTime, bool running)
        {
            CombatState fight = figure.Fight;
            float dt = running ? deltaTime : 0f;

            // The held states' edges. A figure seeing a pawn for the first time takes it as it is,
            // with no begin: somebody scrolled to a colonist who has been down for an hour, not
            // watched her fall.
            // Knocked down is on the ground as downed is, and drawn by the same clips (design 33 §9a):
            // the knock-down's begin, its loop while the flag is up, and its get-up.
            bool downed = CombatReactions.Floored(pawn.Flags);
            bool stunned = pawn.IsStunned && !downed;
            bool first = !figure.Settled;
            if (downed != fight.Downed)
            {
                // Going down or getting up ends whatever one-shot was playing: a swing does not
                // follow through on the ground, nor a flinch.
                fight.HeldSeconds = 0f;
                fight.Action = CombatRole.None;
                fight.Reaction.Clear();
                fight.ReactionClip = null;
                if (!first) StartHeldPhase(fight, CombatRole.Downed, downed ? CombatVariant.Begin : CombatVariant.End);
                fight.Downed = downed;
            }
            if (stunned != fight.Stunned)
            {
                fight.HeldSeconds = 0f;
                // A stun opens with the stagger its blow already started, where there was one.
                if (!first && !downed && !(fight.Reaction.Live && fight.Reaction.Kind == HitReaction.Stagger))
                    StartHeldPhase(fight, CombatRole.Stun, stunned ? CombatVariant.Begin : CombatVariant.End);
                fight.Stunned = stunned;
            }
            if (downed || stunned) fight.HeldSeconds += dt;

            // The reaction's clock and the slide's, on the frame's seconds like every ease here.
            fight.Reaction.Step(dt);
            if (!fight.Reaction.Live) fight.ReactionClip = null;
            if (fight.Sliding)
            {
                fight.SlideSeconds += dt;
                if (KnockbackSlide.Finished(fight.SlideSeconds)) fight.Sliding = false;
            }

            // The one-shot: advanced, and let go when it is over.
            float clipTime = 0f;
            if (fight.Action != CombatRole.None)
            {
                if (IsSwing(fight.Action))
                {
                    float elapsed = Mathf.Max(0f, _frameTicks - fight.StartTick);
                    if (fight.Clip != null)
                    {
                        clipTime = CombatPose.ClipTime(elapsed, fight.Windup, fight.Clip.impactSeconds);
                        if (clipTime >= fight.Clip.clip!.length) fight.Action = CombatRole.None;
                    }
                    else if (CombatPose.SwingFinished(CombatPose.Phase(elapsed, fight.Windup)))
                        fight.Action = CombatRole.None;
                }
                else
                {
                    fight.Seconds += dt;
                    clipTime = fight.Seconds;
                    float span = fight.Clip != null ? fight.Clip.clip!.length
                        : CombatPose.ReactionSeconds(fight.Action);
                    if (clipTime >= span) fight.Action = CombatRole.None;
                }
            }

            // Who has the layer: the swing or the reaction, when both are live (design 33 §9a).
            bool swinging = IsSwing(fight.Action);
            bool reacting = fight.Reaction.Live;
            float swingPhase = swinging
                ? CombatPose.Phase(Mathf.Max(0f, _frameTicks - fight.StartTick), fight.Windup)
                : 0f;
            ActionShown shown = fight.Reaction.Show(swinging, swingPhase);

            // What shows. The reaction or a one-shot first; then the held loop; then nothing.
            CombatClipEntry? showing = null;
            float showingTime = 0f;
            fight.Computed = CombatShape.Rest;
            fight.Lying = false;
            fight.Unplanted = 0f;
            fight.Shown = CombatRole.None;
            fight.Overlaid = false;

            if (shown == ActionShown.Reaction)
            {
                fight.Shown = CombatPose.RoleOf(fight.Reaction.Kind);
                if (fight.ReactionClip != null && fight.HasLayer)
                {
                    showing = fight.ReactionClip;
                    showingTime = fight.Reaction.Seconds;
                }
                else fight.Computed = CombatPose.Reaction(fight.Reaction);
            }
            else if (fight.Action != CombatRole.None)
            {
                fight.Shown = fight.Action;
                if (fight.Clip != null && fight.HasLayer)
                {
                    showing = fight.Clip;
                    showingTime = clipTime;
                }
                else if (IsSwing(fight.Action))
                {
                    float elapsed = Mathf.Max(0f, _frameTicks - fight.StartTick);
                    fight.Computed = CombatPose.Swing(fight.Action, CombatPose.Phase(elapsed, fight.Windup));
                }
                else if (!IsHeldPhase(fight.Action))
                {
                    fight.Computed = CombatPose.Reaction(fight.Action, fight.Side, fight.Seconds);
                }
                if (fight.Action == CombatRole.Downed && showing != null) fight.Unplanted = 1f;
            }

            if (shown != ActionShown.Reaction
                && (fight.Action == CombatRole.None || (IsHeldPhase(fight.Action) && showing == null)))
            {
                if (downed || stunned) fight.Shown = downed ? CombatRole.Downed : CombatRole.Stun;
                if (downed)
                {
                    // Carried, or lying in a bed (design 33 §11e): not the pack's floor loop but the
                    // lying path, which the sleep pose aims at the cradle or the mattress.
                    CombatClipEntry? loop = fight.HasLayer && !Cradled(in pawn)
                        ? ExactClip(CombatRole.Downed, CombatVariant.Loop) : null;
                    if (loop != null)
                    {
                        showing = loop;
                        showingTime = Mathf.Repeat(fight.HeldSeconds, Mathf.Max(1e-3f, loop.clip!.length));
                        fight.Unplanted = 1f;
                    }
                    else fight.Lying = true;
                }
                else if (stunned)
                {
                    CombatClipEntry? loop = fight.HasLayer ? ExactClip(CombatRole.Stun, CombatVariant.Loop) : null;
                    if (loop != null)
                    {
                        showing = loop;
                        showingTime = Mathf.Repeat(fight.HeldSeconds, Mathf.Max(1e-3f, loop.clip!.length));
                    }
                    else fight.Computed = CombatPose.Stunned(fight.HeldSeconds);
                }
            }

            // A blow the layer is not showing as a reaction — taken round the figure's own impact,
            // or outliving the swing it yielded to — is laid on as the computed flinch, over the
            // swing or the idle, so every landed blow is seen (design 33 §9a).
            if (reacting)
            {
                CombatShape flinch = CombatShape.Of(fight.Reaction.Overlay(shown));
                if (!flinch.IsRest)
                {
                    fight.Overlaid = true;
                    fight.Computed = fight.Computed.Plus(flinch);
                }
            }

            // A jump over a stream takes the slot whenever the fight has nothing to show in it
            // (design 43 §7). Timed from the step, not by the frame's seconds, so it needs no dt.
            if (showing == null && figure.JumpClip != null)
            {
                showing = figure.JumpClip;
                showingTime = figure.JumpClipTime;
            }

            ShowCombatClip(figure, showing, showingTime, dt);
        }

        /// <summary>Begin or end a held state with its clip, where the pack has one; with none, the ease does it.</summary>
        void StartHeldPhase(CombatState fight, CombatRole role, string phase)
        {
            CombatClipEntry? clip = fight.HasLayer ? ExactClip(role, phase) : null;
            if (clip == null)
            {
                if (IsHeldPhase(fight.Action)) fight.Action = CombatRole.None;
                return;
            }
            fight.Action = role;
            fight.Variant = phase;
            fight.Clip = clip;
            fight.Seconds = 0f;
        }

        /// <summary>
        /// Put a clip in the action layer at a time, or ease the layer out. A clip already in the
        /// active slot is re-timed; a new one goes in the other slot and is cross-faded in.
        /// </summary>
        void ShowCombatClip(Figure figure, CombatClipEntry? clip, float time, float dt)
        {
            CombatState fight = figure.Fight;
            if (!fight.HasLayer) return;

            float ease = CombatEaseSeconds > 1e-4f ? dt / CombatEaseSeconds : 1f;
            if (clip != null && clip.clip != null)
            {
                if (!ReferenceEquals(fight.SlotClips[fight.Active], clip))
                {
                    // A swap. Straight in when nothing was showing, cross-faded against the clip
                    // going out when something was.
                    bool fromNothing = fight.Weight <= 0.001f || fight.SlotClips[fight.Active] == null;
                    int next = fromNothing ? fight.Active : 1 - fight.Active;
                    PutInSlot(figure, next, clip);
                    fight.Active = next;
                    fight.Fade = fromNothing ? 1f : 0f;
                }

                fight.Slots[fight.Active].SetTime(time);
                fight.Fade = Mathf.MoveTowards(fight.Fade, 1f,
                    CombatCrossFadeSeconds > 1e-4f ? dt / CombatCrossFadeSeconds : 1f);
                fight.Weight = Mathf.MoveTowards(fight.Weight, 1f, ease);
            }
            else
            {
                fight.Weight = Mathf.MoveTowards(fight.Weight, 0f, ease);
            }

            fight.Actions.SetInputWeight(fight.Active, fight.Fade);
            fight.Actions.SetInputWeight(1 - fight.Active, 1f - fight.Fade);
            fight.Layer.SetInputWeight(1, fight.Weight);
            if (fight.Weight > 0.001f) FightingFigures++;
        }

        void PutInSlot(Figure figure, int slot, CombatClipEntry clip)
        {
            CombatState fight = figure.Fight;
            if (fight.Slots[slot].IsValid())
            {
                figure.Graph.Disconnect(fight.Actions, slot);
                fight.Slots[slot].Destroy();
            }

            AnimationClipPlayable playable = AnimationClipPlayable.Create(figure.Graph, clip.clip);
            // Timed by hand, never by the graph's clock: see the class remarks.
            playable.SetSpeed(0d);
            playable.SetApplyFootIK(false);
            figure.Graph.Connect(playable, 0, fight.Actions, slot);
            fight.Slots[slot] = playable;
            fight.SlotClips[slot] = clip;
        }

        /// <summary>
        /// The whole-body part of a computed pose: the lunge and the sidestep, turned into the
        /// figure's own frame. Added to where the figure is drawn, like the work stance's step.
        /// </summary>
        Vector3 CombatOffset(Figure figure)
        {
            CombatState fight = figure.Fight;
            Vector3 offset = Vector3.zero;
            // A knock-back's slide from where the blow found it (design 33 §9a), in the world's frame.
            if (fight.Sliding)
            {
                KnockbackSlide.Offset(fight.SlideFrom.x, fight.SlideFrom.y, fight.SlideFrom.z, fight.SlideSeconds,
                    out float x, out float y, out float z);
                offset = new Vector3(x, y, z);
            }

            CombatShape shape = fight.Computed;
            if (shape.IsRest) return offset;
            return offset + Quaternion.Euler(0f, figure.Yaw, 0f) * new Vector3(shape.Side, 0f, shape.Lunge);
        }

        /// <summary>
        /// The whole-body pitch of a computed pose, for a figure with no bones to fold — an animal
        /// biting drops its head into the bite by pitching its whole body. Nothing for a person,
        /// whose spine takes the fold in <see cref="ApplyCombatPose"/>.
        /// </summary>
        Quaternion CombatBodyPitch(Figure figure)
        {
            CombatShape shape = figure.Fight.Computed;
            if (shape.IsRest || figure.RightUpperArm != null) return Quaternion.identity;
            return Quaternion.Euler(shape.Spine, shape.Twist, 0f);
        }

        /// <summary>Whether the pose pass has a computed combat pose to lay on this figure.</summary>
        static bool ShowsComputedCombat(Figure figure) => !figure.Fight.Computed.IsRest;

        /// <summary>
        /// Lay a computed combat pose over the clip: the chest folded and turned, the working arm
        /// raised and bent, the other arm up in guard. Applied in the pose pass after the graph has
        /// rewritten the skeleton, exactly as the work stroke is.
        /// </summary>
        void ApplyCombatPose(Figure figure)
        {
            if (figure.RightUpperArm == null) return;
            CombatShape shape = figure.Fight.Computed;

            Vector3 right = figure.Transform.right;
            Vector3 up = figure.Transform.up;

            Pitch(figure.Spine, up, shape.Twist);
            Pitch(figure.Spine, right, shape.Spine);
            // The flinch's snap of the head on top of the chest (design 33 §9a).
            if (Mathf.Abs(shape.Head) > 1e-3f) Pitch(figure.Head, right, shape.Head);

            // An arm hangs down, so a negative pitch carries it forward and up; and it rides the
            // spine, so the spine's own fold is taken back out of it (WorkSwing's convention).
            Pitch(figure.RightUpperArm, right, -shape.Shoulder - shape.Spine);
            Pitch(figure.RightLowerArm, right, -shape.Elbow);
            Pitch(figure.LeftUpperArm, right, -shape.OffShoulder - shape.Spine);
            Pitch(figure.LeftLowerArm, right, -shape.Elbow * 0.5f);
        }

        // ---- The dead ---------------------------------------------------------------------------

        /// <summary>
        /// A figure lent out to lie down as a corpse (design 33 §3): posed through the death clip or
        /// the computed fall, baked to a static copy, and handed back. <b>Not counted against the
        /// ceiling</b> — it is nobody's live figure, and it is back in the pool as soon as it has
        /// been baked.
        /// </summary>
        public sealed class CorpseLoan
        {
            internal CorpseLoan(int index, Transform transform, int look, float length,
                CombatClipEntry? dying, CombatClipEntry? pose, bool animal)
            {
                Index = index;
                Transform = transform;
                Look = look;
                Length = length > 0.01f ? length : FigureBuild.FallbackHeight;
                Dying = dying;
                Pose = pose;
                Animal = animal;
            }

            /// <summary>Which of the director's figures is lent: they are never removed, so the index holds.</summary>
            internal readonly int Index;
            internal readonly CombatClipEntry? Dying;
            internal readonly CombatClipEntry? Pose;

            /// <summary>An animal lies on its side through the computed fall; it has no death clip.</summary>
            public readonly bool Animal;

            /// <summary>The figure being laid down, for a test to look at.</summary>
            public readonly Transform Transform;

            /// <summary>Which face it wears, as an index into the look table.</summary>
            public readonly int Look;

            /// <summary>Whether the pack's clips lay it down, rather than the computed fall.</summary>
            public bool Clipped => Dying != null || Pose != null;

            /// <summary>How long the fall takes, in seconds of game time.</summary>
            public float DyingSeconds =>
                Dying?.clip != null ? Dying.clip.length : ComputedFallSeconds;

            /// <summary>The drawn height, which is the length laid on the ground.</summary>
            public readonly float Length;
        }

        /// <summary>How long the computed fall takes, in seconds.</summary>
        public const float ComputedFallSeconds = 0.7f;

        /// <summary>
        /// Which face a corpse wears: the book's answer for the dead pawn's own id <b>and the seed
        /// it carried</b>, so the colonist who fell is the colonist lying there (design 33 §1). The
        /// frame no longer carries the pawn, so the seed comes off the corpse and not the frame.
        /// An animal wears its kind's row.
        /// </summary>
        public int LookForCorpse(in CorpseView corpse) =>
            (corpse.Flags & PawnFlags.Person) == 0
                ? AnimalLookIndex(corpse.Kind)
                : Appearances.LookFor(corpse.Pawn.Value, corpse.RollSeed, PawnOutfits.For(corpse));

        /// <summary>Whether this corpse can be drawn as a body at all; if not, a marker stands in.</summary>
        public bool CanDrawCorpse(in CorpseView corpse)
        {
            int look = LookForCorpse(corpse);
            return LookAt(look) != null;
        }

        /// <summary>
        /// Lend a figure out to lie down as this corpse, dressed in the colours its seed deals, or
        /// null when its face did not resolve. <paramref name="side"/> picks the directional death
        /// clip; the finished body is turned to lie along the corpse's own facing whichever it is.
        /// </summary>
        public CorpseLoan? BorrowForCorpse(in CorpseView corpse, BlowSide side = BlowSide.Front)
        {
            int look = LookForCorpse(corpse);
            Look? face = LookAt(look);
            if (face == null) return null;

            Figure figure = Free(look) ?? Create(look);
            figure.Borrowed = true;
            figure.Pawn = -1;
            figure.Outfit = PawnOutfits.For(corpse);
            Repaint(figure, corpse.Pawn, corpse.RollSeed);
            figure.Fight.Forget();
            figure.WorkWeight = 0f;
            ShowHeldTool(figure, working: false);
            // A corpse lets go of its weapon: the simulation laid it on the ground at the body.
            HideWeapon(figure);
            figure.GameObject.SetActive(true);

            string variant = CombatPose.VariantOf(side);
            bool animal = face.Animal;
            CombatClipEntry? dying = animal || !figure.Fight.HasLayer ? null : ExactClip(CombatRole.Death, variant) ?? CombatClip(CombatRole.Death, variant);
            CombatClipEntry? pose = animal || !figure.Fight.HasLayer ? null : ExactClip(CombatRole.DeathPose, variant) ?? CombatClip(CombatRole.DeathPose, variant);
            return new CorpseLoan(_figures.IndexOf(figure), figure.Transform, figure.Look, figure.StandingHeight,
                dying, pose, animal);
        }

        /// <summary>
        /// Pose a lent figure <paramref name="seconds"/> into its fall, lying along
        /// <paramref name="headingYaw"/> over <paramref name="floor"/>. Past the fall's end it holds
        /// the death pose. The graph is evaluated here, because a lent figure has no pawn and the
        /// director's own passes skip it — and because a bake straight after must see this pose.
        /// </summary>
        public void PoseCorpse(CorpseLoan loan, float seconds, Vector3 floor, float headingYaw)
        {
            Figure figure = _figures[loan.Index];
            CombatState fight = figure.Fight;

            if (loan.Clipped)
            {
                // Measure the finished pose first: which way from the feet the head ends up. The
                // body is turned so that direction is the corpse's heading — the simulation decided
                // which way it fell — and slid so the middle of it is on the middle of the cell.
                CombatClipEntry held = loan.Pose ?? loan.Dying!;
                float heldTime = loan.Pose != null ? 0f : held.clip!.length;
                figure.Transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                ShowClipNow(figure, held, heldTime);
                Vector3 head = figure.Head != null ? figure.Head.position : Vector3.forward;
                Vector3 reach = new Vector3(head.x, 0f, head.z);
                float turn = reach.sqrMagnitude > 1e-4f ? Mathf.Atan2(reach.x, reach.z) * Mathf.Rad2Deg : 0f;

                Quaternion yaw = Quaternion.Euler(0f, headingYaw - turn, 0f);
                figure.Transform.SetPositionAndRotation(floor - yaw * (reach * 0.5f), yaw);

                if (loan.Dying != null && seconds < loan.Dying.clip!.length)
                    ShowClipNow(figure, loan.Dying, Mathf.Max(0f, seconds));
                else
                    ShowClipNow(figure, held, heldTime);
                return;
            }

            // The computed fall: tipped over from standing to flat, quickly at the end.
            if (fight.HasLayer) fight.Layer.SetInputWeight(1, 0f);
            figure.Graph.Evaluate(0f);
            CombatPose.LieFlat(floor, headingYaw, loan.Length, loan.Animal, out Vector3 root, out Quaternion flat);
            float t = Mathf.Clamp01(seconds / ComputedFallSeconds);
            t *= t;
            Quaternion standing = Quaternion.Euler(0f, headingYaw, 0f);
            Vector3 standAt = floor;
            figure.Transform.SetPositionAndRotation(Vector3.Lerp(standAt, root, t), Quaternion.Slerp(standing, flat, t));
        }

        /// <summary>Put one clip at one time into the action layer, full weight, and evaluate the graph now.</summary>
        void ShowClipNow(Figure figure, CombatClipEntry clip, float time)
        {
            CombatState fight = figure.Fight;
            if (!ReferenceEquals(fight.SlotClips[fight.Active], clip)) PutInSlot(figure, fight.Active, clip);
            fight.Slots[fight.Active].SetTime(time);
            fight.Actions.SetInputWeight(fight.Active, 1f);
            fight.Actions.SetInputWeight(1 - fight.Active, 0f);
            fight.Layer.SetInputWeight(1, 1f);
            fight.Weight = 1f;
            figure.Graph.Evaluate(0f);
        }

        /// <summary>
        /// A static copy of a lent figure as it stands now: every visible skinned part baked to a
        /// mesh, and the hair and beard copied, under a new object parented to
        /// <paramref name="parent"/>. The meshes are the caller's to destroy
        /// (<paramref name="meshes"/>); the materials are shared and are not.
        /// </summary>
        public GameObject BakeCorpse(CorpseLoan loan, Transform parent, int layer, List<Mesh> meshes)
        {
            Figure figure = _figures[loan.Index];
            var root = new GameObject("Corpse");
            root.transform.SetParent(parent, false);
            root.layer = layer;

            for (int i = 0; i < figure.Skins.Length; i++)
            {
                SkinnedMeshRenderer skin = figure.Skins[i];
                if (skin == null || !skin.enabled || !skin.gameObject.activeInHierarchy || skin.sharedMesh == null) continue;
                var baked = new Mesh { name = skin.sharedMesh.name + "/corpse" };
                skin.BakeMesh(baked, useScale: true);
                meshes.Add(baked);
                AddStaticPart(root.transform, skin.transform, baked, skin.sharedMaterials, layer);
            }

            CopyAttachment(root.transform, figure.HairMesh, figure.HairRenderer, layer);
            CopyAttachment(root.transform, figure.BeardMesh, figure.BeardRenderer, layer);
            return root;
        }

        static void CopyAttachment(Transform root, MeshFilter? filter, MeshRenderer? renderer, int layer)
        {
            if (filter == null || renderer == null || !renderer.enabled || filter.sharedMesh == null) return;
            AddStaticPart(root, renderer.transform, filter.sharedMesh, renderer.sharedMaterials, layer);
        }

        static void AddStaticPart(Transform root, Transform source, Mesh mesh, Material[] materials, int layer)
        {
            var part = new GameObject(source.name);
            part.layer = layer;
            part.transform.SetParent(root, false);
            part.transform.SetPositionAndRotation(source.position, source.rotation);
            // The root is unscaled, so the part carries the whole of the source's scale: a baked
            // skin is in its renderer's own space and the figure's 1.4 lives above it (FigureBuild).
            part.transform.localScale = source.lossyScale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
        }

        /// <summary>
        /// Draw a lent figure or not, for a fall on a layer the slice shows or hides. Its renderers
        /// are forced off rather than the object put away, because a bake reads only the parts
        /// that are active and enabled, and an animator put away mid-fall would stop posing.
        /// </summary>
        public void ShowCorpse(CorpseLoan loan, bool shown)
        {
            if ((uint)loan.Index >= (uint)_figures.Count) return;
            Figure figure = _figures[loan.Index];
            if (figure.CorpseHidden == !shown) return;
            figure.CorpseHidden = !shown;
            foreach (Renderer renderer in figure.GameObject.GetComponentsInChildren<Renderer>(includeInactive: true))
                renderer.forceRenderingOff = !shown;
        }

        /// <summary>
        /// Hand a lent figure back to the pool, parked and at rest. A loan outliving its director —
        /// a session torn down mid-fall — has nothing to hand back to and is let go.
        /// </summary>
        public void ReturnCorpse(CorpseLoan loan)
        {
            if ((uint)loan.Index >= (uint)_figures.Count) return;
            ShowCorpse(loan, true);
            Figure figure = _figures[loan.Index];
            figure.Borrowed = false;
            figure.Pawn = -1;
            figure.Fight.Forget();
            figure.GameObject.SetActive(false);
        }
    }
}
