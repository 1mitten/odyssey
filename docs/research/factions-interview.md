# Factions: owner interview

**Phase:** the interview, at the feature level, in the shape of `cover-interview.md` and
`ranged-interview.md`.
**Date:** 2026-09-26.
**Branch:** `claude/lucid-euler-9puelu` (documents only; no code was written before this file or
under it).
**Conducted by:** Claude Code. Twelve questions in three rounds, asked after three read-only
explorations: the code that decides sides, what the docs already said about factions, and the
reference's faction mechanics (`a-13-factions-and-goodwill.md`).

**The owner's brief:** *"We need to explore factions and how this could work - could you explore,
plan come up with ideas. Ask me questions. https://rimworldwiki.com/wiki/Factions"*

**Read next:**
- `docs/design/61-factions.md`: the design these answers decide.
- `docs/plans/factions.md`
- `docs/research/a-13-factions-and-goodwill.md`

## 1. What the exploration found, put to the owner before the questions

- **A side is a property of a kind, not of a pawn.**
  - `enum Faction { Colony, Wild, Hostile }` sits on `PawnKindDef`.
  - Nothing has goodwill, relations or a faction object, and no pawn can change sides.
- **"Is this an enemy?" has about a dozen owners.** They are in melee, ranged, reactions, friendly
  fire, building targets and the raid code, and every one of them assumes the colony is at the
  centre.
- **Factions were pencilled in for M7, "scope lightly".**
  - Research item A13 was never run.
  - The names are *proposed* in `proper-nouns.csv`: the Tithe (raiders "who consider a cut of your
    salvage theirs by right"), the Cartage (a hauliers' combine), the Kindred (other survivor
    holdings).
  - There is a B9 Factions panel spec, and icon keys exist for gifts, comms and credits.
- **The reference** runs goodwill from −100 to +100 with hysteresis:
  - a faction turns hostile at −75, is neutral again at 0 and becomes an ally at +75;
  - goodwill is earned with gifts and by returning a faction's wounded alive;
  - it is spent on aid (−25) and on caravan requests (−15).
- **Alternatives put to the owner:**
  - Stellaris and Crusader Kings: opinion as a sum of dated reasons;
  - Kenshi: factions fighting each other on the map;
  - Dwarf Fortress: civilisations with histories.
- **Ideas pitched:**
  - the raiders as a protection racket;
  - the traders as the first peaceful arrivals;
  - the survivors as where people come from;
  - each faction arriving from its own stratum;
  - one shared hostility rule as unit F0.

## 2. Round 1

| Question | Options offered | Answer |
|---|---|---|
| When should factions be built? | Design now, thin slice now (recommended) · Design only · Full system now | **Design now, thin slice now** |
| What should the Tithe (the raiders) be? | Protection racket (recommended) · Permanent enemy · Several gangs | **Protection racket** |
| Which non-hostile interactions first? (multi) | Trade caravans · Visitors and refugees · Captives and returns · Allies and aid | **"Trade caravans is on another agent", Trade caravans** |
| What is the world beyond the map? | Abstract list (recommended) · Regional city map · Full world map · Nothing yet | **Abstract list** |

## 3. Found between rounds

- **The prisoner line** (`claude/prisoner-bed-assignment-98afc0`, design 58, P1–P6 built) has
  already added `Sim/Pawns/Prison/Allegiance.cs`, "the one owner of whose side a pawn is on".
  - It reserves Ransom until factions exist.
  - It names a goodwill cost for Execute and a gain for Release.
  - **So unit F0 extends `Allegiance` rather than adding a class beside it.**
- **No trade branch is on the remote yet.** The other agent's work is unpushed or not started, so
  who owns the trader's faction had to be asked.
- **Design numbers 57–60 are taken on branches**, so this is design 61.

## 4. Round 2

| Question | Options offered | Answer |
|---|---|---|
| Who owns the faction model, given trade is with another agent? | This line owns it (recommended) · Trade line owns the Cartage · Contract first, both build | **This line owns it** |
| What does the collector demand? | A share of stores by value (recommended) · Named goods · A person or labour | **A share of stores** |
| What happens on refusal? | Warning, then a raid (recommended) · Raid at once · Goodwill only | **Warning, then a raid** |
| Which factions should the roster have? (multi) | Tithe, Cartage, Kindred · An undercity faction · Feral machines · A second gang | *Other:* **"Bandits as we know them. The Orc Army and maybe something else?"** |

## 5. Round 3

| Question | Options offered | Answer |
|---|---|---|
| Are the bandits (as they are now) the ones running the racket? | Bandits run the racket (recommended) · Bandits always hostile · Orcs run the racket | **Bandits run the racket** |
| What is the Orc Army in a sci-fi city-world? | A war horde (recommended) · A disciplined army · Mercenaries | **A war horde** |
| Where does the Orc Army's art come from? | A Synty pack I own · Our own outfit on humans · Decide later | **A Synty pack I own** (not named) |
| For "maybe something else?", which should join? (multi) | Traders (the Cartage) · Survivors (the Kindred) · From below (undercity) · Feral machines | **Something else, Traders (the Cartage), Survivors (the Kindred)** |

## 6. What the answers settle, and the two left open

**Settled:**
- The roster is **Bandits** (the racket), **the Orc Army** (a permanently hostile war horde),
  **the Cartage** (traders, owned here and read by the trade line) and **the Kindred** (survivors).
- The first interaction built on this line is **the collector**:
  - a value-weighted share of the stores;
  - refusal brings a warning, a deadline and a punitive raid;
  - paying late calls the raid off.
- The world is **a list of settlements** with a distance in days.

**Open, to be asked in the handover:**
1. **Which Synty pack has the orcs.** The Orc Army's look is its own unit until the pack is named.
2. **What "something else" is.**
   - The owner ticked it alongside the Cartage and the Kindred without naming it.
   - Design 61 §3 proposes **the Sump**, people of the buried city who arrive from below, as the
     candidate.
3. *(Raised by round 3.)* **Whether the bandits keep the proper name *the Tithe*.** Design 61
   proposes they are called **Bandits** on screen, and that *tithe* becomes the word for what their
   collector demands.
