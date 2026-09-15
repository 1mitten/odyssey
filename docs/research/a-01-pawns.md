# Lane A1 — Pawns: needs, mood, thoughts, traits, skills, social, mental breaks

## Question

How do RimWorld's pawn systems work — needs (food, rest, joy, comfort, beauty, outdoors, mood), thoughts and memories with decay, traits, skills with passions and learning curves, social relations and opinion, and mental breaks with thresholds? Extract formulas, Def data shapes (structure only), state machines, tick cadence and tuning constants. Clean-room: everything below is paraphrased from the RimWorld wiki; no game XML or code was copied.

## Findings

### Tick cadence (the frame everything hangs on)

- 1 tick = 1/60 real second at 1× speed. 2,500 ticks = 1 in-game hour; 60,000 ticks = 1 in-game day (≈16.7 real minutes).
- Things tick on one of three cadences: **Normal** (every tick), **Rare** (every 250 ticks ≈ 4.17 s), **Long** (every 2,000 ticks ≈ 33.3 s). A thing declares its cadence in its Def.
- **Needs are not updated every tick.** The wiki's Rest and Comfort pages both state need values change on a **150-tick interval** (2.5 in-game seconds; 400 updates per day). This is the natural cadence for a needs subsystem: cheap, and fine-grained enough that players never notice quantisation.
- Mental-break eligibility is a continuous mean-time-between-events roll while the pawn is below a threshold (see below), i.e. a per-check probability calibrated so the expected wait equals the stated MTB.
- Sleeping pawns freeze mood change and cannot mental-break; several needs also freeze or slow during sleep (outdoors freezes, food keeps falling).

Design takeaway: three fixed tick buckets plus a 150-tick needs interval is the entire scheduling model — no priority queues, no per-need timers.

### Needs: the general shape

Every need is a scalar 0..1 (displayed 0–100 %) on the pawn, updated at the needs interval. Each need has named threshold bands; crossing a band swaps a **situational thought** that feeds mood. Which needs a pawn has depends on species/type (animals: food and rest only; some traits/genes delete a need, e.g. an undergrounder type lacks Outdoors; blind pawns ignore Beauty). A need definition therefore carries: identifier, whether it shows for a given pawn kind, fall behaviour, threshold band boundaries, and the linked situational thoughts.

### Food (saturation)

- Capacity: `max nutrition = species body size × life-stage body-size factor × life-stage food-max factor` — adult human 1.0; baby 0.125, child ≈0.8.
- Drain: `hunger rate = (base rate × (1 + Σ offsets)) × Π multipliers`; adult human base 1.6 nutrition/day. Drain slows as the bar empties: ≈ −6.67 %/h while Fed, −3.33 %/h while Hungry, −1.67 %/h while Urgently Hungry.
- Bands and effects: Fed > 25 % (no effect); Hungry 12.5–25 % (−6 mood); Urgently/ravenously hungry 0–12.5 % (−12 mood, immunity gain ×0.9); at 0 % a **malnutrition** condition starts (−20 mood, immunity ×0.7) whose severity rises 2 %/hour — death at 100 %, i.e. 50 hours at empty; severity falls at the same 2 %/hour once fed again. Full bar to death ≈ 72.5 h.
- Behaviour trigger: pawns seek a meal when saturation ≤ 30 %. Meals give 0.9 nutrition (from 0.5 nutrition of ingredients — cooking "creates" nutrition at 180 %); raw food 0.05/unit. Overshoot past capacity is wasted.
- State machine: Fed → Hungry → UrgentlyHungry → Malnourished(severity 0→1) → dead; fully reversible above death.

### Rest

- Falls only while awake, at the needs interval; the fall rate slackens in lower bands (roughly: full band ≈ 95 %/day, middle ≈ 66 %/day, low ≈ 28 %/day, so an awake pawn lasts well past one day before collapse).
- Bands: Rested ≥ 28 %; then three tired bands with escalating situational thoughts (−6 / −12 / −18 mood) and immunity-gain factors (×0.96 / ×0.92 / ×0.80). At 0 % the pawn collapses and sleeps where it stands; in the lowest band there is a random chance of collapsing early.
- Recovery per 150-tick interval ≈ `0.57143 % × bed Rest Effectiveness × pawn Rest Rate Multiplier` → 10.5 h for 0→100 % at 1.0/1.0. Rest Effectiveness: ground 0.8, bedroll 0.95, bed 1.0, top-tier bed 1.05, scaled by quality ≈ ×0.86 (awful) to ×1.6 (legendary). Rest Rate Multiplier is a pawn stat derived from body-function capacities; a "quick sleeper" style trait gives +50 %.

