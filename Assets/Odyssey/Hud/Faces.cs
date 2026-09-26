#nullable enable
using System;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// What a colonist's face is holding (design 65 §3). Drawn only; never saved. Appended to, never
    /// reordered: the debug tab's rows are in this order.
    /// </summary>
    public enum FaceExpression : byte
    {
        Neutral,
        Raised,
        Alarmed,
        Stern,
        Sceptical,
        Tired,
        Pained,
        Glum,
        Asleep,
    }

    /// <summary>A colonist's part in a conversation this frame (design 65 §5).</summary>
    public enum TalkRole : byte
    {
        None,
        Speaking,
        Listening,
    }

    /// <summary>How a speaker delivers one phrase (design 65 §5a). Rolled per phrase.</summary>
    public enum TalkManner : byte
    {
        /// <summary>Beats, and every few beats an emphasis: a bigger nod and a brow lift.</summary>
        Emphatic,

        /// <summary>A shake of the head to open the phrase: no, or not that.</summary>
        Shake,

        /// <summary>Beats, then brows up, chin up and a tilt at the end, held through the pause.</summary>
        Question,

        /// <summary>Slower beats, looking away for the first part of the phrase: thinking aloud.</summary>
        Musing,

        /// <summary>The head and the brows tilted for the whole phrase: doubt.</summary>
        Tilt,
    }

    /// <summary>Which hand a speaker talks with (design 65 §5b).</summary>
    public enum TalkArm : byte
    {
        None,
        Right,
        Left,
        Both,
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
        /// The expressions. The first six are at the magnitudes <c>FaceSheet</c> photographed on
        /// 2026-09-26 (e-15), all of which read in the 128 px portrait and at the closest zoom;
        /// Pained, Glum and Asleep sit inside the same envelope.
        /// </summary>
        public static FacePose Of(FaceExpression expression) => expression switch
        {
            FaceExpression.Raised => new FacePose(0.015f, 0f, 1f, 1f),
            FaceExpression.Alarmed => new FacePose(0.020f, 0f, 1.2f, 1.2f),
            FaceExpression.Stern => new FacePose(-0.008f, 0f, 0.6f, 1f),
            FaceExpression.Sceptical => new FacePose(0.004f, 12f, 1f, 1f),
            FaceExpression.Tired => new FacePose(-0.004f, 0f, 0.45f, 1f),
            FaceExpression.Pained => new FacePose(-0.006f, 0f, 0.35f, 1f),
            FaceExpression.Glum => new FacePose(-0.003f, 0f, 0.8f, 1f),
            FaceExpression.Asleep => new FacePose(-0.002f, 0f, FaceMotion.BlinkShutOpen, 1f),
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

    /// <summary>What the frame says about one colonist that a face should answer to (design 65 §3a).</summary>
    public readonly struct FaceSignals
    {
        public FaceSignals(bool asleep, bool downed, bool stunned, bool fleeing, bool fighting, bool drafted,
            int painPerMille, int rest, int mood)
        {
            Asleep = asleep;
            Downed = downed;
            Stunned = stunned;
            Fleeing = fleeing;
            Fighting = fighting;
            Drafted = drafted;
            PainPerMille = painPerMille;
            Rest = rest;
            Mood = mood;
        }

        public readonly bool Asleep, Downed, Stunned, Fleeing, Fighting, Drafted;
        public readonly int PainPerMille, Rest, Mood;

        /// <summary>A colonist at ease and awake: no pain, rested, content — the face as painted.</summary>
        public static FaceSignals AtEase => new FaceSignals(false, false, false, false, false, false, 0, 1000, 600);

        /// <summary>From one published pawn and her pain aspect (0 where none is published).</summary>
        public static FaceSignals Of(in PawnView pawn, int painPerMille) => new FaceSignals(
            pawn.Asleep, pawn.IsDowned, pawn.IsStunned, pawn.JobDef == JobHandle.Flee,
            pawn.JobDef == JobHandle.AttackMelee || pawn.JobDef == JobHandle.AttackRanged || pawn.IsWeaponDrawn,
            pawn.IsDrafted, painPerMille, pawn.Rest, pawn.Mood);
    }

    /// <summary>
    /// What face a colonist wears when nobody has told her one (design 65 §3a; owner, 2026-09-26:
    /// <i>"stern should happen when fighting/in draft, obviously when you tired"</i>). First match
    /// wins, so the order is the ranking: what she is doing to survive beats how she feels.
    /// </summary>
    public static class FaceContext
    {
        /// <summary>Pain that shows through anything but sleep, per mille. The simulation's shock is 800.</summary>
        public const int AgonyAbove = 500;

        /// <summary>Pain that shows on a face at ease, per mille.</summary>
        public const int PainedAbove = 200;

        /// <summary>
        /// Tired below this rest: <c>Need_Rest</c>'s <c>seekThreshold</c>, where a colonist starts
        /// wanting a bed. <b>A reading aid, as <see cref="MoodBands"/> are</b> — the interface
        /// cannot read the content — so if the content moves, this is the line that moves with it.
        /// </summary>
        public const int TiredBelow = 280;

        public static FaceExpression Expression(in FaceSignals s)
        {
            if (s.Asleep) return FaceExpression.Asleep;
            if (s.Downed) return FaceExpression.Pained;
            if (s.Stunned || s.Fleeing) return FaceExpression.Alarmed;
            if (s.PainPerMille > AgonyAbove) return FaceExpression.Pained;
            if (s.Fighting || s.Drafted) return FaceExpression.Stern;
            if (s.PainPerMille > PainedAbove) return FaceExpression.Pained;
            if (s.Rest < TiredBelow) return FaceExpression.Tired;
            if (s.Mood < MoodBands.Strained) return FaceExpression.Glum;
            return FaceExpression.Neutral;
        }

        /// <summary>
        /// Whether a colonist is at leisure enough to strike up a conversation (design 65 §5c): idle,
        /// wandering, waiting or eating, and not drafted, asleep, downed or at work.
        /// </summary>
        public static bool CanChat(in PawnView pawn) =>
            !pawn.Asleep && !pawn.IsDowned && !pawn.IsDrafted && !pawn.Working && !pawn.IsStunned &&
            (pawn.JobDef < 0 || pawn.JobDef == JobHandle.Wander || pawn.JobDef == JobHandle.Wait ||
             pawn.JobDef == JobHandle.Eat);
    }

    /// <summary>
    /// One face's motion (design 65): the expression eased in, the blink, and the head and hands a
    /// talker moves. Engine-free and allocation-free; the figure director steps one per live figure
    /// and writes <see cref="Pose"/>, the nod and the arm onto the rig.
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

        // The manners (§5a).
        public const float ShakeYaw = 7f, ShakeHz = 2.5f, ShakeSeconds = 0.9f;
        public const float QuestionBrow = 0.012f, QuestionRoll = 7f, QuestionChin = 4f;
        public const float MusingYaw = 18f, MusingBeat = 0.55f;
        public const float TiltRoll = 6f, TiltBrowRoll = 8f;
        public const float MannerEase = 8f;

        // The hands (§5b).
        public const int GestureOneInTen = 6;
        public const float ArmEase = 5f;
        public const float ElbowBeatDegrees = 10f;

        // The listener.
        public const float ListenNod = 5f, ListenNodSeconds = 0.5f, ListenMin = 1.6f, ListenMax = 3.6f;
        public const float ListenBrow = 0.008f, ListenTiltRoll = 6f;

        // The greeting.
        public const float FlashBrow = 0.012f, FlashSeconds = 0.45f;

        /// <summary>The expression to hold. Eased towards, never snapped to.</summary>
        public FaceExpression Expression;

        /// <summary>This frame's part in a conversation.</summary>
        public TalkRole Role;

        /// <summary>The bones' pose this frame.</summary>
        public FacePose Pose { get; private set; }

        /// <summary>Degrees the head dips, chin down, on top of wherever the gaze turned it. Negative lifts the chin.</summary>
        public float NodPitch { get; private set; }

        /// <summary>Degrees the head sways about the face's axis.</summary>
        public float NodRoll { get; private set; }

        /// <summary>Degrees the head turns about the neck, on top of the gaze: a shake or a glance away.</summary>
        public float NodYaw { get; private set; }

        /// <summary>How far the talking hand is up, 0 to 1.</summary>
        public float ArmLift { get; private set; }

        /// <summary>Which hand, while <see cref="ArmLift"/> is above nought.</summary>
        public TalkArm Arm { get; private set; }

        /// <summary>Degrees the talking forearm beats with the phrase, on top of the lift.</summary>
        public float ElbowBeat { get; private set; }

        public bool Blinking => _blinkClock >= 0f;

        /// <summary>Whether a speaker is in a phrase rather than a pause.</summary>
        public bool InPhrase => _inPhrase;

        /// <summary>The manner of the phrase under way, or of the last one.</summary>
        public TalkManner Manner => _manner;

        uint _rng;
        FacePose _held;

        float _blinkIn, _blinkClock;
        int _pairRemaining;

        bool _inPhrase;
        float _segmentLeft, _phraseLength, _phraseClock, _beatHz, _beatPhase, _emphasis, _activity, _swayPhase;
        int _beatsToEmphasis;
        TalkManner _manner;
        float _glanceSign;
        TalkArm _phraseArm;

        // The manners' held parts, eased.
        float _yaw, _roll, _chin, _browLift, _browRoll;

        float _listenIn, _listenClock, _listenLength;
        byte _listenKind;

        float _flashClock;

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
                _flashClock = -1f,
            };
            face._blinkIn = face.Range(BlinkMin, BlinkMax);
            face._listenIn = face.Range(ListenMin, ListenMax);
            face._swayPhase = face.Range(0f, 1f);
            return face;
        }

        /// <summary>An eyebrow flash: the brows up and down again in under half a second, the way
        /// people greet one another in passing.</summary>
        public void Flash() => _flashClock = 0f;

        public void Step(float seconds)
        {
            if (!(seconds > 0f)) return;
            float dt = Math.Min(seconds, LongestStep);

            _held = FacePose.Lerp(_held, FacePose.Of(Expression), 1f - MathF.Exp(-Ease * dt));

            float closure = StepBlink(dt);

            float nod = 0f, roll = 0f, yaw = 0f, brow = 0f, browRoll = 0f;
            StepSpeaking(dt, ref nod, ref roll, ref yaw, ref brow, ref browRoll);
            StepListening(dt, ref nod, ref roll, ref brow);
            StepFlash(dt, ref brow);

            // A blink overrides how open the expression holds the eyes rather than scaling it, so
            // a tired face blinks shut rather than to a slit narrower than shut (§4).
            float open = _held.EyeOpen + (BlinkShutOpen - _held.EyeOpen) * closure;
            Pose = new FacePose(_held.BrowLift + brow, _held.BrowRoll + browRoll, open, _held.EyeSize);
            NodPitch = nod;
            NodRoll = roll;
            NodYaw = yaw;
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

        void StepSpeaking(float dt, ref float nod, ref float roll, ref float yaw, ref float brow, ref float browRoll)
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
                _phraseClock += dt;
                float rate = _manner == TalkManner.Musing ? _beatHz * MusingBeat : _beatHz;
                float before = _beatPhase;
                _beatPhase += rate * dt;
                if ((int)_beatPhase != (int)before && --_beatsToEmphasis <= 0)
                {
                    _emphasis = 1f;
                    _beatsToEmphasis = 2 + (int)(Next() % 3);
                }
            }
            _emphasis = Math.Max(0f, _emphasis - dt / EmphasisFade);

            _swayPhase += SwayHz * dt;
            if (_swayPhase > 1f) _swayPhase -= 1f;

            // The manner's held parts: what it wants now, eased.
            float t = _phraseLength > 0f ? _phraseClock / _phraseLength : 0f;
            float wantYaw = 0f, wantRoll = 0f, wantChin = 0f, wantBrow = 0f, wantBrowRoll = 0f;
            if (speaking)
            {
                switch (_manner)
                {
                    case TalkManner.Question:
                        // Rising at the end of the phrase and held through the pause after it.
                        if (!_inPhrase || t > 0.65f)
                        {
                            wantBrow = QuestionBrow;
                            wantRoll = QuestionRoll * _glanceSign;
                            wantChin = QuestionChin;
                        }
                        break;
                    case TalkManner.Musing:
                        if (_inPhrase && t < 0.45f) wantYaw = MusingYaw * _glanceSign;
                        break;
                    case TalkManner.Tilt:
                        if (_inPhrase)
                        {
                            wantRoll = TiltRoll * _glanceSign;
                            wantBrowRoll = TiltBrowRoll * _glanceSign;
                        }
                        break;
                }
            }
            float k = 1f - MathF.Exp(-MannerEase * dt);
            _yaw += (wantYaw - _yaw) * k;
            _roll += (wantRoll - _roll) * k;
            _chin += (wantChin - _chin) * k;
            _browLift += (wantBrow - _browLift) * k;
            _browRoll += (wantBrowRoll - _browRoll) * k;

            yaw += _yaw;
            roll += _roll;
            nod -= _chin;
            brow += _browLift;
            browRoll += _browRoll;

            // A shake opens the phrase: a windowed oscillation, gone by the time the beats take over.
            if (_inPhrase && _manner == TalkManner.Shake && _phraseClock < ShakeSeconds)
            {
                float window = MathF.Sin(MathF.PI * _phraseClock / ShakeSeconds);
                yaw += ShakeYaw * window * MathF.Sin(2f * MathF.PI * ShakeHz * _phraseClock);
            }

            // The hands: up for a phrase that gestures, down for a pause and for one that does not.
            float wantArm = speaking && _inPhrase && _phraseArm != TalkArm.None ? 1f : 0f;
            if (wantArm > 0f) Arm = _phraseArm;
            ArmLift += (wantArm - ArmLift) * (1f - MathF.Exp(-ArmEase * dt));
            if (ArmLift < 1e-3f && wantArm == 0f)
            {
                ArmLift = 0f;
                Arm = TalkArm.None;
            }

            if (_activity <= 0f)
            {
                ElbowBeat = 0f;
                return;
            }
            float beat = 0.5f * (1f - MathF.Cos(2f * MathF.PI * _beatPhase));
            float beatNod = _manner == TalkManner.Musing || _manner == TalkManner.Shake ? BeatNod * 0.5f : BeatNod;
            nod += _activity * (beatNod * beat + EmphasisNod * _emphasis);
            brow += _activity * EmphasisBrow * _emphasis;
            roll += _activity * Sway * MathF.Sin(2f * MathF.PI * _swayPhase);
            ElbowBeat = _activity * ElbowBeatDegrees * (beat - 0.5f) * 2f;
        }

        void BeginPhrase()
        {
            _inPhrase = true;
            _phraseLength = Range(PhraseMin, PhraseMax);
            _segmentLeft = _phraseLength;
            _phraseClock = 0f;
            _beatHz = Range(BeatMinHz, BeatMaxHz);
            _beatPhase = 0f;
            _beatsToEmphasis = 1 + (int)(Next() % 3);
            _glanceSign = Next() % 2 == 0 ? 1f : -1f;

            // Weighted: mostly plain emphasis, then questions, musing, doubt and the odd shake.
            uint roll = Next() % 100;
            _manner = roll < 40 ? TalkManner.Emphatic
                : roll < 58 ? TalkManner.Question
                : roll < 73 ? TalkManner.Musing
                : roll < 88 ? TalkManner.Tilt
                : TalkManner.Shake;

            if (Next() % 10 < GestureOneInTen)
            {
                uint hand = Next() % 20;
                _phraseArm = hand < 9 ? TalkArm.Right : hand < 16 ? TalkArm.Left : TalkArm.Both;
            }
            else
            {
                _phraseArm = TalkArm.None;
            }
        }

        /// <summary>The listener's replies (§5): a nod, a double nod, a brow raise or a tilt of the head.</summary>
        void StepListening(float dt, ref float nod, ref float roll, ref float brow)
        {
            // A reply under way finishes whatever the role does next, rather than snapping off.
            if (_listenClock >= 0f)
            {
                _listenClock += dt;
                if (_listenClock >= _listenLength)
                {
                    _listenClock = -1f;
                    return;
                }
                float u = _listenClock / _listenLength;
                switch (_listenKind)
                {
                    case 1: // a double nod: two in the time of one and a half
                        float half = u < 0.5f ? u * 2f : (u - 0.5f) * 2f;
                        float d = MathF.Sin(MathF.PI * half);
                        nod += ListenNod * 0.8f * d * d;
                        break;
                    case 2:
                        float b = MathF.Sin(MathF.PI * u);
                        brow += ListenBrow * b * b;
                        break;
                    case 3:
                        float r = MathF.Sin(MathF.PI * u);
                        roll += ListenTiltRoll * _glanceSign * r * r;
                        break;
                    default:
                        float s = MathF.Sin(MathF.PI * u);
                        nod += ListenNod * s * s;
                        break;
                }
                return;
            }

            if (Role != TalkRole.Listening) return;
            _listenIn -= dt;
            if (_listenIn > 0f) return;
            _listenClock = 0f;
            _listenIn = Range(ListenMin, ListenMax);
            uint pick = Next() % 20;
            _listenKind = (byte)(pick < 10 ? 0 : pick < 14 ? 1 : pick < 17 ? 2 : 3);
            _listenLength = _listenKind switch { 1 => 0.7f, 2 => 0.6f, 3 => 1.2f, _ => ListenNodSeconds };
            _glanceSign = Next() % 2 == 0 ? 1f : -1f;
        }

        void StepFlash(float dt, ref float brow)
        {
            if (_flashClock < 0f) return;
            _flashClock += dt;
            if (_flashClock >= FlashSeconds)
            {
                _flashClock = -1f;
                return;
            }
            float s = MathF.Sin(MathF.PI * _flashClock / FlashSeconds);
            brow += FlashBrow * s * s;
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        uint Next() => FaceRandom.Next(ref _rng);

        float Range(float min, float max) => FaceRandom.Range(ref _rng, min, max);
    }

    /// <summary>
    /// Two colonists talking, or one talking to nobody, for so many seconds, in turns (design 65
    /// §5). Engine-free: the figure director holds a list of these keyed by pawn id, so a
    /// conversation outlives either colonist's figure being pooled.
    /// </summary>
    public struct Conversation
    {
        /// <summary>The partner of somebody talking to nobody.</summary>
        public const int Nobody = -1;

        public const float TurnMin = 2.5f, TurnMax = 6f;

        /// <param name="ambient">Struck up by the colony itself (§5c) rather than asked for: it ends
        /// as soon as either colonist has something better to do.</param>
        public Conversation(int a, int b, float seconds, bool ambient = false)
        {
            A = a;
            B = b;
            Ambient = ambient;
            Left = seconds;
            ASpeaking = true;
            _rng = FaceRandom.Seed(a * 7919 + b);
            _turnLeft = 0f;
            _turnLeft = FaceRandom.Range(ref _rng, TurnMin, TurnMax);
        }

        public readonly int A;
        public readonly int B;
        public readonly bool Ambient;

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
    public static class FaceRandom
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
