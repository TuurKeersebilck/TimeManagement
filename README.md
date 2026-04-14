# TimeManagement

Full-stack employee time management app. Employees clock in/out throughout the day, request vacations, and track their hours. Admins manage employees, approve requests, and export data.

**Stack:** Vue 3 + Vite (frontend) · ASP.NET Core 10 (backend) · PostgreSQL (Supabase)  
**Deployed:** Vercel (frontend) · Railway (backend)

---

## Getting Started

### Prerequisites

- Node.js ≥ 20
- .NET 10 SDK
- PostgreSQL database (Supabase or local)

### Frontend

```bash
cd frontend
npm install
npm run dev        # http://localhost:5173
```

### Backend

```bash
cd backend
cp .env.template .env   # fill in credentials
dotnet ef database update
dotnet run              # https://localhost:7055
```

See [`backend/.env.template`](backend/.env.template) for all required environment variables (database connection string, JWT secret, SMTP credentials).

---

## Project Structure

```
TimeManagement/
├── frontend/          # Vue 3 + Vite SPA
│   └── src/
│       ├── views/          # Page components (employee + admin)
│       ├── components/     # Shared components + shadcn-vue UI
│       ├── composables/    # Vue composition hooks
│       ├── services/       # Axios API service modules
│       ├── layouts/        # AuthenticatedLayout wrapper
│       ├── router/         # Vue Router config
│       └── utils/          # apiError helper
├── backend/           # ASP.NET Core 10 Web API
│   ├── Controllers/        # HTTP endpoints (auth, clock events, vacations, admin…)
│   ├── Services/           # Business logic
│   ├── Models/             # Domain entities + DTOs
│   ├── Data/               # EF Core DbContext
│   ├── Config/             # AutoMapper, JWT, SMTP config
│   ├── Middleware/         # Global exception handler
│   ├── Helpers/            # Time calculation utilities
│   └── Migrations/         # EF Core migrations
└── .github/           # GitHub Actions workflows
```

### Frontend views

| View | Path |
|------|------|
| Dashboard | `/` |
| Time Tracking (clock in/out) | `/time-tracking` |
| Vacations | `/vacations` |
| Team Calendar | `/team-calendar` |
| Account | `/account` |
| **Admin** | |
| Admin Dashboard | `/admin/dashboard` |
| Employees | `/admin/employees` |
| Employee Detail | `/admin/employees/:id` |
| Vacation Approvals | `/admin/vacations` |
| Vacation Types | `/admin/vacation-types` |
| Time Adjustment Requests | `/admin/adjustment-requests` |
| Time Logs | `/admin/time-logs` |
| App Settings | `/admin/settings` |
| Export | `/admin/export` |

### Backend API endpoints

All routes under `/api/`. JWT required on all routes except auth.

**Auth — `/api/auth`**

| Method | Path | Description |
|--------|------|-------------|
| POST | `/register` | Register a new user |
| POST | `/login` | Login, returns JWT |
| POST | `/logout` | Invalidate token |
| GET | `/profile` | Get current user profile |
| PUT | `/profile` | Update profile |
| PUT | `/change-password` | Change password |
| POST | `/forgot-password` | Send password reset email |
| POST | `/reset-password` | Reset password via token |

**Clock Events — `/api/clockevents`**

| Method | Path | Description |
|--------|------|-------------|
| GET | `/today` | Today's clock events for current user |
| GET | `/summaries` | Daily summaries (hours worked per day) |
| GET | `/target` | Current user's daily/weekly target hours |
| POST | `/` | Submit a clock event (Clock In / Break Start / Break End / Clock Out) |

**Vacations — `/api/vacations`**

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | List own vacation requests |
| POST | `/` | Submit a single vacation day request |
| POST | `/range` | Submit a date-range vacation request |
| PUT | `/{id}` | Update a vacation request |
| DELETE | `/{id}` | Cancel a vacation request |
| GET | `/balances` | Get vacation balance per type |
| GET | `/team` | Get team vacation calendar |
| GET | `/types` | List vacation types |

**Time Adjustment Requests — `/api/timeadjustmentrequests`**

| Method | Path | Description |
|--------|------|-------------|
| POST | `/` | Submit a time correction request |
| GET | `/` | List own adjustment requests |
| POST | `/{id}/reject` | Reject a request (admin) |
| GET | `/approve/{token}` | Approve via email link token (admin) |

