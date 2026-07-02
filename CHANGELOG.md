# Changelog

## [v0.5.0] - 2026-07-02

### New Features

- Multi-session time tracking — the entire clock engine has been rewritten around a WorkSession / BreakRecord model. A single day can now contain multiple sessions (e.g. split shifts), each with their own breaks. The old ClockEvent model is fully removed.
- Flex balance and overtime — every closed day is scored against your per-weekday target. The dashboard and time-tracking view show your daily delta, and a running monthly flex balance so you always know where you stand. Days with an open session are excluded from the balance until you clock out.
- Monthly settlement workflow — at the end of each month, admin generates a settlement for each employee. The settlement records total hours worked, overtime, deficit, and outcome (Paid / Leave Deducted / Unpaid). A blocker check prevents confirmation if any open sessions or pending adjustment requests remain. Settled months are locked and form the basis of the payroll CSV export.
- Manual settlement generation — admins can generate settlements on demand for any completed month via a "Generate settlements" button, instead of waiting for the automatic nightly job. Safe to click repeatedly since it only fills in employees who don't have a settlement for that month yet.
- Payroll CSV export — the admin Settlements screen now exports a properly structured CSV from the confirmed settlement data, including regular hours, overtime hours, total hours, outcome, and notes per employee.
- Break minimum enforcement — a configurable minimum break duration (per employee or global) is enforced on the End Break button. A live countdown in M:SS ticks down; once the minimum is met, the elapsed time is shown and the button unlocks. The server re-validates on submission.
- Break elapsed and countdown display — while on a break the interface shows how long the current break has been running, and if a minimum applies, counts down the remaining time before the button is enabled.
- Admin settlement screen — admins can review each employee's monthly overtime / deficit breakdown, override hours if needed, add notes, choose an outcome, and confirm. The detail dialog shows per-day flex data alongside blockers.
- Adjustment request prefill — when requesting a time adjustment, existing Closed sessions for the selected date are automatically loaded into the form. Supports multiple sessions and multiple breaks per session, with IDs carried through so the backend reconcile can match against existing records rather than treating everything as new.
- Background jobs — a nightly job auto-invalidates sessions that were never closed (missed clock-out), and a separate job detects missed clock-in days and sends reminder emails.
- Per-weekday targets — working hour targets are now set per day of the week (Mon–Fri individually) rather than a single daily/weekly number, allowing flexible schedules.

### Improvements

- Admin adjustment requests view — the "Requested Times" column now parses the snapshot JSON and renders sessions and breaks as `HH:MM–HH:MM [break HH:MM–HH:MM]` instead of showing empty fields.
- Dashboard loading — all data (summaries, schedule, vacation, holidays, overtime) now loads in parallel on mount; the loading state stays active until everything is ready, eliminating the flash where stat cards rendered before vacation and holiday context was available.
- `isClockedIn` priority on dashboard — the status banner now correctly shows "Clocked in" even on weekends, rather than showing "Weekend" when you are actively working.
- MaxSessionHours raised to 13 — the maximum allowed session length is increased from 10 to 13 hours to accommodate long shifts.
- Settlements page guidance — added an explanation of what a settlement is and how it affects payroll, plus tooltips on the Outcome, Overtime hours, Deficit hours, and Notes fields in the confirm dialog clarifying what each one actually does — e.g. deficit hours are record-only and excluded from the payroll CSV, and notes are never shown to the employee.

### Bug Fixes

- Settlement confirmation was blocked even when no real blockers existed — reconcile now hard-deletes sessions not present in the snapshot instead of marking them Invalidated, which previously left stale open sessions that triggered the blocker check
- Flex balance incorrectly dipped negative on days with an open session — days with any open session are now skipped entirely from the balance calculation until closed
- Weekly target in the admin employee summary was read from the legacy `EmployeeTarget.WeeklyHours` field instead of summing the per-weekday `WorkdayTarget` rows
- `GetTodayLiveAsync` elapsed time did not subtract ongoing break time — a break started but not yet ended was excluded from closed-break minutes but not from the raw elapsed calculation
- All status and outcome values (WorkSessionStatus, SettlementOutcome, SettlementStatus) are now consistently serialised as strings (`"Open"`, `"Closed"`, `"Invalidated"`, `"Paid"`, `"Settled"`, etc.) across every endpoint — the admin sessions endpoint was incorrectly casting to `int` (0/1/2), causing broken status badges and outcome selectors throughout the admin UI
- Break minimum enforcement defaulted to "reached" before the work schedule had finished loading, allowing the End Break button to be clicked immediately on page load
- Description and WFH toggle changes now correctly invalidate the shared summaries cache so other views don't serve stale data
- Settlements detail dialog "Already settled" banner did not render for confirmed settlements
- Adjustment request admin view referenced non-existent `requestedClockIn` / `requestedBreakStart` fields — replaced with correct snapshot parsing
- Every `/api/work-sessions/*` request 404'd for every user — the frontend called the kebab-case route while the controller actually resolves to `/api/worksessions`
- Rapidly double-clicking a confirm dialog's action button (e.g. approving an adjustment request) could fire it twice, with the second request hitting a backend guard like "already approved" — the dialog now disables itself while a confirmation is in flight
- Changelog entries rendered literal `**asterisks**` instead of bold text in the What's New dialog
- Payroll CSV numbers were formatted using the server's current culture — on a comma-decimal locale this produced values like `179,00`, corrupting the comma-delimited file by shifting every column after it

