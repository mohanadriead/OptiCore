# Codex Follow-up Task — Hebrew + RTL Customer Frontend

Read `AGENTS.md` and `CODEX_TASK_CUSTOMER_FRONTEND.md` at the repository root before doing anything else.

## Goal

Convert the existing OptiCore Customer frontend from English/LTR presentation to **Hebrew/RTL** presentation.

This is a focused follow-up task. The current Customer frontend already works and must keep its current behavior.

Do **not** redesign search behavior, add new features, change backend contracts, or modify business rules.

## Product language decision

OptiCore's current user-facing interface should be:

- Hebrew
- RTL

Code identifiers, filenames, TypeScript types, API contracts, backend DTOs, and internal engineering terminology remain in English.

Do not rename code symbols just to translate the interface.

## Scope

Translate and RTL-adjust the currently implemented frontend only:

- application shell/sidebar
- customer search page
- customer create page
- customer details page
- customer edit page
- customer status display
- WhatsApp consent UI
- deactivate confirmation dialog
- loading/empty/error states
- success toasts
- not-found states
- form validation messages
- navigation labels
- any other currently visible user-facing English text

Do not implement unfinished modules.

Do not change Customer search semantics in this task.

## Backend safety

Do not modify anything under `backend/`.

Do not change:

- API routes
- DTOs
- request shapes
- persistence
- migrations
- business rules
- search behavior
- backend validation

If a Hebrew UX requirement appears to require a backend change, stop and report it.

## Direction and language

Make Hebrew/RTL the default application presentation.

Set application/document language and direction centrally:

```html
lang="he"
dir="rtl"
```

or equivalent centralized React behavior.

The existing centralized direction setup should become RTL-first.

Avoid scattering `dir="rtl"` through every component unless a local override is genuinely required.

## RTL layout review

Review the current UI for RTL correctness.

Prefer logical layout/alignment concepts where practical:

- start/end
- ms/me
- ps/pe
- border-s/border-e
- text-start/text-end

Avoid unnecessary hard-coded left/right assumptions.

Ensure these remain visually correct in RTL:

- sidebar
- page headers
- action buttons
- search field + search button
- tables
- cards
- dialogs
- forms
- labels
- validation messages
- badges
- breadcrumbs/back links
- toast notifications

Do not perform an unrelated redesign.

## Hebrew terminology

Use clear, professional Hebrew consistently.

Recommended terminology:

```text
Customers                 → לקוחות
Customer                  → לקוח
New Customer              → לקוח חדש
Create Customer           → יצירת לקוח
Edit Customer             → עריכת לקוח
Save Changes              → שמירת שינויים
Cancel                    → ביטול
Search                    → חיפוש
Search customers          → חיפוש לקוחות
Customer #                → מספר לקוח
National ID               → תעודת זהות
First Name                → שם פרטי
Last Name                 → שם משפחה
Full Name                 → שם מלא
Date of Birth             → תאריך לידה
Mobile Phone              → טלפון נייד
Home Phone                → טלפון בבית
Email                     → דוא"ל
City                      → עיר
Street                    → רחוב
Gender                    → מגדר
Notes                     → הערות
Active                    → פעיל
Inactive                  → לא פעיל
Status                    → סטטוס
Customer information      → פרטי לקוח
Communication preferences → העדפות תקשורת
WhatsApp consent          → הסכמה להודעות WhatsApp
Consent given             → ניתנה הסכמה
Consent not given         → לא ניתנה הסכמה
Record activity           → פעילות הרשומה
Created                   → נוצר בתאריך
Last updated              → עודכן לאחרונה
Not updated yet           → טרם עודכן
Deactivate Customer       → השבתת לקוח
Confirm Deactivation      → אישור השבתה
Back to customers         → חזרה ללקוחות
Development               → סביבת פיתוח
Dashboard                 → לוח בקרה
Orders                    → הזמנות
Inventory                 → מלאי
Eye Exams                 → בדיקות ראייה
Employees                 → עובדים
Reports                   → דוחות
Settings                  → הגדרות
Soon                      → בקרוב
```

You may improve individual wording slightly when Hebrew reads more naturally, but keep terminology consistent.

## Search page

Translate the current search UI to Hebrew without changing search logic.

Examples:

```text
Customers
→ לקוחות

Find a customer and manage their details.
→ חיפוש לקוח וניהול פרטיו.

Search customers
→ חיפוש לקוחות

Includes active and inactive customers.
→ החיפוש כולל לקוחות פעילים ולא פעילים.
```

Translate table headers, loading, empty and error states.

Keep the existing explicit Search button behavior.

## Forms

Translate all form labels, helper text, buttons, required indicators, and validation messages.

Use:

```text
שדות המסומנים ב-* הם שדות חובה.
```

Examples:

```text
This field is required.
→ שדה זה הוא שדה חובה.

Use at most 100 characters.
→ ניתן להזין עד 100 תווים.

Enter a valid date.
→ יש להזין תאריך תקין.
```

Do not change validation rules.

Do not invent Gender options or an enum. Gender remains a text input.

## API errors

Translate friendly frontend errors.

Duplicate National ID:

