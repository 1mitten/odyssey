#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Defs;

namespace Odyssey.Sim.Pawns
{
    /// <summary>How a blow does its damage (design 33 §1). Blunt may stun; sharp does not (bleeding waits for a body).</summary>
    public enum DamageKind : byte
    {
        Blunt = 0,
        Sharp = 1,

        /// <summary>
        /// A bullet (design 47 §2c): never stuns, bleeds as sharp does, and strikes a building at
        /// ×1 (<see cref="BuildingTargets"/>). Never rolled through <see cref="MeleeRules"/>, so
        /// the sharp knockback chance there never reaches it.
        /// </summary>
        Bullet = 2,
    }

    /// <summary>
    /// Which family of drawn swing an attack is, so presentation can pick a clip row without the
    /// simulation naming a clip (design 33 §1, <c>docs/research/synty-sword-combat.md</c>): bare
    /// hands, a light one-handed swing, a heavy one, or an animal's bite.
    /// </summary>
    public enum AttackStyle : byte
    {
        Fists = 0,
        Light = 1,
        Heavy = 2,
        Bite = 3,

        /// <summary>A pistol (design 47 §4b): the aim, the shot and the recoil, not a swing.</summary>
        Pistol = 4,
    }

    /// <summary>
    /// One way of hitting something: a weapon's block (<see cref="ItemDef.weapon"/>), an
    /// animal's teeth (<see cref="SpeciesDef.naturalAttack"/>) or a person's bare hands
    /// (<see cref="CombatDef.fists"/>). One attack per weapon, deliberately — the reference's
    /// weighted choice between edge, point and handle was not taken (a-10).
    ///
    /// <para><b>Damage is in whole points here and thousandths on the pawn.</b> Content reads as
    /// the numbers the owner set (fists about 4, weapons 7–10); the rules multiply by 1,000 when
    /// they apply it, so a heal of a fraction of a point a tick is exact.</para>
    /// </summary>
    public class AttackDef
    {
        /// <summary>Points of damage a landed blow does before the spread.</summary>
        public int damage = 4;

        /// <summary>Ticks from one swing starting to the next one being allowed to start.</summary>
        public int cooldownTicks = 120;

        /// <summary>
        /// Ticks from the swing starting to it landing. The drawn clip is played at the speed that
        /// puts its authored impact frame on this tick.
        /// </summary>
        public int windupTicks = 20;

        public DamageKind damageKind = DamageKind.Blunt;

        /// <summary>Chance per mille that a landed blow stuns. Blunt only; zero for sharp.</summary>
        public int stunPerMille;

        /// <summary>How long a stun lasts, in ticks.</summary>
        public int stunTicks;

        /// <summary>Melee experience one swing is worth, landed or not, in thousandths of a point (the SkillDef unit).</summary>
        public int experiencePerSwing;

        public AttackStyle style = AttackStyle.Fists;

        /// <summary>
        /// A gun's block (design 47 §3b), null for everything that swings. <b>For a gun
        /// <see cref="windupTicks"/> is the aim and <see cref="cooldownTicks"/> the cooldown</b> —
        /// the melee fields under the melee names, so one clock serves both — and
        /// <see cref="experiencePerSwing"/> is Shooting experience a shot, hit or miss.
        /// </summary>
        public RangedDef? ranged;

        /// <summary>Is this a gun?</summary>
        public bool IsRanged => ranged != null;
    }

    /// <summary>One point of a distance curve: at this many millimetres, this per mille.</summary>
    public class DistancePoint
    {
        public int mm;
        public int perMille;
    }

    /// <summary>
    /// What makes an attack a gun's (design 47 §3b): how far it reaches, how fast its bullet flies,
    /// and how its accuracy falls away with distance. Distances are millimetres over the true
    /// 2.5 × 2.5 × 3 m cell (<c>GridSize.CellSizeXZMm</c>), so a shot up a layer is longer
    /// than one along it and the fall-off, which is per gun, sees it.
    /// </summary>
    public class RangedDef
    {
        /// <summary>The furthest a target may be, straight-line, in millimetres.</summary>
        public int rangeMm = 26_000;

