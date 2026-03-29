<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
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
import { useConfirm } from "primevue/useconfirm";
import Toast from "primevue/toast";
import ConfirmDialog from "primevue/confirmdialog";

const toast = useToast();
const confirm = useConfirm();

const { timeLogs, loading, fetchTimeLogs, refreshTimeLogs } =
	useTimeLogsStore();

const { totalHoursToday, totalHoursThisWeek, totalHoursThisMonth } =
	useTimeCalculations(timeLogs);

// ─── Dialog state ────────────────────────────────────────────────────────────

const showDialog = ref(false);
const editMode = ref(false);
const currentLogId = ref<number | null>(null);
const saving = ref(false);

const emptyForm = () => ({
	date: new Date(),
	startTime: "",
	endTime: "",
	breakStart: "",
	breakEnd: "",
	description: "",
});

const formData = ref(emptyForm());
const formErrors = ref<Record<string, string>>({});

// ─── Live total hours preview ────────────────────────────────────────────────

const previewHours = computed(() => {
	const { startTime, endTime, breakStart, breakEnd } = formData.value;
	if (!startTime || !endTime) return null;

	const toMinutes = (t: string) => {
		const [h, m] = t.split(":").map(Number);
		return h * 60 + m;
	};

	let total = toMinutes(endTime) - toMinutes(startTime);
	if (total <= 0) return null;

	if (breakStart && breakEnd) {
		const breakDuration = toMinutes(breakEnd) - toMinutes(breakStart);
		if (breakDuration > 0) total -= breakDuration;
	}

	if (total <= 0) return null;
	const h = Math.floor(total / 60);
	const m = total % 60;
	return m === 0 ? `${h}h` : `${h}h ${m}m`;
});

// ─── Form helpers ────────────────────────────────────────────────────────────

const localDateStr = (date: Date) => {
	const y = date.getFullYear();
	const m = String(date.getMonth() + 1).padStart(2, "0");
	const d = String(date.getDate()).padStart(2, "0");
	return `${y}-${m}-${d}`;
};

const toTimeStr = (t: string) => (t ? `${t}:00` : undefined);

const validateForm = (): boolean => {
	const errors: Record<string, string> = {};
	const { startTime, endTime, breakStart, breakEnd } = formData.value;

	if (!startTime) errors.startTime = "Required";
	if (!endTime) errors.endTime = "Required";

	if (startTime && endTime && endTime <= startTime)
		errors.endTime = "Must be after start time";

	const hasBreakStart = !!breakStart;
	const hasBreakEnd = !!breakEnd;
	if (hasBreakStart && !hasBreakEnd)
		errors.breakEnd = "Required when break start is set";
	if (!hasBreakStart && hasBreakEnd)
		errors.breakStart = "Required when break end is set";
	if (hasBreakStart && hasBreakEnd) {
		if (breakStart < startTime || breakEnd > endTime)
			errors.breakStart = "Break must fall within working hours";
		else if (breakEnd <= breakStart)
			errors.breakEnd = "Break end must be after break start";
	}

	// Duplicate date check against cached logs (exclude current log when editing)
	const dateStr = localDateStr(formData.value.date);
	const duplicate = timeLogs.value.some(
		(log) =>
			log.date.split("T")[0] === dateStr &&
			(!editMode.value || log.id !== currentLogId.value)
	);
	if (duplicate) errors.date = "A time log already exists for this date";

	formErrors.value = errors;
	return Object.keys(errors).length === 0;
};

// ─── Dialog actions ──────────────────────────────────────────────────────────

const openNewDialog = () => {
	editMode.value = false;
	currentLogId.value = null;
	formData.value = emptyForm();
	formErrors.value = {};
	showDialog.value = true;
};

const openEditDialog = (log: TimeLog) => {
	editMode.value = true;
	currentLogId.value = log.id;
	formData.value = {
		date: new Date(log.date),
		startTime: log.startTime.substring(0, 5),
		endTime: log.endTime.substring(0, 5),
		breakStart: log.breakStart ? log.breakStart.substring(0, 5) : "",
		breakEnd: log.breakEnd ? log.breakEnd.substring(0, 5) : "",
		description: log.description ?? "",
	};
	formErrors.value = {};
	showDialog.value = true;
};

