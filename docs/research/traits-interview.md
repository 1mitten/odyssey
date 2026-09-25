# Traits and mental health — the interview, 2026-09-25

**Phase 1 for the traits and mental health line** (`TM`; systems catalogue §1, the "social and
trait depth deferred from M2" that `08-milestones.md` put in M7). The ground was the code as of
`main` at PR #206: a working mood system (integer mood drifting toward a target made of need
bands, temperature and eight expiring memories), one break threshold, one break behaviour
(wander), a disabled Thoughts tab, an empty traits section on colonist select, and **no trait
anywhere** — `Pawn.LearningFactorPerMille()` returning 1,000 is the whole of it. Eight questions
were put to the owner in two rounds; every answer below is theirs, with the consequence written
beside it so the design can be written without asking twice.

## Answers

| # | Question | Answer | What it decides |
|---|---|---|---|
| 1 | What does this line cover? | **Traits; the three break tiers and a small break taxonomy; the Thoughts tab.** Not the passion mood buff. | Three units of mechanics and one of interface. Social relations and opinion are a separate line either way — a memory that names another colonist is a change to the memory record. The passion buff stays the recorded hook in `17-rates-and-stats.md` §6. |
| 2 | Traits in existing saves? | **New colonies only.** | A colonist loaded from an older save has no traits and behaves exactly as today: every factor 1,000, no work disabled. Traits are written in their own save section only when a colonist has them, so **no migration, no dealing on load**, and an old save round-trips unchanged. |
| 3 | Which breaks first? | **Sulk (minor), binge (major), tantrum (major), berserk (extreme)**, beside wander. | Every one is a job the drivers mostly have: bed and refuse (sulk), the eating driver against the stores (binge), the combat building-damage path (tantrum), the attack driver against a colonist (berserk). Fire-starting waits on fire being simulated; an insult spree waits on social. |
| 4 | What may a trait change? | **Permanent mood, the break threshold, learning speed, the work-speed spectrum, incapable of a work type.** Walk speed held. | Five effect kinds on `TraitDef`. "Incapable of" is the largest visible effect and reaches the Work tab (`WorkAspects` already says nobody can be incapable "until traits exist"). Walk speed is the recorded hook on `Pawn.PacePerMille`. |
| 5 | How many traits? | **Two or three**: two always, a third sometimes. | Dealt from the colonist's own `RollSeed` under a **new** random purpose, so faces, names and skills do not move for any seed. The third is a roll, not a rarity ladder. |
| 6 | Where are they shown? | **Colonist select's detail pane, under the skills, rerolled with the card; and a Traits list on the inspect pane with tooltips.** Not on the candidate card. | The 76 px card stays identity only (design 18 §6b). A kept card keeps its traits because the seed is kept. |
| 7 | How will the traits be supplied? | **A table the owner fills in**, below. Numbers are proposed by us and marked INVENTED. | The design doc turns each row into a `TraitDef` in `Defs/Core/Pawns/Traits.xml` and a `ui.trait.*` row in `icon-keys.csv`. Names are ours (clean room); a reference name is renamed at the design. |
| 8 | A colonist with every need met and no memories reads "strained". Fix first? | **Yes: resting is Content.** | The sim publishes the band and the broken state on the pawn view; the roster copies no threshold. Fix-first under the standing rule; it is `TM1`. |

## Decisions I am making without asking, and why

Routine calls the answers imply. Any can be overturned at the design or the plan.

- **The mood model stays as it is and is extended.** Integer, drifting, situational offsets
  computed and memories stored — the reference shape, already hash-safe. Tiers are two more
  thresholds and two more clocks on `MoodDef`, not a second accumulator (the stress-meter
  alternative is weighed in `b-mental-health-models.md`).
- **A trait gates a break; it does not choose one.** A trait may require or forbid a break
  (the reference's shape). Which one fires is a commonality-weighted roll among the eligible
  breaks of the deepest tier the colonist is below.
- **A break is a visible event.** Every break puts a row on the Events panel and raises the
  alert naming the colonist and the break, because the standing rule is that a condition either
  does nothing to a rate or produces something the player can see.
- **The Thoughts tab publishes what the code has.** Memories and the situational offsets (need
  bands, temperature) are published as they are computed; `04-data-model.md`'s "situational
  thought with stages" is brought to the code rather than the code to it.
- **Mood faces stay unshot.** The four `ui.mood.*` keys keep waiting on a sheet; the roster band
  is a drawn glyph tinted by band, never a font character (P13).
- **Out, recorded with the seam:** inspirations; a difficulty ladder for the base mood; walk-speed
  and damage traits; fire-starting; the insult spree; social opinion; the passion mood buff; any
  health effect (M6).
- **Combat §12b's two open questions close with the defaults the code already has**: the
  instigator remembers the blows she takes, and a death is felt to three deep, until a play says
  otherwise.

## The traits — fill this in

One row per trait. Leave the numbers to me: say what it does in words and I will propose the
figures, marked INVENTED, for tuning after the first play. Names must be our own; if one is a
reference name I will rename it at the design and say so. An **opposite** is a trait no colonist
can hold at the same time (the roll excludes it). **How common** is rare / normal / common.

| Name | One line (the tooltip) | What it does, in words | Opposite / conflicts with | How common |
|---|---|---|---|---|
| *e.g.* Sanguine | Sees the bright side of a ruin. | permanent good mood | Gloomy | normal |
| | | | | |
| | | | | |
| | | | | |
| | | | | |
| | | | | |
| | | | | |
| | | | | |
| | | | | |

What a trait can do, in this line (question 4): a permanent mood offset; a higher or lower break
threshold; faster or slower learning; faster or slower work; incapable of one work type
(construction, mining, growing, cooking, hauling, cleaning, research, and so on). Anything else
you want a trait to do, write it in words and it becomes a recorded hook if this line cannot
carry it.