        /// <summary>How far the bullet flies a tick. The flight is <c>ceil(distance / speed)</c> ticks, never under one.</summary>
        public int speedMmPerTick = 1_000;

        /// <summary>
        /// The gun's own accuracy by distance, per mille (design 47 §2a): linear between the points
        /// and flat past the ends. This is where "inaccurate with distance depending on the gun"
        /// lives.
        /// </summary>
        public List<DistancePoint> accuracyByDistance = new List<DistancePoint>();

        /// <summary>
        /// The gun's own blow, for an enemy within reach (design 47 §12; the reference's rule: "when
        /// adjacent to an enemy, pawns will always fight in melee, even if they are holding a gun").
        /// A gun-holder never shoots at an enemy she could strike; she clubs it with this, on her
        /// Melee skill, and shoots again when it steps away. Null would leave her bare-fisted.
        /// </summary>
        public AttackDef? melee;

        /// <summary>The gun's accuracy at a distance, per mille. Integer throughout.</summary>
        public int AccuracyPerMille(int distanceMm)
        {
            var curve = accuracyByDistance;
            if (curve == null || curve.Count == 0) return 1_000;
            if (distanceMm <= curve[0].mm) return curve[0].perMille;
            for (int i = 1; i < curve.Count; i++)
            {
                DistancePoint hi = curve[i];
                if (distanceMm > hi.mm) continue;
                DistancePoint lo = curve[i - 1];
                long span = hi.mm - lo.mm;
                if (span <= 0) return hi.perMille;
                return (int)(lo.perMille + (hi.perMille - lo.perMille) * (long)(distanceMm - lo.mm) / span);
            }
            return curve[curve.Count - 1].perMille;
        }
    }

    /// <summary>One point of a level curve: at this skill level, this per mille.</summary>
    public class CurvePoint
    {
        public int level;
        public int perMille;
    }

    /// <summary>
    /// The fight's numbers (design 33 §1, §3), in one record so the rules have one place to read
    /// them and a mod has one place to change them. <c>Combat.xml</c> holds the values and says
    /// which are the owner's and which are invented.
    /// </summary>
    public class CombatDef : Def
    {
        /// <summary>
        /// The attacker's chance to land a swing, per mille, by melee level (owner: 50 % at 0,
        /// 80 % at 10, 90 % at 20). Linear between the points and flat past the ends.
        /// </summary>
        public List<CurvePoint> hitCurve = new List<CurvePoint>();

        /// <summary>
        /// The defender's chance to dodge a swing that would have landed, per mille, by the
        /// <b>defender's</b> melee level (owner: 0 % at 0, 10 % at 10, 30 % at 20).
        /// </summary>
        public List<CurvePoint> dodgeCurve = new List<CurvePoint>();

        /// <summary>A person's bare hands: about 4 damage every two seconds.</summary>
        public AttackDef fists = new AttackDef();

        /// <summary>How far a landed blow may stray from its figure, per mille either way (±20 %).</summary>
        public int damageSpreadPerMille = 200;

        /// <summary>
        /// Hit points a colonist lying in a bed recovers per in-game day, in thousandths
        /// (owner: a colonist heals only in a bed).
        /// </summary>
        public int bedHealPerDay = 20_000;

        /// <summary>Hit points an animal recovers per in-game day wherever it lies, in thousandths
        /// (owner: an animal recovers on its own).</summary>
        public int animalHealPerDay = 12_000;

        /// <summary>
        /// A downed pawn gets back up once its hit points reach this fraction of its pool, per
        /// mille. Above nought on purpose, so a pawn is not knocked down by the first blow after
        /// it rises.
        /// </summary>
        public int downedRecoverAtPerMille = 150;

        // ---- treatment (design 37 §4) ---------------------------------------------------------

        /// <summary>A treatment never lifts a pawn past this fraction of its pool, per mille (owner: 800).</summary>
        public int treatCapPerMille = 800;

        /// <summary>Treating yourself heals this fraction of what a doctor would, per mille (owner: half).</summary>
        public int selfHealPerMille = 500;

        /// <summary>And never past this fraction of the pool, per mille (owner: 600).</summary>
        public int selfCapPerMille = 600;

