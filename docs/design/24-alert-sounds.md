# 24 — Alert sounds

*Written 2026-09-19, on `claude/alert-sounds`. Governs the chimes only. The playback machinery —
buses, the voice pool, the duck, the ambience beds — is ADR 0010 and is untouched by this.*

## 1. What was wrong

Two things, and only one of them was the sound.

**The chime was a test tone.** `AudioSetup.Alert()` synthesised a 0.6 s two-note sine — 830 Hz for
180 ms, then 622 Hz — which was honest placeholder work and sounded like it. The owner's word for
it was *nasty*. Five recordings now stand in its place.

**And it never played.** `AlertWatch` carried its own starvation threshold, `StarveThreshold = 12`,
with a comment saying *"food, in the published 0–100 units"*. Food is published 0–1000
(`PawnContent`: *"a need is 0..1000 rather than 0..1"*), and `AlertModel.StarveAt` — the threshold
the red row on screen uses — is 120. So the chime fired at a hundredth of the food the panel fires
at: a colonist reaching 1.2% food is a colonist who is about to die, long past the point the
warning was for. Its three tests fed literal `13`, `5` and `0`, so every one of them passed.

That is [bug-patterns](../bug-patterns.md)' first pattern exactly — **one rule with two owners** —
and it is why the fix below is not "correct the constant".

## 2. The shape

**One owner of the condition: `AlertModel`.** It already decides what an alert is, holds the
hysteresis, counts the subjects, assigns the severity and remembers what the player dismissed.
Audio does not get a second opinion; it reads the rows.

**One owner of the mapping: `AlertChime`.** A row becomes a sound by **severity first, key
second**:

| Severity | Sound | Because |
|---|---|---|
| `Notice` | `alert.normal` | worth a glance when you have a moment |
| `Warning` | `alert.negative` | will become a problem |
| `Danger` | `alert.negative` | is a problem now |

and a short override table beats the severity where severity undersells the event. It has one row
today: `ui.alert.raid` → `alert.raid`, because a raid and a starving colonist are both `Danger` and
must plainly not make the same noise.

**Severity-first is the whole reason this is a seam.** `docs/design/icon-keys.csv` declares 22
`ui.alert.*` keys; three are implemented. The other nineteen — breach, collapse, fire, injured,
infection, prisoner escape, power loss, spoilage, trapped and the rest — will chime correctly on
the day something raises them, with nobody having to remember to come back to `AlertChime`. A key
gets a row in the override table only when it has earned its own sound.

**One owner of "this is new": `AlertChimeWatch`.** It steps the panel's rows and returns the sound
to play, or null. Three rules:

- **A row chimes when it appears, and not while it stays.** The set of sounded rows is rebuilt from
  what is on screen each step, so a condition that clears and returns is a second alert and chimes
  again — which is what a player expects, and the reason it is not a set that only grows.
- **One chime, not one per row.** Several conditions can cross on the same refresh; they chime once
  between them, at the loudest severity among the *new* ones. An existing danger does not mask a
  new notice beside it.
- **The first step is silent.** It arms on whatever is already on screen when a world arrives, so
  loading a save with a hungry colonist does not chime at the player before they have found the
  mouse.

**The chime is raised from the HUD, not from the audio frame.** `HudShell.RefreshAlerts` calls it,
immediately after `AlertModel.Refresh` and in the same pass that builds the rows. A sound that
arrives on a different frame from the line it belongs to reads as two events. `AudioDirector.Sync`
no longer looks at alerts at all, and no longer scans the pawn list every frame to do it.

## 3. The clips

Baked by `tools/audio/bake_alerts.sh` from the owner's recordings. The script does three things and
refuses to do a fourth.

| Clip | Length | Ch | Plays when |
|---|---|---|---|
| `alert-normal` | 1.57 s | 1 | a `Notice` row appears — today, **Colonists are idle** |
| `alert-negative` | 1.86 s | 1 | a `Warning` or `Danger` row appears — **starving**, **close to breaking** |
| `alert-happy` | 4.96 s | 1 | **nothing yet** — see §5 |
| `alert-joined` | 5.77 s | 2 | **nothing yet** — see §5 |
| `alert-raid` | 9.54 s | 2 | `ui.alert.raid`, raised while a raid assaults (design 50 §7, 2026-09-25). The source is Pixabay's `low-horn-185556`, trading_nation |
| `alert-raid-arrive` | 18.0 s | 2 | a raid's Events row, when it arrives at the edge (design 50 §7). Pixabay's `war-horn-horror-73771`, baked by `bake_raid.sh` |

