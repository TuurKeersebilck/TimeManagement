<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import {
  settlementService,
  type MonthlySettlementDto,
  type SettlementOutcome,
  OUTCOME_LABELS,
  STATUS_LABELS,
} from "@/services/settlementService";
import { workSessionService, type OvertimeResultDto } from "@/services/workSessionService";
import { useAppToast } from "@/composables/useAppToast";
import { extractApiError } from "@/utils/apiError";
import {
  Table,
  TableBody,
  TableCell,
  TableEmpty,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import {
  CheckCircleIcon,
  ClockIcon,
  AlertTriangleIcon,
  TrendingUpIcon,
  TrendingDownIcon,
  ScaleIcon,
  DownloadIcon,
  Loader2Icon,
} from "lucide-vue-next";

const toast = useAppToast();

// ─── Month/year selection ─────────────────────────────────────────────────────

const now = new Date();
const selectedYear = ref(now.getFullYear());
const selectedMonth = ref(now.getMonth() + 1); // 1-based

function prevMonth() {
  if (selectedMonth.value === 1) { selectedYear.value--; selectedMonth.value = 12; }
  else selectedMonth.value--;
  load();
}

function nextMonth() {
  if (selectedMonth.value === 12) { selectedYear.value++; selectedMonth.value = 1; }
  else selectedMonth.value++;
  load();
}

const monthLabel = computed(() =>
  new Date(selectedYear.value, selectedMonth.value - 1, 1).toLocaleDateString(undefined, {
    month: "long",
    year: "numeric",
  })
);

// ─── State ────────────────────────────────────────────────────────────────────

const settlements = ref<MonthlySettlementDto[]>([]);
const loading = ref(false);

// Detail / confirm dialog
const detailOpen = ref(false);
const selected = ref<MonthlySettlementDto | null>(null);
const detailOvertime = ref<OvertimeResultDto | null>(null);
const loadingDetail = ref(false);

// Confirm form
const confirmOutcome = ref<SettlementOutcome>("Paid");
const confirmOvertimeOverride = ref<string>("");
const confirmDeficitOverride = ref<string>("");
const confirmNotes = ref("");
const confirming = ref(false);

// ─── Data loading ─────────────────────────────────────────────────────────────

async function load() {
  loading.value = true;
  try {
    settlements.value = await settlementService.getAll(selectedYear.value, selectedMonth.value);
  } catch (err) {
    toast.error(extractApiError(err, "Failed to load settlements"));
  } finally {
    loading.value = false;
  }
}

async function openDetail(s: MonthlySettlementDto) {
  selected.value = s;
  confirmOutcome.value = s.outcome ?? "Paid";
  confirmOvertimeOverride.value = "";
  confirmDeficitOverride.value = "";
  confirmNotes.value = s.notes ?? "";
  detailOpen.value = true;
  loadingDetail.value = true;
  try {
    // Fetch per-day breakdown for this user/month
    detailOvertime.value = await (async () => {
      // We call the admin employee overtime endpoint
      const resp = await import("@/services/api").then((m) =>
        m.default.get(`/admin/employees/${s.userId}/overtime`, {
          params: { year: s.year, month: s.month },
        })
      );
      return resp.data as OvertimeResultDto;
    })();
  } catch {
    detailOvertime.value = null;
  } finally {
    loadingDetail.value = false;
  }
}

// ─── Confirm ─────────────────────────────────────────────────────────────────

async function confirmSettlement() {
  if (!selected.value) return;
  confirming.value = true;
  try {
    await settlementService.confirm(selected.value.id, {
      outcome: confirmOutcome.value,
      overtimeHoursOverride: confirmOvertimeOverride.value
        ? parseFloat(confirmOvertimeOverride.value)
        : undefined,
      deficitHoursOverride: confirmDeficitOverride.value
        ? parseFloat(confirmDeficitOverride.value)
        : undefined,
      notes: confirmNotes.value.trim() || undefined,
    });
    toast.success(`Settlement confirmed for ${selected.value.employeeName}`);
    detailOpen.value = false;
    selected.value = null;
    await load();
  } catch (err: unknown) {
    const anyErr = err as Record<string, unknown>;
    const data =
      (anyErr?.response as Record<string, unknown>)?.data as Record<string, unknown> | undefined;
    if (data?.code === "SETTLEMENT_BLOCKED") {
      const blockers = (data.blockers as Array<{ description: string }>) ?? [];
      const msgs = blockers.map((b) => b.description).join("; ");
      toast.error(`Cannot confirm: ${msgs}`);
    } else {
      toast.error(extractApiError(err, "Failed to confirm settlement"));
    }
  } finally {
    confirming.value = false;
  }
}

// ─── CSV export ───────────────────────────────────────────────────────────────

function exportCsv() {
  const settled = settlements.value.filter((s) => s.status === "Settled");
  if (settled.length === 0) {
    toast.error("No settled records to export for this month.");
    return;
  }

  const rows = [
    ["Employee", "Regular Hours", "Overtime Hours", "Total Hours", "Outcome", "Notes"],
    ...settled.map((s) => {
      const regular = Math.max(0, s.netBalanceHours >= 0
        ? s.overtimeHours > 0 ? s.netBalanceHours - s.overtimeHours : s.netBalanceHours
        : s.netBalanceHours + s.deficitHours);
      return [
        s.employeeName,
        regular.toFixed(2),
        s.overtimeHours.toFixed(2),
        (regular + s.overtimeHours).toFixed(2),
        s.outcome != null ? OUTCOME_LABELS[s.outcome] : "—",
        s.notes ?? "",
      ];
    }),
  ];

  const csv = rows.map((r) => r.map((c) => `"${String(c).replace(/"/g, '""')}"`).join(",")).join("\n");
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `settlements-${selectedYear.value}-${String(selectedMonth.value).padStart(2, "0")}.csv`;
  a.click();
  URL.revokeObjectURL(url);
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

function fmtH(h: number): string {
  const abs = Math.abs(h);
  const hrs = Math.floor(abs);
  const min = Math.round((abs - hrs) * 60);
  const sign = h < 0 ? "-" : "+";
  return `${sign}${hrs}h${min.toString().padStart(2, "0")}m`;
}

function fmtHPlain(h: number): string {
  const hrs = Math.floor(h);
  const min = Math.round((h - hrs) * 60);
  return `${hrs}h${min.toString().padStart(2, "0")}m`;
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
            <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">Settlements</h1>
            <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
              Review and confirm monthly time settlements
            </p>
          </div>
          <Button variant="outline" size="sm" @click="exportCsv">
            <DownloadIcon class="size-3.5" />
            Export CSV
          </Button>
        </div>

        <!-- Month navigation -->
        <div class="flex items-center gap-3 mb-6">
          <Button variant="outline" size="sm" @click="prevMonth">←</Button>
          <p class="text-base font-semibold text-slate-900 dark:text-slate-100 min-w-[160px] text-center">
            {{ monthLabel }}
          </p>
          <Button variant="outline" size="sm" @click="nextMonth">→</Button>
        </div>

        <!-- Table -->
        <div class="card overflow-hidden">
          <div v-if="loading" class="divide-y divide-slate-100 dark:divide-slate-800">
            <div v-for="i in 5" :key="i" class="flex items-center gap-4 px-4 py-3.5">
              <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-32 animate-pulse" />
              <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-16 animate-pulse" />
              <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-20 animate-pulse" />
              <div class="ml-auto h-5 bg-slate-200 dark:bg-slate-700 rounded w-20 animate-pulse" />
            </div>
          </div>

          <Table v-else>
            <TableHeader>
              <TableRow>
                <TableHead>Employee</TableHead>
                <TableHead>Net Balance</TableHead>
                <TableHead>Overtime</TableHead>
                <TableHead>Deficit</TableHead>
                <TableHead>Flags</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Outcome</TableHead>
                <TableHead class="text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              <TableEmpty v-if="settlements.length === 0" :colspan="8">
                <ScaleIcon class="size-8 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
                <p class="text-slate-500 dark:text-slate-400">
                  No settlements generated for this month.
                </p>
              </TableEmpty>

              <TableRow v-for="s in settlements" :key="s.id">
                <!-- Employee -->
                <TableCell class="font-medium text-slate-900 dark:text-slate-100">
                  {{ s.employeeName }}
                </TableCell>

                <!-- Net balance -->
                <TableCell>
                  <span
                    class="inline-flex items-center gap-1 px-2 py-0.5 rounded-md text-xs font-semibold font-mono"
                    :class="s.netBalanceHours >= 0
                      ? 'bg-emerald-50 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-300'
                      : 'bg-rose-50 dark:bg-rose-950 text-rose-700 dark:text-rose-300'"
                  >
                    <component :is="s.netBalanceHours >= 0 ? TrendingUpIcon : TrendingDownIcon" class="size-3" />
                    {{ fmtH(s.netBalanceHours) }}
                  </span>
                </TableCell>

                <!-- Overtime -->
                <TableCell class="font-mono text-xs text-slate-600 dark:text-slate-400">
                  {{ fmtHPlain(s.overtimeHours) }}
                </TableCell>

                <!-- Deficit -->
                <TableCell class="font-mono text-xs text-slate-600 dark:text-slate-400">
                  {{ s.deficitHours > 0 ? fmtHPlain(s.deficitHours) : '—' }}
                </TableCell>

                <!-- Compliance flags placeholder — loaded in detail -->
                <TableCell>
                  <span class="text-xs text-slate-400">See detail</span>
                </TableCell>

                <!-- Status -->
                <TableCell>
                  <span
                    class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium"
                    :class="s.status === 'Settled'
                      ? 'bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-300'
                      : 'bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-300'"
                  >
                    <CheckCircleIcon v-if="s.status === 'Settled'" class="size-3" />
                    <ClockIcon v-else class="size-3" />
                    {{ STATUS_LABELS[s.status] }}
                  </span>
                </TableCell>

                <!-- Outcome -->
                <TableCell>
                  <span v-if="s.outcome !== null" class="text-xs font-medium text-slate-600 dark:text-slate-400">
                    {{ OUTCOME_LABELS[s.outcome!] }}
                  </span>
                  <span v-else class="text-slate-400 text-xs">—</span>
                </TableCell>

                <!-- Actions -->
                <TableCell class="text-right">
                  <Button size="sm" variant="outline" @click="openDetail(s)">
                    {{ s.status === "Settled" ? 'View' : 'Review' }}
                  </Button>
                </TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </div>

      </div>
    </div>

    <!-- Detail / confirm dialog -->
    <Dialog v-model:open="detailOpen">
      <DialogContent class="sm:max-w-[640px] max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>
            {{ selected?.employeeName }} — {{ monthLabel }}
          </DialogTitle>
        </DialogHeader>

        <div v-if="loadingDetail" class="py-6 flex items-center justify-center gap-2 text-slate-500">
          <Loader2Icon class="size-5 animate-spin" />
          Loading breakdown…
        </div>

        <div v-else-if="selected" class="space-y-6 py-1">
          <!-- Summary row -->
          <div class="grid grid-cols-3 gap-4">
            <div class="rounded-lg border border-slate-200 dark:border-slate-700 p-3 text-center">
              <p class="text-xs text-slate-500 uppercase tracking-wide font-medium">Net Balance</p>
              <p
                class="text-2xl font-mono font-bold mt-1"
                :class="selected.netBalanceHours >= 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400'"
              >
                {{ fmtH(selected.netBalanceHours) }}
              </p>
            </div>
            <div class="rounded-lg border border-slate-200 dark:border-slate-700 p-3 text-center">
              <p class="text-xs text-slate-500 uppercase tracking-wide font-medium">Overtime</p>
              <p class="text-2xl font-mono font-bold mt-1 text-slate-900 dark:text-slate-100">
                {{ fmtHPlain(selected.overtimeHours) }}
              </p>
            </div>
            <div class="rounded-lg border border-slate-200 dark:border-slate-700 p-3 text-center">
              <p class="text-xs text-slate-500 uppercase tracking-wide font-medium">Deficit</p>
              <p class="text-2xl font-mono font-bold mt-1 text-slate-900 dark:text-slate-100">
                {{ selected.deficitHours > 0 ? fmtHPlain(selected.deficitHours) : '—' }}
              </p>
            </div>
          </div>

          <!-- Compliance flags -->
          <div v-if="detailOvertime?.complianceFlags?.length">
            <p class="text-xs font-semibold uppercase tracking-wide text-amber-700 dark:text-amber-300 mb-2 flex items-center gap-1.5">
              <AlertTriangleIcon class="size-3.5" />
              Compliance flags
            </p>
            <div class="space-y-1.5">
              <div
                v-for="(flag, i) in detailOvertime.complianceFlags"
                :key="i"
                class="flex items-start gap-2 rounded-lg bg-amber-50 dark:bg-amber-950/30 px-3 py-2 text-xs"
              >
                <AlertTriangleIcon class="size-3.5 text-amber-500 shrink-0 mt-px" />
                <span class="text-amber-800 dark:text-amber-200">
                  {{ flag.date }} — {{ flag.type === 0 ? 'Daily overtime' : 'Weekly overtime' }}:
                  {{ flag.hoursWorked.toFixed(2) }}h worked vs {{ flag.threshold.toFixed(2) }}h threshold
                </span>
              </div>
            </div>
          </div>

          <!-- Per-day table -->
          <div v-if="detailOvertime?.perDay?.length">
            <p class="text-xs font-semibold uppercase tracking-wide text-slate-500 mb-2">Per-day breakdown</p>
            <div class="rounded-lg border border-slate-200 dark:border-slate-700 overflow-hidden">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Date</TableHead>
                    <TableHead class="text-right">Worked</TableHead>
                    <TableHead class="text-right">Target</TableHead>
                    <TableHead class="text-right">Δ</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  <TableRow v-for="day in detailOvertime.perDay" :key="day.date">
                    <TableCell class="text-xs text-slate-600 dark:text-slate-400 font-mono">{{ day.date }}</TableCell>
                    <TableCell class="text-right text-xs font-mono">{{ day.workedHours.toFixed(2) }}h</TableCell>
                    <TableCell class="text-right text-xs font-mono text-slate-500">{{ day.targetHours.toFixed(2) }}h</TableCell>
                    <TableCell class="text-right">
                      <span
                        class="text-xs font-mono font-semibold"
                        :class="day.flexDelta >= 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400'"
                      >
                        {{ fmtH(day.flexDelta) }}
                      </span>
                    </TableCell>
                  </TableRow>
                </TableBody>
              </Table>
            </div>
          </div>

          <!-- Already settled -->
          <div
            v-if="selected.status === 1"
            class="rounded-lg bg-emerald-50 dark:bg-emerald-950/30 border border-emerald-200 dark:border-emerald-800 px-4 py-3"
          >
            <div class="flex items-center gap-2 text-emerald-800 dark:text-emerald-200 font-medium text-sm">
              <CheckCircleIcon class="size-4" />
              Settled — {{ OUTCOME_LABELS[selected.outcome as 0 | 1 | 2] }}
            </div>
            <p v-if="selected.reviewedByName" class="text-xs text-emerald-700 dark:text-emerald-300 mt-1">
              Confirmed by {{ selected.reviewedByName }} on {{ selected.reviewedAt ? new Date(selected.reviewedAt).toLocaleDateString() : '—' }}
            </p>
            <p v-if="selected.notes" class="text-xs text-emerald-700 dark:text-emerald-300 mt-1 italic">
              "{{ selected.notes }}"
            </p>
          </div>

          <!-- Confirm form (only for PendingReview) -->
          <div v-else class="space-y-4 border-t border-slate-200 dark:border-slate-700 pt-4">
            <p class="text-sm font-semibold text-slate-900 dark:text-slate-100">Confirm settlement</p>

            <!-- Outcome -->
            <div class="space-y-1.5">
              <Label>Outcome</Label>
              <div class="flex gap-2">
                <Button
                  v-for="(label, val) in OUTCOME_LABELS"
                  :key="val"
                  :variant="confirmOutcome === val ? 'default' : 'outline'"
                  size="sm"
                  @click="confirmOutcome = val as SettlementOutcome"
                >
                  {{ label }}
                </Button>
              </div>
            </div>

            <!-- Override hours -->
            <div class="grid grid-cols-2 gap-3">
              <div class="space-y-1.5">
                <Label class="text-xs text-slate-500">
                  Overtime hours
                  <span class="font-normal ml-1">(leave blank to use computed {{ fmtHPlain(selected.overtimeHours) }})</span>
                </Label>
                <Input
                  v-model="confirmOvertimeOverride"
                  type="number"
                  step="0.25"
                  min="0"
                  placeholder="e.g. 2.5"
                />
              </div>
              <div class="space-y-1.5">
                <Label class="text-xs text-slate-500">
                  Deficit hours
                  <span class="font-normal ml-1">(leave blank to use computed {{ fmtHPlain(selected.deficitHours) }})</span>
                </Label>
                <Input
                  v-model="confirmDeficitOverride"
                  type="number"
                  step="0.25"
                  min="0"
                  placeholder="e.g. 1.0"
                />
              </div>
            </div>

            <!-- Notes -->
            <div class="space-y-1.5">
              <Label>Notes <span class="text-slate-400 font-normal ml-1">(optional)</span></Label>
              <textarea
                v-model="confirmNotes"
                rows="2"
                class="input-field resize-none text-sm"
                placeholder="Any notes for the employee…"
              />
            </div>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" @click="detailOpen = false">Close</Button>
          <Button
            v-if="selected?.status !== 'Settled'"
            :disabled="confirming"
            @click="confirmSettlement"
          >
            <Loader2Icon v-if="confirming" class="size-4 animate-spin" />
            Confirm settlement
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </AuthenticatedLayout>
</template>
