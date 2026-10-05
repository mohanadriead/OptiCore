# Codex Task — Customer Application + API

Read `AGENTS.md` at the repository root before doing anything else.

## Goal

Complete the first end-to-end Customer vertical slice by adding the **Application layer + Infrastructure repository + HTTP API** for the already-existing and already-persisted `Customer` entity.

This task should allow OptiCore to:
- create a customer
- retrieve a customer by CustomerNumber
- retrieve a customer by NationalId
- search customers
- update editable customer details
- change WhatsApp consent
- deactivate a customer

The Customer domain model and PostgreSQL persistence already exist and must remain the source of truth.

This is intentionally a narrow task.

Do **not** implement Employees, authentication, permissions, eye exams, products, inventory, orders, payments, reports, frontend code, WhatsApp provider integration, or local/cloud synchronization.

## 1. Current state

The repository already contains:

```text
backend/
  OptiCore.slnx
  src/
    OptiCore.Api
    OptiCore.Application
    OptiCore.Domain
    OptiCore.Infrastructure
  tests/
    OptiCore.Tests
```

Current stack:
- .NET 10
- C#
- ASP.NET Core
- Entity Framework Core
- Npgsql
- PostgreSQL 18
- xUnit

Development database:

```text
opticore_dev
```

Current Customer implementation already includes:
- UUIDv7 internal `Id`
- database-generated unique `CustomerNumber`
- unique `NationalId`
- domain validation
- update behavior
- WhatsApp-consent behavior
- soft deactivation
- creation/update audit metadata
- EF Core mapping
- PostgreSQL migration
- `Customers` DbSet
- six passing Customer domain tests

The current `main` branch has already successfully built and tested after Customer persistence was merged.

## 2. Important architecture rules

Follow the architecture described in `AGENTS.md`.

Conceptually:

```text
Api
  ↓
Application
  ↓
Domain

Infrastructure implements Application abstractions
and talks to PostgreSQL through EF Core.
```

Rules:
- Domain must not depend on EF Core or HTTP.
- Application must not depend on EF Core/Npgsql.
- API must not contain business rules.
- Infrastructure owns EF queries and persistence details.
- Do not put EF attributes on Domain entities.
- Do not introduce MediatR, AutoMapper, FluentValidation, or another framework for this task.
- Prefer a small, explicit implementation over extra abstractions.

## 3. Inspect before editing

Before making changes, inspect at minimum:

```text
backend/src/OptiCore.Domain/Common/Entity.cs
backend/src/OptiCore.Domain/Common/AuditableEntity.cs
backend/src/OptiCore.Domain/Customers/Customer.cs

backend/src/OptiCore.Application/
backend/src/OptiCore.Infrastructure/DependencyInjection.cs
backend/src/OptiCore.Infrastructure/Persistence/OptiCoreDbContext.cs
backend/src/OptiCore.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs

backend/src/OptiCore.Api/Program.cs

backend/tests/OptiCore.Tests/Customers/CustomerTests.cs
```

Also inspect:

```text
git status
git diff
```

before editing.

Do not overwrite unrelated work.

## 4. Temporary actor identity for this task

Authentication and Employee persistence are **not implemented yet**, but Customer creation/update/deactivation requires an employee ID for audit metadata.

For this task only, mutating Customer endpoints must read a required HTTP header:

```text
X-Employee-Id
```

The value must be a valid `Guid`.

Example:

```text
X-Employee-Id: 0199f5d4-2235-7a7e-8a82-123456789abc
```

This is a **temporary development mechanism only**.

Important:
- Centralize header parsing instead of duplicating it everywhere.
- Invalid or missing `X-Employee-Id` must return HTTP 400.
- Do not treat this header as authentication or authorization.
- Do not implement authentication or permissions in this task.
- Add a clear code comment that this mechanism must be replaced once real authentication is implemented.

Read-only endpoints do not require this header.

## 5. Application-layer design

Create a Customer application module under a clear folder such as:

```text
backend/src/OptiCore.Application/Customers/
```

Use clear explicit types.

A reasonable structure is:

```text
Customers/
  ICustomerRepository.cs
  ICustomerService.cs
  CustomerService.cs
  CustomerDto.cs
  CreateCustomerRequest.cs
  UpdateCustomerRequest.cs
  CustomerNotFoundException.cs
  DuplicateNationalIdException.cs
```

You may adjust filenames slightly if there is a simpler coherent structure, but do not introduce unnecessary patterns.

## 6. Repository abstraction

Define an Application-layer abstraction for Customer persistence.

At minimum support async methods for:
- checking whether NationalId already exists
- adding a Customer
- getting by CustomerNumber
- getting by NationalId
- searching
- saving changes

Use `CancellationToken`.

The interface must not expose:
- `DbContext`
- `DbSet`
- `IQueryable`
- EF Core types
- Npgsql types

Application should deal with Domain entities and normal .NET types only.

