# A16 — modding architecture

## Question

OQ-26 / Lane A16. Three things:

1. How RimWorld's Def system loads, inherits and patches — the pipeline, the inheritance mechanism, and XML patch operations as a mechanism.
2. What modders actually reach for with Harmony — which methods get patched, what makes a codebase patchable or unpatchable, and what conventions keep a game friendly to it.
3. Where **Z-Levels** and its successors hurt when bolting vertical layers onto a 2D-first engine — what they had to fight, what broke, what they could not do.

Clean room: mechanisms, shapes and rules only. No Def XML, no decompiled code, no content names. Odyssey names below are minted here.

---

## Findings

### 1. The Def load pipeline

RimWorld's content loading is, structurally, a compiler pipeline: many source files are parsed, merged into one document, transformed, then compiled into runtime objects held in a database keyed by a string identifier. The community's reconstruction of startup counts around 38 discrete stages; the ones that matter architecturally are:

1. **Mod discovery.** Active mods are enumerated in a user-controlled *load order*. Order is the mod system's only global lever and everything downstream depends on it.
2. **Assembly load and construction.** Each mod's DLLs are loaded, in mod order, and its mod-entry classes constructed. This is where Harmony patching happens in practice, because it is the first point at which mod code runs.
3. **Parse.** Every content XML file from every active mod is read into memory. Files are not namespaced by mod; the folder convention is the only separation.
4. **Combine into one document.** All parsed content is merged into a single in-memory XML document. **This is the key move.** After this step there is no such thing as "my mod's file" — there is one tree, and a patch addresses a *node*, not a file.
5. **Load patches, error-check, apply.** Every mod's patch files are applied, in mod order, by XPath against the unified document.
6. **Register inheritance, then resolve it.** Parent/child relationships are registered and then applied — *after* patching.
7. **Parse into typed objects.** Nodes become typed Def instances by reflection on field names; a tag name maps to a field name on the target class.
8. **Resolve cross-references.** String identifiers written in content become object references. This runs in a fixed type order (category-like defs first, recipe-like defs, everything else, then thing defs last), because some types need others already resolved.
9. **Textures, audio, strings; then static startup hooks.** Late — after all defs are resolved — which is why one-time validation and caching code runs at the end rather than beside the data it validates.

Two consequences worth stealing and two worth avoiding:

- **Steal:** *one unified document, then transform, then compile.* It gives a single well-defined point where a third party can change any value without owning the file it was written in, and a single point where the game can say "this is the final content".
- **Steal:** *cross-reference resolution as its own stage with a declared type order.* It converts "does this identifier exist?" from a runtime null-reference into a load-time error.
- **Avoid:** *patching before inheritance resolution.* This is the single most-reported confusion. A patch cannot target a value the child inherits, because at patch time the child does not have it yet; the modder must patch the parent (which hits every sibling) or add the value to the child. It is defensible — you are patching the authored text, not the derived result, which keeps patching independent of inheritance — but it must be *documented loudly* and it needs a "show me the resolved def" tool, which RimWorld does not ship.
- **Avoid:** *silent failure.* An XPath that matches nothing is, by default, not an error; case-sensitivity mismatches therefore fail quietly and the modder discovers it as a missing feature hours later. The community had to invent helper mods to report this. Worse, malformed XML can abort the parse and take active mods with it.

Load cost is real: a third-party cache mod exists purely to memoise the parse/merge/patch stages, claiming launch-time reductions of up to about 45 per cent on heavily modded installs, and it explicitly *cannot* cache the later stages (typed-object construction, reference resolution, Harmony patching, static startup hooks) because those depend on code that changes per run. That tells us where the time goes: text parsing and XPath patching are cacheable; object construction and linking are not.

### 2. Def inheritance as a mechanism

The shape is deliberately minimal:

- A def may declare itself **nameable** (an attribute giving it a template name distinct from its content identifier) and may declare a **parent** by that template name.
- A def may be marked **abstract**, meaning it exists only to donate its fields and is never compiled into a runtime object.
- A child inherits every child node of its parent. **Scalars override**: a value present in the child replaces the parent's. **Lists merge**: the child's entries are *appended to* the parent's, not substituted for them.
- A specific node may opt out of inheritance with a per-node flag, which is the only way to get list replacement rather than list merge.
- Attribute names are case-sensitive, and the failure mode of getting the case wrong is silent non-inheritance.

