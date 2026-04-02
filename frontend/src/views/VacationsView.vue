<script setup lang="ts">
import { ref, computed, watch, onMounted } from "vue";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import {
  vacationService,
  type VacationBalance,
  type VacationDay,
  type CreateVacationDayDto,
  type TeamVacationDay,
} from "../services/vacationService";
import { holidayService, type PublicHoliday } from "../services/holidayService";
import { useAuth } from "@/composables/useAuth";
import { useAppToast } from "@/composables/useAppToast";
import { extractApiError } from "@/utils/apiError";
import { useConfirmDialog } from "@/composables/useConfirmDialog";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import YearCalendarOverlay from "@/components/YearCalendarOverlay.vue";
import VacationBalanceCards from "@/components/VacationBalanceCards.vue";
import VacationDaysList from "@/components/VacationDaysList.vue";
import VacationEditDialog from "@/components/VacationEditDialog.vue";
import {
  CheckCircleIcon,
  XCircleIcon,
  Loader2Icon,
  ChevronLeftIcon,
  ChevronRightIcon,
  PlusIcon,
  Maximize2Icon,
  PencilIcon,
  Trash2Icon,
} from "lucide-vue-next";

const toast = useAppToast();
const { confirm } = useConfirmDialog();
const { currentUser } = useAuth();

// ─── Data state ───────────────────────────────────────────────────────────────

const balances = ref<VacationBalance[]>([]);
const vacationDays = ref<VacationDay[]>([]);
const holidays = ref<PublicHoliday[]>([]);
const teamVacationDays = ref<TeamVacationDay[]>([]);
const loading = ref(false);

// ─── Year overlay ─────────────────────────────────────────────────────────────

const yearOverlayOpen = ref(false);

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

const goToday = () => {
  currentMonth.value = new Date();
};

interface CalDay {
  iso: string;
  day: number;
  isCurrentMonth: boolean;
  isToday: boolean;
  isWeekend: boolean;
}

const toIso = (d: Date) => {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
};

const todayIso = toIso(new Date());

const calendarDays = computed<CalDay[]>(() => {
  const year = currentMonth.value.getFullYear();
  const month = currentMonth.value.getMonth();
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
});

