<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import { vacationTypeService, type VacationType } from "../../services/vacationTypeService";
import Dialog from "primevue/dialog";
import { useToast } from "primevue/usetoast";
import Toast from "primevue/toast";
import ConfirmDialog from "primevue/confirmdialog";
import { useConfirm } from "primevue/useconfirm";

const toast = useToast();
const confirm = useConfirm();

const types = ref<VacationType[]>([]);
const loading = ref(false);

// ─── Form dialog ──────────────────────────────────────────────────────────────

const dialogVisible = ref(false);
const saving = ref(false);
const editingId = ref<number | null>(null);

const form = ref({ name: "", description: "", color: "#6366f1" });

const dialogTitle = computed(() => (editingId.value ? "Edit vacation type" : "New vacation type"));

const openCreate = () => {
	editingId.value = null;
	form.value = { name: "", description: "", color: "#6366f1" };
	dialogVisible.value = true;
};

const openEdit = (type: VacationType) => {
	editingId.value = type.id;
	form.value = { name: type.name, description: type.description ?? "", color: type.color ?? "#6366f1" };
	dialogVisible.value = true;
};

const saveType = async () => {
	if (!form.value.name.trim()) return;
	saving.value = true;
	try {
		const payload = {
			name: form.value.name.trim(),
			description: form.value.description.trim() || undefined,
			color: form.value.color || undefined,
		};
		if (editingId.value) {
			const updated = await vacationTypeService.update(editingId.value, payload);
			const idx = types.value.findIndex((t) => t.id === editingId.value);
			if (idx !== -1) types.value[idx] = updated;
			toast.add({ severity: "success", summary: "Saved", detail: "Vacation type updated", life: 3000 });
		} else {
			const created = await vacationTypeService.create(payload);
			types.value.push(created);
			types.value.sort((a, b) => a.name.localeCompare(b.name));
			toast.add({ severity: "success", summary: "Created", detail: "Vacation type added", life: 3000 });
		}
		dialogVisible.value = false;
	} catch {
		toast.add({ severity: "error", summary: "Error", detail: "Failed to save vacation type", life: 3000 });
	} finally {
		saving.value = false;
	}
};

// ─── Delete ───────────────────────────────────────────────────────────────────

const deleteType = (type: VacationType) => {
	const hasAssigned = type.assignedEmployeeCount > 0;
	confirm.require({
		header: "Delete vacation type",
		message: hasAssigned
			? `"${type.name}" is assigned to ${type.assignedEmployeeCount} employee${type.assignedEmployeeCount > 1 ? "s" : ""}. Deleting it will remove their balance. Continue?`
			: `Delete "${type.name}"? This cannot be undone.`,
		icon: "pi pi-exclamation-triangle",
		acceptClass: "btn-danger",
		acceptLabel: "Delete",
		rejectLabel: "Cancel",
		accept: async () => {
			try {
				await vacationTypeService.delete(type.id);
				types.value = types.value.filter((t) => t.id !== type.id);
				toast.add({ severity: "success", summary: "Deleted", detail: "Vacation type removed", life: 3000 });
			} catch {
				toast.add({ severity: "error", summary: "Error", detail: "Failed to delete vacation type", life: 3000 });
			}
		},
	});
};

// ─── Mount ────────────────────────────────────────────────────────────────────

