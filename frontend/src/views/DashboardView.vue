<script setup lang="ts">
import { computed, onMounted } from "vue";
import { useRouter } from "vue-router";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import { useTimeCalculations } from "../composables/useTimeCalculations";
import { useTimeLogsStore } from "../composables/useTimeLogsStore";
import UpcomingVacationsWidget from "@/components/UpcomingVacationsWidget.vue";
import { ClockIcon, CheckCircleIcon, AlertCircleIcon, CalendarDaysIcon } from "lucide-vue-next";

const router = useRouter();
const { timeLogs, loading, fetchTimeLogs } = useTimeLogsStore();
const { totalHoursToday, totalHoursThisWeek, totalHoursThisMonth } = useTimeCalculations(timeLogs);

const isWeekend = computed(() => {
  const day = new Date().getDay();
  return day === 0 || day === 6;
});

const hasLoggedToday = computed(() => parseFloat(totalHoursToday.value) > 0);

const todayStatus = computed(() => {
  if (loading.value) return null;
  if (isWeekend.value) return "weekend";
  return hasLoggedToday.value ? "logged" : "not-logged";
});

onMounted(async () => {
  await fetchTimeLogs();
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
            todayStatus === 'logged'
              ? 'bg-emerald-50 dark:bg-emerald-950/30 border-emerald-200 dark:border-emerald-800'
              : todayStatus === 'not-logged'
                ? 'bg-amber-50 dark:bg-amber-950/30 border-amber-200 dark:border-amber-800'
                : 'bg-slate-50 dark:bg-slate-900 border-slate-200 dark:border-slate-800',
          ]"
        >
          <div class="flex items-center gap-3">
            <CheckCircleIcon
              v-if="todayStatus === 'logged'"
              class="size-5 text-emerald-500 shrink-0"
            />
            <AlertCircleIcon
              v-else-if="todayStatus === 'not-logged'"
              class="size-5 text-amber-500 shrink-0"
            />
            <CalendarDaysIcon v-else class="size-5 text-slate-400 shrink-0" />

            <div>
              <p
                :class="[
                  'text-sm font-medium',
                  todayStatus === 'logged'
                    ? 'text-emerald-800 dark:text-emerald-200'
                    : todayStatus === 'not-logged'
                      ? 'text-amber-800 dark:text-amber-200'
                      : 'text-slate-600 dark:text-slate-400',
                ]"
              >
                <template v-if="todayStatus === 'logged'">
                  {{ totalHoursToday }}h logged today — great work!
                </template>
                <template v-else-if="todayStatus === 'not-logged'">
                  No hours logged yet today
                </template>
                <template v-else> Enjoy your weekend! </template>
              </p>
            </div>
          </div>

          <button
            v-if="todayStatus !== 'weekend'"
            class="text-sm font-medium shrink-0 transition-colors cursor-pointer"
            :class="
              todayStatus === 'logged'
                ? 'text-emerald-700 dark:text-emerald-300 hover:text-emerald-900 dark:hover:text-emerald-100'
                : 'text-amber-700 dark:text-amber-300 hover:text-amber-900 dark:hover:text-amber-100'
            "
            @click="router.push({ name: 'time-tracking' })"
          >
            {{ todayStatus === "logged" ? "View logs →" : "Log now →" }}
          </button>
        </div>

        <!-- Stats -->
        <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-6">
          <!-- Today -->
          <div class="stat-card">
            <div class="flex items-center gap-2 mb-1">
              <ClockIcon class="size-3.5 text-slate-400" />
              <p
                class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400"
              >
                Today
              </p>
            </div>
            <p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
              <span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600">
                --
              </span>
              <span v-else>{{ totalHoursToday }}h</span>
            </p>
          </div>

          <!-- This week -->
          <div class="stat-card">
            <div class="flex items-center gap-2 mb-1">
              <ClockIcon class="size-3.5 text-slate-400" />
              <p
                class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400"
              >
                This week
              </p>
            </div>
            <p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
              <span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600">
                --
              </span>
              <span v-else>{{ totalHoursThisWeek }}h</span>
            </p>
          </div>

          <!-- This month -->
          <div class="stat-card">
            <div class="flex items-center gap-2 mb-1">
              <ClockIcon class="size-3.5 text-slate-400" />
              <p
                class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400"
              >
                This month
              </p>
            </div>
            <p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
              <span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600">
                --
              </span>
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
