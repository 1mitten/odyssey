# Reference screenshots

Purpose (brief §8): information-design analysis only. What each image communicates, how density is managed, which overlays and panels exist. Nothing here is traced or copied; these inform our own UI and camera decisions.

## Storage

Images pasted into chat reach the assistant as pixels, not files, so they could not be committed from the remote session. Please copy each image into `concept/` under the names below (the folder exists with a `.gitkeep`) so the next session can open them. Until then, the descriptions here are the record.

| File (please add) | Supplied | Source |
|---|---|---|
| `concept/01-compound-day.png` | 2026-09-15, session `cool-gates` | Concept render supplied by the owner (Synty-style low-poly sci-fi colony, daytime) |
| `concept/02-composite-night.png` | 2026-09-15, session `cool-gates` | Concept composite of four views (combat, trade, research, hospital), night |

These are concept images of what the game may look like, not captures of an existing game. They show a RimWorld-derived HUD grammar over a Synty-style 3D scene, which is the target look.

## 01 — Compound by day

**Scene.** Three-quarter perspective camera, mid-height, looking across a walled compound in desert outskirts: modular grey sci-fi buildings with cyan emissive trim, a crashed spacecraft behind the wall, city towers on the horizon. Inside the wall: a colonist at a console in an open workshop bay, a colonist installing a solar panel, a kitchen bay with a colonist carrying a plate, six raised growing beds, a cluster of batteries and generators, turrets on the wall corners.

**What the layout communicates.**
- Interiors are readable because every building is shown *roofless or cut open*. This is the cut-away question in one picture: the target look assumes the player sees inside occupied rooms at the current layer.
- Everything is on one level. No stairs, no second storey, no below-ground. The concept does not yet show how "above" and "below" are signalled, which is exactly what our UI has to add (layer indicator, ghosted upper floors, a slice line).
- The desert-outskirts setting matches the *later* map type, not the ruined-city prototype. Noted so the art direction and the prototype map are not confused.

**HUD inventory.**
- Top-left: resource ledger, icon plus number, about nine rows, stacked vertically. Dense but scannable; no labels, icons carry the meaning.
- Top-centre: colonist bar, seven portraits with a name under each and a small status glyph. Identical in role to RimWorld's colonist bar.
- Top-right: single settings gear.
- Left edge: vertical toolbar of about seven square icon buttons (build, tools, combat, targeting, defence, factions, people). Reads as the architect/orders menu.
- Bottom-centre: a second row of six colonist cards, each with a job or weapon glyph. Duplicates the top bar; one of the two is redundant. Our design must choose one and give the other slot to layer navigation. **Settled 2026-09-15:** the top bar survives with a layer badge added per card, the bottom cards are dropped, and the Depth Ruler takes the right edge below the alert stack. Bottom-centre goes to the main tab bar.
- Bottom-right: a large red X (cancel/close). Prominent enough to double as "clear designation".
- Missing from the concept: time and speed controls, date and weather, temperature, the layer or depth control, alerts (present in image 02), zoom or minimap.

## 02 — Four night-time views

Same HUD grammar in all four quadrants, at night, with a right-edge stack of red alert boxes (attack, pirates, disease and similar) and one modal panel per quadrant.

1. **Top-left, combat.** Raiders at the wall, tracer fire, a contextual "Combat" popup listing pawn controls, firing arcs and similar. Shows that combat orders are a mode with its own sub-menu, not just buttons on the left toolbar.
2. **Top-right, caravan and trade.** A landed shuttle with pawns carrying crates; a "Caravan & Trade" two-column table (pawn inventory against trader inventory, item, quantity, value). Standard split-ledger trade UI.
3. **Bottom-left, research.** Workshop interior with benches; a "Research Tree" panel with tabs and a node graph of about a dozen nodes with edges. The node graph is small and legible because it is windowed, not full-screen.
4. **Bottom-right, hospital and social.** Beds with patients and a doctor, a table where colonists are gathered; a "Social/Relationships" panel at bottom-left with bars for opinion and mood.