## 7. Infrastructure repository

Implement the Application repository interface in Infrastructure using `OptiCoreDbContext`.

Suggested location:

```text
backend/src/OptiCore.Infrastructure/Persistence/Repositories/CustomerRepository.cs
```

Requirements:
- use async EF Core APIs
- use `AsNoTracking()` for read-only queries where appropriate
- tracked entities are required for update/deactivate operations
- do not hard-delete Customers
- do not create a second DbContext
- register the repository through Infrastructure DI

Do not modify the existing Customer schema unless genuinely required.

No new migration should be necessary for this task.

## 8. Customer application service

Implement a service/use-case layer in Application.

It must coordinate the existing Domain behavior and repository.

The service should support:

```text
CreateCustomerAsync
GetByCustomerNumberAsync
GetByNationalIdAsync
SearchAsync
UpdateCustomerAsync
SetWhatsAppConsentAsync
DeactivateCustomerAsync
```

Use `CancellationToken`.

Do not duplicate Domain behavior inside the service.

For example:
- creation must call the existing `Customer` constructor
- updates must call `Customer.UpdateDetails(...)`
- WhatsApp consent must call `Customer.SetWhatsAppConsent(...)`
- deactivation must call `Customer.Deactivate(...)`

Do not make Domain setters public.

## 9. Create Customer behavior

Before creating a Customer:
1. check whether `NationalId` already exists
2. if it exists, fail with a dedicated duplicate-NationalId result/exception
3. otherwise create the Domain entity
4. add it
5. save changes
6. return the created Customer DTO

Important:

The database unique index remains the final integrity backstop.

Do not remove or weaken the database uniqueness constraint.

After `SaveChangesAsync`, the PostgreSQL-generated `CustomerNumber` should be available on the entity and returned to the client.

Do not generate CustomerNumber in Application code.

## 10. Customer DTO

Return a DTO rather than returning the Domain entity directly from the API.

The DTO should contain at least:

```text
Id
CustomerNumber
NationalId
FirstName
LastName
DateOfBirth
MobilePhone
HomePhone
Email
City
Street
Gender
Notes
WhatsAppConsent
IsActive
CreatedAtUtc
UpdatedAtUtc
```

It is not necessary to expose `CreatedByEmployeeId` / `UpdatedByEmployeeId` in the public Customer DTO for this task.

Use explicit mapping code.

Do not add AutoMapper.

## 11. HTTP API endpoints

Keep endpoints outside `Program.cs` except for registration.

A recommended structure is:

```text
backend/src/OptiCore.Api/Endpoints/Customers/CustomerEndpoints.cs
```

Create an extension such as:

```csharp
app.MapCustomerEndpoints();
```

and call it from `Program.cs`.

Base route:

```text
/api/customers
```

### 11.1 Create

```http
POST /api/customers
```

Required header:

```text
X-Employee-Id
```

Example body:

```json
{
  "nationalId": "123456789",
  "firstName": "Ahmad",
  "lastName": "Ali",
  "dateOfBirth": "1995-04-20",
  "mobilePhone": "0501234567",
  "homePhone": null,
  "email": "ahmad@example.com",
  "city": "Haifa",
  "street": null,
  "gender": "Male",
  "notes": null,
  "whatsAppConsent": false
}
```

Behavior:

```text
201 Created   success
400 Bad Request invalid input / invalid actor header
409 Conflict  duplicate NationalId
```

Use `Created(...)` / `CreatedAt...` semantics if practical.

Return the created `CustomerDto`.

### 11.2 Get by CustomerNumber

```http
GET /api/customers/{customerNumber:int}
```

Behavior:

```text
200 OK
404 Not Found
```

Return `CustomerDto`.

### 11.3 Get by NationalId

```http
GET /api/customers/by-national-id/{nationalId}
```

Behavior:

```text
200 OK
404 Not Found
```

### 11.4 Search

```http
GET /api/customers/search?q={query}
```

Search requirements come from confirmed business rules.

A single query should consider:
- CustomerNumber, exact match if query parses as an integer
- NationalId, exact match
- MobilePhone
- HomePhone
- FirstName
- LastName
- full name
- Email

For text fields:
- use case-insensitive matching where appropriate
- partial matching is acceptable for name, phone, and email searches
- do not make phone unique
- if multiple customers share a phone number, return all matching customers

Do not silently exclude inactive customers in this task.
Return `IsActive` in the DTO so the caller can see their status.

If `q` is empty/whitespace, return HTTP 400 rather than returning the entire Customer table.

Return a JSON array of `CustomerDto`.

Do not add pagination unless clearly necessary for implementation; it is outside this task.

### 11.5 Update Customer details

```http
PUT /api/customers/{customerNumber:int}
```

Required header:

```text
X-Employee-Id
```

Editable fields should match the existing Domain method:
- FirstName
- LastName
- DateOfBirth
- MobilePhone
- HomePhone
- Email
- City
- Street
- Gender
- Notes

