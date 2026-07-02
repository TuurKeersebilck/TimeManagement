<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import { useClockEventsStore } from "../composables/useClockEventsStore";
import { workSessionService, type WorkScheduleDto } from "../services/workSessionService";
import { vacationService } from "../services/vacationService";
import { holidayService, type PublicHoliday } from "../services/holidayService";
import UpcomingVacationsWidget from "@/components/UpcomingVacationsWidget.vue";
import {
  ClockIcon,
  CheckCircleIcon,
  AlertCircleIcon,
  CalendarDaysIcon,
  TrendingUpIcon,
  TrendingDownIcon,
  PlaneIcon,
  SunIcon,
  StarIcon,
} from "lucide-vue-next";

const router = useRouter();
const { summaries, loading, fetchSummaries } = useClockEventsStore();

const schedule = ref<WorkScheduleDto | null>(null);
const todayVacationAmount = ref<number | null>(null);
const todayVacationTypeName = ref<string | null>(null);
const todayHoliday = ref<PublicHoliday | null>(null);
const monthlyFlexHours = ref<number | null>(null);
const pageLoading = ref(true);

const isLoading = computed(() => loading.value || pageLoading.value);

function localDateString(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

const isWeekend = computed(() => {
  const day = new Date().getDay();
  return day === 0 || day === 6;
});

const todaysSummary = computed(() => {
  const today = localDateString(new Date());
  return summaries.value.find((s) => s.date === today) ?? null;
});

const hasLoggedToday = computed(() => (todaysSummary.value?.totalWorkedHours ?? 0) > 0);

const isClockedIn = computed(() => todaysSummary.value?.hasOpenSession ?? false);

// Resolve today's target hours from per-weekday schedule
const todayTargetHours = computed<number | null>(() => {
  if (!schedule.value) return null;
  const dow = new Date().getDay();
  return schedule.value.workdayTargets.find((t) => t.dayOfWeek === dow)?.hours ?? null;
});

const todayHours = computed(() => todaysSummary.value?.totalWorkedHours ?? 0);

const todayRemaining = computed(() =>
  todayTargetHours.value != null ? Math.max(0, todayTargetHours.value - todayHours.value) : null
);
const todayExceeded = computed(
  () => todayTargetHours.value != null && todayHours.value > todayTargetHours.value
);
const todayProgress = computed(() =>
  todayTargetHours.value ? Math.min((todayHours.value / todayTargetHours.value) * 100, 100) : null
);

// This-week hours from summaries
const totalHoursThisWeek = computed(() => {
  const now = new Date();
  const daysFromMonday = now.getDay() === 0 ? 6 : now.getDay() - 1;
  const weekStart = new Date(now);
  weekStart.setDate(now.getDate() - daysFromMonday);
  const weekStartStr = localDateString(weekStart);

  return summaries.value
    .filter((s) => s.date >= weekStartStr)
    .reduce((sum, s) => sum + s.totalWorkedHours, 0)
    .toFixed(2);
});

// This-month hours from summaries
const totalHoursThisMonth = computed(() => {
  const now = new Date();
  const monthStartStr = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-01`;

  return summaries.value
    .filter((s) => s.date >= monthStartStr)
    .reduce((sum, s) => sum + s.totalWorkedHours, 0)
    .toFixed(2);
});

// This-week target (Mon–Fri from schedule)
const weeklyTarget = computed<number | null>(() => {
  if (!schedule.value) return null;
  // Sun=0 Mon=1…Sat=6 → Mon=1 Tue=2 … Fri=5
  const weekdayHours = schedule.value.workdayTargets
    .filter((t) => t.dayOfWeek >= 1 && t.dayOfWeek <= 5)
    .reduce((sum, t) => sum + t.hours, 0);
  return weekdayHours > 0 ? weekdayHours : null;
});

const weeklyProgress = computed(() =>
  weeklyTarget.value
    ? Math.min((parseFloat(totalHoursThisWeek.value) / weeklyTarget.value) * 100, 100)
    : null
);

const todayStatus = computed(() => {
  if (isLoading.value) return null;
  if (todayVacationAmount.value === 1.0) return "vacation";
  if (todayVacationAmount.value === 0.5 && !hasLoggedToday.value) return "half-vacation";
  if (todayHoliday.value) return "holiday";
  if (isClockedIn.value) return "clocked-in";
  if (isWeekend.value) return "weekend";
  if (!hasLoggedToday.value) return "not-logged";
  if (todayTargetHours.value != null && todayHours.value >= todayTargetHours.value)
    return "target-reached";
  return "logged";
});

function formatHours(h: number): string {
  const abs = Math.abs(h);
  const hrs = Math.floor(abs);
  const min = Math.round((abs - hrs) * 60);
  const sign = h < 0 ? "-" : "+";
  return `${sign}${hrs}h${min.toString().padStart(2, "0")}m`;
}

onMounted(async () => {
  const today = localDateString(new Date());

  const [, scheduleResult, vacationResult, yearHolidays, overtimeResult] = await Promise.allSettled([
    fetchSummaries(),
    workSessionService.getMySchedule(),
    vacationService.getVacationForDate(today),
    holidayService.getHolidays(new Date().getFullYear()),
    workSessionService.getOvertime(),
  ]);

  if (scheduleResult.status === "fulfilled") schedule.value = scheduleResult.value;

  if (vacationResult.status === "fulfilled" && vacationResult.value) {
    todayVacationAmount.value = vacationResult.value.amount;
    todayVacationTypeName.value = vacationResult.value.vacationTypeName;
  }

  if (yearHolidays.status === "fulfilled") {
    const holiday = yearHolidays.value.find((h) => h.date === today && !h.isWorkingDay);
    if (holiday) todayHoliday.value = holiday;
  }

  if (overtimeResult.status === "fulfilled") {
    monthlyFlexHours.value = overtimeResult.value.runningBalanceHours;
  }

  pageLoading.value = false;
});
</script>

<template>
  <AuthenticatedLayout>
    <div class="p-6 lg:p-8">
      <div class="max-w-6xl mx-auto">
        <div class="mb-8">
          <h1 class="text-2xl font-semibold text-foreground">Dashboard</h1>
          <p class="text-sm text-muted-foreground mt-0.5">Your day at a glance</p>
        </div>

        <!-- Today's status banner -->
        <div
          v-if="todayStatus"
          :class="[
            'rounded-xl border px-5 py-4 mb-6 flex items-center justify-between gap-4',
            todayStatus === 'target-reached'
              ? 'bg-emerald-50 dark:bg-emerald-950/30 border-emerald-200 dark:border-emerald-800'
              : todayStatus === 'logged'
                ? 'bg-indigo-50 dark:bg-indigo-950/30 border-indigo-200 dark:border-indigo-800'
                : todayStatus === 'clocked-in'
                ? 'bg-blue-50 dark:bg-blue-950/30 border-blue-200 dark:border-blue-800'
                : todayStatus === 'not-logged'
                  ? 'bg-amber-50 dark:bg-amber-950/30 border-amber-200 dark:border-amber-800'
                  : todayStatus === 'vacation' || todayStatus === 'half-vacation'
                    ? 'bg-violet-50 dark:bg-violet-950/30 border-violet-200 dark:border-violet-800'
                    : todayStatus === 'holiday'
                      ? 'bg-sky-50 dark:bg-sky-950/30 border-sky-200 dark:border-sky-800'
                      : 'bg-slate-50 dark:bg-slate-900 border-slate-200 dark:border-slate-800',
          ]"
        >
          <div class="flex items-center gap-3">
            <CheckCircleIcon v-if="todayStatus === 'target-reached'" class="size-5 text-emerald-500 shrink-0" />
            <TrendingUpIcon v-else-if="todayStatus === 'logged'" class="size-5 text-indigo-500 shrink-0" />
            <ClockIcon v-else-if="todayStatus === 'clocked-in'" class="size-5 text-blue-500 shrink-0" />
            <AlertCircleIcon v-else-if="todayStatus === 'not-logged'" class="size-5 text-amber-500 shrink-0" />
            <PlaneIcon v-else-if="todayStatus === 'vacation'" class="size-5 text-violet-500 shrink-0" />
            <SunIcon v-else-if="todayStatus === 'half-vacation'" class="size-5 text-violet-500 shrink-0" />
            <StarIcon v-else-if="todayStatus === 'holiday'" class="size-5 text-sky-500 shrink-0" />
            <CalendarDaysIcon v-else class="size-5 text-slate-400 shrink-0" />

            <div>
              <p
                :class="[
                  'text-sm font-medium',
                  todayStatus === 'target-reached' ? 'text-emerald-800 dark:text-emerald-200'
                    : todayStatus === 'logged' ? 'text-indigo-800 dark:text-indigo-200'
                    : todayStatus === 'clocked-in' ? 'text-blue-800 dark:text-blue-200'
                    : todayStatus === 'not-logged' ? 'text-amber-800 dark:text-amber-200'
                    : todayStatus === 'vacation' || todayStatus === 'half-vacation' ? 'text-violet-800 dark:text-violet-200'
                    : todayStatus === 'holiday' ? 'text-sky-800 dark:text-sky-200'
                    : 'text-slate-600 dark:text-slate-400',
                ]"
              >
                <template v-if="todayStatus === 'vacation'">
                  You're on vacation today
                  <span class="font-normal opacity-75" v-if="todayVacationTypeName"> — {{ todayVacationTypeName }}</span>
                </template>
                <template v-else-if="todayStatus === 'half-vacation'">
                  Half-day {{ todayVacationTypeName ?? 'vacation' }} today — clock in and out when ready
                </template>
                <template v-else-if="todayStatus === 'holiday'">
                  Public holiday — {{ todayHoliday!.name }}
                </template>
                <template v-else-if="todayStatus === 'target-reached'">
                  Target reached! {{ todayHours.toFixed(2) }}h logged today
                  <span v-if="todayExceeded" class="font-normal opacity-75">
                    ({{ (todayHours - todayTargetHours!).toFixed(2) }}h over)
                  </span>
                </template>
                <template v-else-if="todayStatus === 'logged'">
                  <template v-if="todayRemaining != null">
                    {{ todayHours.toFixed(2) }}h / {{ todayTargetHours }}h — {{ todayRemaining.toFixed(2) }}h to go
                  </template>
                  <template v-else>{{ todayHours.toFixed(2) }}h logged today — great work!</template>
                </template>
                <template v-else-if="todayStatus === 'clocked-in'">
                  You're clocked in — hours will show once you clock out
                </template>
                <template v-else-if="todayStatus === 'not-logged'">No hours logged yet today</template>
                <template v-else>Enjoy your weekend!</template>
              </p>
            </div>
          </div>

          <button
            v-if="todayStatus !== 'weekend' && todayStatus !== 'vacation' && todayStatus !== 'holiday'"
            class="text-sm font-medium shrink-0 transition-colors cursor-pointer"
            :class="
              todayStatus === 'target-reached' ? 'text-emerald-700 dark:text-emerald-300 hover:text-emerald-900 dark:hover:text-emerald-100'
                : todayStatus === 'logged' ? 'text-indigo-700 dark:text-indigo-300 hover:text-indigo-900 dark:hover:text-indigo-100'
                : todayStatus === 'clocked-in' ? 'text-blue-700 dark:text-blue-300 hover:text-blue-900 dark:hover:text-blue-100'
                : 'text-violet-700 dark:text-violet-300 hover:text-violet-900 dark:hover:text-violet-100'
            "
            @click="router.push({ name: 'time-tracking' })"
          >
            {{ todayStatus === "not-logged" || todayStatus === "half-vacation" ? "Log now →" : "View logs →" }}
          </button>
        </div>

        <!-- Stats -->
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
          <!-- Today -->
          <div class="stat-card">
            <div class="flex items-center gap-2 mb-1">
              <ClockIcon class="size-3.5 text-slate-400" />
              <p class="text-xs font-medium uppercase tracking-wider text-muted-foreground">Today</p>
            </div>
            <p class="text-3xl font-bold font-mono text-foreground">
              <span v-if="isLoading" class="animate-pulse text-muted-foreground/40">--</span>
              <span v-else>{{ todayHours.toFixed(2) }}h</span>
            </p>
            <template v-if="!isLoading && todayTargetHours != null && todayTargetHours > 0">
              <div class="mt-2 w-full bg-muted rounded-full h-1.5">
                <div
                  :class="['h-1.5 rounded-full transition-all', todayProgress === 100 ? 'bg-emerald-500' : 'bg-primary']"
                  :style="{ width: `${todayProgress}%` }"
                />
              </div>
              <p class="text-xs font-mono text-muted-foreground mt-1">/ {{ todayTargetHours }}h target</p>
            </template>
          </div>

          <!-- This week -->
          <div class="stat-card">
            <div class="flex items-center gap-2 mb-1">
              <ClockIcon class="size-3.5 text-slate-400" />
              <p class="text-xs font-medium uppercase tracking-wider text-muted-foreground">This week</p>
            </div>
            <p class="text-3xl font-bold font-mono text-foreground">
              <span v-if="isLoading" class="animate-pulse text-muted-foreground/40">--</span>
              <span v-else>{{ totalHoursThisWeek }}h</span>
            </p>
            <template v-if="!isLoading && weeklyTarget != null">
              <div class="mt-2 w-full bg-muted rounded-full h-1.5">
                <div
                  :class="['h-1.5 rounded-full transition-all', weeklyProgress === 100 ? 'bg-emerald-500' : 'bg-primary']"
                  :style="{ width: `${weeklyProgress}%` }"
                />
              </div>
              <p class="text-xs font-mono text-muted-foreground mt-1">/ {{ weeklyTarget }}h target</p>
            </template>
          </div>

          <!-- This month -->
          <div class="stat-card">
            <div class="flex items-center gap-2 mb-1">
              <ClockIcon class="size-3.5 text-slate-400" />
              <p class="text-xs font-medium uppercase tracking-wider text-muted-foreground">This month</p>
            </div>
            <p class="text-3xl font-bold font-mono text-foreground">
              <span v-if="isLoading" class="animate-pulse text-muted-foreground/40">--</span>
              <span v-else>{{ totalHoursThisMonth }}h</span>
            </p>
          </div>

          <!-- Monthly flex balance -->
          <div class="stat-card">
            <div class="flex items-center gap-2 mb-1">
              <component
                :is="monthlyFlexHours !== null && monthlyFlexHours >= 0 ? TrendingUpIcon : TrendingDownIcon"
                class="size-3.5 text-slate-400"
              />
              <p class="text-xs font-medium uppercase tracking-wider text-muted-foreground">Flex balance</p>
            </div>
            <p
              class="text-3xl font-bold font-mono"
              :class="monthlyFlexHours === null || isLoading
                ? 'text-muted-foreground/40'
                : monthlyFlexHours >= 0
                  ? 'text-emerald-600 dark:text-emerald-400'
                  : 'text-rose-600 dark:text-rose-400'"
            >
              <span v-if="isLoading" class="animate-pulse">--</span>
              <span v-else-if="monthlyFlexHours === null">—</span>
              <span v-else>{{ formatHours(monthlyFlexHours) }}</span>
            </p>
            <p class="text-xs text-muted-foreground mt-1">This month</p>
          </div>
        </div>

        <!-- Vacation widget -->
        <UpcomingVacationsWidget />
      </div>
    </div>
  </AuthenticatedLayout>
</template>
