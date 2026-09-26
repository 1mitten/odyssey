# 54 — The kit (gear unit G3)

**2026-09-26.** The owner, after playing the Gear tab (G1, design 47): *"I should be able to pick up
medical supplies into my inventory and use"*. The kit is the answer the gear interview already gave
(`docs/research/gear-interview.md` #8, #10, #15, #22): **a few small stacks a colonist keeps on her**,
taken by hand and spent where she stands. This document is G3 of `docs/plans/gear.md`.

Asked the same day and answered:

| Question | Answer |
|---|---|
| What "use" means | **Automatic, plus a Use button.** A doctor treats from her own kit before walking to the stores; a colonist who treats herself uses her kit first; and **Use** on a kit tile sends her to use it now. |
| How much of the kit | **G3 as planned**: the general kit, not medical supplies alone. Rations go in too. |
| Where it is built | A branch of its own, `claude/gear-kit`, stacked on G1 (PR #231). **Merge #231 first.** |

## 1. What a kit is

- **Two belt slots per colonist.** Four more arrive with the pack (G4), which is why the tab already
  draws six tiles with four locked.
- **One kind of thing per slot, up to that thing's kit cap.** The cap is content:
  `ItemDef.kitCap`, **0 for anything that never goes in a kit**.

| Item | `kitCap` | Why |
|---|---|---|
| Medical supplies (`Item_MedicalSupplies`, `ui.res.medkit`) | **5** | The owner's own example (interview #10). Five treatments is a fight's worth for one doctor. |
| Ration pack (`Item_Meal`, `ui.res.rations`) | **3** | The owner's own example. Three is a day and a half away from the pantry. |
| Everything else | 0 | Cooked meals, raw food, materials, weapons. A weapon is the hand's (interview #13), and a spare weapon in the kit waits for a use. |

- **Two of one kind are allowed**: a slot is full at its cap, and a second slot may start another
  stack of the same thing. Ten medical supplies in two slots is a choice the player makes — **with a
  second order**. One take lifts one slot's worth, so a stack of eight fills one slot and leaves the
  other for rations; the first build filled both from one stack, and the player lost the choice
  without being asked.
- **Size is not built yet.** The interview's *pocket / pack / arms* only decides which slots a
  thing may go in, and every slot is a belt slot until the pack exists. `kitCap > 0` is the whole
  of "it fits a kit" until G4, which adds `size` when there is a second kind of slot to refuse.

## 2. Where a kit thing is

**Exactly where a held weapon is**: an ordinary `ColonyItem` with `CarriedBy` = the colonist,
`Cell = -1` and `ContainerId = 0`. The colonist holds only its id, `Pawn.Kit[slot]`, as she holds
the weapon's in `EquippedItem`.

That one choice is most of the unit, because every search in the game already asks
`PawnContext.WhereIs` and skips `-1`: **no hauler, cook, eater, doctor, thief or bill can see a kit
thing**, with no new check anywhere. A kit is also on none of the item lists (`PickUp` unlists) and
publishes no `ThingView`, so the board never draws it.

- **`Kit.Held(pawn, slot)`** trusts the id only while the item agrees (`CarriedBy`, `Cell < 0`,
  `ContainerId == 0`, not despawned), exactly as `WeaponHand.Held` does. A slot whose thing has gone
  reads as empty rather than throwing — the fault fixed in PR #236 was this sentence missing.
- **The hand and the kit are separate.** A weapon is never in the kit and a kit thing is never
  `EquippedItem`. A haul load (`Job.CarriedItem`) is a third thing in the arms and is untouched.

## 3. The orders

Every order is refused for anybody but a standing colonist of ours (not downed; a downed colonist's
gear is reached only by **Strip**, G7). All three apply while paused, as Equip and Unequip do.

| Order | Intent | What happens |
|---|---|---|
| **Take into kit** | `OrderTakeIntoKit` — cell of the thing, `A` colonist, `B` thing id | A job, `Job_TakeIntoKit`: walk to the stack (on the ground or in a store) and lift **one slot's worth** (`Kit.TakeRoom`) — the room left in the slots of its kind, or, when those are full, one empty slot's cap — and leave the rest where it was. Refused with nothing to take or no room. |
| **Remove** and **Drop** | `OrderKitDrop` — `A` colonist, `B` slot, `C` 0 Remove / 1 Drop | **Instant**, like Unequip (design 47 §3), through the same door: the stack is laid at her feet, or the nearest cell that can take it. *Remove* leaves it for the haulers; *Drop* forbids it. |
| **Use** | `OrderUseKit` — `A` colonist, `B` slot | Medical supplies: she **treats herself now**, from that slot, with no walk. A ration: she **eats it now**. Refused if she is whole (supplies) or full (a ration), and the button says so rather than failing silently. |

**Where the orders come from** (interview #15):

- **Right-click** a stack on the board with one colonist selected: a *Take into kit* row per kind
  of kit thing under the pointer, beside the Equip row. Disabled with *Kit full* when there is no
  room.
- **The Gear tab**: an empty kit tile opens **Pick from stores**, which lists the stored kit things
  exactly as the weapon's list does; choosing one sends *Take into kit*. A filled tile opens its
  popover with **Use**, **Remove** and **Drop**.

The job is a new job def appended at the end of the job table (`Job_TakeIntoKit`, handle 28). **No
golden moved**, measured on all of them: a job above `JobSystem.HashedAlways` reaches the hash only
once one has run, and nothing in a golden run orders one. This document first said every golden
would move; the tiers said otherwise, and the tiers were right.

## 4. Spending it on her own

- **A doctor treats from her own kit first.** The three places that choose supplies — the Doctor
  work giver, self-treatment and the Tend order — ask one helper, `Medical.SuppliesFor(pawn)`, which
  returns her own kit's supplies before `NearestSupplies`. **The kit thing is the job's target and is
  never lifted**: the driver skips the fetch and the lift, needs no reservation (nobody else can see
  it), and at the end of the treatment spends **one** from the kit stack. An interrupted treatment
  therefore leaves the kit as it was, where a lifted unit would have been dropped on the floor.
- **Self-treatment keeps its rule** (bleeding, or under 60 % and a doctor cannot reach her, design
  37 and 43 §15); the kit only replaces the walk to the shelf.
- **A ration is the fallback, not the first choice.** A colonist eats from her kit only when the
  eat giver finds **no other food she can reach**. Eating it first would empty every kit every day,
  and with no loadout to top it up (G6) the player would refill it by hand after every meal; the
  point of a ration in the kit is the colonist who is somewhere without food. The Use button is how
  the player says "eat it now anyway".
- Both paths spend through **`Kit.Spend(pawn, slot, 1)`**, which clears the slot when the stack
  runs out. Nothing else writes `Pawn.Kit`.

## 5. Death, down and leaving

- **Downed:** she keeps it (interview #18). She cannot use it on herself; a doctor treating her uses
  the *doctor's* kit.
- **Dead:** the kit is laid beside the body with the weapon (`WeaponDropListener.Died`), **a
  departure from interview #18** ("the body keeps its gear; a Strip order drops it"): Strip is G7,
  and until it exists the only alternative is a kit nobody can ever reach. When Strip lands, the kit
  stays on the corpse and this line is reversed.
- **Leaving the board** (`PawnRegistry.Despawn`): laid at her cell, as the weapon is.

## 6. Save, hash and the snapshot

- **Save:** a keyed section, `odyssey.gear`, written only for colonists with a non-empty kit:
  pawn id, slot count, then the slot ids. A keyed section is skipped by a reader that does not know
  it, so **no format bump** (the rule `HealthSection` states). The things themselves are saved by
  `ColonyItems` with their `CarriedBy`, and `ColonyItems.Load` already puts a carried thing on no
  list.
- **Hash:** a hashable of its own, `KitLedger`, walks the colonists in id order and adds the
  non-empty kits — **not a bit in `Pawn.ContributeTo`'s flag word**. That word's last bit (27) went
  to the home area, and `docs/bug-patterns.md` records two branches taking the same bit of it; a
  component that adds nothing for an empty kit leaves every colony without one hashing as before.
- **Snapshot:** sparse pawn aspects, `odyssey.pawn.kit.<slot>` (the def) and
  `odyssey.pawn.kit.<slot>.count`, published only for a filled slot. The Hud mirrors the names, and
  a contract test holds the two copies together, as it does for the weapon.

## 7. The Gear tab

**As built (2026-09-26):**

- `KitOrders` (Hud) is the one builder for the three orders, the mirror of the aspect names, and
  the mirror of the caps (`Cap`, `TakeRoom`).
- **Use is published, not derived.** `Kit.UseOf` is the rule, and both the order and the snapshot
  ask it (`odyssey.pawn.kit.<slot>.use`, a `KitUseHandle`). So the button reads what the order will
  answer, and no second copy of "is she hurt" lives in the interface.
- A greyed Use shows **its reason as the button's words** ("Not hurt") rather than a line above the
  buttons, so the popover is one height whatever the reason.
- Pick from stores for the kit lists **stored** stacks only, with the count where a weapon's quality
  goes ("× 8"). A loose stack is reached by the right-click menu, as a loose weapon is.


- The kit row reads **the real kit** while the preview is off. *Preview full kit* still replaces
  the whole row with the made-up one, as it replaces the worn slots, so the tab can be judged either
  way.
- The Hud knows which things fit a kit and their caps from a mirror, `KitItems.Cap(def)`, held to
  `ItemDef.kitCap` by a test that walks the whole item table — the pattern `CombatOrders.IsWeapon`
  uses, because the Hud cannot read the Defs.
- **Use** is drawn only for a thing that has one (medical supplies, a ration) and is disabled with
  its reason (*Not hurt*, *Not hungry*). **Remove** and **Drop** mirror the weapon's two words.

## 8. What is not built

- **Pack slots** (G4), **loadouts and topping up** (G6), **Strip** (G7).
- **The trip cap** (G2) — unrelated to the kit and not needed by it.
- **Pouches drawn on the belt** (interview #17): presentation, after the pack's back socket exists.

## 9. What not to undo by tidying

- **The kit thing is never lifted to be used.** Splitting one off and carrying it is how the
  stores' supplies are spent, and it is what drops a unit on the floor when a treatment is
  interrupted. The kit spends in place.
- **The ration is a fallback.** Moving it to "first" looks like the plan's sentence and empties
  every kit daily.
- **The hash is a component, not a flag bit** (§6).
