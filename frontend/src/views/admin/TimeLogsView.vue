<script setup lang="ts">
import { ref, computed, watch, onMounted } from "vue";
import { useRoute } from "vue-router";
import {
  adminService,
  type AdminTimeLog,
  type AdminVacationDay,
  type Employee,
  type TimeLogSummary,
} from "../../services/adminService";
import { holidayService, type PublicHoliday } from "../../services/holidayService";
import { useAppToast } from "@/composables/useAppToast";
import { useLiveHours } from "@/composables/useLiveHours";
import { useAutoRefresh } from "@/composables/useAutoRefresh";
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
import {
  Table,
  TableBody,
  TableCell,
  TableEmpty,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  ClockIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  CoffeeIcon,
  MessageSquareTextIcon,
  CalendarIcon,
  StarIcon,
  TrendingUpIcon,
  TrendingDownIcon,
} from "lucide-vue-next";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from "@/components/ui/dialog";

const descriptionDialog = ref<{ open: boolean; text: string; employee: string; date: string }>({
  open: false,
  text: "",
  employee: "",
  date: "",
});

function openDescription(log: AdminTimeLog) {
  descriptionDialog.value = {
    open: true,
    text: log.description!,
    employee: log.employeeName,
    date: formatDate(log.date),
  };
}

const descOverflow = ref<Record<string, boolean>>({});

function checkDescOverflow(el: Element | null, key: string) {
  if (el) descOverflow.value[key] = el.scrollWidth > el.clientWidth;
}

const toast = useAppToast();
const route = useRoute();
const { liveHours, isOnBreak } = useLiveHours();

const allLogs = ref<AdminTimeLog[]>([]);
const allVacations = ref<AdminVacationDay[]>([]);
const allHolidays = ref<PublicHoliday[]>([]);
const employees = ref<Employee[]>([]);
const loading = ref(true); // true until the first load, so the table doesn't flash its empty state

/** The default view: this month so far (1st → today), the same range as the "This month" chip. */
function thisMonthRange() {
  const now = new Date();
  return {
    from: toLocalDateStr(new Date(now.getFullYear(), now.getMonth(), 1)),
    to: toLocalDateStr(now),
  };
}

const selectedEmployeeId = ref<string>("all");
const dateFrom = ref<string>(thisMonthRange().from);
const dateTo = ref<string>(thisMonthRange().to);

// ─── Pagination ───────────────────────────────────────────────────────────────

const pageSize = 15;
const currentPage = ref(1);

const totalPages = computed(() => Math.max(1, Math.ceil(allLogs.value.length / pageSize)));
const paginatedLogs = computed(() => {
  const start = (currentPage.value - 1) * pageSize;
  return allLogs.value.slice(start, start + pageSize);
});

// ─── Week helpers ─────────────────────────────────────────────────────────────