## [v0.4.2] - 2026-06-16

### Bug Fixes

- Calendar feed URL input now correctly displays the generated URL — it was always set correctly but rendered empty due to a prop binding mismatch on the Input component
- Help page — Calendar sync section now explains that regenerating the feed URL invalidates the old one immediately, that any subscribed calendar app will stop syncing until re-subscribed, and that the new URL is only visible in the browser where it was generated

### Improvements

- Search engine indexing is now blocked via `robots.txt`, an `X-Robots-Tag` HTTP header, and `<meta name="robots">` tags — three layers so no crawler can index the app

### Security

- Bumped esbuild to address a moderate-severity vulnerability in the development toolchain (no production impact)

## [v0.4.1] - 2026-06-12

### Bug Fixes

- Missed clock-in reminder emails are no longer sent on weekends or public holidays — previously, Friday's missed clock-in triggered duplicate emails on both Saturday and Sunday
- Employees on a full-day vacation no longer receive a missed clock-in reminder for that day — half-day vacations still trigger the reminder

## [v0.4.0] - 2026-06-02

### New Features

- Help page — a dedicated `/help` route in the sidebar covers how time tracking works, vacation requests, calendar sync behaviour, in-app notifications, and emails; admins also see a section on team management and settings

## [v0.3.0] - 2026-06-01

### New Features

- First-run setup wizard — on a fresh install, a setup screen guides the admin through creating the first account; env-based admin seeding is removed
- In-app changelog viewer — see what's new without leaving the app; a subtle indicator in the sidebar highlights unseen releases
- Admins can disable and re-enable employee accounts — disabled users are blocked from logging in and kicked out of active sessions immediately
- Admins can permanently delete a disabled employee — requires disabling first; deletes all associated data
- iCalendar feed — employees can subscribe to a personal `.ics` URL to sync their approved vacation days with external calendar apps

### Bug Fixes

- Auth cookie SameSite policy changed from `None` to `Lax` — fixes cookie rejection in same-site deployments
- Disabled accounts are now kicked out on session resume, not just on login

### Improvements

- Upgraded PostgreSQL from 16 to 18
- Switched email provider to Mailjet SMTP relay

## [v0.2.0] - 2026-05-11

### New Features

- Seed initial admin user on startup
- Docker production deployment available
- Holiday awareness added to dashboard, history, admin logs, and CSV
- Invite-based onboarding now supported
- Improved admin time logs with filter chips, week subtotals, vacation rows, and stat cards
- Store timezone per clock event and validate LocalDate server-side
- Notify employee when adjustment request is approved or rejected

### Bug Fixes

- Reduced GHCR owner text case for registry name requirements
- Improved SMTP timeout, error logging, and startup warnings
- Pre-filled email field on registration screen
- Excluded weekend holidays from CSV, adjusted weekly targets for holidays/vacations, added sidebar GitHub link, removed duplicate notifications
- Adjusted daily and weekly targets for holidays and vacations
- Removed extra space below weekly hours chart, clarified label

### Improvements

- Moved Holidays list directly under Country & Public Holidays in Admin Settings

## [v0.1.0] - 2026-04-27

### New Features

