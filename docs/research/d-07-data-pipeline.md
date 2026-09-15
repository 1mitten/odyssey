# Lane D7 — The Def system: format, inheritance, patching, validation, hot reload

## Question

How should Odyssey's data-driven Def system work — file format, inheritance, patch operations, cross-references, validation at load, and hot reload in the editor — and how is it implemented efficiently in a pure-C# assembly with no UnityEngine dependency?

Phase 1 fixed the goal: **Defs shaped like RimWorld's, from day one, so modders feel at home**, with the C# scripting API deferred. Milestone M0 needs a Def loader with validation.

Clean-room note: everything below is the *shape and behaviour* of the reference system, learned from public modding documentation and restated in our own words. No Def XML, no decompiled code and no content names from the reference game appear in this repository. Every example fragment in this file is invented Odyssey data.

---

## Findings

### 1. The RimWorld shape, documented for re-implementation

**Declaration and naming.** Data files are XML. Every file has the same root element (`<Defs>`), and each direct child element is one Def whose *element name is the Def's C# type name* (`ThingDef`, `RecipeDef`, …). A concrete Def carries a `<defName>` child element: a string identifier that is unique within its Def type, case-sensitive, and used as the primary key everywhere else. Folder and file names carry no meaning at all — the loader walks the whole `Defs/` tree and merges everything; organisation is purely for the author's benefit. A Def element may also carry a `Class="Namespace.TypeName"` attribute, which tells the binder to instantiate a *subclass* of the declared Def type, so a mod can add fields to an existing Def family without touching the base type.
Sources: rimworldwiki XML file structure; Def classes tutorial.

**Abstract parents and inheritance.** Three XML *attributes* drive inheritance:

- `Name="Something"` marks a Def as available as an inheritance source (this is a different namespace from `defName` — `Name` exists only for the XML layer and never reaches the game object).
- `ParentName="Something"` makes this Def a child: it receives all of the parent's child nodes before its own are applied.
- `Abstract="True"` marks a Def as template-only: it participates in inheritance and is then discarded, never producing a game object. Abstract Defs need no `defName`.

Chains are arbitrarily deep (base → family → makeable family → concrete) and a node can be both parent and child. Keyword casing is significant, and an incomplete or missing parent is a hard failure.

**How a child overrides or extends.** The rule differs by node kind, and this asymmetry is the single most-cited gotcha in the community:

- A **scalar** child node in the child Def *replaces* the parent's node of the same name.
- A **list** node (children are `<li>` items) *appends*: the child's items are added to the parent's items. There is no way to remove a single inherited item by inheritance alone.
- `Inherit="False"` on a node in the child stops that one node inheriting at all, so the child's content stands alone. This is the standard escape hatch for replacing rather than extending a list, and it is per-node, not per-Def.

A further community rule follows from this: never redefine another pack's abstract base in your own pack. Because abstracts became inheritable across packs, two competing definitions of the same `Name` produce whichever loaded last, silently, for everyone downstream.
Sources: rimworldmodding.wiki.gg XML Inheritance; spdskatr's RWModdingResources.

**Cross-Def references.** Fields whose type is another Def are written in XML as the target's `defName` string. They are *not* resolved during deserialisation; the binder registers a "this object wants a cross-reference of type T named N" request, and a later pass resolves all of them at once against the type-keyed databases. Both the type and the defName must match, and failure produces a named diagnostic naming the missing type, the missing defName and the referring Def. There is also a "dictionary-shaped list" idiom, where a list element's *tag name* is itself a defName and its text is the value (used for stat blocks and recipe products); this is implemented by a custom load hook on the element type, and it is why some list nodes use `<li>` and some do not.
Sources: Def classes tutorial; the cross-reference error reports in community troubleshooting threads.

**Load order.** The published startup sequence, which is the strongest single source for our pass order, runs (abridged to the data-relevant steps): build the pack list in the user's configured order → load pack folders → load pack assemblies → **parse all Def XML files** → **combine every file into one single document** → load and error-check patches → **apply patches** → register inheritance → **apply inheritance** → **bind XML into typed Def objects, recording cross-reference requests** → warn about failed patches → put Defs into the type databases → fill the static `DefOf` handles (first pass) → **generate implied Defs** (blueprints, frames, corpses, stone terrain and so on are *derived data*, not authored data) → **resolve cross-references** → fill `DefOf` again with validation → call per-Def `ResolveReferences` in a controlled type order → generate the remaining implied Defs → **log configuration errors** → assign short hashes → load audio/textures/strings/bundles.

