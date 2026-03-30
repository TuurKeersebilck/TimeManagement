<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useRoute } from "vue-router";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import { adminService, type AdminTimeLog, type Employee } from "../../services/adminService";
import DataTable from "primevue/datatable";
import Column from "primevue/column";
import Select from "primevue/select";
import DatePicker from "primevue/datepicker";
import { useToast } from "primevue/usetoast";
import Toast from "primevue/toast";

const toast = useToast();
const route = useRoute();

const allLogs = ref<AdminTimeLog[]>([]);
const employees = ref<Employee[]>([]);
const loading = ref(false);

const selectedEmployee = ref<Employee | null>(null);
const dateFrom = ref<Date | null>(null);
const dateTo = ref<Date | null>(null);

// ─── Filters ────────────────────────────────────────────────────────────────

const filteredLogs = computed(() => {
	let logs = allLogs.value;

	if (selectedEmployee.value) {
		logs = logs.filter((l) => l.userId === selectedEmployee.value!.id);
	}

	if (dateFrom.value) {
		const from = localDateStr(dateFrom.value);
		logs = logs.filter((l) => l.date.split("T")[0] >= from);
	}

	if (dateTo.value) {
		const to = localDateStr(dateTo.value);
		logs = logs.filter((l) => l.date.split("T")[0] <= to);
	}

	return logs;
});

const totalHoursFiltered = computed(() =>
	filteredLogs.value.reduce((sum, l) => sum + (l.totalHours ?? 0), 0).toFixed(2)
);

// ─── Helpers ─────────────────────────────────────────────────────────────────

const localDateStr = (date: Date) => {
	const y = date.getFullYear();
	const m = String(date.getMonth() + 1).padStart(2, "0");
	const d = String(date.getDate()).padStart(2, "0");
	return `${y}-${m}-${d}`;
};

const formatDate = (dateStr: string) =>
	new Date(dateStr).toLocaleDateString("en-GB", {
		year: "numeric",
		month: "short",
		day: "numeric",
	});

const formatTime = (t?: string) => (t ? t.substring(0, 5) : "—");

const formatBreak = (log: AdminTimeLog) =>
	log.breakStart && log.breakEnd
		? `${formatTime(log.breakStart)} – ${formatTime(log.breakEnd)}`
		: "—";

const clearFilters = () => {
	selectedEmployee.value = null;
	dateFrom.value = null;
	dateTo.value = null;
};

// ─── Mount ───────────────────────────────────────────────────────────────────

onMounted(async () => {
	loading.value = true;
	try {
		[allLogs.value, employees.value] = await Promise.all([
			adminService.getAllTimeLogs(),
			adminService.getEmployees(),
		]);
		const preselect = route.query.employeeId as string | undefined;
		if (preselect) {
			selectedEmployee.value = employees.value.find((e) => e.id === preselect) ?? null;
		}
	} catch {
		toast.add({ severity: "error", summary: "Error", detail: "Failed to load data", life: 3000 });
	} finally {
		loading.value = false;
	}
});
</script>

<template>
	<AuthenticatedLayout>
		<Toast />

		<div class="p-6 lg:p-8">
			<div class="max-w-6xl mx-auto">

				<!-- Header -->
				<div class="mb-8">
					<h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">All Time Logs</h1>
					<p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">Overview of all employee time logs</p>
				</div>

				<!-- Stats -->
				<div class="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-8">
					<div class="stat-card">
						<p class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">Employees</p>
						<p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
							<span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600">--</span>
							<span v-else>{{ employees.length }}</span>
						</p>
					</div>
					<div class="stat-card">
						<p class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">Entries shown</p>
						<p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
							<span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600">--</span>
							<span v-else>{{ filteredLogs.length }}</span>
						</p>
					</div>
					<div class="stat-card">
						<p class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">Total hours shown</p>
						<p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
							<span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600">--</span>
							<span v-else>{{ totalHoursFiltered }}h</span>
						</p>
					</div>
				</div>

				<!-- Filters -->
				<div class="card mb-4 flex flex-wrap items-end gap-3">
					<div class="flex-1 min-w-[180px]">
						<label class="form-label">Employee</label>
						<Select
							v-model="selectedEmployee"
							:options="employees"
							optionLabel="fullName"
							placeholder="All employees"
							showClear
							class="w-full"
						/>
					</div>
					<div class="flex-1 min-w-[150px]">
						<label class="form-label">From</label>
						<DatePicker
							v-model="dateFrom"
							dateFormat="yy-mm-dd"
							showIcon
							inputClass="input-field-dialog"
							class="w-full"
						/>
					</div>
					<div class="flex-1 min-w-[150px]">
						<label class="form-label">To</label>
						<DatePicker
							v-model="dateTo"
							dateFormat="yy-mm-dd"
							showIcon
							inputClass="input-field-dialog"
							class="w-full"
						/>
					</div>
					<button
						v-if="selectedEmployee || dateFrom || dateTo"
						@click="clearFilters"
						class="btn-secondary shrink-0"
					>
						Clear filters
					</button>
				</div>

				<!-- Table -->
				<div class="card overflow-hidden">
					<DataTable
						:value="filteredLogs"
						:loading="loading"
						paginator
						:rows="15"
						:rowsPerPageOptions="[15, 25, 50]"
						stripedRows
						class="text-sm"
					>
						<template #empty>
							<div class="text-center py-16">
								<i class="pi pi-clock text-4xl text-slate-300 dark:text-slate-600 mb-3 block"></i>
								<p class="text-slate-500 dark:text-slate-400">No time logs found.</p>
							</div>
						</template>

						<Column header="Employee" sortable sortField="employeeName">
							<template #body="{ data }">
								<div>
									<p class="font-medium text-slate-900 dark:text-slate-100 text-sm">{{ data.employeeName }}</p>
									<p class="text-xs text-slate-400 dark:text-slate-500">{{ data.employeeEmail }}</p>
								</div>
							</template>
						</Column>

						<Column field="date" header="Date" sortable>
							<template #body="{ data }">
								<span class="font-medium text-slate-900 dark:text-slate-100">{{ formatDate(data.date) }}</span>
							</template>
						</Column>

						<Column header="Hours">
							<template #body="{ data }">
								<span class="text-slate-600 dark:text-slate-400">
									{{ formatTime(data.startTime) }} – {{ formatTime(data.endTime) }}
								</span>
							</template>
						</Column>

						<Column header="Break">
							<template #body="{ data }">
								<span class="text-slate-500 dark:text-slate-500 text-xs">{{ formatBreak(data) }}</span>
							</template>
						</Column>

						<Column field="totalHours" header="Total" sortable>
							<template #body="{ data }">
								<span class="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-semibold bg-indigo-50 dark:bg-indigo-950 text-indigo-700 dark:text-indigo-300">
									{{ data.totalHours?.toFixed(2) ?? "0.00" }}h
								</span>
							</template>
						</Column>

						<Column header="Description">
							<template #body="{ data }">
								<span
									class="text-slate-600 dark:text-slate-400 text-sm"
									:title="data.description"
								>
									{{ data.description ? (data.description.length > 50 ? data.description.substring(0, 50) + '…' : data.description) : '—' }}
								</span>
							</template>
						</Column>
					</DataTable>
				</div>

			</div>
		</div>
	</AuthenticatedLayout>
</template>
