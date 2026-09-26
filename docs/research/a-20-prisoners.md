# a-20 — Prisoners

**Question:** How does RimWorld (the reference) model prisoners? That covers:
- how a person becomes one;
- what makes a place a prison;
- what can be done with a prisoner;
- how recruitment and prison breaks are driven.

What do players complain about in that system, and what does the rest of the genre do that could
improve on it?

**Lane:** A (reference mechanics). **Date:** 2026-09-26. **Cap:** 8 lookups (fetches + searches).

**How the cap was spent, and a caveat before anything else.** All eight lookups were used. Four
page reads (`rimworldwiki.com` *Prisoner* and *Prison break*, `steamcommunity.com`,
`rimworldaccess.com`) were **refused by the network egress proxy**, as they were for a-18. The four
searches ran. So what follows comes from the search engine's extracts plus recollection of the
game. Every recollected item is marked **(recalled)** and carries lower confidence.

**Every exact number for the reference is unverified.** That covers starting resistance ranges,
reduction per chat, break MTB and goodwill amounts. A session on a machine that can reach the wiki
should spend one read each on *Prisoner*, *Warden* and *Prison break* and strike the markers.

Clean room: mechanics, shapes and intent only. Nothing is pasted, and the names in our design are
our own.

## Findings

### 1. How a person becomes a prisoner

1. **Capture.** A non-colonist human who is downed can be ordered captured. A colonist carries
   them to a free prisoner bed. **(recalled)**
2. **Arrest.** A standing guest, visitor or colonist can be arrested. **(recalled)**
   - The target may resist, which turns into a fight.
   - Arresting a friendly faction's visitor costs goodwill with that faction.
3. **No bed, no capture.** With no free prisoner bed, the order is unavailable or cannot complete.
   **(recalled)**

### 2. What makes a prison

1. **A bed flag.** A bed has a *for prisoners* toggle. Setting it makes the **whole room** a prison,
   and a room cannot mix colonist and prisoner beds. **(recalled)**
2. **The room is the unit.** Prisoners stay inside their prison room, so walls and doors are what
   hold them. Room stats (space, beauty, cleanliness) reach the prisoner's mood like anyone's.
   **(recalled)**
3. **Doors do not really hold during a break.** Players report that escaping prisoners open doors
   freely, so maze and airlock designs do not slow them down (search extract). That is one of the
   complaints in §5.

### 3. What can be done with a prisoner

The interaction is a **mode set per prisoner**. **(recalled)** Our design has its own names for these.

| Mode | What it does |
|---|---|
| No interaction | The warden only feeds and cares for them. |
| Reduce resistance | The warden talks them down but never attempts a recruit. |
| Recruit | Talk resistance down to zero, then attempt recruitment. Wardens chat, then eventually hard-sell (search extract). |
| Release | Escorted off the map; the home faction usually gains goodwill. |
| Execute | Colonists take a mood penalty and the faction's goodwill drops. Some traits and beliefs are indifferent. |

**Expansions** add:
- **enslave:** a separate *will* track, then suppression and rebellion;
- **convert:** belief certainty;
- **blood-feeding / hemogen farming.**

**Organ harvest** is a surgery, not a mode.

Some pawns can **never be recruited**, only enslaved, released or used.

### 4. Recruitment and prison breaks

1. **Resistance is a gate.** Every prisoner starts with a resistance score that must reach zero
   before any recruit attempt (search extract).
2. **How much a chat removes depends on three factors** (search extract):
   - the warden's negotiation ability, which is driven by Social skill;
   - the prisoner's mood, with a harsh penalty at low mood;
   - the prisoner's opinion of the warden.
   **(recalled)** The shape is roughly: reduction per chat ≈ base × negotiation × mood curve ×
   opinion factor.
3. **Starting resistance is random.** It rises with colony size and depends on faction and pawn type.
   **(recalled)** Ranges not verified.
4. **Recruiting ends on a roll.** Once resistance is zero, each recruit chat is a chance, not a
   certainty. **(recalled)**
   - Older versions used a single percentage *recruitment difficulty* instead (minimum 10 %,
     maximum 99 %, mean 50 %, SD 15 %). That is the old system, recorded only as history (search
     extract).
5. **The warden's job list** **(recalled)**:
   - chatting;
   - feeding prisoners who cannot feed themselves;
   - delivering food to a cell;
   - taking prisoners to beds and to surgery;
   - escorting a release;
   - carrying out an execution.
6. **Food.** Prisoners eat food left in their cell; otherwise wardens bring meals. Low food means a
   mood penalty, which slows recruitment. **(recalled)**
7. **Prison breaks are a random roll per prisoner, not mood-driven.** Each prisoner's info shows a
   *prison break MTB (days)*, and −1 means that prisoner cannot start one (search extract, forum
   thread). Because each prisoner rolls independently, **larger prisons break out more often**.
   **(recalled)** One prisoner starts the break, others in connected cells join, and escapees make
   for the edge, sometimes picking up weapons.
