import { ref, computed } from "vue";

export interface CalDay {
  iso: string;
  day: number;
  isCurrentMonth: boolean;
  isToday: boolean;
  isWeekend: boolean;
}

export function toIso(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

export const todayIso = toIso(new Date());

export function buildCalendarDays(year: number, month: number): CalDay[] {
  const firstDay = new Date(year, month, 1);
  const lastDay = new Date(year, month + 1, 0);
  const startDow = (firstDay.getDay() + 6) % 7; // Monday = 0
  const days: CalDay[] = [];

  for (let i = startDow - 1; i >= 0; i--) {
    const d = new Date(year, month, -i);
    const dow = d.getDay();
    days.push({ iso: toIso(d), day: d.getDate(), isCurrentMonth: false, isToday: false, isWeekend: dow === 0 || dow === 6 });
  }
  for (let n = 1; n <= lastDay.getDate(); n++) {
    const d = new Date(year, month, n);
    const iso = toIso(d);
    const dow = d.getDay();
    days.push({ iso, day: n, isCurrentMonth: true, isToday: iso === todayIso, isWeekend: dow === 0 || dow === 6 });
  }
  const remaining = 42 - days.length;
  for (let n = 1; n <= remaining; n++) {
    const d = new Date(year, month + 1, n);
    const dow = d.getDay();
    days.push({ iso: toIso(d), day: d.getDate(), isCurrentMonth: false, isToday: false, isWeekend: dow === 0 || dow === 6 });
  }
  return days;
}

export const WEEK_DAYS = ["Mo", "Tu", "We", "Th", "Fr", "Sa", "Su"];

export function useCalendar() {
  const currentMonth = ref(new Date());

  const monthLabel = computed(() =>
    currentMonth.value.toLocaleDateString(undefined, { month: "long", year: "numeric" })
  );

  const calendarDays = computed<CalDay[]>(() =>
    buildCalendarDays(currentMonth.value.getFullYear(), currentMonth.value.getMonth())
  );

  const prevMonth = () => {
    const d = new Date(currentMonth.value);
    d.setMonth(d.getMonth() - 1);
    currentMonth.value = d;
  };

  const nextMonth = () => {
    const d = new Date(currentMonth.value);
    d.setMonth(d.getMonth() + 1);
    currentMonth.value = d;
  };

  const goToday = () => { currentMonth.value = new Date(); };

  const jumpToMonth = (year: number, month: number) => {
    currentMonth.value = new Date(year, month, 1);
  };

  return { currentMonth, monthLabel, calendarDays, prevMonth, nextMonth, goToday, jumpToMonth };
}
