# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Full-stack time management app with a **Vue 3 + Vite** SPA frontend and a **.NET 9 ASP.NET Core** backend API, using **MariaDB** (via Pomelo EF Core) with SQLite as fallback.
- Temporary deployment: Railway (backend) + Vercel (frontend)

## Development Commands

### Frontend (`/frontend`)
```bash
npm install        # Install dependencies
npm run dev        # Dev server at http://localhost:5173
npm run build      # Production build
npm run preview    # Preview production build
```

### Backend (`/backend`)
```bash
dotnet run                       # Start API (https://localhost:7055)
dotnet build                     # Build
dotnet ef database update        # Apply EF Core migrations
dotnet ef migrations add <Name>  # Create a new migration
```

### Backend Environment Setup
Copy `.env.template` to `.env` and populate:
- `JWT_SECRET` — secret key for JWT signing
- `JWT_ISSUER`, `JWT_AUDIENCE`
- `DB_CONNECTION_STRING` — MariaDB connection string (falls back to SQLite if not set)

## Architecture

### Backend (`/backend`)

- **Program.cs** — wires everything: DbContext, Identity, JWT auth, CORS, roles, and loads `.env`
- **Controllers/** — thin HTTP layer; `AuthController` (register/login/profile), `TimeLogsController` (CRUD, all routes require JWT `[Authorize]`)
- **Services/** — business logic lives here; `TimeLogService` enforces user-isolation (users only see their own logs)
- **Data/AppDbContext.cs** — EF Core context with Identity integration and a `TimeSpan → varchar` converter for MySQL/MariaDB compatibility
- **Config/AutoMapperProfile.cs** — all entity↔DTO mappings in one place
- **Middleware/ExceptionHandlingMiddleware.cs** — global error handler; wraps all unhandled exceptions

### Frontend (`/frontend`)

- **src/services/api.ts** — Axios instance; JWT token is attached here (interceptor or header)
- **src/services/** — `authService.ts` and `timeLogService.ts` wrap all API calls; views should call services, not Axios directly
- **src/router/** — Vue Router; protected routes require auth
- **src/views/** — page-level components: `LoginView`, `RegisterView`, `DashboardView`, `TimeTrackingView`
- **src/composables/** — shared Vue composition logic
- UI built with **shadcn-vue** components and **Tailwind CSS** v4

### Auth Flow
1. Login/Register → backend returns JWT
2. Frontend stores JWT and attaches it to subsequent requests via `api.ts`
3. All `/api/timelogs` routes require `[Authorize]`; the service layer filters by `userId`

### Database Notes
- `TimeSpan` fields (`StartTime`, `EndTime`, `Break`) are stored as `varchar` — account for this in migrations and queries
- Migrations are in `/backend/Migrations/`; always run `dotnet ef database update` after pulling new migrations

## Working Conventions

### Critical Thinking & Pushback
- If a proposed approach has clear downsides — performance issues, security risks, architectural problems, or will cause pain later — say so directly before implementing. Don't just build what's asked if it's the wrong solution.
- If a request is ambiguous or there are multiple valid approaches with real tradeoffs, stop and ask rather than guessing. One focused question is better than a wrong implementation.
- If something seems like it could break existing functionality, conflict with the current architecture, or introduce technical debt, flag it explicitly.
- Disagreement is welcome and useful — the goal is the best outcome for the project, not just compliance.

### Branching
- Every bug fix or new feature MUST be implemented on a new branch created from `main`
- Branch naming: `fix/<short-description>` for bug fixes, `feat/<short-description>` for features
- Never commit implementation work directly to `main`
- Always verify PR and branch status (merged/open) before taking action on them
- When creating a new fix for an already-merged branch, always create a new branch from main
- Never push to already-merged branches

### Security & Secrets
- NEVER commit `.env` files, secrets, credentials, or connection strings — only `.env.template` (with placeholder values) is safe to commit
- Verify `.gitignore` covers `.env` before staging any backend files
- Secrets in code (JWT keys, DB passwords, API keys) are a hard blocker — flag and refuse to proceed until resolved
- When running the app locally, secrets are loaded from `.env`; never hardcode them as fallbacks in source

### Collaboration
- When facing a design decision or multiple valid approaches, always ask the user before proceeding — explain the options and tradeoffs concisely
- Prefer asking one focused question over multiple at once
- The goal is to build the best version of this project together, so user input on direction is always welcome

### Documentation (Context7)
- Always use Context7 when writing or modifying code that involves any third-party library or framework
- Before generating code for external dependencies (shadcn-vue, Tailwind v4, Pomelo EF Core, ASP.NET Core, Vue Router, Axios, etc.), use `resolve-library-id` then `get-library-docs` to fetch current documentation
- Never rely on training data alone for API signatures, component APIs, or configuration — these change across versions and hallucinated APIs are a hard blocker
- This project uses Tailwind CSS v4 which has significant breaking changes from v3 — always verify via Context7 before writing any Tailwind utilities or config

### shadcn MCP
- The **shadcn MCP** is available via the `mcp__shadcn__*` tools
- Use it when adding, browsing, or auditing shadcn-vue components — it can list available components, fetch examples, get the exact `npx shadcn-vue add` command, and run an audit checklist
- Prefer the shadcn MCP over guessing component names or install commands