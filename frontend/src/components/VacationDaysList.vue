<script setup lang="ts">
import { CalendarIcon, PencilIcon, Trash2Icon } from "lucide-vue-next";
import { Button } from "@/components/ui/button";
import type { VacationDay } from "../services/vacationService";

defineProps<{
  vacationDays: VacationDay[];
  loading: boolean;
}>();

const emit = defineEmits<{
  edit: [day: VacationDay];
  delete: [day: VacationDay];
}>();

const displayDate = (iso: string) =>
  new Date(iso).toLocaleDateString(undefined, { day: "numeric", month: "short", year: "numeric" });
</script>

<template>
  <section>
    <h2 class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500 mb-3">
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
            <span v-if="day.note" class="text-slate-400 dark:text-slate-500"> · {{ day.note }}</span>
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
            @click="emit('edit', day)"
          >
            <PencilIcon class="size-3.5" />
          </Button>
          <Button
            variant="ghost"
            size="icon"
            class="size-8 text-slate-400 hover:text-red-500 dark:hover:text-red-400"
            title="Delete"
            @click="emit('delete', day)"
          >
            <Trash2Icon class="size-3.5" />
          </Button>
        </div>
      </div>
    </div>
  </section>
</template>
