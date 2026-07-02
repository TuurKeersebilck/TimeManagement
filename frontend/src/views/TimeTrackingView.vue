<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from "vue";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import {
  workSessionService,
  type TodayStatusDto,
  type WorkDaySummaryDto,
  type WorkScheduleDto,
  type OvertimeResultDto,
  type WorkSessionDto,
} from "@/services/workSessionService";
import { adjustmentRequestService } from "@/services/adjustmentRequestService";
import { vacationService, type VacationDay } from "@/services/vacationService";
import { holidayService, type PublicHoliday } from "@/services/holidayService";
import { useClockEventsStore } from "@/composables/useClockEventsStore";
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
  CoffeeIcon,
  PlaneIcon,
  SunIcon,
  TimerIcon,
  StarIcon,
  TrendingUpIcon,
  TrendingDownIcon,
  ScaleIcon,
} from "lucide-vue-next";

const toast = useAppToast();
const { clearCache: clearSummariesCache } = useClockEventsStore();

// ─── State ────────────────────────────────────────────────────────────────────

const schedule = ref<WorkScheduleDto | null>(null);
const today = ref<TodayStatusDto | null>(null);
const summaries = ref<WorkDaySummaryDto[]>([]);
const overtime = ref<OvertimeResultDto | null>(null);
const loadingToday = ref(false);
const loadingSummaries = ref(false);
const acting = ref<"clockIn" | "clockOut" | "startBreak" | "endBreak" | null>(null);

const minuteOffset = ref(0);
const description = ref("");
const wfh = ref(false);
const todayVacation = ref<VacationDay | null>(null);
const holidays = ref<PublicHoliday[]>([]);

// Adjustment request dialog
const showAdjustDialog = ref(false);
const adjForm = ref(emptyAdjForm());
const adjSubmitting = ref(false);

// Inline edit state for history tab
const editingDate = ref<string | null>(null);
const editingDescription = ref("");
const savingDate = ref<string | null>(null);

// Live clock
const now = ref(new Date());
let clockInterval: ReturnType<typeof setInterval> | null = null;

// ─── Derived clock state ──────────────────────────────────────────────────────

const isClockedIn = computed(() => today.value?.openSession != null);
const isOnBreak = computed(() => {
  const open = today.value?.openSession;
  return open != null && open.breaks.some((b) => b.breakEnd == null);
});
const openBreak = computed(() =>
  today.value?.openSession?.breaks.find((b) => b.breakEnd == null) ?? null
);

const todayWorkedSeconds = computed(() => {
  const status = today.value;
  if (!status) return 0;
  let total = 0;

  // Add up all closed sessions
  for (const s of status.closedSessions) {
    if (!s.clockOut) continue;
    const raw =
      (new Date(s.clockOut).getTime() - new Date(s.clockIn).getTime()) / 1000;
    const breakSecs = s.breaks
      .filter((b) => b.breakEnd)
      .reduce(
        (sum, b) =>
          sum + (new Date(b.breakEnd!).getTime() - new Date(b.breakStart).getTime()) / 1000,
        0
      );
    total += raw - breakSecs;
  }

  // Add open session (live, excluding ongoing break)
  const open = status.openSession;
  if (open) {
    const raw =
      (now.value.getTime() - new Date(open.clockIn).getTime()) / 1000;
    const closedBreakSecs = open.breaks
      .filter((b) => b.breakEnd)
      .reduce(
        (sum, b) =>
          sum + (new Date(b.breakEnd!).getTime() - new Date(b.breakStart).getTime()) / 1000,
        0
      );
    // Ongoing break time is excluded
    const ongoingBreakSecs = openBreak.value
      ? (now.value.getTime() - new Date(openBreak.value.breakStart).getTime()) / 1000
      : 0;
    total += Math.max(0, raw - closedBreakSecs - ongoingBreakSecs);
  }

  return total;
});

const todayTargetHours = computed<number>(() => {
  if (!schedule.value) return 0;
  const dow = new Date().getDay(); // 0=Sun 6=Sat
  return schedule.value.workdayTargets.find((t) => t.dayOfWeek === dow)?.hours ?? 0;
});

const todayFlexSeconds = computed(() => todayWorkedSeconds.value - todayTargetHours.value * 3600);

