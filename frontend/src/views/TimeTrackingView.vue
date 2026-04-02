<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import { timeLogService, type TimeLog, type TimeLogCreate } from "../services/timeLogService";
import { useTimeCalculations } from "../composables/useTimeCalculations";
import { useTimeLogsStore } from "../composables/useTimeLogsStore";
import { useAppToast } from "@/composables/useAppToast";
import { extractApiError } from "@/utils/apiError";
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
import {
  Table,
  TableBody,
  TableCell,
  TableEmpty,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  PlusIcon,
  PencilIcon,
  Trash2Icon,
  ClockIcon,
  CheckCircleIcon,
  Loader2Icon,
  ChevronLeftIcon,
  ChevronRightIcon,
} from "lucide-vue-next";

const toast = useAppToast();
const { confirm } = useConfirmDialog();

const { timeLogs, loading, fetchTimeLogs, refreshTimeLogs } = useTimeLogsStore();

const { totalHoursToday, totalHoursThisWeek, totalHoursThisMonth } = useTimeCalculations(timeLogs);

// ─── Pagination ───────────────────────────────────────────────────────────────

const pageSize = 10;
const currentPage = ref(1);
const totalPages = computed(() => Math.max(1, Math.ceil(timeLogs.value.length / pageSize)));
const paginatedLogs = computed(() => {
  const start = (currentPage.value - 1) * pageSize;
  return timeLogs.value.slice(start, start + pageSize);
});

// ─── Dialog state ────────────────────────────────────────────────────────────

const showDialog = ref(false);
const editMode = ref(false);
const currentLogId = ref<number | null>(null);
const saving = ref(false);

const todayStr = () => {
  const d = new Date();
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
};

