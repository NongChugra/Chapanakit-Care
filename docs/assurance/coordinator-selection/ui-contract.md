# Backend contract for the UI lane

Namespace: `ChapanakitCare.Infrastructure.Coordinators`.

`CoordinatorRoles.Chairperson = "chairperson"`, `CoordinatorRoles.GroupLeader = "group_leader"`.

`CoordinatorPositionView(string Key, string RoleCode, string? GroupNo, Guid? MemberId, string? MemberName, string? RunNo, DateOnly? AppointedOn, int Version, int MemberCount)` exposes computed `RoleLabel` and `ScopeLabel`. Chairperson key is `chairperson`; leader key is `group:` + exact GroupNo. Vacant never-assigned positions have version 0. MemberCount counts normal nonarchived members in that scope. Positions includes one chairperson plus every nonblank group from nonarchived membership or stored positions.

`CoordinatorRoleView(string PositionKey, string RoleCode, string? GroupNo)` exposes `RoleLabel`, `ScopeLabel`.

`CoordinatorCandidateView(Guid Id, string RunNo, string Name, string? GroupNo)`.

`CoordinatorChangeCommand(string PositionKey, string Action, Guid? MemberId, int ExpectedVersion, Guid? ExpectedMemberId, string? Reason)`; Action is `appoint`, `replace`, or `end`. For end MemberId is null. ExpectedMemberId is the incumbent shown on the form (null for vacancy). Never perform replacement via appoint. Reason required for replace/end.

`CoordinatorApplicationService(AppDbContext database)`:
- `Task<IReadOnlyList<CoordinatorPositionView>> GetPositionsAsync(CancellationToken ct = default)`
- `Task<IReadOnlyDictionary<Guid, CoordinatorRoleView>> GetMemberRolesAsync(CancellationToken ct = default)`
- `Task<IReadOnlyList<CoordinatorCandidateView>> SearchCandidatesAsync(string positionKey, string? search, CancellationToken ct = default)` returns eligible normal nonarchived members with no current role, same GroupNo for leaders; limited to 100 results, sorted RunNo. Support number/name search.
- `Task<IReadOnlyList<ChapanakitCare.Domain.Entities.CoordinatorEvent>> GetHistoryAsync(CancellationToken ct = default)` newest 100 events.
- `Task ChangeAsync(CoordinatorChangeCommand command, DateOnly businessDate, DateTimeOffset now, string actor, CancellationToken ct = default)` throws existing `MemberValidationException` with Thai user-facing message on validation, stale write, or duplicate-role constraint. UI must preserve validation context; use PRG/TempData on success. Server validates all posted fields, regardless of HTML state.

`CoordinatorEvent` read properties: Id Guid, PositionKey string, RoleCode string, GroupNo string?, Action string (`appoint`, `replace`, `end`), PreviousMemberId Guid?, PreviousMemberName string?, PreviousRunNo string?, MemberId Guid?, MemberName string?, MemberRunNo string?, EffectiveDate DateOnly, OccurredAtUtc DateTimeOffset, Actor string, Reason string?, PositionVersion int. Names/numbers are historical snapshots.

Use current date from `DateOnly.FromDateTime(DateTime.Today)` and now `DateTimeOffset.UtcNow`, actor `ผู้ใช้ในเครื่อง` consistent with local app. Display Thai dates with existing helpers. No role writes from library/dashboard; link to coordinator page scoped member/group if useful. No extra roles or group-create controls. The approved prototype is a visual reference only; enforce latest one-role/same-group constraints throughout labels/forms.
