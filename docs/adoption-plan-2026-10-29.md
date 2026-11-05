# Adoption Plan

> **Generated**: 2026-10-29
> **Project phase**: Production (inferred: 110 `.cs` files under `Assets/`; no `project.stage` set)
> **Engine**: Unity 6000.2.6f2 (C#, URP, Physics 2D)
> **Workflow tier**: minimal (from `modes.rigor` default). The design record is `design/game-brief.md`. GDDs, ADRs and UX specs are not expected.
> **Template version**: v1.2.0

Work through these steps in order. Check off each item as you complete it.
Re-run `/adopt` anytime to check remaining gaps.

## What was audited

| Area | Result |
|---|---|
| `design/game-brief.md` | **Missing** |
| GDDs in `design/gdd/` | None (not expected at minimal) |
| ADRs in `docs/architecture/` | None (not expected at minimal); `tr-registry.yaml` is the empty template |
| Stories in `production/epics/` | None |
| Engine reference | Present: `docs/engine-reference/unity/VERSION.md` |
| Project config (`project.yaml`) | engine, specialists, naming and commands set on 2026-10-29 by `/setup-engine` |
| v1.0 migration | Not needed: `project.yaml` exists, no `production/stage.txt` or `review-mode.txt` |
| Existing design docs (advisory) | `docs/level1-zeusonia.md`, `docs/dev-handoff.md`, `docs/art-requirements.md`, `README.md`. Checked with `gdd-structure-check.sh`; 0 of 8 canonical GDD headings present (they use numbered Chinese headings). Advisory only at this tier. |

---

## Step 1: Fix Blocking Gaps

None.

---

## Step 2: Fix High-Priority Gaps

### 2.1 No game brief

**Problem:** `design/game-brief.md` doesn't exist. At the minimal tier it *is* the design doc, and `/create-stories` turns its MVP list into stories. Without it, the pipeline has nothing to plan from.
**Fix:** Write the brief from what already exists, without re-brainstorming. The content is already there:
- pitch, core loop, and goal / fail state → `README.md` (§ Gameplay, § Level 1)
- MVP and build order → `docs/dev-handoff.md` §1, §6, §7 and `docs/level1-zeusonia.md`
- out of scope → `docs/dev-handoff.md` §6 (no save system / Continue yet, Level 2 not built)
- art direction → `docs/art-requirements.md` §0

Template: `.claude/docs/templates/game-brief.md` (one page: 6 required fields, 3 optional one-liners).
**Time:** 30 min
- [x] `design/game-brief.md` written, all 6 required fields filled (2026-10-29; ⚠ lines still need confirming)

---

## Step 3: Bootstrap Infrastructure

Not on the minimal path. The brief's build order is the plan. Next: `/create-stories` (or `/dev-story` once stories exist).

---

## Step 4: Medium-Priority Gaps

### 4.1 Design sources cited but not in the repo

**Problem:** `docs/level1-zeusonia.md` and `README.md` cite "GDD §5", a "Level Design Document" and a "Flash Fiction Story (Act III)". None of these are in the repo, so agents can't check work against them.
**Fix:** Do one of these: add them under `design/` (for example `design/source/`); summarise the parts that matter into the brief (2.1); or note in the brief that they're external and the brief is authoritative.
**Time:** 5–30 min
- [x] Source design docs added, summarised, or marked external (2026-11-04: GDD, Final Doc and Progress Check 2 summarised into `design/game-brief.md`; originals stay in `Desktop/aeternal/Development Documents/`)

### 4.2 Engine reference is one release ahead of the project

**Problem:** `docs/engine-reference/unity/` is curated for 6.3 LTS, but the project and editor are 6000.2.6f2. `project-coherence.sh` reports `[DIFFERS]` on this. It was a deliberate choice, recorded in `VERSION.md` on 2026-10-29: 6.3+ APIs are treated as unconfirmed.
**Fix:** None needed now. Re-check if you upgrade the editor (`/setup-engine upgrade 6000.2.6f2 <new>`), or run `/setup-engine refresh` to add 6.2-specific notes.
**Time:** 0 min now
- [ ] Acknowledged (or the project is upgraded to 6.3 LTS)

---

## Step 5: Optional Improvements

### 5.1 Existing design docs don't use the template headings

**Problem:** `docs/level1-zeusonia.md` is the closest thing to a GDD (flow, decks, balance §5, tuning knobs §6, open questions §7), but it has none of the canonical headings. That's fine at minimal. It only matters if the project moves to `standard` or `full` rigor.
**Fix:** Leave it as is. If you raise rigor later, run `/design-system retrofit` on a copy placed in `design/gdd/`.
**Time:** 1 session (only if rigor is raised)
- [ ] Deferred

### 5.2 Remaining config placeholders

**Problem:** `technical-preferences.md` still has draw calls, memory ceiling, minimum coverage and platform notes as `[TO BE CONFIGURED]`. Forbidden Patterns and Allowed Libraries are empty, which is how the template ships them.
**Fix:** Set them with `/settings` when you know your target hardware. Good candidates for Forbidden Patterns are the hard-won rules in `docs/dev-handoff.md` §4.
**Time:** 5 min
- [ ] Budgets set
- [ ] dev-handoff §4 conventions copied to Forbidden Patterns (optional)

---

## Outside /adopt's scope, but noted

- **No automated tests yet.** There's no `Assets/Tests/` and no `.asmdef`. `commands.test` will report 0 tests, which is not a pass. `/test-setup` scaffolds it, and `docs/dev-handoff.md` §7.2 already lists what to cover first.
- **Most recent work is uncommitted** (see `git status` and dev-handoff §1). Commit it before starting story work so later diffs are reviewable.
- **`commands.build` / `commands.run` haven't been run yet.** `Builds/macOS/CardGame.app` doesn't exist until the first build.

---

## What to Expect from Existing Stories

There are no stories yet. Stories you create from now on get TR-ID and manifest checks automatically. Those checks auto-pass when the fields are absent, which suits the minimal tier.

---

## Re-run

Run `/adopt` again after Step 2 to confirm the brief passes the format check.
