# Events

Three channels, and the difference is not cosmetic. Alerts are conditions that persist until fixed. Bulletins are things that happened and are kept until dismissed. Toasts are things that happened and are gone in six seconds, which is the right home for anything that recurs often enough that clearing it by hand would become a chore. Alerts and bulletins carry the layer they occurred on and jump the camera there, which a flat colony sim never has to think about.

46 entries, 21 without art. Names are what the player sees; the key beside each is the stable identifier — cite it when proposing a change.

## Alerts

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Starving** | `ui.alert.starvation` | Someone is going hungry | sheet 02 (food), low | M2 |
| **Breach** | `ui.alert.breach` | The outside has got in <br>**Needs:** a hole in a wall. Our own concept | no art | M2 |
| **Unsupported span** | `ui.alert.unsupported` | This will collapse <br>**Needs:** an unsupported span about to fall. Central to the layer model | no art | M2 |
| **Collapse** | `ui.alert.collapse` | Something has already come down <br>**Needs:** a collapse that has happened | no art | M2 |
| **Too cold** | `ui.alert.cold` | A room is below tolerance | sheet 06 (action tiles), high | M2 |
| **Too hot** | `ui.alert.heat` | A room is above tolerance | sheet 06 (action tiles), med | M2 |
| **Idle colonists** | `ui.alert.idle` | Nothing assigned that they can do | sheet 06 (action tiles), med | M2 |
| **No medicine** | `ui.alert.nomedicine` | Nothing to tend with | sheet 08 (salvage gear), low | M2 |
| **No bed** | `ui.alert.nobed` | Someone has nowhere to sleep | sheet 08 (salvage gear), low | M2 |
| **No food** | `ui.alert.nofood` | The stores are empty | sheet 02 (food), low | M2 |
| **Fire** | `ui.alert.fire` | Burning now | sheet 06 (action tiles), high | M2 |
| **Injured** | `ui.alert.injured` | Someone needs treatment | sheet 06 (action tiles), med | M2 |
| **Disease** | `ui.alert.disease` | An illness has taken hold | sheet 07 (anatomy), low | M2 |
| **Infection** | `ui.alert.infection` | A wound has gone bad | sheet 07 (anatomy), low | M2 |
| **Mental break** | `ui.alert.mentalbreak` | A colonist has lost control <br>**Needs:** a mental break. Blocked on the missing face art | no art | M2 |
| **Prisoner escaping** | `ui.alert.prisonerescape` | One is getting out <br>**Needs:** a prisoner getting out | no art | M2 |
| **Raid** | `ui.alert.raid` | Hostiles are here | sheet 06 (action tiles), med | M2 |
| **Drop from above** | `ui.alert.drop` | Something came in through open sky <br>**Needs:** something arriving through open sky. Our own concept, and verticality-specific | no art | M2 |
| **Power failure** | `ui.alert.powerloss` | A net has gone dark | sheet 08 (salvage gear), med | M2 |
| **Out of fuel** | `ui.alert.nofuel` | A generator is empty and its net wants power | sheet 08 (salvage gear), med | M3 |
| **Spoiling** | `ui.alert.spoilage` | Food is going off | sheet 02 (food), low | M2 |
| **No light** | `ui.alert.darkness` | Work is slowed for want of a lamp | sheet 06 (action tiles), low | M2 |
| **Trapped** | `ui.alert.trapped` | Someone cannot reach the colony <br>**Needs:** a colonist cut off from the colony | no art | M2 |
| **Store cannot be emptied** | `ui.alert.storagestuck` | A shelf is marked for removal and there is nowhere to put what is in it | no art | M3 |
| **No bed for the wounded** | `ui.alert.norescuebed` | A downed colonist has no free bed to be carried to, so nobody can rescue her | no art | M3 |

## Bulletins

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Salvage found** | `ui.bulletin.salvage` | Something worthwhile in the rubble | sheet 04 (manufactured), med | M2 |
| **Scrap drop** | `ui.bulletin.scrapdrop` | Scrap metal fell from the sky <br>**Needs:** scrap metal falling from the sky. Our own concept | no art | M3 |
| **Arrival** | `ui.bulletin.arrival` | Someone new is here <br>**Needs:** someone arriving. Blocked on the missing human figure | no art | M2 |
| **Trader** | `ui.bulletin.trader` | A trader has come to us | sheet 08 (salvage gear), med | M2 |
| **Caravan** | `ui.bulletin.caravan` | Ours has arrived or returned | sheet 08 (salvage gear), med | M2 |
| **Raid warning** | `ui.bulletin.raidincoming` | Hostiles are on the way | sheet 06 (action tiles), med | M2 |
| **Weather** | `ui.bulletin.weather` | The conditions have turned | sheet 06 (action tiles), med | M2 |
| **Death** | `ui.bulletin.death` | We have lost someone | sheet 07 (anatomy), high | M2 |
| **Recruited** | `ui.bulletin.recruited` | A prisoner has joined us <br>**Needs:** a prisoner joining us | no art | M2 |
| **Research complete** | `ui.bulletin.research` | A project has finished | sheet 06 (action tiles), high | M2 |
| **Opportunity** | `ui.bulletin.quest` | An offer with a deadline | sheet 08 (salvage gear), low | M2 |
| **Wanderer** | `ui.bulletin.wanderer` | Someone wants to join <br>**Needs:** someone asking to join | no art | M2 |
| **Refugee** | `ui.bulletin.refugee` | Someone is asking for shelter <br>**Needs:** someone asking for shelter | no art | M2 |
| **Animal joined** | `ui.bulletin.animaljoin` | A tame beast has attached itself to us | sheet 06 (action tiles), med | M2 |
| **Crash** | `ui.bulletin.crash` | Something has come down nearby <br>**Needs:** a crashed ship or drop pod | no art | M2 |
| **Supply drop** | `ui.bulletin.supplydrop` | Something has fallen from the sky. Fetch it before the weather does <br>**Needs:** a stack falling from the sky. Our own concept | no art | M3 |
| **Birth** | `ui.bulletin.birth` | A tame animal has given birth <br>**Needs:** a newborn animal | no art | M2 |
| **Theft** | `ui.bulletin.theft` | A bandit carried something off the board <br>**Needs:** a figure carrying a sack off the edge. Our own concept | no art | CB |
| **Bandit left** | `ui.bulletin.banditleft` | A bandit walked off the board with nothing <br>**Needs:** a figure walking off the edge. Our own concept | no art | CB |
| **Mental break** | `ui.bulletin.mentalbreak` | A colonist lost control. The row names the break, and a click goes to where it began | no art | TM |

## Toasts

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **{name} has reached {skill} {level}** | `ui.toast.skillup` | A colonist's skill has gone up a level | no art | M2 |

---

Generated from `docs/design/icon-keys.csv` and `docs/design/icon-map.csv`. To change a name or a description, edit `icon-keys.csv` and rerun `python3 tools/wiki/build_wiki.py`.
