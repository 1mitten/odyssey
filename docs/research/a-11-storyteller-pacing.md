# a-11 (second file): storyteller pacing, threat budget and adaptation

**Question.** In the reference, how are the three storytellers' cadences numbered, how is a threat's
size budgeted (the points formula and its curves), how does adaptation rise and fall, what does
difficulty scale, how does population intent work, and what do players criticise?

Asked 2026-09-26 for the storyteller line (`docs/design/68-storyteller.md`), as a companion to
`a-11-storyteller-incidents.md` (2026-09-20), which covered the taxonomy and the incident definition.
One subagent, capped at six fetches or searches. **Every wiki page was refused by the network proxy**,
so the findings come from search-result extracts, marked [extract], and from general knowledge of
the reference, marked [recall]. Clean room: mechanics, numbers and design intent only.

## Findings

### 1. Cadence, per storyteller
| | Steady cycle (Classic) | Calm cycle (Chillax) | Random (Randy) |
|---|---|---|---|
| Model [extract] | on/off cycle | on/off cycle | one random roll, then a category by weight |
| On / off days [extract] | 4.6 / 6.0 | 8 / 8 | — |
| Big threats per on-phase [extract] | 1–2, at least 1.9 days apart | exactly 1 | — |
| First big threat [extract] | not before day 11 | on-phase from day 13 | — |
| Mean roll interval [extract] | — | — | about 1.35 days |
| Anti-drought [extract] | — | — | 13 days without a big threat force one |
| Points randomisation [recall] | none | none | about ×0.5 to ×1.5 |
| Big threats a 60-day year | about 8.5 | about 3.75 | about 8.5, uneven |

Minor and good events run on separate independent streams in the cycle storytellers (a
mean-time-between of about 4.8 days for misc [recall, and `a-11-storyteller-incidents.md`]).

### 2. Selection [recall]
A generator picks a category. Within the category, the candidates are the incidents whose gates pass
(earliest day, refire cooldown, population bounds, a minimum budget, conditions, "big threats
allowed"). One is picked by weight × situational factors, the population factor among them.

### 3. The threat budget
`points = (wealthPoints + pawnPoints) × difficultyScale × adaptationFactor × startingRamp`, clamped
to about 35–10,000 [recall for the structure and clamp].
- **Wealth points** [extract]: 0 up to 14,000 wealth, then linear to 2,400 at 400,000 (one point per
  about 161 wealth). Flattening beyond that towards about 4,200 at 1,000,000 [recall].
- **Storyteller wealth** [recall]: items + creatures + half of buildings. Buildings are discounted so
  that building out is punished less than hoarding.
- **Pawn points** [extract]: about 15 per colonist at 10,000 wealth, rising to 140 at 400,000.
  Trained combat animals add about 8 % of their combat power [recall].
- **Difficulty scale** [extract] is a straight multiplier.
- **Starting ramp** [recall, low]: below 1 early, reaching 1 somewhere between day 40 and day 60.

### 4. Adaptation [extract for the direction, recall for the shape]
A hidden counter rises each day the colony goes without loss. A colonist's death drops it sharply
and a colonist downed in combat drops it less; both losses shrink as the colony grows. It maps
through a curve to a points factor of about 0.4 to about 1.47. Players read the cap as "normal". A
grace period at the start keeps it from rising. The easiest difficulties effectively switch it off.

### 5. Difficulty [recall, medium]
Threat scale by preset: about 0.1 (no big threats), 0.3, 0.6, 1.0, 1.55, 2.2; Custom 0–500 %
[extract]. Difficulty also sets whether big threats are allowed, the adaptation's strength, and many
unrelated economy and health factors. It is independent of the storyteller choice.

### 6. Population intent [recall; shape from `a-11-storyteller-incidents.md`]
A curve from colonist count to a factor: about 8 at zero, 2 at one, 0.35 at seven, about 0 from eleven.
It multiplies the chance of population-gaining incidents (joiners, refugees, rewards that are
people), and a days-since-last-recruit curve multiplies it again. The effect keeps shrinking up to
the twentieth colonist, and prisoners count as half a colonist [extract].

### 7. What players criticise, and what they do about it [extract + recall]
- **Wealth equals raid size**, which punishes building nice things. A whole wealth-management
  practice grew up: don't hoard valuables, remember that buildings count half, recruit skilled pawns
  rather than many.
- **Adaptation is hidden rubber-banding**: losing makes it easier and thriving makes it harder,
  with no way to see it.
- **The random storyteller's clusters feel unfair**.
- Players install mods that show a combat-readiness figure against the raid budget. That is
  evidence that players want the budget to track the colony's real defensive strength.

**Design intent.** The storyteller is a dramatic pacer (tension, then release), not a simulation of
the world. Wealth and headcount stand in for how strong the colony is, and adaptation makes recovery
windows after a tragedy.

## Recommendation (taken into design 68)
Keep the reference's generator shapes and its cadence numbers, scaled ×1.2 to our 72-day year. Do
not use its budget: **measure fighting strength directly**, which is what its players install mods
to see. Keep adaptation's recovery window, but **show it**.

## Sources
- https://rimworldwiki.com/wiki/AI_Storytellers (refused; extracts only)
- https://rimworldwiki.com/wiki/Raid_points (refused; extracts only)
- https://rimworldwiki.com/wiki/Cassandra_Classic, https://rimworldwiki.com/wiki/Phoebe_Chillax (extracts)
- https://rimworldwiki.com/wiki/Wealth_management (title and extract)
- https://en.number13.de/rimworld-storyteller/ (extract)
- Steam community threads on raid size and adaptation (extracts)

## Confidence
**Medium overall.**
- **High:** the three cadence models and their numbers; the wealth breakpoints (14,000 and 400,000)
  and the pawn-point breakpoints; threat scale being a straight multiplier; adaptation's direction.
- **Low:** the adaptation curve's numbers, the starting ramp's exact points, the category weights,
  the population curve's interior, and anything past 400,000 wealth.

## Could not be determined
- The adaptation counter's range per storyteller, its growth rate, and its loss per death or down.
- Randy's exact category weights (`a-11-storyteller-incidents.md` quotes a set; unverified here).
- The exact starting ramp.
- The difficulty presets' values other than threat scale.

## Layer questions touched
**Layer question 7 (the storyteller's use of verticality).** None of this is layer-aware. A budget
and a cadence have no height. Verticality enters through the incidents themselves: an arrival from
a lower stratum, or a drop through open sky (design 03 §11).
