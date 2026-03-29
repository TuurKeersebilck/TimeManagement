import { computed, type Ref } from "vue";
import type { TimeLog } from "../services/timeLogService";

export function useTimeCalculations(timeLogs: Ref<TimeLog[]>) {
	const totalHoursToday = computed(() => {
		const today = new Date();
		const todayStr = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}-${String(today.getDate()).padStart(2, "0")}`;

		return timeLogs.value
			.filter((log) => log.date.split("T")[0] === todayStr)
			.reduce((sum, log) => sum + (log.totalHours ?? 0), 0)
			.toFixed(2);
	});

	const totalHoursThisWeek = computed(() => {
		const today = new Date();
		// Week starts on Monday (getDay(): 0=Sun, 1=Mon … 6=Sat)
		const dayOfWeek = today.getDay();
		const daysFromMonday = dayOfWeek === 0 ? 6 : dayOfWeek - 1;
		const weekStart = new Date(today);
		weekStart.setDate(today.getDate() - daysFromMonday);
		weekStart.setHours(0, 0, 0, 0);

		return timeLogs.value
			.filter((log) => new Date(log.date) >= weekStart)
			.reduce((sum, log) => sum + (log.totalHours ?? 0), 0)
			.toFixed(2);
	});

	const totalHoursThisMonth = computed(() => {
		const today = new Date();
		const monthStart = new Date(today.getFullYear(), today.getMonth(), 1);
		monthStart.setHours(0, 0, 0, 0);

		return timeLogs.value
			.filter((log) => new Date(log.date) >= monthStart)
			.reduce((sum, log) => sum + (log.totalHours ?? 0), 0)
			.toFixed(2);
	});

	return { totalHoursToday, totalHoursThisWeek, totalHoursThisMonth };
}
