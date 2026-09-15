# 04 — The data model: Defs, inheritance, patching, validation

Content is data from day one (brief §2). Hard-coding content and retrofitting a Def system later is exactly the rework the brief forbids, and it is also the difference between a modder feeling at home and bouncing off.

Research: `d-07-data-pipeline.md`. This document is the design that follows from it.

## 1. Format: XML

**Committed: XML.** The tie-break is not modder familiarity — that is a real benefit but it would not decide the question on its own. It is the **path language available for patching**.

A patch operation has to say *where* it applies. XPath is a settled standard, it ships in the base class library, and it addresses by **predicate**: "the def whose defName is X", "every def whose parent is Y". JSON Pointer and JSON Patch address array elements **by index** — which breaks the moment a second mod inserts an element earlier in the same list. That is precisely the failure mode patching exists to prevent, so a format whose path language cannot express a stable address is disqualified regardless of its other merits. YAML and TOML have no path standard at all.

Secondary reinforcements: XML supports comments (content authors need them), diffs cleanly in review, and has mature schema tooling we can generate into.

## 2. Def shape

A Def is a named, immutable record of content. Names are unique within a type and are the currency of every cross-reference: Defs refer to each other **by name**, never by file position or load order.

Two properties are load-bearing and easy to lose:

- **Defs are content, not save data.** A save stores a Def *name* (via a per-save name table), never a copy of the Def. This is what lets content change under an existing save, and what makes a missing mod degrade to a named sentinel rather than a corrupt file (`d-06-save-load.md`).
- **Defs are frozen after load.** Nothing mutates a Def at runtime. Anything that varies per instance lives on the instance.

## 3. The loading pipeline

Ten passes, in this order. Errors **accumulate within a pass** and throw once at its end with code, content pack, file, line and column — never a silent skip, never a partial load.

| Pass | What it does |
|---|---|
| P0 Discover | Enumerate content packs in load order; core first, then mods |
| P1 Parse | Merge into one document, retaining line info and per-node provenance |
| **P2 Patch** | Apply patch operations by XPath |
| **P3 Inherit** | Resolve abstract parents and apply child overrides |
| P4 Prune | Drop abstract Defs; they are templates, not content |
| P5 Bind | Deserialise into typed C# objects |
| P6 Derive | Generate implied Defs (for example, a blueprint and a frame per buildable thing) |
| P7 Index | Assign integer handles; build per-type arrays |
| P8 Resolve | Link cross-references by name; a missing target is an error here |
| P9 Validate + freeze | Type-specific validation, then freeze |

**Patch runs before inherit** (P2 before P3). This matters and is easy to get backwards: a mod patching an abstract parent expects its change to flow down to every child, which only happens if patching happens first. This is also the order the reference implementation uses, which is worth matching since modders' intuitions are built on it.

**Provenance is retained** (P1) so that a validation failure in P9 can name the mod that caused it, not merely the merged line number. Half of modding support is answering "which mod broke this", and it costs almost nothing to record.

## 4. Patch operations

The shipped set: `Add`, `Insert`, `Remove`, `Replace`, `AddOrReplace`, `SetName`, `AttributeAdd`, `AttributeSet`, `AttributeRemove`, `AddExtension`, `Sequence`, `Conditional`, `FindContent`. Plus `MayRequire` / `MayRequireAnyOf` on any node, from day one, so a mod can depend on another optionally.

Three deliberate departures from the reference:

1. **A zero-match XPath is an error by default.** In the reference it silently succeeds, which turns a typo or an upstream rename into a mod that quietly does nothing. Opt out per operation when a no-op is genuinely intended.
2. **No `Test` operation and no success-override.** Both exist mainly to suppress errors, and suppressed errors are how a content bug reaches a player.
3. **Content packs are matched on package id, not display name.** Renaming a mod should not break everything that depends on it.

## 5. Binding, and the tick-time shape

Reflection-based deserialisation is convenient and wrong here: it is slow at startup, and it does not survive IL2CPP ahead-of-time compilation cleanly. Instead a **Roslyn incremental source generator** emits binders from the Def class declarations — no reflection, AOT-safe, and it can emit the type registry, an XSD and modder-facing documentation from the same source of truth.

At runtime, a Def is reached through a `DefHandle<T>` — an integer index into a per-type array. Hot paths never do a dictionary lookup on a string. Stat tables used inside the tick are flattened into frozen struct-of-arrays form at P7, so the simulation reads them like any other array.

