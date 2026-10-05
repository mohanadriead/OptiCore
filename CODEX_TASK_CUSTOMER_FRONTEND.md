# Codex Task — Customer Frontend v1

Read `AGENTS.md` at the repository root before doing anything else.

## Goal

Create the first real OptiCore frontend and connect it to the already-working Customer backend.

This task should initialize the frontend stack and implement the first usable Customer UI:

- application shell/navigation
- customer search/list
- customer details
- create customer
- edit customer
- change WhatsApp consent
- deactivate customer

The backend Customer API is already implemented, tested, merged, and verified end-to-end against PostgreSQL.

This task is intentionally limited to the frontend Customer experience.

Do **not** implement Employees, authentication, permissions, Eye Exams, Orders, Inventory, Products, Payments, Reports, WhatsApp provider integration, offline synchronization, or any unfinished backend module.

## 1. Current repository state

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
feat/customer-frontend
```

The `frontend/` directory currently has no implemented application and may be empty.

Backend stack:

- .NET 10
- ASP.NET Core
- PostgreSQL 18
- EF Core / Npgsql

Frontend stack selected for OptiCore:

- React
- TypeScript
- Vite
- Tailwind CSS
- shadcn/ui
- TanStack Query
- React Hook Form
- Zod
- React Router

Use current stable mutually compatible versions. Do not pin obsolete versions merely because examples online use them.

## 2. Design direction

OptiCore is a professional optical-store management application.

The frontend should look like an internal business system, not a marketing website.

Desired characteristics:

- clean
- professional
- desktop-first
- efficient/dense enough for employees
- responsive where reasonable
- accessible labels and keyboard behavior
- visually consistent
- suitable for long daily use

Avoid:

- flashy gradients
- oversized hero sections
- consumer-style landing pages
- excessive animation
- novelty effects

A reasonable shell:

```text
┌──────────────────────────────────────────────────────────┐
│ OptiCore                              Development         │
├───────────────┬──────────────────────────────────────────┤
│ Dashboard     │                                          │
│ Customers     │  Main content                            │
│ Orders        │                                          │
│ Inventory     │                                          │
│ Eye Exams     │                                          │
│ Employees     │                                          │
│ Reports       │                                          │
│ Settings      │                                          │
└───────────────┴──────────────────────────────────────────┘
```

Only Customers is functional in this task.

Future module navigation entries may be visible but must be clearly disabled/non-functional. Do not create fake pages or business logic for them.

## 3. RTL readiness

OptiCore will likely need Hebrew and possibly Arabic later.

The first implementation may use English UI text, but build with RTL compatibility in mind from the beginning.

Requirements:

- avoid assumptions that layout is always left-to-right
- prefer logical spacing/alignment utilities where practical (`start/end`, `ms/me`, `ps/pe`, etc.)
- avoid hard-coded left/right positioning unless actually directional
- keep application direction centralized so we can switch to `rtl` later
- do not implement a full translation/i18n system in this task

A simple centralized application direction constant/provider is enough.

Current UI language for this task: English.

## 4. Inspect before editing

Before making changes:

1. Read `AGENTS.md`.
2. Inspect the repository structure.
3. Inspect the backend Customer API contracts:
   - `backend/src/OptiCore.Api/Endpoints/Customers/CustomerEndpoints.cs`
   - `backend/src/OptiCore.Application/Customers/CustomerDto.cs`
   - `backend/src/OptiCore.Application/Customers/CreateCustomerRequest.cs`
   - `backend/src/OptiCore.Application/Customers/UpdateCustomerRequest.cs`
4. Inspect `git status` and `git diff`.
5. Inspect the existing `frontend/` folder before replacing/creating anything.

Do not change backend code for this task.

If the frontend discovers a real backend defect, stop and report it rather than silently changing backend behavior.

## 5. Initialize the frontend

Initialize `frontend/` as a Vite React + TypeScript application.

Do not create a nested app such as:

```text
frontend/opticore/
```

The application root must be directly:

```text
frontend/
```

Install/configure the selected stack:

- React
- TypeScript
- Vite
- React Router
- TanStack Query
- React Hook Form
- Zod
- shadcn/ui
- Tailwind CSS

Also add:

- Lucide icons for UI icons
- ESLint if not already created by the Vite template

Do not add Redux.

Do not add a second component framework such as Material UI, Ant Design, Chakra, Bootstrap, etc.

## 6. Frontend structure

Use a clean feature-oriented structure.

A reasonable target is:

```text
frontend/src/
├── app/
│   ├── App.tsx
│   ├── router.tsx
│   ├── queryClient.ts
│   └── direction.ts
├── components/
│   ├── layout/
│   │   ├── AppShell.tsx
│   │   └── Sidebar.tsx
│   └── ui/
│       └── shadcn components...
├── features/
│   └── customers/
│       ├── api/
│       │   ├── customerApi.ts
│       │   └── customerKeys.ts
│       ├── components/
│       │   ├── CustomerForm.tsx
│       │   ├── CustomerStatusBadge.tsx
│       │   └── ...
│       ├── pages/
│       │   ├── CustomerSearchPage.tsx
│       │   ├── CustomerCreatePage.tsx
│       │   ├── CustomerDetailsPage.tsx
│       │   └── CustomerEditPage.tsx
│       ├── schemas/
│       │   └── customerSchemas.ts
│       └── types/
│           └── customer.ts
├── lib/
│   ├── apiClient.ts
│   ├── developmentActor.ts
│   └── utils.ts
├── main.tsx
└── index.css
```

You may simplify this if appropriate, but keep feature boundaries clear.

Avoid one giant `App.tsx`.

## 7. Backend connectivity

The backend Customer API already exists.

Current routes:

```text
POST   /api/customers
GET    /api/customers/{customerNumber}
GET    /api/customers/by-national-id/{nationalId}
GET    /api/customers/search?q={query}
PUT    /api/customers/{customerNumber}
PATCH  /api/customers/{customerNumber}/whatsapp-consent
PATCH  /api/customers/{customerNumber}/deactivate
```

During development, the backend normally runs on:

```text
http://localhost:5063
```

but do not scatter this URL through application code.

Configure the Vite development server to proxy:

```text
/api
/health
```

to the backend.

Use a non-secret environment variable for the proxy target, for example:

```text
OPTICORE_BACKEND_URL=http://localhost:5063
```

Provide:

```text
frontend/.env.example
```

with the default development value.

The frontend application itself should call relative paths such as:

```text
/api/customers
```

not hard-coded backend origins.

Do not modify backend CORS for this task unless proxying proves impossible. Prefer the Vite proxy.

## 8. API client

Create one small centralized typed API client.

Requirements:

- use `fetch`; do not add Axios unless there is a concrete need
- parse JSON responses
- handle empty `204 No Content`
- parse ASP.NET ProblemDetails responses
- throw a typed/sensible frontend error with HTTP status and user-safe message
- do not show raw stack traces or database details
- allow cancellation via `AbortSignal`
- no API URLs duplicated across pages

Do not create an excessively abstract HTTP framework.

## 9. Temporary development Employee ID

The backend currently requires:

```text
X-Employee-Id
```

for mutating Customer requests.

Real authentication/Employee identity does not exist yet.

For frontend development only:

- centralize this in `developmentActor.ts`
- in development mode, generate one stable UUID per browser using `crypto.randomUUID()`
- persist it in localStorage
- reuse the same UUID for later mutations in that browser
- attach it only to mutating Customer requests
- do not show this value in the normal UI
- add a clear comment that this is NOT authentication and must be removed when real auth exists
- do not hard-code one shared UUID in source control

For non-development builds, do not silently invent an actor identity. Fail clearly for mutation calls until real authentication exists.

Do not implement login/authentication in this task.

## 10. Customer TypeScript model

Mirror the existing backend `CustomerDto`.

At minimum:

```text
id
customerNumber
nationalId
firstName
lastName
dateOfBirth
mobilePhone
homePhone
email
city
street
gender
notes
whatsAppConsent
isActive
createdAtUtc
updatedAtUtc
```

Use appropriate TypeScript types.

Do not expose or invent fields that the backend does not return.

## 11. Customer API functions

Create typed functions for:

```text
createCustomer
getCustomerByNumber
getCustomerByNationalId
searchCustomers
updateCustomer
setWhatsAppConsent
deactivateCustomer
```

Integrate them with TanStack Query.

Use query keys consistently.

After mutations:

- invalidate/update relevant Customer queries
- keep details/search results reasonably fresh

Do not duplicate fetching logic inside pages.

## 12. Routes

Use React Router.

Implement:

```text
/customers
/customers/new
/customers/:customerNumber
/customers/:customerNumber/edit
```

Redirect `/` to `/customers` for now.

Do not implement functioning routes for unfinished modules.

## 13. Customer Search page

Route:

```text
/customers
```

Requirements:

- page title: Customers
- prominent `New Customer` button
- search field
- do not call backend for blank/whitespace query
- debounce non-empty search by roughly 250–400ms OR use an explicit Search button; choose one coherent approach
- clear loading state
- clear empty state
- clear error state

Search results table should show at least:

```text
Customer #
Name
National ID
Mobile
City
Status
```

Status:

- Active
- Inactive

Clicking a row should navigate to:

```text
/customers/{customerNumber}
```

Search can return multiple customers with the same phone number.

Inactive customers must remain visible.

Do not fetch the entire customer database when search is blank.

## 14. Create Customer page

Route:

```text
/customers/new
```

Use:

- React Hook Form
- Zod
- shadcn form/input/button components

Fields:

Required:

```text
National ID
First Name
Last Name
Date of Birth
Mobile Phone
City
Gender
```

Optional:

```text
Home Phone
Email
Street
Notes
WhatsApp Consent
```

Important validation rules:

Mirror currently confirmed backend rules and persistence limits.

Do NOT invent stricter business rules.

In particular:

- National ID is required and max 9 characters
- do not add Israeli checksum validation yet
- do not invent numeric-only validation unless the backend currently enforces it
- Email is optional; do not make frontend stricter than current backend by inventing mandatory email-format validation
- Gender is required but allowed values have not been finalized; do NOT invent a Male/Female enum/select
- use a normal required text field for Gender for now
- use current backend max-length limits
- DateOfBirth is required
- do not invent age/future-date business rules not present in backend

On successful create:

- show a success toast
- redirect to `/customers/{generatedCustomerNumber}`

On `409 Conflict`:

- show a clear user-safe message that a customer with this National ID already exists

On validation/API error:

- keep entered form values
- show useful feedback

## 15. Customer Details page

Route:

```text
/customers/:customerNumber
```

Display:

```text
Customer #
Active/Inactive status
Full name
National ID
Date of birth
Mobile phone
Home phone
Email
City
Street
Gender
Notes
WhatsApp consent
Created timestamp
Last updated timestamp
```

Use a clean card/section layout suitable for future expansion with Eye Exams and Orders.

Actions:

```text
Edit
WhatsApp consent toggle
Deactivate
```

For inactive customers:

- clearly show inactive state
- Deactivate action should be disabled/hidden
- viewing must still work

No reactivate action in this task.

## 16. Edit Customer page

Route:

```text
/customers/:customerNumber/edit
```

Editable fields exactly match backend update behavior:

```text
First Name
Last Name
Date of Birth
Mobile Phone
Home Phone
Email
City
Street
Gender
Notes
```

Do not allow editing:

```text
Id
CustomerNumber
NationalId
WhatsAppConsent
IsActive
CreatedAtUtc
UpdatedAtUtc
```

Use the same form component/schema where sensible to avoid duplication.

On success:

- show success toast
- return to Customer Details
- updated data should be visible without a hard browser refresh

## 17. WhatsApp consent

On Customer Details:

- show current consent state
- allow changing it with a switch/checkbox
- call the dedicated PATCH endpoint
- show loading state while saving
- show success/error feedback
- rollback/re-fetch if mutation fails

This is consent state only.

Do not send WhatsApp messages in this task.

## 18. Deactivate Customer

On Customer Details:

- provide a `Deactivate Customer` action
- require a confirmation dialog
- explain that the customer remains in the system but becomes inactive
- call the existing PATCH deactivate endpoint
- on success, update the UI to show `Inactive`
- do not delete the record
- do not create a reactivation action

Use a destructive-looking button/dialog treatment but not an alarming or flashy design.

## 19. Loading / errors / feedback

Use consistent UI feedback.

Requirements:

- loading skeleton/spinner where useful
- meaningful empty search state
- friendly 404 state for nonexistent customer
- friendly 409 duplicate message
- generic user-safe error for unexpected failures
- toast notifications for successful mutations
- buttons disabled while their mutation is in progress where appropriate

Do not expose technical exception messages from the server beyond safe ProblemDetails text.

## 20. shadcn/ui components

Use shadcn/ui components where they improve consistency.

Likely useful components include:

```text
Button
Input
Label
Card
Table
Badge
Textarea
Switch or Checkbox
Dialog / AlertDialog
Skeleton
Separator
Toast/Sonner
```

Install only what is actually used.

Do not install a huge component catalog preemptively.

## 21. Accessibility

At minimum:

- every form field has an associated label
- buttons have meaningful text or aria-labels
- dialogs are keyboard accessible
- table rows/actions remain understandable without color alone
- status uses both text and visual styling
- maintain reasonable focus behavior after navigation/dialog actions

## 22. Tests

Add frontend tests with:

- Vitest
- React Testing Library
- user-event

Do not add a large end-to-end browser framework such as Playwright/Cypress in this task.

Add focused tests for at least:

1. Customer search does not request API for blank query.
2. Customer search renders matching rows from mocked API data.
3. Create Customer client-side required validation prevents invalid submit.
4. Successful create redirects to generated CustomerNumber.
5. Duplicate NationalId (`409`) shows a clear message.
6. Customer details renders inactive status correctly.
7. Deactivate requires confirmation.
8. Development actor returns a stable non-empty UUID during development-mode test setup, without hard-coding one.

Mock network behavior at the API boundary in tests.

Do not test backend business rules again in frontend tests.

## 23. README / developer instructions

Update or add:

```text
frontend/README.md
```

Explain:

- required Node/npm
- install dependencies
- copy `.env.example` if needed
- backend should run locally
- default backend target
- frontend start command
- frontend build command
- test command
- lint command
- temporary development actor behavior

Keep instructions concise.

## 24. Package scripts

Provide working scripts similar to:

```json
{
  "dev": "...",
  "build": "...",
  "lint": "...",
  "test": "...",
  "test:run": "..."
}
```

Exact commands may reflect current Vite/Vitest conventions.

## 25. Verification

Run from `frontend/`:

```text
npm install
npm run build
npm run lint
npm run test:run
```

If package tooling prefers an equivalent command, use it and report it.

Also run from `backend/`:

```powershell
dotnet build OptiCore.slnx
dotnet test OptiCore.slnx
```

to confirm the frontend task did not accidentally break backend files.

If practical, run the backend and frontend development servers and verify at least:

```text
/customers
```

loads without runtime errors.

Do not create additional permanent test customer records unless approval is requested first.

Existing inactive development customer data may be used for read-only manual verification if present.

## 26. Git / safety rules

Do not:

- commit
- push
- switch branches
- merge
- reset
- rebase
- delete branches
- change backend business behavior
- create EF migrations
- expose User Secrets
- commit credentials/tokens
- hard-code passwords
- implement unfinished modules

Do not modify `AGENTS.md` unless a real contradiction is found. If so, stop and explain it.

The human developer will review and commit manually.

## 27. Completion criteria

The task is complete when:

- `frontend/` is a working React + TypeScript + Vite application.
- Tailwind CSS and shadcn/ui are configured.
- TanStack Query is configured.
- React Router is configured.
- React Hook Form + Zod are used for Customer forms.
- Vite proxies API traffic to the backend.
- Customer search/list works.
- Customer details works.
- Customer create works.
- Customer edit works.
- WhatsApp consent mutation works.
- Customer deactivation works.
- frontend uses the existing backend API only.
- temporary X-Employee-Id handling is centralized and development-only.
- UI is RTL-ready in structure.
- frontend build passes.
- frontend lint passes.
- frontend tests pass.
- backend still builds.
- backend 27 tests still pass.
- no backend migration/schema/business-rule change occurred.

## 28. Completion report

When finished, report:

1. Frontend packages installed
2. Files created
3. Files modified
4. Final frontend structure
5. Routes implemented
6. Customer screens/features implemented
7. API client/proxy design
8. Temporary development actor design
9. Tests added and what they cover
10. Exact commands run
11. Frontend build result
12. Frontend lint result
13. Frontend test result
14. Backend build/test result
15. Any assumptions or technical decisions
16. Anything the human developer should inspect carefully
17. Confirmation that no commit, push, branch change, backend migration, schema change, or secret exposure occurred

Do not commit or push the changes.