### Recreation (joy)

- Falls ~2.5 %/h in the top band, slower in lower bands (~1.75 %/h, then ~1 %/h); paused while gaining, asleep, or broken.
- Gain: `base 36 %/h × activity joy factor × (1 − tolerance for that activity's category)`. Activity factors ~1.0 for simple activities up to ~1.6 for the best fixed installations (and rare event activities ~5.0).
- **Tolerance** per recreation *category* rises at about ⅔ of the joy actually gained, decays 7–18 %/day (faster at low colony expectations, slower at high), and above 50 % tolerance the pawn refuses that category ("bored"). Ten-ish categories (solitary, social, cerebral, dexterity, TV, music, study, chemical, eating…). Higher colony wealth demands more distinct available categories.
- Mood bands: ≥85 % +10; 70–85 % +5; neutral middle; 15–30 % −5; 0–15 % −10; 0 % −20.

### Comfort

- The need drifts **up** towards the Comfort stat of whatever the pawn currently sits/lies on, at 3.6 % per 150 ticks (60 %/h), capped at that stat; it decays 4 %/h when nothing supports it. So comfort is a fast-attack, slow-release envelope follower over furniture use.
- Furniture Comfort stats (normal quality): ground 0, sleeping spot 0.4, stool 0.5, dining chair 0.7, bed 0.75, armchair 0.8, couch 0.85, luxury bed 0.9; quality scales these; linked side furniture adds small offsets.
- Mood: <10 % −3; neutral to 60 %; then +4/+6/+8/+10 in 10-point bands to 100 %.

### Beauty

- Environmental beauty of a cell = **sum** of the beauty values of terrain, buildings, items and filth on/around it (nice floor +2, ugly rough terrain −1, art can be > +100 via `(base × material factor + material offset) × quality factor`, loose items −5, dirt −5, blood −30).
- A pawn's *perceived* beauty = **average** cell beauty over a sampled disc of roughly 8-cell radius (~241 cells) restricted to line of sight. Requires working sight.
- The Beauty need bar drifts towards a band determined by that perceived average (rises faster than it falls). Bands map average beauty → mood: ≥6 → +15; ~4.5–6 → +10; ~2.5–4.5 → +5; neutral around −0.5–2.5; then −5 / −10 / −15 as the average drops below ~−0.5 / ~−3.5 / ~−4.
- State machine: none — pure filtered signal over an environment query.

### Outdoors

- Changes per hour by roof/enclosure state of the pawn's current cell: unroofed & outside +33.3 %/h; unroofed but in a "room" +20.8 %/h; under constructed/thin roof: +4.2 %/h if the space counts as outdoors, −1.33 %/h indoors; under deep overhead rock −1.67 to −1.88 %/h. In bed, losses are cut to 20 %; frozen while asleep. The bar cannot fall below 20 % except under overhead mountain (the two worst bands exist only underground).
- Mood bands: >80 % none; then −1 / −3 / −5 (cabin fever) / −7 / −9 down the bands. Some pawns (undergrounder-type trait/gene) lack the need entirely.

### Mood, thoughts and memories

- `mood target = base mood + Σ(all thought mood offsets)`. Base mood comes from difficulty (≈42/37/32/27/22 from easiest to hardest setting). Traits add permanent offsets (a sanguine-type +12, depressive-type −12).
- The displayed/effective mood **drifts towards the target** at a capped rate: +12 points/h rising, −8 points/h falling. This lag is what stops a single bad moment instantly triggering a break.
- Two thought kinds:
  - **Situational**: recomputed continuously from current state (hungry, in darkness, spacious room, expectations). Appear/disappear with their condition; no decay.
  - **Memories**: created by events (ate without table, saw a corpse, insulted), carry a duration in days, and expire. Repeats stack up to a per-thought **stack limit**; each additional copy is scaled by a **stacked-effect multiplier** (commonly 0.75), so spam saturates rather than grows linearly.
