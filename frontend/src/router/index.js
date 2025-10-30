import { createRouter, createWebHistory } from "vue-router";
import LoginView from "../views/LoginView.vue";
import RegisterView from "../views/RegisterView.vue";
import NotFoundView from "../views/NotFoundView.vue";

const router = createRouter({
	history: createWebHistory(import.meta.env.BASE_URL),
	routes: [
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
	],
});

// Navigation guard
router.beforeEach((to, from, next) => {
	const token = localStorage.getItem("token");

	if (to.meta.requiresAuth && !token) {
		next("/login");
	} else if (to.meta.guest && token) {
		next("/");
	} else {
		next();
	}
});

export default router;
