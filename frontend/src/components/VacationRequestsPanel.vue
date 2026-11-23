<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import {
  vacationRequestService,
  type VacationRequest,
  type VacationRequestStatus,
} from "@/services/vacationRequestService";
import { useAppToast } from "@/composables/useAppToast";
import { usePendingVacationRequests } from "@/composables/usePendingVacationRequests";
import { extractApiError } from "@/utils/apiError";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Table,
  TableBody,
  TableCell,
  TableEmpty,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  AlertTriangleIcon,
  CalendarCheckIcon,
  CheckIcon,
  Loader2Icon,
  UsersIcon,
  XIcon,
} from "lucide-vue-next";

const toast = useAppToast();
const requests = ref<VacationRequest[]>([]);
const loading = ref(false);
const busyId = ref<number | null>(null);
const search = ref("");

const { pendingVacationCount } = usePendingVacationRequests();

const pending = computed(() =>
  requests.value
    .filter((r) => r.status === "Pending")
    // Oldest first: whoever asked first has waited longest.
    .sort((a, b) => a.requestedAt.localeCompare(b.requestedAt))
);

const history = computed(() => {
  const q = search.value.trim().toLowerCase();
  return requests.value
    .filter((r) => r.status !== "Pending")
    .filter(
      (r) =>
        !q ||
        r.employeeName.toLowerCase().includes(q) ||
        r.vacationTypeName.toLowerCase().includes(q) ||
        r.startDate.includes(q)
    );
});

// Keep the sidebar badge in step with approvals and denials made here.
watch(pending, (list) => (pendingVacationCount.value = list.length));

// ─── Formatting ──────────────────────────────────────────────────────────────

const parse = (iso: string) => new Date(iso + "T00:00:00");

function fmtDay(iso: string, withYear = true): string {
  return parse(iso).toLocaleDateString(undefined, {
    weekday: "short",
    day: "numeric",
    month: "short",
    ...(withYear ? { year: "numeric" } : {}),
  });
}

function fmtPeriod(r: Pick<VacationRequest, "startDate" | "endDate">): string {
  if (r.startDate === r.endDate) return fmtDay(r.startDate);
  const sameYear = r.startDate.slice(0, 4) === r.endDate.slice(0, 4);
  return `${fmtDay(r.startDate, !sameYear)} – ${fmtDay(r.endDate)}`;
}

function fmtDays(n: number): string {
  return `${n} day${n === 1 ? "" : "s"}`;
}

function fmtShortDates(dates: string[]): string {
  const labels = dates.map((d) =>
    parse(d).toLocaleDateString(undefined, { day: "numeric", month: "short" })
  );
  return labels.length > 3
    ? `${labels.slice(0, 3).join(", ")} +${labels.length - 3}`
    : labels.join(", ");
}

function fmtRequestedAt(iso: string): string {
  const days = Math.floor((Date.now() - new Date(iso).getTime()) / 86_400_000);
  if (days <= 0) return "today";
  if (days === 1) return "yesterday";
  if (days < 14) return `${days} days ago`;
  return new Date(iso).toLocaleDateString(undefined, { day: "numeric", month: "short" });
}

