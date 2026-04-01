<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import { vacationService, type TeamVacationDay } from "../services/vacationService";
import { holidayService, type PublicHoliday } from "../services/holidayService";
import { useAppToast } from "@/composables/useAppToast";
import { useCalendar, WEEK_DAYS } from "@/composables/useCalendar";
import YearOverlay, { type OverlayEntry } from "@/components/YearOverlay.vue";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Button } from "@/components/ui/button";
import {
  ChevronLeftIcon,
  ChevronRightIcon,
  Maximize2Icon,
  UsersIcon,
} from "lucide-vue-next";

const toast = useAppToast();

const teamDays = ref<TeamVacationDay[]>([]);
const holidays = ref<PublicHoliday[]>([]);
const loading = ref(false);

// ─── Calendar composable ─────────────────────────────────────────────────────

const { currentMonth, monthLabel, calendarDays, prevMonth, nextMonth, goToday, jumpToMonth } =
  useCalendar();

// ─── Employee colour palette ──────────────────────────────────────────────────

const PALETTE = [
  "#6366f1", // indigo
  "#0ea5e9", // sky
  "#10b981", // emerald
  "#f59e0b", // amber
  "#ec4899", // pink
  "#8b5cf6", // violet
  "#14b8a6", // teal
  "#f97316", // orange
  "#06b6d4", // cyan
  "#a855f7", // purple
];

const employeeColors = computed(() => {
  const map = new Map<string, string>();
  const seen: string[] = [];
  for (const d of teamDays.value) {
    if (!map.has(d.employeeId)) {
      map.set(d.employeeId, PALETTE[seen.length % PALETTE.length]);
      seen.push(d.employeeId);
    }
  }
  return map;
});

const employees = computed(() => {
  const map = new Map<string, string>(); // id → name
  for (const d of teamDays.value) {
    if (!map.has(d.employeeId)) map.set(d.employeeId, d.employeeName);
  }
  return Array.from(map.entries()).map(([id, name]) => ({
    id,
    name,
    color: employeeColors.value.get(id) ?? "#6366f1",
  }));
});

// ─── Holidays ─────────────────────────────────────────────────────────────────

const holidaysByDate = computed(() => {
  const map = new Map<string, PublicHoliday>();
  for (const h of holidays.value) map.set(h.date, h);
  return map;
});

const holidayNamesByDate = computed(() => {
  const map = new Map<string, string>();
  for (const h of holidays.value) map.set(h.date, h.name);
  return map;
});

// ─── Team vacations by date ───────────────────────────────────────────────────

const teamByDate = computed(() => {
  const map = new Map<string, TeamVacationDay[]>();
  for (const d of teamDays.value) {
    if (!map.has(d.date)) map.set(d.date, []);
    map.get(d.date)!.push(d);
  }
  return map;
});

const MAX_VISIBLE = 3;

// ─── Year overlay ─────────────────────────────────────────────────────────────

const yearOverlayOpen = ref(false);

const overlayVacationsByDate = computed<Map<string, OverlayEntry[]>>(() => {
  const map = new Map<string, OverlayEntry[]>();
  for (const d of teamDays.value) {
    if (!map.has(d.date)) map.set(d.date, []);
    map.get(d.date)!.push({
      color: employeeColors.value.get(d.employeeId) ?? "#6366f1",
      label: d.employeeName,
    });
  }
  return map;
});

// ─── Popover ─────────────────────────────────────────────────────────────────

const openPopoverIso = ref<string | null>(null);

const openPopover = (iso: string) => { openPopoverIso.value = iso; };
const closePopover = () => { openPopoverIso.value = null; };

const popoverDateLabel = computed(() => {
  if (!openPopoverIso.value) return "";
  return new Date(openPopoverIso.value + "T00:00:00").toLocaleDateString(undefined, {
    weekday: "long",
    day: "numeric",
    month: "long",
  });
});

// ─── Mount ────────────────────────────────────────────────────────────────────

