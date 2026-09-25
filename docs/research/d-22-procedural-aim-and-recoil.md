# A procedural pistol stance: aim, both hands on the grip, recoil, draw and holster
*Lane D (Unity architecture and feasibility) · 2026-09-25 · capped at 12 searches and 10 reads (12 searches and 10 read attempts used; 9 of the 10 reads were refused by the network proxy, so those findings are from search summaries, not full pages)*

> *Coordinator's note, 2026-09-25:* nine of ten page reads were refused by the container's proxy; the starting numbers in the recommendation are labelled as extrapolation and design 47 §4 carries them as INVENTED.

## Question

How is a convincing procedural two-handed pistol stance — draw from the hip, aim at a world target with a visible aim time, recoil on the shot, holster — built on a Unity humanoid with no gun clips, and what numbers do practitioners use? The project already has a PlayableGraph writing bones, a post-graph additive pose pass, a two-bone `Grasp` IK for tool grips, an upper-body mask layer for draw/sheathe, and a rule that props are placed absolutely each frame. The camera is a 48° slice camera at 20–160 m, so the pose must read in silhouette.

## Findings

**Aim: rotating the torso toward a target**

1. Unity's own upper-body aim rig is Multi-Aim constraints on the spine chain with *increasing* weight toward the head — spine 0.35, chest 0.5, upper chest 0.75, head 1.0 — so the rotation is shared down the chain rather than taken by one bone, plus an Override Transform that redirects a dedicated weapon bone to a source object; the same combination "affects the spine section as well as the hips". [Unity blog, *Advanced Animation Rigging: character and props interaction*, via search summary]
2. A second published recipe distributes the yaw across three spine bones as *fractions that sum to one* (chest 0.3, spine1 0.4, spine 0.3) with a separate head aim at 0.8–1.0 so the character also looks at the point. Either scheme puts roughly a third of the twist in each spine bone and lets the head take the remainder. [MoCap Online, *Unity Animation Rigging guide*, via search summary]
3. Hand-rolled solves do the same thing without the package: compute the delta rotation between the gun's forward and the vector to the target, apply it to the spine bones after the graph has written them (in `LateUpdate`, or here the post-graph pass), iterate a few times because rotating the spine moves the gun, and clamp per bone. Two threads and a cookbook chapter describe it; the recurring pitfall is aiming the *bone's* axis rather than the *gun's* barrel, since the two are never aligned. Naughty Dog's alternative — a static aim pose blended additively — is named as the other route. [GameDev.net *Rotate character spine so weapon aims at target*; Unity Discussions *Trying to rotate spine bones to aim weapon at target*; O'Reilly Unity 5.x Cookbook, all via search summaries]
4. Spine writes must happen after animation has written the frame — "in LateUpdate rather than Update" — or the graph overwrites them. This is the same ordering rule the project already has for its additive shapes. [Unity Discussions *Spine rotation based off camera*, via search summary]
5. Animation Rigging *can* be hosted in an externally owned graph: `RigBuilder.Build(PlayableGraph)` "builds the RigBuilder Playable nodes in an external PlayableGraph", `SyncLayers` updates constraints before evaluation, `Evaluate(float)` drives it manually, and the changelog records fixes for characters "without a controller". It still needs an `Animator` on the character and its constraints run *inside* the graph as animation jobs, i.e. before any post-graph C# pass. [Animation Rigging 1.2 `RigBuilder` API and changelogs, via search summary]
6. Aim limits are the anti-flip mechanism: when the target goes behind, a Multi-Aim constraint with no limits whips the bone through 180°; the fixes quoted are setting the aim axis to the axis that actually leaves the face/barrel, a Min/Max limit of about ±80°, and a stable World Up (scene up or a reference object) so roll does not wander. [Bugnet, *Multi-Aim constraint snapping the head 180 degrees*, via search summary; Multi-Aim Constraint manual]
7. Unreal's third-person aim offset uses yaw and pitch axes of −90..90 but clamps them in practice, explicitly so "the legs could [not] be pointed forwards while the character turns all the way around", and bends the spine with per-bone Transform (Modify) Bone nodes. Same distribution idea, same reason for a clamp. [Epic docs *Creating an Aim Offset*; Epic forums *Limiting aim offset*, via search summaries]

**Both hands on the grip**

8. The standard two-hand gun set-up is four empties — `L_Hand`, `R_Hand`, `L_Hint`, `R_Hint` — with a Two-Bone IK per arm (shoulder root, forearm mid, hand tip), the hand targets *parented to the gun* as `Hand_Position_R/L` sockets, and the hand IK targets following those sockets through a SmoothDamp so the hands stay on when the gun pitches. Hints "should have their transforms positioned at the back and closer to the floor" — behind the elbow line and below it. [Bergstrand, Killman, Ansley and Onur Medium articles on IK weapon systems in Unity, via search summaries]
9. Kinemation's shooter pack drives the hands from a *virtual* bone, `ik_hand_gun_additive`, "not a native bone but added as an empty GameObject", which procedural layers (recoil, sway, aim) move through their own avatar mask; both hands are IK'd to sockets on the gun, and draw/holster clips are tagged so the IK knows when to let go. This is the pattern of "one gun transform is the truth, both hands chase it". [Kinemation *Tactical Shooter Pack — Character*, via search summary]
10. The two real stances differ exactly in the arm geometry. **Isosceles**: chest square to the target, both arms driven straight out (locked or nearly), gun at the centre of the chest, "an equal triangle when viewed from above". **Weaver**: body bladed, strong arm slightly bent, support arm bent much more with "the elbow pointed almost directly at the ground", a push–pull grip. Isosceles is described as recoil travelling "straight back through locked bone structure into the core". [Infinity Targets; Wikipedia *Isosceles stance* and *Weaver stance*; NRA blog; TacticalHyve, via search summaries]

**Recoil**

11. Procedural recoil in every source is an *impulse into a spring–damper*: on the shot an offset is added instantly to position (back) and rotation (pitch up, with a little random yaw/roll), and every frame it springs back to rest — "a snappy kick followed by a smooth recovery". Parameters are stiffness, damping, mass and initial velocity, per axis (pitch, yaw, roll, plus a vertical/backward offset), the same mechanism as a car's suspension. [gameidea *Adding recoil and impact*; Fab *Procedural Recoil Animation* listing; CryEngine *Procedural Weapon Animations*, via search summaries]
12. The common Unity script shape is `Vector3(recoilX, Random(-recoilY, recoilY), Random(-recoilZ, recoilZ))` with two rates — a *snappiness* toward the target rotation and a *returnSpeed* pulling the target back to zero — i.e. two chained exponential smoothings rather than a true spring. No numeric values were found in the returned summaries. [Unity Discussions *Simple weapon recoil script*; GitHub `Weapon-Recoil-Script-Unity`; unirx-examples, via search summaries]
13. The only *measured* recoil timings found are for CS2 rifles, captured frame by frame: the gun's recoil animation lasts **644 ± 5 ms** (AK-47) and **353 ± 5 ms** (M4A1-S), peak view kick **3.47°** and **1.61°**, and accuracy resets **867 ms** / **542 ms** — "the gun stops moving a couple of hundred milliseconds *before* it is accurate again". The port derives the spring's frequency and damping from a single `recoil_animation_time` so one number controls how long the kick lasts, and steps it with exact exponential damping at 128 Hz substeps. [GitHub, sidcarrollworks/CSGODOT PR #6, full read]
14. Third-person advice: recoil is an **additive** layer (delta from a reference pose) so it works over any locomotion state; in third person "the shoulders absorb recoil, the torso shifts", driven "by additive animation or procedural bone adjustment on the spine and shoulder bones"; and the weapon is "attached and pivoted at the handle", because the hand bracing the linear kick is what turns it into rotation. [MoCap Online *Third-person shooter animation*; Kinemation *Recoil animation* docs, via search summaries]

**Draw and holster**

15. A draw is described as four beats — hand reaches the holster socket, grips, pulls the weapon free, settles into the ready pose — with holster sockets on hip, back or thigh, and the weapon interpolated between sockets during the transition. Draw/holster clips are tagged to switch the hand IK off during the reach and on once the gun is held. [MoCap Online *Weapon animation systems*; Kinemation *Character*, via search summaries]
16. Real draw-to-first-shot times: **1.2–1.8 s** for trained shooters from an open holster; a VirTra study of certified officers from a duty holster averaged **1.777 ± 0.329 s**; concealment adds about half a second (1.8–2.5 s); training standards are 2.0 s civilian, 1.5 s professional, 1.0 s expert. Most of that time is the reach and grip, not the presentation. [Athlon Outdoors; Mantis; Airsoft Shot Timer 2026 draw guide; Quora, via search summaries]
17. Non-looping transition states that must *hold* their end pose use `ClampForever` (keep the last frame) or the follow-on state re-imposes the pose; otherwise the hands fall back to the base layer's pose the instant the transition ends. [GitHub TartarusEngine PR #377/#379, via search summary]

**Aim hold as a stance**

18. All three shooter references layer the same way: a base locomotion layer for the whole body; an upper-body avatar-mask layer for weapon actions; an additive aim/recoil layer; and IK last. The aim itself is either a constraint (fractional weights, finding 1–2) or an additive aim-offset pose (finding 3, 7), never a replacement of the walk. [MoCap Online guides; Kinemation; Naughty Dog reference in Unity Discussions, via search summaries]

**Pitfalls**

19. Jitter when the rig and a script both move the target in the same frame is a reported Multi-Aim problem — the constraint reads the target's transform at graph evaluation, so a target updated afterwards is one frame late. [Unity Discussions *Animation rigging — Multi-Aim constraint jitter*, via search summary]
20. Hands drifting off the gun is answered in every recipe the same way: the gun is the parent of the hand sockets, the hand IK targets are those sockets, and the gun is placed *after* the body pose — never the hands first and the gun after. Smoothing the hand targets (SmoothDamp) is used to hide the residual one-frame lag when the gun pitches. [findings 8–9]

## Recommendation

**Aim (which mechanism).** Ranked: (1) a hand-rolled distributed aim in the existing post-graph pass; (2) Animation Rigging via `RigBuilder.Build(graph)`; (3) additive aim-offset poses. Back **(1)**. The project already owns a pass that adds to bones the graph wrote and a two-bone IK; the aim solve is one delta quaternion split over spine / chest / upper chest by weights and clamped per bone, then the head re-aimed to the target on top — perhaps thirty lines beside code that exists. Animation Rigging would add an `Animator` and a second writer running *inside* the graph, before `Grasp` and the additive shapes, and finding 19 shows what two writers on one bone do. Numbers to start from: split the yaw 0.3 / 0.4 / 0.3 across the three spine bones (finding 2) with a total clamp of **±60° yaw** and **±45° pitch** below the head (below the ±80° the package uses, because at 20–160 m a twisted torso reads as a broken figure long before it reads as aiming); the head gets up to the full ±80°; beyond the yaw clamp, turn the *pawn* (the project already turns figures) rather than the spine. Aim the *forearm-to-muzzle* line, i.e. the gun's barrel after the hands are on it, not the hand bone (finding 3), and iterate twice. Slerp the aim weight in over the aim time so the posture visibly settles before the shot, which is the owner's "visible aim time".

**Two-hand hold.** Ranked: (1) isosceles, both arms near-straight, symmetric hints; (2) Weaver. Back **isosceles** — from 48° above, the isosceles is a symmetric wedge pointing at the target with the gun at its apex, which is exactly the silhouette a slice camera can read; the Weaver's bent support elbow "pointed at the ground" collapses into the torso from above (finding 10). Mechanically, model the gun as the truth: place the gun in front of the chest, on the aim line, at a distance of about **0.85 × that rig's own arm length** — measured from the drawn mesh as the sole and crown already are, never from a bone name — so neither arm locks out on a long rig nor bends double on a short one; put `Grip` and `Support` sockets on the gun (support a little forward and to the left, cupping the strong hand); solve the right arm to `Grip` and the left to `Support` with the existing `Grasp`; hints go outward and slightly down from each elbow, mirror images (finding 8). The tie-breaker if the wedge does not read is the cheapest experiment there is: one screenshot of a posed figure at 60 m with the arms straight and one with them Weaver-bent, side by side.

**Recoil.** Ranked: (1) a critically damped spring on the gun transform with the hands riding it through IK and a fraction leaked into the shoulders; (2) a keyed curve; (3) recoil on the hands only. Back **(1)**, because the project's grip already follows the prop each frame, so kicking the gun kicks both hands for free, and the shoulder share is the same additive-shape machinery as the flinch. Start values: **pitch +6° and 4 cm back** at the muzzle, spring **~250 ms** to settle for a pistol (the measured 353–644 ms in finding 13 is rifles; pistols are quicker), with 10–15 % of the pitch leaked into both shoulders as an additive rotation and released over the same time (finding 14). At distance a 4 cm slide is invisible and 6° of barrel is barely so; what reads is the *shoulders and gun rising together and falling back*, so the shoulder leak is the part to tune first, not the muzzle. The CS2 lesson worth copying whole: derive stiffness and damping from **one** duration number (finding 13), so the design doc can say "the kick lasts 250 ms" and mean it.

**Draw and holster.** Ranked: (1) a keyed hand arc in the project's stroke system, reusing the sword-draw timing; (2) IK-driven reach to a holster socket. Back **(1)**. Author it as three beats to match finding 15: hand to hip (the gun is still parented to the hip socket), the grasp instant (parent switches to the hand — the exact moment the sword draw already calls "hand on hilt"), then the arc up to the chest where the left hand joins and the aim slerp begins. Total **0.6–0.8 s** for the draw — half the real 1.2–1.8 s (finding 16), because the aim time follows it and the real number was measured to the *shot*; holster is the same arc reversed at **0.8–1.0 s**, slower because nothing waits on it. The one thing to hold: the gun changes parent at the grasp instant and at no other time, and between beats the hands are IK'd to the socket they are reaching for so the gun is never seen away from the hand that carries it (finding 20).

**The stance while walking.** Use the existing upper-body mask layer to hold a "ready" arm pose over the gait and put the aim, the IK and the recoil on top of it in the post-pass (finding 18). Do not replace the walk; the legs and hips stay the graph's.

## Sources

- https://blog.unity.com/technology/advanced-animation-rigging-character-and-props-interaction
- https://mocaponline.com/blogs/mocap-news/unity-animation-rigging-guide
- https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.2/manual/constraints/MultiAimConstraint.html
- https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.2/api/UnityEngine.Animations.Rigging.RigBuilder.html
- https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.2/changelog/CHANGELOG.html
- https://gamedev.net/forums/topic/655859-rotate-character-spine-so-weapon-aims-at-target/5149346/
- https://forum.unity.com/threads/trying-to-rotate-spine-bones-to-aim-weapon-at-target.648022/
- https://discussions.unity.com/t/spine-rotation-based-off-camera/668484
- https://discussions.unity.com/t/animation-rigging-multi-aim-constraint-jitter/784849
- https://bugnet.io/blog/how-to-fix-unity-animation-rigging-multi-aim-constraint-head-snapping-180-degrees
- https://dev.epicgames.com/documentation/en-us/unreal-engine/creating-an-aim-offset-in-unreal-engine
- https://forums.unrealengine.com/t/limiting-aim-offset/350802
- https://bergstrand-niklas.medium.com/how-to-set-up-ik-rigging-for-handgun-in-unity-d135aef50f06
- https://medium.com/@pkillman2000/ik-rigging-and-weapon-system-in-unity-3cb6be888de3
- https://m-ansley.medium.com/setting-up-an-ik-weapon-system-in-unity-1-f354ba13ff52
- https://kinemation.gitbook.io/tactical-shooter-pack-unity/animations/character
- https://kinemation.gitbook.io/character-animation-system-docs/fps-addon/recoil-animation
- https://en.wikipedia.org/wiki/Isosceles_Stance
- https://en.wikipedia.org/wiki/Weaver_stance
- https://infinitytargets.com/blogs/training-shooting-tips/weaver-vs-isosceles-pistol-shooting-stance
- https://www.nrablog.com/articles/2017/9/pistol-shooting-positions-weaver-vs-isosceles
- https://gameidea.org/2025/09/07/adding-recoil-and-impact-to-the-weapon-fps-series-part-4/
- https://www.fab.com/listings/299b8cab-b10f-43f9-9819-90603646b9b6
- https://docs.cryengine.com/display/SDKDOC2/Procedural+Weapon+Animations
- https://github.com/sidcarrollworks/CSGODOT/pull/6
- https://discussions.unity.com/t/simple-weapon-recoil-script/430716
- https://github.com/OfficialHaggMarts/Weapon-Recoil-Script-Unity
- https://mocaponline.com/blogs/mocap-news/third-person-shooter-animation-guide
- https://mocaponline.com/blogs/mocap-news/weapon-animation-systems-guide
- https://github.com/JCamberos27/TartarusEngine/pull/379
- https://athlonoutdoors.com/article/drawing-from-a-holster/
- https://mantisx.com/blogs/news/speeding-up-your-draw-everything-to-know-about-draw-speed
- https://airsoftshottimer.com/en/posts/pistol-draw-guide/

## Confidence

**Medium.** The architecture (distributed aim after the graph, gun-as-parent-of-hand-sockets, spring recoil, tagged IK across a draw) is consistent across every source and matches what the project already does; the per-bone weights, the ±80° limit and the draw times are quoted numbers. But nine of ten page reads were blocked, so most findings come from search-engine summaries rather than the pages, the only measured recoil timings are rifles in CS2, and the pistol-specific starting values in the Recommendation (6°, 4 cm, 250 ms, 0.85 × arm length, 0.6–0.8 s draw) are my extrapolation and are labelled as starting points to be measured, not practitioner numbers.

## Could not be determined

- Practitioner **pistol** recoil numbers (degrees, centimetres, milliseconds): every measured figure found is for a rifle or is an unlabelled Unity script parameter; the common tutorial values (`recoilX`, `snappiness`, `returnSpeed`) were not returned with their numbers.
- Whether Unity's blog rig weights (0.35 / 0.5 / 0.75 / 1.0) are constraint weights applied cumulatively or fractions of the total — the page was blocked; both readings appear in secondary summaries.
- Any source on what a pistol stance looks like from a **high oblique camera at 20–160 m**; the isosceles-reads-better claim is inferred from the stance geometry, not from a game that has tested it.
- Whether `RigBuilder.Build(PlayableGraph)` honours a caller-supplied evaluation order against custom `AnimationScriptPlayable` nodes in the same graph — the API page was blocked.
- Sword-draw "hand on hilt" timing from any pack: no source; the mapping to the parent-switch instant is by reasoning from finding 15.

## Layer question 5: can you shoot up and down?

Nothing found that treats vertical targets specifically, but the pitch machinery is the same: the aim offsets in every source carry a pitch axis (Unreal −90..90, clamped; Multi-Aim about ±80°), so a target one layer up (3 m) at five cells (12.5 m) is a 13° pitch and two layers up at three cells is 39° — inside any clamp, and shared the same way (a third each in the spine bones, the head taking the rest). The project-specific question is what the *hands* do: with the gun placed on the aim line at a fixed fraction of the arm length, pitching the aim line raises both hands together and the isosceles wedge simply tilts, which a top-down camera reads only faintly — so a steep shot should also raise the shoulders a little (the same additive shoulder shape as the recoil leak) and the head should visibly look up. Whether a line of fire exists between layers is the simulation's business, not the pose's; the pose only needs the target point, and the ±45° pitch clamp proposed above means a target more than about one cell over per layer up is aimed at by the head and arms with the torso holding.
