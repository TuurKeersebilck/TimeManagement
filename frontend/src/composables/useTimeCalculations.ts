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
    // Use string comparison to avoid UTC-vs-local timezone mismatch when parsing
    // date-only ISO strings ("yyyy-MM-dd") with new Date(), which treats them as UTC midnight.
    const weekStartStr = `${weekStart.getFullYear()}-${String(weekStart.getMonth() + 1).padStart(2, "0")}-${String(weekStart.getDate()).padStart(2, "0")}`;

    return summaries.value
      .filter((s) => s.date >= weekStartStr)
      .reduce((sum, s) => sum + (s.totalHours ?? 0), 0)
      .toFixed(2);
  });

  const totalHoursThisMonth = computed(() => {
    const today = new Date();
    // Use string comparison for the same UTC/local reason as above.
    const monthStartStr = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}-01`;

    return summaries.value
      .filter((s) => s.date >= monthStartStr)
      .reduce((sum, s) => sum + (s.totalHours ?? 0), 0)
      .toFixed(2);
  });

  return { totalHoursToday, totalHoursThisWeek, totalHoursThisMonth };
}
