import { createRouter, createWebHistory, RouteRecordRaw } from "vue-router";
import LoginView from "../views/LoginView.vue";
import RegisterView from "../views/RegisterView.vue";
import NotFoundView from "../views/NotFoundView.vue";
import { authService } from "../services/authService";

const routes: Array<RouteRecordRaw> = [
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
		path: "/",
		name: "dashboard",
		component: () => import("../views/DashboardView.vue"),
		meta: { requiresAuth: true },
	},
	{
		path: "/time-tracking",
		name: "time-tracking",
		component: () => import("../views/TimeTrackingView.vue"),
		meta: { requiresAuth: true },
	},
	{ path: "/:pathMatch(.*)*", name: "not-found", component: NotFoundView },
];

const router = createRouter({
	history: createWebHistory(import.meta.env.BASE_URL),
	routes,
});

// Navigation guard
router.beforeEach((to, from, next) => {
	const isAuthenticated = authService.isAuthenticated();

	if (to.meta.requiresAuth && !isAuthenticated) {
		next("/login");
	} else if (to.meta.guest && isAuthenticated) {
		next("/");
	} else {
		next();
	}
});

export default router;
