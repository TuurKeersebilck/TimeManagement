import { createRouter, createWebHistory } from "vue-router";
import DashboardView from "../views/DashboardView.vue";
import TimeLogsView from "../views/TimeLogsView.vue";
import NotFoundView from "../views/NotFoundView.vue";

const router = createRouter({
	history: createWebHistory(import.meta.env.BASE_URL),
	routes: [
		{ path: "/", name: "dashboard", component: DashboardView },
		{ path: "/logs", name: "timelogs", component: TimeLogsView },
		{ path: "/:pathMatch(.*)*", name: "not-found", component: NotFoundView },
	],
});

export default router;
