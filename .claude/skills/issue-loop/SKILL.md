---
name: issue-loop
description: Works through open GitHub issues one after another — for each issue it implements on a fresh branch, runs /simplify, runs /code-review and applies fixes, then opens a PR, merges it, returns to main and moves on to the next issue. Each phase runs in a fresh sub-agent so context is cleared between phases. Use when the user says "loop over the issues", "work through the backlog", "issue loop", or invokes /issue-loop.
argument-hint: "[issue numbers... | --label <name>] [--max <n>]"
disable-model-invocation: true
---

# Issue loop

You are the **orchestrator**. You never implement, simplify or review yourself — each of those
phases runs in a **fresh sub-agent** (Agent tool, `general-purpose`), which is how context is
"cleared" between phases. You keep only the issue number, branch name and each phase's short
report.

## 1. Build the queue

- Arguments are issue numbers → use them, in the order given.
- `--label <name>` → `gh issue list --state open --label <name> --json number,title --limit 200`.
- No arguments → all open issues **without** the `needs-triage` label, lowest number first.
- `--max <n>` caps how many issues to process (default: no cap).

Show the queue to the user once, then proceed without asking again.

## 2. For each issue `N`

Start clean: `git checkout main && git pull --ff-only`. If the working tree is dirty, stop.

Branch name: `issue-N-<short-kebab-slug-of-title>` (see `docs/agents/issue-tracker.md`).
`git checkout -b <branch>`.

### Phase A — Implement (fresh sub-agent)

Prompt the sub-agent with:

> You are on branch `<branch>` in C:\Code\glimpses-of-glory-eshop. Implement GitHub issue #N.
> Read it with `gh issue view N --comments`. Follow CLAUDE.md, the `project-structure` skill, and
> `CONTEXT.md` / `docs/adr/`. Meet every acceptance criterion; add or update tests.
> Run `dotnet build GlimpsesOfGloryEshop.slnx` and `dotnet test GlimpsesOfGloryEshop.slnx` until
> both pass. Commit your work on this branch (do not push, do not switch branches).
> If the issue is ambiguous or blocked, do NOT guess — make no commits and reply starting with
> `BLOCKED:` and the reason. Otherwise reply with a ≤10-line summary of what changed.

### Phase B — Simplify (fresh sub-agent)

> On branch `<branch>`, invoke the `simplify` skill on the changes since `main`
> (`git diff main...HEAD`). Apply its fixes, re-run `dotnet test GlimpsesOfGloryEshop.slnx`,
> and commit. Do not push or switch branches. Reply with a ≤10-line summary.

### Phase C — Code review (fresh sub-agent)

> On branch `<branch>`, invoke the `code-review` skill against fixed point `main` for
> issue #N. Then fix every finding that is a real defect or standards violation (skip
> nitpicks you judge wrong, and say why). Re-run `dotnet test GlimpsesOfGloryEshop.slnx`,
> commit, do not push or switch branches. Reply with findings → fixed/skipped, ≤15 lines.

### Phase D — Ship (you, the orchestrator)

1. Verify: `git status` clean, `dotnet test GlimpsesOfGloryEshop.slnx` passes.
2. `git push -u origin <branch>`
3. `gh pr create --base main --head <branch> --title "<issue title>" --body` with: summary from
   phases A–C, `Closes #N`, and the PR attribution line from the system reminder.
4. **Approval gate.** Ask the user with AskUserQuestion, making the question self-contained:
   issue `#N` and title, PR URL, phase A–C summaries (incl. review findings skipped), and
   `git diff --stat main...HEAD`. Options: **Merge**, **Skip** (leave PR open, go to next issue),
   **Stop** (leave PR open, end the loop). Never merge without an explicit "Merge".
5. On Merge: `gh pr merge --merge --delete-branch`
6. `git checkout main && git pull --ff-only`
7. Print one line: `#N merged — <PR url>` (or `#N left open — <PR url>`) and continue.

## Skip and stop conditions

**Skip** (non-fatal): Phase A replies `BLOCKED:` → comment the reason on the issue
(`gh issue comment N --body ...`), `git checkout main`, `git branch -D <branch>`, next issue.

**Stop** the whole loop and report to the user when:

- Build or tests still fail after a phase.
- Push, PR creation or merge fails (e.g. conflicts, failing required checks).
- The working tree is unexpectedly dirty.

## Final report

A table: issue, branch, PR URL, result (merged / blocked / failed), and one-line notes.
