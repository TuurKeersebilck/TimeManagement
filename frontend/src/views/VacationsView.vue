<script setup lang="ts">
import { ref, computed, watch, onMounted } from "vue";
import { onBeforeRouteLeave, useRoute, useRouter } from "vue-router";
import {
  vacationService,
  type VacationBalance,
  type VacationDay,
  type CreateVacationDayDto,
} from "../services/vacationService";
import { holidayService, type PublicHoliday } from "../services/holidayService";
import { adminService, type Employee } from "../services/adminService";
import { settlementService } from "../services/settlementService";
import { vacationRequestService, type VacationRequest } from "../services/vacationRequestService";
import { useAuth } from "@/composables/useAuth";
import { useAppToast } from "@/composables/useAppToast";
import { useDraftRequests } from "@/composables/useDraftRequests";
import { extractApiError } from "@/utils/apiError";
import { useConfirmDialog } from "@/composables/useConfirmDialog";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
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
import YearCalendarOverlay from "@/components/YearCalendarOverlay.vue";
import VacationBalanceCards from "@/components/VacationBalanceCards.vue";
import VacationDaysList from "@/components/VacationDaysList.vue";
import MyVacationRequestsList from "@/components/MyVacationRequestsList.vue";
import {
  CheckCircleIcon,
  XCircleIcon,
  Loader2Icon,
  ChevronLeftIcon,
  ChevronRightIcon,
  PlusIcon,
  Maximize2Icon,
  Trash2Icon,
  AlertTriangleIcon,
  LockIcon,
  SendIcon,
  HourglassIcon,
  PencilLineIcon,
} from "lucide-vue-next";

const toast = useAppToast();
const { confirm } = useConfirmDialog();
const { isAdmin, currentUser } = useAuth();
const route = useRoute();
const router = useRouter();

// ─── Data state ───────────────────────────────────────────────────────────────

const balances = ref<VacationBalance[]>([]);
const vacationDays = ref<VacationDay[]>([]);
const holidays = ref<PublicHoliday[]>([]);
const loading = ref(false);

// ─── Approval ────────────────────────────────────────────────────────────────

// Employees plan days as drafts, submit them together, and an admin approves each period.
// Admins book directly, for anyone.
const requiresApproval = computed(() => !isAdmin.value);

const myRequests = ref<VacationRequest[]>([]);
const cancellingRequestId = ref<number | null>(null);
const submitting = ref(false);

const drafts = computed(() =>
  myRequests.value
    .filter((r) => r.status === "Draft")
    .sort((a, b) => a.startDate.localeCompare(b.startDate))
);
const draftTotalDays = computed(() => drafts.value.reduce((sum, r) => sum + r.totalDays, 0));

// Drafts and requests awaiting review: shown on the calendar, not booked yet.
const openRequests = computed(() =>
  myRequests.value.filter((r) => r.status === "Pending" || r.status === "Draft")
);

const { draftCount } = useDraftRequests();
watch(
  () => drafts.value.length,
  (count) => (draftCount.value = count)
);

const loadMyRequests = async () => {
  if (!requiresApproval.value) return;
  try {
    myRequests.value = await vacationRequestService.getMine();
  } catch {
    // The list is extra context; the calendar and balances still work without it.
  }
};

// ─── Admin: manage on behalf of an employee ──────────────────────────────────

const employees = ref<Employee[]>([]);
const selectedEmployeeId = ref<string>(
  typeof route.query.employeeId === "string" && route.query.employeeId ? route.query.employeeId : "own"
);

// undefined => acting on the admin's own vacation days
const actingEmployeeId = computed(() =>
  isAdmin.value && selectedEmployeeId.value !== "own" ? selectedEmployeeId.value : undefined
);

const actingEmployeeName = computed(
  () => employees.value.find((e) => e.id === selectedEmployeeId.value)?.fullName
);

// "Manage my own vacation" already covers the logged-in admin — don't list them again.
const otherEmployees = computed(() =>
  employees.value.filter((e) => e.id !== currentUser.value?.id)
);

watch(selectedEmployeeId, (id) => {
  router.replace({ query: { ...route.query, employeeId: id === "own" ? undefined : id } });
  loadVacationAndBalances();
});

const loadVacationAndBalances = async () => {
  loading.value = true;
  loadSettledMonths();
  try {
    const [b, d] = await Promise.all([
      vacationService.getBalances(new Date().getFullYear(), actingEmployeeId.value),
      vacationService.getVacationDays(actingEmployeeId.value),
    ]);
    balances.value = b;
    vacationDays.value = d;
  } catch {
    toast.error("Failed to load vacation data");
  } finally {
    loading.value = false;
  }
};

// ─── Admin-only types (e.g. sick leave) ──────────────────────────────────────

const adminOnlyTypeIds = computed(
  () => new Set(balances.value.filter((b) => b.isAdminOnly).map((b) => b.vacationTypeId))
);

// Types the viewer can book: an employee doesn't get the admin-only ones.
const bookableBalances = computed(() =>
  isAdmin.value ? balances.value : balances.value.filter((b) => !b.isAdminOnly)
);

const isLockedDay = (day: VacationDay) =>
  !isAdmin.value && adminOnlyTypeIds.value.has(day.vacationTypeId);

// ─── Admin: settled months of the employee ───────────────────────────────────

// "YYYY-MM" of the acting employee's settled months; only feeds a warning.
const settledMonths = ref(new Set<string>());

