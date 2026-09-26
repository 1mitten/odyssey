# Storyteller: owner interview

**Phase:** interview, at the feature level, in the shape of `cover-interview.md` and
`home-area-interview.md`.
**Date:** 2026-09-26.
**Branch:** `claude/sweet-cerf-wzfxgs` (documents only; no code was written before this file or
under it).
**Conducted by:** Claude Code. Nineteen questions in five rounds, asked after two read-only sweeps:
one of the repository (the incident layer, raids, the calendar, settings and the setup page), and one
of the reference's storytellers (`a-11-storyteller-pacing.md`, which adds to `a-11-storyteller-incidents.md`).

**The owner's brief:** *"We should look into the storytelling aspect - we can decide on names later
but base it around this. https://rimworldwiki.com/wiki/AI_Storytellers - we'll invite our own story
tellers later and refine it for us, refine, explore and plan. Interview me for all the details"*

**Read next:**
- `docs/design/58-storyteller.md`: the design these answers decide.
- `docs/plans/storyteller.md`
- `docs/research/a-11-storyteller-incidents.md` (2026-09-20) and `a-11-storyteller-pacing.md` (this
  session)
- `docs/design/23-events-and-storyteller.md` §2 and §8, which named the seams this plugs into

## 1. What the exploration found, put to the owner before the questions

- **The incident layer is built and nothing drives it.** `IncidentDef` carries its gates
  (`earliestDay`, `minRefireDays`, `weight`, `minColonists`, `maxFires`), a favourability and a
  category. `Incidents.TryFire` is the one door, and `IncidentLedger` is saved and hashed and already
  answers `LastFiredTick` and `Fires`. **No code reads any gate**; only the debug menu fires.
- **Four incidents can fire**: the supply, scrap and medical drops (Misc, Good) and the raid
  (ThreatBig, Bad). Theft and BanditLeft are records and are not fireable.
- **A raid sizes itself by headcount and days** (`RaidBudget.AutoSize`). Design 55 recorded that a
  wealth measure would replace that function and nothing else. **Nothing in the game has a value to
  total**: `ItemDef` has no market value.
- **There is no difficulty setting of any kind**, no adaptation, and no storyteller slot on the world
  setup page (design 19).
- **Our year is 72 days**: six 12-day months and three 24-day seasons. The reference's is 60 days
  in four 15-day quadrums, so its day numbers map across at about ×1.2.
- **The reference's most criticised mechanic is its wealth-based threat budget.** It punishes
  building nice things and grew a whole "wealth management" meta. Its second is that adaptation is
  hidden rubber-banding, so a disaster that eases the next raid feels like the game cheating.

## 2. Questions and answers

Recommended options are marked ★. The answer is the owner's; every recommendation was taken.

| # | Question | Options offered | Answer |
|---|---|---|---|
| 1 | What decides how hard a threat hits (the threat budget)? | ★ strength-based · wealth + headcount (reference) · hybrid · headcount + days (today) | **Strength-based** |
| 2 | Should the storyteller ease off after a disaster (adaptation)? | ★ yes, and visible · yes, hidden (reference) · per storyteller · none | **Yes, and visible** |
| 3 | How do difficulty and storyteller relate? | ★ separate axes · storyteller includes difficulty · difficulty only, one storyteller | **Separate axes**, both picked on the world setup page |
| 4 | Can they be changed mid-colony? | ★ yes, from Settings · difficulty only · locked at start | **Yes, both, from Settings** |
| 5 | Which archetypes ship first (names later)? | ★ three classic shapes · three + a reactive director · one (steady) first · steady + chaotic | **Three classic shapes**: steady cycle, calm cycle, chaotic random |
| 6 | How much persona on screen? | ★ portrait + blurb at setup · persona with commentary · invisible | **Portrait and blurb** at setup and in Settings, nothing in play |
| 7 | Should big threats be telegraphed? | ★ rely on the raid's gather · a tension forecast · an explicit warning | **Rely on the raid's gather** (2–4 game hours, design 55) |
| 8 | Pause and jump the camera on a big threat? | ★ a setting, default on · pause only · neither | **A Settings > Gameplay toggle, on by default** |
| 9 | What does the storyteller unit bring with it? | ★ pacer only, pool later · pacer + starter batch · pacer + conditions family | **Pacer only**; each new incident is its own unit |
| 10 | Should it steer colony size (population intent)? | ★ yes, with the first joiner event · design only · no | **Yes, with the first joiner event** |
| 11 | Where does the tension readout live? | ★ a small gauge by the clock · Events panel line · History screen only · debug overlay first | **A small gauge by the clock**, with a tooltip naming what moved it |
| 12 | What shape is the difficulty setting? | ★ presets + Custom · presets only · one slider | **Presets + Custom**, the Graphics presets' pattern |
| 13 | Q9 and Q10 disagree about the joiner: where does it go? | ★ the first follow-up unit · inside the storyteller unit | **The first follow-up unit.** The curve goes on the Def now, read by a stub |
| 14 | What counts towards combat strength? | ★ people and weapons only · + defences at a discount · + animals | **People and weapons only.** Fortifications never count |
| 15 | When may the first big threat come? | ★ per storyteller · one fixed grace · keep day 3 | **Per storyteller**, stretched by difficulty |
| 16 | How many big threats a season for Steady at normal? | ★ about 3 · about 2 · about 5 | **About 3 a 24-day season** |
| 17 | How often do good or neutral events come? | ★ about every 5–6 days · about every 3 · per storyteller | **About every 5–6 days** (Chaotic folds them into its roll) |
| 18 | How precise is the gauge? | ★ bands + cause · exact multiplier · bar only | **Bands + cause** |
| 19 | What does this session produce? | ★ interview + design + plan · also a mockup brief · straight to building | **Interview, design and plan**, then stop for approval |

