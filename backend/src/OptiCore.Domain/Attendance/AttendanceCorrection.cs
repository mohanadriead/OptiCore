using OptiCore.Domain.Common;

namespace OptiCore.Domain.Attendance;

public sealed class AttendanceCorrection : Entity
{
    public Guid AttendanceRecordId { get; private set; }
    public DateTimeOffset PreviousCheckInAtUtc { get; private set; }
    public DateTimeOffset? PreviousCheckOutAtUtc { get; private set; }
    public DateTimeOffset NewCheckInAtUtc { get; private set; }
    public DateTimeOffset? NewCheckOutAtUtc { get; private set; }
    public DateTimeOffset CorrectedAtUtc { get; private set; }
    public Guid CorrectedByEmployeeId { get; private set; }
    public string Reason { get; private set; } = string.Empty;

    private AttendanceCorrection() { }

    internal AttendanceCorrection(AttendanceRecord row, DateTimeOffset previousCheckIn,
        DateTimeOffset? previousCheckOut, Guid actor, DateTimeOffset now, string reason)
    {
        AttendanceRecordId = row.Id;
        PreviousCheckInAtUtc = previousCheckIn;
        PreviousCheckOutAtUtc = previousCheckOut;
        NewCheckInAtUtc = row.CheckInAtUtc;
        NewCheckOutAtUtc = row.CheckOutAtUtc;
        CorrectedAtUtc = now;
        CorrectedByEmployeeId = actor;
        Reason = reason;
    }
}