const emptyForm = () => ({
  date: todayStr(),
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

const toTimeStr = (t: string) => (t ? `${t}:00` : undefined);

const validateForm = (): boolean => {
  const errors: Record<string, string> = {};
  const { startTime, endTime, breakStart, breakEnd } = formData.value;

  if (!startTime) errors.startTime = "Required";
  if (!endTime) errors.endTime = "Required";

  if (startTime && endTime && endTime <= startTime) errors.endTime = "Must be after start time";

  const hasBreakStart = !!breakStart;
  const hasBreakEnd = !!breakEnd;
  if (hasBreakStart && !hasBreakEnd) errors.breakEnd = "Required when break start is set";
  if (!hasBreakStart && hasBreakEnd) errors.breakStart = "Required when break end is set";
  if (hasBreakStart && hasBreakEnd) {
    if (breakStart < startTime || breakEnd > endTime)
      errors.breakStart = "Break must fall within working hours";
    else if (breakEnd <= breakStart) errors.breakEnd = "Break end must be after break start";
  }

  // Duplicate date check against cached logs (exclude current log when editing)
  const duplicate = timeLogs.value.some(
    (log) =>
      log.date.split("T")[0] === formData.value.date &&
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
    date: log.date.split("T")[0],
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
    const { startTime, endTime, breakStart, breakEnd, description } = formData.value;

    const payload: TimeLogCreate = {
      date: formData.value.date,
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
    toast.success(editMode.value ? "Time log updated" : "Time log saved");
  } catch (err: unknown) {
    toast.error(extractApiError(err, "Failed to save time log"));
  } finally {
    saving.value = false;
  }
};

const confirmDelete = (log: TimeLog) => {
  confirm({
    title: "Delete time log",
    message: `Delete the time log for ${formatDate(log.date)}?`,
    confirmLabel: "Delete",
    variant: "destructive",
    onConfirm: () => deleteTimeLog(log.id),
  });
};

const deleteTimeLog = async (id: number) => {
  try {
    loading.value = true;
    await timeLogService.delete(id);
    await refreshTimeLogs();
    toast.success("Time log deleted");
  } catch {
    toast.error("Failed to delete time log");
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
    toast.error("Failed to load time logs");
  }
});
</script>

<template>
  <AuthenticatedLayout>
    <div class="p-6 lg:p-8">
      <div class="max-w-6xl mx-auto">
        <!-- Header -->
        <div class="flex items-center justify-between mb-8">
          <div>
            <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">My Time Logs</h1>
            <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
              Track your daily working hours
            </p>
          </div>
          <Button @click="openNewDialog">
            <PlusIcon class="size-4" />
            Log hours
          </Button>
        </div>

        <!-- Stats -->
        <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-8">
          <div class="stat-card">
            <p
              class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1"
            >
              Today
            </p>
            <p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
              <span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600"
                >--</span
              >
              <span v-else>{{ totalHoursToday }}h</span>
            </p>
          </div>
          <div class="stat-card">
            <p
              class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1"
            >
              This week
            </p>
            <p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
              <span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600"
                >--</span
              >
              <span v-else>{{ totalHoursThisWeek }}h</span>
            </p>
          </div>
          <div class="stat-card">
            <p
              class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1"
            >
              This month
            </p>
            <p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
              <span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600"
                >--</span
              >
              <span v-else>{{ totalHoursThisMonth }}h</span>
            </p>
          </div>
        </div>

        <!-- Table -->
        <div class="card overflow-hidden">
          <!-- Loading skeleton -->
          <div v-if="loading" class="divide-y divide-slate-100 dark:divide-slate-800">
            <div v-for="i in 5" :key="i" class="flex items-center gap-4 px-4 py-3.5">
              <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-24 animate-pulse" />
              <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-28 animate-pulse" />
              <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-20 animate-pulse" />
              <div class="ml-auto h-5 bg-slate-200 dark:bg-slate-700 rounded w-12 animate-pulse" />
            </div>
          </div>

          <Table v-else>
            <TableHeader>
              <TableRow>
                <TableHead>Date</TableHead>
                <TableHead>Hours</TableHead>
                <TableHead>Break</TableHead>
                <TableHead>Total</TableHead>
                <TableHead>Description</TableHead>
                <TableHead class="w-20" />
              </TableRow>
            </TableHeader>
            <TableBody>
              <TableEmpty v-if="timeLogs.length === 0" :colspan="6">
                <ClockIcon class="size-8 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
                <p class="text-slate-500 dark:text-slate-400">
                  No time logs yet. Log your first day above.
                </p>
              </TableEmpty>
              <TableRow v-for="log in paginatedLogs" :key="log.id">
                <TableCell class="font-medium text-slate-900 dark:text-slate-100">
                  {{ formatDate(log.date) }}
                </TableCell>
                <TableCell class="text-slate-600 dark:text-slate-400">
                  {{ formatTime(log.startTime) }} – {{ formatTime(log.endTime) }}
                </TableCell>
                <TableCell class="text-slate-500 dark:text-slate-500 text-xs">
                  {{ formatBreak(log) }}
                </TableCell>
                <TableCell>
                  <span
                    class="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-semibold bg-indigo-50 dark:bg-indigo-950 text-indigo-700 dark:text-indigo-300"
                  >
                    {{ log.totalHours?.toFixed(2) ?? "0.00" }}h
                  </span>
                </TableCell>
                <TableCell
                  class="text-slate-600 dark:text-slate-400 text-sm max-w-[200px] truncate"
                  :title="log.description"
                >
                  {{
                    log.description
                      ? log.description.length > 60
                        ? log.description.substring(0, 60) + "…"
                        : log.description
                      : "—"
                  }}
                </TableCell>
                <TableCell>
                  <div class="flex gap-1 justify-end">
                    <Button
                      variant="ghost"
                      size="icon"
                      class="size-7 text-slate-400 hover:text-indigo-600 dark:hover:text-indigo-400"
                      title="Edit"
                      @click="openEditDialog(log)"
                    >
                      <PencilIcon class="size-3.5" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      class="size-7 text-slate-400 hover:text-red-500 dark:hover:text-red-400"
                      title="Delete"
                      @click="confirmDelete(log)"
                    >
                      <Trash2Icon class="size-3.5" />
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            </TableBody>
          </Table>

          <!-- Pagination -->
          <div
            v-if="!loading && totalPages > 1"
            class="flex items-center justify-between px-4 py-3 border-t border-slate-100 dark:border-slate-800"
          >
            <p class="text-xs text-slate-500 dark:text-slate-400">
              Showing {{ (currentPage - 1) * pageSize + 1 }}–{{
                Math.min(currentPage * pageSize, timeLogs.length)
              }}
              of {{ timeLogs.length }}
            </p>
            <div class="flex items-center gap-1">
              <Button
                variant="ghost"
                size="icon"
                class="size-7"
                :disabled="currentPage === 1"
                @click="currentPage--"
              >
                <ChevronLeftIcon class="size-3.5" />
              </Button>
              <span class="text-xs text-slate-600 dark:text-slate-400 px-2"
                >{{ currentPage }} / {{ totalPages }}</span
              >
              <Button
                variant="ghost"
                size="icon"
                class="size-7"
                :disabled="currentPage === totalPages"
                @click="currentPage++"
              >
                <ChevronRightIcon class="size-3.5" />
              </Button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Add / Edit Dialog -->
    <Dialog v-model:open="showDialog">
      <DialogContent class="sm:max-w-[480px]">
        <DialogHeader>
          <DialogTitle>{{ editMode ? "Edit time log" : "Log hours" }}</DialogTitle>
        </DialogHeader>

        <div class="space-y-4 py-1">
          <!-- Date -->
          <div class="space-y-1.5">
            <Label>Date</Label>
            <Input v-model="formData.date" type="date" />
            <p v-if="formErrors.date" class="text-xs text-destructive">{{ formErrors.date }}</p>
          </div>

          <!-- Start / End times -->
          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <Label>Start time</Label>
              <Input v-model="formData.startTime" type="time" />
              <p v-if="formErrors.startTime" class="text-xs text-destructive">
                {{ formErrors.startTime }}
              </p>
            </div>
            <div class="space-y-1.5">
              <Label>End time</Label>
              <Input v-model="formData.endTime" type="time" />
              <p v-if="formErrors.endTime" class="text-xs text-destructive">
                {{ formErrors.endTime }}
              </p>
            </div>
          </div>

          <!-- Break times -->
          <div class="space-y-1.5">
            <Label>Break <span class="font-normal text-muted-foreground">(optional)</span></Label>
            <div class="grid grid-cols-2 gap-3">
              <div>
                <Input v-model="formData.breakStart" type="time" placeholder="Start" />
                <p v-if="formErrors.breakStart" class="mt-1 text-xs text-destructive">
                  {{ formErrors.breakStart }}
                </p>
              </div>
              <div>
                <Input v-model="formData.breakEnd" type="time" placeholder="End" />
                <p v-if="formErrors.breakEnd" class="mt-1 text-xs text-destructive">
                  {{ formErrors.breakEnd }}
                </p>
              </div>
            </div>
          </div>

          <!-- Description -->
          <div class="space-y-1.5">
            <Label>Description</Label>
            <textarea
              v-model="formData.description"
              rows="3"
              class="input-field resize-none"
              placeholder="What did you work on today?"
            />
          </div>

          <!-- Preview -->
          <div v-if="previewHours" class="flex items-center gap-2 text-sm text-primary">
            <CheckCircleIcon class="size-3.5" />
            <span
              >Total: <strong>{{ previewHours }}</strong></span
            >
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" @click="showDialog = false">Cancel</Button>
          <Button @click="saveTimeLog" :disabled="saving">
            <Loader2Icon v-if="saving" class="size-4 animate-spin" />
            {{ editMode ? "Update" : "Save" }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </AuthenticatedLayout>
</template>
