# TimeManagement API

ASP.NET Core 10 Web API for the Time Management application.

## Getting Started

### Prerequisites

- .NET 10 SDK
- PostgreSQL database (Supabase or local)

### Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/TuurKeersebilck/TimeManagement.git
   cd TimeManagement/backend
   ```

2. **Configure environment variables:**

   ```bash
   cp .env.template .env
   ```

   Edit `.env` with your credentials (see `.env.template` for all variables and descriptions).

3. **Run migrations:**
   ```bash
   dotnet ef database update
   ```

4. **Start the API:**
   ```bash
   dotnet run
   ```

   Available at `https://localhost:7055`.

## API Endpoints

All routes require a valid JWT token in the `Authorization: Bearer <token>` header, except auth endpoints.

### Authentication — `/api/auth`

| Method | Path | Description |
|--------|------|-------------|
| POST | `/register` | Register a new user |
| POST | `/login` | Login, returns JWT |
| POST | `/logout` | Invalidate token |
| GET | `/profile` | Get current user profile |
| POST | `/forgot-password` | Request password reset email |
| POST | `/reset-password` | Reset password via token |

### Clock Events — `/api/clockevents`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/today` | Get today's clock events for current user |
| GET | `/summaries` | Get daily summaries (hours worked per day) |
| POST | `/` | Submit a clock event (Clock In / Break Start / Break End / Clock Out) |
| GET | `/target` | Get the current user's daily/weekly target hours |

### Vacations — `/api/vacations`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | List vacation requests for current user |
| POST | `/` | Submit a vacation request |
| DELETE | `/{id}` | Cancel a vacation request |
| GET | `/balance` | Get vacation balance per type |
| GET | `/team` | Get team vacation calendar |

### Time Adjustment Requests — `/api/timeadjustmentrequests`

| Method | Path | Description |
|--------|------|-------------|
| POST | `/` | Submit a time correction request |
| GET | `/` | List own adjustment requests |

### Public Holidays — `/api/publicholidays`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | List all public holidays |

### Notifications — `/api/notifications`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | Get notifications for current user |
| PUT | `/{id}/read` | Mark notification as read |

### Admin — `/api/admin` _(admin role required)_

| Method | Path | Description |
|--------|------|-------------|
| GET | `/employees` | List all employees |
| GET | `/employees/{id}` | Get employee detail + time logs |
| POST | `/employees` | Create employee account |
| PUT | `/employees/{id}` | Update employee |
| DELETE | `/employees/{id}` | Delete employee |
| GET | `/timelogs` | Filter time logs across all employees |
| GET | `/vacations` | List pending vacation requests |
| PUT | `/vacations/{id}/approve` | Approve vacation request |
| PUT | `/vacations/{id}/reject` | Reject vacation request |
| GET | `/adjustment-requests` | List pending time adjustment requests |
| PUT | `/adjustment-requests/{id}/approve` | Approve adjustment |
| PUT | `/adjustment-requests/{id}/reject` | Reject adjustment |
| GET | `/dashboard` | Dashboard summary (who's clocked in, upcoming vacations) |
| GET | `/export` | Export time/vacation data |
| GET | `/vacation-types` | List vacation types |
| POST | `/vacation-types` | Create vacation type |
| PUT | `/vacation-types/{id}` | Update vacation type |
| DELETE | `/vacation-types/{id}` | Delete vacation type |
| GET | `/public-holidays` | List public holidays |
| POST | `/public-holidays` | Create public holiday |
| DELETE | `/public-holidays/{id}` | Delete public holiday |
| GET | `/targets` | List employee hour targets |
| PUT | `/targets/{userId}` | Update employee hour target |

### App Settings — `/api/admin/settings` _(admin role required)_

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | Get global app configuration |
| PUT | `/` | Update global app configuration |

## Security

- **Passwords** hashed via ASP.NET Core Identity
- **JWT** tokens with configurable expiry; revocation via token blacklist
- **Role-based auth** — `Admin` and `User` roles on all sensitive endpoints
- **CORS** restricted to configured origins
- **Rate limiting** on auth endpoints
- **Input validation** on all request bodies
- **User isolation** — service layer enforces users only access their own data
