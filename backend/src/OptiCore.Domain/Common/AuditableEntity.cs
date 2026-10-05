namespace OptiCore.Domain.Common;

public abstract class AuditableEntity : Entity
{
    public DateTimeOffset CreatedAtUtc { get; protected set; }

    public Guid? CreatedByEmployeeId { get; protected set; }

    public DateTimeOffset? UpdatedAtUtc { get; protected set; }

    public Guid? UpdatedByEmployeeId { get; protected set; }

    protected AuditableEntity()
    {
    }

    protected AuditableEntity(Guid? createdByEmployeeId)
    {
        CreatedAtUtc = DateTimeOffset.UtcNow;
        CreatedByEmployeeId = createdByEmployeeId;
    }

    protected void MarkUpdated(Guid employeeId)
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedByEmployeeId = employeeId;
    }
}