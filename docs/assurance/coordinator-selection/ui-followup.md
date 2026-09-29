# Coordinator UI follow-up — 2026-09-04

User-authorized material follow-up after generation 0: preserve page scroll when searching within appointment, and make the end-role link look like the replace button. User also requested an explanation of the appointment control.

REOPEN_GENERATION: 1, limited to this UI follow-up. Execution: auto, parent local only. Assurance: standard; reversible page scrolling/button presentation, no change to mutation handlers, role constraints, history, migration or financial state. No fresh reviewer or review-budget reset. Generation 0's frozen manifest, verdict and completed journal remain historical evidence for that original candidate, not an attestation of these later edits. Parent runtime observed Sol/high in current persisted turn_context.

Changed files:

- Pages/Coordinators/Index.cshtml: end-role link uses `button secondary small`, label สิ้นสุดตำแหน่ง, same existing end action; includes versioned page-specific script.
- wwwroot/js/coordinator-search.js: save page coordinates for native candidate-search GET, consume only matching URL within one minute, restore after page load, and retain normal GET fallback when browser storage is unavailable. The script does not handle appointment POSTs.

Verification uses direct browser checks for the reversible UI changes under the applicable developer low-impact testing instruction; no new automated test was written. Existing HTTP form regression tests were rerun.

Observed before editing: native ค้นหา changed window.scrollY from 252 to 0 while correctly filtering the result to member 00025.

Observed after editing in the isolated published app with synthetic data:

- Native ค้นหา: scrollY 304.79998779296875 before and after; search changed result from member 00001 to 00025.
- Enter in the search field: scrollY 259.20001220703125 before and after; unmatched term correctly displayed the empty state.
- New end link has `button secondary small`, visually matches เปลี่ยน, and opens the existing required-reason / ยืนยันสิ้นสุดหน้าที่ form. No actual role ending was performed in this follow-up.
- Browser console: no errors. Script syntax check and `git diff --check` passed.
- Release web publish passed. Existing CoordinatorPageTests: 10 passed, zero failures/skips. Commands/logs: tmp/coordinator-ui-publish.log and tmp/coordinator-ui-page-tests.log; dotnet test used Release, --no-restore, -m:1 and filter FullyQualifiedName~CoordinatorPageTests.

Outcome: complete, standard assurance passed. No unrelated edits, production data mutation, commit or release. Temporary browser/server cleaned up after verification.

Button explanation: row แต่งตั้ง opens selection for that particular vacant chair/group-leader position; ยืนยันแต่งตั้งผู้รับผิดชอบ saves the selected member. The top ดูตำแหน่งทั้งหมด returns to the complete roster and closes selection. The exact label แต่งตั้งผู้ประสานงาน is not present in current source.