- Thought definition shape (structure only): thought class (situational vs memory); one or more **stages** each holding a label, a mood offset and (for social thoughts) an opinion offset; duration in days (memories); stack limit; stacked-effect multiplier; nullifying conditions (traits, genes, precepts, health states); for situational thoughts, the worker/condition that selects the active stage.
- **Expectations**: a wealth-driven situational thought ladder gives large positive mood at low colony wealth, shrinking to zero as wealth rises — the main difficulty governor on mood.

### Traits

- Pawns get 1–3 traits at generation (forced by backstory/kind/scenario first, then random picks weighted by per-trait **commonality**, with conflict exclusions).
- **Spectrum traits** are one Def with several degrees (e.g. work-drive from −35 % to +35 % global work speed; permanent mood from −12 to +12; nerves from +15 % break threshold down to −18 %; walk speed; psychic sensitivity; body-beauty −2..+2).
- Trait Def shape (structure only): list of degree entries, each with label, degree number, commonality, stat offsets, stat factors, permanent mood offset, mental-break threshold offset, skill offsets, disabled work tags, forced/blocked thoughts; plus a conflicting-traits list at the Def level.
- Representative constants: tough-type ×0.5 incoming damage; fast/slow learner ±75 % global learning; nervous-type +8 break threshold, iron-willed-type −18; abrasive-type ×2.3 weight on insult-class interactions; pyromaniac disables firefighting and gains mood from fire; kind-type unlocks a +15-opinion interaction and ignores ugliness.

### Skills, passions, learning

- Twelve skills (shooting, melee, construction, mining, cooking, growing, animals, crafting, artistic, medical, social, intellectual), levels 0–20.
- XP to advance from level L is `1000 + 1000·L`… i.e. per-level cost 1,000; 2,000; 3,000 … 32,000 (cumulative 265,000 to reach 20). Level names are cosmetic.
- `XP applied = base XP of the action × global learning factor × passion multiplier`. Passion multipliers: none ×0.35, minor ×1.0, burning ×1.5. Working a passion skill also grants a small mood buff (~+8 minor / +14 burning while doing it).
- Soft daily cap: after 4,000 XP gained in one skill in one day, further gains are ×0.2 until the midnight reset.
- **Decay**: skills at level ≥10 lose XP daily, escalating steeply with level (~30 XP/day at 10, ~3,600 XP/day at 20 — the exact ladder is in the wiki table). A level is only lost after dropping 1,000 XP below its floor, giving hysteresis. A good-memory-type trait halves decay. Net effect: an equilibrium level per skill given usage rate.
- Global learning factor: base 100 %, additive offsets from traits/implants, multiplicative factors from genes; age also modifies learning.

### Social: opinion, relations, interactions

- **Opinion** (per directed pair, −100..+100) = Σ(social memories about that pawn) + Σ(situational social thoughts: relations, traits, appearance). Recomputed from parts, not stored as one number.
- Constant offsets: close family/spouse-type relations +20..+30, lover-type +35, ex-relations −15 plus a large extra penalty on whoever was dumped (−50..−70, decaying); appearance gives ±20 per beauty step capped ±40 (ignored by kind/ascetic/blind pawns); annoying-voice-type traits −25 to everyone.
- **Interactions** fire randomly between nearby pawns; each type has a base weight and produces a social memory on the recipient: small-talk ≈ +0.6 opinion; deep-talk +15 for ~20 days (weight ~0.075 scaled by compatibility); slight −5, insult −15 (both ~20 days; weights ~0.02 / ~0.007, scaled up by abrasive-type traits and low opinion).
- **Compatibility** (symmetric, deterministic per pair — effectively a hash of the two IDs) = a pseudo-random normal component + an age-closeness score `0.45 − 0.9 × |Δ biological age| / 20` clamped to ±0.45. It scales positive vs negative interaction weights and romance chance.
- **Social fights**: insult-class interactions carry a base fight chance (~4 % insult, ~0.5 % slight) multiplied by state (hunger ×1.5–3, some traits ×4, drink/withdrawal ×1.5–5). Aftermath is a coin flip between a big positive pair-memory (~+38, "cleared the air") and a big negative one (~−22), both ~20 days.
- **Romance**: attempt weight from target's opinion of initiator (must be > 0), mutual attraction curves over age gap, appearance multipliers (×0.3 ugly … ×2.3 pretty), cooldowns (~12 days); marriage/breakup follow from sustained opinion, with breakup driven by low opinion. Intimacy events are mean-time-between scheduled with multipliers for pain, consciousness, age curve, appearance and opinion.
- Social Def shapes (structure only): an interaction definition carries initiator/recipient thought references, base weight/selection worker and fight chance; a relation definition carries the opinion offset, generation chances and implied-relation graph position.

