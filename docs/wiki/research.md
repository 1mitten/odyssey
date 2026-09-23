# Research

What the colony can learn, grouped by field, and the words the Research tab uses about it. A project's description here is the body of its detail pane: the screen reads this column, so correcting a line here corrects the game. Only Power is listed yet, and the projects are placeholders until the research mechanism exists (design 34).

26 entries, 26 without art. Names are what the player sees; the key beside each is the stable identifier — cite it when proposing a change.

## Fields

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Power** | `ui.research.category.power` | Making current, carrying it and storing it | no art | RS |

## Projects

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Wiring** | `ui.research.project.wiring` | Running current from where it is made to where it is wanted, along conduit. Everything electrical starts here. | no art | RS |
| **Generators** | `ui.research.project.generators` | A burner that turns fuel into current. Loud and hungry, and the first power a colony can count on. | no art | RS |
| **Electric light** | `ui.research.project.lighting` | Light that does not burn. A room stays usable after dark, at a steady draw on its net. | no art | RS |
| **Batteries** | `ui.research.project.batteries` | Charge put by against the hours a generator stands idle or a panel sees no sun. | no art | RS |
| **Solar arrays** | `ui.research.project.solar` | Free current by day and none at night, which is why it wants batteries behind it. | no art | RS |

## Project states

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Researching** | `ui.research.status.researching` | Being worked on now | no art | RS |
| **Available** | `ui.research.status.available` | Everything it needs is done | no art | RS |
| **Done** | `ui.research.status.done` | Finished; what it unlocks is the colony's | no art | RS |
| **Locked** | `ui.research.status.locked` | Something it needs is not done yet | no art | RS |

## The Research tab's words

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Current** | `ui.research.hud.now` | The label of the strip that says what is being researched. Not Now, which the HUD reads as a placeholder | no art | RS |
| **then {list}** | `ui.research.hud.then` | What the queue does next, after the project in hand | no art | RS |
| **Nothing being researched. Pick a project.** | `ui.research.hud.idle` | The Now strip when no project is in hand | no art | RS |
| **Project** | `ui.research.hud.project` | Column heading | no art | RS |
| **Status** | `ui.research.hud.status` | Column heading | no art | RS |
| **Cost** | `ui.research.hud.cost` | Column heading | no art | RS |
| **{category}, cost {cost}** | `ui.research.hud.meta` | The line under a selected project's name | no art | RS |
| **Needs** | `ui.research.hud.needs` | The projects that must be done first | no art | RS |
| **Unlocks** | `ui.research.hud.unlocks` | What finishing the project gives the colony | no art | RS |
| **Leads to** | `ui.research.hud.leadsto` | The projects this one opens up | no art | RS |
| **None** | `ui.research.hud.none` | An empty list in the facts | no art | RS |
| **Needs {project} first.** | `ui.research.hud.needsfirst` | Why a locked project cannot be started | no art | RS |
| **Research** | `ui.research.hud.research` | Start this project now | no art | RS |
| **Queue** | `ui.research.hud.queue` | Research this project after the one in hand | no art | RS |
| **Unqueue** | `ui.research.hud.unqueue` | Take this project back out of the queue | no art | RS |
| **Pause** | `ui.research.hud.pause` | Stop researching, keeping the progress | no art | RS |

---

Generated from `docs/design/icon-keys.csv` and `docs/design/icon-map.csv`. To change a name or a description, edit `icon-keys.csv` and rerun `python3 tools/wiki/build_wiki.py`.
