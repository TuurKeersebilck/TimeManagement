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
| Admin Dashboard | `/admin` |
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

| Controller | Prefix | Description |
|---|---|---|
| `AuthController` | `/api/auth` | Register, login, logout, password reset |
| `ClockEventsController` | `/api/clockevents` | Clock in/out, today's events, summaries |
| `VacationsController` | `/api/vacations` | Request, approve, reject vacation days |
| `TimeAdjustmentRequestsController` | `/api/timeadjustmentrequests` | Submit and approve time corrections |
| `PublicHolidaysController` | `/api/publicholidays` | Manage public holidays |
| `NotificationsController` | `/api/notifications` | User notifications |
| `AdminController` | `/api/admin` | Employee management, time logs (admin only) |
| `AdminSettingsController` | `/api/admin/settings` | Global app configuration (admin only) |

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
