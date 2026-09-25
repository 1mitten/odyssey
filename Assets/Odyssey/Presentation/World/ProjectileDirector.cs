#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// <b>Bullets, drawn</b> (design 47 §4c): a streak along every <see cref="ProjectileView"/> in
    /// flight, an afterimage where each one landed, and a flash at the muzzle of every shot.
    ///
    /// <para><b>Presentation only, for ever.</b> The flight — who fired, from which cell, to which,
    /// on which ticks — is the simulation's and arrives on the snapshot; what the bullet hit
    /// arrives as a <c>Hit</c> or <c>Miss</c> at the impact tick. Nothing here is in a cell, a save
    /// or the hash, and a world change clears it (<see cref="Clear"/>).</para>
    ///
    /// <para><b>Where along the line</b> is <see cref="FallArc.Progress"/>, reused as it is: the
    /// published tick plus the frame's tick alpha, so a paused world stops the alpha and every
    /// bullet hangs in the air where it was. <b>From</b> is the muzzle of the shooter's drawn gun
    /// when it is in the hand (<see cref="MuzzleOf"/>), else the start cell at chest height,
    /// latched on the frame the bullet is first seen so a shooter turning afterwards does not swing
    /// the streak. <b>To</b> is re-read every frame: the target's drawn chest while its figure
    /// stands on or is stepping out of the end cell, else the end cell at chest height
    /// (<see cref="EndPoint"/>) — the owner's "hits must visibly connect with the target". At the
    /// impact the event decides the last word: a hit ends on the struck body, a miss runs on to
    /// the cell where the bullet went down.</para>
    ///
    /// <para><b>A streak is never shorter-lived than the eye.</b> When a bullet leaves the frame
    /// its last streak is kept as an afterimage at its final position, fading over
    /// <see cref="AfterimageSeconds"/> of real time — so a point-blank shot at 3x, whose whole
    /// flight can fall between two frames, is still seen. A flight that was never on any snapshot
    /// is recovered from its <c>Shot</c> event.</para>
    ///
    /// <para><b>The slice</b> (§4c's cross-layer rule): a bullet is drawn when either end's layer
    /// is drawn — a ghosted layer counts, a storey walls-down hides does not — and clipped to the
    /// band's vertical extent, so one from a hidden storey enters at the band's ceiling and one
    /// into an undrawn cellar leaves at its floor (<see cref="VisibleSpan"/>).</para>
    ///
    /// <para><b>Draws in two buckets, never in bullets</b> (P10): every streak and afterimage is
    /// one matrix in one instanced call on <c>Odyssey/Tracer</c>, and every flash one matrix in a
    /// second, each split only past <see cref="ChunkRenderer.MaxInstancesPerCall"/>. Counted in the
    /// renderer's <see cref="ChunkRenderer.DrawCalls"/>; charged to <c>FrameSection.Overlays</c> by
    /// the composition root. Per frame it walks the bullets in flight (at most one per armed pawn),
    /// the afterimages and the flashes, and nothing else; after the first frames it allocates
    /// nothing.</para>
    /// </summary>
    public sealed class ProjectileDirector : IDisposable
    {
        /// <summary>
        /// How high over a cell's floor a bullet flies when there is no figure to aim at, in metres:
        /// the chest, where <c>CombatFeedback.PersonWoundHeight</c> puts a blow.
        /// </summary>
        public const float ChestHeight = 1.3f;

        /// <summary>
        /// The Battle Royale pistol's muzzle in the prop's own frame, in metres (design 47 §4a): the
        /// slide's front face at the bore's height. Used where the prop's own bounds say nothing
        /// better — <see cref="MuzzleOf"/> prefers the bounds' forward-most face at this height.
        /// </summary>
        public static readonly Vector3 PistolMuzzle = new Vector3(0f, 0.07f, 0.296f);

        /// <summary>The streak's tail as a fraction of the whole flight.</summary>
        public const float TailFraction = 0.15f;

        /// <summary>The streak's tail is never shorter than this, in metres.</summary>
        public const float MinTailMetres = 2f;

        /// <summary>The streak's width in metres; the shader raises it to two pixels at the far zoom.</summary>
        public const float StreakWidth = 0.08f;

        /// <summary>How long a landed bullet's streak stays on screen, fading, in real seconds.</summary>
        public const float AfterimageSeconds = 0.08f;

        /// <summary>How long a muzzle flash lasts, in ticks: it hangs with the bullets on a pause.</summary>
        public const int FlashTicks = 4;

        /// <summary>A flash's size, in metres across.</summary>
        public const float FlashSize = 0.55f;

        /// <summary>
        /// The render queue, explicit (P17): above the rain's Transparent+50, so a tracer through
        /// rain is never hidden by the rain batch's single sort distance.
        /// </summary>
        public const int RenderQueue = 3000 + 60;

        /// <summary>The streak's colour, before the shader's intensity. INVENTED.</summary>
        public static readonly Color TracerColour = new Color(1f, 0.78f, 0.42f, 1f);

        /// <summary>The flash's colour, before the shader's intensity. INVENTED.</summary>
        public static readonly Color FlashColour = new Color(1f, 0.72f, 0.32f, 1f);

        const float TracerIntensity = 2.5f, FlashIntensity = 3.0f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int BillboardId = Shader.PropertyToID("_Billboard");

        /// <summary>One bullet the director is following, in flight or as an afterimage.</summary>
        struct Streak
        {
            public int Shooter;
            public int FireTick;
            public int ImpactTick;
            public PawnId Target;
            public CellRef Start;
            public CellRef End;
            public Vector3 From;
            public Vector3 To;

            /// <summary>Whether this frame's snapshot still carries the bullet.</summary>
            public bool Seen;

            /// <summary>Whether the impact's event has said where it ended.</summary>
            public bool Resolved;

            /// <summary>Whether it has left the snapshot and is fading out.</summary>
            public bool Fading;

            /// <summary>Real seconds of fade left, while <see cref="Fading"/>.</summary>
            public float FadeLeft;
        }

        struct Flash
        {
            public int Shooter;
            public int FireTick;
            public CellRef Cell;
            public Vector3 At;
        }

        readonly WorldRenderModel? _model;
        readonly List<Streak> _streaks = new List<Streak>(16);
        readonly List<Flash> _flashes = new List<Flash>(16);
        readonly Dictionary<int, Vector3> _muzzles = new Dictionary<int, Vector3>();
        readonly List<MeshFilter> _meshScratch = new List<MeshFilter>(8);
        Matrix4x4[] _streakMatrices = new Matrix4x4[16];
        Matrix4x4[] _flashMatrices = new Matrix4x4[16];
        Material? _tracerMaterial, _flashMaterial;
        Mesh? _quad;
        bool _shaderMissing;

        /// <param name="model">The render mirror, for walls-down's question of which storeys it hides. Null in a test that asks nothing of the slice.</param>
        public ProjectileDirector(WorldRenderModel? model = null)
        {
            _model = model;
        }

        /// <summary>
        /// Off, nothing is followed or drawn. <b>For the frame measurement's control</b> (design 47
        /// §5's gunfight arm, the <c>InstanceCellPlates</c> pattern): the same fight timed with and
        /// without the pass.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Instanced calls the last <see cref="Draw"/> submitted.</summary>
        public int LastDrawCalls { get; private set; }

        /// <summary>Streaks the last <see cref="Draw"/> submitted, afterimages included.</summary>
        public int LastStreaksDrawn { get; private set; }

        /// <summary>Flashes the last <see cref="Draw"/> submitted.</summary>
        public int LastFlashesDrawn { get; private set; }

        /// <summary>Bullets being followed: in flight or fading.</summary>
        public int Following => _streaks.Count;

        /// <summary>Flashes still burning.</summary>
        public int FlashesBurning => _flashes.Count;

        // ---------------------------------------------------------------- the pure geometry

        /// <summary>
        /// Where a bullet flying to <paramref name="end"/> ends this frame: the target's drawn chest
        /// while its figure holds the end cell, else the end cell at <see cref="ChestHeight"/>.
        /// </summary>
        /// <param name="targetHoldsEnd">Whether the target stands on, or is stepping out of or into, the end cell (<see cref="HoldsEnd"/>).</param>
        /// <param name="hasDrawnChest">Whether the target has a figure whose chest was read.</param>
        public static Vector3 EndPoint(CellRef end, bool targetHoldsEnd, bool hasDrawnChest, Vector3 drawnChest) =>
            targetHoldsEnd && hasDrawnChest ? drawnChest : ChestOf(end);

        /// <summary>
        /// Whether a pawn's figure is drawn on <paramref name="end"/>: standing on it, stepping out
        /// of it, or stepping into it. A figure walks up to a cell off its sim cell, which is why a
        /// streak must follow the figure rather than stop at the cell's centre.
        /// </summary>
        public static bool HoldsEnd(in PawnView pawn, CellRef end) => pawn.Cell == end || pawn.NextCell == end;

        /// <summary>A cell's centre at chest height, on the relief.</summary>
        public static Vector3 ChestOf(CellRef cell) =>
            GroundRelief.Lift(CellMetrics.FloorCentre(cell)) + Vector3.up * ChestHeight;

        /// <summary>
        /// The height of the plane a bullet crosses on its way to or from an end on
        /// <paramref name="layer"/> that is not drawn: the band's ceiling for an end above it, its
        /// floor for an end below, and — for a storey inside the band that walls-down hides — that
        /// storey's own floor, which is the ceiling of what is drawn beneath it.
        /// </summary>
        public static float PlaneFor(int layer, int lowest, int highest)
        {
            if (layer < lowest) return lowest * CellMetrics.SizeY;
            if (layer > highest) return (highest + 1) * CellMetrics.SizeY;
            return layer * CellMetrics.SizeY;
        }

        /// <summary>
        /// The part of the line from <paramref name="from"/> to <paramref name="to"/> that is drawn,
        /// as fractions of it: all of it when both ends are drawn, nothing when neither is, and
        /// otherwise cut where the line crosses the undrawn end's plane (<see cref="PlaneFor"/>).
        /// </summary>
        public static bool VisibleSpan(Vector3 from, Vector3 to, bool fromDrawn, float fromPlane, bool toDrawn,
            float toPlane, out float tMin, out float tMax)
        {
            tMin = 0f;
            tMax = 1f;
            if (!fromDrawn && !toDrawn) return false;

            float rise = to.y - from.y;
            if (!fromDrawn) tMin = Mathf.Abs(rise) > 1e-5f ? Mathf.Clamp01((fromPlane - from.y) / rise) : 1f;
            if (!toDrawn) tMax = Mathf.Abs(rise) > 1e-5f ? Mathf.Clamp01((toPlane - from.y) / rise) : 0f;
            return tMax > tMin;
        }

        /// <summary>
        /// The streak at <paramref name="progress"/> along the flight: its head there, its tail
        /// <see cref="TailFraction"/> of the flight behind and never under <see cref="MinTailMetres"/>,
        /// neither behind the start, both cut to the visible span. False when nothing is left to draw.
        /// </summary>
        public static bool StreakAt(Vector3 from, Vector3 to, float progress, float tMin, float tMax,
            out Vector3 tail, out Vector3 head)
        {
            tail = head = from;
            float length = Vector3.Distance(from, to);
            if (length < 1e-4f) return false;

            float behind = Mathf.Max(TailFraction, MinTailMetres / length);
            float h = Mathf.Min(Mathf.Clamp01(progress), tMax);
            float t = Mathf.Max(Mathf.Clamp01(progress) - behind, 0f);
            t = Mathf.Max(t, tMin);
            if (h - t < 1e-5f) return false;

            tail = Vector3.LerpUnclamped(from, to, t);
            head = Vector3.LerpUnclamped(from, to, h);
            return true;
        }

        /// <summary>
        /// The instance matrix <c>Odyssey/Tracer</c> reads a streak from: the tail as its
        /// translation, head minus tail as its z column, the width as the x column's length and the
        /// fade as the y column's, the two built square to the line so the matrix stays invertible.
        /// </summary>
        public static Matrix4x4 StreakMatrix(Vector3 tail, Vector3 head, float width, float fade)
        {
            Vector3 along = head - tail;
            Vector3 direction = along.normalized;
            Vector3 side = Vector3.Cross(direction, Vector3.up);
            if (side.sqrMagnitude < 1e-6f) side = Vector3.Cross(direction, Vector3.right);
            side.Normalize();
            Vector3 up = Vector3.Cross(side, direction);

            var m = Matrix4x4.identity;
            m.SetColumn(0, side * width);
            m.SetColumn(1, up * Mathf.Max(fade, 1e-3f));
            m.SetColumn(2, along);
            m.SetColumn(3, new Vector4(tail.x, tail.y, tail.z, 1f));
            return m;
        }

        /// <summary>The instance matrix for a flash: its centre, its size along x and its fade along y.</summary>
        public static Matrix4x4 FlashMatrix(Vector3 at, float size, float fade) =>
            Matrix4x4.TRS(at, Quaternion.identity, new Vector3(size, Mathf.Max(fade, 1e-3f), 1f));

        /// <summary>Where a streak's matrix says its head is. For tests.</summary>
        public static Vector3 HeadOf(in Matrix4x4 streak) =>
            (Vector3)streak.GetColumn(3) + (Vector3)streak.GetColumn(2);

        /// <summary>Where a streak's matrix says its tail is. For tests.</summary>
        public static Vector3 TailOf(in Matrix4x4 streak) => streak.GetColumn(3);

        /// <summary>A streak's fade, as its matrix carries it. For tests.</summary>
        public static float FadeOf(in Matrix4x4 streak) => ((Vector3)streak.GetColumn(1)).magnitude;

        /// <summary>The matrix of the <paramref name="index"/>th streak the last <see cref="Draw"/> submitted. For tests.</summary>
        public Matrix4x4 StreakDrawn(int index) => _streakMatrices[index];

        // ---------------------------------------------------------------- the events

        /// <summary>
        /// A moment of a gunfight, handed on by <c>CombatFeedback</c> in id order, before the frame's
        /// <see cref="Draw"/>: a <c>Shot</c> lights the muzzle and starts following its bullet (so a
        /// flight that falls wholly between two frames is still drawn), and a gun's <c>Hit</c> or
        /// <c>Miss</c> says where that bullet ended. Anything else is ignored.
        /// </summary>
        public void OnCombatEvent(in CombatEventView combatEvent, WorldSnapshot snapshot, PawnFigureDirector? figures)
        {
            if (!Enabled) return;
            switch (combatEvent.Kind)
            {
                case CombatEventKind.Shot:
                    OnShot(combatEvent, snapshot, figures);
                    return;
                case CombatEventKind.Hit:
                case CombatEventKind.Miss:
                    OnImpact(combatEvent, snapshot, figures);
                    return;
            }
        }

        void OnShot(in CombatEventView shot, WorldSnapshot snapshot, PawnFigureDirector? figures)
        {
            int shooter = shot.Attacker.Value;
            bool hasShooter = snapshot.TryGetPawn(shot.Attacker, out PawnView pawn);
            CellRef startCell = hasShooter ? pawn.Cell : shot.Cell;
            Vector3 muzzle = MuzzleOrChest(shot.Attacker, hasShooter, pawn, startCell, figures);

            // The flash: one per shooter, the newest wins.
            var flash = new Flash { Shooter = shooter, FireTick = shot.Tick, Cell = startCell, At = muzzle };
            int f = FindFlash(shooter);
            if (f >= 0) _flashes[f] = flash;
            else _flashes.Add(flash);

            if (FindStreak(shooter, shot.Tick) >= 0) return;
            _streaks.Add(new Streak
            {
                Shooter = shooter,
                FireTick = shot.Tick,
                ImpactTick = shot.Tick + Mathf.Max(1, shot.Amount),
                Target = shot.Target,
                Start = startCell,
                End = shot.Cell,
                From = muzzle,
                To = ChestOf(shot.Cell),
            });
        }

        void OnImpact(in CombatEventView impact, WorldSnapshot snapshot, PawnFigureDirector? figures)
        {
            // The bullet that landed on this tick from this shooter; a shooter who has died since
            // is reported as nobody, and then the bullet is the one landing on this tick.
            int index = -1;
            for (int i = 0; i < _streaks.Count; i++)
            {
                Streak s = _streaks[i];
                if (s.Resolved || s.ImpactTick != impact.Tick) continue;
                if (impact.Attacker.IsValid && s.Shooter != impact.Attacker.Value) continue;
                index = i;
                break;
            }
            if (index < 0) return;

            Streak streak = _streaks[index];
            streak.Resolved = true;
            if (impact.Kind == CombatEventKind.Hit && impact.Target.IsValid)
            {
                // Stopped on the body it struck — the intended target or a bystander on the line.
                Vector3 chest = default;
                bool drawn = figures != null && figures.TryGetChest(impact.Target, out chest);
                if (drawn) streak.To = chest;
                else if (snapshot.TryGetPawn(impact.Target, out PawnView struck)) streak.To = ChestOf(struck.Cell);
                else streak.To = ChestOf(impact.Cell);
            }
            else
            {
                // A miss, or a wall: the streak runs on past the body to where the bullet went down.
                streak.To = ChestOf(impact.Cell);
                streak.End = impact.Cell;
            }
            _streaks[index] = streak;
        }

        // ---------------------------------------------------------------- the frame

        /// <summary>
        /// Follow the frame's bullets, fade the landed ones by <paramref name="realSeconds"/>, and
        /// submit what the slice shows between <paramref name="lowest"/> and
        /// <paramref name="highest"/>: the streaks in one call, the flashes in another.
        /// </summary>
        /// <param name="tickAlpha">How far this frame sits between the last tick and the next; frozen while paused.</param>
        /// <param name="realSeconds">Unscaled seconds since the last frame: the afterimage fades on a pause too.</param>
        /// <param name="slice">When given, an end on a storey walls-down is hiding counts as undrawn.</param>
        public void Draw(ChunkRenderer renderer, WorldSnapshot snapshot, float tickAlpha, float realSeconds,
            int lowest, int highest, PawnFigureDirector? figures = null, SliceSettings? slice = null, int activeLayer = 0)
        {
            int callsBefore = renderer.DrawCalls;
            LastStreaksDrawn = 0;
            LastFlashesDrawn = 0;
            LastDrawCalls = 0;
            if (!Enabled)
            {
                _streaks.Clear();
                _flashes.Clear();
                return;
            }

            Follow(snapshot, figures, Mathf.Max(0f, realSeconds));

            float alpha = Mathf.Clamp01(tickAlpha);
            var bounds = new Bounds();
            bool anyBounds = false;
            int streaks = 0;

            for (int i = _streaks.Count - 1; i >= 0; i--)
            {
                Streak s = _streaks[i];
                float fade = 1f;
                float progress;
                if (s.Fading)
                {
                    s.FadeLeft -= Mathf.Max(0f, realSeconds);
                    if (s.FadeLeft <= 0f)
                    {
                        _streaks.RemoveAt(i);
                        continue;
                    }
                    _streaks[i] = s;
                    fade = s.FadeLeft / AfterimageSeconds;
                    progress = 1f;
                }
                else
                {
                    progress = FallArc.Progress(snapshot.Tick, alpha, s.FireTick, s.ImpactTick);
                }

                bool fromDrawn = Drawn(s.Start, lowest, highest, slice, activeLayer);
                bool toDrawn = Drawn(s.End, lowest, highest, slice, activeLayer);
                if (!VisibleSpan(s.From, s.To, fromDrawn, PlaneFor(s.Start.Y, lowest, highest),
                        toDrawn, PlaneFor(s.End.Y, lowest, highest), out float tMin, out float tMax))
                    continue;
                if (!StreakAt(s.From, s.To, progress, tMin, tMax, out Vector3 tail, out Vector3 head)) continue;

                if (streaks == _streakMatrices.Length) Array.Resize(ref _streakMatrices, streaks * 2);
                _streakMatrices[streaks++] = StreakMatrix(tail, head, StreakWidth, fade);
                Encapsulate(ref bounds, ref anyBounds, tail, head);
            }

            if (streaks > 0 && TracerMaterial() is Material tracer)
            {
                bounds.Expand(2f);
                renderer.DrawOverlayInstances(tracer, Quad(), _streakMatrices, streaks, bounds);
                LastStreaksDrawn = streaks;
            }

            DrawFlashes(renderer, snapshot, alpha, lowest, highest, figures, slice, activeLayer);
            LastDrawCalls = renderer.DrawCalls - callsBefore;
        }

        /// <summary>
        /// Match the snapshot's bullets to the ones being followed: a new one is taken up with its
        /// muzzle latched now; one followed and no longer published has landed and starts to fade.
        /// </summary>
        void Follow(WorldSnapshot snapshot, PawnFigureDirector? figures, float realSeconds)
        {
            for (int i = 0; i < _streaks.Count; i++)
            {
                Streak s = _streaks[i];
                s.Seen = false;
                _streaks[i] = s;
            }

            ReadOnlySpan<ProjectileView> bullets = snapshot.Projectiles;
            for (int b = 0; b < bullets.Length; b++)
            {
                ProjectileView bullet = bullets[b];
                int index = FindStreak(bullet.Shooter.Value, bullet.FireTick);
                Streak s;
                if (index < 0)
                {
                    bool hasShooter = snapshot.TryGetPawn(bullet.Shooter, out PawnView shooter);
                    s = new Streak
                    {
                        Shooter = bullet.Shooter.Value,
                        FireTick = bullet.FireTick,
                        From = MuzzleOrChest(bullet.Shooter, hasShooter, shooter, bullet.Start, figures),
                    };
                    _streaks.Add(s);
                    index = _streaks.Count - 1;
                }
                else s = _streaks[index];

                s.ImpactTick = bullet.ImpactTick;
                s.Target = bullet.Target;
                s.Start = bullet.Start;
                s.End = bullet.End;
                s.Seen = true;
                s.Fading = false;

                // Re-read every frame, so the streak arrives at the body wherever it has walked.
                bool holds = false, hasChest = false;
                Vector3 chest = default;
                if (bullet.Target.IsValid && snapshot.TryGetPawn(bullet.Target, out PawnView target))
                {
                    holds = HoldsEnd(target, bullet.End);
                    hasChest = holds && figures != null && figures.TryGetChest(bullet.Target, out chest);
                }
                s.To = EndPoint(bullet.End, holds, hasChest, chest);
                _streaks[index] = s;
            }

            // Whatever was followed and is not on this snapshot has landed: its last streak stays.
            for (int i = 0; i < _streaks.Count; i++)
            {
                Streak s = _streaks[i];
                if (s.Seen || s.Fading) continue;
                s.Fading = true;
                // The frame that finds it landed is its first frame of afterimage, at full strength.
                s.FadeLeft = AfterimageSeconds + realSeconds;
                _streaks[i] = s;
            }
        }

        void DrawFlashes(ChunkRenderer renderer, WorldSnapshot snapshot, float alpha, int lowest, int highest,
            PawnFigureDirector? figures, SliceSettings? slice, int activeLayer)
        {
            int count = 0;
            var bounds = new Bounds();
            bool anyBounds = false;
            float now = snapshot.Tick + alpha;

            for (int i = _flashes.Count - 1; i >= 0; i--)
            {
                Flash flash = _flashes[i];
                float age = now - flash.FireTick;
                if (age >= FlashTicks || age < -1f)
                {
                    _flashes.RemoveAt(i);
                    continue;
                }
                if (!Drawn(flash.Cell, lowest, highest, slice, activeLayer)) continue;

                // The muzzle moves with the recoil, so it is read again while the flash burns.
                var shooter = new PawnId(flash.Shooter);
                Vector3 at = flash.At;
                if (snapshot.TryGetPawn(shooter, out PawnView pawn) && TryMuzzle(shooter, pawn, figures, out Vector3 muzzle))
                    at = muzzle;

                float fade = 1f - Mathf.Clamp01(age / FlashTicks);
                if (count == _flashMatrices.Length) Array.Resize(ref _flashMatrices, count * 2);
                _flashMatrices[count++] = FlashMatrix(at, FlashSize, fade);
                Encapsulate(ref bounds, ref anyBounds, at, at);
            }

            if (count > 0 && FlashMaterial() is Material material)
            {
                bounds.Expand(FlashSize * 2f);
                renderer.DrawOverlayInstances(material, Quad(), _flashMatrices, count, bounds);
                LastFlashesDrawn = count;
            }
        }

        /// <summary>Forget every bullet and flash: a new world, a load.</summary>
        public void Clear()
        {
            _streaks.Clear();
            _flashes.Clear();
            _muzzles.Clear();
        }

        // ---------------------------------------------------------------- the muzzle

        Vector3 MuzzleOrChest(PawnId shooter, bool hasShooter, in PawnView pawn, CellRef startCell,
            PawnFigureDirector? figures)
        {
            if (hasShooter && TryMuzzle(shooter, pawn, figures, out Vector3 muzzle)) return muzzle;
            if (figures != null && figures.TryGetChest(shooter, out Vector3 chest)) return chest;
            return ChestOf(startCell);
        }

        /// <summary>The muzzle of the gun in the shooter's hand, when the figure has one drawn.</summary>
        bool TryMuzzle(PawnId shooter, in PawnView pawn, PawnFigureDirector? figures, out Vector3 muzzle)
        {
            muzzle = default;
            if (figures == null || !pawn.IsWeaponDrawn) return false;
            Transform? weapon = figures.WeaponOf(shooter);
            if (weapon == null || !weapon.gameObject.activeInHierarchy) return false;
            muzzle = weapon.TransformPoint(MuzzleOf(weapon));
            return true;
        }

        /// <summary>
        /// Where a prop's muzzle is in its own frame: the forward-most face of its meshes' bounds at
        /// the bore's height (<see cref="PistolMuzzle"/>'s y, kept inside the bounds), measured once
        /// per prop and remembered. A prop with no mesh takes <see cref="PistolMuzzle"/> as it is.
        /// </summary>
        public Vector3 MuzzleOf(Transform weapon)
        {
            int key = weapon.GetInstanceID();
            if (_muzzles.TryGetValue(key, out Vector3 known)) return known;

            _meshScratch.Clear();
            weapon.GetComponentsInChildren(true, _meshScratch);
            bool any = false;
            var box = new Bounds();
            Matrix4x4 toProp = weapon.worldToLocalMatrix;
            for (int i = 0; i < _meshScratch.Count; i++)
            {
                Mesh? mesh = _meshScratch[i].sharedMesh;
                if (mesh == null) continue;
                Matrix4x4 local = toProp * _meshScratch[i].transform.localToWorldMatrix;
                Bounds b = mesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents,
                        new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f));
                    Vector3 p = local.MultiplyPoint3x4(corner);
                    if (!any) { box = new Bounds(p, Vector3.zero); any = true; }
                    else box.Encapsulate(p);
                }
            }
            _meshScratch.Clear();

            Vector3 muzzle = any
                ? new Vector3(box.center.x, Mathf.Clamp(PistolMuzzle.y, box.min.y, box.max.y), box.max.z)
                : PistolMuzzle;
            _muzzles[key] = muzzle;
            return muzzle;
        }

        // ---------------------------------------------------------------- plumbing

        bool Drawn(CellRef cell, int lowest, int highest, SliceSettings? slice, int activeLayer) =>
            cell.Y >= lowest && cell.Y <= highest
            && !(slice != null && slice.HidesStandingAt(activeLayer, cell, _model));

        int FindStreak(int shooter, int fireTick)
        {
            for (int i = 0; i < _streaks.Count; i++)
                if (_streaks[i].Shooter == shooter && _streaks[i].FireTick == fireTick) return i;
            return -1;
        }

        int FindFlash(int shooter)
        {
            for (int i = 0; i < _flashes.Count; i++)
                if (_flashes[i].Shooter == shooter) return i;
            return -1;
        }

        static void Encapsulate(ref Bounds bounds, ref bool any, Vector3 a, Vector3 b)
        {
            if (!any)
            {
                bounds = new Bounds(a, Vector3.zero);
                any = true;
            }
            else bounds.Encapsulate(a);
            bounds.Encapsulate(b);
        }

        Mesh Quad()
        {
            if (_quad != null) return _quad;
            // x across (-0.5..0.5), z along (0 tail .. 1 head); the shader places every vertex.
            _quad = new Mesh { name = "Odyssey/TracerQuad", hideFlags = HideFlags.DontSave };
            _quad.SetVertices(new[]
            {
                new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f),
                new Vector3(0.5f, 0f, 1f), new Vector3(-0.5f, 0f, 1f),
            });
            _quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            _quad.bounds = new Bounds(new Vector3(0f, 0f, 0.5f), new Vector3(1f, 1f, 1f));
            return _quad;
        }

        Material? TracerMaterial() => _tracerMaterial ??= MakeMaterial("Odyssey/Tracer/Streak", TracerColour, TracerIntensity, 0f);

        Material? FlashMaterial() => _flashMaterial ??= MakeMaterial("Odyssey/Tracer/Flash", FlashColour, FlashIntensity, 1f);

        Material? MakeMaterial(string name, Color colour, float intensity, float billboard)
        {
            if (_shaderMissing) return null;
            // No fallback: the shape is read out of the matrix, so any other shader draws nonsense.
            Shader? shader = Shader.Find("Odyssey/Tracer");
            if (shader == null)
            {
                _shaderMissing = true;
                Debug.LogWarning("Odyssey/Tracer shader not found; bullets will not be drawn.");
                return null;
            }

            var material = new Material(shader)
            {
                name = name,
                enableInstancing = true,
                hideFlags = HideFlags.DontSave,
                renderQueue = RenderQueue,
            };
            material.SetColor(BaseColorId, colour);
            material.SetFloat(IntensityId, intensity);
            material.SetFloat(BillboardId, billboard);
            return material;
        }

        public void Dispose()
        {
            Clear();
            DestroyObject(_tracerMaterial);
            DestroyObject(_flashMaterial);
            DestroyObject(_quad);
            _tracerMaterial = _flashMaterial = null;
            _quad = null;
        }

        static void DestroyObject(UnityEngine.Object? thing)
        {
            if (thing == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(thing);
            else UnityEngine.Object.DestroyImmediate(thing);
        }
    }
}
