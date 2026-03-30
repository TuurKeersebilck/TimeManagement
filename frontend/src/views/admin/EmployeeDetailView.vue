<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import { adminService, type Employee, type AdminVacationDay } from "../../services/adminService";
import { vacationTypeService, type VacationType, type EmployeeVacationBalance } from "../../services/vacationTypeService";
import Dialog from "primevue/dialog";
import Select from "primevue/select";
import { useToast } from "primevue/usetoast";
import Toast from "primevue/toast";
import ConfirmDialog from "primevue/confirmdialog";
import { useConfirm } from "primevue/useconfirm";

const route = useRoute();
const router = useRouter();
const toast = useToast();
const confirm = useConfirm();

const userId = route.params.id as string;

const employee = ref<Employee | null>(null);
const balances = ref<EmployeeVacationBalance[]>([]);
const allTypes = ref<VacationType[]>([]);
const vacationDays = ref<AdminVacationDay[]>([]);
const loading = ref(false);

// ─── Assign dialog ────────────────────────────────────────────────────────────

const dialogVisible = ref(false);
const saving = ref(false);
const editingBalance = ref<EmployeeVacationBalance | null>(null);

const form = ref<{ vacationTypeId: number | null; yearlyBalance: string }>({
	vacationTypeId: null,
	yearlyBalance: "",
});

const assignedTypeIds = computed(() => new Set(balances.value.map((b) => b.vacationTypeId)));

const availableTypes = computed(() =>
	allTypes.value.filter((t) => !assignedTypeIds.value.has(t.id) || editingBalance.value?.vacationTypeId === t.id)
);

const dialogTitle = computed(() =>
	editingBalance.value ? "Edit balance" : "Assign vacation type"
);

const openAssign = () => {
	editingBalance.value = null;
	form.value = { vacationTypeId: null, yearlyBalance: "" };
	dialogVisible.value = true;
};

const openEdit = (balance: EmployeeVacationBalance) => {
	editingBalance.value = balance;
	form.value = { vacationTypeId: balance.vacationTypeId, yearlyBalance: String(balance.yearlyBalance) };
	dialogVisible.value = true;
};

const saveBalance = async () => {
	const days = parseFloat(form.value.yearlyBalance);
	if (isNaN(days) || days < 0) return;
	saving.value = true;
	try {
		if (editingBalance.value) {
			const updated = await vacationTypeService.updateBalance(userId, editingBalance.value.id, days);
			const idx = balances.value.findIndex((b) => b.id === editingBalance.value!.id);
			if (idx !== -1) balances.value[idx] = updated;
			toast.add({ severity: "success", summary: "Saved", detail: "Balance updated", life: 3000 });
		} else {
			if (!form.value.vacationTypeId) return;
			const created = await vacationTypeService.assignType(userId, {
				vacationTypeId: form.value.vacationTypeId,
				yearlyBalance: days,
			});
			balances.value.push(created);
			balances.value.sort((a, b) => a.vacationTypeName.localeCompare(b.vacationTypeName));
			toast.add({ severity: "success", summary: "Assigned", detail: "Vacation type assigned", life: 3000 });
		}
		dialogVisible.value = false;
	} catch {
		toast.add({ severity: "error", summary: "Error", detail: "Failed to save", life: 3000 });
	} finally {
		saving.value = false;
	}
};

// ─── Remove ───────────────────────────────────────────────────────────────────

const removeBalance = (balance: EmployeeVacationBalance) => {
	confirm.require({
		header: "Remove vacation type",
		message: `Remove "${balance.vacationTypeName}" from ${employee.value?.fullName ?? "this employee"}?`,
		icon: "pi pi-exclamation-triangle",
		acceptLabel: "Remove",
		rejectLabel: "Cancel",
		accept: async () => {
			try {
				await vacationTypeService.removeBalance(userId, balance.id);
				balances.value = balances.value.filter((b) => b.id !== balance.id);
				toast.add({ severity: "success", summary: "Removed", detail: "Vacation type removed", life: 3000 });
			} catch {
				toast.add({ severity: "error", summary: "Error", detail: "Failed to remove", life: 3000 });
			}
		},
	});
};

// ─── Mount ────────────────────────────────────────────────────────────────────

