# Candidate verification receipt

Frozen candidate: `sha256:e9441d06460c6933b38217f8fa7eb57c1eff9ede7e8db5e10ee2ed1201d6f411`.

Environment: Windows PowerShell, .NET SDK 10.0.302 / runtime 10.0.10, cached locked NuGet dependencies. Exact candidate command: `powershell -NoProfile -File docs/assurance/coordinator-selection/verify-candidate.ps1`. The script and per-lane logs record full arguments. Ran on 2026-09-04; log filesystem UTC timestamps are retained. No production data was used.

| Gate | Result and evidence |
| --- | --- |
| Locked solution restore | Pass, candidate-restore.log; explicit local NuGet cache source |
| Changed C# whitespace | Pass, candidate-format-src.log and candidate-format-tests.log; folder workspaces avoid the sandbox's blocked MSBuild named pipe and protected unrelated directories |
| Release solution build | Pass, candidate-build.log; zero warnings and errors |
| All .NET solution tests | Pass, candidate-tests.log; 124 domain/infrastructure/web tests and 4 desktop tests, no skips/failures |
| EF model, upgrade, backup and concurrency | Included in passing suite: no model drift; upgrade from prior migration preserves member data; restored backup preserves roles/history/migrations; competing requests cannot assign one member twice or occupy one slot twice |
| Published app, fresh SQLite | Pass, candidate-publish.log and browser-server.log. Published web app at 127.0.0.1:5198 with isolated tmp/coordinator-browser content root, then 40 synthetic members imported through actual demo UI |
| Browser interaction | Pass, observations below; actual rendered controls and HTTP submissions |
| Report regression | Pass, all three PDFs returned successfully and each rendered single page inspected; existing narrow-column wrapping remains part of deferred report formatting |
| Compose | Not applicable: repository has no Compose environment; local Windows app |
| Installed/delivered binaries | Not applicable: this is source implementation delivery; temporary published binaries are disposable verification fixtures, not an installed app or release |

Browser evidence (in-app browser via CUA, 2026-09-04):

1. Fresh empty dashboard and coordinator vacancies rendered. Demo import created 40 synthetic members across 16 exact group codes.
2. Group 0101 picker contained only its two eligible members, 00001 and 00025. Appointed 00001 through the radio and confirmation button. Success and history appeared.
3. Chair picker excluded 00001, leaving 39 candidates. Search for 00002 found only that member; appointment succeeded. Overview displayed chair 00002, group leader 00001, and 15 vacant groups.
4. Enabled navigation opened coordinator page. Library chair/leader filters each returned the correct single member, with organizational role separate from membership status.
5. Seeded legacy column preference through the isolated app's own antiforgery-protected preference endpoint. Reload preserved the saved actions/runNo/name/status/groupNo relative order and inserted role immediately after name. Existing hidden ID and visible name components were preserved. Hiding the role checkbox persisted after reload; resetting headings restored defaults.
6. Library manage link opened the correct group's replacement form. Missing required reason prevented submission. Selecting 00025 and entering a reason replaced 00001. Ending 00025 with a reason produced a vacancy and four readable chronological event rows, preserving prior names and reasons.
7. Desktop screenshot inspected. At 390 x 844 the filters/form stacked and controls remained readable, with local table scrolling and document width equal to viewport width. Full-page screenshot stitching visually repeated a section; direct DOM inspection confirmed exactly one POST form, two radios and one confirmation button. No browser console errors. Viewport override reset.
8. Rendered `tmp/pdfs/coordinator-regression/group.pdf`, `monthly.pdf`, and `sak-one.pdf` with `pdftoppm -png -r 110 -singlefile <pdf> <prefix>` and visually inspected all three resulting PNGs. Thai text present, tables and rows rendered, no blank or missing pages. Layout redesign remains out of scope.

Candidate executions: first frozen candidate ran full checks and exposed two stale legacy financial assertions (candidate-1-manifest.json / candidate-1-tests-failed.log). Both still expected 15 baht although the recorded baseline already seeds 9 baht and rounds fees down to whole baht. Updated only expected values and explanatory comments; production money logic/settings were unchanged. Focused rerun passed (legacy-expectation-fix.log). The current frozen candidate then passed one complete restore/format/build/test/publish sequence. No successful full gate was repeated on unchanged source.

TDD: backend-red.log, integrity-red.log, migration-red.log demonstrate missing behavior; corresponding green logs and ui-tdd.md record focused results. UI worker runtime was observed Terra/max, but it stopped on usage limits without a final report. The parent inspected its complete UI changes and took ownership of completion, browser checks and all integrated verification.
