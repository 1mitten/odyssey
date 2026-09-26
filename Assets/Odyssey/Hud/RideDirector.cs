#nullable enable
using System;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// Riding along with one colonist (design 57): whether the view is locked to somebody, who,
    /// how far back the camera stands, and where the player has turned it to look.
    ///
    /// <para><b>A spectator, never a driver</b> (owner, 2026-09-26: "watch only"). Nothing here
    /// submits an intent, so riding cannot change a tick, a save or the hash. The colonist goes on
    /// doing whatever her work list and her needs say; the player watches.</para>
    ///
    /// <para>Engine-free, like every director here: the rig in the Unity assembly reads it and
    /// stands the camera where <see cref="RideCamera"/> says. What changes around a ride — the
    /// slice following her layer, the selection put aside and given back — is
    /// <see cref="HudDirectors"/>'s, because it is the rule between directors.</para>
    /// </summary>
    public sealed class RideDirector
    {
        /// <summary>
        /// The arm lengths the wheel steps through, in metres, nearest first. Nought is her eyes.
        /// Stops rather than a continuous zoom so a notch always lands somewhere that was chosen,
        /// and so the eye view is a place the wheel arrives at rather than a limit it creeps to.
        /// </summary>
        public static readonly float[] Stops = { 0f, 1.6f, 2.4f, 3.2f, 4.2f, 5.4f, 7f };

        /// <summary>Where a ride starts: the fourth stop, 3.2 m behind her shoulder.</summary>
        public const int DefaultStop = 3;

        /// <summary>How long the camera holds on the place she was after she is gone, in seconds (design 57 §5).</summary>
        public const float LostHoldSeconds = 2f;

        /// <summary>How long after the mouse last moved the view starts to settle back behind her.</summary>
        public const float IdleReturnSeconds = 2.5f;

        /// <summary>How quickly it settles, per second: a time constant of about half a second.</summary>
        public const float ReturnRate = 2f;

        /// <summary>The steepest the player may look down and up, in degrees.</summary>
        public const float PitchDown = 70f, PitchUp = -40f;

        /// <summary>How far down the view looks when it settles, over the shoulder and at the eyes.</summary>
        public const float ShoulderPitch = 12f, EyePitch = 4f;

        /// <summary>Whether the view is riding with somebody.</summary>
        public bool Riding { get; private set; }

        /// <summary>Who. Meaningless when <see cref="Riding"/> is false.</summary>
        public PawnId Pawn { get; private set; }

        /// <summary>Which of <see cref="Stops"/> the wheel is on.</summary>
        public int Stop { get; private set; } = DefaultStop;

        /// <summary>The arm the player has asked for, in metres: <see cref="Stops"/> at <see cref="Stop"/>.</summary>
        public float Arm => Stops[Stop];

        /// <summary>Whether the wheel is on the eye stop.</summary>
        public bool AtEyes => Stop == 0;

        /// <summary>Where the player has turned the view away from the way she is facing, in degrees, −180 to 180.</summary>
        public float LookYaw { get; private set; }

        /// <summary>How far down the view looks, in degrees; negative looks up.</summary>
        public float LookPitch { get; private set; } = ShoulderPitch;

        /// <summary>
        /// She has gone from the frame — killed, or carried off it — and the camera is holding on
        /// the last place she stood until <see cref="LostHoldSeconds"/> have passed.
        /// </summary>
        public bool Lost { get; private set; }

        /// <summary>The hold is over and the ride should end. <see cref="HudDirectors.AdvanceRide"/> ends it.</summary>
        public bool Expired => Lost && _lostFor >= LostHoldSeconds;

        /// <summary>
        /// Counts rides begun, so a presenter can tell a new ride with the same colonist from the
        /// one it is already showing.
        /// </summary>
        public int Serial { get; private set; }

        /// <summary>
        /// Her card for the strip along the bottom of the screen (design 57 §6): her name, what
        /// she is doing and her health, read by the same model that fills the inspect pane, so the
        /// two can never word the same colonist differently.
        /// </summary>
        public InspectModel Strip { get; } = new InspectModel();

        /// <summary>Raised when a ride begins or ends, with whether one is now running.</summary>
        public event Action<bool>? Changed;

        float _lostFor;
        float _sinceLook;

        /// <summary>
        /// Start riding with <paramref name="id"/>. Refused, and nothing changes, when she is not in
        /// the frame or is not one of the colony's people: an animal or a bandit has no card to
        /// press the button on, and a caller that asks anyway is answered no.
        /// </summary>
        public bool Begin(PawnId id, WorldSnapshot snapshot)
        {
            if (!snapshot.TryGetPawn(id, out PawnView view) || !view.IsColonist) return false;

            Riding = true;
            Pawn = id;
            Stop = DefaultStop;
            LookYaw = 0f;
            LookPitch = ShoulderPitch;
            Lost = false;
            _lostFor = 0f;
            _sinceLook = 0f;
            Serial++;

            Strip.SetColonist(id);
            Strip.Refresh(snapshot);
            Changed?.Invoke(true);
            return true;
        }

        /// <summary>Stop riding. Nothing if no ride is running.</summary>
        public void End()
        {
            if (!Riding) return;
            Riding = false;
            Lost = false;
            _lostFor = 0f;
            Changed?.Invoke(false);
        }

        /// <summary>
        /// A turn of the wheel: positive is towards her (in), negative away (out), a stop a notch.
        /// Held at the two ends rather than wrapping.
        /// </summary>
        public void Wheel(int notches)
        {
            if (!Riding || notches == 0) return;
            bool wasAtEyes = AtEyes;
            Stop = Math.Max(0, Math.Min(Stops.Length - 1, Stop - notches));
            // Arriving at the eyes, the view levels; leaving them, it drops to the shoulder's
            // angle. The player's own pitch is otherwise theirs.
            if (AtEyes != wasAtEyes) LookPitch = DefaultPitch;
        }

        /// <summary>
        /// The mouse moved: turn the view by this many degrees, right and down positive. The view
        /// goes on settling back behind her <see cref="IdleReturnSeconds"/> after the last call.
        /// </summary>
        public void Look(float yawDegrees, float pitchDegrees)
        {
            if (!Riding) return;
            LookYaw = Wrap(LookYaw + yawDegrees);
            LookPitch = Math.Max(PitchUp, Math.Min(PitchDown, LookPitch + pitchDegrees));
            _sinceLook = 0f;
        }

        /// <summary>The pitch the view settles to at the current stop.</summary>
        public float DefaultPitch => AtEyes ? EyePitch : ShoulderPitch;

        /// <summary>
        /// Refresh her card on the strip. On the interface's own cadence rather than every frame,
        /// as the inspect pane is: it is text, and text a frame late is not observable.
        /// </summary>
        public void RefreshStrip(WorldSnapshot snapshot)
        {
            if (Riding) Strip.Refresh(snapshot);
        }

        /// <summary>
        /// Once a frame, in real seconds (a paused game still turns its camera): notice her going,
        /// and let an idle view settle.
        /// </summary>
        public void Advance(WorldSnapshot snapshot, float realSeconds)
        {
            if (!Riding) return;
            float dt = Math.Max(0f, realSeconds);

            // Still one of ours, not merely still on the board (design 59 §16 H8): a ride starts only
            // on a colonist, and an arrest or a break-out left the camera on a prisoner. She is lost
            // to the ride as she would be to the board, and it ends the same way.
            if (snapshot.TryGetPawn(Pawn, out PawnView her) && her.IsColonist)
            {
                Lost = false;
                _lostFor = 0f;
            }
            else
            {
                Lost = true;
                _lostFor += dt;
            }

            _sinceLook += dt;
            if (_sinceLook >= IdleReturnSeconds)
            {
                float keep = (float)Math.Exp(-ReturnRate * dt);
                LookYaw *= keep;
                LookPitch = DefaultPitch + (LookPitch - DefaultPitch) * keep;
            }
        }

        static float Wrap(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f) degrees -= 360f;
            else if (degrees < -180f) degrees += 360f;
            return degrees;
        }
    }
}
