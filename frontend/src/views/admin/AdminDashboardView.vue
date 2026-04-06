<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useRouter } from "vue-router";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import UpcomingVacationsWidget from "@/components/UpcomingVacationsWidget.vue";
import {
  adminService,
  type AdminTimeLog,
  type Employee,
  type AdminVacationDay,
} from "../../services/adminService";
import { useAppToast } from "@/composables/useAppToast";
import { ClockIcon, CheckCircleIcon, CalendarIcon } from "lucide-vue-next";

const toast = useAppToast();
const router = useRouter();

const allLogs = ref<AdminTimeLog[]>([]);
const employees = ref<Employee[]>([]);
const upcomingVacations = ref<AdminVacationDay[]>([]);
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

// ─── Derived data ─────────────────────────────────────────────────────────────

const todayLogs = computed(() => allLogs.value.filter((l) => l.date.split("T")[0] === todayStr));

const employeesLoggedToday = computed(() => {
  const ids = new Set(todayLogs.value.map((l) => l.userId));
  return employees.value.filter((e) => ids.has(e.id));
});

const employeesNotLoggedToday = computed(() => {
  const ids = new Set(todayLogs.value.map((l) => l.userId));
  return employees.value.filter((e) => !ids.has(e.id));
});

// ─── Helpers ──────────────────────────────────────────────────────────────────

const formatTime = (t?: string) => (t ? t.substring(0, 5) : "—");

const formatBreak = (log: AdminTimeLog) =>
  log.breakStart && log.breakEnd
    ? `${formatTime(log.breakStart)} – ${formatTime(log.breakEnd)}`
    : "—";

// ─── Upcoming vacations (next 7 days) ─────────────────────────────────────────

const upcomingDates = computed(() => {
  const today = new Date();
  const in7 = new Date(today);
  in7.setDate(today.getDate() + 7);
  const todayIso = todayStr;
  const in7Iso = (() => {
    const y = in7.getFullYear();
    const m = String(in7.getMonth() + 1).padStart(2, "0");
    const d = String(in7.getDate()).padStart(2, "0");
    return `${y}-${m}-${d}`;
  })();
  return upcomingVacations.value
    .filter((v) => v.date >= todayIso && v.date <= in7Iso)
    .sort((a, b) => a.date.localeCompare(b.date));
});

const formatUpcomingDate = (iso: string) =>
  new Date(iso).toLocaleDateString(undefined, {
    weekday: "short",
    day: "numeric",
    month: "short",
  });

// ─── Mount ────────────────────────────────────────────────────────────────────