Three observations for us:

- **The list-merge default is the right default and the wrong default at the same time.** It is right for additive things (a building gains one more component, a pawn gains one more aspect). It is wrong for ordered or exclusive things (a cost list, a stat set), where merging produces a def with two entries for the same key and no rule about which wins. RimWorld resolves that per-type, implicitly. We should make it explicit: **lists declare their merge mode in the schema, not at the call site** — append, replace, or key-merge (entries matched by a key field, child wins per key). Key-merge is what most modders actually want and neither engine offers it directly.
- **Single inheritance only.** There is no mixin. Modders emulate mixins with deep parent chains, which is why abstract chains three and four deep are common. A chain is cheap to resolve but expensive to read.
- **Inheritance is cross-file and cross-mod by construction**, because it is resolved against the unified document. That is a feature: a mod can parent its content to a base template it did not write. It also means a mod that renames a template silently breaks every dependent.

### 3. XML patch operations as a mechanism

Patching was added because the pre-existing technique — redefining a def with the same identifier so the last loaded copy wins — meant two mods touching the same thing could not coexist. **The design intent is compatibility, not expressiveness.**

The operation set is small and orthogonal:

- **Structural:** add child nodes (append or prepend), insert sibling nodes (before or after), remove nodes, replace nodes.
- **Attribute:** add-if-absent, set (add or overwrite), remove.
- **Typed convenience:** attach an extension record to a def, creating the container if absent; rename a node without touching its contents (used to retype a field in place).
- **Control flow:** a sequence that runs operations in order and aborts on the first failure; a conditional that tests whether an XPath matches and runs one branch or the other; an "is mod X present" test used for optional interoperability. An older explicit test operation is deprecated in favour of the conditional.

Targeting is XPath against the unified tree, with predicates on child values or attributes, and a text-node selector so that a value can be replaced without disturbing the element's attributes. Everything is case-sensitive.

What the community learned, which is the useful part:

- **Conditionals are for optional interoperability only.** Using "is mod X loaded" to implement required behaviour makes the result depend on mod order, since a mod later in the order may not be loaded yet from the patch's point of view.
- **A suppress-errors flag on a patch is an anti-pattern.** It was provided, it hides genuine failures, and the guidance is now never to use it.
- **Sequences that abort on first failure are the only transactional primitive**, and there is no rollback: operations already applied stay applied.
- **XPath is the right power level.** It is declarative, it composes, and it is a standard modders can look up. The cost is that it is a string, so nothing type-checks it.

### 4. What Harmony modders actually reach for

Harmony rewrites a method at runtime. Three forms, with sharply different compatibility properties:

- **Postfix** — runs after the original, always runs even if another mod cancelled the original, and can read and rewrite the return value. **This is the compatible one** and the community guidance is unanimous: prefer it.
- **Prefix** — runs before the original, can mutate arguments and can cancel the original entirely. Cancelling is what breaks other mods: a prefix that returns "skip the original" also skips every other mod's prefix and the original body. The recommended discipline is that a prefix should be *optional* — i.e. never cancel — and anything unconditional belongs in a postfix.
- **Transpiler** — rewrites the method's IL instruction by instruction. It neither affects nor is affected by prefixes and postfixes, so two transpilers can in principle coexist, but each is written against an instruction sequence the game's authors may change in any patch. Guidance is to change as few instructions as possible and to match on anchors rather than offsets. Transpilers are where mod breakage on game updates concentrates.

**What makes a codebase patchable — the actionable list:**

