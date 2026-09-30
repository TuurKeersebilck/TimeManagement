<script setup lang="ts">
import { ref, computed, watch } from "vue";
import { adminService, type AdminTimeLog } from "@/services/adminService";
import { adjustmentRequestService } from "@/services/adjustmentRequestService";
import { settlementService } from "@/services/settlementService";
import { useAppToast } from "@/composables/useAppToast";
import { useConfirmDialog } from "@/composables/useConfirmDialog";
import { extractApiError } from "@/utils/apiError";
import DaySessionsEditor from "@/components/DaySessionsEditor.vue";
import {
  emptySession,
  toSnapshot,
  toTimeString,
  validateDaySessions,
  type EditableSession,
} from "@/lib/daySessions";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { AlertTriangleIcon, Loader2Icon, Trash2Icon } from "lucide-vue-next";

/**
 * Lets an admin set an employee's sessions for one day directly — edit, add or remove — instead
 * of waiting for the employee to send an adjustment request. Always loads the day fresh from the
 * server so it never edits a stale copy.
 */
const props = defineProps<{
  employeeId: string;
  employeeName: string;
  /** Day to edit (yyyy-MM-dd). Null lets the admin pick a date (adding hours). */
  date: string | null;
}>();

const open = defineModel<boolean>("open", { required: true });
const emit = defineEmits<{ saved: [] }>();

const toast = useAppToast();
const { confirm } = useConfirmDialog();

const selectedDate = ref("");
const sessions = ref<EditableSession[]>([]);
const reason = ref("");
const dayLog = ref<AdminTimeLog | null>(null);
const settledMonths = ref(new Set<string>());
const loading = ref(false);
const saving = ref(false);

const todayStr = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
};

const isAdding = computed(() => props.date === null);
const stillClockedIn = computed(() => dayLog.value?.hasOpenSession === true);
const monthSettled = computed(() => settledMonths.value.has(selectedDate.value.slice(0, 7)));
const hasLoggedHours = computed(() => (dayLog.value?.sessions.length ?? 0) > 0);
const removesDay = computed(() => sessions.value.length === 0 && hasLoggedHours.value);

const dateLabel = computed(() =>
  selectedDate.value
    ? new Date(`${selectedDate.value}T00:00:00`).toLocaleDateString(undefined, {
        weekday: "long",
        day: "numeric",
        month: "long",
        year: "numeric",
      })
    : ""
);

function toEditable(log: AdminTimeLog | null): EditableSession[] {
  if (!log || log.sessions.length === 0) return [emptySession()];
  return log.sessions.map((s) => ({
    workSessionId: s.id,
    clockIn: toTimeString(s.clockIn),
    clockOut: toTimeString(s.clockOut),
    note: s.status === "Invalidated" ? "auto-closed — set the clock-out" : undefined,
    breaks: s.breaks.map((b) => ({
      breakRecordId: b.id,
      breakStart: toTimeString(b.breakStart),
      breakEnd: toTimeString(b.breakEnd),
    })),
  }));
}

let loadRequest = 0;
const loadedDate = ref("");

async function loadDay(date: string) {
  if (!date) return;
  // Picking dates quickly can overlap requests; only the latest may fill the form.
  const request = ++loadRequest;
  loadedDate.value = date;
  loading.value = true;
  try {
    const logs = await adminService.getAllTimeLogs({
      userId: props.employeeId,
      dateFrom: date,
      dateTo: date,
    });
    if (request !== loadRequest) return;
    dayLog.value = logs.find((l) => l.date.startsWith(date)) ?? null;
    sessions.value = toEditable(dayLog.value);
  } catch {
    if (request === loadRequest) toast.error("Failed to load this day");
  } finally {
    if (request === loadRequest) loading.value = false;
  }
}

async function loadSettledMonths() {
  try {
    const history = await settlementService.getEmployeeHistory(props.employeeId);
    settledMonths.value = new Set(
      history
        .filter((s) => s.status === "Settled")
        .map((s) => `${s.year}-${String(s.month).padStart(2, "0")}`)
    );
  } catch {
    // Only feeds a warning; editing still works without it.
    settledMonths.value = new Set();
  }
}

// immediate: the parent may mount this dialog already open.
watch(
  open,
  (isOpen) => {
    if (!isOpen) return;
    selectedDate.value = props.date ?? todayStr();
    reason.value = "";
    dayLog.value = null;
    loadDay(selectedDate.value);
    loadSettledMonths();
  },
  { immediate: true }
);

// Adding hours: picking another date loads that day (it may already have hours to edit).
watch(selectedDate, (date) => {
  if (open.value && isAdding.value && date && date !== loadedDate.value) loadDay(date);
});

