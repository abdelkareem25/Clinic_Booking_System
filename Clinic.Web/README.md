# Clinic Booking — Web (Angular)

Front-end for the Clinic Booking System, built with **Angular 21 (standalone + signals)**,
**Angular Material (M3)**, **Tailwind CSS v4**, Reactive Forms and JWT authentication. It consumes
the existing ASP.NET Core Web API in `../Clinic.Api`.

## Tech stack

- Angular standalone components, lazy-loaded feature routes, `OnPush` change detection
- Signals for component state; RxJS for data access
- Angular Material M3 theming with a dark / light toggle (persisted)
- Tailwind CSS v4 utilities (preflight disabled to avoid clashing with Material)
- JWT auth: HTTP interceptor, auth/role/guest guards, role-based navigation

## Getting started

```bash
npm install
npm start          # ng serve → http://localhost:4200
```

The API base URL is configured in [`src/environments/environment.ts`](src/environments/environment.ts)
(`https://localhost:7210/api`). Start the API with the **https** profile so the port matches.

### Seeded login

The API seeds a single account:

- **Email:** `AbdelkarimBadr@gmail.com`
- **Password:** `Password123!`

## Project structure

```text
src/app
├── core          # models, services, guards, interceptors, utils (no UI)
├── shared        # reusable UI: data-table, page-header, dialogs, pipes, directives
├── layouts       # shell (sidebar + navbar + breadcrumb) and auth layouts
├── features      # dashboard, doctors, patients, appointments, doctor-schedules, users, auth, errors
├── routes        # navigation config
└── app.routes.ts
```

## Roles

`Admin`, `Doctor`, `Receptionist`. Navigation items and the Users module are shown/hidden by role.
Roles are read from the JWT; the seeded account has no role, so it sees every non-restricted page but
not the Admin-only Users list.

## Build

```bash
npm run build      # production build → dist/
```

## ⚠️ Backend changes required to run end-to-end

The API is consumed as-is; no fake endpoints were added. Two backend issues currently prevent the
SPA from working against it. They live in `../Clinic.Api` and are **not** changed by this front-end:

1. **CORS is not configured.** The browser will block requests from `http://localhost:4200`.
   Add a policy in `Program.cs`:

   ```csharp
   builder.Services.AddCors(o => o.AddPolicy("Spa", p => p
       .WithOrigins("http://localhost:4200")
       .AllowAnyHeader()
       .AllowAnyMethod()));
   // after build(), before UseAuthentication():
   app.UseCors("Spa");
   ```

2. **`AccountsController.Login` is decorated with `[Authorize]`**, so logging in requires a token
   you don't have yet. Remove `[Authorize]` from the `Login` action.

### Known API gaps (handled gracefully in the UI)

- **No "list users" endpoint** — the Users page shows the current session and a notice.
- **Appointments have no status field** — status (`Upcoming` / `Today` / `Past`) is derived from the
  appointment date and shown as Material chips.
- **List endpoints return `404` when empty** — treated as an empty state (no error toast).
