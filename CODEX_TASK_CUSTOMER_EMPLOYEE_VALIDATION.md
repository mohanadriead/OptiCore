# Codex Task — Customer + Employee Validation Updates

Read `AGENTS.md` completely before doing anything else.

## Goal

Implement the pending OptiCore data-quality and Hebrew-input fixes after the Employee/Auth backend merge.

Scope:

1. Customer National ID: exactly 9 ASCII digits.
2. Customer mobile phone: exactly 10 ASCII digits.
3. Customer home phone: optional; if provided, exactly 9 ASCII digits.
4. Customer gender: backend restricted to `Male` / `Female`; frontend shown as mutually-exclusive Hebrew radio choices `זכר` / `נקבה`.
5. Customer date of birth: replace the browser-native date input with a controlled Hebrew/Israeli date-entry UI so Arabic-localized placeholders cannot appear.
6. Employee National ID: exactly 9 ASCII digits.
7. Employee phone: exactly 10 ASCII digits.
8. Add/update tests.
9. Perform a read-only check of existing development data for violations.

Do not implement authentication frontend in this task.
Do not redesign Customer search.
Do not change inactive-customer search behavior.
Do not hard-delete or silently modify existing Customer/Employee records.
Do not implement Attendance or the future fine-grained permissions matrix.

---

## Repository / branch

The Employee/Auth backend has already been merged to `main`.

Expected working branch:

```text
fix/customer-employee-validation
```

Before editing, inspect:

```text
git status
git diff
git branch --show-current
```

If the current branch is not the expected fix branch, stop and tell the human. Do not switch branches yourself.

---

## Preserve existing behavior

Do not change:

- Customer search semantics.
- Inactive Customers appearing in search.
- Customer soft deactivation.
- Employee auth/cookie architecture.
- Authorization rules.
- Audit attribution.
- Hebrew RTL layout.
- Any business rule not listed here.

Inactive Customers must remain searchable and viewable.

---

## Customer National ID

Rule:

```regex
^[0-9]{9}$
```

Exactly 9 ASCII digits.

Valid:
```text
123456789
```

Invalid:
```text
Jax
12345678
1234567890
12345A789
```

Enforce in both backend and frontend.

Keep it as a string. Do not use `type="number"`.
Use numeric input hints such as `inputMode="numeric"` where appropriate.

Do not add Israeli checksum validation in this task.

---

## Customer mobile phone

Required.

Rule:

```regex
^[0-9]{10}$
```

Exactly 10 ASCII digits.

Enforce in backend and frontend.

Keep it as a string.

---

## Customer home phone

Optional.

Blank/null is valid.

If provided:

```regex
^[0-9]{9}$
```

Exactly 9 ASCII digits.

Enforce in backend and frontend.

Preserve the existing blank-to-null normalization behavior.

---

## Customer gender

Remove free-text gender entry.

Canonical backend/API values:

```text
Male
Female
```

Hebrew UI labels:

```text
זכר
נקבה
```

Backend must reject anything else.

Frontend must use radio-button behavior, not two independent checkboxes:

```text
מגדר *

○ זכר
○ נקבה
```

Selecting one must deselect the other.

Create mode, edit mode, and details view must work correctly.

Keep existing canonical persisted values compatible. Avoid unnecessary schema changes.

---

## Customer Date of Birth — Hebrew controlled UI

The current native browser `<input type="date">` can render Arabic placeholder text depending on Windows/browser locale.

Do not rely on browser-native locale for DOB entry.

Replace it with a controlled Hebrew/Israeli date entry, preferably:

```text
יום | חודש | שנה
```

using three accessible inputs/selects.

Requirements:

- no Arabic-localized placeholder
- Hebrew labels/placeholders
- works correctly in RTL
- create mode works
- edit mode loads existing date
- submitted API value remains `YYYY-MM-DD`
- impossible calendar dates are rejected
- backend/database date type and storage semantics remain unchanged
- avoid adding a heavy date-picker dependency

A small reusable component is preferred if it improves clarity.

---

## Employee validations

Apply only the relevant rules to Employees.

### Employee National ID

Exactly 9 ASCII digits:

```regex
^[0-9]{9}$
```

Apply to:
- manager-created Employee
- bootstrap manager

### Employee phone

Exactly 10 ASCII digits:

```regex
^[0-9]{10}$
```

Apply to:
- manager-created Employee
- bootstrap manager

Do not build Employee frontend in this task.
Do not change authentication or authorization behavior.
Do not expose bootstrap secret values in errors/logs.

---

## Existing development data

Development data may contain records created before these validations existed.

Do NOT automatically edit, delete, or repair them.

After implementation, perform a READ-ONLY audit of `opticore_dev`.

Check Customers for:
- invalid NationalId
- invalid MobilePhone
- invalid non-empty HomePhone
- invalid Gender

Check Employees for:
- invalid NationalId
- invalid Phone

In the report, show only:

```text
CustomerNumber / EmployeeNumber
failed rule(s)
```

Do not print raw National IDs, phone numbers, passwords, password hashes, or secrets.

---

## Backend implementation

Inspect existing Customer and Employee validation before editing.

Prefer small reusable helpers when useful.

Do not:
- move business validation to Infrastructure
- expose EF types outside Infrastructure
- use numeric types for IDs/phones
- weaken existing validation
- unnecessarily change API contracts

