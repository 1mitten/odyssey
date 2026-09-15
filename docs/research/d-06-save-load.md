# Lane D6 — Save and load: format, references, mid-tick state, determinism

## Question

What save/load architecture should Odyssey use — file format, what must persist, how mid-job pawn state survives a round trip, and what it costs in file size and load time at 250 × 250 × 40 cells with tens of thousands of things? Covering: format and compression, the object-reference problem, mid-tick pawn state, the determinism oracle, versioning and missing mods, and which C# serialisation approach fits a pure-C# Sim assembly with no UnityEngine dependency.

## Findings

### 1. What RimWorld does, and what to keep

`a-15-time-and-simulation.md` already establishes the shape: one `ExposeData`-style virtual method per savable object, run in both directions, producing a single XML tree; loading proceeds in phases (write → read raw values → resolve cross-references → post-load initialisation); spawned objects are written once and referenced elsewhere by load ID; homogeneous natural fill is stored as a compressed grid, not as per-thing records; saves happen at tick boundaries, never mid-tick; and mid-job state (job, job driver with toil progress, job queue, posture, reservations) is persisted so a pawn resumes.

The modding guide confirms the reference mechanics: objects implementing the "load referenceable" interface are written by ID only, and "the object has to be saved somewhere else as well for this to work"; Defs are written by `defName` through a dedicated def-reference call; collections mixing deep and reference modes need auxiliary lists to disambiguate.

**Keep**: the single-pass symmetric declaration, the four phases, load IDs with a resolve pass, the deep-versus-reference distinction, Defs by name, the compressed grid, tick-boundary saves, and persisted mid-job state.

**Drop**: XML as the wire format. The compressed-grid trick is itself an admission that a text tree does not scale to per-cell data — and we have 40× the cells. XML also actively works against determinism, because it forces every float through text formatting.

### 2. Size maths for the cell grid

Assumptions: 250 × 250 × 40 = **2,500,000 cells**. Two per-cell grids in M0 — `material` (a `ushort` id into the def table) and `flags` (one byte: solid/floor/buildable/passable bits). The worked example below is the `material` grid (2 B/cell); the flags grid behaves the same at half the size.

A ruined-city column is extremely stratified: roughly z 0–7 solid rock and engineered fill, z 8 the surface, z 9–20 building shells occupying perhaps 20 % of the footprint, z 21–39 air. Run estimate, encoding along X within a layer row (10,000 rows of 250 cells): rock layers ~3 runs/row (2,000 rows → 6,000 runs), air layers 1 run/row (4,750 rows → 4,750), building layers ~20 runs/row (3,000 rows → 60,000), surface ~40 runs/row (250 rows → 10,000). **≈ 81,000 runs.**

