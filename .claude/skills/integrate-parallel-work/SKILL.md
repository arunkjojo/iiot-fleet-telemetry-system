---
name: integrate-parallel-work
description: Protocol for safely integrating features developed in parallel git worktrees/branches for the IIoT Fleet Telemetry System. Activates for merging multiple feature/[name] branches, resolving cross-subsystem conflicts, and verifying combined behavior before merging to main.
---

# Integrate Parallel Work — Merge & Verification Protocol

This skill defines the detailed protocol behind `/integrate-parallel-work` and the `integration-engineer` agent. Load it whenever combining more than one `feature/[name]` branch, even outside that command.

## Why an Integration Branch (Not Direct-to-Main Merges)

Merging features one at a time straight into `main` means the *first* broken combination a user sees is on the branch everyone else builds on. An integration branch is a disposable staging area: if features conflict badly or break each other, you throw it away and retry — `main` never sees a bad state.

## Merge Order Matters

Merge in the order the user specifies, not alphabetical or arbitrary. If the user hasn't specified an order and it matters (e.g. one feature is a prerequisite for another), ask rather than guessing.

## Conflict Resolution Rules

1. **Read both sides.** A conflict is two intents colliding — understand both before picking one.
2. **Check ownership.** Cross-reference `AGENTS.md` File Contracts. If a conflict is in a file normally owned by a different subsystem than the feature you're merging (e.g. a frontend feature branch touching `backend/`), that's a signal the feature branch drifted outside its lane — flag it, don't just silently resolve.
3. **Never blanket-resolve.** `git checkout --ours <file>` / `--theirs <file>` across an entire file discards one side's work wholesale. Only use it when you've confirmed one side is a strict no-op superset of the other change (rare) — otherwise resolve hunk by hunk.
4. **Preserve both intents when possible.** Two features adding different fields to the same model, different routes to the same controller, or different components to the same page should usually keep both additions, not pick one.
5. **Test immediately after resolving.** Run the narrowest relevant test right after resolving a conflict in a file, before moving to the next conflicted file. Waiting until everything is resolved makes it hard to tell which resolution broke what.
6. **Escalate genuine ambiguity.** If both sides changed the *same* behavior in *different* ways (not just different additions), this is a design decision, not a merge mechanics problem — stop and ask the user or escalate to `system-designer`/`team-lead`.

## Verification Levels

Run these in order; stop and fix forward if any level fails before proceeding to the next:

1. **Compile/typecheck:** `dotnet build` (backend), `npm run type-check` (frontend)
2. **Lint:** `npm run lint` (frontend)
3. **Unit/integration tests:** full backend + frontend test suites
4. **Runtime smoke test:** bring the stack up (`docker-compose up --build -d`) and verify the combined features actually interoperate — not just that each works in isolation. Reuse the `chrome` or `review-ux` skills/commands for anything user-facing.

A feature that passes its own tests in isolation can still break another feature at runtime (e.g. two features both mutating shared SignalR hub state). The smoke test step exists specifically to catch that class of bug — do not skip it even under time pressure.

## Fix-Forward, Not Force-Edit

If verification fails after merges are otherwise clean, fix the issue with new commits on the integration branch. Do not rewrite merge commits, do not `git commit --amend` a merge, and do not `git reset --hard` to "start over" without confirming with the user first — that discards the conflict-resolution work already done.

## Gate Before Touching Shared State

Treat these as checkpoints requiring explicit user confirmation, not implicit continuation:

- Merging the integration branch into `main`
- Pushing any branch to the remote
- Deleting any branch (feature or integration), locally or remotely

Passing verification is a precondition for asking to proceed past these gates — it is not itself permission to proceed.

## Reporting Standard

Every integration run should produce a report covering: which branches merged cleanly, which needed conflict resolution and how each was resolved (file + one-line rationale), full verification results per level, and current status relative to the merge-to-main / cleanup gates. This lets a human reviewer audit conflict-resolution decisions after the fact without re-deriving them from the diff alone.
