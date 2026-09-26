# Large enemies and melee knockback on a grid — prior art

**Question** (owner, 2026-09-26). We want a large melee enemy, the Pig Butcher: drawn about 1.5×
a person, very hard to kill, a wide hit area so many blows land, and a heavy swing that usually
knocks colonists back. How do shipped games and well-documented mods handle this on a grid?
(a) Units larger than one cell: a true footprint, or one cell drawn big? (b) Knockback, cleave and
sweep: where the displaced unit goes, what happens when it is blocked, and how the design stays
fair.

Researched by a subagent capped at 14 searches and 14 reads. Clean room: mechanics and numbers
only, in our own words.

## Findings

1. **RimWorld: every pawn takes one cell, whatever its size.**
   - Body size runs from 1.0 for a person to 4.0 for its largest creatures (and 5.0 for one).
   - Size scales how easy the creature is to shoot (×0.5 to ×2.0), its meat, weight and body heat.
   - It does **not** change how many cells the creature fills or where it can path. A large
     creature is drawn big over one cell and goes through ordinary doors. That last point is
     inferred from defence guides, not stated anywhere.
2. **RimWorld's melee hits one target and moves nobody.**
   - An enemy cell holds one melee attacker.
   - Blunt melee has a small chance to stun.
   - **Stagger** comes only from ranged stopping power, and only when that power is at least the
     target's body size: the target's speed drops to a sixth for 95 ticks. So a size-4 creature
     cannot be staggered by ordinary guns, which is how the game makes a big thing unstoppable.
3. **RimWorld mods.**
   - Combat Extended lets animals knock targets down and makes a blunt critical a stun.
   - An animation mod adds lassos that pull a target in, and players report they pull through
     embrasures, so there is no line check.
   - Nothing found resolves knockback into a chosen cell.
4. **Dwarf Fortress: every creature is one tile**, and a rabbit blocks a door as completely as a
   colossus.
   - A crowded corridor lets one creature stand and the rest crawl past.
   - A hard enough blow, scaled by relative mass, **flings** the victim until it hits something.
   - Hitting a wall or a tree adds blunt damage, and a creature struck by a flying body is knocked
     prone. Knockback is used to knock enemies into pits.
5. **XCOM 2: true 2×2 footprints** for its three largest enemies.
   - The corridor problem is dodged by **smashing through** destructible terrain.
   - They cannot change level without a ramp, beyond short ledges.
   - Pathing their centre produced visible faults, such as circling an obstacle and then
     destroying it.
6. **Into the Breach: a push is exactly one tile** in the attack's direction.
   - **A blocked push does not move the unit**; the pushed unit and whatever blocked it each take
     1 damage, and armour does not reduce it. There is no chain.
   - Pushed into a chasm, a ground unit dies; into water, a small ground enemy drowns.
   - Some units are immune to being pushed.
   - The main fairness rule is that every enemy attack is shown before it lands.
7. **Baldur's Gate 3: shove** is an opposed check, with distance from strength against weight,
   between 1 and 6 m.
   - If the landing spot is invalid (a wall), **the target does not move at all**.
   - A fall of 4 m or more does damage, and 8 m or more knocks prone.
8. **Divinity: Original Sin 2** makes a target immune to being knocked down while it has physical
   armour left, so the armour must be broken first. A line charge that hits everyone on its path is
   its built-in cleave.
9. **Anti-stunlock** in general:
   - diminishing returns on repeated control (50 %, 25 %, then immune within a window);
   - brief immunity on getting up.