The palette-and-bit-pack estimate uses a 25 × 25 × 5 chunk (3,125 cells, so exactly 10 × 10 × 8 = **800 chunks**, no padding); a chunk with one palette entry stores no data array at all (Minecraft's rule). Estimate 600 uniform chunks and 200 mixed ones with a ≤16-entry palette at 4 bits/cell.

| Scheme (material grid, 2.5M cells) | Bytes | Size | vs plain | Random access? | Incremental save? |
|---|---:|---:|---:|---|---|
| XML, one element per cell (`<li>12</li>`) | 27,500,000 | 26.2 MiB | 5.5× worse | no | no |
| JSON, flat number array (~4 B/value) | 10,000,000 | 9.54 MiB | 2.0× worse | no | no |
| Plain binary `ushort[]` | 5,000,000 | 4.77 MiB | 1.0× | yes | no |
| Run-length encoded (2 B value + 2 B count) | ~324,000 | **0.31 MiB** | **15.4×** | no (needs an index) | per-row only |
| Palette + bit-packed 25×25×5 chunks | ~325,000 | **0.31 MiB** | **15.4×** | yes, O(1) | **yes, per chunk** |
| Palette-chunked, then LZ4 | ~150,000 | 0.14 MiB | ~33× | after inflate | yes |
| Plain binary, then LZ4 (no RLE) | ~450,000 | 0.43 MiB | ~11× | after inflate | no |

Two conclusions the table makes plain. First, **RLE and palette-chunking land in the same place on bytes** — but chunking additionally gives O(1) random access and a natural dirty-tracking unit, so it wins on everything that is not bytes. Second, **generic compression alone gets most of the byte win but none of the load-time or incremental-save win**: you still allocate and touch the full 7.15 MiB of grids on every load, and rewrite all of it on every autosave. Use both layers, not one.

Dirty tracking: a colonist-day modifies perhaps 5–20 chunks. An incremental autosave therefore rewrites ~30 KB of grid instead of 325 KB, and more importantly touches ~60,000 cells instead of 2.5M.

### 3. Whole-save size and time

| Component | Count | Est. bytes each | Binary total |
|---|---:|---:|---:|
| Cell grids (material + flags), palette-chunked | 2.5M cells | — | ~0.5 MiB |
| Things (rubble stacks, furniture, items, ruin fittings) | 40,000 | ~48 | 1.83 MiB |
| Pawns (needs, skills, health parts, job stack, memory) | 350 | ~5,000 | 1.67 MiB |
| Reservations, zones, designations, region metadata, tick manager | — | — | ~0.2 MiB |
| Header, def table, mod manifest, load-ID counter | — | — | ~0.05 MiB |
| **Total, uncompressed binary** | | | **~4.2 MiB** |
| **Total, LZ4-compressed** | | | **~1.6 MiB** |

Time budget, using the measured throughputs in §4: LZ4 compress 4.2 MiB ≈ **15 ms**; LZ4 decompress ≈ **8 ms**; MemoryPack deserialise 4.2 MiB at a deliberately conservative 200 MB/s ≈ **21 ms**. Total serialiser cost is therefore **under 50 ms in both directions** — which means **the serialiser is not the bottleneck**. Post-load re-derivation is: rebuilding the spatial index over 40,000 things, the room/region graph over 2.5M cells, and the reachability caches. Budget 200–600 ms for that and measure it, because it is the number that will actually decide whether loading feels instant.

The same content in a RimWorld-shaped XML tree would be roughly 12–20× larger (40–80 MiB) and parse at perhaps 20–60 MB/s, i.e. seconds — before any float-formatting cost.

### 4. Compression, measured

Measured .NET figures on a ~3.86 MiB text corpus (NimblePros), plus the K4os LZ4 port's own published numbers:

| Codec (.NET) | Compress | Decompress | Output size | Licence |
|---|---:|---:|---:|---|
| LZ4 (`K4os.Compression.LZ4`, pure managed) | ~280 MB/s | ~520 MB/s | ~57 % | MIT |
| Deflate / GZip / ZLib, `Optimal` | ~28 MB/s (138 ms) | ~290 MB/s (13.4 ms) | 29.6 % | BCL |
| Brotli, `Optimal` | ~43 MB/s (89 ms) | ~300 MB/s (12.7 ms) | 27.1 % | BCL |
| Brotli, `SmallestSize` | ~0.5 MB/s (7,709 ms) | ~300 MB/s | 22.0 % | BCL |

Caveat: that corpus is text, so the *ratios* will not transfer to our binary payload (already-RLE'd grids compress far less; thing records rather more). The *throughput ordering* does transfer, and it is decisive. Deflate at ~28 MB/s turns a 4.2 MiB autosave into a ~150 ms hitch; LZ4 turns it into ~15 ms. `BrotliStream` at `SmallestSize` is a trap — 86× slower than `Optimal` for a further five percentage points.

Decompression is fast for all of them, so the choice is really about save cost, and saves are frequent (autosave) while loads are rare.

### 5. The reference problem

The graph is genuinely tangled: a pawn holds a job, the job targets a thing, the thing sits in a stockpile owned by a zone owned by the map, the pawn holds a reservation on the same thing registered in a map-level manager, and the thing may be inside a container held by another pawn. Straight recursive serialisation would duplicate objects or loop forever.

**Load IDs.** Every savable entity carries a `LoadId` (a `long`) drawn from a **single monotonic counter that is itself part of the save**. Never from `GetHashCode`, object identity, array index, or position — things move between layers, and the counter must survive round trips so newly-spawned objects cannot collide with loaded ones.

**Deep vs reference.** Exactly one place deep-writes each object (its owner: the map's thing list, the pawn's job tracker, the container's inventory). Everywhere else writes only the ID. This is the rule that keeps the graph a tree plus a set of edges.

**Four phases** (RimWorld's, kept verbatim in shape):

1. **Write** — deep-write owners; write reference slots as bare IDs; write the def table.
2. **ReadVars** — construct every object, fill value fields, register `LoadId → object` in a resolve table. Reference slots read their ID into a pending list; the field stays null.
3. **ResolveRefs** — walk the pending list, look up each ID, patch the field. An unresolvable ID is *not* an exception: null the field, log it with the owner's identity, and let phase 4 decide.
4. **PostLoad** — validate combinations (a pawn with a job but no driver has the job cleanly ended, which is exactly what protects saves against a mod that changed a toil sequence), then rebuild all derived state.

**Defs are content, not save data.** Write a **def table once per save**: an ordered list of `defName` strings. Every def reference in the body is then a 2-byte index into that table. This is both compact (a def reference costs 2 bytes, not ~20) and honest about the content/state split — the save never contains def *contents*, only names, so content can be rebalanced between versions without touching saves. A `defName` present in the table but absent from loaded content resolves to a sentinel "unknown def", and phase 4 drops the owning thing with a logged entry rather than throwing.

**Collections** are written as length-prefixed arrays with an explicit element mode (deep / ref / value) in the section header, so a reader never has to guess. Dictionaries are written as two parallel arrays, **sorted by key or by LoadId** — see §7.

### 6. Mid-tick state: what to save, what to re-derive

Saves are taken at tick boundaries (a-15), so "mid-tick" really means mid-job, mid-path, mid-construction *at* a tick boundary. For a pawn to resume seamlessly:

**Must be saved** (state, not derivable):

| Category | Fields |
|---|---|
| Job | current `Job`: def ref, targets A/B/C as `LocalTargetInfo` (thing ref *or* `(x,y,z)` cell), count, haul mode, expiry tick, flags |
| Driver | the concrete driver **type tag** (a union discriminator), the **toil index**, the per-toil tick counter / progress accumulator, and the driver's own fields (e.g. how much of a bill's work is done) |
| Queue | the pawn's queued jobs, in order, with the same shape as the current job |
| Guards | per-tick job-change guard counters, "job started this tick" flags, last job-end reason |
| Reservations | map-level: `(claimant LoadId, target ref, layer, stack count, job ref)` tuples |
| Path | the path's **destination and goal mode only** (see below) |
| Body state | cell `(x,y,z)`, facing, posture (standing/lying/downed), stance and its remaining cooldown ticks, carried thing (deep, as a held container), equipment and apparel refs |
| Needs & mind | need levels as raw floats, mood/thought entries with remaining durations in ticks, mental state and its tick counters |
| Scheduling | the thing's **hash phase offset** for Rare/Long ticker spreading — otherwise cadence phases reshuffle on load and a resumed run diverges from an unbroken one |
| Global | current tick, RNG stream states (if any stream is stateful), the load-ID counter, bill and frame work-done accumulators |

**Safe to discard and re-derive** (pure functions of saved state):

- **The concrete path.** Save the destination, throw away the node list, recompute on the pawn's first tick after load. Cheaper than serialising thousands of cells, and immune to a stale path routed through a region graph that no longer exists. One condition: path re-derivation must not consume RNG from a shared stream (use a per-pawn stream, or none).
- Region/room graph, reachability and connectivity caches, including vertical links through stairs and ladders. The *connector thing* is saved; the *graph edge* is derived.
- Spatial indices (thing-at-cell buckets) and ticker lists — rebuilt from the things list plus each thing's ticker type and saved phase offset.
- Lighting, danger maps, work-giver scan caches, pathfinding cost grids.
- All presentation state: GameObjects, selection, camera, interpolated positions.

**Borderline — save it.** Room *temperature values* are state, not cache, even though the room *graph* is derived; the same goes for gas concentrations and fire spread accumulators. Rule of thumb: if losing it changes the next tick's outcome, it is state.

**The re-derivation rule that makes this safe**: every re-derived structure must be a **pure, deterministic function of saved state**, and the state hash (§7) is computed over *saved state only*, never over caches. That is what makes "discard and rebuild" free rather than a determinism hole.

### 7. Determinism: the oracle and the hazards

Factorio's technique is the model, and it is cheap to copy: run in a diagnostic mode that "makes a CRC from the whole map every tick" and "saves the map with some useful human readable tags to a new file every tick as well"; run the replay through the same process; on the first mismatched checksum, binary-diff the two tagged files, where "the difference is usually very small — typically just a value of one variable".

**Four tests, in cost order:**

- **Test C — byte stability** (cheapest; run it first). Save the same in-memory state twice; assert the two byte streams are identical. This catches unordered iteration and unstable ID assignment without running a second simulation at all. Make it a unit test over a small fixture map.
- **Test A — round trip.** Run seed S to tick N. Hash → `H_N`. Save. In a fresh process, load, hash → `H'_N`. Assert `H_N == H'_N`. Catches any field that fails to round-trip, immediately and with no ambiguity about where.
- **Test B — resume equivalence** (the milestone gate). From the loaded state, run K more ticks → `H'_{N+K}`; compare against the uninterrupted run's `H_{N+K}`. **K must exceed the longest cadence** — at least 2,000 ticks (one long tick), and for the M0 gate the whole remaining day, so the headless one-day run *is* the test: run 60,000 ticks unbroken, and separately 30,000 → save → load → 30,000, then compare final hashes.
- **Test D — replay.** Record the player command stream with tick stamps. Replay from tick 0 comparing per-tick hashes; on mismatch, binary-search to the first divergent tick and emit the canonical text dump for both sides.

**The hash.** xxHash64 or FNV-1a over a canonical ordered walk of saved state. Floats hashed as **raw IEEE bits** (`BitConverter.SingleToInt32Bits`), never as text. Collections walked in LoadId order. Hash the *save payload* itself where possible, which makes the hash and the format agree by construction.

**The canonical text dump.** A `--dump-text` mode that emits the same walk as a stable, sorted, line-per-field text file. This is the one place text serialisation belongs, and it is what makes Test D's diff readable. It is a debug artefact, never a save format.

| Hazard | How the design prevents it |
|---|---|
| Unordered `Dictionary`/`HashSet` iteration at write time — .NET documents the order as **undefined**, and it varies with insert/delete history | Dictionaries and sets are written as arrays **sorted by key or LoadId**, always. Sim code is banned from iterating an unordered collection where order affects outcomes. Test C catches every regression. |
| Load IDs derived from hash codes, addresses or enumeration order | IDs come from a monotonic counter that is saved and restored; nothing derives an ID from memory layout. |
| RNG re-seeded or advanced differently after load | Per a-15 the RNG is reseeded every tick from `(worldSeed, tick, streamId)`, so it carries **no cross-tick state** to lose. Any stream that is genuinely stateful writes its state explicitly. |
| Float text formatting | Binary IEEE bits throughout. Even though .NET Core 3.0+ makes `ToString()`/`"R"` shortest-roundtrippable for 100 % of inputs (before that change only 32 % of floats and 8 % of doubles round-tripped), Unity's Mono and IL2CPP runtimes do not all share that BCL version, and culture-sensitive formatting remains a live trap. Writing bits removes the class of bug rather than managing it. |
| Wall-clock time or frame counts leaking into sim state | The Sim assembly has no engine reference and no `DateTime.Now`; the save contains tick counts only. |
| Re-derived caches differing between a fresh load and a long run | The hash covers saved state only; caches are pure functions of it; a cache that cannot be expressed that way is promoted to saved state. |
| A field silently not saved, defaulting on load | Test A fails at tick N with the mismatch localised to that section. |
| Ticker phase offsets reshuffling on load | Phase offsets are saved per thing, not recomputed from registration order. |

### 8. Versioning and missing mods, without a migration framework

Four mechanisms, all cheap, none of them a framework:

1. **A plain header**: magic bytes, `formatVersion` (int), engine/game version string, tick count, and a **content manifest** — an ordered list of `(modId, version, contentHash)`.
2. **A section table of contents**: each section is `(nameHash, byteOffset, byteLength, codec)`. A reader that does not recognise a section **skips it by length**. This single property buys most practical forward compatibility for free, and is the main argument for a framed container over one monolithic blob.
3. **Additive-only within a format version.** MemoryPack's default mode permits appending members but not reordering or deleting; its `VersionTolerant` mode permits deletion with the slot retired. That is exactly the discipline we want: append fields freely, never reorder, never reuse a retired slot.
4. **When a break is unavoidable**, bump `formatVersion` and write one function `Upgrade_7_to_8(SaveDocument)`. Register them in an ordered list and apply them in sequence. That list *is* the migration system; it becomes a framework only if it ever needs to be one.

**Policy through M3** (prototype, nothing shipped): refuse to load a save whose `formatVersion` differs, with a clear message, and regenerate the test fixtures. Writing migrations for a game nobody has played is pure waste. What must exist from day one is the *header and TOC*, because retrofitting those is what actually hurts.

**Missing content on load**: compare the manifest against loaded content, show the player the differences **before** loading, and let them proceed. Then unknown `defName`s resolve to the sentinel and their owners are dropped in phase 4; unknown union discriminators (e.g. a job driver type from an absent mod) end the job cleanly through the same path that already handles a driver whose toil sequence changed. Both cases produce a post-load report, not an exception.

### 9. The C# choice for a pure Sim assembly

Constraint: `Odyssey.Sim.asmdef` sets `noEngineReferences: true`, and the same sources compile as a plain `dotnet` class library for headless tests and CI. Unity 6's CoreCLR work is still constrained to .NET Standard 2.1, and Mono is only slated for removal around 6.8 — so **target .NET Standard 2.1** and take nothing that needs a newer BCL.

**Recommended: MemoryPack** (MIT, Cysharp).

- MIT; netstandard2.1 minimum; Unity 2022.3.12f1+ with IL2CPP support — comfortably inside Unity 6.3.
- An **incremental source generator**, so no `Reflection.Emit`; safe under IL2CPP and safe under a plain dotnet SDK.
- **Zero-encoding** design: near-`memcpy` for arrays of unmanaged structs, which is precisely our shape (chunk payloads, thing records). Reported roughly 10× faster than other binary serialisers for objects and 50–200× for struct arrays.
- Has the three features the reference model needs: **unions** (`[MemoryPackUnion]`) for polymorphic job drivers and comps, **circular reference** support, and **custom formatters** for our LoadId reference slots and def-table indices.
- Version-tolerance modes as in §8. The core package has no UnityEngine dependency; Unity-specific shims are a separate optional package.
- Caveats, both acceptable: the format is **not self-describing** (mitigated by our own header/TOC plus the canonical text dump), and it **assumes little-endian** (true of every x64 and ARM64 target we care about; assert it at load).

**Second choice, and the fallback if the source generator fights Unity's compilation pipeline: MessagePack-CSharp** (MIT, with a BSD-2 lz4net port and one Apache-2.0 file vendored). Self-describing-ish via integer keys, built-in `Lz4Block`/`Lz4BlockArray` compression, netstandard2.0, the same Unity floor. Slower on large struct arrays, and historically dependent on an AOT codegen step.

**Rejected:**

- **System.Text.Json source generators** — Unity integration is a known mess: duplicate precompiled `System.Text.Json.SourceGeneration.resources.dll` errors, source-generated files that Unity's compiler does not pick up, and unresolved netstandard references. Fighting all that to obtain a format we do not want is the worst of both worlds.
- **Unity's own serialisation** — requires UnityEngine, so it violates the no-engine rule outright; no reference-graph support; weak polymorphism.
- **Hand-written `BinaryWriter` for the whole graph** — RimWorld proves it is viable and it gives total control, but writing and maintaining 200+ symmetric expose methods by hand is exactly the cost we are trying to avoid.

**The hybrid we actually take**: the **container is ours**, hand-written with `BinaryWriter`/`BinaryReader` — magic, version, manifest, def table, section TOC, per-section codec, and the palette-chunked grid encoder. Only **section payloads** go through MemoryPack. That keeps the serialiser swappable (falling back to MessagePack changes payload encoding and nothing else), keeps the grid encoding under our own control where the size win lives, and keeps the header readable by any tool.

**Compression**: `K4os.Compression.LZ4` (MIT, pure managed, no native dependency; set `LZ4Codec.Enforce32 = true` on IL2CPP/ARMv7 targets for unaligned-access safety). LZ4 block per section for autosaves; `BrotliStream` at `CompressionLevel.Optimal` as an opt-in "compact save" for manual or shared saves. Never `SmallestSize`.

## Recommendation

**One committed design.**

**Format.** A single `.odysave` file: a hand-written binary container — 8-byte magic, `formatVersion`, game version, tick, content manifest, def-name table, then a section table of contents of `(nameHash, offset, length, codec)` — whose section payloads are serialised with **MemoryPack** (MIT) and compressed per section with **LZ4** via `K4os.Compression.LZ4` (MIT). Unknown sections are skipped by length. A `--dump-text` mode emits a canonical sorted text rendering of the same walk, for diffing only.

**Grid layout.** Cell grids are stored as **palette + bit-packed chunks of 25 × 25 × 5 cells** (3,125 cells; exactly 10 × 10 × 8 = 800 chunks over the map, no padding), with a single-palette-entry chunk storing no data array at all, and a dirty flag per chunk so autosaves rewrite only what changed. Cell index order within a chunk is **z-major, then y, then x**, so a layer slab is contiguous. Estimated **0.31 MiB for the material grid — 15.4× smaller than plain binary and ~85× smaller than per-cell XML**; ~0.5 MiB for all grids; **~4.2 MiB for the whole save uncompressed, ~1.6 MiB after LZ4**, with serialiser cost under 50 ms in each direction and post-load re-derivation (spatial index, region graph) as the real load-time budget at 200–600 ms.

**Reference model.** A monotonic `LoadId` counter, itself saved. Exactly one deep-writer per object; everywhere else writes the bare ID. Four phases — Write, ReadVars (register `LoadId → object`), ResolveRefs (patch pending slots; unresolvable IDs null out and are logged), PostLoad (validate, then rebuild every cache). Defs are referenced by a 2-byte index into a per-save `defName` table; def *contents* never enter a save. Dictionaries and sets are written as arrays sorted by key or LoadId, without exception.

**Mid-job persistence.** Save job + driver type tag + toil index + progress counters + job queue + guard counters + reservations (with layer) + posture and stance cooldowns + carried thing + need and thought timers + per-thing ticker phase offset + destination-only for paths. Discard and re-derive: the path node list, region/room graph, reachability, spatial indices, ticker lists, lighting, all presentation. Room temperature, gas and fire accumulators are state and are saved.

**The round-trip test** (the M0 gate, in this order):

1. **Byte stability** — save the same state twice, assert identical bytes.
2. **Round trip** — seed S to tick N, hash; save; load in a fresh process, hash; assert equal.
3. **Resume equivalence** — 60,000 ticks unbroken versus 30,000 → save → load → 30,000; assert final hashes equal. This *is* the headless one-day run, so the gate costs one extra run, not a new harness.
4. **Replay** — record the command stream, replay with per-tick hashes, binary-search the first divergence and diff the text dumps.

Hashes are xxHash64 over an ordered walk of saved state with floats as raw IEEE bits; caches are never hashed.

**Versioning.** Header `formatVersion` + skippable sections + additive-only fields now; an ordered list of `Upgrade_N_to_N+1` functions if and when a break happens; through M3, refuse mismatched versions and regenerate fixtures. Missing mods are reported from the manifest before load; unknown defs and unknown union tags degrade to sentinels and clean removal in PostLoad, never an exception.

## 3D/layer impact

- **Chunk shape is a 3D decision, and a thin one is right.** A 40-layer ruined city is vertically stratified — whole layers are uniform rock or uniform air — so a chunk spanning few z levels maximises the single-palette-entry case that costs 8 bytes. 25 × 25 × 5 divides 250 × 250 × 40 exactly, avoiding the padding waste a 16³ chunk would incur on a 250-wide map.
- **Layer-major ordering.** Store z-major so a layer slab is contiguous: runs are horizontal within a layer, the player thinks in layers, and a layer is the obvious unit for future streaming or a "save only the layers that changed" optimisation.
- **Vertical connectivity is derived; its causes are saved.** A stair or ladder is a thing and is saved; the region-graph edge it creates between z and z+1 is rebuilt in PostLoad. This keeps the save free of the most volatile 3D structure.
- **Everything positional carries z.** Reservations, job targets (`LocalTargetInfo` must be `(x,y,z)` or a thing ref, never a 2D cell), designations, zones, path destinations. A 2D cell type anywhere in the save schema is a bug that will be expensive later.
- **Load IDs must not encode position.** Things move between layers; an ID derived from a cell index would break on the first hauled item.
- **The 40× cell count does not threaten the save.** Palette-chunking plus dirty tracking makes grid cost proportional to *variety and change*, not to volume — the same conclusion a-15 reached for tick cost. What 40× does threaten is post-load re-derivation over 2.5M cells, which is why that is the number to measure at the M0 gate.

## Layer questions touched

- **Q12 (what the vertical slice must prove / can stub).** The slice must prove: the round-trip and resume-equivalence tests above passing over a one-day headless run; a pawn saved mid-haul resuming its toil at the right index; grid chunking and dirty tracking across all 40 layers; and the measured post-load re-derivation time. It can stub: incremental/delta autosaves (full saves are fast enough at this size), save migrations entirely, the replay recorder (Test D can arrive with M2), compression tuning beyond "LZ4, default settings", and any multi-map or world-level save scope.
- **Q10 (unit of simulation for gas, fire, water, sound).** Touched only obliquely: this file reinforces that per-chunk dirty bitsets are the right granularity for both grid simulation and grid persistence, and that room/region-derived quantities must be classified explicitly, per system, as state (saved) or cache (rebuilt).
- No other layer question is engaged by save and load.

## Sources

- https://spdskatr.github.io/RWModdingResources/saving-guide.html — RimWorld's single `ExposeData` pass, load-referenceable IDs, deep vs reference saving, Defs by name, mixed-mode collections
- https://rimworldwiki.com/wiki/Save_file — save structure; compressed grids for homogeneous fill
- https://minecraft.wiki/w/Chunk_format — 16³ section palette, bit-packed 64-bit index array, minimum 4 bits per index, single-palette-entry omits the data array
- https://minecraft.wiki/w/Anvil_file_format — region files, per-chunk compressed NBT
- https://www.factorio.com/blog/post/fff-47 — per-tick whole-map CRC plus a tagged save file every tick; binary-diffing the first divergent tick
- https://factorio.com/blog/post/fff-188 — deterministic lockstep and desync reporting
- https://github.com/Cysharp/MemoryPack — MIT; netstandard2.1 / Unity 2022.3.12f1+; source generator; zero-encoding; unions, circular references, custom formatters, version tolerance; not self-describing; little-endian assumption
- https://github.com/MessagePack-CSharp/MessagePack-CSharp — MIT; netstandard2.0; `Lz4Block`/`Lz4BlockArray`; source generator and AOT notes
- https://raw.githubusercontent.com/MessagePack-CSharp/MessagePack-CSharp/master/LICENSE — MIT, with a BSD-2 lz4net port and one Apache-2.0 file vendored
- https://github.com/MiloszKrajewski/K4os.Compression.LZ4 — MIT; pure managed; ~280 MB/s compress, ~520 MB/s decompress; `LZ4Codec.Enforce32` for ARMv7/IL2CPP
- https://blog.nimblepros.com/blogs/compression-benchmarks/ — measured .NET Brotli/GZip/Deflate/ZLib times and sizes
- https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1.getenumerator — enumeration order is undefined
- https://devblogs.microsoft.com/dotnet/floating-point-parsing-and-formatting-improvements-in-net-core-3-0/ — shortest-roundtrippable formatting from .NET Core 3.0; only 32 % of floats and 8 % of doubles round-tripped before
- https://discussions.unity.com/t/coreclr-and-net-modernization-unite-2024/1519272 — CoreCLR still limited to .NET Standard 2.1; Mono removal around 6.8
- https://github.com/KageKirin/System.Text.Json.UPM and https://discussions.unity.com/t/getting-source-generators-system-text-json-to-work-in-unity-2021-3/913558 — System.Text.Json/Unity friction: duplicate precompiled assemblies, source generators not picked up
- https://docs.unity3d.com/6000.2/Documentation/Manual/cus-asmdef.html — assembly definitions and `noEngineReferences`
- `docs/research/a-15-time-and-simulation.md` (this repo) — tick model, per-tick RNG reseed, save at tick boundaries, mid-job persistence, determinism hazard list

## Confidence

**High** on: library licences and capabilities (MemoryPack MIT, MessagePack-CSharp MIT, K4os MIT — all read from the projects' own documentation and licence files); the .NET compression throughput ordering; .NET's undefined dictionary and set enumeration order; the Minecraft palette-and-bit-pack scheme; RimWorld's save architecture and reference model; Factorio's per-tick-CRC-plus-tagged-save oracle.

**Medium** on: the size maths. The scheme comparisons (plain vs RLE vs palette-chunked) are arithmetic and sound, but the **run counts and chunk-uniformity fractions are my estimates of what a ruined-city map looks like**, not measurements — they depend entirely on how the template stamper fills the map. The 15× RLE win is robust to a factor of two or three in those estimates (a 40-layer map is overwhelmingly uniform under any plausible generator); the absolute 0.31 MiB figure is not. Likewise the 40,000-things and 5 KB-per-pawn figures are extrapolations.

**Medium** on: the load-time estimates, which use a deliberately conservative 200 MB/s for MemoryPack rather than its published figures, and a guessed 200–600 ms for post-load re-derivation. The claim that "the serialiser is not the bottleneck" is robust — it holds by an order of magnitude — but the re-derivation number is a budget to be measured, not a prediction.

**Low–medium** on: MemoryPack under Unity 6.3 specifically. The project states Unity 2022.3.12f1+ support, but Unity source-generator packaging is a recurring source of friction (the System.Text.Json experience is the cautionary tale), and no 2026-era report of MemoryPack under Unity 6.3 with IL2CPP was found. This is the single riskiest element of the recommendation, which is why the container is designed to make the serialiser swappable.

## Could not be determined

- Whether MemoryPack's source generator integrates cleanly with Unity 6.3's compilation pipeline and IL2CPP in practice. **Cheapest experiment that settles it:** an M0 spike that serialises one 3,125-cell chunk struct plus one polymorphic job-driver union inside the Sim asmdef, built once for the editor, once for IL2CPP, and once under a plain `dotnet test`. If it fails, MessagePack-CSharp substitutes with no change to the container design.
- MemoryPack's maintenance status and current release as of 2026; the documentation read here carries no date.
- Real measured RimWorld `.rws` sizes for a large late-game colony (community figures exist, but no primary source was found), so the XML contrast in §3 is my own extrapolation rather than a measurement.
- Going Medieval's actual save format, size and layer encoding — the nearest published comparable (Unity, 3D-layered colony sim). Public material covers save-file *location* and praises the system's design, but no technical description of the format was found.
- Whether Unity 6.3's CoreCLR path changes the BCL story enough to make System.Text.Json viable later; the roadmap material says .NET Standard 2.1 remains the constraint for now.
- Actual LZ4 and Brotli ratios on our specific payload (already-RLE'd grids versus dense thing records) — the cited ratios come from a text corpus and will not transfer; measurable only once a real save exists.
- Whether saving can be moved off the tick thread cheaply (snapshot-and-write) without copying the world; not investigated, and not needed while a full save costs ~15 ms.
