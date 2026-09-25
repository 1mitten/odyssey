# Lane A19 — Mental breaks: the taxonomy by tier, and the four to build first

## Question

The mental-break taxonomy in the reference (RimWorld), in enough detail to build the four breaks the
project has chosen — **sulk** (minor), **binge** (major), **tantrum** (major), **berserk** (extreme) —
and to know what was left out: every break by tier with its mechanics, duration, ending, weight and
requirements; which are trait-gated; the mental states that are not breaks; how a break interacts
with drafting, sleep, being downed and arrest; the catharsis memory; whether selection is weighted
and whether a tier can fall through to a shallower one; the notification per tier; and a
recommendation for this project.

Already known and not re-researched: thresholds 35 / 20 / 5 %, asleep pawns never break, one break
(wander) already exists.

**How this lane was run.** Every wiki-class domain is blocked by this container's egress proxy
(rimworldwiki.com and its www. mirror, the Fandom wiki, Steam discussions, the Wayback Machine, a
reader proxy). The numbers below marked **[wiki]** were pulled out of rimworldwiki.com's pages by
the search tool's own page reads, and are as reliable as a direct read. Everything marked
**[memory]** is paraphrased from prior knowledge of the reference and is *unverified in this lane*;
treat it as a design starting point, not a citation. Nothing here is copied — no Def XML,
decompiled code or flavour text.

## Findings

### A correction to the "known" cadence, first

The current wiki gives the mean time between breaks while below a threshold as **minor 4 days,
major 0.8 days, extreme 0.5 days** [wiki], not the 10 / 3 / 0.7 the brief carries. The older
figures date from before the 1.0 rework; the brief should adopt 4 / 0.8 / 0.5. The three
thresholds are related as *major = 4/7 of minor, extreme = 1/7 of minor* [wiki], which is how a
trait that shifts the minor line (Nervous, Volatile, Iron-willed, Steadfast) moves all three at
once — one number per pawn, two ratios, not three fields.

### 1. Every break by tier

Each tier has its own pool [wiki]. Since the 1.0 rework "aside from some exceptional cases, all
breaks are more or less equally probable" within a tier [wiki, patch notes], so a weight of 1 is the
norm and the exceptions are the interesting part.

| Break | Tier | What it does | Duration | Ends when | Requires | Weight |
|---|---|---|---|---|---|---|
| Wander in confusion ("daze") | minor | walks aimlessly, ignores work, will eat anything including food it is normally forbidden [wiki] | ~half a day [memory] | timer (recovery pauses while asleep [wiki]) | — | 1 [memory] |
| Hide in room | minor | goes to its own bedroom and stays there, doing nothing | ~1 day ("hiding in the pawn's room for a day" [wiki]) | timer | a bedroom of its own [memory] | 1 [memory] |
| Sad wander | minor | wanders, no work, weeping — visible to others | ~half a day [memory] | timer | — | 1 [memory] |
| Insulting spree | minor | walks up to colonists and insults them repeatedly, souring their mood and relations | a few hours [memory] | timer | another colonist to insult | 1 [memory] |
| Food binge | minor [wiki] | eats from stores over and over regardless of hunger, wasting food | a few hours to ~half a day [memory] | timer, or nothing left to eat | food it can reach | 1 [memory] |
| Social drug binge | minor [memory] | takes social drugs repeatedly | hours [memory] | timer / drugs run out | social drugs on the map | 1 [memory] |
| Tantrum | major | walks to random nearby things (items, buildings) and smashes them until it stops | short — a few hours [memory] | timer, or nothing left in reach | — | 1 [memory] |
| Targeted tantrum | major [wiki] | picks one specific, valuable item or building at random and goes to destroy it [wiki] | until the target is destroyed | target destroyed, or timer | a valuable thing | 1 [memory] |
| Corpse obsession | major [wiki] | digs up a corpse and drops it on the dining table or in a busy spot, then the break ends [wiki] | until dropped | the drop | a buried corpse | 1 [memory] |
| Jailbreaker | major [wiki] and extreme [wiki] | walks to a random prisoner able to break out and triggers an immediate prison break [wiki] | until triggered | the break-out | a prisoner | 1 (extreme pool) [wiki] |
| Sadistic rage | major [wiki] | beats a prisoner | hours [memory] | timer / prisoner downed | a prisoner | 1 [memory] |
| Hard drug binge | major [memory] and extreme [wiki] | takes hard drugs repeatedly, with addiction risk | hours to a day [memory] | timer / drugs run out | hard drugs on the map | 1 (extreme pool) [wiki] |
| Slaughterer | major [wiki] and extreme [wiki] | kills the colony's tame animals one after another | hours [memory] | timer / no animals | a tame animal | 0.75 (extreme pool) [wiki] |
| Fire-starting spree | major [memory] | sets fires around the colony | hours [memory] | timer | the **Pyromaniac** trait | trait-only |
| Berserk | extreme | attacks the nearest pawn, colonist or animal, with whatever it holds; is not arrestable | recovery MTB 10,000 ticks (4 h), min 40,000 (16 h), max 60,000 (24 h) [wiki, as found — the min looks long against the MTB and could not be re-checked] | timer, or being downed | — | 1 [wiki] |
| Murderous rage | extreme | chooses one colonist and hunts them to kill | until resolved [memory] | target or attacker downed / dead, or timer | another colonist | 1 × population curve [wiki] |
| Run wild | extreme | strips off, runs into the wild and leaves the colony (becomes a wild human that can be recaptured) | instant departure [memory] | leaving | — | 0.5 × population curve [wiki] |
| Give up and exit | extreme | walks off the map edge and is gone [wiki: "Gave up"] | until off the map | reaching the edge | a reachable edge | 1 × population curve [wiki] |
| Catatonic breakdown | extreme | collapses, unconscious, for days — a health condition rather than a state [wiki: "may have different mechanics"] | days [memory] | the condition lifting | — | 1 [wiki] |

