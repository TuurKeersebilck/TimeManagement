<script setup lang="ts">
import { onMounted } from "vue";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import { useTimeCalculations } from "../composables/useTimeCalculations";
import { useTimeLogsStore } from "../composables/useTimeLogsStore";

const { timeLogs, loading, fetchTimeLogs } = useTimeLogsStore();

const { totalHoursToday, totalHoursThisWeek, totalHoursThisMonth } =
	useTimeCalculations(timeLogs);

onMounted(async () => {
	await fetchTimeLogs();
});
</script>

<template>
	<AuthenticatedLayout>
		<div class="page-background p-4 sm:p-6 lg:p-8">
			<!-- Header for desktop -->
			<div class="hidden lg:block mb-10">
				<div class="max-w-7xl mx-auto">
					<h1 class="text-4xl font-bold">Time Management Dashboard</h1>
				</div>
			</div>

			<div class="max-w-7xl mx-auto">
				<!-- Quick Stats Cards -->
				<div class="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
					<div class="card-gradient-blue p-6">
						<div>
							<p class="text-blue-100 text-sm font-medium mb-1">Today</p>
							<p class="text-4xl font-bold">
								<span v-if="loading" class="animate-pulse">--</span>
								<span v-else>{{ totalHoursToday }}h</span>
							</p>
						</div>
					</div>
					<div class="card-gradient-purple p-6">
						<div>
							<p class="text-purple-100 text-sm font-medium mb-1">This Week</p>
							<p class="text-4xl font-bold">
								<span v-if="loading" class="animate-pulse">--</span>
								<span v-else>{{ totalHoursThisWeek }}h</span>
							</p>
						</div>
					</div>
					<div class="card-gradient-green p-6">
						<div>
							<p class="text-emerald-100 text-sm font-medium mb-1">
								This Month
							</p>
							<p class="text-4xl font-bold">
								<span v-if="loading" class="animate-pulse">--</span>
								<span v-else>{{ totalHoursThisMonth }}h</span>
							</p>
						</div>
					</div>
				</div>
			</div>
		</div>
	</AuthenticatedLayout>
</template>
