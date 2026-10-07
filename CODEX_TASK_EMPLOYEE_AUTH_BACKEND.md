# Codex Task — Employee + Authentication Backend v1

Read `AGENTS.md` completely before doing anything else.

## Goal

Implement the backend foundation for Employees and Authentication in OptiCore.

This task must add:
- Employee domain model
- Employee PostgreSQL persistence
- initial manager bootstrap
- secure password hashing
- cookie-based login/logout/current-user endpoints
- employee self password change
- manager-only employee creation
- manager-only employee deactivation
- manager-only manager-status changes
- manager-only employee password reset
- prevention of zero active managers
- replacement of the temporary Customer `X-Employee-Id` mechanism with authenticated employee identity

This task is backend-focused.

Do not build the Employee frontend in this task.
Do not implement Attendance yet.
Do not implement the future fine-grained permission matrix yet.
Do not implement password recovery by email/phone.
Do not implement payroll.
Do not redesign Customer business rules.

## 1. Current project state

Repository:

```text
OptiCore/
├── AGENTS.md
├── backend/
│   ├── OptiCore.slnx
│   ├── src/
│   │   ├── OptiCore.Api/
│   │   ├── OptiCore.Application/
│   │   ├── OptiCore.Domain/
│   │   └── OptiCore.Infrastructure/
│   └── tests/
│       └── OptiCore.Tests/
├── frontend/
└── docs/
```

Current branch:

```text
feat/employee-auth
```

Current backend stack:
- .NET 10
- ASP.NET Core
- EF Core
- Npgsql
- PostgreSQL 18
- xUnit

Existing Customer vertical slice is complete and merged.

Current Customer mutating endpoints temporarily read:

```text
X-Employee-Id
```

That temporary mechanism must be removed by the end of this task and replaced by authenticated employee identity.

Existing backend test count before this task:

```text
27 passing tests
```

## 2. Confirmed Employee business rules

Every Employee has:

```text
internal Id (Guid / inherited Entity Id)
EmployeeNumber
FirstName
LastName
Username
PasswordHash
Phone
NationalId
IsActive
IsManager
```

Rules:
- `EmployeeNumber` is generated automatically and unique.
- `Username` is required and unique.
- `NationalId` is required and unique.
- `FirstName`, `LastName`, `Phone`, and password are required.
- Employees are never hard-deleted; they are deactivated.
- A manager is a normal Employee with manager privileges.
- There may be multiple active managers.
- A manager may promote/demote another employee/manager.
- The system must never allow zero active managers.
- Only a manager may create employees.
- Only a manager may deactivate employees.
- Only a manager may reset another employee's password.
- An employee may change their own password.
- No password recovery by email/phone in V1.
- No forced password change on first login in V1.
- The same employee may be logged in on multiple computers simultaneously.
- Only one branch exists in V1.

Do not add detailed permissions such as discount limits, inventory rights, etc. Those requirements are not finalized yet.

## 3. Architecture rules

Preserve current layering:

```text
Api
  ↓
Application
  ↓
Domain

Infrastructure implements Application abstractions
and owns EF/PostgreSQL/security implementation details.
```

Rules:
- Domain must not depend on ASP.NET, EF Core, Npgsql, or HTTP.
- Application must not depend on EF Core/Npgsql.
- API must stay thin.
- Infrastructure owns EF mappings/repositories.
- Do not put EF attributes on Domain entities.
- Do not introduce MediatR, AutoMapper, FluentValidation, or ASP.NET Core Identity's full user-store system.
- Do not replace the existing architecture with ASP.NET Identity entities/tables.
- Prefer explicit services and small abstractions.

Using ASP.NET Core's `PasswordHasher<TUser>` behind an OptiCore abstraction is acceptable.

## 4. Inspect before editing

Before modifying files, inspect at minimum:

```text
AGENTS.md

backend/src/OptiCore.Domain/Common/Entity.cs
backend/src/OptiCore.Domain/Common/AuditableEntity.cs
backend/src/OptiCore.Domain/Customers/Customer.cs

backend/src/OptiCore.Application/Customers/
backend/src/OptiCore.Application/DependencyInjection.cs

backend/src/OptiCore.Infrastructure/DependencyInjection.cs
backend/src/OptiCore.Infrastructure/Persistence/OptiCoreDbContext.cs
backend/src/OptiCore.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs
backend/src/OptiCore.Infrastructure/Persistence/Repositories/CustomerRepository.cs

backend/src/OptiCore.Api/Program.cs
backend/src/OptiCore.Api/Endpoints/Customers/
backend/tests/OptiCore.Tests/
```

Also run:

```text
git status
git diff
git branch --show-current
```

Do not overwrite unrelated work.

## 5. Employee domain model

Create an Employee module under:

```text
backend/src/OptiCore.Domain/Employees/
```

Employee should inherit from the existing auditable/domain base as appropriate.

Suggested properties:

```text
Guid Id
int EmployeeNumber
string FirstName
string LastName
string Username
string NormalizedUsername
string PasswordHash
string Phone
string NationalId
bool IsActive
bool IsManager
```

Use existing audit base-class properties rather than duplicating them.

Domain behavior should include explicit methods, not public setters, for at least:

```text
ChangePasswordHash(...)
Deactivate(...)
SetManagerStatus(...)
```

The Employee entity must not hash passwords itself.

## 6. Normalization / uniqueness

Treat usernames case-insensitively.

Recommended design:

```text
Username
NormalizedUsername
```

where `NormalizedUsername` is produced by a deterministic invariant normalization such as trim + uppercase invariant and has a unique database index.

Do not allow case variants such as:

```text
mohanad
MOHANAD
Mohanad
```

to create separate accounts.

NationalId is also unique.

Do not add Israeli ID checksum validation in this task unless it already exists as a shared finalized rule.

## 7. Password security

Define an Application abstraction such as:

```text
IPasswordHasher
```

with `Hash` and `Verify` capabilities.

Infrastructure should implement it using a proven framework hasher such as ASP.NET Core `PasswordHasher<Employee>`.

Do not:
- store plaintext passwords
- log passwords
- return password hashes in DTOs
- expose password hashes in API responses
- build a custom cryptographic hashing algorithm

Technical password baseline for this task:

```text
minimum length: 8 characters
maximum length: 128 characters
```

Do not invent uppercase/symbol/number composition rules yet.

## 8. Employee persistence

Add:

```text
DbSet<Employee> Employees
```

and EF configuration.

Suggested table:

```text
Employees
```

Requirements:
- `Id` UUID primary key
- `EmployeeNumber` database-generated integer
- unique `EmployeeNumber`
- unique `NormalizedUsername`
- unique `NationalId`
- required fields mapped with reasonable max lengths
- `PasswordHash` required
- `IsActive` required
- `IsManager` required
- audit fields mapped consistently with Customers
- no hard-delete behavior

Create a normal EF migration for this feature.

Do not modify the existing Customer migration.

## 9. Employee Application abstractions

Create a clear Application module such as:

```text
backend/src/OptiCore.Application/Employees/
```

Expected concepts may include:

```text
IEmployeeRepository
IEmployeeService
EmployeeService
EmployeeDto
CreateEmployeeRequest
ResetEmployeePasswordRequest
ChangeOwnPasswordRequest
SetManagerStatusRequest
EmployeeNotFoundException
DuplicateUsernameException
DuplicateEmployeeNationalIdException
LastActiveManagerException
InvalidCredentialsException
InactiveEmployeeException
IPasswordHasher
```

Exact filenames may vary, but keep the design explicit and small.

Employee DTO must never include password or password hash.

## 10. Employee repository

Application abstraction should support:
- create
- get by EmployeeNumber
- get by Id
- get by normalized username
- check username existence
- check NationalId existence
- list employees
- count active managers
- save changes

