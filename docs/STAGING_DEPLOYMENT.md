# Shared staging deployment

This is preparation only. No service, database, resource, deployment or migration
was created/applied/accessed during this task. Provisioning and deployment below
are future, explicitly authorized human actions. Use fake/test data only.

## Architecture and separation

```text
GitHub: reviewed main
         |
         v
Render: one Docker web service, one HTTPS origin
  ASP.NET Core 10
    /api/*        authenticated application API
    /health       process liveness
    /assets/*     compiled React assets
    React routes  index.html fallback
         |
         v
Neon: dedicated staging PostgreSQL, shared synthetic data
```

Each developer continues using their own local PostgreSQL and the Vite dev server.
Customer production remains the local/offline-first store server with local
PostgreSQL. Cloud staging is not a dependency of either local development or store
operations. There is no synchronization/data-copy feature in this preparation.

## Build and publish

Node 24 satisfies the frontend's Node >=22.12 requirement. Docker uses .NET 10 SDK
and ASP.NET 10 runtime images, matching the projects. `package-lock.json` and
`npm ci` provide deterministic frontend dependency resolution.

From the repository root, a normal publish builds the frontend and copies its
output into the published backend's `wwwroot`:

```powershell
dotnet publish backend/src/OptiCore.Api/OptiCore.Api.csproj -c Release -o artifacts/staging
```

The publish-only MSBuild target copies frontend source/config into the API's ignored
`obj/frontend-publish` directory, runs `npm ci`, then `npm run build` there. Each
publish refreshes source/public/dist, so removed source files cannot linger. It
never replaces the working Vite session's node_modules or loads local .env files.
Tailwind explicitly scans src and index.html, so source detection is consistent
in local, isolated publish and Docker builds. It does not start ASP.NET, bootstrap,
or access a database. `npm.cmd` is used automatically on
Windows. `appsettings.Development.json` is excluded from publish output.

Docker builds frontend and backend in separate stages. It copies the built `dist`
into the SDK stage, then publishes with `-p:BuildFrontend=false`. That property
requires an existing `frontend/dist/index.html`; it fails instead of silently
shipping a backend without a frontend. Developers can use the same prebuilt-dist
flow after explicitly building the frontend.

```powershell
docker build -t opticore-staging .
```

Final image: ASP.NET runtime and published files only, including `wwwroot`.
No Node, SDK, node_modules, Git, IDE configuration, local database files, tests or
User Secrets. `.dockerignore` limits build inputs and excludes credentials/local
configuration/generated output. Runtime uses the image's non-root `APP_UID`.
Base tags stay on supported major lines; rebuild regularly for patches and review
digest pinning when a release/reproducibility policy is established.

## HTTP, ports, files and routing

`PORT`, when set, is validated and binds Kestrel to `0.0.0.0` on that port. Invalid
ports fail startup without echoing values. Without `PORT`, ordinary ASP.NET URL
configuration applies; the container defaults to HTTP port 8080. The provider
terminates public HTTPS. Do not set a localhost-only production binding.

Compiled assets use static-file middleware. Static files under `/api` or `/health`
are never served. SPA fallback is GET/HEAD-only and explicitly limited to `/`,
`/login`, `/change-password`, `/customers`, `/products`, `/brands`, `/employees`
and their non-file descendants. Direct `/products/5/edit` works. Missing assets
remain 404; unsupported methods remain errors. New React route namespaces must be
added to the backend fallback allowlist. Index responses use `Cache-Control: no-cache`.

Unknown `/api/*` paths stay API 404s. Existing API 401/403/404/500 responses are
never rewritten to HTML. Public static content contains no customer/employee data;
all data requests retain API authentication. No broad CORS policy is needed.

`GET /health` returns only `{"status":"ok"}` without querying a database. Configure
the hosting liveness check to use this route, not `/health/database`. The existing
database health route remains separate and is not called by this preparation.
Startup bootstrap still requires the database, so the service cannot reach healthy
startup until the database exists, is migrated and has valid bootstrap settings.

## Environment variables

Names and purposes only; supply private values through the hosting secret UI.

| Name | Purpose |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | Runtime environment; choose Staging for shared staging |
| `Hosting__PublicOrigin` | Exact external HTTPS origin for same-origin mutation checks; mandatory in Staging |
| `AllowedHosts` | Restrict accepted hostnames to the actual staging hostname |
| `ConnectionStrings__OptiCoreDatabase` | Existing standard Npgsql connection configuration; secret |
| `BootstrapManager__FirstName` | Initial manager's synthetic first name |
| `BootstrapManager__LastName` | Initial manager's synthetic last name |
| `BootstrapManager__Username` | Initial manager login name |
| `BootstrapManager__Password` | Initial manager password; secret |
| `BootstrapManager__Phone` | Synthetic phone meeting the existing format |
| `BootstrapManager__NationalId` | Synthetic national ID meeting the existing format |
| `PORT` | Provider-supplied listening port; optional locally |
| `ASPNETCORE_HTTP_PORTS` | Standard fallback container HTTP port configuration when PORT is absent |

