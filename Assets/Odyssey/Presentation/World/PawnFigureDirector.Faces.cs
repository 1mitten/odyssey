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
    /// <para><b>Nothing here decides when.</b> The owner's word on 2026-09-26 was that the mechanism
    /// comes first and the triggers later; the debug menu's Faces tab calls
    /// <see cref="StartConversation"/> and <see cref="SetExpression"/>, and so will whatever does
    /// decide, when there is one (§8).</para>
    ///
    /// <para><b>Held by pawn id, not by figure</b> (§6): figures are pooled, and a colonist who walks
    /// off screen and back keeps her expression and her conversation.</para>
    /// </summary>
    public sealed partial class PawnFigureDirector
    {
        /// <summary>
        /// Off, no face moves and nobody nods. <b>A measurement control, not a setting</b> — the same
        /// footing as <see cref="Odyssey.Presentation.Rendering.ColonistAttachments.Enabled"/>: the
        /// face pass is judged against the same run with it off, and nothing in the game writes it.
        /// </summary>
        public static bool FacesEnabled { get; set; } = true;

        /// <summary>How far a debug conversation looks for somebody to talk to (design 59 §7).</summary>
        public const float TalkReach = 8f;

        readonly Dictionary<int, FaceExpression> _expressions = new Dictionary<int, FaceExpression>();
        readonly List<Conversation> _conversations = new List<Conversation>();
        FaceExpression _everyone = FaceExpression.Neutral;

        /// <summary>The expression every colonist holds who has not been given one of her own.</summary>
        public FaceExpression EveryoneExpression => _everyone;

        /// <summary>Every colonist holds <paramref name="expression"/>, and any held individually is forgotten.</summary>
        public void SetEveryoneExpression(FaceExpression expression)
        {
            _expressions.Clear();
            _everyone = expression;
        }

        /// <summary>One colonist holds <paramref name="expression"/> until given another.</summary>
        public void SetExpression(PawnId pawn, FaceExpression expression)
        {
            if (expression == _everyone) _expressions.Remove(pawn.Value);
            else _expressions[pawn.Value] = expression;
        }

        public FaceExpression ExpressionOf(PawnId pawn) =>
            _expressions.TryGetValue(pawn.Value, out FaceExpression e) ? e : _everyone;

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
            pawn.Kind == PawnKindLabels.ColonistKind && !pawn.Asleep && !pawn.IsDowned;

        void EndConversationsOf(int pawn)
        {
            for (int i = _conversations.Count - 1; i >= 0; i--)
                if (_conversations[i].Involves(pawn)) _conversations.RemoveAt(i);
        }

        /// <summary>
        /// Step the conversations by <paramref name="seconds"/>, ending any whose time is up or one
        /// of whose colonists has gone to sleep, been downed or left the frame.
        /// </summary>
        void StepConversations(float seconds)
        {
            for (int i = _conversations.Count - 1; i >= 0; i--)
            {
                Conversation talk = _conversations[i];
                bool alive = talk.Step(seconds) && StillTalking(talk.A) &&
                             (talk.B == Conversation.Nobody || StillTalking(talk.B));
                if (alive) _conversations[i] = talk;
                else _conversations.RemoveAt(i);
            }
        }

        bool StillTalking(int pawn) =>
            _frame == null || (_frame.TryGetPawn(new PawnId(pawn), out PawnView view) && CanTalk(in view));

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
        /// brows' lift and the eyes' openness read off the rig, the nod this frame, and where the
        /// gaze is. False when the pawn has no figure or the figure no face.
        /// </summary>
        public bool TryGetFace(int pawnId, out float browLift, out float eyeOpen, out float nodPitch,
            out GazePriority gaze)
        {
            browLift = 0f;
            eyeOpen = 1f;
            nodPitch = 0f;
            gaze = GazePriority.None;
            if (!_byPawn.TryGetValue(pawnId, out Figure? figure) || figure.FaceRig == null) return false;
            browLift = figure.FaceRig.MeasuredBrowLift;
            eyeOpen = figure.FaceRig.MeasuredEyeOpen;
            nodPitch = figure.Face.NodPitch;
            gaze = figure.Gaze.ActivePriority;
            return true;
        }

        /// <summary>
        /// Write every live figure's face and nod (§6). After the gaze, which sets the head
        /// absolutely; before the head is hidden, which shrinks the head as drawn.
        /// </summary>
        void ApplyFaces(float deltaTime, bool stepConversations)
        {
            if (!FacesEnabled) return;
            float seconds = Running ? deltaTime : 0f;
            if (stepConversations) StepConversations(seconds);

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
                    figure.FaceRig.Nod(figure.Head, figure.Face.NodPitch, figure.Face.NodRoll);
            }
        }
    }
}
