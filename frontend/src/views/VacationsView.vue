<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import { vacationService, type VacationBalance, type VacationDay, type CreateVacationDayDto } from "../services/vacationService";
import { useAppToast } from "@/composables/useAppToast";
import { useConfirmDialog } from "@/composables/useConfirmDialog";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PlusIcon, PencilIcon, Trash2Icon, CalendarIcon, CheckCircleIcon, XCircleIcon, Loader2Icon } from "lucide-vue-next";

const toast = useAppToast();
const { confirm } = useConfirmDialog();

const balances = ref<VacationBalance[]>([]);
const vacationDays = ref<VacationDay[]>([]);
const loading = ref(false);

// ─── Dialog ───────────────────────────────────────────────────────────────────

const dialogVisible = ref(false);
const saving = ref(false);
const editingDay = ref<VacationDay | null>(null);

const form = ref<{
	vacationTypeId: string;
	date: string;
	amount: string;
	note: string;
}>({
	vacationTypeId: "",
	date: "",
	amount: "1",
	note: "",
});

const dialogTitle = computed(() => (editingDay.value ? "Edit vacation day" : "Plan vacation day"));

// Live remaining balance while filling in the dialog
const liveRemaining = computed(() => {
	if (!form.value.vacationTypeId) return null;
	const typeId = parseInt(form.value.vacationTypeId);
	const balance = balances.value.find((b) => b.vacationTypeId === typeId);
	if (!balance) return null;
	// When editing, add back the original amount for the current year
	let base = balance.remainingDays;
	if (editingDay.value && editingDay.value.vacationTypeId === typeId) {
		const origYear = new Date(editingDay.value.date).getUTCFullYear();
		if (origYear === new Date().getUTCFullYear()) base += editingDay.value.amount;
	}
	return base - parseFloat(form.value.amount);
});

const canSubmit = computed(() => {
	if (!form.value.vacationTypeId || !form.value.date) return false;
	if (liveRemaining.value === null) return false;
	return liveRemaining.value >= 0;
});

const openCreate = () => {
	editingDay.value = null;
	form.value = { vacationTypeId: "", date: "", amount: "1", note: "" };
	dialogVisible.value = true;
};

const openEdit = (day: VacationDay) => {
	editingDay.value = day;
	form.value = {
		vacationTypeId: String(day.vacationTypeId),
		date: day.date,
		amount: String(day.amount),
		note: day.note ?? "",
	};
	dialogVisible.value = true;
};