const loadSettledMonths = async () => {
  const employeeId = actingEmployeeId.value;
  if (!employeeId) {
    settledMonths.value = new Set();
    return;
  }
  try {
    const history = await settlementService.getEmployeeHistory(employeeId);
    if (employeeId !== actingEmployeeId.value) return;
    settledMonths.value = new Set(
      history
        .filter((s) => s.status === "Settled")
        .map((s) => `${s.year}-${String(s.month).padStart(2, "0")}`)
    );
  } catch {
    settledMonths.value = new Set();
  }
};

// ─── Year overlay ─────────────────────────────────────────────────────────────

const yearOverlayOpen = ref(false);

// ─── Calendar ─────────────────────────────────────────────────────────────────

const currentMonth = ref(new Date());

const monthLabel = computed(() =>
  currentMonth.value.toLocaleDateString(undefined, { month: "long", year: "numeric" })
);

const prevMonth = () => {
  const d = new Date(currentMonth.value);
  d.setMonth(d.getMonth() - 1);
  currentMonth.value = d;
};

const nextMonth = () => {
  const d = new Date(currentMonth.value);
  d.setMonth(d.getMonth() + 1);
  currentMonth.value = d;
};

const goToday = () => {
  currentMonth.value = new Date();
};

interface CalDay {
  iso: string;
  day: number;
  isCurrentMonth: boolean;
  isToday: boolean;
  isWeekend: boolean;
}

const toIso = (d: Date) => {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
};

const todayIso = toIso(new Date());

const calendarDays = computed<CalDay[]>(() => {
  const year = currentMonth.value.getFullYear();
  const month = currentMonth.value.getMonth();
  const firstDay = new Date(year, month, 1);
  const lastDay = new Date(year, month + 1, 0);
  const startDow = (firstDay.getDay() + 6) % 7; // Monday = 0

  const days: CalDay[] = [];

  for (let i = startDow - 1; i >= 0; i--) {
    const d = new Date(year, month, -i);
    const dow = d.getDay();
    days.push({ iso: toIso(d), day: d.getDate(), isCurrentMonth: false, isToday: false, isWeekend: dow === 0 || dow === 6 });
  }
  for (let n = 1; n <= lastDay.getDate(); n++) {
    const d = new Date(year, month, n);
    const iso = toIso(d);
    const dow = d.getDay();
    days.push({ iso, day: n, isCurrentMonth: true, isToday: iso === todayIso, isWeekend: dow === 0 || dow === 6 });
  }
  const remaining = 42 - days.length;
  for (let n = 1; n <= remaining; n++) {
    const d = new Date(year, month + 1, n);
    const dow = d.getDay();
    days.push({ iso: toIso(d), day: d.getDate(), isCurrentMonth: false, isToday: false, isWeekend: dow === 0 || dow === 6 });
  }

  return days;
});

const holidaysByDate = computed(() => {
  const map = new Map<string, PublicHoliday>();
  for (const h of holidays.value) map.set(h.date, h);
  return map;
});

const vacationsByDate = computed(() => {
  const map = new Map<string, VacationDay[]>();
  for (const d of vacationDays.value) {
    if (!map.has(d.date)) map.set(d.date, []);
    map.get(d.date)!.push(d);
  }
  return map;
});

// Days covered by a draft or pending request, as they'd be booked: no weekends or public holidays off.
const pendingByDate = computed(() => {
  const map = new Map<string, VacationRequest[]>();
  for (const r of openRequests.value) {
    const d = new Date(r.startDate + "T00:00:00");
    const end = new Date(r.endDate + "T00:00:00");
    while (d <= end) {
      const iso = toIso(d);
      const dow = d.getDay();
      const holiday = holidaysByDate.value.get(iso);
      if (dow !== 0 && dow !== 6 && (!holiday || holiday.isWorkingDay)) {
        if (!map.has(iso)) map.set(iso, []);
        map.get(iso)!.push(r);
      }
      d.setDate(d.getDate() + 1);
    }
  }
  return map;
});

const isBookableCell = (cell: CalDay) =>
  !cell.isWeekend &&
  bookableBalances.value.length > 0 &&
  !(holidaysByDate.value.has(cell.iso) && !holidaysByDate.value.get(cell.iso)!.isWorkingDay);

const isCellInteractive = (cell: CalDay) =>
  cell.isCurrentMonth &&
  (vacationsByDate.value.has(cell.iso) || pendingByDate.value.has(cell.iso) || isBookableCell(cell));

const upcomingVacationDays = computed(() =>
  vacationDays.value.filter((d) => d.date >= todayIso).sort((a, b) => a.date.localeCompare(b.date))
);

const WEEK_DAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
const MAX_VISIBLE = 2;

// ─── Popover (inline create / edit) ──────────────────────────────────────────

const openPopoverIso = ref<string | null>(null);
const popoverSaving = ref(false);
const popoverEditingId = ref<number | null>(null);
const isPopoverEditMode = computed(() => popoverEditingId.value !== null);

const popoverDayEntries = computed(() =>
  openPopoverIso.value ? (vacationsByDate.value.get(openPopoverIso.value) ?? []) : []
);
const popoverPendingRequests = computed(() =>
  openPopoverIso.value ? (pendingByDate.value.get(openPopoverIso.value) ?? []) : []
);
const popoverDayTotal = computed(() =>
  popoverDayEntries.value.reduce((sum, e) => sum + e.amount, 0)
);

const popoverForm = ref({
  vacationTypeId: "",
  startDate: "",
  endDate: "",
  amount: "1",
  note: "",
});

