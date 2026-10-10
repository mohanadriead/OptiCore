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

    public AttendanceCorrection Correct(DateTimeOffset checkIn, DateTimeOffset? checkOut,
        DateTimeOffset midnight, Guid actor, DateTimeOffset now, string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000)
            throw new ArgumentException("סיבת התיקון נדרשת ועד 2000 תווים.");
        if (actor == Guid.Empty || now.Offset != TimeSpan.Zero || checkIn.Offset != TimeSpan.Zero ||
            midnight.Offset != TimeSpan.Zero || (checkOut.HasValue && checkOut.Value.Offset != TimeSpan.Zero))
            throw new ArgumentException("התיקון דורש עובד מזוהה וזמנים ב־UTC.");
        if (checkIn > now || midnight <= checkIn || checkOut < checkIn || checkOut > now || checkOut > midnight)
            throw new ArgumentException("זמני הנוכחות אינם תקינים. היציאה חייבת להיות לאחר הכניסה ועד חצות.");
        if (checkOut is null && (CheckOutAtUtc is not null || midnight <= now))
            throw new ArgumentException("לא ניתן לפתוח מחדש רשומה סגורה או להשאיר כניסה פתוחה לאחר חצות.");
        var previousCheckIn = CheckInAtUtc;
        var previousCheckOut = CheckOutAtUtc;
        // Keeping an automatic boundary keeps its original processing time. An override becomes
        // a manual checkout; the correction timestamp is its actual processing time.
        var remainsAutomatic = WasCheckoutAutomatic && checkOut == midnight;
        var processed = checkOut is null ? (DateTimeOffset?)null : remainsAutomatic && CheckoutProcessedAtUtc >= checkOut
            ? CheckoutProcessedAtUtc : now;
        CheckInAtUtc = checkIn;
        CheckOutAtUtc = checkOut;
        AutomaticCheckoutDueAtUtc = midnight;
        CheckoutProcessedAtUtc = processed;
        WasCheckoutAutomatic = remainsAutomatic;
        UpdatedAtUtc = now;
        UpdatedByEmployeeId = actor;
        return new AttendanceCorrection(this, previousCheckIn, previousCheckOut, actor, now, reason.Trim());
    }
}