const save = async () => {
	if (!canSubmit.value || !form.value.date || !form.value.vacationTypeId) return;
	saving.value = true;
	const payload: CreateVacationDayDto = {
		vacationTypeId: parseInt(form.value.vacationTypeId),
		date: form.value.date,
		amount: parseFloat(form.value.amount),
		note: form.value.note.trim() || undefined,
	};
	try {
		if (editingDay.value) {
			const updated = await vacationService.update(editingDay.value.id, payload);
			const idx = vacationDays.value.findIndex((d) => d.id === editingDay.value!.id);
			if (idx !== -1) vacationDays.value[idx] = updated;
			toast.success("Vacation day updated");
		} else {
			const created = await vacationService.create(payload);
			vacationDays.value.unshift(created);
			toast.success("Vacation day added");
		}
		// Refresh balances so the cards update
		balances.value = await vacationService.getBalances();
		dialogVisible.value = false;
	} catch (err: unknown) {
		const msg =
			(err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
			"Failed to save vacation day";
		toast.error(msg);
	} finally {
		saving.value = false;
	}
};

// ─── Delete ───────────────────────────────────────────────────────────────────

const deleteDay = (day: VacationDay) => {
	confirm({
		title: "Delete vacation day",
		message: `Remove the ${day.amount === 0.5 ? "half" : "full"} day on ${displayDate(day.date)} (${day.vacationTypeName})?`,
		confirmLabel: "Delete",
		variant: "destructive",
		onConfirm: async () => {
			try {
				await vacationService.delete(day.id);
				vacationDays.value = vacationDays.value.filter((d) => d.id !== day.id);
				balances.value = await vacationService.getBalances();
				toast.success("Vacation day removed");
			} catch {
				toast.error("Failed to delete");
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
		toast.error("Failed to load vacation data");
	} finally {
		loading.value = false;
	}
});
</script>

<template>
	<AuthenticatedLayout>
		<div class="p-6 lg:p-8">
			<div class="max-w-3xl mx-auto">

				<!-- Header -->
				<div class="flex items-center justify-between mb-8">
					<div>
						<h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">My Vacations</h1>
						<p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">Plan and track your vacation days</p>
					</div>
					<Button
						@click="openCreate"
						:disabled="balances.length === 0"
						:title="balances.length === 0 ? 'No vacation types assigned yet' : undefined"
					>
						<PlusIcon class="size-4" />
						Plan a day
					</Button>
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
						<CalendarIcon class="size-8 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
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
						<CalendarIcon class="size-6 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
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
								<Button
									variant="ghost"
									size="icon"
									class="size-8 text-slate-400 hover:text-slate-700 dark:hover:text-slate-200"
									title="Edit"
									@click="openEdit(day)"
								>
									<PencilIcon class="size-3.5" />
								</Button>
								<Button
									variant="ghost"
									size="icon"
									class="size-8 text-slate-400 hover:text-red-500 dark:hover:text-red-400"
									title="Delete"
									@click="deleteDay(day)"
								>
									<Trash2Icon class="size-3.5" />
								</Button>
							</div>
						</div>
					</div>
				</section>

			</div>
		</div>

		<!-- Plan / Edit dialog -->
		<Dialog v-model:open="dialogVisible">
			<DialogContent class="sm:max-w-[400px]">
				<DialogHeader>
					<DialogTitle>{{ dialogTitle }}</DialogTitle>
				</DialogHeader>

				<div class="flex flex-col gap-4 py-2">

					<!-- Vacation type -->
					<div class="space-y-1.5">
						<Label>Vacation type <span class="text-destructive">*</span></Label>
						<Select v-model="form.vacationTypeId">
							<SelectTrigger class="w-full">
								<SelectValue placeholder="Select a type" />
							</SelectTrigger>
							<SelectContent>
								<SelectItem
									v-for="balance in balances"
									:key="balance.vacationTypeId"
									:value="String(balance.vacationTypeId)"
								>
									{{ balance.vacationTypeName }}
								</SelectItem>
							</SelectContent>
						</Select>
					</div>

					<!-- Date -->
					<div class="space-y-1.5">
						<Label>Date <span class="text-destructive">*</span></Label>
						<Input v-model="form.date" type="date" />
					</div>

					<!-- Full / Half day -->
					<div class="space-y-1.5">
						<Label>Duration <span class="text-destructive">*</span></Label>
						<Select v-model="form.amount">
							<SelectTrigger class="w-full">
								<SelectValue />
							</SelectTrigger>
							<SelectContent>
								<SelectItem value="1">Full day (1.0)</SelectItem>
								<SelectItem value="0.5">Half day (0.5)</SelectItem>
							</SelectContent>
						</Select>
					</div>

					<!-- Note -->
					<div class="space-y-1.5">
						<Label>Note</Label>
						<Input
							v-model="form.note"
							type="text"
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
								? 'bg-destructive/10 text-destructive'
								: 'bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-300',
						]"
					>
						<XCircleIcon v-if="liveRemaining < 0" class="size-3.5 shrink-0" />
						<CheckCircleIcon v-else class="size-3.5 shrink-0" />
						<span v-if="liveRemaining < 0">Exceeds balance — {{ Math.abs(liveRemaining) }} day(s) short</span>
						<span v-else>{{ liveRemaining }} day(s) remaining after this entry</span>
					</div>

				</div>

				<DialogFooter>
					<Button variant="outline" @click="dialogVisible = false">Cancel</Button>
					<Button @click="save" :disabled="saving || !canSubmit">
						<Loader2Icon v-if="saving" class="size-4 animate-spin" />
						{{ editingDay ? "Save changes" : "Plan day" }}
					</Button>
				</DialogFooter>
			</DialogContent>
		</Dialog>
	</AuthenticatedLayout>
</template>
