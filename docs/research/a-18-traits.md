# Lane A18 — Traits: what a trait can do, how many, and which effects show

## Question

How are the reference's (RimWorld's) traits structured, and which trait effects produce the most
*visible* difference in play? Six parts: (1) every kind of effect a trait can carry, with examples
and numbers; (2) the spectrum traits with degrees; (3) how many traits a pawn gets and how they are
picked; (4) how traits are shown in the interface; (5) what 1.5/1.6 and the expansions changed;
(6) a ranked list of the effects a player in a fifty-colonist colony would actually notice.

**A note on provenance before the findings.** The research container's egress proxy refused every
page this lane tried to read — `rimworldwiki.com`, `rimworld.fandom.com`, `steamcommunity.com`,
`web.archive.org` and three blogs — so no page was read whole. Everything below comes from the
summaries returned by eight web searches (which quote the reference wiki's *Traits*, *Global Work
Speed*, *Mental Break Threshold* and *Characters* pages) and from recall of the same public wiki.
Rows are marked **[S]** where a search summary states the number and **[R]** where it is recalled
and should be checked against the wiki before it is tuned into a Def. Nothing here is copied from
the reference's data files; names are the reference's names, given only so the mechanic can be
looked up, and the project invents its own.

## Findings

### 1. The kinds of effect a trait can carry

A trait in the reference is a Def holding one or more *degrees*; each degree carries a label, a
description, a commonality, and any subset of the effect kinds below. A trait with one degree is a
"singular" trait; one with several is a "spectrum" (§2). The effect kinds, with two or three
representative traits each:

| Effect kind | How it is applied | Representative traits and numbers |
|---|---|---|
| **Permanent mood offset** | a permanent thought carried by the trait, so it appears as a line in the mood breakdown, not as a stat | Sanguine **+12**, Optimist **+6**, Pessimist **−6**, Depressive **−12** [R]; the wiki names Pessimist as the example of "a permanent negative thought" [S] |
| **Mental-break threshold offset** | additive to the 35 % minor / 20 % major / 5 % extreme lines; the minor line is capped to 1–50 % [S] | Nerves spectrum (§2): Iron-willed **−18 pp**, Steadfast **−9**, Nervous **+8**, Volatile **+15** [R]; Neurotic **+8**, Very neurotic **+14** [R] |
| **Stat factor** (multiplicative, per stat) | a `statFactors` list on the degree | Global work speed: Industrious **+35 %**, Hard worker **+20 %**, Lazy **−20 %**, Slothful **−30 to −35 %** [S, the summary says −30, recall says −35]; Neurotic **+20 %**, Very neurotic **+40 %** [S]; Tough: incoming damage **×0.5** [R]; Quick sleeper: rest gain **×1.5** [R]; Fast learner learning **×1.75**, Slow learner **×0.35** [R]; Too smart learning **×1.75** [R] |
| **Stat offset** (additive, per stat) | a `statOffsets` list | Speed spectrum: Slowpoke **−0.2 cells/s**, Fast walker **+0.2**, Jogger **+0.4** on a base of 4.6 [R]; Nimble: melee dodge **+15 pp** [R]; Super-immune: immunity gain **+30 %** [R]; Trigger-happy: aiming time **×0.5** with an accuracy penalty; Careful shooter: aiming time **×1.25** with an accuracy bonus [R] |
| **Skill offset** | a `skillGains` list, added to the rolled level ("negative numbers decrease the skill and positive numbers increase it" [S]) | Brawler: Melee **+4** [R]; Gourmand: Cooking **+4** [R]; Tortured artist: Artistic **+3** [R] |
| **Disabled work type** | the trait names work types that are greyed out for good | Pyromaniac: **Firefighting** disabled [R]. Few vanilla traits use this; backstories are the main source of disabled work |
| **Forced (conditional) thoughts** | thoughts that fire only for a carrier | Night owl (mood **+** at night, **−** by day) [S names it as "conditional negative thoughts"]; Greedy (**−** unless the bedroom is impressive); Jealous (**−** when someone has a better bedroom); Nudist (**+** naked, **−** clothed); Green thumb (**+** after sowing); Body modder (**+** per implant, **−** with none); Body purist (**−** per implant) [R] |
| **Blocked (nullified) thoughts** | the trait lists thoughts a carrier never gets | Psychopath: no mood from deaths, organ harvesting, prisoner selling, and others' opinion of them lowered; Bloodlust: no penalty from butchering or witnessing death, **+** on a kill; Cannibal: no penalty from human meat, **+** for eating it; Ascetic: no bedroom-size or fine-meal penalty, **+** for plain food [R] |
| **Opinion offsets** | how others regard the carrier | Beauty spectrum (§2): Beautiful **+40** opinion from others, Pretty **+20**, Ugly **−20**, Staggeringly ugly **−40** [R]; Abrasive and Psychopath carry a standing opinion penalty [R] |
| **Unlocked or weighted interactions** | changes to the social interaction roll | Abrasive: insults far more often; Kind: kind words, more often [R] |
| **Unlocked mental break** | a break that fires on its own timer regardless of mood | Pyromaniac: **fire-starting spree** [S, "a special mental break separate from standard mental break mechanics"]; Gourmand: food binge; Chemical interest / Chemical fascination: drug-seeking [R] |
| **Need changes** | a need added, removed or re-rated | Undergrounder: no outdoors need; Chemical fascination: a drug need; Gourmand: hunger **×1.5** [R] |
| **Gating** | what the pawn refuses or accepts | Teetotaler refuses drugs; Brawler unhappy holding a ranged weapon; Psychopath may be assigned grim work without mood cost [R] |