```text
לקוח עם תעודת זהות זו כבר קיים במערכת.
```

Customer not found:

```text
הלקוח לא נמצא.
```

Generic failure:

```text
לא ניתן להשלים את הפעולה. נסה שוב.
```

Connectivity failure:

```text
לא ניתן להתחבר ל-OptiCore. בדוק את החיבור ונסה שוב.
```

Do not expose raw backend/database details.

## Customer details

Translate all sections, fields, statuses and actions.

Inactive explanation:

```text
לקוח זה אינו פעיל. הרשומה נשמרת במערכת ונשארת זמינה לצפייה ולחיפוש.
```

Deactivation explanation:

```text
השבתת הלקוח אינה מוחקת אותו מהמערכת. הרשומה תישאר זמינה לצפייה ולחיפוש.
```

## Deactivation dialog

Translate fully.

Suggested text:

```text
כותרת:
השבתת לקוח

תיאור:
האם להשבית את {customerName}?
הרשומה תישאר במערכת ותהיה זמינה לצפייה ולחיפוש.

כפתורים:
ביטול
אישור השבתה
```

Preserve behavior exactly.

## Toasts

Translate all existing success/error toasts.

Suggested messages:

```text
Customer created
→ הלקוח נוצר בהצלחה

Customer updated
→ פרטי הלקוח עודכנו בהצלחה

WhatsApp consent updated
→ הסכמת WhatsApp עודכנה

Customer deactivated
→ הלקוח הושבת בהצלחה
```

## Mixed RTL/LTR data

The interface is RTL, but some data is naturally LTR.

Use appropriate local `dir="ltr"` only where useful for readability, such as:

- National ID
- phone numbers
- email addresses
- UUIDs if shown
- technical URLs

Do not make entire forms LTR.

Date display should use Hebrew/Israel formatting where practical (`he-IL`).

Preserve `dateOfBirth` as a date-only value without timezone shifts.

Audit timestamps may use Hebrew/Israel local formatting.

Keep the brand name `WhatsApp` in Latin characters inside Hebrew text.

## Sidebar/navigation

Translate all visible navigation text.

Only Customers remains functional.

Future modules remain disabled and marked:

```text
בקרוב
```

Do not add routes/features.

Ensure the sidebar looks correct in RTL.

## Accessibility

Preserve accessibility:

- labels remain associated with fields
- user-facing aria-labels should be Hebrew
- dialogs remain keyboard accessible
- status remains text-based, not color-only
- focus behavior stays intact
- screen-reader-facing visible UI text should be Hebrew

## No full i18n framework yet

Do **not** add react-i18next or another translation framework.

The current product language is Hebrew.

Keep visible text organized cleanly, but do not add multilingual infrastructure yet.

## Tests

Update frontend tests to reflect Hebrew UI.

Do not remove behavioral coverage.

Ensure coverage verifies at least:

1. Root app renders RTL.
2. Main Customer heading is Hebrew.
3. Required validation message is Hebrew.
4. Duplicate National ID error is Hebrew.
5. Inactive status is Hebrew.
6. Deactivation confirmation is Hebrew.
7. Existing search/create/edit/consent/deactivate behavior remains covered.

API mocks/business behavior must remain unchanged.

## Verification

Run from `frontend/`:

```powershell
npm.cmd run build
npm.cmd run lint
npm.cmd run test:run
```

Then from `backend/`:

```powershell
dotnet build OptiCore.slnx
dotnet test OptiCore.slnx
```

Backend must remain unchanged and all 27 tests must still pass.

If practical, visually inspect:

```text
/customers
/customers/new
/customers/:customerNumber
/customers/:customerNumber/edit
```

Check for:

- clipped Hebrew
- wrong alignment
- reversed icon/text spacing
- broken dialogs
- bad table alignment
- awkward RTL/LTR values
- console errors

Do not create additional permanent customer records for this translation task.

## Git/safety

Do not:

- commit
- push
- switch branches
- merge
- reset/rebase
- modify backend code
- change search behavior
- create migrations
- change business rules
- add authentication
- add unfinished modules
- expose secrets

The human developer will review and commit manually.

## Completion criteria

Complete when:

- Customer frontend is Hebrew-first.
- App defaults to RTL.
- All current visible Customer UI is translated.
- Sidebar/navigation is translated.
- Functionality is unchanged.
- Search behavior is unchanged.
- Validation/error/success messages are Hebrew.
- Dates render appropriately for Hebrew/Israel.
- Numeric/LTR values remain readable.
- Accessibility remains intact.
- Frontend build/lint/tests pass.
- Backend build passes.
- All 27 backend tests pass.
- No backend files were changed.

## Completion report

Report:

1. Files modified
2. Hebrew terminology decisions
3. RTL changes
4. Mixed RTL/LTR handling
5. Tests updated/added
6. Exact commands run
7. Frontend build result
8. Frontend lint result
9. Frontend test result
10. Backend build/test result
11. Visual/manual checks
12. Any intentionally untranslated user-facing text and why
13. Anything the human developer should inspect carefully
14. Confirmation that search behavior, backend code, schema, business rules, Git history, and secrets were not changed

Do not commit or push.
