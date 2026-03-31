<script setup lang="ts">
import { ref, computed, watch, onMounted } from "vue";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Button } from "@/components/ui/button";
import { useAppToast } from "@/composables/useAppToast";
import { adminService, type AdminVacationDay, type Employee } from "../../services/adminService";
import { vacationTypeService, type VacationType } from "../../services/vacationTypeService";
import { ChevronLeftIcon, ChevronRightIcon, XIcon } from "lucide-vue-next";

const toast = useAppToast();

const vacationDays = ref<AdminVacationDay[]>([]);
const employees = ref<Employee[]>([]);
const vacationTypes = ref<VacationType[]>([]);
const loading = ref(false);

// ─── Filters ──────────────────────────────────────────────────────────────────

const filterEmployee = ref<string>("");
const filterType = ref<string>("");

const filteredDays = computed(() => {
	let days = vacationDays.value;
	if (filterEmployee.value) days = days.filter((d) => d.userId === filterEmployee.value);
	if (filterType.value) days = days.filter((d) => d.vacationTypeId === parseInt(filterType.value));
	return days;
});

const clearFilters = () => {
	filterEmployee.value = "";
	filterType.value = "";
	selectedIso.value = null;
};

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

const goToday = () => { currentMonth.value = new Date(); };

interface CalDay {
	iso: string;
	day: number;
	isCurrentMonth: boolean;
	isToday: boolean;
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
		days.push({ iso: toIso(d), day: d.getDate(), isCurrentMonth: false, isToday: false });
	}
	for (let n = 1; n <= lastDay.getDate(); n++) {
		const d = new Date(year, month, n);
		const iso = toIso(d);
		days.push({ iso, day: n, isCurrentMonth: true, isToday: iso === todayIso });
	}
	const remaining = 42 - days.length;
	for (let n = 1; n <= remaining; n++) {
		const d = new Date(year, month + 1, n);
		days.push({ iso: toIso(d), day: d.getDate(), isCurrentMonth: false, isToday: false });
	}

	return days;
});

const vacationsByDate = computed(() => {
	const map = new Map<string, AdminVacationDay[]>();
	for (const d of filteredDays.value) {
		if (!map.has(d.date)) map.set(d.date, []);
		map.get(d.date)!.push(d);
	}
	return map;
});

// ─── Selected day panel ───────────────────────────────────────────────────────

const selectedIso = ref<string | null>(null);

const selectedEntries = computed(() =>
	selectedIso.value ? (vacationsByDate.value.get(selectedIso.value) ?? []) : []
);

const selectDay = (iso: string) => {
	if (!vacationsByDate.value.has(iso)) { selectedIso.value = null; return; }
	selectedIso.value = selectedIso.value === iso ? null : iso;
};

const selectedLabel = computed(() => {
	if (!selectedIso.value) return "";
	return new Date(selectedIso.value).toLocaleDateString(undefined, {
		weekday: "long", day: "numeric", month: "long", year: "numeric",
	});
});

// ─── Data loading ─────────────────────────────────────────────────────────────

const fetchVacationDays = async () => {
	loading.value = true;
	try {
		vacationDays.value = await adminService.getAllVacationDays({
			year: currentMonth.value.getFullYear(),
			month: currentMonth.value.getMonth() + 1,
		});
	} catch {
		toast.error("Failed to load vacation data");
	} finally {
		loading.value = false;
	}
};

watch(currentMonth, fetchVacationDays);

// ─── Mount ────────────────────────────────────────────────────────────────────

onMounted(async () => {
	loading.value = true;
	try {
		const [days, emps, types] = await Promise.all([
			adminService.getAllVacationDays({
				year: currentMonth.value.getFullYear(),
				month: currentMonth.value.getMonth() + 1,
			}),
			adminService.getEmployees(),
			vacationTypeService.getAll(),
		]);
		vacationDays.value = days;
		employees.value = emps;
		vacationTypes.value = types;
	} catch {
		toast.error("Failed to load vacation data");
	} finally {
		loading.value = false;
	}
});

const WEEK_DAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
const MAX_VISIBLE = 3;
</script>