Two consequences matter enormously for us and are called out explicitly in the patching documentation:

- patches are applied **before** inheritance, so a patch cannot target a tag that a Def only has by inheritance — but a patch that alters a parent changes every child; and
- packs are patched **in pack order**, so the later pack sees the earlier pack's result.

Sources: legodude17's Loading Order page; rimworldwiki PatchOperations "Tips and Tricks".

**Patch operations.** Patches live in a separate `Patches/` folder, in files rooted at `<Patch>`, each containing a list of `<Operation Class="…">` elements. Almost every operation takes an `<xpath>` selecting one or more nodes of the *merged document* (so the first path segment is the document root element, not a file path), and predicates are ordinary XPath predicates — `[defName="X"]`, `[@Name="X"]`, `[@ParentName="X"]`, `[text()="X"]`, and `or` to hit several targets in one operation. The documented kinds, in our own words:

| Kind | What it does |
|---|---|
| Add | Inserts the supplied nodes as *children* of each selected node; appends by default, `<order>Prepend</order>` puts them first. It will not overwrite an existing non-list tag — a collision is a load error. |
| Insert | Inserts the supplied nodes as *siblings* of each selected node; prepends by default, `<order>Append</order>` puts them after. |
| Remove | Deletes each selected node. |
| Replace | Replaces each selected node with the supplied nodes. Selecting `…/text()` replaces only the text, preserving attributes. |
| AttributeAdd | Adds an attribute only if it is not already present. |
| AttributeSet | Adds or overwrites an attribute. |
| AttributeRemove | Deletes an attribute. |
| SetName | Renames a node without touching its contents — the tool for the dictionary-shaped lists, where the tag name is the key. |
| AddModExtension | Appends a typed extension object to the target Def, creating the container node if absent. The sanctioned way to attach a pack's own data to somebody else's Def. |
| Sequence | Runs a list of operations in order, aborting the rest on the first failure. |
| FindMod | Tests whether named packs are loaded and runs a `<match>` or `<nomatch>` operation accordingly. |
| Conditional | Tests whether an xpath selects anything and runs `<match>` or `<nomatch>` accordingly. The modern conditional. |
| Test | Older existence test, used to abort a Sequence. Documented as obsolete. |

Two further mechanisms sit alongside: a `MayRequire="packageId,…"` attribute usable on ordinary Def nodes *and* on operations inside a Sequence, which drops the node unless the named content is present (`MayRequireAnyOf` for the or-case); and a `<success>` node (Always / Normal / Invert / Never) that rewrites how an operation's failure is treated — documented as obsolete, because `Always` also suppresses genuine errors and makes failures undebuggable. Custom operation kinds can be written in C# by subclassing the operation base type.

The community's accumulated advice is worth importing wholesale: use explicit paths rather than `//` or `*/` (the recursive-descent forms are markedly slower over a document holding every Def in the game); prefer several targets in one predicate over several operations; do not wrap operations in a Sequence unless you actually need the conditional or `MayRequire` scoping, because a Sequence hides which child failed; `Add` vs `Insert` is the most common confusion (child vs sibling); and a duplicated non-`li` node is a load error that kills the Def.
Sources: rimworldwiki PatchOperations; Zhentar's original introduction; Lanilor's notes on xpath performance.

**Why patches exist at all** — the framing we should keep in our own documentation: before patching, the only way to modify somebody else's data was to redefine the whole Def, so with two packs touching one Def the later one silently won and the earlier one's work vanished. Patch operations turn that into a composable pipeline. This is the entire argument for building the mechanism at M0 rather than retrofitting it: the data format's *merge semantics* are the compatibility story, and they cannot be added later without invalidating every mod written in the meantime.

### 2. Format choice — XML, JSON, YAML or TOML

Criteria and how the candidates land:

