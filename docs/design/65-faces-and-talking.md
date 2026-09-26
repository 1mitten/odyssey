# 65 — Faces and talking

Status: **built 2026-09-26, not yet played — PR #247.** Branch `claude/face-expressions`. The
context, the talking variety and the hands (§3a, §5a–§5d) were added the same day on the owner's
first look at the sheets. Research:
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
| **Then, on the first look** | *"stern should happen when fighting/in draft, obviously when you tired, a variety of motions when talking etc - give it context where we can for now"* | Faces follow context (§3a); talking varies its manner and uses the hands (§5a, §5b); colonists at leisure standing together fall to talking (§5c) — the trigger the first answer deferred, read as covered by "give it context where we can". |

**Nothing here is in a cell, a save or the hash.** It is drawn state in `PawnFigureDirector`, like
the gaze. A colony's goldens cannot move.

Not built, and each is a later unit with a seam left for it (§8): conversations the simulation
means (the social system, M6), a speech marker that reads at any zoom, a mouth piece of our own,
portraits by mood band, animals.

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
| Pained | −6 mm | 0° | 0.35 | 1 | hurt |
| Glum | −3 mm | 0° | 0.8 | 1 | unhappy |
| Asleep | −2 mm | 0° | 0.08 | 1 | eyes shut |

