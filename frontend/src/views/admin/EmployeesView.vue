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
import { UsersIcon } from "lucide-vue-next";

const toast = useAppToast();
const router = useRouter();
const employees = ref<Employee[]>([]);
const loading = ref(false);

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
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">All registered employees</p>
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
              </TableRow>
            </TableHeader>
            <TableBody>
              <TableEmpty v-if="employees.length === 0" :colspan="2">
                <UsersIcon class="size-8 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
                <p class="text-slate-500 dark:text-slate-400">No employees found.</p>
              </TableEmpty>
              <TableRow
                v-for="employee in employees"
                :key="employee.id"
                class="cursor-pointer"
                @click="router.push({ name: 'admin-employee-detail', params: { id: employee.id } })"
              >
                <TableCell class="font-medium text-slate-900 dark:text-slate-100">
                  {{ employee.fullName }}
                </TableCell>
                <TableCell class="text-slate-600 dark:text-slate-400">
                  {{ employee.email }}
                </TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </div>
      </div>
    </div>
  </AuthenticatedLayout>
</template>
