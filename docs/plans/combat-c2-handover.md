# Combat C2 and C3 — the hand-over

**Checkpoints 2 and 3 together** (`docs/plans/combat.md`): health and melee against a marauder
(C2) and weapons (C3). Built 2026-09-23 by four parallel lanes and integrated the same day; what was
merged, wired, measured and decided is `docs/design/33-combat.md` §6E; the eight faults two
reviewers found afterwards, all fixed, are §6F.

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
| A second colonist hitting a marauder | it dropped the swing it was winding up and seldom turned on her | it keeps fighting the colonist in front of it and goes for the hitter when that one is down; a marauder still chasing somebody turns on whoever hits it | §6A.6, §6F |
| Right-clicking the same target again | restarted the attack and threw away the wind-up | ignored: the swing in the air lands | §6A.8, §6F |
| Finishing a downed pawn | the body stood up and fell over a second time | it dies where it lies | §6F |
| A fight on a layer that is not drawn | its words floated over whatever hid it, and a body fell in view | no words, no fall; the body is there, and clickable, when the layer is shown | §6F |
| A corpse chosen again after a colonist | wore the colonist's name and activity | its own | §6F |
| Pausing on a death, then Load, New game or Leave | an error, and a load left half a colony | works | §6F |

## What to test

| Test | Look for | A wrong answer looks like |
|---|---|---|
| Spawn a marauder beside two colonists and **do not draft** | it walks to the nearer one and swings; she stops work and fights back; numbers float off both | the marauder stands idle; she carries on working while being hit; the two circle each other and never land a blow |
| Watch a **machete** swing, then a **bat** or **crowbar** | the blade reaches the target on the instant the number floats; a heavy swing reads as a blow | a twitch (the heavy clips run 1.4–1.9× their authored speed); a swing into the air beside the target; the number well before or after the blade arrives; sword clips looking wrong with a club in the hand |
| Select a colonist, **undrafted**, and right-click the **bat** by the food | she walks over, stoops, and stands up with the bat in her right hand, gripped at the handle, pointing out of the fist | nothing happens; the bat hovers off the hand, is held by its head, sticks sideways through the forearm, or is far too big or small (the grip is measured and has never been seen) |
| Look at the **bat and machete lying** beside the food at the start | two recognisable weapons lying flat on the grass | standing on end like fence posts, sunk into the ground, or the orange box |
| Fight until **somebody goes down** | she drops to the ground; her bar empties to red; *Downed* floats and lingers; the marauder turns to the next colonist still standing | she stays upright; the marauder keeps beating a body on the ground; the bar vanishes |
| **Finish a downed marauder**: draft a colonist, right-click the body | she strikes until it dies; *Dead*; the body stays lying — it does not get up to fall again; clicking it says *Corpse of a marauder*; the machete lies beside it | the body vanishes, or stands and falls over; the machete is gone; a click on the body selects nothing |
| Two colonists, **undrafted**, beside one marauder | it keeps swinging at the one in front of it; when she goes down it turns on the other, not on whoever is nearest | it stops landing blows while both hit it; it ignores the one still hitting it; it flips between them every blow |
| **Is it right that only an order kills?** Let a fight run unattended | it ends in downs, never corpses — the killing blow must cross −50 % of the pool, and nobody strikes a body on the ground unless told to | you expected unattended fights to be lethal: say so, and the rule in design 33 §3 changes |
| Select a **hurt colonist** and open the Health tab | *73 / 100*, *Hurt*, the weapon she holds; the bar in the tab is the colour of the bar over her head | the tab is empty; the two bars disagree in colour; a whole colonist reads *0 / 0* |
| Two drafted colonists attack a **hog**, then a **rat** | the hog usually turns on them; the rat usually runs | the hog always runs, or the rat always fights |
| **Pause** in the middle of a swing | the figure freezes mid-blow and carries on when unpaused | the swing plays on while paused, or restarts |
| Read the bars, diamond and words **at the play camera's distance** | legible at a glance with a few fighting | too small to read, or so many that the fight is hidden under them |
| **Listen** | — | a fight is silent: the five combat sounds have no clips yet. That is expected, not a fault |

