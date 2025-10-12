# Time Management App

A full-stack time management application featuring:

- **Frontend:** Vue 3 + Vite + PrimeVue UI
- **Backend:** .NET 9 MVC API
- **Database:** MariaDB

---

## Features

- Log work hours with date, start/end time, and break
- Responsive UI with PrimeVue components
- RESTful API for CRUD operations
- Secure CORS setup for local development

---

## Project Structure

```
frontend/   # Vue 3 + PrimeVue SPA
backend/    # .NET 9 MVC API
```

---

## Getting Started

### Prerequisites

- Node.js (v20+)
- .NET 9 SDK
- MariaDB server

---

### Frontend Setup

```sh
cd frontend
npm install
npm run dev
```

Access at [http://localhost:5173](http://localhost:5173).

---

### Backend Setup

1. Update `backend/appsettings.json` with your MariaDB connection string:

    ```json
    "ConnectionStrings": {
      "DefaultConnection": "Server=localhost;Database=timemanagement;User=root;Password=yourpassword;"
    }
    ```

2. Run database migrations:

    ```sh
    cd backend
    dotnet ef database update
    ```

3. Start the API server:

    ```sh
    dotnet run
    ```

API runs at [https://localhost:7055](https://localhost:7055).

---

## Usage

- Open the frontend in your browser.
- Add, view, and manage time logs.
- Data is stored in MariaDB via the .NET backend.

---

## Tech Stack

- **Vue 3** ([src/main.js](frontend/src/main.js))
- **PrimeVue** ([primevue](https://www.primefaces.org/primevue/))
- **.NET 9 MVC** ([Program.cs](backend/Program.cs))
- **MariaDB**

---

## License

MIT
