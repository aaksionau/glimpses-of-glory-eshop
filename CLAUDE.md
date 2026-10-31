## Agent skills

### Project structure

Solution layout (Abstractions/Core/Infrastructure/Web projects plus UnitTests/IntegrationTests, type-first folders, the boundary rules enforced by `ProjectBoundaryTests`, where external integrations go per `docs/adr/0001`). Use the `project-structure` skill when deciding where a new file belongs or exploring how the codebase is organized.

### Issue tracker

Issues and PRDs live as GitHub issues (via the `gh` CLI). See `docs/agents/issue-tracker.md`.

### Domain docs

Single-context: `CONTEXT.md` + `docs/adr/` at the repo root. See `docs/agents/domain.md`.
