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
import { useConfirmDialog } from "@/composables/useConfirmDialog";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
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
import {
  PencilIcon,
  Trash2Icon,
  CalendarIcon,
  CheckCircleIcon,
  XCircleIcon,
  Loader2Icon,
  ChevronLeftIcon,
  ChevronRightIcon,
  PlusIcon,
  Maximize2Icon,
} from "lucide-vue-next";

const toast = useAppToast();
const { confirm } = useConfirmDialog();
const { currentUser } = useAuth();

const balances = ref<VacationBalance[]>([]);
const vacationDays = ref<VacationDay[]>([]);
const holidays = ref<PublicHoliday[]>([]);
const teamVacationDays = ref<TeamVacationDay[]>([]);
const loading = ref(false);

// ─── Holidays ─────────────────────────────────────────────────────────────────

const holidaysByDate = computed(() => {
  const map = new Map<string, PublicHoliday>();
  for (const h of holidays.value) map.set(h.date, h);
  return map;
});

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
    days.push({
      iso: toIso(d),
      day: d.getDate(),
      isCurrentMonth: false,
      isToday: false,
      isWeekend: dow === 0 || dow === 6,
    });
  }
  for (let n = 1; n <= lastDay.getDate(); n++) {
    const d = new Date(year, month, n);
    const iso = toIso(d);
    const dow = d.getDay();
    days.push({
      iso,
      day: n,
      isCurrentMonth: true,
      isToday: iso === todayIso,
      isWeekend: dow === 0 || dow === 6,
    });
  }
  const remaining = 42 - days.length;
  for (let n = 1; n <= remaining; n++) {
    const d = new Date(year, month + 1, n);
    const dow = d.getDay();
    days.push({
      iso: toIso(d),
      day: d.getDate(),
      isCurrentMonth: false,
      isToday: false,
      isWeekend: dow === 0 || dow === 6,
    });
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

// ─── Popover (inline create / view entries) ───────────────────────────────────

const openPopoverIso = ref<string | null>(null);
const popoverSaving = ref(false);

const popoverForm = ref({
  vacationTypeId: "",
  startDate: "",
  endDate: "",
  amount: "1",
  note: "",
});

// Working days count between start and end (inclusive, excl. weekends + holidays)
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

// Live balance remaining after the planned entry/range
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

// Which ISOs are in the current popover range (for calendar highlight)
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
      // Single day
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
      // Range
      const result = await vacationService.createRange({
        vacationTypeId: parseInt(vacationTypeId),
        startDate,
        endDate,
        amount: parseFloat(amount),
        note: note.trim() || undefined,
      });
      vacationDays.value.unshift(...result.created);
      const skipped = result.skippedWeekends + result.skippedExisting;
      const msg =
        skipped > 0
          ? `${result.created.length} day(s) planned (${skipped} skipped)`
          : `${result.created.length} day(s) planned`;
      toast.success(msg);
    }

    balances.value = await vacationService.getBalances();
    closePopover();
  } catch (err: unknown) {
    const msg =
      (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
      "Failed to plan vacation";
    toast.error(msg);
  } finally {
    popoverSaving.value = false;
  }
};

// ─── Edit dialog ──────────────────────────────────────────────────────────────

const editDialogVisible = ref(false);
const editSaving = ref(false);
const editingDay = ref<VacationDay | null>(null);

const editForm = ref({
  vacationTypeId: "",
  date: "",
  amount: "1",
  note: "",
});

const editLiveRemaining = computed(() => {
  if (!editForm.value.vacationTypeId || !editingDay.value) return null;
  const typeId = parseInt(editForm.value.vacationTypeId);
  const balance = balances.value.find((b) => b.vacationTypeId === typeId);
  if (!balance) return null;
  let base = balance.remainingDays;
  if (editingDay.value.vacationTypeId === typeId) {
    const origYear = new Date(editingDay.value.date).getUTCFullYear();
    if (origYear === new Date().getUTCFullYear()) base += editingDay.value.amount;
  }
  return base - parseFloat(editForm.value.amount);
});

const canEditSubmit = computed(() => {
  if (!editForm.value.vacationTypeId || !editForm.value.date) return false;
  if (editLiveRemaining.value === null || editLiveRemaining.value < 0) return false;
  return true;
});