Do **not** modify in this endpoint:
- Id
- CustomerNumber
- NationalId
- WhatsAppConsent
- IsActive

Use the Domain `UpdateDetails(...)` method.

Behavior:

```text
200 OK or 204 No Content
400 Bad Request
404 Not Found
```

Prefer returning the updated DTO if simple.

### 11.6 WhatsApp consent

```http
PATCH /api/customers/{customerNumber:int}/whatsapp-consent
```

Required header:

```text
X-Employee-Id
```

Body:

```json
{
  "consent": true
}
```

Use the existing Domain method:

```text
SetWhatsAppConsent(...)
```

Behavior:

```text
200 OK or 204 No Content
400 Bad Request
404 Not Found
```

### 11.7 Deactivate

```http
PATCH /api/customers/{customerNumber:int}/deactivate
```

Required header:

```text
X-Employee-Id
```

Use:

```text
Customer.Deactivate(...)
```

Do not delete the database row.

Behavior:

```text
204 No Content
400 Bad Request
404 Not Found
```

If the Customer is already inactive, it is acceptable for the operation to remain idempotent rather than fail.

Do not add reactivation in this task.

## 12. Error handling

Keep error handling simple and coherent.

At minimum map:

```text
invalid Domain/Application input  -> 400
Customer not found                -> 404
duplicate NationalId              -> 409
unexpected error                  -> normal ASP.NET 500 handling
```

Do not expose stack traces or database exception details in API responses.

Do not introduce a large custom error framework.

A small global exception handler is acceptable if it reduces endpoint duplication, but keep the scope focused.

## 13. Application registration

Create or use an Application dependency-registration extension such as:

```csharp
builder.Services.AddApplication();
```

Register the Customer application service there.

The resulting composition root should remain conceptually simple:

```csharp
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
```

Do not move business logic into `Program.cs`.

## 14. Tests

Add meaningful tests for the new Application behavior.

Do not add a mocking framework unless genuinely necessary.

A simple in-memory/fake `ICustomerRepository` inside the test project is preferred.

At minimum cover:

1. Create succeeds with valid data.
2. Create rejects duplicate NationalId.
3. Get by CustomerNumber returns existing Customer.
4. Get by CustomerNumber fails for missing Customer.
5. Update uses Domain behavior and records updater metadata.
6. WhatsApp consent change works through the service.
7. Deactivate works through the service.
8. Search returns multiple customers sharing the same phone.

Keep the existing six Domain tests passing.

You do not need to add a full persistent API integration-test framework in this task.

## 15. Manual/API verification

Build and test the solution.

Do not insert permanent sample records into `opticore_dev` merely for verification unless you ask for approval first.

It is enough to verify:
- API starts
- route registration succeeds
- build/tests pass

If you want to run mutating curl/API verification against `opticore_dev`, ask before creating test data.

## 16. Existing persistence must remain intact

Do not change unless absolutely necessary:

```text
CustomerConfiguration.cs
AddCustomers migration
OptiCoreDbContextModelSnapshot.cs
existing Customer Domain behavior
```

No schema change is expected.

Therefore:

```text
do not create a new EF migration
```

unless you discover a genuine persistence defect.

If you believe a migration is required, stop and explain why before generating one.

## 17. Build and verification

From `backend/`, run:

```powershell
dotnet build OptiCore.slnx
dotnet test OptiCore.slnx
```

Final result must have:
- 0 build errors
- all tests passing

Also run:

```powershell
git diff --check
git status --short
```

## 18. Git / safety rules

Do not:
- commit
- push
- switch branches
- merge
- rebase
- reset
- delete branches
- discard unrelated changes
- expose User Secrets
- print connection strings/passwords
- hard-delete Customer data
- create or modify unrelated modules

The human developer will review and commit manually.

## 19. Completion criteria

The task is complete when:
- Customer repository abstraction exists in Application.
- Infrastructure implements and registers it.
- Customer application service exists and is registered.
- Customer DTO/request models exist.
- Customer API endpoints are registered outside Program.cs.
- Create/Get/Search/Update/Consent/Deactivate are implemented.
- duplicate NationalId maps to 409.
- missing Customer maps to 404.
- invalid actor header/input maps to 400.
- CustomerNumber remains database-generated.
- Customer soft-deactivation remains intact.
- no new migration was created.
- existing six tests still pass.
- new Application tests pass.
- build succeeds.

## 20. Completion report

When finished, report:
1. Files created
2. Files modified
3. Endpoint list with methods/routes/status codes
4. Application abstractions/services added
5. Infrastructure implementation added
6. Tests added and what they cover
7. Exact commands run
8. Final build result
9. Final test result
10. Any assumptions or technical decisions
11. Anything the human developer should inspect carefully
12. Confirmation that no migration, commit, push, branch change, or secret exposure occurred

Do not commit or push the changes.
