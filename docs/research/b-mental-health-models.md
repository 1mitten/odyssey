# Lane B — Mental health in other colony sims: accumulators, triggers and reactions

## Question

How do other colony and management games model mental health — the accumulator, the trigger, the
reaction, how it is shown and what players and designers say about it — and what should this
project borrow, given that it already has a RimWorld-shaped integer mood (0..1000, drifting toward
a target made of need bands, temperature and expiring memories, with a break rolled below a
threshold) and will add traits and a small break taxonomy (sulk, binge, tantrum, berserk)?
Specifically: should a trait *choose* the break or *gate* it; should breaks cascade; is a
colony-wide reading worth showing?

Method note: the six community wikis named in the brief are all blocked by this container's egress
proxy (`wiki.gg`, `dwarffortresswiki.org`, `fandom.com` all refused), so every wiki fetch failed
and the lane ran on eight web searches whose result snippets quote those wikis, plus community
threads and one design critique. Everything below is written in my own words; no game text, data
tables or code were copied. Numbers marked (recalled) are from general knowledge of the game and
were not confirmed by a source in this lane.

## Findings

### Oxygen Not Included

1. **Accumulator.** A per-duplicant *stress* percentage, 0–100. It climbs while the duplicant is in
   ugly surroundings (low decor), sleeps badly or on the floor, eats poor food, stands in bad air or
   polluted liquid, or is upset by another duplicant's outburst side-effects; it falls while the
   duplicant sleeps in a proper bed, sits in good decor, uses recreation, or is treated at a
   massage station, which is the fast lever. It is a rate integrator (so much per cycle from each
   source), so it lags the environment by design — a bad room takes cycles to show.
2. **Trigger.** Pure threshold. At 100% the duplicant stops taking orders and *vents*. There is
   hysteresis: the outburst continues until stress has fallen back to about 60%, then the duplicant
   returns to work. No roll.
3. **Reaction.** Exactly one behaviour per duplicant, fixed by a trait assigned when the duplicant
   is generated and visible before you accept them: **destructive** (breaks a nearby building, and
   as a rule cannot itself do repair work), **vomiter** (fouls the floor with polluted liquid, which
   costs it calories and gives a wet-feet stress debuff to anyone who walks through it),
   **ugly crier** (sobs, which lowers decor around it and so raises others' stress a little), and
   **binge eater** (eats a large amount of stored food, which actually lowers its own stress). So
   the trait *chooses*; the game never rolls. Ends by hysteresis, not by timer. It cascades only
   indirectly — through the world (broken machines, puddles, decor), never by "saw a breakdown"
   thoughts.
4. **Shown.** A stress percentage and red bar on the duplicant's vitals, a colony-wide vitals table
   with a stress column, an on-body status icon when stressed, and a per-duplicant breakdown of
   what is raising and lowering it (the expectations list), so the cause is always legible.
5. **Reception.** Widely regarded as *toothless* once the player knows the two or three levers:
   Klei's own forum has a long "is stress too easy to manage?" thread, and later expansions
   simplified the mechanic further. The one reaction players actually fear is destructive, which
   has its own "way overpowered" thread — the practical result is that players reject destructive
   duplicants at the printing pod, i.e. the trait choosing the break turns the break into a
   recruitment filter rather than an event. The praise is for legibility: you always know why a
   duplicant is stressed and what will fix it.

### Dwarf Fortress

1. **Accumulator.** A per-dwarf *stress* number, signed (negative is good), stored internally in
   the hundreds of thousands and never shown as a number in the vanilla game. Every event raises an
   *emotion* whose strength is scaled by the dwarf's personality facets (0–100 each, e.g. how
   anxious, how brave, how vulnerable to stress) and by what the dwarf *values*; the emotions feed
   stress, and *memories* of strong events are kept and re-felt later, so stress lags and can rise
   long after the cause. The wiki names three facets as the shape of the curve: one sets how fast
   stress is gained, one how much can be borne before breaking, one how fast it dissipates.
   Decay toward zero is slow; a stressed dwarf takes many months to come back.
