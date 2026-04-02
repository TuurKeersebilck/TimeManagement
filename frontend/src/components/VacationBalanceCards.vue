<script setup lang="ts">
import { CalendarIcon } from "lucide-vue-next";
import type { VacationBalance } from "../services/vacationService";

defineProps<{
  balances: VacationBalance[];
  loading: boolean;
}>();

const barWidth = (balance: VacationBalance) => {
  if (balance.yearlyBalance === 0) return "0%";
  return `${Math.min((balance.usedDays / balance.yearlyBalance) * 100, 100)}%`;
};

const barColor = (balance: VacationBalance) => {
  const pct = balance.yearlyBalance > 0 ? balance.usedDays / balance.yearlyBalance : 0;
  if (pct >= 1) return "bg-red-500";
  if (pct >= 0.8) return "bg-amber-400";
  return "bg-emerald-500";
};
</script>

<template>
  <section class="mb-8">
    <h2 class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500 mb-3">
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
          <span class="text-sm font-medium text-slate-900 dark:text-slate-100">
            {{ balance.vacationTypeName }}
          </span>
        </div>
        <div class="w-full bg-slate-100 dark:bg-slate-800 rounded-full h-1.5 mb-2">
          <div
            :class="['h-1.5 rounded-full transition-all duration-300', barColor(balance)]"
            :style="{ width: barWidth(balance) }"
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
          >{{ balance.remainingDays }} remaining</span>
        </div>
      </div>
    </div>
  </section>
</template>
