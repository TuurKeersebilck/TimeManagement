<script setup lang="ts">
import { ref, onMounted, computed } from "vue";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import { holidayService, type PublicHoliday, type AvailableCountry } from "@/services/holidayService";
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
import {
  GlobeIcon,
  RefreshCwIcon,
  PlusIcon,
  Trash2Icon,
  Loader2Icon,
  CalendarIcon,
} from "lucide-vue-next";

const toast = useAppToast();
const { confirm } = useConfirmDialog();

const currentYear = new Date().getFullYear();
const selectedYear = ref(currentYear);

const countryCode = ref<string>("");
const countries = ref<AvailableCountry[]>([]);
const holidays = ref<PublicHoliday[]>([]);

const loadingCountries = ref(false);
const loadingHolidays = ref(false);
const savingCountry = ref(false);
const refreshing = ref(false);

// Custom holiday form
const newDate = ref("");
const newName = ref("");
const addingCustom = ref(false);

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
          <h2 class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500 mb-3">
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
                  next. Holidays are skipped when employees plan vacation ranges.
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
        <section>
          <div class="flex items-center justify-between mb-3">
            <h2 class="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">
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
                <Input v-model="newDate" type="date" class="h-8 w-36 text-sm" />
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
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                <TableEmpty v-if="holidays.length === 0" :colspan="4">
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
      </div>
    </div>
  </AuthenticatedLayout>
</template>
