---
name: integrate-parallel-work
description: Safely integrate features developed in parallel worktrees into a dedicated integration branch, test them together, then merge to main. Use when multiple feature/[name] branches need combining before release.
---

# /integrate-parallel-work — Parallel Feature Integration

## 1. Task-Specific Instructions

Integrate one or more features developed in parallel worktrees/branches (`feature/[feature-name]`) into a single integration branch, verify they work together, run the full test suite, and only then merge to `main`. This is a **multi-step, higher-risk** workflow — several steps are hard to reverse (branch merges, pushes, branch deletion) and must be confirmed with the user before executing, per this repo's execution rules. Delegate the actual merge/conflict-resolution/test work to the `integration-engineer` subagent; use this command to sequence and gate that work.

## 2. Arguments and Placeholders

```
/integrate-parallel-work $ARGUMENTS
```

| Placeholder | Meaning | Notes |
|---|---|---|
| `$ARGUMENTS` | Space- or comma-separated list of feature names | Each maps to a branch `feature/{name}` |
| `{feature-name}` | A single feature slug, e.g. `signalr-batching` | Must already exist as `feature/{feature-name}` |
| `{integration-branch}` | Name of the working integration branch | `integration/parallel-features` (fixed unless user overrides) |
| `{base-branch}` | Branch integration ultimately merges into | `main` |

If `$ARGUMENTS` is empty, stop and ask the user which feature branches to integrate — do not guess from `git branch` output alone, since not every local/remote branch is meant to ship.

## 3. Reusable Process Steps

### Step 1 — Preflight

1. `git status` — confirm no uncommitted work is sitting on the current branch; if there is, stash or ask before switching.
2. `git fetch --all` — ensure all `feature/*` branches are up to date locally.
3. Confirm each `feature/{feature-name}` in `$ARGUMENTS` actually exists (`git branch -a | grep feature/{feature-name}`). If one is missing, report it and ask whether to skip it or abort.

### Step 2 — Create the integration branch

```bash
git checkout main
git pull
git checkout -b {integration-branch}
```

If `{integration-branch}` already exists locally (e.g. a retry), ask the user whether to reuse it or delete and recreate — do not silently delete.

### Step 3 — Merge each feature branch in turn

For each `{feature-name}` in `$ARGUMENTS`, in the order given:

```bash
git merge --no-ff feature/{feature-name} -m "Merge feature/{feature-name} into {integration-branch}"
```

- On a clean merge: continue to the next feature.
- On conflicts: hand off to the `integration-engineer` agent to resolve. Never resolve by blindly taking "ours" or "theirs" for whole files — inspect each conflict hunk. Re-run the affected feature's own tests after resolving before moving to the next merge.
- Never use `git merge -X ours` / `-X theirs` as a blanket strategy.

### Step 4 — Integration verification

1. Build/typecheck both subsystems: `dotnet build` (backend), `npm run type-check && npm run lint` (frontend).
2. Run the full test suite (backend + frontend + any e2e).
3. Bring the stack up (`docker-compose up --build -d` or equivalent) and smoke-test the combined features — reuse `/review-ux` or `/chrome` if the change is user-facing.
4. If any step fails, stop, report the failure, and fix forward on `{integration-branch}` (new commits) rather than force-editing merge commits.

### Step 5 — Merge to main (confirm first)

**Do not run this step without explicit user confirmation in this turn.** Once confirmed:

```bash
git checkout main
git pull
git merge --no-ff {integration-branch} -m "Integrate features: {feature-list}"
git push
```

### Step 6 — Cleanup (confirm first)

**Do not delete branches without explicit user confirmation.** Once confirmed:

```bash
git branch -d {integration-branch}
git push origin --delete {integration-branch}   # only if it was pushed
# Per feature branch, only after confirming it's fully merged and no longer needed:
git branch -d feature/{feature-name}
```

## 4. Guided Examples and References

