<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import {
  adminService,
  type Employee,
  type AdminVacationDay,
  type EmployeeTarget,
  type WeekSummary,
  type TimeBankAdjustment,
} from "../../services/adminService";
import { holidayService, type DayOfWeek } from "../../services/holidayService";
import { extractApiError } from "@/utils/apiError";
import {
  vacationTypeService,
  type VacationType,
  type EmployeeVacationBalance,
} from "../../services/vacationTypeService";
import { useAppToast } from "@/composables/useAppToast";
import { useConfirmDialog } from "@/composables/useConfirmDialog";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import WeeklyHoursChart from "@/components/WeeklyHoursChart.vue";
import { useTheme } from "@/composables/useTheme";
import {
  ArrowLeftIcon,
  PlusIcon,
  PencilIcon,
  Trash2Icon,
  CalendarIcon,
  Loader2Icon,
  ClockIcon,
  CheckCircleIcon,
  ScaleIcon,
  InfoIcon,
  RotateCcwIcon,
} from "lucide-vue-next";
import {
  settlementService,
  type MonthlySettlementDto,
  OUTCOME_LABELS,
  STATUS_LABELS,
} from "@/services/settlementService";

const route = useRoute();
const router = useRouter();
const toast = useAppToast();
const { confirm } = useConfirmDialog();
const { isDark } = useTheme();

const userId = route.params.id as string;

const employee = ref<Employee | null>(null);
const balances = ref<EmployeeVacationBalance[]>([]);
const allTypes = ref<VacationType[]>([]);
const vacationDays = ref<AdminVacationDay[]>([]);
const loading = ref(false);

// ─── Working hours target ─────────────────────────────────────────────────────

const DAYS_MON_SUN: DayOfWeek[] = [
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
  "Sunday",
];

const target = ref<EmployeeTarget | null>(null);
const weeklySummary = ref<WeekSummary[]>([]);
const globalWorkdayTargets = ref<Record<DayOfWeek, number>>({
  Monday: 0,
  Tuesday: 0,
  Wednesday: 0,
  Thursday: 0,
  Friday: 0,
  Saturday: 0,
  Sunday: 0,
});
// Blank = inherit the global default for that day.
const employeeWorkdayTargets = ref<Record<DayOfWeek, string>>({
  Monday: "",
  Tuesday: "",
  Wednesday: "",
  Thursday: "",
  Friday: "",
  Saturday: "",
  Sunday: "",
});
const savingWorkdayTargets = ref(false);
const minBreakForm = ref("");
const savingMinBreak = ref(false);

const hasWorkdayOverride = computed(() =>
  DAYS_MON_SUN.some((day) => employeeWorkdayTargets.value[day] !== "")
);

const weeklyResolvedHours = computed(() =>
  DAYS_MON_SUN.reduce((sum, day) => {
    const override = employeeWorkdayTargets.value[day];
    const hours = override !== "" ? parseFloat(override) : globalWorkdayTargets.value[day];
    return sum + (Number.isFinite(hours) ? hours : 0);
  }, 0)
);

const saveWorkdayTargets = async () => {
  savingWorkdayTargets.value = true;
  try {
    const targets = DAYS_MON_SUN.filter((day) => employeeWorkdayTargets.value[day] !== "").map(
      (day) => ({ dayOfWeek: day, hours: parseFloat(employeeWorkdayTargets.value[day]) })
    );
    await adminService.setEmployeeWorkdayTargets(userId, targets);
    toast.success("Working hours target saved");
  } catch {
    toast.error("Failed to save working hours target");
  } finally {
    savingWorkdayTargets.value = false;
  }
};

const resetWorkdayDay = async (day: DayOfWeek) => {
  employeeWorkdayTargets.value[day] = "";
  await saveWorkdayTargets();
};

const saveMinBreak = async () => {
  savingMinBreak.value = true;
  try {
    const minBreak = minBreakForm.value ? parseInt(minBreakForm.value) : null;
    target.value = await adminService.setEmployeeTarget(userId, { minimumBreakMinutes: minBreak });
    toast.success("Minimum break saved");
  } catch {
    toast.error("Failed to save minimum break");
  } finally {
    savingMinBreak.value = false;
  }
};

const resetMinBreak = async () => {
  minBreakForm.value = "";
  await saveMinBreak();
};

