<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from "vue";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import {
  clockEventService,
  CLOCK_EVENT_TYPE_ORDER,
  CLOCK_EVENT_LABELS,
  CLOCK_EVENT_ENUM,
  type ClockEvent,
  type ClockEventType,
  type DaySummary,
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
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { Switch } from "@/components/ui/switch";
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
  CheckCircleIcon,
  CheckIcon,
  SendIcon,
  MinusIcon,
  PlusIcon,
  PencilIcon,
  HomeIcon,
  BuildingIcon,
} from "lucide-vue-next";

const toast = useAppToast();

// ─── State ────────────────────────────────────────────────────────────────────

const todayEvents = ref<ClockEvent[]>([]);
const summaries = ref<DaySummary[]>([]);
const loadingEvents = ref(false);
const loadingSummaries = ref(false);
const submitting = ref(false);

const minuteOffset = ref(0);
const description = ref("");

// Adjustment request dialog
const showAdjustDialog = ref(false);
const adjForm = ref(emptyAdjForm());
const adjSubmitting = ref(false);

// Inline edit state for history tab
const editingDate = ref<string | null>(null);
const editingDescription = ref("");
const savingDate = ref<string | null>(null);

// ─── Computed ─────────────────────────────────────────────────────────────────

const completedTypes = computed<Set<ClockEventType>>(
  () => new Set(todayEvents.value.map((e) => e.type as ClockEventType))
);

const nextAction = computed<ClockEventType | null>(() => {
  return CLOCK_EVENT_TYPE_ORDER.find((t) => !completedTypes.value.has(t)) ?? null;
});

const isDayComplete = computed(() => completedTypes.value.has("ClockOut"));

const now = ref(new Date());
let clockInterval: ReturnType<typeof setInterval> | null = null;

const selectedTime = computed(() => {
  const d = new Date(now.value);
  d.setMinutes(d.getMinutes() + minuteOffset.value);
  return timeToStr(d);
});

const offsetLabel = computed(() => {
  if (minuteOffset.value === 0) return "now";
  return minuteOffset.value > 0 ? `+${minuteOffset.value} min` : `${minuteOffset.value} min`;
});

function adjustMinutes(delta: number) {
  minuteOffset.value = Math.max(-5, Math.min(5, minuteOffset.value + delta));
}

const showDescription = computed(() => nextAction.value === "ClockOut");

// History: exclude today so it doesn't show twice
const historySummaries = computed(() => {
  const todayStr = localDateString(new Date());
  return summaries.value.filter((s) => s.date !== todayStr);
});

// ─── Helpers ──────────────────────────────────────────────────────────────────

function timeToStr(d: Date): string {
  return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
}

function formatTime(t: string | null | undefined): string {
  if (!t) return "—";
  const d = new Date(t);
  return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
}

function formatDate(s: string): string {
  return new Date(s + "T00:00:00").toLocaleDateString(undefined, {
    weekday: "short",
    month: "short",
    day: "numeric",
  });
}

