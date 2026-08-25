# Repository verification profile

- Authority inspected: repository `AGENTS.md`, project files, restart scope, and checkpoint acceptance criteria.
- FOCUSED_CHECKS: filtered `dotnet test` commands for the active domain or SQLite behavior slice.
- CANDIDATE_CHECKS: formatting verification, build, all tests, EF model/migration drift, a fresh-database packaged-app smoke test, three rendered PDF inspections, and backup/restore equality at the final cumulative boundary.
- COMPOSE_CHECK: not applicable; this is a local single-process Windows application and the repository defines no Compose environment.
- CANDIDATE_BOUNDARY: before independent final-strict review of the completed first-day delivery.
- COMPOSE_LIFECYCLE: not applicable.
- EVIDENCE_REUSE: only for an identical candidate and unchanged command inputs/toolchain.
- EARLY_GATE_OVERRIDES: real temporary-SQLite integration tests may run at checkpoints because they directly reduce schema and data-integrity risk.