Two structural points that matter for a Def shape. First, a trait is not one number: the pawn's
stat calculation asks *every* trait for its offsets and factors on a given stat, sums the offsets and
multiplies the factors, so the effect kinds are lists rather than fields. Second, the mood side is
delivered entirely through the thought system — a permanent thought for the offset, conditional
thoughts for the situational lines, a nullification list for the blocked ones — so the mood pane
already explains the trait without special casing.

### 2. Spectrum traits with degrees

A spectrum is one Def with several degrees, and a pawn may hold at most one degree of it
("a colonist can only have one trait from any single related spectrum" [S]). The vanilla spectra
and their numbers:

| Spectrum | Degrees (low → high) | The numbers |
|---|---|---|
| Industriousness [S] | Slothful, Lazy, Hard worker, Industrious | global work speed **−30/−35 %, −20 %, +20 %, +35 %** |
| Nerves [S for the names, R for the numbers] | Volatile, Nervous, Steadfast, Iron-willed | break threshold **+15, +8, −9, −18** percentage points |
| Neurotic [S] | Neurotic, Very neurotic | work speed **+20 %, +40 %**; break threshold **+8, +14** [R] |
| Speed [R] | Slowpoke, Fast walker, Jogger | move speed **−0.2, +0.2, +0.4** cells/s |
| Beauty [R] | Staggeringly ugly, Ugly, Pretty, Beautiful | beauty stat **−2, −1, +1, +2**; opinion of others **−40, −20, +20, +40** |
| Psychic sensitivity [R] | Psychically deaf, Psychically dull, Psychically sensitive, Psychically hypersensitive | sensitivity **×0, −50 %, +40 %, +80 %** |
| Natural mood [R] | Depressive, Pessimist, Optimist, Sanguine | permanent mood **−12, −6, +6, +12** |
| Immunity [R] | Sickly, Super-immune | periodic random disease / immunity gain **+30 %** |
| Learning [R] | Slow learner, Fast learner | learning factor **×0.35, ×1.75** |
| Drug desire [R] | Teetotaler, Chemical interest, Chemical fascination | refuses / seeks occasionally / seeks compulsively |
| Nudism / nudity attitude, Sexuality [R] | (one degree each in practice) | sexuality is generated by a separate roll and is the one family that can exceed the three-trait limit [S] |