10. **Clearance pathfinding** (Harabor's annotated A* and its hierarchical form). Each cell stores
    the side of the largest open square with its corner there, and an agent of size N may stand
    where that value is at least N.
    - It costs one integer per cell per capability, and an edit touches a bounded window.
    - It handles square footprints only.
    - Several sizes can share one abstract graph if its edges carry the largest size that fits.

## Options this suggests for our boss

Ranked by cost against what the owner asked for.

1. **One cell, drawn big, with a hitting pattern** — the RimWorld and Dwarf Fortress model.
   - The size lives in the attack: a swing covers the target's cell and its neighbours.
   - Knockback goes straight away from the boss, and a blocked knockback is a **slam**: the victim
     stops and takes extra damage (Into the Breach, Dwarf Fortress).
   - A victim knocked over a ledge falls.
   - Fairness comes from a telegraphed wind-up and brief knockback immunity on landing.
   - Hard to kill: a big pool, and immunity to stun and knockback.
   - **Cost:** no navigation change.
2. **Option 1 plus a bulky traversal mask** (XCOM): no ladders, and a door is something to break
   rather than open. **Cost:** very low here. `TraverseMode.Animal` already means exactly this, and
   the bandit already breaks doors down (design 33 §16b).
3. **A true 2×2 footprint** with a clearance map per layer.
   - **Cost:** high. It needs clearance kept up to date on every edit, a second region graph or
     annotated regions, four-cell occupancy and reservations, eviction for four cells, and rules
     for collapse and for the vertical.
   - A one-wide door becomes impassable, so it needs option 2's smashing anyway.
4. **One cell that claims its eight neighbours** while it stands. It looks big without big
   pathing. **Cost:** medium, with crowd and eviction edge cases, and it can trap colonists in a
   corridor.

**The owner took option 1 + 2** (2026-09-26), with a front arc of three, two-cell knockback with a
slam, and the butcher arriving by debug spawn only for now. Built as `docs/design/62-pig-butcher.md`.

## Sources

- https://rimworldwiki.com/wiki/Body_Size
- https://rimworldwiki.com/wiki/Combat
- https://rimworldwiki.com/wiki/Thrumbo
- https://rimworldwiki.com/wiki/Defense_tactics
- https://steamcommunity.com/sharedfiles/filedetails/?id=3154189813
- https://github.com/CombatExtendedRWMod/CombatExtended
- https://steamcommunity.com/workshop/filedetails/?id=2944488802
- https://www.dwarffortresswiki.org/index.php/Size
- https://dwarffortresswiki.org/index.php/DF2014:Combat
- https://dwarffortresswiki.org/index.php/Combat
- https://vigaroe.com/Analyses/XCOM2/Sectopod
- https://xcom.fandom.com/wiki/Gatekeeper_(XCOM_2)
- https://xcom.fandom.com/wiki/Andromedon_(XCOM_2)
- https://steamcommunity.com/app/590380/discussions/0/1697168437876692380/
- https://intothebreach.fandom.com/wiki/Water_Tile
- https://intothebreach.fandom.com/wiki/Watery_Grave
- https://bg3.wiki/wiki/Shove
- https://divinityoriginalsin2.wiki.fextralife.com/Physical+Armour
- https://divinity.fandom.com/wiki/Battering_Ram_(Original_Sin_2)
- https://maxroll.gg/wow/resources/crowd-control-diminishing-returns
- https://users.cecs.anu.edu.au/~dharabor/data/papers/harabor-aigamedev09.pdf
- https://users.cecs.anu.edu.au/~dharabor/data/papers/harabor-botea-keps08.pdf
- https://vav-labs.com/blog/godot-multi-size-agent-pathfinding-clearance/

## Confidence

**Medium-high.**
- **High:** Dwarf Fortress's one tile per creature; XCOM's 2×2 footprints and smashing; Into the
  Breach's bump and hazard rules; BG3's blocked shove; the clearance method; RimWorld's stagger
  numbers.
- **Medium:** RimWorld's large creatures being one cell and passing one-wide doors. It follows from
  the absence of any footprint rule and from the defence guides.

## Could not be determined

- The chance and duration of RimWorld's blunt melee stun. The pages were unavailable, so these are
  not from a primary source.
- Whether any Vanilla Expanded module has true melee knockback into cells.
- BG3's exact distance formula, or its rule for shoving into another creature.
- Dwarf Fortress's knockback thresholds and distances in numbers.
- How XCOM 2 represents its 2×2 units in the pathfinder: a clearance map, or a check of every tile
  of the footprint.
- Songs of Syx and Going Medieval: not reached within the cap.
