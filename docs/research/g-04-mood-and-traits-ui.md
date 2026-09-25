# Lane G4 — Showing mood, thoughts and traits: what the reference screens do and what ours should

## Question

How do RimWorld (primarily), Oxygen Not Included and Dwarf Fortress present mood causes and
traits, and what should this project's Thoughts tab, Traits list, roster mood band and alerts show
— given 19 px rows, a fixed-height pane with about twelve rows visible, and drawn glyphs only?

Method note: the cap was six searches or six reads. All six reads (rimworldwiki.com,
oxygennotincluded.wiki.gg, dwarffortresswiki.org, steamcommunity.com) were refused by the network
egress proxy, so the findings below rest on the six searches' snippets and on the author's prior
knowledge of the three games' screens. Each item says which. Nothing below is quoted from a game;
every string is described, not copied.

## Findings

### 1. RimWorld — the Needs tab mood breakdown

**Layout (prior knowledge, high).** The Needs tab is two columns. The left is a stack of need
bars (mood first, then food, rest, recreation, comfort, beauty, outdoors and so on), each a labelled
horizontal bar with its percentage. The right is the thoughts list. There is no timeline and no
history: the list is only what is acting on the pawn *now*.

**The mood bar (search snippet, high).** The mood bar carries hatch marks at the three mental-break
thresholds — for an unmodified pawn 35 % (minor), 20 % (major) and 5 % (extreme); traits move the
top one and the lower two follow it as fixed fractions (4/7 and 1/7 of the pawn's threshold stat).
A small white triangle on the bar marks the **mood target**, the level the pawn will settle at if
nothing changes; when the marker is inside the filled part the mood is falling towards it, when it
is outside the mood is rising. The pawn therefore has two numbers, the current mood and the target,
and only the target is the sum of the list.

**The thoughts list (prior knowledge, medium).** One row per thought group: a label on the left, a
signed integer on the right. Positive offsets are drawn in green, negative in red. Stacked copies of
the same thought are one row with a multiplier suffix such as "x3" after the label, and the number
shown is the stacked total after the stacking-decay multiplier, not the base times the count.
Situational thoughts (a need band such as hungry, a room's impressiveness, the weather, an expectation
level, a trait's permanent offset) and timed memories sit in the same list with no visual
distinction and no expiry column; the only way to learn whether a row will go away, and when, is to
hover it. The sort is by offset, with the most positive rows at the top and the most negative at the
bottom (medium confidence on the direction — it is the one thing a screenshot would settle in a
second).

**The hover tooltip (prior knowledge, medium).** A flavour sentence describing the thought; the
base mood offset; for stacked rows the stacking arithmetic; for memories a line saying how long
until it expires (in days, to one decimal); and where the thought is about another pawn, that
pawn's name. Situational rows have no expiry line. Nothing in the tooltip says what the row would
take to fix.

**Colour (prior knowledge, high).** Only two hues on the list itself: green for helpful, red for
harmful. The bar's own fill does not change hue with the tier in vanilla; the community's answer to
that is the Color Coded Mood Bar family of mods (item 3).

### 2. RimWorld — the Bio / Character tab traits and "incapable of"

**Prior knowledge, medium.** The Character tab shows name, nickname and title, age, the two
backstories (childhood and adulthood, each with a hover description), then an *incapable-of* line
listing the work groups the backstories or traits disable (violence, skilled labour, hauling and so
on), and then the traits as a short list of labels, typically two or three, in the order they were
rolled — not alphabetical and not by importance. Skills with passion flames are on the right.

**The trait tooltip** carries a flavour paragraph and then an itemised list of mechanical effects
drawn from the Def: stat offsets and factors as percentages (work speed, move speed, mental-break
threshold), skill offsets as signed integers, the work types it disables, and in later versions
the gene that forced it. It does **not** list the traits it conflicts with — exclusion is a
generation rule, not a piece of in-play information. The search results confirm that when genes
force a trait a conflicting rolled trait is suppressed, which is the only place conflicts surface
to a player.

**Sources for this item** were the search snippets only; the wiki page itself could not be read.

### 3. RimWorld — the colonist bar, the break state and the alert

**Search snippets, high.** Under each portrait in the top colonist bar is a thin mood strip. In
vanilla it is one hue whatever the tier; the Color Coded Mood Bar mod (and its CM fork for 1.1+)
recolours it by tier — light blue above every threshold, yellow under the minor line, orange under
the major, red under the extreme — and one of the forks adds an outline box round a portrait at
extreme risk. That a bar-tinting mod is among the most installed interface mods is itself a finding:
players want the tier readable from the roster without opening the pawn.

During a break a small lightning-bolt icon is drawn over the portrait: yellow for a non-aggressive
break (a wander, a binge, a daze), red for an aggressive one (a tantrum, berserk).

**Alerts (prior knowledge, high).** The right-hand alert strip carries three standing alerts by
tier — minor, major and extreme break risk — each naming the colonists at that tier, with the
extreme one in the critical red. When a break fires, a letter arrives with the break's name in its
title and a short paragraph saying what the pawn is doing; the paragraph ends by naming the
"final straw", which is the most recent negative thought rather than the largest one. Players
regularly report the final straw as trivial-sounding (a meal eaten without a table) when the real
cause was a large standing penalty.

### 4. Oxygen Not Included and Dwarf Fortress

**ONI stress (search snippets plus prior knowledge, medium).** Stress is a percentage per
duplicant, shown as a bar in the duplicant's side panel. Hovering the bar gives a tooltip that
lists every current stress source as a **rate** — a signed percentage per cycle — with a total
rate at the bottom, so the player reads "what is pushing this number and how fast" rather than
a target. Since the morale rework the dominant rows are a morale shortfall or surplus against
the duplicant's expectation (the snippets confirm a sufficient-morale row removing 5 % per
cycle), plus specific events such as interrupted sleep. At 100 % the duplicant acts out its
named stress response until it drops to 60 %; the response is a trait of its own.

**ONI traits at the Printing Pod (prior knowledge, medium).** Each candidate is a card: portrait,
name, a list of interests with attribute bonuses, and a traits list where each trait is a name
with a hover tooltip that states the effect numerically (an attribute offset, a task the duplicant
cannot do, a per-cycle rate). Helpful and harmful traits are visually distinguished on the card,
and the stress response and joy response are listed as traits. A Klei bug report in the snippets
complains that a trait tooltip omitted one of its rates, which shows the tooltip is expected to
be exhaustive.

**Dwarf Fortress (search snippets plus prior knowledge, medium).** The unit's thoughts screen is
prose: a paragraph on the dwarf's overall stress in subjective words, followed by sentences for
each recent emotion and its cause, memories highlighted in a distinct colour. The personality
screen has sub-tabs for traits, values, preferences and needs. Memories are tiered — short-term
slots, long-term slots promoted after a year, and core memories — with only the strongest emotion
per category kept, so memories fade by being displaced rather than by a timer. Stress is never
shown as a bare number in the fortress screens.

### 5. Criticism of these screens

Search snippets plus prior knowledge, medium.

- **No history.** RimWorld's list shows the present only, so after a break the player reconstructs
  the cause from a letter that names the last thought, not the worst. The Modern Needs Tab mod
  (2026) adds a mood timeline of when each thought arrived and gives the list the whole panel
  height so more rows fit — direct evidence that the vanilla list is both too short and too
  present-tense.
- **Expiry is hidden.** Time left is in the tooltip only, so "will this be over before she
  breaks?" costs a hover per row.
- **Situational and timed are indistinguishable**, yet they need opposite responses (change the
  room; wait it out).
- **The roster does not show the tier.** The most common fix is a mod that tints the strip.
- **Trait names are read, not scanned.** RimWorld traits carry no tint or glyph, so a player
  learns the dangerous ones by memory; ONI's card marks harmful traits and is easier to judge at
  the pod.
- **Prose does not scan.** Dwarf Fortress's thoughts screen is the richest and the slowest to
  read; players quote it fondly and manage stress from the coloured face icons in the unit list
  instead.
- **Rates versus targets.** ONI's per-cycle rates say how fast; RimWorld's target says where.
  Neither says both, and neither says when.

## Recommendation

Commit to the following. Every string named below is a registry key in `icon-keys.csv`, never a
literal (`RegistryTests`), and every mark is a `HudGlyph`.

### The Thoughts tab

**Header, two rows.** Row one: the mood bar with three 1 px threshold notches at the band edges
(content / strained / breaking / broken) and a drawn triangle at the **target**. Row two, in the
numeric face: the current value, a drawn up or down arrow for the drift, and the target
(`62 -> 48`); the arrow's tooltip says roughly how long the drift takes to arrive. The arithmetic
must be auditable: the footer row is `Base 50 + rows = 48`, so a player can see the list explains
the target. Show the target and the drift; RimWorld's triangle is the one piece of that screen
nobody complains about.

**Two groups, one list, separated by a drawn rule.** *Now* (situational: need bands,
temperature, room, a trait's standing offset) above, *Memories* (timed) below — because the
player's response differs and RimWorld's mixing of the two is a recurring complaint. Within each
group sort **worst first** by contribution; ties by soonest expiry. The tab exists to answer "why is
she breaking", so the answer is the first row, not the last.

**Columns, left to right, in a 19 px row:** a 12 px group glyph (a pin for situational, a drained
clock arc for memories); the label, 150 px, with a plain ASCII `x3` suffix in the numeric face for a
stacked memory; a 28 px **time-left bar** for memories — a filled rectangle draining left to right,
blank for situational rows; the signed offset, right-aligned, 36 px, in `HudTokens.Good` / `Bad`
only. No third hue and no colour change as expiry nears. A stacked row's bar shows the **oldest**
copy's remaining time, because that is when the penalty first shrinks; its offset is the stacked
total. Twelve rows a page, paged like the roster and the Work tab, never scrolled.

**Tooltip:** one description sentence; the base offset and, for a stack, the count and multiplier
and the expiry of every copy; the exact time left; for a situational row the band boundaries
("Hungry below 30 %; Ravenous below 10 %") so the row says what fixes it. Never a conflicts or a
flavour paragraph.

### Traits

**Inspect pane** (in the Character-style tab beside the skills): two or three 19 px rows, each a
trait glyph, the name (95 px, the skill row's budget), and a three-token effect summary in the
numeric face on the right (`Work +20 %`, `Cannot: Hauling`). Order fixed as rolled, so a row never
moves. Under them, only when non-empty, a `Cannot:` row in the `Bad` token listing disabled work —
and the Work tab must grey the same columns from the same source, one owner. The glyph carries the
only tint: neutral by default, `Bad` for a trait that disables work or lowers the break threshold,
`Good` for one that raises it or buys a rate — so the candidate can be judged without reading, which
is ONI's card doing better than RimWorld's. **Tooltip:** one sentence, then every numeric effect
from the Def as a stat/value list, then the disabled work types, then any mood-threshold change.
No conflicts, no forced-by line.

**Select screen:** the same rows and the same tooltip in the detail pane between the occupation
and the two skill columns. The card stays identity only (design 18 §6b).

### The roster band and the alerts, per tier

| Tier | Roster strip under the portrait | Alert strip | Events panel |
|---|---|---|---|
| Content | fill = mood, `Good` hue, three 1 px notches at the band edges | none | none |
| Strained | fill in amber | none — the strip is the alert; RimWorld's minor-risk alert is the one players learn to ignore | none |
| Breaking | fill in `Bad`, portrait frame in `Bad` | "Break risk: name" amber; tooltip is the three worst Thoughts rows and the soonest expiry | none |
| Broken | strip `Bad`, a drawn lightning glyph over the portrait | "name is breaking down" red, click jumps the camera (the Events row behaviour) | one row naming the break kind and the **largest** negative row, with the newest memory second — not RimWorld's last-arrived "final straw" |

The strip is tinted by tier from the first build: the reference's most-installed interface mod
class exists to add exactly that, so do not ship the untinted version and wait for the report.

## Sources

Read attempts (all refused by the egress proxy; listed so the next lane knows what to fetch from a
machine that can):
- https://rimworldwiki.com/wiki/Mood
- https://rimworldwiki.com/wiki/Traits
- https://rimworldwiki.com/wiki/Mental_break
- https://oxygennotincluded.wiki.gg/wiki/Stress
- https://dwarffortresswiki.org/index.php/DF2014:Thought
- https://steamcommunity.com/sharedfiles/filedetails/?id=3743615382 (Modern Needs Tab)

Search results used (snippets only):
- https://rimworldwiki.com/wiki/Mental_Break_Threshold
- https://rimworldwiki.com/wiki/Needs
- https://eatcreatesleep.net/how-the-rimworld-mood-system-really-works-mood-vs-mood-target/
- https://github.com/bcooper94/ColorCodedMoodBar
- https://steamcommunity.com/sharedfiles/filedetails/?id=2006605356 (CM Color Coded Mood Bar)
- https://steamcommunity.com/app/294100/discussions/0/3199241242396947095/ (needs tab UI problem)
- https://oxygennotincluded.wiki.gg/wiki/Duplicant
- https://forums.kleientertainment.com/klei-bug-tracker/oni/traits-duplicant-tooltip-missing-rate-of-co2-production-exhaling-r7025
- https://dwarffortresswiki.org/index.php/DF2014:Thoughts_and_preferences
- https://dwarffortresswiki.org/index.php/DF2014:Memory_(thought)

## Confidence

**Medium.** The RimWorld thresholds, the target triangle, the tier colours of the mod, the break
icon and the existence of the timeline mod are confirmed by search snippets; the list's sort
direction, the tooltip's exact lines and the ONI card's markings are from prior knowledge of the
screens and could not be checked against a page. The recommendation does not depend on any of the
uncertain points.

## Could not be determined

- The exact sort direction of RimWorld's thoughts list (most positive first is believed, not
  verified) and whether the stacked count is drawn on the label or the number.
- The rate at which RimWorld's mood moves towards its target (per tick or per day), and so how
  long a drift takes.
- The full content of RimWorld's trait tooltip in the current version, including whether a
  gene-forced trait says so inline.
- Whether ONI's pod card marks harmful traits by colour, by glyph, or both, and the exact rows of
  the stress tooltip after the morale rework.
- Which words and colours Steam Dwarf Fortress uses for its stress bands in the unit list.
- Any published designer commentary (Tynan Sylvester or Klei) on these screens; the searches
  surfaced only player and modder reactions.