Use `CancellationToken`.

Do not expose EF types/IQueryable/DbSet outside Infrastructure.

Use `AsNoTracking()` for read-only operations where appropriate and tracked entities for mutations.

Translate database unique violations into meaningful Application exceptions as a race-condition backstop.

## 11. Initial manager bootstrap

Implement a one-time bootstrap mechanism.

Requirements:
- Bootstrap only if the Employees table is empty.
- Do not create a manager on every startup.
- Bootstrap details must not be hard-coded in source.
- Read initial manager data from User Secrets/environment configuration.
- Never print the bootstrap password.
- Hash the password before persistence.
- Bootstrap Employee must be active and manager.
- After the first employee exists, bootstrap configuration must not overwrite/recreate accounts.
- If Employees is empty but required bootstrap configuration is missing/invalid, fail startup clearly rather than creating insecure defaults.

Suggested keys:

```text
BootstrapManager:FirstName
BootstrapManager:LastName
BootstrapManager:Username
BootstrapManager:Password
BootstrapManager:Phone
BootstrapManager:NationalId
```

Document how the developer sets these through User Secrets.

Do not commit real credentials.

## 12. Authentication model

Use ASP.NET Core cookie authentication.

Do not store auth tokens in localStorage.

Login flow:

```text
POST /api/auth/login
username + password
        ↓
validate employee
        ↓
employee must be active
        ↓
issue HttpOnly authentication cookie
```

Cookie should be:

```text
HttpOnly = true
SameSite = Lax
```

Use secure cookie behavior appropriate to environment:
- production requires HTTPS
- local HTTP development remains usable

Do not implement "remember me" in this task.

Multiple simultaneous sessions must be allowed.

Do not implement automatic inactivity logout.

## 13. Authentication claims

Include enough claims to identify the current employee:

```text
Employee Id (Guid)
EmployeeNumber
Username
Manager role/status
```

Use standard claim types where reasonable:

```text
ClaimTypes.NameIdentifier
ClaimTypes.Name
ClaimTypes.Role
```

Role value:

```text
Manager
```

is acceptable for the current coarse-grained authorization.

Do not model future detailed permissions yet.

## 14. Auth endpoints

Implement:

```http
POST /api/auth/login
POST /api/auth/logout
GET  /api/auth/me
POST /api/auth/change-password
```

### Login

Body:

```json
{
  "username": "manager",
  "password": "..."
}
```

Behavior:

```text
200 OK           valid active employee
401 Unauthorized invalid username/password
403 Forbidden    inactive employee
400 Bad Request  malformed request
```

Return a safe current-user DTO.

Avoid unnecessarily revealing whether a username exists.

### Logout

Require authentication.

Clear cookie.

Return:

```text
204 No Content
```

### Me

Require authentication.

Return current employee identity without password/hash.

### Change own password

Require authentication.

Body:

```json
{
  "currentPassword": "...",
  "newPassword": "..."
}
```

Requirements:
- verify current password
- validate new password baseline
- replace hash
- record updater as same authenticated employee
- never return password/hash

## 15. Employee manager API

Create a manager-only group:

```text
/api/employees
```

Implement at minimum:

```http
POST  /api/employees
GET   /api/employees
GET   /api/employees/{employeeNumber:int}
PATCH /api/employees/{employeeNumber:int}/deactivate
PATCH /api/employees/{employeeNumber:int}/manager-status
POST  /api/employees/{employeeNumber:int}/reset-password
```

### Create employee

Manager-only.

Input:

```text
FirstName
LastName
Username
Password
Phone
NationalId
IsManager
```

Behavior:

```text
201 Created
400 invalid input
409 duplicate username or NationalId
```

Database generates EmployeeNumber.

Audit creator is authenticated manager.

### List employees

Manager-only.

Return all employees including inactive ones.

A simple list is sufficient.

### Get employee

Manager-only.

Return safe EmployeeDto.

### Deactivate employee

Manager-only.