The public origin contains only scheme and authority, no credentials/path/query/
fragment. It must use HTTPS. For Render, use the actual externally visible origin,
and restrict AllowedHosts to its hostname (not a URL). Update both when changing
the canonical domain. Do not set `ASPNETCORE_FORWARDEDHEADERS_ENABLED` or introduce
a trust-all proxy configuration for this setup.

The application does not need forwarded Host/Proto headers for its current relative
URLs and cookie API flows: CSRF compares against the explicitly configured external
origin and checks that the request Host matches. Spoofed forwarded headers do not
affect that decision. Development with no configured public origin keeps the
existing request-scheme/Host comparison and Vite proxy behavior. Future OAuth,
absolute redirects or IP-dependent features need a separate trusted-proxy design.

Staging is not Development: secure `__Host-OptiCore.Auth` cookies, HttpOnly,
SameSite=Lax, Path=/, no persistent browser-cookie expiry, absolute 14-day ticket
lifetime, no sliding renewal. The current Employee is still validated server-side
on every authenticated request; deactivation/role changes take effect. Foreign
Origins and cross-site mutations are rejected, including login. API errors use
the existing safe exception handler, with no Developer Exception Page.

One staging instance is assumed. Container-local Data Protection keys are not a
shared durable key store; container replacement can invalidate sessions and require
login again. This does not turn the cookie into a persistent/sliding session. Do
not scale to multiple instances before approving shared, protected key storage.
Do not put key material in Git or the image.

## Database and initial manager

Use a separate Neon project/database for staging, or another compatible PostgreSQL
provider. The backend remains EF Core/Npgsql with no Neon SDK or vendor business
logic. Do not point it at a developer/store database or copy local data to it.

Privately assemble the existing Npgsql configuration using the provider's host,
port, database, username and password. Use Npgsql key/value syntax, not an
unconverted PostgreSQL URI. Require certificate-verified TLS (`SSL Mode` set to
`VerifyFull`); do not enable detailed error data, sensitive-data logging or a
trust-all certificate callback. Keep the value in hosting secrets, never source,
screenshots, tickets, shell history or command-line arguments. A direct Neon
endpoint is the conservative initial choice; connection pooling can be evaluated
later without changing application business code.

Bootstrap is unchanged: it runs under the existing exclusive transaction, checks
whether Employees is empty, and only then creates one manager. Required initial
values come from the six BootstrapManager environment names above. Password
requirements remain the existing policy; names/username must be valid, phone must
have exactly 10 ASCII digits, national ID exactly 9 ASCII digits, both synthetic.
Missing/invalid configuration on an empty Employees table fails startup safely.
Once an employee exists, bootstrap does not reset credentials or create another
manager. After verifying initial login, remove the bootstrap settings from hosting
secrets; the database-backed account remains. No default staging password exists.

## Controlled migrations — future authorized action only

There is no `Database.Migrate()` startup call, Docker migration step or arbitrary
feature-branch migration automation. Use approved code, review schema changes,
intentionally apply migrations, then deploy/restart the matching application.

The existing design-time factory is deliberately offline and has no connection.
Therefore do not assume `dotnet ef database update` will consume a hosting secret
automatically. One secure manual route is an offline idempotent SQL script:

```powershell
dotnet ef migrations script --idempotent --project backend/src/OptiCore.Infrastructure --startup-project backend/src/OptiCore.Api --output artifacts/staging-migrations.sql
```

Use a matching .NET 10 EF tool, ensure the ignored artifacts directory exists, and
review the script against the approved commit. Generation does not access a DB.
Do not alter old applied migrations or apply scripts from unreviewed branches.

An authorized developer can then use `psql` against the **confirmed staging** target.
Set its non-password environment settings privately: `PGHOST`, `PGPORT`,
`PGDATABASE`, `PGUSER`, `PGSSLMODE` (certificate verification required). Prompt for
the password rather than placing it in a command or file:

```powershell
psql -W --set ON_ERROR_STOP=1 --file artifacts/staging-migrations.sql
```

Do not run this during preparation. Review destructive migrations/backups first.
Use an appropriate migration role, verify schema/history and clear temporary
environment settings afterward. Never paste credentials into the SQL script. A
provider SQL editor is another authorized option after checking the selected
project/database carefully. No migrations were generated or applied in this task.

## Manual Neon setup (Mohanad, after approval)

1. Review/merge the preparation PR through normal review; do not deploy this feature
   branch against the shared staging database.
2. Sign into Neon yourself and create a dedicated development-team staging project.
   Choose a region close to the future Render service and a compatible PostgreSQL
   version (the project uses PostgreSQL 18). Review available plans and limits.
3. Create/select the dedicated staging database and role. Confirm it contains no
   real data. Keep production and personal development credentials separate.
4. Open the connection panel for the exact staging branch/database/role. Privately
   collect the direct endpoint parameters; do not paste them into chat or Git.
5. Assemble and save the Npgsql configuration in the future hosting secret store,
   with verified TLS. Do not reuse a provider URI without translating its syntax.
6. Review the approved EF SQL script, then intentionally apply it using the secure
   migration process above. Verify the migration history before starting the app.