## The review's fixes (2026-09-23)

Eight faults, all confirmed and fixed, each with a test seen failing first (design 33 §6F). Tiers
after the fixes, at the commit this section was written for:

- **Fast:** Sim 1,164, Hud 797, 0 failed. **Long:** 39, 0 failed. Both content gates pass.
- **EditMode:** 2,832 total, 2,806 passed, 0 failed (17 skipped, 9 inconclusive).
- **PlayMode:** 107 total, 102 passed, 0 failed, 5 skipped — started alone; another project's
  batch run began three minutes in, and nothing failed.
- **Goldens:** unchanged; `Golden.cs` untouched and every golden test green.
- **Seen failing first:** the three simulation tests and the two pane tests in the fast tier, the
  four drawing tests in EditMode and `CorpseTeardownTests` in PlayMode, each with the fix withheld
  (the teardown with the reviewer's exact `ArgumentOutOfRangeException`). One half of one finding
  did **not** reproduce: a body baked while hidden measured the right box in this Unity, so the
  reorder that fixes it is kept as the safe order and is not what the test proves.

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

## The round after the playtest (2026-09-23)

The owner played checkpoints 2 and 3: *"It's mostly pretty decent."* Four asks, interviewed and
built as design 33 §7, integrated on **`D:\code\odyssey-combat-drawn`**, branch
**`claude/combat-c2-polish`**. Synty is junctioned. Press Play, then New game.

### What changed

| Change | Was | Is | Where |
|---|---|---|---|
| Right-click a weapon | the colonist was sent at once, with no sign of it | a menu at the pointer: *Equip machete* / *Cancel*; nothing is sent until you choose | §7a |
| After ordering an equip | no line | the same order line and floor bracket as a drafted move (no diamond); *Equipping* on the activity line | §7a |
| Right-click an enemy | attacked with no visible sign | attacks at once, and a **red lock-on ring** snaps in under the target, flashes once, stays faint, and fades when it goes down or dies or the order changes | §7b |
| Two or more attacking one target | could stand on the same tile | each takes the nearest free cell beside the target, side by side; a ninth waits one cell back | §7c |
| Escape with the menu open | — | closes the menu first | §7a |
| Blood | none | still none visible; the hook for the blood unit is in place | §7d |

### What to test

| Test | Look for | A wrong answer looks like |
|---|---|---|
| Select an undrafted colonist, right-click the machete, pick *Equip* | a readable menu by the pointer, then a line to the weapon and *Equipping* | the menu under the arrow or off-screen; or it is still not clear who is being sent (tell me if the colonist's name should be on the row) |
| Close the menu by Escape, a click elsewhere, an orbit, or clicking another colonist | it goes every time | it stays, or the closing click also does something |
| Draft two, select both, right-click a marauder | the ring closes in and flashes, then stays faint until the marauder is down | noticed only as it lands (0.2 s too quick), the flash reads as a glitch, or the ring is lost on grass or at night |
| Watch the two attackers arrive | they stand side by side on the side they came from | both on one tile, or one walking round to the far side for no reason |
| Right-click a hog | the ring fits the hog's length | a person-sized ring, or off centre |
| Right-click the ground with a drafted colonist | an instant move, no menu | a menu appears |

### Tests on the combined branch

- **Fast tier:** Sim 1,172 and Hud 837, 0 failed. **Long tier:** 39. Both content gates pass.
- **Unity:** EditMode 2,884 total, 0 failed, including the first compile of the menu and the ring. PlayMode 107 total, 0 failed, with no other batch run going.
- **Goldens:** none moved.

### Questions for you

- **Is a hog's bite sharp or blunt?** You said bites draw more blood, but `Species.xml` gives the hog's tusks *Blunt* and the rat's bite *Sharp*. The blood hook follows the content, so as it stands a hog draws the smaller, blunt puff (§7d).
- **Should Rescue join the menu?** It is instant today (right-click a downed colonist). Putting it in the menu would make it two clicks, so it is your call.
- **Blood** is the next unit, built to your answers in §7d.
