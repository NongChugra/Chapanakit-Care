# GitHub handoff

This repository contains source code, migrations, synthetic fixtures, tests, and documentation.
It intentionally excludes the local SDK cache, build output, SQLite runtime databases, backups,
and generated EXE/ZIP packages.

## First repository push

1. Create an empty GitHub repository; do not initialize it with a README, license, or `.gitignore`.
2. From this folder, review the prepared initial commit with `git status` and `git diff --cached`.
3. Commit and connect the remote:

```powershell
git commit -m "Initial Chapanakit Care first-day MVP"
git branch -M main
git remote add origin https://github.com/<your-account>/<your-repository>.git
git push -u origin main
```

Use an SSH remote instead if your GitHub account is configured for SSH.

## What belongs in Git

- `src/`: application, domain, migrations, and the tracked `App_Data/demo-members.json` fixture.
- `tests/`: automated test source and test fixture.
- `docs/`: architecture, verification profile, manual test checklist, and this handoff.
- `.github/workflows/ci.yml`: GitHub Actions build/test workflow.

## What must stay out of Git

- Real SQLite databases, WAL/SHM files, backups, certificates, and member data.
- `.tools/`, `bin/`, `obj/`, test coverage, and `artifacts/` packages.

The `.gitignore` protects these boundaries. Before every commit, run `git status --ignored`
and ensure no real member or death-document data is staged.
