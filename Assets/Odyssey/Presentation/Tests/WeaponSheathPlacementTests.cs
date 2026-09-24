#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
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
    /// Drawn and sheathed, on the figure (design 33 §8b): the weapon hangs at the left hip off the
    /// real pelvis, goes to the right hand when the simulation says drawn — at the hand-on-hilt
    /// moment of the pack's draw, or at once without the pack — comes back after the hold, is never
    /// in two places, and never shares the fist with a work tool.
    ///
    /// <para>Every test that needs a colonist or the machete asks whether the art <em>resolved</em>
    /// and ignores itself on the runner. <b>Written without a Unity run</b> (the lane had none): the
    /// negative controls in §8b are named, not yet seen to fail.</para>
    /// </summary>
    public class WeaponSheathPlacementTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";
        const string SwordCombatPolygon = "Assets/Synty/AnimationSwordCombat/Animations/Polygon";
        const float Frame60 = 1f / 60f;

        static ModuleCatalogue Catalogue()
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
            Assume.That(catalogue, Is.Not.Null, "the module catalogue asset is committed");
            return catalogue!;
        }

        static WorldSnapshot Frame(int tick, PawnView pawn, int weapon = ItemIndex.Machete)
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(tick, new GridSize(12, 12, 4), 0);
            snapshot.AddPawn(pawn);
            if (weapon >= 0) snapshot.AddPawnAspect(new PawnAspect(pawn.Id, CombatAspectNames.WeaponKey, weapon));
            return snapshot;
        }

        static PawnView Colonist(PawnFlags flags = PawnFlags.Person, int job = JobHandle.Wait, bool working = false) =>
            new PawnView(new PawnId(1), new CellRef(3, 3, 0), 800, 800, 600, job,
                working: working, workCell: working ? new CellRef(4, 3, 0) : default, flags: flags);

        const PawnFlags AtPeace = PawnFlags.Person;
        const PawnFlags Drafted = PawnFlags.Person | PawnFlags.Drafted | PawnFlags.Drawn;
        const PawnFlags Retaliating = PawnFlags.Person | PawnFlags.Drawn;

        /// <summary>One frame at 1/60 s on a snapshot of this tick, as the player loop and the harness both run it.</summary>
        static void Frames(PawnFigureDirector director, WorldSnapshot frame, int count)
        {
            for (int i = 0; i < count; i++)
            {
                director.Sync(frame, 0, new SliceSettings(), 0f, 1, Frame60);
                director.Evaluate(Frame60);
            }
        }

        /// <summary>Frames with the tick moving on one a frame — sixty a second, the composition root's rate.</summary>
        static int Ticking(PawnFigureDirector director, int fromTick, PawnFlags flags, int count)
        {
            for (int i = 0; i < count; i++)
                Frames(director, Frame(fromTick + i, Colonist(flags)), 1);
            return fromTick + count;
        }

        static PawnFigureDirector? Armed(ModuleCatalogue catalogue, GameObject parent)
        {
            if (catalogue.Find(ModuleIds.ItemMachete)?.prefab == null) Assert.Ignore("no machete art here");
            var director = new PawnFigureDirector(catalogue, parent.transform, 0);
            if (!director.CanDrawColonists)
            {
                director.Dispose();
                Assert.Ignore("no colonist art here");
            }
            return director;
        }

        static bool AtHip(PawnFigureDirector director, PawnId id)
        {
            Assert.That(director.TryGetWeaponPlace(id, out bool atHip, out Transform? parent), Is.True, "no weapon prop");
            Assert.That(parent, Is.SameAs(atHip ? director.PelvisOf(id) : director.RightHandOf(id)),
                "the figure's record of where the weapon is and its parent disagree");
            return atHip;
        }

        /// <summary>The figure's own root: the ancestor directly under the director's parent.</summary>
        static Transform FigureRoot(Transform part, Transform parent)
        {
            Transform at = part;
            while (at.parent != null && at.parent != parent) at = at.parent;
            return at;
        }

        // ---- The rows ------------------------------------------------------------------------------

        /// <summary>
        /// Both sheath rows are in the committed catalogue, each naming the masculine and feminine
        /// clip; every clip that resolved is a Polygon, in-place Humanoid whole clip; where none did
        /// — the runner — the director snaps. Fails until the catalogue is rebuilt with the rows.
        /// </summary>
        [Test]
        public void TheSheathRowsResolveToAllowedClipsOrTheWeaponSnaps()
        {
            ModuleCatalogue catalogue = Catalogue();
            var parent = new GameObject("figures");
            PawnFigureDirector? director = null;
            try
            {
                director = new PawnFigureDirector(catalogue, parent.transform, 0);
                int resolved = 0;
                foreach (string id in ModuleIds.SheathRows)
                {
                    ModuleEntry? row = catalogue.Find(id);
                    Assert.That(row, Is.Not.Null, $"{id} is in the committed catalogue (rebuild it)");
                    var variants = new List<string>();
                    foreach (CombatClipEntry entry in row!.combat)
                    {
                        variants.Add(entry.variant);
                        Assert.That(entry.clipName, Does.Not.Contain("RootMotion").And.Not.Contain("ReturnToIdle"));
                        if (entry.clip == null)
                        {
                            Assert.That(System.IO.Directory.Exists(SwordCombatPolygon), Is.False,
                                $"the pack is here and {entry.clipName} did not resolve");
                            continue;
                        }
                        resolved++;
                        string path = AssetDatabase.GetAssetPath(entry.clip);
                        Assert.That(path, Does.StartWith(SwordCombatPolygon), entry.clipName);
                        Assert.That(System.IO.Path.GetFileNameWithoutExtension(path), Is.EqualTo(entry.clipName));
                        Assert.That(entry.clip.humanMotion, Is.True, $"{entry.clipName} drives a Humanoid rig");
                        Assert.That(entry.impactSeconds, Is.Zero, $"{entry.clipName} is not a blow");
                    }
                    Assert.That(variants, Is.EquivalentTo(new[] { CombatVariant.Masc, CombatVariant.Femn }), id);
                }
                Assert.That(director.HasSheathClips, Is.EqualTo(resolved == 4),
                    "the director's question and the rows disagree about whether the draw is animated");
            }
            finally
            {
                director?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        // ---- The hip ---------------------------------------------------------------------------------

        /// <summary>
        /// At peace the weapon hangs on the real pelvis — not <c>HumanBodyBones.Hips</c>, which the
        /// Synty avatar maps to <c>Root</c> on the floor — on the figure's left, below the head and
        /// well above the feet, and not in the hand.
        /// </summary>
        [Test]
        public void AtPeaceTheWeaponHangsAtTheLeftHipOffThePelvis()
        {
            var parent = new GameObject("figures");
            PawnFigureDirector? director = null;
            try
            {
                director = Armed(Catalogue(), parent);
                var id = new PawnId(1);
                Frames(director!, Frame(100, Colonist(AtPeace)), 10);

                Assert.That(AtHip(director!, id), Is.True, "at peace, and the weapon was in the hand");
                Transform prop = director!.WeaponOf(id)!;
                Transform pelvis = director.PelvisOf(id)!;
                Transform root = FigureRoot(prop, parent.transform);
                Assert.That(director.TryGetHead(id, out Vector3 head), Is.True);
                float height = head.y - root.position.y;

                Assert.That(pelvis.position.y - root.position.y, Is.GreaterThan(0.3f * height),
                    "the sheath hangs off a bone on the floor: Hips is Root on this avatar");
                Vector3 centre = PropCentre(prop);
                Assert.That(Vector3.Dot(centre - root.position, -root.right), Is.GreaterThan(0.05f * height),
                    "the weapon is not on the figure's left");
                Assert.That(centre.y, Is.LessThan(head.y).And.GreaterThan(root.position.y + 0.1f * height),
                    "the weapon is not at the hip");
            }
            finally
            {
                director?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        static Vector3 PropCentre(Transform prop)
        {
            var renderers = prop.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers.Length == 0) return prop.position;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds.center;
        }

        // ---- Drawn, and back -------------------------------------------------------------------------

        /// <summary>
        /// Without the pack's sheath clips (stripped, as on a checkout without it) the weapon snaps:
        /// in the hand on the first frame the simulation says drawn, with nothing playing, and back
        /// at the hip on the first frame the hold has run out.
        /// </summary>
        [Test]
        public void WithThePackAbsentTheWeaponSnapsBetweenHipAndHand()
        {
            ModuleCatalogue stripped = Object.Instantiate(Catalogue());
            foreach (string row in ModuleIds.SheathRows)
                foreach (CombatClipEntry entry in stripped.Find(row)?.combat ?? new List<CombatClipEntry>())
                    entry.clip = null;

            var parent = new GameObject("figures");
            PawnFigureDirector? director = null;
            try
            {
                director = Armed(stripped, parent);
                Assert.That(director!.HasSheathClips, Is.False);
                var id = new PawnId(1);

                int tick = Ticking(director, 100, AtPeace, 5);
                Assert.That(AtHip(director, id), Is.True);

                tick = Ticking(director, tick, Retaliating, 1);
                Assert.That(AtHip(director, id), Is.False, "drawn, and the weapon waited at the hip with no clip to play");
                Assert.That(director.SheathActionOf(id), Is.EqualTo(SheathChange.None));

                tick = Ticking(director, tick, AtPeace, WeaponSheath.HoldTicks - 1);
                Assert.That(AtHip(director, id), Is.False, "put away inside the hold");
                Ticking(director, tick, AtPeace, 2);
                Assert.That(AtHip(director, id), Is.True, "not put away when the hold ran out");
            }
            finally
            {
                director?.Dispose();
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(stripped);
            }
        }

        /// <summary>
        /// With the pack, the draw plays and the weapon stays at the hip until the hand is on the
        /// hilt — a measured moment inside the clip, not its start — and is in the hand after; on
        /// release from the draft the sheathe plays and it is back at the hip when that ends.
        /// </summary>
        [Test]
        public void WithThePackTheHandTakesTheHiltPartWayThroughTheDraw()
        {
            var parent = new GameObject("figures");
            PawnFigureDirector? director = null;
            try
            {
                director = Armed(Catalogue(), parent);
                if (!director!.HasSheathClips) Assert.Ignore("the Sword Combat pack's draw and sheathe are not here");
                var id = new PawnId(1);

                int tick = Ticking(director, 100, AtPeace, 5);
                tick = Ticking(director, tick, Drafted, 1);
                Assert.That(director.SheathActionOf(id), Is.EqualTo(SheathChange.Draw), "no draw played");
                Assert.That(AtHip(director, id), Is.True, "in the hand on the draw's first frame");

                // The body's own draw, masculine or feminine, and where in it the hand is on the hilt.
                CombatClipEntry draw = director.SheathClipOf(id)!;
                Assert.That(draw, Is.Not.Null);
                float grasp = director.SheathMoment(draw, draw: true);
                Assert.That(grasp, Is.GreaterThan(0.05f).And.LessThan(draw.clip!.length - 0.05f),
                    "the hand-on-hilt moment is the clip's start or end");

                int toGrasp = Mathf.CeilToInt(grasp / Frame60);
                tick = Ticking(director, tick, Drafted, toGrasp);
                Assert.That(AtHip(director, id), Is.False, "the hand passed the hilt and left the weapon");

                tick = Ticking(director, tick, Drafted, Mathf.CeilToInt(draw.clip.length / Frame60) + 2);
                Assert.That(director.SheathActionOf(id), Is.EqualTo(SheathChange.None), "the draw never ended");
                Assert.That(AtHip(director, id), Is.False);

                // Released with nobody near: the sheathe starts at once, not after the hold.
                tick = Ticking(director, tick, AtPeace, 1);
                Assert.That(director.SheathActionOf(id), Is.EqualTo(SheathChange.Sheathe), "released, and nothing was put away");
                CombatClipEntry sheathe = director.SheathClipOf(id)!;
                Assert.That(sheathe, Is.Not.Null);
                Assert.That(AtHip(director, id), Is.False, "at the hip on the sheathe's first frame, before the release");
                Ticking(director, tick, AtPeace, Mathf.CeilToInt(sheathe.clip!.length / Frame60) + 2);
                Assert.That(AtHip(director, id), Is.True, "the sheathe ended with the weapon in the hand");
            }
            finally
            {
                director?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        // ---- One hand --------------------------------------------------------------------------------

        /// <summary>
        /// Drawn and working at once — a view no real colony publishes, which is the point: the
        /// fist has one owner. On every frame the axe is showing, the weapon is on the pelvis; the
        /// weapon is under exactly one of the two bones on every frame; and it comes back to the
        /// hand once the axe has gone.
        /// </summary>
        [Test]
        public void TheToolAndTheWeaponNeverShareTheHand()
        {
            var parent = new GameObject("figures");
            PawnFigureDirector? director = null;
            try
            {
                director = Armed(Catalogue(), parent);
                var id = new PawnId(1);
                int tick = Ticking(director!, 100, Drafted, 120);
                Assert.That(AtHip(director!, id), Is.False, "the control: drafted, and the weapon never came out");

                Transform hand = director!.RightHandOf(id)!;
                Transform pelvis = director.PelvisOf(id)!;
                int toolFrames = 0;
                for (int i = 0; i < 120; i++)
                {
                    Frames(director, Frame(tick + i, Colonist(Drafted, JobHandle.Fell, working: true)), 1);
                    bool toolShowing = false;
                    for (int c = 0; c < hand.childCount; c++)
                    {
                        Transform child = hand.GetChild(c);
                        if (child.name.StartsWith("Tool") && child.gameObject.activeSelf) toolShowing = true;
                    }
                    if (toolShowing)
                    {
                        toolFrames++;
                        Assert.That(AtHip(director, id), Is.True, $"frame {i}: the axe and the weapon in one fist");
                    }
                    Assert.That(CountWeapons(hand) + CountWeapons(pelvis), Is.EqualTo(1), $"frame {i}: the weapon in two places");
                }
                Assume.That(toolFrames, Is.GreaterThan(0), "no axe here, so this proved nothing");

                Ticking(director, tick + 120, Drafted, 120);
                Assert.That(AtHip(director, id), Is.False, "the axe went and the weapon did not come back to the hand");
            }
            finally
            {
                director?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        static int CountWeapons(Transform bone)
        {
            int n = 0;
            for (int c = 0; c < bone.childCount; c++)
                if (bone.GetChild(c).name.StartsWith("Weapon")) n++;
            return n;
        }
    }
}
