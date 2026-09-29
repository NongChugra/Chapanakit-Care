# Reduced accounting release — 2026-09-25

Candidate2 evidence; reduced release complete and independent behavior finding resolved. Scope is the reduced first release at the top of `docs/design/accounting-reporting-scope.md`. Formal archived-framework certification is not claimed; see independent-review-0925.md for that metadata-only disposition.

Final candidate updates supersede the initial evidence below: `reduced-final-suite-3-0925.log`317/317GREEN; `reduced-publish-2-0925.log`successful publish; `published-http-smoke-2-0925.json`8pagesHTTP200/no initial validation errors. `review-attempt.json` binds source/runtime manifests and these logs by SHA256. Both the rate-change rejection and dashboard message have observed RED evidence (`rate-guard-red-0925.log`, `rate-page-red-0925.log`) before their fixes. Details and limits of independent review are in `independent-review-0925.md`.

- `reduced-final-suite-0925.log`: 315 passed, zero failed/skipped, including accounting operations, linked transactions, migrations/append-only integrity, reports, real HTTP forms and existing backup/regression tests.
- `published-pages-red-0925.log` and `published-pages-diagnostic-0925.log`: reproduced dashboard HTTP500 and omitted-default-book validation errors before fixes.
- `reduced-publish-0925.log`: successful publish to `artifacts/accounting-reduced-demo`.
- `published-http-smoke-0925.json`: dashboard, reports, collections, management, opening, receipt and expense reclassification pages all HTTP200 without validation-summary errors on initial GET.
- `artifacts/accounting-reduced-demo/trial-balance-sample.pdf` and `.png`: Thai trial balance rendered with Poppler and visually inspected; readable labels and complete columns. This is an empty-book layout sample, not association financial evidence.

The published smoke used its own `App_Data` beneath the artifact directory and port5199. No production accounting activation or production database changes were performed. The sample app can be launched from its artifact directory with the installed .NET runtime and `--contentRoot . --LocalUrl http://127.0.0.1:5199`. Do not launch the source app to reproduce an isolated test.

Deferred: extra passbook/member-history layouts, annual receipt compilations, daily registers, unrestricted adjustments, confirmed death/opening corrections, bespoke receipt layouts, extensive visual polish. Draft files remain under `deferred/` outside compilation. Statutory submission compliance has not been certified.
