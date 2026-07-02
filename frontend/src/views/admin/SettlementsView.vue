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
import { adminService } from "@/services/adminService";
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
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import {
  CheckCircleIcon,
  ClockIcon,
  AlertTriangleIcon,
  TrendingUpIcon,
  TrendingDownIcon,
  ScaleIcon,
  DownloadIcon,
  Loader2Icon,
  RefreshCwIcon,
  InfoIcon,
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

const canGenerate = computed(
  () =>
    selectedYear.value < now.getFullYear() ||
    (selectedYear.value === now.getFullYear() && selectedMonth.value < now.getMonth() + 1)
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

const generating = ref(false);

async function generateSettlements() {
  if (!canGenerate.value) {
    toast.error("Settlements can only be generated for a month that has fully ended.");
    return;
  }
  generating.value = true;
  const beforeCount = settlements.value.length;
  try {
    settlements.value = await settlementService.generate(selectedYear.value, selectedMonth.value);
    const newCount = settlements.value.length - beforeCount;
    toast.success(
      newCount === 0
        ? "Already up to date — no new settlements to generate"
        : newCount === 1
          ? "Generated 1 new settlement"
          : `Generated ${newCount} new settlements`
    );
  } catch (err) {
    toast.error(extractApiError(err, "Failed to generate settlements"));
  } finally {
    generating.value = false;
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

async function exportCsv() {
  try {
    await adminService.downloadPayrollExport(selectedYear.value, selectedMonth.value);
  } catch {
    toast.error("Failed to export payroll CSV");
  }
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
            <div class="flex items-center gap-1.5">
              <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">Settlements</h1>
              <TooltipProvider :delay-duration="100">
                <Tooltip>
                  <TooltipTrigger as-child>
                    <InfoIcon class="size-4 text-slate-400 dark:text-slate-500 cursor-pointer" />
                  </TooltipTrigger>
                  <TooltipContent side="right" class="max-w-80 p-3 space-y-2 text-left">
                    <p class="text-xs">
                      A settlement closes out one employee's flex balance (overtime vs. deficit)
                      for a completed month, based on hours worked against their target.
                    </p>
                    <p class="text-xs">
                      <span class="font-semibold">Generate</span> computes this for every employee
                      and creates a "Pending Review" record. <span class="font-semibold">Confirm</span>
                      locks it in with an outcome (Paid / Leave Deducted / Unpaid).
                    </p>
                    <p class="text-xs">
                      <span class="font-semibold">Payroll export impact:</span> once generated, the
                      CSV splits Regular vs. Overtime hours for that employee/month. The Outcome and
                      Notes columns only fill in after the settlement is confirmed — until then they're blank.
                    </p>
                  </TooltipContent>
                </Tooltip>
              </TooltipProvider>
            </div>
            <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
              Review and confirm monthly time settlements
            </p>
          </div>
          <div class="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              :disabled="generating"
              @click="generateSettlements"
            >
              <Loader2Icon v-if="generating" class="size-3.5 animate-spin" />
              <RefreshCwIcon v-else class="size-3.5" />
              Generate settlements
            </Button>
            <Button variant="outline" size="sm" @click="exportCsv">
              <DownloadIcon class="size-3.5" />
              Export CSV
            </Button>
          </div>
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
                  No settlements yet for {{ monthLabel }}.
                </p>
                <p v-if="canGenerate" class="text-xs text-slate-400 dark:text-slate-500 mt-1">
                  Click "Generate settlements" above to create them.
                </p>
                <p v-else class="text-xs text-slate-400 dark:text-slate-500 mt-1">
                  Settlements are created automatically once this month ends.
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
            v-if="selected.status === 'Settled'"
            class="rounded-lg bg-emerald-50 dark:bg-emerald-950/30 border border-emerald-200 dark:border-emerald-800 px-4 py-3"
          >
            <div class="flex items-center gap-2 text-emerald-800 dark:text-emerald-200 font-medium text-sm">
              <CheckCircleIcon class="size-4" />
              Settled — {{ OUTCOME_LABELS[selected.outcome!] }}
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
              <div class="flex items-center gap-1.5">
                <Label>Outcome</Label>
                <TooltipProvider :delay-duration="100">
                  <Tooltip>
                    <TooltipTrigger as-child>
                      <InfoIcon class="size-3.5 text-slate-400 dark:text-slate-500 cursor-pointer" />
                    </TooltipTrigger>
                    <TooltipContent side="top" class="max-w-64 p-2.5 text-left">
                      <p class="text-xs">
                        How you settled this balance with the employee. This doesn't change any
                        hours — it's just a record of your decision, and it fills in the Outcome
                        column in the payroll CSV once confirmed.
                      </p>
                    </TooltipContent>
                  </Tooltip>
                </TooltipProvider>
              </div>
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
                <div class="flex items-center gap-1.5">
                  <Label class="text-xs text-slate-500">
                    Overtime hours
                    <span class="font-normal ml-1">(leave blank to use computed {{ fmtHPlain(selected.overtimeHours) }})</span>
                  </Label>
                  <TooltipProvider :delay-duration="100">
                    <Tooltip>
                      <TooltipTrigger as-child>
                        <InfoIcon class="size-3.5 text-slate-400 dark:text-slate-500 cursor-pointer shrink-0" />
                      </TooltipTrigger>
                      <TooltipContent side="top" class="max-w-64 p-2.5 text-left">
                        <p class="text-xs">
                          Only fill this in to correct the computed value. Whatever ends up here
                          becomes permanent once confirmed, and is what the payroll CSV uses to
                          split Regular vs. Overtime hours for this employee/month.
                        </p>
                      </TooltipContent>
                    </Tooltip>
                  </TooltipProvider>
                </div>
                <Input
                  v-model="confirmOvertimeOverride"
                  type="number"
                  step="0.25"
                  min="0"
                  placeholder="e.g. 2.5"
                />
              </div>
              <div class="space-y-1.5">
                <div class="flex items-center gap-1.5">
                  <Label class="text-xs text-slate-500">
                    Deficit hours
                    <span class="font-normal ml-1">(leave blank to use computed {{ fmtHPlain(selected.deficitHours) }})</span>
                  </Label>
                  <TooltipProvider :delay-duration="100">
                    <Tooltip>
                      <TooltipTrigger as-child>
                        <InfoIcon class="size-3.5 text-slate-400 dark:text-slate-500 cursor-pointer shrink-0" />
                      </TooltipTrigger>
                      <TooltipContent side="top" class="max-w-64 p-2.5 text-left">
                        <p class="text-xs">
                          Only fill this in to correct the computed value. Unlike overtime hours,
                          this is <span class="font-semibold">not</span> included in the payroll
                          CSV — it's kept here purely for your own record.
                        </p>
                      </TooltipContent>
                    </Tooltip>
                  </TooltipProvider>
                </div>
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
              <div class="flex items-center gap-1.5">
                <Label>Notes <span class="text-slate-400 font-normal ml-1">(optional)</span></Label>
                <TooltipProvider :delay-duration="100">
                  <Tooltip>
                    <TooltipTrigger as-child>
                      <InfoIcon class="size-3.5 text-slate-400 dark:text-slate-500 cursor-pointer" />
                    </TooltipTrigger>
                    <TooltipContent side="top" class="max-w-64 p-2.5 text-left">
                      <p class="text-xs">
                        Internal only — the employee never sees this. It's visible here after
                        confirming and included in the payroll CSV's Notes column.
                      </p>
                    </TooltipContent>
                  </Tooltip>
                </TooltipProvider>
              </div>
              <textarea
                v-model="confirmNotes"
                rows="2"
                class="input-field resize-none text-sm"
                placeholder="Internal note for this settlement…"
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