Note that a spectrum's degrees are numbered around zero (negative for the bad end, positive for the
good), that the "missing" degree zero is the unremarkable pawn, and that most spectra are
*asymmetric* in count (Industriousness has four, Speed three, Neurotic two).

### 3. How many traits, and how they are chosen

From the wiki's *Traits* page as summarised by the search [S]:

| Age | Trait count |
|---|---|
| under 7 | 0 |
| 7–9 | 1 |
| 10–12 | 1–2 |
| 13 and older | **1–3, uniformly** ("no bias towards any particular number") |

The order of assignment [S, with R for the details]:

1. **Pawn kind** forced traits (a kind Def may force or forbid traits).
2. **Backstory** forced traits, "unless they would conflict with the pawn's pawn kind" [S]. A
   backstory can also *forbid* traits, and a trait that needs a work type the backstory has
   disabled is excluded.
3. **Scenario** forced traits (the scenario editor has a "forced trait" part with a chance and a
   target) [R].
4. **Genes** (Biotech) force or suppress traits: "if a forced trait would ordinarily conflict with
   non-genetically forced traits the pawn already has, the latter are suppressed; if it would
   conflict with genetic traits, the latter are removed" [S].
5. **Random fill**: "each missing trait is filled in by a random trait, weighed based on each trait's
   commonality" [S], excluding any trait that conflicts with one already held, any degree of a
   spectrum already represented, and (since 1.4) any trait that conflicts with a passion the pawn
   has [R].
6. Sexuality traits are rolled separately and may take a pawn to four [S].

**Commonality** is a plain positive weight, not a probability: "Kind has a commonality of 2, while
Psychopath has a commonality of 1", so before backstories and kinds intervene Kind is twice as likely
[S]. Most singular traits sit at **1**, a few common ones at **2–4**, and the extreme degree of a
spectrum is typically **half or less** of its mild degree (so a Jogger or an Industrious is rarer
than a Fast walker or a Hard worker) [R]. The search could not surface the full table; the wiki's
*Property talk: Commonality* page exists but was unreachable.

**Conflicts** are a list of trait keys on each degree (Psychopath ↔ Kind; Bloodlust ↔ Kind;
Ascetic ↔ Greedy, Jealous, Gourmand; Pyromaniac has no "opposite" and conflicts with nothing) [R],
plus the implicit rule that two degrees of one spectrum never coexist.

### 4. How traits are shown

- The **Character (Bio) tab** of the inspect pane lists traits as a short vertical list of labels
  under the backstory block, beside the skills; the tab is where "a pawn's various skills are
  displayed" [S] and traits share it.
- **Forced traits** (from a backstory or a gene) are drawn in **pale blue**; **suppressed** ones
  (overruled by a gene) in **dark grey** [S].
- **Hovering** a trait shows its description (flavour) followed by the generated effect lines: each
  stat offset and factor with its number, each skill offset with its number, disabled work types,
  and the standing mood effect [R]. So yes — the effects are shown numerically, and the flavour
  text and the numbers are two blocks of one tooltip.
- Skill offsets are folded into the displayed skill level rather than shown as a separate column
  [R]; the disabled work types show as greyed cells in the Work tab, which is the only place they
  are visible day to day.

### 5. What 1.5 / 1.6 and the expansions changed

- **Counts by expansion [S]**: Ideology adds **15** traits, Biotech **7**, Anomaly **6** for
  ordinary pawns plus **4** exclusive to creep-joiners. Royalty's are tied to the psychic
  sensitivity spectrum rather than being new families [R].
- **Biotech (1.4)** made genes a second source of traits: a gene can force a trait or suppress one,
  and the "pyrophobia gene suppresses the pyromania trait entirely, including the mental break"
  [S]. It also added the child trait limits in §3 and the passion–trait conflict rule [R].
- **1.5 (with Anomaly)**: a Pyromaniac gets a **+8** mood thought for using a fire weapon
  (1.5.4062) [S]; Anomaly's traits are the flavour of its creep-joiners and its ritual specialists
  rather than new effect kinds [R].
