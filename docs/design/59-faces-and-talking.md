# 59 — Faces and talking

Status: **built 2026-09-26, not yet played.** Branch `claude/face-expressions`. Research:
`docs/research/e-15-facial-emotion-and-talking.md`, which this document rests on and does not
repeat.

## 1. What this is, and what it is not

A colonist's face can now **hold an expression, blink, and talk** — the mechanism and the
animation, and nothing that decides when. The owner's answers, 2026-09-26:

| Question | Answer | So |
|---|---|---|
| What starts a conversation? | *"Later — we are just looking at animation/mechanism now."* | Nothing in the game starts one. The debug menu's **Faces** tab does. |
| A mouth, or no mouth? | *"Whatever you recommend."* | **No mouth** (e-15: 2–3 px at the closest zoom, hidden under the men's beard block, and the owned lips cannot be opened without tearing the head). |
| Portraits follow mood? | *"Not for now."* | Portraits are untouched. |

**Nothing here is in a cell, a save or the hash.** It is drawn state in `PawnFigureDirector`, like
the gaze. A colony's goldens cannot move.

Not built, and each is a later unit with a seam left for it (§8): what triggers a conversation (the
social system, M6), expressions from mood and tiredness, a speech marker that reads at any zoom, a
mouth piece of our own, portraits by mood band, animals.

## 2. The rig, in one paragraph

Every colonist body in the pool has **one `Eyes` bone and one `Eyebrows` bone** under `Head`, each
moving real geometry (16/20 and 14/30 vertices, male/female); the `Jaw` moves no colonist (e-15).
One bone per feature means **both brows move together** — raise, lower, or roll as a pair — and the
eyes scale as a pair. That is the whole vocabulary, and it is enough for the six expressions below.
The head itself is the third instrument: it nods and sways on top of whatever the gaze turned it to.

## 3. The expressions

`Odyssey.Hud.FacePose.Of(FaceExpression)` owns the numbers; nothing else writes one. Lifts are
**metres of the body as authored**, before the figure's 1.4 scale; the eye scale acts along
whichever of the bone's own axes points up the head.

| Expression | Brow lift | Brow roll | Eyes open | Eyes size | Reads as |
|---|---|---|---|---|---|
| Neutral | 0 | 0° | 1 | 1 | the art as painted |
| Raised | +15 mm | 0° | 1 | 1 | interest, a greeting, surprise |
| Alarmed | +20 mm | 0° | 1.2 | 1.2 | fear, shock |
| Stern | −8 mm | 0° | 0.6 | 1 | anger, concentration |
| Sceptical | +4 mm | 12° | 1 | 1 | doubt |
| Tired | −4 mm | 0° | 0.45 | 1 | exhaustion |

**Measured, not guessed**: the magnitudes are the ones `FaceSheet` photographed on 2026-09-26 at the
128 px portrait and the play camera's closest zoom, where all six read on the women and the eyes
visibly change on the men (e-15's table). Smaller than this and the play camera loses them; larger
and the male brow band climbs into the hairline.

An expression is **eased**, not snapped: the held pose approaches the chosen one exponentially at
10 per second, so a change takes about a third of a second — the speed of a real face, and fast
enough that a debug click is answered at once.

## 4. The blink

Every live figure blinks, whatever its expression. An interval rolled per blink between **2.2 and
6.0 s**; each blink **0.18 s** — 0.06 closing, 0.03 shut, 0.09 opening, because a real lid falls
faster than it lifts; and **one in seven is a double**, the second starting 0.1 s after the first.
The eyes close to **8 %** of their height, and a blink *overrides* the expression's openness rather
than multiplying it, so a tired colonist blinks shut rather than to nothing-and-a-bit.

Seeded from the pawn id, so two colonists standing together do not blink in step, and a colonist
keeps her rhythm across a lease. A blink changes only the eyes; the brows never move with it.

## 5. Talking

A conversation is **two colonists, or one talking to nobody**, for a number of seconds, with **turns**:
one speaks while the other listens, and they swap every **2.5–6 s**. Talking to nobody alternates
speaking with silence on the same clock.

**The speaker** talks in **phrases** of 1.0–2.6 s separated by **pauses** of 0.25–0.7 s. Within a
phrase the head beats at **2.2–3.2 Hz** (re-rolled per phrase): a nod of 2.5° on each beat, and every
second to fourth beat an **emphasis** — the nod grows by 4°, the brows lift 8 mm, and both fall away
over a third of a second. A slow sway of ±2.5° about the face's axis runs under the whole phrase.
All of it is scaled by an *activity* that rises into a phrase and falls into a pause over about
0.12 s, so a pause is a stillness rather than a cut.

**The listener** nods slowly and seldom: a single 5° nod lasting half a second, every 1.6–3.6 s. No
brows.

**Both look at each other.** The gaze gains a tier, `GazePriority.Conversation`, between the passing
glance and work: a talking colonist who is not working, climbing or asleep looks at her partner's
head. A colonist working while spoken to keeps her eyes on the work and still nods.

A conversation ends when its time runs out, when it is stopped, or when either colonist is asleep or
downed. Nothing talks through a sleep.

## 6. Where it runs, and the rule that keeps it honest

`PawnFigureDirector.ApplyFaces`, **after `ApplyGazePose`** in both `Sync` and `Evaluate`, before the
head is hidden. Per live figure:

1. step the figure's `FaceMotion` by the frame's time — **real seconds** (`Time.deltaTime`), the
   butterflies' rule, or triple speed would make every colonist chatter; and **zero while the world is
   paused**, the figures' rule, so a paused face holds its frame like everything else (`Running`);
2. write the eyes and brows **absolutely from their rest pose** (`FaceRig.Apply`), never multiplied
   onto the bone's current value — the rest is read once, at bind, before any clip has run;
3. turn the head by the nod and sway **on top of the gaze's absolute rotation**.

Step 2 is the one not to undo by tidying. A face written as *more* of whatever the bone holds winds
itself up whenever something else stops rewriting that bone — which is exactly what froze the sleep
pose into a spiral (`Evaluate`'s comment). Nothing else writes these two bones, so absolute is the
only safe form. Step 3 is safe relative because the gaze has just set the head absolutely; the
talking colonist is never asleep, the one state in which the gaze stands down.

**State lives in two places on purpose.** A pawn's expression and its conversations are the
director's, keyed by pawn id, because figures are pooled and a colonist walking off screen and back
must keep her face and her conversation. The blink and the talking rhythm live on the figure, and
restart on a lease — nobody can see a rhythm restart on a colonist who has just walked into view.

## 7. The debug tab

`Faces`, beside Cheats, Spawn, Events and Weather. **Who it acts on**: the selected colonist, or, with
nobody selected, the one nearest the camera's focus — `DebugAnchorCell`'s own order, so every debug row
means the same thing by "this colonist".

| Row | Does |
|---|---|
| Talk | That colonist talks with the nearest awake colonist within 8 m for 30 s, or to nobody if there is none. Lit while any conversation runs; pressed again, stops them all |
| Neutral · Raised · Alarmed · Stern · Sceptical · Tired | That colonist holds the expression until another is chosen. **With nobody selected it is every colonist**, so the whole colony can be compared at a distance |

The rows are `DebugDirector.FaceRows`, held by the fast tier.

## 8. The seams for what comes later

- **A trigger** calls `PawnFigureDirector.StartConversation(a, b, seconds)` — the same call the debug
  row makes. When the social system publishes an interaction, presentation draws it through this.
- **Mood** calls `SetExpression`. The mapping e-15 proposed (content neutral, strained stern, tired
  from the rest need) is one function over `PawnView` away, and is the owner's to switch on.
- **A marker** reads `IsTalking(pawn)`.
- **A mouth piece** would be a third output of `FacePose` and a slot on the head bone, as the hair is.

## 9. Cost

Measured 2026-09-26 on the Windows dev machine (RTX 5070 Ti), at the figure ceiling of 64:

| What | Where | Per frame |
|---|---|---|
| Stepping 64 `FaceMotion`s | fast tier, .NET 8 (`FaceTests.SixtyFourFacesCostAlmostNothing`) | **4.6 us** |
| The same | Unity EditMode, Mono | **6.9 us** |
| The whole pass — step, two bone writes, the nod — for 64 figures | Unity EditMode, synthetic rigs (`FaceRigTests.TheFacePassFor64FiguresCostsMicroseconds`) | **19.2 us** |

**0.02 ms of a 5 ms budget, and no draw calls** — it writes bones, which the skinned mesh was going
to draw anyway. The 19.2 was taken with three other Unity batch runs sharing the machine, so it is
an upper reading rather than a quiet one. **Not measured inside the player frame**
(`FrameTimeTests`): a pass this size sits below that test's noise floor on this machine, and the
per-pass test is the instrument that can see it. Both tests assert only a ceiling a hundred times
the reading, against a mistake rather than as a budget.

## 10. Not to undo

- Face bones are written **absolutely from rest**. Never `+=` or `*=` a brow or an eye.
- Face time is **real seconds, stopped while paused**. Game time makes speed 3 chatter.
- **One owner for the numbers**: `FacePose.Of`. `FaceSheet` photographs through the same `FaceRig`
  and the same table the game uses, so the picture cannot drift from the game.
- **No mouth by vertex displacement.** Every corner of the owned lips is shared with the face (e-15).
