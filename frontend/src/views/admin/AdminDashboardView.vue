<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from "vue";
import { useRouter } from "vue-router";
import {
  adminService,
  type AdminTimeLog,
  type AdminSession,
  type Employee,
} from "../../services/adminService";
import type { OvertimeResultDto } from "../../services/workSessionService";
import { useAppToast } from "@/composables/useAppToast";
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

const todayStr = (() => {
  const d = new Date();
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
})();

const todayLabel = new Date().toLocaleDateString(undefined, {
  weekday: "long",
  year: "numeric",
  month: "long",
  day: "numeric",
});

// ─── Live hours ───────────────────────────────────────────────────────────────

// The backend only counts closed sessions in totalHours, so an employee who is still
// clocked in would show 0h. Add the running time of open sessions client-side.
const now = ref(Date.now());
let nowInterval: ReturnType<typeof setInterval> | null = null;

const openSessionHours = (session: AdminSession) => {
  const breakMs = session.breaks.reduce((sum, b) => {
    const end = b.breakEnd ? new Date(b.breakEnd).getTime() : now.value;
    return sum + Math.max(0, end - new Date(b.breakStart).getTime());
  }, 0);
  const elapsedMs = now.value - new Date(session.clockIn).getTime();
  return Math.max(0, elapsedMs - breakMs) / 3_600_000;
};

const liveHours = (log: AdminTimeLog) =>
  (log.totalHours ?? 0) +
  log.sessions.filter((s) => s.status === "Open").reduce((sum, s) => sum + openSessionHours(s), 0);

// ─── Rows ─────────────────────────────────────────────────────────────────────

type RowStatus = "working" | "break" | "done" | "invalidated" | "missing" | "leave" | "off";

const STATUS: Record<RowStatus, { label: string; dot: string }> = {
  working: { label: "Working", dot: "bg-emerald-500 animate-pulse" },
  break: { label: "On break", dot: "bg-amber-500" },
  done: { label: "Clocked out", dot: "bg-emerald-500" },
  invalidated: { label: "Session auto-closed", dot: "bg-amber-500" },
  missing: { label: "Not clocked in", dot: "bg-red-500" },
  leave: { label: "On leave", dot: "bg-slate-300 dark:bg-slate-600" },
  off: { label: "Day off", dot: "bg-slate-300 dark:bg-slate-600" },
};

const statusFor = (emp: Employee, log: AdminTimeLog | undefined): RowStatus => {
  if (log) {
    const open = log.sessions.find((s) => s.status === "Open");
    if (open) return open.breaks.some((b) => !b.breakEnd) ? "break" : "working";
    if (log.sessions.some((s) => s.status === "Closed")) return "done";
    if (log.hasInvalidatedSession) return "invalidated";
  }
  if (onLeaveToday.value.has(emp.id)) return "leave";
  const today = overtimeByUserId.value.get(emp.id)?.perDay.find((d) => d.date.startsWith(todayStr));
  if (today && today.targetHours === 0) return "off";
  return "missing";
};

