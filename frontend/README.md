# OptiCore frontend

Customer, authentication and manager Employee UI built with React, TypeScript, Vite, Tailwind CSS, shadcn/ui, React Router, TanStack Query, React Hook Form and Zod.

## Local development

Use Node.js 24 LTS and npm 11 or newer (tested with Node 24.21.0 / npm 11.19.0).

From this directory:

```powershell
npm install
# Optional: copy .env.example to .env.local and adjust the target.
Copy-Item .env.example .env.local
npm run dev
```

Run the backend separately, from ../backend:

```powershell
dotnet run --project src/OptiCore.Api
```

The frontend calls relative /api paths. Vite proxies /api and /health to
OPTICORE_BACKEND_URL, defaulting to http://localhost:5063. Restart Vite after
changing .env.local. Backend secrets stay in .NET User Secrets; never place them
in frontend environment variables.

Open the Vite URL and sign in at /login with your existing Employee account.
Managers also have /employees; every employee can use /change-password.
Managers can open `הרשאות` on an Employee row to manage assignments. Manager targets
automatically have all configurable permissions and appear read-only in this dialog.
See [permissions foundation](../docs/PERMISSIONS_FOUNDATION.md) for endpoints, the
`usePermissions()` hook and the unapplied migration prerequisite.
Customer search is explicit: enter a query and
press Search or Enter. Blank searches do not contact the backend. Customer
search, details, creation, editing, consent and deactivation are functional.
Other sidebar modules are disabled.

## Verification

```powershell
npm run build
npm run lint
npm run test:run
npm test
```

Tests mock the Customer API boundary and Auth/Employee HTTP responses; they never create database records.
Do not create permanent development customer records just for verification
without the repository owner's approval.

## Cookie sessions

AuthProvider bootstraps from GET /api/auth/me before showing protected routes.
The browser sends the backend HttpOnly cookie with same-origin API requests.
No auth token, cookie or password is read into browser storage. Password forms
submit directly, outside the query mutation cache, and clear password fields
after submission. Logout and protected 401 responses clear the session and query
cache. Login 401 remains a form error. Change-password 401 checks /me to distinguish
an incorrect current password from a missing session. Managers can list/create,
deactivate, promote/demote and reset another employee's password. Self-role changes
refresh /me; window focus and manager API 403 also refresh authorization state.

Vite preserves the browser Host header so the backend's same-origin/CSRF check
can compare it with Origin; it does not strip or rewrite Origin. Use the default
HTTP Vite/HTTP backend setup locally. Vite's proxy is development-only.
Production hosting must route /api to the backend and serve index.html for
frontend route fallback, preserving the public scheme/host for the existing
backend same-origin checks. Real credential login must be verified by a human;
automated tests use synthetic input only.

## Implementation notes

- Source lives in app/, components/ and features/customers/, features/auth/, features/employees/, features/permissions/.
- Hebrew/RTL is the default presentation, centralized in src/app/direction.ts
  and the document language. No translation framework is included. IDs, phones,
  email and date inputs use local LTR direction. Display dates use he-IL;
  date-only values are formatted without timezone shifts.
- Query results remain in memory only. No customer data is stored in localStorage.
- Customer IDs require 9 ASCII digits, mobile phones 10, and optional home phones 9.
  Leading zeros and blank-to-null normalization are preserved; email has no extra format validation.
- Gender uses mutually exclusive Hebrew radio labels mapped to Male/Female. DOB uses
  Hebrew day/month/year dropdowns, calendar validation, and YYYY-MM-DD submission.
  Suggested years cover the current year back 120 years; stored years outside that range remain selectable when editing.
  Customer and Employee National ID inputs filter typing/pasting to ASCII digits while retaining schema validation.
  No native date input or browser-localized placeholder is used.
- src/components/ui contains only the shadcn components used here, with local
  import and logical-alignment adjustments.

Setup references: [Vite](https://vite.dev/guide/) and
[shadcn Vite setup](https://ui.shadcn.com/docs/installation/vite).
