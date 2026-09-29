using ChapanakitCare.Domain.Entities;

namespace ChapanakitCare.Infrastructure.Members;

internal static class ActivityAudit
{
    internal static AuditEvent Create(string action, string entityType, string entityId,
        DateTimeOffset now, string actor, string? reason = null) => new()
    {
        Id = Guid.NewGuid(), OperationId = Guid.NewGuid(), Action = action,
        EntityType = entityType, EntityId = entityId, OccurredAtUtc = now,
        ActorUserId = "local-user", ActorDisplayName = actor,
        MachineName = Environment.MachineName, AppVersion = "activity-history-v1", Reason = reason
    };
}