Soft deactivate.

Already inactive may be idempotent.

Must not leave zero active managers.

### Manager status

Manager-only.

Body:

```json
{
  "isManager": true
}
```

Allow promote/demote.

Must not allow demoting the last active manager.

### Reset password

Manager-only.

Body:

```json
{
  "newPassword": "..."
}
```

No current password required.

Do not force first-login password change in V1.

## 16. Prevent zero active managers

This is a confirmed invariant.

Before:
- deactivating an active manager
- demoting an active manager

check whether another active manager exists.

If action would produce zero active managers:

```text
409 Conflict
```

with a dedicated exception such as:

```text
LastActiveManagerException
```

Concurrency matters.

At minimum:
- keep check and mutation inside one database transaction
- use isolation/locking sufficient to prevent two concurrent manager-removal operations from both succeeding

Do not rely only on a non-transactional count followed by save.

Document the concurrency approach in the completion report.

## 17. Current authenticated employee helper

Replace Customer-specific `EmployeeIdHeader`.

Create one centralized way to read authenticated employee identity from claims, such as:

```text
ICurrentEmployee
CurrentEmployeeAccessor
```

or a small claims helper.

Requirements:
- source employee Id from auth claims only
- never accept employee identity from body/header/query for audit attribution
- invalid/missing identity fails safely

## 18. Replace Customer X-Employee-Id

Remove temporary `X-Employee-Id` from Customer mutation endpoints.

Customer create/update/WhatsApp-consent/deactivate must use authenticated employee Id.

All Customer endpoints should now require authentication because customer data is internal employee-only data.

Confirmed rule:

```text
all employees may view all customer information
```

Therefore:
- authenticated Employee may read/search customers
- authenticated Employee may perform currently-allowed Customer mutations
- future fine-grained action permissions are not part of this task

Do not add fake permissions.

## 19. Authorization

Use ASP.NET authorization.

At minimum:
- authenticated employee policy
- Manager-only policy/role

Employee-management endpoints are Manager-only.

Customer endpoints require authentication but not Manager for the current operations.

## 20. Frontend compatibility note

Do not modify frontend source in this backend task.

The current frontend still sends `X-Employee-Id` and has no login UI.

Therefore after this backend task, the Customer frontend will intentionally need the next frontend-auth task before browser mutations work again.

Do not preserve insecure compatibility by continuing to trust `X-Employee-Id`.

Document this clearly.

## 21. Error handling

Generalize API error handling if needed.

Map at minimum:

```text
invalid request/domain input          -> 400
invalid credentials                   -> 401
not authenticated                     -> 401
inactive employee                     -> 403
not authorized / manager required     -> 403
employee not found                    -> 404
duplicate username                    -> 409
duplicate employee NationalId         -> 409
last active manager invariant         -> 409
unexpected exceptions                 -> safe 500
```

Do not expose stack traces, password data, hashes, or DB details.

## 22. Tests

Keep all existing 27 tests passing unless authenticated setup requires safe updates without removing behavior coverage.

Add coverage for at least:

1. Employee creation initializes required fields.
2. Username normalization is deterministic.
3. Duplicate username rejected.
4. Duplicate NationalId rejected.
5. Password is hashed; plaintext is not stored.
6. Correct password verifies.
7. Incorrect password fails.
8. Employee can change own password with correct current password.
9. Wrong current password rejects change.
10. Manager can reset employee password.
11. Employee deactivation works.
12. Manager promotion works.
13. Demoting last active manager rejected.
14. Deactivating last active manager rejected.
15. Removing one of multiple active managers succeeds.
16. Inactive employee cannot authenticate.
17. Login succeeds and issues authentication.
18. Invalid login returns 401.
19. Inactive login returns 403.
20. `/api/auth/me` requires authentication.
21. Logout clears authentication.
22. Employee management rejects unauthenticated request.
23. Employee management rejects non-manager.
24. Manager can create employee.
25. Customer endpoint rejects unauthenticated request.
26. Authenticated normal employee can read/search customers.
27. Customer mutation audit uses authenticated employee Id, not supplied identity.