const holidaysByDate = computed(() => {
  const map = new Map<string, PublicHoliday>();
  for (const h of holidays.value) map.set(h.date, h);
  return map;
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

const fetchTeamVacationDays = async () => {
  try {
    teamVacationDays.value = await vacationService.getTeamVacationDays({
      year: currentMonth.value.getFullYear(),
      month: currentMonth.value.getMonth() + 1,
    });
  } catch {
    // non-critical, fail silently
  }
};

watch(currentMonth, fetchTeamVacationDays);

const WEEK_DAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
const MAX_VISIBLE = 2;

// ─── Popover (inline create) ──────────────────────────────────────────────────

const openPopoverIso = ref<string | null>(null);
const popoverSaving = ref(false);

const popoverForm = ref({
  vacationTypeId: "",
  startDate: "",
  endDate: "",
  amount: "1",
  note: "",
});

const workingDaysInRange = computed(() => {
  const { startDate, endDate } = popoverForm.value;
  if (!startDate || !endDate) return 0;
  const start = new Date(startDate + "T00:00:00");
  const end = new Date(endDate + "T00:00:00");
  if (end < start) return 0;
  let count = 0;
  const d = new Date(start);
  while (d <= end) {
    const dow = d.getDay();
    const iso = toIso(d);
    if (dow !== 0 && dow !== 6 && !holidaysByDate.value.has(iso)) count++;
    d.setDate(d.getDate() + 1);
  }
  return count;
});

const isRangeMode = computed(
  () => popoverForm.value.startDate !== popoverForm.value.endDate && !!popoverForm.value.endDate
);

const popoverLiveRemaining = computed(() => {
  if (!popoverForm.value.vacationTypeId) return null;
  const typeId = parseInt(popoverForm.value.vacationTypeId);
  const balance = balances.value.find((b) => b.vacationTypeId === typeId);
  if (!balance) return null;
  const cost = workingDaysInRange.value * parseFloat(popoverForm.value.amount || "1");
  return balance.remainingDays - cost;
});

const canPopoverSubmit = computed(() => {
  if (!popoverForm.value.vacationTypeId || !popoverForm.value.startDate) return false;
  if (popoverLiveRemaining.value === null || popoverLiveRemaining.value < 0) return false;
  if (workingDaysInRange.value === 0) return false;
  return true;
});

const highlightedRange = computed(() => {
  const { startDate, endDate } = popoverForm.value;
  if (!startDate || !endDate || !openPopoverIso.value) return new Set<string>();
  const s = startDate < endDate ? startDate : endDate;
  const e = startDate < endDate ? endDate : startDate;
  const set = new Set<string>();
  const d = new Date(s + "T00:00:00");
  const end = new Date(e + "T00:00:00");
  while (d <= end) {
    set.add(toIso(d));
    d.setDate(d.getDate() + 1);
  }
  return set;
});

const popoverDateLabel = computed(() => {
  if (!openPopoverIso.value) return "";
  return new Date(openPopoverIso.value + "T00:00:00").toLocaleDateString(undefined, {
    weekday: "long",
    day: "numeric",
    month: "long",
  });
});

const openPopover = (iso: string) => {
  openPopoverIso.value = iso;
  popoverForm.value = {
    vacationTypeId: balances.value.length === 1 ? String(balances.value[0].vacationTypeId) : "",
    startDate: iso,
    endDate: iso,
    amount: "1",
    note: "",
  };
};

const closePopover = () => {
  openPopoverIso.value = null;
};

const savePopover = async () => {
  if (!canPopoverSubmit.value) return;
  popoverSaving.value = true;
  try {
    const { vacationTypeId, startDate, endDate, amount, note } = popoverForm.value;

    if (startDate === endDate) {
      const payload: CreateVacationDayDto = {
        vacationTypeId: parseInt(vacationTypeId),
        date: startDate,
        amount: parseFloat(amount),
        note: note.trim() || undefined,
      };
      const created = await vacationService.create(payload);
      vacationDays.value.unshift(created);
      toast.success("Vacation day planned");
    } else {
      const result = await vacationService.createRange({
        vacationTypeId: parseInt(vacationTypeId),
        startDate,
        endDate,
        amount: parseFloat(amount),
        note: note.trim() || undefined,
      });
      vacationDays.value.unshift(...result.created);
      const skipped = result.skippedWeekends + result.skippedExisting;
      toast.success(
        skipped > 0
          ? `${result.created.length} day(s) planned (${skipped} skipped)`
          : `${result.created.length} day(s) planned`
      );
    }

    balances.value = await vacationService.getBalances();
    closePopover();
  } catch (err: unknown) {
    toast.error(extractApiError(err, "Failed to plan vacation"));
  } finally {
    popoverSaving.value = false;
  }
};

// ─── Edit dialog ──────────────────────────────────────────────────────────────

const editDialogOpen = ref(false);
const editingDay = ref<VacationDay | null>(null);

const openEdit = (day: VacationDay) => {
  editingDay.value = day;
  closePopover();
  editDialogOpen.value = true;
};

const onEditSaved = (updated: VacationDay, newBalances: VacationBalance[]) => {
  const idx = vacationDays.value.findIndex((d) => d.id === updated.id);
  if (idx !== -1) vacationDays.value[idx] = updated;
  balances.value = newBalances;
};

// ─── Delete ───────────────────────────────────────────────────────────────────

const displayDate = (iso: string) =>
  new Date(iso).toLocaleDateString(undefined, { day: "numeric", month: "short", year: "numeric" });

const deleteDay = (day: VacationDay) => {
  confirm({
    title: "Delete vacation day",
    message: `Remove the ${day.amount === 0.5 ? "half" : "full"} day on ${displayDate(day.date)} (${day.vacationTypeName})?`,
    confirmLabel: "Delete",
    variant: "destructive",
    onConfirm: async () => {
      try {
        await vacationService.delete(day.id);
        vacationDays.value = vacationDays.value.filter((d) => d.id !== day.id);
        balances.value = await vacationService.getBalances();
        toast.success("Vacation day removed");
      } catch {
        toast.error("Failed to delete");
      }
    },
  });
};

// ─── Mount ────────────────────────────────────────────────────────────────────

onMounted(async () => {
  loading.value = true;
  try {
    const year = new Date().getFullYear();
    const month = new Date().getMonth() + 1;
    const [b, d, h, t] = await Promise.all([
      vacationService.getBalances(),
      vacationService.getVacationDays(),
      holidayService.getHolidays(year).catch(() => [] as PublicHoliday[]),
      vacationService.getTeamVacationDays({ year, month }).catch(() => [] as TeamVacationDay[]),
    ]);
    balances.value = b;
    vacationDays.value = d;
    holidays.value = h;
    teamVacationDays.value = t;
  } catch {
    toast.error("Failed to load vacation data");
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
        <div class="mb-6">
          <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">Vacations</h1>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
            Plan your vacation and see when your team is off
          </p>
        </div>

        <div class="flex justify-end mb-6">
          <Button variant="outline" size="sm" @click="yearOverlayOpen = true">
            <Maximize2Icon class="size-3.5" />
            Year view
          </Button>
        </div>

        <div class="max-w-4xl">
          <!-- Balance cards -->
          <VacationBalanceCards :balances="balances" :loading="loading" />

          <!-- Calendar -->
          <section class="mb-8">
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
              <Button variant="outline" size="sm" @click="goToday">Today</Button>
            </div>

            <div v-if="loading" class="card h-64 animate-pulse bg-slate-100 dark:bg-slate-800" />

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
                      if (v && cell.isCurrentMonth && !cell.isWeekend && balances.length > 0)
                        openPopover(cell.iso);
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
                        openPopoverIso === cell.iso ? 'ring-2 ring-inset ring-indigo-400 dark:ring-indigo-500' : '',
                        highlightedRange.has(cell.iso) && openPopoverIso !== cell.iso ? 'bg-indigo-50 dark:bg-indigo-950/30' : '',
                        cell.isCurrentMonth && !cell.isWeekend && balances.length > 0
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

                      <!-- Own vacation chips -->
                      <template v-if="vacationsByDate.has(cell.iso)">
                        <div
                          v-for="entry in vacationsByDate.get(cell.iso)!.slice(0, MAX_VISIBLE)"
                          :key="entry.id"
                          :style="{
                            backgroundColor: (entry.vacationTypeColor ?? '#6366f1') + '28',
                            borderLeftColor: entry.vacationTypeColor ?? '#6366f1',
                          }"
                          class="text-[10px] leading-tight truncate rounded px-1 py-0.5 mb-0.5 border-l-2 text-slate-700 dark:text-slate-200"
                        >
                          {{ entry.vacationTypeName }}<span v-if="entry.amount === 0.5" class="opacity-50"> ½</span>
                        </div>
                        <div
                          v-if="vacationsByDate.get(cell.iso)!.length > MAX_VISIBLE"
                          class="text-[10px] text-slate-400 dark:text-slate-500 pl-1"
                        >
                          +{{ vacationsByDate.get(cell.iso)!.length - MAX_VISIBLE }} more
                        </div>
                      </template>

                      <!-- Teammates' chips -->
                      <template v-if="teamVacationsByDate.has(cell.iso)">
                        <div
                          v-for="entry in teamVacationsByDate.get(cell.iso)!.slice(0, 2)"
                          :key="`team-${entry.id}`"
                          :style="{ borderLeftColor: entry.vacationTypeColor ?? '#6366f1' }"
                          class="text-[10px] leading-tight truncate rounded px-1 py-0.5 mb-0.5 border-l-2 text-slate-400 dark:text-slate-500 bg-slate-50 dark:bg-slate-800/50"
                        >
                          {{ entry.employeeName.split(" ")[0] }}<span v-if="entry.amount === 0.5" class="opacity-50"> ½</span>
                        </div>
                      </template>
                    </div>
                  </PopoverTrigger>

                  <PopoverContent class="w-72 p-0 shadow-lg" side="bottom" :collision-padding="12">
                    <!-- Popover header -->
                    <div class="flex items-center justify-between px-4 py-3 border-b border-slate-100 dark:border-slate-800">
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

                    <!-- Own existing entries -->
                    <div
                      v-if="vacationsByDate.has(cell.iso)"
                      class="divide-y divide-slate-100 dark:divide-slate-800 border-b border-slate-100 dark:border-slate-800"
                    >
                      <div
                        v-for="entry in vacationsByDate.get(cell.iso)"
                        :key="entry.id"
                        class="flex items-center gap-2.5 px-4 py-2.5"
                      >
                        <div
                          class="w-2 h-2 rounded-full shrink-0 ring-1 ring-black/10"
                          :style="{ backgroundColor: entry.vacationTypeColor ?? '#6366f1' }"
                        />
                        <div class="flex-1 min-w-0">
                          <p class="text-sm font-medium text-slate-900 dark:text-slate-100 truncate">
                            {{ entry.vacationTypeName }}
                          </p>
                          <p v-if="entry.note" class="text-xs text-slate-400 dark:text-slate-500 truncate">
                            {{ entry.note }}
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
                        <div class="flex items-center gap-0.5 shrink-0">
                          <button
                            class="p-1 rounded text-slate-400 hover:text-indigo-600 dark:hover:text-indigo-400 transition-colors"
                            title="Edit"
                            @click="openEdit(entry)"
                          >
                            <PencilIcon class="size-3.5" />
                          </button>
                          <button
                            class="p-1 rounded text-slate-400 hover:text-red-500 dark:hover:text-red-400 transition-colors"
                            title="Delete"
                            @click="deleteDay(entry)"
                          >
                            <Trash2Icon class="size-3.5" />
                          </button>
                        </div>
                      </div>
                    </div>

                    <!-- Teammates' entries (read-only) -->
                    <div
                      v-if="teamVacationsByDate.has(cell.iso)"
                      class="divide-y divide-slate-100 dark:divide-slate-800 border-b border-slate-100 dark:border-slate-800"
                    >
                      <div
                        v-for="entry in teamVacationsByDate.get(cell.iso)"
                        :key="`team-${entry.id}`"
                        class="flex items-center gap-2.5 px-4 py-2"
                      >
                        <div
                          class="w-2 h-2 rounded-full shrink-0 ring-1 ring-black/10"
                          :style="{ backgroundColor: entry.vacationTypeColor ?? '#6366f1' }"
                        />
                        <div class="flex-1 min-w-0">
                          <p class="text-xs font-medium text-slate-700 dark:text-slate-300 truncate">
                            {{ entry.employeeName }}
                          </p>
                          <p class="text-xs text-slate-400 dark:text-slate-500 truncate">
                            {{ entry.vacationTypeName }}
                          </p>
                        </div>
                        <span class="text-[10px] font-medium px-1.5 py-0.5 rounded shrink-0 bg-slate-100 dark:bg-slate-800 text-slate-500 dark:text-slate-400">
                          {{ entry.amount === 1 ? "Full" : "½" }}
                        </span>
                      </div>
                    </div>

                    <!-- Create form -->
                    <div class="p-4 space-y-3">
                      <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">
                        Plan vacation
                      </p>

                      <div class="space-y-1">
                        <Label class="text-xs">Type</Label>
                        <Select v-model="popoverForm.vacationTypeId">
                          <SelectTrigger class="h-8 text-sm">
                            <SelectValue placeholder="Select type" />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem
                              v-for="balance in balances"
                              :key="balance.vacationTypeId"
                              :value="String(balance.vacationTypeId)"
                            >
                              {{ balance.vacationTypeName }}
                            </SelectItem>
                          </SelectContent>
                        </Select>
                      </div>

                      <div class="grid grid-cols-2 gap-2">
                        <div class="space-y-1">
                          <Label class="text-xs">From</Label>
                          <Input v-model="popoverForm.startDate" type="date" class="h-8 text-sm" />
                        </div>
                        <div class="space-y-1">
                          <Label class="text-xs">To</Label>
                          <Input v-model="popoverForm.endDate" type="date" class="h-8 text-sm" />
                        </div>
                      </div>

                      <div class="space-y-1">
                        <Label class="text-xs">Duration</Label>
                        <Select v-model="popoverForm.amount">
                          <SelectTrigger class="h-8 text-sm">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="1">Full day</SelectItem>
                            <SelectItem value="0.5">Half day</SelectItem>
                          </SelectContent>
                        </Select>
                      </div>

                      <div
                        v-if="isRangeMode"
                        class="text-xs text-slate-500 dark:text-slate-400 bg-slate-50 dark:bg-slate-800 rounded-lg px-3 py-2"
                      >
                        {{ workingDaysInRange }} working day{{ workingDaysInRange !== 1 ? "s" : "" }} selected
                        <span class="text-slate-400 dark:text-slate-500">(weekends skipped)</span>
                      </div>

                      <div
                        v-if="popoverForm.vacationTypeId && popoverLiveRemaining !== null"
                        :class="[
                          'rounded-lg px-3 py-2 text-xs flex items-center gap-1.5',
                          popoverLiveRemaining < 0
                            ? 'bg-destructive/10 text-destructive'
                            : 'bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-300',
                        ]"
                      >
                        <XCircleIcon v-if="popoverLiveRemaining < 0" class="size-3.5 shrink-0" />
                        <CheckCircleIcon v-else class="size-3.5 shrink-0" />
                        <span v-if="popoverLiveRemaining < 0">
                          Exceeds balance by {{ Math.abs(popoverLiveRemaining) }} day(s)
                        </span>
                        <span v-else>{{ popoverLiveRemaining }} day(s) remaining after this</span>
                      </div>

                      <Button
                        class="w-full"
                        size="sm"
                        :disabled="popoverSaving || !canPopoverSubmit"
                        @click="savePopover"
                      >
                        <Loader2Icon v-if="popoverSaving" class="size-3.5 animate-spin" />
                        <PlusIcon v-else class="size-3.5" />
                        {{ isRangeMode ? `Plan ${workingDaysInRange} day(s)` : "Plan day" }}
                      </Button>
                    </div>
                  </PopoverContent>
                </Popover>
              </div>
            </div>
          </section>

          <!-- Planned days list -->
          <VacationDaysList
            :vacation-days="vacationDays"
            :loading="loading"
            @edit="openEdit"
            @delete="deleteDay"
          />
        </div>
      </div>
    </div>

    <!-- Year overview overlay -->
    <YearCalendarOverlay
      v-model:open="yearOverlayOpen"
      :vacations-by-date="vacationsByDate"
      :team-vacations-by-date="teamVacationsByDate"
    />

    <!-- Edit dialog -->
    <VacationEditDialog
      v-model:open="editDialogOpen"
      :editing-day="editingDay"
      :balances="balances"
      @saved="onEditSaved"
    />
  </AuthenticatedLayout>
</template>