- **A method must exist with a real IL body.** You cannot patch what is not there. In particular, an inherited method that a subclass does not override cannot be patched *on the subclass*; the modder must patch the base and filter, which is coarse. So: **where a behaviour is meant to vary, give the type its own override, even a trivial one.**
- **Small methods get inlined by the JIT and a patch on them silently does nothing.** This is the most cited hazard and it is a *silent* failure at runtime, not a load error. Simple, short, non-throwing methods are the most likely to be inlined. The stated developer-side mitigations are: make hookable methods non-trivial, or make them virtual, or explicitly suppress inlining on them.
- **Aggressive optimisation is hostile.** Anything that removes the call — inlining, devirtualisation, whole-program optimisation, source generation that flattens calls, and, for us, **Burst compilation** — removes the seam.
- **Structure-of-arrays removes per-entity extension.** A mod cannot add a field to a struct in an array. It can only be given a *parallel* side table. This is the single most important interaction between our architecture (ADR 0005) and modding, and it is not something Harmony can rescue.
- **The community's own escalation order** is: use the data (defs and patches) → use a provided component or extension point → subclass and name the subclass in data → only then Harmony. The wiki's own advice for "add a field to a def" is explicitly *not* to subclass the def any more but to use the extension-record mechanism, and for "add behaviour to a thing" it is to attach a component rather than patch.
- **A pre-patching layer exists.** The current multi-level mod requires a separate framework (a pre-Harmony patcher) as a hard dependency, i.e. the mod could not be built on Harmony alone — it needed to change the shape of the game's types before they were loaded, which Harmony does not do. That is direct evidence that a 2D-first data model cannot be extended to layers by method patching alone.

### 5. Z-Levels and its successors — where bolting layers onto a 2D engine hurt

Two generations exist. **Z-Levels** (beta, unmaintained, last activity roughly four years ago) was the ambitious one. **MultiFloors** is the current, self-described "better planned, better performing, more compatible" successor which explicitly gave up features to get there. A third path opened when the base game gained embedded sub-maps, which the community describes as "almost acting like a z layer", with the caveat that many of them would be performance-intensive.

**What both had to fight, in order of how much it cost them:**

1. **The map is the unit of everything.** In a 2D-first engine, one map is one grid, one region graph, one reachability oracle, one lighting, temperature and power domain, one set of zones, one set of lists of things. A second level is therefore a second *map*, and every system that implicitly means "on my map" becomes a bug. The mods did not fight movement — they fought *everything that queries*.
2. **Job scanning, not pathfinding, is where it breaks.** This is the clearest signal in the evidence and the most valuable finding here. Both mods got pawns walking between levels early. What neither fixed generally:
   - **Bills assume one map.** A work order can only be completed from materials present on the same level; a pawn will not fetch across levels. MultiFloors' workaround is an opt-in *link* between a bench and one other level, which periodically checks whether ingredients are missing locally and, if so, raises a haul order from the linked level. That is a bespoke bridge, per bench, per level — a confession that the general query could not be made layer-aware.
   - **Hauling does not cross levels automatically.** Storage on another level does not count as accessible, so stockpile choice degrades to per-level, and players report resource management as the main pain.
   - **Delivering materials to a construction site** had to be *specially patched* to scan across levels. One job type, patched individually.
   - **Right-click command menus do not see other levels** — the UI's "what can I do here" query is also a same-map query.
   - **Work priority assignment** interacts badly: reports of sub-type work priorities being ignored over time when the multi-level mod is active.
3. **Third-party systems silently become per-level.** Prisoner labour only works on the prisoner's current level; two robot mods do not work off their level; a ship mod generates a new, *unlinked* map; another verticality mod produces undefined behaviour around stairs. None of these are bugs in the multi-level mod — they are every other mod's "my map" assumption surfacing.
4. **Environment had to be synchronised by hand.** Heat, power, gas, weather and outdoor temperature are each explicitly synchronised across the stair connection. Z-Levels synchronised roofs to floors and weather to upper levels. "Synchronise" is the word to notice: these are separate simulations per level, reconciled at the seam, not one volumetric simulation.
5. **Structure was modelled as roofs.** Building upward risks floor collapse "similar to normal roof collapses" and upper floors need supports; digging down risks dropping a mountain roof on yourself. The 2D engine's roof-and-support concept was the nearest available analogue and it was reused rather than replaced.
6. **What they could not do at all.** Z-Levels' own list of not-implemented: **temperature effects across levels, shooting between layers, and fall damage**. MultiFloors explicitly *avoids cross-level combat* to protect performance and compatibility. So: **no generation of this work has delivered cross-layer line of sight or cross-layer combat.**
7. **Saves and stability.** Z-Levels was save-incompatible with existing colonies, warned of game-breaking bugs, told players to save often, and shipped a structure that players were advised not to use. It was eventually abandoned.
8. **Performance framed as a hard ceiling, by the game's own authors.** The base game destroys a departed map rather than preserving it, on the stated reasoning that keeping many maps resident costs too much; sub-maps are acknowledged to be z-layer-like but explicitly performance-intensive in quantity. That is the 2D-first engine telling us that "N maps" is not a viable representation of "N layers" — which is exactly the design we avoided by making z a dimension of one grid.

