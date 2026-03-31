<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { vacationService, type VacationDay } from "@/services/vacationService";
import { Button } from "@/components/ui/button";
import { CalendarIcon, ChevronLeftIcon, ChevronRightIcon } from "lucide-vue-next";

const vacationDays = ref<VacationDay[]>([]);
const loading = ref(true);

onMounted(async () => {
  try {
    vacationDays.value = await vacationService.getVacationDays();
  } finally {
    loading.value = false;
  }
});

// ─── Calendar ─────────────────────────────────────────────────────────────────

const currentMonth = ref(new Date());

const monthLabel = computed(() =>
  currentMonth.value.toLocaleDateString(undefined, { month: "long", year: "numeric" })
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

const toIso = (d: Date) => {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
};

const todayIso = toIso(new Date());

interface CalDay {
  iso: string;
  day: number;
  isCurrentMonth: boolean;
  isToday: boolean;
}

const calendarDays = computed<CalDay[]>(() => {
  const year = currentMonth.value.getFullYear();
  const month = currentMonth.value.getMonth();
  const firstDay = new Date(year, month, 1);
  const lastDay = new Date(year, month + 1, 0);
  const startDow = (firstDay.getDay() + 6) % 7; // Monday = 0

  const days: CalDay[] = [];

  for (let i = startDow - 1; i >= 0; i--) {
    const d = new Date(year, month, -i);
    days.push({ iso: toIso(d), day: d.getDate(), isCurrentMonth: false, isToday: false });
  }
  for (let n = 1; n <= lastDay.getDate(); n++) {
    const d = new Date(year, month, n);
    const iso = toIso(d);
    days.push({ iso, day: n, isCurrentMonth: true, isToday: iso === todayIso });
  }
  const remaining = 42 - days.length;
  for (let n = 1; n <= remaining; n++) {
    const d = new Date(year, month + 1, n);
    days.push({ iso: toIso(d), day: d.getDate(), isCurrentMonth: false, isToday: false });
  }

  return days;
});

const vacationsByDate = computed(() => {
  const map = new Map<string, VacationDay[]>();
  for (const d of vacationDays.value) {
    if (!map.has(d.date)) map.set(d.date, []);
    map.get(d.date)!.push(d);
  }
  return map;
});

const WEEK_DAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

// ─── Upcoming list ─────────────────────────────────────────────────────────────

const upcomingDays = computed(() =>
  [...vacationDays.value]
    .filter((d) => d.date >= todayIso)
    .sort((a, b) => a.date.localeCompare(b.date))
    .slice(0, 5)
);

const displayDate = (iso: string) =>
  new Date(iso).toLocaleDateString(undefined, {
    weekday: "short",
    day: "numeric",
    month: "short",
  });
</script>

<template>
  <div class="card p-5">
    <div class="flex items-center justify-between mb-4">
      <h2 class="text-sm font-semibold text-slate-700 dark:text-slate-300">Vacation calendar</h2>
      <div class="flex items-center gap-1">
        <Button variant="ghost" size="icon" class="size-7" @click="prevMonth">
          <ChevronLeftIcon class="size-3.5" />
        </Button>
        <span
          class="text-xs font-medium text-slate-600 dark:text-slate-400 capitalize min-w-28 text-center"
        >
          {{ monthLabel }}
        </span>
        <Button variant="ghost" size="icon" class="size-7" @click="nextMonth">
          <ChevronRightIcon class="size-3.5" />
        </Button>
      </div>
    </div>

    <!-- Mini calendar -->
    <div v-if="loading" class="h-44 animate-pulse bg-slate-100 dark:bg-slate-800 rounded-lg mb-5" />
    <div v-else class="mb-5">
      <div class="grid grid-cols-7 mb-1">
        <div
          v-for="wd in WEEK_DAYS"
          :key="wd"
          class="text-center text-[10px] font-semibold text-slate-400 dark:text-slate-500 py-1"
        >
          {{ wd }}
        </div>
      </div>
      <div class="grid grid-cols-7 gap-y-0.5">
        <div
          v-for="cell in calendarDays"
          :key="cell.iso"
          class="flex flex-col items-center py-0.5 gap-0.5"
        >
          <div
            :class="[
              'text-[11px] font-medium w-6 h-6 flex items-center justify-center rounded-full',
              cell.isToday
                ? 'bg-indigo-600 text-white'
                : cell.isCurrentMonth
                  ? 'text-slate-700 dark:text-slate-200'
                  : 'text-slate-300 dark:text-slate-600',
            ]"
          >
            {{ cell.day }}
          </div>
          <!-- Vacation dots -->
          <div v-if="vacationsByDate.has(cell.iso)" class="flex gap-0.5 flex-wrap justify-center">
            <div
              v-for="entry in vacationsByDate.get(cell.iso)!.slice(0, 3)"
              :key="entry.id"
              class="w-1.5 h-1.5 rounded-full ring-1 ring-black/10"
              :style="{ backgroundColor: entry.vacationTypeColor ?? '#6366f1' }"
              :title="entry.vacationTypeName"
            />
          </div>
          <div v-else class="h-2" />
        </div>
      </div>
    </div>

    <!-- Upcoming list -->
    <div class="border-t border-slate-100 dark:border-slate-800 pt-4">
      <h3
        class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500 mb-3"
      >
        Upcoming
      </h3>

      <div v-if="loading" class="space-y-2.5">
        <div v-for="i in 3" :key="i" class="flex items-center gap-3">
          <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-20 animate-pulse" />
          <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded flex-1 animate-pulse" />
        </div>
      </div>

      <div
        v-else-if="upcomingDays.length === 0"
        class="flex flex-col items-center py-4 text-center"
      >
        <CalendarIcon class="size-5 text-slate-300 dark:text-slate-600 mb-1.5" />
        <p class="text-xs text-slate-400 dark:text-slate-500">No upcoming vacation days.</p>
      </div>

      <div v-else class="space-y-2">
        <div v-for="day in upcomingDays" :key="day.id" class="flex items-center gap-2.5">
          <div
            class="w-2 h-2 rounded-full shrink-0 ring-1 ring-black/10"
            :style="{ backgroundColor: day.vacationTypeColor ?? '#6366f1' }"
          />
          <span class="text-xs text-slate-500 dark:text-slate-400 w-24 shrink-0">
            {{ displayDate(day.date) }}
          </span>
          <span class="text-xs font-medium text-slate-700 dark:text-slate-300 flex-1 truncate">
            {{ day.vacationTypeName }}
          </span>
          <span
            :class="[
              'text-[10px] font-medium px-1.5 py-0.5 rounded shrink-0',
              day.amount === 1
                ? 'bg-indigo-50 dark:bg-indigo-950 text-indigo-700 dark:text-indigo-300'
                : 'bg-sky-50 dark:bg-sky-950 text-sky-700 dark:text-sky-300',
            ]"
            >{{ day.amount === 1 ? "Full" : "½" }}</span
          >
        </div>
      </div>
    </div>
  </div>
</template>