const workingDaysInRange = computed(() => {
  const { startDate, endDate } = popoverForm.value;
  if (!startDate || !endDate) return 0;
  const start = new Date(startDate + "T00:00:00");
  const end = new Date(endDate + "T00:00:00");
  if (end < start) return 0;
  let count = 0;
  const d = new Date(start);
  while (d <= end) {
    const dow = d.getDay();
    const iso = toIso(d);
    const holiday = holidaysByDate.value.get(iso);
    if (dow !== 0 && dow !== 6 && (!holiday || holiday.isWorkingDay)) count++;
    d.setDate(d.getDate() + 1);
  }
  return count;
});

// Range mode is only available when the day has no existing entries
const isRangeMode = computed(
  () => !isPopoverEditMode.value
    && popoverDayEntries.value.length === 0
    && popoverForm.value.startDate !== popoverForm.value.endDate
    && !!popoverForm.value.endDate
);

const popoverLiveRemaining = computed(() => {
  if (!popoverForm.value.vacationTypeId) return null;
  const typeId = parseInt(popoverForm.value.vacationTypeId);
  const balance = balances.value.find((b) => b.vacationTypeId === typeId);
  if (!balance || balance.remainingDays === null) return null;
  if (isPopoverEditMode.value) {
    const orig = vacationDays.value.find((d) => d.id === popoverEditingId.value);
    let base = balance.remainingDays;
    if (orig && orig.vacationTypeId === typeId) {
      const origYear = new Date(orig.date + "T00:00:00").getFullYear();
      if (origYear === new Date().getFullYear()) base += orig.amount;
    }
    return base - parseFloat(popoverForm.value.amount || "1");
  }
  const cost = workingDaysInRange.value * parseFloat(popoverForm.value.amount || "1");
  // Days in drafts and pending requests are already spoken for.
  const held = requiresApproval.value ? balance.pendingDays + balance.draftDays : 0;
  return balance.remainingDays - held - cost;
});

const canPopoverSubmit = computed(() => {
  if (!popoverForm.value.vacationTypeId || !popoverForm.value.startDate) return false;
  if (popoverEntryLocked.value) return false;
  const typeId = parseInt(popoverForm.value.vacationTypeId);
  if (!bookableBalances.value.some((b) => b.vacationTypeId === typeId)) return false;
  // null = unlimited type, nothing to check
  if (popoverLiveRemaining.value !== null && popoverLiveRemaining.value < 0) return false;
  if (!isPopoverEditMode.value && workingDaysInRange.value === 0) return false;
  return true;
});

const highlightedRange = computed(() => {
  const { startDate, endDate } = popoverForm.value;
  if (!startDate || !endDate || !openPopoverIso.value) return new Set<string>();
  const s = startDate < endDate ? startDate : endDate;
  const e = startDate < endDate ? endDate : startDate;
  const set = new Set<string>();
  const d = new Date(s + "T00:00:00");
  const end = new Date(e + "T00:00:00");
  while (d <= end) {
    set.add(toIso(d));
    d.setDate(d.getDate() + 1);
  }
  return set;
});

const popoverDateLabel = computed(() => {
  if (!openPopoverIso.value) return "";
  return new Date(openPopoverIso.value + "T00:00:00").toLocaleDateString(undefined, {
    weekday: "long",
    day: "numeric",
    month: "long",
  });
});

// An employee looking at a day the admin registered with an admin-only type.
const popoverEntryLocked = computed(() => {
  if (!isPopoverEditMode.value) return false;
  const entry = vacationDays.value.find((d) => d.id === popoverEditingId.value);
  return !!entry && isLockedDay(entry);
});

// An employee looking at an approved day: they can give it back, but changing it needs a new request.
const popoverEntryReadOnly = computed(() => requiresApproval.value && isPopoverEditMode.value);
const popoverEntry = computed(() =>
  vacationDays.value.find((d) => d.id === popoverEditingId.value)
);

const popoverTouchesSettledMonth = computed(() => {
  if (settledMonths.value.size === 0) return false;
  const { startDate } = popoverForm.value;
  const dates = isPopoverEditMode.value
    ? [startDate, vacationDays.value.find((d) => d.id === popoverEditingId.value)?.date ?? ""]
    : [...highlightedRange.value];
  return dates.some((d) => settledMonths.value.has(d.slice(0, 7)));
});

const fillFormForEntry = (v: VacationDay) => {
  popoverEditingId.value = v.id;
  popoverForm.value = {
    vacationTypeId: String(v.vacationTypeId),
    startDate: v.date,
    endDate: v.date,
    amount: String(v.amount),
    note: v.note ?? "",
  };
};

const switchToNewMode = () => {
  popoverEditingId.value = null;
  const remaining = 1 - popoverDayTotal.value;
  popoverForm.value = {
    vacationTypeId: bookableBalances.value.length === 1 ? String(bookableBalances.value[0].vacationTypeId) : "",
    startDate: openPopoverIso.value ?? "",
    endDate: openPopoverIso.value ?? "",
    amount: String(remaining <= 0.5 ? 0.5 : 1),
    note: "",
  };
};

const openPopover = (iso: string) => {
  openPopoverIso.value = iso;
  const existing = vacationsByDate.value.get(iso);
  if (existing && existing.length > 0) {
    fillFormForEntry(existing[0]);
  } else {
    popoverEditingId.value = null;
    popoverForm.value = {
      vacationTypeId: bookableBalances.value.length === 1 ? String(bookableBalances.value[0].vacationTypeId) : "",
      startDate: iso,
      endDate: iso,
      amount: "1",
      note: "",
    };
  }
};

