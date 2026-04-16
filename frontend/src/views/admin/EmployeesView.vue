<script setup lang="ts">
import { ref, onMounted } from "vue";
import { useRouter } from "vue-router";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import { adminService, type Employee } from "../../services/adminService";
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
import { UsersIcon, ChevronRightIcon } from "lucide-vue-next";

const toast = useAppToast();
const router = useRouter();
const employees = ref<Employee[]>([]);
const loading = ref(false);

const weekStatus = (emp: Employee): "on-track" | "behind" | "none" => {
  if (emp.resolvedWeeklyTarget == null) return "none";
  return emp.weeklyHoursLogged >= emp.resolvedWeeklyTarget ? "on-track" : "behind";
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
      <div class="max-w-4xl mx-auto">
        <div class="mb-8">
          <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">Employees</h1>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">Click an employee to manage their vacation types, balance and hours targets</p>
        </div>

        <div class="card overflow-hidden">
          <!-- Loading skeleton -->
          <div v-if="loading" class="divide-y divide-slate-100 dark:divide-slate-800">
            <div v-for="i in 5" :key="i" class="flex items-center gap-4 px-4 py-3.5">
              <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-36 animate-pulse" />
              <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-48 animate-pulse" />
            </div>
          </div>

          <Table v-else>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Email</TableHead>
                <TableHead>This week</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              <TableEmpty v-if="employees.length === 0" :colspan="4">
                <UsersIcon class="size-8 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
                <p class="text-slate-500 dark:text-slate-400">No employees found.</p>
              </TableEmpty>
              <TableRow
                v-for="employee in employees"
                :key="employee.id"
                class="cursor-pointer hover:bg-muted/50 transition-colors"
                @click="router.push({ name: 'admin-employee-detail', params: { id: employee.id } })"
              >
                <TableCell class="font-medium text-slate-900 dark:text-slate-100">
                  {{ employee.fullName }}
                </TableCell>
                <TableCell class="text-slate-600 dark:text-slate-400">
                  {{ employee.email }}
                </TableCell>
                <TableCell>
                  <div class="flex items-center gap-2">
                    <span class="text-sm text-slate-700 dark:text-slate-300">
                      {{ employee.weeklyHoursLogged.toFixed(1) }}h
                      <span v-if="employee.resolvedWeeklyTarget != null" class="text-slate-400 dark:text-slate-500">
                        / {{ employee.resolvedWeeklyTarget }}h
                      </span>
                    </span>
                    <span
                      v-if="weekStatus(employee) !== 'none'"
                      :class="[
                        'inline-flex items-center px-1.5 py-0.5 rounded text-xs font-medium',
                        weekStatus(employee) === 'on-track'
                          ? 'bg-emerald-50 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-300'
                          : 'bg-amber-50 dark:bg-amber-950 text-amber-700 dark:text-amber-300',
                      ]"
                    >
                      {{ weekStatus(employee) === "on-track" ? "On track" : "Behind" }}
                    </span>
                  </div>
                </TableCell>
                <TableCell class="text-right w-8">
                  <ChevronRightIcon class="size-4 text-slate-400 dark:text-slate-500 ml-auto" />
                </TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </div>
      </div>
    </div>
  </AuthenticatedLayout>
</template>
