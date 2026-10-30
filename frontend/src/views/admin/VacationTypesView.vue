<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { vacationTypeService, type VacationType } from "../../services/vacationTypeService";
import { useAppToast } from "@/composables/useAppToast";
import { useConfirmDialog } from "@/composables/useConfirmDialog";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { PlusIcon, PencilIcon, Trash2Icon, TagIcon, Loader2Icon } from "lucide-vue-next";

const toast = useAppToast();
const { confirm } = useConfirmDialog();

const types = ref<VacationType[]>([]);
const loading = ref(false);

// ─── Form dialog ──────────────────────────────────────────────────────────────

const dialogVisible = ref(false);
const saving = ref(false);
const editingId = ref<number | null>(null);

const emptyForm = () => ({
  name: "",
  description: "",
  color: "#6366f1",
  isUnlimited: false,
  isAdminOnly: false,
});

const form = ref(emptyForm());

const dialogTitle = computed(() => (editingId.value ? "Edit vacation type" : "New vacation type"));

const openCreate = () => {
  editingId.value = null;
  form.value = emptyForm();
  dialogVisible.value = true;
};

const openEdit = (type: VacationType) => {
  editingId.value = type.id;
  form.value = {
    name: type.name,
    description: type.description ?? "",
    color: type.color ?? "#6366f1",
    isUnlimited: type.isUnlimited,
    isAdminOnly: type.isAdminOnly,
  };
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
      isUnlimited: form.value.isUnlimited,
      isAdminOnly: form.value.isAdminOnly,
    };
    if (editingId.value) {
      const updated = await vacationTypeService.update(editingId.value, payload);
      const idx = types.value.findIndex((t) => t.id === editingId.value);
      if (idx !== -1) types.value[idx] = updated;
      toast.success("Vacation type updated");
    } else {
      const created = await vacationTypeService.create(payload);
      types.value.push(created);
      types.value.sort((a, b) => a.name.localeCompare(b.name));
      toast.success("Vacation type added");
    }
    dialogVisible.value = false;
  } catch {
    toast.error("Failed to save vacation type");
  } finally {
    saving.value = false;
  }
};

// ─── Delete ───────────────────────────────────────────────────────────────────

const deleteType = (type: VacationType) => {
  const hasAssigned = type.assignedEmployeeCount > 0;
  confirm({
    title: "Delete vacation type",
    message: hasAssigned
      ? `"${type.name}" is assigned to ${type.assignedEmployeeCount} employee${type.assignedEmployeeCount > 1 ? "s" : ""}. Deleting it will remove their balance. Continue?`
      : `Delete "${type.name}"? This cannot be undone.`,
    confirmLabel: "Delete",
    variant: "destructive",
    onConfirm: async () => {
      try {
        await vacationTypeService.delete(type.id);
        types.value = types.value.filter((t) => t.id !== type.id);
        toast.success("Vacation type removed");
      } catch {
        toast.error("Failed to delete vacation type");
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
    toast.error("Failed to load vacation types");
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <div class="p-6 lg:p-8">
    <div class="max-w-3xl mx-auto">
      <!-- Header -->
      <div class="flex items-center justify-between mb-8">
        <div>
          <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">Vacation Types</h1>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
            Manage global vacation day types
          </p>
        </div>
        <Button @click="openCreate">
          <PlusIcon class="size-4" />
          New type
        </Button>
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
        <TagIcon class="size-10 text-slate-300 dark:text-slate-600 mb-3 mx-auto" />
        <p class="text-slate-500 dark:text-slate-400 mb-4">No vacation types yet.</p>
        <Button @click="openCreate">Create your first type</Button>
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
            <p class="flex flex-wrap items-center gap-1.5 text-sm font-medium text-slate-900 dark:text-slate-100">
              {{ type.name }}
              <span
                v-if="type.isUnlimited"
                class="rounded bg-slate-100 dark:bg-slate-800 px-1.5 py-0.5 text-[10px] font-medium text-slate-500 dark:text-slate-400"
              >No limit</span>
              <span
                v-if="type.isAdminOnly"
                class="rounded bg-slate-100 dark:bg-slate-800 px-1.5 py-0.5 text-[10px] font-medium text-slate-500 dark:text-slate-400"
              >Admin only</span>
            </p>
            <p
              v-if="type.description"
              class="text-xs text-slate-400 dark:text-slate-500 truncate mt-0.5"
            >
              {{ type.description }}
            </p>
          </div>

          <!-- Assigned count badge -->
          <span class="text-xs text-slate-500 dark:text-slate-400 shrink-0">
            <template v-if="type.isUnlimited">All employees</template>
            <template v-else>
              {{ type.assignedEmployeeCount }} employee{{
                type.assignedEmployeeCount !== 1 ? "s" : ""
              }}
            </template>
          </span>

          <!-- Actions -->
          <div class="flex items-center gap-1 shrink-0">
            <Button
              variant="ghost"
              size="icon"
              @click="openEdit(type)"
              class="size-8 text-slate-400 hover:text-slate-700 dark:hover:text-slate-200"
              title="Edit"
            >
              <PencilIcon class="size-3.5" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              @click="deleteType(type)"
              class="size-8 text-slate-400 hover:text-red-500 dark:hover:text-red-400"
              title="Delete"
            >
              <Trash2Icon class="size-3.5" />
            </Button>
          </div>
        </div>
      </div>
    </div>
  </div>

  <!-- Create / Edit dialog -->
  <Dialog v-model:open="dialogVisible">
    <DialogContent class="sm:max-w-[420px]">
      <DialogHeader>
        <DialogTitle>{{ dialogTitle }}</DialogTitle>
      </DialogHeader>

      <div class="flex flex-col gap-4 py-2">
        <div class="space-y-1.5">
          <Label>Name <span class="text-destructive">*</span></Label>
          <Input
            v-model="form.name"
            type="text"
            placeholder="e.g. ADV, Sick leave, Student"
            maxlength="100"
          />
        </div>
        <div class="space-y-1.5">
          <Label>Description</Label>
          <Input
            v-model="form.description"
            type="text"
            placeholder="Optional description"
            maxlength="255"
          />
        </div>
        <div class="space-y-1.5">
          <Label>Color</Label>
          <div class="flex items-center gap-3">
            <input
              v-model="form.color"
              type="color"
              class="w-10 h-10 rounded-lg cursor-pointer border border-border p-0.5 bg-background"
            />
            <span class="text-sm text-muted-foreground font-mono">{{ form.color }}</span>
          </div>
        </div>
        <div class="flex items-start gap-3">
          <Switch id="type-unlimited" v-model="form.isUnlimited" class="mt-0.5" />
          <div class="space-y-0.5">
            <Label for="type-unlimited">No balance limit</Label>
            <p class="text-xs text-muted-foreground">
              Available to every employee without assigning a yearly balance, e.g. sick leave.
            </p>
          </div>
        </div>
        <div class="flex items-start gap-3">
          <Switch id="type-admin-only" v-model="form.isAdminOnly" class="mt-0.5" />
          <div class="space-y-0.5">
            <Label for="type-admin-only">Only admins can register</Label>
            <p class="text-xs text-muted-foreground">
              Employees see these days but can't add, change or remove them.
            </p>
          </div>
        </div>
      </div>

      <DialogFooter>
        <Button variant="outline" @click="dialogVisible = false">Cancel</Button>
        <Button @click="saveType" :disabled="saving || !form.name.trim()">
          <Loader2Icon v-if="saving" class="size-4 animate-spin" />
          {{ editingId ? "Save changes" : "Create" }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
