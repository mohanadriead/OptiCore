# AGENTS.md — OptiCore Engineering Guide

## 1. Project identity

**Project:** OptiCore  
**Purpose:** Web-based optical-store management system.

OptiCore is intended to manage the operational side of an optical store, including customers, employees, attendance, eye exams, products, inventory, suppliers, labs, orders, payment records, reports, notifications, and audit history.

OptiCore is **not** the store's fiscal cash-register/accounting system. Actual payment processing and legal fiscal documents are handled by a separate external system.

This file is the authoritative engineering context for AI coding agents working in this repository. Read it before making changes.

## 2. Repository

```text
OptiCore/
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
├── docs/
├── .gitignore
└── README.md
```

The frontend directory exists but frontend implementation has not started yet.

## 3. Current technology stack

### Backend
- .NET 10
- C#
- ASP.NET Core Web API
- Entity Framework Core
- Npgsql
- PostgreSQL 18
- xUnit

### Frontend — planned
- React
- TypeScript
- Vite

### Version control
- Git
- GitHub

## 4. Architectural style

OptiCore is a **modular monolith**.

Do not introduce microservices unless explicitly requested.

### `OptiCore.Domain`
Contains business entities, domain rules, and business behavior.
It must remain independent of EF Core, PostgreSQL, HTTP, React, WhatsApp providers, and cloud vendors.

### `OptiCore.Application`
Contains application use cases, orchestration, and interfaces/abstractions needed by use cases.
Application may depend on Domain.

### `OptiCore.Infrastructure`
Contains EF Core, PostgreSQL, database mappings, external service implementations, and future sync/integration infrastructure.
Infrastructure may depend on Application and Domain.

### `OptiCore.Api`
Contains HTTP/API entry points, endpoints/controllers, authentication/authorization entry point, and DI composition root.
The API should not contain business rules.

### `OptiCore.Tests`
Contains automated tests.

## 5. Dependency direction

```text
OptiCore.Api
├── OptiCore.Application
└── OptiCore.Infrastructure

OptiCore.Infrastructure
├── OptiCore.Application
└── OptiCore.Domain

OptiCore.Application
└── OptiCore.Domain

OptiCore.Tests
├── OptiCore.Application
└── OptiCore.Domain
```

Do not introduce reverse dependencies from Domain to Infrastructure/Application/API.

## 6. Current running infrastructure

PostgreSQL 18 is installed locally.

Development database:

```text
opticore_dev
```

The API is already connected to PostgreSQL through EF Core/Npgsql.

Current health endpoints:

```text
GET /health
GET /health/database
```

Expected responses:

```json
{"status":"ok"}
```

and:

```json
{"status":"ok","database":"connected"}
```

Infrastructure registration is exposed through:

```csharp
builder.Services.AddInfrastructure(builder.Configuration);
```

Database secrets are stored using .NET User Secrets.

**Never commit database passwords, API keys, tokens, certificates, or secrets.**

## 7. Offline architecture decision

OptiCore must continue supporting core store operations during an internet outage.

Chosen architecture:

```text
Store PCs
   │
   │ Local network
   ▼
Local OptiCore server
├── ASP.NET Core backend
├── Local PostgreSQL
└── Sync service
        │
        │ Internet
        ▼
Cloud OptiCore / Cloud PostgreSQL
```

Core business activity must continue locally when the internet is unavailable.
Internet-dependent actions such as WhatsApp should be queued until connectivity returns.

Detailed local/cloud synchronization mechanics are **not yet designed**. Do not invent them unless a task explicitly asks for that design.

## 8. ID strategy

Important entities use globally unique internal identifiers.

### Internal ID
Use UUIDv7 / .NET `Guid`.

Current base entity:

```csharp
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
}
```

### Human-visible identifiers
Human-facing records may also have simple sequential numbers, such as:

```text
CustomerNumber
EmployeeNumber
OrderNumber
```

Do not confuse these with the internal `Id`.

Example:

```text
Id             = UUIDv7 used internally
CustomerNumber = 1057 shown to employees
NationalId     = Israeli ID number
```

Israeli national IDs and phone numbers are strings, not numeric types.

## 9. Auditable entity foundation

Current base hierarchy:

```text
Entity
  └── AuditableEntity
```

`AuditableEntity` contains:

```text
CreatedAtUtc
CreatedByEmployeeId
UpdatedAtUtc
UpdatedByEmployeeId
```

Creation time is non-null.
Updated fields are nullable until a record is edited.

These fields represent creation/last-update metadata only.
They do **not** replace the full Audit Log.

# 10. Confirmed Phase 0 business requirements

## 10.1 Customers — CONFIRMED

Customer fields:

```text
Id                    internal UUIDv7
CustomerNumber        system-generated, unique, human-visible
NationalId            Israeli ID, mandatory, unique
FirstName             mandatory
LastName              mandatory
DateOfBirth           mandatory
MobilePhone           mandatory, not unique
HomePhone             optional
Email                 optional
City                  mandatory
Street                optional
Gender                mandatory
Notes                 optional
WhatsAppConsent       boolean
IsActive              boolean, default true
CreatedAtUtc
CreatedByEmployeeId
UpdatedAtUtc
UpdatedByEmployeeId
```

Additional rules:
- Foreign-customer handling is out of V1 scope.
- A customer cannot be created without `NationalId`.
- Duplicate `NationalId` must be blocked.
- When duplicate NationalId is entered, the system should indicate that the customer already exists.
- Phone number is not unique.
- One mobile phone and one optional home phone per customer.
- One address per customer.
- No house number or postal code in V1.
- Search must support CustomerNumber, NationalId, phone, first name, last name, full name, and email.
- Multiple customers with the same phone number must all appear in matching search results.
- Customers are not hard-deleted during normal business operation.
- Customer removal means deactivation (`IsActive = false`).
- All employees may view customer information.
- No family linking is required.
- Customer preferred language is not stored in V1.

The current `Customer` domain entity already exists and has passing tests.
Do not redesign it without an explicit task.

## 10.2 Employees — CONFIRMED

Employee fields currently include:

```text
Id                    internal UUIDv7
EmployeeNumber        system-generated, unique, human-visible
FirstName
LastName
Username              unique
PasswordHash
Phone
NationalId            mandatory, unique
IsActive
audit metadata
```

Rules:
- Never store plaintext passwords.
- Employee can change their own password.
- Manager can reset an employee password.
- No email/phone password recovery in V1.
- No mandatory first-login password change.
- Concurrent login from multiple computers is allowed.
- Employees are deactivated, not hard-deleted.
- Managers are employees with additional permissions.
- Multiple managers are allowed.
- A manager can modify another manager.
- Only managers may create/deactivate employees.
- The system must never allow zero active managers.
- Initial installation creates one initial manager account.
- That manager creates additional employees/managers.
- V1 assumes one branch.

## 10.3 Permissions — PARTIALLY CONFIRMED

All employees may view customer information.

Permissions primarily restrict actions, for example:
- create/deactivate employees
- change permissions
- add/edit/deactivate products
- inventory adjustments
- cancel orders
- refunds
- reports
- messaging
- discount limits

Employees have individual permissions.
There is also a per-employee maximum discount percentage.

The exact complete permission matrix is still waiting for business clarification.
Do not invent the final permissions list.

## 10.4 Attendance — CONFIRMED

Attendance is separate from the currently logged-in application account.

A dedicated attendance UI must allow employee Y to check in/out while employee X remains logged into the system.

Attendance identification:
- EmployeeNumber
- dedicated attendance PIN

Rules:
- Employees may use OptiCore without being checked in.
- Same-day duplicate Check-in is blocked.
- Check-out without an open shift is blocked.
- Duplicate Check-out is blocked.
- If an old open shift exists from a previous day, show a warning.
- Employee may explicitly start a new shift.
- The old shift becomes `MISSING_CHECKOUT`.
- Do not invent a fake checkout time.
- Manager can correct attendance records later.
- Manager attendance edits must be audited.
- Missing checkout shifts are not counted as valid completed work time until corrected.
- Breaks are not tracked.
- Total work hours are calculated.
- Payroll/salary calculation is outside V1.
- Normal employees see only their own attendance.
- Managers see everyone's attendance.
- No automatic logout due to inactivity.
- V1 supports one branch.

Do not add an unnecessary generic attendance `method` field unless requirements later justify it.

## 10.5 WhatsApp / Notifications — CONFIRMED

V1 messages are Hebrew only.
Arabic may be added later.
No customer preferred-language field in V1.

Supported message types:

```text
ORDER_READY
BIRTHDAY_MONTH
INACTIVE_6_MONTHS
OUTSTANDING_BALANCE
```

### ORDER_READY
- automatic when order becomes READY
- prevent duplicate automatic sends

### BIRTHDAY_MONTH
- automatic/scheduled
- sent during birthday month

### INACTIVE_6_MONTHS
- automatic
- eligible every six months from the later of:
  - last purchase date
  - last INACTIVE_6_MONTHS message

### OUTSTANDING_BALANCE
- manual only
- manager has a screen listing customers with debt
- manager can trigger reminder message

Other requirements:
- save message history
- message history visible from customer card
- manager may manage message templates
- scheduled messages supported
- store send/delivery status when provider supports it
- WhatsApp consent must be represented according to provider/platform requirements

## 10.6 Audit Log — CONFIRMED

Audit these actions:
- customer edits
- eye-exam edits
- order edits
- discounts
- price changes
- payment records
- credits/refunds
- inventory adjustments
- permission changes
- employee changes
- manager attendance corrections

Each audit record should preserve:
- who
- when
- entity
- old value
- new value
- action