const initials = computed(() =>
	employee.value?.fullName.split(" ").map((n) => n[0]).join("").substring(0, 2).toUpperCase() ?? ""
);

const currentYear = new Date().getFullYear();

// Calculate used days per vacation type from planned vacation days (current year)
const usedByType = computed(() => {
	const map = new Map<number, number>();
	for (const d of vacationDays.value) {
		if (new Date(d.date).getFullYear() === currentYear) {
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
		const [employees, fetchedBalances, fetchedTypes, fetchedDays] = await Promise.all([
			adminService.getEmployees(),
			vacationTypeService.getEmployeeBalances(userId),
			vacationTypeService.getAll(),
			adminService.getAllVacationDays({ userId }),
		]);
		employee.value = employees.find((e) => e.id === userId) ?? null;
		if (!employee.value) {
			router.push({ name: "admin-employees" });
			return;
		}
		balances.value = fetchedBalances;
		allTypes.value = fetchedTypes;
		vacationDays.value = fetchedDays;
	} catch {
		toast.add({ severity: "error", summary: "Error", detail: "Failed to load employee", life: 3000 });
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

				<!-- Back -->
				<button
					@click="router.push({ name: 'admin-employees' })"
					class="flex items-center gap-1.5 text-sm text-slate-500 dark:text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 mb-6 transition-colors"
				>
					<i class="pi pi-arrow-left text-xs"></i>
					All employees
				</button>

				<!-- Employee header skeleton -->
				<div v-if="loading" class="flex items-center gap-4 mb-8">
					<div class="w-14 h-14 rounded-full bg-slate-200 dark:bg-slate-700 animate-pulse shrink-0" />
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
						<h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">{{ employee.fullName }}</h1>
						<p class="text-sm text-slate-500 dark:text-slate-400">{{ employee.email }}</p>
					</div>
				</div>

				<!-- Vacation balances section -->
				<div>
					<div class="flex items-center justify-between mb-3">
						<h2 class="text-sm font-semibold text-slate-700 dark:text-slate-300">Vacation balances</h2>
						<button
							@click="openAssign"
							:disabled="availableTypes.length === 0"
							class="btn-secondary text-xs !py-1.5"
							:title="availableTypes.length === 0 ? 'All vacation types are already assigned' : undefined"
						>
							<i class="pi pi-plus mr-1.5 text-xs"></i>Assign type
						</button>
					</div>

					<!-- Loading skeleton -->
					<div v-if="loading" class="card divide-y divide-slate-100 dark:divide-slate-800">
						<div v-for="i in 2" :key="i" class="flex items-center gap-4 px-5 py-4">
							<div class="w-3 h-3 rounded-full bg-slate-200 dark:bg-slate-700 animate-pulse shrink-0" />
							<div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-32 animate-pulse flex-1" />
							<div class="h-6 bg-slate-200 dark:bg-slate-700 rounded w-16 animate-pulse" />
						</div>
					</div>

					<!-- Empty state -->
					<div v-else-if="balances.length === 0" class="card text-center py-10">
						<i class="pi pi-calendar-times text-3xl text-slate-300 dark:text-slate-600 mb-2 block"></i>
						<p class="text-sm text-slate-500 dark:text-slate-400 mb-3">No vacation types assigned yet.</p>
						<button
							v-if="allTypes.length > 0"
							@click="openAssign"
							class="btn-secondary text-xs"
						>
							Assign a type
						</button>
						<p v-else class="text-xs text-slate-400 dark:text-slate-500">
							Create vacation types first in
							<router-link :to="{ name: 'admin-vacation-types' }" class="text-indigo-600 dark:text-indigo-400 hover:underline">
								Vacation Types
							</router-link>.
						</p>
					</div>

					<!-- Balances list -->
					<div v-else class="card divide-y divide-slate-100 dark:divide-slate-800 overflow-hidden">
						<div
							v-for="balance in balances"
							:key="balance.id"
							class="px-5 py-4"
						>
							<div class="flex items-center gap-4 mb-2">
								<div
									class="w-3 h-3 rounded-full shrink-0 ring-1 ring-black/10"
									:style="{ backgroundColor: balance.vacationTypeColor ?? '#6366f1' }"
								/>
								<span class="flex-1 text-sm font-medium text-slate-900 dark:text-slate-100">
									{{ balance.vacationTypeName }}
								</span>
								<div class="flex items-center gap-1 shrink-0">
									<button
										@click="openEdit(balance)"
										class="btn-ghost !px-2 !py-1.5 text-slate-400 hover:text-slate-700 dark:hover:text-slate-200"
										title="Edit balance"
									>
										<i class="pi pi-pencil text-sm"></i>
									</button>
									<button
										@click="removeBalance(balance)"
										class="btn-ghost !px-2 !py-1.5 text-slate-400 hover:text-red-500 dark:hover:text-red-400"
										title="Remove"
									>
										<i class="pi pi-trash text-sm"></i>
									</button>
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
										width: balance.yearlyBalance > 0
											? `${Math.min(((usedByType.get(balance.vacationTypeId) ?? 0) / balance.yearlyBalance) * 100, 100)}%`
											: '0%'
									}"
								/>
							</div>
							<div class="flex justify-between text-xs text-slate-500 dark:text-slate-400">
								<span>{{ usedByType.get(balance.vacationTypeId) ?? 0 }} / {{ balance.yearlyBalance }} days used ({{ new Date().getFullYear() }})</span>
								<span
									:class="(balance.yearlyBalance - (usedByType.get(balance.vacationTypeId) ?? 0)) <= 0
										? 'text-red-600 dark:text-red-400 font-medium'
										: 'text-emerald-600 dark:text-emerald-400 font-medium'"
								>{{ balance.yearlyBalance - (usedByType.get(balance.vacationTypeId) ?? 0) }} remaining</span>
							</div>
						</div>
					</div>
				</div>

				<!-- Planned vacation days section -->
				<div class="mt-6" v-if="!loading">
					<h2 class="text-sm font-semibold text-slate-700 dark:text-slate-300 mb-3">Planned vacation days</h2>

					<div v-if="vacationDays.length === 0" class="card text-center py-8">
						<i class="pi pi-calendar text-2xl text-slate-300 dark:text-slate-600 mb-2 block"></i>
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
									<span v-if="day.note" class="text-slate-400 dark:text-slate-500"> · {{ day.note }}</span>
								</span>
							</div>
							<span
								:class="[
									'text-xs font-medium px-1.5 py-0.5 rounded shrink-0',
									day.amount === 1
										? 'bg-indigo-50 dark:bg-indigo-950 text-indigo-700 dark:text-indigo-300'
										: 'bg-sky-50 dark:bg-sky-950 text-sky-700 dark:text-sky-300',
								]"
							>{{ day.amount === 1 ? "Full day" : "Half day" }}</span>
						</div>
					</div>
				</div>

			</div>
		</div>

		<!-- Assign / Edit dialog -->
		<Dialog
			v-model:visible="dialogVisible"
			:header="dialogTitle"
			modal
			:style="{ width: '380px' }"
			:draggable="false"
		>
			<div class="flex flex-col gap-4 pt-1">
				<div v-if="!editingBalance">
					<label class="form-label">Vacation type <span class="text-red-500">*</span></label>
					<Select
						v-model="form.vacationTypeId"
						:options="availableTypes"
						optionLabel="name"
						optionValue="id"
						placeholder="Select a type"
						class="w-full"
					/>
				</div>
				<div>
					<label class="form-label">Yearly balance (days) <span class="text-red-500">*</span></label>
					<input
						v-model="form.yearlyBalance"
						type="number"
						min="0"
						step="0.5"
						class="input-field"
						placeholder="e.g. 12"
					/>
				</div>
			</div>
			<template #footer>
				<div class="flex justify-end gap-2">
					<button @click="dialogVisible = false" class="btn-secondary">Cancel</button>
					<button
						@click="saveBalance"
						:disabled="saving || (!editingBalance && !form.vacationTypeId) || form.yearlyBalance === ''"
						class="btn-primary"
					>
						<span v-if="saving"><i class="pi pi-spin pi-spinner mr-2"></i>Saving…</span>
						<span v-else>{{ editingBalance ? "Save changes" : "Assign" }}</span>
					</button>
				</div>
			</template>
		</Dialog>
	</AuthenticatedLayout>
</template>