const closePopover = () => {
  openPopoverIso.value = null;
};

const savePopover = async () => {
  if (!canPopoverSubmit.value) return;
  popoverSaving.value = true;
  try {
    const { vacationTypeId, startDate, endDate, amount, note } = popoverForm.value;

    if (requiresApproval.value) {
      if (isPopoverEditMode.value) return;
      const request = await vacationRequestService.create({
        vacationTypeId: parseInt(vacationTypeId),
        startDate,
        endDate: endDate || startDate,
        amount: parseFloat(amount),
        note: note.trim() || undefined,
      });
      myRequests.value = [request, ...myRequests.value];
      toast.success(
        `${request.totalDays} day(s) planned. Submit your request when you're done planning.`
      );
    } else if (isPopoverEditMode.value) {
      const updated = await vacationService.update(
        popoverEditingId.value!,
        {
          vacationTypeId: parseInt(vacationTypeId),
          date: startDate,
          amount: parseFloat(amount),
          note: note.trim() || undefined,
        },
        actingEmployeeId.value
      );
      const idx = vacationDays.value.findIndex((d) => d.id === popoverEditingId.value);
      if (idx !== -1) vacationDays.value[idx] = updated;
      toast.success("Vacation day updated");
    } else if (startDate === endDate) {
      const payload: CreateVacationDayDto = {
        vacationTypeId: parseInt(vacationTypeId),
        date: startDate,
        amount: parseFloat(amount),
        note: note.trim() || undefined,
      };
      const created = await vacationService.create(payload, actingEmployeeId.value);
      vacationDays.value.unshift(created);
      toast.success("Vacation day planned");
    } else {
      const result = await vacationService.createRange(
        {
          vacationTypeId: parseInt(vacationTypeId),
          startDate,
          endDate,
          amount: parseFloat(amount),
          note: note.trim() || undefined,
        },
        actingEmployeeId.value
      );
      vacationDays.value.unshift(...result.created);
      const skipped = result.skippedWeekends + result.skippedHolidays + result.skippedExisting;
      toast.success(
        skipped > 0
          ? `${result.created.length} day(s) planned (${skipped} skipped)`
          : `${result.created.length} day(s) planned`
      );
    }

    balances.value = await vacationService.getBalances(new Date().getFullYear(), actingEmployeeId.value);
    closePopover();
  } catch (err: unknown) {
    toast.error(extractApiError(err, "Failed to save vacation"));
  } finally {
    popoverSaving.value = false;
  }
};

const deletePopoverEntry = () => {
  const day = vacationDays.value.find((d) => d.id === popoverEditingId.value);
  if (!day) return;
  closePopover();
  deleteDay(day);
};

// ─── Drafts: remove one, or submit them all ──────────────────────────────────

const fmtPeriod = (r: VacationRequest) =>
  r.startDate === r.endDate
    ? displayDate(r.startDate)
    : `${displayDate(r.startDate)} – ${displayDate(r.endDate)}`;

const removeDraft = async (request: VacationRequest) => {
  cancellingRequestId.value = request.id;
  try {
    await vacationRequestService.deleteDraft(request.id);
    myRequests.value = myRequests.value.filter((r) => r.id !== request.id);
    balances.value = await vacationService.getBalances(new Date().getFullYear(), actingEmployeeId.value);
    toast.success("Planned days removed");
  } catch (err: unknown) {
    toast.error(extractApiError(err, "Failed to remove planned days"));
    loadMyRequests();
  } finally {
    cancellingRequestId.value = null;
  }
};

const submitDrafts = async () => {
  submitting.value = true;
  try {
    const submitted = await vacationRequestService.submit();
    const sentIds = new Set(submitted.map((r) => r.id));
    myRequests.value = [
      ...submitted,
      ...myRequests.value.filter((r) => !sentIds.has(r.id)),
    ];
    balances.value = await vacationService.getBalances(new Date().getFullYear(), actingEmployeeId.value);
    toast.success("Request sent. You'll get an email once your admin has reviewed it.");
    return true;
  } catch (err: unknown) {
    toast.error(extractApiError(err, "Failed to send your request"));
    loadMyRequests();
    return false;
  } finally {
    submitting.value = false;
  }
};

const draftSummary = () =>
  `${drafts.value.map(fmtPeriod).join(", ")} (${draftTotalDays.value} day${draftTotalDays.value === 1 ? "" : "s"} in total)`;

const confirmSubmit = () => {
  confirm({
    title: "Send vacation request",
    message: `Send ${draftSummary()} to your admin for approval? You can still cancel a request until it's reviewed.`,
    confirmLabel: "Send request",
    cancelLabel: "Keep planning",
    onConfirm: async () => {
      await submitDrafts();
    },
  });
};

// Leaving with unsent drafts: remind once per visit. Nothing is lost either way (drafts are saved),
// so "Keep as draft" just carries on to where the employee was going.
let remindedAboutDrafts = false;
onBeforeRouteLeave((to) => {
  if (!requiresApproval.value || drafts.value.length === 0 || remindedAboutDrafts) return true;
  remindedAboutDrafts = true;
  confirm({
    title: "Your request hasn't been sent",
    message: `You planned ${draftSummary()}, but your admin hasn't been asked yet. Send it now?`,
    confirmLabel: "Send request",
    cancelLabel: "Keep as draft",
    onConfirm: async () => {
      if (await submitDrafts()) router.push(to.fullPath);
    },
    onCancel: () => {
      router.push(to.fullPath);
    },
  });
  return false;
});