Invalid input should continue to surface as safe `400 Bad Request` responses through existing exception handling.

---

## Frontend UX / messages

Customer create/edit must show immediate Hebrew validation feedback.

Suggested messages:

```text
תעודת זהות חייבת להכיל בדיוק 9 ספרות.
טלפון נייד חייב להכיל בדיוק 10 ספרות.
טלפון בבית חייב להכיל בדיוק 9 ספרות.
יש לבחור מגדר.
יש להזין תאריך לידה תקין.
```

Use existing project tone/style if appropriate.

Keep numeric/contact/date fields easy to read even inside RTL layout.

---

## Backend tests

Add/update focused tests for at least:

1. valid 9-digit Customer National ID accepted
2. alphabetic Customer National ID rejected
3. 8-digit Customer National ID rejected
4. 10-digit Customer National ID rejected
5. valid 10-digit Customer mobile accepted
6. invalid-length Customer mobile rejected
7. non-digit Customer mobile rejected
8. empty optional Customer home phone accepted
9. valid 9-digit Customer home phone accepted
10. invalid Customer home phone rejected
11. `Male` accepted
12. `Female` accepted
13. arbitrary gender text rejected
14. other Customer behavior unchanged
15. valid 9-digit Employee National ID accepted
16. invalid Employee National ID rejected
17. valid 10-digit Employee phone accepted
18. invalid Employee phone rejected
19. bootstrap manager obeys Employee validation
20. valid Employee authentication behavior remains unchanged

Keep all existing backend tests passing.

---

## Frontend tests

Add/update tests for at least:

1. invalid Customer National ID rejected
2. invalid Customer mobile rejected
3. empty home phone accepted
4. invalid non-empty home phone rejected
5. gender is rendered as Hebrew mutually-exclusive radio choices
6. `זכר` submits canonical `Male`
7. `נקבה` submits canonical `Female`
8. edit form preselects persisted gender
9. DOB control uses Hebrew day/month/year labels/placeholders
10. no native Arabic date-placeholder dependency remains
11. valid DOB produces `YYYY-MM-DD`
12. edit mode loads existing DOB correctly
13. impossible DOB is rejected
14. Customer details show canonical gender in Hebrew

Keep all existing frontend tests passing.

---

## Database migration

No migration is expected for this task.

Do not create a migration solely for application validation.

If you believe a migration is materially necessary, stop and ask the human before generating it.

This is especially important because old development rows may violate the new rules.

---

## Verification

Backend, from `backend/`:

```powershell
dotnet build OptiCore.slnx
dotnet test OptiCore.slnx
```

Frontend, from `frontend/`:

```powershell
npm.cmd run build
npm.cmd run lint
npm.cmd run test:run
```

Repository root:

```powershell
git diff --check
git status --short
```

Also perform the read-only `opticore_dev` validation audit.

Because Employee/Auth frontend is not implemented yet, do not broaden this task just to perform authenticated browser mutation flows. Component/unit tests and safe visual form checks are sufficient.

---

## Safety / Git rules

Do not:

- commit
- push
- switch branches
- merge
- rebase
- reset
- delete branches
- expose User Secrets
- print passwords/password hashes
- print raw National IDs
- print raw phone numbers
- modify existing Customer/Employee records during the audit
- hard-delete Customers or Employees
- change search behavior
- hide inactive Customers from search
- implement auth frontend
- implement Attendance
- implement detailed permissions

The human will review and commit manually.

---

## Completion criteria

Complete when:

- Customer NationalId = exactly 9 ASCII digits in frontend/backend.
- Customer MobilePhone = exactly 10 ASCII digits in frontend/backend.
- Customer HomePhone = optional; if present exactly 9 ASCII digits in frontend/backend.
- Customer gender is restricted to canonical Male/Female backend values.
- Customer gender UI uses Hebrew radio buttons.
- Customer details show gender in Hebrew.
- Customer DOB no longer relies on browser-native localized placeholders.
- Customer DOB stays `YYYY-MM-DD` at API/storage boundary.
- Employee NationalId = exactly 9 ASCII digits.
- Employee Phone = exactly 10 ASCII digits.
- Bootstrap manager obeys Employee validation.
- Inactive Customers remain searchable.
- Existing behavior outside scope remains unchanged.
- backend build/tests pass.
- frontend build/lint/tests pass.
- existing data was audited read-only.
- no secrets/sensitive values were exposed.
- no migration was added unless explicitly approved.

---

## Completion report

Report:

1. files created
2. files modified
3. Customer validation rules implemented
4. Employee validation rules implemented
5. gender canonical values + Hebrew mapping
6. DOB control design
7. whether API/storage contracts changed
8. backend tests added/updated
9. frontend tests added/updated
10. backend build/test results
11. frontend build/lint/test results
12. read-only audit result:
   - number of violating Customers
   - CustomerNumbers only + failed rule(s)
   - number of violating Employees
   - EmployeeNumbers only + failed rule(s)
13. confirmation no existing data was modified
14. confirmation inactive Customer search was unchanged
15. whether a migration was needed
16. exact commands run
17. assumptions / review notes
18. confirmation no commit, push, branch switch, auth-frontend work, Attendance work, permission-matrix work, secret exposure, or hard deletion occurred