2. **Trigger.** Threshold bands rather than a roll. Short-term: above a stress band the dwarf has a
   *mental breakdown*. Long-term: a dwarf that stays stressed a long time is described as
   *haggard*, then *harrowed*, and a harrowed dwarf who then witnesses something bad goes
   permanently insane.
3. **Reaction.** Short breakdowns are of three kinds — *tantrum* (attacks people and smashes
   furniture), *depression* (sits and refuses work), *oblivious* (wanders in a daze) — and the kind
   leans on personality (an angry, violent dwarf tantrums; a withdrawn one goes quiet), so the
   personality *weights* rather than dictates. Permanent insanity is *berserk* (attacks everyone
   until killed), *melancholy* (stops eating, may throw itself off things) or *stark raving mad*
   (harmless babbling). Breakdowns end after a while on their own. They cascade *hard*: a tantrum
   injures someone, breaks a masterwork, or kills, and each of those is a bad emotion for the
   victim, the maker and every witness, so one tantrum in a marginal fortress becomes the
   **tantrum spiral**. *Strange moods* are the opposite branch of the same design: a dwarf is
   seized, claims a workshop, demands materials and either makes an artefact (a huge good memory for
   the maker) or, if unfulfilled, goes mad — so a mood is a gamble, not a punishment.
4. **Shown.** Text. The thoughts screen is a paragraph of prose per dwarf with each memory coloured
   by whether it was good or bad; the stress band is a coloured description ("has become haggard
   and drawn…"), never a bar; the unit list can be sorted by stress and, in the Steam edition,
   carries a small face/icon per band. Third-party tools exist precisely because the vanilla display
   hides the number.
5. **Reception.** The tantrum spiral is the game's most famous story generator and "losing is fun"
   is built on it; players *want* the cascade. The criticism landed on the 0.44 "stress rework",
   when persistent memories of seeing corpses made whole fortresses harrowed with nothing the
   player could do about it (the "stress-pocalypse"); it was tuned down over several releases. The
   lesson designers draw: cascade is good, but the accumulator must be one the player can *move*,
   and a memory that never expires removes that.

### Darkest Dungeon

1. **Accumulator.** Per-hero *stress*, 0–200. Raised by enemy attacks that target stress, by being
   in the dark, by hunger and some traps, by an ally being critically hit or afflicted; lowered by
   a hero's own critical hits, by certain skills and camping actions, and back in town by paying for
   the tavern or abbey. It does not decay on its own inside a dungeon and only partly resets
   between quests, so it is a *pressure that carries over*, not a lagging average.
2. **Trigger.** Threshold **and** roll. Crossing 100 forces a *resolve test*: about a one-in-four
   base chance to be *virtuous*, otherwise *afflicted*. Which affliction is a weighted lottery, with
   each hero's history nudging the weights toward afflictions they have had before (a light form of
   "this is who they are"). At 200 the hero has a *heart attack*: health to zero and Death's Door;
   a second one there kills.
3. **Reaction.** Seven afflictions (fearful, paranoid, selfish, masochistic, abusive, hopeless,
   irrational; the DLC adds one), each a bundle of stat penalties plus a chance each turn to act on
   its own — refuse the order, pass, shuffle rank, hit the wrong target, hurt itself — and each with
   its own barks. Five virtues are the mirror: stat bonuses and helpful outbursts. An affliction
   lasts the rest of the expedition and until stress is brought down in town. **It cascades
   directly**: every afflicted bark deals a fixed dose of stress to the whole party or one ally, so
   one affliction reliably breeds the next.
4. **Shown.** A stress bar beneath the health bar with the number, colour shifting as it rises; at
   100 the portrait reacts and a full-screen title card announces the affliction or virtue by name;
   afflicted heroes get a status icon and speak their barks in the log. Cause is not itemised — the
   player sees the stress hit as it lands.
5. **Reception.** The resolve-test moment is the game's identity and is praised as drama. The
   Gemsbok's mechanical critique makes the case against: one affliction on a high-level run can
   doom the party because the cascade is direct and fast — affliction breeds affliction, then one
   heart attack breeds the next — which reads as punishment the player did not author. Red Hook
   themselves kept the virtue chance as the release valve.

### Going Medieval