| | XML | JSON | YAML | TOML |
|---|---|---|---|---|
| Familiar to our target modders | **yes, exactly** | partly | partly | no |
| Comments | yes | **no** (fatal) | yes | yes |
| Diffs / merges | good (line per tag) | good | fragile (indentation) | good |
| Deep nesting, polymorphic lists | verbose but natural | natural | natural | **poor** |
| Path language for patching | **XPath: standard, predicate-capable, in the BCL** | JSON Pointer (no predicates) / JSONPath (young standard, weak mutation semantics) | none standard | none |
| Patch standard | XPath plus our own operations | JSON Patch (RFC 6902) — add/remove/replace/move/copy/test | none | none |
| Schema tooling | XSD, plus editor autocomplete from XSD | JSON Schema (stronger ecosystem) | via JSON Schema | weak |
| C# parsing cost | acceptable; `XmlReader` is fast and low-allocation | `System.Text.Json` faster still | needs a third-party library | needs a third-party library |
| Attributes (metadata beside a value) | **native** (`Name`, `ParentName`, `Abstract`, `Class`, `Inherit`, `MayRequire`) | must be encoded as magic keys | magic keys | magic keys |

**Committed choice: XML.** The tie-break is not familiarity, which is only an argument for XML over JSON and could be answered by good documentation. The tie-break is **the path language for patch operations**. Patching another pack's data requires addressing a node *by content* — "the structure def whose defName is Girder", "every def whose ParentName is BaseWall" — and then mutating the tree at that address. XPath does exactly that, is a settled W3C standard that thousands of people already know, and ships in the BCL (`System.Xml.XPath`) so we write no path engine. The JSON equivalents fail on this precise point: JSON Pointer (RFC 6901), which JSON Patch (RFC 6902) is built on, addresses array elements *by index*, so a patch that says "element 3 of the ingredients list" breaks the moment another pack inserts an element earlier — the exact failure mode patching exists to prevent. JSONPath has predicates but was standardised far more recently, has no node-identity or mutation semantics, and has no in-box .NET implementation. YAML and TOML have no path standard at all, so we would be inventing and documenting one.

Secondary reinforcements, in order of weight: JSON has no comments, and a data format that modders cannot annotate is a non-starter for a game whose data files are also its design documentation; XML attributes give us a clean place for loader metadata (`Abstract`, `ParentName`, `Inherit`, `MayRequire`) that is visibly *not* game data, where JSON or YAML would need reserved keys colliding with real field names; and XSD gives Visual Studio Code and Rider free autocomplete over our Def types, which we can generate from the C# types at build time.

XML's real costs are accepted with eyes open: verbosity (mitigated by inheritance, which is the point of the feature), and per-node parsing cost (addressed in §4).

### 3. Loading pipeline — the pass order

Ten passes. Each pass has one job, one error class, and the ability to report file, line and column.

