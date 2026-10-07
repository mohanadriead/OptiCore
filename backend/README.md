# Employee and authentication backend

The API now uses ASP.NET Core cookie authentication. Customer endpoints require an active authenticated employee; employee management requires an active manager. The temporary Customer actor header has been removed. The frontend is intentionally unchanged and needs the next frontend-auth task: it has no login UI yet, so Customer requests return 401 until authenticated.

## Development setup

From `backend/`, use the existing .NET User Secrets configuration for `ConnectionStrings:OptiCoreDatabase`. Never print or commit the connection string. Apply migrations before running the API:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet ef migrations list --project src/OptiCore.Infrastructure --startup-project src/OptiCore.Api
dotnet ef database update --project src/OptiCore.Infrastructure --startup-project src/OptiCore.Api
```

If `dotnet ef` is not on PATH, invoke `$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe` with the same arguments.

Before the first normal API startup, the human developer must supply these six values. Replace the placeholders locally; do not send real values to a chat, commit them, or include them in logs. A shell may retain command history, so use a private terminal and the team's secret-handling procedure.

```powershell
dotnet user-secrets set "BootstrapManager:FirstName" "<first-name>" --project src/OptiCore.Api
dotnet user-secrets set "BootstrapManager:LastName" "<last-name>" --project src/OptiCore.Api
dotnet user-secrets set "BootstrapManager:Username" "<username>" --project src/OptiCore.Api
dotnet user-secrets set "BootstrapManager:Password" "<password-8-to-128-characters>" --project src/OptiCore.Api
dotnet user-secrets set "BootstrapManager:Phone" "<phone>" --project src/OptiCore.Api
dotnet user-secrets set "BootstrapManager:NationalId" "<national-id>" --project src/OptiCore.Api
dotnet run --project src/OptiCore.Api
```

Environment equivalents use double underscores, for example `BootstrapManager__Password`. Production must use protected deployment configuration and HTTPS. Names/usernames are at most 100 characters; Employee phone requires exactly 10 ASCII digits and NationalId exactly 9 ASCII digits after trimming, including for bootstrap. Username comparison is trim + invariant uppercase. No ID checksum or password composition rules are added. Passwords are not trimmed and must contain non-whitespace content, with length 8–128.

Bootstrap acquires the same transaction lock as employee writes, then checks whether any employee exists. With an empty table, missing/invalid configuration fails startup with a value-free message. Valid configuration creates one active manager with a framework-generated password hash and a null creator (installation bootstrap). Once any employee exists, configuration is ignored, even if missing or changed. Remove the bootstrap secret values after successful initialization; subsequent startup does not need them. No fallback credentials exist. EF migration commands do not bootstrap employees.

## HTTP endpoints

All bodies are JSON. No request accepts an audit actor; the authenticated claim supplies it. DTOs exclude passwords and hashes. Errors use safe ProblemDetails; authentication/authorization middleware returns 401/403 without login-page redirects.

| Endpoint | Access | Body | Success |
|---|---|---|---|
| `POST /api/auth/login` | Anonymous | `username`, `password` | 200 current employee + cookie |
| `POST /api/auth/logout` | Employee | None | 204, expires cookie |
| `GET /api/auth/me` | Employee | None | 200 current employee |
| `POST /api/auth/change-password` | Employee | `currentPassword`, `newPassword` | 204 |
| `POST /api/employees` | Manager | `firstName`, `lastName`, `username`, `password`, `phone`, `nationalId`, `isManager` | 201 + Location + employee |
| `GET /api/employees` | Manager | None | 200 all employees, including inactive |
| `GET /api/employees/{employeeNumber:int}` | Manager | None | 200 employee |
| `PATCH /api/employees/{employeeNumber:int}/deactivate` | Manager | None | 204 |
| `PATCH /api/employees/{employeeNumber:int}/manager-status` | Manager | `isManager` | 200 employee |
| `POST /api/employees/{employeeNumber:int}/reset-password` | Manager | `newPassword` | 204 |

Invalid input returns 400; invalid credentials 401; inactive login (after correct password verification) 403; non-manager management requests 403; missing employee 404; duplicate username/NationalId and last-active-manager removal 409. Unexpected failures return a generic 500. Password changes/resets follow the same length baseline as creation. No forced first-login change or recovery flow exists.

All existing `/api/customers` routes, search semantics, and request/response data remain otherwise unchanged. Authenticated normal employees may use every existing Customer operation. A supplied actor header, body property, or query parameter cannot override audit identity.

## Security and session behavior

- Framework `PasswordHasher<object>` implements the Application hashing abstraction; no Identity user store/tables are introduced. Hashes have per-password salts and the framework's versioned PBKDF2 format.
- Cookies are HttpOnly, SameSite=Lax, path `/`, non-persistent, and allow simultaneous sessions. Production uses Secure=Always and the `__Host-OptiCore.Auth` name; Development permits HTTP with `OptiCore.Auth` and SameAsRequest.
- Tickets have a 14-day **absolute** lifetime, with sliding renewal disabled. There is no inactivity logout and no remember-me option. Browser session-cookie retention is controlled by the browser.
- Every authenticated request rechecks the database and rebuilds current claims (`NameIdentifier`, `Name`, `EmployeeNumber`, and optional `Manager` role). Deactivated/deleted/missing identities lose authentication; demotion removes manager access on the next request. Infrastructure failures fail closed.
- Password changes/resets do not revoke other existing sessions in this task. Logout clears the current browser cookie. No session registry or recovery mechanism is added.
- JSON binding, SameSite cookies, and rejection of foreign `Origin`/cross-site Fetch Metadata protect mutation endpoints, including login. Deploy browser and API under the same origin; do not enable wildcard credentialed CORS. Production reverse-proxy HTTPS/host forwarding must be configured for the trusted deployment.
- Keep Data Protection keys persistent and protected for the deployment identity; never commit keys. There is no custom token storage or localStorage authentication.

## Last-manager invariant and persistence

All application employee writes (including bootstrap) use one READ COMMITTED transaction and acquire `LOCK TABLE "Employees" IN SHARE ROW EXCLUSIVE MODE` **before** reading actors/targets/counts. This permits ordinary reads but serializes employee writers across processes. The second concurrent removal observes the first committed change and cannot remove the remaining manager. Actor authorization is checked again inside the lock. Transactions roll back on failure. This deliberately simple table-level lock suits the small employee roster; hashing inside the transaction briefly holds it.

`Employees` has a UUID primary key, database-generated identity-always EmployeeNumber, and unique indexes on EmployeeNumber, NormalizedUsername, and NationalId. Unique constraint violations are also translated at save time. Audit mappings match Customers. There are no normal-operation hard deletes or detailed permissions. The migration Down method is a normal EF schema rollback and is not an employee-removal API.

## Verification

Customer creation/details updates validate NationalId (9 ASCII digits), MobilePhone (10 ASCII digits), optional HomePhone (blank/null or 9 ASCII digits), and canonical Gender (Male/Female), preserving trimming and optional normalization. Existing rows are not automatically repaired. Customer reads/search, including inactive records, remain unchanged. Storage types, column sizes, and API shapes are unchanged.

```powershell
dotnet build OptiCore.slnx
dotnet test OptiCore.slnx
# Optional real PostgreSQL verification against the configured opticore_dev:
$env:OPTICORE_VERIFY_DATABASE = '1'
dotnet test OptiCore.slnx
Remove-Item Env:OPTICORE_VERIFY_DATABASE
```

Default tests include the original Customer suite plus Employee/domain/service/bootstrap and HTTP tests. HTTP tests use real cookie middleware with an ephemeral key provider and isolated in-memory repository fixtures; their test passwords are never installation credentials. The opt-in database test creates a uniquely named temporary schema, verifies identity generation, unique-violation translation, and cross-connection locking, then removes only that schema. It refuses a database other than `opticore_dev` and never changes public Customer/Employee rows. The database test is explicitly skipped unless opted in.