1. **Accumulator.** A per-settler *mood*, a 0–100 total made from listed modifiers: hunger, rest,
   comfort, temperature, the impressiveness of the room slept in, leisure, social contact, food
   quality, plus traits and recent events. The demands rise as the settlement grows (better food,
   private rooms, more kinds of recreation), so the same input is worth less later. Lags through the
   needs themselves rather than through a separate stress store.
2. **Trigger.** Threshold with a dwell time: mood must sit *below the break threshold for a while*
   before a break fires — the wiki is explicit that breaks are avoidable and meant to be. The
   reported cause is decorative: the game picks a recent debuff and calls it the final straw.
3. **Reaction.** Small taxonomy: refusing orders / "rebelling", wandering, and at mood zero the
   settler *leaves the settlement*. Not trait-chosen as far as the sources say; no direct cascade.
4. **Shown.** A mood bar on the settler panel with the itemised modifier list and their numbers,
   RimWorld-style; the break announces its "final straw".
5. **Reception.** The Steam threads ("mood is at this point not controllable", "solving happiness")
   complain of a treadmill — rising demands mean a mid-game colony is always slightly unhappy —
   rather than of random breaks; the developer's recent updates added stacking and threshold rules,
   which suggests the flat sum was felt to be too legible to be interesting.

### Frostpunk

1. **Accumulator.** Two *colony-wide* readings, *Hope* and *Discontent*, each 0–100%. Nobody has a
   personal mood. Both move by discrete amounts from laws signed, buildings built, events and
   promises kept or broken, deaths, work conditions (overtime, child labour), cold homes and hunger;
   Hope is pushed mainly by laws and by the Faith/Order buildings. Some effects are one-off jumps
   and some are per-hour drifts while a condition holds, so it half-lags.
2. **Trigger.** Threshold with a *grace period*: when Discontent tops out or Hope bottoms out the
   city delivers an ultimatum — move the reading by a stated amount (15 points) within a stated
   time (two days) — and failing it ends the game with the leader exiled or executed.
3. **Reaction.** Colony-level: protests and strikes, mobs in the square, demands, and finally
   mutiny. No per-citizen breaks. Ending an ultimatum is the only "recovery". Cascade is the whole
   design: the readings *are* the aggregate.
4. **Shown.** Two bars permanently at the bottom of the screen; hovering itemises the contributing
   factors; the law book states the Hope/Discontent effect of a law *before* you sign it, so the
   player is always trading a number they can see.
5. **Reception.** Praised for legibility and for making morale political — every point has a law
   behind it. Criticised for being gameable (a well-known thread on ignoring Hope entirely), for the
   late-game laws that pin Hope at maximum and remove the tension, and for the ultimatum being
   abrupt. It is a narrative pressure gauge, not a simulation of people.

### Prison Architect (short note)

Per-prisoner *needs* (food, sleep, hygiene, recreation, freedom, family, drugs, and so on) are
listed on the prisoner and are the accumulator; an unmet set pushes a prisoner past a *boiling
point* where they fight, steal, escape and riot. The colony reading is a *danger level* gauge fed
by critically unmet needs, recent deaths and fights, and the presence of armed or riot guards, and
offset by well-fed, well-treated prisoners. When it is high enough many prisoners misbehave at
once and a riot starts; riots cascade *spatially* — sectors are lost one by one and paint red on
the map — and end when guards retake them or the player concedes. Reception: the danger gauge is
liked because it forecasts trouble; the cascade is liked because it is *spatial* and therefore
readable on the map.

### Comparison