**Notifications — `/api/notifications`**

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | List notifications for current user |
| GET | `/unread-count` | Count of unread notifications |
| PUT | `/{id}/read` | Mark a notification as read |
| PUT | `/read-all` | Mark all notifications as read |

**Public Holidays — `/api/publicholidays`**

| Method | Path | Description |
|--------|------|-------------|
| GET | `/{year}` | List public holidays for a given year |

**Admin — `/api/admin`** _(admin role required)_

| Method | Path | Description |
|--------|------|-------------|
| GET | `/employees` | List all employees |
| GET | `/timelogs` | Filter time logs across all employees |
| GET | `/vacations` | List vacation requests |
| GET | `/vacation-types` | List vacation types |
| POST | `/vacation-types` | Create vacation type |
| PUT | `/vacation-types/{id}` | Update vacation type |
| DELETE | `/vacation-types/{id}` | Delete vacation type |
| GET | `/employees/{userId}/vacation-balances` | Get employee vacation balances |
| POST | `/employees/{userId}/vacation-balances` | Add vacation balance |
| PUT | `/employees/{userId}/vacation-balances/{balanceId}` | Update vacation balance |
| DELETE | `/employees/{userId}/vacation-balances/{balanceId}` | Delete vacation balance |
| GET | `/employees/{userId}/target` | Get employee hour target |
| PUT | `/employees/{userId}/target` | Update employee hour target |
| GET | `/employees/{userId}/weekly-summary` | Get employee weekly summary |
| GET | `/export` | Export time/vacation data |

**Admin Settings — `/api/admin/settings`** _(admin role required)_

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | Get global app configuration |
| PUT | `/country` | Update country setting |
| PUT | `/targets` | Update default hour targets |
| PUT | `/notification-email` | Update notification email address |
| GET | `/available-countries` | List available countries |
| GET | `/holidays/{year}` | List holidays for year |
| POST | `/holidays` | Add a public holiday |
| POST | `/holidays/refresh/{year}` | Refresh holidays from external source |
| DELETE | `/holidays/{id}` | Delete a public holiday |

---

## Features

- **Clock in/out** — Employees log Clock In, Break Start, Break End, and Clock Out with a ±5 min adjustment window
- **Time adjustment requests** — Employees submit correction requests; admins approve via email link
- **Vacation management** — Request, approve/reject vacation days with balance tracking per vacation type
- **Public holidays** — Configurable per country/region, automatically excluded from vacation counts
- **Team calendar** — View the whole team's scheduled time off
- **Admin dashboard** — See who's clocked in today, upcoming vacations, pending requests
- **Notifications** — In-app bell for pending admin actions
- **Data export** — Export time logs and vacation data
- **Email** — Transactional emails via Gmail SMTP (MailKit)
- **Dark mode** — System-aware with manual toggle
- **Role-based access** — `Admin` and `User` roles enforced on both frontend (router guards) and backend (`[Authorize(Roles = ...)]`)

---

## Deployment

### Frontend (Vercel)

Deployed automatically on push to `main`. `vercel.json` rewrites `/api/*` to the Railway backend.

### Backend (Railway)

Railway detects .NET automatically. Set the following environment variables in the Railway dashboard (see `.env.template`):

| Variable | Description |
|---|---|
| `CONNECTION_STRING` | PostgreSQL connection string |
| `JWT_SECRET` | HS256 signing key (≥ 32 chars) |
| `JWT_ISSUER` | Token issuer (e.g. `TimeManagementAPI`) |
| `JWT_AUDIENCE` | Token audience (e.g. `TimeManagementAPI`) |
| `JWT_EXPIRY_MINUTES` | Token lifetime |
| `CORS_ORIGINS` | Comma-separated allowed origins |
| `SMTP_HOST` | SMTP server (e.g. `smtp.gmail.com`) |
| `SMTP_PORT` | SMTP port (e.g. `587`) |
| `SMTP_USER` | Sender email address |
| `SMTP_PASS` | Gmail App Password |

### Database migrations

EF Core migrations run automatically on startup in production. To add a new migration locally:

```bash
cd backend
dotnet ef migrations add <MigrationName>
dotnet ef database update
```
