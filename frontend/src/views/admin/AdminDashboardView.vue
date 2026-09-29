<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useRouter } from "vue-router";
import { adminService, type AdminTimeLog, type Employee } from "../../services/adminService";
import type { OvertimeResultDto } from "../../services/workSessionService";
import { useAppToast } from "@/composables/useAppToast";
import { useLiveHours } from "@/composables/useLiveHours";
import { useAutoRefresh } from "@/composables/useAutoRefresh";
import { employeeColor, employeeColorWash } from "@/lib/employeeColors";
import {
  Table,
  TableBody,
  TableCell,
  TableEmpty,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { UsersIcon } from "lucide-vue-next";

const toast = useAppToast();
const router = useRouter();

const todayLogs = ref<AdminTimeLog[]>([]);
const employees = ref<Employee[]>([]);
const overtimeByUserId = ref(new Map<string, OvertimeResultDto>());
const onLeaveToday = ref(new Set<string>());
const loading = ref(false);

// ─── Today helpers ────────────────────────────────────────────────────────────

const localDateStr = (d: Date) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;

// Re-evaluated on every load so a dashboard left open past midnight moves on to the new day.
const todayStr = ref(localDateStr(new Date()));

const todayLabel = computed(() =>
  new Date(`${todayStr.value}T00:00:00`).toLocaleDateString(undefined, {
    weekday: "long",
    year: "numeric",
    month: "long",
    day: "numeric",
  })
);

const { liveHours } = useLiveHours();

// ─── Rows ─────────────────────────────────────────────────────────────────────

type RowStatus = "working" | "break" | "done" | "invalidated" | "missing" | "leave" | "off";

const STATUS: Record<RowStatus, { label: string; dot: string; pill: string }> = {
  working: {
    label: "Working",
    dot: "bg-emerald-500",
    pill: "bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300",
  },
  break: {
    label: "On break",
    dot: "bg-amber-500",
    pill: "bg-amber-50 text-amber-700 dark:bg-amber-950 dark:text-amber-300",
  },
  done: {
    label: "Clocked out",
    dot: "bg-slate-400 dark:bg-slate-500",
    pill: "bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300",
  },
  invalidated: {
    label: "Auto-closed",
    dot: "bg-amber-500",
    pill: "bg-amber-50 text-amber-700 dark:bg-amber-950 dark:text-amber-300",
  },
  missing: {
    label: "Not clocked in",
    dot: "bg-red-500",
    pill: "bg-red-50 text-red-700 dark:bg-red-950 dark:text-red-300",
  },
  leave: {
    label: "On leave",
    dot: "bg-violet-400",
    pill: "bg-violet-50 text-violet-700 dark:bg-violet-950 dark:text-violet-300",
  },
  off: {
    label: "Day off",
    dot: "bg-slate-300 dark:bg-slate-600",
    pill: "bg-slate-100 text-slate-500 dark:bg-slate-800 dark:text-slate-400",
  },
};

const initials = (name: string) =>
  name
    .split(" ")
    .map((n) => n[0])
    .join("")
    .substring(0, 2)
    .toUpperCase();

const statusFor = (emp: Employee, log: AdminTimeLog | undefined): RowStatus => {
  if (log) {
    const open = log.sessions.find((s) => s.status === "Open");
    if (open) return open.breaks.some((b) => !b.breakEnd) ? "break" : "working";
    if (log.sessions.some((s) => s.status === "Closed")) return "done";
    if (log.hasInvalidatedSession) return "invalidated";
  }
  if (onLeaveToday.value.has(emp.id)) return "leave";
  if (targetToday(emp.id) === 0) return "off";
  return "missing";
};

/** Today's target hours (0 on weekends, holidays, non-working weekdays and full leave). */
const targetToday = (userId: string) =>
  overtimeByUserId.value.get(userId)?.perDay.find((d) => d.date.startsWith(todayStr.value))?.targetHours;

const rows = computed(() => {
  const logByUserId = new Map(todayLogs.value.map((l) => [l.userId, l]));
  return employees.value.map((emp) => {
    const log = logByUserId.get(emp.id);
    const sessions = log?.sessions ?? [];
    const breaks = sessions.flatMap((s) => s.breaks);
    const hasOpen = sessions.some((s) => s.status === "Open");
    const status = statusFor(emp, log);
    const worked = log ? liveHours(log) : undefined;
    const target = targetToday(emp.id);
    return {
      employee: emp,
      log,
      status,
      muted: status === "leave" || status === "off",
      worked,
      target,
      progress: worked !== undefined && target ? Math.min(1, worked / target) : undefined,
      start: sessions[0]?.clockIn,
      breakStart: breaks[0]?.breakStart,
      breakEnd: breaks[0]?.breakEnd,
      extraBreaks: Math.max(0, breaks.length - 1),
      end: hasOpen ? undefined : sessions[sessions.length - 1]?.clockOut,
      balance: overtimeByUserId.value.get(emp.id)?.runningBalanceHours,
    };
  });
});

// ─── Formatting ───────────────────────────────────────────────────────────────

const formatTime = (t?: string) => {
  if (!t) return "—";
  const d = new Date(t);
  return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
};

function formatFlexHours(h: number): string {
  const abs = Math.abs(h);
  const hrs = Math.floor(abs);
  const min = Math.round((abs - hrs) * 60);
  const sign = h < 0 ? "-" : "+";
  return `${sign}${hrs}h${min.toString().padStart(2, "0")}m`;
}

const balanceClass = (h?: number) => {
  if (h === undefined || Math.abs(h) < 0.01)
    return "bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300";
  return h > 0
    ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300"
    : "bg-red-50 text-red-700 dark:bg-red-950 dark:text-red-300";
};

const lastUpdated = ref<Date | null>(null);

// ─── Load ─────────────────────────────────────────────────────────────────────

// `silent` is used by the background refresh: no skeleton, and no toast every minute
// if the API is briefly unreachable — the last good data simply stays on screen.
const load = async ({ silent = false } = {}) => {
  if (!silent) loading.value = true;
  try {
    const today = new Date();
    const day = localDateStr(today);
    const [logs, emps, vacations] = await Promise.all([
      adminService.getAllTimeLogs({ dateFrom: day, dateTo: day }),
      // Admins don't track time, so they don't belong in the overview.
      adminService.getEmployees("Employee"),
      adminService.getAllVacationDays({ year: today.getFullYear(), month: today.getMonth() + 1 }),
    ]);
    const activeEmployees = emps
      .filter((e) => !e.isDisabled)
      .sort((a, b) => a.fullName.localeCompare(b.fullName));

    const leaveTotals = new Map<string, number>();
    for (const v of vacations) {
      if (v.date !== day) continue;
      leaveTotals.set(v.userId, (leaveTotals.get(v.userId) ?? 0) + v.amount);
    }

    // One failing balance shouldn't blank the whole table — show "—" for that row instead.
    const results = await Promise.allSettled(
      activeEmployees.map((e) => adminService.getEmployeeOvertime(e.id))
    );
    const byUser = new Map<string, OvertimeResultDto>();
    results.forEach((r, i) => {
      if (r.status === "fulfilled") byUser.set(activeEmployees[i].id, r.value);
    });

    // Swap everything in at once so the table never shows a mix of old and new data.
    todayStr.value = day;
    todayLogs.value = logs.filter((l) => l.date.split("T")[0] === day);
    employees.value = activeEmployees;
    onLeaveToday.value = new Set([...leaveTotals].filter(([, a]) => a >= 1).map(([id]) => id));
    overtimeByUserId.value = byUser;
    lastUpdated.value = new Date();
    if (!silent && results.some((r) => r.status === "rejected")) toast.error("Failed to load some balances");
  } catch {
    if (!silent) toast.error("Failed to load dashboard data");
  } finally {
    loading.value = false;
  }
};

onMounted(() => load());
useAutoRefresh(() => load({ silent: true }));
</script>

<template>
  <div class="p-6 lg:p-8">
    <div class="max-w-6xl mx-auto">
      <!-- Header -->
      <div class="mb-6 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">Dashboard</h1>
          <p class="text-sm text-muted-foreground mt-0.5">{{ todayLabel }}</p>
        </div>
        <div
          v-if="lastUpdated"
          class="inline-flex items-center gap-2 rounded-full border border-border bg-card px-3 py-1 text-xs text-muted-foreground shadow-sm"
          title="Refreshes automatically every minute"
        >
          <span class="relative flex size-2">
            <span class="absolute inline-flex size-full rounded-full bg-emerald-400 opacity-60 animate-ping" />
            <span class="relative inline-flex size-2 rounded-full bg-emerald-500" />
          </span>
          Live · updated {{ formatTime(lastUpdated.toISOString()) }}
        </div>
      </div>

      <div class="card overflow-hidden">
        <!-- Loading skeleton -->
        <div v-if="loading" class="divide-y divide-border">
          <div v-for="i in 6" :key="i" class="flex items-center gap-4 px-5 py-4">
            <div class="size-9 rounded-full bg-muted animate-pulse" />
            <div class="space-y-1.5">
              <div class="h-3 bg-muted rounded w-32 animate-pulse" />
              <div class="h-3 bg-muted rounded w-16 animate-pulse" />
            </div>
            <div class="ml-auto h-3 bg-muted rounded w-72 animate-pulse" />
          </div>
        </div>

        <Table v-else>
          <TableHeader class="bg-muted/40">
            <TableRow class="hover:bg-transparent">
              <TableHead class="head pl-5">Employee</TableHead>
              <TableHead class="head">Start</TableHead>
              <TableHead class="head">Break start</TableHead>
              <TableHead class="head">Break end</TableHead>
              <TableHead class="head">End</TableHead>
              <TableHead class="head">Worked today</TableHead>
              <TableHead class="head pr-5 text-right" title="Flex balance for the current month">
                Overtime (month)
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <TableEmpty v-if="rows.length === 0" :colspan="7">
              <UsersIcon class="size-8 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
              <p class="text-muted-foreground">No active employees.</p>
            </TableEmpty>
            <TableRow
              v-for="row in rows"
              :key="row.employee.id"
              class="group cursor-pointer"
              @click="router.push({ name: 'admin-time-logs', query: { employeeId: row.employee.id } })"
            >
              <!-- Employee: identity-colored avatar with status dot, name and status pill -->
              <TableCell class="py-3 pl-5">
                <div class="flex items-center gap-3">
                  <div class="relative shrink-0">
                    <div
                      class="size-9 rounded-full flex items-center justify-center text-xs font-semibold"
                      :style="{
                        backgroundColor: employeeColorWash(row.employee.id),
                        color: employeeColor(row.employee.id),
                      }"
                    >
                      {{ initials(row.employee.fullName) }}
                    </div>
                    <span
                      class="absolute -bottom-0.5 -right-0.5 size-3 rounded-full ring-2 ring-card"
                      :class="STATUS[row.status].dot"
                    >
                      <span
                        v-if="row.status === 'working'"
                        class="absolute inset-0 rounded-full bg-emerald-400 opacity-60 animate-ping"
                      />
                    </span>
                  </div>
                  <div class="min-w-0">
                    <p
                      class="text-sm font-medium truncate group-hover:text-primary transition-colors"
                      :class="row.muted ? 'text-muted-foreground' : 'text-slate-900 dark:text-slate-100'"
                    >
                      {{ row.employee.fullName }}
                    </p>
                    <span
                      class="mt-0.5 inline-flex items-center px-1.5 py-px rounded text-[11px] font-medium"
                      :class="STATUS[row.status].pill"
                    >
                      {{ STATUS[row.status].label }}
                    </span>
                  </div>
                </div>
              </TableCell>

              <!-- Times -->
              <TableCell class="data" :class="{ empty: !row.start }">{{ formatTime(row.start) }}</TableCell>
              <TableCell class="data" :class="{ empty: !row.breakStart }">{{ formatTime(row.breakStart) }}</TableCell>
              <TableCell class="data" :class="{ empty: !row.breakEnd }">
                <span v-if="row.breakStart && !row.breakEnd" class="text-amber-600 dark:text-amber-400">now</span>
                <template v-else>{{ formatTime(row.breakEnd) }}</template>
                <span
                  v-if="row.extraBreaks"
                  class="ml-1.5 inline-flex items-center rounded bg-muted px-1 text-[11px] text-muted-foreground"
                  :title="`${row.extraBreaks} more break(s) today`"
                  >+{{ row.extraBreaks }}</span
                >
              </TableCell>
              <TableCell class="data" :class="{ empty: !row.end }">
                <span v-if="row.log?.hasOpenSession" class="text-emerald-600 dark:text-emerald-400">now</span>
                <template v-else>{{ formatTime(row.end) }}</template>
              </TableCell>

              <!-- Worked today, with progress toward today's target -->
              <TableCell class="min-w-36">
                <div v-if="row.worked !== undefined" class="space-y-1">
                  <p class="data">
                    {{ row.worked.toFixed(2) }}h
                    <span v-if="row.target" class="text-muted-foreground">/ {{ row.target }}h</span>
                  </p>
                  <div v-if="row.progress !== undefined" class="h-1 w-full max-w-28 rounded-full bg-muted overflow-hidden">
                    <div
                      class="h-full rounded-full transition-[width] duration-500"
                      :class="row.progress >= 1 ? 'bg-emerald-500' : 'bg-primary'"
                      :style="{ width: `${row.progress * 100}%` }"
                    />
                  </div>
                </div>
                <span v-else class="data empty">—</span>
              </TableCell>

              <!-- Monthly flex balance -->
              <TableCell class="pr-5 text-right">
                <span
                  v-if="row.balance !== undefined"
                  class="data inline-flex items-center rounded-md px-2 py-0.5 text-xs font-semibold"
                  :class="balanceClass(row.balance)"
                >
                  {{ formatFlexHours(row.balance) }}
                </span>
                <span v-else class="data empty">—</span>
              </TableCell>
            </TableRow>
          </TableBody>
        </Table>
      </div>
    </div>
  </div>
</template>

<style scoped>
.head {
  font-size: 0.6875rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--muted-foreground);
}

.data {
  font-family: var(--font-data);
  font-size: 0.8125rem;
  font-variant-numeric: tabular-nums;
}

.empty {
  color: color-mix(in srgb, var(--muted-foreground) 45%, transparent);
}
</style>
