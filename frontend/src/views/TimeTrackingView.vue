<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import {
  clockEventService,
  CLOCK_EVENT_TYPE_ORDER,
  CLOCK_EVENT_LABELS,
  CLOCK_EVENT_ENUM,
  type ClockEvent,
  type ClockEventType,
} from "@/services/clockEventService";
import { adjustmentRequestService } from "@/services/adjustmentRequestService";
import { useAppToast } from "@/composables/useAppToast";
import { extractApiError } from "@/utils/apiError";
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
  ClockIcon,
  Loader2Icon,
  AlertCircleIcon,
  CheckCircleIcon,
  SendIcon,
} from "lucide-vue-next";

const toast = useAppToast();

// ─── State ────────────────────────────────────────────────────────────────────

const todayEvents = ref<ClockEvent[]>([]);
const loadingEvents = ref(false);
const submitting = ref(false);

// Clock-in/out form
const selectedTime = ref(nowTimeStr());
const description = ref("");

// Adjustment request dialog
const showAdjustDialog = ref(false);
const adjForm = ref(emptyAdjForm());
const adjSubmitting = ref(false);

// ─── Computed ─────────────────────────────────────────────────────────────────

const completedTypes = computed<Set<ClockEventType>>(
  () => new Set(todayEvents.value.map((e) => e.type as ClockEventType))
);

const nextAction = computed<ClockEventType | null>(() => {
  return CLOCK_EVENT_TYPE_ORDER.find((t) => !completedTypes.value.has(t)) ?? null;
});

const isDayComplete = computed(() => completedTypes.value.has("ClockOut"));

const minTime = computed(() => {
  const d = new Date();
  d.setMinutes(d.getMinutes() - 5);
  return timeToStr(d);
});

const maxTime = computed(() => {
  const d = new Date();
  d.setMinutes(d.getMinutes() + 5);
  return timeToStr(d);
});

const showDescription = computed(() => nextAction.value === "ClockOut");

// ─── Helpers ──────────────────────────────────────────────────────────────────

function nowTimeStr(): string {
  return timeToStr(new Date());
}

function timeToStr(d: Date): string {
  return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
}

function formatTime(t: string | undefined): string {
  return t ? t.substring(0, 5) : "—";
}

function eventLabel(type: string): string {
  return CLOCK_EVENT_LABELS[type as ClockEventType] ?? type;
}