Only managers may view audit logs.

Audit records:
- cannot be edited
- cannot be deleted through OptiCore
- should be append-only from the application perspective
- retained indefinitely
- old logs may later be archived but not silently discarded

Never log:
- plaintext passwords
- password hashes
- tokens
- sensitive authentication secrets

## 10.7 Backup / recovery — CONFIRMED

Use managed PostgreSQL backup/recovery capabilities in production where possible.

Requirements:
- automated backups
- Point-in-Time Recovery
- target RPO close to zero
- restore procedure must be tested

Exact cloud hosting/provider has not been selected.
Do not hard-code a provider-specific architecture prematurely.

## 10.8 Payment scope — IMPORTANT

OptiCore is **not** a fiscal POS/cash-register/accounting system.

Actual payment happens in an external system.

OptiCore will NOT:
- process credit-card transactions
- operate a cash drawer
- calculate current physical cash in a till
- issue tax invoices
- issue fiscal receipts
- perform fiscal accounting

OptiCore DOES record payment-related business information:
- item prices
- discounts
- order total
- payment amount
- remaining balance
- payment status
- payment method
- payment date/time
- employee who recorded the payment

At minimum payment method includes:
- Cash
- Credit Card

One order may have multiple payment records.
Actual monetary processing remains external.

# 11. Requirements still waiting for business clarification

Do not invent final rules for:
- Eye Exams
- exact Permissions
- Products
- Inventory
- Suppliers
- Labs
- Orders/Sales
- exact Payments edge cases
- Returns/Refunds
- Reports
- Hardware details
- Existing-data migration

If a task touches one of these areas and the requirement is not explicitly provided, stop and ask rather than guessing.

# 12. Current Customer implementation

The `Customer` domain entity exists.

It currently supports:
- valid construction
- required-field validation
- trimming text values
- optional-value normalization
- update details
- WhatsApp consent changes
- deactivation
- updating last-modified audit metadata

Current automated tests: **6 passing customer tests**.

Do not remove passing tests.

# 13. Coding principles

## Business logic
Keep business rules in Domain where appropriate.
Avoid anemic entities when behavior belongs naturally to the entity.

## Persistence
Use EF Core configuration classes in Infrastructure.
Prefer:

```text
Infrastructure/Persistence/Configurations/
```

Do not put EF attributes/configuration into Domain unless explicitly justified.

## Nullable values
Model optional business fields explicitly.
Do not make required business fields nullable merely to silence compiler warnings.

## Dates
- Use UTC for timestamps.
- Prefer `DateTimeOffset` for auditable timestamps.
- Use `DateOnly` for date-only business values such as DateOfBirth.

## Identifiers
- internal primary key: UUIDv7/Guid
- human-visible numbers: separate fields
- NationalId/phone numbers: strings

## Soft deletion
Where requirements say records are deactivated, do not hard-delete them from normal business workflows.

## Secrets
Never commit:
- DB passwords
- API keys
- WhatsApp credentials
- access/refresh tokens
- private certificates

## Migrations
Migration files should be checked in, but secrets must not be.

## Naming
Use clear English names in code.
Avoid abbreviations unless they are established domain terminology.

# 14. Agent behavior rules

Before editing:
1. Inspect relevant files.
2. Understand current architecture.
3. Read existing tests.
4. Check `git status`.
5. Do not overwrite unrelated user work.

While editing:
- Make the smallest coherent change.
- Do not opportunistically refactor unrelated modules.
- Do not introduce new frameworks/packages unless necessary.
- Do not change business requirements.
- Do not implement unfinished modules unless explicitly instructed.
- Keep Domain free of Infrastructure dependencies.

After editing:
1. Run build.
2. Run tests.
3. Report exact files changed.
4. Report commands executed.
5. Report assumptions.
6. Do not commit or push unless explicitly instructed.

# 15. Required verification commands

For backend tasks, normally run from `backend/`:

```powershell
dotnet build OptiCore.slnx
dotnet test OptiCore.slnx
```

If a database migration is part of the task, also verify that:
- migration generation succeeds
- migration applies successfully to `opticore_dev`
- expected constraints/indexes exist

# 16. Git rules for AI agents

The human developer owns commits and pushes unless explicitly stated otherwise.

Default agent behavior:
- may inspect Git status/diff
- may edit files
- may run safe build/test/database-development commands
- must NOT commit
- must NOT push
- must NOT reset/rebase/delete branches
- must NOT discard unrelated working-tree changes

# 17. Current development philosophy

AI is used to accelerate implementation, not replace engineering understanding.

Prefer:
- small well-defined tasks
- explicit requirements
- reviewable diffs
- automated tests
- explanation of design choices

Avoid:
- giant autonomous rewrites
- speculative architecture
- unnecessary abstraction
- implementing future requirements that are not finalized

If a requirement is ambiguous and materially affects business behavior or data integrity, ask before deciding.