Add more focused tests if useful.

## 23. Development database migration

Generate Employee migration using existing EF tooling.

Before applying:
- build
- inspect migration
- confirm it does not alter/drop Customer data unexpectedly

Applying migration to `opticore_dev` is allowed.

Do not delete existing Customer rows.

Do not print connection strings/passwords.

## 24. Bootstrap verification

If bootstrap secrets are not already present, Codex must not invent real credentials or write secrets without approval.

It may:
- implement mechanism
- document exact `dotnet user-secrets set ...` commands with placeholders
- stop before runtime bootstrap verification if human values are required

Do not commit secrets.

## 25. Dependency registration

Keep composition clean.

Conceptually:

```csharp
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuthentication(...);
builder.Services.AddAuthorization(...);
```

Register employee service, repository, password hasher, current-employee accessor/helper, and bootstrap service if used.

Do not put business logic in `Program.cs`.

## 26. Documentation

Update backend developer docs as needed for:
- Employee/Auth endpoints
- bootstrap manager configuration
- User Secrets setup
- frontend auth being next
- removal of `X-Employee-Id`

Do not document real secret values.

## 27. Verification

From `backend/`:

```powershell
dotnet build OptiCore.slnx
dotnet test OptiCore.slnx
```

Also run:

```powershell
dotnet ef migrations list --project src/OptiCore.Infrastructure --startup-project src/OptiCore.Api
```

and after migration inspection/approval:

```powershell
dotnet ef database update --project src/OptiCore.Infrastructure --startup-project src/OptiCore.Api
```

Run:

```powershell
git diff --check
git status --short
```

If startup/bootstrap cannot be verified without user-provided secrets, report that instead of fabricating them.

## 28. Git / safety rules

Do not:
- commit
- push
- switch branches
- merge
- rebase
- reset
- delete branches
- expose User Secrets
- print passwords
- print connection strings
- hard-delete Employees
- hard-delete Customers
- modify frontend source
- implement Attendance
- implement detailed permissions
- implement payroll
- create insecure fallback manager credentials

The human developer will review and commit manually.

## 29. Completion criteria

Complete when:
- Employee domain model exists.
- Employee persistence exists.
- Employee migration exists and is reviewed.
- EmployeeNumber is generated and unique.
- Username is case-insensitively unique.
- NationalId is unique.
- Passwords are securely hashed.
- Initial manager bootstrap mechanism exists.
- Login works via HttpOnly cookie.
- Logout works.
- `/api/auth/me` works.
- Employee can change own password.
- Manager can create employees.
- Manager can deactivate employees.
- Manager can promote/demote manager status.
- Manager can reset passwords.
- Zero active managers is prevented transactionally.
- Employee management is manager-only.
- Customer endpoints require authentication.
- Customer mutation audit uses authenticated employee identity.
- `X-Employee-Id` is removed from backend.
- Existing Customer business behavior remains unchanged.
- backend build passes.
- all backend tests pass.
- no frontend files changed.
- no secrets exposed.

## 30. Completion report

When finished, report:
1. Files created
2. Files modified
3. Employee domain design
4. Database schema/indexes added
5. Migration name and summary
6. Password hashing design
7. Bootstrap-manager design
8. Authentication cookie configuration
9. Claims/current-employee design
10. Auth endpoints and status codes
11. Employee endpoints and authorization rules
12. Zero-manager prevention and concurrency handling
13. Customer API authentication changes
14. Tests added/updated
15. Exact commands run
16. Build result
17. Test result
18. Migration/apply result
19. Whether bootstrap runtime verification was performed or awaits human secrets
20. Assumptions/technical decisions
21. Human review notes
22. Confirmation that no frontend changes, commit, push, branch change, secret exposure, hard delete, Attendance implementation, or fine-grained permission work occurred

Do not commit or push.
