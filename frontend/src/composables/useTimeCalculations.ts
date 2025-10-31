import { computed, type Ref } from "vue";
import type { TimeLog } from "../services/timeLogService";

export function useTimeCalculations(timeLogs: Ref<TimeLog[]>) {
	// Calculate total hours for today only
	const totalHoursToday = computed(() => {
		const today = new Date();
		const year = today.getFullYear();
		const month = String(today.getMonth() + 1).padStart(2, "0");
		const day = String(today.getDate()).padStart(2, "0");
		const todayStr = `${year}-${month}-${day}`;

		return timeLogs.value
			.filter((log) => {
				// Extract just the date part from the log (in case it has time component)
				const logDateStr = log.date.split("T")[0];
				return logDateStr === todayStr;
			})
			.reduce((sum, log) => sum + (log.totalHours ?? 0), 0)
			.toFixed(2);
	});

	// Calculate total hours for this week
	const totalHoursThisWeek = computed(() => {
		const today = new Date();
		const weekStart = new Date(today);
		weekStart.setDate(today.getDate() - today.getDay());
		weekStart.setHours(0, 0, 0, 0);

		return timeLogs.value
			.filter((log) => new Date(log.date) >= weekStart)
			.reduce((sum, log) => sum + (log.totalHours ?? 0), 0)
			.toFixed(2);
	});

	// Calculate total hours for this month
	const totalHoursThisMonth = computed(() => {
		const today = new Date();
		const monthStart = new Date(today.getFullYear(), today.getMonth(), 1);
		monthStart.setHours(0, 0, 0, 0);

		return timeLogs.value
			.filter((log) => new Date(log.date) >= monthStart)
			.reduce((sum, log) => sum + (log.totalHours ?? 0), 0)
			.toFixed(2);
	});

	return {
		totalHoursToday,
		totalHoursThisWeek,
		totalHoursThisMonth,
	};
}
