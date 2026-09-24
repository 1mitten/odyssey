# Combat report — C1 to C7

Written 2026-09-24 on the Windows dev machine (AMD Ryzen 7 9800X3D, 32 GB, Windows 11 build 26200,
NVIDIA RTX 5070 Ti, Unity 6000.3.24f1, dotnet SDK 8.0.425 running the net8.0 test projects under
CoreCLR), branch `claude/combat-c7` from `main` at `3ca5098c` (PR #194). The design is
`docs/design/33-combat.md`, the plan `docs/plans/combat.md`, the reasoning `docs/journal.md`.

**This is a stop-for-review report.** The gate is green on three seeds with and without hostiles. It
found one real fault on the way — a save taken mid-raid did not resume the same — which is fixed
test-first (§3). What is worth an argument is in §5: the colony loses, at today's invented numbers,
and a list of questions only the owner can close.

## 1. What the line built

The owner's MVP (2026-09-23): *"a colonist can be put into an attack mode, ordered to move to
locations / attack something"* — animals, colonists, buildings and enemies — with melee weapons,
**starting small and making sure it works great**. Every decision is in design 33 §1.

| Unit | What | Design | State |
|---|---|---|---|
| **C1** draft and move | the drafted state and hold, T and the pane's button, right-click to move, the spread, the four-hour release, the run, the deeper red, the draw sound | §2 | played, merged (PR #176) |
| **C2** health and melee | hit points by species, downed at 0 and dead at −50 %, the swing, hit, dodge and damage rolls, the chase, revenge and self-defence, a debug bandit that hunts, corpses, the health tab, floating text, the clip layer | §3, §6A–§6C | played, merged (PR #180) |
| **C3** weapons | bat, crowbar, machete, sci-fi blade; stun from blunt; equip by right-click; drawn at the hip and in the hand; *Arm every colonist* | §4, §6D, §8b, §9c | played, merged (PR #180) |
| **CB** blood | a spurt and a mark per hit, a pool under the fallen, a fade by the tick, at most 19 draw calls | §10 | played (*"it seems great"*), merged (PR #182) |
| **C4** rescue | carried in the arms to her own or the nearest free bed, in it until whole; *No bed for the wounded* | §11 | merged (PR #194), awaiting play |
| **C5** friendly fire | Ctrl-attack, self-defence, being attacked and a death felt as memories | §12 | merged (PR #194), awaiting play |
| **C6** buildings as targets | hit points per edifice, the attack's building mode, demolished with no refund at zero | §13 | merged (PR #194), awaiting play |
| The owner's rounds | a bandit breaks in (§14b), the blow against the material (§14d), drafted colonists help within eight cells (§15), doors hold bandits and beds are spared (§16), bandits steal and leave (§17), the ring means an order and a colonist's response — fight back, defend, flee (§18), the stall at a one-sided wall and the squad upstairs (§19), the landing ring and dragging across the roster (§20) | §14–§20 | merged (PR #194), awaiting play |
| **C7** the gate | the ten-day gate with and without hostiles, the benchmark rows, these records | §21 | **this branch** |

Reactions, criticals and knockback (§9a–§9b), health on the cards (§9f), the sound of a blow (§9g),
spreading spawns (§9h) and the debug tab for a fight (§9i) came out of the playtests between.

## 2. The gate

| Part | Requirement | Result |
|---|---|---|
| 1 | A colony survives ten headless days, three seeds, no hostiles | **Green.** `SoakRunTests.TenDays` on seeds 1–3 and `TenDaysOnAField`, unchanged on today's `main`. |
| 2 | The same with hostiles, invariants every hour | **Green.** `BanditSoakTests.TheGateWithRaids` on seeds 1–3: seven raids, thirteen bandits, every hourly and per-tick invariant held (§21b lists them). |
| 3 | Same seed, same hash | **Green, every hour.** A lockstep twin hashes the same for all 240 hours on each seed. |
| 4 | Save mid-raid, load, same hash a day on | **Green after one fix** (§3). |
| 5 | Goldens unchanged | **Green.** `Golden.cs` untouched; the three golden cases pass in the fast and Long tiers. |
| 6 | Tiers | **Green.** Fast Sim **1,476**, Hud **1,076**; Long **48** (after merging `main`); content gates clean; EditMode **3,567 / 3,534 / 0 failed**; PlayMode **138 / 127 / 0 failed**; player build boots into a colony clean (§4). |

Per seed, with hostiles:

| Seed | Drafts | Swings at pawns | Downed (colonists) | Died | Got up | Rescues (failed) | Buildings broken | Thefts | Bandits left on the board | Colonists at day ten |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 4 | 220 | 7 (5) | 0 | 1 | 5 (1) | 16 of 16 | 12 | 1 | 0 up, 5 down |
| 2 | 9 | 420 | 14 (5) | 0 | 4 | 5 (1) | 16 of 16 | 8 | 5 | 0 up, 5 down |
| 3 | 6 | 327 | 12 (5) | 0 | 3 | 5 (2) | 16 of 16 | 7 | 6 | 2 up, 3 down |

The longest any bandit stood on *Fighting* at a building without a step or a swing was **95 ticks**
(bound 500, the owner's §19 report was 3,245). No attacker stood on a target already gone for more
than a tick. The longest a downed colonist lay rescuable and unrescued was **2,500 ticks** (bound
5,000). Nobody was freed from a wall. Nobody died.

## 3. What the gate found

**A save taken mid-raid resumed differently**, on two seeds of three. A pawn loaded in reach of its
target and part way through a step stood frozen: five branches of the attack driver wait for a step
under way to land and trust the mover to finish it along the path she holds, and a path is never
saved. Fixed in the driver (`LandTheStep`), test first
(`AttackDriverTests.ASaveTakenMidStepInReachResumesTheSame`, seen failing), and the gate's final
hashes were identical before and after, which is the evidence it touches only a loaded world.
`docs/bug-patterns.md` 2026-09-24; design 33 §21c.

It is the kind of fault only a crowd finds: every earlier round trip saved a duel mid-swing or a
thief mid-carry, and it takes an attacker in reach while still walking.

## 4. Measured

**The tick** (`TickBenchmarkTests`, Explicit, CoreCLR, the machine alone at 5 % CPU, two runs with
identical swings). Colonies built by `ColonyWorld.Build` on the played map, **not** the class's room
lattice, so the region counts (24,412 and 8,690) are a generated board's:

| Board | At peace | Fifty against ten | Fifty drafted against ten |
|---|---|---|---|
| 250 × 250 × 40 | 0.096–0.108 ms | 0.158–0.187 | 0.186–0.189 |
| Huge 240 × 240 × 16 | 0.093–0.098 | 0.117–0.125 | 0.160–0.174 |
| 120 × 120 × 16, twenty against twenty (the older row) | 0.036 | 0.087 | 0.091 |

A fight costs 0.02–0.09 ms a tick; the whole tick stays under 0.19 ms. The dearest part is the drafted
hold, whose scan for a threat or a fight to join scales with drafted × pawns (design 33 §21e).

**The frame** (`FrameTimeTests`, inside the full PlayMode tier, which started with no other Unity
process on the machine and the CPU at 7 %; 640 × 480, RTX 5070 Ti; not a play resolution and not the
target laptop):

| Test | Without | With | Draw calls | Where the difference is |
|---|---|---|---|---|
| `TheFrameWithAFightInView` (15 colonists, 10 bandits brawling) | 1.79 ms | 2.24 ms | 738 → 762 | Figures 0.111 → 0.238 ms, Overlays 0.010 → 0.033 |
| `TheBloodAtItsCap` (200 marks) | 1.87 ms | 1.95 ms | 738 → 741 (3 of blood) | Overlays 0.010 → 0.089 |

The C2 integration read the same brawl at 2.06 → 2.29 ms (§6E); both frames are faster today and the
fight now costs 0.45 ms rather than 0.23, all of it in the figures. That is the swing and reaction
work the playtest rounds added (§9a–§9c), and it is inside the 5 ms budget with room.

**The tiers**, on this branch merged with `main` (walls-down, Meadow M7 and selective CI came in
after the gate was written): fast Sim **1,476**, Hud **1,076**, 0 failed; Long **48**, 0 failed, every gate
number and final hash identical to the run before the merge; the three content gates clean. Before
the merge, alone on the machine: EditMode **3,567 total, 3,534 passed, 0 failed**; PlayMode
**138 total, 127 passed, 0 failed** (7 explicit, 11 ignored — the click-harness tests CLAUDE.md
names). The player build compiled, and booted straight into a colony for 45 s with no error or
exception in its log; the only warnings are the stylesheet's pseudo-classes, which predate combat.

## 5. Open

**The colony loses.** Ten days of raids leave two seeds of three with every colonist down; on seed 1
an armed, drafted squad of four gathered at the start lost to three bandits on day one. Every
number in the fight is invented (§1), so this reads the tuning and is the owner's to judge.

**Blocked on the owner, not built:**

- **Right-clicking a ladder, door or bed with drafted colonists attacks it** (§19c). The
  recommendation on the table is *right-click moves; Ctrl + right-click attacks your own*.
- **Drawing owed to a damaged building** (§13k): a hit-point bar, its hit points on the pane,
  cracks, a crash and dust when it falls, the lock-on ring round it.
- **The Thoughts tab**: the friendly-fire memories move mood with no name on screen (§12).
- **Kidnap** is seamed and does what theft does (§17f).
- The questions at the end of §15i, §17i, §18h and §20e, and whether a bandit that can reach no
  side of any colonist should break a building instead of queueing (§19d).

**Recorded, not fixed:** a thief climbs a ladder with a load a hauler would not (§17c); a patient
keeps her reservation on a demolished bed's head cell until she gets up (§11h); a carried patient
past the 64-figure cap is drawn standing at her carrier's cell (§11h); `Skill_Hauling` accrues
unseen (CLAUDE.md, known gaps). The draft sound's licence is still to be confirmed (§2i).

**Every combat row in `docs/plans/playtest-queue.md` stays open** until the owner plays it.

## 6. Stop

This is the end of the combat plan. The next decision is the owner's: play the combat rows in the
queue — C4 to C6 and the rounds after them have never been played — and answer §5.
