# Staging preparation completion report

Prepared on `feat/staging-deployment`. This is repository preparation only, not a
deployment. The executable manual runbook is [STAGING_DEPLOYMENT.md](STAGING_DEPLOYMENT.md).

## 1. Exact files created

- `.dockerignore`
- `Dockerfile`
- `backend/src/OptiCore.Api/Hosting/PublicOrigin.cs`
- `backend/src/OptiCore.Api/Hosting/WebHosting.cs`
- `backend/tests/OptiCore.Tests/Hosting/WebHostingTests.cs`
- `frontend/src/test/stagingApi.test.ts`
- `docs/STAGING_DEPLOYMENT.md`
- `docs/STAGING_COMPLETION_REPORT.md`

## 2. Exact files modified

- `backend/src/OptiCore.Api/OptiCore.Api.csproj`
- `backend/src/OptiCore.Api/Program.cs`
- `backend/src/OptiCore.Api/Security/SameOriginRequests.cs`
- `frontend/src/index.css`

Existing untracked specification/permissions/product documentation was preserved.
No domain model, migration, application business service or persistence mapping changed.

## 3. Final staging architecture

Reviewed GitHub main → one Render Docker web service → dedicated Neon PostgreSQL.
ASP.NET Core serves both compiled React and `/api` on the same HTTPS origin.
Only synthetic shared staging data is allowed. Local development databases remain
separate; customer production remains local/offline-first.

## 4. Docker build structure

Node 24 Debian slim installs the lockfile with `npm ci` and builds React. A .NET 10
SDK stage restores/publishes the API and incorporates that frontend. The ASP.NET
10 final image runs `OptiCore.Api.dll` as the image's non-root APP_UID. Build context
is allowlisted; runtime excludes SDK, Node, node_modules, Git and local credentials.

## 5. React production inclusion

Normal `dotnet publish` copies frontend source/configuration into ignored
`obj/frontend-publish`, installs/builds there, and publishes dist into `wwwroot`.
It does not disturb the working Vite node_modules. Docker uses a separately built
dist and `BuildFrontend=false`; missing index fails publication. Explicit Tailwind
source paths make local and isolated builds scan the same UI files.

## 6. Static files and SPA routing

Static middleware serves compiled assets. GET/HEAD fallback serves index for `/`,
login, change-password, customers, products, brands and employees route namespaces,
including detail/edit paths. Missing assets and unrelated paths remain 404.
Fallback index responses have no-cache. Add new frontend namespaces to the allowlist
when new modules are actually implemented.

## 7. API fallback protection

Static middleware excludes `/api` and `/health` (including case variants), and the
SPA allowlist excludes them. Tests cover API 401/403/404/500, missing API files and
HTML Accept headers. API errors are not converted into the SPA document.

## 8. Health

GET `/health` returns HTTP 200 and only `{"status":"ok"}` without querying a DB.
The existing database health endpoint is separate and was never called. Actual
application startup still requires database/bootstrap readiness before serving.

## 9. Environment variable names

- `ASPNETCORE_ENVIRONMENT`
- `Hosting__PublicOrigin`
- `AllowedHosts`
- `ConnectionStrings__OptiCoreDatabase`
- `BootstrapManager__FirstName`
- `BootstrapManager__LastName`
- `BootstrapManager__Username`
- `BootstrapManager__Password`
- `BootstrapManager__Phone`
- `BootstrapManager__NationalId`
- `PORT` (provider supplied)
- `ASPNETCORE_HTTP_PORTS` (fallback)

Choose Staging. PublicOrigin must be an external HTTPS origin and is required in
Staging; AllowedHosts must contain the actual hostname. PORT binds all interfaces
and validates 1–65535. No values containing credentials are supplied in this report.

## 10. Connection configuration

The existing OptiCoreDatabase key accepts standard Npgsql key/value configuration
through hosting secrets. Use verified TLS, not an unconverted provider URI. No
Neon SDK, vendor-specific business code or real connection value was added.

## 11. Migration strategy

Approved code → reviewed offline EF SQL script → intentional authorized staging
application → matching application deployment. The runbook explains secure psql
password prompting and target verification. No automatic startup migration, schema
change, new migration, migration modification or migration application occurred.

## 12. Initial manager

Existing transactional bootstrap is unchanged. Six BootstrapManager variables are
required when Employees is empty; existing password/input policy applies. Invalid
or absent settings still fail startup. Existing employees prevent re-bootstrap.
Remove bootstrap settings after successful initial login. No default password added.

## 13. Render preparation

Portable root Dockerfile, PORT binding, same-origin static hosting and `/health`
are ready for later manual configuration. No render.yaml or provisioning automation
was added. Use reviewed main, one instance and controlled schema releases.

## 14. Neon preparation

Runbook describes a dedicated staging project/database/role, direct connection,
certificate verification and reviewed migrations. No account/resource/database was
created or contacted.

## 15. Local development impact

Normal dotnet build/run and Vite development remain separate and Docker is optional.
Vite proxy/API clients and local DB configuration are unchanged. Frontend building
is publish-only; the only style-file change explicitly limits Tailwind source scans.
No live development server/database startup was used as verification.

## 16. Security review

- No secret, connection string, private credential or real store/customer data added.
- No production API dependency on localhost; clients use relative `/api` paths.
- Secure, HttpOnly, host-only, SameSite=Lax session cookies remain unchanged.
- Absolute 14-day ticket lifetime and disabled sliding renewal remain unchanged.
- Server-side Employee validation, inactive rejection and role updates pass tests.
- Same-origin checks use the configured canonical HTTPS origin behind the proxy;
  request Host must match. Spoofed forwarded headers cannot change this decision.
