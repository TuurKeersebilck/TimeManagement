import { createRouter, createWebHistory, RouteRecordRaw } from "vue-router";
import LoginView from "../views/LoginView.vue";
import RegisterView from "../views/RegisterView.vue";
import NotFoundView from "../views/NotFoundView.vue";
import AuthenticatedLayout from "../layouts/AuthenticatedLayout.vue";
import { authService } from "../services/authService";
import { setupService } from "../services/setupService";

const routes: Array<RouteRecordRaw> = [
  {
    path: "/setup",
    name: "setup",
    component: () => import("../views/SetupView.vue"),
    meta: { isSetup: true },
  },

  // Guest routes
  {
    path: "/login",
    name: "login",
    component: LoginView,
    meta: { guest: true },
  },
  {
    path: "/register",
    name: "register",
    component: RegisterView,
    meta: { guest: true },
  },
  {
    path: "/forgot-password",
    name: "forgot-password",
    component: () => import("../views/ForgotPasswordView.vue"),
    meta: { guest: true },
  },
  {
    path: "/reset-password",
    name: "reset-password",
    component: () => import("../views/ResetPasswordView.vue"),
    meta: { guest: true },
  },

  {
    path: "/time-tracking",
    redirect: "/",
  },
  {
    path: "/admin/vacations",
    redirect: "/team-calendar",
  },

  // Authenticated app shell — Sidebar/header mount once here and persist
  // across navigation instead of remounting on every route change.
  {
    path: "/",
    component: AuthenticatedLayout,
    meta: { requiresAuth: true },
    children: [
      // Shared (employee + admin) — admins are redirected to /admin/dashboard below
      {
        path: "",
        name: "time-tracking",
        component: () => import("../views/TimeTrackingView.vue"),
      },

      // Employee routes
      {
        path: "vacations",
        name: "vacations",
        component: () => import("../views/VacationsView.vue"),
      },
      {
        path: "team-calendar",
        name: "team-calendar",
        component: () => import("../views/TeamCalendarView.vue"),
      },
      {
        path: "account",
        name: "account",
        component: () => import("../views/AccountView.vue"),
      },
      {
        path: "help",
        name: "help",
        component: () => import("../views/HelpView.vue"),
      },

      // Admin-only routes
      {
        path: "admin",
        meta: { requiresAdmin: true },
        children: [
          {
            path: "dashboard",
            name: "admin-dashboard",
            component: () => import("../views/admin/AdminDashboardView.vue"),
          },
          {
            path: "time-logs",
            name: "admin-time-logs",
            component: () => import("../views/admin/TimeLogsView.vue"),
          },
          {
            path: "employees",
            name: "admin-employees",
            component: () => import("../views/admin/EmployeesView.vue"),
          },
          {
            path: "employees/:id",
            name: "admin-employee-detail",
            component: () => import("../views/admin/EmployeeDetailView.vue"),
          },
          {
            path: "vacation-types",
            name: "admin-vacation-types",
            component: () => import("../views/admin/VacationTypesView.vue"),
          },
          {
            path: "settings",
            name: "admin-settings",
            component: () => import("../views/admin/AppSettingsView.vue"),
          },
          {
            path: "export",
            name: "admin-export",
            component: () => import("../views/admin/ExportView.vue"),
          },
          {
            path: "adjustment-requests",
            name: "admin-adjustment-requests",
            component: () => import("../views/admin/AdjustmentRequestsView.vue"),
          },
          {
            path: "settlements",
            name: "admin-settlements",
            component: () => import("../views/admin/SettlementsView.vue"),
          },
        ],
      },
    ],
  },

  { path: "/:pathMatch(.*)*", name: "not-found", component: NotFoundView },
];

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
});

router.beforeEach(async (to, from, next) => {
  let setupRequired = false;
  try {
    setupRequired = await setupService.isSetupRequired();
  } catch {
    setupRequired = false;
  }

  if (setupRequired && to.name !== "setup") {
    next("/setup");
    return;
  }

  if (!setupRequired && to.name === "setup") {
    next("/login");
    return;
  }

  const isAuthenticated = authService.isAuthenticated();

  if (to.meta.requiresAuth && !isAuthenticated) {
    next("/login");
    return;
  }

  if (to.meta.guest && isAuthenticated) {
    next("/");
    return;
  }

  if (to.name === "time-tracking" && isAuthenticated && authService.getRoles().includes("Admin")) {
    next("/admin/dashboard");
    return;
  }

  if (to.meta.requiresAdmin && !authService.getRoles().includes("Admin")) {
    next("/");
    return;
  }

  next();
});

export default router;
