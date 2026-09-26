#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// Faces and talking (design 59): what each colonist's face holds, who is talking with whom,
    /// and the pass that writes both onto the live figures.
    ///
    /// <para><b>A face follows its context unless told otherwise</b> (§3a; owner, 2026-09-26:
    /// <i>"give it context where we can for now"</i>): stern drafted or fighting, pained, tired,
    /// glum, eyes shut asleep — <see cref="FaceContext"/> decides from what the frame publishes. The
    /// debug menu's Faces tab can force one for a colonist or for everyone, and hand it back.</para>
    ///
    /// <para><b>Conversations start two ways</b>: asked for (<see cref="StartConversation"/>, the
    /// debug row and whatever decides later), and struck up by colonists at leisure standing close
    /// (§5c). Drawing only either way: nothing in a cell, a save or the hash.</para>
    ///
    /// <para><b>Held by pawn id, not by figure</b> (§6): figures are pooled, and a colonist who walks
    /// off screen and back keeps her forced face and her conversation.</para>
    /// </summary>
    public sealed partial class PawnFigureDirector
    {
        /// <summary>
        /// Off, no face moves and nobody nods. <b>A measurement control, not a setting</b> — the same
        /// footing as <see cref="Odyssey.Presentation.Rendering.ColonistAttachments.Enabled"/>: the
        /// face pass is judged against the same run with it off, and nothing in the game writes it.
        /// </summary>
        public static bool FacesEnabled { get; set; } = true;

        /// <summary>
        /// Off, colonists never strike up a conversation of their own (§5c) — only one asked for
        /// happens. For a test that needs to count conversations; nothing in the game writes it.
        /// </summary>
        public static bool AmbientConversations { get; set; } = true;

        /// <summary>How far a debug conversation looks for somebody to talk to (design 59 §7).</summary>
        public const float TalkReach = 8f;

        // Struck-up conversations (§5c).
        public const float ChatCheckSeconds = 1f, ChatReach = 4f, ChatBreak = 6f;
        public const int ChatChancePercent = 25;
        public const float ChatMin = 8f, ChatMax = 18f, ChatRestMin = 45f, ChatRestMax = 90f;

        // The talking hands (§5b), about the figure's own axes as the carry and the climb are.
        public const float TalkShoulder = 25f, TalkElbow = 70f, TalkOut = 16f, TalkHandsEase = 6f;

        /// <summary>Faster than this, in drawn metres a second, a colonist keeps her hands down.</summary>
        public const float TalkHandsBelowSpeed = 0.35f;

        static readonly AspectKey PainKey = AspectKey.Of(HealthAspectNames.Pain);

        readonly Dictionary<int, FaceExpression> _expressions = new Dictionary<int, FaceExpression>();
        readonly List<Conversation> _conversations = new List<Conversation>();
        FaceExpression? _everyone;

        readonly Dictionary<int, float> _chatNotBefore = new Dictionary<int, float>();
        float _chatTime, _chatClock;
        uint _chatRng = FaceRandom.Seed(0x51A7);

        /// <summary>The face every colonist is forced to, or null: each follows her own context.</summary>
        public FaceExpression? EveryoneExpression => _everyone;

        /// <summary>
        /// Force every colonist's face to <paramref name="expression"/>, or hand every face back to
        /// its context with null. Any face forced one at a time is forgotten.
        /// </summary>
        public void SetEveryoneExpression(FaceExpression? expression)
        {
            _expressions.Clear();
            _everyone = expression;
        }

        /// <summary>Force one colonist's face, or with null hand it back to the colony's rule.</summary>
        public void SetExpression(PawnId pawn, FaceExpression? expression)
        {
            if (expression is FaceExpression e) _expressions[pawn.Value] = e;
            else _expressions.Remove(pawn.Value);
        }

        /// <summary>The face forced on <paramref name="pawn"/>, one at a time or with everyone; null when her context decides.</summary>
        public FaceExpression? ForcedExpressionOf(PawnId pawn) =>
            _expressions.TryGetValue(pawn.Value, out FaceExpression e) ? e : _everyone;

        /// <summary>The face <paramref name="pawn"/> is holding: forced, or her context's, or neutral off screen.</summary>
        public FaceExpression ExpressionOf(PawnId pawn) =>
            ForcedExpressionOf(pawn) ??
            (_byPawn.TryGetValue(pawn.Value, out Figure? figure) ? figure.ContextFace : FaceExpression.Neutral);

        /// <summary>
        /// <paramref name="a"/> talks with <paramref name="b"/> — or to nobody, given
        /// <see cref="PawnId.None"/> — for <paramref name="seconds"/>. Either one's conversation
        /// already under way ends first: nobody holds two at once.
        /// </summary>
        public void StartConversation(PawnId a, PawnId b, float seconds)
        {
            if (!a.IsValid || seconds <= 0f) return;
            EndConversationsOf(a.Value);
            if (b.IsValid && b != a) EndConversationsOf(b.Value);
            _conversations.Add(new Conversation(a.Value, b.IsValid && b != a ? b.Value : Conversation.Nobody, seconds));
        }

        public void EndConversations() => _conversations.Clear();

        public int ConversationCount => _conversations.Count;

        /// <summary>How many of the conversations under way the colony struck up by itself.</summary>
        public int AmbientConversationCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _conversations.Count; i++) if (_conversations[i].Ambient) n++;
                return n;
            }
        }

        public TalkRole RoleOf(PawnId pawn)
        {
            for (int i = 0; i < _conversations.Count; i++)
                if (_conversations[i].Involves(pawn.Value)) return _conversations[i].RoleOf(pawn.Value);
            return TalkRole.None;
        }

        public bool IsTalking(PawnId pawn)
        {
            for (int i = 0; i < _conversations.Count; i++)
                if (_conversations[i].Involves(pawn.Value)) return true;
            return false;
        }

        /// <summary>
        /// The awake, standing colonist nearest <paramref name="pawn"/> within
        /// <see cref="TalkReach"/>, or none — who a debug conversation is had with.
        /// </summary>
        public static PawnId NearestListener(System.ReadOnlySpan<PawnView> pawns, PawnId pawn)
        {
            int at = -1;
            for (int i = 0; i < pawns.Length; i++) if (pawns[i].Id == pawn) { at = i; break; }
            if (at < 0) return PawnId.None;

            Vector3 from = Odyssey.Presentation.Rendering.CellMetrics.FloorCentre(pawns[at].Cell);
            PawnId best = PawnId.None;
            float bestSq = TalkReach * TalkReach;
            for (int i = 0; i < pawns.Length; i++)
            {
                if (i == at || !CanTalk(in pawns[i])) continue;
                float sq = (Odyssey.Presentation.Rendering.CellMetrics.FloorCentre(pawns[i].Cell) - from).sqrMagnitude;
                if (sq <= bestSq) { bestSq = sq; best = pawns[i].Id; }
            }
            return best;
        }

        /// <summary>A colonist, awake and on her feet. Animals have no face and bandits nothing to say.</summary>
        public static bool CanTalk(in PawnView pawn) =>
            pawn.Kind == PawnKindLabels.ColonistKind && !pawn.IsHostile && !pawn.Asleep && !pawn.IsDowned;

        /// <summary>
        /// Read what this pawn's own state asks of her face, whether she is at leisure to chat, and
        /// whether her hands are free — once a frame, from the frame, beside the rest of her pose.
        /// </summary>
        void NoteFace(Figure figure, in PawnView pawn)
        {
            if (figure.FaceRig == null) return;
            int pain = 0;
            if (_frame != null && _frame.TryGetPawnAspect(pawn.Id, PainKey, out int published)) pain = published;
            figure.ContextFace = FaceContext.Expression(FaceSignals.Of(in pawn, pain));
            figure.CanChat = CanTalk(in pawn) && FaceContext.CanChat(in pawn);
            figure.HandsFree = !pawn.IsWeaponDrawn && !pawn.IsDrafted && !pawn.Seated && pawn.JobDef != JobHandle.Eat;
        }

        void EndConversationsOf(int pawn)
        {
            for (int i = _conversations.Count - 1; i >= 0; i--)
                if (_conversations[i].Involves(pawn)) _conversations.RemoveAt(i);
        }

        /// <summary>
        /// Step the conversations by <paramref name="seconds"/>, ending any whose time is up or one
        /// of whose colonists has gone to sleep, been downed or left the frame — and a struck-up one
        /// as soon as either has something better to do or they have drifted apart.
        /// </summary>
        void StepConversations(float seconds)
        {
            for (int i = _conversations.Count - 1; i >= 0; i--)
            {
                Conversation talk = _conversations[i];
                bool alive = talk.Step(seconds) && StillTalking(talk.A) &&
                             (talk.B == Conversation.Nobody || StillTalking(talk.B));
                if (alive && talk.Ambient) alive = StillChatting(talk.A) && StillChatting(talk.B) && Close(talk.A, talk.B, ChatBreak);
                if (alive) _conversations[i] = talk;
                else _conversations.RemoveAt(i);
            }
        }

        bool StillTalking(int pawn) =>
            _frame == null || (_frame.TryGetPawn(new PawnId(pawn), out PawnView view) && CanTalk(in view));

        bool StillChatting(int pawn) => _byPawn.TryGetValue(pawn, out Figure? figure) && figure.CanChat;

        bool Close(int a, int b, float reach)
        {
            if (!_byPawn.TryGetValue(a, out Figure? fa) || !_byPawn.TryGetValue(b, out Figure? fb)) return false;
            Vector3 d = fb.Transform.position - fa.Transform.position;
            return Mathf.Abs(d.y) <= 1.5f && d.x * d.x + d.z * d.z <= reach * reach;
        }

        /// <summary>
        /// Once a second, any two drawn colonists at leisure and standing within
        /// <see cref="ChatReach"/> of each other may fall to talking (§5c): a small chance each check,
        /// a conversation of <see cref="ChatMin"/> to <see cref="ChatMax"/> seconds, and then a rest
        /// before either strikes up another. Only drawn figures are asked, so the cost follows what
        /// is on screen and never the colony.
        /// </summary>
        void StrikeUpConversations(float seconds)
        {
            if (!AmbientConversations || seconds <= 0f) return;
            _chatTime += seconds;
            _chatClock += seconds;
            if (_chatClock < ChatCheckSeconds) return;
            _chatClock = 0f;

            for (int i = 0; i < _figures.Count; i++)
            {
                Figure a = _figures[i];
                if (!ReadyToChat(a)) continue;
                for (int j = i + 1; j < _figures.Count; j++)
                {
                    Figure b = _figures[j];
                    if (!ReadyToChat(b) || !Close(a.Pawn, b.Pawn, ChatReach)) continue;
                    if (FaceRandom.Next(ref _chatRng) % 100 >= ChatChancePercent) continue;
                    float length = FaceRandom.Range(ref _chatRng, ChatMin, ChatMax);
                    _conversations.Add(new Conversation(a.Pawn, b.Pawn, length, ambient: true));
                    float rest = _chatTime + length + FaceRandom.Range(ref _chatRng, ChatRestMin, ChatRestMax);
                    _chatNotBefore[a.Pawn] = rest;
                    _chatNotBefore[b.Pawn] = rest;
                    break;
                }
            }
        }

        /// <summary>
        /// At leisure, not already talking, rested from the last conversation — and <b>standing
        /// still</b>. Measured with <c>TalkCheck</c> (design 59 §5c): idle colonists wander, and a
        /// pair who met in passing had walked six metres apart within seconds, so a conversation
        /// struck up on the move ended before it began. Standing still is a pause between legs of
        /// a wander, waiting, or a meal.
        /// </summary>
        bool ReadyToChat(Figure figure) =>
            figure.Pawn >= 0 && figure.FaceRig != null && figure.CanChat && figure.Speed < TalkHandsBelowSpeed &&
            !IsTalking(new PawnId(figure.Pawn)) &&
            (!_chatNotBefore.TryGetValue(figure.Pawn, out float after) || _chatTime >= after);

        /// <summary>
        /// Where a talking colonist should look: her partner's head, drawn or not. False when she is
        /// not in a conversation, or is talking to nobody.
        /// </summary>
        bool TalkPartnerHead(int pawn, out Vector3 head)
        {
            head = default;
            for (int i = 0; i < _conversations.Count; i++)
            {
                if (!_conversations[i].Involves(pawn)) continue;
                int partner = _conversations[i].PartnerOf(pawn);
                if (partner == Conversation.Nobody) return false;
                if (_byPawn.TryGetValue(partner, out Figure? figure) && figure.Head != null)
                {
                    head = figure.Head.position;
                    return true;
                }
                if (_frame != null && _frame.TryGetPawn(new PawnId(partner), out PawnView view))
                {
                    // Not drawn: a standing colonist's eyes, at the figure's scale.
                    head = Odyssey.Presentation.Rendering.CellMetrics.FloorCentre(view.Cell) + Vector3.up * 2.2f;
                    return true;
                }
                return false;
            }
            return false;
        }

        /// <summary>
        /// A live figure's face as the bones hold it, for a test under the real player loop: the
        /// brows' lift and the eyes' openness read off the rig, the nod this frame, where the gaze
        /// is, the face her context asks for and how far her talking hand is up as drawn. False
        /// when the pawn has no figure or the figure no face.
        /// </summary>
        public bool TryGetFace(int pawnId, out float browLift, out float eyeOpen, out float nodPitch,
            out GazePriority gaze, out FaceExpression context, out float hands)
        {
            browLift = 0f;
            eyeOpen = 1f;
            nodPitch = 0f;
            gaze = GazePriority.None;
            context = FaceExpression.Neutral;
            hands = 0f;
            if (!_byPawn.TryGetValue(pawnId, out Figure? figure) || figure.FaceRig == null) return false;
            browLift = figure.FaceRig.MeasuredBrowLift;
            eyeOpen = figure.FaceRig.MeasuredEyeOpen;
            nodPitch = figure.Face.NodPitch;
            gaze = figure.Gaze.ActivePriority;
            context = figure.ContextFace;
            hands = figure.Face.ArmLift * figure.TalkHands;
            return true;
        }

        /// <summary>
        /// Write every live figure's face, nod and talking hands (§6). After the gaze, which sets the
        /// head absolutely; before the head is hidden, which shrinks the head as drawn.
        /// </summary>
        void ApplyFaces(float deltaTime, bool stepConversations)
        {
            if (!FacesEnabled) return;
            float seconds = Running ? deltaTime : 0f;
            if (stepConversations)
            {
                StepConversations(seconds);
                StrikeUpConversations(seconds);
            }

            for (int i = 0; i < _figures.Count; i++)
            {
                Figure figure = _figures[i];
                if (figure.Pawn < 0 || figure.FaceRig == null) continue;

                var id = new PawnId(figure.Pawn);
                figure.Face.Expression = ExpressionOf(id);
                figure.Face.Role = RoleOf(id);
                figure.Face.Step(seconds);

                figure.FaceRig.Apply(figure.Face.Pose);
                if (figure.Head != null)
                    figure.FaceRig.Nod(figure.Head, figure.Face.NodPitch, figure.Face.NodRoll, figure.Face.NodYaw);

                bool free = figure.HandsFree && figure.Speed < TalkHandsBelowSpeed && figure.WorkWeight < 0.01f &&
                            figure.CarryWeight < 0.01f && figure.SitWeight < 0.1f && figure.SleepWeight < 0.01f &&
                            figure.SwimWeight < 0.01f && figure.ClimbWeight < 0.01f;
                figure.TalkHands += ((free ? 1f : 0f) - figure.TalkHands) * (1f - Mathf.Exp(-TalkHandsEase * seconds));
                float hands = figure.Face.ArmLift * figure.TalkHands;
                if (hands > 1e-3f) ApplyTalkingHands(figure, hands, figure.Face.Arm, figure.Face.ElbowBeat);
            }
        }

        /// <summary>
        /// Talk with the hands (§5b): the upper arm forward and a little out, the forearm up in front,
        /// beating with the phrase. <b>Added over the clip</b>, as a gesture laid on a stance is —
        /// the idle's own arm is the rest it starts from — and scaled by <paramref name="weight"/>,
        /// so it rises and falls rather than arriving. The signs are the climb's and the carry's.
        /// </summary>
        void ApplyTalkingHands(Figure figure, float weight, TalkArm arm, float beat)
        {
            Vector3 axis = SwingAxis(figure.Transform, 0f);
            Vector3 outward = figure.Transform.forward;
            if (arm == TalkArm.Right || arm == TalkArm.Both)
            {
                Pitch(figure.RightUpperArm, axis, -TalkShoulder * weight);
                Pitch(figure.RightUpperArm, outward, -TalkOut * weight);
                Pitch(figure.RightLowerArm, axis, -(TalkElbow + beat) * weight);
            }
            if (arm == TalkArm.Left || arm == TalkArm.Both)
            {
                Pitch(figure.LeftUpperArm, axis, -TalkShoulder * weight);
                Pitch(figure.LeftUpperArm, outward, TalkOut * weight);
                Pitch(figure.LeftLowerArm, axis, -(TalkElbow + beat) * weight);
            }
        }
    }
}