## 6. Hot reload

Three tiers, because they have different risk:

- **Tuning constants** — swap freely at a tick boundary.
- **Structural changes within existing types** — swap at a tick boundary if handle stability can be preserved; otherwise require a reload.
- **New or removed Def types** — editor restart.

The swap itself is atomic: build the new `DefSet` fully, then exchange one reference at a tick boundary. A half-swapped content set is not a state the simulation should ever be able to observe.

**The Def-set hash feeds the determinism harness.** Two runs with different content are not expected to match, and the harness should say so rather than report a mysterious desync.

## 7. Testing

Defs load through an `IDefSource`, which has an in-memory implementation, an on-disk one and a temp-folder fixture one. Tests therefore need no Unity, no scene, and no `Assets/` content — which is what keeps the Sim assembly honest.

Golden tests dump the fully resolved Def set to text and compare, so an accidental change to inheritance or patching shows up as a diff rather than as a subtle behaviour change three milestones later.

## 8. The first Def types

Enough for the vertical slice, in dependency order. Types marked ○ are defined but barely used in the slice; they exist now so their shape is not invented under pressure later.

| # | Type | Carries |
|---|---|---|
| 1 | `StatDef` | Name, default value, min/max, display formatting |
| 2 | `StuffDef` | Per-stat factors and offsets (`stat = base × factor + offset`), tint colour, category |
| 3 | `TerrainDef` | Natural cell material: rock, fill, soil, pavement, rubble; work to clear; yields |
| 4 | `ThingDef` | The base record: footprint **in cells**, vertical extent, passability, base stats, allowed stuff categories, build work, module id for presentation |
| 5 | `SlabDef` | Floor/roof slab: `S_max` support, thickness, work, allowed stuff |
| 6 | `ConnectorDef` | Stairs, ladders, holes: footprint, the layer offset it connects, traversal cost, carry penalty |
| 7 | `DesignationDef` | Mine, deconstruct, build, forbid: icon key, target validity rule |
| 8 | `NeedDef` | Fall rate bands, thresholds, tick interval |
| 9 | `ThoughtDef` | Kind (situational or memory), stages with mood offsets, duration, stack limit, stack multiplier |
| 10 | `SkillDef` | Experience curve, passion multipliers, decay |
| 11 | `PawnKindDef` | Starting skills, needs, appearance set, rig |
| 12 | `WorkTypeDef` | Label, natural order, required skills, capacities |
| 13 | `WorkGiverDef` | Giver class, parent work type, intra-type priority, emergency flag, scan mode |
| 14 | `JobDef` | Driver class, report string, interruptibility, expiry |
| 15 | `ThinkTreeDef` | Nested node tree with insertion tags and priorities |
| 16 | `StorageSettingsDef` | Priority, filter (category allow-set, hit-point range, quality range, stuff set) |
| 17 | `TemplateDef` | A building shell authored **in cells**: module ids, vertical extent, damage tolerance |
| 18 | `MapGenDef` | The ordered pass list with per-pass parameters |
| 19 | `DistrictDef` | Per-district material palette, damage intensity, salvage weighting |
| 20 | `StrataDef` | What each underground layer band contains |
| 21 ○ | `RecipeDef` | Bills: ingredient filter, work, products, counting mode — M5, shaped now because `StorageSettingsDef` shares its filter record |
| 22 ○ | `IncidentDef` | Storyteller hooks — M6 |

`StorageSettingsDef` and `RecipeDef` deliberately share one **filter** record. The research found the reference does the same, and it is the right call: a stockpile filter and a bill ingredient filter ask identical questions, and one implementation means one set of bugs.

## 9. Risks

- **`System.Xml.XPath` under IL2CPP is unconfirmed.** This and the MemoryPack question from `d-06-save-load.md` are the two ahead-of-time serialisation risks; both are settled by small spikes inside M0 (units U04 and U05) before anything is built on them. Fallback if XPath fails under AOT: a small in-house path evaluator over the merged document, supporting the subset of XPath the operations actually use.
- **Startup cost is unmeasured.** No figures of our own exist for parsing a few thousand Defs. A synthetic 3,000-Def fixture in U04 settles it, and a hash-keyed binary Def cache is designed for but not built until a measurement asks for it.
- **List-merge edge cases.** The reference documentation never states them precisely. We specify our own rules and test them, rather than guessing at bug-compatibility.
