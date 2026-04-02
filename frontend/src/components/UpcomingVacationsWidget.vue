<script setup lang="ts">
import { ref, computed, watch, onMounted } from "vue";
import { vacationService, type VacationDay, type TeamVacationDay } from "@/services/vacationService";
import { holidayService, type PublicHoliday } from "@/services/holidayService";
import { useAuth } from "@/composables/useAuth";
import { Button } from "@/components/ui/button";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import YearCalendarOverlay from "@/components/YearCalendarOverlay.vue";
import { CalendarIcon, ChevronLeftIcon, ChevronRightIcon, Maximize2Icon } from "lucide-vue-next";

const { currentUser } = useAuth();

const vacationDays = ref<VacationDay[]>([]);
const teamVacationDays = ref<TeamVacationDay[]>([]);
const holidays = ref<PublicHoliday[]>([]);
const loading = ref(true);
const error = ref(false);
const yearOverlayOpen = ref(false);

const fetchedYears = new Set<number>();

async function fetchHolidaysForYear(year: number) {
  if (fetchedYears.has(year)) return;
  fetchedYears.add(year);
  try {
    const result = await holidayService.getHolidays(year);
    holidays.value = [...holidays.value.filter((h) => !h.date.startsWith(`${year}-`)), ...result];
  } catch {
    // holidays are non-critical, fail silently
  }
}

const fetchTeamDays = async () => {
  try {
    teamVacationDays.value = await vacationService.getTeamVacationDays({
      year: currentMonth.value.getFullYear(),
      month: currentMonth.value.getMonth() + 1,
    });
  } catch {
    // non-critical
  }
};

