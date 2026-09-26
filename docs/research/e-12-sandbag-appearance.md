# e-12 — What a sandbag wall looks like

**Asked 2026-09-25** by the owner on the first look at design 53's placeholder boxes: *"the sandbags
need to look like sandbags. Please search the internet to understand how a sandbag looks."* One
subagent, capped at 8 searches and 10 reads.

## Question

What does a military or flood sandbag emplacement physically look like, in enough concrete detail to
model it procedurally as a low-poly mesh in the Synty style?

## Findings

**A single bag.**

1. The standard bag is **14 × 26 in (36 × 66 cm)** empty; larger ones run to 17 × 32 in.
2. Flood bags are filled **one-half to two-thirds**, military bags **about three-quarters**. At 75 % a
   bag is **about 10 × 15 × 5 in (25 × 38 × 13 cm)**, and 18 in of sandbags is four courses, so a
   tamped course is 11–13 cm.
3. The filled shape is a **flattened pillow, roughly 2 : 3 : 1** (width : length : height). The top
   and bottom are nearly flat because each bag is tamped or walked on, the long sides bulge, and every
   corner is rounded, in plan and in section.
4. **The two ends differ.** One is the sewn bottom, its corners tucked in, and reads as a rounded box
   end. The other is **choked**: gathered and tied with an attached string, or folded under. It tapers
   to a neck about 40–60 % of the body's width.
5. The fabric is woven polypropylene or acrylic, or older hessian (burlap), with the side seam along
   one long edge.

**Colour.**

6. Military bags are **olive drab** and **desert tan** (FS 33446). The civil flood bag is white
   polypropylene, and hessian is a warm mid-brown. The hex values below are estimates, not measurements:

   | Colour | Hex |
   |---|---|
   | Desert tan | about `#C2A67A` |
   | Hessian | about `#A88A5E` |
   | Olive drab | about `#4B5320`–`#5A5C38` |
   | White polypropylene | about `#E6E2D6` |

7. Weathering fades a bag towards grey-beige and stains the lower courses. No source describes the
   look; it is the usual one.

**Stacking.**

8. **Running bond:** each course is staggered half a bag against the one below, as bricks are.
9. A military revetment alternates **stretchers** (long side to the face) and **headers** (end to the
   face), and **the top course is all headers**. Seams and choked ends are turned inward, away from
   the exposed face.
10. The face is **battered** at 1 : 4 rather than vertical, so each course steps back about a quarter
    of its height.
11. A flood dike is a pyramid whose base is three times its height (two times at least).
12. A fighting-position parapet is at least 1 m thick. A waist-high wall is about seven real courses
    and a chest-high one nine or ten.

**What reads as "sandbags" from a distance** (judgement, not sourced):

13. These cues, in rough order of how much they carry:
    1. the **scalloped top edge**, one hump per bag;
    2. **dark grooves between bags** in both directions, which the rounded corners make without any
       texture;
    3. the **half-bag stagger**, which is what separates it from a stone wall;
    4. a slight batter;
    5. a little irregularity in size, turn and colour. Identical bags read as tiles or pipes.

    Commercial low-poly kits sell straight, curve, end and corner pieces. **No sandbag asset exists in
    the local `Assets/Synty`.**

## Recommendation

Draw the bags, not a wall. Adopted in design 53 §7a-bis:

- **One procedural bag mesh**, `SandbagMesh`, beside `PillowMesh`: a flattened rounded pillow at
  2 : 3 : 1, with a pinched, tied end.
- **Instanced one bag at a time.** At the game's scale (a colonist drawn 2.5 m tall, about 1.4 times
  life) the bags are **exaggerated for readability** at the play camera rather than kept to life
  size. Five courses make the wall's 1.3 m:
  - four courses of stretchers, **two rows deep**, laid in **running bond in world coordinates**, so
    a dragged line is continuous across cells;
  - a top course of **headers**.
- **A batter** of a few centimetres a course.
- **Per-bag jitter** in size, turn and one of three desert-tan and hessian shades.

## Sources

- https://www.globalsecurity.org/military/library/policy/army/fm/5-103/CH3.HTM
- https://infantrydrills.com/manuals/fm-atp-3-21-8-infantry-rifle-platoon-squad-2024/defense/fighting-position-construction/
- https://www.ndsu.edu/agriculture/extension/publications/sandbagging-flood-protection
- https://en.wikipedia.org/wiki/Sandbag
- https://www.daybag.com/polypropylene-sandbag
- https://www.sandbagstore.com/products/acrylic-sandbags (search excerpt only)
- https://www.chasetactical.com/intel/od-green-vs-other-military-colors (search excerpt only)
- https://www.spa.usace.army.mil/Portals/16/docs/emergencymgmt/2010-EM-Flood_Fight_Manual.pdf (search excerpt only)
- https://www.cgtrader.com/3d-models/military/other/sandbags-wall-construction-kit (search excerpt only)
- https://syntystore.com/products/polygon-war-pack

## Confidence

| Findings | Confidence |
|---|---|
| Bag sizes and fill fractions (1–2) | **High.** Several official sources agree. |
| The 75 % filled size (2) | **Medium.** One source, corroborated by the course count. |
| Shape and proportions (3–4) | **Medium.** The choked end and tamping are sourced; the ratios are inferred. |
| Fabric (5), colour names (6), stacking (8–12) | **High.** |
| Hex values (6) | **Low to medium.** |
| Weathering (7) and the silhouette cues (13) | **Low to medium.** Judgement. |

## Could not be determined

- The tamped thickness of a half-filled flood bag.
- An official course count for a waist-high or chest-high wall.
- Measured fabric colours, as against paint standards.
- How Synty's own sandbag props are built; the pack is not in this repository.