onMounted(async () => {
	loading.value = true;
	try {
		types.value = await vacationTypeService.getAll();
	} catch {
		toast.add({ severity: "error", summary: "Error", detail: "Failed to load vacation types", life: 3000 });
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
						<h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">Vacation Types</h1>
						<p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">Manage global vacation day types</p>
					</div>
					<button @click="openCreate" class="btn-primary">
						<i class="pi pi-plus mr-2 text-sm"></i>New type
					</button>
				</div>

				<!-- Loading skeleton -->
				<div v-if="loading" class="card divide-y divide-slate-100 dark:divide-slate-800">
					<div v-for="i in 3" :key="i" class="flex items-center gap-4 px-5 py-4">
						<div class="w-3 h-3 rounded-full bg-slate-200 dark:bg-slate-700 animate-pulse shrink-0" />
						<div class="flex-1 space-y-1.5">
							<div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-32 animate-pulse" />
							<div class="h-2.5 bg-slate-200 dark:bg-slate-700 rounded w-48 animate-pulse" />
						</div>
					</div>
				</div>

				<!-- Empty state -->
				<div v-else-if="types.length === 0" class="card text-center py-16">
					<i class="pi pi-tag text-4xl text-slate-300 dark:text-slate-600 mb-3 block"></i>
					<p class="text-slate-500 dark:text-slate-400 mb-4">No vacation types yet.</p>
					<button @click="openCreate" class="btn-primary">Create your first type</button>
				</div>

				<!-- Types list -->
				<div v-else class="card divide-y divide-slate-100 dark:divide-slate-800 overflow-hidden">
					<div
						v-for="type in types"
						:key="type.id"
						class="flex items-center gap-4 px-5 py-4 hover:bg-slate-50 dark:hover:bg-slate-800/50 transition-colors"
					>
						<!-- Color swatch -->
						<div
							class="w-3 h-3 rounded-full shrink-0 ring-1 ring-black/10"
							:style="{ backgroundColor: type.color ?? '#6366f1' }"
						/>

						<!-- Name + description -->
						<div class="flex-1 min-w-0">
							<p class="text-sm font-medium text-slate-900 dark:text-slate-100">{{ type.name }}</p>
							<p v-if="type.description" class="text-xs text-slate-400 dark:text-slate-500 truncate mt-0.5">
								{{ type.description }}
							</p>
						</div>

						<!-- Assigned count badge -->
						<span class="text-xs text-slate-500 dark:text-slate-400 shrink-0">
							{{ type.assignedEmployeeCount }} employee{{ type.assignedEmployeeCount !== 1 ? "s" : "" }}
						</span>

						<!-- Actions -->
						<div class="flex items-center gap-1 shrink-0">
							<button
								@click="openEdit(type)"
								class="btn-ghost !px-2 !py-1.5 text-slate-400 hover:text-slate-700 dark:hover:text-slate-200"
								title="Edit"
							>
								<i class="pi pi-pencil text-sm"></i>
							</button>
							<button
								@click="deleteType(type)"
								class="btn-ghost !px-2 !py-1.5 text-slate-400 hover:text-red-500 dark:hover:text-red-400"
								title="Delete"
							>
								<i class="pi pi-trash text-sm"></i>
							</button>
						</div>
					</div>
				</div>

			</div>
		</div>

		<!-- Create / Edit dialog -->
		<Dialog
			v-model:visible="dialogVisible"
			:header="dialogTitle"
			modal
			:style="{ width: '420px' }"
			:draggable="false"
		>
			<div class="flex flex-col gap-4 pt-1">
				<div>
					<label class="form-label">Name <span class="text-red-500">*</span></label>
					<input
						v-model="form.name"
						type="text"
						class="input-field"
						placeholder="e.g. ADV, European, Student"
						maxlength="100"
					/>
				</div>
				<div>
					<label class="form-label">Description</label>
					<input
						v-model="form.description"
						type="text"
						class="input-field"
						placeholder="Optional description"
						maxlength="255"
					/>
				</div>
				<div>
					<label class="form-label">Color</label>
					<div class="flex items-center gap-3">
						<input
							v-model="form.color"
							type="color"
							class="w-10 h-10 rounded-lg cursor-pointer border border-slate-200 dark:border-slate-700 p-0.5 bg-white dark:bg-slate-800"
						/>
						<span class="text-sm text-slate-500 dark:text-slate-400 font-mono">{{ form.color }}</span>
					</div>
				</div>
			</div>
			<template #footer>
				<div class="flex justify-end gap-2">
					<button @click="dialogVisible = false" class="btn-secondary">Cancel</button>
					<button @click="saveType" :disabled="saving || !form.name.trim()" class="btn-primary">
						<span v-if="saving"><i class="pi pi-spin pi-spinner mr-2"></i>Saving…</span>
						<span v-else>{{ editingId ? "Save changes" : "Create" }}</span>
					</button>
				</div>
			</template>
		</Dialog>
	</AuthenticatedLayout>
</template>
