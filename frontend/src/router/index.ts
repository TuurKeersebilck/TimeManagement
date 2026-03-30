import { createRouter, createWebHistory, RouteRecordRaw } from "vue-router";
import LoginView from "../views/LoginView.vue";
import RegisterView from "../views/RegisterView.vue";
import NotFoundView from "../views/NotFoundView.vue";
import { authService } from "../services/authService";

const routes: Array<RouteRecordRaw> = [
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

	// Shared (employee + admin)
	{
		path: "/",
		name: "dashboard",
		component: () => import("../views/DashboardView.vue"),
		meta: { requiresAuth: true },
	},

	// Employee routes
	{
		path: "/time-tracking",
		name: "time-tracking",
		component: () => import("../views/TimeTrackingView.vue"),
		meta: { requiresAuth: true },
	},
	{
		path: "/vacations",
		name: "vacations",
		component: () => import("../views/VacationsView.vue"),
		meta: { requiresAuth: true },
	},

	// Admin-only routes
	{
		path: "/admin/time-logs",
		name: "admin-time-logs",
		component: () => import("../views/admin/TimeLogsView.vue"),
		meta: { requiresAuth: true, requiresAdmin: true },
	},
	{
		path: "/admin/vacations",
		name: "admin-vacations",
		component: () => import("../views/admin/VacationsView.vue"),
		meta: { requiresAuth: true, requiresAdmin: true },
	},
	{
		path: "/admin/employees",
		name: "admin-employees",
		component: () => import("../views/admin/EmployeesView.vue"),
		meta: { requiresAuth: true, requiresAdmin: true },
	},
	{
		path: "/admin/employees/:id",
		name: "admin-employee-detail",
		component: () => import("../views/admin/EmployeeDetailView.vue"),
		meta: { requiresAuth: true, requiresAdmin: true },
	},
	{
		path: "/admin/vacation-types",
		name: "admin-vacation-types",
		component: () => import("../views/admin/VacationTypesView.vue"),
		meta: { requiresAuth: true, requiresAdmin: true },
	},

	{ path: "/:pathMatch(.*)*", name: "not-found", component: NotFoundView },
];

const router = createRouter({
	history: createWebHistory(import.meta.env.BASE_URL),
	routes,
});

router.beforeEach((to, from, next) => {
	const isAuthenticated = authService.isAuthenticated();

	if (to.meta.requiresAuth && !isAuthenticated) {
		next("/login");
		return;
	}

	if (to.meta.guest && isAuthenticated) {
		next("/");
		return;
	}

	if (to.meta.requiresAdmin && !authService.getRoles().includes("Admin")) {
		next("/");
		return;
	}

	next();
});

export default router;
