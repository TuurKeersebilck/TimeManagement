<script setup lang="ts">
import { ref, computed, watch } from "vue";
import { Button } from "@/components/ui/button";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { ChevronLeftIcon, ChevronRightIcon, XIcon } from "lucide-vue-next";
import type { VacationDay, TeamVacationDay } from "@/services/vacationService";
import { holidayService, type PublicHoliday } from "@/services/holidayService";

const props = defineProps<{
  open: boolean;
  vacationsByDate: Map<string, VacationDay[]>;
  teamVacationsByDate?: Map<string, TeamVacationDay[]>;
}>();

const emit = defineEmits<{
  "update:open": [value: boolean];
}>();

const overlayYear = ref(new Date().getFullYear());

// ─── Holidays ─────────────────────────────────────────────────────────────────

const holidayList = ref<PublicHoliday[]>([]);

async function fetchHolidays(year: number) {
  try {
    holidayList.value = await holidayService.getHolidays(year);
  } catch {
    holidayList.value = [];
  }
}

watch(
  [() => props.open, overlayYear],
  ([isOpen]) => {
    if (isOpen) fetchHolidays(overlayYear.value);
  },
  { immediate: true },
);

const holidaysByDate = computed(() => {
  const map = new Map<string, PublicHoliday>();
  for (const h of holidayList.value) map.set(h.date, h);
  return map;
});

// ─── Calendar grid ─────────────────────────────────────────────────────────────

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
  isWeekend: boolean;
}

interface MiniCalMonth {
  year: number;
  month: number;
  label: string;
  days: CalDay[];
}

