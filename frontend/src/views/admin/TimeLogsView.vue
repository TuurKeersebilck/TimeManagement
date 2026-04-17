<script setup lang="ts">
import { ref, computed, watch, onMounted } from "vue";
import { useRoute } from "vue-router";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import { adminService, type AdminTimeLog, type Employee } from "../../services/adminService";
import { useAppToast } from "@/composables/useAppToast";
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
import {
  Table,
  TableBody,
  TableCell,
  TableEmpty,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { ClockIcon, ChevronLeftIcon, ChevronRightIcon, CoffeeIcon, MessageSquareTextIcon } from "lucide-vue-next";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from "@/components/ui/dialog";

const descriptionDialog = ref<{ open: boolean; text: string; employee: string; date: string }>({
  open: false,
  text: "",
  employee: "",
  date: "",
});

function openDescription(log: AdminTimeLog) {
  descriptionDialog.value = {
    open: true,
    text: log.description!,
    employee: log.employeeName,
    date: formatDate(log.date),
  };
}

const descOverflow = ref<Record<string, boolean>>({});

function checkDescOverflow(el: Element | null, key: string) {
  if (el) descOverflow.value[key] = el.scrollWidth > el.clientWidth;
}

const toast = useAppToast();
const route = useRoute();

const allLogs = ref<AdminTimeLog[]>([]);
const employees = ref<Employee[]>([]);
const loading = ref(false);

const selectedEmployeeId = ref<string>("all");
const dateFrom = ref<string>("");
const dateTo = ref<string>("");

// ─── Pagination ───────────────────────────────────────────────────────────────

const pageSize = 15;
const currentPage = ref(1);

const totalPages = computed(() => Math.max(1, Math.ceil(allLogs.value.length / pageSize)));
const paginatedLogs = computed(() => {
  const start = (currentPage.value - 1) * pageSize;
  return allLogs.value.slice(start, start + pageSize);
});

// ─── Filters ────────────────────────────────────────────────────────────────

const totalHoursFiltered = computed(() =>
  allLogs.value.reduce((sum, l) => sum + (l.totalHours ?? 0), 0).toFixed(2)
);

const fetchLogs = async () => {
  loading.value = true;
  currentPage.value = 1;
  try {
    allLogs.value = await adminService.getAllTimeLogs({
      userId: selectedEmployeeId.value === "all" ? undefined : selectedEmployeeId.value,
      dateFrom: dateFrom.value || undefined,
      dateTo: dateTo.value || undefined,
    });
  } catch {
    toast.error("Failed to load time logs");
  } finally {
    loading.value = false;
  }
};

watch([selectedEmployeeId, dateFrom, dateTo], fetchLogs);

// ─── Helpers ─────────────────────────────────────────────────────────────────

const formatDate = (dateStr: string) =>
  new Date(dateStr).toLocaleDateString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
  });

const formatTime = (t?: string) => {
  if (!t) return "—";
  const d = new Date(t);
  return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
};


const clearFilters = () => {
  selectedEmployeeId.value = "all";
  dateFrom.value = "";
  dateTo.value = "";
};

const hasFilters = computed(
  () => selectedEmployeeId.value !== "all" || dateFrom.value || dateTo.value
);

// ─── Mount ───────────────────────────────────────────────────────────────────