- **1.6 (with Odyssey)**: fixes to what counts as an incendiary weapon for a pyromaniac (smoke, EMP
  and tox launchers no longer do) and Odyssey's unique weapon traits count as incendiary [S].
- **No effect kind was removed or merged** across these versions; the changes were additions,
  gene interactions, and thought tweaks. Trait *count* per pawn and the 1–3 rule are unchanged
  since 1.0 [R].

### 6. What a player of fifty colonists would notice, ranked

Ranked by how often a player sees it without opening a pane, in a colony too large to know each
colonist.

| Rank | Effect (category, not name) | Why it shows |
|---|---|---|
| 1 | **Global work speed factor** (industriousness, ±20–35 %) | every stroke of every job; the roster's activity line and the pile growing show it; the project already has the per-mille rate seam (WS2), so it is cheap |
| 2 | **Break threshold and permanent mood offset** together (nerves, natural mood) | they decide *who* breaks; in a big colony the player meets colonists through the break alert, and the same three names keep coming up |
| 3 | **Unlocked mental break on a timer** (the fire-starter) | disproportionate to its rarity — one carrier is a standing threat to the whole base, and it is the one trait players reject a pawn for on sight |
| 4 | **Move speed offset** (±0.2–0.4 on 4.6, so ±4–9 %) | hauling is most of what fifty colonists do; the fast one arrives first every time |
| 5 | **Learning factor** (×1.75 / ×0.35) | shows as level-up toasts (SK4) coming weeks apart or days apart; needs the toast to be visible at all |
| 6 | **Conditional thoughts** (night owl, greedy/jealous, ascetic, nudist) | they drive demands — a better room, a night schedule — and the mood pane names the trait when the player looks for why |
| 7 | **Disabled work type** | one grey cell in the Work tab; noticed the day the only cook refuses to cook |
| 8 | **Interaction weighting** (abrasive, kind) | the social log and the fights; invisible without a social system |
| 9 | **Incoming damage / dodge** (tough, nimble, wimp) | combat only; strong there, silent otherwise |
| 10 | **Beauty, psychic sensitivity, immunity** | need opinion, psychic and disease systems that do not exist here |

Reasoning across the whole list: the top four are visible through systems the project already
draws (activity lines, alerts, hauling, the Events panel); five and six need a pane open; the rest
need systems the project does not have.

## Recommendation

**Ship traits as a data-driven Def with list-shaped effects and a spectrum key, and ship only the
four effect kinds the game can already show.** Everything else is a field that exists in the Def
and is read by nothing until its system arrives — the same pattern the project used for
`PlantDef.yields` and got bitten by, so each held field should be either absent or covered by a
test that says it is unread.

A `TraitDef` shape:

| Field | Type | Notes |
|---|---|---|
| `key` | registry key (`ui.trait.*`) | name and description live in `icon-keys.csv`, never in C# |
| `spectrum` | key or empty | two degrees of one spectrum never coexist; empty means singular |
| `degree` | int, signed | negative is the bad end; zero is never a trait |
| `commonality` | per-mille weight | plain weight, summed over candidates |
| `conflicts` | list of trait keys | symmetric by convention; a test asserts symmetry |
| `moodOffset` | int | delivered as a permanent thought, so the mood pane explains it |
| `breakThresholdPerMille` | int, signed | additive; clamp the minor line to 10–500 per mille |
| `statFactors` | list of (stat key, per-mille) | work speed, move speed, learning — the three the rate seam already reads |
| `statOffsets` | list of (stat key, per-mille) | empty at first; here so the shape does not change |
| `skillOffsets` | list of (skill index, levels) | applied at roll time only |
| `disabledWork` | list of work type keys | greys the Work tab cell; hold until a work type is worth refusing |
| `blockedThoughts` / `forcedThoughts` | lists of thought keys | hold until the thought catalogue exists |
| `unlockedBreak` | break key or empty | hold; the one that matters (fire) needs fire |

