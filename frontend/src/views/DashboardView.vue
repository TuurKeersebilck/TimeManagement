<script setup lang="ts">
import { onMounted } from "vue";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import { useTimeCalculations } from "../composables/useTimeCalculations";
import { useTimeLogsStore } from "../composables/useTimeLogsStore";
import UpcomingVacationsWidget from "@/components/UpcomingVacationsWidget.vue";

const { timeLogs, loading, fetchTimeLogs } = useTimeLogsStore();
const { totalHoursToday, totalHoursThisWeek, totalHoursThisMonth } =
	useTimeCalculations(timeLogs);

onMounted(async () => {
	await fetchTimeLogs();
});
</script>

<template>
	<AuthenticatedLayout>
		<div class="p-6 lg:p-8">
			<div class="max-w-6xl mx-auto">

				<div class="mb-8">
					<h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">Dashboard</h1>
					<p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">Your hours at a glance</p>
				</div>

				<div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
					<!-- Left: stats + (future widgets) -->
					<div class="lg:col-span-2 space-y-6">
						<div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
							<div class="stat-card">
								<p class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">Today</p>
								<p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
									<span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600">--</span>
									<span v-else>{{ totalHoursToday }}h</span>
								</p>
							</div>
							<div class="stat-card">
								<p class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">This week</p>
								<p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
									<span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600">--</span>
									<span v-else>{{ totalHoursThisWeek }}h</span>
								</p>
							</div>
							<div class="stat-card">
								<p class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">This month</p>
								<p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
									<span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600">--</span>
									<span v-else>{{ totalHoursThisMonth }}h</span>
								</p>
							</div>
						</div>
					</div>

					<!-- Right: vacation widget -->
					<div class="lg:col-span-1">
						<UpcomingVacationsWidget />
					</div>
				</div>

			</div>
		</div>
	</AuthenticatedLayout>
</template>