onMounted(async () => {
  loading.value = true;
  try {
    [allLogs.value, employees.value] = await Promise.all([
      adminService.getAllTimeLogs(),
      adminService.getEmployees(),
    ]);
    const preselect = route.query.employeeId as string | undefined;
    if (preselect) {
      selectedEmployeeId.value = preselect;
    }
  } catch {
    toast.error("Failed to load data");
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <AuthenticatedLayout>
    <div class="p-6 lg:p-8">
      <div class="max-w-6xl mx-auto">
        <!-- Header -->
        <div class="mb-8">
          <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">All Time Logs</h1>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
            Overview of all employee time logs
          </p>
        </div>

        <!-- Stats -->
        <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-8">
          <div class="stat-card">
            <p
              class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1"
            >
              Employees
            </p>
            <p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
              <span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600"
                >--</span
              >
              <span v-else>{{ employees.length }}</span>
            </p>
          </div>
          <div class="stat-card">
            <p
              class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1"
            >
              Entries shown
            </p>
            <p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
              <span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600"
                >--</span
              >
              <span v-else>{{ allLogs.length }}</span>
            </p>
          </div>
          <div class="stat-card">
            <p
              class="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1"
            >
              Total hours shown
            </p>
            <p class="text-3xl font-bold text-slate-900 dark:text-slate-100">
              <span v-if="loading" class="animate-pulse text-slate-300 dark:text-slate-600"
                >--</span
              >
              <span v-else>{{ totalHoursFiltered }}h</span>
            </p>
          </div>
        </div>

        <!-- Filters -->
        <div class="card p-4 mb-4 flex flex-wrap items-end gap-3">
          <div class="flex-1 min-w-[180px] space-y-1.5">
            <Label>Employee</Label>
            <Select v-model="selectedEmployeeId">
              <SelectTrigger class="w-full">
                <SelectValue placeholder="All employees" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All employees</SelectItem>
                <SelectItem v-for="emp in employees" :key="emp.id" :value="emp.id">
                  {{ emp.fullName }}
                </SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div class="flex-1 min-w-[150px] space-y-1.5">
            <Label>From</Label>
            <Input v-model="dateFrom" type="date" />
          </div>
          <div class="flex-1 min-w-[150px] space-y-1.5">
            <Label>To</Label>
            <Input v-model="dateTo" type="date" />
          </div>
          <Button v-if="hasFilters" variant="outline" @click="clearFilters" class="shrink-0">
            Clear filters
          </Button>
        </div>

        <!-- Table -->
        <div class="card overflow-hidden">
          <!-- Loading skeleton -->
          <div v-if="loading" class="divide-y divide-slate-100 dark:divide-slate-800">
            <div v-for="i in 6" :key="i" class="flex items-center gap-4 px-4 py-3.5">
              <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-28 animate-pulse" />
              <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-20 animate-pulse" />
              <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded w-24 animate-pulse" />
              <div class="ml-auto h-5 bg-slate-200 dark:bg-slate-700 rounded w-12 animate-pulse" />
            </div>
          </div>

          <Table v-else>
            <TableHeader>
              <TableRow>
                <TableHead>Employee</TableHead>
                <TableHead>Date</TableHead>
                <TableHead>Timeline</TableHead>
                <TableHead>Total</TableHead>
                <TableHead>WFH</TableHead>
                <TableHead>Description</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              <TableEmpty v-if="allLogs.length === 0" :colspan="6">
                <ClockIcon class="size-8 text-slate-300 dark:text-slate-600 mb-2 mx-auto" />
                <p class="text-slate-500 dark:text-slate-400">No time logs found.</p>
              </TableEmpty>
              <TableRow v-for="log in paginatedLogs" :key="`${log.userId}-${log.date}`">
                <TableCell>
                  <p class="font-medium text-slate-900 dark:text-slate-100 text-sm">
                    {{ log.employeeName }}
                  </p>
                  <p class="text-xs text-slate-400 dark:text-slate-500">{{ log.employeeEmail }}</p>
                </TableCell>
                <TableCell class="font-medium text-slate-900 dark:text-slate-100">
                  {{ formatDate(log.date) }}
                </TableCell>
                <TableCell class="font-mono text-xs text-slate-600 dark:text-slate-400 whitespace-nowrap">
                  <template v-if="log.breakStart && log.breakEnd">
                    {{ formatTime(log.clockIn) }} → {{ formatTime(log.breakStart) }}
                    <CoffeeIcon class="inline size-3 mx-1 text-amber-400" />
                    {{ formatTime(log.breakEnd) }} → {{ formatTime(log.clockOut) }}
                  </template>
                  <template v-else>
                    {{ formatTime(log.clockIn) }} → {{ formatTime(log.clockOut) }}
                  </template>
                </TableCell>
                <TableCell>
                  <span
                    class="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-semibold bg-indigo-50 dark:bg-indigo-950 text-indigo-700 dark:text-indigo-300"
                  >
                    {{ log.totalHours?.toFixed(2) ?? "0.00" }}h
                  </span>
                </TableCell>
                <TableCell>
                  <span v-if="log.workedFromHome" class="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium bg-emerald-50 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-300">
                    WFH
                  </span>
                  <span v-else class="text-slate-400 text-xs">—</span>
                </TableCell>
                <TableCell class="text-slate-600 dark:text-slate-400 text-sm max-w-[180px]">
                  <div v-if="log.description" class="flex items-center gap-1.5 min-w-0">
                    <span
                      class="truncate"
                      :ref="(el) => checkDescOverflow(el as Element | null, `${log.userId}-${log.date}`)"
                    >{{ log.description }}</span>
                    <button
                      v-if="descOverflow[`${log.userId}-${log.date}`]"
                      class="shrink-0 cursor-pointer text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 transition-colors"
                      title="View full description"
                      @click="openDescription(log)"
                    >
                      <MessageSquareTextIcon class="size-3.5" />
                    </button>
                  </div>
                  <span v-else class="text-slate-400">—</span>
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
                Math.min(currentPage * pageSize, allLogs.length)
              }}
              of {{ allLogs.length }}
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
    <!-- Description dialog -->
    <Dialog v-model:open="descriptionDialog.open">
      <DialogContent class="max-w-md" @open-auto-focus.prevent>
        <DialogHeader>
          <DialogTitle>Description</DialogTitle>
          <DialogDescription>
            {{ descriptionDialog.employee }} &mdash; {{ descriptionDialog.date }}
          </DialogDescription>
        </DialogHeader>
        <p class="text-sm text-slate-700 dark:text-slate-300 leading-relaxed whitespace-pre-wrap">
          {{ descriptionDialog.text }}
        </p>
      </DialogContent>
    </Dialog>
  </AuthenticatedLayout>
</template>
