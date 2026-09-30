<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { adminService, type Employee } from "../../services/adminService";
import { useAppToast } from "@/composables/useAppToast";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { DownloadIcon } from "lucide-vue-next";

const toast = useAppToast();

const employees = ref<Employee[]>([]);
const selectedEmployeeId = ref<string>("all");
const loading = ref(false);
// Which export is running, so only that button shows the spinner text.
const exporting = ref<"classic" | "daily" | null>(null);

const now = new Date();
const selectedYear = ref<string>(String(now.getFullYear()));
const selectedMonth = ref<string>(String(now.getMonth() + 1));

const years = Array.from({ length: 5 }, (_, i) => now.getFullYear() - i);
const months = [
  { value: "1", label: "January" },
  { value: "2", label: "February" },
  { value: "3", label: "March" },
  { value: "4", label: "April" },
  { value: "5", label: "May" },
  { value: "6", label: "June" },
  { value: "7", label: "July" },
  { value: "8", label: "August" },
  { value: "9", label: "September" },
  { value: "10", label: "October" },
  { value: "11", label: "November" },
  { value: "12", label: "December" },
];

const selectedMonthLabel = computed(
  () => months.find((m) => m.value === selectedMonth.value)?.label ?? ""
);

const selectedEmployeeName = computed(() => {
  if (selectedEmployeeId.value === "all") return "all employees";
  return employees.value.find((e) => e.id === selectedEmployeeId.value)?.fullName ?? "";
});

const handleExport = async (kind: "classic" | "daily") => {
  exporting.value = kind;
  const userId = selectedEmployeeId.value === "all" ? undefined : selectedEmployeeId.value;
  const year = Number(selectedYear.value);
  const month = Number(selectedMonth.value);
  try {
    if (kind === "daily") {
      await adminService.downloadDailyPayrollExport(
        year,
        month,
        userId,
        userId ? selectedEmployeeName.value : undefined
      );
    } else {
      await adminService.downloadPayrollExport(year, month, userId);
    }
    toast.success(`Payroll exported for ${selectedMonthLabel.value} ${selectedYear.value}`);
  } catch {
    toast.error("Failed to generate export");
  } finally {
    exporting.value = null;
  }
};

onMounted(async () => {
  loading.value = true;
  try {
    // Admins don't track time; disabled employees stay listed for past months.
    employees.value = await adminService.getEmployees("Employee");
  } catch {
    toast.error("Failed to load employees");
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <div class="p-6 lg:p-8">
    <div class="max-w-2xl mx-auto">
      <!-- Header -->
      <div class="mb-8">
        <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">Payroll Export</h1>
        <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
          Download a monthly payroll summary as CSV — ready for HR or payroll processing.
        </p>
      </div>

      <!-- Export card -->
      <div class="card p-6 space-y-6">
        <!-- Period row -->
        <div class="grid grid-cols-2 gap-4">
          <div class="space-y-1.5">
            <Label>Month</Label>
            <Select v-model="selectedMonth">
              <SelectTrigger class="w-full">
                <SelectValue placeholder="Month" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem v-for="m in months" :key="m.value" :value="m.value">
                  {{ m.label }}
                </SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div class="space-y-1.5">
            <Label>Year</Label>
            <Select v-model="selectedYear">
              <SelectTrigger class="w-full">
                <SelectValue placeholder="Year" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem v-for="y in years" :key="y" :value="String(y)">
                  {{ y }}
                </SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>

        <!-- Employee filter -->
        <div class="space-y-1.5">
          <Label>Employee</Label>
          <Select v-model="selectedEmployeeId" :disabled="loading">
            <SelectTrigger class="w-full">
              <SelectValue placeholder="All employees" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All employees</SelectItem>
              <SelectItem v-for="emp in employees" :key="emp.id" :value="emp.id">
                {{ emp.fullName }}
                <span v-if="emp.isDisabled" class="text-slate-400 dark:text-slate-500">(disabled)</span>
              </SelectItem>
            </SelectContent>
          </Select>
        </div>

        <!-- What's included -->
        <div class="grid gap-3 sm:grid-cols-2">
          <div class="rounded-lg border border-primary/30 bg-primary/5 p-4 text-sm text-slate-600 dark:text-slate-400">
            <p class="font-medium text-slate-800 dark:text-slate-200 mb-2">Export (new)</p>
            <ul class="space-y-1 list-disc list-inside">
              <li>One row per employee per day: hours worked and overtime that day</li>
              <li>Decimal hours on the quarter hour (30 min = 0.5), ready to enter in payroll</li>
              <li>Leave type and days (1 or 0.5), holiday or "Missing Log"</li>
              <li>Worked from home (WFH)</li>
              <li>Month totals with the approved overtime from the settlement</li>
            </ul>
          </div>
          <div class="rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/50 p-4 text-sm text-slate-600 dark:text-slate-400">
            <p class="font-medium text-slate-800 dark:text-slate-200 mb-2">Export (original)</p>
            <ul class="space-y-1 list-disc list-inside">
              <li>Overtime summary on top: approved paid overtime, outcome and notes (blank until confirmed)</li>
              <li>One row per employee per working day with hours worked (two decimals)</li>
              <li>Vacation type, public holiday or "Missing Log"</li>
              <li>Description: the vacation note, or the employee's work note</li>
            </ul>
          </div>
        </div>

        <p class="text-xs text-muted-foreground">
          {{ selectedMonthLabel }} {{ selectedYear }} — {{ selectedEmployeeName }}
        </p>

        <!-- Export buttons -->
        <div class="grid gap-3 sm:grid-cols-2">
          <Button :disabled="exporting !== null" @click="handleExport('daily')">
            <DownloadIcon class="size-4 mr-2" />
            {{ exporting === "daily" ? "Generating…" : "Export (new)" }}
          </Button>
          <Button variant="outline" :disabled="exporting !== null" @click="handleExport('classic')">
            <DownloadIcon class="size-4 mr-2" />
            {{ exporting === "classic" ? "Generating…" : "Export" }}
          </Button>
        </div>
      </div>
    </div>
  </div>
</template>
