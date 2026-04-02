<script setup lang="ts">
import { ref, computed, watch } from "vue";
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
import { CheckCircleIcon, XCircleIcon, Loader2Icon } from "lucide-vue-next";
import { vacationService, type VacationBalance, type VacationDay } from "../services/vacationService";
import { useAppToast } from "@/composables/useAppToast";
import { extractApiError } from "@/utils/apiError";

const props = defineProps<{
  open: boolean;
  editingDay: VacationDay | null;
  balances: VacationBalance[];
}>();

const emit = defineEmits<{
  "update:open": [value: boolean];
  saved: [updated: VacationDay, newBalances: VacationBalance[]];
}>();

const toast = useAppToast();
const saving = ref(false);

const form = ref({
  vacationTypeId: "",
  date: "",
  amount: "1",
  note: "",
});

watch(
  () => props.editingDay,
  (day) => {
    if (day) {
      form.value = {
        vacationTypeId: String(day.vacationTypeId),
        date: day.date,
        amount: String(day.amount),
        note: day.note ?? "",
      };
    }
  },
);

const liveRemaining = computed(() => {
  if (!form.value.vacationTypeId || !props.editingDay) return null;
  const typeId = parseInt(form.value.vacationTypeId);
  const balance = props.balances.find((b) => b.vacationTypeId === typeId);
  if (!balance) return null;
  let base = balance.remainingDays;
  if (props.editingDay.vacationTypeId === typeId) {
    const origYear = new Date(props.editingDay.date).getUTCFullYear();
    if (origYear === new Date().getUTCFullYear()) base += props.editingDay.amount;
  }
  return base - parseFloat(form.value.amount);
});

const canSubmit = computed(
  () => !!form.value.vacationTypeId && !!form.value.date && liveRemaining.value !== null && liveRemaining.value >= 0,
);

const save = async () => {
  if (!canSubmit.value || !props.editingDay) return;
  saving.value = true;
  try {
    const updated = await vacationService.update(props.editingDay.id, {
      vacationTypeId: parseInt(form.value.vacationTypeId),
      date: form.value.date,
      amount: parseFloat(form.value.amount),
      note: form.value.note.trim() || undefined,
    });
    const newBalances = await vacationService.getBalances();
    toast.success("Vacation day updated");
    emit("saved", updated, newBalances);
    emit("update:open", false);
  } catch (err: unknown) {
    toast.error(extractApiError(err, "Failed to update vacation day"));
  } finally {
    saving.value = false;
  }
};
</script>

<template>
  <Dialog :open="open" @update:open="emit('update:open', $event)">
    <DialogContent class="sm:max-w-[400px]">
      <DialogHeader>
        <DialogTitle>Edit vacation day</DialogTitle>
      </DialogHeader>

      <div class="flex flex-col gap-4 py-2">
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

        <div class="space-y-1.5">
          <Label>Date <span class="text-destructive">*</span></Label>
          <Input v-model="form.date" type="date" />
        </div>

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

        <div class="space-y-1.5">
          <Label>Note</Label>
          <Input v-model="form.note" type="text" placeholder="Optional note" maxlength="500" />
        </div>

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
          <span v-if="liveRemaining < 0">
            Exceeds balance — {{ Math.abs(liveRemaining) }} day(s) short
          </span>
          <span v-else>{{ liveRemaining }} day(s) remaining after this entry</span>
        </div>
      </div>

      <DialogFooter>
        <Button variant="outline" @click="emit('update:open', false)">Cancel</Button>
        <Button @click="save" :disabled="saving || !canSubmit">
          <Loader2Icon v-if="saving" class="size-4 animate-spin" />
          Save changes
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
