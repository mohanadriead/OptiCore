# OptiCore frontend

Customer UI built with React, TypeScript, Vite, Tailwind CSS, shadcn/ui, React Router, TanStack Query, React Hook Form and Zod.

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

Open the Vite URL and visit /customers. Search is explicit: enter a query and
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

Tests mock the Customer API boundary and never create database records.
Do not create permanent development customer records just for verification
without the repository owner's approval.

## Temporary development identity

During Vite development, the first customer mutation generates a browser UUID
using crypto.randomUUID(), stores it in localStorage, and sends it as
X-Employee-Id. It is reused for that browser and is not displayed. This is NOT
authentication or authorization and must be removed when real authentication
is available. If storage is unavailable, saving fails with feedback.

Production builds deliberately reject mutations until real identity is
implemented; they never invent an actor. Vite's proxy is development-only.
Production hosting must route /api to the backend and serve index.html for
frontend route fallback.

## Implementation notes

- Source lives in app/, components/ and features/customers/.
- Hebrew/RTL is the default presentation, centralized in src/app/direction.ts
  and the document language. No translation framework is included. IDs, phones,
  email and date inputs use local LTR direction. Display dates use he-IL;
  date-only values are formatted without timezone shifts.
- Query results remain in memory only. No customer data is stored in localStorage.
- Required fields and string limits mirror the backend. National ID and gender
  stay text fields; email has no extra format validation.
- src/components/ui contains only the shadcn components used here, with local
  import and logical-alignment adjustments.

Setup references: [Vite](https://vite.dev/guide/) and
[shadcn Vite setup](https://ui.shadcn.com/docs/installation/vite).