const openEdit = (day: VacationDay) => {
  editingDay.value = day;
  editForm.value = {
    vacationTypeId: String(day.vacationTypeId),
    date: day.date,
    amount: String(day.amount),
    note: day.note ?? "",
  };
  closePopover();
  editDialogVisible.value = true;
};

const saveEdit = async () => {
  if (!canEditSubmit.value || !editingDay.value) return;
  editSaving.value = true;
  try {
    const payload: CreateVacationDayDto = {
      vacationTypeId: parseInt(editForm.value.vacationTypeId),
      date: editForm.value.date,
      amount: parseFloat(editForm.value.amount),
      note: editForm.value.note.trim() || undefined,
    };
    const updated = await vacationService.update(editingDay.value.id, payload);
    const idx = vacationDays.value.findIndex((d) => d.id === editingDay.value!.id);
    if (idx !== -1) vacationDays.value[idx] = updated;
    balances.value = await vacationService.getBalances();
    toast.success("Vacation day updated");
    editDialogVisible.value = false;
  } catch (err: unknown) {
    const msg =
      (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
      "Failed to update vacation day";
    toast.error(msg);
  } finally {
    editSaving.value = false;
  }
};

// ─── Delete ───────────────────────────────────────────────────────────────────

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

// ─── Helpers ──────────────────────────────────────────────────────────────────

const displayDate = (iso: string) =>
  new Date(iso).toLocaleDateString(undefined, { day: "numeric", month: "short", year: "numeric" });

const popoverDateLabel = computed(() => {
  if (!openPopoverIso.value) return "";
  return new Date(openPopoverIso.value + "T00:00:00").toLocaleDateString(undefined, {
    weekday: "long",
    day: "numeric",
    month: "long",
  });
});

const balanceBarWidth = (balance: VacationBalance) => {
  if (balance.yearlyBalance === 0) return "0%";
  return `${Math.min((balance.usedDays / balance.yearlyBalance) * 100, 100)}%`;
};

const balanceBarColor = (balance: VacationBalance) => {
  const pct = balance.yearlyBalance > 0 ? balance.usedDays / balance.yearlyBalance : 0;
  if (pct >= 1) return "bg-red-500";
  if (pct >= 0.8) return "bg-amber-400";
  return "bg-emerald-500";
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
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">Plan your vacation and see when your team is off</p>
        </div>

        <div class="flex justify-end mb-6">
          <Button variant="outline" size="sm" @click="yearOverlayOpen = true">
            <Maximize2Icon class="size-3.5" />
            Year view
          </Button>
        </div>
        <div class="max-w-4xl">

        <!-- Balance cards -->
        <section class="mb-8">
          <h2
            class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500 mb-3"
          >
            Balances ({{ new Date().getFullYear() }})
          </h2>

          <div v-if="loading" class="grid gap-3 sm:grid-cols-2">
            <div v-for="i in 2" :key="i" class="card p-4 space-y-3">
              <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-24 animate-pulse" />
              <div class="h-2 bg-slate-200 dark:bg-slate-700 rounded animate-pulse" />
              <div class="h-2.5 bg-slate-200 dark:bg-slate-700 rounded w-32 animate-pulse" />
            </div>
          </div>

          <div v-else-if="balances.length === 0" class="card text-center py-10">
            <CalendarIcon class="size-8 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
            <p class="text-sm text-slate-500 dark:text-slate-400">
              No vacation types have been assigned to you yet.
            </p>
          </div>

          <div v-else class="grid gap-3 sm:grid-cols-2">
            <div v-for="balance in balances" :key="balance.vacationTypeId" class="card p-4">
              <div class="flex items-center gap-2 mb-3">
                <div
                  class="w-3 h-3 rounded-full shrink-0 ring-1 ring-black/10"
                  :style="{ backgroundColor: balance.vacationTypeColor ?? '#6366f1' }"
                />
                <span class="text-sm font-medium text-slate-900 dark:text-slate-100">{{
                  balance.vacationTypeName
                }}</span>
              </div>
              <div class="w-full bg-slate-100 dark:bg-slate-800 rounded-full h-1.5 mb-2">
                <div
                  :class="[
                    'h-1.5 rounded-full transition-all duration-300',
                    balanceBarColor(balance),
                  ]"
                  :style="{ width: balanceBarWidth(balance) }"
                />
              </div>
              <div class="flex justify-between text-xs text-slate-500 dark:text-slate-400">
                <span>{{ balance.usedDays }} / {{ balance.yearlyBalance }} days used</span>
                <span
                  :class="
                    balance.remainingDays <= 0
                      ? 'text-red-600 dark:text-red-400 font-semibold'
                      : 'text-emerald-600 dark:text-emerald-400 font-semibold'
                  "
                  >{{ balance.remainingDays }} remaining</span
                >
              </div>
            </div>
          </div>
        </section>

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
              <span
                class="text-base font-semibold text-slate-900 dark:text-slate-100 ml-2 capitalize"
              >
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
                      openPopoverIso === cell.iso
                        ? 'ring-2 ring-inset ring-indigo-400 dark:ring-indigo-500'
                        : '',
                      highlightedRange.has(cell.iso) && openPopoverIso !== cell.iso
                        ? 'bg-indigo-50 dark:bg-indigo-950/30'
                        : '',
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
                        {{ entry.vacationTypeName
                        }}<span v-if="entry.amount === 0.5" class="opacity-50"> ½</span>
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
                  <div
                    class="flex items-center justify-between px-4 py-3 border-b border-slate-100 dark:border-slate-800"
                  >
                    <span
                      class="text-sm font-semibold text-slate-900 dark:text-slate-100 capitalize"
                    >
                      {{ popoverDateLabel }}
                    </span>
                    <button
                      class="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 transition-colors"
                      @click="closePopover"
                    >
                      <svg
                        class="size-4"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        stroke-width="2"
                      >
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

                  <!-- Existing entries -->
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
                        <p
                          v-if="entry.note"
                          class="text-xs text-slate-400 dark:text-slate-500 truncate"
                        >
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
                      <span
                        :class="[
                          'text-[10px] font-medium px-1.5 py-0.5 rounded shrink-0',
                          entry.amount === 1
                            ? 'bg-slate-100 dark:bg-slate-800 text-slate-500 dark:text-slate-400'
                            : 'bg-slate-100 dark:bg-slate-800 text-slate-500 dark:text-slate-400',
                        ]"
                      >{{ entry.amount === 1 ? "Full" : "½" }}</span>
                    </div>
                  </div>

                  <!-- Create form -->
                  <div class="p-4 space-y-3">
                    <p
                      class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500"
                    >
                      Plan vacation
                    </p>

                    <!-- Vacation type -->
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

                    <!-- Date range -->
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

                    <!-- Duration -->
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

                    <!-- Range summary -->
                    <div
                      v-if="isRangeMode"
                      class="text-xs text-slate-500 dark:text-slate-400 bg-slate-50 dark:bg-slate-800 rounded-lg px-3 py-2"
                    >
                      {{ workingDaysInRange }} working day{{ workingDaysInRange !== 1 ? "s" : "" }}
                      selected
                      <span class="text-slate-400 dark:text-slate-500">(weekends skipped)</span>
                    </div>

                    <!-- Balance feedback -->
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
        <section>
          <h2
            class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500 mb-3"
          >
            All planned days
          </h2>

          <div v-if="loading" class="card divide-y divide-slate-100 dark:divide-slate-800">
            <div v-for="i in 3" :key="i" class="flex items-center gap-4 px-5 py-4">
              <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-24 animate-pulse" />
              <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-32 animate-pulse flex-1" />
              <div class="h-6 bg-slate-200 dark:bg-slate-700 rounded w-16 animate-pulse" />
            </div>
          </div>

          <div v-else-if="vacationDays.length === 0" class="card text-center py-10">
            <CalendarIcon class="size-6 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
            <p class="text-sm text-slate-500 dark:text-slate-400">No vacation days planned yet.</p>
          </div>

          <div v-else class="card divide-y divide-slate-100 dark:divide-slate-800 overflow-hidden">
            <div
              v-for="day in vacationDays"
              :key="day.id"
              class="flex items-center gap-3 px-5 py-3.5"
            >
              <span class="text-sm font-medium text-slate-900 dark:text-slate-100 w-32 shrink-0">
                {{ displayDate(day.date) }}
              </span>
              <div class="flex items-center gap-2 flex-1 min-w-0">
                <div
                  class="w-2.5 h-2.5 rounded-full shrink-0 ring-1 ring-black/10"
                  :style="{ backgroundColor: day.vacationTypeColor ?? '#6366f1' }"
                />
                <span class="text-sm text-slate-600 dark:text-slate-400 truncate">
                  {{ day.vacationTypeName }}
                  <span v-if="day.note" class="text-slate-400 dark:text-slate-500">
                    · {{ day.note }}</span
                  >
                </span>
              </div>
              <span
                :class="[
                  'inline-flex items-center px-2 py-0.5 rounded text-xs font-medium shrink-0',
                  day.amount === 1
                    ? 'bg-indigo-50 dark:bg-indigo-950 text-indigo-700 dark:text-indigo-300'
                    : 'bg-sky-50 dark:bg-sky-950 text-sky-700 dark:text-sky-300',
                ]"
              >
                {{ day.amount === 1 ? "Full day" : "Half day" }}
              </span>
              <div class="flex items-center gap-1 shrink-0">
                <Button
                  variant="ghost"
                  size="icon"
                  class="size-8 text-slate-400 hover:text-slate-700 dark:hover:text-slate-200"
                  title="Edit"
                  @click="openEdit(day)"
                >
                  <PencilIcon class="size-3.5" />
                </Button>
                <Button
                  variant="ghost"
                  size="icon"
                  class="size-8 text-slate-400 hover:text-red-500 dark:hover:text-red-400"
                  title="Delete"
                  @click="deleteDay(day)"
                >
                  <Trash2Icon class="size-3.5" />
                </Button>
              </div>
            </div>
          </div>
        </section>
        </div>
      </div>
    </div>

    <!-- Year overview overlay -->
    <YearCalendarOverlay v-model:open="yearOverlayOpen" :vacations-by-date="vacationsByDate" :team-vacations-by-date="teamVacationsByDate" />

    <!-- Edit dialog -->
    <Dialog v-model:open="editDialogVisible">
      <DialogContent class="sm:max-w-[400px]">
        <DialogHeader>
          <DialogTitle>Edit vacation day</DialogTitle>
        </DialogHeader>

        <div class="flex flex-col gap-4 py-2">
          <div class="space-y-1.5">
            <Label>Vacation type <span class="text-destructive">*</span></Label>
            <Select v-model="editForm.vacationTypeId">
              <SelectTrigger class="w-full">
                <SelectValue placeholder="Select a type" />
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

          <div class="space-y-1.5">
            <Label>Date <span class="text-destructive">*</span></Label>
            <Input v-model="editForm.date" type="date" />
          </div>

          <div class="space-y-1.5">
            <Label>Duration <span class="text-destructive">*</span></Label>
            <Select v-model="editForm.amount">
              <SelectTrigger class="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="1">Full day (1.0)</SelectItem>
                <SelectItem value="0.5">Half day (0.5)</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <div class="space-y-1.5">
            <Label>Note</Label>
            <Input
              v-model="editForm.note"
              type="text"
              placeholder="Optional note"
              maxlength="500"
            />
          </div>

          <div
            v-if="editForm.vacationTypeId && editLiveRemaining !== null"
            :class="[
              'rounded-lg px-3 py-2 text-sm flex items-center gap-2',
              editLiveRemaining < 0
                ? 'bg-destructive/10 text-destructive'
                : 'bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-300',
            ]"
          >
            <XCircleIcon v-if="editLiveRemaining < 0" class="size-3.5 shrink-0" />
            <CheckCircleIcon v-else class="size-3.5 shrink-0" />
            <span v-if="editLiveRemaining < 0">
              Exceeds balance — {{ Math.abs(editLiveRemaining) }} day(s) short
            </span>
            <span v-else>{{ editLiveRemaining }} day(s) remaining after this entry</span>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" @click="editDialogVisible = false">Cancel</Button>
          <Button @click="saveEdit" :disabled="editSaving || !canEditSubmit">
            <Loader2Icon v-if="editSaving" class="size-4 animate-spin" />
            Save changes
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </AuthenticatedLayout>
</template>