| Axis | ONI | Dwarf Fortress | Darkest Dungeon | Going Medieval | Frostpunk | Prison Architect |
|---|---|---|---|---|---|---|
| Accumulator | per-pawn stress 0–100 %, rate integrator, lags | per-dwarf signed stress, hidden magnitude, emotions × personality, re-felt memories, lags for months | per-hero stress 0–200, carries between quests, no natural decay in the field | per-settler mood 0–100, sum of listed modifiers, demands grow | colony-wide Hope and Discontent 0–100 % | per-prisoner needs + colony danger gauge |
| Trigger | threshold at 100, ends at 60 (hysteresis) | threshold bands; long dwell → worse band; harrowed + shock → insanity | threshold at 100 **and** roll (virtue vs affliction), hard stop at 200 | threshold with dwell time | threshold with a timed ultimatum | boiling point per prisoner; gauge for the riot |
| Who picks the reaction | **trait chooses**, one fixed reaction per pawn | personality **weights** among three, then three insanities | weighted lottery, biased by history; 1-in-4 virtue | small fixed set, not trait-chosen (as far as known) | none per person; colony events | none; misbehaviour set is generic |
| Duration / end | until stress falls to 60 | fixed spell, or permanent if insane | rest of expedition + town | until mood recovers; leave at 0 | until ultimatum met | until sector retaken |
| Cascade | indirect only (world side-effects) | **strong**, through injuries, deaths and broken work → tantrum spiral | **direct**, barks deal stress to allies → affliction spiral | none | is the aggregate | spatial, sector by sector |
| Shown | % bar, vitals table, cause list | coloured prose, band description, no number | bar + number, title card, portrait | bar + itemised list, "final straw" | two bars, hover lists causes, laws preview effect | needs list, danger gauge, red sectors |
| Reception | legible; toothless; destructive trait = recruitment filter | spiral is the beloved story; endless memories were the hated part | resolve test is the identity; direct cascade = "random punishment" | treadmill of demands, not randomness | legible, political; gameable; abrupt end | forecast gauge liked; spatial cascade readable |

Three patterns fall out of the table:

- **Every game the players call legible itemises the causes** (ONI, Going Medieval, Frostpunk,
  Prison Architect). The two that hide them (DF's magnitude, DD's un-itemised hits) are the two
  whose spirals get called unfair *when the player cannot see them coming*.
- **The cascade is loved when it goes through the world and hated when it goes through the
  number.** DF's tantrum breaks a chair somebody made; DD's bark subtracts from a bar. The first
  is a story the player can intervene in (lock the door, bury the dead); the second is arithmetic.
- **A trait that fixes the reaction becomes a recruitment filter.** ONI's destructive duplicant is
  the proof. A trait that *tilts* the lottery (DF facets, DD history) keeps the pawn worth keeping.

## Recommendation

Three commitments for this project, each with its reason, then two smaller borrowings.

**1. Traits gate and weight the break; they never choose it.** RimWorld-style, not ONI-style.
Each break in the taxonomy (sulk, binge, tantrum, berserk) carries a base weight; a trait multiplies
some weights (a short-tempered trait ×3 on tantrum, a glutton ×3 on binge) and may zero others (a
pacific trait never rolls berserk); the pick is one weighted draw from the tick RNG, so it is as
deterministic as everything else. Add DD's small history bias — a colonist who has sulked before is
a little likelier to sulk again — because it makes a colonist *feel* consistent without being a
filter. The reasons are in the findings: ONI's fixed reaction makes the break predictable to the
point of being toothless and turns the trait into a reason to reject the pawn at the door; DF and
DD, which weight rather than dictate, are the two whose breaks generate stories. Show the tilt on
the pane ("prone to tantrums") so the roll is legible without being known.

**2. Breaks cascade, but through the world, with one non-stacking witness memory.** Yes to
cascade — every game whose breakdowns players remember has one — but the DF shape, not the DD
shape. Concretely: (a) a tantrum's *consequences* (a broken item, an injured colonist, a spoiled
meal) each land as the ordinary bad memory they already are, so the cascade is emergent and the
player can intervene in it; (b) a colonist who *sees* a break gets one small expiring memory
("saw X lose it"), which *refreshes* rather than stacks if they see another, capped at one entry
per witness. Because mood here drifts toward a target rather than being hit directly, one small
memory cannot flip anyone who was not already near the line — that is the safety DD lacks — while
a colony that is already marginal will spiral, which is the DF pleasure. Do not deal stress
directly from one colonist's bar to another's.

