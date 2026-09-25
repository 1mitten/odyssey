# Rain look: the second interview (2026-09-25)

The owner played the Weather tab (`claude/rain-look`, PR #203): *"I like it but you should be able
to have rain that also keeps the colour of 'clear' — in terms of the terrain — just dim it — as we
want to be colourful when it rains … make that the most likely default but then have dim days.
When I zoomed out I couldn't really see any rain … it clears up too much zoomed out and loses
atmosphere."* Two rounds of questions followed. The answers are the source of every decision in
design 43 §7's revision.

| Question | Answer |
|---|---|
| How should the meadow look in ordinary rain? | **Bright rain.** Keep the colour; dim the light a little, soften the shadows; no desaturation; the sky blue-grey rather than grey. The drained look becomes its own rarer day. |
| Wet ground: darker, richer, or gloss only? | The owner asked why gloss alone was not recommended. Reason given: at the camera's 48° the Fresnel reflectance is about 2 % (the water measurement), so gloss shows only as sun highlights, and a rainy day has a dimmed sun. **Decision: photograph both** — gloss only against richer and slightly darker (~×0.8) — and choose by eye. |
| What should say "rain" when zoomed out? | **Streaks across the screen.** A screen-space rain layer. |
| Where should that layer show? | **It fades in as the camera zooms out** (past roughly 60 m). Close up, the 3D drops and splashes carry the rain. |
| Should it respect roofs? | **Yes.** Mask each pixel with the cover map, using the depth behind it. |
| How do dim days fit the weather roll? | **As a separate, rarer kind** with its own weight in the season table. |
| What is the dim day? | **Storm.** Grey and drained, heavy rain, stronger wind bending the grass and slanting the rain. Lightning stays a later seam. `ui.weather.storm` already exists in the wiki. |

**Not asked, so these are defaults for the owner to tune:**
- how often a storm comes relative to rain (suggested: one rainy spell in four);
- how much bright rain dims the light (suggested: sun to about 75 %, shadows to about 60 %);
- how dense the far streak layer is.
