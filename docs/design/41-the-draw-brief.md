# 41 — The draw: brief for Claude Design

The prompt below goes to Claude Design as it stands. Its output is the visual target for unit CD5
of `41-the-draw.md`. Attach the reference screenshot with it:
<https://www.myabandonware.com/media/screenshots/a/alternate-reality-the-dungeon-68y/alternate-reality-the-dungeon_7.png>.

**What the reference shows**, for anyone reading this without the image: a C64 screen, 320 × 200.
Along the top is a strip of seven small white boxes carrying black abbreviations (STA CHR STR INT WIS
SKL HP) with a second row of boxes directly beneath holding the numbers. Below that is a hexagonal
gateway framed in bands of saturated colour: magenta, lime, cyan, orange. A small SILVER box sits at
the right. In the game the numbers cycle while the player stands in the gateway, and stepping
through freezes them.

What we take from it: **the strip** (boxed labels over boxed numbers, equal cells, ruled edges),
**the white against colour**, and **the idea that the numbers are alive until you commit**. What we
leave behind: the gateway, the C64 palette as a whole, and pixel fonts.

---

## The prompt

> Design one screen component for a sci-fi colony-survival game's character creation: a
> **slot-machine stat reel strip** that deliberately echoes the 1985 C64 game *Alternate Reality:
> The Dungeon* (reference attached). In that game the character's stats sit in a strip across the
> top of the screen: a row of small boxed abbreviations (STA CHR STR INT WIS SKL HP) with the
> numbers in boxes directly beneath, and the numbers cycle rapidly until the player commits.
>
> **What it is for.** The player can choose "Gamble" instead of the usual reroll-until-happy
> character creation. Each of their three colonists gets exactly one pull: every reel spins, the
> player presses STOP, the reels stop one after another from left to right, and whatever lands is
> that colonist, locked for good. It can be a star or a dud. The component has to make that moment
> feel like a fruit machine: suspense, noise, lights, and a verdict.
>
> **The ten reels, left to right:**
> FACE (a small flat portrait) · NAME · CHP · MIN · CON · GRO · MEL · SPD · TRAIT · TRAIT
> - CHP MIN CON GRO MEL are skills (chopping, mining, construction, growing, melee) and show a
>   number 0–15. A skill can carry a passion mark: a small flame beside the numeral, filled for a
>   major passion and outlined for a minor one.
> - SPD is walking pace as a per cent, 85–115.
> - TRAIT reels show a one- or two-word trait name ("Quick study", "Prodigy", "Butterfingers"), or
>   are left blank.
> - Group the reels visibly: identity (FACE, NAME), skills (the five), SPD, traits (the two).
>
> **Style:**
> - A **white machine face** with black ruled boxes of equal height and crisp 2 px rules: the AR
>   strip, rebuilt as a clean modern object. Chunky and a little retro in proportion, never pixel-font
>   kitsch.
> - Type: numerals in **IBM Plex Mono Medium**, large. Labels in **Archivo Narrow**, bold, uppercase,
>   tracked. No other faces.
> - The component sits inside a dark translucent game panel (`rgba(12,16,20,0.96)`, 1 px border,
>   cyan accent `#6fd3e3`, amber warning `#e8b55c`, green `#7fc98c`), so the white machine is the brightest thing
>   on screen. Say how it should meet that dark panel: a bezel, a shadow, a frame.
> - A frame of small **marquee bulbs** round the strip that chase while the reels spin.
> - One large button under or beside the strip: **PULL** before the spin, **STOP** while spinning.
>
> **Draw these states, side by side:**
> 1. **Idle**: before the pull. Reels showing a resting pattern, bulbs dim, PULL.
> 2. **Spinning**: every reel moving, with motion streaks or blur on the numerals, bulbs chasing,
>    STOP.
> 3. **Mid-cascade**: the left four reels landed, one reel "teasing" (visibly slowing toward what
>    looks like a high number), the rest still spinning.
> 4. **Landed, jackpot**: a MIN of 13 with a major passion and "Prodigy" on a trait reel. The frame
>    lit gold and the hot windows amber-lit.
> 5. **Landed, dud**: low numbers, "Wreck" on a trait reel, the bulbs going dark.
>
> Also show the **finished colonist card** it produces: a small card (portrait 60 px, name, age,
> occupation) with a **LOCKED** stamp treatment, and a face-down card for a colonist not yet pulled.
>
> **Size:** about 900 × 220 px at 1920 × 1080, and it must still read at 1280 × 720. Show the
> 1280 × 720 version of state 2.
>
> **Constraints:**
> - Basic ASCII only in every piece of text: no symbols from outside it.
> - Icons are simple line glyphs (Lucide-like), drawn, not emoji.
> - It will be built in Unity UI Toolkit, so prefer boxes, borders, solid fills, simple gradients
>   and opacity over blur-heavy or shader-dependent effects. Motion blur is suggested with ghosted
>   duplicate numerals, not a filter.
> - Give the colours as hex values, the spacing in px, and a short note per state on what moves.

---

## What comes back, and where it goes

The owner sends the prompt and brings the result back. The approved colours become `HudTheme`
tokens (`MachineFace`, `MachineRule`, `MachineHot`, `MachineStar`), the spacing becomes `HudLayout`
constants with tests, and each state's motion note becomes a property of `ReelMachine` or of the
view. Anything the design asks for that UI Toolkit cannot do is recorded in `41-the-draw.md` with
what was built instead.