// ─── Cancel a pending request ────────────────────────────────────────────────

const cancelRequest = (request: VacationRequest) => {
  const period = fmtPeriod(request);
  confirm({
    title: "Cancel vacation request",
    message: `Withdraw your ${request.vacationTypeName} request for ${period}? Your admin won't need to review it any more.`,
    confirmLabel: "Cancel request",
    cancelLabel: "Keep it",
    variant: "destructive",
    onConfirm: async () => {
      cancellingRequestId.value = request.id;
      try {
        await vacationRequestService.cancel(request.id);
        closePopover();
        await loadMyRequests();
        balances.value = await vacationService.getBalances(new Date().getFullYear(), actingEmployeeId.value);
        toast.success("Request cancelled");
      } catch (err: unknown) {
        toast.error(extractApiError(err, "Failed to cancel request"));
        loadMyRequests();
      } finally {
        cancellingRequestId.value = null;
      }
    },
  });
};

// ─── Delete ───────────────────────────────────────────────────────────────────

const displayDate = (iso: string) =>
  new Date(iso).toLocaleDateString(undefined, { day: "numeric", month: "short", year: "numeric" });

const deleteDay = (day: VacationDay) => {
  const settledNote = settledMonths.value.has(day.date.slice(0, 7))
    ? " This month has already been settled; reopen its settlement afterwards to recalculate it."
    : "";
  confirm({
    title: "Delete vacation day",
    message: `Remove the ${day.amount === 0.5 ? "half" : "full"} day on ${displayDate(day.date)} (${day.vacationTypeName})?${settledNote}`,
    confirmLabel: "Delete",
    variant: "destructive",
    onConfirm: async () => {
      try {
        await vacationService.delete(day.id, actingEmployeeId.value);
        vacationDays.value = vacationDays.value.filter((d) => d.id !== day.id);
        balances.value = await vacationService.getBalances(new Date().getFullYear(), actingEmployeeId.value);
        toast.success("Vacation day removed");
      } catch {
        toast.error("Failed to delete");
      }
    },
  });
};

// ─── Mount ────────────────────────────────────────────────────────────────────

