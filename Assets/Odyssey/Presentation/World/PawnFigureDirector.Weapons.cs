#nullable enable
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// <see cref="PawnFigureDirector"/>: the weapon in the right hand (design 33 §1: "real items,
    /// one hand slot, drawn in the right hand"; §4, C3's held prop).
    ///
    /// <para><b>Nobody owned this until the integration</b> (2026-09-23). Lane D put the weapon in
    /// the simulation's hand and published it as <c>odyssey.pawn.weapon</c>; lane B chose the
    /// swing's clip family from it; neither drew it, so a bandit swung a sword clip with an empty
    /// fist. It is the tool code's approach cut down: the prop is the weapon's own ground row
    /// (<see cref="ModuleIds.Item"/>), so a clone without the packs draws no prop and fights
    /// exactly as before.</para>
    ///
    /// <para><b>Seated once, never adjusted.</b> The axe is re-placed every frame because its
    /// hands slide along the haft; a weapon's grip does not move, so it is fitted the first time it
    /// is shown — the haft along the forearm, the flat of the blade turned to the figure's front,
    /// the butt a tenth of its length into the palm — and rides the hand bone as a child after
    /// that. The graph rewrites bones and never touches a prop, so there is nothing to wind
    /// (<see cref="PlaceTool"/> says why that matters).</para>
    ///
    /// <para><b>At the hip or in the hand</b> (design 33 §8b): sheathed at the left hip unless the
    /// simulation publishes <c>PawnFlags.Drawn</c>, and always at the hip while a work tool is in the
    /// hand or a load in the arms; not drawn at all lying down (asleep or downed — a downed pawn
    /// keeps its weapon in the simulation, owner's C2 default). <c>PawnFigureDirector.Sheath.cs</c>
    /// holds the hip, the draw and the sheathe. Scales with the live figures, capped at 64: one
    /// aspect read and one clock step each, and an instantiate only when the weapon a pawn holds
    /// changes.</para>
    /// </summary>
    public sealed partial class PawnFigureDirector
    {
        /// <summary>Where a weapon is held, from its butt, as a fraction of its length. INVENTED.</summary>
        public const float WeaponGripFraction = 0.1f;

        /// <summary>Live figures with a weapon prop showing, at the hip or in the hand. For tests.</summary>
        public int ArmedFigures
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _figures.Count; i++)
                    if (_figures[i].Weapon != null && _figures[i].Weapon!.activeSelf) count++;
                return count;
            }
        }

        /// <summary>The prop a figure is holding for a pawn, or null. For tests and the contact sheet.</summary>
        public Transform? WeaponOf(PawnId pawn) =>
            _byPawn.TryGetValue(pawn.Value, out Figure? figure) && figure.Weapon != null
                ? figure.Weapon.transform
                : null;

        /// <summary>
        /// Show, swap or hide this figure's weapon for this frame, and put it at the hip or in the
        /// hand (design 33 §8b, <c>PawnFigureDirector.Sheath.cs</c>). Called at the end of
        /// <see cref="Pose"/>'s state updates, after the tool, the carry and the sleep weight it reads.
        /// </summary>
        void ShowWeapon(Figure figure, in PawnView pawn, bool carrying, float deltaTime)
        {
            int def = -1;
            if (pawn.IsPerson && _frame != null
                && _frame.TryGetPawnAspect(pawn.Id, CombatAspectNames.WeaponKey, out int held))
                def = held;

            if (def != figure.WeaponDef) SwapWeapon(figure, def);
            if (figure.Weapon == null)
            {
                // Still stepped, so a weapon taken up mid-fight is drawn from the state it finds.
                WeaponSheath.Step(ref figure.Sheath, in pawn, _frame != null ? _frame.Tick : 0);
                return;
            }

            // A body on the ground is drawn without it (a downed pawn keeps it in the simulation).
            bool shown = figure.SleepWeight <= 0.001f && !pawn.IsDowned;
            if (figure.Weapon.activeSelf != shown) figure.Weapon.SetActive(shown);

            // The one hand: a tool in it or a load in the arms sends the weapon to the hip. The same
            // threshold ShowHeldTool shows the tool at, so the two never share the fist.
            bool handBusy = figure.WorkWeight > 0.001f || carrying;
            Look? face = LookAt(figure.Look);
            PoseSheath(figure, in pawn, handBusy, face != null && face.Feminine, deltaTime);
            // A gun is placed absolutely while it aims, so it goes back to its fitted place in the
            // fist first, every frame (design 47 §4b).
            ReseatGun(figure);
        }

        /// <summary>Put the weapon away: into the pool, lent as a corpse, or any figure changing hands.</summary>
        static void HideWeapon(Figure figure)
        {
            if (figure.Weapon != null && figure.Weapon.activeSelf) figure.Weapon.SetActive(false);
        }

        void SwapWeapon(Figure figure, int def)
        {
            if (figure.Weapon != null)
            {
                if (Application.isPlaying) Object.Destroy(figure.Weapon);
                else Object.DestroyImmediate(figure.Weapon);
                figure.Weapon = null;
            }
            figure.WeaponDef = def;
            figure.IsGun = IsGunDef(def);
            figure.GunSlide = null;
            if (def < 0 || figure.RightHand == null) return;

            string? module = ModuleIds.Item(def);
            ModuleEntry? row = module != null && _catalogue != null ? _catalogue.Find(module) : null;
            GameObject? prefab = row != null ? row.prefab : null;
            if (prefab == null) return;

            GameObject prop = Object.Instantiate(prefab, figure.RightHand);
            prop.name = "Weapon" + def;
            SetLayer(prop.transform, _layer);
            var colliders = prop.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;

            FitWeaponBothWays(figure, prop.transform);
            figure.Weapon = prop;
        }

        /// <summary>
        /// Seat a weapon in the right fist, measured off its mesh: the long axis is the haft, the
        /// end the mass sits towards is the business end (every Synty weapon looked at pivots at
        /// the grip, so the bounds centre leans that way), the haft continues the forearm, and the
        /// blade's flat is turned so its edge faces the way the figure faces.
        /// </summary>
        void FitWeapon(Figure figure, Transform prop)
        {
            Transform? hand = figure.RightHand;
            prop.localPosition = Vector3.zero;
            prop.localRotation = Quaternion.identity;
            if (hand == null || !LocalBounds(prop, out Bounds bounds)) return;

            Vector3 extents = bounds.extents;
            Vector3 haft = extents.x >= extents.y && extents.x >= extents.z ? Vector3.right
                : extents.y >= extents.z ? Vector3.up : Vector3.forward;
            float half = Vector3.Dot(extents, haft);
            if (half <= 1e-4f) return;
            if (Vector3.Dot(bounds.center, haft) < 0f) haft = -haft;
            Vector3 bit = BitAxis(bounds, haft);

            Vector3 outOfTheFist = figure.RightLowerArm != null
                ? hand.position - figure.RightLowerArm.position
                : hand.forward;
            if (outOfTheFist.sqrMagnitude < 1e-6f) return;
            outOfTheFist.Normalize();
            prop.rotation = Quaternion.FromToRotation(prop.TransformDirection(haft), outOfTheFist) * prop.rotation;

            // About the haft only, so the line the fitting just set is kept.
            Vector3 haftWorld = prop.TransformDirection(haft);
            Vector3 want = Vector3.ProjectOnPlane(figure.Transform.forward, haftWorld);
            Vector3 have = Vector3.ProjectOnPlane(prop.TransformDirection(bit), haftWorld);
            if (want.sqrMagnitude > 1e-6f && have.sqrMagnitude > 1e-6f)
                prop.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(have, want, haftWorld), haftWorld) * prop.rotation;

            // The butt end of the long axis, then a tenth of the way up it, into the palm.
            Vector3 butt = bounds.center - haft * half;
            Vector3 grip = butt + haft * (2f * half * WeaponGripFraction);
            prop.position += HandGrip.Palm(figure.RightGrip) - prop.TransformPoint(grip);
        }
    }
}
