<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import {
  adminService,
  type Employee,
  type AdminVacationDay,
  type EmployeeTarget,
  type WeekSummary,
} from "../../services/adminService";
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
} from "lucide-vue-next";

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

const target = ref<EmployeeTarget | null>(null);
const weeklySummary = ref<WeekSummary[]>([]);
const targetForm = ref({ dailyHours: "", weeklyHours: "", minimumBreakMinutes: "" });
const savingTarget = ref(false);

const saveTarget = async () => {
  savingTarget.value = true;
  try {
    const daily = targetForm.value.dailyHours ? parseFloat(targetForm.value.dailyHours) : null;
    const weekly = targetForm.value.weeklyHours ? parseFloat(targetForm.value.weeklyHours) : null;
    const minBreak = targetForm.value.minimumBreakMinutes ? parseInt(targetForm.value.minimumBreakMinutes) : null;
    target.value = await adminService.setEmployeeTarget(userId, { dailyHours: daily, weeklyHours: weekly, minimumBreakMinutes: minBreak });
    toast.success("Target saved");
  } catch {
    toast.error("Failed to save target");
  } finally {
    savingTarget.value = false;
  }
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

onMounted(async () => {
  loading.value = true;
  try {
    const [employees, fetchedBalances, fetchedTypes, fetchedDays, fetchedTarget, fetchedSummary] = await Promise.all([
      adminService.getEmployees(),
      vacationTypeService.getEmployeeBalances(userId),
      vacationTypeService.getAll(),
      adminService.getAllVacationDays({ userId }),
      adminService.getEmployeeTarget(userId),
      adminService.getWeeklySummary(userId, 8),
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
    targetForm.value = {
      dailyHours: fetchedTarget.dailyHours != null ? String(fetchedTarget.dailyHours) : "",
      weeklyHours: fetchedTarget.weeklyHours != null ? String(fetchedTarget.weeklyHours) : "",
      minimumBreakMinutes: fetchedTarget.minimumBreakMinutes != null ? String(fetchedTarget.minimumBreakMinutes) : "",
    };
  } catch {
    toast.error("Failed to load employee");
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <AuthenticatedLayout>
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
                <span v-if="target?.resolvedDailyHours || target?.resolvedWeeklyHours || target?.resolvedMinimumBreakMinutes">
                  <span v-if="target?.resolvedDailyHours || target?.resolvedWeeklyHours">
                    <span class="font-medium text-slate-900 dark:text-slate-100">
                      {{ target?.resolvedDailyHours ?? "—" }}h/day
                    </span>
                    ·
                    <span class="font-medium text-slate-900 dark:text-slate-100">
                      {{ target?.resolvedWeeklyHours ?? "—" }}h/week
                    </span>
                  </span>
                  <span v-if="target?.resolvedMinimumBreakMinutes">
                    <span v-if="target?.resolvedDailyHours || target?.resolvedWeeklyHours"> · </span>
                    <span class="font-medium text-slate-900 dark:text-slate-100">
                      {{ target.resolvedMinimumBreakMinutes }}min break
                    </span>
                  </span>
                  <span v-if="target?.hasOverride" class="ml-1.5 inline-flex items-center px-1.5 py-0.5 rounded text-xs font-medium bg-indigo-50 dark:bg-indigo-950 text-indigo-700 dark:text-indigo-300">
                    Override
                  </span>
                  <span v-else class="ml-1.5 text-xs text-slate-400 dark:text-slate-500">(global default)</span>
                </span>
                <span v-else class="text-slate-400 dark:text-slate-500">No target configured</span>
              </div>
            </div>

            <!-- Override form -->
            <div class="flex items-end gap-3 pt-1 flex-wrap">
              <div class="space-y-1.5">
                <Label class="text-xs">Daily (h) — leave blank to use default</Label>
                <Input
                  v-model="targetForm.dailyHours"
                  type="number"
                  min="0"
                  max="24"
                  step="0.5"
                  placeholder="default"
                  class="w-28 h-8 text-sm"
                />
              </div>
              <div class="space-y-1.5">
                <Label class="text-xs">Weekly (h) — leave blank to use default</Label>
                <Input
                  v-model="targetForm.weeklyHours"
                  type="number"
                  min="0"
                  max="168"
                  step="0.5"
                  placeholder="default"
                  class="w-28 h-8 text-sm"
                />
              </div>
              <div class="space-y-1.5">
                <Label class="text-xs">Min. break (min) — leave blank to use default</Label>
                <Input
                  v-model="targetForm.minimumBreakMinutes"
                  type="number"
                  min="1"
                  max="120"
                  step="1"
                  placeholder="default"
                  class="w-28 h-8 text-sm"
                />
              </div>
              <Button size="sm" :disabled="savingTarget" @click="saveTarget">
                <Loader2Icon v-if="savingTarget" class="size-3.5 animate-spin" />
                Save
              </Button>
            </div>

            <!-- Weekly chart -->
            <div v-if="weeklySummary.length > 0" class="pt-2">
              <p class="text-xs font-medium text-slate-500 dark:text-slate-400 mb-3">
                Last 8 weeks — logged vs. target
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
  </AuthenticatedLayout>
</template>
