# B — Station to Station: how the look is composed

**Question.** How does *Station to Station* (Galaxy Grove, published by Prismatika, 2023) compose its
look — the warm golden-hour lighting, the light shafts, the tilt-shift/miniature blur, the hazy
horizon — and what do public sources say about how it was made?

**Method note.** Eight web searches and eight page reads, the hard cap. The headline result is
negative and worth stating first: **there is no public technical account of this game's rendering.**
No Unity case study, no Made With Unity feature, no GDC or Unite talk, no art-of postmortem, no
80.lv art interview, no Reddit dev AMA on the subject. The one place such a thing would normally
live — Joost van Dongen's long-running technical dev blog, which has over a hundred deep articles
and labels for "graphics" (77 posts) and "lighting" (7 posts) — contains **no Station to Station
rendering post at all**; the game appears there only in business-facing posts ("A big step forward",
"Tips for pitching to game publishers"). The interviews that exist are design and business
interviews. So findings 3 to 6 below are, in large part, **read off the marketing shots and the
published vocabulary, and are inference, not sourcing.** Every inference is labelled.

## Findings

### 1. Engine and render pipeline

**Could not be determined, and the likeliest guess is not Unity.** No source names an engine for
*Station to Station*. Steam lists only DirectX 11 minimum / DirectX 12 recommended, Windows 10
64-bit, quad-core, 8 GB, 7 GB install — which discriminates nothing. There is no Unity case study
and no "Made with Unity" feature.

Two pieces of circumstantial evidence point **away** from Unity rather than towards it:

- Galaxy Grove's own site advertises internships in "3D art, animation, technical game design and
  **C++ programming**", and a search of their recruitment history surfaces **Unreal Engine
  programmer** positions. Neither is dated to *Station to Station*, and the studio's later titles
  (*Town to City*, *Steam to Electric*) may account for both.
- Joost van Dongen's background is Ronimo Games, where he was technical director on an **in-house
  C++ engine**. A custom or heavily-modified renderer is within this studio's habits in a way it is
  not for most indies.

Treat "Station to Station is a Unity game" as **unverified**. Nothing in this file depends on it:
the look is reproducible in URP regardless of what they used.

### 2. The voxel pipeline, insofar as it explains the lighting

No source names MagicaVoxel, Qubicle, or any authoring tool, and no source describes the mesh
pipeline. What *is* sourced is the **design intent behind the voxel choice**, and it is more useful
than the tool would have been. Van Dongen: the game is styled after a **physical diorama**, and
specifically after the **model railway at the Efteling theme park** — "we really tried to get to the
vibe of that with this." The pitch line used by the publisher is a **"tilt shift-inspired, voxel-art
world"**; tilt-shift is named in the marketing copy, so the miniature reading is deliberate and
stated, not a critic's invention.

The lighting consequence of voxels, as evident in the shots (**inference**): every surface is an
axis-aligned quad, so there are exactly three normals in the whole scene above ground. That means
the sun alone produces three flat, perfectly uniform bands of value — top, and two sides. All the
visual interest therefore has to come from (a) the difference between those three bands being large,
(b) contact darkening where blocks meet, and (c) coloured bounce filling the shaded faces. A
low-poly Synty scene has far more normal variety than this and gets that interest for free; see the
recommendation.

### 3. Lighting

**Not sourced. Read off the marketing and store shots (inference).** What the shots show
consistently:

- A **low sun**, in the region of 15–25° elevation, warm and strongly saturated — amber rather than
  white. Shadows are long, typically two to four times the height of the object casting them.
- **Hard-edged shadows with a soft tail**: the contact end reads crisp, consistent with a directional
  light of small angular diameter, not a large-radius soft shadow.
- **A heavily lifted shadow, tinted blue-violet**, so shaded faces are never near black. The
  sun/shade contrast is large in *hue* and only moderate in *value*. This is what stops long shadows
  destroying readability, and it is the single most transferable number in this file.
- **No visible time-of-day cycle.** Every shot across announcement, launch and review coverage sits
  at the same warm low hour, and no source describes a day/night cycle. The working assumption is a
  **fixed, art-directed hour per biome**, not a simulated sun.
- Environments *change* over play — the stated design is transformation from "gray and dull" to
  "a vibrant, lush environment full of life and colour" as the network grows — but that is a change
  of **content and saturation**, not of sun position.