// ─── Assign dialog ────────────────────────────────────────────────────────────

const dialogVisible = ref(false);
const saving = ref(false);
const editingBalance = ref<EmployeeVacationBalance | null>(null);

const form = ref<{ vacationTypeId: string; yearlyBalance: string }>({
  vacationTypeId: "",
  yearlyBalance: "",
});

const assignedTypeIds = computed(() => new Set(balances.value.map((b) => b.vacationTypeId)));

const availableTypes = computed(() =>
  allTypes.value.filter(
    (t) => !assignedTypeIds.value.has(t.id) || editingBalance.value?.vacationTypeId === t.id
  )
);

const dialogTitle = computed(() =>
  editingBalance.value ? "Edit balance" : "Assign vacation type"
);

const openAssign = () => {
  editingBalance.value = null;
  form.value = { vacationTypeId: "", yearlyBalance: "" };
  dialogVisible.value = true;
};

const openEdit = (balance: EmployeeVacationBalance) => {
  editingBalance.value = balance;
  form.value = {
    vacationTypeId: String(balance.vacationTypeId),
    yearlyBalance: String(balance.yearlyBalance),
  };
  dialogVisible.value = true;
};

const saveBalance = async () => {
  const days = parseFloat(form.value.yearlyBalance);
  if (isNaN(days) || days < 0) return;
  saving.value = true;
  try {
    if (editingBalance.value) {
      const updated = await vacationTypeService.updateBalance(
        userId,
        editingBalance.value.id,
        days
      );
      const idx = balances.value.findIndex((b) => b.id === editingBalance.value!.id);
      if (idx !== -1) balances.value[idx] = updated;
      toast.success("Balance updated");
    } else {
      const typeId = parseInt(form.value.vacationTypeId);
      if (!typeId) return;
      const created = await vacationTypeService.assignType(userId, {
        vacationTypeId: typeId,
        yearlyBalance: days,
      });
      balances.value.push(created);
      balances.value.sort((a, b) => a.vacationTypeName.localeCompare(b.vacationTypeName));
      toast.success("Vacation type assigned");
    }
    dialogVisible.value = false;
  } catch {
    toast.error("Failed to save");
  } finally {
    saving.value = false;
  }
};

// ─── Remove ───────────────────────────────────────────────────────────────────

const removeBalance = (balance: EmployeeVacationBalance) => {
  confirm({
    title: "Remove vacation type",
    message: `Remove "${balance.vacationTypeName}" from ${employee.value?.fullName ?? "this employee"}?`,
    confirmLabel: "Remove",
    variant: "destructive",
    onConfirm: async () => {
      try {
        await vacationTypeService.removeBalance(userId, balance.id);
        balances.value = balances.value.filter((b) => b.id !== balance.id);
        toast.success("Vacation type removed");
      } catch {
        toast.error("Failed to remove");
      }
    },
  });
};

// ─── Mount ────────────────────────────────────────────────────────────────────

const initials = computed(
  () =>
    employee.value?.fullName
      .split(" ")
      .map((n) => n[0])
      .join("")
      .substring(0, 2)
      .toUpperCase() ?? ""
);

const currentYear = new Date().getUTCFullYear();

const usedByType = computed(() => {
  const map = new Map<number, number>();
  for (const d of vacationDays.value) {
    if (new Date(d.date).getUTCFullYear() === currentYear) {
      map.set(d.vacationTypeId, (map.get(d.vacationTypeId) ?? 0) + d.amount);
    }
  }
  return map;
});

const displayDate = (iso: string) =>
  new Date(iso).toLocaleDateString(undefined, { day: "numeric", month: "short", year: "numeric" });

// ─── Settlement history ───────────────────────────────────────────────────────

const settlementHistory = ref<MonthlySettlementDto[]>([]);
const loadingSettlements = ref(false);

function fmtH(h: number): string {
  const abs = Math.abs(h);
  const hrs = Math.floor(abs);
  const min = Math.round((abs - hrs) * 60);
  const sign = h < 0 ? "-" : "+";
  return `${sign}${hrs}h${min.toString().padStart(2, "0")}m`;
}

