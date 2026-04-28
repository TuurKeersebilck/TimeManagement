<script setup lang="ts">
import { ref, onMounted, computed } from "vue";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import {
  holidayService,
  type PublicHoliday,
  type AvailableCountry,
} from "@/services/holidayService";
import { useAppToast } from "@/composables/useAppToast";
import { useConfirmDialog } from "@/composables/useConfirmDialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Table,
  TableBody,
  TableCell,
  TableEmpty,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Switch } from "@/components/ui/switch";
import {
  GlobeIcon,
  RefreshCwIcon,
  PlusIcon,
  Trash2Icon,
  Loader2Icon,
  CalendarIcon,
  ClockIcon,
  MailIcon,
} from "lucide-vue-next";

const toast = useAppToast();
const { confirm } = useConfirmDialog();

const currentYear = new Date().getFullYear();
const selectedYear = ref(currentYear);

const countryCode = ref<string>("");
const countries = ref<AvailableCountry[]>([]);
const holidays = ref<PublicHoliday[]>([]);

const defaultDailyHours = ref<string>("");
const defaultWeeklyHours = ref<string>("");
const minimumBreakMinutes = ref<string>("");
const savingTargets = ref(false);
const savingMinBreak = ref(false);

const notificationEmail = ref<string>("");
const savingEmail = ref(false);

const enableAdjustmentRequestEmails = ref(true);
const enableMissedClockInEmails = ref(true);
const savingToggles = ref(false);

const loadingCountries = ref(false);
const loadingHolidays = ref(false);
const savingCountry = ref(false);
const refreshing = ref(false);

// Custom holiday form
const newDate = ref("");
const newName = ref("");
const addingCustom = ref(false);
const togglingId = ref<number | null>(null);

const yearOptions = [currentYear - 1, currentYear, currentYear + 1];

const selectedCountryName = computed(
  () => countries.value.find((c) => c.countryCode === countryCode.value)?.name ?? ""
);

onMounted(async () => {
  loadingCountries.value = true;
  try {
    const [config, available] = await Promise.all([
      holidayService.getConfiguration(),
      holidayService.getAvailableCountries(),
    ]);
    countries.value = available;
    if (config.countryCode) countryCode.value = config.countryCode;
    if (config.defaultDailyHours != null) defaultDailyHours.value = String(config.defaultDailyHours);
    if (config.defaultWeeklyHours != null) defaultWeeklyHours.value = String(config.defaultWeeklyHours);
    if (config.minimumBreakMinutes != null) minimumBreakMinutes.value = String(config.minimumBreakMinutes);
    if (config.notificationEmail) notificationEmail.value = config.notificationEmail;
    enableAdjustmentRequestEmails.value = config.enableAdjustmentRequestEmails;
    enableMissedClockInEmails.value = config.enableMissedClockInEmails;
  } catch {
    toast.error("Failed to load settings");
  } finally {
    loadingCountries.value = false;
  }

  await loadHolidays();
});

const loadHolidays = async () => {
  loadingHolidays.value = true;
  try {
    holidays.value = await holidayService.getAdminHolidays(selectedYear.value);
  } catch {
    // No country configured yet — expected
  } finally {
    loadingHolidays.value = false;
  }
};

const saveCountry = async () => {
  if (!countryCode.value) return;
  savingCountry.value = true;
  try {
    await holidayService.setCountry(countryCode.value);
    toast.success(`Country set to ${selectedCountryName.value}. Holidays fetched.`);
    await loadHolidays();
  } catch {
    toast.error("Failed to save country");
  } finally {
    savingCountry.value = false;
  }
};

const refresh = async () => {
  refreshing.value = true;
  try {
    holidays.value = await holidayService.refreshHolidays(selectedYear.value);
    toast.success("Holidays refreshed from API");
  } catch {
    toast.error("Failed to refresh holidays");
  } finally {
    refreshing.value = false;
  }
};

const addCustom = async () => {
  if (!newDate.value || !newName.value.trim()) return;
  addingCustom.value = true;
  try {
    const created = await holidayService.addCustomHoliday(newDate.value, newName.value.trim());
    holidays.value = [...holidays.value, created].sort((a, b) => a.date.localeCompare(b.date));
    newDate.value = "";
    newName.value = "";
    toast.success("Custom holiday added");
  } catch {
    toast.error("Failed to add holiday");
  } finally {
    addingCustom.value = false;
  }
};

