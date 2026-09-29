# UI RED / GREEN evidence

Worker: `/root/coordinator_ui`, observed Terra/max; exact terminal rollout bound in worker-runtime.json. Its turn ended at a usage limit before the final report. Parent interrupted the pending lane, took ownership, inspected its files, completed integration, and reran the real HTTP suite. No worker final verdict is claimed.

Focused command used throughout:

```
C:/Users/mooha/.dotnet/dotnet.exe test tests/ChapanakitCare.Domain.Tests/ChapanakitCare.Domain.Tests.csproj --no-restore -m:1 --filter FullyQualifiedName~CoordinatorPageTests --logger console;verbosity=minimal
```

The worker's durable rollout records the exact commands/results; these observations were also sent to the parent before the corresponding production changes:

| Slice | Observed RED | GREEN checkpoint |
| --- | --- | --- |
| Coordinator route and scoped form | GET /Coordinators expected 200 OK, actual 404 NotFound; 1 failed | Route/form plus real appointment POST/PRG, invalid replacement context and readable history: 4 passing |
| Library role filter and dashboard | /Members?RoleFilter=chairperson returned both fixture member rows; 1 failed / 4 passed | Cross-page holder, role column/filter and vacancy display implemented and later HTTP checks passed |
| Explicit replacement / ending | 2 failed action-specific feedback assertions / 6 passed | Correct action-specific feedback implemented; later 8 passing |
| Navigation | Rendered nav lacked href="/Coordinators"; 1 failed / 8 passed | Parent enabled the existing navigation anchor after observing this failure |

Parent completion: preserved saved relative column order including actions; inserted a missing role column beside name; assigned unique radio IDs; displayed all model-binding errors; matched textarea length to the existing service limit; added scoped responsive CSS. These low-impact presentation and preference adjustments are inspected in the actual browser rather than tested by mirroring JavaScript implementation text. No database/role rule depends on JavaScript.

Parent integrated GREEN and final exact-candidate evidence are in integrated-focused.log and the candidate receipt. Core role behavior, constraints, migration and lifecycle RED/GREEN evidence is separately recorded in ledger.md.