function toLocalDateStr(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

function getWeekKey(dateStr: string): string {
  const d = new Date(dateStr + "T00:00:00");
  const day = d.getDay() || 7;
  const monday = new Date(d);
  monday.setDate(d.getDate() - day + 1);
  return toLocalDateStr(monday);
}

function formatWeekLabel(mondayStr: string): string {
  const monday = new Date(mondayStr + "T00:00:00");
  const sunday = new Date(monday);
  sunday.setDate(monday.getDate() + 6);
  return `${monday.toLocaleDateString(undefined, { month: "short", day: "numeric" })} – ${sunday.toLocaleDateString(undefined, { month: "short", day: "numeric", year: "numeric" })}`;
}

// ─── Merged rows (single-employee view) ──────────────────────────────────────

type MergedRow =
  | { kind: "log"; data: AdminTimeLog }
  | { kind: "vacation"; data: AdminVacationDay }
  | { kind: "holiday"; data: PublicHoliday }
  | { kind: "week-subtotal"; label: string; hours: number };

const mergedRows = computed<MergedRow[] | null>(() => {
  if (selectedEmployeeId.value === "all") return null;

  const logDates = new Set(allLogs.value.map((l) => l.date));
  const today = toLocalDateStr(new Date());

  const effectiveFrom = dateFrom.value || (allLogs.value.length > 0
    ? [...allLogs.value].sort((a, b) => a.date.localeCompare(b.date))[0].date
    : null);
  const effectiveTo = dateTo.value || today;

  type Entry = { date: string } & (
    | { kind: "log"; data: AdminTimeLog }
    | { kind: "vacation"; data: AdminVacationDay }
    | { kind: "holiday"; data: PublicHoliday }
  );

  const entries: Entry[] = [
    ...allLogs.value.map((l) => ({ kind: "log" as const, date: l.date, data: l })),
    ...allVacations.value
      .filter((v) => {
        if (dateFrom.value && v.date < dateFrom.value) return false;
        if (dateTo.value && v.date > dateTo.value) return false;
        return true;
      })
      .map((v) => ({ kind: "vacation" as const, date: v.date, data: v })),
    ...allHolidays.value
      .filter((h) => {
        if (!h.isWorkingDay && (!effectiveFrom || h.date >= effectiveFrom) && h.date <= effectiveTo && !logDates.has(h.date)) return true;
        return false;
      })
      .map((h) => ({ kind: "holiday" as const, date: h.date, data: h })),
  ].sort((a, b) => b.date.localeCompare(a.date));

  const result: MergedRow[] = [];
  let currentWeek: string | null = null;
  let weekHours = 0;

  for (const entry of entries) {
    const week = getWeekKey(entry.date);
    if (currentWeek !== null && week !== currentWeek) {
      result.push({ kind: "week-subtotal", label: formatWeekLabel(currentWeek), hours: weekHours });
      weekHours = 0;
    }
    currentWeek = week;
    if (entry.kind === "log") {
      result.push({ kind: "log", data: entry.data });
      weekHours += liveHours(entry.data);
    } else if (entry.kind === "vacation") {
      result.push({ kind: "vacation", data: entry.data });
    } else {
      result.push({ kind: "holiday", data: entry.data });
    }
  }
  if (currentWeek !== null) {
    result.push({ kind: "week-subtotal", label: formatWeekLabel(currentWeek), hours: weekHours });
  }

  return result;
});

// ─── Predefined filters ───────────────────────────────────────────────────────

function setFilter(preset: "today" | "this-week" | "this-month" | "last-month") {
  const now = new Date();
  const today = toLocalDateStr(now);
  if (preset === "today") {
    dateFrom.value = today;
    dateTo.value = today;
  } else if (preset === "this-week") {
    dateFrom.value = getWeekKey(today);
    dateTo.value = today;
  } else if (preset === "this-month") {
    dateFrom.value = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-01`;
    dateTo.value = today;
  } else {
    const lastMonthStart = new Date(now.getFullYear(), now.getMonth() - 1, 1);
    const lastMonthEnd = new Date(now.getFullYear(), now.getMonth(), 0);
    dateFrom.value = toLocalDateStr(lastMonthStart);
    dateTo.value = toLocalDateStr(lastMonthEnd);
  }
}

const activePreset = computed(() => {
  const now = new Date();
  const today = toLocalDateStr(now);
  const lastMonthStart = new Date(now.getFullYear(), now.getMonth() - 1, 1);
  const lastMonthEnd = new Date(now.getFullYear(), now.getMonth(), 0);
  if (dateFrom.value === today && dateTo.value === today) return "today";
  if (dateFrom.value === getWeekKey(today) && dateTo.value === today) return "this-week";
  if (
    dateFrom.value === `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-01` &&
    dateTo.value === today
  )
    return "this-month";
  if (
    dateFrom.value === toLocalDateStr(lastMonthStart) &&
    dateTo.value === toLocalDateStr(lastMonthEnd)
  )
    return "last-month";
  return null;
});

// ─── Fetch ────────────────────────────────────────────────────────────────────

// `silent` is used by the background refresh: keep the current page and skip the
// skeleton/toast, so the table doesn't jump around while the admin is reading it.
const fetchLogs = async ({ silent = false } = {}) => {
  if (!silent) {
    loading.value = true;
    currentPage.value = 1;
  }
  try {
    const userId = selectedEmployeeId.value === "all" ? undefined : selectedEmployeeId.value;

    const fromYear = dateFrom.value ? parseInt(dateFrom.value.slice(0, 4)) : new Date().getFullYear();
    const toYear = dateTo.value ? parseInt(dateTo.value.slice(0, 4)) : new Date().getFullYear();
    const years: number[] = [];
    for (let y = fromYear; y <= toYear; y++) years.push(y);

    const [[logs, vacations], holidayArrays] = await Promise.all([
      Promise.all([
        adminService.getAllTimeLogs({
          userId,
          dateFrom: dateFrom.value || undefined,
          dateTo: dateTo.value || undefined,
        }),
        userId
          ? adminService.getAllVacationDays({ userId })
          : Promise.resolve([] as AdminVacationDay[]),
      ]),
      Promise.all(years.map((y) => holidayService.getHolidays(y))),
    ]);
    allLogs.value = logs;
    allVacations.value = vacations;
    allHolidays.value = holidayArrays.flat();
    currentPage.value = Math.min(currentPage.value, totalPages.value);
  } catch {
    if (!silent) toast.error("Failed to load time logs");
  } finally {
    loading.value = false;
  }
};

function formatFlexHours(h: number): string {
  const abs = Math.abs(h);
  const hrs = Math.floor(abs);
  const min = Math.round((abs - hrs) * 60);
  const sign = h < 0 ? "-" : "+";
  return `${sign}${hrs}h${min.toString().padStart(2, "0")}m`;
}

// ─── Summary cards ───────────────────────────────────────────────────────────

const summary = ref<TimeLogSummary | null>(null);
const summaryLoading = ref(false);
let summaryRequest = 0;

const fetchSummary = async ({ silent = false } = {}) => {
  const request = ++summaryRequest;
  if (!silent) summaryLoading.value = true;
  try {
    const result = await adminService.getTimeLogSummary({
      userId: selectedEmployeeId.value === "all" ? undefined : selectedEmployeeId.value,
      dateFrom: dateFrom.value || undefined,
      dateTo: dateTo.value || undefined,
    });
    // Filters can change faster than the (heavier) summary returns — drop stale answers.
    if (request === summaryRequest) summary.value = result;
  } catch {
    if (!silent && request === summaryRequest) summary.value = null;
  } finally {
    if (request === summaryRequest) summaryLoading.value = false;
  }
};

// The summary only counts finished days; add the running time of anyone still clocked in,
// like the table's Total column does.
const workedHoursWithRunning = computed(() => {
  if (!summary.value) return null;
  const running = allLogs.value
    .filter((l) => l.hasOpenSession)
    .reduce((sum, l) => sum + liveHours(l) - (l.totalHours ?? 0), 0);
  return summary.value.workedHours + running;
});

const summaryScope = computed(() => {
  const who =
    selectedEmployeeId.value === "all"
      ? "All employees"
      : (employees.value.find((e) => e.id === selectedEmployeeId.value)?.fullName ?? "");
  const to = dateTo.value ? formatDate(dateTo.value) : "today";
  const period = dateFrom.value ? `${formatDate(dateFrom.value)} – ${to}` : dateTo.value ? `until ${to}` : "all time";
  return `${who} · ${period}`;
});

watch([selectedEmployeeId, dateFrom, dateTo], () => {
  fetchLogs();
  fetchSummary();
});

// Only periods that include today can change while the page is open.
useAutoRefresh(async () => {
  if (dateTo.value && dateTo.value < toLocalDateStr(new Date())) return;
  await Promise.all([fetchLogs({ silent: true }), fetchSummary({ silent: true })]);
});

// ─── Helpers ─────────────────────────────────────────────────────────────────

const formatDate = (dateStr: string) =>
  new Date(dateStr).toLocaleDateString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
  });

const formatTime = (t?: string) => {
  if (!t) return "—";
  const d = new Date(t);
  return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
};

// "Clear" goes back to the default view (all employees, this month) rather than all time.
const clearFilters = () => {
  const range = thisMonthRange();
  selectedEmployeeId.value = "all";
  dateFrom.value = range.from;
  dateTo.value = range.to;
};

const hasFilters = computed(() => {
  const range = thisMonthRange();
  return (
    selectedEmployeeId.value !== "all" || dateFrom.value !== range.from || dateTo.value !== range.to
  );
});

// ─── Mount ───────────────────────────────────────────────────────────────────

onMounted(async () => {
  try {
    employees.value = await adminService.getEmployees("Employee");
  } catch {
    toast.error("Failed to load employees");
  }
  const preselect = route.query.employeeId as string | undefined;
  if (preselect) {
    selectedEmployeeId.value = preselect; // the filter watcher loads logs and summary
  } else {
    fetchLogs();
    fetchSummary();
  }
});
</script>

<template>
  <div class="p-6 lg:p-8">
    <div class="max-w-6xl mx-auto">
      <!-- Header -->
      <div class="mb-8">
        <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">All Time Logs</h1>
        <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
          Overview of all employee time logs
        </p>
      </div>

      <!-- Summary cards (follow the employee and date filters below) -->
      <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-2">
        <div class="stat-card" title="Worked minus target in this period, plus manual adjustments. Carry-overs from settlements are not included.">
          <p class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">
            Flex balance
          </p>
          <p class="text-3xl font-bold flex items-center gap-1.5">
            <span v-if="summaryLoading || !summary" class="animate-pulse text-slate-300 dark:text-slate-600">--</span>
            <template v-else>
              <component
                :is="summary.flexHours >= 0 ? TrendingUpIcon : TrendingDownIcon"
                class="size-5 shrink-0"
                :class="summary.flexHours >= 0 ? 'text-emerald-500' : 'text-rose-500'"
              />
              <span
                :class="
                  summary.flexHours >= 0
                    ? 'text-emerald-600 dark:text-emerald-400'
                    : 'text-rose-600 dark:text-rose-400'
                "
              >
                {{ formatFlexHours(summary.flexHours) }}
              </span>
            </template>
          </p>
        </div>
        <div class="stat-card">
          <p class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">
            Hours worked
          </p>
          <p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
            <span v-if="summaryLoading || workedHoursWithRunning === null" class="animate-pulse text-slate-300 dark:text-slate-600">--</span>
            <span v-else>{{ workedHoursWithRunning.toFixed(2) }}h</span>
          </p>
        </div>
        <div class="stat-card">
          <p class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">
            WFH days
          </p>
          <p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
            <span v-if="summaryLoading || !summary" class="animate-pulse text-slate-300 dark:text-slate-600">--</span>
            <span v-else>{{ summary.wfhDays }}</span>
          </p>
        </div>
      </div>
      <p class="text-xs text-muted-foreground mb-6">{{ summaryScope }}</p>

      <!-- Filters -->
      <div class="card p-4 mb-3 flex flex-wrap items-end gap-3">
        <div class="flex-1 min-w-[180px] space-y-1.5">
          <Label>Employee</Label>
          <Select v-model="selectedEmployeeId">
            <SelectTrigger class="w-full">
              <SelectValue placeholder="All employees" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All employees</SelectItem>
              <SelectItem v-for="emp in employees" :key="emp.id" :value="emp.id">
                {{ emp.fullName }}
                <span v-if="emp.isDisabled" class="text-slate-400 dark:text-slate-500">(disabled)</span>
              </SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div class="flex-1 min-w-[150px] space-y-1.5">
          <Label>From</Label>
          <Input v-model="dateFrom" type="date" class="cursor-pointer" />
        </div>
        <div class="flex-1 min-w-[150px] space-y-1.5">
          <Label>To</Label>
          <Input v-model="dateTo" type="date" class="cursor-pointer" />
        </div>
        <Button v-if="hasFilters" variant="outline" @click="clearFilters" class="shrink-0">
          Clear filters
        </Button>
      </div>

      <!-- Quick filter chips -->
      <div class="flex flex-wrap gap-2 mb-4">
        <button
          v-for="chip in [
            { label: 'Today', value: 'today' },
            { label: 'This week', value: 'this-week' },
            { label: 'This month', value: 'this-month' },
            { label: 'Last month', value: 'last-month' },
          ]"
          :key="chip.value"
          :class="[
            'px-3 py-1.5 rounded-full text-xs font-medium transition-colors cursor-pointer',
            activePreset === chip.value
              ? 'bg-primary text-primary-foreground'
              : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 hover:bg-slate-200 dark:hover:bg-slate-700',
          ]"
          @click="setFilter(chip.value as 'today' | 'this-week' | 'this-month' | 'last-month')"
        >
          {{ chip.label }}
        </button>
      </div>

      <!-- Table -->
      <div class="card overflow-hidden">
        <!-- Loading skeleton -->
        <div v-if="loading" class="divide-y divide-slate-100 dark:divide-slate-800">
          <div v-for="i in 6" :key="i" class="flex items-center gap-4 px-4 py-3.5">
            <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-28 animate-pulse" />
            <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-20 animate-pulse" />
            <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-24 animate-pulse" />
            <div class="ml-auto h-5 bg-slate-200 dark:bg-slate-700 rounded w-12 animate-pulse" />
          </div>
        </div>

        <Table v-else>
          <TableHeader>
            <TableRow>
              <TableHead>Employee</TableHead>
              <TableHead>Date</TableHead>
              <TableHead>Timeline</TableHead>
              <TableHead>Total</TableHead>
              <TableHead>WFH</TableHead>
              <TableHead>Description</TableHead>
            </TableRow>
          </TableHeader>

          <!-- Single-employee view: merged rows with vacation days + week subtotals -->
          <TableBody v-if="mergedRows !== null">
            <TableEmpty v-if="mergedRows.length === 0" :colspan="6">
              <ClockIcon class="size-8 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
              <p class="text-slate-500 dark:text-slate-400">No time logs found.</p>
            </TableEmpty>
            <template v-else v-for="(row, i) in mergedRows" :key="i">
              <!-- Regular time log row -->
              <TableRow v-if="row.kind === 'log'">
                <TableCell>
                  <p class="font-medium text-slate-900 dark:text-slate-100 text-sm">
                    {{ row.data.employeeName }}
                  </p>
                  <p class="text-xs text-slate-400 dark:text-slate-500">{{ row.data.employeeEmail }}</p>
                </TableCell>
                <TableCell class="font-medium text-slate-900 dark:text-slate-100">
                  {{ formatDate(row.data.date) }}
                </TableCell>
                <TableCell class="font-mono text-xs text-slate-600 dark:text-slate-400">
                  <div v-if="row.data.sessions.length" class="flex flex-col gap-0.5">
                    <div
                      v-for="(session, si) in row.data.sessions"
                      :key="si"
                      class="flex items-center gap-1 whitespace-nowrap"
                      :class="session.status === 'Invalidated' ? 'line-through opacity-40' : ''"
                    >
                      <template v-if="session.breaks.length">
                        {{ formatTime(session.clockIn) }} → {{ formatTime(session.breaks[0].breakStart) }}
                        <CoffeeIcon class="shrink-0 size-3 text-amber-400" />
                        {{ formatTime(session.breaks[session.breaks.length - 1].breakEnd) }} → {{ formatTime(session.clockOut) }}
                      </template>
                      <template v-else>
                        {{ formatTime(session.clockIn) }} → {{ formatTime(session.clockOut) }}
                      </template>
                    </div>
                  </div>
                  <span v-else class="text-slate-400">—</span>
                </TableCell>
                <TableCell>
                  <span
                    class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md text-xs font-semibold bg-primary/10 text-primary"
                    :title="row.data.hasOpenSession ? 'Still clocked in — running total' : undefined"
                  >
                    <span
                      v-if="row.data.hasOpenSession"
                      class="size-1.5 rounded-full"
                      :class="isOnBreak(row.data) ? 'bg-amber-500' : 'bg-emerald-500 animate-pulse'"
                    />
                    {{ liveHours(row.data).toFixed(2) }}h
                  </span>
                </TableCell>
                <TableCell>
                  <span v-if="row.data.workedFromHome" class="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium bg-emerald-50 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-300">
                    WFH
                  </span>
                  <span v-else class="text-slate-400 text-xs">—</span>
                </TableCell>
                <TableCell class="text-slate-600 dark:text-slate-400 text-sm max-w-[180px]">
                  <div v-if="row.data.description" class="flex items-center gap-1.5 min-w-0">
                    <span
                      class="truncate"
                      :ref="(el) => checkDescOverflow(el as Element | null, `${row.data.userId}-${row.data.date}`)"
                    >{{ row.data.description }}</span>
                    <button
                      v-if="descOverflow[`${row.data.userId}-${row.data.date}`]"
                      class="shrink-0 cursor-pointer text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 transition-colors"
                      title="View full description"
                      @click="openDescription(row.data)"
                    >
                      <MessageSquareTextIcon class="size-3.5" />
                    </button>
                  </div>
                  <span v-else class="text-slate-400">—</span>
                </TableCell>
              </TableRow>

              <!-- Holiday row -->
              <TableRow
                v-else-if="row.kind === 'holiday'"
                class="bg-sky-50/40 dark:bg-sky-950/20 hover:bg-sky-50/70 dark:hover:bg-sky-950/30"
              >
                <TableCell :colspan="2" class="font-medium text-slate-900 dark:text-slate-100">
                  {{ formatDate(row.data.date) }}
                </TableCell>
                <TableCell :colspan="4">
                  <div class="flex items-center gap-2">
                    <StarIcon class="size-3.5 text-sky-500 shrink-0" />
                    <span class="text-sm text-slate-600 dark:text-slate-400">{{ row.data.name }}</span>
                    <span class="text-xs text-slate-400 dark:text-slate-500">Public holiday</span>
                  </div>
                </TableCell>
              </TableRow>

              <!-- Vacation day row -->
              <TableRow
                v-else-if="row.kind === 'vacation'"
                class="bg-violet-50/40 dark:bg-violet-950/20 hover:bg-violet-50/70 dark:hover:bg-violet-950/30"
              >
                <TableCell>
                  <p class="font-medium text-slate-900 dark:text-slate-100 text-sm">
                    {{ row.data.employeeName }}
                  </p>
                </TableCell>
                <TableCell class="font-medium text-slate-900 dark:text-slate-100">
                  {{ formatDate(row.data.date) }}
                </TableCell>
                <TableCell :colspan="4">
                  <div class="flex items-center gap-2">
                    <CalendarIcon class="size-3.5 text-violet-500 shrink-0" />
                    <div
                      class="w-2 h-2 rounded-full shrink-0 ring-1 ring-black/10"
                      :style="{ backgroundColor: row.data.vacationTypeColor ?? '#6366f1' }"
                    />
                    <span class="text-sm text-slate-600 dark:text-slate-400">
                      {{ row.data.vacationTypeName }}
                    </span>
                    <span class="text-xs text-slate-400 dark:text-slate-500">
                      {{ row.data.amount === 1 ? "Full day" : "Half day" }}
                    </span>
                  </div>
                </TableCell>
              </TableRow>

              <!-- Week subtotal row -->
              <TableRow
                v-else-if="row.kind === 'week-subtotal'"
                class="bg-slate-50 dark:bg-slate-800/60 hover:bg-slate-100 dark:hover:bg-slate-800/80"
              >
                <TableCell :colspan="6" class="py-2">
                  <div class="flex items-center justify-between px-1">
                    <span class="text-xs text-slate-500 dark:text-slate-400">{{ row.label }}</span>
                    <span class="text-xs font-semibold text-slate-700 dark:text-slate-300">
                      {{ row.hours.toFixed(2) }}h
                    </span>
                  </div>
                </TableCell>
              </TableRow>
            </template>
          </TableBody>

          <!-- All-employees view: paginated -->
          <TableBody v-else>
            <TableEmpty v-if="allLogs.length === 0" :colspan="6">
              <ClockIcon class="size-8 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
              <p class="text-slate-500 dark:text-slate-400">No time logs found.</p>
            </TableEmpty>
            <TableRow v-for="log in paginatedLogs" :key="`${log.userId}-${log.date}`">
              <TableCell>
                <p class="font-medium text-slate-900 dark:text-slate-100 text-sm">
                  {{ log.employeeName }}
                </p>
                <p class="text-xs text-slate-400 dark:text-slate-500">{{ log.employeeEmail }}</p>
              </TableCell>
              <TableCell class="font-medium text-slate-900 dark:text-slate-100">
                {{ formatDate(log.date) }}
              </TableCell>
              <TableCell class="font-mono text-xs text-slate-600 dark:text-slate-400">
                <div v-if="log.sessions.length" class="flex flex-col gap-0.5">
                  <div
                    v-for="(session, si) in log.sessions"
                    :key="si"
                    class="flex items-center gap-1 whitespace-nowrap"
                    :class="session.status === 'Invalidated' ? 'line-through opacity-40' : ''"
                  >
                    <template v-if="session.breaks.length">
                      {{ formatTime(session.clockIn) }} → {{ formatTime(session.breaks[0].breakStart) }}
                      <CoffeeIcon class="shrink-0 size-3 text-amber-400" />
                      {{ formatTime(session.breaks[session.breaks.length - 1].breakEnd) }} → {{ formatTime(session.clockOut) }}
                    </template>
                    <template v-else>
                      {{ formatTime(session.clockIn) }} → {{ formatTime(session.clockOut) }}
                    </template>
                  </div>
                </div>
                <span v-else class="text-slate-400">—</span>
              </TableCell>
              <TableCell>
                <span
                  class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md text-xs font-semibold bg-primary/10 text-primary"
                  :title="log.hasOpenSession ? 'Still clocked in — running total' : undefined"
                >
                  <span
                    v-if="log.hasOpenSession"
                    class="size-1.5 rounded-full"
                    :class="isOnBreak(log) ? 'bg-amber-500' : 'bg-emerald-500 animate-pulse'"
                  />
                  {{ liveHours(log).toFixed(2) }}h
                </span>
              </TableCell>
              <TableCell>
                <span v-if="log.workedFromHome" class="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium bg-emerald-50 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-300">
                  WFH
                </span>
                <span v-else class="text-slate-400 text-xs">—</span>
              </TableCell>
              <TableCell class="text-slate-600 dark:text-slate-400 text-sm max-w-[180px]">
                <div v-if="log.description" class="flex items-center gap-1.5 min-w-0">
                  <span
                    class="truncate"
                    :ref="(el) => checkDescOverflow(el as Element | null, `${log.userId}-${log.date}`)"
                  >{{ log.description }}</span>
                  <button
                    v-if="descOverflow[`${log.userId}-${log.date}`]"
                    class="shrink-0 cursor-pointer text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 transition-colors"
                    title="View full description"
                    @click="openDescription(log)"
                  >
                    <MessageSquareTextIcon class="size-3.5" />
                  </button>
                </div>
                <span v-else class="text-slate-400">—</span>
              </TableCell>
            </TableRow>
          </TableBody>
        </Table>

        <!-- Pagination (all-employees view only) -->
        <div
          v-if="!loading && mergedRows === null && totalPages > 1"
          class="flex items-center justify-between px-4 py-3 border-t border-slate-100 dark:border-slate-800"
        >
          <p class="text-xs text-slate-500 dark:text-slate-400">
            Showing {{ (currentPage - 1) * pageSize + 1 }}–{{
              Math.min(currentPage * pageSize, allLogs.length)
            }}
            of {{ allLogs.length }}
          </p>
          <div class="flex items-center gap-1">
            <Button
              variant="ghost"
              size="icon"
              class="size-7"
              :disabled="currentPage === 1"
              @click="currentPage--"
            >
              <ChevronLeftIcon class="size-3.5" />
            </Button>
            <span class="text-xs text-slate-600 dark:text-slate-400 px-2"
              >{{ currentPage }} / {{ totalPages }}</span
            >
            <Button
              variant="ghost"
              size="icon"
              class="size-7"
              :disabled="currentPage === totalPages"
              @click="currentPage++"
            >
              <ChevronRightIcon class="size-3.5" />
            </Button>
          </div>
        </div>
      </div>
    </div>
  </div>

  <!-- Description dialog -->
  <Dialog v-model:open="descriptionDialog.open">
    <DialogContent class="max-w-md" @open-auto-focus.prevent>
      <DialogHeader>
        <DialogTitle>Description</DialogTitle>
        <DialogDescription>
          {{ descriptionDialog.employee }} &mdash; {{ descriptionDialog.date }}
        </DialogDescription>
      </DialogHeader>
      <p class="text-sm text-slate-700 dark:text-slate-300 leading-relaxed whitespace-pre-wrap">
        {{ descriptionDialog.text }}
      </p>
    </DialogContent>
  </Dialog>
</template>