**Information-design notes.**
- Alerts stack on the right edge, newest at top, in a red that reads at a glance even against the night scene. Keep this, but our alerts must say *which layer* the event is on and offer a jump. **Adopted 2026-09-15** as a mandatory rule: every alert and bulletin carries its layer and a jump target, and severity is encoded in colour *and* shape *and* stack position so it does not depend on colour alone.
- Emissive cyan trim keeps the modular buildings readable at night with no extra lighting UI. This is a material-level decision (emissive channel on the Synty materials) that Lane E should confirm is available.
- Every panel is a mid-sized floating window over the scene rather than a full-screen takeover; the world stays visible. Good default for our inspector, bills and research panels.
- All four views are single-layer, roofless cut-aways. The composite never shows two storeys at once. Whether to render the storey above as a ghost outline or hide it entirely was a Lane B question (Going Medieval and Timberborn take different positions), and it is now **resolved**: x-ray by default, with six modes and two further axes shipped for playtest, per `docs/adr/0006-layer-visibility-policy.md`. How it resolved is worth recording, because these images turned out to be evidence rather than a gap: they are the clearest statement of one mode, `roofs-off`, where geometry above stays solid but roofs and floors are dropped. That mode was absent from the original three candidates and joined the set only once the renders were re-read for what they say about layering rather than about the HUD.

## What these change in the plan

- Confirms the target look is Synty POLYGON Sci-Fi City plus a RimWorld-style HUD; no new asset requirement.
- Adds a UI requirement to the design phase: a layer control and a depth cue that the concept omits. **Resolved 2026-09-15** in `docs/design/10-ui-panel-catalogue.md` as region A11, the Depth Ruler, and in `docs/design/09-ui-and-input.md` as `SliceDirector`. `06-rendering-and-camera.md` keeps the camera and rendering half; the HUD half moved to `09` and `10`, which the Phase 3 manifest had no slot for.
- Flags that the concept shows the outskirts map, not the ruined city; the prototype map stays the ruined city as agreed.

## Station to Station — the lighting reference (added 2026-09-16)

Six press and store screenshots of *Station to Station* (Galaxy Grove, 2023), in
`station-to-station/`, supplied by the owner as the target for the golden-hour look
(`docs/research/look-interview.md`). Reference only, for lighting and post-processing: the voxel
art, the buildings and the trains are not our subject and nothing is traced or copied. Every image
shares a low sun of roughly 25–35°, long soft shadows, lifted cool shadow tones, bloom on lit faces,
a warm amber haze that rises to a horizon the same colour as the sky, and a horizontal band of
focus with blur above and below.

| File | What it shows about the lighting |
|---|---|
| `s2s-01-village-golden.jpg` | The fullest statement. Sun low from the upper left, warm on every roof and tree crown, long shadows lying across the grass without hiding it. Pale rock terraces at the top bloom into the haze. Focus band through the village; the near roofs and the far terraces soften. |
| `s2s-02-locomotive-tiltshift.jpg` | Low camera, the sky in frame: pale warm blue fading to near-white at the horizon, matching the fog. Tilt-shift is strongest here — the foreground barn and the far hill are both blurred, the locomotive sharp. Smoke reads as a lit volume. |
| `s2s-03-amber-haze.jpg` | The whole frame is amber. Fog is dense enough that the far town is a silhouette and the sky is the fog colour; the near ground still reads. Proof that the haze is a mood, not a limit of view. |
| `s2s-04-light-shafts.jpg` | In-game HUD shot with the sun near the top of frame: crisp radial light shafts through the tree line, a bright bloomed sky, and the ground under the shafts still readable. The shafts are screen-space streaks radiating from the sun's position. |
| `s2s-05-cathedral-overview.jpg` | Higher pitch, closer to our default view. Warmth arrives through the grading and the lit faces alone, with the horizon only a pale band at the very top. Shows what the default 48° view must achieve without the sky. |
| `s2s-06-desert-tiltshift.jpg` | Different palette, same rig: the focus band is narrow and everything else is blurred, which is more than we want in play but shows the range. The sky again goes pale warm at the horizon. |

**What these change in the plan.** The sun comes down from 72° to the raking angle, bloom is
adopted, fog moves onto the board as a warm haze that agrees with the sky's horizon, a light-shaft
pass is written, and a subtle always-on tilt-shift is added. All of it is post-processing and light
rig; none of it is a cell, a save field or a hash.