The population curve: the last three extreme breaks (murderous rage, run wild, give up) are scaled
by colony size — rarer in a very small colony, commoner in a large one — and the curve was not
designed past a hundred colonists [wiki]. That is the reference protecting a three-person start
from losing a third of itself to one bad day.

Anomaly DLC adds a further set of "anomalous" breaks with their own, smaller, catharsis [wiki];
out of scope here.

### 2. Trait gating

Only one break is gated to a trait in the base game: **fire-starting spree** needs Pyromaniac
[memory]. Everything else is gated by a *resource* — a prisoner, a corpse, a tame animal, drugs,
food, a bedroom, a map edge — and the gate is checked at selection time, so a colony that has none
of the thing never sees the break. Traits otherwise act on the *threshold* (Nervous +8, Volatile
+12, Steadfast −9, Iron-willed −18 [memory]) and not on the pool.

### 3. Mental states that are not breaks

- **Daze** is the same state as wander in confusion but reached from a different cause (a psychic
  weapon, drug side-effects, a healing ritual) [memory]; the wiki notes a dazed pawn's recovery
  timer **pauses while asleep** [wiki].
- **Catatonic breakdown** is delivered as a *health condition* — the pawn is downed and the
  condition carries the days — which is why the wiki lists it beside Run wild and Fleeing fire as
  breaks that "do not" follow the normal rules [wiki].
- **Fleeing fire** and **panic flee** are mental states with no mood cause [memory]: a pawn next to
  fire runs, a defeated raider runs.
- **Manhunter** is an animal's aggressive state, same machinery [memory].

**How they are modelled** [memory]: a pawn carries at most one mental state, with a start tick.
While it is set, the pawn's think tree is *bypassed at the root* in favour of the state's own job
giver (a wander giver for the daze, an attack-nearest giver for berserk, a find-and-eat giver for
the binge). Each tick the state ages; once past its minimum it rolls a recovery against its MTB,
and past its maximum it ends regardless. Ending fires the catharsis and restores the tree. The same
one-slot object is what drafting, arrest and downing check.

### 4. Interaction with drafting, sleep, downing, arrest

- **Sleep**: to break a pawn must be awake and able to move [wiki]; the risk is *paused* rather
  than accumulated while asleep or unconscious [wiki]. A break in progress is not ended by
  tiredness — the daze pauses its timer while the pawn sleeps [wiki].
- **Drafting**: a pawn in a mental state cannot be drafted, and a drafted pawn that breaks is
  undrafted [memory]. Drafting therefore neither prevents nor ends a break.
- **Downing**: being downed ends an aggressive break immediately [memory]; the wiki says downing
  or arresting a pawn interrupts the break and forfeits the catharsis — **except berserk, which
  grants catharsis even when interrupted** [wiki].