### 4. Post-processing

**Not sourced; evident in the shots (inference).** The stack that reproduces what is visible:

- **Tilt-shift blur.** The critical question — real depth of field or a screen-space band — is not
  answered by any source, but the shots answer it by behaviour: the blur in *Station to Station*
  shots tracks a **horizontal band across the screen**, blurring the top and bottom of the frame,
  including objects at the *same* camera distance as sharp ones near the centre. That is a
  **screen-space band**, i.e. classic tilt-shift faking, not a physically-focused depth of field.
  It is also the cheaper and better-behaved choice for a fixed-pitch strategy camera: a real DoF on
  an isometric-ish view blurs by distance, which in a tilted view is very nearly the same as
  vertical screen position anyway, but costs depth reads and flickers on thin geometry.
- **Bloom**, moderate, biting mainly on the sunlit tops and on water.
- **Vignette**, mild, reinforcing the lens-and-miniature reading.
- **Colour grading**, warm and high-saturation, with the shadow end pushed cool — the amber/violet
  split that does the work in finding 3.
- **Film grain**: not evident. The image reads clean.
- **Hazy horizon**: a strong, warm, height- or distance-based fog that closes the scene off well
  before the geometry ends. Combined with the blur band it is what makes the world read as a model on
  a table rather than a landscape.

### 5. Light shafts

**Nothing sourced at all.** No developer statement on volumetric lighting versus screen-space god
rays, and no store asset named in any credit list or dev post that a public search reaches. From the
shots (**inference**): the shafts are thin, they radiate from the sun's screen position, and they are
occluded by the terrain silhouette — consistent with a **screen-space radial blur / light-shaft
post-effect** driven from the sun's on-screen position, which is the cheap classic. A true froxel
volumetric would be evident from shafts that respond to geometry *behind* the camera plane and from
soft depth interaction, and the shots do not settle that either way. **Low confidence.**

### 6. Readability under long shadows

This is the one place where a **direct developer statement** exists, and it is about silhouette
rather than lighting. Van Dongen on building design: "Is it easy to read from a distance? Can you
easily distinguish these buildings from each other?" — and the team "axed some excellent designs …
because they read badly." Readability is treated as a **veto on content**, applied before art
quality: a beautiful building that does not read is cut.

The lighting half is inference: the lifted, hue-shifted shadow described in finding 3 is what keeps
a long shadow from being a hole in the image. A shadow in these shots is a *colour* change far more
than a *brightness* change, so track, terrain and buildings keep their value separation inside
shadow. No developer comment addresses this directly.

### 7. Reception of the look

Universally the most-praised thing about the game, and consistently praised in miniature terms.
A representative review: the game has "a unique tilt-shift perspective that adds a stylish flair"
and is "a contender for the most aesthetically pleasing puzzler of the year", with a "voxel-based,
brightly coloured world" that is "inviting and perpetually charming." Announcement coverage led on
the look ("Gorgeous voxel-style railroad sim"). The words that recur across press and players are
**cozy, charming, relaxing, diorama, toy-like** — the miniature reading lands, and it is the reason
people describe a logistics puzzle as a comfort object. The game sits around 90% positive on Steam,
a figure the developer himself quotes.

## Recommendation

What transfers to a low-poly Synty scene in URP, in descending order of value per hour:

1. **Fix the hour; do not simulate the sun.** A single art-directed low warm sun per biome is what
   makes every shot of this game look like the same game. Odyssey's board already has one directional
   light; pin it at roughly 20° elevation with a warm colour and stop there. This is nearly free and
   is the largest single change to the impression the board gives. It also *contradicts* the current
   72° sun cited in the ground-relief work — and that measurement is itself the argument: a 1.7°
   slope moved the lit value under one per cent against a 72° sun. At 20° the same slope is legible.
   Relief and a low sun are the same feature; neither pays off without the other.
2. **Lift and hue-shift the shadow rather than darkening it.** Warm sun, cool ambient, small value
   gap, large hue gap. This is the mechanism that lets long shadows exist without eating the board,
   and it is the prerequisite for item 1 not wrecking readability. In URP this is the ambient colour
   and a shadow-tint in the grade, not a new pass.
3. **Tilt-shift as a screen-space band, not a depth of field.** For a fixed-pitch strategy camera the
   band is cheaper, more stable on thin geometry, and easier to tune because the tuning parameter is
   in screen space where the player's attention already is. Odyssey has a 5 ms budget and the board
   currently runs 0.99 ms; a band blur is affordable where a full DoF would want justification.