- Added clocked-in banner state to employee dashboard
- Fixed notification type and navigated to correct page on click
- Improved minimum break duration settings (global and per-user)
- Added clock-out indicator and admin email notification toggles
- Introduced per-holiday 'day off' toggle for company-specific working days
- Implemented vacation-aware clocking, updated dashboard target, and enhanced CSV export
- Showed full day breakdown (clock-in, break, clock-out) in history
- Added WFH toggle on clock-in step with success toast on history toggle
- Admin can now approve adjustment requests and set token expiry to 30 days
- Notified admins in-app when an adjustment request is submitted
- Updated History tab, added WFH flag, improved overlap validation, and fixed local date issues
- Replaced time logs with a clock-in/out system
- Enhanced app branding with SVG favicon, logo, and footer
- Admin can export payroll (CSV) per employee per month
- Cleaned up vacation popover and added multi-entry support
- Provided context-aware vacation popovers with inline edit options
- Added password visibility toggle on login and register
- Notified admins in-app when a new vacation plan is submitted
- Allowed profile editing on the account page
- Shared team calendar for all roles
- Set daily & weekly working hours targets per employee
- Enhanced password management, including forgot/reset flow and security page
- Configured public holidays with country settings, calendar markers, range skipping, and year overview
- Planned vacation date ranges, inline calendar actions, and dashboard status updates
- Polished global user experience with focus rings, cursor states, and interaction consistency
- Migrated from MariaDB to PostgreSQL (Supabase)
- Added vacation calendar widget to the dashboard
- Included calendar view in user vacation planner
- Removed PrimeVue, primeicons, and Noir preset in Phase 4
- Migrated remaining views to shadcn-vue in Phases 3 and 2
- Applied rate limiting on auth endpoints
- Upgraded project to .NET 10 SDK
- Provided admin calendar & employee summaries for vacation overview
- Allowed employee view & balance enforcement for vacation planning
- Configured vacation types with admin settings and per-employee balances
- Admin dashboard now includes daily snapshot and employee navigation
- Updated time log overview and added an employee list
- Redesigned time logging to include break start/end, description, and validation
- Established a sidebar, dark/light mode, and role-based navigation

### Bug Fixes

- Added cursor-pointer to interactive UI elements for better user experience
- Fixed NullReferenceException when updating vacation day type
- Improved notification toggles to load correctly on settings page refresh
- Normalized all timestamps to UTC DateTimeOffset for consistency
- Reopened sidebar when resizing back to desktop mode
- Allowed vacation planning on working-day holidays
- Corrected Reka UI Switch prop/event (modelValue not checked)
- Optimistic update toggle in holiday refresh now uses API types
- Improved vacation popover scroll by pinning the header and making the body scrollable
- Added notification bell to logo area and improved vacation popover overflow handling
- Placed toasts at bottom-right and enhanced employee table click affordance
- Redirected admin users on login and applied year overlay theme tokens
- Handled vacation year boundary and API error scenarios
- Converted DbUpdateException to ValidationException for duplicate clock events
- Sent full ISO 8601 DateTimeOffset for adjustment request times
- Skipped public holidays in vacation ranges and validated time orderings
- Improved history visibility, addressed stale cache issues, enhanced clock reactivity, and fixed UTC date bugs
- Resolved clock event timezone validation failures for non-UTC users
- Corrected inaccurate API endpoints and routes in READMEs
- Enhanced toast positioning and improved clock UI/UX; overhauled README documentation
- Displayed toggle button when sidebar is collapsed on desktop
- Persisted sidebar state across navigations for user convenience
- Removed double password reveal icon and updated clock icon in sidebar
- Hardened production environment with CSRF protection, 404 messages, remember me functionality, and description length constraints
- Disabled 401 interceptor redirect for auth endpoint calls
- Added Account link to admin sidebar navigation
- Corrected HH to hh format for TimeSpan in payroll CSV
- Moved vercel.json into frontend directory for correct SPA routing
- Added vercel.json rewrite rule to fix SPA routing on refresh
- Fixed missing migration attributes and corrected migration ordering
- Enabled SameSite=None in production for cross-origin cookie authentication
- Enhanced security with CORS, JWT secret, HTTPS metadata, and DB fallback mechanisms
- Added disabled styles to btn-primary for silent vacation create button
- Addressed data integrity issues including duplicate checks, timezone corrections, soft deletes, and transaction management
- Strengthened auth & session security through logout invalidation, 401 handling, and profile/password updates
- Further secured application with CORS, JWT secret, HTTPS metadata, and DB fallback

### Improvements

- Added support for updated NuGet packages
- Improved clock in/out system functionality
- Fixed formatting and UI bugs
- Consistent error responses and status codes across API
- Optimized indexes, fixed N+1 query issues, and improved server-side filtering
- Enhanced authentication security