**Measured, not guessed**: the magnitudes are the ones `FaceSheet` photographed on 2026-09-26 at the
128 px portrait and the play camera's closest zoom, where all six read on the women and the eyes
visibly change on the men (e-15's table). Smaller than this and the play camera loses them; larger
and the male brow band climbs into the hairline.

The last three were added with the context (§3a) and photographed the same way: Asleep shuts the
eyes, Pained screws them tighter than Tired, and **Glum is the subtlest face in the set** — a
droop that reads in the portrait and barely at the closest zoom. Pained and Tired are close
cousins by construction: one bone per feature allows a narrowing and a lowering, and both are that.

An expression is **eased**, not snapped: the held pose approaches the chosen one exponentially at
10 per second, so a change takes about a third of a second — the speed of a real face, and fast
enough that a debug click is answered at once.

### 3a. Faces from context

**A colonist's face follows what the frame publishes about her** unless the debug tab forces one
(`Odyssey.Hud.FaceContext.Expression`, over `FaceSignals.Of(PawnView, pain)`). First match wins,
so the order is the ranking — what she is doing to survive beats how she feels:

| Rank | When | Face | Read from |
|---|---|---|---|
| 1 | asleep | Asleep | `PawnView.Asleep` |
| 2 | downed | Pained | `PawnFlags.Downed` |
| 3 | fleeing or stunned | Alarmed | `JobHandle.Flee`, `PawnFlags.Stunned` |
| 4 | pain above 500 per mille | Pained | `odyssey.pawn.health.pain` (shock is 800) |
| 5 | fighting or drafted | **Stern** | `AttackMelee`/`AttackRanged`, a drawn weapon, `PawnFlags.Drafted` |
| 6 | pain above 200 | Pained | the same aspect |
| 7 | rest below 280 | **Tired** | `PawnView.Rest` — `Need_Rest`'s `seekThreshold`, where she starts wanting a bed |
| 8 | mood below 350 | Glum | `PawnView.Mood` against `MoodBands.Strained` |
| — | otherwise | Neutral | |

**280 and 350 are reading aids, as `MoodBands` are**: the interface cannot read the content, so
`FaceContext.TiredBelow` names the line it copies and is the one to move if the content does. It
is one read of the pain aspect per drawn figure per frame, through the aspect index.

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

### 5a. How a phrase is delivered

Each phrase rolls a **manner** (`TalkManner`), weighted: **Emphatic** 40 % (the beats and emphases
above), **Question** 18 % (beats, then brows up 12 mm, the chin up 4° and the head tilted 7° over
the last third, *held through the pause after it* — the listener's turn to answer), **Musing** 15 %
(beats at half the rate, looking 18° away for the first part of the phrase), **Tilt** 15 % (head
6° and brows 8° tilted for the phrase: doubt), and **Shake** 12 % (the head shaken ±7° at 2.5 Hz
for the first 0.9 s: no, or not that). The held parts ease at 8 per second so nothing snaps.

**The listener replies in four ways**: a nod (half the time), a double nod, a brow raise of 8 mm
(surprise) and a 6° tilt of the head, one every 1.6–3.6 s. A listener never looks away and never
lifts a hand.

### 5b. Talking with the hands

**The part of talking a player can see at the zoom the game is played at** — the board-angle
photograph (`TalkCheck`, `Logs/talk-board.png`) shows the raised forearm at 16 m where no face
reads. Six phrases in ten gesture: the right hand, the left, or both. The upper arm goes forward
25° and out 16°, the forearm up 70°, and the forearm beats ±10° with the phrase; the lift eases in
and out at 5 per second, so a pause lowers the hand.

**Added over the clip**, the way a gesture laid on a stance is, and about the figure's own swing
axis with the climb's and the carry's signs. **Only a colonist standing still with her hands free**
gestures — slower than 0.35 m/s drawn, not working, carrying, sitting, sleeping, swimming or
climbing, not drafted or holding a drawn weapon, not eating — and the freedom is itself eased, so
starting to walk lowers a raised hand rather than dropping it. The angles were tuned by photograph
(TalkCheck, 2026-09-26): out 10° let a woman's forearm fold across her belly; 16° opens it.

### 5c. Conversations the colony strikes up

Once a second, any two drawn colonists **at leisure** (`FaceContext.CanChat`: idle, wandering,
waiting or eating; not drafted, working, asleep, downed or stunned) and **standing still** within
4 m of each other have a one-in-four chance of falling to talking, for 8–18 s, after which neither
strikes up another for 45–90 s beyond its end. A struck-up conversation ends early the moment either
colonist has something better to do or they drift 6 m apart. Only drawn figures are asked, so the
cost follows the screen, never the colony.

**Measured with a control** (`TalkCheck`, 2026-09-26): two colonists held standing together struck
up a conversation after 0.4 s. A bare colony left to wander for 90 s struck up **none** — its
colonists scatter, and the one pair that met at 1.5 m had walked apart within seconds. Standing
still was made a condition *because* of that measurement: a conversation struck up between two
people walking in different directions ends before it starts. So expect chatter at meals and in
the pauses of a wander near the fire, not on the move — and if play shows it too rare or too
common, the chance and the reach are the two numbers.

### 5d. The greeting

When the passing glance fires (`CheckSocialGreetings`), the brows flash: up 12 mm and down again
in 0.45 s. The eyebrow flash is how people greet one another in passing, and the rig can do it.

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
| Talk | That colonist talks with the nearest awake colonist within 8 m for 30 s, or to nobody if there is none. Lit while a conversation it started runs; pressed again, stops those. **The conversations colonists strike up by themselves (§5c) are not its to stop**: the review found the first build counting them, so once colonists chatted on their own the row lit for no reason and silenced them all |
| From context | Hands the face back to §3a — lit by default |
| Neutral · Raised · Alarmed · Stern · Sceptical · Tired · Pained · Glum · Eyes shut | That colonist is **forced** to the expression until another is chosen or From context. **With nobody selected it is every colonist**, so the whole colony can be compared at a distance |

The rows are `DebugDirector.FaceRows`, held by the fast tier.

## 8. The seams for what comes later

- **A trigger** calls `PawnFigureDirector.StartConversation(a, b, seconds)` — the same call the debug
  row makes. When the social system publishes an interaction, presentation draws it through this.
- **A face the simulation wants to force** (a mental break, say) calls `SetExpression`; null hands
  it back to context.
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
- **The hands are added over the clip and only when standing still.** Written over a walk, a raised
  forearm swings with the gait.
- **A struck-up conversation needs both colonists standing still** (§5c). Taking that out makes
  conversations between people walking away from each other, measured.