**Loudness was the real problem with the files.** As supplied they spanned −14.5 to −24.5 LUFS: ten
decibels, which is the difference between a chime that startles and one that is missed. They are
matched to −18 LUFS with a −1.5 dBTP ceiling, which puts every clip inside two decibels of every
other. `alert-normal` stops at −20 rather than −18 — it is a peaky bell, and reaching target would
have meant squashing the transient — so it is given the two decibels back as catalogue `Volume`
0.95 against the others' 0.9. **That is what per-sound `Volume` is for**: the file holds the
recording, the catalogue holds the mix.

**Dead air was the second problem.** The raid siren carried half a second of silence before it
started, which is half a second of nothing after an alarm has been raised. Trimmed at −45 dB at the
head and −50 dB at the tail (every source starts above −30 dB, so nothing musical was at risk),
with 6 ms guard fades over the cuts. Raid 10.73 → 9.54 s, joined 6.38 → 5.77 s, normal 1.96 →
1.57 s.

**Nothing was compressed, EQ'd or shortened musically**, and the script will not do it. If a clip
wants to be shorter that is a decision about the sound, and it belongs in a conversation.

### Why the import settings differ between them

**The two that fire in play are decompressed; the three that do not are not.** A chime has to sound
on the frame its row appears, so `normal` and `negative` are PCM decompressed on load — 168 KB
apiece and no decode at the moment of play. The three fanfares are seconds long, rare, and not
latency-critical, so they ride ADPCM compressed in memory at about a third the size. **1.3 MB of
alerts in total rather than 2.8 MB**, against the 53 KB the synthesised chime cost.

**None is forced to mono.** Unity's importer peak-normalises the downmix when `forceToMono` is set,
which would throw away the loudness match the bake exists to produce. `normal`, `negative` and
`happy` are mono in the file already; the two stereo fanfares keep their width, because an alert
plays 2D and has nowhere else to get any.

**Zero pitch and volume variance on all five**, against the work sounds' ±8% and ±12%. A chime is a
signal, and a signal that wobbles reads as a fault. Cooldown 2 s, except the raid siren's 10 s,
which covers its own length: two crescendos overlapping would be a mess.

## 4. What it costs

`AudioDirector.Sync` no longer walks the pawn list every frame, so this is a small net saving on
the frame rather than a cost. The watch steps a list that is 0–3 long, once every
`MidBucketSeconds`, not every frame. Two `HashSet<int>` of at most a handful of entries, allocated
once at construction and reused.

## 5. Recorded hooks

Three clips are in the library and played by nothing, which is the same bargain `SoundIds.Campfire`
already makes: named, imported, mixed and mapped, so the day the event lands the work is whatever
raises it.

| Clip | Waiting on |
|---|---|
| `alert-raid` | anything raising `ui.alert.raid`. **Already wired** — no code change needed. |
| `alert-joined` | a pawn arriving from outside the colony. There is no such event. |
| `alert-happy` | a `Good` severity, which `AlertRow` does not have. A visitor, a trade closing, a research project finishing — none exists. |

`alert-happy` is the one that needs a decision before it can be used: adding `AlertSeverity.Good`
means the alerts panel has to draw it, and a green row in a panel whose job is problems is a design
question, not a plumbing one.

## 6. Open, for a person at the keyboard

The owner has four more recordings in the same set that this branch did not take —
`notification-attention` (byte-identical to `negative`), `notification-death`,
`notification-doom` and `notification-big-raid`. They are candidates for a **death** alert and for
a raid whose size is known, both of which want the event before they want the sound.

And two questions no measurement answers, both recorded in CLAUDE.md:

- whether −18 LUFS is right in a quiet room, against an ambience bed and against the work sounds;
- whether `alert-raid` at 9.54 s and `alert-joined` at 5.77 s are too long to be alerts rather than
  cutscene stings. The bake deliberately did not shorten them.

## The end of a chime (2026-09-20)

On the first look at the supply drop the owner heard the happy chime end as the sound snapping to
silence and the music switching back on. Two causes, both in `AudioDirector`, neither in the
recordings. The duck came back at the rate it went down — 0.15 s, right for carving the chime's
space and wrong for handing it back — so the music reappeared as a switch; it now has two rates,
`DuckAttackSeconds` 0.15 and `DuckReleaseSeconds` 1.0, and a second's swell reads as the room
settling. And the clip itself stopped dead at its end: `StepChimeTails` fades the last
`ChimeTailSeconds` (0.4 s) of every voice on the Alerts bus, from the gain it was played at so a
fader move during the tail is applied on top rather than fought. Alerts only — a chop or a pick is
a transient and is meant to stop dead. `TheMusicSwellsBackAfterAChimeRatherThanSwitchingOn` and
`AChimeFadesOutOverItsTailInsteadOfCuttingOff` pin both.
