# TimeManagement Frontend

Vue 3 + Vite SPA for the Time Management application.

## Setup

```bash
npm install
npm run dev      # dev server at http://localhost:5173
npm run build    # production build
npm run preview  # preview production build
```

## Environment

| File | Used for |
|------|----------|
| `.env.development` | Local dev (points to `https://localhost:7055`) |
| `.env.production` | Production build (points to Railway backend via Vercel rewrite) |

`VITE_API_BASE_URL` is the only env variable — set it to your backend URL.

## Tech Stack

- **Vue 3** (Composition API, `<script setup>`)
- **Vite** — build tool
- **Vue Router** — client-side routing with navigation guards
- **Axios** — HTTP client with JWT interceptor
- **Tailwind CSS v4** — utility-first styling
- **shadcn-vue** — accessible UI components
- **vue-sonner** — toast notifications
- **Chart.js** — weekly hours chart
- **lucide-vue-next** — icons

## Structure

```
src/
├── views/              # Page components
│   ├── admin/          # Admin-only pages
│   ├── LoginView.vue
│   ├── DashboardView.vue
│   ├── TimeTrackingView.vue
│   ├── VacationsView.vue
│   └── ...
├── components/
│   ├── ui/             # shadcn-vue components
│   ├── Sidebar.vue
│   ├── AppConfirmDialog.vue
│   └── ...
├── composables/        # Shared Vue composition logic
│   ├── useAuth.ts
│   ├── useAppToast.ts
│   ├── useConfirmDialog.ts
│   └── ...
├── services/           # API modules (one per backend domain)
│   ├── api.ts          # Axios instance + JWT interceptor
│   ├── authService.ts
│   ├── clockEventService.ts
│   ├── vacationService.ts
│   └── ...
├── layouts/
│   └── AuthenticatedLayout.vue
├── router/
│   └── index.ts        # Routes + auth guards
├── utils/
│   └── apiError.ts     # Extract human-readable error from Axios errors
└── styles/
    └── theme.css       # Design system CSS utilities
```