const monthlyFlexHours = computed(() => overtime.value?.runningBalanceHours ?? null);

// Minimum break enforcement
const breakElapsedSeconds = computed<number>(() => {
  if (!isOnBreak.value || !openBreak.value) return 0;
  return Math.max(0, (now.value.getTime() - new Date(openBreak.value.breakStart).getTime()) / 1_000);
});

const breakRemainingSeconds = computed<number | null>(() => {
  const minimum = schedule.value?.minimumBreakMinutes;
  if (!minimum || !isOnBreak.value) return null;
  const remaining = minimum * 60 - breakElapsedSeconds.value;
  return remaining > 0 ? remaining : 0;
});

const breakMinimumReached = computed(
  () => breakRemainingSeconds.value === null || breakRemainingSeconds.value <= 0
);

function formatMmSs(totalSeconds: number): string {
  const s = Math.ceil(totalSeconds);
  const m = Math.floor(s / 60);
  const sec = s % 60;
  return `${m}:${String(sec).padStart(2, "0")}`;
}

function formatElapsedBreak(totalSeconds: number): string {
  const s = Math.floor(totalSeconds);
  const m = Math.floor(s / 60);
  const sec = s % 60;
  return `${String(m).padStart(2, "0")}:${String(sec).padStart(2, "0")}`;
}

// Half-day vacation disables breaks
const isHalfDayVacation = computed(() => todayVacation.value?.amount === 0.5);
const isFullDayVacation = computed(() => todayVacation.value?.amount === 1.0);

const canClockIn = computed(() => !isClockedIn.value && !isFullDayVacation.value);
const canClockOut = computed(() => isClockedIn.value && !isOnBreak.value);
const canStartBreak = computed(() => isClockedIn.value && !isOnBreak.value && !isHalfDayVacation.value);
const canEndBreak = computed(() => isOnBreak.value && breakMinimumReached.value);

// ─── History helpers ──────────────────────────────────────────────────────────

type HistoryRow =
  | { kind: "summary"; data: WorkDaySummaryDto }
  | { kind: "holiday"; data: PublicHoliday };

const mergedHistory = computed<HistoryRow[]>(() => {
  const todayStr = localDateString(new Date());
  const summaryDates = new Set(summaries.value.map((s) => s.date));

  const rows: HistoryRow[] = summaries.value.map((s) => ({
    kind: "summary" as const,
    data: s,
  }));

  for (const h of holidays.value) {
    if (!h.isWorkingDay && h.date <= todayStr && !summaryDates.has(h.date)) {
      rows.push({ kind: "holiday" as const, data: h });
    }
  }

  return rows.sort((a, b) => b.data.date.localeCompare(a.data.date));
});

