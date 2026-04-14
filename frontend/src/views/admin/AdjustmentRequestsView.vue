<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import {
  adjustmentRequestService,
  type AdjustmentRequest,
  type AdjustmentRequestStatus,
} from "@/services/adjustmentRequestService";
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
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { useConfirmDialog } from "@/composables/useConfirmDialog";
import { ClipboardListIcon, Loader2Icon, XIcon, CheckIcon } from "lucide-vue-next";

const toast = useAppToast();
const { confirm } = useConfirmDialog();
const requests = ref<AdjustmentRequest[]>([]);
const loading = ref(false);
const search = ref("");
const approvingId = ref<number | null>(null);
const rejectingId = ref<number | null>(null);

const STATUS_LABELS: Record<AdjustmentRequestStatus, string> = {
  Pending: "Pending",
  Approved: "Approved",
  Rejected: "Rejected",
};

const STATUS_CLASSES: Record<AdjustmentRequestStatus, string> = {
  Pending: "bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-300",
  Approved: "bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300",
  Rejected: "bg-red-100 text-red-700 dark:bg-red-950 dark:text-red-300",
};

const filtered = computed(() => {
  const q = search.value.toLowerCase();
  return requests.value.filter(
    (r) =>
      !q ||
      r.employeeName.toLowerCase().includes(q) ||
      r.date.includes(q) ||
      r.reason.toLowerCase().includes(q)
  );
});

function fmt(t: string | null): string {
  if (!t) return "—";
  const [h, m] = t.split(":").map(Number);
  const d = new Date();
  d.setUTCHours(h, m, 0, 0);
  return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
}

function fmtDate(d: string): string {
  return new Date(d).toLocaleDateString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}

async function load() {
  loading.value = true;
  try {
    requests.value = await adjustmentRequestService.getAll();
  } catch {
    toast.error("Failed to load adjustment requests");
  } finally {
    loading.value = false;
  }
}

function approveRequest(r: AdjustmentRequest) {
  confirm({
    title: "Approve adjustment request",
    message: `Approve ${r.employeeName}'s adjustment request for ${fmtDate(r.date)}? This will update their time log and cannot be undone.`,
    confirmLabel: "Approve",
    onConfirm: async () => {
      approvingId.value = r.id;
      try {
        await adjustmentRequestService.approve(r.id);
        requests.value = requests.value.map((req) =>
          req.id === r.id ? { ...req, status: "Approved" as AdjustmentRequestStatus } : req
        );
        toast.success("Request approved");
      } catch {
        toast.error("Failed to approve request");
      } finally {
        approvingId.value = null;
      }
    },
  });
}

function rejectRequest(r: AdjustmentRequest) {
  confirm({
    title: "Reject adjustment request",
    message: `Reject ${r.employeeName}'s adjustment request for ${fmtDate(r.date)}? This cannot be undone.`,
    confirmLabel: "Reject",
    variant: "destructive",
    onConfirm: async () => {
      rejectingId.value = r.id;
      try {
        await adjustmentRequestService.reject(r.id);
        requests.value = requests.value.map((req) =>
          req.id === r.id ? { ...req, status: "Rejected" as AdjustmentRequestStatus } : req
        );
        toast.success("Request rejected");
      } catch {
        toast.error("Failed to reject request");
      } finally {
        rejectingId.value = null;
      }
    },
  });
}

onMounted(load);
</script>

<template>
  <AuthenticatedLayout>
    <div class="p-6 lg:p-8">
      <div class="max-w-6xl mx-auto">
        <!-- Header -->
        <div class="flex items-center justify-between mb-8">
          <div>
            <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">
              Adjustment Requests
            </h1>
            <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
              Employee time adjustment requests requiring approval
            </p>
          </div>
          <div class="w-64">
            <Input v-model="search" placeholder="Search employee, date, reason…" />
          </div>
        </div>

        <!-- Table -->
        <div class="card overflow-hidden">
          <div v-if="loading" class="divide-y divide-slate-100 dark:divide-slate-800">
            <div v-for="i in 5" :key="i" class="flex items-center gap-4 px-4 py-3.5">
              <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-28 animate-pulse" />
              <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-20 animate-pulse" />
              <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-36 animate-pulse" />
              <div class="ml-auto h-5 bg-slate-200 dark:bg-slate-700 rounded w-16 animate-pulse" />
            </div>
          </div>

          <Table v-else>
            <TableHeader>
              <TableRow>
                <TableHead>Employee</TableHead>
                <TableHead>Date</TableHead>
                <TableHead>Requested Times</TableHead>
                <TableHead>Reason</TableHead>
                <TableHead>Requested At</TableHead>
                <TableHead>Status</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              <TableEmpty v-if="filtered.length === 0" :colspan="7">
                <ClipboardListIcon class="size-8 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
                <p class="text-slate-500 dark:text-slate-400">
                  {{ search ? "No requests match your search." : "No adjustment requests yet." }}
                </p>
              </TableEmpty>
              <TableRow v-for="r in filtered" :key="r.id">
                <TableCell class="font-medium text-slate-900 dark:text-slate-100">
                  {{ r.employeeName }}
                </TableCell>
                <TableCell class="text-slate-600 dark:text-slate-400">
                  {{ fmtDate(r.date) }}
                </TableCell>
                <TableCell class="font-mono text-xs text-slate-600 dark:text-slate-400 whitespace-nowrap">
                  {{ fmt(r.requestedClockIn) }} / {{ fmt(r.requestedBreakStart) }} / {{ fmt(r.requestedBreakEnd) }} / {{ fmt(r.requestedClockOut) }}
                </TableCell>
                <TableCell class="text-slate-600 dark:text-slate-400 text-sm max-w-[220px]">
                  <span :title="r.reason">
                    {{ r.reason.length > 80 ? r.reason.substring(0, 80) + "…" : r.reason }}
                  </span>
                </TableCell>
                <TableCell class="text-slate-500 dark:text-slate-400 text-sm">
                  {{ fmtDate(r.requestedAt) }}
                </TableCell>
                <TableCell>
                  <span
                    class="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-semibold"
                    :class="STATUS_CLASSES[r.status]"
                  >
                    {{ STATUS_LABELS[r.status] }}
                  </span>
                </TableCell>
                <TableCell class="text-right">
                  <div v-if="r.status === 'Pending'" class="flex items-center justify-end gap-1">
                    <Button
                      variant="ghost"
                      size="icon"
                      class="size-7 text-slate-400 hover:text-emerald-600 dark:hover:text-emerald-400"
                      title="Approve"
                      :disabled="approvingId === r.id || rejectingId === r.id"
                      @click="approveRequest(r)"
                    >
                      <Loader2Icon v-if="approvingId === r.id" class="size-3.5 animate-spin" />
                      <CheckIcon v-else class="size-3.5" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      class="size-7 text-slate-400 hover:text-red-500 dark:hover:text-red-400"
                      title="Reject"
                      :disabled="rejectingId === r.id || approvingId === r.id"
                      @click="rejectRequest(r)"
                    >
                      <Loader2Icon v-if="rejectingId === r.id" class="size-3.5 animate-spin" />
                      <XIcon v-else class="size-3.5" />
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </div>
      </div>
    </div>
  </AuthenticatedLayout>
</template>
