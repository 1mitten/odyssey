#nullable enable
using System;

namespace Odyssey.Hud
{
    /// <summary>What a colonist's face is holding (design 59 §3). Drawn only; never saved.</summary>
    public enum FaceExpression : byte
    {
        Neutral,
        Raised,
        Alarmed,
        Stern,
        Sceptical,
        Tired,
    }

    /// <summary>A colonist's part in a conversation this frame (design 59 §5).</summary>
    public enum TalkRole : byte
    {
        None,
        Speaking,
        Listening,
    }

    /// <summary>
    /// One pose of the two facial bones every colonist rig carries — one <c>Eyebrows</c>, one
    /// <c>Eyes</c>, each moving both of its pair (<c>docs/research/e-15</c>).
    ///
    /// <para><b>The only owner of the expression numbers</b>: the game and <c>FaceSheet</c> both read
    /// <see cref="Of"/>, so a photograph of an expression is the expression the game draws.</para>
    /// </summary>
    public readonly struct FacePose : IEquatable<FacePose>
    {
        public FacePose(float browLift, float browRoll, float eyeOpen, float eyeSize)
        {
            BrowLift = browLift;
            BrowRoll = browRoll;
            EyeOpen = eyeOpen;
            EyeSize = eyeSize;
        }

        /// <summary>Metres up the head, of the body as authored — before the figure's 1.4 scale.</summary>
        public readonly float BrowLift;

        /// <summary>Degrees about the face's forward axis: the pair tilts, one brow up and one down.</summary>
        public readonly float BrowRoll;

        /// <summary>The eyes' height, 1 as painted, along whichever of the bone's axes points up the head.</summary>
        public readonly float EyeOpen;

        /// <summary>The eyes' size in every direction, 1 as painted.</summary>
        public readonly float EyeSize;

        public static readonly FacePose Rest = new FacePose(0f, 0f, 1f, 1f);

        /// <summary>
        /// The six expressions, at the magnitudes <c>FaceSheet</c> photographed on 2026-09-26: all
        /// six read in the 128 px portrait and at the play camera's closest zoom (e-15). Larger, and
        /// the male brow band climbs into the hairline.
        /// </summary>
        public static FacePose Of(FaceExpression expression) => expression switch
        {
            FaceExpression.Raised => new FacePose(0.015f, 0f, 1f, 1f),
            FaceExpression.Alarmed => new FacePose(0.020f, 0f, 1.2f, 1.2f),
            FaceExpression.Stern => new FacePose(-0.008f, 0f, 0.6f, 1f),
            FaceExpression.Sceptical => new FacePose(0.004f, 12f, 1f, 1f),
            FaceExpression.Tired => new FacePose(-0.004f, 0f, 0.45f, 1f),
            _ => Rest,
        };

        public static FacePose Lerp(in FacePose a, in FacePose b, float t) => new FacePose(
            a.BrowLift + (b.BrowLift - a.BrowLift) * t,
            a.BrowRoll + (b.BrowRoll - a.BrowRoll) * t,
            a.EyeOpen + (b.EyeOpen - a.EyeOpen) * t,
            a.EyeSize + (b.EyeSize - a.EyeSize) * t);

        public bool Equals(FacePose other) =>
            BrowLift.Equals(other.BrowLift) && BrowRoll.Equals(other.BrowRoll) &&
            EyeOpen.Equals(other.EyeOpen) && EyeSize.Equals(other.EyeSize);

        public override bool Equals(object? obj) => obj is FacePose other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(BrowLift, BrowRoll, EyeOpen, EyeSize);

        public override string ToString() =>
            $"brows {BrowLift * 1000f:F1} mm {BrowRoll:F1} deg, eyes {EyeOpen:F2} x {EyeSize:F2}";
    }

    /// <summary>
    /// One face's motion (design 59): the expression eased in, the blink, and the head a talker
    /// nods and sways. Engine-free and allocation-free; the figure director steps one per live
    /// figure and writes <see cref="Pose"/>, <see cref="NodPitch"/> and <see cref="NodRoll"/> onto
    /// the rig.
    ///
    /// <para><b>Stepped in real seconds, and not at all while paused</b> (§6): a zero or negative
    /// step changes nothing, so a paused colony's faces hold their frame like everything else.</para>
    /// </summary>
    public struct FaceMotion
    {
        /// <summary>The longest step taken at once; a longer hitch counts as this much.</summary>
        public const float LongestStep = 0.1f;

        /// <summary>How fast the held pose closes on the chosen expression, per second.</summary>
        public const float Ease = 10f;

        // The blink (§4): a lid falls faster than it lifts.
        public const float BlinkClose = 0.06f, BlinkShut = 0.03f, BlinkOpen = 0.09f;
        public const float BlinkShutOpen = 0.08f;
        public const float BlinkMin = 2.2f, BlinkMax = 6.0f;
        public const float DoubleBlinkGap = 0.1f;
        public const int DoubleBlinkOneIn = 7;