7. Only manually create obvious synthetic records after authenticated startup.

## Manual Render setup (Mohanad, after approval)

1. Complete the approved database migration first and have the bootstrap secrets
   ready privately. Render service creation can initiate its first deployment.
2. In Render, choose New > Web Service and authorize the correct GitHub repository.
3. Select Docker, branch `main`, repository-root build context, and `./Dockerfile`.
   Do not set the root to `backend`; Docker needs both frontend and backend.
4. Choose the staging service name/region/plan and a single instance. Review cold
   starts and plan restrictions. No separate frontend service is needed.
5. Configure the runtime as Staging. Set the environment names listed above through
   Render's environment/secret controls. Set the exact public origin and allowed
   hostname for the assigned service domain. Verify the hostname after creation;
   if it differs from the planned name, correct both settings and redeploy.
6. Set the health-check path to `/health`. Keep the Docker entrypoint; do not add a
   database migration pre-deploy/start command. Allow Render to supply PORT.
7. Initially disable automatic deploys while setup/schema verification is underway.
   Only now explicitly create/deploy the service. Inspect logs safely without
   copying environment contents or credentials into issues.
8. Verify HTTPS, health, root/asset loads, direct `/products/5/edit`, unauthenticated
   API 401, missing API 404, login/logout, same-origin mutations, and manager-only
   screens. Test with synthetic accounts only. Remove bootstrap settings after
   successful initial login.
9. Link only reviewed `main` to shared staging. When CI/branch protection is in
   place, choose deploy-after-checks where available. Feature branches use PRs,
   tests and review; do not point them at the shared staging DB.
10. For schema-changing releases, pause auto-deploy until the approved migration
    is applied, then deploy the matching commit and restore normal main-based
    deploys. No automation here applies migrations.

There is no render.yaml: the portable Dockerfile plus manual setup avoids a
Blueprint that provisions resources or starts an unprepared first deployment.

## Local development and Docker use

The normal `dotnet run --project backend/src/OptiCore.Api` and `npm.cmd run dev`
(from frontend) workflows are unchanged. Existing local PostgreSQL settings and
Vite proxy continue to work. Docker is optional and contains no cloud hostname.

For a later authorized local container run, use a prepared **synthetic** database
and an HTTPS reverse proxy if testing Staging cookie flows. Inject runtime settings
through the environment, without embedding values in commands/images:

```powershell
docker run --rm -p 8080:8080 --env ASPNETCORE_ENVIRONMENT --env Hosting__PublicOrigin --env AllowedHosts --env ConnectionStrings__OptiCoreDatabase --env BootstrapManager__FirstName --env BootstrapManager__LastName --env BootstrapManager__Username --env BootstrapManager__Password --env BootstrapManager__Phone --env BootstrapManager__NationalId opticore-staging
```

That command passes existing environment values by name; it does not provide or
print them. Do not run with an external DB without authorization. Secure staging
cookies are intentionally unsuitable for plain HTTP browser login. Health/static
requests can use HTTP on the internal port; the public browser origin uses HTTPS.

## Fake-data-only policy

Never load real customer/employee national IDs, contact details, addresses, eye
exams/prescriptions, payments, debts, supplier finances or operational records.
Never import a local database dump. Both developers see the same synthetic staging
data, so choose unmistakably fake names and never send real messages from it.

## Troubleshooting

- Missing connection/bootstrap variable: check the **names** in the hosting UI.
  Do not dump all configuration. Bootstrap failure on an empty database is expected
  if any required setting is invalid. Existing password/format rules still apply.
- Database not migrated: stop deployment, verify the selected staging database,
  review/apply the approved script explicitly. Restart only after schema checks.
- Missing frontend: inspect publish `wwwroot/index.html` and its referenced assets.
  Rebuild from root; do not bypass the publish missing-dist check.
- SPA route 404: confirm the route namespace is in WebHosting's allowlist and the
  index exists. An API 404 or missing asset 404 is correct, not a fallback failure.
- Health failure: confirm PORT/all-interface binding and process startup logs.
  `/health` is liveness, not database readiness; startup still runs bootstrap.
- Render cold start/restart: account for plan wake-up behavior and startup time;
  container replacement may require signing in again due to ephemeral keys.
- Login/mutations return 403: check HTTPS public origin and Host match, browser
  Origin and Sec-Fetch-Site; do not disable the CSRF guard or enable broad CORS.
- Cookie missing/401: use HTTPS, check secure host-only cookie attributes, account
  activation and current role, and whether the container was replaced. Never put
  cookies or credentials into troubleshooting logs.

## Reference documentation

- [Render Docker](https://render.com/docs/docker)
- [Render web service setup/port binding](https://render.com/docs/web-services)
- [Render health checks](https://render.com/docs/health-checks)
- [Render deploy workflow](https://render.com/docs/deploys)
- [Neon connection overview](https://neon.com/docs/connect/connect-intro)
- [Npgsql TLS verification](https://www.npgsql.org/doc/security.html)

Provider UIs/plans can change; verify the displayed staging target before any
future provisioning or database operation.
