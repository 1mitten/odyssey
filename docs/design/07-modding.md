# 07 — Modding

Short by design (fast-track decision, `phase1-answers.md` Q9). The modding *API* is M8. What this document fixes now is the set of decisions that are expensive to reverse later: the extension points, and the code conventions that keep them open.

The brief's position is that Defs ship from day one and the scripting API and Workshop wait. That is the right split, because data-driven content is an architecture decision while a scripting API is a feature.

## 1. The three seams

A mod reaches the game through exactly three places. Keeping the list this short is what makes it maintainable.

**1. Defs.** New content and changes to existing content, by XML with XPath patch operations (`04-data-model.md`). This covers most of what mods actually do: new things to build, new materials, retuned constants, new job types wired to existing behaviour.

**2. Registries.** Where a Def needs behaviour that data cannot express, it names a class, and that class is found in a registry populated at load. The registries: work givers, job drivers, think-tree nodes, map-generation passes, storage filters, and — later — alerts, overlays and incidents. A mod registers into these the same way core content does, because core content goes through the same path. This is the rule that keeps the seams honest: **if core code takes a shortcut past a registry, that registry is already broken for mods.**

**3. Harmony-style patching.** For everything nobody anticipated. This is not something we build; it is something we avoid *preventing*. See §2.

## 2. Code conventions that keep patching possible

These cost almost nothing to follow and are very expensive to retrofit, which is the entire argument for adopting them now:

- Simulation classes are **public and unsealed**, with virtual methods where virtuality is cheap — decision points, scan methods, evaluators. Not tight inner-loop maths.
- **No static mutable state.** Everything hangs off the world built by the composition root, which also happens to be what makes headless tests trivial.
- Behaviour is reached through **interfaces named in Defs**, not through switch statements on enums. A switch is a closed set; a registry is an open one.
- Tuning constants live in Defs, never as literals in code. A mod that wants a different number should not need a code patch.

A note on the architecture choice: mod-friendliness is a **high-weight criterion** in ADR 0005 precisely because these conventions are easy in plain C# classes and awkward in an entity-component system, where "patch a method" often has no target. Whatever the benchmark decides, this section is the bill that decision has to pay.

## 3. Extension points, with the reason each exists

Each of these is here because Lane A research showed modders reach for it, not because it seemed tidy.

| Extension | Mechanism | Why |
|---|---|---|
| New buildable thing, material, terrain | Def only | The overwhelming majority of content mods |
| Retune anything | Def patch by XPath | The second most common mod, and it must not require code |
| New work type or work giver | `WorkTypeDef` / `WorkGiverDef` plus a registered giver class | New jobs are the most common behavioural mod |
| New job driver | `JobDef` plus a registered driver | Follows from the above |
| Alter pawn decision-making | Think-tree node insertion by tag and priority | Insertion by tag, rather than editing the tree, is what lets two mods coexist |
| New map-generation pass | `MapGenDef` pass list plus a registered pass | Map mods are popular and otherwise need deep patching |
| New Def type entirely | Source-generated binder picks up any Def class in a loaded assembly | Large mods invent their own content types |
| Anything else | Harmony against public, unsealed, virtual members | The escape hatch that makes a modding scene possible |

## 4. What is deliberately deferred

- **A scripting API** (C# or otherwise) for mods that do not want to compile against the game. M8.
- **Workshop integration and mod ordering UI.** M8.
- **A stable public API contract.** Until M8 the internals move freely; mods built against a prototype are built against a prototype, and saying so plainly now is kinder than implying otherwise.
- **Localisation extraction.** Not a modding blocker at prototype stage, but the Def loader keeps display strings separate from identifiers so it stays possible.

## 5. The one thing that would break all of this

Content that is not in Defs. Every literal in the simulation that should have been a Def is a small permanent tax on modders, and the taxes compound. The guard is a review habit rather than a test: when a unit introduces a number that a player might reasonably want to change, it goes in a Def.