onMounted(async () => {
  loading.value = true;
  try {
    const [
      employees,
      fetchedBalances,
      fetchedTypes,
      fetchedDays,
      fetchedTarget,
      fetchedSummary,
      fetchedGlobalTargets,
      fetchedEmployeeTargets,
    ] = await Promise.all([
      adminService.getEmployees(),
      vacationTypeService.getEmployeeBalances(userId),
      vacationTypeService.getAll(),
      adminService.getAllVacationDays({ userId }),
      adminService.getEmployeeTarget(userId),
      adminService.getWeeklySummary(userId, 8),
      holidayService.getGlobalWorkdayTargets(),
      adminService.getEmployeeWorkdayTargets(userId),
    ]);
    employee.value = employees.find((e) => e.id === userId) ?? null;
    if (!employee.value) {
      router.push({ name: "admin-employees" });
      return;
    }
    balances.value = fetchedBalances;
    allTypes.value = fetchedTypes;
    vacationDays.value = fetchedDays;
    target.value = fetchedTarget;
    weeklySummary.value = fetchedSummary;
    minBreakForm.value = fetchedTarget.minimumBreakMinutes != null ? String(fetchedTarget.minimumBreakMinutes) : "";
    for (const t of fetchedGlobalTargets) globalWorkdayTargets.value[t.dayOfWeek] = t.hours;
    for (const t of fetchedEmployeeTargets) employeeWorkdayTargets.value[t.dayOfWeek] = String(t.hours);
  } catch {
    toast.error("Failed to load employee");
  } finally {
    loading.value = false;
  }

  // Load settlement history in background (non-blocking)
  loadingSettlements.value = true;
  settlementService.getEmployeeHistory(userId)
    .then((data) => { settlementHistory.value = data; })
    .catch(() => { /* non-critical */ })
    .finally(() => { loadingSettlements.value = false; });

  // Load flex balance adjustments in background (non-blocking)
  loadAdjustments();
});

// ─── Flex balance adjustments ──────────────────────────────────────────────────

const adjustments = ref<TimeBankAdjustment[]>([]);
const loadingAdjustments = ref(false);
const adjustmentDialogVisible = ref(false);
const savingAdjustment = ref(false);
const adjustmentForm = ref({ effectiveDate: "", hours: "", reason: "" });

async function loadAdjustments() {
  loadingAdjustments.value = true;
  try {
    adjustments.value = await adminService.getTimeBankAdjustments(userId);
  } catch {
    /* non-critical */
  } finally {
    loadingAdjustments.value = false;
  }
}

function openAddAdjustment() {
  const today = new Date();
  const firstOfNextMonth = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth() + 1, 1));
  adjustmentForm.value = {
    effectiveDate: firstOfNextMonth.toISOString().slice(0, 10),
    hours: "",
    reason: "",
  };
  adjustmentDialogVisible.value = true;
}

async function saveAdjustment() {
  const hours = parseFloat(adjustmentForm.value.hours);
  if (!adjustmentForm.value.effectiveDate || isNaN(hours) || hours === 0 || !adjustmentForm.value.reason.trim()) {
    return;
  }
  savingAdjustment.value = true;
  try {
    const created = await adminService.createTimeBankAdjustment(userId, {
      effectiveDate: adjustmentForm.value.effectiveDate,
      hours,
      reason: adjustmentForm.value.reason.trim(),
    });
    adjustments.value.unshift(created);
    toast.success("Flex balance adjustment added");
    adjustmentDialogVisible.value = false;
  } catch (err) {
    toast.error(extractApiError(err, "Failed to add adjustment"));
  } finally {
    savingAdjustment.value = false;
  }
}

function removeAdjustment(adjustment: TimeBankAdjustment) {
  confirm({
    title: "Delete adjustment",
    message: `Delete the ${adjustment.hours > 0 ? "+" : ""}${adjustment.hours}h adjustment for ${displayDate(adjustment.effectiveDate)}?`,
    confirmLabel: "Delete",
    variant: "destructive",
    onConfirm: async () => {
      try {
        await adminService.deleteTimeBankAdjustment(adjustment.id);
        adjustments.value = adjustments.value.filter((a) => a.id !== adjustment.id);
        toast.success("Adjustment deleted");
      } catch (err) {
        toast.error(extractApiError(err, "Failed to delete adjustment"));
      }
    },
  });
}
</script>

