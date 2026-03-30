<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import { vacationService, type VacationBalance, type VacationDay, type CreateVacationDayDto } from "../services/vacationService";
import Dialog from "primevue/dialog";
import DatePicker from "primevue/datepicker";
import Select from "primevue/select";
import { useToast } from "primevue/usetoast";
import Toast from "primevue/toast";
import ConfirmDialog from "primevue/confirmdialog";
import { useConfirm } from "primevue/useconfirm";

const toast = useToast();
const confirm = useConfirm();

const balances = ref<VacationBalance[]>([]);
const vacationDays = ref<VacationDay[]>([]);
const loading = ref(false);

// ─── Dialog ───────────────────────────────────────────────────────────────────

const dialogVisible = ref(false);
const saving = ref(false);
const editingDay = ref<VacationDay | null>(null);

const form = ref<{
	vacationTypeId: number | null;
	date: Date | null;
	amount: number;
	note: string;
}>({
	vacationTypeId: null,
	date: null,
	amount: 1,
	note: "",
});

const dialogTitle = computed(() => (editingDay.value ? "Edit vacation day" : "Plan vacation day"));

const amountOptions = [
	{ label: "Full day (1.0)", value: 1 },
	{ label: "Half day (0.5)", value: 0.5 },
];

// Live remaining balance while filling in the dialog
const liveRemaining = computed(() => {
	if (!form.value.vacationTypeId) return null;
	const balance = balances.value.find((b) => b.vacationTypeId === form.value.vacationTypeId);
	if (!balance) return null;
	// When editing, add back the original amount for the current year
	let base = balance.remainingDays;
	if (editingDay.value && editingDay.value.vacationTypeId === form.value.vacationTypeId) {
		// Only add back if the original day is in the current year
		const origYear = new Date(editingDay.value.date).getUTCFullYear();
		if (origYear === new Date().getUTCFullYear()) base += editingDay.value.amount;
	}
	return base - form.value.amount;
});

const canSubmit = computed(() => {
	if (!form.value.vacationTypeId || !form.value.date) return false;
	if (liveRemaining.value === null) return false;
	return liveRemaining.value >= 0;
});

const openCreate = () => {
	editingDay.value = null;
	form.value = { vacationTypeId: null, date: null, amount: 1, note: "" };
	dialogVisible.value = true;
};

const openEdit = (day: VacationDay) => {
	editingDay.value = day;
	form.value = {
		vacationTypeId: day.vacationTypeId,
		date: new Date(day.date),
		amount: day.amount,
		note: day.note ?? "",
	};
	dialogVisible.value = true;
};

const formatDate = (d: Date): string => {
	const y = d.getFullYear();
	const m = String(d.getMonth() + 1).padStart(2, "0");
	const day = String(d.getDate()).padStart(2, "0");
	return `${y}-${m}-${day}`;
};