const saveTimeLog = async () => {
	if (!validateForm()) return;

	saving.value = true;
	try {
		const { startTime, endTime, breakStart, breakEnd, description } =
			formData.value;

		const payload: TimeLogCreate = {
			date: localDateStr(formData.value.date),
			startTime: toTimeStr(startTime)!,
			endTime: toTimeStr(endTime)!,
			...(breakStart && breakEnd
				? { breakStart: toTimeStr(breakStart), breakEnd: toTimeStr(breakEnd) }
				: {}),
			...(description?.trim() ? { description: description.trim() } : {}),
		};

		if (editMode.value && currentLogId.value !== null) {
			await timeLogService.update(currentLogId.value, payload);
		} else {
			await timeLogService.create(payload);
		}

		showDialog.value = false;
		await refreshTimeLogs();
		toast.add({
			severity: "success",
			summary: editMode.value ? "Updated" : "Logged",
			detail: `Time log ${editMode.value ? "updated" : "saved"} successfully`,
			life: 3000,
		});
	} catch (error: any) {
		const message =
			error.response?.data?.message ||
			error.response?.data ||
			error.message ||
			"Failed to save time log";
		toast.add({ severity: "error", summary: "Error", detail: message, life: 5000 });
	} finally {
		saving.value = false;
	}
};

const confirmDelete = (log: TimeLog) => {
	confirm.require({
		message: `Delete the time log for ${formatDate(log.date)}?`,
		header: "Delete time log",
		icon: "pi pi-trash",
		rejectLabel: "Cancel",
		acceptLabel: "Delete",
		acceptClass: "p-button-danger",
		accept: () => deleteTimeLog(log.id),
	});
};

const deleteTimeLog = async (id: number) => {
	try {
		loading.value = true;
		await timeLogService.delete(id);
		await refreshTimeLogs();
		toast.add({ severity: "success", summary: "Deleted", detail: "Time log deleted", life: 3000 });
	} catch {
		toast.add({ severity: "error", summary: "Error", detail: "Failed to delete time log", life: 3000 });
	} finally {
		loading.value = false;
	}
};

// ─── Formatters ─────────────────────────────────────────────────────────────

const formatDate = (dateStr: string) =>
	new Date(dateStr).toLocaleDateString("en-GB", {
		year: "numeric",
		month: "short",
		day: "numeric",
	});

const formatTime = (t?: string) => (t ? t.substring(0, 5) : "—");

const formatBreak = (log: TimeLog) =>
	log.breakStart && log.breakEnd
		? `${formatTime(log.breakStart)} – ${formatTime(log.breakEnd)}`
		: "—";

// ─── Mount ───────────────────────────────────────────────────────────────────

onMounted(async () => {
	try {
		await fetchTimeLogs();
	} catch {
		toast.add({ severity: "error", summary: "Error", detail: "Failed to load time logs", life: 3000 });
	}
});
</script>