- No broad CORS, trust-all proxy configuration or developer exception page added.
- Protected APIs stay protected; API errors are not swallowed by SPA fallback.
- No automatic migration or database access occurred.
- Container specifies non-root runtime; health reveals only status.

## 17. Backend verification

`dotnet build OptiCore.slnx -c Release`: passed, zero warnings/errors.

`dotnet test OptiCore.slnx -c Release --no-build`, with the existing
`OPTICORE_VERIFY_DATABASE` switch explicitly set to `0`: 200 passed, 2 skipped,
0 failed, 202 total. Skipped tests are EmployeePostgresTests:
AppliedMigrationHasExpectedPublicConstraints and
IdentityUniqueConstraintsAndConcurrentManagerRemoval. Neither test body ran;
there was no User Secrets or DB access. Hosting tests use fake repositories and
ephemeral Data Protection keys, not the application's database startup.

## 18. Frontend verification

- `npm.cmd run build`: passed.
- `npm.cmd run lint`: passed.
- `npm.cmd run test:run -- --maxWorkers=1`: 220 passed in 10 files, 0 failed.

The previously verified single-worker setting avoids known runner contention;
timeouts were not increased and tests were not disabled. Vite retains its existing
warning for the approximately 645 kB minified JavaScript chunk. Vitest prints an
informational jsdom setup-cost suggestion; test isolation was retained.

## 19. Production publish

Both prebuilt-dist and full isolated frontend publication were exercised using
`dotnet publish backend/src/OptiCore.Api/OptiCore.Api.csproj -c Release -o artifacts/staging`
and the same command with `-p:BuildFrontend=false`. Both passed. Final artifact
verification passed: API assembly and frontend index exist, both referenced assets
match the local build hashes, and appsettings.Development.json is absent. The app
itself was not started against a database.

Initial iterations exposed and fixed a C# record-constructor ambiguity and an
MSBuild item-transform error. An initial sandbox publish exited without diagnostics;
an approved unsandboxed retry succeeded. An early publish attempt using the working
frontend install encountered a Windows EPERM native-module lock. The target was
changed to an isolated install; missing local dependency files were restored without
overwriting existing files. Subsequent source build/lint/full tests passed. No user
Vite process was stopped. These intermediate failures are not concealed.

## 20. Docker verification

Docker is unavailable on PATH. No image build/container smoke test ran, and Docker
was not installed. Dockerfile/context/runtime flow were statically reviewed; the
prebuilt frontend publication path was exercised outside Docker. Real Render proxy,
Linux container, HTTPS browser cookies and actual bootstrap still need authorized
manual verification with synthetic data.

## 21. Diff check

`git diff --check`: passed. No whitespace errors.

## 22. Branch and status

Current branch: `feat/staging-deployment`. Four tracked files modified and eight
task-created files untracked. Pre-existing untracked files remain: the product
requirements DOCX and docs/PERMISSIONS_FOUNDATION.md, docs/PRODUCTS_AND_BRANDS.md,
docs/PRODUCTS_AND_BRANDS_COMPLETION.md. No staging/commit/push/branch manipulation.
Build artifacts and installed dependency outputs remain ignored.

## 23. Assumptions

One instance, one canonical external HTTPS origin and proxy-preserved Host. Existing
relative-URL API flows do not require forwarded headers. Container replacement may
invalidate sessions because Data Protection keys are not a shared durable store.
Multi-instance hosting requires an approved protected key-storage design first.
Provider plan/region/version choices remain human decisions. No new business rules.

## 24. Mohanad's later Neon steps

1. Review and merge this preparation through normal human review.
2. Create a dedicated team staging project in Neon, selecting a region near Render
   and a compatible PostgreSQL version; review current plan limits.
3. Create/select the staging database and role, separate from local/store databases.
4. Privately obtain direct-endpoint parameters for the exact branch/database/role.
5. Prepare the standard Npgsql configuration with verified TLS in hosting secrets.
6. Generate/review the approved offline migration script and intentionally apply it
   to the confirmed staging target using the runbook's secure procedure.
7. Verify schema/history; create only synthetic data after authenticated startup.

## 25. Mohanad's later Render steps

1. Complete approved staging schema setup and privately prepare bootstrap settings.
2. Create a Docker Web Service linked to the correct repository, reviewed main,
   root build context and ./Dockerfile. Creation may trigger deployment.
3. Select region/plan/name and one instance; do not create a separate frontend host.
4. Set the listed environment names privately, choose Staging, and configure the
   exact external HTTPS origin and AllowedHosts hostname. Recheck assigned domain.
5. Set `/health`; preserve entrypoint and provider PORT; add no migration command.
6. Keep automatic deployment off during initial configuration, then explicitly deploy.
7. Verify HTTPS, health/assets, direct SPA navigation, API 401/404, login/logout,
   same-origin edits, manager authorization and inactive-account rejection.
8. Remove bootstrap secrets after initial account/login verification.
9. Enable main-based deployment after approved checks/review. Pause automatic
   deployment for schema changes until their intentional migration is complete.
10. Verify both developers see the same fake data; never copy real local records.

## 26. Remaining manual decisions and explicit confirmations

Provisioning, credentials, schema application and deployment require later human
authorization. Actual Docker/Render/Neon execution and browser testing remain
unverified; no claim of live deployment readiness replaces those checks. There is
no unresolved failing automated test. The existing bundle-size warning remains.

No deployment occurred. No Render or Neon resource was created. No migration was
applied. No database or User Secrets were accessed. No connection string was
exposed. No commit or push was made. No branch switch, reset or rebase occurred.
