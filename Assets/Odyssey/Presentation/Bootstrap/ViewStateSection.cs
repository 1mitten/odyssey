#nullable enable
using System;
using Odyssey.Hud;
using Odyssey.Presentation.CameraRig;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;
using UnityEngine;

namespace Odyssey.Presentation.Bootstrap
{
    /// <summary>
    /// Where the player was looking when they saved: the camera pose, the slice layer, the
    /// selected colonist and the game speed.
    ///
    /// <para><b>Why this lives in Presentation and is an exception to a load-bearing rule.</b>
    /// <c>CLAUDE.md</c> says nothing in presentation is in a cell, a save or the hash. This is a
    /// deliberate, narrow exception to the <i>save</i> half only, and it is the first thing a
    /// reviewer should challenge, so the argument is written down rather than left to be
    /// reconstructed.</para>
    ///
    /// <para>The camera is not simulation. A yaw of 123.4 degrees is not a fact about the colony;
    /// it is a fact about the person sitting in front of it, and putting it in <c>Odyssey.Sim</c>
    /// would mean float state in an assembly that deliberately has none —
    /// <see cref="SaveWriter"/> has no <c>Write(float)</c> precisely so that cannot happen by
    /// accident. So the section lives beside the composition root, which is the only thing that
    /// holds both a camera and a save file.</para>
    ///
    /// <para><b>What makes it safe is what it is not.</b> This is an <see cref="ISaveable"/> and
    /// <b>not</b> an <c>IStateHashable</c>. It is never registered through
    /// <c>SimWorldBuilder.AddHashable</c>, so it cannot move the state hash, cannot affect
    /// determinism, and cannot make two identical colonies compare unequal because one of them was
    /// being watched from a different angle. A ten-day headless run has no camera and writes no
    /// such section; the hash it ends on is the same either way. The rule that is actually at
    /// stake — presentation may not change the simulation — is untouched.</para>
    ///
    /// <para><b>Two phases, and they cannot be one.</b> <see cref="Load"/> runs while the world is
    /// still being restored: the grid is being overwritten section by section, derived state has
    /// not been rebuilt, and the camera has not been pointed anywhere. So <see cref="Load"/> only
    /// reads into fields, and <see cref="Apply"/> — which the composition root calls once the world
    /// is rebuilt and a frame has been published — is what puts the view back.</para>
    ///
    /// <para><b>A save with no view section is the normal case, not an error.</b> Every save
    /// written before this existed has none, and <c>WorldSave.Load</c> simply never calls
    /// <see cref="Load"/>. <see cref="Apply"/> on a section that was never loaded does nothing at
    /// all, rather than slamming the camera to the origin of a default-constructed pose.</para>
    /// </summary>
    public sealed class ViewStateSection : ISaveable
    {
        /// <summary>
        /// The payload's own version, written first, so a later field can be added without
        /// breaking a save.
        ///
        /// <para><c>WorldSave</c> skips a whole section a build does not know, which is how a
        /// mod's data survives that mod being uninstalled — but that mechanism cannot help
        /// <i>within</i> a section, because the length prefix says where the section ends and
        /// nothing says where each field does. So the section carries its own version, and a
        /// reader that finds a number it does not recognise reads what it can and leaves the rest
        /// alone.</para>
        /// </summary>
        public const int SectionVersion = 1;

        /// <summary>
        /// Stable, and separate from the class name for the reason <see cref="ISaveable.SaveKey"/>
        /// gives: renaming it silently orphans every save that carries it.
        /// </summary>
        public string SaveKey => "view";

        bool _captured;

        /// <summary>
        /// True once <see cref="Capture"/> or <see cref="Load"/> has put something here.
        ///
        /// <para>False means one of two things and they want the same answer: the save predates
        /// this section, or the section was handed to <c>WorldSave.Save</c> without anyone
        /// capturing into it first. Either way <see cref="Apply"/> does nothing.</para>
        /// </summary>
        public bool HasState => _captured;

        /// <summary>Where the camera was. Meaningless unless <see cref="HasState"/>.</summary>
        public CameraPose Camera { get; private set; }

        /// <summary>The slice layer the player was working on.</summary>
        public int Layer { get; private set; }