### Mental breaks

- Three thresholds on mood: **minor 35 %**, **major = 4/7 × minor ≈ 20 %**, **extreme = 1/7 × minor ≈ 5 %**. Trait offsets shift all three together (additively on the minor value, clamped to 1–50 %).
- While mood sits below a threshold, a break of that tier is rolled as a mean-time-between event: expected wait **10 days below minor, 3 days below major, 0.7 days below extreme** (deeper tiers roll their faster clock instead). Asleep pawns never break.
- When a break fires, a concrete break type is picked from those the pawn is eligible for at that tier, roughly uniformly but weighted by a per-break **commonality** value and gated by requirements (e.g. fire-starting only for pyromaniac-type pawns, jailbreak needs prisoners). Break Def shape (structure only): tier, commonality, required/forbidden traits, mental-state definition to enter, duration range, and the letter/notification level.
- Tiers by example: minor = withdraw/wander/binge-on-food/insulting-spree/hide-in-room; major = tantrum-class (destroys things), targeted rage, dazes; extreme = berserk, hard-drug binge, fire-spree, give-up-and-leave, kill-spree, **catatonia** (incapacitated 100,000–300,000 ticks ≈ 1.7–5 days).
- Afterwards a large positive "catharsis"-style memory typically lifts mood well above the threshold, making breaks self-limiting; a tortured-artist-type trait converts some breaks into inspiration rolls (~50 %).
- Related but separate: **inspirations** are the positive mirror (high mood raises their chance), and catatonia/daze are implemented as mental states with durations, i.e. the pawn AI swaps its job-giver tree for the state's tree until expiry — a clean state-machine seam worth copying.

### The overall pawn mood pipeline (summary state machine)

environment + events → situational thoughts (recomputed) + memories (decaying list) → Σ offsets + base → mood target → rate-limited drift (+12/−8 per h) → effective mood → threshold comparison → MTB roll → mental state (duration) → catharsis memory → recovery.

## 3D/layer impact

The pawn-side state (needs, thoughts, skills, traits, opinion) is a bag of scalars and lists on the pawn and is completely layer-agnostic; every 3D question lives in the **environment queries the needs make**. Three queries must be layer-aware from their first commit: (1) the beauty scan — "average beauty over a line-of-sight disc of radius ~8" must become a same-layer disc plus visibility through vertical openings, and its cost argues for caching per (cell, layer) region; (2) the roofed/outdoors classification — "is there a roof" becomes "is there any floor/intact slab on any layer above this column", with the deep-underground band mapping naturally to cells many intact layers down; (3) room membership (used by comfort/beauty/expectations-adjacent thoughts and temperature) — rooms must be found per layer with explicit vertical portals (stairwells, shafts, holes), which is exactly layer question 3. Additionally the 150-tick needs interval and the three tick buckets scale linearly with pawn count, not map volume, so a 250×250×40 map does not threaten this subsystem — only the environment queries need spatial acceleration.

## Ruined-city impact

A ruined city inverts RimWorld's usual environmental defaults in ways this system handles for free. Rubble, filth, corpses and broken items are all just negative beauty contributors, so decayed districts are an automatic, tunable mood pressure and cleaning/renovation is an automatic mood payoff — no new mechanics needed. Holed roofs make the outdoors need nearly self-satisfying above ground while intact deep structures and service levels recreate the "overhead mountain" bands, giving a real reason to value pawns of an undergrounder-type trait. Salvaged furniture arriving at random quality plugs straight into the comfort and rest-effectiveness formulas. The expectations ladder (wealth → smaller mood bonus) suits a scavenger-colony arc perfectly and should be kept. Recreation's category-variety demand maps well to reclaiming different building types (bar, arcade, gym) as the colony grows.