**The lesson, stated plainly:** every attempt succeeded at *traversal* and failed at *queries*. Movement across layers is a pathfinding problem and it was solved repeatedly. Reachability, nearest-thing lookup, "is there space", "are there ingredients", "what can I do here" and line of sight are the ones that stayed 2D, because in a 2D-first engine they are written against an implicit map and there are hundreds of call sites.

---

## Recommendation

**Position: Odyssey should be moddable through data and registries first, tolerant of assembly modding, and should not promise IL patchability of the simulation core.** Ranked:

1. **Data-and-registry modding, with a declared extension API. ← back this.**
2. Managed plugin API only (explicit interfaces and events, no IL patching supported at all).
3. Full Harmony-friendly design (large virtual methods, inlining suppressed on hot paths, no Burst where a modder might want a seam).
4. XML data only, no third-party code.

Option 3 is disqualified on our own fixed decisions: ADR 0005 puts Burst on measured hot paths and structure-of-arrays everywhere, and both destroy patch seams — Burst removes the method, SoA removes the field. Designing for patchability would mean unwinding an architecture chosen by benchmark. Option 4 is too weak; every interesting mod in the reference ecosystem needs code. Options 1 and 2 are close; **the observation that breaks the tie is whether a third party can add a behaviour that runs inside the tick loop without editing a shared file.** We already have three of the five chokepoints open, so option 1 is mostly *finishing what the seam audit started* rather than new work. **Cheapest experiment that settles it: write a throwaway mod assembly that adds one commodity, one work type, one need and one pawn aspect, and count the shared files it must edit. Target: zero.** That is the same measurement the mining line failed (73 files, six shared files edited, five of which should have been extension points) and it is about a day's work.

Concretely, for Odyssey:

- **Keep the pipeline shape: parse → unify → patch → resolve inheritance → construct → link → freeze.** We already parse Defs from `Assets/Odyssey/Defs/Core` through `ContentPack`; the unified-document stage and a patch stage are the two additions, and `ContentPack.UseRoot` is already the seam for "where does content come from".
- **Patch before inheritance, as RimWorld does** — patch the authored text, not the derived result, so patching is independent of inheritance order. **But fix the two failures:** ship a resolved-content dump so a modder can see the post-inheritance def, and make **a patch that matches nothing an error by default**, opt-out with an explicit "optional" flag. Silent XPath failure is the most expensive bug in the reference ecosystem and it costs us one boolean to avoid.
- **Inheritance: single parent, a template flag for abstract, and per-list merge mode declared in the schema** — append, replace, or keyed (entries matched on a named key field, child wins per key). Keyed merge is what modders want and neither reference engine offers it; it is cheap because our loader already knows the field types.
- **One extension mechanism, not three.** RimWorld has components for things, extension records for defs, and subclass-by-name for classes. We already have `PawnAspect` — a sparse row keyed by a name the feature mints for itself (OQ-45, ADR 0004). **Generalise that single shape to defs and to every entity kind**: a sparse side table, keyed by entity and aspect name, authorable in XML, invisible to anything that does not ask for it. This is the only per-entity extension that survives structure-of-arrays, and it costs a mod nothing to add a field because it never widens a core array.
- **Registries over switch statements.** Work givers already register by existing (OQ-44). Extend that to subsystems, job drivers, mesh contributors (OQ-46 is open and is exactly this), alerts, HUD tabs and presentation directors (`OdysseyBootstrap` is the remaining chokepoint with no queue row). A registry is the option-2 API and the option-1 API at once, and it is testable: "a new X in a new assembly joins by existing" is an assertion, not a hope.
- **Say out loud what we do not promise.** The determinism contract is ours: the state hash, the tick schedule and the golden tables. A mod may add content and register behaviour; a mod that rewrites simulation IL voids determinism and we will not chase it. Publish that boundary early — RimWorld never did, and the result is that the community treats IL patching of the hot loop as normal.
- **Put content identity in the save.** Record the active content set and its fingerprint alongside the world, in the same spirit as `PawnContentDefTests`' folded literal. A save that loads under different content should say so rather than diverge quietly.
- **And the layer lesson, which is a coding convention rather than a mod feature: no query may take (x, y) alone.** See below.

