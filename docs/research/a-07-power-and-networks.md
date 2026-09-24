# a-07 — Power and networks

## Question

How does RimWorld's electrical power system work, as mechanics a reimplementation could follow: nets and connectivity, the per-tick balance and shortfall, the wood-fired generator, the heater (and electric stove), conduits and the overlay, short circuits and solar flares, and what players criticise. Clean room: mechanics, numbers and intent only.

## Findings

1. **Nets.** Transmitters are conduits (and the waterproof variant), power switches, batteries and **every generator** — batteries are explicitly "as if it were a power conduit". Transmitters connect only when **directly adjacent** (orthogonal, one layer — the game is 2D), and each connected set is one independent net in which "all points … have instantaneous access to all power generated and stored" (no distance loss, no capacity limit). A consumer is not a transmitter: it **auto-connects to one transmitter within 6 tiles** on placement, drawn as a visible cord, and a *Reconnect* button cycles it through other transmitters in range. A consumer therefore belongs to **exactly one net**; if its net loses power "the appliance will auto-seek a new connect" (player report).
2. **Balance.** Output and draw in **W**; storage in **Wd** (1 Wd = 1 W for one day). Each tick the net sums generation minus consumption. Surplus charges batteries at **50 % efficiency**, split evenly across them, with no rate limit either way; anything not stored is wasted. Batteries hold **600 Wd** and self-discharge **5 Wd/day**. A deficit is drawn from storage; when storage cannot cover it, **consumers lose power until demand falls below supply** — the wiki states the choice of which one is unknown and "may be random", and players describe devices switched off at random. The visible symptom is **flicker** as devices drop and return; the return rule is not documented.
3. **Wood-fired generator.** 1,000 W, 2 × 2, 100 steel + 2 components, 2,500 ticks of work, 300 HP, Construction 4, Electricity research. Holds **75 wood**, burns **22 wood/day** (3.27 days full), and **burn does not depend on load**. Switched off ("flicked") it burns nothing and makes nothing; flicking is a designation a colonist walks to. Refuelling is a **hauling-class job** (a separate *Refuel* column only under mods), with a toggle for auto-refuel and a target fuel level; players report pawns wait until it is nearly empty, so generators run dry briefly. Emits heat (**6** heat/s), light (3.44 tiles), beauty −20, flammability 100 %.
4. **Heater.** 1 × 1, 50 steel + 1 component, 1,000 ticks, 100 HP, Construction 5. Thermostat with a target (default 21 °C): **175 W** while heating, **17 W** idle at target; **21** heat/s when active. **Electric stove:** 3 × 1, 80 steel + 2 components, **350 W**, 3 heat/s while working, no idle draw listed.
5. **Conduits.** 1 steel, 35 ticks of work, 80 HP, flammability 70 %, beauty −2, nothing refunded; placeable **under walls and other buildings** (anywhere but unsmoothed rock and ore) and blocks nothing. **Hidden conduit** (added 1.5.4062): 2 steel, 280 ticks, 100 HP, invisible, untargetable, fireproof, no beauty penalty, **cannot short-circuit**. Selecting any transmitter or attached appliance shows *Grid excess / stored* in the inspector. The conduit overlay (the grid drawn through walls) appears while placing power buildings or conduits — from play knowledge, not sourced here.
6. **Short circuit ("Zzztt")**, record only: a random event on powered ordinary conduits (hidden and waterproof immune, 8-day minimum cooldown for the weather kind), plus unroofed electrics in rain or snow. It damages a conduit, **discharges all stored battery power**, and explodes with radius clamp(√stored × 0.05, 1.5, 14.9) tiles, adding a bomb blast above 3.5 tiles. **Solar flare**: 0.15–0.5 days in which every electrical device stops; fuelled generators keep running.
7. **Complaints.** Random conduit explosions are the loudest ("rubbing salt in the wound"); the 1.5 hidden conduit is effectively the developer's answer. Players cannot say which appliance sheds first, so shortfall is read as flicker and a stove interrupted; one missing conduit tile silently cuts a whole section; there is no priority — the workaround is hand-wired switches or separate nets; refuelling competes with all other hauling.

## Recommendation

The owner's decisions stand; nothing found argues against any of them, and two answer the reference's own complaints.

- **Burn in proportion to load — departs** (reference: flat 22/day). Keep 1,000 W and 75 capacity as the scale, and hold fuel in integer milli-units with a per-tick accumulator (22 wood/day over a 60,000-tick day does not divide evenly). Make the generator's heat proportional too, or a cold generator warms a room while burning nothing.
- **Conduits anywhere, including under walls and floors — matches**, and one hidden-style type is enough: with no short circuits there is nothing for a second variant to buy.
- **Vertical connection — departs** (the reference is flat). Use **6-neighbour adjacency** (four sides, above, below), so a stack of conduits is a riser. Keep the consumer's auto-connect **within its own layer** and shorter than 6 (6 of our 2.5 m cells is 15 m); roughly 3 cells, nearest first, ties broken by lowest cell index, so the result is deterministic. Only a conduit crosses a floor.
- **Whole net dark on a shortfall — departs**, and deliberately: it is the legible answer to the flicker complaint. The one trap: compute demand from consumers that are **switched on and want power**, not those currently powered, or a dark net sheds its demand, relights and oscillates exactly as the reference does. The heater's 175/17 W step means a net can go dark the moment a thermostat trips; that is correct but the interface must name the net and the shortfall.
- **Heater first — matches**: 175 W heating, 17 W idle, thermostat target. Map its 21 heat/s against the generator's 6 into design 28's energy units.
- **Auto refuel by haulers — matches**, but fix the known fault: refuel below an explicit threshold, up to a target, so a generator does not run dry waiting.
- **On/off switch — matches**; say whether it needs a colonist's visit (reference) or acts at once (simpler, and no job to write).
- No batteries or short circuits yet; the net should still keep a stored-energy term, even if always zero, so batteries slot in later.

## Sources

- https://rimworldwiki.com/wiki/Power
- https://rimworldwiki.com/wiki/Wood-fired_generator
- https://rimworldwiki.com/wiki/Power_conduit
- https://rimworldwiki.com/wiki/Hidden_conduit
- https://rimworldwiki.com/wiki/Heater
- https://rimworldwiki.com/wiki/Electric_stove
- https://rimworldwiki.com/wiki/Battery
- https://rimworldwiki.com/wiki/Events
- https://steamcommunity.com/app/294100/discussions/0/1815422173036325040/
- https://steamcommunity.com/app/294100/discussions/0/3317353727674990554/
- https://steamcommunity.com/app/294100/discussions/0/1735465524703920043/
- https://steamcommunity.com/app/294100/discussions/0/359543951727971866/
- https://steamcommunity.com/app/294100/discussions/0/1697221160915637568/

## Confidence

1 high (adjacency, 6-tile range, batteries transmit) · 2 high on units and batteries, **low** on shed order · 3 high on numbers, medium on refuel threshold · 4 high · 5 high on costs, low on the overlay trigger · 6 high · 7 medium (forum sample, not a survey).

## Could not be determined

- The shedding algorithm: whether it drops one consumer or several per tick, how it picks, and when a shed consumer comes back. The wiki itself flags this as unknown.
- The exact auto-refuel threshold and default target fuel level.
- Exactly when the vanilla conduit overlay is drawn (which designators or selections arm it); unsourced.
- Whether the 6-tile consumer range is Euclidean or square.
