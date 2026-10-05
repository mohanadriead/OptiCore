# Codex Task — Customer Persistence

Read `AGENTS.md` at the repository root before doing anything else.

## Goal

Implement the PostgreSQL / Entity Framework Core persistence layer for the already-existing `Customer` domain entity.

This is intentionally a narrow task.

Do not implement employees, eye exams, products, inventory, orders, permissions, reports, frontend code, or synchronization.

## Current state

The repository already contains:
- `OptiCore.Domain`
- `OptiCore.Application`
- `OptiCore.Infrastructure`
- `OptiCore.Api`
- `OptiCore.Tests`
- PostgreSQL connection
- `OptiCoreDbContext`
- infrastructure DI setup
- `Customer` domain entity
- base `Entity` with UUIDv7 ID
- `AuditableEntity`
- 6 passing Customer tests

Development database:

```text
opticore_dev
```

## Requirements

### 1. Inspect before editing

Inspect at minimum:
- `backend/src/OptiCore.Domain/Common/Entity.cs`
- `backend/src/OptiCore.Domain/Common/AuditableEntity.cs`
- `backend/src/OptiCore.Domain/Customers/Customer.cs`
- `backend/src/OptiCore.Infrastructure/Persistence/OptiCoreDbContext.cs`
- `backend/src/OptiCore.Infrastructure/DependencyInjection.cs`
- `backend/src/OptiCore.Api/Program.cs`
- Customer tests under `backend/tests/OptiCore.Tests`

Do not redesign working domain behavior.

### 2. DbContext

Add `DbSet<Customer>` to `OptiCoreDbContext`.

Configure `OptiCoreDbContext` to automatically apply `IEntityTypeConfiguration<>` mappings from the Infrastructure assembly.

### 3. Customer EF configuration

Create a dedicated configuration class, preferably:

```text
backend/src/OptiCore.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs
```

using:

```csharp
IEntityTypeConfiguration<Customer>
```

Persistence-specific configuration belongs in Infrastructure, not Domain.

### 4. Map the Customer entity

Configure:

#### Primary key
- `Id`
- PostgreSQL UUID
- primary key

#### CustomerNumber
- integer
- database generated
- unique
- human-facing identifier separate from UUID

#### NationalId
- required
- unique
- string
- choose a sensible maximum length appropriate for Israeli ID representation

#### Required fields
- FirstName
- LastName
- DateOfBirth
- MobilePhone
- City
- Gender
- WhatsAppConsent
- IsActive
- CreatedAtUtc

#### Optional fields
- HomePhone
- Email
- Street
- Notes
- CreatedByEmployeeId
- UpdatedAtUtc
- UpdatedByEmployeeId

Use sensible max lengths for strings.

Do not add an Employee FK yet because Employee persistence does not exist yet.

#### Dates/timestamps
- `DateOfBirth` should map as PostgreSQL `date`
- `CreatedAtUtc` and `UpdatedAtUtc` should use timestamp with time zone

#### Internal audit IDs
- `CreatedByEmployeeId`
- `UpdatedByEmployeeId`
should remain nullable UUID values with no FK yet.

### 5. Do not change the business model unnecessarily

Do not:
- replace UUIDv7
- remove CustomerNumber
- change NationalId to an integer
- add hard-delete behavior
- add employee/navigation entities
- invent a Gender enum
- implement Israeli ID checksum validation in this task
- implement the full AuditLog in this task
- add unrelated repositories/services/controllers

If persistence requires a minimal technical adjustment to `Customer`, explain exactly why.

### 6. Migration

Create the first customer migration with a clear name:

```text
AddCustomers
```

Use the correct EF startup/project combination for this repository.

The migration should be stored in Infrastructure.

Apply it to:

```text
opticore_dev
```

Do not drop/recreate the database unless strictly necessary.

### 7. Verify the resulting PostgreSQL schema

Verify at minimum:
- UUID primary key
- unique CustomerNumber
- unique NationalId
- required vs nullable columns
- DateOfBirth mapped as date
- audit timestamps mapped correctly
- IsActive and WhatsAppConsent persisted

### 8. Build and tests

Run:

```powershell
dotnet build OptiCore.slnx
dotnet test OptiCore.slnx
```

All existing tests must pass.

If migration tooling requires an additional package/tool, first check what is already installed. Add only what is actually required.

## Safety

Do not:
- commit
- push
- change Git branches
- reset/rebase
- expose User Secrets
- write database passwords to tracked files
- delete unrelated files
- modify unfinished business modules

If an important requirement is ambiguous, stop and ask instead of inventing a rule.

## Completion report

When finished, report:
1. Files created
2. Files modified
3. Exact database table/columns created
4. Unique constraints/indexes created
5. Exact commands run
6. Migration name/location
7. Build result
8. Test result
9. Any assumptions or technical decisions
10. Anything the human developer should manually inspect before committing

Do not commit or push the changes.
