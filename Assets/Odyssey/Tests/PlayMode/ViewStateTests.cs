#nullable enable
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;
using UnityEngine;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// A save puts the player back where they were looking, not merely back in the same world.
    ///
    /// <para><b>The complaint this answers</b> (owner, 2026-09-17): "it didn't save where the
    /// camera was and the exact state of where I was at - which it should do." A save restored the
    /// colony and dropped the player at the generated start cell, on the default layer, at the
    /// default angle, with nothing selected.</para>
    ///
    /// <para><b>PlayMode and not the fast tier</b>, because <see cref="SliceCameraRig"/> is a
    /// MonoBehaviour on a real camera and the numbers under test are the ones it is holding after a
    /// frame has run. The fast tier compiles neither Presentation nor the rig.</para>
    ///
    /// <para><b>Exact bits, and that is the point of most of this file.</b>
    /// <see cref="ViewStateSection"/> writes each float through
    /// <see cref="BitConverter.SingleToInt32Bits"/> rather than as a rounded fixed-point value, so
    /// the assertions here compare bit patterns rather than asking for a tolerance. A camera that
    /// came back a hundredth of a degree out every time would pass any reasonable tolerance and
    /// drift for months.</para>
    /// </summary>
    public class ViewStateTests
    {
        // A view nobody arrives at by accident, and deliberately off round numbers — though every
        // one of these is exactly representable as a float, so a failure means the round trip lost
        // something rather than that the literal was never there in the first place.
        const float Yaw = 123.4375f;
        const float Pitch = 37.3125f;
        const float Distance = 41.5f;
        const int Layer = 4;
        const int Speed = 2;
        static readonly CellRef Looking = new CellRef(17, 23, Layer);

        // Somewhere definitely else, for the moves that must not survive and the ones that must.
        const float OtherPitch = 61.5f;
        const float OtherYaw = 210.25f;
        const int OtherLayer = 1;
        static readonly CellRef Elsewhere = new CellRef(3, 5, OtherLayer);

        [UnityTest]
        public IEnumerator TheViewSurvivesAStreamBitForBit()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig rig, out _);
            yield return RigWorld.WarmUp();
            yield return RigWorld.SettleCamera(rig);

            HudDirectors directors = boot.Directors!;
            PawnId pawn = FirstColonist(boot);
            PointSomewhereDefinite(rig, directors, pawn);

            var written = new ViewStateSection();
            written.Capture(rig, directors, Speed);

            ViewStateSection read = RoundTrip(boot, written);

            Assert.That(read.HasState, Is.True, "the section came back with nothing in it");
            SameBits(Looking.X * CellMetrics.SizeXZ + CellMetrics.HalfXZ, read.Camera.Focus.x, "focus x");
            SameBits(Layer * CellMetrics.SizeY, read.Camera.Focus.y, "focus y");
            SameBits(Looking.Z * CellMetrics.SizeXZ + CellMetrics.HalfXZ, read.Camera.Focus.z, "focus z");
            SameBits(Yaw, read.Camera.Yaw, "yaw");
            SameBits(Pitch, read.Camera.Pitch, "pitch");
            SameBits(Distance, read.Camera.Distance, "distance");

            // And against what was actually captured, which is the assertion that would catch a
            // capture reading the wrong field rather than a writer losing precision.
            SameBits(written.Camera.Focus.x, read.Camera.Focus.x, "focus x against capture");
            SameBits(written.Camera.Yaw, read.Camera.Yaw, "yaw against capture");
            SameBits(written.Camera.Pitch, read.Camera.Pitch, "pitch against capture");
            SameBits(written.Camera.Distance, read.Camera.Distance, "distance against capture");

            Assert.That(read.Layer, Is.EqualTo(Layer), "the slice layer did not come back");
            Assert.That(read.Selected, Is.EqualTo(pawn), "the selected colonist did not come back");
            Assert.That(read.GameSpeed, Is.EqualTo(Speed), "the game speed did not come back");

            UnityEngine.Object.Destroy(root);
        }

        /// <summary>
        /// The other half: what was read is put back on a camera and an interface that have since
        /// moved on, which is exactly the state a load leaves them in.
        /// </summary>
        [UnityTest]
        public IEnumerator ASavedViewIsPutBackOnTheRigAndTheInterface()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig rig, out _);
            yield return RigWorld.WarmUp();
            yield return RigWorld.SettleCamera(rig);

            HudDirectors directors = boot.Directors!;
            PawnId pawn = FirstColonist(boot);
            PointSomewhereDefinite(rig, directors, pawn);

            var written = new ViewStateSection();
            written.Capture(rig, directors, Speed);
            ViewStateSection read = RoundTrip(boot, written);

            // Everything the restore has to overcome. A real load rebuilds the world from scratch,
            // so the camera is on the start cell and the selection is empty; this is that state
            // reached by hand.
            LookElsewhere(rig, directors);

            int? speed = null;
            read.Apply(rig, directors, s => speed = s);

            Assert.That(directors.Slice.ActiveLayer, Is.EqualTo(Layer), "the slice did not go back");
            Assert.That(directors.Selection.Pawn, Is.EqualTo(pawn), "the colonist was not reselected");
            Assert.That(speed.HasValue, Is.True, "nothing was asked to set the game speed");
            Assert.That(speed!.Value, Is.EqualTo(Speed), "the wrong speed was restored");

            // On the rig itself, bit for bit. Not on a recorded pose: what is being asserted is
            // that the camera is back, and a delegate that was handed the right numbers would say
            // nothing about whether the rig kept them.
            SameBits(Yaw, rig.yaw, "restored yaw");
            SameBits(Pitch, rig.pitch, "restored pitch");
            SameBits(Distance, rig.distance, "restored distance");
            SameBits(Distance, rig.TargetDistance, "restored zoom target");
            SameBits(written.Camera.Focus.x, rig.Focus.x, "restored focus x");
            SameBits(written.Camera.Focus.y, rig.Focus.y, "restored focus y");
            SameBits(written.Camera.Focus.z, rig.Focus.z, "restored focus z");

            // And it survives the frames that follow, which is the failure mode the rig's own
            // RestorePose exists to prevent: yaw and distance are lerped toward private targets
            // every Update, so a restore that set only the public fields would look right for one
            // frame and then swing back to wherever the camera was already heading.
            yield return null;
            yield return null;
            SameBits(Yaw, rig.yaw, "yaw a frame later");
            SameBits(Distance, rig.TargetDistance, "zoom target a frame later");
            Assert.That(Mathf.Abs(rig.Focus.x - written.Camera.Focus.x), Is.LessThan(0.001f),
                "the focus wandered in the frames after the restore");

            UnityEngine.Object.Destroy(root);
        }

        /// <summary>
        /// The order inside <c>Apply</c>, asserted rather than left as a comment: the layer moves
        /// first, and moving the layer is what clears the selection. Restoring the colonist before
        /// the layer would restore nothing at all, and the failure would look like "the selection
        /// is not saved" rather than like an ordering mistake.
        /// </summary>
        [UnityTest]
        public IEnumerator RestoringTheLayerDoesNotTakeTheSelectionWithIt()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig rig, out _);
            yield return RigWorld.WarmUp();
            yield return RigWorld.SettleCamera(rig);

            HudDirectors directors = boot.Directors!;
            PawnId pawn = FirstColonist(boot);
            PointSomewhereDefinite(rig, directors, pawn);

            var written = new ViewStateSection();
            written.Capture(rig, directors, Speed);
            ViewStateSection read = RoundTrip(boot, written);

            LookElsewhere(rig, directors);
            Assert.That(directors.Slice.ActiveLayer, Is.Not.EqualTo(Layer),
                "the layer has to actually differ or this test proves nothing");

            read.Apply(rig, directors, _ => { });

            Assert.That(directors.Slice.ActiveLayer, Is.EqualTo(Layer));
            Assert.That(directors.Selection.Pawn, Is.EqualTo(pawn),
                "the layer change cleared the selection that was restored before it");

            UnityEngine.Object.Destroy(root);
        }

        /// <summary>
        /// With no way given to set the speed, the restore falls back to asking the rig — which
        /// raises the same event the space bar does, so the composition root's paused-clock
        /// handling stays in one place.
        ///
        /// <para><b>The fallback is documented as second best and this is why.</b> The bootstrap's
        /// own handler treats a request for 0 while already at 0 as "start again", which is right
        /// for a key and wrong for a restore. It is harmless after a load, because a freshly built
        /// world starts at speed 1 — but the caller that has an exact setter should pass one.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator WithNoSpeedSetterTheRestoreAsksTheRig()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig rig, out _);
            yield return RigWorld.WarmUp();
            yield return RigWorld.SettleCamera(rig);

            HudDirectors directors = boot.Directors!;
            PawnId pawn = FirstColonist(boot);
            PointSomewhereDefinite(rig, directors, pawn);

            var written = new ViewStateSection();
            written.Capture(rig, directors, Speed);
            ViewStateSection read = RoundTrip(boot, written);

            int asked = -1;
            Action<int> listener = s => asked = s;
            rig.GameSpeedRequested += listener;
            LookElsewhere(rig, directors);
            read.Apply(rig, directors);
            rig.GameSpeedRequested -= listener;

            Assert.That(asked, Is.EqualTo(Speed), "the rig was not asked for the saved speed");

            // The world is asked through an intent, and intents drain on a tick — but a Unity frame
            // does not always contain one. The composition root accumulates real time and steps the
            // simulation only when it has a tick's worth, so a fixed one-frame wait passes or fails
            // on how long that frame happened to take. **Measured, not supposed:** this test failed
            // once in three runs on exactly that line, expecting 2 and finding 1.
            //
            // Waiting for the effect rather than for a frame is the fix, and it is still a real
            // assertion: a request that never arrives exhausts the budget and fails with the same
            // message it always did.
            for (int frame = 0; frame < 120 && boot.World!.GameSpeed != Speed; frame++)
                yield return null;

            Assert.That(boot.World!.GameSpeed, Is.EqualTo(Speed),
                "the request never reached the simulation");

            UnityEngine.Object.Destroy(root);
        }

        /// <summary>
        /// Every save written before this section existed carries no <c>"view"</c> section at all,
        /// and <c>WorldSave.Load</c> simply never calls <c>Load</c> on it. A section in that state
        /// must do nothing — not restore a default-constructed pose, which would put the camera at
        /// the world origin looking down at layer 0 and read as a far worse bug than the one this
        /// unit fixes.
        /// </summary>
        [UnityTest]
        public IEnumerator ASaveWithNoViewSectionLeavesTheViewExactlyAsItWas()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig rig, out _);
            yield return RigWorld.WarmUp();
            yield return RigWorld.SettleCamera(rig);

            HudDirectors directors = boot.Directors!;
            PawnId pawn = FirstColonist(boot);
            PointSomewhereDefinite(rig, directors, pawn);

            // An old save: the same world, written with no view section in the component list.
            var stream = new MemoryStream();
            WorldSave.Save(boot.World!, stream, Array.Empty<ISaveable>());
            stream.Position = 0;
            var read = new ViewStateSection();
            WorldSave.Load(boot.World!, stream, new ISaveable[] { read });

            Assert.That(read.HasState, Is.False, "a save with no view section produced view state");

            Vector3 focus = rig.Focus;
            float pitch = rig.pitch;
            float yaw = rig.yaw;
            int layer = directors.Slice.ActiveLayer;

            read.Apply(rig, directors,
                _ => Assert.Fail("a section that was never loaded set the game speed"));

            Assert.That(rig.Focus, Is.EqualTo(focus), "a section that was never loaded moved the camera");
            Assert.That(rig.pitch, Is.EqualTo(pitch));
            Assert.That(rig.yaw, Is.EqualTo(yaw));
            Assert.That(directors.Slice.ActiveLayer, Is.EqualTo(layer));
            Assert.That(directors.Selection.Pawn, Is.EqualTo(pawn), "the selection was cleared by a no-op");

            UnityEngine.Object.Destroy(root);
        }

        /// <summary>
        /// The control: the round-trip assertions above are only worth anything if they can fail.
        /// The camera is moved somewhere definite and deliberately <b>not</b> restored, and the
        /// same comparisons are made — they must all disagree.
        ///
        /// <para><b>Confirmed by breaking it, not by reasoning about it.</b> Run once with
        /// <c>read.Apply(rig, directors, _ => { })</c> inserted after <c>LookElsewhere</c> — the
        /// line the comment below says not to add — this failed, and failed on the pitch:
        /// <c>"the camera was moved and never restored, yet the pitch matches the save. Expected:
        /// not equal to 37.3125f. But was: 37.3125f"</c>. PlayMode went 44 passed / 0 failed to
        /// 43 passed / 1 failed, and back again when the line was removed.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheRoundTripAssertionsCanFail()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig rig, out _);
            yield return RigWorld.WarmUp();
            yield return RigWorld.SettleCamera(rig);

            HudDirectors directors = boot.Directors!;
            PawnId pawn = FirstColonist(boot);
            PointSomewhereDefinite(rig, directors, pawn);

            var written = new ViewStateSection();
            written.Capture(rig, directors, Speed);
            ViewStateSection read = RoundTrip(boot, written);

            // Somewhere definite, and no restore. This is the line that gets deleted to check the
            // control; see the summary above.
            LookElsewhere(rig, directors);

            Assert.That(rig.pitch, Is.Not.EqualTo(read.Camera.Pitch),
                "the camera was moved and never restored, yet the pitch matches the save");
            Assert.That(Mathf.Abs(rig.Focus.x - read.Camera.Focus.x), Is.GreaterThan(CellMetrics.SizeXZ),
                "the camera was moved and never restored, yet the focus matches the save");
            Assert.That(directors.Slice.ActiveLayer, Is.Not.EqualTo(read.Layer));
            Assert.That(directors.Selection.Pawn, Is.Not.EqualTo(read.Selected));

            UnityEngine.Object.Destroy(root);
        }

        // ----------------------------------------------------------------- fixture

        static PawnId FirstColonist(OdysseyBootstrap boot)
        {
            Assert.That(boot.World, Is.Not.Null, "the rig built no world");
            Assert.That(boot.Directors, Is.Not.Null, "the rig built no directors");
            WorldSnapshot frame = boot.World!.Views.Current;
            Assert.That(frame.PawnCount, Is.GreaterThan(0), "no colonists to select");
            return frame.Pawns[0].Id;
        }

        /// <summary>
        /// The view the save is supposed to remember. Nothing yields between the moves and the
        /// capture that follows, because <c>yaw</c> and <c>distance</c> are smoothed toward private
        /// targets on the next <c>Update</c> — a frame in between would capture something on its
        /// way somewhere rather than the numbers this test chose.
        /// </summary>
        static void PointSomewhereDefinite(SliceCameraRig rig, HudDirectors directors, PawnId pawn)
        {
            // The layer first: a layer change clears the selection, so choosing the colonist before
            // it would leave nothing selected to capture.
            directors.Slice.SetLayer(Layer);
            rig.FocusOn(Looking, Distance);
            rig.yaw = Yaw;
            rig.pitch = Pitch;
            rig.distance = Distance;
            directors.Selection.Choose(pawn);
        }

        /// <summary>Where a freshly loaded session would be: another layer, another corner of the
        /// board, nothing selected.</summary>
        static void LookElsewhere(SliceCameraRig rig, HudDirectors directors)
        {
            directors.Slice.SetLayer(OtherLayer);
            rig.FocusOn(Elsewhere, 90f);
            rig.pitch = OtherPitch;
            // The yaw is moved here too, and it is the one that matters. The first run of this file
            // left it alone and the fallback test then reported "yaw 123.438, was 123.438: not
            // restored" — which was true and proved nothing, because nothing had moved it. A gap
            // has to be shown from a state it could not have arrived at by itself.
            rig.yaw = OtherYaw;
            directors.Selection.Clear();
        }

        /// <summary>
        /// Through a real stream and the real container, not through a direct field copy: the
        /// header, the section key, the length prefix and the payload all take part, which is what
        /// makes this a test of the save format rather than of the class.
        /// </summary>
        static ViewStateSection RoundTrip(OdysseyBootstrap boot, ViewStateSection written)
        {
            var stream = new MemoryStream();
            WorldSave.Save(boot.World!, stream, new ISaveable[] { written });
            stream.Position = 0;
            var read = new ViewStateSection();
            WorldSave.Load(boot.World!, stream, new ISaveable[] { read });
            return read;
        }

        /// <summary>
        /// Float equality by bit pattern. <c>Is.EqualTo</c> on two floats in this NUnit is exact
        /// anyway, but saying it in bits is what the assertion actually means — and it prints the
        /// two patterns when it fails, which is how a one-ulp drift is recognised as a drift.
        /// </summary>
        static void SameBits(float expected, float actual, string what) =>
            Assert.That(BitConverter.SingleToInt32Bits(actual),
                Is.EqualTo(BitConverter.SingleToInt32Bits(expected)),
                $"{what}: {actual} is not bit-identical to {expected}");
    }
}
