<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
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
const exporting = ref(false);

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

const handleExport = async () => {
  exporting.value = true;
  try {
    await adminService.downloadPayrollExport(
      Number(selectedYear.value),
      Number(selectedMonth.value),
      selectedEmployeeId.value === "all" ? undefined : selectedEmployeeId.value
    );
    toast.success(`Payroll exported for ${selectedMonthLabel.value} ${selectedYear.value}`);
  } catch {
    toast.error("Failed to generate export");
  } finally {
    exporting.value = false;
  }
};

onMounted(async () => {
  loading.value = true;
  try {
    employees.value = await adminService.getEmployees();
  } catch {
    toast.error("Failed to load employees");
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <AuthenticatedLayout>
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
                </SelectItem>
              </SelectContent>
            </Select>
          </div>

          <!-- What's included info box -->
          <div class="rounded-lg bg-slate-50 dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700 p-4 text-sm text-slate-600 dark:text-slate-400 space-y-1">
            <p class="font-medium text-slate-700 dark:text-slate-300 mb-2">The CSV includes:</p>
            <ul class="space-y-1 list-disc list-inside">
              <li>Summary: days worked, total hours, and vacation days per employee</li>
              <li>Daily breakdown: start/end times, break duration, net hours, description</li>
              <li>Vacation days: date, type, and amount</li>
            </ul>
          </div>

          <!-- Export button -->
          <Button class="w-full" :disabled="exporting" @click="handleExport">
            <DownloadIcon class="size-4 mr-2" />
            <span v-if="exporting">Generating…</span>
            <span v-else>
              Export {{ selectedMonthLabel }} {{ selectedYear }} — {{ selectedEmployeeName }}
            </span>
          </Button>
        </div>
      </div>
    </div>
  </AuthenticatedLayout>
</template>
