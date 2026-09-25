# The reference bullet: flight, miss scatter, interception and line of sight
*Lane A (RimWorld mechanics) · 2026-09-25 · capped at 12 searches and 10 reads (12 searches and 10 read attempts used; 8 of the 10 reads were refused by the egress proxy — rimworldwiki.com, steamcommunity.com, rimworldbase.com, nexusmods.com and web.archive.org are all blocked from this container — and the other 2 returned nothing on the question. Every number below therefore comes through the search engine's summaries of the wiki and Steam pages rather than from the pages themselves; where a detail is my recollection of the reference and no summary confirmed it, the finding says so.)*

> *Coordinator's note, 2026-09-25:* the container's proxy refused most content hosts, so the findings rest on search excerpts and the confidence line says so. After this ran the owner moved from "own layer only" to **shooting between layers** (design 44 §1); the recommendation's line-of-sight paragraph is superseded by `c-3d-shot-line.md`.

## Question

In the reference game, how does a bullet fly, where does a missed shot go, and what can a bullet hit on the way? Specifically: the projectile as a thing (speed, ticks to impact, straightness); how a miss is placed and what happens if the target has moved; interception of pawns and objects along the path; the line-of-sight rule; fire-at-will target choice, the adjacent enemy and dodging while aiming; friendly fire and its consequences; and the shape of the hunting job.

## Findings

**The projectile as a thing**

1. **The hit is decided at the trigger, not at the impact.** Whether a shot hits is rolled once when it is fired; the flight that follows is purely visual, and a shot rolled as a hit lands on its target *regardless of where the bullet is drawn landing or any movement by the defender* [wiki Combat via search; Steam discussion "Bullets Miss Visually But Still Hit The Target"; Steam workshop discussion on projectile speed: "hitting or missing is determined before it's even fired, and the speed of the projectile is purely cosmetic"]. This is the single largest difference from what the owner has decided (re-check at impact), and it is the one that matters for the recommendation.

2. **Speed is measured in cells per tick and the flight is a straight line in the grid plane.** The wiki's shield-belt text gives the unit — velocity in cells per tick, with shields "reliably stopping projectiles whose velocity is 1 cell/tick or less", and the shield scanning its perimeter cells every tick so that the projectile "must be in one of those cells at least once" — which also tells you the projectile has a definite cell position each tick [wiki via search]. Normal weapons "fire in a straight path like a traditional projectile and do not travel over walls or other obstructions"; mortar shells are the exception and fly overhead [wiki Weapons via search; wiki Mortar via search]. *Recollection, unconfirmed:* the weapon's projectile Def carries a speed number in hundredths of a cell per tick (a typical rifle round is around 0.7 cells a tick, so a 30-cell shot is about 43 ticks, three quarters of a second at 60 ticks a second), and ticks-to-impact is the straight-line distance in cells divided by that speed, rounded and never below 1. The mortar's arc is drawing only; the simulation still walks a straight line between the two cells.

3. **The reference's tick is the same as ours** — 60 ticks a second, so its tick counts translate one to one [wiki Template:Ticks via search; the rocketswarm launcher page gives its 60-tick (1 s) warm-up in the same unit].

**The miss**

4. **A missed shot lands a random distance from the aimed cell, and that distance grows with how bad the shot was.** "Whenever a regular ranged weapon misses, the projectile will land within a random distance from the original target depending on the actual accuracy of the shot based on the weapon and the shooter" [wiki Combat via search]. *Recollection, unconfirmed:* the landing cell is drawn from the ring pattern of cells around the aimed cell, the number of candidate cells growing as the hit chance falls, so a good shooter's misses cluster tight and a poor shooter's spray wide; the exact scale could not be determined from any summary.

5. **A stray shot has a 50 per cent chance of being allowed to hit anyone at all.** "Stray shots have a 50% chance of hitting nothing, and a 50% chance of hitting another target on the alternative cell it happens to land on" [wiki Combat via search, repeated in three summaries]. Read beside finding 10, which says bystanders intercept "if the shot is a hit", the shape is: a shot rolled as a hit may strike a bystander anywhere along its path; a shot rolled as a miss is given a second coin-flip at launch for whether it may strike *any* pawn (in flight or at its landing cell), and if the coin says no it can only hit the ground or an object. (The pairing of the two rules is my inference; each rule alone is sourced.)

6. **Weapons with a forced miss radius ignore accuracy altogether.** Mostly explosives: "weapons with this stat will always land within their forced miss radius, ignoring every factor to accuracy" [wiki Combat / Weapons via search].

7. **If the target moved, a hit still hits it.** Because the roll is at the trigger, a shot rolled as a hit is applied to the intended target wherever it now stands (finding 1). A shot rolled as a miss was never aimed at the target's new cell; it lands where finding 4 put it and finding 5 decides whether whoever is there can be struck. *Recollection, unconfirmed:* one further rule sits on the hit branch — a target that is lying down (downed or asleep) and further than about four and a half cells away only receives one hit in five that was rolled for it; the other four are quietly turned into misses, so a prone body is hard to finish off from range and easy to finish up close. The Downed wiki page carries this rule and could not be read.

8. **"Miss into cover"** *(recollection, unconfirmed)*: when the target is a pawn behind cover, the hit roll is taken in two stages — first whether the shot would have hit, then whether it gets past the cover — and a shot that passes the first and fails the second is redirected at one random cover object adjacent to the target, which takes the damage. This is why sandbags and walls wear down in a firefight.

**Interception along the path**

9. **Anything living in a crossed cell can catch the bullet, with a chance of 40 per cent times body size.** "Living pawns can intercept projectiles on their way to their target if the shot is a hit, with the chance of interception based on four factors including the body size of the pawn, specifically 40% multiplied by body size, with a minimum of 0.1 (4%) and a maximum of 2.0 (80%)" [wiki Combat via search]. A human is body size 1, so 40 per cent per crossed cell; a rat's 0.1 floor gives 4 per cent; a large animal caps at 80.

10. **There is a dead zone in front of the shooter, and the chance ramps up with distance.** "Interception is impossible at a distance of 5 or fewer tiles [from the shooter], and the chance scales up to the body size factor at a distance of 12 or more" [wiki Combat via search]. Community phrasing of the same rule: "there is a safe zone around the shooter in which allies cannot be hit by friendly fire at all, and colonists can always shoot over the shoulders of allies up to 5 tiles away from them, but any further and allies may be hit" [Steam discussions via search]; and "a pawn cannot hit a friendly pawn standing directly adjacent to them (any of the 8 tiles, including diagonals), but any further than that is friendly fire danger zone" [Steam guide "Everything About Combat" via search]. Note the ramp is by distance from the *shooter*, not from the target, and it is not friend-or-foe specific: an enemy three cells in front of the shooter does not intercept either.

11. **A downed pawn in the path is much less likely to catch the bullet.** "If the interceptor is downed the chance is reduced" [wiki Combat via search]. *Recollection:* to a tenth.

12. **Objects in the path intercept in proportion to their cover value; walls always stop the bullet.** "For non-pawn objects, this interception factor is based on Cover Effectiveness rather than body size" [wiki Combat via search]; "walls and other impassable objects will block line of fire" [wiki Drafting via search]; normal projectiles "do not travel over walls or other obstructions" [wiki Weapons via search]. *Recollection, unconfirmed:* only objects with a cover value above roughly a fifth are tested at all (a chair is not), the chance is a fraction of the cover value rather than the whole of it, anything that fills its cell completely (wall, closed door, natural rock) is a certain stop, an open door is nothing, and a tree is a cover object rather than a wall. Projectiles can hit "other things that are between the target and the shooter, such as stray animals" [Steam guide via search].

13. **Neither the shooter nor the aimed target is ever a "free" interception**, and the shooter's own allies get no discount — friendly fire is the same 40 per cent rule as anyone else [inferred from findings 9–10 and the whole genre of friendly-fire mods that exist to change exactly this: NoMoreFriendlyFire, Avoid Friendly Fire, RimWorld-FuckFriendlyFire; the last describes itself as "the chance of pawns' and turrets' bullets from hitting each other accidentally", adjustable 0–100 per cent].

**Line of sight**

14. **A shooter needs line of sight to the target cell, and full-cell things block it.** "Weapons can target any tile within their range and within line of sight"; "walls and other impassible objects will block line of fire" [wiki Combat / Drafting via search]. Mortars are the exception: "fired over walls" [wiki Mortar via search].

15. **The line is Bresenham-like and not perfectly symmetric.** A Workshop mod called "Line Of Sight Fix" exists [Steam Workshop listing via search], which is evidence that the vanilla line can let A see B without B seeing A. *Recollection, unconfirmed:* the walk is a Bresenham line from shooter cell to target cell with half-cell offsets to soften the asymmetry, each intermediate cell tested for "can be seen over" (walls, closed doors and rock fail it; sandbags, furniture, trees and open doors pass); when the shooter's own cell has no line, the cells adjacent to the shooter are tried and the shooter *leans* out from one of them — the familiar peek-round-the-corner pose — with the bullet launched from the lean cell. Smoke does not block the line but sharply lowers the hit chance of shots through it (a Smoke shell page exists; not read).

**Fire at will, hold position, the adjacent enemy, dodging**

16. **Drafted shooters fire on their own at the nearest enemy they can see.** "While drafted, pawns with a ranged weapon will automatically fire at enemies they see"; "ranged will attack the closest enemy by themselves" [wiki Drafting via search; Steam discussion via search]. Fire at will is on whenever a pawn is drafted and can be toggled off; there is no standing hold-fire preference, and the community workaround for a permanent hold is to order the pawn to attack itself [Steam discussions via search]. A right-clicked target overrides the automatic choice [wiki Combat via search].

17. **An adjacent enemy is fought in melee, gun or no gun.** "When adjacent to an enemy, pawns will always fight in melee — even if they are holding a gun" [Steam guide via search]. (Every gun carries a poor melee attack for this.)

18. **A pawn aiming or firing cannot dodge.** "Characters will not dodge while aiming or firing a ranged weapon"; dodge is a stat capped at 80 per cent and driven heavily by movement (moving carries 1,800 per cent importance in its formula) [wiki Melee Dodge Chance via search]. A dodged blow "will always miss, regardless of whether or not it is supposed to hit" [same].

**Friendly fire's consequence**

19. **No dedicated thought was found.** The summaries of the Thoughts and Events pages list only the ordinary consequences — pain, "colonist died", "friend died", "observed a corpse" [wiki Thoughts / Events via search]. *Recollection:* vanilla has no "shot by a colonist" opinion or mood, and the shooter suffers nothing social for it; the cost is the wound.

**Hunting with a gun**

20. **The job is: shoot from maximum range until the animal is down, walk up and cut its throat, haul the corpse home, repeat.** "Hunters with ranged weapons will proceed to shoot them at maximum range, before executing them with a neck cut when they are downed"; "after killing their target, they will haul the carcass to a stockpile zone even if they are naturally incapable of hauling", then resume hunting [wiki Hunt via search].

## Recommendation

The owner has already fixed the frame — real flight in the sim, roll at the fire tick, impact tick = fire + distance over speed, re-check at impact, a miss scatters near the target, a bullet can strike the first pawn or wall on its line, own layer only. What follows ranks the ways of filling that frame and backs one for each part. Every one of them is one Def field or one rule, and none touches a cell, a save section or the hash beyond the projectile record itself.

**Flight.** (1) *Straight line, ticks = max(1, ceil(cells ÷ speed)), speed a per-projectile Def in cells per tick, the projectile advancing a fixed fraction per tick and inspecting every cell crossed since its last position* — the reference's shape, and it fits the supply drop's launch-tick / landing-tick pattern that presentation already draws between. (2) Per-tick ray marching with a physical velocity in metres — more work for nothing a player can see. (3) Instant hit at the fire tick with a cosmetic flight — the reference as it actually is, ruled out by the owner. **Back (1)**, at about 0.7 cells a tick for a rifle (a 20-cell shot lands in half a second), and treat "re-check at impact" narrowly: the shot is rolled at the fire tick; at the impact tick the bullet hits the intended target if it is still in the destination cell, else it is applied to whoever is there under the bystander rule below, else the ground. That keeps the reference's "a good shot rewards the shooter" while giving the owner the dodge-by-moving he wants.

**Miss scatter.** (1) *A cell drawn from the ring pattern round the aimed cell, the disc's radius = clamp(round((1 − hitChance) × 3), 1, 3) cells, excluding the aimed cell*, so a skilled miss is a near miss. (2) A fixed one-cell ring — simplest, and in a 2.5 m cell a stray that always lands within 2.5 m of a colonist's ally will feel like a rifle that never misses by more than an arm. (3) A Gaussian over/undershoot along the shot line — realistic and hard to read on a grid. **Back (1)**; the constant 3 is a guess to be tuned by eye, which is the cheapest experiment there is. Weapons with a forced miss radius (none yet) replace this rule rather than add to it.

**Interception.** (1) *The reference's rule as it stands*: for every cell the bullet crosses, excluding the launch cell and the destination, a pawn in it is struck with chance 0.4 × clamp(bodySize, 0.1, 2.0) × ramp(distance from shooter, 0 at ≤ 5 cells, 1 at ≥ 12), downed pawns at a tenth of that, cover-class objects at a fraction of their cover value, a wall or a closed door always; the aimed target and the shooter never intercept "freely"; allies get no discount; and a shot rolled as a *miss* is given one further coin-flip at launch for whether it may strike any pawn at all. (2) The owner's phrase read literally — the first pawn or wall on the line is always hit — which makes a second rank of shooters impossible and every doorway a killing box for one's own side. (3) No interception; the bullet only exists at its destination. **Back (1)** with one simplification for now: drop the 5-to-12 ramp to a flat dead zone of two cells in front of the shooter, because our cells are 2.5 m and the reference's dead zone of five would be 12.5 m of untouchable air in a colony that fights at ten cells. **(1) and (2) are the real tie**, and the observation that breaks it is whether two colonists can stand one behind the other and both fire. The cheapest experiment is a headless fight — four colonists in a 2 × 2 block, three bandits at eight cells, 200 shots — counting friendly hits per 100 shots under each rule; under (2) the back rank's count will be the whole of its shooting and the result decides itself.

**Line of sight.** (1) *A Bresenham walk between the two cell centres on the shooter's layer, blocked by anything that fills its cell (wall, closed door, rock), made symmetric by walking it both ways and accepting either*, with the same cell list reused as the bullet's path so what a shooter can see is exactly what the bullet can cross. (2) The reference's asymmetric single walk with half-offsets — it shipped a mod to fix it. (3) A supercover (thick) line — symmetric by construction but so permissive that a diagonal gap between two wall corners is a firing slit. **Back (1)**, sight to the target *cell* rather than the thing, and add the reference's *lean* (try the shooter's adjacent cells when its own has no line) as a later unit, because it needs a pose. Trees do not block sight; they intercept at their cover value, which is what makes a wood dangerous rather than opaque.

**Fire at will.** Nearest visible hostile within range, re-chosen when the current one is dead, downed or out of sight; an ordered target holds until it is dead, downed or the order is cancelled. An adjacent hostile is met in melee with the gun's own weak blow, as the reference does — it fits the melee system that exists and stops a gunner in a doorway being immune. No dodge while aiming or cooling down, the day dodging exists. No mood or opinion consequence for friendly fire; the wound is the cost.

**Hunting.** One job in three toils — approach to range and shoot until down, walk up and finish in melee, haul the corpse — and the haul is part of the job so a hunter who cannot haul still brings the meat home.

## Sources

- https://rimworldwiki.com/wiki/Combat
- https://rimworldwiki.com/wiki/Cover
- https://rimworldwiki.com/wiki/Shooting_Accuracy
- https://rimworldwiki.com/wiki/Weapons
- https://rimworldwiki.com/wiki/Drafting
- https://rimworldwiki.com/wiki/Melee_Dodge_Chance
- https://rimworldwiki.com/wiki/Hunt
- https://rimworldwiki.com/wiki/Downed
- https://rimworldwiki.com/wiki/Mortar
- https://rimworldwiki.com/wiki/Roof
- https://rimworldwiki.com/wiki/Thoughts
- https://rimworldwiki.com/wiki/Template:Ticks
- https://steamcommunity.com/sharedfiles/filedetails/?id=3154189813
- https://steamcommunity.com/app/294100/discussions/0/5167301764848817814/
- https://steamcommunity.com/app/294100/discussions/0/350540973994877354/
- https://steamcommunity.com/app/294100/discussions/0/3013438019096430370/
- https://steamcommunity.com/sharedfiles/filedetails/?id=2663381778
- https://steamcommunity.com/sharedfiles/filedetails/?id=1134165362
- https://github.com/WilliamVenner/RimWorld-FuckFriendlyFire
- https://github.com/falconne/AvoidFriendlyFire
- https://www.nexusmods.com/rimworld/mods/197

## Confidence

**Medium.** The three numeric rules that matter most — 40 per cent × body size clamped to 4–80 per cent, no interception within 5 cells of the shooter rising to full at 12, and the 50 per cent coin-flip on whether a stray may hit anyone — each appeared in two or more independent search summaries of the wiki's Combat page and agree with each other and with memory; but no page was read directly, the miss-scatter scale and the object-interception fraction rest on recollection, and every domain that holds the primary text was blocked from this container.

## Could not be determined

- The exact scale of the miss scatter (how the candidate radius grows with the hit chance) and its cap.
- The exact multiplier applied to an object's cover value for in-flight interception, and the cover threshold below which an object is not tested.
- The downed-interceptor reduction (recollection says a tenth) and the downed-*target* rule (recollection says four hits in five become misses beyond ~4.5 cells).
- Typical projectile speeds in cells per tick per weapon (recollection says ~0.55–0.7 for guns, slower for rockets and shells; the shield-belt page's "≤ 1 cell/tick" is the only sourced bound).
- Whether the reference's line-of-sight walk is retried in the reverse direction, and the exact lean rule.
- Whether smoke blocks sight or only lowers the hit chance, and by how much.
- Whether vanilla applies any friendly-fire avoidance to a drafted colonist's automatic targeting (the AI scores targets with allies in the way lower — recollection — but colonists appear to take the nearest regardless, which is why the "Avoid Friendly Fire" mod exists).

## Layer question 5: can you shoot up and down?

Nothing found that bears on it directly: the reference has no vertical layers, so a bullet has no height and no shot ever crosses a floor. The one vertical idea it has is *overhead* flight — mortar shells "fly overhead", are not blocked by walls or intercepted by pawns in flight, are "only stopped by overhead mountain", and "constructed roofs can be broken through" so that a shell landing on a roof collapses it onto the room [wiki Mortar / Roof via search]. That is a rule about the *landing cell's* roof, not about aiming between heights; it gives us nothing for own-layer-only shooting except a precedent that a projectile class can carry a flag saying which of walls and roofs it respects.
