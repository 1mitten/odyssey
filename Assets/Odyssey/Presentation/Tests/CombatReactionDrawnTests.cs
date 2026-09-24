#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using UnityEditor;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The reactions to a blow, on the figures (design 33 §9a): the owner's report — "when a person
    /// is hit there should be a reaction" — measured through the real path, a scripted
    /// <see cref="CombatEventView"/> stream handed on by <see cref="CombatFeedback"/> to
    /// <see cref="PawnFigureDirector"/>. The rules themselves (which reaction, which side, who has
    /// the layer, the slide's curve) are <see cref="CombatReactions"/>'s and are held in the fast
    /// tier; these hold the wiring.
    ///
    /// <para><b>Written without a Unity run.</b> Compiled with <c>dotnet</c> against the editor's
    /// module DLLs only. The negative control for the first — lane B's <c>React</c>, which returned
    /// while the figure's own swing was showing — is named and not yet seen to fail here; its
    /// fast-tier twin, <c>CombatReactionsTests.LaneBsRuleLeftMostBlowsUnseen</c>, is.</para>
    /// </summary>
    public class CombatReactionDrawnTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";
        static readonly GridSize Size = new GridSize(12, 12, 4);

        static ModuleCatalogue Catalogue()
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
            Assume.That(catalogue, Is.Not.Null, "the module catalogue asset is committed");
            return catalogue!;
        }

        static WorldSnapshot Frame(int tick, PawnView[] pawns, params CombatEventView[] events)
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(tick, Size, 0);
            foreach (PawnView pawn in pawns) snapshot.AddPawn(pawn);
            foreach (CombatEventView e in events) snapshot.AddCombatEvent(e);
            return snapshot;
        }

        static PawnView Fighter(int id, int x, int z, int facingX, byte serial, PawnGesture gesture = PawnGesture.None,
            PawnFlags flags = PawnFlags.Person) =>
            new PawnView(new PawnId(id), new CellRef(x, z, 0), 800, 800, 600, JobHandle.AttackMelee,
                working: true, workCell: new CellRef(facingX, z, 0), gesture: gesture, gestureSerial: serial, flags: flags);

        static PawnView Standing(int id, int x, int z, PawnFlags flags = PawnFlags.Person) =>
            new PawnView(new PawnId(id), new CellRef(x, z, 0), 800, 800, 600, JobHandle.Wait, flags: flags);

        static CombatEventView Event(int id, int tick, CombatEventKind kind, int attacker, int target, CellRef cell,
            int amount = 0) =>
            new CombatEventView(id, tick, kind, new PawnId(attacker), new PawnId(target), cell, amount);

        /// <summary>One frame as the bootstrap runs it: the figures, then the events since the last frame.</summary>
        static void Drive(PawnFigureDirector figures, CombatFeedback feedback, object world, WorldSnapshot frame,
            int frames = 1)
        {
            for (int i = 0; i < frames; i++)
            {
                figures.Sync(frame, 0, new SliceSettings(), 0f, 1, 1f / 60f);
                figures.Evaluate(1f / 60f);
                feedback.Consume(frame, world, figures, null);
            }
        }

        /// <summary>
        /// Two colonists squared up, the first one tick into her own thirty-tick swing (a punch:
        /// no weapon is published). <paramref name="swinging"/> and <paramref name="other"/> are the
        /// two as the frames that follow publish them.
        /// </summary>
        static void SquareUp(PawnFigureDirector figures, CombatFeedback feedback, object world,
            out PawnView swinging, out PawnView other)
        {
            PawnView her = Fighter(1, 3, 3, 4, serial: 4);
            other = Fighter(2, 4, 3, 3, serial: 1);
            Drive(figures, feedback, world, Frame(100, new[] { her, other }), 5);

            swinging = Fighter(1, 3, 3, 4, serial: 5, gesture: PawnGesture.Strike);
            Drive(figures, feedback, world, Frame(101, new[] { swinging, other },
                Event(1, 101, CombatEventKind.Swing, 1, 2, new CellRef(4, 3, 0), amount: 30)));
        }

        /// <summary>
        /// <b>The owner's report.</b> A colonist struck early in her own wind-up flinches: the
        /// reaction is taken on the frame the Hit is handed on and has the clip layer on the next,
        /// from the front, where her attacker stands. Lane B refused the react outright because her
        /// own swing was showing, which in a fight is most of the time.
        /// </summary>
        [Test]
        public void AColonistStruckInHerOwnWindUpFlinchesOnTheHitFrame()
        {
            var parent = new GameObject("figures");
            PawnFigureDirector? figures = null;
            try
            {
                figures = new PawnFigureDirector(Catalogue(), parent.transform, 0);
                if (!figures.CanDrawColonists) Assert.Ignore("no colonist art here: no figure to strike");
                var feedback = new CombatFeedback();
                object world = new object();
                var her = new PawnId(1);

                SquareUp(figures, feedback, world, out PawnView swinging, out PawnView other);
                Assert.That(figures.TryGetFight(her, out CombatRole action, out _, out _, out _), Is.True);
                Assert.That(action, Is.EqualTo(CombatRole.Punch), "the control: she is in her own swing");

                Drive(figures, feedback, world, Frame(107, new[] { swinging, other },
                    Event(1, 101, CombatEventKind.Swing, 1, 2, new CellRef(4, 3, 0), amount: 30),
                    Event(2, 107, CombatEventKind.Hit, 2, 1, new CellRef(3, 3, 0), amount: 8_000)));
                Assert.That(figures.TryGetReaction(her, out HitReaction reaction, out _, out _, out _), Is.True);
                Assert.That(reaction, Is.EqualTo(HitReaction.Flinch), "the blow is taken on the frame it is handed on");

                Drive(figures, feedback, world, Frame(108, new[] { swinging, other },
                    Event(2, 107, CombatEventKind.Hit, 2, 1, new CellRef(3, 3, 0), amount: 8_000)));
                figures.TryGetFight(her, out action, out float clipWeight, out _, out bool computed);
                Assert.That(action, Is.EqualTo(CombatRole.HitReact), "and it has the layer over her wind-up");
                if (figures.HasCombatClips)
                    Assert.That(clipWeight, Is.GreaterThan(0f), "the pack's hit react is easing in");
                else
                    Assert.That(computed, Is.True, "the computed flinch stands in");
            }
            finally
            {
                figures?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        /// <summary>
        /// Struck on the tick before her own blow lands, her swing keeps the layer — the simulation
        /// lands it whatever is drawn — and the flinch is laid over it instead.
        /// </summary>
        [Test]
        public void AHitRoundHerOwnImpactIsLaidOverTheSwing()
        {
            var parent = new GameObject("figures");
            PawnFigureDirector? figures = null;
            try
            {
                figures = new PawnFigureDirector(Catalogue(), parent.transform, 0);
                if (!figures.CanDrawColonists) Assert.Ignore("no colonist art here: no figure to strike");
                var feedback = new CombatFeedback();
                object world = new object();
                var her = new PawnId(1);

                SquareUp(figures, feedback, world, out PawnView swinging, out PawnView other);
                Drive(figures, feedback, world, Frame(129, new[] { swinging, other },
                    Event(2, 129, CombatEventKind.Hit, 2, 1, new CellRef(3, 3, 0), amount: 8_000)));
                Drive(figures, feedback, world, Frame(130, new[] { swinging, other }));

                figures.TryGetFight(her, out CombatRole action, out _, out _, out bool computed);
                Assert.That(action, Is.EqualTo(CombatRole.Punch), "her swing keeps the layer to land");
                Assert.That(figures.TryGetReaction(her, out HitReaction reaction, out _, out bool overlaid, out _), Is.True);
                Assert.That(reaction, Is.EqualTo(HitReaction.Flinch));
                Assert.That(overlaid, Is.True, "the flinch is laid over it");
                Assert.That(computed, Is.True);
            }
            finally
            {
                figures?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        /// <summary>A critical, published beside its hit on the same tick, turns the flinch into a stagger.</summary>
        [Test]
        public void ACriticalStaggers()
        {
            var parent = new GameObject("figures");
            PawnFigureDirector? figures = null;
            try
            {
                figures = new PawnFigureDirector(Catalogue(), parent.transform, 0);
                if (!figures.CanDrawColonists) Assert.Ignore("no colonist art here: no figure to strike");
                var feedback = new CombatFeedback();
                object world = new object();
                var her = new PawnId(1);
                PawnView[] pawns = { Standing(1, 3, 3), Standing(2, 4, 3) };

                Drive(figures, feedback, world, Frame(100, pawns), 5);
                Drive(figures, feedback, world, Frame(101, pawns,
                    Event(1, 101, CombatEventKind.Hit, 2, 1, new CellRef(3, 3, 0), amount: 8_000),
                    Event(2, 101, CombatEventKind.Critical, 2, 1, new CellRef(3, 3, 0))));
                Drive(figures, feedback, world, Frame(102, pawns));

                Assert.That(figures.TryGetReaction(her, out HitReaction reaction, out _, out _, out _), Is.True);
                Assert.That(reaction, Is.EqualTo(HitReaction.Stagger));
                figures.TryGetFight(her, out CombatRole action, out _, out _, out _);
                Assert.That(action, Is.EqualTo(CombatRole.Stagger));
            }
            finally
            {
                figures?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        /// <summary>
        /// Knocked back a tile: on the frame the event is read she is drawn where the blow found
        /// her, a few frames on she is between the two cells, and a quarter of a second on she is on
        /// the tile the simulation put her on — although the pawn was on the landing tile from the
        /// first frame. The flag is left off, so the lie does not move her; that is the next test.
        /// </summary>
        [Test]
        public void AKnockBackSlidesHerFromTheTileTheBlowFoundHerOn()
        {
            var parent = new GameObject("figures");
            PawnFigureDirector? figures = null;
            try
            {
                figures = new PawnFigureDirector(Catalogue(), parent.transform, 0);
                if (!figures.CanDrawColonists) Assert.Ignore("no colonist art here: no figure to knock back");
                var feedback = new CombatFeedback();
                object world = new object();
                var her = new PawnId(1);
                var from = new CellRef(3, 3, 0);
                var landing = new CellRef(2, 3, 0);

                Drive(figures, feedback, world, Frame(100, new[] { Standing(1, 3, 3) }), 5);
                Assert.That(figures.TryGetFeet(her, out Vector3 before), Is.True);

                PawnView landed = Standing(1, 2, 3);
                Drive(figures, feedback, world, Frame(101, new[] { landed },
                    Event(1, 101, CombatEventKind.KnockedBack, 2, 1, landing, amount: Size.Index(from))));
                Assert.That(figures.TryGetReaction(her, out _, out _, out _, out bool sliding), Is.True);
                Assert.That(sliding, Is.True);
                figures.TryGetFeet(her, out Vector3 atBlow);
                Assert.That(Flat(atBlow - before).magnitude, Is.LessThan(0.05f),
                    $"drawn where the blow found her on the frame it is read: {atBlow} against {before}");

                Drive(figures, feedback, world, Frame(102, new[] { landed }), 7);
                figures.TryGetFeet(her, out Vector3 midway);
                float x0 = CellMetrics.FloorCentre(from).x, x1 = CellMetrics.FloorCentre(landing).x;
                Assert.That(midway.x, Is.LessThan(Mathf.Max(x0, x1) - 0.1f).And.GreaterThan(Mathf.Min(x0, x1) + 0.1f),
                    $"between the two tiles a few frames on: {midway.x:F2} between {x0:F2} and {x1:F2}");

                Drive(figures, feedback, world, Frame(103, new[] { landed }), 20);
                figures.TryGetReaction(her, out _, out _, out _, out sliding);
                Assert.That(sliding, Is.False, "the slide is over");
                figures.TryGetFeet(her, out Vector3 after);
                Assert.That(Mathf.Abs(after.x - x1), Is.LessThan(0.1f), $"on the landing tile: {after.x:F2} against {x1:F2}");
            }
            finally
            {
                figures?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        /// <summary>
        /// Knocked down, she goes down on the knock-down clip (or the computed lie without the pack),
        /// is down while the flag is, gets up when it clears, and a swing begun while she is still
        /// getting up is drawn — the simulation has her on her feet and swinging.
        /// </summary>
        [Test]
        public void AKnockedDownColonistGoesDownAndASwingCutsHerGettingUpShort()
        {
            var parent = new GameObject("figures");
            PawnFigureDirector? figures = null;
            try
            {
                figures = new PawnFigureDirector(Catalogue(), parent.transform, 0);
                if (!figures.CanDrawColonists) Assert.Ignore("no colonist art here: no figure to knock down");
                var feedback = new CombatFeedback();
                object world = new object();
                var her = new PawnId(1);

                Drive(figures, feedback, world, Frame(100, new[] { Fighter(1, 3, 3, 4, serial: 4) }), 5);
                Assert.That(figures.TryGetHead(her, out Vector3 standing), Is.True);

                PawnView down = Fighter(1, 3, 3, 4, serial: 4, flags: PawnFlags.Person | PawnFlags.KnockedDown);
                Drive(figures, feedback, world, Frame(101, new[] { down }), 90);
                figures.TryGetFight(her, out CombatRole action, out float clipWeight, out _, out bool computed);
                Assert.That(action, Is.EqualTo(CombatRole.Downed), "knocked down is drawn as down");
                if (figures.HasCombatClips) Assert.That(clipWeight, Is.EqualTo(1f).Within(1e-3f), "on the knock-down clip");
                else Assert.That(computed, Is.True, "on the computed lie");
                Assert.That(figures.TryGetHead(her, out Vector3 lying), Is.True);
                Assert.That(lying.y, Is.LessThan(standing.y - 1.0f), $"the head came down: {standing.y:F2} to {lying.y:F2} m");

                Drive(figures, feedback, world, Frame(191, new[] { Fighter(1, 3, 3, 4, serial: 4) }));
                Drive(figures, feedback, world, Frame(192, new[] { Fighter(1, 3, 3, 4, serial: 5, gesture: PawnGesture.Strike) }));
                figures.TryGetFight(her, out action, out _, out _, out _);
                Assert.That(action, Is.EqualTo(CombatRole.Punch), "a swing cuts the get-up short");
            }
            finally
            {
                figures?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        /// <summary>A hog struck flinches too, computed: it has no clips, and its whole body jolts.</summary>
        [Test]
        public void AnAnimalStruckFlinchesOnTheComputedPose()
        {
            var parent = new GameObject("figures");
            PawnFigureDirector? figures = null;
            try
            {
                figures = new PawnFigureDirector(Catalogue(), parent.transform, 0);
                var feedback = new CombatFeedback();
                object world = new object();
                var hog = new PawnId(1);
                PawnView[] pawns =
                {
                    new PawnView(hog, new CellRef(3, 3, 0), 800, 800, 600, JobHandle.Wait, kind: 1, flags: PawnFlags.None),
                };

                Drive(figures, feedback, world, Frame(100, pawns), 5);
                Assume.That(figures.TryGetFight(hog, out _, out _, out _, out _), Is.True, "the hog's row resolves");

                Drive(figures, feedback, world, Frame(101, pawns,
                    Event(1, 101, CombatEventKind.Hit, 2, 1, new CellRef(3, 3, 0), amount: 6_000)));
                Drive(figures, feedback, world, Frame(102, pawns), 2);

                figures.TryGetFight(hog, out CombatRole action, out float clipWeight, out _, out bool computed);
                Assert.That(action, Is.EqualTo(CombatRole.HitReact));
                Assert.That(clipWeight, Is.Zero, "an animal has no clip layer");
                Assert.That(computed, Is.True, "its flinch is computed");
            }
            finally
            {
                figures?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);
    }
}
