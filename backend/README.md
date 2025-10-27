# TimeManagement API

Backend API for the Time Management application built with .NET 9.

## Getting Started

### Prerequisites

- .NET 9 SDK
- SQLite (default) or MariaDB

### Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/TuurKeersebilck/TimeManagement.git
   cd TimeManagement/backend
   ```

2. **Configure Environment Variables:**

   Copy `.env.template` to `.env` and update the values:

   ```bash
   cp .env.template .env
   ```

   Edit the `.env` file with your database connection and JWT settings:

   ```
   CONNECTION_STRING=Data Source=timemanagement.db
   JWT_SECRET=your_very_secure_random_string_here
   JWT_ISSUER=TimeManagementAPI
   JWT_AUDIENCE=TimeManagementAPI
   JWT_EXPIRY_MINUTES=60
   ```

   For MariaDB, use a connection string like:
   ```
   CONNECTION_STRING=Server=localhost;Database=timemanagement;User=root;Password=yourpassword;
   ```

3. **Run Migrations:**
   ```bash
   dotnet ef database update
   ```

4. **Start the API:**
   ```bash
   dotnet run
   ```

   The API will be available at https://localhost:7055 (or the port specified in your configuration).

## API Endpoints

### Authentication

- **POST /api/auth/register** - Register a new user
  ```json
  {
    "username": "testuser",
    "email": "test@example.com",
    "password": "Password123",
    "confirmPassword": "Password123"
  }
  ```

- **POST /api/auth/login** - Login and get JWT token
  ```json
  {
    "email": "test@example.com",
    "password": "Password123"
  }
  ```

### Time Logs (Requires Authentication)

All time log endpoints require a valid JWT token passed in the Authorization header:
`Authorization: Bearer {your_token}`

- **GET /api/timelogs** - Get all time logs for current user
- **GET /api/timelogs/{id}** - Get a specific time log
- **POST /api/timelogs** - Create a new time log
  ```json
  {
    "date": "2023-10-25",
    "startTime": "09:00:00",
    "endTime": "17:00:00",
    "break": "00:30:00"
  }
  ```
- **PUT /api/timelogs/{id}** - Update an existing time log
- **DELETE /api/timelogs/{id}** - Delete a time log

## Security Features

- **Password Security**: Secure hashing using ASP.NET Core Identity
- **JWT Authentication**: Tokens expire after the configured time period
- **Input Validation**: All inputs are validated to prevent injection attacks
- **User Isolation**: Users can only access their own time logs
- **Environment Variables**: Sensitive configuration is loaded from environment variables
- **HTTPS Support**: The API enforces HTTPS in production environments