async function submit(date: string, daySessions: EditableSession[], successMessage: string) {
  saving.value = true;
  try {
    await adjustmentRequestService.adminEditDay({
      userId: props.employeeId,
      date,
      desiredDaySnapshot: toSnapshot(date, daySessions),
      reason: reason.value.trim() || undefined,
    });
    toast.success(successMessage);
    open.value = false;
    emit("saved");
  } catch (err) {
    toast.error(extractApiError(err, "Failed to save hours"));
  } finally {
    saving.value = false;
  }
}

async function save() {
  const invalid = validateDaySessions(sessions.value, { allowEmpty: true });
  if (invalid) {
    toast.error(invalid);
    return;
  }
  if (selectedDate.value > todayStr()) {
    toast.error("You can't enter hours for a future date");
    return;
  }
  await submit(selectedDate.value, sessions.value, removesDay.value ? "Hours removed" : "Hours saved");
}

/** Remove every session of the day. Closes this dialog first so the confirmation isn't stacked on it. */
function deleteDay() {
  const date = selectedDate.value;
  open.value = false;
  confirm({
    title: "Delete hours",
    message: `Delete all of ${props.employeeName}'s hours on ${dateLabel.value}? The change is kept in the adjustment history.`,
    confirmLabel: "Delete",
    variant: "destructive",
    onConfirm: () => submit(date, [], "Hours removed"),
  });
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="sm:max-w-130 max-h-[90vh] overflow-y-auto">
      <DialogHeader>
        <DialogTitle>{{ isAdding ? "Add hours" : "Edit hours" }} — {{ employeeName }}</DialogTitle>
        <DialogDescription>
          Changes apply immediately; no request or approval is needed.
        </DialogDescription>
      </DialogHeader>

      <div class="space-y-4 py-1">
        <div v-if="isAdding" class="space-y-1.5">
          <Label>Date</Label>
          <Input v-model="selectedDate" type="date" :max="todayStr()" class="cursor-pointer" />
        </div>
        <p v-else class="text-sm font-medium text-slate-700 dark:text-slate-300">{{ dateLabel }}</p>

        <!-- Hints: they inform, the admin decides -->
        <div
          v-if="stillClockedIn"
          class="flex items-start gap-2 rounded-lg border border-amber-200 dark:border-amber-800 bg-amber-50 dark:bg-amber-950/30 px-3 py-2 text-xs text-amber-700 dark:text-amber-300"
        >
          <AlertTriangleIcon class="size-3.5 shrink-0 mt-px" />
          {{ employeeName }} is still clocked in on this day. You can edit it once they clock out.
        </div>
        <div
          v-else-if="monthSettled"
          class="flex items-start gap-2 rounded-lg border border-amber-200 dark:border-amber-800 bg-amber-50 dark:bg-amber-950/30 px-3 py-2 text-xs text-amber-700 dark:text-amber-300"
        >
          <AlertTriangleIcon class="size-3.5 shrink-0 mt-px" />
          This month has already been settled. The settlement keeps its confirmed numbers; use a
          flex adjustment on the employee page if the balance needs correcting too.
        </div>

        <div v-if="loading" class="flex items-center gap-2 text-xs text-slate-400 py-1">
          <Loader2Icon class="size-3 animate-spin" />
          Loading this day…
        </div>
        <template v-else-if="!stillClockedIn">
          <DaySessionsEditor v-model="sessions" allow-empty />

          <p v-if="removesDay" class="text-xs text-amber-600 dark:text-amber-400">
            No sessions left — saving removes all hours for this day.
          </p>

          <div class="space-y-1.5">
            <Label>Reason <span class="text-slate-400 font-normal">(optional)</span></Label>
            <textarea
              v-model="reason"
              rows="2"
              maxlength="2000"
              class="input-field resize-none"
              placeholder="e.g. forgot to clock out"
            />
            <p class="text-xs text-slate-400">Kept in the adjustment history; the employee isn't notified.</p>
          </div>
        </template>
      </div>

      <DialogFooter class="sm:justify-between gap-2">
        <Button
          v-if="hasLoggedHours && !stillClockedIn && !loading"
          variant="ghost"
          class="text-destructive hover:text-destructive hover:bg-destructive/10 sm:mr-auto"
          :disabled="saving"
          @click="deleteDay"
        >
          <Trash2Icon class="size-4" />
          Delete day
        </Button>
        <div class="flex flex-col-reverse sm:flex-row gap-2">
          <Button variant="outline" @click="open = false">Cancel</Button>
          <Button
            :variant="removesDay ? 'destructive' : 'default'"
            :disabled="saving || loading || stillClockedIn || !selectedDate"
            @click="save"
          >
            <Loader2Icon v-if="saving" class="size-4 animate-spin" />
            {{ removesDay ? "Remove hours" : "Save" }}
          </Button>
        </div>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
