# Time Management

![License](https://img.shields.io/github/license/TuurKeersebilck/TimeManagement)
![Issues](https://img.shields.io/github/issues/TuurKeersebilck/TimeManagement)
![Last Commit](https://img.shields.io/github/last-commit/TuurKeersebilck/TimeManagement)
![Vue](https://img.shields.io/badge/Vue-3-4FC08D?logo=vue.js&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-9-512BD4?logo=dotnet&logoColor=white)
![Tailwind CSS](https://img.shields.io/badge/Tailwind_CSS-v4-06B6D4?logo=tailwindcss&logoColor=white)

A full-stack time tracking and management application.

- **Frontend:** Vue 3 + Vite + shadcn-vue + Tailwind CSS v4
- **Backend:** .NET 9 ASP.NET Core Web API
- **Database:** MariaDB (SQLite fallback for local dev)

---

## Features

- Log work hours with date, start/end time, and break duration
- Dashboard with daily, weekly, and monthly summaries
- Target hours tracking with progress indicators
- Vacation requests and balance management
- Team calendar view
- Admin panel — manage employees, time logs, vacation types, and payroll export
- Dark / light mode
- Email-based password reset

---

## Project Structure

```
frontend/   # Vue 3 SPA
backend/    # .NET 9 REST API
```

---

## Getting Started

### Prerequisites

- Node.js v20+
- .NET 9 SDK
- MariaDB (optional — falls back to SQLite)

### Frontend

```sh
cd frontend
npm install
npm run dev
```

Runs at [http://localhost:5173](http://localhost:5173).

### Backend

1. Copy the environment template and fill in your values:

    ```sh
    cp backend/.env.template backend/.env
    ```

2. Apply database migrations:

    ```sh
    cd backend
    dotnet ef database update
    ```

3. Start the API:

    ```sh
    dotnet run
    ```

API runs at [https://localhost:7055](https://localhost:7055).

> See `backend/.env.template` for all available configuration options (JWT, database, SMTP, CORS).

---

## License

[MIT](LICENSE)
