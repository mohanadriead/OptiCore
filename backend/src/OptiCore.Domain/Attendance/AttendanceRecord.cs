using OptiCore.Domain.Common;

namespace OptiCore.Domain.Attendance;

public sealed class AttendanceRecord : AuditableEntity
{
    public Guid EmployeeId { get; private set; }
    public DateTimeOffset CheckInAtUtc { get; private set; }
    public DateTimeOffset AutomaticCheckoutDueAtUtc { get; private set; }
    public DateTimeOffset? CheckOutAtUtc { get; private set; }
    public DateTimeOffset? CheckoutProcessedAtUtc { get; private set; }
    public bool WasCheckoutAutomatic { get; private set; }

    private AttendanceRecord() { }

    public AttendanceRecord(Guid employeeId, Guid actor, DateTimeOffset now, DateTimeOffset midnight)
    {
        if (employeeId == Guid.Empty || actor == Guid.Empty) throw new ArgumentException("Employee and actor are required.");
        if (now.Offset != TimeSpan.Zero || midnight.Offset != TimeSpan.Zero || midnight <= now)
            throw new ArgumentException("Attendance requires UTC timestamps and a future midnight.");
        EmployeeId = employeeId;
        CheckInAtUtc = CreatedAtUtc = now;
        AutomaticCheckoutDueAtUtc = midnight;
        CreatedByEmployeeId = actor;
    }

    public void CheckOut(DateTimeOffset effective, DateTimeOffset processed, Guid? actor)
    {
        if (CheckOutAtUtc is not null) throw new InvalidOperationException("Attendance is already closed.");
        if (effective.Offset != TimeSpan.Zero || processed.Offset != TimeSpan.Zero || effective < CheckInAtUtc || processed < effective)
            throw new ArgumentException("Invalid checkout timestamps.");
        if (actor == Guid.Empty || (actor is null && effective != AutomaticCheckoutDueAtUtc) ||
            (actor is not null && effective >= AutomaticCheckoutDueAtUtc))
            throw new ArgumentException("Invalid checkout actor or boundary.");
        CheckOutAtUtc = effective;
        CheckoutProcessedAtUtc = UpdatedAtUtc = processed;
        UpdatedByEmployeeId = actor;
        WasCheckoutAutomatic = actor is null;
    }
}
