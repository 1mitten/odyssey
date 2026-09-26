#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.Ui;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using UnityEngine;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// Faces under the real player loop (design 59): the pieces are each tested alone, and this is
    /// the test that they are joined — that the figure director binds a live colonist's face,
    /// writes an expression onto her bones, nods a talker, turns two talkers to each other and
    /// blinks them, frame after frame, with nothing but the game driving it.
    ///
    /// <para><b>Needs the licensed packs</b>, and asks whether a colonist can be drawn rather than
    /// whether any art is here: the runner has animal art of the project's own.</para>
    /// </summary>
    public class FaceLiveTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        [UnityTest]
        public IEnumerator AnExpressionATalkAndABlinkReachALiveColonist()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
#if UNITY_EDITOR
                if (boot.moduleCatalogue == null)
                    boot.moduleCatalogue = UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
#endif
                if (boot.moduleCatalogue == null) Assert.Ignore("no catalogue on this machine");

                for (int i = 0; i < 8; i++) yield return null;
                shell.Menu.Choose(SessionCommands.NewGameKey);
                for (int i = 0; i < 10; i++) yield return null;
                Assert.That(shell.Menu.Start(), Is.True, "Start built no world");
                for (int i = 0; i < 20; i++) yield return null;

                PawnFigureDirector figures = boot.Figures!;
                if (!figures.CanDrawColonists) Assert.Ignore("the colonist rows resolved no art — the licensed packs are absent");

                var world = boot.World!;
                var colony = boot.Colony!;
                // This test counts conversations: only the ones it asks for.
                PawnFigureDirector.AmbientConversations = false;
                // A face holds still while the world is paused (§6), so the world must be running.
                world.Intents.Submit(new Intent(IntentKind.SetGameSpeed, default, SpeedControl.Normal));
                world.Tick();
                for (int i = 0; i < 10; i++) yield return null;

                int a = -1, b = -1;
                foreach (Pawn pawn in colony.Pawns.Pawns.All)
                {
                    if (!world.Views.Current.TryGetPawn(pawn.Id, out PawnView view) || !PawnFigureDirector.CanTalk(in view)) continue;
                    if (!figures.TryGetFace(pawn.Id.Value, out _, out _, out _, out _, out _, out _)) continue;
                    if (a < 0) a = pawn.Id.Value;
                    else if (b < 0) { b = pawn.Id.Value; break; }
                }
                Assert.That(a > 0 && b > 0, Is.True, "two awake colonists with faces on screen");

                // ---- the control: nothing has been asked of the face, so the brows are at rest.
                // Neutral forced, so the brows' rest is measured whatever her context would ask.
                figures.SetEveryoneExpression(FaceExpression.Neutral);
                yield return Wait(0.8f);
                figures.TryGetFace(a, out float restLift, out _, out _, out _, out _, out _);
                Assert.That(restLift, Is.EqualTo(0f).Within(1e-4f), "a neutral face is the art as painted");

                // ---- an expression reaches the bone.
                figures.SetEveryoneExpression(FaceExpression.Raised);
                yield return Wait(0.8f);
                figures.TryGetFace(a, out float raised, out _, out _, out _, out _, out _);
                Assert.That(raised, Is.EqualTo(FacePose.Of(FaceExpression.Raised).BrowLift).Within(0.001f),
                    "the brows sit where Raised puts them");
                // ---- context (design 59 §3a): drafted, her own face goes stern with nothing forced.
                figures.SetEveryoneExpression(null);
                world.Intents.Submit(new Intent(IntentKind.SetDrafted, default, a, 1));
                world.Tick();
                yield return Wait(0.8f);
                figures.TryGetFace(a, out float drafted, out _, out _, out _, out FaceExpression context, out _);
                Assert.That(context, Is.EqualTo(FaceExpression.Stern), "drafted, her context is stern");
                Assert.That(drafted, Is.EqualTo(FacePose.Of(FaceExpression.Stern).BrowLift).Within(0.001f),
                    "and her brows are where Stern puts them");
                world.Intents.Submit(new Intent(IntentKind.SetDrafted, default, a, 0));
                world.Tick();
                yield return Wait(0.3f);
                figures.SetEveryoneExpression(FaceExpression.Neutral);

                // ---- a talk: nods, the two looking at each other, and a blink along the way.
                figures.StartConversation(new PawnId(a), new PawnId(b), 20f);
                float mostNod = 0f, leastOpen = 1f;
                int free = 0, looking = 0;
                float until = Time.realtimeSinceStartup + 8f;
                while (Time.realtimeSinceStartup < until)
                {
                    yield return null;
                    foreach (int who in new[] { a, b })
                    {
                        if (!figures.TryGetFace(who, out _, out float open, out float nod, out GazePriority gaze, out _, out _)) continue;
                        mostNod = Mathf.Max(mostNod, nod);
                        leastOpen = Mathf.Min(leastOpen, open);
                        // Work, a ladder and sleep all pre-empt a conversation's gaze; count only
                        // the frames it was free to win.
                        if (!world.Views.Current.TryGetPawn(new PawnId(who), out PawnView view) || view.Working) continue;
                        free++;
                        if (gaze == GazePriority.Conversation) looking++;
                    }
                }
                Assert.That(figures.ConversationCount, Is.EqualTo(1), "the talk is still going");
                Assert.That(mostNod, Is.GreaterThan(1f), "somebody nodded");
                Assert.That(leastOpen, Is.LessThan(0.3f), "somebody blinked: every face does, every 2.2 to 6 s");
                if (free >= 30)
                    Assert.That(looking, Is.GreaterThan(free / 2), $"they looked at each other in {looking} of {free} free frames");
                else
                    TestContext.WriteLine($"only {free} frames free of work; the gaze was not judged");

                // ---- and it ends.
                figures.EndConversations();
                // A listener's nod under way finishes (half a second) rather than snapping off.
                yield return Wait(0.8f);
                figures.TryGetFace(a, out _, out _, out float after, out GazePriority afterGaze, out _, out _);
                Assert.That(after, Is.LessThan(0.2f), "the head settles");
                Assert.That(afterGaze, Is.Not.EqualTo(GazePriority.Conversation), "and looks elsewhere");
            }
            finally
            {
                PawnFigureDirector.FacesEnabled = true;
                PawnFigureDirector.AmbientConversations = true;
                Object.Destroy(root);
            }
        }

        static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }
    }
}
