<script setup lang="ts">
import { computed } from "vue";
import { Loader2Icon, XIcon } from "lucide-vue-next";
import { Button } from "@/components/ui/button";
import type { VacationRequest, VacationRequestStatus } from "../services/vacationRequestService";

const props = defineProps<{
  requests: VacationRequest[];
  cancellingId: number | null;
}>();

const emit = defineEmits<{
  cancel: [request: VacationRequest];
  remove: [request: VacationRequest];
}>();

const RECENT_DAYS = 30;

// Drafts and pending ones, plus anything reviewed recently so the outcome (and a denial's reason)
// stays visible. Drafts first: they still need the employee's action.
const visible = computed(() => {
  const cutoff = Date.now() - RECENT_DAYS * 86_400_000;
  return props.requests
    .filter(
      (r) =>
        r.status === "Draft" ||
        r.status === "Pending" ||
        (r.status !== "Cancelled" && r.reviewedAt && new Date(r.reviewedAt).getTime() >= cutoff)
    )
    .sort((a, b) => Number(b.status === "Draft") - Number(a.status === "Draft"));
});

const STATUS_LABELS: Record<VacationRequestStatus, string> = {
  Draft: "Not sent yet",
  Pending: "Awaiting approval",
  Approved: "Approved",
  Rejected: "Denied",
  Cancelled: "Cancelled",
};

const STATUS_CLASSES: Record<VacationRequestStatus, string> = {
  Draft: "border border-dashed border-primary/50 text-primary bg-primary/5",
  Pending: "bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-300",
  Approved: "bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300",
  Rejected: "bg-red-100 text-red-700 dark:bg-red-950 dark:text-red-300",
  Cancelled: "bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300",
};

const fmtDay = (iso: string) =>
  new Date(iso + "T00:00:00").toLocaleDateString(undefined, {
    day: "numeric",
    month: "short",
    year: "numeric",
  });

const fmtPeriod = (r: VacationRequest) =>
  r.startDate === r.endDate ? fmtDay(r.startDate) : `${fmtDay(r.startDate)} – ${fmtDay(r.endDate)}`;
</script>

<template>
  <section v-if="visible.length > 0" class="mb-8">
    <h2
      class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500 mb-3"
    >
      My requests
    </h2>

    <div class="card divide-y divide-slate-100 dark:divide-slate-800 overflow-hidden">
      <div
        v-for="r in visible"
        :key="r.id"
        class="flex flex-wrap items-center gap-x-3 gap-y-1 px-5 py-3.5"
      >
        <div class="flex items-center gap-2 min-w-0 flex-1">
          <div
            class="w-2.5 h-2.5 rounded-full shrink-0 ring-1 ring-black/10"
            :style="{ backgroundColor: r.vacationTypeColor ?? '#6366f1' }"
          />
          <div class="min-w-0">
            <p class="text-sm font-medium text-slate-900 dark:text-slate-100">
              {{ fmtPeriod(r) }}
              <span class="font-normal text-slate-500 dark:text-slate-400">
                · {{ r.vacationTypeName }}, {{ r.totalDays }} day{{ r.totalDays !== 1 ? "s" : "" }}
              </span>
            </p>
            <p
              v-if="r.status === 'Rejected' && r.reviewNote"
              class="text-xs text-slate-500 dark:text-slate-400 mt-0.5"
            >
              “{{ r.reviewNote }}”
            </p>
          </div>
        </div>

        <span
          class="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-semibold shrink-0"
          :class="STATUS_CLASSES[r.status]"
        >
          {{ STATUS_LABELS[r.status] }}
        </span>

        <Button
          v-if="r.status === 'Pending' || r.status === 'Draft'"
          variant="ghost"
          size="sm"
          class="h-7 text-slate-500 hover:text-red-500 dark:hover:text-red-400 shrink-0"
          :disabled="cancellingId === r.id"
          @click="r.status === 'Draft' ? emit('remove', r) : emit('cancel', r)"
        >
          <Loader2Icon v-if="cancellingId === r.id" class="size-3.5 animate-spin" />
          <XIcon v-else class="size-3.5" />
          {{ r.status === "Draft" ? "Remove" : "Cancel" }}
        </Button>
      </div>
    </div>
  </section>
</template>