- `/integrate-parallel-work fuel-gauge-redesign, alert-batching` — creates `integration/parallel-features`, merges both branches in order, tests, then pauses for confirmation before touching `main`.
- `/integrate-parallel-work signalr-reconnect` — single-feature integration; still goes through the full verify → confirm → merge → cleanup pipeline rather than fast-forwarding straight to `main`.
- See `.claude/skills/integrate-parallel-work/SKILL.md` for the detailed conflict-resolution and test-verification protocol.
- See the `integration-engineer` agent (`.claude/agents/integration-engineer.md`) for the delegate that performs merges and conflict resolution.
- See root `AGENTS.md` File Contracts before resolving conflicts that span `frontend/` and `backend/` — don't let one feature's merge silently overwrite another agent's owned files.

## 5. Explicit Output Requirements

Report progress after each step, and a final summary in this structure:

```
## Integration Report — {integration-branch} — {datetime}

### Branches Integrated
- feature/{name}: merged ✓ / conflicts resolved ✓ / failed ✗

### Conflicts Resolved
- {file path}: {one-line description of resolution} — or "none"

### Verification
- Backend build: ✓/✗
- Frontend type-check/lint: ✓/✗
- Test suite: {pass count}/{total} — ✓/✗
- Smoke test (docker-compose / review-ux): ✓/✗

### Merge to main
- Status: pending confirmation / completed / not attempted (verification failed)

### Cleanup
- Branches deleted: {list} — or "none (pending confirmation)"
```

## 6. Template-Based Naming

- Integration branch: `integration/parallel-features` (fixed name; if run multiple times in the same effort, append a date suffix only if the user asks to keep a prior attempt around: `integration/parallel-features-{YYYYMMDD}`).
- Merge commit messages: `Merge feature/{feature-name} into {integration-branch}` for Step 3, `Integrate features: {feature-list}` for Step 5 — not the `IIOT-S{NN}-{TASK-ID}` sprint-task format, since this is a cross-cutting integration commit rather than a single sprint task.
- Any saved test/build logs: `integration-{step-name}-{YYYYMMDD}-{HHmm}.log` in the scratchpad directory.

## 7. Error Handling and Edge Cases

| Condition | Handling |
|---|---|
| A named `feature/{name}` branch doesn't exist | Report and ask whether to skip or abort — never silently continue without it |
| Merge conflict touches a file owned by a different subsystem than the feature (per File Contracts in `AGENTS.md`) | Flag explicitly; resolution must respect ownership, not just "make it compile" |
| Tests fail after all merges are clean | Do not merge to `main`; fix forward on `{integration-branch}`, re-run full verification before asking again to merge |
| Conflict resolution is ambiguous (both sides changed the same logic differently) | Stop and ask the user rather than guessing which version is correct |
| User asks to skip verification "to save time" | Push back once, explaining the risk (this is exactly the scenario this command exists to prevent); proceed only if they insist |
| `{integration-branch}` already exists with unrelated history | Ask before reusing or deleting — do not `git branch -D` without confirmation |
| Push to `main` or force operations requested | Follow standard git safety rules: never force-push, never skip hooks, always confirm before pushing |
| Feature branches diverge significantly from `main` (stale) | Recommend rebasing/updating the feature branch first and flag it to the user rather than merging stale code silently |

## 8. Documentation and Context

- This command spans both `frontend/` and `backend/` — read both `frontend/AGENTS.md` and `backend/AGENTS.md` before resolving any cross-cutting conflict, not just the root `AGENTS.md`.
- Steps 5 and 6 (merge to main, branch deletion, remote push) are exactly the class of "hard to reverse" / "affects shared systems" actions called out in this repo's execution-care rules — always pause for explicit confirmation, even if the user approved the overall `/integrate-parallel-work` invocation up front. Approval of the command is not blanket approval of every destructive step inside it.
- If integration reveals a design conflict between features (not just a text merge conflict), escalate to the `system-designer` or `team-lead` agent rather than picking a resolution unilaterally.