8. **The expansions' slave rebellion answered the headcount complaint.** It has a 45-day base MTB,
   modified by mood, suppression, nearby weapons, the escape route and colonists present. It is
   counted only while the pawn is awake, and **per-slave odds shrink as the slave count grows**, so
   total risk does not grow linearly (search extract). That is the developer conceding the point
   our design takes up.

### 5. What players complain about (medium-high confidence; several forum threads via search)

- **Recruiting is slow and random.** It takes many chats over days, and a hard mood penalty means
  prisoners must be kept happy first.
- **Break risk scales with headcount.** Every prisoner rolls, so big prisons feel punished.
- **Breaks are unexplained.** Players cannot see what drives one, so they cannot prevent it.
- **Cell architecture does not matter.** Prisoners open doors freely during a break.
- **The break UI is poor.** The alert names one prisoner, and the rest are found by hand.
- **Prisoners have nothing to do.** Players want labour and more to manage.
- **Hauling the downed back is a chore.** **(recalled)** General sentiment.
- **The only ways to be rid of an unwanted prisoner are grim** (execute, harvest) or a release.
  **(recalled)**

### 6. The rest of the genre

| Game | Mechanic worth borrowing |
|---|---|
| **Dwarf Fortress** | Jails are built from **restraints** (chains, cages, ropes) zoned as a cell. A chained prisoner reaches adjacent tiles; a caged one has food brought. A justice system gives sentences. **Idea:** restraint tiers trading mobility against guard work. Our *shackled* bed is the first tier. |
| **Going Medieval** | Only enemies that **surrender** can be taken. A settler with the *gaoler* job makes **one attempt a day** that raises one **recruitability bar**, and the prisoner joins at 100 %. Shackles allow labour. **Idea:** a single rising bar is readable, and surrender avoids the hauling. |
| **Prison Architect** | **Reform programmes** unlock jobs. Parole at fixed fractions of a sentence, with a bonus if the parolee does not reoffend. **Idea:** trust tiers that unlock prison labour. |
| **Norland** | Prisoners work longer hours and are paid in food. They **revolt when average prisoner mood falls below a visible threshold**. Conscripting one frees them and lifts their mood. **Idea:** a group-level risk the player can see and manage, not a hidden per-prisoner roll. |
| Oxygen Not Included | No prisoner system. **(recalled)** |

### 7. What this points to for Odyssey (synthesis; the owner's choices are in `prisoner-interview.md`)

1. **Tie break risk to visible, player-controlled conditions** and normalise it by prisoner count.
2. **Make door and cell security matter.** A prisoner cannot open a door and an escapee has to
   break one, so door material and airlocks count.
3. **Show recruitment as deterministic progress** with an ETA and what is stalling it, not a hidden
   roll.
4. **Cut the hauling.** Offer surrender and a warden auto-capture mark.
5. **Offer a non-grim way out.** Exile and release; ransom once factions exist.
6. **Give prisoners something to do.** Labour, a later unit.
7. **Room roles already exist in the research.** `a-05-rooms-and-beauty.md` treats a *prison cell*
   as a room role, and says a bedroom may not contain a prison bed. The bed-flag-sets-the-room rule
   fits it.
8. **Layers.** `a-16-modding-architecture.md` records a multi-floor mod in which prisoner labour
   broke across levels. **Every prisoner rule must be layer-aware from its first commit.**

## Sources

- https://rimworldwiki.com/wiki/Prisoner — search extract only; the page read was refused by the proxy.
- https://rimworldwiki.com/wiki/Slavery — search extract.
- https://rimworldwiki.com/wiki/Template:Prison_Break — search listing only.
- https://steamcommunity.com/app/294100/discussions/0/2789318172120409181/ — the break MTB of −1 thread.
- https://steamcommunity.com/app/294100/discussions/0/3833172420307717880/
- Steam discussion threads …/1693788384145316769, …/5691968038965160264, …/1291817208481120586
  and …/4289187621818902909 — complaints, via search summary.
- https://gamerant.com/rimworld-prison-building-prisoner-recruitment-guide/
- https://rimworldaccess.com/colony/prisoners/ — search extract only.
- https://dwarffortresswiki.org/index.php/DF2014:Jail
- https://goingmedieval.fandom.com/wiki/Prisoner
- https://steamcommunity.com/games/1029780/announcements/detail/4237411074236853162 — Going Medieval prisoners revision.
- https://prison-architect.fandom.com/wiki/Parole_Hearing
- https://prison-architect.fandom.com/wiki/Reform_Programs
- https://www.gamepressure.com/norland/what-to-do-with-prisoners/z2114ff

## Confidence

- **Medium:**
  - the reference's structure: capture and arrest, the bed flag setting the room, the modes, what
    drives resistance, the warden's role;
  - the other games' mechanics, taken from search extracts.
- **Medium-high:** the complaints.
- **Low:** every exact reference number.

## Could not be determined

- Current starting-resistance ranges, the exact reduction curve, and the recruit chance once
  resistance is zero.
- The prison-break MTB base value, its modifiers, and how a break spreads between cells.
- Goodwill amounts for release, execution and organ harvest.
- Whether a prisoner walks out of an open door outside a break.
- The warden's job priorities and food-delivery rules.
- To settle these, read the three wiki pages from a network the proxy does not block.