function emptyAdjForm() {
  const today = new Date();
  const todayStr = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}-${String(today.getDate()).padStart(2, "0")}`;
  return {
    date: todayStr,
    clockIn: "",
    breakStart: "",
    breakEnd: "",
    clockOut: "",
    reason: "",
  };
}

function toTimeSpan(val: string): string | undefined {
  return val ? `${val}:00` : undefined;
}

// ─── Actions ──────────────────────────────────────────────────────────────────

async function loadTodayEvents() {
  try {
    loadingEvents.value = true;
    todayEvents.value = await clockEventService.getTodayEvents();
  } catch {
    toast.error("Failed to load today's events");
  } finally {
    loadingEvents.value = false;
  }
}

async function submitClock() {
  if (!nextAction.value) return;
  submitting.value = true;
  try {
    await clockEventService.submit({
      type: CLOCK_EVENT_ENUM[nextAction.value],
      recordedTime: `${selectedTime.value}:00`,
      description: showDescription.value && description.value.trim()
        ? description.value.trim()
        : undefined,
    });
    await loadTodayEvents();
    selectedTime.value = nowTimeStr();
    description.value = "";
    toast.success(`${CLOCK_EVENT_LABELS[nextAction.value!]} recorded`);
  } catch (err: unknown) {
    toast.error(extractApiError(err, "Failed to record clock event"));
  } finally {
    submitting.value = false;
  }
}

async function submitAdjustmentRequest() {
  if (!adjForm.value.reason.trim()) {
    toast.error("Please provide a reason for the adjustment");
    return;
  }
  if (!adjForm.value.clockIn && !adjForm.value.breakStart && !adjForm.value.breakEnd && !adjForm.value.clockOut) {
    toast.error("Please fill in at least one time field");
    return;
  }

  adjSubmitting.value = true;
  try {
    await adjustmentRequestService.create({
      date: adjForm.value.date,
      requestedClockIn: toTimeSpan(adjForm.value.clockIn),
      requestedBreakStart: toTimeSpan(adjForm.value.breakStart),
      requestedBreakEnd: toTimeSpan(adjForm.value.breakEnd),
      requestedClockOut: toTimeSpan(adjForm.value.clockOut),
      reason: adjForm.value.reason.trim(),
    });
    showAdjustDialog.value = false;
    adjForm.value = emptyAdjForm();
    toast.success("Adjustment request sent to admin for approval");
  } catch (err: unknown) {
    toast.error(extractApiError(err, "Failed to submit adjustment request"));
  } finally {
    adjSubmitting.value = false;
  }
}

// ─── Mount ────────────────────────────────────────────────────────────────────

onMounted(loadTodayEvents);
</script>

<template>
  <AuthenticatedLayout>
    <div class="p-6 lg:p-8">
      <div class="max-w-2xl mx-auto">

        <!-- Header -->
        <div class="flex items-center justify-between mb-8">
          <div>
            <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">Time Tracking</h1>
            <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
              {{ new Date().toLocaleDateString(undefined, { weekday: "long", year: "numeric", month: "long", day: "numeric" }) }}
            </p>
          </div>
          <Button variant="outline" size="sm" @click="showAdjustDialog = true">
            <SendIcon class="size-3.5" />
            Request adjustment
          </Button>
        </div>

        <!-- Clock action card -->
        <div class="card p-6 mb-6">
          <div v-if="isDayComplete" class="flex flex-col items-center gap-3 py-4 text-center">
            <CheckCircleIcon class="size-12 text-emerald-500" />
            <p class="text-lg font-medium text-slate-900 dark:text-slate-100">Day complete</p>
            <p class="text-sm text-slate-500 dark:text-slate-400">
              You've clocked out for today. See you tomorrow!
            </p>
          </div>

          <div v-else-if="!nextAction" class="flex flex-col items-center gap-3 py-4 text-center">
            <ClockIcon class="size-12 text-slate-300 dark:text-slate-600" />
            <p class="text-sm text-slate-500 dark:text-slate-400">Loading…</p>
          </div>

          <div v-else class="space-y-5">
            <div class="text-center">
              <p class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">
                Next action
              </p>
              <p class="text-2xl font-bold text-slate-900 dark:text-slate-100">
                {{ CLOCK_EVENT_LABELS[nextAction] }}
              </p>
            </div>

            <!-- Time input -->
            <div class="space-y-1.5">
              <Label>Time</Label>
              <Input
                v-model="selectedTime"
                type="time"
                :min="minTime"
                :max="maxTime"
                class="text-center text-lg font-mono"
              />
              <p class="text-xs text-slate-400 dark:text-slate-500 text-center">
                Adjustable ±5 minutes from now
              </p>
            </div>

            <!-- Description (clock-out only) -->
            <div v-if="showDescription" class="space-y-1.5">
              <Label>
                Description
                <span class="font-normal text-slate-400 ml-1">(optional)</span>
              </Label>
              <textarea
                v-model="description"
                rows="3"
                class="input-field resize-none"
                placeholder="What did you work on today?"
              />
            </div>

            <Button class="w-full" size="lg" :disabled="submitting" @click="submitClock">
              <Loader2Icon v-if="submitting" class="size-4 animate-spin" />
              {{ CLOCK_EVENT_LABELS[nextAction] }}
            </Button>
          </div>
        </div>

        <!-- Today's events table -->
        <div class="card overflow-hidden">
          <div class="px-4 py-3 border-b border-slate-100 dark:border-slate-800">
            <h2 class="text-sm font-semibold text-slate-700 dark:text-slate-300">Today's log</h2>
          </div>

          <div v-if="loadingEvents" class="divide-y divide-slate-100 dark:divide-slate-800">
            <div v-for="i in 2" :key="i" class="flex items-center gap-4 px-4 py-3">
              <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-24 animate-pulse" />
              <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-16 animate-pulse ml-auto" />
            </div>
          </div>

          <Table v-else>
            <TableHeader>
              <TableRow>
                <TableHead>Event</TableHead>
                <TableHead class="text-right">Time</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              <TableEmpty v-if="todayEvents.length === 0" :colspan="2">
                <ClockIcon class="size-8 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
                <p class="text-slate-500 dark:text-slate-400">
                  No events yet today. Use the button above to clock in.
                </p>
              </TableEmpty>
              <TableRow v-for="event in todayEvents" :key="event.id">
                <TableCell class="font-medium text-slate-900 dark:text-slate-100">
                  {{ eventLabel(event.type) }}
                </TableCell>
                <TableCell class="text-right font-mono text-slate-600 dark:text-slate-400">
                  {{ formatTime(event.recordedTime) }}
                </TableCell>
              </TableRow>

              <!-- Placeholder rows for remaining events -->
              <TableRow
                v-for="type in CLOCK_EVENT_TYPE_ORDER.filter(t => !completedTypes.has(t))"
                :key="type"
                class="opacity-30"
              >
                <TableCell class="text-slate-500 dark:text-slate-400 italic">
                  {{ CLOCK_EVENT_LABELS[type] }}
                </TableCell>
                <TableCell class="text-right text-slate-400">—</TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </div>

        <!-- Fraud notice -->
        <p class="mt-4 text-xs text-center text-slate-400 dark:text-slate-500 flex items-center justify-center gap-1">
          <AlertCircleIcon class="size-3.5 shrink-0" />
          Times are server-validated. Adjustments beyond ±5 min require admin approval.
        </p>
      </div>
    </div>

    <!-- Adjustment request dialog -->
    <Dialog v-model:open="showAdjustDialog">
      <DialogContent class="sm:max-w-[480px]">
        <DialogHeader>
          <DialogTitle>Request time adjustment</DialogTitle>
        </DialogHeader>

        <div class="space-y-4 py-1">
          <p class="text-sm text-slate-500 dark:text-slate-400">
            Fill in the times you need corrected and provide a reason. An admin will receive an email and can approve with one click.
          </p>

          <!-- Date -->
          <div class="space-y-1.5">
            <Label>Date</Label>
            <Input v-model="adjForm.date" type="date" />
          </div>

          <!-- Times grid -->
          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <Label class="text-xs text-slate-500">Clock In</Label>
              <Input v-model="adjForm.clockIn" type="time" />
            </div>
            <div class="space-y-1.5">
              <Label class="text-xs text-slate-500">Break Start</Label>
              <Input v-model="adjForm.breakStart" type="time" />
            </div>
            <div class="space-y-1.5">
              <Label class="text-xs text-slate-500">Break End</Label>
              <Input v-model="adjForm.breakEnd" type="time" />
            </div>
            <div class="space-y-1.5">
              <Label class="text-xs text-slate-500">Clock Out</Label>
              <Input v-model="adjForm.clockOut" type="time" />
            </div>
          </div>
          <p class="text-xs text-slate-400">Leave blank any times that don't need changing.</p>

          <!-- Reason -->
          <div class="space-y-1.5">
            <Label>Reason <span class="text-destructive">*</span></Label>
            <textarea
              v-model="adjForm.reason"
              rows="3"
              class="input-field resize-none"
              placeholder="Why couldn't you log your time at the normal time?"
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" @click="showAdjustDialog = false">Cancel</Button>
          <Button :disabled="adjSubmitting" @click="submitAdjustmentRequest">
            <Loader2Icon v-if="adjSubmitting" class="size-4 animate-spin" />
            Send request
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </AuthenticatedLayout>
</template>
