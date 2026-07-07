<script setup lang="ts">
import { ref, computed, watch } from "vue";
import { Button } from "@/components/ui/button";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { ChevronLeftIcon, ChevronRightIcon, XIcon } from "lucide-vue-next";
import { vacationService, type TeamVacationDay } from "@/services/vacationService";
import { holidayService, type PublicHoliday } from "@/services/holidayService";
import { employeeColor, employeeColorWash } from "@/lib/employeeColors";

const props = defineProps<{
  open: boolean;
}>();

const emit = defineEmits<{
  "update:open": [value: boolean];
}>();

const overlayYear = ref(new Date().getFullYear());

// ─── Data ──────────────────────────────────────────────────────────────────────

const vacationDays = ref<TeamVacationDay[]>([]);
const holidayList = ref<PublicHoliday[]>([]);

async function fetchYear(year: number) {
  try {
    const [days, holidays] = await Promise.all([
      vacationService.getTeamVacationDays({ year }),
      holidayService.getHolidays(year),
    ]);
    vacationDays.value = days;
    holidayList.value = holidays;
  } catch {
    vacationDays.value = [];
    holidayList.value = [];
  }
}

watch(
  [() => props.open, overlayYear],
  ([isOpen]) => {
    if (isOpen) fetchYear(overlayYear.value);
  },
  { immediate: true },
);

const vacationsByDate = computed(() => {
  const map = new Map<string, TeamVacationDay[]>();
  for (const d of vacationDays.value) {
    if (!map.has(d.date)) map.set(d.date, []);
    map.get(d.date)!.push(d);
  }
  return map;
});

const holidaysByDate = computed(() => {
  const map = new Map<string, PublicHoliday>();
  for (const h of holidayList.value) map.set(h.date, h);
  return map;
});

const legendEmployees = computed(() => {
  const byUser = new Map<string, string>();
  for (const d of vacationDays.value) byUser.set(d.userId, d.employeeName);
  return [...byUser.entries()]
    .map(([userId, name]) => ({ userId, name }))
    .sort((a, b) => a.name.localeCompare(b.name));
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
  const entries = vacationsByDate.value.get(iso);
  if (!entries?.length) return {};
  const userId = entries[0].userId;
  return { backgroundColor: employeeColorWash(userId), borderLeft: `2px solid ${employeeColor(userId)}` };
}
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
        <div class="flex flex-col flex-1 overflow-hidden bg-card m-4 lg:m-8 rounded-2xl shadow-2xl">
          <!-- Header -->
          <div class="flex items-center justify-between px-6 py-4 border-b border-border shrink-0">
            <div class="flex items-center gap-3">
              <Button variant="ghost" size="icon" class="size-8" @click="overlayYear--">
                <ChevronLeftIcon class="size-4" />
              </Button>
              <h2 class="text-lg font-semibold text-card-foreground">{{ overlayYear }}</h2>
              <Button variant="ghost" size="icon" class="size-8" @click="overlayYear++">
                <ChevronRightIcon class="size-4" />
              </Button>
            </div>

            <!-- Employee legend -->
            <div
              v-if="legendEmployees.length"
              class="hidden md:flex items-center gap-x-3 gap-y-1.5 flex-wrap text-xs text-muted-foreground"
            >
              <span
                v-for="employee in legendEmployees"
                :key="employee.userId"
                class="flex items-center gap-1.5"
              >
                <span
                  class="w-2.5 h-2.5 rounded-full shrink-0 ring-1 ring-black/10"
                  :style="{ backgroundColor: employeeColor(employee.userId) }"
                />
                {{ employee.name }}
              </span>
            </div>

            <div class="flex items-center gap-4">
              <div class="hidden sm:flex items-center gap-4 text-xs text-muted-foreground flex-wrap">
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
                <p class="text-sm font-semibold text-card-foreground mb-2 capitalize">
                  {{ miniMonth.label }}
                </p>
                <div class="grid grid-cols-7 mb-1">
                  <div
                    v-for="wd in ['M', 'T', 'W', 'T', 'F', 'S', 'S']"
                    :key="wd"
                    class="text-center text-[10px] font-semibold text-muted-foreground"
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
                            cell.isToday && 'bg-primary text-primary-foreground font-bold',
                            !cell.isToday && holidaysByDate.has(cell.iso) && !vacationsByDate.has(cell.iso) && 'bg-amber-100 dark:bg-amber-950/40 text-amber-700 dark:text-amber-300',
                            !cell.isToday && !vacationsByDate.has(cell.iso) && !holidaysByDate.has(cell.iso) && 'text-card-foreground',
                          ]"
                          :style="!cell.isToday && vacationsByDate.has(cell.iso) ? vacationCellStyle(cell.iso) : undefined"
                        >
                          {{ cell.day }}
                        </div>
                      </TooltipTrigger>
                      <TooltipContent
                        v-if="vacationsByDate.has(cell.iso) || holidaysByDate.has(cell.iso)"
                        side="top"
                        class="text-xs max-w-48 space-y-1"
                      >
                        <p v-if="holidaysByDate.has(cell.iso)" class="flex items-center gap-1">
                          <span class="w-1.5 h-1.5 rounded-full bg-amber-400 shrink-0 inline-block" />
                          {{ holidaysByDate.get(cell.iso)!.name }}
                        </p>
                        <p
                          v-for="entry in vacationsByDate.get(cell.iso) ?? []"
                          :key="entry.id"
                          class="flex items-center gap-1"
                        >
                          <span
                            class="w-1.5 h-1.5 rounded-full shrink-0 inline-block"
                            :style="{ backgroundColor: employeeColor(entry.userId) }"
                          />
                          {{ entry.employeeName.split(" ")[0] }}<template v-if="entry.vacationTypeName"> · {{ entry.vacationTypeName }}</template><span v-if="entry.amount === 0.5"> ½</span>
                        </p>
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