        // The speaker (§5).
        public const float PhraseMin = 1.0f, PhraseMax = 2.6f, PauseMin = 0.25f, PauseMax = 0.7f;
        public const float BeatMinHz = 2.2f, BeatMaxHz = 3.2f;
        public const float BeatNod = 2.5f, EmphasisNod = 4f, EmphasisBrow = 0.008f, EmphasisFade = 0.33f;
        public const float Sway = 2.5f, SwayHz = 0.35f;
        public const float ActivityRise = 0.12f;

        // The listener.
        public const float ListenNod = 5f, ListenNodSeconds = 0.5f, ListenMin = 1.6f, ListenMax = 3.6f;

        /// <summary>The expression to hold. Eased towards, never snapped to.</summary>
        public FaceExpression Expression;

        /// <summary>This frame's part in a conversation.</summary>
        public TalkRole Role;

        /// <summary>The bones' pose this frame.</summary>
        public FacePose Pose { get; private set; }

        /// <summary>Degrees the head dips, chin down, on top of wherever the gaze turned it. Never negative.</summary>
        public float NodPitch { get; private set; }

        /// <summary>Degrees the head sways about the face's axis.</summary>
        public float NodRoll { get; private set; }

        public bool Blinking => _blinkClock >= 0f;

        /// <summary>Whether a speaker is in a phrase rather than a pause.</summary>
        public bool InPhrase => _inPhrase;

        uint _rng;
        FacePose _held;

        float _blinkIn, _blinkClock;
        int _pairRemaining;

        bool _inPhrase;
        float _segmentLeft, _beatHz, _beatPhase, _emphasis, _activity, _swayPhase;
        int _beatsToEmphasis;

        float _listenIn, _listenClock;

        /// <summary>A face at rest, its rhythms seeded by <paramref name="seed"/> — the pawn id, so a
        /// colonist keeps hers across a lease and two colonists never share one.</summary>
        public static FaceMotion Start(int seed)
        {
            var face = new FaceMotion
            {
                _rng = FaceRandom.Seed(seed),
                _held = FacePose.Rest,
                Pose = FacePose.Rest,
                _blinkClock = -1f,
                _listenClock = -1f,
            };
            face._blinkIn = face.Range(BlinkMin, BlinkMax);
            face._listenIn = face.Range(ListenMin, ListenMax);
            face._swayPhase = face.Range(0f, 1f);
            return face;
        }

        public void Step(float seconds)
        {
            if (!(seconds > 0f)) return;
            float dt = Math.Min(seconds, LongestStep);

            _held = FacePose.Lerp(_held, FacePose.Of(Expression), 1f - MathF.Exp(-Ease * dt));

            float closure = StepBlink(dt);

            float nod = 0f, roll = 0f, brow = 0f;
            StepSpeaking(dt, ref nod, ref roll, ref brow);
            StepListening(dt, ref nod);

            // A blink overrides how open the expression holds the eyes rather than scaling it, so
            // a tired face blinks shut rather than to a slit narrower than shut (§4).
            float open = _held.EyeOpen + (BlinkShutOpen - _held.EyeOpen) * closure;
            Pose = new FacePose(_held.BrowLift + brow, _held.BrowRoll, open, _held.EyeSize);
            NodPitch = nod;
            NodRoll = roll;
        }

        /// <summary>How shut the eyes are, 0 to 1.</summary>
        float StepBlink(float dt)
        {
            if (_blinkClock < 0f)
            {
                _blinkIn -= dt;
                if (_blinkIn > 0f) return 0f;
                _blinkClock = Math.Min(-_blinkIn, BlinkClose);
            }
            else
            {
                _blinkClock += dt;
            }

            if (_blinkClock >= BlinkClose + BlinkShut + BlinkOpen)
            {
                _blinkClock = -1f;
                if (_pairRemaining > 0)
                {
                    _pairRemaining--;
                    _blinkIn = DoubleBlinkGap;
                }
                else
                {
                    _blinkIn = Range(BlinkMin, BlinkMax);
                    _pairRemaining = Next() % DoubleBlinkOneIn == 0 ? 1 : 0;
                }
                return 0f;
            }

            if (_blinkClock < BlinkClose) return Smooth(_blinkClock / BlinkClose);
            if (_blinkClock < BlinkClose + BlinkShut) return 1f;
            return 1f - Smooth((_blinkClock - BlinkClose - BlinkShut) / BlinkOpen);
        }