function flexDeltaForDay(s: WorkDaySummaryDto): number | null {
  if (!overtime.value) return null;
  const dayEntry = overtime.value.perDay.find((p) => p.date === s.date);
  return dayEntry ? dayEntry.flexDelta : null;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

function localDateString(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

function formatUtc(t: string | null | undefined): string {
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

function formatSeconds(secs: number): string {
  const abs = Math.abs(secs);
  const h = Math.floor(abs / 3600);
  const m = Math.floor((abs % 3600) / 60);
  const sign = secs < 0 ? "-" : "+";
  return `${sign}${h}h${m.toString().padStart(2, "0")}m`;
}

function formatHours(h: number): string {
  const abs = Math.abs(h);
  const hrs = Math.floor(abs);
  const min = Math.round((abs - hrs) * 60);
  const sign = h < 0 ? "-" : "+";
  return `${sign}${hrs}h${min.toString().padStart(2, "0")}m`;
}

function sessionTimeline(session: WorkSessionDto): string {
  const ci = formatUtc(session.clockIn);
  const co = session.clockOut ? formatUtc(session.clockOut) : "…";
  const breaks = session.breaks
    .filter((b) => b.breakEnd)
    .map((b) => ` ☕ ${formatUtc(b.breakStart)}–${formatUtc(b.breakEnd)}`)
    .join("");
  return `${ci} → ${co}${breaks}`;
}

const selectedTime = computed(() => {
  const d = new Date(now.value);
  d.setMinutes(d.getMinutes() + minuteOffset.value);
  return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
});

const offsetLabel = computed(() => {
  if (minuteOffset.value === 0) return "now";
  return minuteOffset.value > 0 ? `+${minuteOffset.value} min` : `${minuteOffset.value} min`;
});

function adjustMinutes(delta: number) {
  minuteOffset.value = Math.max(-5, Math.min(5, minuteOffset.value + delta));
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

function toLocalIso(date: string, time: string): string {
  const dt = new Date(`${date}T${time}:00`);
  const offsetMin = -dt.getTimezoneOffset();
  const sign = offsetMin >= 0 ? "+" : "-";
  const abs = Math.abs(offsetMin);
  const oh = String(Math.floor(abs / 60)).padStart(2, "0");
  const om = String(abs % 60).padStart(2, "0");
  const [y, mo, d, h, m] = [
    dt.getFullYear(),
    String(dt.getMonth() + 1).padStart(2, "0"),
    String(dt.getDate()).padStart(2, "0"),
    String(dt.getHours()).padStart(2, "0"),
    String(dt.getMinutes()).padStart(2, "0"),
  ];
  return `${y}-${mo}-${d}T${h}:${m}:00${sign}${oh}:${om}`;
}

// ─── Clock actions ────────────────────────────────────────────────────────────

async function handleClockIn() {
  acting.value = "clockIn";
  try {
    await workSessionService.clockIn(minuteOffset.value, wfh.value);
    await refreshAll();
    minuteOffset.value = 0;
    wfh.value = false;
    toast.success("Clocked in");
  } catch (err) {
    toast.error(extractApiError(err, "Failed to clock in"));
  } finally {
    acting.value = null;
  }
}

async function handleClockOut() {
  acting.value = "clockOut";
  try {
    await workSessionService.clockOut(minuteOffset.value, description.value);
    await refreshAll();
    minuteOffset.value = 0;
    description.value = "";
    toast.success("Clocked out");
  } catch (err) {
    toast.error(extractApiError(err, "Failed to clock out"));
  } finally {
    acting.value = null;
  }
}

async function handleStartBreak() {
  acting.value = "startBreak";
  try {
    await workSessionService.startBreak();
    await refreshToday();
    toast.success("Break started");
  } catch (err) {
    toast.error(extractApiError(err, "Failed to start break"));
  } finally {
    acting.value = null;
  }
}

async function handleEndBreak() {
  acting.value = "endBreak";
  try {
    await workSessionService.endBreak(minuteOffset.value);
    await refreshToday();
    minuteOffset.value = 0;
    toast.success("Break ended");
  } catch (err: unknown) {
    // Structured BREAK_TOO_SHORT error
    const anyErr = err as Record<string, unknown>;
    const data =
      (anyErr?.response as Record<string, unknown>)?.data as Record<string, unknown> | undefined;
    if (data?.code === "BREAK_TOO_SHORT") {
      toast.error(
        `Break too short — ${data.elapsedMinutes} / ${data.requiredMinutes} min elapsed`
      );
    } else {
      toast.error(extractApiError(err, "Failed to end break"));
    }
  } finally {
    acting.value = null;
  }
}

// ─── Data loading ─────────────────────────────────────────────────────────────

async function refreshToday() {
  loadingToday.value = true;
  try {
    today.value = await workSessionService.getToday();
  } catch {
    toast.error("Failed to load today's status");
  } finally {
    loadingToday.value = false;
  }
}

async function refreshAll() {
  await Promise.allSettled([
    refreshToday(),
    (async () => {
      loadingSummaries.value = true;
      try {
        [summaries.value, overtime.value] = await Promise.all([
          workSessionService.getSummaries(),
          workSessionService.getOvertime(),
        ]);
        clearSummariesCache();
      } catch {
        // Summaries are non-critical
      } finally {
        loadingSummaries.value = false;
      }
    })(),
  ]);
}

async function loadTodayVacation() {
  try {
    todayVacation.value = await vacationService.getVacationForDate(localDateString(new Date()));
  } catch {
    // Non-critical
  }
}

async function loadHolidays() {
  try {
    holidays.value = await holidayService.getHolidays(new Date().getFullYear());
  } catch {
    // Non-critical
  }
}

// ─── History actions ──────────────────────────────────────────────────────────

function startEdit(summary: WorkDaySummaryDto) {
  editingDate.value = summary.date;
  editingDescription.value = summary.workDay?.description ?? "";
}

function cancelEdit() {
  editingDate.value = null;
  editingDescription.value = "";
}

async function saveDescription(date: string) {
  savingDate.value = date;
  try {
    await workSessionService.updateDay(date, {
      description: editingDescription.value.trim() || undefined,
    });
    // Refresh summaries to reflect update
    summaries.value = await workSessionService.getSummaries();
    editingDate.value = null;
    toast.success("Description saved");
  } catch (err) {
    toast.error(extractApiError(err, "Failed to save description"));
  } finally {
    savingDate.value = null;
  }
}

async function toggleWfh(summary: WorkDaySummaryDto) {
  savingDate.value = summary.date;
  try {
    await workSessionService.updateDay(summary.date, {
      workedFromHome: !(summary.workDay?.workedFromHome ?? false),
    });
    summaries.value = await workSessionService.getSummaries();
    toast.success(
      !(summary.workDay?.workedFromHome ?? false)
        ? "Marked as working from home"
        : "Marked as office"
    );
  } catch (err) {
    toast.error(extractApiError(err, "Failed to update WFH status"));
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
  if (!adjForm.value.clockIn || !adjForm.value.clockOut) {
    toast.error("Clock-in and clock-out times are required");
    return;
  }

  const breaks = [];
  if (adjForm.value.breakStart && adjForm.value.breakEnd) {
    breaks.push({
      breakStart: toLocalIso(adjForm.value.date, adjForm.value.breakStart),
      breakEnd: toLocalIso(adjForm.value.date, adjForm.value.breakEnd),
    });
  }

  adjSubmitting.value = true;
  try {
    await adjustmentRequestService.create({
      date: adjForm.value.date,
      desiredDaySnapshot: {
        sessions: [
          {
            clockIn: toLocalIso(adjForm.value.date, adjForm.value.clockIn),
            clockOut: toLocalIso(adjForm.value.date, adjForm.value.clockOut),
            breaks,
          },
        ],
      },
      reason: adjForm.value.reason.trim(),
    });
    showAdjustDialog.value = false;
    adjForm.value = emptyAdjForm();
    toast.success("Adjustment request sent to admin for approval");
  } catch (err) {
    toast.error(extractApiError(err, "Failed to submit adjustment request"));
  } finally {
    adjSubmitting.value = false;
  }
}

// ─── Visibility / mount ───────────────────────────────────────────────────────

function handleVisibilityChange() {
  if (!document.hidden) {
    now.value = new Date();
    refreshToday();
  }
}

onMounted(async () => {
  clockInterval = setInterval(() => { now.value = new Date(); }, 1_000);
  document.addEventListener("visibilitychange", handleVisibilityChange);

  await Promise.allSettled([
    refreshToday(),
    (async () => {
      try {
        schedule.value = await workSessionService.getMySchedule();
      } catch {}
    })(),
    (async () => {
      loadingSummaries.value = true;
      try {
        [summaries.value, overtime.value] = await Promise.all([
          workSessionService.getSummaries(),
          workSessionService.getOvertime(),
        ]);
      } catch {} finally {
        loadingSummaries.value = false;
      }
    })(),
    loadTodayVacation(),
    loadHolidays(),
  ]);
});

onUnmounted(() => {
  if (clockInterval) clearInterval(clockInterval);
  document.removeEventListener("visibilitychange", handleVisibilityChange);
});
</script>

<template>
  <AuthenticatedLayout>
    <div class="p-6 lg:p-8">
      <div class="max-w-4xl mx-auto">

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

            <!-- Full vacation -->
            <div
              v-if="isFullDayVacation"
              class="card flex flex-col items-center gap-3 py-8 text-center"
            >
              <div class="size-10 rounded-full bg-violet-100 dark:bg-violet-900/40 flex items-center justify-center">
                <PlaneIcon class="size-5 text-violet-500" />
              </div>
              <div>
                <p class="font-medium text-slate-900 dark:text-slate-100">Full vacation day</p>
                <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
                  {{ todayVacation?.vacationTypeName }} vacation today — enjoy!
                </p>
              </div>
            </div>

            <template v-else>
              <!-- Half-day banner -->
              <div
                v-if="isHalfDayVacation"
                class="flex items-center gap-3 rounded-lg border border-violet-200 dark:border-violet-800 bg-violet-50 dark:bg-violet-950/30 px-4 py-3"
              >
                <SunIcon class="size-4 text-violet-500 shrink-0" />
                <p class="text-sm text-violet-800 dark:text-violet-200">
                  Half-day {{ todayVacation?.vacationTypeName }} vacation — clock in/out only, no break.
                </p>
              </div>

              <!-- Clock action card -->
              <div class="card p-6 space-y-5">
                <!-- Live elapsed -->
                <div class="flex items-center justify-between">
                  <div>
                    <p class="text-xs font-medium uppercase tracking-wide text-slate-400 dark:text-slate-500">
                      Today
                    </p>
                    <p class="text-4xl font-mono font-bold text-slate-900 dark:text-slate-100 tabular-nums">
                      {{ (() => {
                        const s = Math.floor(todayWorkedSeconds);
                        const h = Math.floor(s / 3600);
                        const m = Math.floor((s % 3600) / 60);
                        const sec = s % 60;
                        return `${String(h).padStart(2, "0")}:${String(m).padStart(2, "0")}:${String(sec).padStart(2, "0")}`;
                      })() }}
                    </p>
                  </div>
                  <!-- Daily flex -->
                  <div class="text-right">
                    <p class="text-xs font-medium uppercase tracking-wide text-slate-400 dark:text-slate-500">
                      Today's Δ
                    </p>
                    <p
                      class="text-xl font-mono font-semibold tabular-nums"
                      :class="todayFlexSeconds >= 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400'"
                    >
                      {{ formatSeconds(Math.floor(todayFlexSeconds)) }}
                    </p>
                    <p class="text-xs text-slate-400 mt-0.5">vs {{ todayTargetHours }}h target</p>
                  </div>
                </div>

                <!-- Monthly flex balance -->
                <div
                  v-if="monthlyFlexHours !== null"
                  class="flex items-center gap-2 rounded-lg px-4 py-2.5 text-sm"
                  :class="monthlyFlexHours >= 0
                    ? 'bg-emerald-50 dark:bg-emerald-950/40'
                    : 'bg-rose-50 dark:bg-rose-950/40'"
                >
                  <component
                    :is="monthlyFlexHours >= 0 ? TrendingUpIcon : TrendingDownIcon"
                    class="size-4 shrink-0"
                    :class="monthlyFlexHours >= 0 ? 'text-emerald-500' : 'text-rose-500'"
                  />
                  <span class="text-slate-600 dark:text-slate-400">Monthly balance:</span>
                  <span
                    class="font-semibold font-mono"
                    :class="monthlyFlexHours >= 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400'"
                  >
                    {{ formatHours(monthlyFlexHours) }}
                  </span>
                  <span v-if="monthlyFlexHours < 0" class="text-slate-500 text-xs ml-1">below target</span>
                </div>

                <!-- On-break status / countdown -->
                <div
                  v-if="isOnBreak"
                  class="flex items-center justify-center gap-2 rounded-lg px-4 py-2.5 text-sm"
                  :class="breakMinimumReached
                    ? 'bg-emerald-50 dark:bg-emerald-950/40'
                    : 'bg-amber-50 dark:bg-amber-950/40'"
                >
                  <CoffeeIcon
                    class="size-4 shrink-0"
                    :class="breakMinimumReached ? 'text-emerald-500' : 'text-amber-500'"
                  />
                  <template v-if="!breakMinimumReached && breakRemainingSeconds !== null">
                    <span class="text-slate-600 dark:text-slate-400">End Break available in</span>
                    <span class="font-semibold font-mono text-amber-600 dark:text-amber-400">
                      {{ formatMmSs(breakRemainingSeconds) }}
                    </span>
                  </template>
                  <template v-else>
                    <span class="text-slate-600 dark:text-slate-400">On break —</span>
                    <span class="font-semibold font-mono text-emerald-600 dark:text-emerald-400">
                      {{ formatElapsedBreak(breakElapsedSeconds) }}
                    </span>
                  </template>
                </div>

                <!-- Time offset adjuster (only when an action is available) -->
                <div
                  v-if="canClockIn || canClockOut || canEndBreak"
                  class="flex items-center justify-center gap-4"
                >
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

                <!-- WFH toggle (only when clock-in is available) -->
                <div v-if="canClockIn" class="flex items-center gap-3">
                  <Switch v-model="wfh" />
                  <Label>Working from home today</Label>
                </div>

                <!-- Description (only when clocked in and break not active) -->
                <div v-if="canClockOut" class="space-y-1.5">
                  <Label>Description <span class="font-normal text-slate-400 ml-1">(optional)</span></Label>
                  <textarea
                    v-model="description"
                    rows="3"
                    class="input-field resize-none"
                    placeholder="What did you work on today?"
                  />
                </div>

                <!-- Action buttons -->
                <div class="grid gap-2" :class="isClockedIn ? 'grid-cols-2' : 'grid-cols-1'">
                  <Button
                    v-if="canClockIn"
                    class="w-full"
                    size="lg"
                    :disabled="acting != null"
                    @click="handleClockIn"
                  >
                    <Loader2Icon v-if="acting === 'clockIn'" class="size-4 animate-spin" />
                    <ClockIcon v-else class="size-4" />
                    Clock In
                  </Button>

                  <template v-if="isClockedIn">
                    <Button
                      v-if="isOnBreak"
                      class="col-span-2 w-full"
                      size="lg"
                      variant="secondary"
                      :disabled="acting != null || !breakMinimumReached"
                      @click="handleEndBreak"
                    >
                      <Loader2Icon v-if="acting === 'endBreak'" class="size-4 animate-spin" />
                      <CoffeeIcon v-else class="size-4" />
                      End Break
                    </Button>

                    <template v-else>
                      <Button
                        variant="secondary"
                        size="lg"
                        :disabled="acting != null || !canStartBreak"
                        @click="handleStartBreak"
                      >
                        <Loader2Icon v-if="acting === 'startBreak'" class="size-4 animate-spin" />
                        <CoffeeIcon v-else class="size-4" />
                        Start Break
                      </Button>

                      <Button
                        size="lg"
                        :disabled="acting != null || !canClockOut"
                        @click="handleClockOut"
                      >
                        <Loader2Icon v-if="acting === 'clockOut'" class="size-4 animate-spin" />
                        <CheckIcon v-else class="size-4" />
                        Clock Out
                      </Button>
                    </template>
                  </template>
                </div>
              </div>

              <!-- Today's sessions -->
              <div class="card overflow-hidden">
                <div class="px-4 py-3 border-b border-slate-100 dark:border-slate-800">
                  <h2 class="text-sm font-semibold text-slate-700 dark:text-slate-300">Today's sessions</h2>
                </div>

                <div v-if="loadingToday" class="divide-y divide-slate-100 dark:divide-slate-800">
                  <div v-for="i in 2" :key="i" class="flex items-center gap-4 px-4 py-3">
                    <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-32 animate-pulse" />
                    <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-20 animate-pulse ml-auto" />
                  </div>
                </div>

                <div v-else>
                  <div
                    v-if="!today || (today.closedSessions.length === 0 && !today.openSession)"
                    class="flex flex-col items-center gap-2 py-8 text-center"
                  >
                    <ClockIcon class="size-8 text-slate-300 dark:text-slate-600" />
                    <p class="text-sm text-slate-500 dark:text-slate-400">
                      No sessions yet. Use the button above to clock in.
                    </p>
                  </div>

                  <div v-else class="divide-y divide-slate-100 dark:divide-slate-800">
                    <!-- Open session -->
                    <div
                      v-if="today?.openSession"
                      class="flex items-center gap-3 px-4 py-3"
                    >
                      <span class="inline-flex size-2 rounded-full bg-emerald-500 animate-pulse" />
                      <span class="text-sm font-mono text-slate-700 dark:text-slate-300 flex-1">
                        {{ sessionTimeline(today.openSession) }}
                      </span>
                      <span
                        class="text-xs px-2 py-0.5 rounded-full font-medium"
                        :class="isOnBreak
                          ? 'bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-300'
                          : 'bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-300'"
                      >
                        {{ isOnBreak ? 'On break' : 'Active' }}
                      </span>
                    </div>

                    <!-- Closed sessions -->
                    <div
                      v-for="session in today?.closedSessions"
                      :key="session.id"
                      class="flex items-center gap-3 px-4 py-3"
                    >
                      <span class="inline-flex size-2 rounded-full bg-slate-300 dark:bg-slate-600" />
                      <span class="text-sm font-mono text-slate-700 dark:text-slate-300 flex-1">
                        {{ sessionTimeline(session) }}
                      </span>
                      <span class="text-xs px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-500 font-medium">
                        Closed
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            </template>
          </TabsContent>

          <!-- ── History tab ─────────────────────────────────────────────── -->
          <TabsContent value="history">
            <div class="card overflow-hidden">
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
                    <TableHead>Δ</TableHead>
                    <TableHead>Sessions</TableHead>
                    <TableHead class="text-center">WFH</TableHead>
                    <TableHead class="text-center">Vacation</TableHead>
                    <TableHead>Description</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  <TableEmpty v-if="mergedHistory.length === 0" :colspan="7">
                    <ClockIcon class="size-8 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
                    <p class="text-slate-500 dark:text-slate-400">No clocked days yet.</p>
                  </TableEmpty>

                  <template v-else v-for="row in mergedHistory" :key="row.data.date + '-' + row.kind">
                    <!-- Holiday row -->
                    <TableRow
                      v-if="row.kind === 'holiday'"
                      class="bg-sky-50/40 dark:bg-sky-950/20 hover:bg-sky-50/70 dark:hover:bg-sky-950/30"
                    >
                      <TableCell class="font-medium text-slate-900 dark:text-slate-100 whitespace-nowrap">
                        {{ formatDate(row.data.date) }}
                      </TableCell>
                      <TableCell :colspan="6">
                        <div class="flex items-center gap-2">
                          <StarIcon class="size-3.5 text-sky-500 shrink-0" />
                          <span class="text-sm text-slate-600 dark:text-slate-400">{{ row.data.name }}</span>
                          <span class="text-xs text-slate-400">Public holiday</span>
                        </div>
                      </TableCell>
                    </TableRow>

                    <!-- Summary row -->
                    <TableRow v-else>
                      <!-- Date -->
                      <TableCell class="font-medium text-slate-900 dark:text-slate-100 whitespace-nowrap">
                        {{ formatDate(row.data.date) }}
                        <span
                          v-if="row.data.date === localDateString(new Date())"
                          class="ml-1.5 inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-semibold bg-indigo-100 dark:bg-indigo-900 text-indigo-600 dark:text-indigo-300"
                        >Today</span>
                      </TableCell>

                      <!-- Hours -->
                      <TableCell>
                        <span
                          v-if="row.data.totalWorkedHours > 0"
                          class="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-semibold font-mono bg-indigo-50 dark:bg-indigo-950 text-indigo-700 dark:text-indigo-300"
                        >
                          {{ row.data.totalWorkedHours.toFixed(2) }}h
                        </span>
                        <span v-else class="text-slate-400 text-xs">—</span>
                      </TableCell>

                      <!-- Flex delta -->
                      <TableCell>
                        <template v-if="flexDeltaForDay(row.data) !== null">
                          <span
                            class="inline-flex items-center gap-1 px-2 py-0.5 rounded-md text-xs font-semibold font-mono"
                            :class="flexDeltaForDay(row.data)! >= 0
                              ? 'bg-emerald-50 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-300'
                              : 'bg-rose-50 dark:bg-rose-950 text-rose-700 dark:text-rose-300'"
                          >
                            <component
                              :is="flexDeltaForDay(row.data)! >= 0 ? TrendingUpIcon : TrendingDownIcon"
                              class="size-3"
                            />
                            {{ formatHours(flexDeltaForDay(row.data)!) }}
                          </span>
                        </template>
                        <span v-else class="text-slate-400 text-xs">—</span>
                      </TableCell>

                      <!-- Sessions timeline -->
                      <TableCell class="font-mono text-xs text-slate-600 dark:text-slate-400 whitespace-nowrap max-w-[220px] truncate">
                        <div v-for="s in row.data.sessions.filter(s => s.status === 'Closed')" :key="s.id" class="truncate">
                          {{ sessionTimeline(s) }}
                        </div>
                        <span v-if="row.data.sessions.filter(s => s.status === 'Closed').length === 0" class="text-slate-400">—</span>
                      </TableCell>

                      <!-- WFH -->
                      <TableCell class="text-center">
                        <div class="flex items-center justify-center gap-1.5">
                          <Switch
                            :model-value="row.data.workDay?.workedFromHome ?? false"
                            :disabled="savingDate === row.data.date"
                            @update:model-value="toggleWfh(row.data)"
                          />
                          <component
                            :is="(row.data.workDay?.workedFromHome ?? false) ? HomeIcon : BuildingIcon"
                            class="size-3.5"
                            :class="(row.data.workDay?.workedFromHome ?? false) ? 'text-indigo-500' : 'text-slate-400'"
                          />
                        </div>
                      </TableCell>

                      <!-- Vacation -->
                      <TableCell class="text-center">
                        <span
                          v-if="row.data.vacationAmount === 1.0"
                          class="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium bg-violet-50 dark:bg-violet-950 text-violet-700 dark:text-violet-300"
                          :title="row.data.vacationTypeName ?? undefined"
                        >Full</span>
                        <span
                          v-else-if="row.data.vacationAmount === 0.5"
                          class="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium bg-violet-50 dark:bg-violet-950 text-violet-700 dark:text-violet-300"
                          :title="row.data.vacationTypeName ?? undefined"
                        >Half</span>
                        <span v-else class="text-slate-400 text-xs">—</span>
                      </TableCell>

                      <!-- Description -->
                      <TableCell class="max-w-[200px]">
                        <div v-if="editingDate === row.data.date" class="flex items-start gap-1.5">
                          <textarea
                            v-model="editingDescription"
                            rows="2"
                            class="input-field resize-none text-xs py-1 px-2 flex-1"
                            placeholder="Add a description…"
                            @keydown.enter.exact.prevent="saveDescription(row.data.date)"
                            @keydown.escape="cancelEdit"
                          />
                          <div class="flex flex-col gap-1">
                            <Button
                              size="icon"
                              class="size-6"
                              :disabled="savingDate === row.data.date"
                              @click="saveDescription(row.data.date)"
                            >
                              <Loader2Icon v-if="savingDate === row.data.date" class="size-3 animate-spin" />
                              <CheckIcon v-else class="size-3" />
                            </Button>
                            <Button size="icon" variant="ghost" class="size-6" @click="cancelEdit">
                              <MinusIcon class="size-3" />
                            </Button>
                          </div>
                        </div>
                        <button
                          v-else
                          class="group flex cursor-pointer items-center gap-1.5 text-left w-full disabled:cursor-not-allowed"
                          :disabled="row.data.sessions.every(s => s.status !== 'Closed')"
                          @click="row.data.sessions.some(s => s.status === 'Closed') ? startEdit(row.data) : undefined"
                        >
                          <span class="text-xs text-slate-600 dark:text-slate-400 truncate" :title="row.data.workDay?.description ?? undefined">
                            {{ row.data.workDay?.description || (row.data.sessions.some(s => s.status === 'Closed') ? "Add description…" : "—") }}
                          </span>
                          <PencilIcon
                            v-if="row.data.sessions.some(s => s.status === 'Closed')"
                            class="size-3 text-slate-300 dark:text-slate-600 group-hover:text-slate-500 shrink-0"
                          />
                        </button>
                      </TableCell>
                    </TableRow>
                  </template>
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
            Enter the session times you need recorded. An admin will receive an email and can approve with one click.
          </p>

          <div class="space-y-1.5">
            <Label>Date</Label>
            <Input v-model="adjForm.date" type="date" class="cursor-pointer" />
          </div>

          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <Label class="text-xs text-slate-500">Clock In <span class="text-destructive">*</span></Label>
              <Input v-model="adjForm.clockIn" type="time" />
            </div>
            <div class="space-y-1.5">
              <Label class="text-xs text-slate-500">Clock Out <span class="text-destructive">*</span></Label>
              <Input v-model="adjForm.clockOut" type="time" />
            </div>
            <div class="space-y-1.5">
              <Label class="text-xs text-slate-500">Break Start</Label>
              <Input v-model="adjForm.breakStart" type="time" />
            </div>
            <div class="space-y-1.5">
              <Label class="text-xs text-slate-500">Break End</Label>
              <Input v-model="adjForm.breakEnd" type="time" />
            </div>
          </div>
          <p class="text-xs text-slate-400">Leave break blank if no break was taken.</p>

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
