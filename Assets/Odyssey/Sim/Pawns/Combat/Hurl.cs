#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The butcher's thrown rock (design 62 §7a; owner, 2026-09-26: "I was able to get up onto a
    /// rock/one height and just shoot the pigs until they were dead"). A species can carry a second,
    /// ranged attack of its own — <see cref="SpeciesDef.hurl"/> — which it throws <b>only</b> at a
    /// colonist it can see and cannot yet strike from where it is because she is <b>above</b> it on a
    /// rock, a ledge or a roof, or because nothing it can walk reaches her. On open ground it closes
    /// and cleaves as before. A landed rock rolls the species' fling
    /// (<see cref="SweepDef.knockbackPerMille"/>), so it usually knocks her off her perch.
    ///
    /// <para><b>Reuses the pistol's machinery</b> (design 47): the ranged driver, the shot roll, the
    /// saved and hashed flight, the landing. What is its own is small and here: which armament a
    /// throw uses, what a thrown thing's weapon id is, when it throws, and its own clock
    /// (<see cref="Pawn.HurlReadyTick"/>), so a throw never costs the cleaver its cadence.</para>
    ///
    /// <para><b>A thrown thing's weapon id is below every item</b>: <see cref="WeaponOf"/> is
    /// <c>−100 − kind</c>, so a flight in the air knows which species threw it after the thrower has
    /// died, and presentation draws a rock where it would draw a tracer.</para>
    /// </summary>
    public static class Hurl
    {
        /// <summary>The weapon id of the first thrower's kind; every thrown thing is at or below it.</summary>
        public const int WeaponBase = -100;

        /// <summary>The weapon id a thrower of <paramref name="kind"/> reports and launches with.</summary>
        public static int WeaponOf(int kind) => WeaponBase - kind;

        /// <summary>Is this weapon id a thrown thing rather than an item or bare hands?</summary>
        public static bool IsHurl(int weapon) => weapon <= WeaponBase;

        /// <summary>The kind that threw it.</summary>
        public static int KindOf(int weapon) => WeaponBase - weapon;

        /// <summary>The attack a thrown thing lands with, off its thrower's species, or null.</summary>
        public static AttackDef? AttackOf(PawnContext ctx, int weapon)
        {
            int kind = KindOf(weapon);
            return (uint)kind < (uint)ctx.Content.Kinds.Length ? ctx.Content.SpeciesOf(kind).hurl : null;
        }

        /// <summary>
        /// What the ranged driver and the firing use: the throw for a species that has one, else what
        /// the pawn holds (<see cref="IWeaponRules.ArmamentOf"/>), which is every gunman.
        /// </summary>
        public static Armament ArmamentOf(Pawn pawn, PawnContext ctx) =>
            pawn.Species.hurl is AttackDef hurl && hurl.ranged != null
                ? new Armament(hurl, WeaponOf(pawn.Kind))
                : ctx.WeaponRules.ArmamentOf(pawn, ctx);

        /// <summary>Does this pawn throw, and is its throw ready at <paramref name="tick"/>?</summary>
        public static bool Ready(Pawn pawn, int tick) =>
            pawn.Species.hurl?.ranged != null && tick >= pawn.HurlReadyTick && !pawn.StunnedAt(tick);

        /// <summary>
        /// Whom a thrower throws at now, or null (design 62 §7a): the colonist it is after,
        /// <paramref name="foe"/>, when she is <b>above</b> it — a higher layer, which is a perch —
        /// out of reach, in range and in sight; or, when it can reach nobody at all, the nearest
        /// colonist it can see. Never one on its own level it can walk to: that one is cleaved.
        /// <b>Scales with the pawns on the board</b> for the unreachable case, as the gunman's sight
        /// scan does, and costs one comparison for anybody who does not throw.
        /// </summary>
        public static Pawn? TargetFor(Pawn pawn, PawnContext ctx, Pawn? foe)
        {
            if (!Ready(pawn, ctx.CurrentTick)) return null;
            RangedDef ranged = pawn.Species.hurl!.ranged!;
            if (!Ranged.CanShootFrom(ctx, pawn.Cell)) return null;
            if (foe != null)
            {
                GridSize size = ctx.Size;
                bool above = size.FromIndex(foe.Cell).Y > size.FromIndex(pawn.Cell).Y;
                return above && !Melee.InReach(ctx, pawn, foe, pawn.OwnMode) && Ranged.CanHit(ctx, pawn.Cell, foe.Cell, ranged)
                    ? foe : null;
            }
            return Ranged.NearestTargetInSight(ctx, pawn, ranged);
        }
    }
}
