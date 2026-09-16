# Prompt for the next interactive session

Paste everything below the line into a fresh session in `D:\code\odyssey`. It is written for an
agent that starts cold and knows nothing except what is in the repository. It is *not* the
overnight prompt — that one is `overnight-prompt.md` and is for unattended agents working the
queue without a human. This one assumes the owner is awake, merges pull requests and playtests.

Written 2026-09-16, against `main` at `9caa502`. Everything in it was true then; check the status
bullet in `CLAUDE.md` and `docs/plans/overnight-queue.md` before trusting the positions below.

---

You are continuing work on Odyssey, a colony sim in the RimWorld mould with true 3D
discrete layers. Repo: D:\code\odyssey (Unity 6000.3.24f1, branch `main`).

## Read first, in this order

  CLAUDE.md — working rules; the "Where this is, 2026-09-16" bullet is the status
  docs/plans/vertical-slice.md — §"Status" and §"Where the seams are"
  docs/plans/overnight-queue.md — the work queue; 23 rows open
  docs/lessons.md — things that already cost someone a day

## Position

M0 closed. M1 and M2 done in substance and beyond what they asked for. M3 started
early: designations, felling, stockpiles in; mining written on `claude/mines` (24
commits, pushed, unmerged). Gates green as of 2026-09-16: fast tier 326 Sim + 29
Hud; Unity 488 total / 486 passed; PlayMode 7; wiki, labels and icon tooling current.

## Fix these before any new feature

Each is real, each is recorded, none is speculative.

  1. **OQ-40 — mouse input cannot be driven in a PlayMode test at all.**
     InputSystem.QueueStateEvent with a MouseState reaches neither the scroll nor the
     buttons of SliceCameraRig. Proven by a negative control: a scroll over the world
     that must zoom the camera did not. THIS IS THE BLOCKER. Until it is solved,
     nothing about pointer behaviour can be tested and three tests written against it
     were deleted for passing vacuously. Solve the harness first and prove it with a
     control that fails when the input is withheld.

  2. **The scroll-over-HUD guard in SliceCameraRig is UNPROVEN.**
     It ships on inspection only — the scroll path never consulted PointerOverInterface
     while the click path did. Owner confirmed it works by hand. It has no test, because
     of (1). Add one once (1) is fixed.

  3. **OQ-40, second half — InputRouter does not exist.**
     design 09 §6 specifies it with a capture stack and eight enumerated cases; four of
     them (drag, modal, tooltip) have nothing to test against. U25 Designations needs
     cases 3 and 4 for drag-to-designate, so this is ON the M3 path, not beside it.

  4. **OQ-47 — a scenario cannot place things on a chosen storey.**
     M2DemoTests runs a colony on a ruined city for a day and CANNOT assert the thing M2
     is about. FindStartSpots takes the nearest layer per column, so on a city map all 33
     spots land on the start layer and no colonist changes layer all day. The finder's
     layer spread is not the lever (it is a fallback, not a preference — measured).
     OQ-21, the M2 milestone report, should not be written until this lands, or it will
     report a milestone whose central claim is undemonstrated.

  5. **A layout coupling, named in Hud.uss and not yet fixed.**
     The inspect pane clears the two-row bottom bar by a hand-picked offset. It holds,
     because the bar cannot exceed two rows at any resolution the panel scales to. The
     honest fix is a three-row layout for the whole sheet rather than a fifth offset.

  6. **Two owner decisions left open, deliberately:**
     - the Build palette shows 2 categories of 10 and scrolls — correct but cramped;
       how much of the screen it deserves is the owner's call
     - the 81 ui.arch.* category and tool keys still spell "arch" after Architect was
       renamed to Build; they are mapped to the owner's icon sheets in icon-map.csv, so
       renaming is an art-mapping migration and its own change

  7. **ADR 0003 F2 and F3 are unmeasured.** F1 was measured 2026-09-16 and does not fire.
     R2 (icon atlas) is two hours. R3 is blocked by (1).

## Then, in this order

  OQ-47 → OQ-21 (M2 closed and reported honestly)
  OQ-15, OQ-16 (content to XML Defs — the largest chokepoint)
  OQ-44 (work givers register), OQ-45 (pawn-view contributor; NEEDS AN ADR 0004
  AMENDMENT, not just a row), OQ-46 (mesh contributors)
  then merge claude/mines

## Standing rules for this session

  • ALWAYS WRITE THE NEGATIVE CONTROL. Four tests passed vacuously in one day, and each
    time the control was the only thing that caught it. A test that has not been seen to
    fail is not evidence.
  • DO NOT DIAGNOSE UI BY READING. Three wrong diagnoses in a row came from reading
    code. HudShotTests renders the panel into an sRGB RenderTexture at a pinned
    resolution and writes Logs/hud-shot.png; SelectCheck photographs a colonist being
    clicked. Use them.
  • WaitForEndOfFrame is never evoked in batchmode, so ScreenCapture is unusable
    headlessly. GC.GetTotalAllocatedBytes does not exist in this runtime (OQ-19's row
    names it and is wrong); use GC.GetTotalMemory(false) with GC.CollectionCount(0)
    beside it.
  • OTHER SESSIONS SHARE THIS CHECKOUT. Check `git status` before staging and never
    `git add -A` blind — work was swept in both directions on 2026-09-16. Before
    removing a worktree, `rmdir Assets\Synty` first or the licensed art is deleted.
  • Unity batch commands cannot run while the owner has the editor open. Use the
    sibling checkout D:\code\odyssey-ui, detached at the same commit.
  • NOTHING IS DONE UNTIL IT IS IN THE BRANCH THE OWNER PLAYS. Do not report work as
    finished because it is committed to a branch behind an unmerged PR.

The owner merges pull requests and playtests. Ask at ADR forks. British English.