        void StepSpeaking(float dt, ref float nod, ref float roll, ref float brow)
        {
            bool speaking = Role == TalkRole.Speaking;
            if (speaking)
            {
                _segmentLeft -= dt;
                if (_segmentLeft <= 0f)
                {
                    if (_inPhrase)
                    {
                        _inPhrase = false;
                        _segmentLeft = Range(PauseMin, PauseMax);
                    }
                    else
                    {
                        BeginPhrase();
                    }
                }
            }
            else
            {
                // The next turn opens on a phrase at once rather than on a pause.
                _inPhrase = false;
                _segmentLeft = 0f;
            }

            float target = speaking && _inPhrase ? 1f : 0f;
            _activity += (target - _activity) * (1f - MathF.Exp(-dt / ActivityRise));
            if (_activity < 1e-4f && target == 0f) _activity = 0f;

            if (_inPhrase)
            {
                float before = _beatPhase;
                _beatPhase += _beatHz * dt;
                if ((int)_beatPhase != (int)before && --_beatsToEmphasis <= 0)
                {
                    _emphasis = 1f;
                    _beatsToEmphasis = 2 + (int)(Next() % 3);
                }
            }
            _emphasis = Math.Max(0f, _emphasis - dt / EmphasisFade);

            _swayPhase += SwayHz * dt;
            if (_swayPhase > 1f) _swayPhase -= 1f;

            if (_activity <= 0f) return;
            float beat = 0.5f * (1f - MathF.Cos(2f * MathF.PI * _beatPhase));
            nod += _activity * (BeatNod * beat + EmphasisNod * _emphasis);
            brow += _activity * EmphasisBrow * _emphasis;
            roll += _activity * Sway * MathF.Sin(2f * MathF.PI * _swayPhase);
        }

        void BeginPhrase()
        {
            _inPhrase = true;
            _segmentLeft = Range(PhraseMin, PhraseMax);
            _beatHz = Range(BeatMinHz, BeatMaxHz);
            _beatPhase = 0f;
            _beatsToEmphasis = 1 + (int)(Next() % 3);
        }

        void StepListening(float dt, ref float nod)
        {
            // A nod under way finishes whatever the role does next, rather than snapping off.
            if (_listenClock >= 0f)
            {
                _listenClock += dt;
                if (_listenClock >= ListenNodSeconds)
                {
                    _listenClock = -1f;
                    return;
                }
                float s = MathF.Sin(MathF.PI * _listenClock / ListenNodSeconds);
                nod += ListenNod * s * s;
                return;
            }

            if (Role != TalkRole.Listening) return;
            _listenIn -= dt;
            if (_listenIn > 0f) return;
            _listenClock = 0f;
            _listenIn = Range(ListenMin, ListenMax);
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        uint Next() => FaceRandom.Next(ref _rng);

        float Range(float min, float max) => FaceRandom.Range(ref _rng, min, max);
    }

    /// <summary>
    /// Two colonists talking, or one talking to nobody, for so many seconds, in turns (design 59
    /// §5). Engine-free: the figure director holds a list of these keyed by pawn id, so a
    /// conversation outlives either colonist's figure being pooled.
    /// </summary>
    public struct Conversation
    {
        /// <summary>The partner of somebody talking to nobody.</summary>
        public const int Nobody = -1;

        public const float TurnMin = 2.5f, TurnMax = 6f;

        public Conversation(int a, int b, float seconds)
        {
            A = a;
            B = b;
            Left = seconds;
            ASpeaking = true;
            _rng = FaceRandom.Seed(a * 7919 + b);
            _turnLeft = 0f;
            _turnLeft = FaceRandom.Range(ref _rng, TurnMin, TurnMax);
        }

        public readonly int A;
        public readonly int B;

        /// <summary>Seconds left to run.</summary>
        public float Left { get; private set; }

        /// <summary>Whether <see cref="A"/> has the turn. Talking to nobody, the other turn is silence.</summary>
        public bool ASpeaking { get; private set; }

        uint _rng;
        float _turnLeft;

        /// <summary>Advance; false once the time is up. A zero step changes nothing.</summary>
        public bool Step(float seconds)
        {
            if (!(seconds > 0f)) return Left > 0f;
            Left -= seconds;
            _turnLeft -= seconds;
            if (_turnLeft <= 0f)
            {
                ASpeaking = !ASpeaking;
                _turnLeft = FaceRandom.Range(ref _rng, TurnMin, TurnMax);
            }
            return Left > 0f;
        }

        public bool Involves(int pawn) => pawn == A || (B != Nobody && pawn == B);

        public int PartnerOf(int pawn) =>
            pawn == A ? B : B != Nobody && pawn == B ? A : Nobody;

        public TalkRole RoleOf(int pawn)
        {
            if (pawn == A)
                return ASpeaking ? TalkRole.Speaking : B == Nobody ? TalkRole.None : TalkRole.Listening;
            if (B != Nobody && pawn == B)
                return ASpeaking ? TalkRole.Listening : TalkRole.Speaking;
            return TalkRole.None;
        }
    }

    /// <summary>A small xorshift for faces: seeded, repeatable, allocation-free. Drawing only.</summary>
    static class FaceRandom
    {
        public static uint Seed(int seed)
        {
            uint s = unchecked((uint)seed * 2654435761u) ^ 0x9E3779B9u;
            if (s == 0) s = 1;
            // A few rounds, so neighbouring pawn ids do not start their sequences side by side.
            for (int i = 0; i < 4; i++) Next(ref s);
            return s;
        }

        public static uint Next(ref uint s)
        {
            s ^= s << 13;
            s ^= s >> 17;
            s ^= s << 5;
            return s;
        }

        public static float Range(ref uint s, float min, float max) =>
            min + (max - min) * ((Next(ref s) >> 8) / 16777216f);
    }
}