---

## Layer questions touched

This file is infrastructure, but the Z-Levels evidence lands squarely on several of the twelve.

- **Q12 (what the slice must prove) — the strongest claim here.** Every 2D-first multi-level attempt solved *movement* and failed at *queries*. Our slice therefore proves nothing by demonstrating a pawn walking up a ladder. **It must prove cross-layer job scanning:** a bill whose ingredients are two levels down, a haul to the best stockpile regardless of level, a construction delivery across a level, and a command menu that offers a target the player cannot currently see. We already do the last two in part (mining, building). Make the bill-across-levels case an explicit slice acceptance test, because it is the one that defeated both mods.
- **Q1 (vertical movement).** Confirmed as the easy half — but note the mods' failure mode was *reservation and reachability*, not pathing. Our hop and ladder seams already carry the "planner and mover must agree" rule; add the third agreement: **the reachability oracle must agree with both**, or job scanning will pick targets the mover cannot reach and we will reproduce the mods' symptom from the other direction.
- **Q6 (zones per layer).** A14 already answered "per layer". The Z-Levels evidence *validates the geometry decision and warns about the consequence*: players' loudest complaint was that storage on another level did not count as accessible. Per-layer geometry is right; **per-layer reachability is wrong**. The stockpile lister must be 3D even though the zone is 2D.
- **Q9 (camera slice and UI).** "The command menu does not recognise other levels" is a UI-side same-map query. Our command and inspect queries must be layer-aware from their first commit, exactly as cell-touching systems already are.
- **Q3 (rooms across layers) and Q10 (gas, fire, water, sound).** Both mods *synchronised* separate per-level simulations at the stair. That is an artefact of a 2D engine and we should not imitate it: our flood fill and any future gas or heat model should be volumetric from the start, with the connector as an ordinary face rather than a special case.
- **Q5 (shoot up and down).** No multi-level mod has shipped cross-layer line of sight or combat; the current one deliberately refuses it for performance and compatibility. That is not proof it is infeasible in a native design, but it is the best available evidence that it is the most expensive layer feature. **Gate it behind measurement and stub it in the slice.**
- **Q2 (is a roof a floor).** The mods reused roof-and-support as the nearest analogue and inherited its collapse rules. We have real support solving; the transferable warning is the *player-facing* one — both mods' most-reported hazard was accidental collapse when building up or digging down, so the affordance and the warning matter as much as the solver.

---

## Sources

Read in full:

- <https://rimworldmodding.wiki.gg/wiki/XML_Inheritance> — the inheritance mechanism: named parents, abstract templates, scalar override versus list merge, the per-node opt-out, case sensitivity.
- <https://rimworldwiki.com/wiki/Modding_Tutorials/PatchOperations> — the full patch-operation catalogue, XPath targeting, conditional and sequence semantics, and the critical ordering fact that patching runs **after** parse and **before** inheritance resolution; also the pitfalls list (silent XPath failure, the deprecated suppress-errors flag).
- <https://legodude17.github.io/RimWorldModdingResources/loadingorder.html> — the roughly 38-stage startup order, and where assemblies, patches, inheritance registration, cross-reference resolution and static startup hooks each sit.
- <https://rimworldwiki.com/wiki/Modding_Tutorials/Harmony> — prefix, postfix and transpiler semantics and their compatibility properties; the inlining hazard; "you can't patch what isn't there".
- <https://rimworldwiki.com/wiki/Modding_Tutorials/Modifying_classes> — the escalation order in practice: components for behaviour, extension records for def fields, subclassing, Harmony last; and the explicit advice against subclassing defs to add fields.
- <https://rimworldbase.com/z-levels-mod/> — Z-Levels' feature list, structural rules (support and collapse in both directions), known bugs, save incompatibility, and the explicit not-implemented list (cross-level temperature, shooting between layers, fall damage).
- <https://top-mods.com/mods/rimworld/gameplay/13484-multifloors.html> — MultiFloors' three stated limitations (bills assume one map, no automatic cross-level hauling, command menu blind to other levels), its hard dependency on a pre-patching framework, and the list of systems synchronised across stairs (heat, power, gas, weather, outdoor temperature).

Used via search-result summaries only (the pages themselves were rate-limited or blocked):

