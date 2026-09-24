#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Audio;
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
    /// The fight, drawn (design 33 §6B, lane B): the Sword Combat clip rows and their computed
    /// fallbacks, a swing's impact landing on its wind-up tick, the one reader of the combat
    /// events, the corpse's face, the attacker's missing work stroke and the cursor round a
    /// selected corpse.
    ///
    /// <para><b>Built against the contract, not the fight.</b> Every event here is scripted
    /// (<see cref="CombatEventView"/> written straight into a snapshot), so nothing waits on the
    /// simulation lane. The tests that need a colonist's art ask whether it <em>resolved</em>
    /// (<see cref="PawnFigureDirector.CanDrawColonists"/>, <see cref="PawnFigureDirector.HasCombatClips"/>)
    /// and ignore themselves on the runner, which has no <c>Assets/Synty</c>; everything else runs
    /// there too.</para>
    /// </summary>
    public class CombatDrawnTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";
        const string SwordCombatPolygon = "Assets/Synty/AnimationSwordCombat/Animations/Polygon";

        static ModuleCatalogue Catalogue()
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
            Assume.That(catalogue, Is.Not.Null, "the module catalogue asset is committed");
            return catalogue!;
        }

        static WorldSnapshot Frame(int tick, params PawnView[] pawns)
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(tick, new GridSize(12, 12, 4), 0);
            foreach (PawnView pawn in pawns) snapshot.AddPawn(pawn);
            return snapshot;
        }

        static PawnView Colonist(int id, int x, int z, int job = JobHandle.Wait, bool working = false,
            CellRef workCell = default, PawnGesture gesture = PawnGesture.None, byte serial = 0,
            PawnFlags flags = PawnFlags.Person) =>
            new PawnView(new PawnId(id), new CellRef(x, z, 0), 800, 800, 600, job,
                working: working, workCell: workCell, gesture: gesture, gestureSerial: serial, flags: flags);

        // ---- The rows ----------------------------------------------------------------------------

        /// <summary>
        /// Every combat role has its row in the committed catalogue, and every clip a row resolved
        /// is one the owner allowed: the Polygon set, in place, not a return-to-idle, Humanoid, and
        /// never <c>Dodge_R</c>. A swing's clip carries a measured impact inside its own length.
        /// Where a row resolved nothing — the runner, which has no pack — its role has a computed
        /// pose and the director hands out no clip for it.
        /// </summary>
        [Test]
        public void EveryCombatRowResolvesToAllowedClipsOrFallsBackToTheComputedPose()
        {
            ModuleCatalogue catalogue = Catalogue();
            var parent = new GameObject("figures");
            PawnFigureDirector? director = null;
            try
            {
                director = new PawnFigureDirector(catalogue, parent.transform, 0);
                int resolved = 0;

                for (int i = 0; i < ModuleIds.CombatRows.Length; i++)
                {
                    string id = ModuleIds.CombatRows[i];
                    var role = (CombatRole)(i + 1);
                    Assert.That(CombatPose.RowOf(role), Is.EqualTo(id), $"role {role} reads row {id}");

                    ModuleEntry? row = catalogue.Find(id);
                    Assert.That(row, Is.Not.Null, $"{id} is in the committed catalogue");
                    Assert.That(row!.combat, Is.Not.Empty, $"{id} names its clips");

                    int rowResolved = 0;
                    foreach (CombatClipEntry entry in row.combat)
                    {
                        Assert.That(entry.clipName, Does.Not.Contain("RootMotion"), entry.clipName);
                        Assert.That(entry.clipName, Does.Not.Contain("ReturnToIdle"), entry.clipName);
                        Assert.That(entry.clipName, Does.Not.Contain("Dodge_R"), "Dodge_R imports Generic");
                        if (entry.clip == null)
                        {
                            // With the pack here, a clip that did not resolve is a row that lost
                            // part of its role without anything saying so.
                            Assert.That(System.IO.Directory.Exists(SwordCombatPolygon), Is.False,
                                $"the pack is here and {entry.clipName} did not resolve");
                            continue;
                        }

                        rowResolved++;
                        string path = AssetDatabase.GetAssetPath(entry.clip);
                        Assert.That(path, Does.StartWith(SwordCombatPolygon), $"{entry.clipName} is a Polygon clip");
                        Assert.That(System.IO.Path.GetFileNameWithoutExtension(path), Is.EqualTo(entry.clipName),
                            "the file the row names");
                        Assert.That(entry.clip.name, Does.Not.EndWith("_WindUp_Sword")
                            .And.Not.EndWith("_Hit_Sword").And.Not.EndWith("_FollowThrough_Sword"),
                            $"{entry.clipName} is the whole clip, not a sub-clip");
                        Assert.That(entry.clip.humanMotion, Is.True, $"{entry.clipName} drives a Humanoid rig");

                        bool swing = role == CombatRole.SwingLight || role == CombatRole.SwingHeavy;
                        if (swing)
                            Assert.That(entry.impactSeconds, Is.GreaterThan(0f).And.LessThan(entry.clip.length),
                                $"{entry.clipName}'s blow is measured inside it");
                        else
                            Assert.That(entry.impactSeconds, Is.Zero, $"{entry.clipName} is not a blow");
                    }

                    resolved += rowResolved;
                    if (rowResolved == 0)
                    {
                        Assert.That(CombatPose.HasFallback(role), Is.True, $"{role} computes without its clips");
                        Assert.That(director.CombatClip(role, CombatVariant.Front), Is.Null,
                            $"with no clip resolved, {role} is handed no clip and computes");
                    }
                    else
                    {
                        Assert.That(director.CombatClip(role, row.combat[0].variant), Is.Not.Null);
                    }
                }

                // The two computed-only roles are never handed a clip, pack or no pack.
                Assert.That(director.CombatClip(CombatRole.Punch, "A"), Is.Null);
                Assert.That(director.CombatClip(CombatRole.Bite, "A"), Is.Null);
                Assert.That(director.HasCombatClips, Is.EqualTo(resolved > 0),
                    "the director's question and the rows agree about whether the pack is here");
            }
            finally
            {
                director?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        /// <summary>
        /// With the pack absent — the rows stripped of their clips, as on the runner — a downed
        /// colonist still lies down, on the computed lie, and a stunned one still sways. The pack
        /// absent is a fight drawn differently, never a fight not drawn.
        /// </summary>
        [Test]
        public void WithThePackAbsentADownedColonistLiesOnTheComputedPose()
        {
            ModuleCatalogue stripped = Object.Instantiate(Catalogue());
            foreach (string id in ModuleIds.CombatRows)
                foreach (CombatClipEntry entry in stripped.Find(id)?.combat ?? new List<CombatClipEntry>())
                    entry.clip = null;

            var parent = new GameObject("figures");
            PawnFigureDirector? director = null;
            try
            {
                director = new PawnFigureDirector(stripped, parent.transform, 0);
                if (!director.CanDrawColonists) Assert.Ignore("no colonist art here: nothing to lay down");
                Assert.That(director.HasCombatClips, Is.False, "the stripped rows hand out nothing");

                var id = new PawnId(1);
                Step(director, Frame(100, Colonist(1, 3, 3)), 5);
                Assert.That(director.TryGetHead(id, out Vector3 standing), Is.True);

                Step(director, Frame(101, Colonist(1, 3, 3, flags: PawnFlags.Person | PawnFlags.Downed)), 120);
                Assert.That(director.TryGetFight(id, out _, out float clipWeight, out _, out bool computed), Is.True);
                Assert.That(clipWeight, Is.Zero, "no clip layer without the pack");
                Assert.That(computed, Is.True, "the computed lie stands in");
                Assert.That(director.TryGetHead(id, out Vector3 lying), Is.True);
                Assert.That(lying.y, Is.LessThan(standing.y - 1.0f),
                    $"the head came down from {standing.y:F2} m to {lying.y:F2} m");
            }
            finally
            {
                director?.Dispose();
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(stripped);
            }
        }

        /// <summary>
        /// With the pack, a downed colonist is knocked down by the clip itself: the action layer is
        /// showing and the head is on the ground. The one test that the pack's Humanoid clips
        /// really drive our retargeted bodies, rather than binding nothing and standing still.
        /// </summary>
        [Test]
        public void WithThePackADownedColonistIsKnockedDownByTheClip()
        {
            var parent = new GameObject("figures");
            PawnFigureDirector? director = null;
            try
            {
                director = new PawnFigureDirector(Catalogue(), parent.transform, 0);
                if (!director.CanDrawColonists || !director.HasCombatClips)
                    Assert.Ignore("the Sword Combat pack or the colonist art is not here");

                var id = new PawnId(1);
                Step(director, Frame(100, Colonist(1, 3, 3)), 5);
                Assert.That(director.TryGetHead(id, out Vector3 standing), Is.True);

                Step(director, Frame(101, Colonist(1, 3, 3, flags: PawnFlags.Person | PawnFlags.Downed)), 150);
                Assert.That(director.TryGetFight(id, out _, out float clipWeight, out _, out bool computed), Is.True);
                Assert.That(clipWeight, Is.EqualTo(1f).Within(1e-3f), "the knock-down clip is showing whole");
                Assert.That(computed, Is.False, "and the computed lie is not also laid on");
                Assert.That(director.TryGetHead(id, out Vector3 lying), Is.True);
                Assert.That(lying.y, Is.LessThan(standing.y - 1.0f),
                    $"the clip put the head on the ground: {standing.y:F2} m to {lying.y:F2} m");
            }
            finally
            {
                director?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        static void Step(PawnFigureDirector director, WorldSnapshot frame, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                director.Sync(frame, 0, new SliceSettings(), 0f, 1, 1f / 60f);
                director.Evaluate(1f / 60f);
            }
        }

        // ---- The swing's timing -------------------------------------------------------------------

        /// <summary>
        /// A clip's measured impact lands on the attack's wind-up tick, whatever the wind-up: the
        /// clip is scaled by <c>impactSeconds / windupTicks</c>, not played at its authored rate.
        /// The four numbers are the owner's weapons' wind-ups (design 33 §5b) and the pack's three
        /// swing impacts (<c>synty-sword-combat.md</c>).
        /// </summary>
        [Test]
        public void AClipsImpactLandsOnTheWindupTick()
        {
            int[] windups = { 18, 22, 26, 30, 36 };
            float[] impacts = { 10f / 30f, 29f / 30f, 26f / 30f };
            foreach (int windup in windups)
            foreach (float impact in impacts)
            {
                Assert.That(CombatPose.ClipTime(windup, windup, impact), Is.EqualTo(impact).Within(1e-5f),
                    $"wind-up {windup}, impact {impact:F3} s: the blow is on the tick");
                Assert.That(CombatPose.ClipTime(windup * 0.5f, windup, impact), Is.EqualTo(impact * 0.5f).Within(1e-5f),
                    "and the wind-up is spread evenly up to it");
                Assert.That(CombatPose.ClipTime(0f, windup, impact), Is.Zero, "a swing starts at the clip's start");
            }

            // With the pack, the same of the real rows: every swing clip's blow, scaled, is on the tick.
            ModuleCatalogue catalogue = Catalogue();
            foreach (string id in new[] { ModuleIds.CombatSwingLight, ModuleIds.CombatSwingHeavy })
            foreach (CombatClipEntry entry in catalogue.Find(id)?.combat ?? new List<CombatClipEntry>())
            {
                if (entry.clip == null) continue;
                Assert.That(CombatPose.ClipTime(24, 24, entry.impactSeconds),
                    Is.EqualTo(entry.impactSeconds).Within(1e-5f), entry.clipName);
            }
        }

        /// <summary>
        /// A computed swing lands on the wind-up tick too, stepped a frame at a time at an awkward
        /// part-tick: the frame the blow lands is the first frame at or past the wind-up, never a
        /// frame early and never more than a frame late — and the pose at the blow is the blow.
        /// </summary>
        [Test]
        public void AComputedSwingLandsOnTheWindupTick()
        {
            foreach (CombatRole role in new[] { CombatRole.Punch, CombatRole.SwingLight, CombatRole.SwingHeavy, CombatRole.Bite })
            foreach (int windup in new[] { 15, 18, 30 })
            {
                const float perFrame = 0.37f;   // ticks a frame: speed one at about 160 frames a second
                float before = 0f;
                float landedAt = -1f;
                for (float elapsed = perFrame; elapsed < windup * 3f; elapsed += perFrame)
                {
                    float phase = CombatPose.Phase(elapsed, windup);
                    if (CombatPose.Lands(CombatPose.Phase(before, windup), phase))
                    {
                        landedAt = elapsed;
                        break;
                    }
                    before = elapsed;
                }

                Assert.That(landedAt, Is.GreaterThanOrEqualTo(windup).And.LessThan(windup + perFrame),
                    $"{role}, wind-up {windup}: landed at {landedAt:F2} ticks");

                CombatShape blow = CombatPose.Swing(role, 1f);
                CombatShape early = CombatPose.Swing(role, 0.5f);
                Assert.That(blow.Lunge, Is.GreaterThan(early.Lunge), $"{role} is furthest into the blow at the impact");
                Assert.That(CombatPose.Swing(role, 1f + CombatPose.RecoverFraction).IsRest, Is.True,
                    $"{role} is back at rest when it is over");
            }
        }

        // ---- The events ---------------------------------------------------------------------------

        static WorldSnapshot Events(params int[] ids)
        {
            WorldSnapshot snapshot = Frame(500);
            foreach (int id in ids)
                snapshot.AddCombatEvent(new CombatEventView(id, 400 + id, CombatEventKind.Miss,
                    new PawnId(1), new PawnId(2), new CellRef(1, 1, 0)));
            return snapshot;
        }

        /// <summary>
        /// The one reader hands each event on exactly once, in order, and a world it has not seen
        /// is taken as it is — its tail marked handled and nothing replayed — however many events
        /// that world's ring already holds, and even though the new world's ids start again below
        /// the old watermark.
        /// </summary>
        [Test]
        public void CombatFeedbackHandsEachEventOnOnceAndReplaysNoneAfterAWorldChange()
        {
            var feedback = new CombatFeedback();
            var handed = new List<int>();
            feedback.Handed += e => handed.Add(e.Id);

            object worldA = new object();
            feedback.Consume(Events(1, 2, 3), worldA, null, null);
            Assert.That(handed, Is.Empty, "a world seen for the first time replays nothing");
            Assert.That(feedback.Watermark, Is.EqualTo(3));

            feedback.Consume(Events(1, 2, 3, 4, 5), worldA, null, null);
            Assert.That(handed, Is.EqualTo(new[] { 4, 5 }), "the new ones, in order");

            feedback.Consume(Events(1, 2, 3, 4, 5), worldA, null, null);
            Assert.That(handed, Is.EqualTo(new[] { 4, 5 }), "and each only once");

            object worldB = new object();
            feedback.Consume(Events(1, 2), worldB, null, null);
            Assert.That(handed, Is.EqualTo(new[] { 4, 5 }), "a load mid-fight replays none of its tail");
            Assert.That(feedback.Watermark, Is.EqualTo(2), "the watermark is the new world's");

            feedback.Consume(Events(1, 2, 3), worldB, null, null);
            Assert.That(handed, Is.EqualTo(new[] { 4, 5, 3 }),
                "and the new world's next event is handed on although its id is below the old watermark");
        }

        [Test]
        public void EveryMomentOfAFightHasItsSoundOrNone()
        {
            // A swing's sound is scheduled against its blow, not played on its frame (design 33 §9g).
            Assert.That(SoundIds.ForCombat(CombatEventKind.Swing), Is.Null, "a swing is timed, not played on its frame");
            Assert.That(SoundIds.ForCombat(CombatEventKind.SwingCritical), Is.Null);
            Assert.That(SoundIds.ForCue(Odyssey.Hud.CombatCue.Whoosh), Is.EqualTo(SoundIds.CombatSwing));
            Assert.That(SoundIds.ForCue(Odyssey.Hud.CombatCue.Slice), Is.EqualTo(SoundIds.CombatCritSlice));
            Assert.That(SoundIds.ForCue(Odyssey.Hud.CombatCue.Thud), Is.EqualTo(SoundIds.CombatHit));
            Assert.That(SoundIds.ForCombat(CombatEventKind.Hit), Is.EqualTo(SoundIds.CombatHit));
            Assert.That(SoundIds.ForCombat(CombatEventKind.Miss), Is.EqualTo(SoundIds.CombatMiss));
            Assert.That(SoundIds.ForCombat(CombatEventKind.Dodge), Is.EqualTo(SoundIds.CombatMiss));
            Assert.That(SoundIds.ForCombat(CombatEventKind.Downed), Is.EqualTo(SoundIds.CombatDown));
            Assert.That(SoundIds.ForCombat(CombatEventKind.Died), Is.EqualTo(SoundIds.CombatDeath));
            Assert.That(SoundIds.ForCombat(CombatEventKind.Stun), Is.Null, "the blow that stunned is already heard");
            Assert.That(SoundIds.ForCombat(CombatEventKind.Recovered), Is.Null);
        }

        // ---- The work stroke ----------------------------------------------------------------------

        /// <summary>
        /// The attack driver reports a work focus through its wind-up, so the figure turns to its
        /// target — and the axe's stroke never plays for it (design 33 §5j). Felling still strokes.
        /// </summary>
        [Test]
        public void AnAttackingFigurePlaysNoWorkStroke()
        {
            PawnView attacking = Colonist(1, 3, 3, JobHandle.AttackMelee, working: true, workCell: new CellRef(4, 3, 0));
            PawnView felling = Colonist(1, 3, 3, JobHandle.Fell, working: true, workCell: new CellRef(4, 3, 0));
            Assert.That(PawnFigureDirector.PlaysWorkStroke(in attacking), Is.False);
            Assert.That(PawnFigureDirector.PlaysWorkStroke(in felling), Is.True);

            var parent = new GameObject("figures");
            PawnFigureDirector? director = null;
            try
            {
                director = new PawnFigureDirector(Catalogue(), parent.transform, 0);
                if (!director.CanDrawColonists) Assert.Ignore("no colonist art here: no figure to swing");

                Step(director, Frame(100, attacking), 60);
                Assert.That(director.TryGetFight(new PawnId(1), out _, out _, out float work, out _), Is.True);
                Assert.That(work, Is.Zero, "a second of wind-up focus put no stroke on the figure");

                Step(director, Frame(100, felling), 60);
                director.TryGetFight(new PawnId(1), out _, out _, out work, out _);
                Assert.That(work, Is.GreaterThan(0.9f), "the control: the same focus on a felling job strokes");
            }
            finally
            {
                director?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        /// <summary>
        /// A strike on the gesture serial starts a swing, not the lift's crouch every other gesture
        /// is drawn as; the Swing event refines its timing without starting a second one.
        /// </summary>
        [Test]
        public void AStrikeStartsASwingAndItsEventRefinesIt()
        {
            var parent = new GameObject("figures");
            PawnFigureDirector? director = null;
            try
            {
                director = new PawnFigureDirector(Catalogue(), parent.transform, 0);
                if (!director.CanDrawColonists) Assert.Ignore("no colonist art here");
                var id = new PawnId(1);

                Step(director, Frame(100, Colonist(1, 3, 3, serial: 4)), 2);
                WorldSnapshot strike = Frame(101, Colonist(1, 3, 3, JobHandle.AttackMelee, working: true,
                    workCell: new CellRef(4, 3, 0), gesture: PawnGesture.Strike, serial: 5));
                Step(director, strike, 1);

                Assert.That(director.TryGetFight(id, out CombatRole action, out _, out _, out _), Is.True);
                Assert.That(action, Is.EqualTo(CombatRole.Punch), "bare hands punch");

                director.OnCombatEvent(new CombatEventView(1, 101, CombatEventKind.Swing, id, new PawnId(2),
                    new CellRef(4, 3, 0), amount: 18));
                // Nine ticks into an eighteen-tick wind-up, on the same gesture serial.
                Step(director, Frame(110, Colonist(1, 3, 3, JobHandle.AttackMelee, working: true,
                    workCell: new CellRef(4, 3, 0), gesture: PawnGesture.Strike, serial: 5)), 1);
                director.TryGetFight(id, out action, out _, out _, out bool computed);
                Assert.That(action, Is.EqualTo(CombatRole.Punch), "one swing, refined");
                Assert.That(computed, Is.True, "the punch is always computed");
                Assert.That(director.CrouchedFigures, Is.Zero, "and no lift crouch was drawn for the strike");
            }
            finally
            {
                director?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        // ---- The dead -----------------------------------------------------------------------------

        /// <summary>
        /// A corpse wears the face the book deals its <b>own seed</b>: the frame no longer carries
        /// the dead pawn, so a seed read off the frame would be nought and deal somebody else.
        /// </summary>
        [Test]
        public void ACorpseWearsTheFaceItsRollSeedDeals()
        {
            ModuleCatalogue catalogue = Catalogue();
            var parent = new GameObject("figures");
            PawnFigureDirector? director = null;
            try
            {
                director = new PawnFigureDirector(catalogue, parent.transform, 0);
                ColonistAppearanceBook book = director.Appearances;
                Assume.That(director.LookCount, Is.GreaterThan(1), "the catalogue has a cast to deal from");

                // A seed whose face differs from the fallback's, so the test can tell them apart.
                const int pawn = 5;
                uint seed = 0u;
                for (uint s = 1u; s < 400u && seed == 0u; s++)
                    if (book.LookFor(pawn, s) != book.LookFor(pawn, 0u)) seed = s;
                Assume.That(seed, Is.Not.Zero, "some seed deals a different face");

                var corpse = new CorpseView(7, new PawnId(pawn), 0, seed, new CellRef(3, 3, 0), 100, 2, PawnFlags.Person);
                Assert.That(director.LookForCorpse(corpse), Is.EqualTo(book.LookFor(pawn, seed)));
                Assert.That(director.LookForCorpse(corpse), Is.Not.EqualTo(book.LookFor(pawn, 0u)));

                if (!director.CanDrawColonists) return;
                PawnFigureDirector.CorpseLoan? loan = director.BorrowForCorpse(corpse);
                Assert.That(loan, Is.Not.Null);
                Assert.That(loan!.Look, Is.EqualTo(book.LookFor(pawn, seed)), "the body lent out is that face");
                director.ReturnCorpse(loan);
            }
            finally
            {
                director?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        /// <summary>
        /// A corpse is drawn and a corpse gone is gone; the dead do not hold a live figure. With no
        /// art, a capsule lies where it fell; with art, a baked body — and the figure that was lent
        /// to lay it down is back in the pool.
        /// </summary>
        [Test]
        public void ACorpseIsDrawnLyingAndItsFigureGoesBackToThePool()
        {
            var world = new RenderTestWorld(8, 8, 3);
            var parent = new GameObject("corpses");
            PawnFigureDirector? figures = null;
            CorpseDirector? corpses = null;
            try
            {
                figures = new PawnFigureDirector(Catalogue(), parent.transform, 0);
                corpses = new CorpseDirector(world.Model, null, figures, parent.transform, 0);

                WorldSnapshot frame = Frame(5_000);
                frame.AddCorpse(new CorpseView(3, new PawnId(9), 0, 77u, new CellRef(2, 2, 0), 100, 2, PawnFlags.Person));
                corpses.Sync(frame, 0, new SliceSettings(), 1f / 60f);

                Assert.That(corpses.Count, Is.EqualTo(1));
                Assert.That(corpses.Falling, Is.Zero, "an old corpse is found lying, not seen falling");
                Assert.That(corpses.TryGetBox(3, out Bounds box), Is.True);
                Assert.That(box.size.y, Is.LessThan(1.2f), $"it lies down: {box.size}");
                Assert.That(Mathf.Max(box.size.x, box.size.z), Is.GreaterThan(1.0f), "along the ground");
                Assert.That(figures.FigureCount, Is.Zero, "no live figure is held for the dead");
                if (!figures.CanDrawColonists) Assert.That(corpses.Markers, Is.EqualTo(1), "no art: the capsule");

                corpses.Sync(Frame(5_001), 0, new SliceSettings(), 1f / 60f);
                Assert.That(corpses.Count, Is.Zero, "a corpse the frame no longer carries is gone");
            }
            finally
            {
                corpses?.Dispose();
                figures?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        /// <summary>
        /// The cursor brackets a selected corpse (design 33 §5f), round the body and not its cell,
        /// and brackets nothing for a corpse that is not selected. Selected directly through
        /// <see cref="SelectionDirector.ChooseCorpse"/>, because the click's own choice is lane C's
        /// and answers no until it merges.
        /// </summary>
        [Test]
        public void TheCursorBracketsASelectedCorpse()
        {
            var world = new RenderTestWorld(8, 8, 3);
            var parent = new GameObject("corpses");
            CorpseDirector? corpses = null;
            try
            {
                corpses = new CorpseDirector(world.Model, null, null, parent.transform, 0);
                WorldSnapshot frame = Frame(5_000);
                frame.AddCorpse(new CorpseView(4, new PawnId(9), 0, 77u, new CellRef(5, 2, 0), 100, 0, PawnFlags.Person));
                corpses.Sync(frame, 0, new SliceSettings(), 0f);

                var selection = new SelectionDirector();
                Assert.That(corpses.TryBracket(selection, out _, out _), Is.False, "nothing selected, nothing bracketed");

                selection.ChooseCorpse(4);
                Assert.That(corpses.TryBracket(selection, out Matrix4x4 place, out Vector3 size), Is.True);
                Vector3 cell = CellMetrics.FloorCentre(new CellRef(5, 2, 0));
                Vector3 at = place.GetPosition();
                Assert.That(new Vector2(at.x - cell.x, at.z - cell.z).magnitude, Is.LessThan(0.5f),
                    $"bracketed where it lies: {at} against the cell at {cell}");
                Assert.That(corpses.TryGetBox(4, out Bounds box), Is.True);
                Assert.That(size.x, Is.GreaterThan(box.size.x).And.LessThan(box.size.x + 1f), "round the body, with a margin");
                Assert.That(size.y, Is.LessThan(CellMetrics.SizeY), "a body's height, not the cell's");

                selection.ChooseCorpse(99);
                Assert.That(corpses.TryBracket(selection, out _, out _), Is.False, "a corpse that is not drawn is not bracketed");
            }
            finally
            {
                corpses?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void ACorpseUnderTheRayIsFoundAndOneBesideItIsNot()
        {
            var world = new RenderTestWorld(8, 8, 3);
            var parent = new GameObject("corpses");
            CorpseDirector? corpses = null;
            try
            {
                corpses = new CorpseDirector(world.Model, null, null, parent.transform, 0);
                WorldSnapshot frame = Frame(5_000);
                frame.AddCorpse(new CorpseView(4, new PawnId(9), 0, 77u, new CellRef(5, 2, 0), 100, 0, PawnFlags.Person));
                corpses.Sync(frame, 0, new SliceSettings(), 0f);

                Vector3 cell = GroundRelief.Lift(CellMetrics.FloorCentre(new CellRef(5, 2, 0)));
                var down = new Ray(cell + Vector3.up * 20f, Vector3.down);
                Assert.That(corpses.CorpseUnderRay(down, 0), Is.EqualTo(4));

                var beside = new Ray(cell + new Vector3(CellMetrics.SizeXZ * 2f, 20f, 0f), Vector3.down);
                Assert.That(corpses.CorpseUnderRay(beside, 0), Is.Zero);
            }
            finally
            {
                corpses?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        // ---- The pure parts -----------------------------------------------------------------------

        [Test]
        public void ABlowIsDrawnFromTheSideItCameFromAndADodgeNeverGoesRight()
        {
            Vector3 north = Vector3.forward;
            Assert.That(CombatPose.SideOf(north, Vector3.forward), Is.EqualTo(BlowSide.Front));
            Assert.That(CombatPose.SideOf(north, Vector3.back), Is.EqualTo(BlowSide.Back));
            Assert.That(CombatPose.SideOf(north, Vector3.left), Is.EqualTo(BlowSide.Left));
            Assert.That(CombatPose.SideOf(north, Vector3.right), Is.EqualTo(BlowSide.Right));
            Assert.That(CombatPose.SideOf(north, Vector3.zero), Is.EqualTo(BlowSide.Front), "no direction is the front");

            foreach (BlowSide side in new[] { BlowSide.Front, BlowSide.Back, BlowSide.Left, BlowSide.Right })
                Assert.That(CombatPose.DodgeVariant(side), Is.Not.EqualTo(CombatVariant.Right),
                    "Dodge_R imports Generic and is never asked for");
            Assert.That(CombatPose.DodgeVariant(BlowSide.Front), Is.EqualTo(CombatVariant.Back), "away from the blow");
        }

        [Test]
        public void TheSwingFamilyComesFromTheWeaponElseTheBody()
        {
            var styles = new AttackStyle?[] { null, AttackStyle.Light, AttackStyle.Heavy };
            Assert.That(CombatPose.StyleFor(1, styles, isPerson: true), Is.EqualTo(AttackStyle.Light));
            Assert.That(CombatPose.StyleFor(2, styles, isPerson: true), Is.EqualTo(AttackStyle.Heavy));
            Assert.That(CombatPose.StyleFor(0, styles, isPerson: true), Is.EqualTo(AttackStyle.Fists), "an item that is not a weapon");
            Assert.That(CombatPose.StyleFor(-1, styles, isPerson: true), Is.EqualTo(AttackStyle.Fists), "bare hands");
            Assert.That(CombatPose.StyleFor(-1, styles, isPerson: false), Is.EqualTo(AttackStyle.Bite), "an animal bites");
            Assert.That(CombatPose.SwingRole(AttackStyle.Light), Is.EqualTo(CombatRole.SwingLight));
            Assert.That(CombatPose.SwingRole(AttackStyle.Heavy), Is.EqualTo(CombatRole.SwingHeavy));
            Assert.That(CombatPose.SwingRole(AttackStyle.Fists), Is.EqualTo(CombatRole.Punch));
            Assert.That(CombatPose.SwingRole(AttackStyle.Bite), Is.EqualTo(CombatRole.Bite));
        }

        [Test]
        public void EveryComputedReactionMovesAndSettles()
        {
            foreach (CombatRole role in new[] { CombatRole.HitReact, CombatRole.Stagger, CombatRole.Dodge })
            foreach (BlowSide side in new[] { BlowSide.Front, BlowSide.Back, BlowSide.Left, BlowSide.Right })
            {
                float span = CombatPose.ReactionSeconds(role);
                Assert.That(CombatPose.Reaction(role, side, span * 0.2f).IsRest, Is.False, $"{role} from {side} moves");
                Assert.That(CombatPose.Reaction(role, side, span).IsRest, Is.True, $"{role} from {side} settles");
            }
            Assert.That(CombatPose.Stunned(0.3f).IsRest, Is.False, "a stun sways");
        }

        [Test]
        public void ABodyLaidFlatLiesAlongTheWayItFellFaceUp()
        {
            Vector3 floor = new Vector3(10f, 3f, 10f);
            CombatPose.LieFlat(floor, 90f, 2.4f, animal: false, out Vector3 root, out Quaternion rotation);
            Vector3 head = root + rotation * Vector3.up * 2.4f;
            Assert.That(head.x - root.x, Is.EqualTo(2.4f).Within(1e-3f), "head towards +X, the way it fell");
            Assert.That((root + head).x * 0.5f, Is.EqualTo(floor.x).Within(1e-3f), "its middle on the cell");
            Assert.That((rotation * Vector3.forward).y, Is.EqualTo(1f).Within(1e-3f), "face to the sky");
            Assert.That(root.y, Is.GreaterThan(floor.y), "its back on the floor, not through it");
        }

        [Test]
        public void TheFloatingWordsRiseFadeAndAreBounded()
        {
            var floaters = new CombatFloaters();
            floaters.Add(string.Empty, HudTheme.Bad, Vector3.zero);
            Assert.That(floaters.Alive, Is.Empty, "an empty word floats nothing");

            floaters.Add("7", HudTheme.Bad, Vector3.zero);
            floaters.Step(CombatFloaters.Seconds * 0.5f);
            Assert.That(floaters.Alive[0].At.y, Is.GreaterThan(0f), "it rises");
            Assert.That(floaters.Alive[0].Alpha, Is.EqualTo(1f), "whole in the first half");
            floaters.Step(CombatFloaters.Seconds * 0.3f);
            Assert.That(floaters.Alive[0].Alpha, Is.LessThan(1f), "fading at the end");
            floaters.Step(CombatFloaters.Seconds);
            Assert.That(floaters.Alive, Is.Empty, "and gone");

            for (int i = 0; i < CombatFloaters.Capacity + 5; i++) floaters.Add(i.ToString(), HudTheme.Bad, Vector3.zero);
            Assert.That(floaters.Alive.Count, Is.EqualTo(CombatFloaters.Capacity));
            Assert.That(floaters.Alive[0].Text, Is.EqualTo("5"), "the oldest went first");

            floaters.Step(0f);
            Assert.That(floaters.Alive.Count, Is.EqualTo(CombatFloaters.Capacity), "a paused frame ages nothing");
        }

        /// <summary>
        /// A word stays up as long as the interface says (<c>CombatFeedbackModel.FloatingSeconds</c>):
        /// "downed" outlives a number. Lane B floated every word for the same 1.2 s; the integration
        /// passes the model's lifetime through (2026-09-23).
        /// </summary>
        [Test]
        public void AWordStaysUpAsLongAsTheInterfaceSays()
        {
            var floaters = new CombatFloaters();
            var downed = new CombatEventView(1, 0, CombatEventKind.Downed, new PawnId(2), new PawnId(1), default, 0, -1);
            var hit = new CombatEventView(2, 0, CombatEventKind.Hit, new PawnId(2), new PawnId(1), default, 7_000, -1);
            floaters.Add("Downed", HudTheme.Bad, Vector3.zero, CombatFeedbackModel.FloatingSeconds(downed));
            floaters.Add("-7", HudTheme.Bad, Vector3.zero, CombatFeedbackModel.FloatingSeconds(hit));
            Assume.That(CombatFeedbackModel.FloatingSeconds(downed), Is.GreaterThan(CombatFeedbackModel.FloatingSeconds(hit)));

            floaters.Step(CombatFeedbackModel.FloatingSeconds(hit) + 0.05f);

            Assert.That(floaters.Alive.Count, Is.EqualTo(1), "the number outlived its time, or \"downed\" went with it");
            Assert.That(floaters.Alive[0].Text, Is.EqualTo("Downed"));
        }

        [Test]
        public void AHealthBarFillsFromTheLeftInTheHudsInks()
        {
            Assert.That(CombatMarks.Fraction(50_000, 100_000), Is.EqualTo(0.5f));
            Assert.That(CombatMarks.Fraction(-10_000, 100_000), Is.Zero, "a downed pawn's bar is empty, not negative");
            Assert.That(CombatMarks.Fraction(5, 0), Is.Zero);
        }

        /// <summary>
        /// Design 33 §8a: every piece of a bar lies in the one plane that faces the camera, at its
        /// place along the screen's right and up, so the pieces the layout keeps apart stay apart
        /// on the screen — which is what makes the transparent sort's order not matter.
        /// </summary>
        [Test]
        public void EveryPieceOfABarLiesInThePlaneFacingTheCamera()
        {
            var pieces = new HealthBarPiece[HealthBarLayout.MaxPieces];
            int n = HealthBarLayout.Pieces(0.4f, pieces);
            Quaternion facing = Quaternion.Euler(48f, 30f, 0f);
            Vector3 centre = new Vector3(12f, 3f, -7f);
            Vector3 forward = facing * Vector3.forward, right = facing * Vector3.right, up = facing * Vector3.up;

            for (int i = 0; i < n; i++)
            {
                Matrix4x4 m = CombatMarks.Place(in pieces[i], centre, facing);
                Vector3 at = m.GetColumn(3);
                Assert.That(Vector3.Dot(at - centre, forward), Is.EqualTo(0f).Within(1e-5f), $"piece {i} out of the plane");
                Assert.That(Vector3.Dot(at - centre, right), Is.EqualTo(pieces[i].CentreU).Within(1e-5f), $"piece {i} across");
                Assert.That(Vector3.Dot(at - centre, up), Is.EqualTo(pieces[i].CentreV).Within(1e-5f), $"piece {i} up");
                Assert.That(((Vector3)m.GetColumn(0)).magnitude, Is.EqualTo(pieces[i].Width).Within(1e-5f));
                Assert.That(((Vector3)m.GetColumn(1)).magnitude, Is.EqualTo(pieces[i].Height).Within(1e-5f));
                Assert.That(((Vector3)m.GetColumn(2)).magnitude, Is.EqualTo(CombatMarks.PieceDepth).Within(1e-6f));
                Assert.That(Vector3.Dot(((Vector3)m.GetColumn(2)).normalized, forward), Is.EqualTo(1f).Within(1e-5f),
                    $"piece {i} is not square to the camera");
            }
        }

        // ---- The review's corpse and floater faults (2026-09-23) -----------------------------------

        /// <summary>A frame of a running world, which is what a fall is seen in.</summary>
        static WorldSnapshot Running(int tick, params PawnView[] pawns)
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(tick, new GridSize(12, 12, 4), 0, gameSpeed: 1);
            foreach (PawnView pawn in pawns) snapshot.AddPawn(pawn);
            return snapshot;
        }

        /// <summary>A hog: its row is the project's own art, so these run on the runner too.</summary>
        static PawnView Hog(int id, int x, int z, int y = 0, PawnFlags flags = PawnFlags.None) =>
            new PawnView(new PawnId(id), new CellRef(x, z, y), 800, 800, 600, JobHandle.Wait, kind: 1, flags: flags);

        static CorpseView HogCorpse(int id, int pawn, int x, int z, int y, int tick) =>
            new CorpseView(id, new PawnId(pawn), 1, 11u, new CellRef(x, z, y), tick, 2, PawnFlags.None);

        /// <summary>Renderers under <paramref name="root"/> that would draw this frame.</summary>
        static int Drawn(GameObject root)
        {
            int n = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(includeInactive: false))
                if (renderer.enabled && !renderer.forceRenderingOff) n++;
            return n;
        }

        static string DrawnNames(GameObject root)
        {
            var names = new List<string>();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(includeInactive: false))
                if (renderer.enabled && !renderer.forceRenderingOff)
                    names.Add(renderer.transform.parent != null ? renderer.transform.parent.name + "/" + renderer.name : renderer.name);
            return string.Join(", ", names);
        }

        /// <summary>
        /// Putting a session down while a body is still falling does not throw. The bootstrap
        /// disposed the figures before the corpses, and handing the lent figure back then indexed
        /// a cleared list — so pausing on a death and loading threw out of <c>TeardownSession</c>
        /// and left a half-torn session. The order is fixed there; this is the guard beneath it, a
        /// loan handed back to a director that has already let its figures go.
        /// </summary>
        [Test]
        public void AFallCutShortByATeardownHandsItsFigureBackQuietly()
        {
            var world = new RenderTestWorld(8, 8, 3);
            var parent = new GameObject("corpses");
            PawnFigureDirector? figures = null;
            CorpseDirector? corpses = null;
            try
            {
                figures = new PawnFigureDirector(Catalogue(), parent.transform, 0);
                corpses = new CorpseDirector(world.Model, null, figures, parent.transform, 0);
                CorpseView corpse = HogCorpse(3, 12, 2, 2, 0, 1_000);
                Assume.That(figures.CanDrawCorpse(corpse), Is.True, "the hog's row resolves");

                corpses.Sync(Running(990), 0, new SliceSettings(), 1f / 60f);
                WorldSnapshot frame = Running(1_000);
                frame.AddCorpse(corpse);
                corpses.Sync(frame, 0, new SliceSettings(), 1f / 60f);
                Assert.That(corpses.Falling, Is.EqualTo(1), "the control: a fresh death is seen falling");

                figures.Dispose();
                CorpseDirector falling = corpses;
                Assert.DoesNotThrow(() => falling.Dispose(), "a fall cut short by the teardown threw");
                corpses = null;
            }
            finally
            {
                corpses?.Dispose();
                figures?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        /// <summary>
        /// A pawn killed where it lay downed is not stood back up to fall again: its body is baked
        /// lying at once. The control is a pawn killed on its feet in the same frame, which falls.
        /// Before the fix both fell, from standing (review, 2026-09-23).
        /// </summary>
        [Test]
        public void APawnKilledWhileDownIsFoundLyingAndOneKilledStandingFalls()
        {
            var world = new RenderTestWorld(8, 8, 3);
            var parent = new GameObject("corpses");
            PawnFigureDirector? figures = null;
            CorpseDirector? corpses = null;
            try
            {
                figures = new PawnFigureDirector(Catalogue(), parent.transform, 0);
                corpses = new CorpseDirector(world.Model, null, figures, parent.transform, 0);
                CorpseView down = HogCorpse(3, 12, 2, 2, 0, 1_000), standing = HogCorpse(4, 13, 5, 5, 0, 1_000);
                Assume.That(figures.CanDrawCorpse(down), Is.True, "the hog's row resolves");

                corpses.Sync(Running(999, Hog(12, 2, 2, flags: PawnFlags.Downed), Hog(13, 5, 5)), 0, new SliceSettings(), 1f / 60f);
                WorldSnapshot frame = Running(1_000);
                frame.AddCorpse(down);
                frame.AddCorpse(standing);
                corpses.Sync(frame, 0, new SliceSettings(), 1f / 60f);

                Assert.That(corpses.Count, Is.EqualTo(2));
                Assert.That(corpses.Falling, Is.EqualTo(1), "the one killed standing falls; the one killed lying does not");
                Assert.That(corpses.TryGetBox(3, out Bounds box), Is.True);
                Assert.That(box.size.y, Is.LessThan(1.2f), $"the downed one lies at once: {box.size}");
            }
            finally
            {
                corpses?.Dispose();
                figures?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        /// <summary>
        /// A body falling on a layer that is not drawn is not drawn, and one that finishes its fall
        /// there is found where it lies once the layer is drawn again. Before the fix the lent
        /// figure fell in full view on a hidden layer, and a body baked while hidden measured the
        /// bounds of inactive renderers — a zero box at the world's origin, so the click missed it
        /// and the cursor bracketed the origin (review, 2026-09-23).
        /// </summary>
        [Test]
        public void ABodyFallingOnAHiddenLayerIsHiddenAndIsFoundWhereItLies()
        {
            var world = new RenderTestWorld(8, 8, 3);
            var parent = new GameObject("corpses");
            PawnFigureDirector? figures = null;
            CorpseDirector? corpses = null;
            try
            {
                figures = new PawnFigureDirector(Catalogue(), parent.transform, 0);
                corpses = new CorpseDirector(world.Model, null, figures, parent.transform, 0);
                var cell = new CellRef(2, 2, 0);
                CorpseView corpse = HogCorpse(3, 12, cell.X, cell.Z, cell.Y, 1_000);
                Assume.That(figures.CanDrawCorpse(corpse), Is.True, "the hog's row resolves");

                // Layer 2 active with nothing drawn below it: the corpse's layer 0 is not drawn.
                var hidden = new SliceSettings { below = BelowMode.Hide };
                var shown = new SliceSettings();
                corpses.Sync(Running(990), 2, hidden, 1f / 60f);
                // Whatever the directors draw with no body at all (the chips' mesh): the floor.
                int none = Drawn(parent);
                WorldSnapshot frame = Running(1_000);
                frame.AddCorpse(corpse);

                corpses.Sync(frame, 0, shown, 1f / 60f);
                Assume.That(corpses.Falling, Is.EqualTo(1), "a fresh death is seen falling");
                Assert.That(Drawn(parent), Is.GreaterThan(none), "the control: a fall on a drawn layer is drawn");

                corpses.Sync(frame, 2, hidden, 1f / 60f);
                Assert.That(Drawn(parent), Is.EqualTo(none), "a body falling on a hidden layer was drawn: " + DrawnNames(parent));

                for (int i = 0; i < 20 && corpses.Falling > 0; i++) corpses.Sync(frame, 2, hidden, 0.25f);
                Assert.That(corpses.Falling, Is.Zero, "the fall never finished");
                Assert.That(Drawn(parent), Is.EqualTo(none), "a body baked on a hidden layer was drawn: " + DrawnNames(parent));

                corpses.Sync(frame, 0, shown, 1f / 60f);
                Assert.That(Drawn(parent), Is.GreaterThan(none), "the body is drawn once its layer is");
                Assert.That(corpses.TryGetBox(3, out Bounds box), Is.True);
                Vector3 floor = CellMetrics.FloorCentre(cell);
                Assert.That(new Vector2(box.center.x - floor.x, box.center.z - floor.z).magnitude, Is.LessThan(CellMetrics.SizeXZ),
                    $"its box is where it lies: {box.center} against the cell at {floor}");
                Assert.That(box.size.magnitude, Is.GreaterThan(0.3f), $"and has a size: {box.size}");
            }
            finally
            {
                corpses?.Dispose();
                figures?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        /// <summary>
        /// A word floats only over a fight on a drawn layer, as the health bars do; the fight is
        /// still handed on (the figures and the sound take every event). Before the fix a fight
        /// three layers down floated "Miss" over the grass above it (review, 2026-09-23).
        /// </summary>
        [Test]
        public void AFightOffTheDrawnLayersFloatsNoWords()
        {
            var feedback = new CombatFeedback();
            object world = new object();
            feedback.Consume(Events(1), world, null, null);

            // The events are on layer 0; draw layers 1 to 3.
            feedback.Consume(Events(1, 2), world, null, null, lowestLayer: 1, highestLayer: 3);
            Assert.That(feedback.Handled, Is.EqualTo(1), "the event is still handed on");
            Assert.That(feedback.Floaters.Alive.Count, Is.Zero, "a word floated over a fight nobody can see");

            feedback.Consume(Events(1, 2, 3), world, null, null, lowestLayer: 0, highestLayer: 3);
            Assert.That(feedback.Floaters.Alive.Count, Is.EqualTo(1), "the control: a fight on a drawn layer floats its word");
        }
    }
}