onMounted(async () => {
  loading.value = true;
  try {
    const [logs, emps, vacations] = await Promise.all([
      adminService.getAllTimeLogs(),
      adminService.getEmployees(),
      adminService.getAllVacationDays(),
    ]);
    allLogs.value = logs;
    employees.value = emps;
    upcomingVacations.value = vacations;
  } catch {
    toast.error("Failed to load dashboard data");
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <AuthenticatedLayout>
    <div class="p-6 lg:p-8">
      <div class="max-w-6xl mx-auto">
        <!-- Header -->
        <div class="mb-8">
          <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">Dashboard</h1>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
            {{ todayLabel }}
          </p>
        </div>

        <!-- Stats -->
        <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-8">
          <div class="stat-card">
            <p
              class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1"
            >
              Total employees
            </p>
            <p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
              <span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600"
                >--</span
              >
              <span v-else>{{ employees.length }}</span>
            </p>
          </div>
          <div class="stat-card">
            <p
              class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1"
            >
              Logged today
            </p>
            <p class="text-3xl font-bold text-emerald-600 dark:text-emerald-400">
              <span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600"
                >--</span
              >
              <span v-else>{{ employeesLoggedToday.length }}</span>
            </p>
          </div>
          <div class="stat-card">
            <p
              class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1"
            >
              Not logged today
            </p>
            <p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
              <span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600"
                >--</span
              >
              <span
                v-else
                :class="
                  employeesNotLoggedToday.length > 0 ? 'text-amber-500 dark:text-amber-400' : ''
                "
              >
                {{ employeesNotLoggedToday.length }}
              </span>
            </p>
          </div>
        </div>

        <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-6">
          <!-- Today's time log entries -->
          <div class="lg:col-span-2">
            <div class="flex items-center justify-between mb-3">
              <h2 class="text-sm font-semibold text-slate-700 dark:text-slate-300">
                Today's entries
              </h2>
              <button
                @click="router.push({ name: 'admin-time-logs' })"
                class="text-xs text-indigo-600 dark:text-indigo-400 hover:underline cursor-pointer focus-visible:outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50 rounded"
              >
                View all logs →
              </button>
            </div>

            <div class="card overflow-hidden">
              <!-- Loading skeleton -->
              <div v-if="loading" class="divide-y divide-slate-100 dark:divide-slate-800">
                <div v-for="i in 4" :key="i" class="flex items-center gap-4 px-4 py-3">
                  <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-28 animate-pulse" />
                  <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-20 animate-pulse" />
                  <div
                    class="ml-auto h-5 bg-slate-200 dark:bg-slate-700 rounded w-12 animate-pulse"
                  />
                </div>
              </div>

              <!-- Empty state -->
              <div v-else-if="todayLogs.length === 0" class="text-center py-12">
                <ClockIcon class="size-8 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
                <p class="text-sm text-slate-500 dark:text-slate-400">
                  No entries logged yet today.
                </p>
              </div>

              <!-- Entries list -->
              <ul v-else class="divide-y divide-slate-100 dark:divide-slate-800">
                <li
                  v-for="log in todayLogs"
                  :key="log.id"
                  class="flex items-center gap-4 px-4 py-3 hover:bg-slate-50 dark:hover:bg-slate-800/50 transition-colors cursor-pointer"
                  @click="
                    router.push({
                      name: 'admin-time-logs',
                      query: { employeeId: log.userId },
                    })
                  "
                >
                  <div class="flex-1 min-w-0">
                    <p class="text-sm font-medium text-slate-900 dark:text-slate-100 truncate">
                      {{ log.employeeName }}
                    </p>
                    <p
                      v-if="log.description"
                      class="text-xs text-slate-400 dark:text-slate-500 truncate mt-0.5"
                    >
                      {{ log.description }}
                    </p>
                  </div>
                  <div class="text-xs text-slate-500 dark:text-slate-400 shrink-0">
                    {{ formatTime(log.startTime) }} –
                    {{ formatTime(log.endTime) }}
                    <span v-if="log.breakStart" class="ml-1 text-slate-400 dark:text-slate-500"
                      >(brk {{ formatBreak(log) }})</span
                    >
                  </div>
                  <span
                    class="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-semibold bg-indigo-50 dark:bg-indigo-950 text-indigo-700 dark:text-indigo-300 shrink-0"
                  >
                    {{ log.totalHours?.toFixed(2) ?? "0.00" }}h
                  </span>
                </li>
              </ul>
            </div>
          </div>

          <!-- Right column -->
          <div class="flex flex-col gap-6">
            <!-- Who hasn't logged -->
            <div>
              <h2 class="text-sm font-semibold text-slate-700 dark:text-slate-300 mb-3">
                Not logged today
              </h2>
              <div class="card overflow-hidden">
                <div v-if="loading" class="divide-y divide-slate-100 dark:divide-slate-800">
                  <div v-for="i in 3" :key="i" class="flex items-center gap-3 px-4 py-3">
                    <div
                      class="w-7 h-7 rounded-full bg-slate-200 dark:bg-slate-700 animate-pulse shrink-0"
                    />
                    <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-24 animate-pulse" />
                  </div>
                </div>
                <div v-else-if="employeesNotLoggedToday.length === 0" class="text-center py-8">
                  <CheckCircleIcon class="size-6 text-emerald-400 mb-1 mx-auto" />
                  <p class="text-xs text-slate-500 dark:text-slate-400">
                    Everyone has logged today.
                  </p>
                </div>
                <ul v-else class="divide-y divide-slate-100 dark:divide-slate-800">
                  <li
                    v-for="emp in employeesNotLoggedToday"
                    :key="emp.id"
                    class="flex items-center gap-3 px-4 py-3 hover:bg-slate-50 dark:hover:bg-slate-800/50 transition-colors cursor-pointer"
                    @click="
                      router.push({
                        name: 'admin-time-logs',
                        query: { employeeId: emp.id },
                      })
                    "
                  >
                    <div
                      class="w-7 h-7 rounded-full bg-slate-200 dark:bg-slate-700 flex items-center justify-center shrink-0"
                    >
                      <span class="text-xs font-bold text-slate-500 dark:text-slate-400">
                        {{
                          emp.fullName
                            .split(" ")
                            .map((n: string) => n[0])
                            .join("")
                            .substring(0, 2)
                            .toUpperCase()
                        }}
                      </span>
                    </div>
                    <span class="text-sm text-slate-700 dark:text-slate-300 truncate">{{
                      emp.fullName
                    }}</span>
                  </li>
                </ul>
              </div>
            </div>

            <!-- Upcoming vacations (next 7 days) -->
            <div>
              <div class="flex items-center justify-between mb-3">
                <h2 class="text-sm font-semibold text-slate-700 dark:text-slate-300">
                  Upcoming vacations
                </h2>
                <button
                  @click="router.push({ name: 'team-calendar' })"
                  class="text-xs text-indigo-600 dark:text-indigo-400 hover:underline cursor-pointer focus-visible:outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50 rounded"
                >
                  Calendar →
                </button>
              </div>
              <div class="card overflow-hidden">
                <div v-if="loading" class="divide-y divide-slate-100 dark:divide-slate-800">
                  <div v-for="i in 3" :key="i" class="flex items-center gap-3 px-4 py-3">
                    <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-16 animate-pulse" />
                    <div
                      class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-20 animate-pulse flex-1"
                    />
                  </div>
                </div>
                <div v-else-if="upcomingDates.length === 0" class="text-center py-8">
                  <CalendarIcon class="size-6 text-slate-300 dark:text-slate-600 mb-1 mx-auto" />
                  <p class="text-xs text-slate-500 dark:text-slate-400">
                    No vacations in the next 7 days.
                  </p>
                </div>
                <ul v-else class="divide-y divide-slate-100 dark:divide-slate-800">
                  <li
                    v-for="v in upcomingDates"
                    :key="v.id"
                    class="flex items-center gap-3 px-4 py-2.5"
                  >
                    <div
                      class="w-2.5 h-2.5 rounded-full shrink-0 ring-1 ring-black/10"
                      :style="{
                        backgroundColor: v.vacationTypeColor ?? '#6366f1',
                      }"
                    />
                    <div class="flex-1 min-w-0">
                      <p class="text-xs font-medium text-slate-800 dark:text-slate-200 truncate">
                        {{ v.employeeName }}
                      </p>
                      <p class="text-xs text-slate-400 dark:text-slate-500">
                        {{ v.vacationTypeName }}
                      </p>
                    </div>
                    <div class="text-right shrink-0">
                      <p class="text-xs text-slate-600 dark:text-slate-400">
                        {{ formatUpcomingDate(v.date) }}
                      </p>
                      <p class="text-xs text-slate-400 dark:text-slate-500">
                        {{ v.amount === 0.5 ? "Half day" : "Full day" }}
                      </p>
                    </div>
                  </li>
                </ul>
              </div>
            </div>
          </div>
        </div>

        <!-- Personal vacation calendar -->
        <UpcomingVacationsWidget />
      </div>
    </div>
  </AuthenticatedLayout>
</template>