const save = async () => {
	if (!canSubmit.value || !form.value.date || !form.value.vacationTypeId) return;
	saving.value = true;
	const payload: CreateVacationDayDto = {
		vacationTypeId: form.value.vacationTypeId,
		date: formatDate(form.value.date),
		amount: form.value.amount,
		note: form.value.note.trim() || undefined,
	};
	try {
		if (editingDay.value) {
			const updated = await vacationService.update(editingDay.value.id, payload);
			const idx = vacationDays.value.findIndex((d) => d.id === editingDay.value!.id);
			if (idx !== -1) vacationDays.value[idx] = updated;
			toast.add({ severity: "success", summary: "Updated", detail: "Vacation day updated", life: 3000 });
		} else {
			const created = await vacationService.create(payload);
			vacationDays.value.unshift(created);
			toast.add({ severity: "success", summary: "Planned", detail: "Vacation day added", life: 3000 });
		}
		// Refresh balances so the cards update
		balances.value = await vacationService.getBalances();
		dialogVisible.value = false;
	} catch (err: unknown) {
		const msg =
			(err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
			"Failed to save vacation day";
		toast.add({ severity: "error", summary: "Error", detail: msg, life: 5000 });
	} finally {
		saving.value = false;
	}
};

// ─── Delete ───────────────────────────────────────────────────────────────────

const deleteDay = (day: VacationDay) => {
	confirm.require({
		header: "Delete vacation day",
		message: `Remove the ${day.amount === 0.5 ? "half" : "full"} day on ${displayDate(day.date)} (${day.vacationTypeName})?`,
		icon: "pi pi-exclamation-triangle",
		acceptLabel: "Delete",
		rejectLabel: "Cancel",
		accept: async () => {
			try {
				await vacationService.delete(day.id);
				vacationDays.value = vacationDays.value.filter((d) => d.id !== day.id);
				balances.value = await vacationService.getBalances();
				toast.add({ severity: "success", summary: "Deleted", detail: "Vacation day removed", life: 3000 });
			} catch {
				toast.add({ severity: "error", summary: "Error", detail: "Failed to delete", life: 3000 });
			}
		},
	});
};

// ─── Helpers ──────────────────────────────────────────────────────────────────

const displayDate = (iso: string) =>
	new Date(iso).toLocaleDateString(undefined, { day: "numeric", month: "short", year: "numeric" });

const balanceBarWidth = (balance: VacationBalance) => {
	if (balance.yearlyBalance === 0) return "0%";
	const pct = Math.min((balance.usedDays / balance.yearlyBalance) * 100, 100);
	return `${pct}%`;
};

const balanceBarColor = (balance: VacationBalance) => {
	const pct = balance.yearlyBalance > 0 ? balance.usedDays / balance.yearlyBalance : 0;
	if (pct >= 1) return "bg-red-500";
	if (pct >= 0.8) return "bg-amber-400";
	return "bg-emerald-500";
};

// ─── Mount ────────────────────────────────────────────────────────────────────

onMounted(async () => {
	loading.value = true;
	try {
		const [b, d] = await Promise.all([vacationService.getBalances(), vacationService.getVacationDays()]);
		balances.value = b;
		vacationDays.value = d;
	} catch {
		toast.add({ severity: "error", summary: "Error", detail: "Failed to load vacation data", life: 3000 });
	} finally {
		loading.value = false;
	}
});
</script>

<template>
	<AuthenticatedLayout>
		<Toast />
		<ConfirmDialog />

		<div class="p-6 lg:p-8">
			<div class="max-w-3xl mx-auto">

				<!-- Header -->
				<div class="flex items-center justify-between mb-8">
					<div>
						<h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">My Vacations</h1>
						<p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">Plan and track your vacation days</p>
					</div>
					<button
						@click="openCreate"
						:disabled="balances.length === 0"
						class="btn-primary"
						:title="balances.length === 0 ? 'No vacation types assigned yet' : undefined"
					>
						<i class="pi pi-plus mr-2 text-sm"></i>Plan a day
					</button>
				</div>

				<!-- Balance cards -->
				<section class="mb-8">
					<h2 class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500 mb-3">Balances ({{ new Date().getFullYear() }})</h2>

					<!-- Skeleton -->
					<div v-if="loading" class="grid gap-3 sm:grid-cols-2">
						<div v-for="i in 2" :key="i" class="card p-4 space-y-3">
							<div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-24 animate-pulse" />
							<div class="h-2 bg-slate-200 dark:bg-slate-700 rounded animate-pulse" />
							<div class="h-2.5 bg-slate-200 dark:bg-slate-700 rounded w-32 animate-pulse" />
						</div>
					</div>

					<!-- Empty -->
					<div v-else-if="balances.length === 0" class="card text-center py-10">
						<i class="pi pi-calendar-times text-3xl text-slate-300 dark:text-slate-600 mb-2 block"></i>
						<p class="text-sm text-slate-500 dark:text-slate-400">No vacation types have been assigned to you yet.</p>
					</div>

					<!-- Cards -->
					<div v-else class="grid gap-3 sm:grid-cols-2">
						<div
							v-for="balance in balances"
							:key="balance.vacationTypeId"
							class="card p-4"
						>
							<div class="flex items-center gap-2 mb-3">
								<div
									class="w-3 h-3 rounded-full shrink-0 ring-1 ring-black/10"
									:style="{ backgroundColor: balance.vacationTypeColor ?? '#6366f1' }"
								/>
								<span class="text-sm font-medium text-slate-900 dark:text-slate-100">{{ balance.vacationTypeName }}</span>
							</div>
							<!-- Progress bar -->
							<div class="w-full bg-slate-100 dark:bg-slate-800 rounded-full h-1.5 mb-2">
								<div
									:class="['h-1.5 rounded-full transition-all duration-300', balanceBarColor(balance)]"
									:style="{ width: balanceBarWidth(balance) }"
								/>
							</div>
							<div class="flex justify-between text-xs text-slate-500 dark:text-slate-400">
								<span>{{ balance.usedDays }} / {{ balance.yearlyBalance }} days used</span>
								<span
									:class="balance.remainingDays <= 0
										? 'text-red-600 dark:text-red-400 font-semibold'
										: 'text-emerald-600 dark:text-emerald-400 font-semibold'"
								>{{ balance.remainingDays }} remaining</span>
							</div>
						</div>
					</div>
				</section>

				<!-- Vacation days list -->
				<section>
					<h2 class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500 mb-3">Planned days</h2>

					<!-- Skeleton -->
					<div v-if="loading" class="card divide-y divide-slate-100 dark:divide-slate-800">
						<div v-for="i in 3" :key="i" class="flex items-center gap-4 px-5 py-4">
							<div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-24 animate-pulse" />
							<div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-32 animate-pulse flex-1" />
							<div class="h-6 bg-slate-200 dark:bg-slate-700 rounded w-16 animate-pulse" />
						</div>
					</div>

					<!-- Empty -->
					<div v-else-if="vacationDays.length === 0" class="card text-center py-10">
						<i class="pi pi-calendar text-3xl text-slate-300 dark:text-slate-600 mb-2 block"></i>
						<p class="text-sm text-slate-500 dark:text-slate-400">No vacation days planned yet.</p>
					</div>

					<!-- List -->
					<div v-else class="card divide-y divide-slate-100 dark:divide-slate-800 overflow-hidden">
						<div
							v-for="day in vacationDays"
							:key="day.id"
							class="flex items-center gap-3 px-5 py-3.5"
						>
							<!-- Date -->
							<span class="text-sm font-medium text-slate-900 dark:text-slate-100 w-32 shrink-0">
								{{ displayDate(day.date) }}
							</span>

							<!-- Type + note -->
							<div class="flex items-center gap-2 flex-1 min-w-0">
								<div
									class="w-2.5 h-2.5 rounded-full shrink-0 ring-1 ring-black/10"
									:style="{ backgroundColor: day.vacationTypeColor ?? '#6366f1' }"
								/>
								<span class="text-sm text-slate-600 dark:text-slate-400 truncate">
									{{ day.vacationTypeName }}
									<span v-if="day.note" class="text-slate-400 dark:text-slate-500"> · {{ day.note }}</span>
								</span>
							</div>

							<!-- Amount badge -->
							<span
								:class="[
									'inline-flex items-center px-2 py-0.5 rounded text-xs font-medium shrink-0',
									day.amount === 1
										? 'bg-indigo-50 dark:bg-indigo-950 text-indigo-700 dark:text-indigo-300'
										: 'bg-sky-50 dark:bg-sky-950 text-sky-700 dark:text-sky-300',
								]"
							>
								{{ day.amount === 1 ? "Full day" : "Half day" }}
							</span>

							<!-- Actions -->
							<div class="flex items-center gap-1 shrink-0">
								<button
									@click="openEdit(day)"
									class="btn-ghost !px-2 !py-1.5 text-slate-400 hover:text-slate-700 dark:hover:text-slate-200"
									title="Edit"
								>
									<i class="pi pi-pencil text-sm"></i>
								</button>
								<button
									@click="deleteDay(day)"
									class="btn-ghost !px-2 !py-1.5 text-slate-400 hover:text-red-500 dark:hover:text-red-400"
									title="Delete"
								>
									<i class="pi pi-trash text-sm"></i>
								</button>
							</div>
						</div>
					</div>
				</section>

			</div>
		</div>

		<!-- Plan / Edit dialog -->
		<Dialog
			v-model:visible="dialogVisible"
			:header="dialogTitle"
			modal
			:style="{ width: '400px' }"
			:draggable="false"
		>
			<div class="flex flex-col gap-4 pt-1">

				<!-- Vacation type -->
				<div>
					<label class="form-label">Vacation type <span class="text-red-500">*</span></label>
					<Select
						v-model="form.vacationTypeId"
						:options="balances"
						optionLabel="vacationTypeName"
						optionValue="vacationTypeId"
						placeholder="Select a type"
						class="w-full"
					/>
				</div>

				<!-- Date -->
				<div>
					<label class="form-label">Date <span class="text-red-500">*</span></label>
					<DatePicker
						v-model="form.date"
						dateFormat="dd/mm/yy"
						placeholder="Pick a date"
						class="w-full"
						showIcon
					/>
				</div>

				<!-- Full / Half day -->
				<div>
					<label class="form-label">Duration <span class="text-red-500">*</span></label>
					<Select
						v-model="form.amount"
						:options="amountOptions"
						optionLabel="label"
						optionValue="value"
						class="w-full"
					/>
				</div>

				<!-- Note -->
				<div>
					<label class="form-label">Note</label>
					<input
						v-model="form.note"
						type="text"
						class="input-field"
						placeholder="Optional note"
						maxlength="500"
					/>
				</div>

				<!-- Live balance feedback -->
				<div
					v-if="form.vacationTypeId && liveRemaining !== null"
					:class="[
						'rounded-lg px-3 py-2 text-sm flex items-center gap-2',
						liveRemaining < 0
							? 'bg-red-50 dark:bg-red-950/40 text-red-700 dark:text-red-300'
							: 'bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-300',
					]"
				>
					<i :class="['pi text-sm', liveRemaining < 0 ? 'pi-times-circle' : 'pi-check-circle']"></i>
					<span v-if="liveRemaining < 0">Exceeds balance — {{ Math.abs(liveRemaining) }} day(s) short</span>
					<span v-else>{{ liveRemaining }} day(s) remaining after this entry</span>
				</div>

			</div>

			<template #footer>
				<div class="flex justify-end gap-2">
					<button @click="dialogVisible = false" class="btn-secondary">Cancel</button>
					<button @click="save" :disabled="saving || !canSubmit" class="btn-primary">
						<span v-if="saving"><i class="pi pi-spin pi-spinner mr-2"></i>Saving…</span>
						<span v-else>{{ editingDay ? "Save changes" : "Plan day" }}</span>
					</button>
				</div>
			</template>
		</Dialog>
	</AuthenticatedLayout>
</template>