| Pass | Job | Errors caught here |
|---|---|---|
| **P0 Discover** | Read `content-order.xml` (user's pack order, core always first); read each pack's `pack.xml` manifest (id, version, dependencies, loadBefore/loadAfter); topologically sort within the user's order; enumerate `Defs/**/*.xml` and `Patches/**/*.xml` per pack with a deterministic ordinal sort of relative paths. | Missing or unparsable manifest; missing hard dependency; cyclic loadBefore/loadAfter; duplicate pack id. |
| **P1 Parse** | Each Def file becomes an element tree with line info. Drop nodes whose `MayRequire` is unsatisfied. Append every pack's Def elements, in order, to **one merged document**. Record provenance (pack id, file path, line) for every node. | Malformed XML; wrong root element; a Def element outside the root; unknown attribute on a Def element. Never abort on the first one — parse every file, collect every error. |
| **P2 Patch** | Apply patch operations, in pack order then file order then document order, against the merged document. | Malformed operation; unknown operation kind; xpath that compiles but selects nothing (an **error** by default, suppressible per operation with `<allowNoMatch>true</allowNoMatch>`); Add colliding with an existing non-list tag; Sequence child failure (reported as the child, not the sequence). |
| **P3 Inherit** | Index `Name` to node; resolve `ParentName` edges; topologically sort; merge parent into child bottom-up (scalar replace, list append, `Inherit="False"` resets the node). | Unknown `ParentName`; inheritance cycle; duplicate `Name`; `Inherit="False"` on a node with no parent counterpart (warning). |
| **P4 Prune** | Discard `Abstract` nodes. | A concrete Def with no `<defName>`; a `defName` that is not a legal identifier. |
| **P5 Bind** | Deserialise each element into a typed Def object using generated binders. Record cross-reference requests rather than resolving them. | Unknown Def element name (no such Def type); unknown child tag (no such field) — **an error, not a silent skip**; value not parsable as the field type; `Class=` naming an unknown or incompatible type; a required field absent. |
| **P6 Derive** | Generate implied Defs from authored ones (for example a blueprint Def and a frame Def per buildable structure Def, per `a-04-building-and-materials.md`). Derived Defs are ordinary Defs from here on and are validated like any other. | Name collision between a derived Def and an authored one. |
| **P7 Index** | Assign an integer handle per (type, defName); build the defName-to-handle map and the handle-to-object array; freeze. | Duplicate `defName` within a type (with both source locations printed). |
| **P8 Resolve** | Satisfy every recorded cross-reference request by (type, defName) to handle. | Unresolved reference, reported as *missing type and defName, wanted by referring def, at file:line*. All of them, not the first. |
| **P9 Validate** | Per-Def semantic checks (`Validate(DefLoadContext)` on each Def, in a fixed type order), then global checks (for example: every material referenced by a structure is in a category the structure accepts; a connector's cell count matches the layer height). Then freeze into the runtime tables. | Any rule a Def declares about itself or its neighbours. This is where "silently wrong at 3am" is converted into "refused to start". |

**Error policy, committed.** Errors accumulate into a report and are thrown once, as a single exception carrying the whole list, at the end of the pass that produced them (never at the first error — a modder must see all twenty problems, not one per relaunch). Every entry has: severity, error code (`ODY-DEF-0031`), message, pack id, file path, line, column, and the Def being processed. There is no "drop the broken Def and carry on" mode in M0: a failed load is a failed load, in tests, in CI and in the editor. A later softening — quarantine the failing Def, disable dependents, warn loudly — is recorded as a deliberate follow-up for the modding milestone (M8), not a day-one behaviour, because a partially loaded Def set is exactly the class of bug that shows up as a mysterious simulation divergence.

**Why this order.** It follows the reference sequence deliberately: patch before inherit (so a patch to a parent reaches every child, and patch authors reason about *authored* text rather than the post-inheritance expansion, which they cannot see in any file); bind before resolve (so a cross-reference to a derived Def works); derive before index (so derived Defs get handles in the same table). The one place we diverge is that our validation pass is unconditional, not dev-mode only.

### 4. Performance

Scale to design for: a few thousand Defs in core, plus whatever mods add. Heavily modded sessions of the reference game spend a large share of their startup inside patch application, which is a warning about `//` xpaths more than about XML itself.

**Parse.** `XmlReader` is the fast, low-allocation path — it never holds more than the current node. Published comparisons put it at roughly half the allocation of LINQ-to-XML at scale (about 4.2 MB against 8.0 MB for a 10,000-entry document) and consistently ahead on time. But patching *needs* a mutable, XPath-addressable tree, so a pure streaming reader cannot serve P2. Committed compromise:

- P1 builds the merged tree with `XDocument.Load(XmlReader.Create(stream), LoadOptions.SetLineInfo)` — reader-driven, one tree, line info attached. `SetLineInfo` is known to cost time and roughly double the tree's memory (dotnet/runtime#498); we pay it, because a Def error without a line number is the thing this design exists to prevent, and this is a one-off startup cost on a tree we throw away after P5.
- P2 uses `System.Xml.XPath` over that tree, with every operation's xpath **compiled once** (`XPathExpression.Compile`) and cached, and with a lint that warns on `//` and `*/` in a shipped pack's patches.
- P5 discards the tree. Nothing beyond P5 holds XML.

If measurement later says the tree is too expensive, the escape hatch is a compact home-grown node store (parallel arrays of tag id, first child, next sibling, text span, line) with our own XPath-subset evaluator — strictly a later optimisation, not an M0 task, and the pass boundaries above are drawn so it can be swapped in behind them.

**Bind.** Committed: **a Roslyn incremental source generator emits the binders**, not reflection. Reasons: no runtime type inspection and therefore no first-use metadata cost at exactly the moment we care about (startup); IL2CPP and AOT safety, since reflective field setting on types only reached via reflection is the classic AOT stripping failure; and the generator is the natural place to also emit the defName-keyed registry, the XSD for editor autocomplete, and the modder-facing field reference documentation — three artefacts that otherwise rot. Unity supports source generators as a `netstandard2.0` DLL asset labelled `RoslynAnalyzer` (Unity 6 documents the workflow), and the generator project itself is an ordinary C# class library outside the Unity assembly graph. Fallback if the generator proves painful: cached `System.Linq.Expressions`-compiled setters, which are still far ahead of raw `FieldInfo.SetValue` but are not AOT-safe, so this fallback would confine us to Mono builds — noted as a risk, not a plan.

**A binary Def cache** is the real answer to cold-start cost and should be designed in from M0 even if built later: hash (all source file contents, plus pack list and order, plus loader version, plus Def type schema version); if the hash matches the cached blob, deserialise the frozen tables directly and skip P1–P8 entirely. Everything above is arranged to make this possible — deterministic enumeration order, no ambient state, a pure function from (sources, order) to (def set, hash).

**Runtime shape.** Def objects stay public and unsealed with virtual members where cheap (the Harmony-style patchability rule in `CLAUDE.md`), so they are classes, not structs. Immutability is enforced by convention plus a freeze flag: fields are settable only by the loader, exposed as get-only or `init` properties, and any setter called after freeze throws. The hot path never touches a string and never touches a Def object:

- every Def gets a `DefHandle<T>` — a 32-bit index, `readonly struct`, comparable, serialisable;
- per type there is one `T[]` indexed directly by the handle, so lookup is a bounds check and an array read;
- `Dictionary<string, int>` exists only in the loader, in save/load remapping and in dev tools;
- at freeze time, fields consulted every tick (material stats, movement costs, support spans, flags) are **copied out into flat arrays indexed by handle** — a struct-of-arrays "stat table" — so the tick loop never chases a reference into managed Def objects and never risks a cache miss per query.

Saves store `defName` strings, not handles or hashes, and remap to handles on load. This costs a little file size and buys robustness against the pack list changing between sessions, which matters more.

### 5. Hot reload in the Unity editor

Where the data lives, committed: `Content/Core/Defs/**` and `Content/Core/Patches/**` at the **repository root**, outside `Assets/`. The loader takes a plain directory path, so headless tests and CI read it with no Unity involved; a build step copies `Content/` into `StreamingAssets`. This also keeps the licensing boundary clean — Defs carry *asset keys as strings*, never Unity object references, so the Sim assembly depends on neither UnityEngine nor anything under `Assets/Synty/`.

The reload model, in three tiers:

- **Tier A — soft swap (the common case).** A file watcher over `Content/**` debounces changes and queues a reload command. The command executes **at a tick boundary, never mid-tick**. The loader runs the whole pipeline from scratch into a *new* immutable `DefSet`. Handles are assigned by matching `defName` against the previous set first and appending new entries afterwards, so existing handles stay valid. If no Def was removed or renamed, the new `DefSet` and its derived stat tables are swapped in atomically and the simulation carries on. Editing a number, a label, a stat or a list is therefore a sub-second round trip with the game running.
- **Tier B — structural change.** A Def was removed or renamed, or a Def type's shape changed (a code change, which means a domain reload anyway). Live objects may hold handles that no longer mean anything. The editor tool refuses the soft swap and offers "reload Defs and restart the simulation from the last autosave" — the save stores `defName` strings, so the round trip repairs the references by construction.
- **Tier C — never.** No reload mid-tick, no partial reload of one file, no mutation of a live `DefSet`. A partial reload cannot be made to interact correctly with inheritance and patching, whose whole point is that the result depends on *all* the inputs.

Interaction with the running simulation, and with determinism: a reload writes a marked entry into the run log (`defs reloaded at tick N, def-set hash H1 to H2`), and the determinism harness includes the Def-set content hash in the state hash it compares. So an editor Def edit that changes a golden-master run fails the test *and explains why*, rather than presenting as a mysterious desync. Unity specifics: the `DefSet` is owned by the composition root object, never by a static, so the domain reload (and the no-domain-reload play mode option) cannot leave a half-initialised static cache behind; the loader must be idempotent and free of mutable statics for the same reason.

### 6. Testability and assembly layout

```
Odyssey.Defs            pure C#, no UnityEngine. Parser, patch engine, inheritance,
                        binder runtime, handles, error reporting. Knows nothing about
                        any concrete Def type.
Odyssey.Defs.Generator  netstandard2.0 Roslyn incremental source generator, shipped
                        as a DLL asset labelled RoslynAnalyzer. Emits binders, the
                        Def registry, the XSD and the field reference docs.
Odyssey.Sim            references Odyssey.Defs. Declares the concrete Def classes
                        with [Def]-style attributes; the generator emits the registry
                        partial here. No UnityEngine.
Odyssey.Presentation   references Odyssey.Sim. Resolves the string asset keys on
                        Defs to actual prefabs and materials. The only place Synty
                        is named.
Odyssey.Editor         editor tooling, the file watcher and the reload command.
Odyssey.Sim.Tests      EditMode-runnable and dotnet-runnable; no scene, no Synty.
```

How tests supply fixtures, committed: the loader's entry point takes an `IDefSource` abstraction (enumerate relative paths, open a stream) rather than a directory string. The default implementation is the file system; the test default is an **in-memory source** built from a dictionary of virtual path to XML string, which needs no temp folder, no I/O and no cleanup, and makes a fixture legible inside the test that uses it. Alongside it:

- a small **on-disk fixture tree** under `Tests/Fixtures/defs/` exercised through the real file-system source and through a **temp-folder copy** (so the directory-walking, ordering and file-watching code is genuinely covered);
- **golden dumps**: for each fixture pack set, a canonical text dump of the fully resolved Def set, committed next to the fixture. Inheritance and patch regressions then show up as a readable diff rather than as a failing assertion on one field;
- **negative fixtures**, one per error code, asserting the code, the file and the line number. These are the tests that keep the "fail loudly with a pointer" promise honest;
- a **determinism test**: load the same fixture set twice, and once with the files enumerated in reverse, and assert an identical def-set hash.

---

## Recommendation

**One committed design.**

1. **Format: XML.** Root `<Defs>`, one element per Def named for its type, `<defName>` as the primary key, `<li>` list items, and the loader attributes `Name`, `ParentName`, `Abstract`, `Inherit`, `Class`, `MayRequire` / `MayRequireAnyOf`. We keep those structural keyword names deliberately — they are the format's public surface and the whole point is that a modder's existing muscle memory transfers. We invent every *content* name: our own Def type names, our own defNames, our own field names, our own flavour text. **Tie-break: XPath.** Patch operations must address nodes by content, not by position, and XPath is the only candidate that is a settled standard, is predicate-capable, and ships in the BCL. JSON Pointer's index-based array addressing breaks under exactly the multi-mod scenario patching exists to solve; JSON has no comments; YAML and TOML have no path standard to build patching on.

2. **Patch operations we ship at M0**: Add, Insert, Remove, Replace, SetName, AttributeAdd, AttributeSet, AttributeRemove, AddExtension, Sequence, Conditional (`match`/`nomatch`), FindContent (`match`/`nomatch`), and **AddOrReplace** (which the reference community reimplements in every framework mod, so we ship it). We deliberately **omit** the obsolete existence-test operation and the `<success>` override entirely — Conditional supersedes both, and an always-succeed override is documented as an error-suppressing debugging trap. A zero-match xpath is an error by default, opt-out per operation. Custom C# operation kinds are reserved by keeping the `Class=` attribute shape, and land with the deferred scripting API. Our one fix to a known wart: FindContent matches on **package id**, never on display name.

3. **Pass order: P0 Discover → P1 Parse → P2 Patch → P3 Inherit → P4 Prune abstracts → P5 Bind → P6 Derive implied Defs → P7 Index and assign handles → P8 Resolve cross-references → P9 Validate and freeze.** Patch before inherit. Errors accumulate per pass and are thrown once, with error code, pack, file, line, column and the Def in hand. No silent skips, no partial loads.

4. **Library and approach:** `XmlReader`-driven `XDocument` with `SetLineInfo` for P1–P2, compiled and cached `System.Xml.XPath` expressions for P2, a **Roslyn incremental source generator** emitting binders, registry, XSD and field docs for P5, integer `DefHandle<T>` plus per-type arrays plus frozen struct-of-arrays stat tables for runtime, and a hash-keyed binary Def cache designed in from the start and built when measurement demands it. Nothing in `Odyssey.Defs` or `Odyssey.Sim` touches UnityEngine.

5. **First Def types for the vertical slice.** M0 proves the loader with three: `StatDef`, `MaterialDef`, `StructureDef` — enough to exercise inheritance (a structure family), a cross-reference (structure to material category), a dictionary-shaped list (stat modifiers) and a patch. The full slice set through M3, in dependency order:

   `StatDef` · `MaterialDef` (our stuff) · `SurfaceDef` (floor and ground) · `StructureDef` (walls, doors, floors) · `SlabDef` (roofs as material, per `a-04`) · `ConnectorDef` (stairs, ladders) · `ItemDef` · `DesignationDef` and `DesignationCategoryDef` · `WorkTypeDef`, `WorkGiverDef`, `JobDef` (per `a-03`) · `NeedDef`, `ThoughtDef`, `SkillDef` (per `a-01`) · `VisualDef` (asset key and cut-away behaviour). Blueprint and frame Defs are **derived** in P6, never authored. Recipes, biomes, factions and research wait for M4 onwards.

**Cheapest experiment to de-risk this before it is built:** generate 3,000 synthetic Defs and 500 patch operations into a fixture, run the pipeline cold and cached, on the 2022-laptop target and in an IL2CPP player build. It gives three numbers we currently lack (cold load time, cache hit time, patch share of the total) and smoke-tests the one genuine unknown — `System.Xml.XPath` under IL2CPP.

---

## 3D/layer impact

A Def in a layered world is not a 2D Def with a z field bolted on. What our Defs must carry from their first commit:

- **`size` as (x, y, z) in cells**, never a 2D footprint, defaulting to 1 × 1 × 1. At the fixed cell of 2.5 × 2.5 × 3.0 m (ADR 0002), a two-layer machine is `z=2` data, not special-case code.
- **`mount`**: Volume, FloorSlab, CeilingMount, WallEdge, EdgeCorner. Synty walls are 2.5 m wide, 3.01 m tall and 0.23 m thick (`e-01`), that is, edge-mounted, so the cell's *edges and faces* are addressable Def targets and not just its interior.
- **Structure**: `supportsAbove`, `requiresSupportBelow`, `supportSpan` (the integer stability radius idiom from `b-going-medieval.md`), `massPerCell`. Support and collapse are data, not hard-coded constants.
- **Per-face occlusion**: `blocksLight`, `blocksAirflow`, `blocksSound` as **six-face masks** (including up and down), because in 3D a floor slab occludes differently from a wall, and a broken floor is the whole point of layer questions 4 and 5.
- **`ConnectorDef`**: cells consumed (Synty stairs rise 1.5 m per cell, so two cells per 3.0 m layer — `e-01`), traversal cost, direction constraints, whether hauling and carrying are permitted, whether it is passable while under construction.
- **Zones and volumes**: a `ZoneKindDef` needs an explicit "single layer or 3D volume" flag, so layer question 6 is answered in data rather than by a system's assumption.
- **`VisualDef` asset keys, never Unity references**, plus a cut-away behaviour enum so the sliced camera knows how each module draws when the slice passes through it. This is what keeps `Assets/Synty/` out of the simulation and its tests.

## Layer questions touched

- **Q1 (vertical movement):** `ConnectorDef` carries the cells-consumed and traversal rules, so the two-cell stair and one-cell ladder are data.
- **Q2 (is a roof a floor):** `SlabDef` plus the support fields make roofs material objects with authored support spans rather than annotations.
- **Q6 (zones per layer or 3D):** answered in data by a flag on `ZoneKindDef` rather than in code.
- **Q10 (unit of simulation for gas and fire):** touched only indirectly — the six-face permeability masks are the Def-side contribution; the simulation model itself is D1's question.
- **Q12 (what the slice must prove):** the Def loader is M0's gate, and the three-type M0 set above is the minimum that proves it.

## Sources

- https://rimworldmodding.wiki.gg/wiki/XML_Inheritance — Name/ParentName/Abstract, scalar override against list append, `Inherit="False"`.
- https://rimworldwiki.com/wiki/Modding_Tutorials/XML_file_structure — root and Def element structure, keyword casing, the inheritance walk-through, `Inherit="False"` on lists. (Live site is Cloudflare-gated; read via a Wayback Machine snapshot.)
- https://rimworldwiki.com/wiki/Modding_Tutorials/PatchOperations — the complete operation catalogue, xpath usage and predicates, `order`, `MayRequire` on sequence children, the obsolete success override and test operation, custom operations, tips and troubleshooting, and the "patching runs before inheritance, in mod order" statement. (Read via a Wayback snapshot.)
- https://rimworldwiki.com/wiki/Modding_Tutorials/Def_classes — how a Def class's fields map to tags, subtags, `<li>` lists against tag-name-keyed lists, the `Class=` attribute, and the register-then-resolve cross-reference idiom. (Read via a Wayback snapshot.)
- https://legodude17.github.io/RimWorldModdingResources/loadingorder.html — the full 38-step startup sequence; the authority for our pass order.
- https://spdskatr.github.io/RWModdingResources/abstracts.html — abstracts as templates, `Name` against `defName`, the list-append gotcha, and why redefining another pack's abstracts breaks compatibility.
- https://gist.github.com/Zhentar/4a1b71cea45b9337f70b30a21d868782 — the original patch-operation introduction; the compatibility argument for patching over redefinition.
- https://gist.github.com/Lanilor/e36af29ce5ce725b9b29768c63b00ef2 — xpath selector performance (`/Defs/X` against `//X` against `*/X`), predicate filtering, defensive patching.
- https://rimworldwiki.com/wiki/Modding_Tutorials/MayRequire — MayRequire and MayRequireAnyOf semantics.
- https://bertwagner.com/posts/xmlreader-vs-xmldocument-performance/ and https://github.com/zulimazuli/dotnetXmlBenchmarks — XmlReader against document-tree parsing time and allocation figures.
- https://github.com/dotnet/runtime/issues/498 — `LoadOptions.SetLineInfo` time and memory cost.
- https://learn.microsoft.com/en-us/dotnet/api/system.xml.linq.xobject.system-xml-ixmllineinfo-lineposition — `IXmlLineInfo` on tree nodes; line info exists only for nodes loaded with `SetLineInfo`, not for nodes added afterwards (relevant: P2 must carry provenance explicitly for patched-in nodes).
- https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/reflection-vs-source-generation — source generation against reflection: no runtime type inspection, no startup metadata cost, AOT and trim safety.
- https://docs.unity3d.com/6000.4/Documentation/Manual/create-source-generator.html — Unity's source-generator workflow: netstandard2.0 class library, Microsoft.CodeAnalysis.CSharp 4.3, DLL imported with all platforms unchecked and the `RoslynAnalyzer` asset label.
- RFC 6901 (JSON Pointer) and RFC 6902 (JSON Patch) — the JSON alternative's index-based addressing, cited for why it loses the tie-break.
- Repository context: `docs/adr/0002-cell-size-and-layer-model.md`, `docs/research/a-01-pawns.md`, `a-03-work-and-jobs.md`, `a-04-building-and-materials.md`, `e-01-module-mapping.md`, `b-going-medieval.md`, `docs/brief.md` §5.

## Confidence

- **The reference system's shape (declaration, inheritance, list semantics, patch operation catalogue, load order): high.** The operation catalogue and the "patch before inherit, in pack order" rule come straight from the official tutorial page, and the 38-step sequence from a second, independent modding-resources site; they agree with one another and with the community explanations.
- **Format choice (XML) and the tie-break: high.** The path-language argument is structural rather than a matter of taste, and the JSON Pointer weakness is a documented property of the standard.
- **Pass order and error policy: high.** It is the reference sequence with our own stricter validation pass, and nothing in it is speculative.
- **Parsing and binding approach: medium-high.** The direction (reader-driven tree, compiled xpath, generated binders, integer handles, frozen stat tables) is well supported, but we have **no measurements of our own** — the numbers cited are other people's benchmarks on other people's documents.
- **Hot reload tiers: medium.** The design is sound and conservative, but the handle-stability trick and the tick-boundary swap have not been built here; the first implementation may find that Tier A is rarer in practice than hoped.
- **Def field lists for 3D: medium-high.** Derived from our own earlier research files rather than from any external source, which is the right provenance, but they will move once D1's architecture decision lands.

## Could not be determined

- **Absolute startup cost.** No measured figure for parsing *our* Def set exists, because our Def set does not exist. The 3,000-Def synthetic fixture named in the recommendation is the cheapest way to get one, and it should run before P1's `SetLineInfo` decision is treated as final.
- **`System.Xml.XPath` under IL2CPP.** The XML APIs are in Unity's .NET Standard profile and there is no reported blocker, but no source confirmed XPath specifically on an IL2CPP player build. Smoke-test it in M0; the fallback (our own xpath-subset evaluator) is a day of work, not a redesign.
- **The reference system's exact list-merge rules in edge cases** — for instance what happens when a child sets `Inherit="False"` on a node its parent does not have, or how a patch that adds a `ParentName` attribute interacts with inheritance registration in the same load. The documentation is silent; we should simply *specify* our own answers and test them rather than guess at somebody else's.
- **Whether patch application or binding dominates startup** in a heavily modded session. Community lore says patching, and the xpath-performance advice implies it, but no profile was found.
- **How the reference system reports errors precisely** (whether its diagnostics carry file and line at all). Irrelevant to our design — we require file, line and column regardless — but it means we have no prior art to copy for the report format, so ours is invented here.
