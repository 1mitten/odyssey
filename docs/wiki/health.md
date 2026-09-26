# Health and anatomy

Body parts, injuries and conditions. The anatomy sheet covers this better than any other part of the game, which is either fortunate or ominous.

39 entries, 21 without art. Names are what the player sees; the key beside each is the stable identifier — cite it when proposing a change.

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Brain** | `ui.health.brain` | Consciousness, and everything downstream of it | sheet 07 (anatomy), high | M6 |
| **Head** | `ui.health.head` | Houses most of what matters | sheet 07 (anatomy), high | M6 |
| **Eye** | `ui.health.eye` | Sight, and therefore shooting | sheet 07 (anatomy), high | M6 |
| **Ear** | `ui.health.ear` | Hearing, and social work <br>**Needs:** an ear | no art | M6 |
| **Nose** | `ui.health.nose` | Minor, until it is gone <br>**Needs:** a nose | no art | M6 |
| **Jaw** | `ui.health.jaw` | Eating and talking | sheet 07 (anatomy), med | M6 |
| **Neck** | `ui.health.neck` | A bad place to be hurt <br>**Needs:** a neck, distinguishable from a torso | no art | M6 |
| **Torso** | `ui.health.torso` | Vital, with the head, and where most blows land | sheet 07 (anatomy), high | M6 |
| **Heart** | `ui.health.heart` | Blood pumping. Fatal to lose | sheet 07 (anatomy), high | M6 |
| **Lung** | `ui.health.lung` | Breathing, and stamina | sheet 07 (anatomy), high | M6 |
| **Liver** | `ui.health.liver` | Filters. Slow to kill you | sheet 07 (anatomy), high | M6 |
| **Kidney** | `ui.health.kidney` | You have two for a reason | sheet 07 (anatomy), med | M6 |
| **Stomach** | `ui.health.stomach` | Digestion | sheet 07 (anatomy), med | M6 |
| **Spine** | `ui.health.spine` | Moving at all | sheet 07 (anatomy), med | M6 |
| **Shoulder** | `ui.health.shoulder` | Carries the arm <br>**Needs:** a shoulder joint | no art | M6 |
| **Arm** | `ui.health.arm` | Manipulation | sheet 07 (anatomy), med | M6 |
| **Hand** | `ui.health.hand` | Fine work | sheet 07 (anatomy), med | M6 |
| **Finger** | `ui.health.finger` | Small loss, real cost <br>**Needs:** a finger | no art | M6 |
| **Leg** | `ui.health.leg` | Walking speed | sheet 07 (anatomy), med | M6 |
| **Foot** | `ui.health.foot` | Balance and pace <br>**Needs:** a foot | no art | M6 |
| **Blood loss** | `ui.health.blood` | How much has been lost | sheet 06 (action tiles), low | M6 |
| **Scar** | `ui.health.scar` | Permanent, and it aches <br>**Needs:** an old healed wound | no art | M6 |
| **Wound** | `ui.health.wound` | A cut from a blade or a bullet. The only kind that bleeds | sheet 07 (anatomy), low | M6 |
| **Burn** | `ui.health.burn` | Fire damage | sheet 06 (action tiles), low | M6 |
| **Fracture** | `ui.health.fracture` | Broken, not severed | sheet 07 (anatomy), med | M6 |
| **Missing part** | `ui.health.missing` | Gone. Replace or adapt <br>**Needs:** an absent limb, shown as a silhouette gap | no art | M6 |
| **Implant** | `ui.health.implant` | Fitted, and working <br>**Needs:** a fitted mechanical part | no art | M6 |
| **Anaesthetic** | `ui.health.anaesthetic` | Under, and safe to operate on <br>**Needs:** under anaesthetic. A syringe or a mask would do | no art | M6 |
| **Left arm** | `ui.health.arm.left` | Half of what her hands can do | no art | HE |
| **Right arm** | `ui.health.arm.right` | Half of what her hands can do | no art | HE |
| **Left leg** | `ui.health.leg.left` | Half of her walking | no art | HE |
| **Right leg** | `ui.health.leg.right` | Half of her walking | no art | HE |
| **Pain** | `ui.health.pain` | Every point of injury hurts. At 80 per cent she goes down | no art | HE |
| **Consciousness** | `ui.health.consciousness` | What pain and blood loss take. Under 30 per cent she goes down | no art | HE |
| **Moving** | `ui.health.moving` | Her legs, and how awake she is. How fast she walks | no art | HE |
| **Manipulation** | `ui.health.manipulation` | Her arms, and how awake she is. How fast she works | no art | HE |
| **Bruise** | `ui.health.bruise` | From a blunt blow or a short fall. Hurts, and never bleeds | no art | HE |
| **Tended** | `ui.health.tended` | Treated. The bleeding has stopped, and it heals out of bed | no art | HE |
| **to death** | `ui.health.todeath` | How long the bleeding leaves her, untreated | no art | HE |

---

Generated from `docs/design/icon-keys.csv` and `docs/design/icon-map.csv`. To change a name or a description, edit `icon-keys.csv` and rerun `python3 tools/wiki/build_wiki.py`.