function buildCalendarDays(year: number, month: number): CalDay[] {
  const firstDay = new Date(year, month, 1);
  const lastDay = new Date(year, month + 1, 0);
  const startDow = (firstDay.getDay() + 6) % 7;
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

const overlayMonths = computed<MiniCalMonth[]>(() => {
  const months: MiniCalMonth[] = [];
  for (let m = 0; m < 12; m++) {
    const label = new Date(overlayYear.value, m, 1).toLocaleDateString(undefined, { month: "long" });
    months.push({ year: overlayYear.value, month: m, label, days: buildCalendarDays(overlayYear.value, m) });
  }
  return months;
});

// ─── Vacation cell helpers ─────────────────────────────────────────────────────

function vacationCellStyle(iso: string): Record<string, string> {
  const entries = props.vacationsByDate.get(iso);
  if (!entries?.length) return {};
  const color = entries[0].vacationTypeColor ?? "#6366f1";
  const isHalf = entries[0].amount < 1;
  return isHalf
    ? { background: `linear-gradient(135deg, transparent 50%, ${color}20 50%)`, borderLeft: `2px solid ${color}` }
    : { backgroundColor: color + "28", borderLeft: `2px solid ${color}` };
}

function vacationCellTitle(iso: string): string | undefined {
  const entries = props.vacationsByDate.get(iso);
  if (!entries?.length) return undefined;
  return entries.map((e) => `${e.vacationTypeName}${e.amount < 1 ? " (½)" : ""}`).join(", ");
}

const vacationTypes = computed(() => {
  const seen = new Map<string, string>();
  for (const entries of props.vacationsByDate.values()) {
    for (const e of entries) {
      if (!seen.has(e.vacationTypeName)) {
        seen.set(e.vacationTypeName, e.vacationTypeColor ?? "#6366f1");
      }
    }
  }
  return [...seen.entries()].map(([name, color]) => ({ name, color }));
});
</script>

<template>
  <Teleport to="body">
    <Transition
      enter-active-class="transition-opacity duration-200"
      enter-from-class="opacity-0"
      leave-active-class="transition-opacity duration-200"
      leave-to-class="opacity-0"
    >
      <div
        v-if="open"
        class="fixed inset-0 z-50 bg-black/60 backdrop-blur-sm flex flex-col"
        @click.self="emit('update:open', false)"
      >
        <div class="flex flex-col flex-1 overflow-hidden bg-white dark:bg-slate-900 m-4 lg:m-8 rounded-2xl shadow-2xl">
          <!-- Header -->
          <div class="flex items-center justify-between px-6 py-4 border-b border-slate-200 dark:border-slate-800 shrink-0">
            <div class="flex items-center gap-3">
              <Button variant="ghost" size="icon" class="size-8" @click="overlayYear--">
                <ChevronLeftIcon class="size-4" />
              </Button>
              <h2 class="text-lg font-semibold text-slate-900 dark:text-slate-100">{{ overlayYear }}</h2>
              <Button variant="ghost" size="icon" class="size-8" @click="overlayYear++">
                <ChevronRightIcon class="size-4" />
              </Button>
            </div>
            <div class="flex items-center gap-4">
              <!-- Legend -->
              <div class="hidden sm:flex items-center gap-4 text-xs text-slate-500 dark:text-slate-400 flex-wrap">
                <span v-for="type in vacationTypes" :key="type.name" class="flex items-center gap-2">
                  <span class="flex items-center gap-1">
                    <span
                      class="w-3 h-3 rounded-sm inline-block"
                      :style="{ backgroundColor: type.color + '28', borderLeft: `2px solid ${type.color}` }"
                      title="Full day"
                    />
                    <span
                      class="w-3 h-3 rounded-sm inline-block"
                      :style="{ background: `linear-gradient(135deg, transparent 50%, ${type.color}20 50%)`, borderLeft: `2px solid ${type.color}` }"
                      title="Half day"
                    />
                  </span>
                  {{ type.name }}
                </span>
                <span v-if="holidaysByDate.size > 0" class="flex items-center gap-1.5">
                  <span class="w-3 h-3 rounded-sm bg-amber-100 dark:bg-amber-950/40 border-l-2 border-amber-400 inline-block" />
                  Holiday
                </span>
                <span class="flex items-center gap-1.5">
                  <span class="w-3 h-3 rounded-full bg-indigo-600 inline-block" />
                  Today
                </span>
              </div>
              <Button variant="ghost" size="icon" class="size-8" @click="emit('update:open', false)">
                <XIcon class="size-4" />
              </Button>
            </div>
          </div>

          <!-- 12-month grid -->
          <div class="flex-1 overflow-y-auto p-4 lg:p-6">
            <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
              <div v-for="miniMonth in overlayMonths" :key="miniMonth.month" class="card p-3">
                <p class="text-sm font-semibold text-slate-700 dark:text-slate-300 mb-2 capitalize">
                  {{ miniMonth.label }}
                </p>
                <div class="grid grid-cols-7 mb-1">
                  <div
                    v-for="wd in ['M', 'T', 'W', 'T', 'F', 'S', 'S']"
                    :key="wd"
                    class="text-center text-[10px] font-semibold text-slate-400 dark:text-slate-500"
                  >
                    {{ wd }}
                  </div>
                </div>
                <div class="grid grid-cols-7 gap-y-0.5">
                  <TooltipProvider
                    v-for="cell in miniMonth.days"
                    :key="cell.iso"
                    :delay-duration="50"
                  >
                    <Tooltip>
                      <TooltipTrigger as-child>
                        <div
                          :class="[
                            'relative text-[11px] h-6 flex items-center justify-center rounded cursor-default',
                            !cell.isCurrentMonth && 'opacity-20',
                            cell.isToday && 'bg-indigo-600 text-white font-bold',
                            !cell.isToday && holidaysByDate.has(cell.iso) && !vacationsByDate.has(cell.iso) && 'bg-amber-100 dark:bg-amber-950/40 text-amber-700 dark:text-amber-300',
                            !cell.isToday && !vacationsByDate.has(cell.iso) && !holidaysByDate.has(cell.iso) && cell.isWeekend && 'text-slate-400 dark:text-slate-600',
                            !cell.isToday && !vacationsByDate.has(cell.iso) && !holidaysByDate.has(cell.iso) && !cell.isWeekend && cell.isCurrentMonth && 'text-slate-700 dark:text-slate-300',
                            !cell.isToday && !vacationsByDate.has(cell.iso) && !holidaysByDate.has(cell.iso) && props.teamVacationsByDate?.has(cell.iso) && 'bg-slate-100 dark:bg-slate-800/60',
                          ]"
                          :style="!cell.isToday && vacationsByDate.has(cell.iso) ? vacationCellStyle(cell.iso) : undefined"
                        >
                          {{ cell.day }}
                          <!-- Team-vacation dot when others are off but I'm not -->
                          <span
                            v-if="!cell.isToday && !vacationsByDate.has(cell.iso) && props.teamVacationsByDate?.has(cell.iso)"
                            class="absolute bottom-0.5 right-0.5 w-1 h-1 rounded-full bg-slate-400 dark:bg-slate-500"
                          />
                        </div>
                      </TooltipTrigger>
                      <TooltipContent
                        v-if="vacationsByDate.has(cell.iso) || holidaysByDate.has(cell.iso) || props.teamVacationsByDate?.has(cell.iso)"
                        side="top"
                        class="text-xs max-w-44 space-y-1"
                      >
                        <p v-if="holidaysByDate.has(cell.iso)" class="flex items-center gap-1">
                          <span class="w-1.5 h-1.5 rounded-full bg-amber-400 shrink-0 inline-block" />
                          {{ holidaysByDate.get(cell.iso)!.name }}
                        </p>
                        <p v-if="vacationsByDate.has(cell.iso)">{{ vacationCellTitle(cell.iso) }}</p>
                        <template v-if="props.teamVacationsByDate?.has(cell.iso)">
                          <p
                            v-for="entry in props.teamVacationsByDate!.get(cell.iso)"
                            :key="entry.id"
                            class="flex items-center gap-1 opacity-80"
                          >
                            <span
                              class="w-1.5 h-1.5 rounded-full shrink-0 inline-block opacity-60"
                              :style="{ backgroundColor: entry.vacationTypeColor ?? '#94a3b8' }"
                            />
                            {{ entry.employeeName.split(" ")[0] }} · {{ entry.vacationTypeName }}<span v-if="entry.amount === 0.5"> ½</span>
                          </p>
                        </template>
                      </TooltipContent>
                    </Tooltip>
                  </TooltipProvider>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>