onMounted(async () => {
  loading.value = true;
  try {
    const year = new Date().getFullYear();
    const [days, h] = await Promise.all([
      vacationService.getTeamVacationDays(),
      holidayService.getHolidays(year).catch(() => [] as PublicHoliday[]),
    ]);
    teamDays.value = days;
    holidays.value = h;
  } catch {
    toast.error("Failed to load team calendar");
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <AuthenticatedLayout>
    <div class="p-6 lg:p-8">
      <div class="max-w-5xl mx-auto">
        <!-- Header -->
        <div class="mb-8">
          <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">Team Calendar</h1>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
            See who's off to plan your vacation without overlapping
          </p>
        </div>

        <!-- Employee legend -->
        <section class="mb-6">
          <div v-if="loading" class="flex flex-wrap gap-2">
            <div v-for="i in 4" :key="i" class="h-6 w-24 bg-slate-200 dark:bg-slate-700 rounded-full animate-pulse" />
          </div>
          <div v-else-if="employees.length === 0" class="text-sm text-slate-400 dark:text-slate-500">
            No vacation days planned by anyone yet.
          </div>
          <div v-else class="flex flex-wrap gap-2">
            <span
              v-for="emp in employees"
              :key="emp.id"
              class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium text-white"
              :style="{ backgroundColor: emp.color }"
            >
              {{ emp.name }}
            </span>
          </div>
        </section>

        <!-- Calendar -->
        <section>
          <!-- Month navigation -->
          <div class="flex items-center justify-between mb-3">
            <div class="flex items-center gap-1">
              <Button variant="ghost" size="icon" class="size-8" @click="prevMonth">
                <ChevronLeftIcon class="size-4" />
              </Button>
              <Button variant="ghost" size="icon" class="size-8" @click="nextMonth">
                <ChevronRightIcon class="size-4" />
              </Button>
              <span class="text-base font-semibold text-slate-900 dark:text-slate-100 ml-2 capitalize">
                {{ monthLabel }}
              </span>
            </div>
            <div class="flex items-center gap-2">
              <Button variant="outline" size="sm" @click="goToday">Today</Button>
              <Button variant="outline" size="sm" @click="yearOverlayOpen = true">
                <Maximize2Icon class="size-3.5" />
                Year view
              </Button>
            </div>
          </div>

          <div v-if="loading" class="card h-96 animate-pulse bg-slate-100 dark:bg-slate-800" />

          <div v-else class="card overflow-hidden">
            <!-- Weekday headers -->
            <div class="grid grid-cols-7 border-b border-slate-200 dark:border-slate-800">
              <div
                v-for="wd in WEEK_DAYS"
                :key="wd"
                class="text-center text-xs font-semibold text-slate-400 dark:text-slate-500 py-2"
              >
                {{ wd }}
              </div>
            </div>

            <!-- Day cells -->
            <div class="grid grid-cols-7">
              <Popover
                v-for="cell in calendarDays"
                :key="cell.iso"
                :open="openPopoverIso === cell.iso"
                @update:open="
                  (v) => {
                    const hasContent =
                      teamByDate.has(cell.iso) || holidaysByDate.has(cell.iso);
                    if (v && cell.isCurrentMonth && hasContent) openPopover(cell.iso);
                    else if (!v) closePopover();
                  }
                "
              >
                <PopoverTrigger as-child>
                  <div
                    :class="[
                      'border-b border-r border-slate-100 dark:border-slate-800/60 min-h-20 p-1.5 transition-colors select-none',
                      !cell.isCurrentMonth && 'opacity-40',
                      cell.isToday && 'bg-indigo-50/60 dark:bg-indigo-950/20',
                      cell.isWeekend && 'bg-slate-50/80 dark:bg-slate-900/60',
                      openPopoverIso === cell.iso && 'ring-2 ring-inset ring-indigo-400 dark:ring-indigo-500',
                      cell.isCurrentMonth && (teamByDate.has(cell.iso) || holidaysByDate.has(cell.iso))
                        ? 'cursor-pointer hover:bg-slate-50 dark:hover:bg-slate-800/40'
                        : 'cursor-default pointer-events-none',
                    ]"
                  >
                    <!-- Day number -->
                    <div
                      :class="[
                        'text-xs font-medium w-6 h-6 flex items-center justify-center rounded-full mb-1',
                        cell.isToday
                          ? 'bg-indigo-600 text-white'
                          : cell.isWeekend
                            ? 'text-slate-400 dark:text-slate-600'
                            : cell.isCurrentMonth
                              ? 'text-slate-700 dark:text-slate-200'
                              : 'text-slate-300 dark:text-slate-600',
                      ]"
                    >
                      {{ cell.day }}
                    </div>

                    <!-- Holiday marker -->
                    <div
                      v-if="holidaysByDate.has(cell.iso)"
                      class="text-[10px] leading-tight truncate rounded px-1 py-0.5 mb-0.5 bg-amber-50 dark:bg-amber-950/40 text-amber-700 dark:text-amber-300 border-l-2 border-amber-400"
                      :title="holidaysByDate.get(cell.iso)!.name"
                    >
                      {{ holidaysByDate.get(cell.iso)!.name }}
                    </div>

                    <!-- Employee chips -->
                    <template v-if="teamByDate.has(cell.iso)">
                      <div
                        v-for="entry in teamByDate.get(cell.iso)!.slice(0, MAX_VISIBLE)"
                        :key="entry.employeeId + entry.vacationTypeName"
                        :style="{
                          backgroundColor: (employeeColors.get(entry.employeeId) ?? '#6366f1') + '28',
                          borderLeftColor: employeeColors.get(entry.employeeId) ?? '#6366f1',
                        }"
                        class="text-[10px] leading-tight truncate rounded px-1 py-0.5 mb-0.5 border-l-2 text-slate-700 dark:text-slate-200"
                      >
                        {{ entry.employeeName
                        }}<span v-if="entry.amount === 0.5" class="opacity-50"> ½</span>
                      </div>
                      <div
                        v-if="teamByDate.get(cell.iso)!.length > MAX_VISIBLE"
                        class="text-[10px] text-slate-400 dark:text-slate-500 pl-1"
                      >
                        +{{ teamByDate.get(cell.iso)!.length - MAX_VISIBLE }} more
                      </div>
                    </template>
                  </div>
                </PopoverTrigger>

                <PopoverContent class="w-72 p-0 shadow-lg" side="bottom" :collision-padding="12">
                  <!-- Popover header -->
                  <div
                    class="flex items-center justify-between px-4 py-3 border-b border-slate-100 dark:border-slate-800"
                  >
                    <span class="text-sm font-semibold text-slate-900 dark:text-slate-100 capitalize">
                      {{ popoverDateLabel }}
                    </span>
                    <button
                      class="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 transition-colors"
                      @click="closePopover"
                    >
                      <svg class="size-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M18 6 6 18M6 6l12 12" />
                      </svg>
                    </button>
                  </div>

                  <!-- Holiday notice -->
                  <div
                    v-if="holidaysByDate.has(cell.iso)"
                    class="flex items-center gap-2 px-4 py-2 bg-amber-50 dark:bg-amber-950/30 border-b border-amber-100 dark:border-amber-900"
                  >
                    <span class="text-xs text-amber-700 dark:text-amber-300 font-medium">
                      🎉 {{ holidaysByDate.get(cell.iso)!.name }}
                    </span>
                  </div>

                  <!-- Who's off -->
                  <div
                    v-if="teamByDate.has(cell.iso)"
                    class="divide-y divide-slate-100 dark:divide-slate-800"
                  >
                    <div
                      v-for="entry in teamByDate.get(cell.iso)"
                      :key="entry.employeeId + entry.vacationTypeName"
                      class="flex items-center gap-2.5 px-4 py-2.5"
                    >
                      <div
                        class="w-2 h-2 rounded-full shrink-0 ring-1 ring-black/10"
                        :style="{ backgroundColor: employeeColors.get(entry.employeeId) ?? '#6366f1' }"
                      />
                      <div class="flex-1 min-w-0">
                        <p class="text-sm font-medium text-slate-900 dark:text-slate-100 truncate">
                          {{ entry.employeeName }}
                        </p>
                        <p class="text-xs text-slate-400 dark:text-slate-500 truncate">
                          {{ entry.vacationTypeName }}
                        </p>
                      </div>
                      <span
                        :class="[
                          'text-[10px] font-medium px-1.5 py-0.5 rounded shrink-0',
                          entry.amount === 1
                            ? 'bg-indigo-50 dark:bg-indigo-950 text-indigo-700 dark:text-indigo-300'
                            : 'bg-sky-50 dark:bg-sky-950 text-sky-700 dark:text-sky-300',
                        ]"
                      >
                        {{ entry.amount === 1 ? "Full" : "½" }}
                      </span>
                    </div>
                  </div>

                  <!-- Empty state inside popover (holiday only) -->
                  <div
                    v-else
                    class="px-4 py-3 flex items-center gap-2 text-xs text-slate-400 dark:text-slate-500"
                  >
                    <UsersIcon class="size-3.5 shrink-0" />
                    No one off this day
                  </div>
                </PopoverContent>
              </Popover>
            </div>
          </div>
        </section>
      </div>
    </div>

    <!-- Year overlay -->
    <YearOverlay
      v-model:open="yearOverlayOpen"
      :vacations-by-date="overlayVacationsByDate"
      :holidays-by-date="holidayNamesByDate"
      @month-click="jumpToMonth"
    />
  </AuthenticatedLayout>
</template>