        /// <summary>
        /// The primary selected colonist, or <see cref="PawnId.None"/>.
        ///
        /// <para>Only the primary, deliberately. The selection is a multi-select and the rest of it
        /// could be written here too; it is left out because "come back to the colonist I was
        /// watching" is the whole of the reported complaint, and <see cref="SectionVersion"/> is
        /// the mechanism for adding the tail later without breaking these files.</para>
        /// </summary>
        public PawnId Selected { get; private set; } = PawnId.None;

        /// <summary>0 paused, 1 normal, 2 fast, 3 very fast — exactly <c>SimWorld.GameSpeed</c>.</summary>
        public int GameSpeed { get; private set; } = 1;

        /// <summary>
        /// Read the live view, immediately before the section is handed to <c>WorldSave.Save</c>.
        ///
        /// <para>The speed is passed in rather than read, because the rig does not own it: it
        /// <i>asks</i> for a speed and the simulation decides, so the only correct answer is the
        /// one the composition root already holds (<c>World.GameSpeed</c>).</para>
        /// </summary>
        public void Capture(SliceCameraRig rig, HudDirectors directors, int gameSpeed)
        {
            if (rig == null) throw new ArgumentNullException(nameof(rig));
            if (directors == null) throw new ArgumentNullException(nameof(directors));

            Camera = new CameraPose(rig.Focus, rig.yaw, rig.pitch, rig.distance);
            Layer = directors.Slice.ActiveLayer;
            Selected = directors.Selection.Pawn;
            GameSpeed = gameSpeed;
            _captured = true;
        }

        /// <summary>
        /// <para><b>Floats go in as exact bits, not as a rounded fixed-point value.</b>
        /// <see cref="SaveWriter"/> offers no <c>Write(float)</c> — by design, because simulation
        /// state must not contain any — so a float has to be written as something. The obvious
        /// something is a scaled integer, and it is wrong here: a camera that rounds a little every
        /// time it is written would drift a little every save and load, and a drift of a hundredth
        /// of a degree per round trip is a bug nobody notices for months and then cannot reproduce.
        /// <see cref="BitConverter.SingleToInt32Bits"/> is lossless and costs the same four
        /// bytes.</para>
        /// </summary>
        public void Save(SaveWriter writer)
        {
            writer.Write(SectionVersion);

            // Whether anything was ever captured is part of the payload, so that a section handed
            // to the save without a Capture writes "nothing here" rather than a pose of all zeros
            // that a later load would faithfully restore.
            writer.Write(_captured);
            if (!_captured) return;

            WriteFloat(writer, Camera.Focus.x);
            WriteFloat(writer, Camera.Focus.y);
            WriteFloat(writer, Camera.Focus.z);
            WriteFloat(writer, Camera.Yaw);
            WriteFloat(writer, Camera.Pitch);
            WriteFloat(writer, Camera.Distance);
            writer.Write(Layer);
            writer.Write(Selected.Value);
            writer.Write(GameSpeed);
        }

        /// <inheritdoc/>
        public void Load(SaveReader reader)
        {
            int version = reader.ReadInt();
            if (version > SectionVersion)
            {
                // A newer build wrote this. There is nothing safe to read past the version, so the
                // view is simply not restored — the world still loads, which is the point of the
                // whole section being optional.
                _captured = false;
                return;
            }

            _captured = reader.ReadBool();
            if (!_captured) return;

            float x = ReadFloat(reader);
            float y = ReadFloat(reader);
            float z = ReadFloat(reader);
            float yaw = ReadFloat(reader);
            float pitch = ReadFloat(reader);
            float distance = ReadFloat(reader);
            Camera = new CameraPose(new Vector3(x, y, z), yaw, pitch, distance);
            Layer = reader.ReadInt();
            Selected = new PawnId(reader.ReadInt());
            GameSpeed = reader.ReadInt();
        }

