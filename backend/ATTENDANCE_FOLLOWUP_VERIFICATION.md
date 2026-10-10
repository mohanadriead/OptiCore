# Attendance follow-up verification

Branch: `feat/attendance`. No branch switches, merges, database migrations, or database record writes were performed.

Preflight verified the configured local development PostgreSQL database `opticore` on localhost port 5432. A read-only transaction selected `20261010082618_AddAttendance` from `__EFMigrationsHistory`, confirmed it was applied, and rolled back. No credentials or connection strings were displayed. The existing migration and its designer are unchanged.

The additive `20261010200538_AddAttendanceCorrections` migration was generated offline and was **not applied**. Its five Up operations only affect AttendanceRecords and AttendanceCorrections. References to Employees are restrictive foreign keys on the new correction table; no Employee table alterations occur. The open-session partial unique index is unchanged. The model snapshot matches the generated migration.

| Check | Result |
| --- | --- |
| `dotnet build backend/OptiCore.slnx -c Release --no-restore` | Passed; 0 warnings, 0 errors |
| Full backend suite, Release, PostgreSQL opt-in disabled | 138 passed, 0 failed, 3 skipped; 141 total |
| `npm.cmd run build` in frontend | Passed; Vite warns about the existing application bundle exceeding 500 kB |
| `npm.cmd run lint` in frontend | Passed |
| `npm.cmd run test:run` in frontend | 232 passed, 0 failed, 0 skipped; 10 test files |
| `dotnet ef migrations has-pending-model-changes` with Release/offline factory | Passed; no pending model changes |
| `git diff --check` | Passed |

The three skipped PostgreSQL opt-in tests are:

- `AttendancePostgresTests.PostgresIndexAndTransactionsProtectConcurrentAttendanceAndRecovery` — includes real SQL history projections, concurrent entry/exit/recovery, the partial unique index, correction persistence, and atomic record/audit rollback.
- `EmployeePostgresTests.AppliedMigrationHasExpectedPublicConstraints`.
- `EmployeePostgresTests.IdentityUniqueConstraintsAndConcurrentManagerRemoval`.

These tests require separate authorization and a configured isolated development test database named `opticore_dev`. They were explicitly disabled with `OPTICORE_VERIFY_DATABASE=0`; the approved read-only history query does not authorize them. Real PostgreSQL correction/locking tests have not been executed.

An earlier backend run reported 107 passed, 27 failed, 3 skipped because Windows Smart App Control blocked the API assembly. The final build and full run succeeded without changing application-control settings. An initial frontend run reported 231 passed and 1 failed because a test matched session-loading feedback before history-loading feedback; the corrected full suite passed.

Attendance table-wide locks are deliberately retained after review to preserve the existing lock-order protocol with Employee writers and recovery. They serialize mutations across employees; a narrower locking design needs separate cross-process verification. See [Attendance behavior and API](ATTENDANCE.md) for correction semantics, date filters, UTC/Israel conversion, and deployment prerequisites.

The changed-file list below is limited to Attendance implementation, its integration into existing authorization/navigation/error handling, its additive migration and snapshot, tests, and documentation.

- backend/ATTENDANCE.md
- backend/ATTENDANCE_FOLLOWUP_VERIFICATION.md
- backend/src/OptiCore.Api/Endpoints/Attendance/AttendanceEndpoints.cs
- backend/src/OptiCore.Api/Errors/ApiExceptionHandler.cs
- backend/src/OptiCore.Application/Attendance/AttendanceContracts.cs
- backend/src/OptiCore.Application/Attendance/AttendanceMidnightPolicy.cs
- backend/src/OptiCore.Application/Attendance/AttendanceService.cs
- backend/src/OptiCore.Application/Attendance/IAttendanceRepository.cs
- backend/src/OptiCore.Application/Attendance/IAttendanceService.cs
- backend/src/OptiCore.Domain/Attendance/AttendanceCorrection.cs
- backend/src/OptiCore.Domain/Attendance/AttendanceRecord.cs
- backend/src/OptiCore.Infrastructure/Persistence/Configurations/AttendanceCorrectionConfiguration.cs
- backend/src/OptiCore.Infrastructure/Persistence/Configurations/AttendanceRecordConfiguration.cs
- backend/src/OptiCore.Infrastructure/Persistence/Migrations/20261010200538_AddAttendanceCorrections.cs
- backend/src/OptiCore.Infrastructure/Persistence/Migrations/20261010200538_AddAttendanceCorrections.Designer.cs
- backend/src/OptiCore.Infrastructure/Persistence/Migrations/OptiCoreDbContextModelSnapshot.cs
- backend/src/OptiCore.Infrastructure/Persistence/OptiCoreDbContext.cs
- backend/src/OptiCore.Infrastructure/Persistence/Repositories/AttendanceRepository.cs
- backend/tests/OptiCore.Tests/Attendance/AttendanceApiTests.cs
- backend/tests/OptiCore.Tests/Attendance/AttendanceCorrectionTests.cs
- backend/tests/OptiCore.Tests/Attendance/AttendancePersistenceTests.cs
- backend/tests/OptiCore.Tests/Attendance/AttendancePostgresTests.cs
- backend/tests/OptiCore.Tests/Attendance/AttendanceServiceTests.cs
- frontend/src/app/router.tsx
- frontend/src/components/layout/AppShell.tsx
- frontend/src/features/attendance/attendanceApi.ts
- frontend/src/features/attendance/AttendanceManagementPage.tsx
- frontend/src/features/attendance/AttendancePage.tsx
- frontend/src/features/attendance/attendanceTime.ts
- frontend/src/lib/apiClient.ts
- frontend/src/test/attendance.test.tsx
- frontend/src/test/attendanceTime.test.ts
