#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// Something a figure can take hold of: a line in the world with a thickness.
    ///
    /// <para>Deliberately not "an axe". A haft, a ladder rung, a handrail, the shaft of a
    /// stretcher, the lip of a crate and a rope are all the same thing to a hand — a run of
    /// material a palm can close round — and describing it that way is what lets one solver serve
    /// all of them. The only facts a hand needs are where the run is, which way it goes, how far
    /// along it to take hold, and how thick it is.</para>
    ///
    /// <para><see cref="Thickness"/> is why a grip looks gripped rather than magnetised. A palm
    /// does not sit on the centre line of a haft; it sits on the *surface*, half a thickness out,
    /// with the fingers coming round the far side. Seat two palms on the centre line and the wood
    /// is inside both hands.</para>
    /// </summary>
    public readonly struct Hold
    {
        /// <summary>One end of the run.</summary>
        public readonly Vector3 From;

        /// <summary>The other end.</summary>
        public readonly Vector3 To;

        /// <summary>How thick the thing is, in metres. A palm sits half of this off the line.</summary>
        public readonly float Thickness;

        public Hold(Vector3 from, Vector3 to, float thickness)
        {
            From = from;
            To = to;
            Thickness = thickness;
        }

        /// <summary>The way the run goes, normalised. Zero-length holds report "up" rather than NaN.</summary>
        public Vector3 Axis
        {
            get
            {
                Vector3 along = To - From;
                return along.sqrMagnitude > 1e-10f ? along.normalized : Vector3.up;
            }
        }

        public float Length => Vector3.Distance(From, To);

        /// <summary>The point a fraction of the way along, 0 at <see cref="From"/>.</summary>
        public Vector3 At(float fraction) => Vector3.Lerp(From, To, Mathf.Clamp01(fraction));

        /// <summary>How far a point is from the run's centre line.</summary>
        public float DistanceFrom(Vector3 point)
        {
            Vector3 axis = Axis;
            Vector3 offset = point - From;
            return (offset - axis * Vector3.Dot(offset, axis)).magnitude;
        }

        /// <summary>A haft, a rung or a bar between two points. Thickness is the wood's own.</summary>
        public static Hold Bar(Vector3 from, Vector3 to, float thickness = 0.045f) =>
            new Hold(from, to, thickness);
    }

    /// <summary>
    /// One arm, as much of it as taking hold of something needs: two bones to solve, a wrist to
    /// land, and the fingers to close.
    ///
    /// <para>A value rather than an interface because it is assembled once per figure at bind time
    /// and then handed about. Every field may be absent — a rig that stops at the wrist, or a clone
    /// with no character art at all — and the solver does nothing rather than throwing.</para>
    /// </summary>
    public readonly struct GripArm
    {
        public readonly Transform? Upper;
        public readonly Transform? Lower;
        public readonly HandGrip.Bones Fingers;

        /// <summary>
        /// Where the elbow is sent, as an offset from the shoulder in the figure's own frame:
        /// out to the side, and up or down.
        ///
        /// <para>It settles the one thing the law of cosines leaves open — the whole arm can spin
        /// about the line from shoulder to grip — and it is also what decides which of two arms on
        /// one haft passes over the other. Not a detail: sent the wrong way an elbow goes through
        /// the ribs, and two elbows sent the same way put both forearms in the same place.</para>
        /// </summary>
        public readonly Vector3 ElbowOffset;

        public GripArm(Transform? upper, Transform? lower, in HandGrip.Bones fingers, Vector3 elbowOffset)
        {
            Upper = upper;
            Lower = lower;
            Fingers = fingers;
            ElbowOffset = elbowOffset;
        }

        public Transform? Hand => Fingers.Hand;

        public bool Usable => Upper != null && Lower != null && Fingers.Hand != null;
    }

    /// <summary>
    /// Put hands on things.
    ///
    /// <para><b>Why this is its own file and not more lines inside the swing.</b> Taking hold of
    /// something came into this project as part of an axe — the off hand reaching for a haft held
    /// in the other fist — and everything about it was written where the axe is posed. Then a
    /// hammer wanted it, then a pick, and a ladder rung, a rifle fore-end, a crate carried between
    /// two hands and the other end of a stretcher all want exactly the same thing and none of them
    /// is a swing. So it is a hold and an arm and a solver, and the swing is one caller.</para>
    ///
    /// <para><b>It iterates, and that is the whole reason it works.</b> Three things have to be
    /// true at once: the palm is on the wood, the palm is turned towards it, and the fingers are
    /// closed round it. Each of them moves the other two — turning a hand swings the palm to the
    /// far side of the wrist, and the wrist is what the inverse-kinematics solve actually places.
    /// Done in one pass, in any order, the palm finishes a hand's breadth out; measured on the
    /// felling swing it finished between 0.13 and 0.30 m from the haft. Repeating the pass closes
    /// it, because each round starts from a better guess than the last.</para>
    ///
    /// <para>Nothing here knows what it is holding, and nothing here moves the thing being held. A
    /// caller whose tool hangs off the hand it is posing has to put the tool back afterwards; see
    /// <c>PawnFigureDirector</c>, which does exactly that so a settled blade does not move because
    /// a wrist did.</para>
    /// </summary>
    public static class Grasp
    {
        /// <summary>
        /// How many times to go round. Two is visibly better than one and three is not visibly
        /// better than two, at a cost of a dozen multiplications an arm.
        /// </summary>
        public const int Passes = 3;

        /// <summary>
        /// Take hold with one hand, at a fraction along the hold.
        ///
        /// <paramref name="amount"/> eases the whole thing in, so a colonist closes on a haft as it
        /// raises the tool rather than snapping shut on one frame.
        ///
        /// <paramref name="facing"/> is a point the *back* of the hand is turned away from — for a
        /// two-handed grip, the other hand's shoulder, so the palms face each other round the wood.
        /// Pass the hold's own point to leave the roll to the search alone.
        ///
        /// <returns>How far the palm finished from the hold's centre line, in metres. A number
        /// worth reading: it is the one statement about a grip that a photograph cannot settle.</returns>
        /// </summary>
        public static float One(in GripArm arm, in Hold hold, float at, Vector3 figureRight,
            Vector3 figureUp, float amount)
        {
            if (!arm.Usable || amount <= 0.001f) return float.NaN;

            Transform hand = arm.Hand!;
            Vector3 axis = hold.Axis;
            Vector3 point = hold.At(at);

            for (int pass = 0; pass < Passes; pass++)
            {
                // Where the palm is relative to the wrist, as things stand. This is the quantity
                // that makes a single pass insufficient: it swings right round when the hand rolls.
                Vector3 palmOffset = HandGrip.Palm(arm.Fingers) - hand.position;

                // The seat is on the surface, not the centre line — half a thickness out, on the
                // side the palm is coming from. A palm on the centre line has the wood inside it.
                Vector3 outward = hand.position - point;
                outward -= axis * Vector3.Dot(outward, axis);
                Vector3 seat = outward.sqrMagnitude > 1e-8f
                    ? point + outward.normalized * (hold.Thickness * 0.5f)
                    : point;

                Vector3 shoulder = arm.Upper!.position;
                Vector3 pole = Pole(shoulder, seat, arm.ElbowOffset, figureRight, figureUp);

                TwoBoneIk.Reach(arm.Upper, arm.Lower, hand, seat - palmOffset, pole);
                HandGrip.FaceHaft(arm.Fingers, point, axis, amount);
            }

            HandGrip.Close(arm.Fingers, amount);
            return hold.DistanceFrom(HandGrip.Palm(arm.Fingers));
        }

        /// <summary>
        /// Both hands on one hold, spaced along it: the two-handed tool, a ladder rung taken with
        /// both hands, a bar, the shaft of a barrow.
        ///
        /// <para>The spacing is not decoration. Two fists closer together than a hand is wide
        /// occupy the same space and read as one arm passing through the other, which is what the
        /// felling axe did until 2026-09-16 with its hands 0.08 m apart on a 0.74 m haft.</para>
        /// </summary>
        public static void Both(in GripArm lead, in GripArm trail, in Hold hold,
            float leadAt, float trailAt, Vector3 figureRight, Vector3 figureUp, float amount)
        {
            One(lead, hold, leadAt, figureRight, figureUp, amount);
            One(trail, hold, trailAt, figureRight, figureUp, amount);
        }

        /// <summary>
        /// Hands on opposite sides of something, palms facing each other: a crate, a cut block, a
        /// body being carried, anything too thick to get a hand round.
        ///
        /// <para>The hold handed to each arm is a short run *across* the object at that face, so
        /// each palm seats flat on its own side and the fingers curl over the edge. It is the same
        /// solver: the difference between gripping a haft and gripping a box is entirely in where
        /// the two holds are put.</para>
        ///
        /// <para>Nothing in the game carries a crate yet. This exists because the owner asked for
        /// the helper to be the general thing rather than the axe's thing (2026-09-16), and because
        /// a vocabulary justified by one caller is not a vocabulary.</para>
        /// </summary>
        public static void Opposed(in GripArm left, in GripArm right, Vector3 centre, Vector3 across,
            Vector3 along, float width, float depth, Vector3 figureRight, Vector3 figureUp, float amount)
        {
            if (across.sqrMagnitude < 1e-8f) return;
            Vector3 side = across.normalized * (width * 0.5f);
            Vector3 run = along.sqrMagnitude > 1e-8f ? along.normalized * (depth * 0.5f) : Vector3.zero;

            var leftFace = new Hold(centre - side - run, centre - side + run, 0.0f);
            var rightFace = new Hold(centre + side - run, centre + side + run, 0.0f);

            One(left, leftFace, 0.5f, figureRight, figureUp, amount);
            One(right, rightFace, 0.5f, figureRight, figureUp, amount);
        }

        /// <summary>
        /// Where to send the elbow: out and up from the shoulder, square to the arm.
        ///
        /// <para>Square to the arm rather than fixed beside the body, and that is what holds a grip
        /// together through a whole swing. A pole at a fixed place works while the hold is out in
        /// front and stops working the moment it goes overhead — the shoulder-to-grip line swings
        /// past the pole and the elbow rolls under with it.</para>
        /// </summary>
        static Vector3 Pole(Vector3 shoulder, Vector3 grip, Vector3 offset,
            Vector3 figureRight, Vector3 figureUp)
        {
            Vector3 along = grip - shoulder;
            Vector3 up = figureUp - along * (Vector3.Dot(figureUp, along)
                                             / Mathf.Max(along.sqrMagnitude, 1e-6f));
            if (up.sqrMagnitude < 1e-6f) up = figureUp;

            return shoulder + up.normalized * offset.y + figureRight * offset.x;
        }
    }
}