## Layer questions touched

- **Q2 (is a roof a floor / support):** directly — the outdoors and beauty needs, and the roofed check they share, require a per-column "what is above me" answer; if a roof is a floor, "roofed" = "any intact floor above on this column", and collapse events feed the thought system (danger/ugliness memories).
- **Q3 (rooms across layers / heat rise):** directly — comfort, beauty, indoors thoughts and expectations-style room thoughts all consume room identity; room detection must be per layer with vertical portals, and whether a stairwell merges two rooms decides whether mood-relevant room stats (beauty, cleanliness, temperature) leak between layers.
- **Q4 (light and sun to lower layers):** partially — darkness is a mood/thought input and the beauty scan is sight-based, so lightfall through shafts and broken floors changes mood on lower layers.
- **Q5 (line of sight in 3D):** partially — the beauty scan is LoS-limited, and "observed corpse/death" memories fire on sight, so 3D LoS feeds the thought system, not just combat.
- **Q10 (unit of simulation across layers):** indirectly — the 150-tick needs interval and Normal/Rare/Long buckets are a proven cadence template for whatever per-cell simulations (gas, fire) are chosen.
- **Q12 (what the vertical slice must prove):** the slice needs needs+mood+thoughts end-to-end, but only the three environment queries above need real 3D implementations; breaks, skills and social can ship layer-blind.

## Sources

- https://rimworldwiki.com/wiki/Mood
- https://rimworldwiki.com/wiki/Thoughts
- https://rimworldwiki.com/wiki/Mental_break
- https://rimworldwiki.com/wiki/Needs
- https://rimworldwiki.com/wiki/Skills
- https://rimworldwiki.com/wiki/Social
- https://rimworldwiki.com/wiki/Traits
- https://rimworldwiki.com/wiki/Recreation
- https://rimworldwiki.com/wiki/Rest
- https://rimworldwiki.com/wiki/Comfort
- https://rimworldwiki.com/wiki/Beauty
- https://rimworldwiki.com/wiki/Outdoors
- https://rimworldwiki.com/wiki/Saturation
- https://rimworldwiki.com/wiki/Time
- https://github.com/UnlimitedHugs/RimworldHugsLib/wiki/Custom-Tick-Scheduling (tick bucket intervals)

(rimworldwiki.com blocks direct fetches; pages were read via a text-render proxy of the same URLs.)

## Confidence

**High** overall. Nearly everything comes from rimworldwiki.com mechanics pages, which document exact formulas and constants, and the numbers are mutually consistent (e.g. the 150-tick needs interval appears independently on the Rest and Comfort pages; the 35 %/20 %/5 % thresholds appear on both Mood and Mental break pages). **Medium** on: the exact per-band rest fall rates; the precise compounding of the thought stacked-effect multiplier; some social interaction weights and the post-fight opinion values (single summarised source, not cross-checked); the ×1.5 burning-passion multiplier interacting with the daily soft cap. Tick bucket sizes (250/2000) come from a well-known modding source rather than the wiki itself.

## Could not be determined

- Whether memory thoughts hold full strength until expiry or fade linearly over their duration (the wiki implies expiry-only for mood memories; opinion memories appear to shrink over time — needs a small in-game check or a modding-doc source, not decompilation).
- Whether the stacked-effect multiplier compounds per extra stack (0.75, 0.75², …) or applies flat 0.75 to every copy after the first.
- The exact per-tick probability form of the mean-time-between-breaks roll (per-tick vs per-interval check) and whether it is evaluated on the 150-tick needs cadence.
- Initial skill rolls at character generation (backstory bonuses plus a random component; exact distribution not documented on the Skills page).
- Exact commonality weights per mental-break type and per trait (present in-game as data; the wiki lists only examples).
- How opinion memories and mood memories share one thought definition (single Def type with both offsets per stage is implied but not stated outright).
