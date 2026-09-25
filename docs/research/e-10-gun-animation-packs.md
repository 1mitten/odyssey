# Gun animation packs for the Synty rig
*Lane E (Synty asset fit) · 2026-09-25 · capped at 12 searches and 10 reads (12 searches and 10 reads used; 9 of the 10 reads were blocked by the container's egress proxy, so store pages are known only through search snippets)*

> *Coordinator's note, 2026-09-25:* every store page was refused by the container's proxy; prices and clip lists are from search snippets. The owner chose procedural-first with a clip seam (design 47 §1, question 6); the retarget experiment here is the gate before any purchase.

## Question
Which animation packs ship pistol draw, aim, fire, recoil and holster clips that retarget cleanly to the Synty humanoid rig, and on what licence and at what price?

## Findings

1. **Synty sells no gun animation pack.** The Synty ANIMATION collection is six packs — Base Locomotion, Idles (sold as "Character Idle & Motion"), Sword Combat, Bow Combat, Emotes and Taunts, and Goblin Locomotion — and the "ANIMATION Complete Collection Bundle" is those six. Three searches aimed at syntystore.com for pistol, rifle, gun, shooter, military or "modern weapons" animation returned only *art* packs that contain weapon meshes (Military, War, Police Station, Sci-Fi Worlds). [syntystore.com/collections/animation; syntystore.com/products/animation-complete-collection]

2. **Synty's own packs are authored on the Synty rig, per clip, with root-motion twins.** Sword Combat is 105 FBX files, one animation per file, "including RootMotion versions", with transition clips into Base Locomotion and an example controller; the Idles pack says its motions are made for "standard POLYGON characters, POLYGON Kids, and POLYGON Big Rig". So the two packs the project owns need no retarget, and any third-party pack is the first thing that will. Per-pack prices could not be read (store blocked); the store's subscription is "from $30 USD/mo" for the whole library of 150+ packs. [syntystore.com/products/animation-sword-combat; syntystore.com/products/animation-idles; syntystore.com/products/simple-military-cartoon-assets]

3. **POLYGON Battle Royale ships no animations**: "characters set up with Mecanim with no animations included" (509 assets, 15 characters). Nothing found either way for Sci-Fi City, Military or Apocalypse, but the Battle Royale wording is the standard Synty art-pack sentence and the project's own catalogue already records both owned character packs as clip-free. [syntystore.com/products/polygon-battle-royale-pack]

4. **Mixamo's licence is the friendliest: free, royalty-free, commercial games allowed, no redistribution as standalone files.** Adobe's FAQ (via snippets and Adobe-staff replies on the community forum): the auto-rigger, characters and animations may be used "royalty free for personal, commercial, and non-profit projects, including creating video games"; the one prohibition is redistributing them as standalone assets or reselling them (and bulk-downloading for machine learning). Team members of one project may share downloaded files; customers and non-team members may not receive them. Consequence for Odyssey: Mixamo FBX may sit in a **private** repository, but if the repository is ever public the raw FBX is a standalone redistribution — put it beside `Assets/Synty/` under the same gitignore rule. [helpx.adobe.com/creative-cloud/faq/mixamo-faq.html; community.adobe.com "mixamo faq licensing royalties ownership"; community.adobe.com "mixamo character and animation use"]

5. **Mixamo's pistol family is real but only partly verified.** One project's import log names six Mixamo pistol clips as PistolIdle, PistolJump, PistolStrafeL, PistolStrafeR, PistolBack (their renames of Mixamo's "Pistol Idle", "Pistol Strafe" etc.). A repository holding thumbnails of ~1,447 Mixamo clips shows, in the fraction of the listing that was returned, "Aim Pistol (aim pistol while sitting)", "Aiming (turning around to aim gun)", "Aiming Gun (picking up gun from ground to aiming)", and a rifle locomotion set ("Backwards Rifle Run/Walk", "Block With Rifle"). Mixamo also offers a free "Pro Rifle Pack". **No Mixamo pistol *draw*, *holster* or *reload* clip name was verified**, and Mixamo has no aim-offset pose set — every clip is a whole-body performance. [github.com/ajithgopi/deamland-3d/pull/62; github.com/RobertRosic/Mixamo-Gif-Thumbnails; instructables "Bullet Time Project"]

6. **Mixamo onto Synty: reported leg stretch and foot slide.** A GameDev.tv thread on "perfect retargeting results" says Synty characters "sometimes experience slight stretching of the legs or feet sliding when applying Mixamo animations". Mixamo's FBX has no separate root bone — the armature root *is* `mixamorig:Hips` — so root motion lives on the hips and Unity's Humanoid importer settings ("Root Transform Position (Y)" based on Feet, "Bake Into Pose") do the work; the site's "In Place" checkbox exists for locomotion only. [community.gamedev.tv "Working with Mixamo, getting perfect retargeting results"; community.adobe.com "force mixamo to use hips bone instead of armature as root"; discussions.unity.com "How can I specify a root bone"]

7. **MoCap Online Pistol Pro is the most complete paid set: $150, 372 animations, 868 files, Humanoid.** Idles, walks, jogs, runs, crouches, **aim offsets**, shooting, turns, jumps, split jumps, deaths, transitions; explicit "transitions to swap between Pistol and Rifle as well as holstering the Pistol to transition to the Mobility Pack with no weapon" — so draw/holster exist as transitions. "True Root Motion (moving reference root)" is offered as an option beside in-place, and "all animations are set to Humanoid". Clip naming seen in an indexed list is of the form `W1_Stand_Aim_Idle`, `W1_Stand_Aim_L_90`, `W1_Stand_Aim_R_90`, `W1_Stand_Aim_Jump`, plus turn loops and aim-offset clips. Cheaper siblings: Pistol Basic (same families, smaller) and Pistol Starter, which is also given away free on itch ("Free Pistol Animation Pack", with aim offsets and split jumps); their prices and the free pack's licence could not be read. [assetstore.unity.com/packages/3d/animations/pistol-pro-mocap-animation-pack-46984; mocaponline.com/products/unity-pistol-pro; mocaponline.com/products/unity-pistol-basic; mocaponline.com/products/unity-pistol-starter; mocaponline.itch.io/free-pistol-animation-starter-pack]

8. **Kubold Pistol Animset Pro: 150 mocap clips plus aim-offset poses, Mecanim Humanoid, FBX sources included; price not read.** Walking, running, crouching, shooting, getting hit, grenade throw, "aiming with additive animations", melee, sprint, platformer jump; built to pair with Rifle Animset Pro (asset 15098) or stand alone; a v2.5 update added clips; Kubold publishes a per-clip description page. Sold on the Unity Asset Store (asset 15828) and Fab, so the Unity Asset Store standard EULA applies (per-seat, embed-only, no redistribution — the same handling as `Assets/Synty/`). [assetstore.unity.com/packages/3d/animations/pistol-animset-pro-15828; kubold.com/s/PistolAnimsetPro_AnimationsDescriptions.html; unrealengine.com/marketplace/en-US/product/pistol-animset-pro; owler.com report on "Pistol Animset Pro v2.5"]

9. **Opsive Omni Animation – Pistol Pack: $92, v1.0.1, Unity 2021.3+, "configured for Humanoid retargeting", updated 2026.** 131.5 MB; part of Opsive's Omni system with a separate Core Locomotion Pack (asset 286945) and a WebGL demo. The clip list was not obtainable, and the pack is documented as an integration for their Ultimate Character Controller, which the project does not use. [assetstore.unity.com/packages/3d/animations/omni-animation-pistol-pack-276060; opsive.com/support/documentation/ultimate-character-controller/integrations/omni-animation-packs/; opsive.com/demos/OmniAnimationPistolPack/]

10. **Other names that surfaced, unverified:** "Pistol Animations 3D" (Conquerror, asset 247480); "Animated Modern Weapons + Arms" (Indiegamemodels, asset 27253 — first-person arms and props, not a full-body humanoid set); "ALL IN ONE PISTOL PACK ANIMATIONS" (asset 111839); Rokoko's "11 free gun animations"; "Human Soldier Animations" on itch (kevdev). **No Kevin Iglesias pistol set appeared in any result** — his catalogue as indexed is melee, locomotion and emotes. [assetstore.unity.com/packages/3d/animations/pistol-animations-3d-247480; assetstore.unity.com/packages/3d/props/animated-modern-weapons-arms-pistols-shotguns-and-snipers-sfx-27253; rokoko.com/resources/rokoko-mocap-11-free-gun-animations; kevdev.itch.io/human-soldier-animations]

11. **What a pack buys that a procedural stance cannot easily fake** (reasoned from the clip families above and the project's own posing code): the *draw* — a hand path that goes to the hip, closes, clears the holster and rotates up into a two-handed grip while the torso counter-rotates and the weight moves to the back foot; the *reload*, which is a hand-to-hand relation (off hand to a pouch, to the magazine well, to the slide) that a two-bone IK solver has no target for unless somebody authors the way-points; hit reactions and deaths; and the small weight shift that makes an aim-idle read as a person rather than a mannequin. **What procedural does better**: aiming at *any* point (aim-offset sets are a handful of poses blended on two axes and stop at their outer pose), per-shot recoil (an additive kick with a random amplitude, decaying over a few frames — Kubold's "additive" aiming is the pack-side version of the same idea), and fitting the off hand to the grip across the **61 rigs whose arm lengths vary** — the project already measured that variance to be larger than the carry cradle (`24-carrying.md`), so a retargeted two-handed grip will float on some rigs whatever pack it came from, and the fix is the same two-bone IK to a grip socket either way. [project: `docs/design/24-carrying.md`; findings 7–8]

12. **Import gotchas specific to this project.** (a) The sim owns position, so every clip must run in place with root motion discarded; MoCap Online ships in-place with root motion optional, Synty ships both twins, Kubold's set is mocap and its jumps carry Y motion that must be baked into pose. (b) The Synty avatar maps `HumanBodyBones.Hips` to a floor-standing bone called `Root` (P11 in the project's own bug catalogue); Humanoid retargeting normalises a clip by the *destination* avatar's hip height, so a clip authored on a 1.8 m mocap rig may land on a Synty character with the sole off the floor or the legs stretched — which is exactly the symptom the GameDev.tv thread reports for Mixamo on Synty (finding 6). Synty's own packs never showed this because source and destination were the same rig. (c) No pack knows where the Synty pistol's grip is: the gun parents to the right-hand bone with a per-weapon authored offset, and the muzzle/grip markers must be added on the project side. (d) Mixamo has no root bone; MoCap Online and Kubold do. [project `CLAUDE.md` P11 note; findings 2, 6, 7, 8; mocaponline.com "Skeleton Hierarchy: Why Your Retargets Keep Breaking"]

## Recommendation

Ranked:

1. **Do not buy yet. Use Mixamo (free, commercial-safe) for the draw, holster and aim-idle on an upper-body mask over Base Locomotion, and do fire, recoil and aim pitch procedurally** — an additive recoil kick and a spine-plus-arm aim chain, which the project's `WorkSwing`/`WorkStyle` pattern already models. The five clips the question names are two authored gestures (draw, holster), one held pose (aim) and two things procedural does better (fire, recoil). Mixamo's pistol draw and holster were *not* verified by name, so the first hour is checking they exist; "Aiming Gun (picking up gun from ground to aiming)" is the only gun-transition confirmed.
2. **Kubold Pistol Animset Pro** if a purchase is wanted: the smallest full set (150 clips), Humanoid, FBX sources, additive aim offsets, hit reactions, per-clip documentation; price unread, historically the cheapest of the three paid packs.
3. **MoCap Online Pistol Pro** ($150): the most complete and the only one with holster transitions confirmed in the listing, but 372 clips of which Odyssey would use perhaps a dozen — its bulk is locomotion the project already has from Base Locomotion. Its free Pistol Starter is the cheaper way to test the same rig.
4. **Opsive Omni Pistol Pack** ($92): clip list unobtainable and tied to a controller the project does not use. Not recommended.

**Say it plainly:** no pack is worth buying over the procedural route *until one observation is made*: whether a third-party Humanoid clip stands on the floor on a Synty character. Findings 6 and 12(b) say it may not, because of the Hips-to-`Root` mapping. **The cheapest experiment**: download one free pistol clip (Mixamo "Pistol Idle", or MoCap Online's free Pistol Starter), retarget it onto three of the 61 Synty characters at the extremes of `FigureBuild`'s measured heights, and read the sole height with `MeasureSole` and the off hand's distance to the right hand. If the sole is within a couple of centimetres and the feet do not slide, packs are viable and Kubold is the buy; if not, every pack needs a fix-up avatar and the procedural route wins outright. That experiment also answers whether the draw's hand path survives retargeting, which is the one thing the pack is being bought for.

Whatever is chosen, third-party FBX goes under a gitignored folder with the same rules as `Assets/Synty/` — the Asset Store EULA and Mixamo's terms both forbid standalone redistribution — and the simulation and its tests must not depend on it, as they already do not on Synty's.

## Sources
- https://syntystore.com/collections/animation
- https://syntystore.com/products/animation-complete-collection
- https://syntystore.com/products/animation-sword-combat
- https://syntystore.com/products/animation-base-locomotion
- https://syntystore.com/products/animation-idles
- https://syntystore.com/products/polygon-battle-royale-pack
- https://syntystore.com/products/polygon-military-pack
- https://syntystore.com/products/simple-military-cartoon-assets
- https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html
- https://community.adobe.com/questions-696/mixamo-faq-licensing-royalties-ownership-eula-and-tos-589400
- https://community.adobe.com/questions-696/mixamo-character-and-animation-use-590037
- https://community.adobe.com/questions-696/force-mixamo-to-use-hips-bone-instead-of-armature-as-root-589640
- https://github.com/ajithgopi/deamland-3d/pull/62
- https://github.com/RobertRosic/Mixamo-Gif-Thumbnails
- https://community.gamedev.tv/t/working-with-mixamo-getting-perfect-retargeting-results/205909
- https://discussions.unity.com/t/how-can-i-specify-a-root-bone-mixamo-blender-unity/244730
- https://mocaponline.com/blogs/mocap-news/skeleton-hierarchy-animation-guide
- https://assetstore.unity.com/packages/3d/animations/pistol-pro-mocap-animation-pack-46984
- https://mocaponline.com/products/unity-pistol-pro
- https://mocaponline.com/products/unity-pistol-basic
- https://mocaponline.com/products/unity-pistol-starter
- https://mocaponline.com/pages/animlist/pistol-pro
- https://mocaponline.itch.io/free-pistol-animation-starter-pack
- https://assetstore.unity.com/packages/3d/animations/pistol-animset-pro-15828
- https://assetstore.unity.com/packages/3d/animations/rifle-animset-pro-15098
- https://www.kubold.com/s/PistolAnimsetPro_AnimationsDescriptions.html
- https://www.unrealengine.com/marketplace/en-US/product/pistol-animset-pro
- https://www.owler.com/reports/kubold/kubold-posted-a-video--pistol-animset-pro-v2-5---m/1449869288653
- https://assetstore.unity.com/packages/3d/animations/omni-animation-pistol-pack-276060
- https://opsive.com/support/documentation/ultimate-character-controller/integrations/omni-animation-packs/
- https://opsive.com/demos/OmniAnimationPistolPack/
- https://assetstore.unity.com/packages/3d/animations/pistol-animations-3d-247480
- https://assetstore.unity.com/packages/3d/props/animated-modern-weapons-arms-pistols-shotguns-and-snipers-sfx-27253
- https://www.rokoko.com/resources/rokoko-mocap-11-free-gun-animations
- https://kevdev.itch.io/human-soldier-animations

## Confidence
**Low to medium.** Every store page (Synty, Unity Asset Store, MoCap Online, Adobe, Rokoko, itch) was blocked by the container's proxy, so prices and clip families come from search-engine snippets rather than the pages; only the GitHub read was first-hand. The Synty "no gun pack" and "Battle Royale ships no animations" findings are consistent across several snippets and the project's own catalogue, and the Mixamo licence is corroborated by Adobe staff on the forum; the retarget-onto-Synty risk rests on one forum report plus the project's own P11 measurement.

## Could not be determined
- Per-pack prices on the Synty Store (Base Locomotion, Idles, etc.) and the Complete Collection bundle price.
- Whether POLYGON Sci-Fi City, Military or Apocalypse ship any clips (Battle Royale confirmed none; the others were not returned).
- Mixamo's exact pistol clip names beyond Pistol Idle / Strafe L / Strafe R / Back / Jump and the three "Aiming" clips — specifically whether a pistol **draw**, **holster** or **reload** exists, and whether a "Firing Pistol" exists as a separate clip.
- Kubold's price and its per-clip list (the descriptions page exists but was not readable); whether its clips are shipped in-place or root-motion.
- The Omni Pistol Pack's clip list.
- MoCap Online Pistol Basic and Starter prices, and the licence on the free itch starter.
- The pitch range of any aim-offset set.
- Whether Adobe has announced any change to Mixamo's status (not searched within the cap).
- Any first-hand report of a MoCap Online or Kubold clip on a Synty rig — the only retarget report found is Mixamo-to-Synty.

## Layer question 5: can you shoot up and down?
Two packs answer yes in principle: MoCap Online's Pistol Pro, Basic and Starter all list "aim offsets" (yaw clips such as `W1_Stand_Aim_L_90` / `R_90` were seen; pitch clips are implied by the family but not seen by name), and Kubold's Pistol Animset Pro ships "Aim Offset poses" with additive aiming. Mixamo has no offset set (whole-body clips only) and Synty's own packs have nothing gun-shaped at all. Nothing found says how far up or down either set reaches; in Odyssey's geometry a target one 3 m layer up at 2.5 m range is roughly 50° of pitch, and the same going down, which is near or beyond the outer pose of a typical offset set, so a layered shot will need a procedural spine-and-arm aim (or IK on top of the pose) whichever pack is bought — which is the same conclusion as the recommendation above.