const toggleWorkingDay = async (holiday: PublicHoliday) => {
  const idx = holidays.value.findIndex((h) => h.id === holiday.id);
  if (idx === -1) return;
  const newValue = !holiday.isWorkingDay;
  // Optimistic update so the switch reflects the change immediately
  holidays.value[idx] = { ...holidays.value[idx], isWorkingDay: newValue };
  togglingId.value = holiday.id;
  try {
    const updated = await holidayService.setIsWorkingDay(holiday.id, newValue);
    holidays.value[idx] = updated;
  } catch {
    // Revert on failure
    holidays.value[idx] = { ...holidays.value[idx], isWorkingDay: !newValue };
    toast.error("Failed to update holiday");
  } finally {
    togglingId.value = null;
  }
};

const deleteHoliday = (holiday: PublicHoliday) => {
  confirm({
    title: "Remove holiday",
    message: `Remove "${holiday.name}" on ${formatDate(holiday.date)}?`,
    confirmLabel: "Remove",
    variant: "destructive",
    onConfirm: async () => {
      try {
        await holidayService.deleteHoliday(holiday.id);
        holidays.value = holidays.value.filter((h) => h.id !== holiday.id);
        toast.success("Holiday removed");
      } catch {
        toast.error("Failed to remove holiday");
      }
    },
  });
};

const saveTargets = async () => {
  savingTargets.value = true;
  try {
    const daily = defaultDailyHours.value ? parseFloat(defaultDailyHours.value) : null;
    const weekly = defaultWeeklyHours.value ? parseFloat(defaultWeeklyHours.value) : null;
    await holidayService.setDefaultTargets(daily, weekly);
    toast.success("Default targets saved");
  } catch {
    toast.error("Failed to save targets");
  } finally {
    savingTargets.value = false;
  }
};

const saveMinBreak = async () => {
  savingMinBreak.value = true;
  try {
    const minutes = minimumBreakMinutes.value ? parseInt(minimumBreakMinutes.value) : null;
    await holidayService.setMinimumBreakMinutes(minutes);
    toast.success("Minimum break saved");
  } catch {
    toast.error("Failed to save minimum break");
  } finally {
    savingMinBreak.value = false;
  }
};

const saveNotificationToggles = async () => {
  savingToggles.value = true;
  try {
    await holidayService.setNotificationToggles(
      enableAdjustmentRequestEmails.value,
      enableMissedClockInEmails.value,
    );
    toast.success("Notification preferences saved");
  } catch {
    toast.error("Failed to save notification preferences");
  } finally {
    savingToggles.value = false;
  }
};

const saveNotificationEmail = async () => {
  savingEmail.value = true;
  try {
    const result = await holidayService.setNotificationEmail(notificationEmail.value.trim() || null);
    notificationEmail.value = result.notificationEmail ?? "";
    toast.success("Notification email saved");
  } catch {
    toast.error("Failed to save notification email");
  } finally {
    savingEmail.value = false;
  }
};

const formatDate = (iso: string) =>
  new Date(iso + "T00:00:00").toLocaleDateString(undefined, {
    day: "numeric",
    month: "long",
    year: "numeric",
  });
</script>

