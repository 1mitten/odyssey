#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// Riding along with a colonist (design 57): who can be ridden with, what a ride puts aside
    /// and gives back, how it follows her and how it ends. The owner, 2026-09-26: <i>"This will lock
    /// the user into a fps mode until they push esc."</i>
    /// </summary>
    public class RideTests
    {
        static readonly PawnId Ada = new PawnId(1), Raider = new PawnId(10), Hog = new PawnId(11);

        /// <summary>Ada, a colonist on layer <paramref name="adaLayer"/>; a bandit and a hog beside her.</summary>
        static WorldSnapshot Frame(int adaLayer = 1, bool withAda = true)
        {
            WorldSnapshot frame = Odyssey.Tests.Hud.Frame.Write();
            if (withAda)
                frame.AddPawn(new PawnView(Ada, new CellRef(1, 1, adaLayer), 800, 800, 700, JobHandle.Wait,
                    flags: PawnFlags.Person));
            frame.AddPawn(new PawnView(Raider, new CellRef(5, 1, 1), 800, 800, 700, JobHandle.Wait, kind: 3,
                flags: PawnFlags.Person | PawnFlags.Hostile));
            frame.AddPawn(new PawnView(Hog, new CellRef(6, 1, 1), 800, 800, 700, JobHandle.Wander, kind: 1,
                flags: PawnFlags.None));
            return frame;
        }

        // ------------------------------------------------------------------ the button

        /// <summary>It is on her card, live, after Draft and the response, under the registry's name.</summary>
        [Test]
        public void TheCardOffersRideAlongAfterTheResponse()
        {
            var pane = new InspectModel();
            pane.SetColonist(Ada);
            pane.Refresh(Frame());

            int ride = pane.Commands.FindIndex(c => c.IconKey == InspectModel.RideKey);
            int respond = pane.Commands.FindIndex(c => ResponseModel.IsResponseKey(c.IconKey));
            Assert.That(ride, Is.EqualTo(respond + 1), "not after the response");
            Assert.That(pane.Commands[ride].Enabled, Is.True);
            Assert.That(pane.Commands[ride].Label, Is.EqualTo("First Person"));
        }

        /// <summary>A colonist who has gone cannot be ridden with; the control is the live pane above.</summary>
        [Test]
        public void ItIsOffForAColonistWhoHasGone()
        {
            var pane = new InspectModel();
            pane.SetColonist(Ada);
            pane.Refresh(Frame());
            pane.Refresh(Frame(withAda: false));
            Assert.That(pane.Tombstoned, Is.True);
            Assert.That(pane.Commands.Find(c => c.IconKey == InspectModel.RideKey).Enabled, Is.False);
        }

        // ------------------------------------------------------------------ beginning

        /// <summary>Only the colony's people: a bandit or an animal is refused and nothing changes.</summary>
        [Test]
        public void OnlyAColonistCanBeRiddenWith()
        {
            var directors = new HudDirectors(4, 1);
            Assert.That(directors.BeginRide(Raider, Frame()), Is.False, "a bandit");
            Assert.That(directors.BeginRide(Hog, Frame()), Is.False, "an animal");
            Assert.That(directors.BeginRide(new PawnId(99), Frame()), Is.False, "nobody");
            Assert.That(directors.Ride.Riding, Is.False);
            Assert.That(directors.Hotkeys.GameKeysLive, Is.True, "a refused ride took the keys");

            Assert.That(directors.BeginRide(Ada, Frame()), Is.True, "the control: a colonist");
            Assert.That(directors.Ride.Riding, Is.True);
            Assert.That(directors.Ride.Pawn, Is.EqualTo(Ada));
        }

        /// <summary>
        /// A ride puts aside what would stand in the shot: the selection, an armed tool, the game's
        /// keys — and the slice goes to her layer.
        /// </summary>
        [Test]
        public void ARidePutsAsideTheSelectionTheToolAndTheKeysAndTakesHerLayer()
        {
            var directors = new HudDirectors(4, 1);
            WorldSnapshot frame = Frame(adaLayer: 2);
            directors.Selection.Choose(Ada);
            directors.Designate.Tool = DesignateTool.Mine;

            Assert.That(directors.BeginRide(Ada, frame), Is.True);

            Assert.That(directors.Selection.HasPawn, Is.False, "her outline would be drawn over the shot");
            Assert.That(directors.Designate.Tool, Is.EqualTo(DesignateTool.None));
            Assert.That(directors.Hotkeys.GameKeysLive, Is.False, "a tool key would act on a board nobody can see");
            Assert.That(directors.Slice.ActiveLayer, Is.EqualTo(2));
        }

        /// <summary>A second ride cannot be begun over the first.</summary>
        [Test]
        public void ARideCannotBeBegunOverAnother()
        {
            var directors = new HudDirectors(4, 1);
            directors.BeginRide(Ada, Frame());
            Assert.That(directors.BeginRide(Ada, Frame()), Is.False);
        }

        // ------------------------------------------------------------------ riding

        /// <summary>Up a ladder and down a shaft: the slice follows her layer.</summary>
        [Test]
        public void TheSliceFollowsHerLayer()
        {
            var directors = new HudDirectors(4, 1);
            directors.BeginRide(Ada, Frame(adaLayer: 1));
            directors.AdvanceRide(Frame(adaLayer: 3), 0.016f);
            Assert.That(directors.Slice.ActiveLayer, Is.EqualTo(3));
        }

        /// <summary>
        /// She goes from the frame: the camera holds on where she was for two seconds, then the ride
        /// ends by itself. The control is the frame with her in it, which never ends it.
        /// </summary>
        [Test]
        public void TheRideEndsTwoSecondsAfterSheHasGone()
        {
            var directors = new HudDirectors(4, 1);
            directors.BeginRide(Ada, Frame());

            for (int i = 0; i < 300; i++) directors.AdvanceRide(Frame(), 0.1f);
            Assert.That(directors.Ride.Riding, Is.True, "the control: thirty seconds with her still there");

            directors.AdvanceRide(Frame(withAda: false), 1.5f);
            Assert.That(directors.Ride.Riding, Is.True, "ended before the hold was over");
            Assert.That(directors.Ride.Lost, Is.True);

            directors.AdvanceRide(Frame(withAda: false), 0.6f);
            Assert.That(directors.Ride.Riding, Is.False);
            Assert.That(directors.Hotkeys.GameKeysLive, Is.True, "the keys were not given back");
        }

        /// <summary>Back in the frame inside the hold (a dropped frame, a load): the hold is forgotten.</summary>
        [Test]
        public void ComingBackInsideTheHoldKeepsTheRide()
        {
            var directors = new HudDirectors(4, 1);
            directors.BeginRide(Ada, Frame());
            directors.AdvanceRide(Frame(withAda: false), 1.5f);
            directors.AdvanceRide(Frame(), 0.1f);
            directors.AdvanceRide(Frame(withAda: false), 1.5f);
            Assert.That(directors.Ride.Riding, Is.True);
        }

        // ------------------------------------------------------------------ leaving

        /// <summary>Leaving gives back the keys and the layer, and she is the selection again.</summary>
        [Test]
        public void LeavingGivesBackTheLayerTheKeysAndHerAsTheSelection()
        {
            var directors = new HudDirectors(4, 1);
            directors.BeginRide(Ada, Frame(adaLayer: 3));
            Assert.That(directors.Slice.ActiveLayer, Is.EqualTo(3));

            directors.EndRide(Frame(adaLayer: 3));

            Assert.That(directors.Ride.Riding, Is.False);
            Assert.That(directors.Slice.ActiveLayer, Is.EqualTo(1), "the layer the ride began on");
            Assert.That(directors.Hotkeys.GameKeysLive, Is.True);
            Assert.That(directors.Selection.Pawns, Is.EqualTo(new[] { Ada }));
        }

        /// <summary>
        /// The keys live in preferences that outlive a session, so a colony left mid-ride must not
        /// hand the next one a dead keyboard.
        /// </summary>
        [Test]
        public void ANewSessionStartsWithTheKeysLive()
        {
            var settings = new SettingsDirector();
            var hotkeys = new HotkeyDirector();
            var first = new HudDirectors(4, 1, settings, hotkeys);
            first.BeginRide(Ada, Frame());
            Assert.That(hotkeys.GameKeysLive, Is.False, "the control: the ride held them");

            _ = new HudDirectors(4, 1, settings, hotkeys);
            Assert.That(hotkeys.GameKeysLive, Is.True);
        }

        /// <summary>
        /// The wake into a world holds the keys across the build of the new session, and that build
        /// constructs a new <see cref="HudDirectors"/>, which gives back a ride's hold. The two holds
        /// are separate flags, or every wake would hand the player live keys under its curtain.
        /// </summary>
        [Test]
        public void ANewSessionDoesNotReleaseTheWakesHold()
        {
            var settings = new SettingsDirector();
            var hotkeys = new HotkeyDirector { Suspended = true };
            Assert.That(hotkeys.GameKeysLive, Is.False, "the control: the wake holds them");

            _ = new HudDirectors(4, 1, settings, hotkeys);
            Assert.That(hotkeys.GameKeysLive, Is.False, "building the session released the wake's hold");
            Assert.That(hotkeys.Suspended, Is.True);
        }

        /// <summary>A ride's hold and the wake's are separate: ending one leaves the other alone.</summary>
        [Test]
        public void EndingARideLeavesTheWakesHoldAlone()
        {
            var settings = new SettingsDirector();
            var hotkeys = new HotkeyDirector();
            var directors = new HudDirectors(4, 1, settings, hotkeys);
            Assert.That(directors.BeginRide(Ada, Frame()), Is.True);
            hotkeys.Suspended = true;
            directors.EndRide(Frame());
            Assert.That(hotkeys.GameKeysLive, Is.False, "leaving the ride released a hold it did not take");
            hotkeys.Suspended = false;
            Assert.That(hotkeys.GameKeysLive, Is.True);
        }

        /// <summary>Escape means leaving the ride and nothing else while one runs, whatever else is open.</summary>
        [Test]
        public void EscapeLeavesTheRideBeforeAnythingElse()
        {
            var settings = new SettingsDirector();
            Assert.That(settings.Escape(true, true, true, true, true, true, true, true, true, true, true, null),
                Is.EqualTo(EscapeAction.LeaveRide));
            Assert.That(settings.Escape(false, false, false, false, false, false, false, false, false, false, false, null),
                Is.EqualTo(EscapeAction.OpenPanel), "the control: no ride, nothing open");
            Assert.That(settings.Escape(false, true, false, false, false, false, false, false, false, false, false, null),
                Is.EqualTo(EscapeAction.CloseContextMenu), "the rest of the ladder is unchanged");
        }

        // ------------------------------------------------------------------ the wheel and the mouse

        /// <summary>It starts over her shoulder; the wheel steps to her eyes and back, held at both ends.</summary>
        [Test]
        public void TheWheelStepsFromTheShoulderToTheEyesAndHoldsAtTheEnds()
        {
            var ride = new RideDirector();
            ride.Begin(Ada, Frame());
            Assert.That(ride.Arm, Is.EqualTo(3.2f));
            Assert.That(ride.AtEyes, Is.False);

            ride.Wheel(RideDirector.DefaultStop);
            Assert.That(ride.AtEyes, Is.True);
            Assert.That(ride.Arm, Is.Zero);
            ride.Wheel(5);
            Assert.That(ride.Arm, Is.Zero, "wheeled past the eyes");

            ride.Wheel(-100);
            Assert.That(ride.Arm, Is.EqualTo(RideDirector.Stops[RideDirector.Stops.Length - 1]));
        }

        /// <summary>Arriving at the eyes levels the view; leaving them drops it to the shoulder's angle.</summary>
        [Test]
        public void TheEyesLevelTheView()
        {
            var ride = new RideDirector();
            ride.Begin(Ada, Frame());
            Assert.That(ride.LookPitch, Is.EqualTo(RideDirector.ShoulderPitch));
            ride.Wheel(RideDirector.DefaultStop);
            Assert.That(ride.LookPitch, Is.EqualTo(RideDirector.EyePitch));
            ride.Wheel(-1);
            Assert.That(ride.LookPitch, Is.EqualTo(RideDirector.ShoulderPitch));
        }

        /// <summary>
        /// The mouse turns the view; left alone, it settles back behind her. It waits
        /// <see cref="RideDirector.IdleReturnSeconds"/> first, so a player looking round is not
        /// pulled back mid-look.
        /// </summary>
        [Test]
        public void AnIdleViewSettlesBackBehindHer()
        {
            var ride = new RideDirector();
            ride.Begin(Ada, Frame());
            ride.Look(90f, 20f);
            Assert.That(ride.LookYaw, Is.EqualTo(90f));

            ride.Advance(Frame(), RideDirector.IdleReturnSeconds * 0.9f);
            Assert.That(ride.LookYaw, Is.EqualTo(90f), "pulled back while the player was still looking");

            for (int i = 0; i < 200; i++) ride.Advance(Frame(), 0.05f);
            Assert.That(ride.LookYaw, Is.EqualTo(0f).Within(0.5f));
            Assert.That(ride.LookPitch, Is.EqualTo(RideDirector.ShoulderPitch).Within(0.5f));
        }

        /// <summary>The look wraps round rather than winding up, and the pitch stops short of straight up and down.</summary>
        [Test]
        public void TheLookWrapsAndThePitchIsHeld()
        {
            var ride = new RideDirector();
            ride.Begin(Ada, Frame());
            ride.Look(200f, 500f);
            Assert.That(ride.LookYaw, Is.EqualTo(-160f).Within(1e-3f));
            Assert.That(ride.LookPitch, Is.EqualTo(RideDirector.PitchDown));
            ride.Look(0f, -1000f);
            Assert.That(ride.LookPitch, Is.EqualTo(RideDirector.PitchUp));
        }

        /// <summary>Her card on the strip reads her the way the pane does.</summary>
        [Test]
        public void TheStripCarriesHerCard()
        {
            var pane = new InspectModel();
            pane.SetColonist(Ada);
            pane.Refresh(Frame());

            var ride = new RideDirector();
            ride.Begin(Ada, Frame());
            Assert.That(ride.Strip.Title, Is.EqualTo(pane.Title));
            Assert.That(ride.Strip.Job, Is.EqualTo(pane.Job));
        }
    }

    /// <summary>The chase camera's geometry (design 57 §3), without a camera.</summary>
    public class RideCameraTests
    {
        const float Eps = 1e-3f;

        /// <summary>Looking along +z, level, in open air: the arm comes out whole, behind her and over her right shoulder.</summary>
        [Test]
        public void InTheOpenTheCameraStandsBehindHerRightShoulder()
        {
            var frame = new RideFrame(10f, 0f, 10f, eyeLift: 2f, yaw: 0f, pitch: 0f, arm: 3f);
            RidePose pose = RideCamera.Solve(frame, null);

            Assert.That(pose.Arm, Is.EqualTo(3f).Within(Eps));
            Assert.That(pose.Shoulder, Is.EqualTo(1f));
            Assert.That(pose.X, Is.EqualTo(10f + RideCamera.ShoulderRight).Within(Eps), "not over the right shoulder");
            Assert.That(pose.Y, Is.EqualTo(2f + RideCamera.ShoulderAboveEyes).Within(Eps));
            Assert.That(pose.Z, Is.EqualTo(10f - 3f).Within(Eps), "not behind her");
        }

        /// <summary>Turned to +x (yaw 90), behind her is −x and her right is −z.</summary>
        [Test]
        public void TheShoulderTurnsWithTheView()
        {
            var frame = new RideFrame(0f, 0f, 0f, 2f, yaw: 90f, pitch: 0f, arm: 3f);
            RidePose pose = RideCamera.Solve(frame, null);
            Assert.That(pose.X, Is.EqualTo(-3f).Within(Eps));
            Assert.That(pose.Z, Is.EqualTo(-RideCamera.ShoulderRight).Within(Eps));
        }

        /// <summary>Looking down, the camera rises behind her: a positive pitch is Unity's "down".</summary>
        [Test]
        public void LookingDownRaisesTheCamera()
        {
            RidePose level = RideCamera.Solve(new RideFrame(0f, 0f, 0f, 2f, 0f, 0f, 3f), null);
            RidePose down = RideCamera.Solve(new RideFrame(0f, 0f, 0f, 2f, 0f, 30f, 3f), null);
            Assert.That(down.Y, Is.GreaterThan(level.Y + 1f));
        }

        /// <summary>At the eyes: no shoulder, no arm, a hand's width in front of the face.</summary>
        [Test]
        public void TheEyeStopIsJustInFrontOfTheFace()
        {
            RidePose pose = RideCamera.Solve(new RideFrame(4f, 1f, 4f, 2f, 0f, 0f, 0f), null);
            Assert.That(pose.Shoulder, Is.Zero);
            Assert.That(pose.Arm, Is.Zero);
            Assert.That(pose.X, Is.EqualTo(4f).Within(Eps));
            Assert.That(pose.Y, Is.EqualTo(3f).Within(Eps));
            Assert.That(pose.Z, Is.EqualTo(4f + RideCamera.EyeForward).Within(Eps));
            Assert.That(pose.FromEyes, Is.Zero.Within(Eps));
        }

        /// <summary>
        /// Walls close behind her and at her shoulder squeeze the camera up against her head, and
        /// <see cref="RidePose.FromEyes"/> says so; the control is the open arm, well clear of it.
        /// </summary>
        [Test]
        public void ACameraSqueezedAgainstHerSaysHowClose()
        {
            var frame = new RideFrame(0f, 0f, 0f, 2f, 0f, 0f, 3f);
            RidePose open = RideCamera.Solve(frame, null);
            Assert.That(open.FromEyes, Is.GreaterThan(3f), "the control: the whole arm");

            RidePose squeezed = RideCamera.Solve(frame, (x, y, z) => z < -0.2f || x > 0.25f);
            Assert.That(squeezed.FromEyes, Is.LessThan(0.6f));
        }

        /// <summary>
        /// A wall a metre and a half behind her pulls the camera in short of it by the clearance;
        /// the control is the same arm with the wall further off than the arm, which leaves it whole.
        /// </summary>
        [Test]
        public void AWallBehindHerPullsTheCameraIn()
        {
            var frame = new RideFrame(0f, 0f, 0f, 2f, 0f, 0f, 3f);
            RidePose near = RideCamera.Solve(frame, (x, y, z) => z < -1.5f);
            Assert.That(near.Arm, Is.LessThanOrEqualTo(1.5f - RideCamera.Clearance + RideCamera.Step + Eps));
            Assert.That(near.Arm, Is.GreaterThan(1f));
            Assert.That(near.Z, Is.GreaterThan(-1.5f), "the camera is in the wall");

            RidePose far = RideCamera.Solve(frame, (x, y, z) => z < -5f);
            Assert.That(far.Arm, Is.EqualTo(3f).Within(Eps), "the control: a wall beyond the arm");
        }

        /// <summary>A wall at her right shoulder shortens the step out to it rather than swallowing the pivot.</summary>
        [Test]
        public void AWallAtHerShoulderKeepsThePivotOutOfIt()
        {
            var frame = new RideFrame(0f, 0f, 0f, 2f, 0f, 0f, 3f);
            RidePose pose = RideCamera.Solve(frame, (x, y, z) => x > 0.4f);
            Assert.That(pose.X, Is.LessThan(0.4f));
        }

        /// <summary>Solid ground everywhere under her feet does not bury the camera when it looks level.</summary>
        [Test]
        public void TheGroundDoesNotShortenALevelArm()
        {
            var frame = new RideFrame(0f, 0f, 0f, 2f, 0f, 0f, 3f);
            RidePose pose = RideCamera.Solve(frame, (x, y, z) => y < 0f);
            Assert.That(pose.Arm, Is.EqualTo(3f).Within(Eps));
        }
    }
}