<template>
	<AuthenticatedLayout>
		<Toast />
		<ConfirmDialog />

		<div class="p-6 lg:p-8">
			<div class="max-w-6xl mx-auto">

				<!-- Header -->
				<div class="flex items-center justify-between mb-8">
					<div>
						<h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">My Time Logs</h1>
						<p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">Track your daily working hours</p>
					</div>
					<button @click="openNewDialog" class="btn-primary">
						<i class="pi pi-plus text-sm"></i>
						Log hours
					</button>
				</div>

				<!-- Stats -->
				<div class="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-8">
					<div class="stat-card">
						<p class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">Today</p>
						<p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
							<span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600">--</span>
							<span v-else>{{ totalHoursToday }}h</span>
						</p>
					</div>
					<div class="stat-card">
						<p class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">This week</p>
						<p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
							<span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600">--</span>
							<span v-else>{{ totalHoursThisWeek }}h</span>
						</p>
					</div>
					<div class="stat-card">
						<p class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">This month</p>
						<p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
							<span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600">--</span>
							<span v-else>{{ totalHoursThisMonth }}h</span>
						</p>
					</div>
				</div>

				<!-- Table -->
				<div class="card overflow-hidden">
					<DataTable
						:value="timeLogs"
						:loading="loading"
						paginator
						:rows="10"
						:rowsPerPageOptions="[10, 25, 50]"
						stripedRows
						class="text-sm"
					>
						<template #empty>
							<div class="text-center py-16">
								<i class="pi pi-clock text-4xl text-slate-300 dark:text-slate-600 mb-3 block"></i>
								<p class="text-slate-500 dark:text-slate-400">No time logs yet. Log your first day above.</p>
							</div>
						</template>

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
									{{ data.description ? (data.description.length > 60 ? data.description.substring(0, 60) + '…' : data.description) : '—' }}
								</span>
							</template>
						</Column>

						<Column header="" :exportable="false" style="width: 80px">
							<template #body="{ data }">
								<div class="flex gap-1 justify-end">
									<button
										@click="openEditDialog(data)"
										class="btn-ghost !px-2 !py-1.5 text-slate-400 hover:text-indigo-600 dark:hover:text-indigo-400"
										title="Edit"
									>
										<i class="pi pi-pencil text-sm"></i>
									</button>
									<button
										@click="confirmDelete(data)"
										class="btn-ghost !px-2 !py-1.5 text-slate-400 hover:text-red-500 dark:hover:text-red-400"
										title="Delete"
									>
										<i class="pi pi-trash text-sm"></i>
									</button>
								</div>
							</template>
						</Column>
					</DataTable>
				</div>
			</div>
		</div>

		<!-- Add / Edit Dialog -->
		<Dialog
			v-model:visible="showDialog"
			:modal="true"
			:closable="true"
			:draggable="false"
			:style="{ width: '480px' }"
		>
			<template #header>
				<h2 class="text-base font-semibold text-slate-900 dark:text-slate-100">
					{{ editMode ? "Edit time log" : "Log hours" }}
				</h2>
			</template>

			<div class="space-y-4 py-1">
				<!-- Date -->
				<div>
					<label class="form-label">Date</label>
					<DatePicker
						v-model="formData.date"
						dateFormat="yy-mm-dd"
						showIcon
						inputClass="input-field-dialog"
						class="w-full"
					/>
					<p v-if="formErrors.date" class="mt-1 text-xs text-red-500">{{ formErrors.date }}</p>
				</div>

				<!-- Start / End times -->
				<div class="grid grid-cols-2 gap-3">
					<div>
						<label class="form-label">Start time</label>
						<input v-model="formData.startTime" type="time" class="input-field-dialog" />
						<p v-if="formErrors.startTime" class="mt-1 text-xs text-red-500">{{ formErrors.startTime }}</p>
					</div>
					<div>
						<label class="form-label">End time</label>
						<input v-model="formData.endTime" type="time" class="input-field-dialog" />
						<p v-if="formErrors.endTime" class="mt-1 text-xs text-red-500">{{ formErrors.endTime }}</p>
					</div>
				</div>

				<!-- Break times -->
				<div>
					<label class="form-label">Break <span class="font-normal text-slate-400">(optional)</span></label>
					<div class="grid grid-cols-2 gap-3">
						<div>
							<input v-model="formData.breakStart" type="time" class="input-field-dialog" placeholder="Start" />
							<p v-if="formErrors.breakStart" class="mt-1 text-xs text-red-500">{{ formErrors.breakStart }}</p>
						</div>
						<div>
							<input v-model="formData.breakEnd" type="time" class="input-field-dialog" placeholder="End" />
							<p v-if="formErrors.breakEnd" class="mt-1 text-xs text-red-500">{{ formErrors.breakEnd }}</p>
						</div>
					</div>
				</div>

				<!-- Description -->
				<div>
					<label class="form-label">Description</label>
					<textarea
						v-model="formData.description"
						rows="3"
						class="input-field-dialog resize-none"
						placeholder="What did you work on today?"
					></textarea>
				</div>

				<!-- Preview -->
				<div v-if="previewHours" class="flex items-center gap-2 text-sm text-indigo-600 dark:text-indigo-400">
					<i class="pi pi-check-circle text-xs"></i>
					<span>Total: <strong>{{ previewHours }}</strong></span>
				</div>
			</div>

			<template #footer>
				<div class="flex justify-end gap-2 pt-2">
					<button @click="showDialog = false" class="btn-secondary">Cancel</button>
					<button @click="saveTimeLog" :disabled="saving" class="btn-primary disabled:opacity-50 disabled:cursor-not-allowed">
						<i v-if="saving" class="pi pi-spin pi-spinner text-sm"></i>
						{{ editMode ? "Update" : "Save" }}
					</button>
				</div>
			</template>
		</Dialog>
	</AuthenticatedLayout>
</template>