<template>
	<AuthenticatedLayout>
		<div class="p-6 lg:p-8">
			<div class="max-w-5xl mx-auto">

				<!-- Header -->
				<div class="mb-8">
					<h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">Vacation Overview</h1>
					<p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">Team vacation calendar</p>
				</div>

				<!-- Filters -->
				<div class="flex flex-wrap items-center gap-3 mb-6">
					<Select v-model="filterEmployee">
						<SelectTrigger class="w-48">
							<SelectValue placeholder="All employees" />
						</SelectTrigger>
						<SelectContent>
							<SelectItem value="">All employees</SelectItem>
							<SelectItem v-for="emp in employees" :key="emp.id" :value="emp.id">
								{{ emp.fullName }}
							</SelectItem>
						</SelectContent>
					</Select>

					<Select v-model="filterType">
						<SelectTrigger class="w-44">
							<SelectValue placeholder="All types" />
						</SelectTrigger>
						<SelectContent>
							<SelectItem value="">All types</SelectItem>
							<SelectItem v-for="type in vacationTypes" :key="type.id" :value="String(type.id)">
								{{ type.name }}
							</SelectItem>
						</SelectContent>
					</Select>

					<Button v-if="filterEmployee || filterType" variant="ghost" size="sm" @click="clearFilters">
						<XIcon class="size-3.5" />
						Clear
					</Button>
				</div>

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
					<Button variant="outline" size="sm" @click="goToday">Today</Button>
				</div>

				<!-- Calendar -->
				<div class="card overflow-hidden">
					<!-- Weekday headers -->
					<div class="grid grid-cols-7 border-b border-slate-200 dark:border-slate-800">
						<div
							v-for="wd in WEEK_DAYS"
							:key="wd"
							class="text-center text-xs font-semibold text-slate-400 dark:text-slate-500 py-2"
						>{{ wd }}</div>
					</div>

					<!-- Day cells -->
					<div class="grid grid-cols-7">
						<div
							v-for="cell in calendarDays"
							:key="cell.iso"
							:class="[
								'border-b border-r border-slate-100 dark:border-slate-800/60 min-h-24 p-1.5 transition-colors',
								!cell.isCurrentMonth && 'bg-slate-50/60 dark:bg-slate-900/40',
								cell.isToday && 'bg-indigo-50/60 dark:bg-indigo-950/20',
								selectedIso === cell.iso ? 'ring-2 ring-inset ring-indigo-400 dark:ring-indigo-500' : '',
								vacationsByDate.has(cell.iso) ? 'cursor-pointer hover:bg-slate-50 dark:hover:bg-slate-800/40' : 'cursor-default',
							]"
							@click="selectDay(cell.iso)"
						>
							<!-- Day number -->
							<div
								:class="[
									'text-xs font-medium w-6 h-6 flex items-center justify-center rounded-full mb-1',
									cell.isToday
										? 'bg-indigo-600 text-white'
										: cell.isCurrentMonth
											? 'text-slate-700 dark:text-slate-200'
											: 'text-slate-300 dark:text-slate-600',
								]"
							>{{ cell.day }}</div>

							<!-- Vacation chips -->
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
									{{ entry.employeeName.split(" ")[0] }}<span v-if="entry.amount === 0.5" class="opacity-50"> ½</span>
								</div>
								<div
									v-if="vacationsByDate.get(cell.iso)!.length > MAX_VISIBLE"
									class="text-[10px] text-slate-400 dark:text-slate-500 pl-1"
								>+{{ vacationsByDate.get(cell.iso)!.length - MAX_VISIBLE }} more</div>
							</template>
						</div>
					</div>
				</div>

				<!-- Day detail panel -->
				<Transition
					enter-active-class="transition-all duration-200 ease-out"
					leave-active-class="transition-all duration-150 ease-in"
					enter-from-class="opacity-0 -translate-y-1"
					enter-to-class="opacity-100 translate-y-0"
					leave-from-class="opacity-100"
					leave-to-class="opacity-0"
				>
					<div v-if="selectedIso && selectedEntries.length" class="mt-4 card p-4">
						<div class="flex items-center justify-between mb-3">
							<h3 class="text-sm font-semibold text-slate-700 dark:text-slate-300 capitalize">{{ selectedLabel }}</h3>
							<Button variant="ghost" size="icon" class="size-7" @click="selectedIso = null">
								<XIcon class="size-3.5" />
							</Button>
						</div>
						<div class="divide-y divide-slate-100 dark:divide-slate-800">
							<div v-for="entry in selectedEntries" :key="entry.id" class="flex items-center gap-3 py-2.5">
								<div
									class="w-2.5 h-2.5 rounded-full shrink-0 ring-1 ring-black/10"
									:style="{ backgroundColor: entry.vacationTypeColor ?? '#6366f1' }"
								/>
								<span class="flex-1 text-sm font-medium text-slate-900 dark:text-slate-100">{{ entry.employeeName }}</span>
								<span class="text-xs text-slate-500 dark:text-slate-400">{{ entry.vacationTypeName }}</span>
								<span
									:class="[
										'text-xs font-medium px-1.5 py-0.5 rounded shrink-0',
										entry.amount === 1
											? 'bg-indigo-50 dark:bg-indigo-950 text-indigo-700 dark:text-indigo-300'
											: 'bg-sky-50 dark:bg-sky-950 text-sky-700 dark:text-sky-300',
									]"
								>{{ entry.amount === 1 ? "Full day" : "Half day" }}</span>
								<span v-if="entry.note" class="text-xs text-slate-400 dark:text-slate-500 italic truncate max-w-[140px]">
									{{ entry.note }}
								</span>
							</div>
						</div>
					</div>
				</Transition>

			</div>
		</div>
	</AuthenticatedLayout>
</template>