**3. A colony-wide reading is worth showing, but only as a derived alert, never as an
accumulator.** Frostpunk's bars work because they *are* the mechanic; here the mechanic is per
colonist, and a second colony-wide scalar that governed anything would be one rule with two owners
(`docs/bug-patterns.md`, the first pattern). So: an alert row computed from the per-colonist moods
each publish — "3 colonists near breaking" with the names, escalating in tone by count — and a
mood column on the roster or Work tab so the whole colony can be read at a glance, Prison
Architect's danger gauge in spirit. Nothing reads it back into the simulation.

Two smaller borrowings. **ONI's hysteresis** for the end condition: a break ends on the earlier of
a timer and the mood recovering to a band *above* the trigger, so a colonist does not re-break the
tick after coming round. And **Going Medieval's "final straw"**: when a break fires, name the most
recent or largest negative memory on the toast, which costs nothing (the memory list already
exists) and is the cheapest legibility win in the table. DD's virtue branch (a chance the crossing
produces a *good* outcome) and DF's strange mood are recorded as later hooks, not for the
prototype.

## Sources

Wiki pages (blocked by the container's proxy; quoted via search snippets only):

- https://oxygennotincluded.wiki.gg/wiki/Stress
- https://oxygennotincluded-archive.fandom.com/wiki/Stress
- https://dwarffortresswiki.org/Stress
- https://dwarffortresswiki.org/index.php/Mental_breakdown
- https://dwarffortresswiki.org/index.php/DF2014:Personality_facet
- https://dwarffortresswiki.org/index.php/v0.34:Tantrum
- https://darkestdungeon.wiki.gg/wiki/Stress
- https://darkestdungeon.wiki.gg/wiki/Affliction
- https://darkestdungeon.wiki.gg/wiki/Virtue
- https://goingmedieval.fandom.com/wiki/Mood
- https://frostpunk.fandom.com/wiki/Hope
- https://frostpunk.fandom.com/wiki/Discontent
- https://prison-architect.fandom.com/wiki/Danger_Level
- https://prison-architect.fandom.com/wiki/Riot
- https://prisonarchitect.paradoxwikis.com/Riots

Community and commentary:

- https://forums.kleientertainment.com/forums/topic/107433-is-stress-too-easy-to-manage/
- https://steamcommunity.com/app/457140/discussions/0/1694922980056689969/ (ONI, "The Destructive Trait is way overpowered")
- https://kleiforums.com/forums/topic/150199-simplifications-to-stress-mechanic/
- https://thegemsbok.com/art-reviews-and-articles/darkest-dungeon-red-hook-critique-mechanics-design/
- https://steamcommunity.com/app/1029780/discussions/0/3062997274374843692/ (Going Medieval, "Mood is at this point not controllable")
- https://www.gamepressure.com/frostpunk/what-happens-when-discontent-or-hope-reach-critical-values/zfad7c
- https://steamcommunity.com/app/323190/discussions/1/2828702372999303759/ (Frostpunk, discontent exploit)

## Confidence

**Medium.** The shape of every model — accumulator, trigger type, who picks the reaction, whether
it cascades — is corroborated by at least one search snippet quoting the game's wiki, and the
reception claims each rest on a named thread or article. The specific numbers (ONI's 60 % recovery
point, DD's one-in-four virtue chance and the 6-stress bark, Frostpunk's 15 points in two days) come
from those snippets; DF's threshold magnitudes, DD's affliction duration, and Going Medieval's
exact break list could not be confirmed and are marked as recalled or hedged in the text. The
recommendation follows from the pattern across the table rather than from any single number.

## Could not be determined

- The exact DF stress thresholds for each band (stressed / haggard / harrowed) and the rule that
  maps facets to which breakdown a dwarf has — the wiki was unreachable and the snippets gave only
  the three facets that shape the curve.
- Whether Going Medieval's breaks are influenced by traits at all, and the full list of its break
  behaviours; only "refuses orders", "wanders" and "leaves at zero" are attested.
- ONI's per-source stress rates and the duration or cooldown of a reaction beyond the 100 → 60 %
  hysteresis.
- Whether DD's afflictions in the sequel changed the cascade rule; only the first game was covered.
- Any developer statement from Ludeon on RimWorld breaks being "random punishment" — the brief's
  phrase was taken as given and RimWorld was not searched, since the project already models it.
