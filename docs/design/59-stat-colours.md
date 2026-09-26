# 59. How good a number is: one scale, one place

**Status: built 2026-09-26, not yet played.** Branch `claude/vigilant-bardeen-8idplc` (PR #231).

## 1. The ask

The owner, on the Gear tab, 2026-09-26:

> could you add some color to the gear numbers. For example 0% armour should be red — and then use
> appropriate colours depending (it helps understand stats more). Include this to temperature
> but — good temperature is green, amber if questionable and more towards red if not good. Keep
> rain a neutral bright white

and then, while it was being built:

> Could you apply this everywhere so we have consistency? … and use a central point to
> configure/style all of this

## 2. What was there

Three surfaces already coloured a number by how good it was, and each had its own thresholds:

| Surface | Rule | Where it lived |
|---|---|---|
| Need bars | stepped: green from 60 %, amber from 40 %, red below | `HudTokens.NeedBand` (Presentation) |
| Health bar over a head, the pane's bar, the roster card, the Health tab | the need bars' steps, in deeper inks | `CombatFeedbackModel.HealthBarColour` |
| Health tab pain and blood loss | stepped, lower is better, 300/600 and 150/450 | `HealthTab.Lost` |
| A tile's temperature | **a hue, not a verdict**: blue under 10 °C, amber over 30, red over 35 | `HudTheme.Temperature` |
| Generator fuel | amber under half, otherwise uncoloured | inline in `InspectModel` |

The rest — Gear's armour and warmth, pace, a shot's chance to hit, the clock's outdoor reading —
were plain text.

## 3. The rule

**`Odyssey.Hud.StatInks` is the one place a number's colour is decided.** It holds:

- **the scales**: one `StatScale` per judged figure, three anchors in the figure's own units — the
  value that is fully bad, the one that is questionable, the one that is fully good. A scale may
  run downwards (pain is good low).
- **the ramp**: a score in per mille — 0 red, 500 amber, 1000 green — turned into a colour.
- **the style**: `StatInks.Blend` (true: the colour blends along the ramp, so a figure getting
  worse gets redder; false: three bands, the nearest anchor's colour) and **two palettes**: *Panel*,
  the interface's stat tokens, and *World*, the same hues deepened for bars drawn over the lit
  board (the owner's 2026-09-23 ask, design 33 §8a, now owned here).

A surface asks `StatInks.Ink(scale, value)` and never writes a threshold or picks a colour.
To retune a threshold, change its scale; to restyle the lot, change the palette or the switch.

| Scale | Bad | Questionable | Good | Units |
|---|---|---|---|---|
| Need | 20 % | 40 % | 60 % | thousandths full |
| Health (a pool or a region left) | 20 % | 40 % | 60 % | per mille |
| Capacity (consciousness, moving, manipulation) | 20 % | 40 % | 60 % | per mille |
| Pain | 60 % | 30 % | 10 % | per mille, lower is better |
| Blood loss | 45 % | 15 % | 5 % | per mille, lower is better |
| Armour | 0 % | 20 % | 40 % | per cent |
| Fuel | empty | half | full | per mille of the hopper |
| Pace | 50 % | 80 % | 100 % | per mille of the standard walk |
| Chance to hit | 20 % | 50 % | 80 % | per mille |
| Temperature | 12 °C outside | 6 °C outside | inside | the comfortable range |

**A temperature is judged against a comfortable range** (`StatInks.Comfort`): green inside it,
amber 6 °C outside, red 12 °C outside, on either side. On its own — a tile, the clock — the range is
the bare colonist's, 16 to 26 °C, so it reads amber at 10 and 32 °C and red at 4 and 38 °C.
**On the Gear tab the range is what she wears and the temperature is the outdoor reading**: a coat
good to 4 °C makes 4 °C outside green, and the bare jumpsuit at 4 °C is red. That makes a tile's
temperature and the Gear tab's warmth the same judgement, and a colonist in the bare jumpsuit reads
exactly what the clock reads.

## 4. What is not judged, and why

- **Rain protection** — the owner: *"a neutral bright white"* (`HudTheme.TextPrimary`).
- **The kit's "used of capacity"** — a count, not a quality.
- **Skill levels** — their own ramp (`WorkBands`) is a rank, not a verdict.
- **Quality tiers** — a name with its own colours (`HudTheme.Quality`).
- **States** — power live or dark, an alert's severity, a combat word. They are not numbers.

## 5. What changed on screen

- **Every judged figure blends** where the old ones stepped: a need at 50 % is between amber and
  green rather than amber. `StatInks.Blend = false` puts the steps back, for all of them at once.
- **Temperature lost its blue.** Cold was blue and heat red; now both are "not good", in the same
  red. This reverses design 28 §8's colour, at the owner's word.
- **The bare colonist's Gear line is no longer dim**: 0 % armour is red, warmth is judged against
  the weather, rain and the kit are bright white.
- **New colour** on the pace line, the shot readout (red when out of range or sight), the clock's
  outdoor reading and a generator's fuel line at every level.

## 6. Tests

`StatInksTests` (fast tier): a scale's arithmetic both ways, the ramp's ends and middle in both
palettes, the one switch, the owner's three rulings (0 % armour red; temperature green, amber, red;
rain and the kit never judged), warmth against the weather, and the health bar reading the table.
The health, roster and pane tests that asserted a band now ask the table for the expected ink, so
retuning a scale moves no test but `StatInksTests`.

## 7. Not proven

Whether the blend reads better than the bands, and whether the red-green ramp is readable to a
colour-blind player. The value is always written beside the colour, so nothing is carried by colour
alone, but the storage categories were re-tuned for dichromacy (design 35 §5a) and these were not.
