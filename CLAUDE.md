# Claude session bootstrap — IIOT-FLEET-TELEMETRY-SYSTEM

This file is auto-loaded at the start of every Claude Code session in this repo.

## Read order (do this every session)

1. Read `AGENTS.md` (root) **in full**.
2. Read the AGENTS.md file for the subsystem you are about to touch:
   - `frontend/AGENTS.md` — Next.js 16 (App Router) + TypeScript app that owns BOTH the user-facing UI and the public REST API (Route Handlers under `app/api/`).
   - `backend/AGENTS.md` — ASP.NET Core Web API. Shares the same Postgre SQL DB.
3. Read the active sprint file referenced under `## Current Sprint` in the root `AGENTS.md`.
4. If you have any questions about the tasks, ask the user for clarification before starting work/execution. The sprint file always includes the line: "Understand the below modification and bug fix and instruction, if any clarification or doubt, ask me before start the task execution." This is your signal to pause and confirm understanding before executing.
5. Execute the task, following the instructions and rules in the sprint file and AGENTS.md. When done, mark the task complete with "Status: [x]" on the sprint file and BACKLOG.md file, commit with message `IIOT-S{{NN}}-{{TASK-ID}}: <one-line summary>` for each task to control the commit history against the task.
6. Before committing, ensure all tests pass and the code adheres to the project's coding standards. and check the eslint and type checks pass with zero errors. and update the `CHANGELOG.md` with the new changes if there is any change in the system design after the sprint. each commits not want the seperate version. version will be updated at the end of the sprint by ARCH and complete all the tasks in the sprint before open the PR.