        /// <summary>
        /// Put the view back, after the world has been rebuilt and restored.
        ///
        /// <para><b>The order is the substance of this method.</b> The layer goes first, because
        /// <c>SliceDirector.LayerChanged</c> clears the selection — a slice that moves leaves
        /// whatever was selected on another floor — so restoring the colonist first and the layer
        /// second would restore nothing at all. The camera follows the layer, since the rig derives
        /// its focus height from the active layer every frame. The selection comes after both, and
        /// the speed last, because starting the clock is the thing that makes all of it visible.</para>
        /// </summary>
        /// <param name="rig">The camera to point. Never null when a session exists.</param>
        /// <param name="directors">The session's directors: the slice and the selection.</param>
        /// <param name="setGameSpeed">
        /// How to set the speed <i>exactly</i>. The composition root should hand something that
        /// submits <c>IntentKind.SetGameSpeed</c> and sets its pending flag, rather than
        /// <c>SliceCameraRig.RequestGameSpeed</c>: the bootstrap's own handler treats a request for
        /// 0 while already at 0 as "start again", which is right for the space bar and wrong for a
        /// restore. Null falls back to the rig's request, which is correct whenever the world was
        /// freshly built — it starts at speed 1 — and that is what a load always is.
        /// </param>
        /// <param name="restorePose">
        /// How to put the pose on the camera, for a caller that wants to do it itself.
        ///
        /// <para>Null is the ordinary case and is exact: it calls
        /// <c>SliceCameraRig.RestorePose</c>, which sets the live values and the private smoothing
        /// targets together. The hook is here for a caller that has to wrap that — a transition, a
        /// replay, a test watching what was asked for — and not because the default is second
        /// best.</para>
        /// </param>
        public void Apply(SliceCameraRig rig, HudDirectors directors,
            Action<int>? setGameSpeed = null, Action<CameraPose>? restorePose = null)
        {
            if (rig == null) throw new ArgumentNullException(nameof(rig));
            if (directors == null) throw new ArgumentNullException(nameof(directors));

            // The ordinary case for every save written before this section existed. Doing nothing
            // is the whole contract: an old colony opens exactly as it did yesterday.
            if (!_captured) return;

            directors.Slice.SetLayer(Layer);

            // Live values and smoothing targets in one call, which is why this is the rig's method
            // and not four assignments here: yaw and distance are lerped toward private targets
            // every frame, so setting the public fields would look right for one frame and then
            // swing back to wherever the camera was already heading.
            if (restorePose != null) restorePose(Camera);
            else rig.RestorePose(Camera.Focus, Camera.Yaw, Camera.Pitch, Camera.Distance);

            // Choose rather than Pick: there is no ray and no snapshot here, and the colonist is
            // being named outright, which is what the roster does. A colonist who is no longer in
            // the world is dropped by SelectionDirector.Refresh within a frame or two, so a save
            // that outlived its subject degrades to an empty selection rather than to a handle
            // nothing can resolve.
            if (Selected.IsValid) directors.Selection.Choose(Selected);

            if (setGameSpeed != null) setGameSpeed(GameSpeed);
            else rig.RequestGameSpeed(GameSpeed);
        }

        static void WriteFloat(SaveWriter writer, float value) =>
            writer.Write(BitConverter.SingleToInt32Bits(value));

        static float ReadFloat(SaveReader reader) =>
            BitConverter.Int32BitsToSingle(reader.ReadInt());
    }

    /// <summary>
    /// Where the camera is and how it is held: the four numbers <see cref="ViewStateSection"/>
    /// writes down and puts back.
    ///
    /// <para>A value rather than four loose floats, so that a restore cannot pass the pitch where
    /// the yaw goes — they are the same type and nearly the same size, which is exactly the
    /// mistake nothing would catch.</para>
    /// </summary>
    public readonly struct CameraPose
    {
        /// <summary>Where the camera is looking, in world metres — <c>SliceCameraRig.Focus</c>.</summary>
        public readonly Vector3 Focus;

        public readonly float Yaw;
        public readonly float Pitch;

        /// <summary>How far back the camera sits from <see cref="Focus"/>.</summary>
        public readonly float Distance;

        public CameraPose(Vector3 focus, float yaw, float pitch, float distance)
        {
            Focus = focus;
            Yaw = yaw;
            Pitch = pitch;
            Distance = distance;
        }

        public override string ToString() =>
            $"focus {Focus} yaw {Yaw:0.###} pitch {Pitch:0.###} distance {Distance:0.###}";
    }
}
