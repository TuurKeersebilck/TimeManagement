<script setup lang="ts">
import { ref, onMounted } from "vue";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import {
	timeLogService,
	type TimeLog,
	type TimeLogCreate,
} from "../services/timeLogService";
import { useTimeCalculations } from "../composables/useTimeCalculations";
import { useTimeLogsStore } from "../composables/useTimeLogsStore";
import DataTable from "primevue/datatable";
import Column from "primevue/column";
import Dialog from "primevue/dialog";
import DatePicker from "primevue/datepicker";
import { useToast } from "primevue/usetoast";
import Toast from "primevue/toast";

const toast = useToast();

const { timeLogs, loading, fetchTimeLogs, refreshTimeLogs } =
	useTimeLogsStore();

const { totalHoursToday, totalHoursThisWeek, totalHoursThisMonth } =
	useTimeCalculations(timeLogs);

const showDialog = ref(false);
const editMode = ref(false);
const currentLogId = ref<number | null>(null);

const formData = ref({
	date: new Date(),
	startTime: "",
	endTime: "",
	break: "00:30",
});

const openNewDialog = () => {
	editMode.value = false;
	currentLogId.value = null;
	formData.value = {
		date: new Date(),
		startTime: "09:00",
		endTime: "17:00",
		break: "00:30",
	};
	showDialog.value = true;
};

const openEditDialog = (log: TimeLog) => {
	editMode.value = true;
	currentLogId.value = log.id;
	formData.value = {
		date: new Date(log.date),
		startTime: log.startTime.substring(0, 5),
		endTime: log.endTime.substring(0, 5),
		break: log.break.substring(0, 5),
	};
	showDialog.value = true;
};

const saveTimeLog = async () => {
	try {
		loading.value = true;

		// Format date in local timezone to avoid timezone issues
		const year = formData.value.date.getFullYear();
		const month = String(formData.value.date.getMonth() + 1).padStart(2, "0");
		const day = String(formData.value.date.getDate()).padStart(2, "0");
		const localDateStr = `${year}-${month}-${day}`;

		const payload: TimeLogCreate = {
			date: localDateStr,
			startTime: `${formData.value.startTime}:00`,
			endTime: `${formData.value.endTime}:00`,
			break: `${formData.value.break}:00`,
		};

		const isEdit = editMode.value && currentLogId.value !== null;
		if (isEdit) {
			await timeLogService.update(currentLogId.value!, payload);
		} else {
			await timeLogService.create(payload);
		}

		toast.add({
			severity: "success",
			summary: isEdit ? "Updated" : "Created",
			detail: `Time log ${isEdit ? "updated" : "created"} successfully`,
			life: 3000,
		});

		showDialog.value = false;
		await refreshTimeLogs();
	} catch (error: any) {
		toast.add({
			severity: "error",
			summary: "Error",
			detail:
				error.response?.data?.message ||
				error.message ||
				"Failed to save time log",
			life: 5000,
		});
	} finally {
		loading.value = false;
	}
};

const deleteTimeLog = async (id: number) => {
	if (!confirm("Are you sure you want to delete this time log?")) return;

	try {
		loading.value = true;
		await timeLogService.delete(id);
		toast.add({
			severity: "success",
			summary: "Deleted",
			detail: "Time log deleted successfully",
			life: 3000,
		});
		await refreshTimeLogs();
	} catch (error) {
		toast.add({
			severity: "error",
			summary: "Error",
			detail: "Failed to delete time log",
			life: 3000,
		});
	} finally {
		loading.value = false;
	}
};

// Formatters
const formatDate = (dateStr: string) =>
	new Date(dateStr).toLocaleDateString("en-US", {
		year: "numeric",
		month: "short",
		day: "numeric",
	});

const formatTime = (timeStr: string) => timeStr.substring(0, 5);

onMounted(async () => {
	try {
		await fetchTimeLogs();
	} catch (error) {
		toast.add({
			severity: "error",
			summary: "Error",
			detail: "Failed to load time logs",
			life: 3000,
		});
	}
});
</script>

