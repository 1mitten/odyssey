# Combat C2 and C3 — the hand-over

**Checkpoints 2 and 3 together** (`docs/plans/combat.md`): health and melee against a marauder
(C2) and weapons (C3). Built 2026-09-23 by four parallel lanes and integrated the same day; what was
merged, wired, measured and decided is `docs/design/33-combat.md` §6E.

## Where

**`D:\code\odyssey-combat`** — branch `claude/combat-c2`, **no PR yet** (open one after the
playtest). `Assets/Synty` is junctioned to the main checkout's packs, the Sword Combat clips
included, and the module catalogue has been rebuilt against them. Press Play → **New game** on the
meadow: the default *Playtest* start lays a **bat and a machete** on the ground beside the food. The
debug menu is **backtick**; its **Spawn** tab has *Spawn marauder* and one row per weapon.

## What changed

| Change | Was | Is | Where |
|---|---|---|---|
| Hit points | nobody could be hurt | one pool per pawn — person 100, hog 60, rat 15; **downed at 0**, **dead at −50 %** | design 33 §1, §3 |
| A health bar over a pawn | none | over the hurt, the downed and the drafted; green, amber below 60 %, red below 40 % — the need bars' colours | §6B, §6E |
| The marauder | none | *Spawn marauder* puts one near the camera **holding a machete**, under a red diamond; it hunts the nearest colonist **standing** and ignores the downed | §1, §6A.6, §6D |
| Right-click with drafted colonists selected | always a move | on an **animal or a marauder**, an attack by every drafted colonist selected; **Ctrl** on a colonist attacks her; on the ground, still a move | §2f, §6C |
| Right-click a weapon | a move | the selected colonist — **drafted or not** — walks to it, stoops and takes it up; the old one is put down where she stands | §5j, §6D, §6E |
| The fight, drawn | nothing | the pack's sword swings, hit reacts, staggers, dodges, stun loop and knock-down; a punch and a hog's or rat's bite are computed | §6B |
| Words over a fight | none | *Miss*, *Dodge*, *Stunned*, *Downed*, *Dead* and the damage (*-7*); a number is brief, *Downed* and *Dead* linger | §6C, §6E |
| Being struck | nothing happened | a colonist stops and fights back against whoever hit her; a hog usually turns, a rat usually runs | §6A.6 |
| A drafted colonist | held her ground | holds her ground **and strikes a threat beside her by herself**, never chasing it | §6A.6 |
| Downed | — | lies where she fell, needs paused; a colonist heals **only in a bed**, an animal wherever it lies, a marauder never | §6A.3–4 |
| Death, and the body | — | the body lies in a death pose; click it: *Corpse of Aria*, *Dead · since 07h, day 3 of …*; a dead pawn's weapon lies at the body | §6A.3, §6B, §6C, §6D |
| Weapons | none | **bat and crowbar** (blunt — can stun), **machete and arc blade** (sharp); drawn **in the right hand**, and **lying flat** on the ground | §6D, §6E |
| The Health tab | empty | *73 / 100*, a bar, and two rows: condition (*Unhurt*, *Hurt*, *Stunned*, *Downed*) and weapon (*Bare hands* or the weapon) | §6C |
| A marauder's pane | a colonist's, with a Draft button | no face, no tabs, no commands; its line says what it is doing, e.g. *Fighting* | §6C |
| Melee | not a skill | a live skill on the Skills tab, trained by every swing | §5a |

## What to test

| Test | Look for | A wrong answer looks like |
|---|---|---|
| Spawn a marauder beside two colonists and **do not draft** | it walks to the nearer one and swings; she stops work and fights back; numbers float off both | the marauder stands idle; she carries on working while being hit; the two circle each other and never land a blow |
| Watch a **machete** swing, then a **bat** or **crowbar** | the blade reaches the target on the instant the number floats; a heavy swing reads as a blow | a twitch (the heavy clips run 1.4–1.9× their authored speed); a swing into the air beside the target; the number well before or after the blade arrives; sword clips looking wrong with a club in the hand |
| Select a colonist, **undrafted**, and right-click the **bat** by the food | she walks over, stoops, and stands up with the bat in her right hand, gripped at the handle, pointing out of the fist | nothing happens; the bat hovers off the hand, is held by its head, sticks sideways through the forearm, or is far too big or small (the grip is measured and has never been seen) |
| Look at the **bat and machete lying** beside the food at the start | two recognisable weapons lying flat on the grass | standing on end like fence posts, sunk into the ground, or the orange box |
| Fight until **somebody goes down** | she drops to the ground; her bar empties to red; *Downed* floats and lingers; the marauder turns to the next colonist still standing | she stays upright; the marauder keeps beating a body on the ground; the bar vanishes |
| **Finish a downed marauder**: draft a colonist, right-click the body | she strikes until it dies; *Dead*; the body stays lying; clicking it says *Corpse of a marauder*; the machete lies beside it | the body vanishes or stands up; the machete is gone; a click on the body selects nothing |
| **Is it right that only an order kills?** Let a fight run unattended | it ends in downs, never corpses — the killing blow must cross −50 % of the pool, and nobody strikes a body on the ground unless told to | you expected unattended fights to be lethal: say so, and the rule in design 33 §3 changes |
| Select a **hurt colonist** and open the Health tab | *73 / 100*, *Hurt*, the weapon she holds; the bar in the tab is the colour of the bar over her head | the tab is empty; the two bars disagree in colour; a whole colonist reads *0 / 0* |
| Two drafted colonists attack a **hog**, then a **rat** | the hog usually turns on them; the rat usually runs | the hog always runs, or the rat always fights |
| **Pause** in the middle of a swing | the figure freezes mid-blow and carries on when unpaused | the swing plays on while paused, or restarts |
| Read the bars, diamond and words **at the play camera's distance** | legible at a glance with a few fighting | too small to read, or so many that the fight is hidden under them |
| **Listen** | — | a fight is silent: the five combat sounds have no clips yet. That is expected, not a fault |

## Still owed

- **The combat sounds.** Swing, hit, miss, down and death are named (`SoundIds.Combat*`) with no
  clips, so every fight is silent until clips are supplied or chosen.
- **Rescue (C4).** A downed colonist heals only in a bed and nothing can carry her there yet, so a
  colonist who goes down in this build stays down. That is C4's job, next.
- **The weapon's grip has never been looked at.** It is measured from the mesh (§6E); a contact
  sheet would settle it before the playtest does.
- **The heavy swings' speed, every computed pose angle, the words' lifetimes and colours** are
  INVENTED and wait for this playtest.
- **Known and left:** a downed pawn beyond the 64-figure cap is drawn standing; a downed colonist's
  click box is the standing one; a corpse always falls the same way; swapping weapons shows no
  put-down of the old one.

## Blocked on you

- **The playtest itself**, on the path above. Phase 4 (rescue, friendly fire, buildings as targets)
  waits on the verdict.
- **A PR** from `claude/combat-c2` once you are happy — not opened, by the workflow's instruction.
- **Sound clips** for the five combat sounds, or a word that I should choose them.
- **The draft sound's licence** (Pixabay, Dragon Studio), still to confirm — design 33 §2i.

## Merge order

One branch. `claude/combat-c2` sits on `main` after PR #176 (C1 and wildlife) and carries nothing
another open branch needs. Temperature (`claude/temperature-core`) is independent of it — combat adds
save sections, not a format bump — so either may merge first; whichever goes second re-runs the fast,
Long and Unity tiers after merging `main`, because both change what a pawn saves and hashes.
