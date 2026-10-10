# Attendance Phase 1

Both mutation endpoints require the existing Manager policy and return the attendance record:

- `POST /api/attendance/{employeeNumber:int}/check-in`
- `POST /api/attendance/{employeeNumber:int}/check-out`

No body is required. EmployeeNumber identifies the subject; the authenticated manager is the audit actor. Clients cannot supply timestamps or audit identity. Only active subjects can check in. Existing sessions can be checked out after subject deactivation. Duplicate entry and missing open-session exit return HTTP 409 with safe Hebrew ProblemDetails titles.

All persisted timestamps are UTC. Asia/Jerusalem supplies local calendar boundaries with DST rules. Each session has a persisted cutoff at the first local midnight after entry. At or after that boundary it can only close automatically, with effective checkout equal to that cutoff, a null update actor, and CheckoutProcessedAtUtc/UpdatedAtUtc equal to actual processing time. Overnight work requires a new entry. A recovery worker runs immediately on host startup and every 30 seconds; physical processing can lag midnight by that interval while effective checkout remains midnight. Failed recovery is retried with a value-free warning. Attendance requests also recover their subject's overdue session transactionally. A checkout which recovers an old session commits the closure before returning the no-open-session conflict.

Transactions lock Employees in SHARE mode before AttendanceRecords in SHARE ROW EXCLUSIVE mode. This keeps actor/subject state stable against existing employee writers and serializes attendance mutations and recovery across processes. The filtered unique index on EmployeeId where CheckOutAtUtc is null independently enforces one open session. This deliberately simple table-level locking suits Phase 1; it serializes different employees' attendance actions too.

The AddAttendance migration is generated offline using the existing design-time factory. It adds only AttendanceRecords, constraints and indexes. It must be applied through the normal deployment process before running this version of the API; it was not applied during implementation.

Run the backend suite with `dotnet test tests/OptiCore.Tests/OptiCore.Tests.csproj -c Release` from backend. The Attendance PostgreSQL concurrency test follows the existing OPTICORE_VERIFY_DATABASE opt-in convention. It creates and removes only its generated temporary schema in opticore_dev and does not apply migrations or modify public data. Default tests do not connect to PostgreSQL.