<template>
	<AuthenticatedLayout>
		<Toast />
		<div class="page-background p-4 sm:p-6 lg:p-8">
			<div class="max-w-7xl mx-auto">
				<!-- Header -->
				<div
					class="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 mb-8"
				>
					<div>
						<h1 class="text-4xl font-bold text-slate-900">Time Tracking</h1>
					</div>
					<button @click="openNewDialog" class="btn-primary">
						<i class="pi pi-plus"></i>
						<span>Log New Hours</span>
					</button>
				</div>

				<!-- Stats Cards -->
				<div class="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
					<div class="card-gradient-blue p-6">
						<div>
							<p class="text-blue-100 text-sm font-medium mb-1">Today</p>
							<p class="text-4xl font-bold">
								<span v-if="loading" class="animate-pulse">--</span>
								<span v-else>{{ totalHoursToday }}h</span>
							</p>
						</div>
					</div>
					<div class="card-gradient-purple p-6">
						<div>
							<p class="text-purple-100 text-sm font-medium mb-1">This Week</p>
							<p class="text-4xl font-bold">
								<span v-if="loading" class="animate-pulse">--</span>
								<span v-else>{{ totalHoursThisWeek }}h</span>
							</p>
						</div>
					</div>
					<div class="card-gradient-green p-6">
						<div>
							<p class="text-emerald-100 text-sm font-medium mb-1">
								This Month
							</p>
							<p class="text-4xl font-bold">
								<span v-if="loading" class="animate-pulse">--</span>
								<span v-else>{{ totalHoursThisMonth }}h</span>
							</p>
						</div>
					</div>
				</div>

				<!-- Data Table -->
				<div
					class="bg-white rounded-xl shadow-lg overflow-hidden border border-slate-200"
				>
					<DataTable
						:value="timeLogs"
						:loading="loading"
						paginator
						:rows="10"
						:rowsPerPageOptions="[5, 10, 20, 50]"
						stripedRows
					>
						<template #empty>
							<div class="text-center py-12">
								<i class="pi pi-inbox text-5xl text-slate-300 mb-4"></i>
								<p class="text-slate-500 text-lg">
									No time logs found. Start by logging your hours!
								</p>
							</div>
						</template>

						<Column field="date" header="Date" sortable>
							<template #body="{ data }">
								<span class="font-medium text-slate-700">{{
									formatDate(data.date)
								}}</span>
							</template>
						</Column>

						<Column field="startTime" header="Start Time" sortable>
							<template #body="{ data }">
								<span class="text-slate-600">{{
									formatTime(data.startTime)
								}}</span>
							</template>
						</Column>

						<Column field="endTime" header="End Time" sortable>
							<template #body="{ data }">
								<span class="text-slate-600">{{
									formatTime(data.endTime)
								}}</span>
							</template>
						</Column>

						<Column field="break" header="Break" sortable>
							<template #body="{ data }">
								<span class="text-slate-600">{{ formatTime(data.break) }}</span>
							</template>
						</Column>

						<Column field="totalHours" header="Total Hours" sortable>
							<template #body="{ data }">
								<span
									class="inline-flex items-center px-3 py-1 rounded-full text-sm font-bold bg-indigo-100 text-indigo-700"
								>
									{{ data.totalHours?.toFixed(2) ?? "0.00" }}h
								</span>
							</template>
						</Column>

						<Column header="Actions" :exportable="false">
							<template #body="{ data }">
								<div class="flex gap-2">
									<button
										@click="openEditDialog(data)"
										class="p-2 text-blue-600 hover:bg-blue-50 rounded-lg transition-colors duration-200"
										title="Edit"
									>
										<i class="pi pi-pencil"></i>
									</button>
									<button
										@click="deleteTimeLog(data.id)"
										class="p-2 text-red-600 hover:bg-red-50 rounded-lg transition-colors duration-200"
										title="Delete"
									>
										<i class="pi pi-trash"></i>
									</button>
								</div>
							</template>
						</Column>
					</DataTable>
				</div>

				<!-- Add/Edit Dialog -->
				<Dialog
					v-model:visible="showDialog"
					:modal="true"
					:closable="true"
					:draggable="false"
					class="w-full max-w-md mx-4"
				>
					<template #header>
						<div class="flex items-center gap-3">
							<div class="p-3 bg-indigo-100 rounded-lg">
								<i class="pi pi-clock text-2xl text-indigo-600"></i>
							</div>
							<h2 class="text-2xl font-bold text-slate-900">
								{{ editMode ? "Edit Time Log" : "Log New Hours" }}
							</h2>
						</div>
					</template>

					<div class="space-y-5 py-2">
						<!-- Date -->
						<div>
							<label for="date" class="form-label">Date</label>
							<DatePicker
								v-model="formData.date"
								inputId="date"
								dateFormat="yy-mm-dd"
								showIcon
								inputClass="input-field-dialog"
							/>
						</div>

						<!-- Start Time -->
						<div>
							<label for="startTime" class="form-label">Start Time</label>
							<input
								v-model="formData.startTime"
								id="startTime"
								type="time"
								class="input-field-dialog"
							/>
						</div>

						<!-- End Time -->
						<div>
							<label for="endTime" class="form-label">End Time</label>
							<input
								v-model="formData.endTime"
								id="endTime"
								type="time"
								class="input-field-dialog"
							/>
						</div>

						<!-- Break -->
						<div>
							<label for="break" class="form-label">Break Duration</label>
							<input
								v-model="formData.break"
								id="break"
								type="time"
								class="input-field-dialog"
							/>
						</div>
					</div>

					<template #footer>
						<div class="flex justify-end gap-3 pt-4">
							<button
								@click="showDialog = false"
								class="px-5 py-2.5 text-slate-700 hover:bg-slate-100 rounded-lg font-medium transition-colors duration-200"
							>
								Cancel
							</button>
							<button
								@click="saveTimeLog"
								:disabled="loading"
								class="px-5 py-2.5 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg font-medium transition-colors duration-200 disabled:opacity-50 disabled:cursor-not-allowed inline-flex items-center gap-2"
							>
								<i v-if="loading" class="pi pi-spin pi-spinner"></i>
								<span>{{ editMode ? "Update" : "Create" }}</span>
							</button>
						</div>
					</template>
				</Dialog>
			</div>
		</div>
	</AuthenticatedLayout>
</template>