<template>
  <AuthenticatedLayout>
    <div class="p-6 lg:p-8">
      <div class="max-w-3xl mx-auto">
        <div class="mb-8">
          <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">App Settings</h1>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
            Configure public holidays and other app-wide settings
          </p>
        </div>

        <!-- Country configuration -->
        <section class="mb-8">
          <h2
            class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500 mb-3"
          >
            Country & Public Holidays
          </h2>

          <div class="card p-5">
            <div class="flex items-start gap-3 mb-5">
              <GlobeIcon class="size-5 text-indigo-500 mt-0.5 shrink-0" />
              <div>
                <p class="text-sm font-medium text-slate-900 dark:text-slate-100">
                  Country configuration
                </p>
                <p class="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
                  Selecting a country automatically fetches its public holidays for this year and
                  next. Holidays marked as "Day off" are skipped when employees plan vacation ranges.
                  Toggle off any holidays your company still works on.
                </p>
              </div>
            </div>

            <div class="flex items-end gap-3">
              <div class="flex-1 space-y-1.5">
                <Label>Country</Label>
                <Select v-model="countryCode" :disabled="loadingCountries">
                  <SelectTrigger class="w-full">
                    <SelectValue placeholder="Select a country…" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem
                      v-for="country in countries"
                      :key="country.countryCode"
                      :value="country.countryCode"
                    >
                      {{ country.name }} ({{ country.countryCode }})
                    </SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <Button :disabled="!countryCode || savingCountry" @click="saveCountry">
                <Loader2Icon v-if="savingCountry" class="size-4 animate-spin" />
                Save
              </Button>
            </div>
          </div>
        </section>

        <!-- Holidays list -->
        <section class="mb-8">
          <div class="flex items-center justify-between mb-3">
            <h2
              class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500"
            >
              Holidays
            </h2>
            <div class="flex items-center gap-2">
              <Select v-model.number="selectedYear" @update:model-value="loadHolidays">
                <SelectTrigger class="h-8 w-28 text-sm">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem v-for="y in yearOptions" :key="y" :value="y">{{ y }}</SelectItem>
                </SelectContent>
              </Select>
              <Button
                variant="outline"
                size="sm"
                :disabled="!countryCode || refreshing"
                @click="refresh"
              >
                <Loader2Icon v-if="refreshing" class="size-3.5 animate-spin" />
                <RefreshCwIcon v-else class="size-3.5" />
                Refresh from API
              </Button>
            </div>
          </div>

          <!-- Add custom holiday -->
          <div class="card p-4 mb-3">
            <p class="text-xs font-medium text-slate-500 dark:text-slate-400 mb-3">
              Add custom holiday
            </p>
            <div class="flex items-end gap-2">
              <div class="space-y-1">
                <Label class="text-xs">Date</Label>
                <Input v-model="newDate" type="date" class="h-8 w-36 cursor-pointer text-sm" />
              </div>
              <div class="flex-1 space-y-1">
                <Label class="text-xs">Name</Label>
                <Input
                  v-model="newName"
                  type="text"
                  placeholder="e.g. Company day off"
                  class="h-8 text-sm"
                  maxlength="100"
                />
              </div>
              <Button
                size="sm"
                :disabled="!newDate || !newName.trim() || addingCustom"
                @click="addCustom"
              >
                <Loader2Icon v-if="addingCustom" class="size-3.5 animate-spin" />
                <PlusIcon v-else class="size-3.5" />
                Add
              </Button>
            </div>
          </div>

          <!-- Holidays table -->
          <div class="card overflow-hidden">
            <div v-if="loadingHolidays" class="divide-y divide-slate-100 dark:divide-slate-800">
              <div v-for="i in 6" :key="i" class="flex items-center gap-4 px-4 py-3.5">
                <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-28 animate-pulse" />
                <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded flex-1 animate-pulse" />
              </div>
            </div>

            <Table v-else>
              <TableHeader>
                <TableRow>
                  <TableHead>Date</TableHead>
                  <TableHead>Name</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Day off</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                <TableEmpty v-if="holidays.length === 0" :colspan="5">
                  <CalendarIcon class="size-8 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
                  <p class="text-slate-500 dark:text-slate-400">
                    {{
                      countryCode
                        ? "No holidays found. Try refreshing from the API."
                        : "Set a country above to load public holidays."
                    }}
                  </p>
                </TableEmpty>
                <TableRow v-for="holiday in holidays" :key="holiday.id">
                  <TableCell class="font-medium text-slate-900 dark:text-slate-100">
                    {{ formatDate(holiday.date) }}
                  </TableCell>
                  <TableCell class="text-slate-600 dark:text-slate-400">
                    {{ holiday.name }}
                  </TableCell>
                  <TableCell>
                    <span
                      :class="[
                        'inline-flex items-center px-2 py-0.5 rounded text-xs font-medium',
                        holiday.isCustom
                          ? 'bg-violet-50 dark:bg-violet-950 text-violet-700 dark:text-violet-300'
                          : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400',
                      ]"
                    >
                      {{ holiday.isCustom ? "Custom" : "Official" }}
                    </span>
                  </TableCell>
                  <TableCell>
                    <Switch
                      :model-value="!holiday.isWorkingDay"
                      :disabled="togglingId === holiday.id"
                      @update:model-value="() => toggleWorkingDay(holiday)"
                    />
                  </TableCell>
                  <TableCell class="text-right">
                    <Button
                      variant="ghost"
                      size="icon"
                      class="size-7 text-slate-400 hover:text-red-500 dark:hover:text-red-400"
                      title="Remove"
                      @click="deleteHoliday(holiday)"
                    >
                      <Trash2Icon class="size-3.5" />
                    </Button>
                  </TableCell>
                </TableRow>
              </TableBody>
            </Table>
          </div>
        </section>

        <!-- Working hours targets -->
        <section class="mb-8">
          <h2
            class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500 mb-3"
          >
            Working Hours Targets
          </h2>

          <div class="card p-5">
            <div class="flex items-start gap-3 mb-5">
              <ClockIcon class="size-5 text-indigo-500 mt-0.5 shrink-0" />
              <div>
                <p class="text-sm font-medium text-slate-900 dark:text-slate-100">
                  Default targets
                </p>
                <p class="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
                  These apply to all employees unless overridden individually in their profile.
                </p>
              </div>
            </div>

            <div class="flex items-end gap-3">
              <div class="space-y-1.5">
                <Label>Daily target (hours)</Label>
                <Input
                  v-model="defaultDailyHours"
                  type="number"
                  min="0"
                  max="24"
                  step="0.5"
                  placeholder="e.g. 8"
                  class="w-32"
                />
              </div>
              <div class="space-y-1.5">
                <Label>Weekly target (hours)</Label>
                <Input
                  v-model="defaultWeeklyHours"
                  type="number"
                  min="0"
                  max="168"
                  step="0.5"
                  placeholder="e.g. 40"
                  class="w-32"
                />
              </div>
              <Button :disabled="savingTargets" @click="saveTargets">
                <Loader2Icon v-if="savingTargets" class="size-4 animate-spin" />
                Save
              </Button>
            </div>

            <div class="border-t border-slate-100 dark:border-slate-800 pt-5">
              <p class="text-sm font-medium text-slate-900 dark:text-slate-100 mb-1">Minimum break duration</p>
              <p class="text-xs text-slate-500 dark:text-slate-400 mb-3">
                Employees must wait this many minutes before they can end their break. Leave blank to disable. Does not apply on half-day vacation days. Can be overridden per employee.
              </p>
              <div class="flex items-end gap-3">
                <div class="space-y-1.5">
                  <Label>Minimum break (minutes)</Label>
                  <Input
                    v-model="minimumBreakMinutes"
                    type="number"
                    min="1"
                    max="120"
                    step="1"
                    placeholder="e.g. 30"
                    class="w-32"
                  />
                </div>
                <Button :disabled="savingMinBreak" @click="saveMinBreak">
                  <Loader2Icon v-if="savingMinBreak" class="size-4 animate-spin" />
                  Save
                </Button>
              </div>
            </div>
          </div>
        </section>

        <!-- Notification email -->
        <section class="mb-8">
          <h2
            class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500 mb-3"
          >
            Notifications
          </h2>

          <div class="card p-5 space-y-5">
            <div class="flex items-start gap-3">
              <MailIcon class="size-5 text-indigo-500 mt-0.5 shrink-0" />
              <div>
                <p class="text-sm font-medium text-slate-900 dark:text-slate-100">
                  Adjustment request notification email
                </p>
                <p class="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
                  When an employee submits a time adjustment request, the approval email is sent to
                  this address. Leave blank to disable email notifications.
                </p>
              </div>
            </div>

            <div class="flex items-end gap-3">
              <div class="flex-1 space-y-1.5">
                <Label>Email address</Label>
                <Input
                  v-model="notificationEmail"
                  type="email"
                  placeholder="admin@company.com"
                />
              </div>
              <Button :disabled="savingEmail" @click="saveNotificationEmail">
                <Loader2Icon v-if="savingEmail" class="size-4 animate-spin" />
                Save
              </Button>
            </div>

            <div class="border-t border-slate-100 dark:border-slate-800 pt-5 space-y-4">
              <p class="text-xs font-medium text-slate-500 dark:text-slate-400">Email types</p>

              <div class="flex items-center justify-between">
                <div>
                  <p class="text-sm font-medium text-slate-900 dark:text-slate-100">Adjustment request emails</p>
                  <p class="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
                    Send an approval email when an employee submits a time adjustment request.
                  </p>
                </div>
                <Switch v-model="enableAdjustmentRequestEmails" />
              </div>

              <div class="flex items-center justify-between">
                <div>
                  <p class="text-sm font-medium text-slate-900 dark:text-slate-100">Missed clock-in reminders</p>
                  <p class="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
                    Send a daily reminder to employees who forgot to clock in the previous working day.
                  </p>
                </div>
                <Switch v-model="enableMissedClockInEmails" />
              </div>

              <div class="flex justify-end">
                <Button :disabled="savingToggles" @click="saveNotificationToggles">
                  <Loader2Icon v-if="savingToggles" class="size-4 animate-spin" />
                  Save
                </Button>
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  </AuthenticatedLayout>
</template>
