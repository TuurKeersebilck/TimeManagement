<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import { useTimeCalculations } from "../composables/useTimeCalculations";
import { useClockEventsStore } from "../composables/useClockEventsStore";
import { clockEventService } from "../services/clockEventService";
import { vacationService } from "../services/vacationService";
import UpcomingVacationsWidget from "@/components/UpcomingVacationsWidget.vue";
import { ClockIcon, CheckCircleIcon, AlertCircleIcon, CalendarDaysIcon, TrendingUpIcon, PlaneIcon, SunIcon } from "lucide-vue-next";

const router = useRouter();
const { summaries, loading, fetchSummaries } = useClockEventsStore();
const { totalHoursToday, totalHoursThisWeek, totalHoursThisMonth } = useTimeCalculations(summaries);

const dailyTarget = ref<number | null>(null);
const weeklyTarget = ref<number | null>(null);
const todayVacationAmount = ref<number | null>(null);
const todayVacationTypeName = ref<string | null>(null);

function localDateString(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

const isWeekend = computed(() => {
  const day = new Date().getDay();
  return day === 0 || day === 6;
});

const hasLoggedToday = computed(() => parseFloat(totalHoursToday.value) > 0);

// Today vs. target
const todayHours = computed(() => parseFloat(totalHoursToday.value));
const todayRemaining = computed(() =>
  dailyTarget.value != null ? Math.max(0, dailyTarget.value - todayHours.value) : null
);
const todayExceeded = computed(() =>
  dailyTarget.value != null && todayHours.value > dailyTarget.value
);
const todayProgress = computed(() =>
  dailyTarget.value ? Math.min((todayHours.value / dailyTarget.value) * 100, 100) : null
);

const weeklyProgress = computed(() =>
  weeklyTarget.value
    ? Math.min((parseFloat(totalHoursThisWeek.value) / weeklyTarget.value) * 100, 100)
    : null
);

const todayStatus = computed(() => {
  if (loading.value) return null;
  if (todayVacationAmount.value === 1.0) return "vacation";
  if (todayVacationAmount.value === 0.5 && !hasLoggedToday.value) return "half-vacation";
  if (isWeekend.value) return "weekend";
  if (!hasLoggedToday.value) return "not-logged";
  if (dailyTarget.value != null && todayHours.value >= dailyTarget.value) return "target-reached";
  return "logged";
});

onMounted(async () => {
  await fetchSummaries();
  try {
    const [target, vacation] = await Promise.all([
      clockEventService.getMyTarget(),
      vacationService.getVacationForDate(localDateString(new Date())),
    ]);
    dailyTarget.value = target.dailyHours ?? null;
    weeklyTarget.value = target.weeklyHours ?? null;
    if (vacation) {
      todayVacationAmount.value = vacation.amount;
      todayVacationTypeName.value = vacation.vacationTypeName;
      if (vacation.amount === 1.0) {
        dailyTarget.value = null;
      } else if (vacation.amount === 0.5 && dailyTarget.value != null) {
        dailyTarget.value = dailyTarget.value / 2;
      }
    }
  } catch {
    // No target configured — show plain hours
  }
});
</script>

<template>
  <AuthenticatedLayout>
    <div class="p-6 lg:p-8">
      <div class="max-w-6xl mx-auto">
        <div class="mb-8">
          <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">Dashboard</h1>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">Your day at a glance</p>
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
                : todayStatus === 'not-logged'
                  ? 'bg-amber-50 dark:bg-amber-950/30 border-amber-200 dark:border-amber-800'
                  : todayStatus === 'vacation' || todayStatus === 'half-vacation'
                    ? 'bg-violet-50 dark:bg-violet-950/30 border-violet-200 dark:border-violet-800'
                    : 'bg-slate-50 dark:bg-slate-900 border-slate-200 dark:border-slate-800',
          ]"
        >
          <div class="flex items-center gap-3">
            <CheckCircleIcon
              v-if="todayStatus === 'target-reached'"
              class="size-5 text-emerald-500 shrink-0"
            />
            <TrendingUpIcon
              v-else-if="todayStatus === 'logged'"
              class="size-5 text-indigo-500 shrink-0"
            />
            <AlertCircleIcon
              v-else-if="todayStatus === 'not-logged'"
              class="size-5 text-amber-500 shrink-0"
            />
            <PlaneIcon
              v-else-if="todayStatus === 'vacation'"
              class="size-5 text-violet-500 shrink-0"
            />
            <SunIcon
              v-else-if="todayStatus === 'half-vacation'"
              class="size-5 text-violet-500 shrink-0"
            />
            <CalendarDaysIcon v-else class="size-5 text-slate-400 shrink-0" />

            <div>
              <p
                :class="[
                  'text-sm font-medium',
                  todayStatus === 'target-reached'
                    ? 'text-emerald-800 dark:text-emerald-200'
                    : todayStatus === 'logged'
                      ? 'text-indigo-800 dark:text-indigo-200'
                      : todayStatus === 'not-logged'
                        ? 'text-amber-800 dark:text-amber-200'
                        : todayStatus === 'vacation' || todayStatus === 'half-vacation'
                          ? 'text-violet-800 dark:text-violet-200'
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
                <template v-else-if="todayStatus === 'target-reached'">
                  Target reached! {{ totalHoursToday }}h logged today
                  <span v-if="todayExceeded" class="font-normal opacity-75">
                    ({{ (todayHours - dailyTarget!).toFixed(2) }}h over)
                  </span>
                </template>
                <template v-else-if="todayStatus === 'logged'">
                  <template v-if="todayRemaining != null">
                    {{ totalHoursToday }}h / {{ dailyTarget }}h — {{ todayRemaining.toFixed(2) }}h to go
                  </template>
                  <template v-else>
                    {{ totalHoursToday }}h logged today — great work!
                  </template>
                </template>
                <template v-else-if="todayStatus === 'not-logged'">
                  No hours logged yet today
                </template>
                <template v-else>Enjoy your weekend!</template>
              </p>
            </div>
          </div>

          <button
            v-if="todayStatus !== 'weekend' && todayStatus !== 'vacation'"
            class="text-sm font-medium shrink-0 transition-colors cursor-pointer"
            :class="
              todayStatus === 'target-reached'
                ? 'text-emerald-700 dark:text-emerald-300 hover:text-emerald-900 dark:hover:text-emerald-100'
                : todayStatus === 'logged'
                  ? 'text-indigo-700 dark:text-indigo-300 hover:text-indigo-900 dark:hover:text-indigo-100'
                  : 'text-violet-700 dark:text-violet-300 hover:text-violet-900 dark:hover:text-violet-100'
            "
            @click="router.push({ name: 'time-tracking' })"
          >
            {{ todayStatus === "not-logged" || todayStatus === "half-vacation" ? "Log now →" : "View logs →" }}
          </button>
        </div>

        <!-- Stats -->
        <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-6">
          <!-- Today -->
          <div class="stat-card">
            <div class="flex items-center gap-2 mb-1">
              <ClockIcon class="size-3.5 text-slate-400" />
              <p class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400">
                Today
              </p>
            </div>
            <p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
              <span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600">--</span>
              <span v-else>{{ totalHoursToday }}h</span>
            </p>
            <template v-if="!loading && dailyTarget != null">
              <div class="mt-2 w-full bg-slate-100 dark:bg-slate-800 rounded-full h-1.5">
                <div
                  :class="[
                    'h-1.5 rounded-full transition-all',
                    todayProgress === 100 ? 'bg-emerald-500' : 'bg-indigo-500',
                  ]"
                  :style="{ width: `${todayProgress}%` }"
                />
              </div>
              <p class="text-xs text-slate-400 dark:text-slate-500 mt-1">
                / {{ dailyTarget }}h target
              </p>
            </template>
          </div>

          <!-- This week -->
          <div class="stat-card">
            <div class="flex items-center gap-2 mb-1">
              <ClockIcon class="size-3.5 text-slate-400" />
              <p class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400">
                This week
              </p>
            </div>
            <p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
              <span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600">--</span>
              <span v-else>{{ totalHoursThisWeek }}h</span>
            </p>
            <template v-if="!loading && weeklyTarget != null">
              <div class="mt-2 w-full bg-slate-100 dark:bg-slate-800 rounded-full h-1.5">
                <div
                  :class="[
                    'h-1.5 rounded-full transition-all',
                    weeklyProgress === 100 ? 'bg-emerald-500' : 'bg-indigo-500',
                  ]"
                  :style="{ width: `${weeklyProgress}%` }"
                />
              </div>
              <p class="text-xs text-slate-400 dark:text-slate-500 mt-1">
                / {{ weeklyTarget }}h target
              </p>
            </template>
          </div>

          <!-- This month -->
          <div class="stat-card">
            <div class="flex items-center gap-2 mb-1">
              <ClockIcon class="size-3.5 text-slate-400" />
              <p class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400">
                This month
              </p>
            </div>
            <p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
              <span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600">--</span>
              <span v-else>{{ totalHoursThisMonth }}h</span>
            </p>
          </div>
        </div>

        <!-- Vacation widget -->
        <UpcomingVacationsWidget />
      </div>
    </div>
  </AuthenticatedLayout>
</template>
