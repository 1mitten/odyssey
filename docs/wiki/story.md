# Storytellers and difficulty

Who decides when things happen to the colony, how hard the threats hit, and the tension gauge that shows how hard the storyteller is pressing. A storyteller's description is the blurb on its New game card: the screen reads this column. Nothing reads the choice until the storyteller itself is built (design 59).

35 entries, 35 without art. Names are what the player sees; the key beside each is the stable identifier — cite it when proposing a change.

## Storytellers

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Jacob** | `ui.storyteller.jacob` | A llama who keeps the calendar. Trouble arrives on schedule, and he spits between visits. | no art | SY |
| **Trent** | `ui.storyteller.trent` | Half man, half salvage. His implants misfire at random, and so does your luck. | no art | SY |
| **Kano** | `ui.storyteller.kano` | A vast, sleepy pig. Long quiet seasons to build in, then one day he gets up. | no art | SY |
| **Jacob's portrait** | `ui.storyteller.portrait.jacob` | A llama keeping a calendar. Drawn as a square wave until the portrait is commissioned <br>**Needs:** a llama holding a calendar. Commissioned portrait art, not a sheet icon | no art | SY |
| **Trent's portrait** | `ui.storyteller.portrait.trent` | A man half rebuilt from salvage. Drawn as a jagged line until the portrait is commissioned <br>**Needs:** a man half rebuilt from salvage. Commissioned portrait art, not a sheet icon | no art | SY |
| **Kano's portrait** | `ui.storyteller.portrait.kano` | A vast pig asleep. Drawn as a sun on the horizon until the portrait is commissioned <br>**Needs:** a vast sleeping pig. Commissioned portrait art, not a sheet icon | no art | SY |
| **Storyteller** | `ui.storyteller.heading` | Who decides when things happen to the colony, and how hard the threats hit | no art | SY |

## Difficulty

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Difficulty** | `ui.difficulty.heading` | How hard threats hit, and how forgiving the game is after a disaster | no art | SY |
| **Peaceful** | `ui.difficulty.peaceful` | No big threats at all. The colony is left to build | no art | SY |
| **Gentle** | `ui.difficulty.gentle` | Small threats, and a loss eases the next ones a great deal | no art | SY |
| **Easy** | `ui.difficulty.easy` | Lighter threats, and a forgiving recovery after a loss | no art | SY |
| **Normal** | `ui.difficulty.normal` | The game as it is tuned | no art | SY |
| **Hard** | `ui.difficulty.hard` | Heavier threats, and a loss is remembered for longer | no art | SY |
| **Brutal** | `ui.difficulty.brutal` | The heaviest threats, and little mercy after a loss | no art | SY |
| **Custom** | `ui.difficulty.custom` | Your own numbers for all four levers | no art | SY |
| **Threat scale** | `ui.difficulty.threatscale` | How hard threats hit, as a share of Normal | no art | SY |
| **Big threats** | `ui.difficulty.bigthreats` | Whether raids and other big threats come at all | no art | SY |
| **Adaptation** | `ui.difficulty.adaptation` | How strongly a loss eases the next threats, and how fast quiet days build them back | no art | SY |
| **Grace** | `ui.difficulty.grace` | How long before the first big threat, against the storyteller's own | no art | SY |
| **Set by {rung}. Pick Custom to change.** | `ui.difficulty.setby` | Why the four levers cannot be moved on any rung but Custom | no art | SY |
| **Your own settings** | `ui.difficulty.own` | The four levers are yours to move | no art | SY |

## Tension

| Name | Key | What it is | Art | Milestone |
|---|---|---|---|---|
| **Tension** | `ui.tension.gauge` | How hard the storyteller is pressing. A loss eases it; quiet days build it back | no art | SY |
| **Reeling** | `ui.tension.band.0` | Just hit. The next threats will be small | no art | SY |
| **Easing** | `ui.tension.band.1` | Recovering. Threats are still lighter than usual | no art | SY |
| **Even** | `ui.tension.band.2` | Neither easing nor building | no art | SY |
| **Building** | `ui.tension.band.3` | The colony is doing well, and threats are growing to match | no art | SY |
| **Peak** | `ui.tension.band.4` | As hard as the storyteller presses | no art | SY |
| **A colonist died, {ago}** | `ui.tension.cause.died` | What last moved the gauge: a death | no art | SY |
| **A colonist was downed, {ago}** | `ui.tension.cause.downed` | What last moved the gauge: a colonist downed | no art | SY |
| **{n} quiet days** | `ui.tension.cause.quiet` | What last moved the gauge: days with no loss | no art | SY |
| **One quiet day** | `ui.tension.cause.quietone` | What last moved the gauge: a day with no loss | no art | SY |
| **Nothing has moved it yet** | `ui.tension.cause.none` | The gauge has not moved since the colony began | no art | SY |
| **today** | `ui.tension.ago.today` | A cause from the same day | no art | SY |
| **1 day ago** | `ui.tension.ago.day` | A cause from the day before | no art | SY |
| **{n} days ago** | `ui.tension.ago.days` | A cause from some days before | no art | SY |

---

Generated from `docs/design/icon-keys.csv` and `docs/design/icon-map.csv`. To change a name or a description, edit `icon-keys.csv` and rerun `python3 tools/wiki/build_wiki.py`.