4. **Close the horizon with warm fog.** Odyssey already has a decorative surround and fog opaque at
   1,100 m; warming the fog to the sun's colour turns "the board runs out" into "the board is a model
   on a table", which is exactly what the surround was built to buy.
5. **Readability as a veto.** The one hard developer rule in this file, and it is free: a building
   that does not read at play distance is cut regardless of how good it is. Worth writing into the
   build line's acceptance criteria before there are buildings to argue about.

What does **not** transfer:

- **The voxel lighting logic.** Three axis-aligned normals is a constraint Synty does not have. Do
  not chase per-face flat shading or voxel-style contact AO; Synty meshes already carry normal
  variety and baked colour, and flattening them fights the asset pack. The *palette* discipline
  transfers, the *shading model* does not.
- **The miniature framing wholesale.** *Station to Station* is a puzzle about a landscape seen from
  outside. Odyssey is a colony sim where the player inspects a colonist's needs and mood; a strong
  blur band on the top and bottom of the frame will blur exactly the thing being inspected. Adopt the
  band weakly, and consider fading it out while an inspect pane is open.
- **God rays.** Unsourced even for them, and they cost more than they return on a top-down board
  where the sun is rarely in frame — at the default 48° pitch the horizon is not in frame at all.
  Skip until the camera can pitch low.
- **The engine question.** Do not cite this game as Unity evidence for anything. It may not be.

Cheapest experiment that decides most of this: **one screenshot pair of the existing wooded meadow at
72° and at 20° sun with a warm/cool split**, taken by the existing `ReliefCheck` harness, which
already shoots at three pitches. It costs one constant and settles items 1 and 2 by eye, which is the
only way they can be settled.

## Sources

https://store.steampowered.com/app/2272400/Station_to_Station/
https://www.escapistmagazine.com/station-to-station-interview/
https://joostdevblog.blogspot.com/
https://joostdevblog.blogspot.com/2024/12/a-big-step-forward.html
https://joostdevblog.blogspot.com/2024/08/tips-for-pitching-to-game-publishers.html
https://dutchgamesassociation.nl/2023/11/23/galaxy-grove/
https://www.galaxy-grove.com/
https://www.workwithindies.com/work-with/galaxy-grove
https://80.lv/articles/craft-a-cozy-mediterranean-town-in-this-upcoming-voxel-city-builder
https://gamingpizza.com/2024/01/station-to-station-review/
https://prismatika.games/game/station-to-station/
https://www.gamingnexus.com/News/62178/Gorgeous-voxel-style-railroad-sim-Station-to-Station-announced/
https://www.digitallydownloaded.net/2023/08/voxel-art-railway-sim-station-to-station-launches-this-october.html
https://gaymingmag.com/2023/10/station-to-station-review-all-aboard-the-hype-train/
https://www.pcgamingwiki.com/wiki/Station_to_Station (HTTP 403, not read)

## Confidence

**Low on everything technical; medium on design intent and reception.** The technical findings (3, 4,
5 and the lighting half of 6) are read off marketing images and standard practice, not off any
developer statement, because no such statement exists in public. The diorama/model-railway intent,
the readability veto, the "tilt shift-inspired" marketing framing and the reception are directly
quoted and are solid.

## Could not be determined

- The engine and render pipeline. Not stated anywhere; the studio's recruitment mentions C++ and
  Unreal, so a Unity attribution would be a guess against weak contrary evidence.
- The voxel authoring tool (MagicaVoxel or otherwise) and the mesh/atlas pipeline.
- Whether the blur is a true depth of field or a screen-space band — inferred from image behaviour
  only.
- Whether the light shafts are volumetric or screen-space, and whether any Asset Store package was
  used for them. No credits list was reachable.
- Actual sun elevation, colour temperature, shadow softness, ambient values, or any grading numbers.
- Whether there is any time-of-day system at all, or only per-biome fixed lighting.
- Whether film grain is present.
- Any developer comment specifically on lighting readability under long shadows.
- Whether the post-launch photo mode (confirmed as planned in the Escapist interview) shipped, and
  whether it exposes the tilt-shift and grading parameters — that would be the best available way to
  reverse-engineer their stack, and is the obvious next step if this question is reopened.