const STATUS_LABELS: Record<VacationRequestStatus, string> = {
  Draft: "Not sent yet",
  Pending: "Pending",
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

// ─── Actions ─────────────────────────────────────────────────────────────────

async function load() {
  loading.value = true;
  try {
    requests.value = await vacationRequestService.getAll();
  } catch {
    toast.error("Failed to load vacation requests");
  } finally {
    loading.value = false;
  }
}

function markReviewed(id: number, status: VacationRequestStatus, reviewNote: string | null = null) {
  requests.value = requests.value.map((r) =>
    r.id === id
      ? {
          ...r,
          status,
          reviewNote,
          reviewedAt: new Date().toISOString(),
          balance: null,
          overlaps: null,
        }
      : r
  );
}

async function approve(r: VacationRequest) {
  busyId.value = r.id;
  try {
    const result = await vacationRequestService.approve(r.id);
    markReviewed(r.id, "Approved");
    const booked = result.created.length;
    toast.success(
      booked > 0
        ? `Approved: ${fmtDays(booked)} booked for ${r.employeeName}`
        : `Approved. ${r.employeeName} already had these days, so nothing new was booked`
    );
    // Other cards' balance and "also off" lines may have changed with these days booked.
    load();
  } catch (err) {
    toast.error(extractApiError(err, "Failed to approve request"));
    load();
  } finally {
    busyId.value = null;
  }
}

const denyDialog = ref<{ open: boolean; request: VacationRequest | null; reason: string }>({
  open: false,
  request: null,
  reason: "",
});

function openDeny(r: VacationRequest) {
  denyDialog.value = { open: true, request: r, reason: "" };
}

async function confirmDeny() {
  const r = denyDialog.value.request;
  if (!r) return;
  busyId.value = r.id;
  try {
    const reason = denyDialog.value.reason.trim();
    await vacationRequestService.reject(r.id, reason);
    markReviewed(r.id, "Rejected", reason || null);
    denyDialog.value.open = false;
    toast.success(`Request denied. ${r.employeeName} has been notified`);
  } catch (err) {
    toast.error(extractApiError(err, "Failed to deny request"));
    load();
  } finally {
    busyId.value = null;
  }
}

onMounted(load);
</script>

<template>
  <div class="space-y-8">
    <!-- Pending -->
    <section>
      <div v-if="loading && requests.length === 0" class="grid gap-3">
        <div
          v-for="i in 2"
          :key="i"
          class="card h-36 animate-pulse bg-slate-100 dark:bg-slate-800"
        />
      </div>

      <div
        v-else-if="pending.length === 0"
        class="card flex flex-col items-center justify-center py-10 text-center"
      >
        <CalendarCheckIcon class="size-8 text-slate-300 dark:text-slate-600 mb-2" />
        <p class="text-sm text-slate-500 dark:text-slate-400">
          No vacation requests waiting for review.
        </p>
      </div>

      <ul v-else class="grid gap-3">
        <li v-for="r in pending" :key="r.id" class="card p-4 sm:p-5">
          <div class="flex flex-wrap items-start justify-between gap-x-4 gap-y-2">
            <div class="min-w-0">
              <!-- Flex gaps, not template whitespace, keep these apart: Vue drops line breaks between tags. -->
              <p
                class="flex flex-wrap items-center gap-x-1.5 font-semibold text-slate-900 dark:text-slate-100"
              >
                <span>{{ r.employeeName }}</span>
                <span class="font-normal text-slate-500 dark:text-slate-400">requested</span>
                <span
                  class="inline-flex items-center gap-1.5 font-medium text-slate-700 dark:text-slate-200"
                >
                  <span
                    class="size-2 rounded-full shrink-0"
                    :style="{ backgroundColor: r.vacationTypeColor ?? '#6366f1' }"
                  />{{ r.vacationTypeName }}
                </span>
              </p>
              <p class="mt-1 text-lg font-semibold text-slate-900 dark:text-slate-100 tabular-nums">
                {{ fmtPeriod(r) }}
              </p>
              <p class="text-sm text-slate-500 dark:text-slate-400">
                {{ fmtDays(r.totalDays) }}<span v-if="r.amount === 0.5"> (half days)</span> · asked
                {{ fmtRequestedAt(r.requestedAt) }}
              </p>
            </div>

            <div class="flex items-center gap-2 shrink-0">
              <Button variant="outline" size="sm" :disabled="busyId === r.id" @click="openDeny(r)">
                <XIcon class="size-3.5" />
                Deny
              </Button>
              <Button size="sm" :disabled="busyId === r.id" @click="approve(r)">
                <Loader2Icon v-if="busyId === r.id" class="size-3.5 animate-spin" />
                <CheckIcon v-else class="size-3.5" />
                Approve
              </Button>
            </div>
          </div>

          <p
            v-if="r.note"
            class="mt-3 text-sm text-slate-700 dark:text-slate-300 border-l-2 border-slate-200 dark:border-slate-700 pl-3 whitespace-pre-wrap"
          >
            {{ r.note }}
          </p>

          <div class="mt-4 grid gap-2 sm:grid-cols-2 text-sm">
            <!-- Balance -->
            <div
              v-if="r.balance"
              :class="[
                'rounded-lg px-3 py-2',
                r.balance.remainingAfterApproval !== null && r.balance.remainingAfterApproval < 0
                  ? 'bg-amber-50 text-amber-800 dark:bg-amber-950/40 dark:text-amber-300'
                  : 'bg-slate-50 text-slate-700 dark:bg-slate-800/60 dark:text-slate-300',
              ]"
            >
              <p class="text-xs font-medium uppercase tracking-wide opacity-70 mb-0.5">Balance</p>
              <template v-if="r.balance.yearlyBalance === null">
                No yearly limit for this type
              </template>
              <template v-else-if="r.balance.remainingAfterApproval! < 0">
                <AlertTriangleIcon class="inline size-3.5 -mt-0.5" />
                {{ fmtDays(Math.abs(r.balance.remainingAfterApproval!)) }} over their
                {{ r.balance.yearlyBalance }}-day balance after approval
              </template>
              <template v-else>
                {{ r.balance.remainingAfterApproval }} of {{ r.balance.yearlyBalance }} days left
                after approval
              </template>
              <span v-if="r.balance.otherPendingDays > 0" class="block text-xs opacity-75">
                Counts {{ fmtDays(r.balance.otherPendingDays) }} in their other pending requests
              </span>
            </div>

            <!-- Who else is off -->
            <div
              class="rounded-lg px-3 py-2 bg-slate-50 text-slate-700 dark:bg-slate-800/60 dark:text-slate-300"
            >
              <p
                class="text-xs font-medium uppercase tracking-wide opacity-70 mb-0.5 flex items-center gap-1"
              >
                <UsersIcon class="size-3" /> Also off
              </p>
              <p
                v-if="!r.overlaps || r.overlaps.length === 0"
                class="text-slate-500 dark:text-slate-400"
              >
                Nobody else on these days
              </p>
              <ul v-else class="space-y-0.5">
                <li
                  v-for="o in r.overlaps"
                  :key="o.employeeName + o.isPending"
                  class="flex flex-wrap items-baseline gap-x-2"
                >
                  <span class="font-medium">{{ o.employeeName }}</span>
                  <span class="text-slate-500 dark:text-slate-400">
                    {{ fmtShortDates(o.dates) }}<span v-if="o.isPending"> (requested)</span>
                  </span>
                </li>
              </ul>
            </div>
          </div>
        </li>
      </ul>
    </section>

    <!-- History -->
    <section>
      <div class="flex items-center justify-between gap-4 mb-3">
        <h2 class="text-sm font-semibold text-slate-900 dark:text-slate-100">Reviewed</h2>
        <div class="w-64 shrink-0">
          <Input v-model="search" placeholder="Search employee, type, date…" />
        </div>
      </div>

      <div class="card overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Employee</TableHead>
              <TableHead>Type</TableHead>
              <TableHead>Period</TableHead>
              <TableHead class="text-right">Days</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Reviewed</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <TableEmpty v-if="history.length === 0" :colspan="6">
              <p class="text-slate-500 dark:text-slate-400">
                {{
                  search ? "No requests match your search." : "No reviewed vacation requests yet."
                }}
              </p>
            </TableEmpty>
            <TableRow v-for="r in history" :key="r.id">
              <TableCell class="font-medium text-slate-900 dark:text-slate-100">{{
                r.employeeName
              }}</TableCell>
              <TableCell class="text-slate-600 dark:text-slate-400">
                <span class="inline-flex items-center gap-1.5">
                  <span
                    class="size-2 rounded-full shrink-0"
                    :style="{ backgroundColor: r.vacationTypeColor ?? '#6366f1' }"
                  />{{ r.vacationTypeName }}
                </span>
              </TableCell>
              <TableCell class="text-slate-600 dark:text-slate-400 whitespace-nowrap">{{
                fmtPeriod(r)
              }}</TableCell>
              <TableCell class="text-right tabular-nums text-slate-600 dark:text-slate-400">{{
                r.totalDays
              }}</TableCell>
              <TableCell>
                <span
                  class="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-semibold"
                  :class="STATUS_CLASSES[r.status]"
                  :title="r.reviewNote ?? undefined"
                >
                  {{ STATUS_LABELS[r.status] }}
                </span>
              </TableCell>
              <TableCell class="text-sm text-slate-500 dark:text-slate-400 whitespace-nowrap">
                <template v-if="r.reviewedAt">
                  {{
                    new Date(r.reviewedAt).toLocaleDateString(undefined, {
                      day: "numeric",
                      month: "short",
                      year: "numeric",
                    })
                  }}
                  <span v-if="r.reviewedByName"> · {{ r.reviewedByName }}</span>
                </template>
                <template v-else>—</template>
              </TableCell>
            </TableRow>
          </TableBody>
        </Table>
      </div>
    </section>
  </div>

  <!-- Deny dialog -->
  <Dialog v-model:open="denyDialog.open">
    <DialogContent class="max-w-md">
      <DialogHeader>
        <DialogTitle>Deny vacation request</DialogTitle>
        <DialogDescription v-if="denyDialog.request">
          {{ denyDialog.request.employeeName }} · {{ denyDialog.request.vacationTypeName }},
          {{ fmtPeriod(denyDialog.request) }}
        </DialogDescription>
      </DialogHeader>
      <div class="space-y-1.5">
        <Label for="deny-reason"
          >Reason <span class="font-normal text-slate-400">(optional)</span></Label
        >
        <textarea
          id="deny-reason"
          v-model="denyDialog.reason"
          rows="3"
          maxlength="500"
          placeholder="Shown to the employee in the notification and email"
          class="w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-xs outline-none placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 dark:bg-input/30 resize-none"
        />
      </div>
      <DialogFooter>
        <Button variant="outline" @click="denyDialog.open = false">Back</Button>
        <Button
          variant="destructive"
          :disabled="busyId === denyDialog.request?.id"
          @click="confirmDeny"
        >
          <Loader2Icon v-if="busyId === denyDialog.request?.id" class="size-3.5 animate-spin" />
          Deny request
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
