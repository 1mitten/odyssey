#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;
using UnityEditor;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The gun on a figure (design 47 §4a, §4b): how it sits in the fist, the aim, the recoil, low
    /// ready. The curves and the rules run everywhere; the posed figure asks whether the art resolved
    /// — a colonist to hold it and the pistol's own row — and ignores itself on the runner.
    /// </summary>
    public class GunPoseTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        [Test]
        public void TheRecoilKicksAtTheShotAndRestsWithinItsLength()
        {
            Assert.That(PawnFigureDirector.RecoilKick(0f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(PawnFigureDirector.RecoilKick(-0.1f), Is.EqualTo(0f), "before any shot, nothing");
            float last = 1f;
            for (float t = 0.01f; t < PawnFigureDirector.RecoilSeconds; t += 0.01f)
            {
                float k = PawnFigureDirector.RecoilKick(t);
                Assert.That(k, Is.LessThanOrEqualTo(last), $"it swung back at {t:F2} s: a damped spring never overshoots");
                last = k;
            }
            Assert.That(PawnFigureDirector.RecoilKick(PawnFigureDirector.RecoilSeconds - 0.01f), Is.LessThan(0.05f),
                "near rest by the end of its length");
            Assert.That(PawnFigureDirector.RecoilKick(PawnFigureDirector.RecoilSeconds), Is.EqualTo(0f));
        }

        [Test]
        public void TheSlideRunsBackAndHomeInsideItsTime()
        {
            Assert.That(PawnFigureDirector.SlideRun(0f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(PawnFigureDirector.SlideRun(PawnFigureDirector.SlideSeconds * 0.5f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(PawnFigureDirector.SlideRun(PawnFigureDirector.SlideSeconds), Is.EqualTo(0f));
        }

        /// <summary>The work stroke never plays for an attack: the swing is the fight's and the aim is the gun's.</summary>
        [Test]
        public void AnAttackPlaysNoWorkStroke()
        {
            PawnView With(int job) => new PawnView(new PawnId(1), new CellRef(3, 3, 0), 800, 800, 600, job,
                working: true, workCell: new CellRef(6, 3, 0));
            Assert.That(PawnFigureDirector.PlaysWorkStroke(With(JobHandle.Fell)), Is.True, "the control");
            Assert.That(PawnFigureDirector.PlaysWorkStroke(With(JobHandle.AttackMelee)), Is.False);
            Assert.That(PawnFigureDirector.PlaysWorkStroke(With(JobHandle.AttackRanged)), Is.False);
        }

        static WorldSnapshot Frame(int tick, PawnView pawn, int weapon)
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(tick, new GridSize(16, 16, 4), 0);
            snapshot.AddPawn(pawn);
            if (weapon >= 0) snapshot.AddPawnAspect(new PawnAspect(pawn.Id, CombatAspectNames.WeaponKey, weapon));
            return snapshot;
        }

        static void Step(PawnFigureDirector director, WorldSnapshot frame, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                director.Sync(frame, 0, new SliceSettings(), 0f, 1, 1f / 60f);
                director.Evaluate(1f / 60f);
            }
        }

        /// <summary>
        /// A drafted colonist aiming a pistol at a cell four along: the gun is on the line to the
        /// target, both hands are on it, and low ready is what she shows with nothing to shoot —
        /// the control that the aim is the job's and not merely the gun's. A shot then kicks the
        /// muzzle up and it comes back.
        /// </summary>
        [Test]
        public void AnAimingColonistHoldsTheGunOnTheLineWithBothHands()
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
            Assume.That(catalogue, Is.Not.Null, "the module catalogue asset is committed");
            if (catalogue!.Find(ModuleIds.ItemPistol)?.prefab == null) Assert.Ignore("no pistol art here: nothing to hold");

            var parent = new GameObject("figures");
            PawnFigureDirector? director = null;
            try
            {
                director = new PawnFigureDirector(catalogue, parent.transform, 0);
                if (!director.CanDrawColonists) Assert.Ignore("no colonist art here: nobody to hold it");
                director.WeaponStyles = CombatPose.StylesOf(ContentPack.Pawns().Items);
                var id = new PawnId(1);
                const PawnFlags drawn = PawnFlags.Person | PawnFlags.Drafted | PawnFlags.Drawn;

                // Drawn, nothing to shoot: low ready, in the hand.
                PawnView ready = new PawnView(id, new CellRef(3, 3, 0), 800, 800, 600, JobHandle.DraftHold, flags: drawn);
                Step(director, Frame(100, ready, ItemIndex.Pistol), 120);
                Assert.That(director.TryGetWeaponPlace(id, out bool atHip, out _), Is.True);
                Assert.That(atHip, Is.False, "drafted and drawn, and the pistol stayed at the hip");
                Assert.That(director.AimingFigures, Is.EqualTo(0), "the control: nothing to shoot, no aim");

                // Aiming at the cell four along.
                var target = new CellRef(7, 3, 0);
                PawnView aiming = new PawnView(id, new CellRef(3, 3, 0), 800, 800, 600, JobHandle.AttackRanged,
                    working: true, workCell: target, flags: drawn);
                Step(director, Frame(200, aiming, ItemIndex.Pistol), 60);
                Assert.That(director.AimingFigures, Is.EqualTo(1));

                Transform gun = director.WeaponOf(id)!;
                Vector3 toTarget = CellMetrics.FloorCentre(target) + Vector3.up * (FigureBuild.FallbackHeight * PawnFigureDirector.ChestOfHeight)
                                   - gun.position;
                float off = Vector3.Angle(gun.forward, toTarget);
                Assert.That(off, Is.LessThan(20f), $"the barrel points {off:F0} degrees off the target");
                Transform? hand = director.RightHandOf(id);
                Assert.That(Vector3.Distance(hand!.position, gun.position), Is.LessThan(0.2f), "the right hand is off the gun");

                // A shot: the muzzle kicks up, and is back within the recoil's length.
                float level = gun.forward.y;
                PawnView fired = new PawnView(id, new CellRef(3, 3, 0), 800, 800, 600, JobHandle.AttackRanged,
                    working: true, workCell: target, gesture: PawnGesture.Fire, gestureSerial: 1, flags: drawn);
                Step(director, Frame(260, fired, ItemIndex.Pistol), 1);
                Step(director, Frame(260, fired, ItemIndex.Pistol), 2);
                Assert.That(gun.forward.y, Is.GreaterThan(level + 0.02f), "no kick");
                Step(director, Frame(280, fired, ItemIndex.Pistol), 30);
                Assert.That(gun.forward.y, Is.EqualTo(level).Within(0.02f), "the kick did not settle");
            }
            finally
            {
                director?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }
    }
}
