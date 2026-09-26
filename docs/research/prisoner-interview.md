# Prisoners — owner interview

**Phase:** Interview (feature-level, in the shape of `home-area-interview.md`).
**Date:** 2026-09-26. **Branch:** `claude/prisoner-bed-assignment-98afc0` (documents only; no code
was written before or under this file).
**Conducted by:** Claude Code. Sixteen questions in four rounds, after a read-only exploration of:
- the beds and their owners;
- the enclosure grid (rooms);
- doors and traverse modes;
- allegiance and the minds;
- downed hostiles, rescue and raids;
- needs and the doctor;
- work types and saves.

The owner's brief: *"We need a prisoner similarish is to this but improved
https://rimworldwiki.com/wiki/Prisoner — We need to be able to assign beds as prison bed to assign
room. We need to create some systems here that could help bring this together, … explore research
and clarify details with me."*

**Read next:**
- `docs/design/59-prisoners.md`, the design these answers decide;
- `docs/research/a-20-prisoners.md`, how the reference and the genre do it;
- `docs/plans/prisoners.md`.

## 1. What the exploration found, put to the owner before the first question

- **Nothing of it is simulated, but the names are reserved.** These keys are already in
  `icon-keys.csv` and the generated registry:
  - `ui.pawn.prisoner`
  - `ui.command.capture` (*Carry to a prisoner bed*)
  - `ui.command.arrest`
  - `ui.command.recruit`
  - `ui.bulletin.recruited`
  - `ui.alert.prisonerescape`
  - `ui.work.warden`

  The Work tab draws the Warden column as not simulated, and `ui.skill.social` is drawn but not
  simulated.
- **A bed has an owner but no purpose.** Four near-copies of *own bed, else the nearest unowned
  bed* choose beds for sleep, patients and rescue. The owner picker offers hogs and bandits too.
- **Rooms exist but have no roles.** The temperature work's `EnclosureGrid` finds every enclosed,
  roofed room per layer, and doors are its boundaries.
- **Doors have no locks.** A bandit's traverse mode cannot open one, but it can walk through one a
  colonist is holding open.
- **Being hostile is a property of the pawn's kind, not the pawn.**
  - A downed bandit never heals and bleeds out.
  - A right-click on one today **executes** it.
  - The rescue carry is the obvious template for capture.

## 2. The answers

### Round 1: the core model

| Question | Options put | Answer |
|---|---|---|
| What makes somewhere a prison? | **Bed flag sets the room** (recommended; a prison bed in an open room still works, shackled) · room designation on a room pane · bed only, no rooms | **Bed flag sets the room** |
| How does a cell hold a prisoner? | **Prisoners can't open doors** (recommended; they slip through one held open) · lockable doors · soft confinement only | **Prisoners can't open doors** |
| How does a downed enemy become a prisoner? (multi) | Right-click Capture · Warden auto-captures · Surrender · Arrest a standing person | **All four** |
| What does the first build include? | Capture, hold, feed, tend, release (recommended) · include recruitment · capture and hold only | **Include recruitment** |

### Round 2: recruiting, the warden, escapes, fates

| Question | Options put | Answer |
|---|---|---|
| How does recruiting work? | **Visible progress bar** (recommended; warden skill × mood × treatment, ETA and blockers shown) · RimWorld style (resistance then a roll) · once-a-day attempt | **Visible progress bar** |
| What makes one warden better? | **Add the Social skill** (recommended; save-format bump, goldens re-baked) · no skill, fixed rate | **Add the Social skill** |
| How do escapes work? | **Visible risk from conditions** (recommended; normalised by prison size, recapturable) · RimWorld random break · no escapes yet | **Visible risk from conditions** |
| Other fates? (multi) | Prison labour · Execute · Ransom / exchange (a seam until factions) · Exile | **All four wanted** (the build order is in round 3) |

### Round 3: details a player sees

| Question | Options put | Answer |
|---|---|---|
| When may a colonist be arrested (no visitors exist yet)? | **Any colonist, any time** (recommended; she may resist; colony mood hit) · only during a mental break · record it, build later | **Any colonist, any time** |
| What does a prisoner look like? | **Prison uniform** (recommended; face, hair and beard kept; issued jumpsuit on recruiting) · keep their clothes, with a badge | **Prison uniform** |
| Which extra fates go in the first build? (multi) | Exile · Execute · Prison labour · Ransom (seam only) | **Exile, and Ransom as a seam.** Execute and labour are later units. |
| How does a prisoner spend the day in a proper cell? | **Walk the cell, sleep in bed** (recommended) · stay in bed | **Walk the cell, sleep in bed** |

### Round 4: the calls the design pass left open

| Question | Options put | Answer |
|---|---|---|
| How does an escapee get through a closed cell door? | **Bash the door down** (recommended; the building attack exists; door material matters) · open it · only through a held-open door | **Bash the door down** |
| What does a plain right-click on a downed enemy do? | **Menu: Capture / Finish off** (recommended) · capture, Ctrl+right-click kills · keep the kill, Capture in the menu only | **Menu: Capture / Finish off** |
| What does Release do for a colonist you arrested? | **Back to the colony** (recommended) · leaves the map | **Back to the colony** |
| Can prisoners have mental breaks? | **No; low mood raises escape risk** (recommended) · yes, like colonists | **No** |

## 3. What was not asked, and is decided in the design

These are tuning proposals, and **the owner confirms or corrects them at the first play**:
- the recruitment numbers;
- the escape-risk factors;
- the surrender chance;
- the arrest resistance.

They are in `docs/design/59-prisoners.md` §8–§10, with the days-to-recruit table the recruitment numbers imply.