        /// <summary>And takes this many times as long (owner: 3).</summary>
        public int selfWorkFactor = 3;

        /// <summary>Whole hit points a doctor's dressing restores with no supplies at all (owner: about 10).</summary>
        public int bareHeal = 10;

        /// <summary>A standing colonist below this fraction of her pool goes to bed as a patient, per mille (INVENTED: 500).</summary>
        public int patientBelowPerMille = 500;

        /// <summary>A patient gets up at this fraction of her pool, per mille (INVENTED: 800, the treatment cap).</summary>
        public int patientReleasePerMille = 800;

        /// <summary>Ticks after a treatment before the same pawn may be treated again (INVENTED: a quarter of a day).</summary>
        public int treatedCooldownTicks = 15_000;

        /// <summary>How often a chase re-plans its path to a moving target, in ticks.</summary>
        public int chaseRepathTicks = 60;

        /// <summary>
        /// How long an attack nobody ordered — a hunt, a revenge, a self-defence — keeps its first
        /// target before thinking again, in ticks, so a nearer colonist is noticed. INVENTED (lane A,
        /// five seconds at one speed); moved from a constant on the driver at the integration.
        /// </summary>
        public int rechooseTicks = 300;

        /// <summary>
        /// How near a fight must be for a drafted colonist on her hold to join it (design 33 §15;
        /// owner, 2026-09-24: <i>"if there is any fight going on nearby ... they will help"</i>): the
        /// colonist being attacked and her attacker both within this many cells on the ground
        /// (Chebyshev, the same layer or one either side). INVENTED: eight cells, 20 m.
        /// </summary>
        public int helpRadiusCells = 8;

        /// <summary>How long a colonist struck by a colonist fights back for, in ticks.</summary>
        public int retaliationTicks = 1_200;

        /// <summary>How long an animal that turned stays vengeful, in ticks (the reference's 10,000 floor).</summary>
        public int revengeTicks = 10_000;

        /// <summary>How far a fleeing animal runs from whatever hurt it, in cells.</summary>
        public int fleeCells = 12;

        /// <summary>
        /// The chance that a blow which lands is critical, per mille, before the attacker's level
        /// adds to it (owner, 2026-09-23: 10 %; design 33 §9b).
        /// </summary>
        public int critChancePerMille = 100;

        /// <summary>
        /// What every four whole melee levels of the attacker add to <see cref="critChancePerMille"/>,
        /// per mille (owner: 1 % per 4 levels). An animal counts its species' meleeSkill.
        /// </summary>
        public int critPerMillePerFourLevels = 10;

        /// <summary>A critical blow's damage, per mille of the blow it would have been (owner: ×1.5).</summary>
        public int critDamagePerMille = 1_500;

        /// <summary>The chance a critical blow knocks its target back a tile, per mille (owner: 50 %).</summary>
        public int knockbackPerMille = 500;

        /// <summary>The same chance for a blunt weapon's critical, per mille (owner: 75 %).</summary>
        public int knockbackBluntPerMille = 750;

        /// <summary>
        /// How long a pawn knocked back lies where it landed before it stands, in ticks (owner:
        /// about a second and a half).
        /// </summary>
        public int knockedDownTicks = 90;

        // ── The gun (design 47 §2a, §2c, §3b). ──────────────────────────────────────────────

        /// <summary>
        /// The shooter's chance not to miss <b>per 2.5 m cell</b>, per mille, by Shooting level: the
        /// hit chance is this raised to the distance in cells. The reference's 89 %/m at level 0
        /// and ~98 %/m at 20, raised to the power 2.5; the middle point is ours.
        /// </summary>
        public List<CurvePoint> shootingPerCell = new List<CurvePoint>();

        /// <summary>No shot is surer to miss than this, per mille (the reference's 2 %).</summary>
        public int hitFloorPerMille = 20;

        // ── Cover (design 53 §2). The angle bands are geometry and live in `Cover`; these are the
        // numbers a play session tunes. ─────────────────────────────────────────────────────────

        /// <summary>
        /// What a thing that fills its whole cell — a wall, a closed door, a pillar, a rock face —
        /// is worth as cover, per mille (design 53 §3). The reference's 75 %: a wall beside you is
        /// not invulnerability, and full cover is not certainty.
        /// </summary>
        public int fullFillCoverPerMille = 750;