onMounted(async () => {
  try {
    const year = new Date().getFullYear();
    const month = new Date().getMonth() + 1;
    const [days, team] = await Promise.all([
      vacationService.getVacationDays(),
      vacationService.getTeamVacationDays({ year, month }).catch(() => [] as TeamVacationDay[]),
    ]);
    vacationDays.value = days;
    teamVacationDays.value = team;
  } catch {
    error.value = true;
  } finally {
    loading.value = false;
  }
  fetchHolidaysForYear(new Date().getFullYear());
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

watch(currentMonth, (d) => {
  fetchHolidaysForYear(d.getFullYear());
  fetchTeamDays();
});

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

const teamVacationsByDate = computed(() => {
  const map = new Map<string, TeamVacationDay[]>();
  for (const d of teamVacationDays.value) {
    if (d.userId === currentUser.value?.id) continue;
    if (!map.has(d.date)) map.set(d.date, []);
    map.get(d.date)!.push(d);
  }
  return map;
});

const holidaysByDate = computed(() => {
  const map = new Map<string, PublicHoliday>();
  for (const h of holidays.value) map.set(h.date, h);
  return map;
});

const WEEK_DAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

// ─── Upcoming list ─────────────────────────────────────────────────────────────

interface UpcomingItem {
  date: string;
  type: "vacation" | "holiday";
  label: string;
  color: string;
  amount?: number;
}

const upcomingItems = computed<UpcomingItem[]>(() => {
  const items: UpcomingItem[] = [];
  for (const d of vacationDays.value) {
    if (d.date >= todayIso)
      items.push({ date: d.date, type: "vacation", label: d.vacationTypeName, color: d.vacationTypeColor ?? "#6366f1", amount: d.amount });
  }
  for (const h of holidays.value) {
    if (h.date >= todayIso)
      items.push({ date: h.date, type: "holiday", label: h.name, color: "#f59e0b" });
  }
  return items.sort((a, b) => a.date.localeCompare(b.date)).slice(0, 5);
});

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
        <Button variant="ghost" size="icon" class="size-7" @click="yearOverlayOpen = true">
          <Maximize2Icon class="size-3.5" />
        </Button>
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
    <div v-else-if="error" class="h-44 flex items-center justify-center text-xs text-slate-400 dark:text-slate-500 mb-5">
      Could not load vacation data.
    </div>
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
        <TooltipProvider
          v-for="cell in calendarDays"
          :key="cell.iso"
          :delay-duration="50"
        >
          <Tooltip>
            <TooltipTrigger as-child>
              <div class="flex flex-col items-center py-0.5 gap-0.5 group">
                <!-- Day number with hover orb -->
                <div
                  :class="[
                    'text-[11px] font-medium w-6 h-6 flex items-center justify-center rounded-full transition-colors',
                    cell.isToday
                      ? 'bg-indigo-600 text-white'
                      : cell.isCurrentMonth
                        ? 'text-slate-700 dark:text-slate-200 group-hover:bg-slate-200 dark:group-hover:bg-slate-700'
                        : 'text-slate-300 dark:text-slate-600 group-hover:bg-slate-100 dark:group-hover:bg-slate-800',
                  ]"
                >
                  {{ cell.day }}
                </div>
                <!-- Dots: own vacations + team vacations + holiday -->
                <div
                  v-if="vacationsByDate.has(cell.iso) || teamVacationsByDate.has(cell.iso) || holidaysByDate.has(cell.iso)"
                  class="flex gap-0.5 flex-wrap justify-center"
                >
                  <div
                    v-for="entry in vacationsByDate.get(cell.iso)?.slice(0, 2) ?? []"
                    :key="entry.id"
                    class="w-1.5 h-1.5 rounded-full ring-1 ring-black/10"
                    :style="{ backgroundColor: entry.vacationTypeColor ?? '#6366f1' }"
                  />
                  <div
                    v-for="entry in teamVacationsByDate.get(cell.iso)?.slice(0, 2) ?? []"
                    :key="`t-${entry.id}`"
                    class="w-1.5 h-1.5 rounded-full ring-1 ring-black/10 opacity-50"
                    :style="{ backgroundColor: entry.vacationTypeColor ?? '#94a3b8' }"
                  />
                  <div
                    v-if="holidaysByDate.has(cell.iso)"
                    class="w-1.5 h-1.5 rounded-full ring-1 ring-black/10 bg-amber-400"
                  />
                </div>
                <div v-else class="h-2" />
              </div>
            </TooltipTrigger>

            <!-- Only render tooltip content if there's something to show -->
            <TooltipContent
              v-if="vacationsByDate.has(cell.iso) || teamVacationsByDate.has(cell.iso) || holidaysByDate.has(cell.iso)"
              side="bottom"
              class="max-w-48 p-2 space-y-1.5"
            >
              <!-- Holiday -->
              <div
                v-if="holidaysByDate.has(cell.iso)"
                class="flex items-center gap-1.5"
              >
                <div class="w-1.5 h-1.5 rounded-full bg-amber-400 shrink-0" />
                <span class="text-xs">{{ holidaysByDate.get(cell.iso)!.name }}</span>
              </div>
              <!-- Own vacations -->
              <div
                v-for="entry in vacationsByDate.get(cell.iso) ?? []"
                :key="entry.id"
                class="flex items-center gap-1.5"
              >
                <div
                  class="w-1.5 h-1.5 rounded-full shrink-0"
                  :style="{ backgroundColor: entry.vacationTypeColor ?? '#6366f1' }"
                />
                <span class="text-xs">{{ entry.vacationTypeName }}<span v-if="entry.amount === 0.5" class="opacity-60"> ½</span></span>
              </div>
              <!-- Team vacations -->
              <div
                v-for="entry in teamVacationsByDate.get(cell.iso) ?? []"
                :key="`t-${entry.id}`"
                class="flex items-center gap-1.5"
              >
                <div
                  class="w-1.5 h-1.5 rounded-full shrink-0 opacity-60"
                  :style="{ backgroundColor: entry.vacationTypeColor ?? '#94a3b8' }"
                />
                <span class="text-xs opacity-80">{{ entry.employeeName.split(" ")[0] }} · {{ entry.vacationTypeName }}<span v-if="entry.amount === 0.5" class="opacity-60"> ½</span></span>
              </div>
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
      </div>
    </div>

    <YearCalendarOverlay v-model:open="yearOverlayOpen" :vacations-by-date="vacationsByDate" :team-vacations-by-date="teamVacationsByDate" />

    <!-- Upcoming list -->
    <div v-if="!error" class="border-t border-slate-100 dark:border-slate-800 pt-4">
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
        v-else-if="upcomingItems.length === 0"
        class="flex flex-col items-center py-4 text-center"
      >
        <CalendarIcon class="size-5 text-slate-300 dark:text-slate-600 mb-1.5" />
        <p class="text-xs text-slate-400 dark:text-slate-500">Nothing upcoming.</p>
      </div>

      <div v-else class="space-y-2">
        <div v-for="item in upcomingItems" :key="`${item.type}-${item.date}-${item.label}`" class="flex items-center gap-2.5">
          <div
            class="w-2 h-2 rounded-full shrink-0 ring-1 ring-black/10"
            :style="{ backgroundColor: item.color }"
          />
          <span class="text-xs text-slate-500 dark:text-slate-400 w-24 shrink-0">
            {{ displayDate(item.date) }}
          </span>
          <span class="text-xs font-medium text-slate-700 dark:text-slate-300 flex-1 truncate">
            {{ item.label }}
          </span>
          <span
            v-if="item.type === 'vacation'"
            :class="[
              'text-[10px] font-medium px-1.5 py-0.5 rounded shrink-0',
              item.amount === 1
                ? 'bg-indigo-50 dark:bg-indigo-950 text-indigo-700 dark:text-indigo-300'
                : 'bg-sky-50 dark:bg-sky-950 text-sky-700 dark:text-sky-300',
            ]"
          >{{ item.amount === 1 ? "Full" : "½" }}</span>
          <span
            v-else
            class="text-[10px] font-medium px-1.5 py-0.5 rounded shrink-0 bg-amber-50 dark:bg-amber-950 text-amber-700 dark:text-amber-300"
          >Holiday</span>
        </div>
      </div>
    </div>
  </div>
</template>