- <https://steamcommunity.com/sharedfiles/filedetails/?id=3384660931> — MultiFloors' own page: the bench-linking workaround, the specially patched construction-delivery job, the per-level failures of prisoner-labour and robot mods, the unlinked-map interaction with a ship mod, and the work-priority regression. HTTP-blocked; content reached through search summaries.
- <https://steamcommunity.com/sharedfiles/filedetails/?id=2127428910> — Z-Levels Beta's own page: beta status, stairs in the structure tab, raiders using stairs, pets following owners, cross-level needs seeking. Rate-limited.
- <https://github.com/FluxxField/rimworld-defload-cache> — names the pipeline stages and, usefully, which stages are cacheable (parse, merge, patch) and which are not (typed construction, reference resolution, Harmony, static hooks); claims up to roughly 45 per cent launch-time reduction on heavy installs.
- <https://rimworldwiki.com/wiki/Pocket_map> — embedded sub-maps described as "almost acting like a z layer", with the caveat that many of them would be performance-intensive.
- <https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Harmony> — the clearest statement of the inlining hazard and the developer-side mitigations (avoid trivial methods, prefer virtual, suppress inlining).
- <https://github.com/UnlimitedHugs/RimworldHugsLib/wiki/Introduction-to-Patching> — the compatibility ranking of patch types: postfixes always run, prefixes can be suppressed by other prefixes, transpilers are orthogonal to both.
- <https://harmony.pardeike.net/v2/articles/patching-transpiler.html> — transpiler guidance: change as little as possible, stay flexible so other transpilers can coexist.
- <https://ludeon.com/forums/index.php?topic=52434.0> — the Z-Levels release thread. Attempted and returned HTTP 403; listed so a later session does not repeat the attempt without an authenticated fetch.

---

## Confidence

- **Def pipeline, inheritance and patch operations: high.** Multiple independent sources agree, including a stage-by-stage reconstruction and a cache mod whose design depends on the stage boundaries being real. The one item I would re-verify before building on it is the exact ordering of patching relative to inheritance registration — two sources agree, but it is the single fact the whole "patch the parent, not the child" rule rests on, and it is worth a five-minute confirmation before we copy it.
- **Harmony mechanics and patchability conventions: high.** Consistent across the RimWorld wiki, the Harmony documentation, a second game's modding wiki and a framework author's guide.
- **Z-Levels and MultiFloors symptom list: medium-high.** The limitations, the workarounds and the not-implemented list are stated by the mods themselves and corroborated by player reports. I am confident in *what broke*.
- **Z-Levels and MultiFloors internals: low.** I could not read either mod's source and could not confirm the "one level equals one map object" model beyond a search summary and strong circumstantial evidence (per-level environment synchronisation, a ship mod producing an "unlinked map", the base game's own stance on resident map count).
- **Performance numbers: low.** Nothing quantitative was available beyond the load-cache mod's own claim.

---

## Could not be determined

- **Z-Levels' implementation.** No repository was located within the cap; the release thread returned HTTP 403 and Steam rate-limited. Whether each level is a distinct map object, how stair connections are represented, and how saves were structured are all unconfirmed. **If this line matters later, the cheapest route is the current mod's source rather than the abandoned one**, since it is maintained and its dependency on a pre-patching framework is the most informative single fact we have.
- **What the pre-patching framework actually does.** It is a hard dependency of the current multi-level mod, which strongly implies it changes type shape — fields on game types — before load, something Harmony does not offer. Not confirmed.
- **Whether RimWorld exposes a supported way for a mod to register an entirely new def *type*** (as opposed to new instances of existing types), and whether new types participate in the cross-reference resolution order. This matters directly to the registry recommendation and I did not establish it.
- **MultiFloors' complete incompatibility list and its performance figures.** Its own page was blocked; what is above is a partial list assembled from search summaries.
- **Quantified patch cost.** How long XPath patching takes on a large modded content set, and whether XPath or typed-object construction dominates — the cache mod implies parsing and patching are significant, but gives no split.
- **Whether Burst-compiled methods can be patched at all in practice**, and what the community does about equivalent ahead-of-time-compiled paths in other Unity games. I asserted the seam is destroyed on general principle; it is not measured, and it is the one claim in the recommendation resting on reasoning rather than a source.