        /// <summary>
        /// The descent, as a tangent in per mille, below which <b>low</b> cover (sandbags, a
        /// barricade, a bush, furniture) keeps all of its value (design 53 §2b): about 10°. INVENTED.
        /// </summary>
        public int coverLowFullTanPerMille = 176;

        /// <summary>…and at and past which it keeps none: about 35°. Linear in the tangent between. INVENTED.</summary>
        public int coverLowGoneTanPerMille = 700;

        /// <summary>The same for <b>tall</b> cover (a wall, a rock face, a door, a tree): full to about 30°. INVENTED.</summary>
        public int coverTallFullTanPerMille = 577;

        /// <summary>…and gone by about 60°. INVENTED.</summary>
        public int coverTallGoneTanPerMille = 1_732;

        /// <summary>
        /// The chance a stray bullet crossing a cover cell is caught by it, per mille of that
        /// thing's base cover (design 53 §2e, the owner: "yes, at a fraction"), before the dead
        /// zone's ramp from the shooter. INVENTED: the reference's constant was not recovered.
        /// </summary>
        public int coverInterceptPerMille = 500;

        /// <summary>
        /// The cover from its current target at and above which a fighter crouches behind a low
        /// piece (design 53 §8a), per mille. A pose only; the rule never reads it. INVENTED.
        /// </summary>
        public int coverCrouchPerMille = 200;

        /// <summary>A bystander nearer the shooter than this is never hit by her bullet, in millimetres.</summary>
        public int interceptDeadZoneMm = 5_000;

        /// <summary>…and one this far or further takes its full chance; linear between.</summary>
        public int interceptFullMm = 12_000;

        /// <summary>The widest a miss scatters round its target, in cells. INVENTED.</summary>
        public int scatterMaxCells = 3;

        /// <summary>
        /// How fast the scatter grows with how bad the shot was: <c>r = min(max, 1 + (1000 − hit‰) ×
        /// this / 1000)</c> cells. INVENTED.
        /// </summary>
        public int scatterPerMissPerMille = 3;

        /// <summary>How often a drafted gun-holder looks for a target in sight, in ticks, phase-spread by id. INVENTED.</summary>
        public int rangedScanTicks = 15;

        /// <summary>The per-cell chance at a Shooting level, per mille.</summary>
        public int ShootingPerCellPerMille(int level) => Evaluate(shootingPerCell, level);

        /// <summary>
        /// The chance of a critical for an attacker at <paramref name="level"/>, per mille:
        /// <see cref="critChancePerMille"/> plus <see cref="critPerMillePerFourLevels"/> for every
        /// four whole levels. Integer, so it replays exactly.
        /// </summary>
        public int CritChancePerMille(int level) =>
            critChancePerMille + critPerMillePerFourLevels * (level > 0 ? level / 4 : 0);

        /// <summary>
        /// The curve's value at a level: linear between the two points either side, flat beyond
        /// the ends, and a flat 0 for an empty curve. Integer throughout, so it hashes and replays
        /// exactly. Content arithmetic, not a rule: what to do with the chance is the rules'.
        /// </summary>
        public static int Evaluate(List<CurvePoint> curve, int level)
        {
            if (curve == null || curve.Count == 0) return 0;
            if (level <= curve[0].level) return curve[0].perMille;

            for (int i = 1; i < curve.Count; i++)
            {
                CurvePoint hi = curve[i];
                if (level > hi.level) continue;

                CurvePoint lo = curve[i - 1];
                int span = hi.level - lo.level;
                if (span <= 0) return hi.perMille;
                return lo.perMille + (hi.perMille - lo.perMille) * (level - lo.level) / span;
            }

            return curve[curve.Count - 1].perMille;
        }

        /// <summary>The hit chance at a melee level, per mille.</summary>
        public int HitChancePerMille(int level) => Evaluate(hitCurve, level);

        /// <summary>The dodge chance at a melee level, per mille.</summary>
        public int DodgeChancePerMille(int level) => Evaluate(dodgeCurve, level);
    }
}
