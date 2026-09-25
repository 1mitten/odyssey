#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// Jumping a one-cell stream, as the live figures draw it (design 46 §7). Where the figure is
    /// comes from <see cref="JumpArc"/> through <see cref="PawnPose"/>; what its body does comes
    /// from here — the pack's take-off and landing clips in the combat action slot, timed from the
    /// step's phase at speed nought as a sword swing is, and the air weight the footing fades by.
    ///
    /// <para><b>Why the combat slot rather than an input of its own.</b> A second layer input on
    /// every figure for a clip that plays for a second, when a fight and a jump never coincide:
    /// nothing in the game swings a blade in mid-air. The fight keeps the slot whenever it has
    /// something to show, and the jump takes it only when it does not.</para>
    ///
    /// <para><b>With no clip</b> — the CI runner, a clone without the packs, a row that did not
    /// resolve — the figure keeps the gait it arrived with, exactly as it does through a hop, and
    /// travels the same arc. Nothing else changes.</para>
    /// </summary>
    public sealed partial class PawnFigureDirector
    {
        /// <summary>The jump rows' usable clips by row id, read out of the catalogue once.</summary>
        Dictionary<string, List<CombatClipEntry>>? _jumpRows;

        Dictionary<string, List<CombatClipEntry>> JumpRows
        {
            get
            {
                if (_jumpRows != null) return _jumpRows;
                _jumpRows = new Dictionary<string, List<CombatClipEntry>>(StringComparer.Ordinal);
                if (_catalogue == null) return _jumpRows;
                foreach (string id in ModuleIds.JumpRows)
                {
                    ModuleEntry? row = _catalogue.Find(id);
                    if (row == null) continue;
                    var usable = new List<CombatClipEntry>();
                    foreach (CombatClipEntry entry in row.combat)
                        if (entry.clip != null) usable.Add(entry);
                    if (usable.Count > 0) _jumpRows[id] = usable;
                }
                return _jumpRows;
            }
        }

        /// <summary>
        /// True when the take-off and the landing both resolved to clips. The question a clip test
        /// asks, rather than whether there is a catalogue at all (CLAUDE.md, the runner rule).
        /// </summary>
        public bool HasJumpClips =>
            JumpRows.ContainsKey(ModuleIds.JumpTakeOff) && JumpRows.ContainsKey(ModuleIds.JumpLand);

        /// <summary>The take-off or the landing for a body: its own sex first, else the row's first clip, else null.</summary>
        public CombatClipEntry? JumpClipFor(bool land, bool feminine)
        {
            string id = land ? ModuleIds.JumpLand : ModuleIds.JumpTakeOff;
            if (!JumpRows.TryGetValue(id, out List<CombatClipEntry>? clips)) return null;
            string variant = feminine ? CombatVariant.Femn : CombatVariant.Masc;
            for (int i = 0; i < clips.Count; i++)
                if (string.Equals(clips[i].variant, variant, StringComparison.Ordinal)) return clips[i];
            return clips[0];
        }

        /// <summary>
        /// This frame's jump for one figure: how far off the ground it is and which clip is due.
        /// Called before the fight is posed, so <c>PoseCombat</c> can hand the jump the slot.
        /// </summary>
        void PoseJump(Figure figure, in PawnView pawn)
        {
            figure.AirWeight = 0f;
            figure.JumpClip = null;
            figure.JumpClipTime = 0f;
            if (!JumpArc.IsJump(in pawn)) return;

            float t = PawnPose.StepProgress(in pawn, _tickAlpha, _movePerTick);
            bool shortJump = !JumpArc.IsFullJump(in pawn);
            figure.AirWeight = JumpArc.Airborne(t, shortJump);

            JumpArc.ClipPhase phase = JumpArc.Clip(t, shortJump, out float seconds);
            if (phase == JumpArc.ClipPhase.None || !figure.Fight.HasLayer) return;

            Look? face = LookAt(figure.Look);
            figure.JumpClip = JumpClipFor(phase == JumpArc.ClipPhase.Land, face != null && face.Feminine);
            figure.JumpClipTime = seconds;
        }

        /// <summary>
        /// What a pawn's figure is drawing of a jump: how far off the ground, and the clip showing,
        /// if any. False for a pawn with no figure. Diagnostic — a PlayMode test asks it.
        /// </summary>
        public bool TryGetJump(PawnId pawn, out float air, out CombatClipEntry? clip)
        {
            if (_byPawn.TryGetValue(pawn.Value, out Figure? figure))
            {
                air = figure.AirWeight;
                clip = figure.JumpClip;
                return true;
            }
            air = 0f;
            clip = null;
            return false;
        }
    }
}