<template>
  <div class="p-6 lg:p-8">
    <div class="max-w-3xl mx-auto">
      <!-- Back -->
      <button
        @click="router.push({ name: 'admin-employees' })"
        class="flex items-center gap-1.5 text-sm text-slate-500 dark:text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 mb-6 transition-colors"
      >
        <ArrowLeftIcon class="size-3.5" />
        All employees
      </button>

      <!-- Employee header skeleton -->
      <div v-if="loading" class="flex items-center gap-4 mb-8">
        <div
          class="w-14 h-14 rounded-full bg-slate-200 dark:bg-slate-700 animate-pulse shrink-0"
        />
        <div class="space-y-2">
          <div class="h-4 bg-slate-200 dark:bg-slate-700 rounded w-40 animate-pulse" />
          <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-56 animate-pulse" />
        </div>
      </div>

      <!-- Employee header -->
      <div v-else-if="employee" class="flex items-center gap-4 mb-8">
        <div class="w-14 h-14 rounded-full user-avatar shrink-0">
          <span class="text-lg font-bold text-white">{{ initials }}</span>
        </div>
        <div>
          <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">
            {{ employee.fullName }}
          </h1>
          <p class="text-sm text-slate-500 dark:text-slate-400">{{ employee.email }}</p>
        </div>
      </div>

      <!-- Vacation balances section -->
      <div>
        <div class="flex items-center justify-between mb-3">
          <h2 class="text-sm font-semibold text-slate-700 dark:text-slate-300">
            Vacation balances
          </h2>
          <Button
            variant="outline"
            size="sm"
            @click="openAssign"
            :disabled="availableTypes.length === 0"
            :title="
              availableTypes.length === 0 ? 'All vacation types are already assigned' : undefined
            "
          >
            <PlusIcon class="size-3.5" />
            Assign type
          </Button>
        </div>

        <!-- Loading skeleton -->
        <div v-if="loading" class="card divide-y divide-slate-100 dark:divide-slate-800">
          <div v-for="i in 2" :key="i" class="flex items-center gap-4 px-5 py-4">
            <div
              class="w-3 h-3 rounded-full bg-slate-200 dark:bg-slate-700 animate-pulse shrink-0"
            />
            <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-32 animate-pulse flex-1" />
            <div class="h-6 bg-slate-200 dark:bg-slate-700 rounded w-16 animate-pulse" />
          </div>
        </div>

        <!-- Empty state -->
        <div v-else-if="balances.length === 0" class="card text-center py-10">
          <CalendarIcon class="size-8 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
          <p class="text-sm text-slate-500 dark:text-slate-400 mb-3">
            No vacation types assigned yet.
          </p>
          <Button v-if="allTypes.length > 0" variant="outline" size="sm" @click="openAssign">
            Assign a type
          </Button>
          <p v-else class="text-xs text-slate-400 dark:text-slate-500">
            Create vacation types first in
            <router-link
              :to="{ name: 'admin-vacation-types' }"
              class="text-primary hover:underline"
            >
              Vacation Types </router-link
            >.
          </p>
        </div>

        <!-- Balances list -->
        <div v-else class="card divide-y divide-slate-100 dark:divide-slate-800 overflow-hidden">
          <div v-for="balance in balances" :key="balance.id" class="px-5 py-4">
            <div class="flex items-center gap-4 mb-2">
              <div
                class="w-3 h-3 rounded-full shrink-0 ring-1 ring-black/10"
                :style="{ backgroundColor: balance.vacationTypeColor ?? '#6366f1' }"
              />
              <span class="flex-1 text-sm font-medium text-slate-900 dark:text-slate-100">
                {{ balance.vacationTypeName }}
              </span>
              <div class="flex items-center gap-1 shrink-0">
                <Button
                  variant="ghost"
                  size="icon"
                  @click="openEdit(balance)"
                  class="size-8 text-slate-400 hover:text-slate-700 dark:hover:text-slate-200"
                  title="Edit balance"
                >
                  <PencilIcon class="size-3.5" />
                </Button>
                <Button
                  variant="ghost"
                  size="icon"
                  @click="removeBalance(balance)"
                  class="size-8 text-slate-400 hover:text-red-500 dark:hover:text-red-400"
                  title="Remove"
                >
                  <Trash2Icon class="size-3.5" />
                </Button>
              </div>
            </div>
            <!-- Usage bar -->
            <div class="w-full bg-slate-100 dark:bg-slate-800 rounded-full h-1.5 mb-1.5">
              <div
                :class="[
                  'h-1.5 rounded-full transition-all',
                  (usedByType.get(balance.vacationTypeId) ?? 0) >= balance.yearlyBalance
                    ? 'bg-red-500'
                    : (usedByType.get(balance.vacationTypeId) ?? 0) / balance.yearlyBalance >= 0.8
                      ? 'bg-amber-400'
                      : 'bg-emerald-500',
                ]"
                :style="{
                  width:
                    balance.yearlyBalance > 0
                      ? `${Math.min(((usedByType.get(balance.vacationTypeId) ?? 0) / balance.yearlyBalance) * 100, 100)}%`
                      : '0%',
                }"
              />
            </div>
            <div class="flex justify-between text-xs text-slate-500 dark:text-slate-400">
              <span
                >{{ usedByType.get(balance.vacationTypeId) ?? 0 }} /
                {{ balance.yearlyBalance }} days used ({{ new Date().getFullYear() }})</span
              >
              <span
                :class="
                  balance.yearlyBalance - (usedByType.get(balance.vacationTypeId) ?? 0) <= 0
                    ? 'text-red-600 dark:text-red-400 font-medium'
                    : 'text-emerald-600 dark:text-emerald-400 font-medium'
                "
                >{{
                  balance.yearlyBalance - (usedByType.get(balance.vacationTypeId) ?? 0)
                }}
                remaining</span
              >
            </div>
          </div>
        </div>
      </div>

      <!-- Working hours target section -->
      <div class="mt-6" v-if="!loading">
        <h2 class="text-sm font-semibold text-slate-700 dark:text-slate-300 mb-3">
          Working hours target
        </h2>

        <div class="card p-5 space-y-4">
          <!-- Current resolved targets -->
          <div class="flex items-center gap-3">
            <ClockIcon class="size-4 text-indigo-500 shrink-0" />
            <div class="text-sm text-slate-600 dark:text-slate-400">
              <span class="font-medium text-slate-900 dark:text-slate-100">
                {{ weeklyResolvedHours }}h/week
              </span>
              <span v-if="target?.resolvedMinimumBreakMinutes">
                ·
                <span class="font-medium text-slate-900 dark:text-slate-100">
                  {{ target.resolvedMinimumBreakMinutes }}min break
                </span>
              </span>
              <span v-if="hasWorkdayOverride || target?.hasOverride" class="ml-1.5 inline-flex items-center px-1.5 py-0.5 rounded text-xs font-medium bg-indigo-50 dark:bg-indigo-950 text-indigo-700 dark:text-indigo-300">
                Override
              </span>
              <span v-else class="ml-1.5 text-xs text-slate-400 dark:text-slate-500">(global default)</span>
            </div>
          </div>

          <!-- Per-weekday override — blank = inherit the global default for that day -->
          <div class="rounded-lg border border-slate-100 dark:border-slate-800 divide-y divide-slate-100 dark:divide-slate-800">
            <div
              v-for="day in DAYS_MON_SUN"
              :key="day"
              class="flex items-center justify-between gap-3 px-4 py-2"
            >
              <span class="text-sm text-slate-700 dark:text-slate-300">{{ day }}</span>
              <div class="flex items-center gap-1">
                <Input
                  v-model="employeeWorkdayTargets[day]"
                  type="number"
                  min="0"
                  max="24"
                  step="0.5"
                  :placeholder="String(globalWorkdayTargets[day])"
                  class="w-24 h-8 text-sm"
                />
                <Button
                  variant="ghost"
                  size="icon-sm"
                  :class="employeeWorkdayTargets[day] === '' && 'invisible pointer-events-none'"
                  :disabled="savingWorkdayTargets || employeeWorkdayTargets[day] === ''"
                  title="Reset to default"
                  @click="resetWorkdayDay(day)"
                >
                  <RotateCcwIcon class="size-3.5 text-slate-400" />
                </Button>
              </div>
            </div>
          </div>
          <Button size="sm" :disabled="savingWorkdayTargets" @click="saveWorkdayTargets">
            <Loader2Icon v-if="savingWorkdayTargets" class="size-3.5 animate-spin" />
            Save hours
          </Button>

          <!-- Minimum break override -->
          <div class="flex items-end gap-3 pt-1 border-t border-slate-100 dark:border-slate-800">
            <div class="space-y-1.5 pt-4">
              <Label class="text-xs">Min. break (min) — leave blank to use default</Label>
              <div class="flex items-center gap-1">
                <Input
                  v-model="minBreakForm"
                  type="number"
                  min="1"
                  max="120"
                  step="1"
                  placeholder="default"
                  class="w-28 h-8 text-sm"
                />
                <Button
                  variant="ghost"
                  size="icon-sm"
                  :class="minBreakForm === '' && 'invisible pointer-events-none'"
                  :disabled="savingMinBreak || minBreakForm === ''"
                  title="Reset to default"
                  @click="resetMinBreak"
                >
                  <RotateCcwIcon class="size-3.5 text-slate-400" />
                </Button>
              </div>
            </div>
            <Button size="sm" :disabled="savingMinBreak" @click="saveMinBreak">
              <Loader2Icon v-if="savingMinBreak" class="size-3.5 animate-spin" />
              Save
            </Button>
          </div>

          <!-- Weekly chart -->
          <div v-if="weeklySummary.length > 0" class="pt-2">
            <p class="text-xs font-medium text-slate-500 dark:text-slate-400 mb-0.5">
              Last 8 weeks — logged vs. target
            </p>
            <p class="text-xs text-slate-400 dark:text-slate-500 mb-3">
              Logged = actual hours worked · Target = configured weekly target
            </p>
            <WeeklyHoursChart :weeks="weeklySummary" :is-dark="isDark" />
          </div>
        </div>
      </div>

      <!-- Planned vacation days section -->
      <div class="mt-6" v-if="!loading">
        <h2 class="text-sm font-semibold text-slate-700 dark:text-slate-300 mb-3">
          Planned vacation days
        </h2>

        <div v-if="vacationDays.length === 0" class="card text-center py-8">
          <CalendarIcon class="size-6 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
          <p class="text-sm text-slate-500 dark:text-slate-400">No vacation days planned.</p>
        </div>

        <div v-else class="card divide-y divide-slate-100 dark:divide-slate-800 overflow-hidden">
          <div
            v-for="day in vacationDays"
            :key="day.id"
            class="flex items-center gap-3 px-5 py-3"
          >
            <span class="text-sm font-medium text-slate-900 dark:text-slate-100 w-28 shrink-0">
              {{ displayDate(day.date) }}
            </span>
            <div class="flex items-center gap-2 flex-1 min-w-0">
              <div
                class="w-2.5 h-2.5 rounded-full shrink-0 ring-1 ring-black/10"
                :style="{ backgroundColor: day.vacationTypeColor ?? '#6366f1' }"
              />
              <span class="text-sm text-slate-600 dark:text-slate-400 truncate">
                {{ day.vacationTypeName }}
                <span v-if="day.note" class="text-slate-400 dark:text-slate-500">
                  · {{ day.note }}</span
                >
              </span>
            </div>
            <span
              :class="[
                'text-xs font-medium px-1.5 py-0.5 rounded shrink-0',
                day.amount === 1
                  ? 'bg-indigo-50 dark:bg-indigo-950 text-indigo-700 dark:text-indigo-300'
                  : 'bg-sky-50 dark:bg-sky-950 text-sky-700 dark:text-sky-300',
              ]"
              >{{ day.amount === 1 ? "Full day" : "Half day" }}</span
            >
          </div>
        </div>
      </div>

      <!-- Settlement history section -->
      <div class="mt-6">
        <div class="flex items-center justify-between mb-3">
          <h2 class="text-sm font-semibold text-slate-700 dark:text-slate-300 flex items-center gap-1.5">
            <ScaleIcon class="size-4" />
            Settlement history
          </h2>
          <RouterLink
            :to="{ name: 'admin-settlements' }"
            class="text-xs text-indigo-600 dark:text-indigo-400 hover:underline"
          >
            View all settlements →
          </RouterLink>
        </div>

        <div v-if="loadingSettlements" class="card divide-y divide-slate-100 dark:divide-slate-800">
          <div v-for="i in 3" :key="i" class="flex items-center gap-4 px-4 py-3">
            <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-20 animate-pulse" />
            <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-12 animate-pulse" />
            <div class="ml-auto h-5 bg-slate-200 dark:bg-slate-700 rounded w-16 animate-pulse" />
          </div>
        </div>

        <div v-else-if="settlementHistory.length === 0" class="card text-center py-8">
          <ScaleIcon class="size-6 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
          <p class="text-sm text-slate-500 dark:text-slate-400">No settlements generated yet.</p>
        </div>

        <div v-else class="card divide-y divide-slate-100 dark:divide-slate-800 overflow-hidden">
          <div
            v-for="s in settlementHistory"
            :key="s.id"
            class="flex items-center gap-3 px-4 py-3"
          >
            <span class="text-sm font-medium text-slate-900 dark:text-slate-100 w-24 shrink-0 font-mono">
              {{ s.year }}-{{ String(s.month).padStart(2, '0') }}
            </span>
            <span
              class="text-xs font-mono px-1.5 py-0.5 rounded"
              :class="s.netBalanceHours >= 0
                ? 'bg-emerald-50 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-300'
                : 'bg-rose-50 dark:bg-rose-950 text-rose-700 dark:text-rose-300'"
            >
              {{ fmtH(s.netBalanceHours) }}
            </span>
            <span class="text-xs text-slate-500 dark:text-slate-400">
              {{ s.outcome !== null ? OUTCOME_LABELS[s.outcome!] : '—' }}
            </span>
            <div class="ml-auto flex items-center gap-2">
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
            </div>
          </div>
        </div>
      </div>

      <!-- Flex balance adjustments section -->
      <div class="mt-6" v-if="!loading">
        <div class="flex items-center justify-between mb-3">
          <div class="flex items-center gap-1.5">
            <h2 class="text-sm font-semibold text-slate-700 dark:text-slate-300 flex items-center gap-1.5">
              <ScaleIcon class="size-4" />
              Flex balance adjustments
            </h2>
            <TooltipProvider :delay-duration="100">
              <Tooltip>
                <TooltipTrigger as-child>
                  <InfoIcon class="size-3.5 text-slate-400 dark:text-slate-500 cursor-pointer" />
                </TooltipTrigger>
                <TooltipContent side="top" class="max-w-72 p-2.5 text-left">
                  <p class="text-xs">
                    Manually add or deduct hours from a specific month's flex balance —
                    e.g. carry forward a deficit from last month so it must be made up this
                    month, or grant extra flex hours. Positive hours add to the balance,
                    negative hours are subtracted. Adjustments can't be deleted once that
                    month's settlement is confirmed.
                  </p>
                </TooltipContent>
              </Tooltip>
            </TooltipProvider>
          </div>
          <Button variant="outline" size="sm" @click="openAddAdjustment">
            <PlusIcon class="size-3.5" />
            Add adjustment
          </Button>
        </div>

        <div v-if="loadingAdjustments" class="card divide-y divide-slate-100 dark:divide-slate-800">
          <div v-for="i in 2" :key="i" class="flex items-center gap-4 px-4 py-3">
            <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-20 animate-pulse" />
            <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-40 animate-pulse flex-1" />
            <div class="h-5 bg-slate-200 dark:bg-slate-700 rounded w-14 animate-pulse" />
          </div>
        </div>

        <div v-else-if="adjustments.length === 0" class="card text-center py-8">
          <ScaleIcon class="size-6 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
          <p class="text-sm text-slate-500 dark:text-slate-400">No manual adjustments yet.</p>
        </div>

        <div v-else class="card divide-y divide-slate-100 dark:divide-slate-800 overflow-hidden">
          <div
            v-for="a in adjustments"
            :key="a.id"
            class="flex items-center gap-3 px-4 py-3"
          >
            <span class="text-sm font-medium text-slate-900 dark:text-slate-100 w-24 shrink-0">
              {{ displayDate(a.effectiveDate) }}
            </span>
            <span
              class="text-xs font-mono px-1.5 py-0.5 rounded shrink-0"
              :class="a.hours >= 0
                ? 'bg-emerald-50 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-300'
                : 'bg-rose-50 dark:bg-rose-950 text-rose-700 dark:text-rose-300'"
            >
              {{ a.hours > 0 ? '+' : '' }}{{ a.hours }}h
            </span>
            <span class="text-sm text-slate-600 dark:text-slate-400 truncate flex-1 min-w-0">
              {{ a.reason }}
            </span>
            <span
              v-if="a.sourceSettlementId != null"
              class="text-[10px] font-medium uppercase tracking-wide px-1.5 py-0.5 rounded bg-slate-100 dark:bg-slate-800 text-slate-500 dark:text-slate-400 shrink-0"
              title="Created automatically by confirming a monthly settlement — cannot be deleted manually"
            >
              Settlement
            </span>
            <Button
              v-else
              variant="ghost"
              size="icon"
              @click="removeAdjustment(a)"
              class="size-8 text-slate-400 hover:text-red-500 dark:hover:text-red-400 shrink-0"
              title="Delete"
            >
              <Trash2Icon class="size-3.5" />
            </Button>
          </div>
        </div>
      </div>
    </div>
  </div>

  <!-- Assign / Edit dialog -->
  <Dialog v-model:open="dialogVisible">
    <DialogContent class="sm:max-w-[380px]">
      <DialogHeader>
        <DialogTitle>{{ dialogTitle }}</DialogTitle>
      </DialogHeader>

      <div class="flex flex-col gap-4 py-2">
        <div v-if="!editingBalance" class="space-y-1.5">
          <Label>Vacation type <span class="text-destructive">*</span></Label>
          <Select v-model="form.vacationTypeId">
            <SelectTrigger class="w-full">
              <SelectValue placeholder="Select a type" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem v-for="type in availableTypes" :key="type.id" :value="String(type.id)">
                {{ type.name }}
              </SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div class="space-y-1.5">
          <Label>Yearly balance (days) <span class="text-destructive">*</span></Label>
          <Input
            v-model="form.yearlyBalance"
            type="number"
            min="0"
            step="0.5"
            placeholder="e.g. 12"
          />
        </div>
      </div>

      <DialogFooter>
        <Button variant="outline" @click="dialogVisible = false">Cancel</Button>
        <Button
          @click="saveBalance"
          :disabled="
            saving || (!editingBalance && !form.vacationTypeId) || form.yearlyBalance === ''
          "
        >
          <Loader2Icon v-if="saving" class="size-4 animate-spin" />
          {{ editingBalance ? "Save changes" : "Assign" }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>

  <!-- Add flex balance adjustment dialog -->
  <Dialog v-model:open="adjustmentDialogVisible">
    <DialogContent class="sm:max-w-[420px]">
      <DialogHeader>
        <DialogTitle>Add flex balance adjustment</DialogTitle>
      </DialogHeader>

      <div class="flex flex-col gap-4 py-2">
        <div class="space-y-1.5">
          <Label>Effective date <span class="text-destructive">*</span></Label>
          <Input v-model="adjustmentForm.effectiveDate" type="date" />
          <p class="text-xs text-slate-400 dark:text-slate-500">
            Any date within the target month — determines which month's balance this affects.
          </p>
        </div>
        <div class="space-y-1.5">
          <Label>Hours <span class="text-destructive">*</span></Label>
          <Input
            v-model="adjustmentForm.hours"
            type="number"
            step="0.25"
            placeholder="e.g. 3 to add, -3 to deduct"
          />
          <p class="text-xs text-slate-400 dark:text-slate-500">
            Positive adds to the flex balance, negative deducts from it.
          </p>
        </div>
        <div class="space-y-1.5">
          <Label>Reason <span class="text-destructive">*</span></Label>
          <textarea
            v-model="adjustmentForm.reason"
            rows="2"
            class="input-field resize-none text-sm"
            placeholder="e.g. Carry forward June deficit"
          />
        </div>
      </div>

      <DialogFooter>
        <Button variant="outline" @click="adjustmentDialogVisible = false">Cancel</Button>
        <Button
          @click="saveAdjustment"
          :disabled="
            savingAdjustment ||
            !adjustmentForm.effectiveDate ||
            !adjustmentForm.hours ||
            !adjustmentForm.reason.trim()
          "
        >
          <Loader2Icon v-if="savingAdjustment" class="size-4 animate-spin" />
          Add adjustment
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
