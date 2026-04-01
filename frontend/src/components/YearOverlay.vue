<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted } from "vue";
import { buildCalendarDays } from "@/composables/useCalendar";
import { Button } from "@/components/ui/button";
import { ChevronLeftIcon, ChevronRightIcon, XIcon } from "lucide-vue-next";

export interface OverlayEntry {
  color: string;
  label: string;
}

const props = defineProps<{
  open: boolean;
  vacationsByDate: Map<string, OverlayEntry[]>;
  holidaysByDate: Map<string, string>; // iso → name
  initialYear?: number;
}>();

const emit = defineEmits<{
  "update:open": [value: boolean];
  "month-click": [year: number, month: number];
  "year-change": [year: number];
}>();

const close = () => emit("update:open", false);

const overlayYear = ref(props.initialYear ?? new Date().getFullYear());

watch(overlayYear, (year) => emit("year-change", year));

// Reset to current year (or initialYear) whenever the overlay opens
watch(
  () => props.open,
  (isOpen) => {
    if (isOpen) overlayYear.value = props.initialYear ?? new Date().getFullYear();
  }
);

const todayIso = (() => {
  const d = new Date();
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
})();

const currentMonth = new Date().getMonth();
const currentYear = new Date().getFullYear();

interface MiniMonth {
  year: number;
  month: number;
  label: string;
  days: ReturnType<typeof buildCalendarDays>;
}

const overlayMonths = computed<MiniMonth[]>(() => {
  const months: MiniMonth[] = [];
  for (let m = 0; m < 12; m++) {
    months.push({
      year: overlayYear.value,
      month: m,
      label: new Date(overlayYear.value, m, 1).toLocaleDateString(undefined, { month: "long" }),
      days: buildCalendarDays(overlayYear.value, m),
    });
  }
  return months;
});

const isCurrentDisplayMonth = (m: number) =>
  overlayYear.value === currentYear && m === currentMonth;

const onKeydown = (e: KeyboardEvent) => {
  if (e.key === "Escape" && props.open) close();
};

onMounted(() => document.addEventListener("keydown", onKeydown));
onUnmounted(() => document.removeEventListener("keydown", onKeydown));

const onMonthClick = (year: number, month: number) => {
  emit("month-click", year, month);
  close();
};
</script>

<template>
  <Teleport to="body">
    <Transition
      enter-active-class="transition-opacity duration-200 ease-out"
      leave-active-class="transition-opacity duration-150 ease-in"
      enter-from-class="opacity-0"
      enter-to-class="opacity-100"
      leave-from-class="opacity-100"
      leave-to-class="opacity-0"
    >
      <div
        v-if="open"
        class="fixed inset-0 z-50 bg-black/60 backdrop-blur-sm flex flex-col"
        @click.self="close"
        role="dialog"
        aria-modal="true"
        aria-label="Year overview"
      >
        <div class="flex flex-col flex-1 overflow-hidden bg-white dark:bg-slate-900 m-4 lg:m-8 rounded-2xl shadow-2xl">
          <!-- Header -->
          <div class="flex items-center justify-between px-6 py-4 border-b border-slate-200 dark:border-slate-800 shrink-0">
            <div class="flex items-center gap-3">
              <Button variant="ghost" size="icon" class="size-8" @click="overlayYear--">
                <ChevronLeftIcon class="size-4" />
              </Button>
              <h2 class="text-lg font-semibold text-slate-900 dark:text-slate-100 w-12 text-center">
                {{ overlayYear }}
              </h2>
              <Button variant="ghost" size="icon" class="size-8" @click="overlayYear++">
                <ChevronRightIcon class="size-4" />
              </Button>
            </div>
            <div class="flex items-center gap-4">
              <!-- Legend -->
              <div class="flex items-center gap-3 text-xs text-slate-500 dark:text-slate-400">
                <span class="flex items-center gap-1.5">
                  <span class="w-3 h-3 rounded-sm bg-indigo-100 dark:bg-indigo-950 border-l-2 border-indigo-400 inline-block" />
                  Vacation
                </span>
                <span class="flex items-center gap-1.5">
                  <span class="w-3 h-3 rounded-sm bg-amber-100 dark:bg-amber-950/40 border-l-2 border-amber-400 inline-block" />
                  Holiday
                </span>
                <span class="flex items-center gap-1.5">
                  <span class="w-3 h-3 rounded-full bg-indigo-600 inline-block" />
                  Today
                </span>
              </div>
              <Button variant="ghost" size="icon" class="size-8" @click="close">
                <XIcon class="size-4" />
              </Button>
            </div>
          </div>

          <!-- 12-month grid -->
          <div class="flex-1 overflow-y-auto p-4 lg:p-6">
            <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
              <button
                v-for="miniMonth in overlayMonths"
                :key="miniMonth.month"
                :class="[
                  'card p-3 text-left transition-colors hover:bg-slate-50 dark:hover:bg-slate-800/60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400',
                  isCurrentDisplayMonth(miniMonth.month) && 'ring-2 ring-indigo-300 dark:ring-indigo-600',
                ]"
                :title="`Jump to ${miniMonth.label} ${miniMonth.year}`"
                @click="onMonthClick(miniMonth.year, miniMonth.month)"
              >
                <p class="text-sm font-semibold text-slate-700 dark:text-slate-300 mb-2 capitalize">
                  {{ miniMonth.label }}
                </p>
                <!-- Weekday headers -->
                <div class="grid grid-cols-7 mb-1">
                  <div
                    v-for="(wd, i) in ['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su']"
                    :key="i"
                    class="text-center text-[9px] font-semibold text-slate-400 dark:text-slate-500"
                  >
                    {{ wd }}
                  </div>
                </div>
                <!-- Day cells -->
                <div class="grid grid-cols-7 gap-y-0.5">
                  <div
                    v-for="cell in miniMonth.days"
                    :key="cell.iso"
                    :class="[
                      'relative text-[11px] h-6 flex items-center justify-center rounded',
                      !cell.isCurrentMonth && 'opacity-20',
                      cell.isToday && 'bg-indigo-600 text-white font-bold',
                      !cell.isToday && vacationsByDate.has(cell.iso) && 'bg-indigo-100 dark:bg-indigo-950/60 text-indigo-700 dark:text-indigo-300 font-medium',
                      !cell.isToday && !vacationsByDate.has(cell.iso) && holidaysByDate.has(cell.iso) && 'bg-amber-100 dark:bg-amber-950/40 text-amber-700 dark:text-amber-300',
                      !cell.isToday && !vacationsByDate.has(cell.iso) && !holidaysByDate.has(cell.iso) && cell.isWeekend && 'text-slate-400 dark:text-slate-600',
                      !cell.isToday && !vacationsByDate.has(cell.iso) && !holidaysByDate.has(cell.iso) && !cell.isWeekend && cell.isCurrentMonth && 'text-slate-700 dark:text-slate-300',
                    ]"
                    :title="holidaysByDate.get(cell.iso) ?? (vacationsByDate.get(cell.iso)?.[0]?.label)"
                  >
                    {{ cell.day }}
                  </div>
                </div>
              </button>
            </div>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>