onMounted(async () => {
  loading.value = true;
  try {
    const year = new Date().getFullYear();
    const [b, d, h, emps] = await Promise.all([
      vacationService.getBalances(new Date().getFullYear(), actingEmployeeId.value),
      vacationService.getVacationDays(actingEmployeeId.value),
      holidayService.getHolidays(year).catch(() => [] as PublicHoliday[]),
      isAdmin.value ? adminService.getEmployees() : Promise.resolve([] as Employee[]),
    ]);
    balances.value = b;
    vacationDays.value = d;
    holidays.value = h;
    employees.value = emps;
    loadSettledMonths();
    loadMyRequests();
  } catch {
    toast.error("Failed to load vacation data");
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <div class="p-6 lg:p-8">
    <div class="max-w-5xl mx-auto">
      <!-- Header -->
      <div class="mb-6 flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">
            {{ actingEmployeeName ? `${actingEmployeeName}'s vacation` : "Vacations" }}
          </h1>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
            {{
              actingEmployeeName
                ? `Manage vacation on behalf of ${actingEmployeeName}`
                : requiresApproval
                  ? "Plan your days, then send them to your admin for approval."
                  : "Plan your vacation days"
            }}
          </p>
        </div>

        <!-- Admin: act on behalf of an employee -->
        <Select v-if="isAdmin" v-model="selectedEmployeeId">
          <SelectTrigger class="w-56">
            <SelectValue placeholder="Manage my own vacation" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="own">Manage my own vacation</SelectItem>
            <SelectItem v-for="emp in otherEmployees" :key="emp.id" :value="emp.id">
              {{ emp.fullName }}
            </SelectItem>
          </SelectContent>
        </Select>
      </div>

      <div class="max-w-4xl">
        <!-- Balance cards -->
        <VacationBalanceCards :balances="balances" :loading="loading" />

        <!-- Calendar -->
        <section class="mb-8">
          <!-- Month navigation -->
          <div class="flex items-center justify-between mb-3">
            <div class="flex items-center gap-1">
              <Button variant="ghost" size="icon" class="size-8" @click="prevMonth">
                <ChevronLeftIcon class="size-4" />
              </Button>
              <Button variant="ghost" size="icon" class="size-8" @click="nextMonth">
                <ChevronRightIcon class="size-4" />
              </Button>
              <span class="text-base font-semibold text-slate-900 dark:text-slate-100 ml-2 capitalize">
                {{ monthLabel }}
              </span>
            </div>
            <div class="flex items-center gap-2">
              <Button variant="outline" size="sm" @click="goToday">Today</Button>
              <Button variant="outline" size="sm" @click="yearOverlayOpen = true">
                <Maximize2Icon class="size-3.5" />
                Year view
              </Button>
            </div>
          </div>

          <div v-if="loading" class="card h-64 animate-pulse bg-slate-100 dark:bg-slate-800" />

          <div v-else class="card overflow-hidden">
            <!-- Weekday headers -->
            <div class="grid grid-cols-7 border-b border-slate-200 dark:border-slate-800">
              <div
                v-for="wd in WEEK_DAYS"
                :key="wd"
                class="text-center text-xs font-semibold text-slate-400 dark:text-slate-500 py-2"
              >
                {{ wd }}
              </div>
            </div>

            <!-- Day cells -->
            <div class="grid grid-cols-7">
              <Popover
                v-for="cell in calendarDays"
                :key="cell.iso"
                :open="openPopoverIso === cell.iso"
                @update:open="
                  (v) => {
                    if (v && isCellInteractive(cell))
                      openPopover(cell.iso);
                    else if (!v) closePopover();
                  }
                "
              >
                <PopoverTrigger as-child>
                  <div
                    :data-date="cell.iso"
                    :class="[
                      'border-b border-r border-slate-100 dark:border-slate-800/60 min-h-20 p-1.5 transition-colors select-none',
                      !cell.isCurrentMonth && 'opacity-40',
                      cell.isToday && 'bg-primary/10',
                      cell.isWeekend && 'bg-slate-50/80 dark:bg-slate-900/60',
                      openPopoverIso === cell.iso ? 'ring-2 ring-inset ring-primary' : '',
                      highlightedRange.has(cell.iso) && openPopoverIso !== cell.iso ? 'bg-primary/10' : '',
                      isCellInteractive(cell)
                        ? 'cursor-pointer hover:bg-slate-50 dark:hover:bg-slate-800/40'
                        : 'cursor-default pointer-events-none',
                    ]"
                  >
                    <!-- Day number -->
                    <div
                      :class="[
                        'text-xs font-medium w-6 h-6 flex items-center justify-center rounded-full mb-1',
                        cell.isToday
                          ? 'bg-primary text-primary-foreground'
                          : cell.isWeekend
                            ? 'text-slate-400 dark:text-slate-600'
                            : cell.isCurrentMonth
                              ? 'text-slate-700 dark:text-slate-200'
                              : 'text-slate-300 dark:text-slate-600',
                      ]"
                    >
                      {{ cell.day }}
                    </div>

                    <!-- Holiday marker -->
                    <div
                      v-if="holidaysByDate.has(cell.iso)"
                      class="text-[10px] leading-tight truncate rounded px-1 py-0.5 mb-0.5 bg-amber-50 dark:bg-amber-950/40 text-amber-700 dark:text-amber-300 border-l-2 border-amber-400"
                      :title="holidaysByDate.get(cell.iso)!.name"
                    >
                      {{ holidaysByDate.get(cell.iso)!.name }}
                    </div>

                    <!-- Own vacation chips -->
                    <template v-if="vacationsByDate.has(cell.iso)">
                      <div
                        v-for="entry in vacationsByDate.get(cell.iso)!.slice(0, MAX_VISIBLE)"
                        :key="entry.id"
                        :style="{
                          backgroundColor: (entry.vacationTypeColor ?? '#6366f1') + '28',
                          borderLeftColor: entry.vacationTypeColor ?? '#6366f1',
                        }"
                        class="text-[10px] leading-tight truncate rounded px-1 py-0.5 mb-0.5 border-l-2 text-slate-700 dark:text-slate-200"
                      >
                        {{ entry.vacationTypeName }}<span v-if="entry.amount === 0.5" class="opacity-50"> ½</span>
                      </div>
                      <div
                        v-if="vacationsByDate.get(cell.iso)!.length > MAX_VISIBLE"
                        class="text-[10px] text-slate-400 dark:text-slate-500 pl-1"
                      >
                        +{{ vacationsByDate.get(cell.iso)!.length - MAX_VISIBLE }} more
                      </div>
                    </template>

                    <!-- Planned (draft) or requested, awaiting approval -->
                    <div
                      v-for="r in pendingByDate.get(cell.iso) ?? []"
                      :key="`open-${r.id}`"
                      :style="{ borderColor: r.vacationTypeColor ?? '#6366f1' }"
                      :class="[
                        'text-[10px] leading-tight truncate rounded px-1 py-0.5 mb-0.5 border border-dashed',
                        r.status === 'Draft'
                          ? 'text-primary bg-primary/5'
                          : 'text-slate-500 dark:text-slate-400',
                      ]"
                      :title="r.status === 'Draft' ? `${r.vacationTypeName}: planned, not sent yet` : `${r.vacationTypeName}: awaiting approval`"
                    >
                      {{ r.vacationTypeName }}<span v-if="r.amount === 0.5" class="opacity-50"> ½</span>
                      <span class="opacity-60">· {{ r.status === "Draft" ? "draft" : "pending" }}</span>
                    </div>
                  </div>
                </PopoverTrigger>

                <PopoverContent class="w-72 p-0 shadow-lg flex flex-col max-h-[min(520px,70svh)]" :collision-padding="12">
                  <!-- Popover header — pinned, never scrolls away -->
                  <div class="flex items-center justify-between px-4 py-3 border-b border-slate-100 dark:border-slate-800 shrink-0">
                    <span class="text-sm font-semibold text-slate-900 dark:text-slate-100 capitalize">
                      {{ popoverDateLabel }}
                    </span>
                    <button
                      class="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 transition-colors"
                      @click="closePopover"
                    >
                      <svg class="size-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M18 6 6 18M6 6l12 12" />
                      </svg>
                    </button>
                  </div>

                  <!-- Scrollable body (min-h-0 lets flex child shrink below content size) -->
                  <div class="overflow-y-auto min-h-0">

                  <!-- Holiday notice -->
                  <div
                    v-if="holidaysByDate.has(cell.iso)"
                    class="flex items-center gap-2 px-4 py-2 bg-amber-50 dark:bg-amber-950/30 border-b border-amber-100 dark:border-amber-900"
                  >
                    <span class="text-xs text-amber-700 dark:text-amber-300 font-medium">
                      🎉 {{ holidaysByDate.get(cell.iso)!.name }}
                    </span>
                  </div>

                  <!-- Pending requests covering this day -->
                  <div
                    v-for="r in popoverPendingRequests"
                    :key="`popover-pending-${r.id}`"
                    class="flex items-start gap-2 px-4 py-2.5 bg-amber-50/70 dark:bg-amber-950/20 border-b border-amber-100 dark:border-amber-900/60"
                  >
                    <PencilLineIcon v-if="r.status === 'Draft'" class="size-3.5 mt-0.5 shrink-0 text-primary" />
                    <HourglassIcon v-else class="size-3.5 mt-0.5 shrink-0 text-amber-600 dark:text-amber-400" />
                    <div class="flex-1 min-w-0 text-xs">
                      <p class="font-medium text-slate-800 dark:text-slate-200">
                        {{ r.vacationTypeName }} {{ r.status === "Draft" ? "planned" : "requested" }}
                      </p>
                      <p class="text-slate-500 dark:text-slate-400">
                        {{ fmtPeriod(r) }} · {{ r.status === "Draft" ? "not sent yet" : "awaiting approval" }}
                      </p>
                    </div>
                    <button
                      type="button"
                      class="text-xs font-medium text-slate-500 hover:text-red-600 dark:hover:text-red-400 shrink-0 cursor-pointer"
                      :disabled="cancellingRequestId === r.id"
                      @click="r.status === 'Draft' ? removeDraft(r) : cancelRequest(r)"
                    >
                      {{ r.status === "Draft" ? "Remove" : "Cancel" }}
                    </button>
                  </div>

                  <!-- Entry switcher (multiple entries on same day) -->
                  <div
                    v-if="popoverDayEntries.length > 0"
                    class="flex flex-wrap gap-1.5 px-4 pt-3 pb-1"
                  >
                    <button
                      v-for="entry in popoverDayEntries"
                      :key="entry.id"
                      type="button"
                      :class="[
                        'flex items-center gap-1.5 px-2 py-1 rounded-md text-xs font-medium border transition-colors',
                        popoverEditingId === entry.id
                          ? 'bg-primary/10 border-primary/30 text-primary'
                          : 'bg-slate-50 dark:bg-slate-800 border-slate-200 dark:border-slate-700 text-slate-500 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-700',
                      ]"
                      @click="fillFormForEntry(entry)"
                    >
                      <span
                        class="w-1.5 h-1.5 rounded-full shrink-0"
                        :style="{ backgroundColor: entry.vacationTypeColor ?? '#6366f1' }"
                      />
                      {{ entry.vacationTypeName }}
                      <span class="opacity-60">{{ entry.amount === 1 ? "Full" : "½" }}</span>
                    </button>
                    <button
                      v-if="popoverDayTotal < 1"
                      type="button"
                      :class="[
                        'flex items-center gap-1 px-2 py-1 rounded-md text-xs font-medium border transition-colors',
                        !isPopoverEditMode
                          ? 'bg-primary/10 border-primary/30 text-primary'
                          : 'bg-slate-50 dark:bg-slate-800 border-slate-200 dark:border-slate-700 text-slate-500 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-700',
                      ]"
                      @click="switchToNewMode"
                    >
                      <PlusIcon class="size-3" />
                      Add
                    </button>
                  </div>

                  <!-- Create / Edit form -->
                  <div class="p-4 space-y-3">
                    <p class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">
                      {{
                        popoverEntryLocked
                          ? "Registered by your admin"
                          : popoverEntryReadOnly
                            ? "Approved vacation"
                            : isPopoverEditMode
                              ? "Edit vacation"
                              : "Plan vacation"
                      }}
                    </p>

                    <div
                      v-if="popoverEntryLocked"
                      class="flex items-start gap-2 rounded-lg bg-slate-50 dark:bg-slate-800 px-3 py-2 text-xs text-slate-600 dark:text-slate-300"
                    >
                      <LockIcon class="size-3.5 shrink-0 mt-px" />
                      Only an admin can change or remove this day. Ask your admin if something isn't right.
                    </div>

                    <template v-else-if="popoverEntryReadOnly && popoverEntry">
                      <div class="text-sm text-slate-700 dark:text-slate-300 space-y-0.5">
                        <p class="font-medium">
                          {{ popoverEntry.vacationTypeName }}
                          <span class="font-normal text-slate-500 dark:text-slate-400">
                            · {{ popoverEntry.amount === 1 ? "Full day" : "Half day" }}
                          </span>
                        </p>
                        <p v-if="popoverEntry.note" class="text-xs text-slate-500 dark:text-slate-400">
                          {{ popoverEntry.note }}
                        </p>
                      </div>
                      <p class="text-xs text-slate-500 dark:text-slate-400">
                        To change this day, remove it and send a new request.
                      </p>
                      <Button class="w-full" size="sm" variant="ghost" @click="deletePopoverEntry">
                        <Trash2Icon class="size-3.5" />
                        Remove day
                      </Button>
                    </template>

                    <template v-else>
                    <div
                      v-if="popoverTouchesSettledMonth"
                      class="flex items-start gap-2 rounded-lg border border-amber-200 dark:border-amber-800 bg-amber-50 dark:bg-amber-950/30 px-3 py-2 text-xs text-amber-700 dark:text-amber-300"
                    >
                      <AlertTriangleIcon class="size-3.5 shrink-0 mt-px" />
                      This month has already been settled. To update the balance, reopen the settlement under
                      Settlements afterwards: it's recalculated with this change.
                    </div>

                    <div class="space-y-1">
                      <Label class="text-xs">Type</Label>
                      <Select v-model="popoverForm.vacationTypeId">
                        <SelectTrigger class="h-8 text-sm">
                          <SelectValue placeholder="Select type" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem
                            v-for="balance in bookableBalances"
                            :key="balance.vacationTypeId"
                            :value="String(balance.vacationTypeId)"
                          >
                            {{ balance.vacationTypeName }}
                          </SelectItem>
                        </SelectContent>
                      </Select>
                    </div>

                    <div :class="isPopoverEditMode || popoverDayEntries.length > 0 ? '' : 'grid grid-cols-2 gap-2'">
                      <div class="space-y-1">
                        <Label class="text-xs">{{ isPopoverEditMode || popoverDayEntries.length > 0 ? "Date" : "From" }}</Label>
                        <Input v-model="popoverForm.startDate" type="date" class="h-8 cursor-pointer text-sm" />
                      </div>
                      <div v-if="!isPopoverEditMode && popoverDayEntries.length === 0" class="space-y-1">
                        <Label class="text-xs">To</Label>
                        <Input v-model="popoverForm.endDate" type="date" class="h-8 cursor-pointer text-sm" />
                      </div>
                    </div>

                    <div class="space-y-1">
                      <Label class="text-xs">Duration</Label>
                      <Select v-model="popoverForm.amount">
                        <SelectTrigger class="h-8 text-sm">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="1">Full day</SelectItem>
                          <SelectItem value="0.5">Half day</SelectItem>
                        </SelectContent>
                      </Select>
                    </div>

                    <div class="space-y-1">
                      <Label class="text-xs">Note</Label>
                      <Input v-model="popoverForm.note" type="text" placeholder="Optional" class="h-8 text-sm" maxlength="500" />
                    </div>

                    <div
                      v-if="isRangeMode"
                      class="text-xs text-slate-500 dark:text-slate-400 bg-slate-50 dark:bg-slate-800 rounded-lg px-3 py-2"
                    >
                      {{ workingDaysInRange }} working day{{ workingDaysInRange !== 1 ? "s" : "" }} selected
                      <span class="text-slate-400 dark:text-slate-500">(weekends &amp; holidays skipped)</span>
                    </div>

                    <div
                      v-if="popoverForm.vacationTypeId && popoverLiveRemaining !== null"
                      :class="[
                        'rounded-lg px-3 py-2 text-xs flex items-center gap-1.5',
                        popoverLiveRemaining < 0
                          ? 'bg-destructive/10 text-destructive'
                          : 'bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-300',
                      ]"
                    >
                      <XCircleIcon v-if="popoverLiveRemaining < 0" class="size-3.5 shrink-0" />
                      <CheckCircleIcon v-else class="size-3.5 shrink-0" />
                      <span v-if="popoverLiveRemaining < 0">
                        Exceeds balance by {{ Math.abs(popoverLiveRemaining) }} day(s)
                      </span>
                      <span v-else>{{ popoverLiveRemaining }} day(s) remaining after this</span>
                    </div>

                    <Button
                      class="w-full"
                      size="sm"
                      :disabled="popoverSaving || !canPopoverSubmit"
                      @click="savePopover"
                    >
                      <Loader2Icon v-if="popoverSaving" class="size-3.5 animate-spin" />
                      <PlusIcon v-else-if="!isPopoverEditMode" class="size-3.5" />
                      {{ isPopoverEditMode ? "Save changes" : isRangeMode ? `Plan ${workingDaysInRange} day(s)` : "Plan day" }}
                    </Button>
                    <Button
                      v-if="isPopoverEditMode"
                      class="w-full"
                      size="sm"
                      variant="ghost"
                      :disabled="popoverSaving"
                      @click="deletePopoverEntry"
                    >
                      <Trash2Icon class="size-3.5" />
                      Delete
                    </Button>
                    </template>
                  </div>
                  </div><!-- end scrollable body -->
                </PopoverContent>
              </Popover>
            </div>
          </div>
        </section>

        <!-- Employee's own requests -->
        <MyVacationRequestsList
          v-if="requiresApproval"
          :requests="myRequests"
          :cancelling-id="cancellingRequestId"
          @cancel="cancelRequest"
          @remove="removeDraft"
        />

        <!-- Upcoming -->
        <VacationDaysList
          :vacation-days="upcomingVacationDays"
          :loading="loading"
          :is-locked="isLockedDay"
          @delete="deleteDay"
        />

        <!-- Unsent drafts: stays in view until the request is sent -->
        <div
          v-if="requiresApproval && drafts.length > 0"
          class="sticky bottom-4 z-20 mt-6 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-primary/30 bg-background/95 px-4 py-3 shadow-lg backdrop-blur"
        >
          <div class="min-w-0 text-sm">
            <p class="font-medium text-slate-900 dark:text-slate-100">
              {{ drafts.length }} period{{ drafts.length === 1 ? "" : "s" }} planned
              · {{ draftTotalDays }} day{{ draftTotalDays === 1 ? "" : "s" }}
            </p>
            <p class="text-xs text-slate-500 dark:text-slate-400">
              Not sent yet. Your admin is asked once you submit.
            </p>
          </div>
          <Button :disabled="submitting" @click="confirmSubmit">
            <Loader2Icon v-if="submitting" class="size-4 animate-spin" />
            <SendIcon v-else class="size-4" />
            Submit request
          </Button>
        </div>
      </div>
    </div>
  </div>

  <!-- Year overview overlay -->
  <YearCalendarOverlay
    v-model:open="yearOverlayOpen"
    :vacations-by-date="vacationsByDate"
  />

</template>
