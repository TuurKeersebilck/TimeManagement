<script setup lang="ts">
import { computed } from "vue";
import { CalendarIcon } from "lucide-vue-next";
import type { VacationBalance } from "../services/vacationService";

const props = defineProps<{
  balances: VacationBalance[];
  loading: boolean;
}>();

// Unlimited types (e.g. sick leave) apply to everyone, so only show them once a day was taken
// or requested.
const visibleBalances = computed(() =>
  props.balances.filter((b) => !b.isUnlimited || b.usedDays > 0 || b.pendingDays > 0),
);

const pendingWidth = (balance: VacationBalance) => {
  if (!balance.yearlyBalance || !balance.pendingDays) return "0%";
  const used = Math.min(balance.usedDays / balance.yearlyBalance, 1);
  const pending = Math.min(balance.pendingDays / balance.yearlyBalance, 1 - used);
  return `${pending * 100}%`;
};

const barWidth = (balance: VacationBalance) => {
  if (!balance.yearlyBalance) return "0%";
  return `${Math.min((balance.usedDays / balance.yearlyBalance) * 100, 100)}%`;
};

const barColor = (balance: VacationBalance) => {
  const pct = balance.yearlyBalance ? balance.usedDays / balance.yearlyBalance : 0;
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

    <div v-else-if="visibleBalances.length === 0" class="card text-center py-10">
      <CalendarIcon class="size-8 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
      <p class="text-sm text-slate-500 dark:text-slate-400">
        No vacation types have been assigned to you yet.
      </p>
    </div>

    <div v-else class="grid gap-3 sm:grid-cols-2">
      <div v-for="balance in visibleBalances" :key="balance.vacationTypeId" class="card p-4">
        <div class="flex items-center gap-2 mb-3">
          <div
            class="w-3 h-3 rounded-full shrink-0 ring-1 ring-black/10"
            :style="{ backgroundColor: balance.vacationTypeColor ?? '#6366f1' }"
          />
          <span class="text-sm font-medium text-slate-900 dark:text-slate-100">
            {{ balance.vacationTypeName }}
          </span>
        </div>
        <p v-if="balance.isUnlimited" class="text-xs text-slate-500 dark:text-slate-400">
          {{ balance.usedDays }} day{{ balance.usedDays !== 1 ? "s" : "" }} this year · no limit
        </p>
        <div v-else class="w-full bg-slate-100 dark:bg-slate-800 rounded-full h-1.5 mb-2 flex overflow-hidden">
          <div
            :class="['h-1.5 transition-all duration-300', barColor(balance)]"
            :style="{ width: barWidth(balance) }"
          />
          <div
            class="h-1.5 bg-amber-300/70 dark:bg-amber-500/50 transition-all duration-300"
            :style="{ width: pendingWidth(balance) }"
            :title="`${balance.pendingDays} day(s) requested, awaiting approval`"
          />
        </div>
        <div v-if="!balance.isUnlimited" class="flex justify-between text-xs text-slate-500 dark:text-slate-400">
          <span>{{ balance.usedDays }} / {{ balance.yearlyBalance }} days used</span>
          <span
            :class="
              balance.remainingDays! <= 0
                ? 'text-red-600 dark:text-red-400 font-semibold'
                : 'text-emerald-600 dark:text-emerald-400 font-semibold'
            "
          >{{ balance.remainingDays }} remaining</span>
        </div>
        <p v-if="balance.pendingDays > 0" class="mt-1 text-xs text-amber-700 dark:text-amber-300">
          {{ balance.pendingDays }} day{{ balance.pendingDays !== 1 ? "s" : "" }} awaiting approval
        </p>
      </div>
    </div>
  </section>
</template>
