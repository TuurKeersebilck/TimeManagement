---
name: verify
description: How to run and drive this app locally to verify a change end-to-end (API surface).
---

# Verifying changes in TimeManagement

## Build & launch

- Backend: `PATH=$PATH:~/.dotnet DOTNET_ROOT=~/.dotnet dotnet build` in `backend/`.
  - `dotnet-ef` (in `~/.dotnet/tools`) needs the SYSTEM dotnet root:
    `DOTNET_ROOT=/usr/local/share/dotnet PATH=$PATH:~/.dotnet/tools dotnet-ef ...`
- The user's own dev server usually holds ports 7055/5103. Run a second instance on a free port,
  bypassing the launch profile (which otherwise overrides ASPNETCORE_URLS):
  `ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5199 dotnet run --no-build --no-launch-profile`
- Postgres dev DB runs in Docker container `tm-dev-db` (postgres:16, db `timemanagement`):
  `docker exec tm-dev-db psql -U postgres -d timemanagement -c "..."` (no local psql installed).

## Auth handle

No known dev passwords. Mint an admin JWT with the dev secret from `backend/.env`
(HS256; claims must match `backend/Services/JwtService.cs`: full-URI `nameidentifier`,
`name`, `sub`, `email`, `jti`, full-URI `role` = "Admin"; iss/aud from JWT_ISSUER/JWT_AUDIENCE).
A ready-made minting script pattern: build header/payload/signature with node:crypto base64url.
Look up user ids in `AspNetUsers` (Role 1 = Admin, 0 = Employee).

## Driving the settlement/flex flows

- `GET/POST /api/settlements?year=&month=`, `/api/settlements/generate`, `/api/settlements/{id}/confirm`
- Adjustments: `GET/POST /api/admin/employees/{userId}/time-bank-adjustments`,
  `DELETE /api/admin/time-bank-adjustments/{id}`
- Payroll CSV: `GET /api/admin/export?year=&month=` (NOT /payroll-export)
- Overtime calc: `GET /api/admin/employees/{userId}/overtime?year=&month=`
- Balances are computed on the fly (worked − target + month's adjustments), so you can
  manufacture any net balance for a past month by POSTing a manual adjustment dated in it,
  then (re)generating settlements. Settlement rows are deletable via SQL for resets.

## Gotchas

- Frontend `npm run build` is plain `vite build` — no type-check step; rely on IDE diagnostics.
- Verification mutates the shared dev DB — say what you changed in the report.
