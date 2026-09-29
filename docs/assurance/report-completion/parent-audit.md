# Parent acceptance and adversarial audit

The completed source boundary contains nine changed-since-task-base paths in candidate-manifest.json. Parent inspected each actual file and its report/model/page interactions. All earlier coordinator, GET-handler, reference-image and setup changes remain preserved. No migration or member/role/financial write logic changed.

| Acceptance / risk | Counterexample and evidence | Result |
| --- | --- | --- |
| Reference content | Four initial RED cases in layout-red.log prove absent relationship/spouse columns and monthly certification; final layout-green.log passes eight cases. | Pass |
| Complete rows and pagination | 80 unique members, 160 beneficiaries, real SQLite, every name exactly once; page X/Y asserted for every page. Initial dense fixture had duplicate ID values and was corrected; those initial fixture failures are not behavior RED evidence. | Pass |
| Complete numbers and printable bounds | Independent pdfplumber extraction checks all 13-digit IDs, dates and phones as whole words and all glyph bounds on all 37 pages; pdf-geometry.json binds exact PDFs. | Pass |
| Thai headings | Parent rendered every page with Poppler, inspected contact sheets plus full-size first/empty/last samples. Corrected relationship, registration, change-date, membership-exit and notes headings to avoid fragmented words. | Pass |
| Monthly history | data-red.log reproduces prior resignation inflating opening count. Final test expects 3 opening, 1 added, 1 resignation, 1 death, 0 expulsion, 2 remaining. Confirmed case and status event collapse to one exit; archived members excluded consistently. | Pass |
| Saved data / placeholders | Relationship reaches its beneficiary row; saved exit date/reason printed as of period end. Spouse, member type, funeral manager and general changes unavailable in schema remain '-'; no sample data or vendor branding inferred. | Pass |
| Group isolation / current leader | Exact 010 vs 0100 query and current normal/nonarchived leader test. UI fixture appoints group 0101's member then shows that name in selector and PDF. Unassigned groups stay selectable with explicit vacancy. | Pass |
| UI buttons / invalid input | Four rendered-form HTTP tests, missing group and invalid/overflow month 400 tests. Four actual browser buttons emit download events; additional packaged-app HTTP calls return PDF attachments with correct filenames. | Pass |
| Existing workflows | One final locked restore, formatting, Release build (zero warnings/errors), full 145 tests and isolated publish. Existing EF drift, migration, backup and coordinator tests included. | Pass |

No unresolved acceptance/implementation contradictions within the four reference reports. Reports use current nonarchived member data; historical group rosters and unrecorded profile attributes cannot be reconstructed and are outside this task. Expulsion is a zero-count category unless an existing terminal event is present; no expulsion workflow was added. Monthly UI accepts four-digit Buddhist Era years, hence Gregorian years above 9456 are rejected.

TDD: actual missing-column/certification RED preceded PDF rewrite; data and HTTP RED preceded parent service/page implementation. Worker added model seams then hit its usage limit; no worker completion claim accepted. Parent corrected its expected summary typo and duplicate-ID dense fixture, completed implementation and reran focused checks. Logs retain unsuccessful harness runs honestly. Production data changes are read-only queries.

PARENT_ADVERSARIAL_READY: yes. REVIEWABILITY: pass, one bounded report family. KNOWN_BLOCKERS: none. Protected external boundaries crossed: none. Source delivery only; generated PDFs and published app are disposable verification fixtures. Compose is not applicable to this single-PC Windows app.