**Ship first (four kinds):** the global work speed factor (industriousness, into
`WorkSpeedPerMille`), the move speed factor (into `PacePerMille`), the permanent mood offset, and
the break threshold offset. The learning factor is a fifth for free — `LearningFactorPerMille()` is
already the empty seam — but it only shows once the level-up toast has landed on `main`.

**Trait count:** 1–3 uniform for an adult, matching the reference, with the roll's order being
scenario → backstory → commonality fill, all from the pawn's existing `RollSeed` so the person on
the setup card carries the traits the player read there. **Hold** skill offsets (they interfere with
the colonist-select card's skills line until the card shows the trait), disabled work, the thought
lists, interactions, combat stats and everything in rank 8–10.

**Numbers to start from**, scaled to per-mille: work speed **±200 / ±350**, move **±40 / +90**,
mood **±6 / ±12**, break threshold **+80 / +150 / −90 / −180**. These are the reference's, and the
first playtest will say whether a fifty-colonist colony can feel a ±20 % on one colonist at all.

**Naming:** the project invents its own. Suggest naming by category on the card — *diligence*,
*temper*, *outlook*, *pace* — so the tooltip can say "diligence: works 35 % faster" and the wiki
row is one line per degree.

## Sources

Only search summaries could be read; every page fetch was refused by the egress proxy. The pages the
summaries quote:

- Traits — RimWorld Wiki: https://rimworldwiki.com/wiki/Traits (trait limits by age, commonality
  weighting, Kind 2 vs Psychopath 1, gene suppression, spectrum rule)
- Global Work Speed — RimWorld Wiki: https://rimworldwiki.com/wiki/Global_Work_Speed (industriousness
  and neurotic numbers)
- Mental Break Threshold — RimWorld Wiki: https://rimworldwiki.com/wiki/Mental_Break_Threshold
  (35/20/5 lines, additive offsets, 1–50 % cap)
- Mental break — RimWorld Wiki: https://rimworldwiki.com/wiki/Mental_break (pyromaniac break,
  1.5.4062 and 1.6.4566 notes)
- Characters — RimWorld Wiki: https://rimworldwiki.com/wiki/Characters (Bio tab; pale blue / dark
  grey rendering of forced and suppressed traits)
- Anomaly (DLC) — RimWorld Wiki: https://rimworldwiki.com/wiki/Anomaly_(DLC) (trait counts per
  expansion)
- Property talk: Commonality — RimWorld Wiki: https://rimworldwiki.com/wiki/Property_talk:Commonality
  (exists; unread)
- Community discussion on impact: https://steamcommunity.com/app/294100/discussions/0/3113646913573228752/
  ("best and worst traits", movement speed and learning rate as the valued ones; Teetotaler, Body
  purist and Depressive as the rejected ones); https://www.dualshockers.com/rimworld-best-pawn-traits/

## Confidence

**Medium.** The generation rules (§3), the industriousness and neurotic numbers, the break-threshold
lines and the interface colours are stated by the wiki through the search summaries and are high.
The remaining numbers in §1 and §2 are recalled from the same public wiki, not read this session,
and should be checked against it before any is copied into a Def; a single value being off by a
degree (Slothful −30 vs −35) is already visible in the sources.

## Could not be determined

- **The full commonality table.** Only Kind (2) and Psychopath (1) are confirmed; the wiki's
  *Property talk: Commonality* page holds the rest and was unreachable.
- **The exact numbers of the nerves, speed, beauty, psychic and learning spectra** as of 1.6 — given
  from recall and marked [R].
- **Which of the Ideology, Biotech and Anomaly traits carry new effect *kinds*** rather than new
  combinations of the kinds in §1 — the counts are known, the contents are not.
- **The exact order in which the tooltip lists its effect lines**, and whether the mood offset is
  printed as a number in the tooltip or only in the mood breakdown.
- Whether the search summary's Slothful **−30 %** or the recalled **−35 %** is current.
