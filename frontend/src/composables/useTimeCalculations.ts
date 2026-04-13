import { computed, type Ref } from "vue";
import type { DaySummary } from "../services/clockEventService";

export function useTimeCalculations(summaries: Ref<DaySummary[]>) {
  const totalHoursToday = computed(() => {
    const today = new Date();
    const todayStr = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}-${String(today.getDate()).padStart(2, "0")}`;

    return summaries.value
      .filter((s) => s.date.split("T")[0] === todayStr)
      .reduce((sum, s) => sum + (s.totalHours ?? 0), 0)
      .toFixed(2);
  });

  const totalHoursThisWeek = computed(() => {
    const today = new Date();
    const dayOfWeek = today.getDay();
    const daysFromMonday = dayOfWeek === 0 ? 6 : dayOfWeek - 1;
    const weekStart = new Date(today);
    weekStart.setDate(today.getDate() - daysFromMonday);
    weekStart.setHours(0, 0, 0, 0);

    return summaries.value
      .filter((s) => new Date(s.date) >= weekStart)
      .reduce((sum, s) => sum + (s.totalHours ?? 0), 0)
      .toFixed(2);
  });

  const totalHoursThisMonth = computed(() => {
    const today = new Date();
    const monthStart = new Date(today.getFullYear(), today.getMonth(), 1);
    monthStart.setHours(0, 0, 0, 0);

    return summaries.value
      .filter((s) => new Date(s.date) >= monthStart)
      .reduce((sum, s) => sum + (s.totalHours ?? 0), 0)
      .toFixed(2);
  });

  return { totalHoursToday, totalHoursThisWeek, totalHoursThisMonth };
}