- **Arrest**: the vanilla game does not allow arrest during an aggressive mental state; violent
  breakers must be subdued (melee until downed), and arrest is for the non-aggressive ones
  [github issue, paraphrasing vanilla]. Arresting a non-aggressive breaker ends the break at once
  and forfeits catharsis [wiki]. **What the player can do**: wait it out (and get the catharsis);
  arrest a non-violent one (no catharsis); subdue a violent one (no catharsis, except berserk);
  or lower the thing they are smashing/eating out of reach — forbidding the food ends a binge in
  practice [memory].

### 5. Catharsis and aftermath

- **Catharsis: +40 mood for 3 days** [wiki], stacking up to five times with diminishing returns
  [wiki]. It requires the break to run its course [wiki]. Anomalous breaks give +30 ("void
  catharsis") instead [wiki].
- Aftermath per break is whatever the break did: the binge leaves food gone (and a hangover or
  addiction if it was drugs), the tantrum leaves wreckage, the insult spree leaves soured
  relations, corpse obsession leaves a body on the table and the "saw a corpse" thoughts it causes.
  The reference has no separate "aftermath memory" per break beyond catharsis [memory].

### 6. Selection: weighted, and falling through tiers

- Within a tier, selection is a **weighted random** over the breaks whose requirements are met,
  by the commonality weights above [wiki]; weights are mostly 1, so it is nearly uniform.
- **A deeper tier falls through to a shallower one only when nothing in its own pool is
  possible** [memory — the fallback exists but its exact order could not be re-checked]. A pawn
  below the extreme line therefore normally gets an extreme break, not a lucky minor one.

### 7. Notification

- Aggressive breaks show a **red** icon over the pawn; non-aggressive ones **yellow** [wiki].
- Letters [memory]: non-aggressive breaks arrive as a *bad, non-urgent* letter (the orange kind);
  aggressive breaks as a *threat* letter (red, and the largest — berserk, murderous rage — pause
  the game).
- Before any break, the mood bar's three lines show the risk, and a standing **"break risk"
  alert** names the colonist and the tier they are below [wiki: the 35 / 20 / 5 % lines are drawn
  on the mood bar].

## Recommendation

For this project — a colony sim with drafting, downing, doors, stores, beds, buildings as targets
and one wander break already in — the four chosen breaks should be built as **one mental-state
slot on the pawn with one timer rule**, and four job givers behind it. The rules, in the
project's units (a day is 60,000 ticks):

**Common machinery (do this once).**
- One `MentalState` per pawn: kind, start tick, min, max, recovery-MTB in ticks. Past `min`, roll
  recovery each needs interval; past `max`, end. The slot is saved and hashed.
- While set, the job system takes the state's giver before anything else; work is refused; the
  pawn cannot be drafted and is undrafted on entry.
- Selection: the tier from the mood against 35 / 20 / 5 %, MTB **4 / 0.8 / 0.5 days** (adopt the
  current figures), never while asleep or downed, weighted-random within the tier over breaks
  whose requirement holds, **falling through to the next shallower tier when the pool is empty**
  (a two-colonist colony with no food in store must still be able to break).
- Ending naturally gives **catharsis +40 for 3 days**; being downed or arrested ends the state with
  no catharsis — except berserk, which keeps it. A single stacking cap of 5 with diminishing
  effect can wait until mood has enough sources to need it.
- One alert while below a line ("*Name* is close to breaking / breaking down"), one event row on
  entry, red or yellow by whether the break is aggressive.

**Sulk (minor, weight 1).** Requirement: a bed the pawn owns. Giver: walk to own bed, lie down
awake, refuse every job; if no bed, fall back to the existing wander (so sulk *replaces* hide in
room and is the project's second minor beside wander). min 4 h, MTB 8 h, max 24 h. Not aggressive;
arrestable; ends on arrest.

**Binge (major, weight 1).** Requirement: any reachable edible in a store or on the ground the
pawn may eat. Giver: the existing eat job, re-issued regardless of hunger, choosing the *nearest*
edible rather than the best, and ignoring the store's own reservations only in the sense that
it never waits — if nothing is reachable the state ends early. min 2 h, MTB 4 h, max 12 h. Not
aggressive; arrestable. Aftermath is simply the food gone (no hangover until drugs exist).

**Tantrum (major, weight 1).** Requirement: a standing building or item within ~15 cells that is
not a wall of the room the pawn is in and not its own bed. Target: a random such thing, nearest-
weighted, re-chosen after each one is destroyed or after 10 strokes; deal building damage with the
combat unit's building-target path (bare hands, so it *damages* rather than levels). min 1 h,
MTB 2 h, max 4 h, and end early when nothing is in reach. Not aggressive to pawns, but the
project should treat it as **non-arrestable while swinging** (arrest ends it after the stroke).
Reserve targeted tantrum (one most-valuable thing) as a later variant of the same giver.

**Berserk (extreme, weight 1).** Requirement: none. Target: the nearest reachable pawn — colonist
or animal — within 20 cells, re-targeted every strike; wander when none is in range. Uses the
combat attack driver with whatever is held (this is where "an unordered fight ends in downs" does
the work: a berserker downs, never kills). min 4 h, MTB 4 h, max 12 h — shorter than the
reference's 16–24 h because a downing ends it anyway and the colony is small. Aggressive:
**not arrestable**, ended by being downed, and it still grants catharsis. Draft others to subdue.

**Deliberately left out of the first cut, and why**: sad wander and insulting spree (need a social
layer to be seen), corpse obsession and jailbreaker and sadistic rage (need prisoners or burial),
the drug binges and fire-starting (no drugs, no traits), slaughterer (no tame animals yet), run
wild (an exit with a recapture mechanic behind it).

**The three to build next, in order:**
1. **Give up and leave** (extreme, weight 1 × population curve) — walk to the nearest reachable
   map edge and be removed. The project already has both halves: bandits and wildlife leave by
   the edge and `PawnRegistry.Despawn` exists. Add the population curve here first, so a colony of
   three cannot lose one to a die roll.
2. **Catatonic breakdown** (extreme, weight 1) — downed for 1–3 days as a health condition, not a
   state; the combat unit's downed path carries it and the bed-rescue job already exists. This is
   the extreme break that punishes without violence, which the owner's "downs, never deaths" rule
   wants.
3. **Targeted tantrum** (major, weight 1) — the tantrum giver with one most-valuable target; cheap
   once tantrum exists, and the first break that makes the player *care what they built where*.

## Sources

- https://rimworldwiki.com/wiki/Mental_break — read only through the search tool's page snippets
  (the domain is blocked at this container's proxy): tiers, MTB 4 / 0.8 / 0.5 days, threshold
  ratios 4/7 and 1/7, the extreme-tier weights and population curve, berserk's recovery numbers,
  targeted tantrum / corpse obsession / jailbreaker mechanics, the icon colours, the "awake and
  able to move" rule, the daze timer pausing in sleep, the catatonic / run wild / fleeing fire note.
- https://rimworldwiki.com/wiki/Mental_Break_Threshold — via snippets: the 35 / 20 / 5 % lines.
- https://rimworldwiki.com/wiki/Thoughts/Memory_Misc and https://rimworldwiki.com/wiki/Mood — via
  snippets: catharsis +40 for 3 days, stacking to 5, forfeited on interruption, berserk exception,
  void catharsis +30; mood bar paused while asleep.
- https://store.steampowered.com/news/posts/?appids=294100 (1.0 patch notes, via snippet): breaks
  made "more or less equally probable".
- https://github.com/davidarcher/rimgovernor/issues/404 — read directly: vanilla forbids arrest
  during an aggressive mental state; subdue instead.
- https://rimworld.wiki/mental-breaks/berserk/ — surfaced by search, not read (cap reached); a
  possible unblocked mirror for a future lane.

## Confidence

**Medium.** The cadence, thresholds, catharsis, extreme-tier weights, arrest rule and the sleep
rule are wiki-backed and I would build on them. The per-break durations, the minor and major
weights, the letter levels and the tier fall-through are from memory of the reference and are
marked so in the table; they are the right shape but the numbers should be re-read the day a
container can reach rimworldwiki.com or the rimworld.wiki mirror.

## Could not be determined

- The exact min / max / MTB durations for every break except berserk (and berserk's own min of
  16 h reads oddly against its 4 h MTB and could not be re-checked).
- The exact commonality weights of the minor and major pools (assumed 1 from the "equally
  probable" note).
- Whether a deeper tier can pick a shallower break *by chance* rather than only by fallback when
  its pool is empty.
- The precise letter type per break (which are red, which pause the game).
- The population curve's actual points.
- Whether tantrum is classed as aggressive for the arrest rule.
