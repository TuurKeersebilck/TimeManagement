<script setup lang="ts">
import { ref, onMounted } from "vue";
import { useRouter } from "vue-router";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import { adminService, type Employee } from "../../services/adminService";
import DataTable from "primevue/datatable";
import Column from "primevue/column";
import { useToast } from "primevue/usetoast";
import Toast from "primevue/toast";

const toast = useToast();
const router = useRouter();
const employees = ref<Employee[]>([]);
const loading = ref(false);

onMounted(async () => {
	loading.value = true;
	try {
		employees.value = await adminService.getEmployees();
	} catch {
		toast.add({ severity: "error", summary: "Error", detail: "Failed to load employees", life: 3000 });
	} finally {
		loading.value = false;
	}
});
</script>

<template>
	<AuthenticatedLayout>
		<Toast />

		<div class="p-6 lg:p-8">
			<div class="max-w-4xl mx-auto">

				<div class="mb-8">
					<h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">Employees</h1>
					<p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">All registered employees</p>
				</div>

				<div class="card overflow-hidden">
					<DataTable
						:value="employees"
						:loading="loading"
						stripedRows
						class="text-sm"
						rowHover
						@row-click="(e) => router.push({ name: 'admin-employee-detail', params: { id: e.data.id } })"
						:pt="{ bodyRow: { class: 'cursor-pointer' } }"
					>
						<template #empty>
							<div class="text-center py-16">
								<i class="pi pi-users text-4xl text-slate-300 dark:text-slate-600 mb-3 block"></i>
								<p class="text-slate-500 dark:text-slate-400">No employees found.</p>
							</div>
						</template>

						<Column field="fullName" header="Name" sortable>
							<template #body="{ data }">
								<span class="font-medium text-slate-900 dark:text-slate-100">{{ data.fullName }}</span>
							</template>
						</Column>

						<Column field="email" header="Email" sortable>
							<template #body="{ data }">
								<span class="text-slate-600 dark:text-slate-400">{{ data.email }}</span>
							</template>
						</Column>
					</DataTable>
				</div>

			</div>
		</div>
	</AuthenticatedLayout>
</template>