Three more were asked after the plan agent read the code, because the code forced them:

| # | Question | Options offered | Answer |
|---|---|---|---|
| 20 | A save from before the storyteller has none. What happens on load? | ★ load with none, pick in Settings · load with Steady at normal | **Load with none**; a one-time note says to pick one in Settings |
| 21 | `Incident_Raid.minRefireDays` is 4, a floor every storyteller inherits. Change it? | ★ lower to about 2 · keep 4 | **Lower to about 2**: the storyteller paces, the Def is a safety floor |
| 22 | How many tension bands? | ★ five · four · three | **Five** |

## 3. Why the recommendations were recommended

- **Strength, not wealth.** A threat budget measures how dangerous the colony is. Wealth is a proxy
  for that, and a poor one, because it counts a marble floor as much as a rifle. Measuring what can
  fight removes the proxy. It also fits a game whose richest content is combat (melee, ranged,
  cover, raids) and that has no value measure to total.
- **People and weapons only.** Counting sandbags would mean building a defence calls a bigger raid,
  which is the wealth meta again in a new form. Animals are left out until taming exists.
- **Visible adaptation.** The recovery window after a disaster is good design; hiding it is what
  players resent. Bands with a named cause show the mechanism without inviting min-maxing on a
  number (Q18).
- **Separate axes and mid-game change.** Three pacing shapes × six difficulty rungs is eighteen
  experiences from nine pieces of content. Letting both change mid-game forgives a bad pick.
- **Three shapes on one kit.** The reference's three cover the space (predictable rise, long
  peace, chaos). The reactive "director" (Left 4 Dead's idea, reading mood and injuries) is the most
  original option, and it can be added later as a fourth Def on the same kit once there is enough
  content to direct.
- **Pacer only.** The pacer is what can be proven in a headless soak. A thin pool is a content
  problem, and design 23's recipe already makes each incident a small unit.

## 4. Tensions chosen into

- **Strength-based budgets invite the inverse meta: disarm before a raid.** If raid size follows
  what the colony holds at the instant it fires, stowing weapons shrinks raids. Design 58 §4c
  answers this with a **remembered peak**: strength is read as a slowly decaying maximum, so
  disarming buys nothing for days.
- **Visible rubber-banding is still rubber-banding.** A player can read "reeling" and know the next
  raid is small. Accepted: the bands are coarse, and the pacer still decides *when*.
- **No storyteller warning** relies on the raid's gather phase. A future threat with no gather (a
  drop pod, a mad animal) will need its own telegraph, or will land as the reference's do.
- **Pacer only, with population intent.** The curve is designed and saved now but moves nothing
  until the joiner exists (ST7). A test with a stub is its only proof until then.
- **Our threat budget cannot be checked against the reference's numbers.** Its curves are built on
  wealth. Ours start invented and are tuned by the soak (design 58 §9), which is the instrument.

## 5. What this does not settle

- **The storytellers' names, portraits and blurbs**, and the difficulty rungs' names. Placeholder
  keys go in the registry; the owner names them later, and "inviting our own storytellers" is
  writing a Def and a registry row.
- **The five band names** (placeholders: reeling, easing, even, building, peak; "steady" was renamed so it cannot be read as the storyteller of that name).
- **Every number in design 58** is invented until the Long-tier soak has run.
- **Conditions** (a timed, map-wide event with no entities: cold snap, eclipse), **quests**, the
  **History screen** (F9) and a **reactive director** are recorded, not planned here.
- **Whether a mockup brief is wanted** for the setup page's picker and the gauge, before ST5.