const rows = computed(() => {
  const logByUserId = new Map(todayLogs.value.map((l) => [l.userId, l]));
  return employees.value.map((emp) => {
    const log = logByUserId.get(emp.id);
    const sessions = log?.sessions ?? [];
    const breaks = sessions.flatMap((s) => s.breaks);
    const hasOpen = sessions.some((s) => s.status === "Open");
    return {
      employee: emp,
      log,
      status: statusFor(emp, log),
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
  if (h === undefined || Math.abs(h) < 0.01) return "text-slate-500 dark:text-slate-400";
  return h > 0 ? "text-emerald-600 dark:text-emerald-400" : "text-red-600 dark:text-red-400";
};

// ─── Mount ────────────────────────────────────────────────────────────────────

onUnmounted(() => {
  if (nowInterval) clearInterval(nowInterval);
});

onMounted(async () => {
  nowInterval = setInterval(() => {
    now.value = Date.now();
  }, 30_000);
  loading.value = true;
  try {
    const today = new Date();
    const [logs, emps, vacations] = await Promise.all([
      adminService.getAllTimeLogs({ dateFrom: todayStr, dateTo: todayStr }),
      // Admins don't track time, so they don't belong in the overview.
      adminService.getEmployees("Employee"),
      adminService.getAllVacationDays({ year: today.getFullYear(), month: today.getMonth() + 1 }),
    ]);
    todayLogs.value = logs.filter((l) => l.date.split("T")[0] === todayStr);
    employees.value = emps
      .filter((e) => !e.isDisabled)
      .sort((a, b) => a.fullName.localeCompare(b.fullName));

    const leaveTotals = new Map<string, number>();
    for (const v of vacations) {
      if (v.date !== todayStr) continue;
      leaveTotals.set(v.userId, (leaveTotals.get(v.userId) ?? 0) + v.amount);
    }
    onLeaveToday.value = new Set([...leaveTotals].filter(([, a]) => a >= 1).map(([id]) => id));

    // One failing balance shouldn't blank the whole table — show "—" for that row instead.
    const results = await Promise.allSettled(
      employees.value.map((e) => adminService.getEmployeeOvertime(e.id))
    );
    const byUser = new Map<string, OvertimeResultDto>();
    results.forEach((r, i) => {
      if (r.status === "fulfilled") byUser.set(employees.value[i].id, r.value);
    });
    overtimeByUserId.value = byUser;
    if (results.some((r) => r.status === "rejected")) toast.error("Failed to load some balances");
  } catch {
    toast.error("Failed to load dashboard data");
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <div class="p-6 lg:p-8">
    <div class="max-w-6xl mx-auto">
      <!-- Header -->
      <div class="mb-6">
        <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">Dashboard</h1>
        <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
          {{ todayLabel }}
        </p>
      </div>

      <div class="card overflow-hidden">
        <!-- Loading skeleton -->
        <div v-if="loading" class="divide-y divide-slate-100 dark:divide-slate-800">
          <div v-for="i in 6" :key="i" class="flex items-center gap-4 px-4 py-3">
            <div class="size-2 rounded-full bg-slate-200 dark:bg-slate-700 animate-pulse" />
            <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-32 animate-pulse" />
            <div class="ml-auto h-3 bg-slate-200 dark:bg-slate-700 rounded w-64 animate-pulse" />
          </div>
        </div>

        <Table v-else>
          <TableHeader>
            <TableRow>
              <TableHead>Employee</TableHead>
              <TableHead>Start</TableHead>
              <TableHead>Break start</TableHead>
              <TableHead>Break end</TableHead>
              <TableHead>End</TableHead>
              <TableHead class="text-right">Worked</TableHead>
              <TableHead class="text-right" title="Flex balance for the current month">
                Overtime (month)
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <TableEmpty v-if="rows.length === 0" :colspan="7">
              <UsersIcon class="size-8 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
              <p class="text-slate-500 dark:text-slate-400">No active employees.</p>
            </TableEmpty>
            <TableRow
              v-for="row in rows"
              :key="row.employee.id"
              class="cursor-pointer"
              @click="router.push({ name: 'admin-time-logs', query: { employeeId: row.employee.id } })"
            >
              <TableCell>
                <div class="flex items-center gap-2.5">
                  <span
                    class="size-2.5 rounded-full shrink-0"
                    :class="STATUS[row.status].dot"
                    :title="STATUS[row.status].label"
                  />
                  <div class="min-w-0">
                    <p class="text-sm font-medium text-slate-900 dark:text-slate-100 truncate">
                      {{ row.employee.fullName }}
                    </p>
                    <p class="text-xs text-slate-400 dark:text-slate-500">
                      {{ STATUS[row.status].label }}
                    </p>
                  </div>
                </div>
              </TableCell>
              <TableCell class="font-mono text-sm tabular-nums">{{ formatTime(row.start) }}</TableCell>
              <TableCell class="font-mono text-sm tabular-nums">{{ formatTime(row.breakStart) }}</TableCell>
              <TableCell class="font-mono text-sm tabular-nums">
                {{ formatTime(row.breakEnd) }}
                <span
                  v-if="row.extraBreaks"
                  class="ml-1 text-xs text-slate-400 dark:text-slate-500"
                  :title="`${row.extraBreaks} more break(s) today`"
                  >+{{ row.extraBreaks }}</span
                >
              </TableCell>
              <TableCell class="font-mono text-sm tabular-nums">{{ formatTime(row.end) }}</TableCell>
              <TableCell class="text-right font-mono text-sm tabular-nums">
                {{ row.log ? `${liveHours(row.log).toFixed(2)}h` : "—" }}
              </TableCell>
              <TableCell
                class="text-right font-mono text-sm font-semibold tabular-nums"
                :class="balanceClass(row.balance)"
              >
                {{ row.balance === undefined ? "—" : formatFlexHours(row.balance) }}
              </TableCell>
            </TableRow>
          </TableBody>
        </Table>
      </div>
    </div>
  </div>
</template>