function localDateString(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

function eventLabel(type: string): string {
  return CLOCK_EVENT_LABELS[type as ClockEventType] ?? type;
}

function emptyAdjForm() {
  return {
    date: localDateString(new Date()),
    clockIn: "",
    breakStart: "",
    breakEnd: "",
    clockOut: "",
    reason: "",
  };
}

function toUtcTimeSpan(val: string): string | undefined {
  if (!val) return undefined;
  const [h, m] = val.split(":").map(Number);
  const d = new Date();
  d.setHours(h, m, 0, 0);
  return `${String(d.getUTCHours()).padStart(2, "0")}:${String(d.getUTCMinutes()).padStart(2, "0")}:00`;
}

// ─── Clock actions ────────────────────────────────────────────────────────────

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

async function loadSummaries() {
  try {
    loadingSummaries.value = true;
    summaries.value = await clockEventService.getSummaries();
  } catch {
    toast.error("Failed to load history");
  } finally {
    loadingSummaries.value = false;
  }
}

async function submitClock() {
  if (!nextAction.value) return;
  submitting.value = true;
  try {
    const d = new Date(now.value);
    d.setMinutes(d.getMinutes() + minuteOffset.value);
    d.setSeconds(0);
    d.setMilliseconds(0);
    await clockEventService.submit({
      type: CLOCK_EVENT_ENUM[nextAction.value],
      recordedAt: d.toISOString(),
      localDate: localDateString(d),
      description: showDescription.value && description.value.trim()
        ? description.value.trim()
        : undefined,
    });
    const label = CLOCK_EVENT_LABELS[nextAction.value!];
    await loadTodayEvents();
    await loadSummaries();
    minuteOffset.value = 0;
    description.value = "";
    toast.success(`${label} recorded`);
  } catch (err: unknown) {
    toast.error(extractApiError(err, "Failed to record clock event"));
  } finally {
    submitting.value = false;
  }
}

// ─── History actions ──────────────────────────────────────────────────────────

function startEdit(summary: DaySummary) {
  editingDate.value = summary.date;
  editingDescription.value = summary.description ?? "";
}

function cancelEdit() {
  editingDate.value = null;
  editingDescription.value = "";
}

async function saveDescription(date: string) {
  savingDate.value = date;
  try {
    const updated = await clockEventService.updateDay(date, {
      description: editingDescription.value.trim() || undefined,
    });
    const idx = summaries.value.findIndex((s) => s.date === date);
    if (idx !== -1) summaries.value[idx] = updated;
    editingDate.value = null;
    toast.success("Description saved");
  } catch (err: unknown) {
    toast.error(extractApiError(err, "Failed to save description"));
  } finally {
    savingDate.value = null;
  }
}

async function toggleWfh(summary: DaySummary) {
  savingDate.value = summary.date;
  try {
    const updated = await clockEventService.updateDay(summary.date, {
      workedFromHome: !summary.workedFromHome,
    });
    const idx = summaries.value.findIndex((s) => s.date === summary.date);
    if (idx !== -1) summaries.value[idx] = updated;
  } catch (err: unknown) {
    toast.error(extractApiError(err, "Failed to update work from home status"));
  } finally {
    savingDate.value = null;
  }
}

// ─── Adjustment request ───────────────────────────────────────────────────────

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
      requestedClockIn: toUtcTimeSpan(adjForm.value.clockIn),
      requestedBreakStart: toUtcTimeSpan(adjForm.value.breakStart),
      requestedBreakEnd: toUtcTimeSpan(adjForm.value.breakEnd),
      requestedClockOut: toUtcTimeSpan(adjForm.value.clockOut),
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

onMounted(() => {
  loadTodayEvents();
  loadSummaries();
  clockInterval = setInterval(() => { now.value = new Date(); }, 30_000);
});

onUnmounted(() => {
  if (clockInterval) clearInterval(clockInterval);
});
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

        <Tabs default-value="today">
          <TabsList class="w-full mb-6">
            <TabsTrigger value="today" class="flex-1">Today</TabsTrigger>
            <TabsTrigger value="history" class="flex-1">History</TabsTrigger>
          </TabsList>

          <!-- ── Today tab ───────────────────────────────────────────────── -->
          <TabsContent value="today" class="space-y-6">

            <!-- Clock action card -->
            <div class="card p-6">
              <!-- Loading skeleton -->
              <div v-if="loadingEvents" class="space-y-5">
                <div class="flex items-start justify-between">
                  <div v-for="i in 4" :key="i" class="flex flex-col items-center gap-1.5 flex-1">
                    <div class="size-7 rounded-full bg-slate-200 dark:bg-slate-700 animate-pulse" />
                    <div class="h-2.5 w-10 rounded bg-slate-200 dark:bg-slate-700 animate-pulse" />
                  </div>
                </div>
                <div class="flex items-center justify-center gap-4">
                  <div class="size-9 rounded-lg bg-slate-200 dark:bg-slate-700 animate-pulse" />
                  <div class="h-9 w-28 rounded bg-slate-200 dark:bg-slate-700 animate-pulse" />
                  <div class="size-9 rounded-lg bg-slate-200 dark:bg-slate-700 animate-pulse" />
                </div>
                <div class="h-11 w-full rounded-lg bg-slate-200 dark:bg-slate-700 animate-pulse" />
              </div>

              <div v-else class="space-y-5">
                <!-- Step progress indicator -->
                <div class="flex items-start justify-between">
                  <template v-for="(type, index) in CLOCK_EVENT_TYPE_ORDER" :key="type">
                    <div class="flex flex-col items-center gap-1.5">
                      <div :class="['size-7 rounded-full flex items-center justify-center border-2 transition-all',
                        completedTypes.has(type)
                          ? 'bg-emerald-500 border-emerald-500 text-white'
                          : nextAction === type
                          ? 'border-indigo-600 dark:border-indigo-400 text-indigo-600 dark:text-indigo-400'
                          : 'border-slate-200 dark:border-slate-700 text-slate-400'
                      ]">
                        <CheckIcon v-if="completedTypes.has(type)" class="size-3.5" />
                        <span v-else class="text-xs font-semibold leading-none">{{ index + 1 }}</span>
                      </div>
                      <span class="text-[11px] leading-tight text-center max-w-[52px]" :class="[
                        completedTypes.has(type)
                          ? 'text-emerald-600 dark:text-emerald-400 font-medium'
                          : nextAction === type
                          ? 'text-slate-900 dark:text-slate-100 font-medium'
                          : 'text-slate-400 dark:text-slate-500'
                      ]">{{ CLOCK_EVENT_LABELS[type] }}</span>
                    </div>
                    <div
                      v-if="index < CLOCK_EVENT_TYPE_ORDER.length - 1"
                      class="flex-1 h-px mt-3.5 mx-1 transition-colors"
                      :class="completedTypes.has(type) ? 'bg-emerald-400 dark:bg-emerald-600' : 'bg-slate-200 dark:bg-slate-700'"
                    />
                  </template>
                </div>

                <!-- Day complete -->
                <div v-if="isDayComplete" class="flex flex-col items-center gap-3 py-2 text-center">
                  <CheckCircleIcon class="size-10 text-emerald-500" />
                  <div>
                    <p class="font-medium text-slate-900 dark:text-slate-100">Day complete</p>
                    <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
                      You've clocked out for today. See you tomorrow!
                    </p>
                  </div>
                </div>

                <!-- Active action -->
                <template v-else>
                  <div class="flex items-center justify-center gap-4">
                    <Button variant="outline" size="icon" :disabled="minuteOffset <= -5" @click="adjustMinutes(-1)">
                      <MinusIcon class="size-4" />
                    </Button>
                    <div class="text-center w-28">
                      <p class="text-3xl font-mono font-bold text-slate-900 dark:text-slate-100">
                        {{ selectedTime }}
                      </p>
                      <p class="text-xs text-slate-400 dark:text-slate-500 mt-0.5">{{ offsetLabel }}</p>
                    </div>
                    <Button variant="outline" size="icon" :disabled="minuteOffset >= 5" @click="adjustMinutes(1)">
                      <PlusIcon class="size-4" />
                    </Button>
                  </div>

                  <div v-if="showDescription" class="space-y-1.5">
                    <Label>Description <span class="font-normal text-slate-400 ml-1">(optional)</span></Label>
                    <textarea
                      v-model="description"
                      rows="3"
                      class="input-field resize-none"
                      placeholder="What did you work on today?"
                    />
                  </div>

                  <Button class="w-full" size="lg" :disabled="submitting" @click="submitClock">
                    <Loader2Icon v-if="submitting" class="size-4 animate-spin" />
                    {{ CLOCK_EVENT_LABELS[nextAction!] }}
                  </Button>
                </template>
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
                    <p class="text-slate-500 dark:text-slate-400">No events yet today. Use the button above to clock in.</p>
                  </TableEmpty>
                  <TableRow v-for="event in todayEvents" :key="event.id">
                    <TableCell class="font-medium text-slate-900 dark:text-slate-100">
                      {{ eventLabel(event.type) }}
                    </TableCell>
                    <TableCell class="text-right font-mono text-slate-600 dark:text-slate-400">
                      {{ formatTime(event.recordedAt) }}
                    </TableCell>
                  </TableRow>
                  <TableRow
                    v-for="type in CLOCK_EVENT_TYPE_ORDER.filter(t => !completedTypes.has(t))"
                    :key="type"
                    class="opacity-30"
                  >
                    <TableCell class="text-slate-500 dark:text-slate-400 italic">{{ CLOCK_EVENT_LABELS[type] }}</TableCell>
                    <TableCell class="text-right text-slate-400">—</TableCell>
                  </TableRow>
                </TableBody>
              </Table>
            </div>

          </TabsContent>

          <!-- ── History tab ─────────────────────────────────────────────── -->
          <TabsContent value="history">
            <div class="card overflow-hidden">

              <!-- Loading skeleton -->
              <div v-if="loadingSummaries" class="divide-y divide-slate-100 dark:divide-slate-800">
                <div v-for="i in 5" :key="i" class="flex items-center gap-4 px-4 py-3.5">
                  <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-24 animate-pulse" />
                  <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-16 animate-pulse" />
                  <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-20 animate-pulse" />
                  <div class="ml-auto h-5 bg-slate-200 dark:bg-slate-700 rounded w-12 animate-pulse" />
                </div>
              </div>

              <Table v-else>
                <TableHeader>
                  <TableRow>
                    <TableHead>Date</TableHead>
                    <TableHead>Hours</TableHead>
                    <TableHead>In → Out</TableHead>
                    <TableHead class="text-center">WFH</TableHead>
                    <TableHead>Description</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  <TableEmpty v-if="historySummaries.length === 0" :colspan="5">
                    <ClockIcon class="size-8 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
                    <p class="text-slate-500 dark:text-slate-400">No history yet.</p>
                  </TableEmpty>

                  <TableRow v-for="s in historySummaries" :key="s.date">
                    <!-- Date -->
                    <TableCell class="font-medium text-slate-900 dark:text-slate-100 whitespace-nowrap">
                      {{ formatDate(s.date) }}
                    </TableCell>

                    <!-- Hours badge -->
                    <TableCell>
                      <span
                        v-if="s.totalHours > 0"
                        class="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-semibold bg-indigo-50 dark:bg-indigo-950 text-indigo-700 dark:text-indigo-300"
                      >
                        {{ s.totalHours.toFixed(2) }}h
                      </span>
                      <span v-else class="text-slate-400 text-xs">—</span>
                    </TableCell>

                    <!-- Clock in → out -->
                    <TableCell class="font-mono text-xs text-slate-600 dark:text-slate-400 whitespace-nowrap">
                      {{ formatTime(s.clockIn) }} → {{ formatTime(s.clockOut) }}
                    </TableCell>

                    <!-- WFH toggle -->
                    <TableCell class="text-center">
                      <div class="flex items-center justify-center gap-1.5">
                        <Switch
                          :checked="s.workedFromHome"
                          :disabled="savingDate === s.date"
                          @update:checked="toggleWfh(s)"
                        />
                        <component
                          :is="s.workedFromHome ? HomeIcon : BuildingIcon"
                          class="size-3.5"
                          :class="s.workedFromHome ? 'text-indigo-500' : 'text-slate-400'"
                        />
                      </div>
                    </TableCell>

                    <!-- Description (inline edit) -->
                    <TableCell class="max-w-[200px]">
                      <div v-if="editingDate === s.date" class="flex items-start gap-1.5">
                        <textarea
                          v-model="editingDescription"
                          rows="2"
                          class="input-field resize-none text-xs py-1 px-2 flex-1"
                          placeholder="Add a description…"
                          @keydown.enter.exact.prevent="saveDescription(s.date)"
                          @keydown.escape="cancelEdit"
                        />
                        <div class="flex flex-col gap-1">
                          <Button
                            size="icon"
                            class="size-6"
                            :disabled="savingDate === s.date"
                            @click="saveDescription(s.date)"
                          >
                            <Loader2Icon v-if="savingDate === s.date" class="size-3 animate-spin" />
                            <CheckIcon v-else class="size-3" />
                          </Button>
                          <Button size="icon" variant="ghost" class="size-6" @click="cancelEdit">
                            <MinusIcon class="size-3" />
                          </Button>
                        </div>
                      </div>
                      <button
                        v-else
                        class="group flex items-center gap-1.5 text-left w-full"
                        @click="s.clockOut ? startEdit(s) : undefined"
                        :disabled="!s.clockOut"
                      >
                        <span
                          class="text-xs text-slate-600 dark:text-slate-400 truncate"
                          :title="s.description ?? undefined"
                        >
                          {{ s.description || (s.clockOut ? "Add description…" : "—") }}
                        </span>
                        <PencilIcon
                          v-if="s.clockOut"
                          class="size-3 text-slate-300 dark:text-slate-600 group-hover:text-slate-500 shrink-0"
                        />
                      </button>
                    </TableCell>
                  </TableRow>
                </TableBody>
              </Table>
            </div>
          </TabsContent>
        </Tabs>

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

          <div class="space-y-1.5">
            <Label>Date</Label>
            <Input v-model="adjForm.date" type="date" />
          </div>

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
