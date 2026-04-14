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
| PUT | `/profile` | Update profile |
| PUT | `/change-password` | Change password |
| POST | `/forgot-password` | Send password reset email |
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
| GET | `/` | List own vacation requests |
| POST | `/` | Submit a single vacation day request |
| POST | `/range` | Submit a date-range vacation request |
| PUT | `/{id}` | Update a vacation request |
| DELETE | `/{id}` | Cancel a vacation request |
| GET | `/balances` | Get vacation balance per type |
| GET | `/team` | Get team vacation calendar |
| GET | `/types` | List vacation types |

### Time Adjustment Requests — `/api/timeadjustmentrequests`

| Method | Path | Description |
|--------|------|-------------|
| POST | `/` | Submit a time correction request |
| GET | `/` | List own adjustment requests |
| POST | `/{id}/reject` | Reject a request (admin) |
| GET | `/approve/{token}` | Approve via email link token (admin) |

### Public Holidays — `/api/publicholidays`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/{year}` | List public holidays for a given year |

### Notifications — `/api/notifications`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | List notifications for current user |
| GET | `/unread-count` | Count of unread notifications |
| PUT | `/{id}/read` | Mark a notification as read |
| PUT | `/read-all` | Mark all notifications as read |

### Admin — `/api/admin` _(admin role required)_

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

### App Settings — `/api/admin/settings` _(admin role required)_

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

## Security

- **Passwords** hashed via ASP.NET Core Identity
- **JWT** tokens with configurable expiry; revocation via token blacklist
- **Role-based auth** — `Admin` and `User` roles on all sensitive endpoints
- **CORS** restricted to configured origins
- **Rate limiting** on auth endpoints
- **Input validation** on all request bodies
- **User isolation** — service layer enforces users only access their own data
